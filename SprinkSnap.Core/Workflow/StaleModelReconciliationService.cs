using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public sealed class StaleModelReconciliationStep
{
    public SprinkSnapWorkflowStep WorkflowStep { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public bool IsCurrent { get; set; }

    public string StatusText => IsComplete ? "Complete" : IsCurrent ? "Next step" : "Pending";
}

public static class StaleModelReconciliationService
{
    public static bool IsReconciliationActive(SprinkSnapProjectState state)
    {
        if (state == null)
        {
            return false;
        }

        return SprinkSnapWorkflowGate.IsModelStale(state)
            || state.SessionProgress.ReconciliationRequired;
    }

    public static string GetBannerTitle(SprinkSnapProjectState state)
    {
        if (SprinkSnapWorkflowGate.IsModelStale(state))
        {
            return "Revit model changed since last SprinkSnap save";
        }

        if (state?.SessionProgress.ReconciliationRequired == true)
        {
            return "Model reconciliation required";
        }

        return string.Empty;
    }

    public static string GetBannerMessage(SprinkSnapProjectState state)
    {
        if (state == null)
        {
            return string.Empty;
        }

        if (SprinkSnapWorkflowGate.IsModelStale(state))
        {
            if (state.ModelChangeAssessment?.Messages.Count > 0)
            {
                return string.Join(" ", state.ModelChangeAssessment.Messages)
                    + " Re-run Analyze Model, then work through the checklist below.";
            }

            return SprinkSnapWorkflowGate.StaleModelBlockReason;
        }

        if (state.SessionProgress.ReconciliationRequired)
        {
            return "The Revit model was re-analyzed and downstream results were cleared. "
                + "Review changed rooms and repeat design, clash, placement, hydraulics, and materials steps.";
        }

        return string.Empty;
    }

    public static IList<StaleModelReconciliationStep> BuildSteps(SprinkSnapProjectState state)
    {
        List<StaleModelReconciliationStep> steps = new List<StaleModelReconciliationStep>();
        if (state == null)
        {
            return steps;
        }

        bool analyzeComplete = !SprinkSnapWorkflowGate.IsModelStale(state)
            && SprinkSnapWorkflowGate.IsAnalyzeComplete(state);
        bool hazardComplete = IsChangedRoomHazardReviewComplete(state);
        bool sprinklerComplete = SprinkSnapWorkflowGate.IsSprinklerReviewComplete(state);
        bool designComplete = SprinkSnapWorkflowGate.IsDesignGenerated(state);
        bool clashComplete = SprinkSnapWorkflowGate.IsClashDetectionComplete(state);
        bool placementComplete = SprinkSnapWorkflowGate.IsSprinklersPlacedInRevit(state);
        bool hydraulicsComplete = state.SessionProgress.HydraulicsComplete;
        bool materialsComplete = state.SessionProgress.MaterialsComplete;

        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.AnalyzeModel,
            "Re-analyze model",
            "Extract current rooms, ceilings, and geometry from Revit.",
            analyzeComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.HazardReview,
            "Review changed rooms",
            GetChangedRoomDescription(state),
            hazardComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.SprinklerReview,
            "Confirm sprinkler selections",
            "Verify manufacturer mappings and room head selections after model changes.",
            sprinklerComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.GenerateDesign,
            "Regenerate design",
            "Rebuild sprinkler layout and schematic routing from approved rooms.",
            designComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.ClashDetection,
            "Re-run clash detection",
            "Scan updated layout against ducts, structure, and obstructions.",
            clashComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.PlaceSprinklers,
            "Refresh Revit placement",
            "Re-place sprinklers and pipes, or refresh measured pipe lengths from Revit.",
            placementComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.Hydraulics,
            "Re-run hydraulics",
            "Recalculate demand using updated layout and placed pipe lengths.",
            hydraulicsComplete));
        steps.Add(CreateStep(
            SprinkSnapWorkflowStep.Materials,
            "Refresh material takeoff",
            "Regenerate BOM from current design and placed geometry.",
            materialsComplete));

        StaleModelReconciliationStep firstIncomplete = steps.FirstOrDefault(step => !step.IsComplete);
        if (firstIncomplete != null)
        {
            firstIncomplete.IsCurrent = true;
        }

        return steps;
    }

    public static void UpdateReconciliationState(SprinkSnapProjectState state)
    {
        if (state == null || !state.SessionProgress.ReconciliationRequired)
        {
            return;
        }

        if (AreAllStepsComplete(state))
        {
            state.SessionProgress.ReconciliationRequired = false;
            if (state.ModelChangeAssessment != null)
            {
                state.ModelChangeAssessment.ChangedRoomRevitElementIds.Clear();
                state.ModelChangeAssessment.ChangedRoomCount = 0;
            }
        }
    }

    public static bool AreAllStepsComplete(SprinkSnapProjectState state)
    {
        IList<StaleModelReconciliationStep> steps = BuildSteps(state);
        return steps.Count > 0 && steps.All(step => step.IsComplete);
    }

    private static bool IsChangedRoomHazardReviewComplete(SprinkSnapProjectState state)
    {
        if (!SprinkSnapWorkflowGate.IsHazardReviewComplete(state))
        {
            return false;
        }

        HashSet<int> changedRoomIds = state.ModelChangeAssessment?.ChangedRoomRevitElementIds?.ToHashSet()
            ?? new HashSet<int>();
        if (changedRoomIds.Count == 0)
        {
            return true;
        }

        return changedRoomIds.All(roomId =>
        {
            RoomInfo room = state.Rooms.FirstOrDefault(candidate => candidate.RevitElementId == roomId);
            return room != null
                && room.DesignerApproved
                && !string.IsNullOrWhiteSpace(room.ApprovedHazardClassification);
        });
    }

    private static string GetChangedRoomDescription(SprinkSnapProjectState state)
    {
        int changedCount = state.ModelChangeAssessment?.ChangedRoomCount ?? 0;
        if (changedCount > 0)
        {
            return "Re-approve hazard classifications for "
                + changedCount
                + " changed room(s) highlighted in Hazard Review.";
        }

        return "Confirm hazard approvals after the model update.";
    }

    private static StaleModelReconciliationStep CreateStep(
        SprinkSnapWorkflowStep workflowStep,
        string title,
        string description,
        bool isComplete)
    {
        return new StaleModelReconciliationStep
        {
            WorkflowStep = workflowStep,
            Title = title,
            Description = description,
            IsComplete = isComplete
        };
    }
}
