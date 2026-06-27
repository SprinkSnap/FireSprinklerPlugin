using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeRouter
{
    public static SchematicPipeRoutingSummary RouteProject(
        IEnumerable<RoomInfo> rooms,
        SprinkSnapProjectPreferences preferences = null)
    {
        double branchDiameterInches = PipeDiameterDefaults.ResolveBranchDiameterInches(preferences);
        double mainDiameterInches = PipeDiameterDefaults.ResolveMainDiameterInches(preferences);
        string pipingSystemType = PipeScheduleDefaults.ResolvePipingSystemType(preferences);
        SchematicPipeRoutingSummary summary = new SchematicPipeRoutingSummary
        {
            PipingSystemType = pipingSystemType,
            PipeSchedule = PipeScheduleDefaults.ResolvePipeSchedule(preferences)
        };

        foreach (RoomInfo room in rooms ?? Enumerable.Empty<RoomInfo>())
        {
            IList<PipeSegment> roomSegments = RouteRoom(room, branchDiameterInches, mainDiameterInches, pipingSystemType);
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
                + " "
                + pipingSystemType
                + " schematic pipe segment(s) totaling "
                + summary.TotalLengthFeet.ToString("N0")
                + " ft ("
                + summary.PipeSchedule
                + ").");
        }
        else
        {
            summary.Messages.Add("No sprinkler layout coordinates were available for schematic pipe routing.");
        }

        ProjectTrunkRouter.AppendProjectTrunk(summary, rooms, null, mainDiameterInches);
        summary.TotalSegmentCount = summary.Segments.Count;
        summary.TotalLengthFeet = summary.Segments.Sum(segment => segment.LengthFeet);

        return summary;
    }

    public static IList<PipeSegment> RouteRoom(RoomInfo room, SprinkSnapProjectPreferences preferences = null)
    {
        return RouteRoom(
            room,
            PipeDiameterDefaults.ResolveBranchDiameterInches(preferences),
            PipeDiameterDefaults.ResolveMainDiameterInches(preferences),
            PipeScheduleDefaults.ResolvePipingSystemType(preferences));
    }

    public static IList<PipeSegment> RouteRoom(
        RoomInfo room,
        double branchDiameterInches,
        double mainDiameterInches,
        string pipingSystemType)
    {
        if (PipingSystemTypes.IsGrid(pipingSystemType))
        {
            return RouteRoomGrid(room, branchDiameterInches, mainDiameterInches);
        }

        return RouteRoomTree(room, branchDiameterInches, mainDiameterInches);
    }

    private static IList<PipeSegment> RouteRoomTree(
        RoomInfo room,
        double branchDiameterInches,
        double mainDiameterInches)
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

        double branchElevationFeet = ResolveBranchElevationFeet(room, headLocations);
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
            mainDiameterInches,
            riserBase,
            riserTop,
            PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " tree riser"));

        if (HorizontalDistance(crossMainStart, crossMainEnd) > 0.5)
        {
            segments.Add(CreateSegment(
                room,
                PipeSegmentTypes.CrossMain,
                mainDiameterInches,
                crossMainStart,
                crossMainEnd,
                PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " tree cross main"));
        }

        AppendHeadBranchSegments(
            segments,
            room,
            headLocations,
            branchDiameterInches,
            branchElevationFeet,
            crossMainY,
            "tree");

        return segments;
    }

    private static IList<PipeSegment> RouteRoomGrid(
        RoomInfo room,
        double branchDiameterInches,
        double mainDiameterInches)
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

        double branchElevationFeet = ResolveBranchElevationFeet(room, headLocations);
        double floorElevationFeet = room.FloorElevationFeet;
        double minHeadX = headLocations.Min(point => point.X);
        double maxHeadX = headLocations.Max(point => point.X);
        double minHeadY = headLocations.Min(point => point.Y);
        double maxHeadY = headLocations.Max(point => point.Y);

        if (Math.Abs(maxHeadY - minHeadY) < 0.5)
        {
            return RouteRoomTree(room, branchDiameterInches, mainDiameterInches);
        }

        double gridFeederX = minHeadX;
        Point3D riserBase = new Point3D(gridFeederX, (minHeadY + maxHeadY) / 2.0, floorElevationFeet);
        Point3D riserTop = new Point3D(gridFeederX, minHeadY, branchElevationFeet);
        Point3D lowerCrossMainStart = new Point3D(minHeadX, minHeadY, branchElevationFeet);
        Point3D lowerCrossMainEnd = new Point3D(maxHeadX, minHeadY, branchElevationFeet);
        Point3D upperCrossMainStart = new Point3D(minHeadX, maxHeadY, branchElevationFeet);
        Point3D upperCrossMainEnd = new Point3D(maxHeadX, maxHeadY, branchElevationFeet);
        Point3D gridFeederTop = new Point3D(gridFeederX, maxHeadY, branchElevationFeet);

        segments.Add(CreateSegment(
            room,
            PipeSegmentTypes.Riser,
            mainDiameterInches,
            riserBase,
            riserTop,
            PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " grid riser"));

        if (OrthogonalLengthFeet(riserTop, gridFeederTop) > 0.5)
        {
            segments.Add(CreateSegment(
                room,
                PipeSegmentTypes.Main,
                mainDiameterInches,
                riserTop,
                gridFeederTop,
                PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " grid feeder"));
        }

        segments.Add(CreateSegment(
            room,
            PipeSegmentTypes.CrossMain,
            mainDiameterInches,
            lowerCrossMainStart,
            lowerCrossMainEnd,
            PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " grid cross main (lower)"));

        segments.Add(CreateSegment(
            room,
            PipeSegmentTypes.CrossMain,
            mainDiameterInches,
            upperCrossMainStart,
            upperCrossMainEnd,
            PipeDiameterDefaults.FormatDiameterLabel(mainDiameterInches) + " grid cross main (upper)"));

        for (int index = 0; index < headLocations.Count; index++)
        {
            Point3D head = headLocations[index];
            double distanceToLower = Math.Abs(head.Y - minHeadY);
            double distanceToUpper = Math.Abs(head.Y - maxHeadY);
            double crossMainY = distanceToLower <= distanceToUpper ? minHeadY : maxHeadY;
            AppendSingleHeadBranchSegments(
                segments,
                room,
                head,
                index,
                branchDiameterInches,
                branchElevationFeet,
                crossMainY,
                "grid");
        }

        return segments;
    }

    private static void AppendHeadBranchSegments(
        IList<PipeSegment> segments,
        RoomInfo room,
        IReadOnlyList<Point3D> headLocations,
        double branchDiameterInches,
        double branchElevationFeet,
        double crossMainY,
        string routingLabel)
    {
        for (int index = 0; index < headLocations.Count; index++)
        {
            AppendSingleHeadBranchSegments(
                segments,
                room,
                headLocations[index],
                index,
                branchDiameterInches,
                branchElevationFeet,
                crossMainY,
                routingLabel);
        }
    }

    private static void AppendSingleHeadBranchSegments(
        IList<PipeSegment> segments,
        RoomInfo room,
        Point3D head,
        int index,
        double branchDiameterInches,
        double branchElevationFeet,
        double crossMainY,
        string routingLabel)
    {
        Point3D branchDropEnd = new Point3D(head.X, head.Y, branchElevationFeet);
        if (VerticalDistance(head, branchDropEnd) > 0.25)
        {
            segments.Add(CreateSegment(
                room,
                PipeSegmentTypes.Branch,
                branchDiameterInches,
                head,
                branchDropEnd,
                PipeDiameterDefaults.FormatDiameterLabel(branchDiameterInches) + " " + routingLabel + " branch drop #" + (index + 1)));
        }

        Point3D tieInPoint = new Point3D(head.X, crossMainY, branchElevationFeet);
        if (HorizontalDistance(branchDropEnd, tieInPoint) > 0.25)
        {
            segments.Add(CreateSegment(
                room,
                PipeSegmentTypes.Branch,
                branchDiameterInches,
                branchDropEnd,
                tieInPoint,
                PipeDiameterDefaults.FormatDiameterLabel(branchDiameterInches) + " " + routingLabel + " branch tie-in #" + (index + 1)));
        }
    }

    private static double ResolveBranchElevationFeet(RoomInfo room, IReadOnlyList<Point3D> headLocations)
    {
        double branchElevationFeet = headLocations.Average(point => point.Z);
        if (room.CeilingElevationFeet > 0)
        {
            branchElevationFeet = room.CeilingElevationFeet - 1.0;
        }

        return branchElevationFeet;
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
