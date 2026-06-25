using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public sealed class FittingTakeoffEntry
{
    public string JointType { get; set; } = string.Empty;

    public double DiameterInches { get; set; }

    public int Count { get; set; }
}

public sealed class RoomFittingTakeoff
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public IList<FittingTakeoffEntry> FittingCounts { get; set; } = new List<FittingTakeoffEntry>();

    public int RiserAssemblyCount { get; set; }

    public int ValveCount { get; set; }

    public bool UsesPlacedPipes { get; set; }

    public bool UsesPlacedFittings { get; set; }
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
            IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildRoomJoints(roomSegments);
            bool hasRiser = roomSegments.Any(segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));

            placedRooms.TryGetValue(roomGroup.Key, out PipePlacementRoomResult placedRoom);
            bool usesPlacedFittings = PlacedFittingTakeoffCalculator.UsesPlacedFittingCounts(placedRoom);
            bool usesPlacedPipes = usesPlacedFittings
                || (placedRoom?.PlacedSegmentCount ?? 0) > 0;

            RoomFittingTakeoff takeoff = new RoomFittingTakeoff
            {
                RoomRevitElementId = roomGroup.Key,
                RoomNumber = first.RoomNumber,
                RoomName = first.RoomName,
                LevelName = first.LevelName,
                FittingCounts = usesPlacedFittings
                    ? BuildPlacedFittingCounts(placedRoom)
                    : BuildSchematicFittingCounts(joints),
                RiserAssemblyCount = PlacedFittingTakeoffCalculator.CountRiserAssemblies(placedRoom, hasRiser),
                ValveCount = usesPlacedFittings
                    ? PlacedFittingTakeoffCalculator.CountValves(placedRoom)
                    : joints.Count(joint =>
                        string.Equals(joint.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase)),
                UsesPlacedPipes = usesPlacedPipes,
                UsesPlacedFittings = usesPlacedFittings
            };

            if (takeoff.FittingCounts.Count > 0
                || takeoff.RiserAssemblyCount > 0
                || takeoff.ValveCount > 0)
            {
                takeoffs.Add(takeoff);
            }
        }

        return takeoffs.OrderBy(takeoff => takeoff.LevelName).ThenBy(takeoff => takeoff.RoomNumber).ToList();
    }

    private static IList<FittingTakeoffEntry> BuildSchematicFittingCounts(IEnumerable<PipeJoint> joints)
    {
        return joints
            .Where(joint => !string.Equals(joint.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase))
            .GroupBy(joint => joint.JointType + "|" + joint.DiameterInches.ToString("0.##"), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                PipeJoint first = group.First();
                return new FittingTakeoffEntry
                {
                    JointType = first.JointType,
                    DiameterInches = first.DiameterInches,
                    Count = group.Count()
                };
            })
            .OrderBy(entry => entry.JointType)
            .ThenBy(entry => entry.DiameterInches)
            .ToList();
    }

    private static IList<FittingTakeoffEntry> BuildPlacedFittingCounts(PipePlacementRoomResult placedRoom)
    {
        if (placedRoom?.PlacedFittings == null || placedRoom.PlacedFittings.Count == 0)
        {
            return new List<FittingTakeoffEntry>();
        }

        return placedRoom.PlacedFittings
            .Where(fitting => !string.Equals(fitting.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase))
            .GroupBy(fitting => fitting.JointType + "|" + fitting.DiameterInches.ToString("0.##"), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                PipePlacementFittingResult first = group.First();
                return new FittingTakeoffEntry
                {
                    JointType = first.JointType,
                    DiameterInches = first.DiameterInches,
                    Count = group.Count()
                };
            })
            .OrderBy(entry => entry.JointType)
            .ThenBy(entry => entry.DiameterInches)
            .ToList();
    }
}
