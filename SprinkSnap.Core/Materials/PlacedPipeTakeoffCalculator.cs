using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public static class PlacedPipeTakeoffCalculator
{
    public static double ResolvePipeLengthFeet(
        PipeSegment schematicSegment,
        double schematicLengthFeet,
        PipePlacementRoomResult placedRoom)
    {
        PipePlacementSegmentResult placedMatch = FindPlacedMatch(placedRoom, schematicSegment);
        return placedMatch != null && placedMatch.LengthFeet > 0
            ? placedMatch.LengthFeet
            : schematicLengthFeet;
    }

    public static bool UsesPlacedLength(PipeSegment schematicSegment, PipePlacementRoomResult placedRoom)
    {
        PipePlacementSegmentResult placedMatch = FindPlacedMatch(placedRoom, schematicSegment);
        return placedMatch != null && placedMatch.LengthFeet > 0;
    }

    public static double ResolvePipeLengthFeet(
        string segmentType,
        double diameterInches,
        double schematicLengthFeet,
        PipePlacementRoomResult placedRoom)
    {
        if (placedRoom?.PlacedSegments == null || placedRoom.PlacedSegments.Count == 0)
        {
            return schematicLengthFeet;
        }

        return placedRoom.PlacedSegments
            .Where(segment => SegmentMatches(segment, segmentType, diameterInches))
            .Sum(segment => segment.LengthFeet) > 0
            ? placedRoom.PlacedSegments
                .Where(segment => SegmentMatches(segment, segmentType, diameterInches))
                .Sum(segment => segment.LengthFeet)
            : schematicLengthFeet;
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

    private static PipePlacementSegmentResult FindPlacedMatch(
        PipePlacementRoomResult placedRoom,
        PipeSegment schematicSegment)
    {
        if (placedRoom == null || schematicSegment == null)
        {
            return null;
        }

        PipePlacementSummary summary = new PipePlacementSummary
        {
            RoomResults = { placedRoom }
        };

        return PlacedPipeHydraulicResolver.FindPlacedMatch(summary, schematicSegment);
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
