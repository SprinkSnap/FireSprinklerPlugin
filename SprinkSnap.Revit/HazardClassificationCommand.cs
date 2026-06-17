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
            ISprinklerPlacementCandidateGenerator candidateGenerator = new SprinklerPlacementCandidateGenerator();
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
                candidateGenerator.GenerateCandidates);

            ShowMessage(
                "SprinkSnap Hazard Classification Saved",
                BuildSummary(approvedRooms, candidatesByRoom));

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
        IDictionary<int, IReadOnlyList<SprinklerPlacementCandidate>> candidatesByRoom)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Designer-approved room hazard classifications were saved.");
        builder.AppendLine();
        builder.AppendLine("Rooms processed: " + rooms.Count);
        builder.AppendLine("Conceptual sprinkler placement candidate points prepared: "
            + candidatesByRoom.Values.Sum(candidates => candidates.Count));
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
        builder.AppendLine("Sprinkler placement remains disabled until a future NFPA 13 placement engine validates spacing, obstructions, sprinkler type, and hydraulic criteria.");
        return builder.ToString();
    }

    private static void ShowMessage(string title, string message)
    {
        Wpf.MessageBox.Show(message, title, Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Information);
    }
}

