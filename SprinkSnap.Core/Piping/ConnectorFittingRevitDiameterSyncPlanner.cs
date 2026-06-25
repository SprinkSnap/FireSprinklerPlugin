using System;
using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class ConnectorFittingRevitDiameterSyncPlanner
{
    private const double BranchDiameterThresholdInches = 2.5;

    public static PlacedFittingDiameterSyncPlan BuildPlan(
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary,
        ISet<int> excludedElementIds = null)
    {
        PlacedFittingDiameterSyncPlan plan = new PlacedFittingDiameterSyncPlan();
        if (schematicPipeRouting?.Segments == null
            || schematicPipeRouting.Segments.Count == 0
            || (pipePlacementSummary?.PlacedFittingCount ?? 0) <= 0)
        {
            return plan;
        }

        if (!schematicPipeRouting.UsesAppliedPipeSizing)
        {
            return plan;
        }

        HashSet<int> claimedElementIds = excludedElementIds != null
            ? new HashSet<int>(excludedElementIds)
            : new HashSet<int>();

        Dictionary<int, RoomDiameterTargets> roomTargets = BuildRoomDiameterTargets(schematicPipeRouting.Segments);
        Dictionary<int, PipePlacementRoomResult> placedRooms = (pipePlacementSummary.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (KeyValuePair<int, PipePlacementRoomResult> roomEntry in placedRooms)
        {
            if (!roomTargets.TryGetValue(roomEntry.Key, out RoomDiameterTargets targets))
            {
                continue;
            }

            foreach (PipePlacementFittingResult placedFitting in roomEntry.Value.PlacedFittings ?? new List<PipePlacementFittingResult>())
            {
                if (placedFitting.PlacedElementId <= 0
                    || claimedElementIds.Contains(placedFitting.PlacedElementId)
                    || !IsConnectorFitting(placedFitting))
                {
                    plan.SkippedCount++;
                    continue;
                }

                double targetDiameterInches = ResolveTargetDiameterInches(placedFitting, targets);
                if (targetDiameterInches <= placedFitting.DiameterInches + 0.01)
                {
                    plan.SkippedCount++;
                    continue;
                }

                claimedElementIds.Add(placedFitting.PlacedElementId);
                plan.Targets.Add(new PlacedFittingDiameterSyncTarget
                {
                    PlacedElementId = placedFitting.PlacedElementId,
                    RoomRevitElementId = roomEntry.Key,
                    RoomNumber = roomEntry.Value.RoomNumber,
                    JointType = placedFitting.JointType,
                    TargetDiameterInches = targetDiameterInches,
                    CurrentDiameterInches = placedFitting.DiameterInches,
                    UpdatedDescription = UpdateConnectorDescription(
                        placedFitting.Description,
                        targetDiameterInches,
                        placedFitting.JointType)
                });
            }
        }

        if (plan.Targets.Count > 0)
        {
            plan.Messages.Add(
                "Prepared "
                + plan.Targets.Count
                + " connector-routed Revit fitting(s) for diameter sync.");
        }

        return plan;
    }

    private static Dictionary<int, RoomDiameterTargets> BuildRoomDiameterTargets(IEnumerable<PipeSegment> segments)
    {
        Dictionary<int, RoomDiameterTargets> targets = new Dictionary<int, RoomDiameterTargets>();
        foreach (IGrouping<int, PipeSegment> roomGroup in segments.GroupBy(segment => segment.RoomRevitElementId))
        {
            IEnumerable<PipeSegment> roomSegments = roomGroup.ToList();
            targets[roomGroup.Key] = new RoomDiameterTargets
            {
                BranchDiameterInches = roomSegments
                    .Where(segment => IsBranchSegment(segment.SegmentType))
                    .Select(segment => segment.DiameterInches)
                    .Where(diameterInches => diameterInches > 0)
                    .DefaultIfEmpty(0)
                    .Max(),
                MainDiameterInches = roomSegments
                    .Where(segment => !IsBranchSegment(segment.SegmentType))
                    .Select(segment => segment.DiameterInches)
                    .Where(diameterInches => diameterInches > 0)
                    .DefaultIfEmpty(0)
                    .Max()
            };
        }

        return targets;
    }

    private static double ResolveTargetDiameterInches(
        PipePlacementFittingResult placedFitting,
        RoomDiameterTargets roomTargets)
    {
        if (string.Equals(placedFitting.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase))
        {
            return roomTargets.MainDiameterInches;
        }

        if (string.Equals(placedFitting.JointType, PipeJointTypes.Tee, StringComparison.OrdinalIgnoreCase))
        {
            return roomTargets.BranchDiameterInches > 0
                ? roomTargets.BranchDiameterInches
                : roomTargets.MainDiameterInches;
        }

        if (placedFitting.DiameterInches <= BranchDiameterThresholdInches && roomTargets.BranchDiameterInches > 0)
        {
            return roomTargets.BranchDiameterInches;
        }

        return roomTargets.MainDiameterInches > 0
            ? roomTargets.MainDiameterInches
            : roomTargets.BranchDiameterInches;
    }

    private static bool IsConnectorFitting(PipePlacementFittingResult placedFitting)
    {
        string description = placedFitting.Description ?? string.Empty;
        return description.IndexOf("connector routing", StringComparison.OrdinalIgnoreCase) >= 0
            || description.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string UpdateConnectorDescription(
        string description,
        double targetDiameterInches,
        string jointType)
    {
        string existing = description ?? string.Empty;
        int inchMarkIndex = existing.IndexOf('"');
        if (inchMarkIndex > 0)
        {
            return targetDiameterInches.ToString("0.##") + existing.Substring(inchMarkIndex);
        }

        return targetDiameterInches.ToString("0.##") + "\" " + jointType + " at connector routing joint";
    }

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RoomDiameterTargets
    {
        public double BranchDiameterInches { get; set; }

        public double MainDiameterInches { get; set; }
    }
}
