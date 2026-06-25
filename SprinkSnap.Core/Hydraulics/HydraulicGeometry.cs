using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

internal static class HydraulicGeometry
{
    public static double DistanceFeet(Point3D from, Point3D to)
    {
        double deltaX = to.X - from.X;
        double deltaY = to.Y - from.Y;
        double deltaZ = to.Z - from.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    public static double HorizontalDistanceFeet(Point3D from, Point3D to)
    {
        double deltaX = to.X - from.X;
        double deltaY = to.Y - from.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    public static Point3D ResolveBranchJunction(RoomInfo room)
    {
        double junctionZ = room.CeilingElevationFeet > 0
            ? room.CeilingElevationFeet - 1.0
            : room.FloorElevationFeet + Math.Max(room.CeilingHeightFeet - 1.0, 8.0);

        return new Point3D(room.Centroid.X, room.Centroid.Y, junctionZ);
    }

    public static Point3D ResolveSourcePoint(RoomInfo room)
    {
        return new Point3D(room.Centroid.X, room.Centroid.Y, room.FloorElevationFeet);
    }
}
