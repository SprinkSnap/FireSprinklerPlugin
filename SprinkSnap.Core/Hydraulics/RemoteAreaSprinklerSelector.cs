using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class RemoteAreaRectangle
{
    public double CrossMainMin { get; set; }

    public double CrossMainMax { get; set; }

    public double BranchMin { get; set; }

    public double BranchMax { get; set; }

    public bool CrossMainAlongX { get; set; }
}

public static class RemoteAreaSprinklerSelector
{
    private const double MinimumBranchDepthFeet = 8.0;

    public static IList<LayoutSprinklerPoint> SelectOperatingSprinklers(
        IEnumerable<LayoutSprinklerPoint> sprinklerPoints,
        Point3D sourcePoint,
        int operatingSprinklerCount,
        double remoteAreaSquareFeet,
        double maxCoverageSquareFeet,
        SchematicPipeRoutingSummary schematicPipeRouting = null)
    {
        List<LayoutSprinklerPoint> points = sprinklerPoints?.ToList() ?? new List<LayoutSprinklerPoint>();
        if (points.Count == 0)
        {
            return points;
        }

        int count = Math.Min(Math.Max(operatingSprinklerCount, 1), points.Count);
        if (remoteAreaSquareFeet <= 0 || maxCoverageSquareFeet <= 0)
        {
            return SelectBySourceDistance(points, sourcePoint, count);
        }

        if (points.Count <= count)
        {
            return points;
        }

        bool crossMainAlongX = ResolveCrossMainAlongX(points);
        double branchDepthFeet = Math.Max(
            MinimumBranchDepthFeet,
            Math.Sqrt(Math.Max(maxCoverageSquareFeet, 1.0)));
        double crossMainLengthFeet = remoteAreaSquareFeet / branchDepthFeet;
        RemoteAreaRectangle rectangle = BuildRemoteAreaRectangle(
            points,
            sourcePoint,
            crossMainAlongX,
            crossMainLengthFeet,
            branchDepthFeet);

        List<LayoutSprinklerPoint> insideRectangle = points
            .Where(point => IsInsideRectangle(point, rectangle))
            .ToList();

        if (insideRectangle.Count < count)
        {
            insideRectangle = ExpandSelection(
                points,
                insideRectangle,
                sourcePoint,
                crossMainAlongX,
                remoteAreaSquareFeet,
                maxCoverageSquareFeet,
                count,
                schematicPipeRouting);
        }

        if (insideRectangle.Count <= count)
        {
            return insideRectangle;
        }

        return insideRectangle
            .OrderByDescending(point => EstimatePipeRunFeet(point, sourcePoint, schematicPipeRouting))
            .ThenByDescending(point => CrossMainCoordinate(point, crossMainAlongX))
            .Take(count)
            .ToList();
    }

    public static LayoutSprinklerPoint ResolveMostRemoteSprinkler(
        IEnumerable<LayoutSprinklerPoint> operatingSprinklers,
        Point3D sourcePoint,
        SchematicPipeRoutingSummary schematicPipeRouting = null)
    {
        List<LayoutSprinklerPoint> sprinklers = operatingSprinklers?.ToList() ?? new List<LayoutSprinklerPoint>();
        if (sprinklers.Count == 0)
        {
            return null;
        }

        return sprinklers
            .OrderByDescending(point => EstimatePipeRunFeet(point, sourcePoint, schematicPipeRouting))
            .ThenByDescending(point => HydraulicGeometry.DistanceFeet(point.Location, sourcePoint))
            .First();
    }

