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
            IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildRoomJoints(roomSegments);
            bool hasRiser = roomSegments.Any(segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));

            placedRooms.TryGetValue(roomGroup.Key, out PipePlacementRoomResult placedRoom);
            bool usesPlaced = (placedRoom?.PlacedSegmentCount ?? 0) > 0
                || (placedRoom?.PlacedFittingCount ?? 0) > 0;

            RoomFittingTakeoff takeoff = new RoomFittingTakeoff
            {
                RoomRevitElementId = roomGroup.Key,
                RoomNumber = first.RoomNumber,
                RoomName = first.RoomName,
                LevelName = first.LevelName,
                Tee125Count = joints.Count(joint =>
                    string.Equals(joint.JointType, PipeJointTypes.Tee, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(joint.DiameterInches - 1.25) < 0.01),
                Elbow125Count = joints.Count(joint =>
                    string.Equals(joint.JointType, PipeJointTypes.Elbow, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(joint.DiameterInches - 1.25) < 0.01),
                Elbow4InchCount = joints.Count(joint =>
                    string.Equals(joint.JointType, PipeJointTypes.Elbow, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(joint.DiameterInches - 4.0) < 0.01),
                RiserAssemblyCount = hasRiser ? 1 : 0,
                ValveCount = joints.Count(joint =>
                    string.Equals(joint.JointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase)),
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
}
