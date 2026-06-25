using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Revit.Session;

namespace FireSprinklerPlugin.SprinkSnap.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public abstract class SprinkSnapWorkflowCommandBase : IExternalCommand
{
    protected abstract SprinkSnapWorkflowStep WorkflowStep { get; }

    protected abstract string DisplayName { get; }

    protected virtual bool RefreshRoomsOnOpen => false;

    protected virtual bool RunModelAnalysisOnOpen => false;

    public virtual Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIApplication uiApplication = commandData.Application;
            if (uiApplication.ActiveUIDocument == null)
            {
                TaskDialog.Show("SprinkSnap AI", "No active Revit document is available.");
                return Result.Succeeded;
            }

            SprinkSnapRevitSession session = SprinkSnapRevitSession.Activate(uiApplication);
            if (session == null)
            {
                TaskDialog.Show("SprinkSnap AI", "Unable to activate SprinkSnap for the current document.");
                return Result.Failed;
            }

            session.EnsureLoaded(refreshRooms: RefreshRoomsOnOpen, runModelAnalysis: RunModelAnalysisOnOpen);
            session.ShowWorkflowStep(uiApplication, WorkflowStep, BuildOpenFeedback(session));
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            TaskDialog.Show("SprinkSnap AI", "Unable to open " + DisplayName + "." + Environment.NewLine + ex.Message);
            return Result.Failed;
        }
    }

    protected virtual string BuildOpenFeedback(SprinkSnapRevitSession session)
    {
        int roomCount = session.Context.ProjectState.Rooms.Count;
        if (roomCount == 0)
        {
            return DisplayName + " opened. No rooms were found — run Analyze Model to extract rooms from Revit.";
        }

        return DisplayName + " opened for " + roomCount + " room(s) from " + session.Context.DocumentTitle + ".";
    }
}

public sealed class AnalyzeModelCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.AnalyzeModel;

    protected override string DisplayName => "Analyze Model";

    protected override bool RefreshRoomsOnOpen => true;

    protected override bool RunModelAnalysisOnOpen => true;

    protected override string BuildOpenFeedback(SprinkSnapRevitSession session)
    {
        ModelAnalysisSummary summary = session.Context.ProjectState.ModelAnalysis;
        return "Analyze Model complete: "
            + summary.RoomCount
            + " rooms, "
            + summary.SlopedCeilingCount
            + " sloped ceilings, "
            + summary.MissingCeilingCount
            + " missing ceilings, "
            + summary.ObstructionZoneCount
            + " obstruction zones.";
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

public sealed class PlaceSprinklersCommand : SprinkSnapWorkflowCommandBase
{
    protected override SprinkSnapWorkflowStep WorkflowStep => SprinkSnapWorkflowStep.PlaceSprinklers;

    protected override string DisplayName => "Place Sprinklers";
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
