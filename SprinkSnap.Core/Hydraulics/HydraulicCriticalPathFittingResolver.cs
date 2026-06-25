using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class CriticalPathFitting
{
    public PipeJoint Joint { get; set; }

    public int SegmentIndex { get; set; }

    public bool AtSegmentStart { get; set; }

    public double EquivalentLengthFeet { get; set; }

    public double FrictionLossPsi { get; set; }

    public double FlowGpm { get; set; }

    public double DownstreamPressurePsi { get; set; }

    public double UpstreamPressurePsi { get; set; }

    public string DataSource { get; set; } = "Schematic";
}

public static class HydraulicCriticalPathFittingResolver
{
    private const double LocationToleranceFeet = 0.15;

    public static IList<CriticalPathFitting> ResolveForSegmentChain(
        IList<HydraulicGraphSegment> segmentChain,
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary)
    {
        if (segmentChain == null || segmentChain.Count == 0)
        {
            return new List<CriticalPathFitting>();
        }

        IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildFromRouting(schematicPipeRouting);
        HashSet<string> placedJointKeys = BuildPlacedJointKeys(pipePlacementSummary);
        List<CriticalPathFitting> fittings = new List<CriticalPathFitting>();
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < segmentChain.Count; index++)
        {
            HydraulicGraphSegment segment = segmentChain[index];
            AddMatches(fittings, visited, joints, segment, index, segment.Start, atSegmentStart: true, placedJointKeys);
            AddMatches(fittings, visited, joints, segment, index, segment.End, atSegmentStart: false, placedJointKeys);
        }

        return fittings;
    }

    private static void AddMatches(
        IList<CriticalPathFitting> fittings,
        ISet<string> visited,
        IEnumerable<PipeJoint> joints,
        HydraulicGraphSegment segment,
        int segmentIndex,
        Point3D location,
        bool atSegmentStart,
        ISet<string> placedJointKeys)
    {
        foreach (PipeJoint joint in joints)
        {
            if (!PointsMatch(joint.Location, location))
            {
                continue;
            }

            string visitKey = BuildVisitKey(joint);
            if (!visited.Add(visitKey))
            {
                continue;
            }

            fittings.Add(new CriticalPathFitting
            {
                Joint = joint,
                SegmentIndex = segmentIndex,
                AtSegmentStart = atSegmentStart,
                EquivalentLengthFeet = FittingEquivalentLengthTable.GetEquivalentLengthFeet(
                    joint.JointType,
                    joint.DiameterInches > 0 ? joint.DiameterInches : segment.DiameterInches),
                DataSource = placedJointKeys.Contains(BuildPlacedJointKey(joint))
                    ? "Placed"
                    : "Schematic"
            });
        }
    }

    private static HashSet<string> BuildPlacedJointKeys(PipePlacementSummary pipePlacementSummary)
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PipePlacementRoomResult roomResult in pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
        {
            foreach (PipePlacementFittingResult fitting in roomResult.PlacedFittings ?? new List<PipePlacementFittingResult>())
            {
                keys.Add(
                    roomResult.RoomRevitElementId
                    + "|"
                    + fitting.JointType
                    + "|"
                    + fitting.DiameterInches.ToString("0.###"));
            }
        }

        return keys;
    }

    private static string BuildPlacedJointKey(PipeJoint joint)
    {
        return joint.RoomRevitElementId
            + "|"
            + joint.JointType
            + "|"
            + joint.DiameterInches.ToString("0.###");
    }

    private static string BuildVisitKey(PipeJoint joint)
    {
        return joint.JointType
            + "|"
            + joint.DiameterInches.ToString("0.###")
            + "|"
            + joint.Location.X.ToString("0.###")
            + "|"
            + joint.Location.Y.ToString("0.###")
            + "|"
            + joint.Location.Z.ToString("0.###");
    }

    private static bool PointsMatch(Point3D left, Point3D right)
    {
        return Math.Abs(left.X - right.X) <= LocationToleranceFeet
            && Math.Abs(left.Y - right.Y) <= LocationToleranceFeet
            && Math.Abs(left.Z - right.Z) <= LocationToleranceFeet;
    }
}
