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
    public const string StaleModelBlockReason =
        "Revit model changed since the last SprinkSnap save. Re-run Analyze Model and review hazard, layout, and clash results before continuing.";

    public static bool RequiresSessionBackedProgress(SprinkSnapProjectState state)
    {
        return IsModelStale(state)
            || state?.SessionProgress?.ReconciliationRequired == true;
    }

    public static bool IsModelStale(SprinkSnapProjectState state)
    {
        return state?.ModelChangeAssessment?.IsStale == true;
    }

    public static WorkflowModuleAccess Evaluate(SprinkSnapProjectState state, SprinkSnapWorkflowStep step)
    {
        bool analyzeComplete = IsAnalyzeComplete(state);
        bool hazardComplete = IsHazardReviewComplete(state);
        bool sprinklerComplete = IsSprinklerReviewComplete(state);
        bool waterSupplyComplete = IsWaterSupplyComplete(state);
        bool designComplete = IsDesignGenerated(state);
        bool clashComplete = IsClashDetectionComplete(state);
        bool placementComplete = IsSprinklersPlacedInRevit(state);
        bool hydraulicsComplete = state.SessionProgress.HydraulicsComplete;
        bool materialsComplete = state.SessionProgress.MaterialsComplete;
        bool modelStale = IsModelStale(state);

        switch (step)
        {
            case SprinkSnapWorkflowStep.AnalyzeModel:
                if (modelStale)
                {
                    return CreateAccess(
                        step,
                        isUnlocked: true,
                        isComplete: false,
                        blockReason: StaleModelBlockReason,
                        status: WorkflowStepStatus.Warning,
                        statusLabel: "Re-analyze");
                }

                return CreateAccess(
                    step,
                    isUnlocked: true,
                    isComplete: analyzeComplete,
                    blockReason: string.Empty);

            case SprinkSnapWorkflowStep.HazardReview:
                if (!analyzeComplete)
                {
                    return CreateAccess(
                        step,
                        false,
                        hazardComplete,
                        "Run Analyze Model and extract rooms before hazard review.");
                }

                if (modelStale)
                {
                    return CreateAccess(
                        step,
                        true,
                        hazardComplete,
                        StaleModelBlockReason,
                        WorkflowStepStatus.Warning,
                        "Review changes");
                }

                return CreateAccess(step, true, hazardComplete, string.Empty);

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

                if (modelStale)
                {
                    return CreateAccess(step, false, designComplete, StaleModelBlockReason);
                }

                return CreateAccess(step, true, designComplete, string.Empty);

            case SprinkSnapWorkflowStep.ClashDetection:
                if (!designComplete)
                {
                    return CreateAccess(
                        step,
                        false,
                        clashComplete,
                        "Generate sprinkler design before running clash detection.");
                }

                if (modelStale)
                {
                    return CreateAccess(step, false, clashComplete, StaleModelBlockReason);
                }

                return CreateAccess(step, true, clashComplete, string.Empty);

            case SprinkSnapWorkflowStep.PlaceSprinklers:
                if (!clashComplete)
                {
                    return CreateAccess(
                        step,
                        false,
                        placementComplete,
                        "Resolve clashes and update layout before placing sprinklers in Revit.");
                }

                if (modelStale)
                {
                    return CreateAccess(step, false, placementComplete, StaleModelBlockReason);
                }

                if (state.SessionProgress.ReconciliationRequired)
                {
                    return CreateAccess(
                        step,
                        false,
                        placementComplete,
                        "Complete model reconciliation before placing sprinklers in Revit.",
                        WorkflowStepStatus.Blocked,
                        "Reconcile");
                }

                return CreateAccess(step, true, placementComplete, string.Empty);

            case SprinkSnapWorkflowStep.Hydraulics:
                if (!waterSupplyComplete)
                {
                    return CreateAccess(
                        step,
                        false,
                        hydraulicsComplete,
                        "Enter and validate water supply data before running hydraulics.");
                }

                if (!clashComplete)
                {
                    return CreateAccess(
                        step,
                        false,
                        hydraulicsComplete,
                        "Resolve clashes and update layout before running hydraulics.");
                }

                if (modelStale)
                {
                    return CreateAccess(
                        step,
                        false,
                        hydraulicsComplete,
                        StaleModelBlockReason);
                }

                if (state.SessionProgress.ReconciliationRequired)
                {
                    return CreateAccess(
                        step,
                        false,
                        hydraulicsComplete,
                        "Complete model reconciliation before running hydraulics.",
                        WorkflowStepStatus.Blocked,
                        "Reconcile");
                }

                if (HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state))
                {
                    return CreateAccess(
                        step,
                        true,
                        true,
                        string.Empty,
                        WorkflowStepStatus.Warning,
                        "Place pipes");
                }

                return CreateAccess(step, true, hydraulicsComplete, string.Empty);

            case SprinkSnapWorkflowStep.Materials:
                if (!(designComplete || placementComplete))
                {
                    return CreateAccess(
                        step,
                        false,
                        materialsComplete,
                        "Generate sprinkler design or place sprinklers before opening material takeoff.");
                }

                if (modelStale)
                {
                    return CreateAccess(
                        step,
                        false,
                        materialsComplete,
                        StaleModelBlockReason);
                }

                if (state.SessionProgress.ReconciliationRequired)
                {
                    return CreateAccess(
                        step,
                        false,
                        materialsComplete,
                        "Complete model reconciliation before refreshing material takeoff.",
                        WorkflowStepStatus.Blocked,
                        "Reconcile");
                }

                if (hydraulicsComplete && !materialsComplete)
                {
                    return CreateAccess(
                        step,
                        true,
                        false,
                        string.Empty,
                        WorkflowStepStatus.Warning,
                        "Refresh needed");
                }

                if (HydraulicWorkflowGuidanceService.ShouldWarnMaterialsMissingHydraulics(state))
                {
                    return CreateAccess(
                        step,
                        true,
                        materialsComplete,
                        string.Empty,
                        WorkflowStepStatus.Warning,
                        "Run hydraulics");
                }

                return CreateAccess(step, true, materialsComplete, string.Empty);

            case SprinkSnapWorkflowStep.Reports:
                bool reportsUnlocked = hydraulicsComplete || materialsComplete;
                if (reportsUnlocked && hydraulicsComplete && !materialsComplete)
                {
                    return CreateAccess(
                        step,
                        isUnlocked: true,
                        isComplete: state.SessionProgress.ReportsExported,
                        blockReason: string.Empty,
                        status: WorkflowStepStatus.Warning,
                        statusLabel: "Refresh materials");
                }

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
        if (RequiresSessionBackedProgress(state))
        {
            return state.SessionProgress.DesignGenerated;
        }

        return state.SessionProgress.DesignGenerated
            || state.Rooms.Any(room => room.ProposedSprinklers.Count > 0);
    }

    public static bool IsClashDetectionComplete(SprinkSnapProjectState state)
    {
        if (RequiresSessionBackedProgress(state))
        {
            return state.SessionProgress.ClashDetectionComplete;
        }

        return state.SessionProgress.ClashDetectionComplete
            || (state.ClashSummary != null && state.ClashSummary.TotalClashes == 0 && IsDesignGenerated(state))
            || (state.ClashSummary != null
                && state.ClashSummary.TotalClashes > 0
                && state.ClashSummary.UnresolvedClashes == 0);
    }

    public static bool IsSprinklersPlacedInRevit(SprinkSnapProjectState state)
    {
        if (RequiresSessionBackedProgress(state))
        {
            return state.SessionProgress.SprinklersPlacedInRevit;
        }

        return state.SessionProgress.SprinklersPlacedInRevit
            || (state.PlacementSummary != null && state.PlacementSummary.PlacedCount > 0);
    }

    private static WorkflowModuleAccess CreateAccess(
        SprinkSnapWorkflowStep step,
        bool isUnlocked,
        bool isComplete,
        string blockReason,
        WorkflowStepStatus? status = null,
        string statusLabel = null)
    {
        WorkflowStepStatus resolvedStatus = status ?? ResolveStatus(isUnlocked, isComplete);
        string resolvedLabel = statusLabel ?? ResolveStatusLabel(resolvedStatus, isComplete);

        return new WorkflowModuleAccess
        {
            Step = step,
            IsUnlocked = isUnlocked,
            IsComplete = isComplete,
            Status = resolvedStatus,
            StatusLabel = resolvedLabel,
            BlockReason = blockReason
        };
    }

    private static WorkflowStepStatus ResolveStatus(bool isUnlocked, bool isComplete)
    {
        if (!isUnlocked)
        {
            return WorkflowStepStatus.Blocked;
        }

        if (isComplete)
        {
            return WorkflowStepStatus.Complete;
        }

        return WorkflowStepStatus.InProgress;
    }

    private static string ResolveStatusLabel(WorkflowStepStatus status, bool isComplete)
    {
        switch (status)
        {
            case WorkflowStepStatus.Blocked:
                return "Locked";
            case WorkflowStepStatus.Complete:
                return "Complete";
            case WorkflowStepStatus.Warning:
                return isComplete ? "Review" : "Review needed";
            default:
                return "Ready";
        }
    }
}
