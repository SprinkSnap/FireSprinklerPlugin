using System;
using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicFittingRevitDiameterSyncPlanner
{
    private static readonly string[] DescriptionKeywords =
    {
        "branch drop",
        "branch tie-in",
        "sprinkler outlet",
        "cross main",
        "riser",
        "control valve",
        "os&y"
    };

    public static PlacedFittingDiameterSyncPlan BuildPlan(
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary)
    {
        PlacedFittingDiameterSyncPlan plan = new PlacedFittingDiameterSyncPlan();
        if (schematicPipeRouting?.Segments == null
            || schematicPipeRouting.Segments.Count == 0
            || (pipePlacementSummary?.PlacedFittingCount ?? 0) <= 0)
        {
            plan.Messages.Add("No placed Revit fittings available for diameter sync.");
            return plan;
        }

        if (!schematicPipeRouting.UsesAppliedPipeSizing)
        {
            plan.Messages.Add("Schematic routing has no velocity-sized diameters to sync to fittings.");
            return plan;
        }

        Dictionary<int, PipePlacementRoomResult> placedRooms = (pipePlacementSummary.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        IList<PipeJoint> schematicJoints = SchematicPipeJointBuilder.BuildFromRouting(schematicPipeRouting);
        HashSet<int> claimedElementIds = new HashSet<int>();
        foreach (PipeJoint schematicJoint in schematicJoints)
        {
            if (!placedRooms.TryGetValue(schematicJoint.RoomRevitElementId, out PipePlacementRoomResult placedRoom))
            {
                plan.SkippedCount++;
                continue;
            }

            PipePlacementFittingResult placedMatch = FindPlacedMatch(placedRoom, schematicJoint);
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

            if (schematicJoint.DiameterInches <= placedMatch.DiameterInches + 0.01)
            {
                plan.SkippedCount++;
                continue;
            }

            claimedElementIds.Add(placedMatch.PlacedElementId);
            plan.Targets.Add(new PlacedFittingDiameterSyncTarget
            {
                PlacedElementId = placedMatch.PlacedElementId,
                RoomRevitElementId = schematicJoint.RoomRevitElementId,
                RoomNumber = schematicJoint.RoomNumber,
                JointType = schematicJoint.JointType,
                TargetDiameterInches = schematicJoint.DiameterInches,
                CurrentDiameterInches = placedMatch.DiameterInches,
                UpdatedDescription = schematicJoint.Description ?? placedMatch.Description ?? string.Empty
            });
        }

        if (plan.Targets.Count == 0)
        {
            plan.Messages.Add("No placed Revit fitting diameters require sync with schematic sizing.");
        }
        else
        {
            plan.Messages.Add(
                "Prepared "
                + plan.Targets.Count
                + " placed Revit fitting(s) for diameter sync from schematic joints.");
        }

        return plan;
    }

    private static PipePlacementFittingResult FindPlacedMatch(
        PipePlacementRoomResult placedRoom,
        PipeJoint schematicJoint)
    {
        if (placedRoom?.PlacedFittings == null || placedRoom.PlacedFittings.Count == 0)
        {
            return null;
        }

        List<PipePlacementFittingResult> candidates = placedRoom.PlacedFittings
            .Where(fitting => string.Equals(fitting.JointType, schematicJoint.JointType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PipePlacementFittingResult descriptionMatch = candidates.FirstOrDefault(fitting =>
            string.Equals(fitting.Description, schematicJoint.Description, StringComparison.OrdinalIgnoreCase));
        if (descriptionMatch != null)
        {
            return descriptionMatch;
        }

        descriptionMatch = candidates.FirstOrDefault(fitting =>
            DescriptionsMatch(schematicJoint.Description, fitting.Description));
        if (descriptionMatch != null)
        {
            return descriptionMatch;
        }

        return null;
    }

    private static bool DescriptionsMatch(string schematicDescription, string placedDescription)
    {
        string schematic = schematicDescription ?? string.Empty;
        string placed = placedDescription ?? string.Empty;
        if (string.IsNullOrWhiteSpace(schematic) || string.IsNullOrWhiteSpace(placed))
        {
            return false;
        }

        if (schematic.IndexOf(placed, StringComparison.OrdinalIgnoreCase) >= 0
            || placed.IndexOf(schematic, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        foreach (string keyword in DescriptionKeywords)
        {
            if (schematic.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                && placed.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
