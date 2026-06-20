// NFPA 13 defines occupancy hazard classifications based on quantity/combustibility of contents,
// heat release rates, and stockpile heights. SprinkSnap provides suggestion-only classification
// support; designer approval is mandatory before room data can drive sprinkler placement.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.UI;
using Wpf = System.Windows;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

[Transaction(TransactionMode.Manual)]
public sealed class HazardClassificationCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIApplication uiApplication = commandData.Application;
            UIDocument uiDocument = uiApplication.ActiveUIDocument;
            if (uiDocument == null)
            {
                ShowMessage("SprinkSnap Hazard Classification", "No active Revit document is available.");
                return Result.Succeeded;
            }

            Document document = uiDocument.Document;
            IHazardClassificationParameterStorage parameterStorage = new HazardClassificationParameterStorage();
            IRoomBoundaryExtractor boundaryExtractor = new RoomBoundaryExtractor();
            IRoomAnalyzer roomAnalyzer = new RoomAnalyzer();
            IHazardClassifier hazardClassifier = new HazardClassifier();
            IRoomExtractor roomExtractor = new RoomExtractor(
                boundaryExtractor,
                roomAnalyzer,
                hazardClassifier,
                parameterStorage);

            IReadOnlyList<RoomInfo> rooms = roomExtractor.ExtractRooms(document);
            if (rooms.Count == 0)
            {
                ShowMessage("SprinkSnap Hazard Classification", "No placed rooms with measurable area were found.");
                return Result.Succeeded;
            }

            HazardClassificationViewModel viewModel = new HazardClassificationViewModel(rooms);
            HazardClassificationView view = new HazardClassificationView(viewModel);
            SetRevitOwner(view, uiApplication);

            bool? dialogResult = view.ShowDialog();
            if (dialogResult != true)
            {
                return Result.Succeeded;
            }

            IReadOnlyList<RoomInfo> approvedRooms = viewModel.ApprovedRooms;
            Dictionary<ElementId, string> approvedClassifications = approvedRooms.ToDictionary(
                room => new ElementId(room.RevitElementId),
                room => room.ApprovedHazardClassification);

            using (Transaction transaction = new Transaction(document, "Save SprinkSnap Hazard Classifications"))
            {
                transaction.Start();
                parameterStorage.EnsureRoomParameterBinding(document);
                document.Regenerate();
                parameterStorage.Write(document, approvedClassifications);
                transaction.Commit();
            }

            Dictionary<int, IReadOnlyList<SprinklerPlacementCandidate>> candidatesByRoom = approvedRooms.ToDictionary(
                room => room.RevitElementId,
                room => (IReadOnlyList<SprinklerPlacementCandidate>)room.ProposedSprinklers.ToList());

            ShowMessage(
                "SprinkSnap Hazard Classification Saved",
                BuildSummary(approvedRooms, candidatesByRoom, viewModel.ApprovedWaterDemand));

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            ShowMessage(
                "SprinkSnap Hazard Classification Error",
                "The command could not complete." + Environment.NewLine + Environment.NewLine + ex.Message);
            return Result.Failed;
        }
    }

    private static void SetRevitOwner(Wpf.Window window, UIApplication uiApplication)
    {
        IntPtr mainWindowHandle = uiApplication.MainWindowHandle;
        if (mainWindowHandle == IntPtr.Zero)
        {
            return;
        }

        WindowInteropHelper helper = new WindowInteropHelper(window);
        helper.Owner = mainWindowHandle;
    }

    private static string BuildSummary(
        IReadOnlyList<RoomInfo> rooms,
        IDictionary<int, IReadOnlyList<SprinklerPlacementCandidate>> candidatesByRoom,
        WaterDemandInfo waterDemandInfo)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Designer-approved room hazard classifications were saved.");
        builder.AppendLine();
        builder.AppendLine("Rooms processed: " + rooms.Count);
        builder.AppendLine("AI-assisted sprinkler placement candidate points prepared: "
            + candidatesByRoom.Values.Sum(candidates => candidates.Count));
        builder.AppendLine();
        builder.AppendLine("Water demand information:");
        builder.AppendLine("Static pressure: " + FormatOptionalValue(waterDemandInfo.StaticPressurePsi, " PSI"));
        builder.AppendLine("Residual pressure: " + FormatOptionalValue(waterDemandInfo.ResidualPressurePsi, " PSI"));
        builder.AppendLine("Flow: " + FormatOptionalValue(waterDemandInfo.FlowGpm, " GPM"));
        builder.AppendLine();
        builder.AppendLine("Counts by hazard:");

        foreach (string hazard in HazardClassification.All)
        {
            List<RoomInfo> hazardRooms = rooms
                .Where(room => string.Equals(room.ApprovedHazardClassification, hazard, StringComparison.Ordinal))
                .ToList();

            builder.AppendLine(
                hazard
                + ": "
                + hazardRooms.Count
                + " room(s), "
                + hazardRooms.Sum(room => room.AreaSquareFeet).ToString("N2")
                + " sq ft");
        }

        builder.AppendLine();
        builder.AppendLine("Automatic placement is still preview-only in this command; unresolved exceptions must be reviewed before a future placement command writes sprinklers to Revit.");
        return builder.ToString();
    }

    private static string FormatOptionalValue(double? value, string unit)
    {
        return value.HasValue ? value.Value.ToString("N1") + unit : "Not provided";
    }

    private static void ShowMessage(string title, string message)
    {
        Wpf.MessageBox.Show(message, title, Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information);
    }
}