    public static double EstimatePipeRunFeet(
        LayoutSprinklerPoint sprinkler,
        Point3D sourcePoint,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        if (sprinkler == null)
        {
            return 0.0;
        }

        int roomId = sprinkler.Room?.RevitElementId ?? 0;
        IList<PipeSegment> roomSegments = SchematicPipeRoutingService.GetSegmentsForRoom(schematicPipeRouting, roomId);
        if (roomSegments.Count == 0)
        {
            return HydraulicGeometry.DistanceFeet(sprinkler.Location, sourcePoint);
        }

        int headNumber = sprinkler.SprinklerIndex + 1;
        string dropToken = "branch drop #" + headNumber;
        string tieInToken = "branch tie-in #" + headNumber;
        double runFeet = 0.0;

        PipeSegment branchDrop = roomSegments.FirstOrDefault(segment =>
            (segment.Description ?? string.Empty).IndexOf(dropToken, StringComparison.OrdinalIgnoreCase) >= 0);
        PipeSegment branchTieIn = roomSegments.FirstOrDefault(segment =>
            (segment.Description ?? string.Empty).IndexOf(tieInToken, StringComparison.OrdinalIgnoreCase) >= 0);
        PipeSegment riser = roomSegments.FirstOrDefault(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        PipeSegment crossMain = roomSegments.FirstOrDefault(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));

        runFeet += branchDrop?.LengthFeet ?? 0.0;
        runFeet += branchTieIn?.LengthFeet ?? 0.0;

        if (crossMain != null && riser != null)
        {
            Point3D tieInPoint = branchTieIn?.End ?? branchDrop?.End ?? sprinkler.Location;
            runFeet += HorizontalRunAlongCrossMain(crossMain, riser.End, tieInPoint);
        }

        runFeet += riser?.LengthFeet ?? 0.0;

        if (schematicPipeRouting?.UsesProjectTrunk == true)
        {
            runFeet += ProjectTrunkRouter.ComputePathDistanceFromSupplyFeet(
                schematicPipeRouting,
                roomId,
                sprinkler.Location);
        }

        if (runFeet <= 0.0)
        {
            return HydraulicGeometry.DistanceFeet(sprinkler.Location, sourcePoint);
        }

        return runFeet;
    }

    private static IList<LayoutSprinklerPoint> SelectBySourceDistance(
        IList<LayoutSprinklerPoint> points,
        Point3D sourcePoint,
        int count)
    {
        return points
            .OrderByDescending(point => HydraulicGeometry.DistanceFeet(point.Location, sourcePoint))
            .Take(count)
            .ToList();
    }

    private static bool ResolveCrossMainAlongX(IList<LayoutSprinklerPoint> points)
    {
        double minX = points.Min(point => point.Location.X);
        double maxX = points.Max(point => point.Location.X);
        double minY = points.Min(point => point.Location.Y);
        double maxY = points.Max(point => point.Location.Y);
        return (maxX - minX) >= (maxY - minY);
    }

    private static RemoteAreaRectangle BuildRemoteAreaRectangle(
        IList<LayoutSprinklerPoint> points,
        Point3D sourcePoint,
        bool crossMainAlongX,
        double crossMainLengthFeet,
        double branchDepthFeet)
    {
        double sourceCrossMain = CrossMainCoordinate(sourcePoint, crossMainAlongX);
        List<double> crossMainCoordinates = points
            .Select(point => CrossMainCoordinate(point, crossMainAlongX))
            .ToList();
        double remoteCrossMain = crossMainCoordinates
            .OrderByDescending(coordinate => Math.Abs(coordinate - sourceCrossMain))
            .First();
        int direction = sourceCrossMain <= remoteCrossMain ? -1 : 1;
        double areaStart = remoteCrossMain;
        double areaEnd = remoteCrossMain + (direction * crossMainLengthFeet);

        double crossMainMin = Math.Min(areaStart, areaEnd);
        double crossMainMax = Math.Max(areaStart, areaEnd);
        List<LayoutSprinklerPoint> remoteBand = points
            .Where(point =>
            {
                double crossMainCoordinate = CrossMainCoordinate(point, crossMainAlongX);
                return crossMainCoordinate >= crossMainMin && crossMainCoordinate <= crossMainMax;
            })
            .ToList();

        if (remoteBand.Count == 0)
        {
            remoteBand = points.ToList();
        }

        double centerBranch = remoteBand.Average(point => BranchCoordinate(point, crossMainAlongX));
        return new RemoteAreaRectangle
        {
            CrossMainAlongX = crossMainAlongX,
            CrossMainMin = crossMainMin,
            CrossMainMax = crossMainMax,
            BranchMin = centerBranch - (branchDepthFeet / 2.0),
            BranchMax = centerBranch + (branchDepthFeet / 2.0)
        };
    }

