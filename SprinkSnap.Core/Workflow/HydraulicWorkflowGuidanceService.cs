using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public static class HydraulicWorkflowGuidanceService
{
    public const string SchematicOnlyHydraulicsMessage =
        "Hydraulics used schematic geometry only. Place pipes in Revit and re-run hydraulics to sync sized diameters.";

    public const string PipeSizingWithoutPlacementMessage =
        "Pipe sizing was applied in memory. Place pipes in Revit and re-run hydraulics to sync diameters to the model.";

    public const string MaterialsMissingHydraulicsMessage =
        "Run hydraulics before relying on material takeoff — quantities may not reflect velocity-sized pipe diameters.";

    public const string PlacePipesInRevitMessage =
        "Schematic pipe routing is ready but no pipes are placed in Revit. Click Place Schematic Pipes, then re-run hydraulics.";

    public const string HydraulicsPlacePipesActionMessage =
        "Open Place Sprinklers and click Place Schematic Pipes in Revit, then re-run hydraulics to sync sized diameters.";

    public const string ReRunHydraulicsAfterPlacementMessage =
        "Placed Revit pipes are not reflected in the current hydraulic result. Re-run hydraulics to use measured lengths and sync diameters.";

    public const string HydraulicsRequiredBeforeReportExportMessage =
        "Run hydraulics before exporting hydraulic reports, node diagrams, or material takeoff.";

    public static bool HasPlacedPipeGeometry(SprinkSnapProjectState state)
    {
        return (state?.PipePlacementSummary?.PlacedSegmentCount ?? 0) > 0;
    }

    public static bool HasSchematicPipeRouting(SprinkSnapProjectState state)
    {
        return (state?.SchematicPipeRouting?.TotalSegmentCount ?? 0) > 0;
    }

    public static bool ShouldWarnPipePlacementNeeded(SprinkSnapProjectState state)
    {
        return HasSchematicPipeRouting(state) && !HasPlacedPipeGeometry(state);
    }

    public static bool IsSchematicOnlyHydraulicsComplete(SprinkSnapProjectState state)
    {
        return state?.SessionProgress.HydraulicsComplete == true
            && !HasPlacedPipeGeometry(state);
    }

    public static bool ShouldWarnReRunHydraulicsAfterPipePlacement(SprinkSnapProjectState state)
    {
        if (!HasPlacedPipeGeometry(state) || state?.SessionProgress.HydraulicsComplete != true)
        {
            return false;
        }

        HydraulicCalculationResult result = state.HydraulicResult;
        if (result == null || result.TotalFlowGpm <= 0)
        {
            return false;
        }

        return !result.UsesPlacedPipeLengths && !result.UsesPlacedPipeTopology;
    }

    public static bool ShouldWarnMaterialsMissingHydraulics(SprinkSnapProjectState state)
    {
        if (state?.SessionProgress.HydraulicsComplete == true)
        {
            return false;
        }

        bool designComplete = SprinkSnapWorkflowGate.IsDesignGenerated(state);
        bool placementComplete = SprinkSnapWorkflowGate.IsSprinklersPlacedInRevit(state);
        return designComplete || placementComplete;
    }

    public static bool ShouldWarnPipeSizingWithoutPlacement(HydraulicCalculationResult result, SprinkSnapProjectState state)
    {
        if (result == null || HasPlacedPipeGeometry(state))
        {
            return false;
        }

        return result.UsesSchematicPipeSizingWriteback
            || result.UsesAppliedPipeSizing
            || result.SchematicWritebackSegmentCount > 0
            || result.AppliedPipeSizingSegmentCount > 0;
    }

    public static bool IsPipePlacementGuidanceActive(SprinkSnapProjectState state)
    {
        return ShouldWarnPipePlacementNeeded(state);
    }

    public static string GetPipePlacementBannerTitle(SprinkSnapProjectState state)
    {
        return ShouldWarnPipePlacementNeeded(state) ? "Revit pipe placement needed" : string.Empty;
    }

    public static string GetPipePlacementBannerMessage(SprinkSnapProjectState state)
    {
        return ShouldWarnPipePlacementNeeded(state) ? PlacePipesInRevitMessage : string.Empty;
    }

    public static string GetHydraulicWorkflowActionMessage(SprinkSnapProjectState state, HydraulicCalculationResult result = null)
    {
        HydraulicCalculationResult resolvedResult = result ?? state?.HydraulicResult;

        if (ShouldWarnReRunHydraulicsAfterPipePlacement(state))
        {
            return ReRunHydraulicsAfterPlacementMessage;
        }

        if (ShouldWarnPipeSizingWithoutPlacement(resolvedResult, state))
        {
            return PipeSizingWithoutPlacementMessage + " " + HydraulicsPlacePipesActionMessage;
        }

        if (IsSchematicOnlyHydraulicsComplete(state))
        {
            return SchematicOnlyHydraulicsMessage + " " + HydraulicsPlacePipesActionMessage;
        }

        return string.Empty;
    }
}
