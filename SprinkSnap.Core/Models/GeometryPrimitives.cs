using System;
using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public sealed class Point2D
{
    public Point2D()
    {
    }

    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class Point3D
{
    public Point3D()
    {
    }

    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }
}

public sealed class RoomBoundaryLoop
{
    public IList<Point3D> Points { get; set; } = new List<Point3D>();

    public bool IsClosed
    {
        get
        {
            if (Points.Count < 3)
            {
                return false;
            }

            Point3D first = Points[0];
            Point3D last = Points[Points.Count - 1];
            return Math.Abs(first.X - last.X) < 0.001
                && Math.Abs(first.Y - last.Y) < 0.001
                && Math.Abs(first.Z - last.Z) < 0.001;
        }
    }

    public IReadOnlyList<Point2D> ToPlanPolygon()
    {
        return Points.Select(point => new Point2D(point.X, point.Y)).ToList();
    }
}

