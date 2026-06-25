using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeRouter
{
    private const double BranchDiameterInches = 1.25;

    private const double MainDiameterInches = 4.0;

    public static SchematicPipeRoutingSummary RouteProject(IEnumerable<RoomInfo> rooms)
    {
        SchematicPipeRoutingSummary summary = new SchematicPipeRoutingSummary();
        foreach (RoomInfo room in rooms ?? Enumerable.Empty<RoomInfo>())
        {
            IList<PipeSegment> roomSegments = RouteRoom(room);
            foreach (PipeSegment segment in roomSegments)
            {
                summary.Segments.Add(segment);
            }
        }

        summary.TotalSegmentCount = summary.Segments.Count;
        summary.TotalLengthFeet = summary.Segments.Sum(segment => segment.LengthFeet);
        if (summary.TotalSegmentCount > 0)
        {
            summary.Messages.Add(
                "Generated "
                + summary.TotalSegmentCount
                + " schematic pipe segment(s) totaling "
                + summary.TotalLengthFeet.ToString("N0")
                + " ft.");
        }
        else
        {
            summary.Messages.Add("No sprinkler layout coordinates were available for schematic pipe routing.");
        }

        ProjectTrunkRouter.AppendProjectTrunk(summary, rooms);
        summary.TotalSegmentCount = summary.Segments.Count;
        summary.TotalLengthFeet = summary.Segments.Sum(segment => segment.LengthFeet);

        return summary;
    }

    public static IList<PipeSegment> RouteRoom(RoomInfo room)
    {
        List<PipeSegment> segments = new List<PipeSegment>();
        IList<SprinklerPlacementCandidate> heads = room?.ProposedSprinklers;
        if (heads == null || heads.Count == 0)
        {
            return segments;
        }

        List<Point3D> headLocations = heads
            .Select(candidate => candidate.Location ?? new Point3D())
            .ToList();

        double branchElevationFeet = headLocations.Average(point => point.Z);
        if (room.CeilingElevationFeet > 0)
        {
            branchElevationFeet = room.CeilingElevationFeet - 1.0;
        }

        double floorElevationFeet = room.FloorElevationFeet;
        double minHeadX = headLocations.Min(point => point.X);
        double maxHeadX = headLocations.Max(point => point.X);
        double crossMainY = headLocations.Average(point => point.Y);
        Point3D riserBase = new Point3D(minHeadX, crossMainY, floorElevationFeet);
        Point3D riserTop = new Point3D(minHeadX, crossMainY, branchElevationFeet);
        Point3D crossMainStart = riserTop;
        Point3D crossMainEnd = new Point3D(maxHeadX, crossMainY, branchElevationFeet);

        segments.Add(CreateSegment(
            room,
            PipeSegmentTypes.Riser,
            MainDiameterInches,
            riserBase,
            riserTop,
            MainDiameterInches.ToString("0.##") + "\" riser"));

        if (HorizontalDistance(crossMainStart, crossMainEnd) > 0.5)
        {
            segments.Add(CreateSegment(
                room,
                PipeSegmentTypes.CrossMain,
                MainDiameterInches,
                crossMainStart,
                crossMainEnd,
                MainDiameterInches.ToString("0.##") + "\" cross main"));
        }

        for (int index = 0; index < headLocations.Count; index++)
        {
            Point3D head = headLocations[index];
            Point3D branchDropEnd = new Point3D(head.X, head.Y, branchElevationFeet);
            if (VerticalDistance(head, branchDropEnd) > 0.25)
            {
                segments.Add(CreateSegment(
                    room,
                    PipeSegmentTypes.Branch,
                    BranchDiameterInches,
                    head,
                    branchDropEnd,
                    BranchDiameterInches.ToString("0.##") + "\" branch drop #" + (index + 1)));
            }

            Point3D tieInPoint = new Point3D(head.X, crossMainY, branchElevationFeet);
            if (HorizontalDistance(branchDropEnd, tieInPoint) > 0.25)
            {
                segments.Add(CreateSegment(
                    room,
                    PipeSegmentTypes.Branch,
                    BranchDiameterInches,
                    branchDropEnd,
                    tieInPoint,
                    BranchDiameterInches.ToString("0.##") + "\" branch tie-in #" + (index + 1)));
            }
        }

        return segments;
    }

    private static PipeSegment CreateSegment(
        RoomInfo room,
        string segmentType,
        double diameterInches,
        Point3D start,
        Point3D end,
        string description)
    {
        return new PipeSegment
        {
            RoomRevitElementId = room.RevitElementId,
            RoomNumber = room.Number,
            RoomName = room.Name,
            LevelName = room.LevelName,
            SegmentType = segmentType,
            DiameterInches = diameterInches,
            Start = start,
            End = end,
            LengthFeet = OrthogonalLengthFeet(start, end),
            Description = description
        };
    }

    private static double OrthogonalLengthFeet(Point3D start, Point3D end)
    {
        return Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y) + Math.Abs(end.Z - start.Z);
    }

    private static double HorizontalDistance(Point3D start, Point3D end)
    {
        return Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
    }

    private static double VerticalDistance(Point3D start, Point3D end)
    {
        return Math.Abs(end.Z - start.Z);
    }
}
