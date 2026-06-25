using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class ProjectTrunkRouter
{
    public const int ProjectScopeRoomRevitElementId = 0;

    private const double MainDiameterInches = 4.0;

    private const double LocationToleranceFeet = 0.15;

    public static void EnsureProjectTrunk(
        SchematicPipeRoutingSummary summary,
        IEnumerable<RoomInfo> rooms,
        HydraulicSupplyAnchor supplyAnchor = null)
    {
        if (summary == null)
        {
            return;
        }

        if (supplyAnchor?.IsSet == true)
        {
            RemoveProjectTrunkSegments(summary);
            summary.UsesProjectTrunk = false;
            AppendProjectTrunk(summary, rooms, supplyAnchor);
            return;
        }

        if (summary.UsesProjectTrunk)
        {
            return;
        }

        AppendProjectTrunk(summary, rooms, null);
    }

    public static void AppendProjectTrunk(
        SchematicPipeRoutingSummary summary,
        IEnumerable<RoomInfo> rooms,
        HydraulicSupplyAnchor supplyAnchor = null)
    {
        if (summary == null)
        {
            return;
        }

        IList<RoomTrunkTap> taps = CollectRoomTrunkTaps(summary, rooms);
        if (taps.Count < 2)
        {
            if (supplyAnchor?.IsSet == true)
            {
                summary.SupplyPoint = supplyAnchor.SupplyPoint;
                summary.UsesUserSupplyAnchor = true;
                summary.UserSupplyAnchorLabel = supplyAnchor.ElementLabel ?? string.Empty;
            }

            return;
        }

        IList<IGrouping<string, RoomTrunkTap>> levelGroups = taps
            .GroupBy(tap => tap.LevelKey)
            .Where(group => group.Count() >= 2)
            .ToList();
        if (levelGroups.Count == 0)
        {
            return;
        }

        foreach (IGrouping<string, RoomTrunkTap> levelGroup in levelGroups)
        {
            AppendLevelTrunk(summary, levelGroup.ToList(), supplyAnchor);
        }
    }

    public static void RemoveProjectTrunkSegments(SchematicPipeRoutingSummary summary)
    {
        if (summary?.Segments == null)
        {
            return;
        }

        List<PipeSegment> remaining = summary.Segments
            .Where(segment => segment.RoomRevitElementId != ProjectScopeRoomRevitElementId)
            .ToList();
        summary.Segments.Clear();
        foreach (PipeSegment segment in remaining)
        {
            summary.Segments.Add(segment);
        }

        summary.TotalSegmentCount = summary.Segments.Count;
        summary.TotalLengthFeet = summary.Segments.Sum(segment => segment.LengthFeet);
    }

    public static double ComputePathDistanceFromSupplyFeet(
        SchematicPipeRoutingSummary summary,
        int roomRevitElementId,
        Point3D sprinklerLocation)
    {
        if (summary == null || roomRevitElementId <= 0 || !summary.UsesProjectTrunk)
        {
            return 0.0;
        }

        RoomTrunkTap roomTap = FindRoomTap(summary, roomRevitElementId);
        if (roomTap == null)
        {
            return 0.0;
        }

        Point3D tieInPoint = ResolveTieInPoint(summary, roomRevitElementId, sprinklerLocation, roomTap);
        double distance = OrthogonalLengthFeet(tieInPoint, roomTap.HeaderPoint);
        if (!PointsMatch(roomTap.HeaderPoint, ResolveSupplyHeader(summary)))
        {
            distance += OrthogonalLengthFeet(roomTap.HeaderPoint, ResolveSupplyHeader(summary));
        }

        return distance;
    }

    public static RoomTrunkTap FindRoomTap(SchematicPipeRoutingSummary summary, int roomRevitElementId)
    {
        PipeSegment riser = summary?.Segments?
            .FirstOrDefault(segment =>
                segment.RoomRevitElementId == roomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        if (riser == null)
        {
            return null;
        }

        return new RoomTrunkTap
        {
            RoomRevitElementId = roomRevitElementId,
            LevelKey = BuildLevelKey(riser.LevelName, riser.Start.Z),
            HeaderPoint = ClonePoint(riser.End),
            FloorPoint = ClonePoint(riser.Start),
            BranchElevationFeet = riser.End.Z
        };
    }

    public static Point3D ResolveSupplyHeader(SchematicPipeRoutingSummary summary)
    {
        PipeSegment buildingRiser = summary?.Segments?
            .FirstOrDefault(segment =>
                segment.RoomRevitElementId == ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase)
                && (segment.Description ?? string.Empty).IndexOf("building riser", StringComparison.OrdinalIgnoreCase) >= 0);
        if (buildingRiser != null)
        {
            return ClonePoint(buildingRiser.End);
        }

        return new Point3D(
            summary.SupplyPoint.X,
            summary.SupplyPoint.Y,
            summary.SupplyPoint.Z + 9.0);
    }

    private static void AppendLevelTrunk(
        SchematicPipeRoutingSummary summary,
        IList<RoomTrunkTap> taps,
        HydraulicSupplyAnchor supplyAnchor)
    {
        RoomTrunkTap supplyTap = taps
            .OrderBy(tap => tap.FloorPoint.X + tap.FloorPoint.Y)
            .ThenBy(tap => tap.FloorPoint.X)
            .First();
        double branchElevationFeet = taps.Max(tap => tap.BranchElevationFeet);
        Point3D supplyFloor = supplyAnchor?.IsSet == true
            ? ClonePoint(supplyAnchor.SupplyPoint)
            : ClonePoint(supplyTap.FloorPoint);
        Point3D supplyHeader = supplyAnchor?.IsSet == true
            ? ClonePoint(supplyAnchor.HeaderPoint)
            : new Point3D(supplyFloor.X, supplyFloor.Y, branchElevationFeet);

        summary.Segments.Add(CreateProjectSegment(
            PipeSegmentTypes.Riser,
            supplyFloor,
            supplyHeader,
            "4\" building riser"));
        summary.SupplyPoint = supplyFloor;
        if (supplyAnchor?.IsSet == true)
        {
            summary.UsesUserSupplyAnchor = true;
            summary.UserSupplyAnchorLabel = supplyAnchor.ElementLabel ?? string.Empty;
        }

        foreach (RoomTrunkTap tap in taps)
        {
            if (PointsMatch(tap.HeaderPoint, supplyHeader))
            {
                continue;
            }

            summary.Segments.Add(CreateProjectSegment(
                PipeSegmentTypes.Main,
                tap.HeaderPoint,
                supplyHeader,
                "4\" project trunk to supply"));
        }

        summary.UsesProjectTrunk = true;
        summary.ProjectTrunkRoomRevitElementIds = taps
            .Select(tap => tap.RoomRevitElementId)
            .Distinct()
            .ToList();
        summary.TotalSegmentCount = summary.Segments.Count;
        summary.TotalLengthFeet = summary.Segments.Sum(segment => segment.LengthFeet);
        summary.Messages.Add(
            supplyAnchor?.IsSet == true
                ? "Connected "
                  + taps.Count
                  + " room(s) with a user-picked supply anchor and project trunk main(s)."
                : "Connected "
                  + taps.Count
                  + " room(s) on "
                  + supplyTap.LevelKey
                  + " with a shared building riser and project trunk main(s).");
    }

    private static IList<RoomTrunkTap> CollectRoomTrunkTaps(
        SchematicPipeRoutingSummary summary,
        IEnumerable<RoomInfo> rooms)
    {
        List<RoomTrunkTap> taps = new List<RoomTrunkTap>();
        HashSet<int> roomIds = (rooms ?? Enumerable.Empty<RoomInfo>())
            .Select(room => room.RevitElementId)
            .Where(id => id > 0)
            .ToHashSet();

        foreach (int roomId in summary.Segments
            .Select(segment => segment.RoomRevitElementId)
            .Where(id => id > 0)
            .Distinct())
        {
            if (roomIds.Count > 0 && !roomIds.Contains(roomId))
            {
                continue;
            }

            RoomTrunkTap tap = FindRoomTap(summary, roomId);
            if (tap != null)
            {
                taps.Add(tap);
            }
        }

        return taps;
    }

    private static Point3D ResolveTieInPoint(
        SchematicPipeRoutingSummary summary,
        int roomRevitElementId,
        Point3D sprinklerLocation,
        RoomTrunkTap roomTap)
    {
        PipeSegment crossMain = summary.Segments?
            .FirstOrDefault(segment =>
                segment.RoomRevitElementId == roomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));
        double crossMainY = crossMain?.Start.Y ?? roomTap.HeaderPoint.Y;
        return new Point3D(sprinklerLocation.X, crossMainY, roomTap.BranchElevationFeet);
    }

    private static PipeSegment CreateProjectSegment(
        string segmentType,
        Point3D start,
        Point3D end,
        string description)
    {
        return new PipeSegment
        {
            RoomRevitElementId = ProjectScopeRoomRevitElementId,
            SegmentType = segmentType,
            DiameterInches = MainDiameterInches,
            Start = start,
            End = end,
            LengthFeet = OrthogonalLengthFeet(start, end),
            Description = description
        };
    }

    private static string BuildLevelKey(string levelName, double floorElevationFeet)
    {
        string normalizedLevel = string.IsNullOrWhiteSpace(levelName) ? "Level" : levelName.Trim();
        return normalizedLevel + "@" + floorElevationFeet.ToString("0.###");
    }

    private static Point3D ClonePoint(Point3D point)
    {
        return new Point3D(point.X, point.Y, point.Z);
    }

    private static bool PointsMatch(Point3D left, Point3D right)
    {
        return Math.Abs(left.X - right.X) <= LocationToleranceFeet
            && Math.Abs(left.Y - right.Y) <= LocationToleranceFeet
            && Math.Abs(left.Z - right.Z) <= LocationToleranceFeet;
    }

    private static double OrthogonalLengthFeet(Point3D start, Point3D end)
    {
        return Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y) + Math.Abs(end.Z - start.Z);
    }
}

public sealed class RoomTrunkTap
{
    public int RoomRevitElementId { get; set; }

    public string LevelKey { get; set; } = string.Empty;

    public Point3D HeaderPoint { get; set; } = new Point3D();

    public Point3D FloorPoint { get; set; } = new Point3D();

    public double BranchElevationFeet { get; set; }
}
