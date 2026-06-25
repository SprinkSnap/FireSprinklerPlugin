using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public static class PlacedFittingTakeoffCalculator
{
    public static bool HasPlacedFittings(PipePlacementRoomResult placedRoom)
    {
        return (placedRoom?.PlacedFittings?.Count ?? 0) > 0;
    }

    public static bool UsesPlacedFittingCounts(PipePlacementRoomResult placedRoom)
    {
        return HasPlacedFittings(placedRoom);
    }

    public static int CountValves(PipePlacementRoomResult placedRoom)
    {
        if (!HasPlacedFittings(placedRoom))
        {
            return 0;
        }

        return placedRoom.PlacedFittings.Count(fitting =>
            string.Equals(fitting.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase));
    }

    public static int CountFittings(
        PipePlacementRoomResult placedRoom,
        string jointType,
        double diameterInches)
    {
        if (!HasPlacedFittings(placedRoom))
        {
            return 0;
        }

        return placedRoom.PlacedFittings.Count(fitting =>
            string.Equals(fitting.JointType, jointType, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(fitting.DiameterInches - diameterInches) < 0.01);
    }

    public static int CountRiserAssemblies(
        PipePlacementRoomResult placedRoom,
        bool schematicHasRiser)
    {
        if (placedRoom?.PlacedSegments != null
            && placedRoom.PlacedSegments.Any(segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        if (HasPlacedFittings(placedRoom))
        {
            return 0;
        }

        return schematicHasRiser ? 1 : 0;
    }
}
