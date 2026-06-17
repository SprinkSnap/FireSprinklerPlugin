using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public interface IRoomBoundaryExtractor
{
    IReadOnlyList<RoomBoundaryLoop> ExtractBoundaryLoops(Room room);

    Point2D GenerateCentroid(IReadOnlyList<RoomBoundaryLoop> loops);
}

public sealed class RoomBoundaryExtractor : IRoomBoundaryExtractor
{
    private const double PointTolerance = 0.001;

    public IReadOnlyList<RoomBoundaryLoop> ExtractBoundaryLoops(Room room)
    {
        IList<IList<BoundarySegment>> boundarySegments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
        List<RoomBoundaryLoop> loops = new List<RoomBoundaryLoop>();

        if (boundarySegments == null)
        {
            return loops;
        }

        foreach (IList<BoundarySegment> segmentLoop in boundarySegments)
        {
            RoomBoundaryLoop loop = new RoomBoundaryLoop();

            foreach (BoundarySegment segment in segmentLoop)
            {
                Curve curve = segment.GetCurve();
                IList<XYZ> tessellatedPoints = curve.Tessellate();

                foreach (XYZ point in tessellatedPoints)
                {
                    Point3D convertedPoint = Convert(point);
                    if (loop.Points.Count == 0 || !AreSamePoint(loop.Points[loop.Points.Count - 1], convertedPoint))
                    {
                        loop.Points.Add(convertedPoint);
                    }
                }
            }

            CloseLoop(loop);
            if (loop.Points.Count >= 4)
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    public Point2D GenerateCentroid(IReadOnlyList<RoomBoundaryLoop> loops)
    {
        RoomInfo roomInfo = new RoomInfo
        {
            Boundaries = loops.ToList()
        };

        new RoomAnalyzer().Analyze(roomInfo);
        return roomInfo.Centroid;
    }

    private static Point3D Convert(XYZ point)
    {
        return new Point3D(point.X, point.Y, point.Z);
    }

    private static void CloseLoop(RoomBoundaryLoop loop)
    {
        if (loop.Points.Count == 0)
        {
            return;
        }

        Point3D first = loop.Points[0];
        Point3D last = loop.Points[loop.Points.Count - 1];

        if (!AreSamePoint(first, last))
        {
            loop.Points.Add(new Point3D(first.X, first.Y, first.Z));
        }
    }

    private static bool AreSamePoint(Point3D first, Point3D second)
    {
        return Math.Abs(first.X - second.X) < PointTolerance
            && Math.Abs(first.Y - second.Y) < PointTolerance
            && Math.Abs(first.Z - second.Z) < PointTolerance;
    }
}

