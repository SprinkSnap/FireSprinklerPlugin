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

    public static bool HasPlacedPipeGeometry(SprinkSnapProjectState state)
    {
        return (state?.PipePlacementSummary?.PlacedSegmentCount ?? 0) > 0;
    }

    public static bool IsSchematicOnlyHydraulicsComplete(SprinkSnapProjectState state)
    {
        return state?.SessionProgress.HydraulicsComplete == true
            && !HasPlacedPipeGeometry(state);
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
}
