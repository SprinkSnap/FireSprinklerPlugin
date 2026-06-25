using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeJointBuilder
{
    private const double DefaultBranchDiameterInches = 1.25;

    private const double DefaultMainDiameterInches = 4.0;

    private const double LocationToleranceFeet = 0.15;

    public static IList<PipeJoint> BuildFromRouting(SchematicPipeRoutingSummary routing)
    {
        List<PipeJoint> joints = new List<PipeJoint>();
        List<PipeSegment> segments = routing?.Segments?.ToList() ?? new List<PipeSegment>();
        foreach (IGrouping<int, PipeSegment> roomGroup in segments.GroupBy(segment => segment.RoomRevitElementId))
        {
            joints.AddRange(BuildRoomJoints(roomGroup.ToList()));
        }

        return joints;
    }

    public static IList<PipeJoint> BuildRoomJoints(IList<PipeSegment> segments)
    {
        List<PipeJoint> joints = new List<PipeJoint>();
        if (segments == null || segments.Count == 0)
        {
            return joints;
        }

        PipeSegment first = segments.First();
        double mainDiameterInches = ResolveMainDiameterInches(segments);
        bool hasCrossMain = segments.Any(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));

        PipeSegment riser = segments.FirstOrDefault(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        if (riser != null)
        {
            double riserDiameterInches = ResolveSegmentDiameterInches(riser, mainDiameterInches);
            AddJoint(
                joints,
                first,
                PipeJointTypes.Valve,
                riserDiameterInches,
                riser.Start,
                FormatDiameterDescription(riserDiameterInches, " OS&Y control valve at riser base"));

            if (hasCrossMain)
            {
                AddJoint(
                    joints,
                    first,
                    PipeJointTypes.Elbow,
                    riserDiameterInches,
                    riser.End,
                    FormatDiameterDescription(riserDiameterInches, " elbow at riser / cross main"));
            }
        }

        int branchDropCount = CountBranchSegments(segments, "branch drop");
        int branchTieInCount = CountBranchSegments(segments, "branch tie-in");
        foreach (PipeSegment segment in segments)
        {
            if (!string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double branchDiameterInches = ResolveSegmentDiameterInches(segment, DefaultBranchDiameterInches);
            string description = segment.Description ?? string.Empty;
            if (description.IndexOf("branch tie-in", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddJoint(
                    joints,
                    first,
                    PipeJointTypes.Tee,
                    branchDiameterInches,
                    segment.End,
                    FormatDiameterDescription(branchDiameterInches, " tee at cross main tie-in"));

                AddJoint(
                    joints,
                    first,
                    PipeJointTypes.Elbow,
                    branchDiameterInches,
                    segment.Start,
                    FormatDiameterDescription(branchDiameterInches, " elbow at branch tie-in"));
            }
            else if (description.IndexOf("branch drop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddJoint(
                    joints,
                    first,
                    PipeJointTypes.Elbow,
                    branchDiameterInches,
                    segment.End,
                    FormatDiameterDescription(branchDiameterInches, " elbow at branch drop"));
            }
        }

        // Align elbow counts with schematic takeoff when only drops or only tie-ins exist.
        if (branchDropCount > 0 && branchTieInCount == 0)
        {
            foreach (PipeSegment segment in segments.Where(segment =>
                         string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase)
                         && (segment.Description ?? string.Empty).IndexOf("branch drop", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                double branchDiameterInches = ResolveSegmentDiameterInches(segment, DefaultBranchDiameterInches);
                AddJoint(
                    joints,
                    first,
                    PipeJointTypes.Elbow,
                    branchDiameterInches,
                    segment.Start,
                    FormatDiameterDescription(branchDiameterInches, " elbow at sprinkler outlet"));
            }
        }

        return joints;
    }

    private static double ResolveMainDiameterInches(IEnumerable<PipeSegment> segments)
    {
        return segments
            .Where(segment => !IsBranchSegment(segment.SegmentType))
            .Select(segment => segment.DiameterInches)
            .Where(diameterInches => diameterInches > 0)
            .DefaultIfEmpty(DefaultMainDiameterInches)
            .Max();
    }

    private static double ResolveSegmentDiameterInches(PipeSegment segment, double fallbackDiameterInches)
    {
        return segment?.DiameterInches > 0 ? segment.DiameterInches : fallbackDiameterInches;
    }

    private static string FormatDiameterDescription(double diameterInches, string suffix)
    {
        return diameterInches.ToString("0.##") + "\"" + suffix;
    }

    private static void AddJoint(
        List<PipeJoint> joints,
        PipeSegment roomSegment,
        string jointType,
        double diameterInches,
        Point3D location,
        string description)
    {
        if (location == null)
        {
            return;
        }

        if (joints.Any(existing =>
                string.Equals(existing.JointType, jointType, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(existing.DiameterInches - diameterInches) < 0.01
                && LocationsMatch(existing.Location, location)))
        {
            return;
        }

        joints.Add(new PipeJoint
        {
            RoomRevitElementId = roomSegment.RoomRevitElementId,
            RoomNumber = roomSegment.RoomNumber,
            RoomName = roomSegment.RoomName,
            LevelName = roomSegment.LevelName,
            JointType = jointType,
            DiameterInches = diameterInches,
            Location = new Point3D(location.X, location.Y, location.Z),
            Description = description
        });
    }

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LocationsMatch(Point3D left, Point3D right)
    {
        return Math.Abs(left.X - right.X) <= LocationToleranceFeet
            && Math.Abs(left.Y - right.Y) <= LocationToleranceFeet
            && Math.Abs(left.Z - right.Z) <= LocationToleranceFeet;
    }

    private static int CountBranchSegments(IEnumerable<PipeSegment> segments, string descriptionToken)
    {
        return segments.Count(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase)
            && (segment.Description ?? string.Empty).IndexOf(descriptionToken, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
