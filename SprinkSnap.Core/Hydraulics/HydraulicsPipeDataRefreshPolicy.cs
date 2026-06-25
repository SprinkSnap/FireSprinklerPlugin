using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicsPipeDataRefreshPolicy
{
    public static bool ShouldRemeasureBeforeCalculation(
        SchematicPipeRoutingSummary schematicRouting,
        PipePlacementSummary pipePlacementSummary,
        bool isPreviewMode,
        bool remeasureAvailable)
    {
        if (isPreviewMode || !remeasureAvailable)
        {
            return false;
        }

        return (schematicRouting?.TotalSegmentCount ?? 0) > 0
            || (pipePlacementSummary?.PlacedSegmentCount ?? 0) > 0;
    }

    public static bool ShouldSyncPlacedPipeDiametersAfterCalculation(
        HydraulicCalculationResult hydraulicResult,
        PipePlacementSummary pipePlacementSummary,
        bool isPreviewMode,
        bool syncAvailable)
    {
        if (isPreviewMode || !syncAvailable)
        {
            return false;
        }

        return hydraulicResult?.UsesSchematicPipeSizingWriteback == true
            && (pipePlacementSummary?.PlacedSegmentCount ?? 0) > 0;
    }

    public static bool ShouldReSolveAfterDiameterSync(bool diameterSyncWasAttempted, bool isPreviewMode)
    {
        return !isPreviewMode && diameterSyncWasAttempted;
    }
}
