using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public sealed class WorkflowModuleAccess
{
    public SprinkSnapWorkflowStep Step { get; set; }

    public bool IsUnlocked { get; set; }

    public bool IsComplete { get; set; }

    public WorkflowStepStatus Status { get; set; }

    public string StatusLabel { get; set; } = string.Empty;

    public string BlockReason { get; set; } = string.Empty;
}

public static class SprinkSnapWorkflowGate
{
    public static WorkflowModuleAccess Evaluate(SprinkSnapProjectState state, SprinkSnapWorkflowStep step)
    {
        bool analyzeComplete = IsAnalyzeComplete(state);
        bool hazardComplete = IsHazardReviewComplete(state);
        bool sprinklerComplete = IsSprinklerReviewComplete(state);
        bool waterSupplyComplete = IsWaterSupplyComplete(state);
        bool designComplete = IsDesignGenerated(state);
        bool clashComplete = IsClashDetectionComplete(state);
        bool hydraulicsComplete = state.SessionProgress.HydraulicsComplete;
        bool materialsComplete = state.SessionProgress.MaterialsComplete;

        switch (step)
        {
            case SprinkSnapWorkflowStep.AnalyzeModel:
                return CreateAccess(
                    step,
                    isUnlocked: true,
                    isComplete: analyzeComplete,
                    blockReason: string.Empty);

            case SprinkSnapWorkflowStep.HazardReview:
                return CreateAccess(
                    step,
                    isUnlocked: analyzeComplete,
                    isComplete: hazardComplete,
                    blockReason: analyzeComplete ? string.Empty : "Run Analyze Model and extract rooms before hazard review.");

            case SprinkSnapWorkflowStep.SprinklerReview:
                return CreateAccess(
                    step,
                    isUnlocked: hazardComplete,
                    isComplete: sprinklerComplete,
                    blockReason: hazardComplete
                        ? string.Empty
                        : "Approve hazard classifications for all rooms before sprinkler review.");

            case SprinkSnapWorkflowStep.WaterSupply:
                return CreateAccess(
                    step,
                    isUnlocked: analyzeComplete,
                    isComplete: waterSupplyComplete,
                    blockReason: analyzeComplete ? string.Empty : "Run Analyze Model before entering water supply data.");

            case SprinkSnapWorkflowStep.GenerateDesign:
                if (!hazardComplete)
                {
                    return CreateAccess(step, false, designComplete, "Approve all room hazards before generating design.");
                }

                if (!sprinklerComplete)
                {
                    return CreateAccess(step, false, designComplete, "Complete sprinkler review before generating design.");
                }

                if (!waterSupplyComplete)
                {
                    return CreateAccess(step, false, designComplete, "Enter and validate water supply before generating design.");
                }

                return CreateAccess(step, true, designComplete, string.Empty);

            case SprinkSnapWorkflowStep.ClashDetection:
                return CreateAccess(
                    step,
                    isUnlocked: designComplete,
                    isComplete: clashComplete,
                    blockReason: designComplete
                        ? string.Empty
                        : "Generate sprinkler design before running clash detection.");

            case SprinkSnapWorkflowStep.Hydraulics:
                return CreateAccess(
                    step,
                    isUnlocked: clashComplete,
                    isComplete: hydraulicsComplete,
                    blockReason: clashComplete
                        ? string.Empty
                        : "Resolve clashes and update layout before running hydraulics.");

            case SprinkSnapWorkflowStep.Materials:
                return CreateAccess(
                    step,
                    isUnlocked: designComplete,
                    isComplete: materialsComplete,
                    blockReason: designComplete ? string.Empty : "Generate sprinkler design before opening material takeoff.");

            case SprinkSnapWorkflowStep.Reports:
                bool reportsUnlocked = hydraulicsComplete || materialsComplete;
                return CreateAccess(
                    step,
                    isUnlocked: reportsUnlocked,
                    isComplete: state.SessionProgress.ReportsExported,
                    blockReason: reportsUnlocked
                        ? string.Empty
                        : "Run hydraulics or refresh material takeoff before exporting reports.");

            case SprinkSnapWorkflowStep.Settings:
                return CreateAccess(step, true, true, string.Empty);

            default:
                return CreateAccess(step, false, false, "Module is not available.");
        }
    }

    public static bool IsAnalyzeComplete(SprinkSnapProjectState state)
    {
        return state.SessionProgress.ModelAnalysisComplete
            || (state.Rooms.Count > 0 && state.ModelAnalysis.RoomCount > 0);
    }

    public static bool IsHazardReviewComplete(SprinkSnapProjectState state)
    {
        if (state.SessionProgress.HazardReviewComplete)
        {
            return true;
        }

        return state.Rooms.Count > 0
            && state.Rooms.All(room =>
                room.DesignerApproved
                && !string.IsNullOrWhiteSpace(room.ApprovedHazardClassification));
    }

    public static bool IsSprinklerReviewComplete(SprinkSnapProjectState state)
    {
        if (state.SessionProgress.SprinklerReviewComplete)
        {
            return true;
        }

        return state.Rooms.Count > 0
            && state.Rooms.All(room => !string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName));
    }

    public static bool IsWaterSupplyComplete(SprinkSnapProjectState state)
    {
        return state.SessionProgress.WaterSupplyComplete
            || (state.WaterSupply.StaticPressurePsi.HasValue
                && state.WaterSupply.ResidualPressurePsi.HasValue
                && state.WaterSupply.FlowAtResidualGpm.HasValue);
    }

    public static bool IsDesignGenerated(SprinkSnapProjectState state)
    {
        return state.SessionProgress.DesignGenerated
            || state.Rooms.Any(room => room.ProposedSprinklers.Count > 0);
    }

    public static bool IsClashDetectionComplete(SprinkSnapProjectState state)
    {
        return state.SessionProgress.ClashDetectionComplete
            || (state.ClashSummary != null && state.ClashSummary.TotalClashes == 0 && IsDesignGenerated(state))
            || (state.ClashSummary != null
                && state.ClashSummary.TotalClashes > 0
                && state.ClashSummary.UnresolvedClashes == 0);
    }

    private static WorkflowModuleAccess CreateAccess(
        SprinkSnapWorkflowStep step,
        bool isUnlocked,
        bool isComplete,
        string blockReason)
    {
        WorkflowStepStatus status;
        string statusLabel;

        if (!isUnlocked)
        {
            status = WorkflowStepStatus.Blocked;
            statusLabel = "Locked";
        }
        else if (isComplete)
        {
            status = WorkflowStepStatus.Complete;
            statusLabel = "Complete";
        }
        else
        {
            status = WorkflowStepStatus.InProgress;
            statusLabel = "Ready";
        }

        return new WorkflowModuleAccess
        {
            Step = step,
            IsUnlocked = isUnlocked,
            IsComplete = isComplete,
            Status = status,
            StatusLabel = statusLabel,
            BlockReason = blockReason
        };
    }
}
