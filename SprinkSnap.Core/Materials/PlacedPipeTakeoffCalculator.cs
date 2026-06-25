using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public static class PlacedPipeTakeoffCalculator
{
    public static double ResolvePipeLengthFeet(
        string segmentType,
        double diameterInches,
        double schematicLengthFeet,
        PipePlacementRoomResult placedRoom)
    {
        double placedLength = GetPlacedLengthFeet(segmentType, diameterInches, placedRoom);
        return placedLength > 0 ? placedLength : schematicLengthFeet;
    }

    public static double GetPlacedLengthFeet(
        string segmentType,
        double diameterInches,
        PipePlacementRoomResult placedRoom)
    {
        if (placedRoom?.PlacedSegments == null || placedRoom.PlacedSegments.Count == 0)
        {
            return 0.0;
        }

        return placedRoom.PlacedSegments
            .Where(segment => SegmentMatches(segment, segmentType, diameterInches))
            .Sum(segment => segment.LengthFeet);
    }

    public static bool UsesPlacedLengthForGroup(
        string segmentType,
        double diameterInches,
        PipePlacementRoomResult placedRoom)
    {
        return GetPlacedLengthFeet(segmentType, diameterInches, placedRoom) > 0;
    }

    public static double GetTotalPlacedLengthFeet(PipePlacementSummary pipePlacementSummary)
    {
        return (pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
            .Sum(room => room.PlacedLengthFeet);
    }

    private static bool SegmentMatches(
        PipePlacementSegmentResult segment,
        string segmentType,
        double diameterInches)
    {
        return string.Equals(segment.SegmentType, segmentType, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(segment.DiameterInches - diameterInches) < 0.01;
    }
}
