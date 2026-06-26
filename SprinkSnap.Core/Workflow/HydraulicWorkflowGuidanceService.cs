using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public static class HydraulicWorkflowGuidanceService
{
    public const string SchematicOnlyHydraulicsMessage =
        "Hydraulics used schematic geometry only. Place pipes in Revit and re-run hydraulics to sync sized diameters.";

    public const string MaterialsMissingHydraulicsMessage =
        "Run hydraulics before relying on material takeoff — quantities may not reflect velocity-sized pipe diameters.";

    public const string ReRunHydraulicsAfterPlacementMessage =
        "Placed Revit pipes are not reflected in the current hydraulic result. Re-run hydraulics to use measured lengths and sync diameters.";

    public const string HydraulicsRequiredBeforeReportExportMessage =
        "Run hydraulics before exporting hydraulic reports, node diagrams, or material takeoff.";

    public static bool HasPlacedPipeGeometry(SprinkSnapProjectState state)
    {
        return (state?.PipePlacementSummary?.PlacedSegmentCount ?? 0) > 0;
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
}
