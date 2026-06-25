using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class PlacedPipeGraphBuilder
{
    private const double LocationToleranceFeet = 0.15;

    public static bool RoomHasPlacedTopology(PipePlacementRoomResult placedRoom)
    {
        if (placedRoom?.PlacedSegments == null || placedRoom.PlacedSegments.Count == 0)
        {
            return false;
        }

        int topologyCount = placedRoom.PlacedSegments.Count(segment => segment.HasTopology);
        return topologyCount >= 2
            && topologyCount >= Math.Max(2, placedRoom.PlacedSegments.Count / 2);
    }

    public static IList<HydraulicGraphSegment> BuildRoomSegments(
        PipePlacementRoomResult placedRoom,
        int roomRevitElementId,
        double defaultBranchDiameterInches,
        double defaultMainDiameterInches)
    {
        List<HydraulicGraphSegment> segments = new List<HydraulicGraphSegment>();
        foreach (PipePlacementSegmentResult placedSegment in placedRoom.PlacedSegments)
        {
            segments.Add(new HydraulicGraphSegment
            {
                SegmentId = string.IsNullOrWhiteSpace(placedSegment.Description)
                    ? placedSegment.SegmentType
                    : placedSegment.Description,
                Start = placedSegment.Start,
                End = placedSegment.End,
                LengthFeet = placedSegment.LengthFeet,
                DiameterInches = placedSegment.DiameterInches > 0
                    ? placedSegment.DiameterInches
                    : IsBranchSegment(placedSegment.SegmentType)
                        ? defaultBranchDiameterInches
                        : defaultMainDiameterInches,
                SegmentType = placedSegment.SegmentType,
                RoomRevitElementId = roomRevitElementId,
                Description = placedSegment.Description,
                DataSource = "Placed"
            });
        }

        return segments;
    }

    public static IList<HydraulicGraphSegment> TraceCriticalPath(
        LayoutSprinklerPoint remoteSprinkler,
        IList<HydraulicGraphSegment> roomSegments,
        Point3D sourcePoint)
    {
        if (roomSegments == null || roomSegments.Count == 0 || remoteSprinkler == null)
        {
            return new List<HydraulicGraphSegment>();
        }

        Point3D targetPoint = ResolveRoomTargetPoint(roomSegments, sourcePoint);
        HydraulicGraphSegment startSegment = FindNearestBranchSegment(remoteSprinkler.Location, roomSegments);
        if (startSegment == null)
        {
            return new List<HydraulicGraphSegment>();
        }

        OrientSegmentTowardHead(startSegment, remoteSprinkler.Location);
        List<HydraulicGraphSegment> chain = new List<HydraulicGraphSegment> { CloneSegment(startSegment) };
        Point3D currentPoint = startSegment.End;
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BuildSegmentKey(startSegment)
        };

        while (!PointsMatch(currentPoint, targetPoint) && chain.Count < roomSegments.Count + 2)
        {
            HydraulicGraphSegment nextSegment = FindNextSegmentTowardTarget(
                currentPoint,
                targetPoint,
                roomSegments,
                visited);
            if (nextSegment == null)
            {
                break;
            }

            OrientSegmentFromPoint(nextSegment, currentPoint);
            chain.Add(CloneSegment(nextSegment));
            visited.Add(BuildSegmentKey(nextSegment));
            currentPoint = nextSegment.End;

            if (string.Equals(nextSegment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return chain.Where(segment => segment.LengthFeet > 0.01).ToList();
    }

    private static Point3D ResolveRoomTargetPoint(IList<HydraulicGraphSegment> roomSegments, Point3D sourcePoint)
    {
        HydraulicGraphSegment riser = roomSegments
            .FirstOrDefault(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        if (riser != null)
        {
            double riserBaseDistanceToSource = Math.Min(
                DistanceFeet(riser.Start, sourcePoint),
                DistanceFeet(riser.End, sourcePoint));
            return riserBaseDistanceToSource <= DistanceFeet(riser.Start, riser.End) / 2.0
                ? riser.Start
                : riser.End;
        }

        return sourcePoint;
    }

    private static HydraulicGraphSegment FindNearestBranchSegment(
        Point3D sprinklerLocation,
        IList<HydraulicGraphSegment> roomSegments)
    {
        return roomSegments
            .Where(segment => IsBranchSegment(segment.SegmentType))
            .OrderBy(segment => Math.Min(
                DistanceFeet(sprinklerLocation, segment.Start),
                DistanceFeet(sprinklerLocation, segment.End)))
            .FirstOrDefault();
    }

    private static HydraulicGraphSegment FindNextSegmentTowardTarget(
        Point3D currentPoint,
        Point3D targetPoint,
        IList<HydraulicGraphSegment> roomSegments,
        ISet<string> visited)
    {
        HydraulicGraphSegment bestSegment = null;
        double bestScore = double.MaxValue;
        foreach (HydraulicGraphSegment segment in roomSegments)
        {
            string segmentKey = BuildSegmentKey(segment);
            if (visited.Contains(segmentKey))
            {
                continue;
            }

            if (!PointsMatch(segment.Start, currentPoint) && !PointsMatch(segment.End, currentPoint))
            {
                continue;
            }

            Point3D nextPoint = PointsMatch(segment.Start, currentPoint) ? segment.End : segment.Start;
            double score = DistanceFeet(nextPoint, targetPoint);
            if (score < bestScore)
            {
                bestScore = score;
                bestSegment = segment;
            }
        }

        return bestSegment;
    }

    private static void OrientSegmentTowardHead(HydraulicGraphSegment segment, Point3D headLocation)
    {
        if (DistanceFeet(headLocation, segment.End) < DistanceFeet(headLocation, segment.Start))
        {
            SwapEndpoints(segment);
        }
    }

    private static void OrientSegmentFromPoint(HydraulicGraphSegment segment, Point3D fromPoint)
    {
        if (!PointsMatch(segment.Start, fromPoint) && PointsMatch(segment.End, fromPoint))
        {
            SwapEndpoints(segment);
        }
    }

    private static void SwapEndpoints(HydraulicGraphSegment segment)
    {
        Point3D start = segment.Start;
        segment.Start = segment.End;
        segment.End = start;
    }

    private static HydraulicGraphSegment CloneSegment(HydraulicGraphSegment segment)
    {
        return new HydraulicGraphSegment
        {
            SegmentId = segment.SegmentId,
            Start = segment.Start,
            End = segment.End,
            LengthFeet = segment.LengthFeet,
            DiameterInches = segment.DiameterInches,
            SegmentType = segment.SegmentType,
            RoomRevitElementId = segment.RoomRevitElementId,
            Description = segment.Description,
            DataSource = segment.DataSource
        };
    }

    private static string BuildSegmentKey(HydraulicGraphSegment segment)
    {
        return segment.RoomRevitElementId
            + "|"
            + segment.SegmentId
            + "|"
            + segment.SegmentType
            + "|"
            + segment.Description;
    }

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PointsMatch(Point3D left, Point3D right)
    {
        return Math.Abs(left.X - right.X) <= LocationToleranceFeet
            && Math.Abs(left.Y - right.Y) <= LocationToleranceFeet
            && Math.Abs(left.Z - right.Z) <= LocationToleranceFeet;
    }

    private static double DistanceFeet(Point3D from, Point3D to)
    {
        double deltaX = to.X - from.X;
        double deltaY = to.Y - from.Y;
        double deltaZ = to.Z - from.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }
}
