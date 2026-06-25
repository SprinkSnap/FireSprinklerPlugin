using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class FittingDiameterSyncPlanComposer
{
    public static PlacedFittingDiameterSyncPlan BuildCombinedPlan(
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary)
    {
        PlacedFittingDiameterSyncPlan schematicPlan = SchematicFittingRevitDiameterSyncPlanner.BuildPlan(
            schematicPipeRouting,
            pipePlacementSummary);
        HashSet<int> claimedElementIds = schematicPlan.Targets
            .Select(target => target.PlacedElementId)
            .ToHashSet();

        PlacedFittingDiameterSyncPlan connectorPlan = ConnectorFittingRevitDiameterSyncPlanner.BuildPlan(
            schematicPipeRouting,
            pipePlacementSummary,
            claimedElementIds);

        foreach (PlacedFittingDiameterSyncTarget target in connectorPlan.Targets)
        {
            schematicPlan.Targets.Add(target);
        }

        schematicPlan.SkippedCount += connectorPlan.SkippedCount;
        foreach (string message in connectorPlan.Messages)
        {
            if (!schematicPlan.Messages.Contains(message))
            {
                schematicPlan.Messages.Add(message);
            }
        }

        if (schematicPlan.Targets.Count == 0 && schematicPlan.Messages.Count == 0)
        {
            schematicPlan.Messages.Add("No placed Revit fitting diameters require sync with schematic sizing.");
        }

        return schematicPlan;
    }
}
