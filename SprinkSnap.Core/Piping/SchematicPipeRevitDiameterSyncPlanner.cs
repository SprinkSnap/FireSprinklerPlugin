using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeRevitDiameterSyncPlanner
{
    public static PlacedPipeDiameterSyncPlan BuildPlan(
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary)
    {
        PlacedPipeDiameterSyncPlan plan = new PlacedPipeDiameterSyncPlan();
        if (schematicPipeRouting?.Segments == null
            || schematicPipeRouting.Segments.Count == 0
            || pipePlacementSummary?.PlacedSegmentCount <= 0)
        {
            plan.Messages.Add("No placed Revit pipes available for diameter sync.");
            return plan;
        }

        if (!schematicPipeRouting.UsesAppliedPipeSizing)
        {
            plan.Messages.Add("Schematic routing has no velocity-sized diameters to sync.");
            return plan;
        }

        HashSet<int> claimedElementIds = new HashSet<int>();
        foreach (PipeSegment schematicSegment in schematicPipeRouting.Segments)
        {
            PipePlacementSegmentResult placedMatch = PlacedPipeHydraulicResolver.FindPlacedMatch(
                pipePlacementSummary,
                schematicSegment);
            if (placedMatch == null || placedMatch.PlacedElementId <= 0)
            {
                plan.SkippedCount++;
                continue;
            }

            if (claimedElementIds.Contains(placedMatch.PlacedElementId))
            {
                plan.SkippedCount++;
                continue;
            }

            if (schematicSegment.DiameterInches <= placedMatch.DiameterInches + 0.01)
            {
                plan.SkippedCount++;
                continue;
            }

            claimedElementIds.Add(placedMatch.PlacedElementId);
            plan.Targets.Add(new PlacedPipeDiameterSyncTarget
            {
                PlacedElementId = placedMatch.PlacedElementId,
                RoomRevitElementId = schematicSegment.RoomRevitElementId,
                RoomNumber = schematicSegment.RoomNumber,
                SegmentType = schematicSegment.SegmentType,
                TargetDiameterInches = schematicSegment.DiameterInches,
                CurrentDiameterInches = placedMatch.DiameterInches,
                UpdatedDescription = schematicSegment.Description ?? placedMatch.Description ?? string.Empty
            });
        }

        if (plan.Targets.Count == 0)
        {
            plan.Messages.Add("No placed Revit pipe diameters require sync with schematic sizing.");
        }
        else
        {
            plan.Messages.Add(
                "Prepared "
                + plan.Targets.Count
                + " placed Revit pipe(s) for diameter sync from schematic routing.");
        }

        return plan;
    }
}