    private static List<LayoutSprinklerPoint> ExpandSelection(
        IList<LayoutSprinklerPoint> points,
        IList<LayoutSprinklerPoint> currentSelection,
        Point3D sourcePoint,
        bool crossMainAlongX,
        double remoteAreaSquareFeet,
        double maxCoverageSquareFeet,
        int targetCount,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        HashSet<LayoutSprinklerPoint> selected = new HashSet<LayoutSprinklerPoint>(currentSelection);
        double branchDepthFeet = Math.Max(
            MinimumBranchDepthFeet,
            Math.Sqrt(Math.Max(maxCoverageSquareFeet, 1.0)));
        double crossMainLengthFeet = remoteAreaSquareFeet / branchDepthFeet;

        for (int expansion = 0; expansion < 4 && selected.Count < targetCount; expansion++)
        {
            crossMainLengthFeet *= 1.25;
            branchDepthFeet *= 1.15;
            RemoteAreaRectangle rectangle = BuildRemoteAreaRectangle(
                points,
                sourcePoint,
                crossMainAlongX,
                crossMainLengthFeet,
                branchDepthFeet);

            foreach (LayoutSprinklerPoint point in points.Where(candidate => IsInsideRectangle(candidate, rectangle)))
            {
                selected.Add(point);
            }
        }

        if (selected.Count >= targetCount)
        {
            return selected.ToList();
        }

        foreach (LayoutSprinklerPoint point in points
            .OrderByDescending(candidate => EstimatePipeRunFeet(candidate, sourcePoint, schematicPipeRouting)))
        {
            selected.Add(point);
            if (selected.Count >= targetCount)
            {
                break;
            }
        }

        return selected.ToList();
    }

    private static bool IsInsideRectangle(LayoutSprinklerPoint point, RemoteAreaRectangle rectangle)
    {
        double crossMainCoordinate = CrossMainCoordinate(point, rectangle.CrossMainAlongX);
        double branchCoordinate = BranchCoordinate(point, rectangle.CrossMainAlongX);
        return crossMainCoordinate >= rectangle.CrossMainMin
            && crossMainCoordinate <= rectangle.CrossMainMax
            && branchCoordinate >= rectangle.BranchMin
            && branchCoordinate <= rectangle.BranchMax;
    }

    private static double CrossMainCoordinate(LayoutSprinklerPoint point, bool crossMainAlongX)
    {
        return CrossMainCoordinate(point.Location, crossMainAlongX);
    }

    private static double CrossMainCoordinate(Point3D location, bool crossMainAlongX)
    {
        return crossMainAlongX ? location.X : location.Y;
    }

    private static double BranchCoordinate(LayoutSprinklerPoint point, bool crossMainAlongX)
    {
        return crossMainAlongX ? point.Location.Y : point.Location.X;
    }

    private static double HorizontalRunAlongCrossMain(PipeSegment crossMain, Point3D riserTop, Point3D tieInPoint)
    {
        double deltaX = Math.Abs(tieInPoint.X - riserTop.X);
        double deltaY = Math.Abs(tieInPoint.Y - riserTop.Y);
        if (Math.Abs(crossMain.End.X - crossMain.Start.X) >= Math.Abs(crossMain.End.Y - crossMain.Start.Y))
        {
            return deltaX;
        }

        return deltaY;
    }
}
