using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public sealed class RoomFittingTakeoff
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public int Elbow125Count { get; set; }

    public int Tee125Count { get; set; }

    public int Elbow4InchCount { get; set; }

    public int RiserAssemblyCount { get; set; }

    public int ValveCount { get; set; }

    public bool UsesPlacedPipes { get; set; }
}

public static class FittingTakeoffCalculator
{
    public static IList<RoomFittingTakeoff> Calculate(
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary = null)
    {
        List<PipeSegment> segments = schematicPipeRouting?.Segments?.ToList() ?? new List<PipeSegment>();
        if (segments.Count == 0)
        {
            return new List<RoomFittingTakeoff>();
        }

        Dictionary<int, PipePlacementRoomResult> placedRooms = (pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        List<RoomFittingTakeoff> takeoffs = new List<RoomFittingTakeoff>();
        foreach (IGrouping<int, PipeSegment> roomGroup in segments.GroupBy(segment => segment.RoomRevitElementId))
        {
            List<PipeSegment> roomSegments = roomGroup.ToList();
            PipeSegment first = roomSegments.First();
            int branchDropCount = CountBranchSegments(roomSegments, "branch drop");
            int branchTieInCount = CountBranchSegments(roomSegments, "branch tie-in");
            bool hasRiser = roomSegments.Any(segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
            bool hasCrossMain = roomSegments.Any(segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));

            placedRooms.TryGetValue(roomGroup.Key, out PipePlacementRoomResult placedRoom);
            bool usesPlaced = placedRoom?.PlacedSegmentCount > 0;

            RoomFittingTakeoff takeoff = new RoomFittingTakeoff
            {
                RoomRevitElementId = roomGroup.Key,
                RoomNumber = first.RoomNumber,
                RoomName = first.RoomName,
                LevelName = first.LevelName,
                Tee125Count = branchTieInCount,
                Elbow125Count = branchDropCount + branchTieInCount,
                Elbow4InchCount = hasRiser && hasCrossMain ? 1 : 0,
                RiserAssemblyCount = hasRiser ? 1 : 0,
                ValveCount = hasRiser ? 1 : 0,
                UsesPlacedPipes = usesPlaced
            };

            if (takeoff.Elbow125Count > 0
                || takeoff.Tee125Count > 0
                || takeoff.Elbow4InchCount > 0
                || takeoff.RiserAssemblyCount > 0
                || takeoff.ValveCount > 0)
            {
                takeoffs.Add(takeoff);
            }
        }

        return takeoffs.OrderBy(takeoff => takeoff.LevelName).ThenBy(takeoff => takeoff.RoomNumber).ToList();
    }

    private static int CountBranchSegments(IEnumerable<PipeSegment> segments, string descriptionToken)
    {
        return segments.Count(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase)
            && (segment.Description ?? string.Empty).IndexOf(descriptionToken, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
