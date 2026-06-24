using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public abstract class SprinkSnapWorkflowCommandBase : IExternalCommand
{
    protected abstract SprinkSnapWorkflowStep WorkflowStep { get; }

    protected abstract string DisplayName { get; }

    public virtual Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            DockablePane pane = commandData.Application.GetDockablePane(SprinkSnapDockablePaneRegistration.PaneId);
            pane.Show();
            TaskDialog.Show("SprinkSnap AI", DisplayName + " workspace opened.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            TaskDialog.Show("SprinkSnap AI", "Unable to open " + DisplayName + "." + Environment.NewLine + ex.Message);
            return Result.Failed;
        }
    }
}

public sealed class AnalyzeModelCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.AnalyzeModel;

    protected override string DisplayName => "Analyze Model";

    public override Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uiDocument = commandData.Application.ActiveUIDocument;
        if (uiDocument == null)
        {
            TaskDialog.Show("SprinkSnap AI", "No active Revit document is available.");
            return Result.Succeeded;
        }

        Document document = uiDocument.Document;
        IHazardClassificationParameterStorage parameterStorage = new HazardClassificationParameterStorage();
        IRoomExtractor roomExtractor = new RoomExtractor(
            new RoomBoundaryExtractor(),
            new RoomAnalyzer(),
            new HazardClassifier(),
            parameterStorage);

        SprinkSnapProjectState state = new SprinkSnapProjectState();
        foreach (RoomInfo room in roomExtractor.ExtractRooms(document))
        {
            state.Rooms.Add(room);
        }

        state.ModelAnalysis.LinkedModelCount = new FilteredElementCollector(document)
            .OfClass(typeof(RevitLinkInstance))
            .GetElementCount();
        state.ModelAnalysis.ExistingSprinklerCount = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Sprinklers)
            .WhereElementIsNotElementType()
            .GetElementCount();

        IModelAnalysisEngine engine = new ModelAnalysisEngine();
        ModelAnalysisSummary summary = engine.Analyze(state);
        summary.LinkedModelCount = state.ModelAnalysis.LinkedModelCount;
        summary.ExistingSprinklerCount = state.ModelAnalysis.ExistingSprinklerCount;

        string jsonPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SprinkSnap_ModelAnalysis.json");
        File.WriteAllText(jsonPath, engine.ExportJson(summary));

        TaskDialog.Show(
            "Model Analysis Summary",
            "✓ "
            + summary.RoomCount
            + " rooms extracted"
            + Environment.NewLine
            + "✓ "
            + summary.SlopedCeilingCount
            + " sloped ceilings"
            + Environment.NewLine
            + "✓ "
            + summary.LinkedModelCount
            + " linked models"
            + Environment.NewLine
            + "⚠ "
            + summary.MissingCeilingCount
            + " rooms missing ceilings"
            + Environment.NewLine
            + "⚠ "
            + summary.ObstructionZoneCount
            + " obstruction zones"
            + Environment.NewLine
            + Environment.NewLine
            + "JSON exported:"
            + Environment.NewLine
            + jsonPath);

        return Result.Succeeded;
    }
}

public sealed class SprinklerReviewCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.SprinklerReview;

    protected override string DisplayName => "Sprinkler Review";
}

public sealed class WaterSupplyCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.WaterSupply;

    protected override string DisplayName => "Water Supply";
}

public sealed class GenerateDesignCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.GenerateDesign;

    protected override string DisplayName => "Generate Design";
}

public sealed class ClashDetectionCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.ClashDetection;

    protected override string DisplayName => "Clash Detection";
}

public sealed class HydraulicsCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.Hydraulics;

    protected override string DisplayName => "Hydraulics";
}

public sealed class MaterialsCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.Materials;

    protected override string DisplayName => "Materials";
}

public sealed class ReportsCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.Reports;

    protected override string DisplayName => "Reports";
}

public sealed class SettingsCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.Settings;

    protected override string DisplayName => "Settings";
}

