using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface IRoomAnalyzer
{
    void Analyze(RoomInfo room);
}

public sealed class RoomAnalyzer : IRoomAnalyzer
{
    private const double GeometryTolerance = 0.01;

    public void Analyze(RoomInfo room)
    {
        IReadOnlyList<Point2D> polygon = GetPrimaryPolygon(room);
        room.BoundaryPolygon = polygon;

        if (polygon.Count < 3)
        {
            room.HasIrregularGeometry = true;
            room.RoomShape = "Unknown";
            return;
        }

        room.AreaSquareFeet = CalculatePolygonArea(polygon);
        room.PerimeterFeet = CalculatePerimeter(polygon);
        room.Centroid = CalculateCentroid(polygon);

        (double width, double length) = CalculateBoundingDimensions(polygon);
        room.WidthFeet = width;
        room.LengthFeet = length;
        room.AspectRatio = width > GeometryTolerance ? length / width : 0.0;

        room.Slope = CalculateCeilingSlope(room);
        room.HasFlatCeiling = Math.Abs(room.Slope) < 0.02;
        room.HasSlopedCeiling = !room.HasFlatCeiling;
        room.HasMultiSlopeCeiling = false;
        room.HasIrregularGeometry = IsIrregularRoom(polygon, room.AreaSquareFeet, width, length);
        room.RoomShape = DetermineRoomShape(room);
        room.ElevationBelowDeckFeet = Math.Max(0.0, room.DeckElevationFeet - room.CeilingElevationFeet);
    }

    private static IReadOnlyList<Point2D> GetPrimaryPolygon(RoomInfo room)
    {
        RoomBoundaryLoop primaryLoop = room.Boundaries
            .Where(loop => loop.Points.Count >= 3)
            .OrderByDescending(loop => Math.Abs(CalculatePolygonArea(loop.ToPlanPolygon())))
            .FirstOrDefault();

        if (primaryLoop == null)
        {
            return new List<Point2D>();
        }

        List<Point2D> polygon = primaryLoop.ToPlanPolygon().ToList();
        if (polygon.Count > 1 && AreSamePoint(polygon[0], polygon[polygon.Count - 1]))
        {
            polygon.RemoveAt(polygon.Count - 1);
        }

        return polygon;
    }

    private static double CalculatePolygonArea(IReadOnlyList<Point2D> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0.0;
        }

        double area = 0.0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Point2D current = polygon[i];
            Point2D next = polygon[(i + 1) % polygon.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return Math.Abs(area) / 2.0;
    }

    private static double CalculatePerimeter(IReadOnlyList<Point2D> polygon)
    {
        double perimeter = 0.0;
        for (int i = 0; i < polygon.Count; i++)
        {
            perimeter += Distance(polygon[i], polygon[(i + 1) % polygon.Count]);
        }

        return perimeter;
    }

    private static Point2D CalculateCentroid(IReadOnlyList<Point2D> polygon)
    {
        double signedArea = 0.0;
        double centroidX = 0.0;
        double centroidY = 0.0;

        for (int i = 0; i < polygon.Count; i++)
        {
            Point2D current = polygon[i];
            Point2D next = polygon[(i + 1) % polygon.Count];
            double cross = current.X * next.Y - next.X * current.Y;
            signedArea += cross;
            centroidX += (current.X + next.X) * cross;
            centroidY += (current.Y + next.Y) * cross;
        }

        signedArea *= 0.5;
        if (Math.Abs(signedArea) < GeometryTolerance)
        {
            return new Point2D(polygon.Average(point => point.X), polygon.Average(point => point.Y));
        }

        return new Point2D(centroidX / (6.0 * signedArea), centroidY / (6.0 * signedArea));
    }

    private static (double Width, double Length) CalculateBoundingDimensions(IReadOnlyList<Point2D> polygon)
    {
        double minX = polygon.Min(point => point.X);
        double maxX = polygon.Max(point => point.X);
        double minY = polygon.Min(point => point.Y);
        double maxY = polygon.Max(point => point.Y);

        double xDimension = maxX - minX;
        double yDimension = maxY - minY;
        return (Math.Min(xDimension, yDimension), Math.Max(xDimension, yDimension));
    }

    private static double CalculateCeilingSlope(RoomInfo room)
    {
        if (room.WidthFeet <= GeometryTolerance)
        {
            return 0.0;
        }

        double elevationChange = Math.Max(0.0, room.DeckElevationFeet - room.CeilingElevationFeet);
        return elevationChange / room.WidthFeet;
    }

    private static bool IsIrregularRoom(
        IReadOnlyList<Point2D> polygon,
        double area,
        double width,
        double length)
    {
        if (polygon.Count > 6)
        {
            return true;
        }

        double boundingArea = width * length;
        if (boundingArea < GeometryTolerance)
        {
            return true;
        }

        double fillRatio = area / boundingArea;
        return fillRatio < 0.85 || fillRatio > 1.05;
    }

    private static string DetermineRoomShape(RoomInfo room)
    {
        if (room.HasIrregularGeometry)
        {
            return "Irregular";
        }

        if (room.AspectRatio > 2.5)
        {
            return "Long Narrow";
        }

        return "Rectangular";
    }

    private static double Distance(Point2D first, Point2D second)
    {
        double deltaX = second.X - first.X;
        double deltaY = second.Y - first.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static bool AreSamePoint(Point2D first, Point2D second)
    {
        return Distance(first, second) < GeometryTolerance;
    }
}

