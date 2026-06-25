using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicSupplyAnchorService
{
    private const double LocationToleranceFeet = 0.15;

    public static void PrepareRouting(
        SchematicPipeRoutingSummary summary,
        IEnumerable<RoomInfo> rooms,
        HydraulicSupplyAnchor supplyAnchor,
        PipePlacementSummary pipePlacementSummary)
    {
        if (summary == null)
        {
            return;
        }

        HydraulicSupplyAnchor resolvedAnchor = EnrichFromPlacedPipes(supplyAnchor, pipePlacementSummary);
        if (resolvedAnchor?.IsSet != true)
        {
            return;
        }

        summary.SupplyPoint = resolvedAnchor.SupplyPoint;
        summary.UsesUserSupplyAnchor = true;
        summary.UserSupplyAnchorLabel = resolvedAnchor.ElementLabel ?? string.Empty;
        ProjectTrunkRouter.EnsureProjectTrunk(summary, rooms, resolvedAnchor);
        UpdateBuildingRiserSegment(summary, resolvedAnchor);
    }

    public static Point3D ResolveSourcePoint(
        IEnumerable<RoomInfo> controllingRooms,
        SchematicPipeRoutingSummary schematicPipeRouting,
        HydraulicSupplyAnchor supplyAnchor)
    {
        HydraulicSupplyAnchor resolvedAnchor = EnrichAnchor(supplyAnchor, schematicPipeRouting);
        if (resolvedAnchor?.IsSet == true)
        {
            return resolvedAnchor.SupplyPoint;
        }

        if (schematicPipeRouting?.UsesProjectTrunk == true)
        {
            return schematicPipeRouting.SupplyPoint;
        }

        return HydraulicGraphBuilder.ResolveSourcePoint(controllingRooms, schematicPipeRouting);
    }

    public static Point3D ResolveSupplyHeader(
        SchematicPipeRoutingSummary schematicPipeRouting,
        HydraulicSupplyAnchor supplyAnchor,
        PipePlacementSummary pipePlacementSummary)
    {
        HydraulicSupplyAnchor resolvedAnchor = EnrichFromPlacedPipes(supplyAnchor, pipePlacementSummary);
        if (resolvedAnchor?.IsSet == true)
        {
            return resolvedAnchor.HeaderPoint;
        }

        return ProjectTrunkRouter.ResolveSupplyHeader(schematicPipeRouting);
    }

    public static HydraulicSupplyAnchor EnrichFromPlacedPipes(
        HydraulicSupplyAnchor supplyAnchor,
        PipePlacementSummary pipePlacementSummary)
    {
        if (supplyAnchor?.IsSet != true || supplyAnchor.RevitElementId <= 0)
        {
            return supplyAnchor;
        }

        PipePlacementSegmentResult placedSegment = FindPlacedSegment(pipePlacementSummary, supplyAnchor.RevitElementId);
        if (placedSegment == null || !placedSegment.HasTopology)
        {
            return supplyAnchor;
        }

        Point3D supplyPoint = supplyAnchor.SupplyPoint;
        Point3D headerPoint = supplyAnchor.HeaderPoint;
        if (supplyPoint.Z > headerPoint.Z || PointsMatch(supplyPoint, headerPoint))
        {
            supplyPoint = placedSegment.Start.Z <= placedSegment.End.Z
                ? placedSegment.Start
                : placedSegment.End;
            headerPoint = placedSegment.Start.Z <= placedSegment.End.Z
                ? placedSegment.End
                : placedSegment.Start;
        }

        return new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = supplyAnchor.RevitElementId,
            ElementLabel = supplyAnchor.ElementLabel,
            SupplyPoint = supplyPoint,
            HeaderPoint = headerPoint,
            SourceKind = "PlacedPipe"
        };
    }

    public static HydraulicSupplyAnchor CreateFromPipeEndpoints(
        int revitElementId,
        string elementLabel,
        Point3D start,
        Point3D end,
        string sourceKind)
    {
        Point3D supplyPoint = start.Z <= end.Z ? start : end;
        Point3D headerPoint = start.Z <= end.Z ? end : start;
        return new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = revitElementId,
            ElementLabel = elementLabel ?? string.Empty,
            SupplyPoint = supplyPoint,
            HeaderPoint = headerPoint,
            SourceKind = sourceKind ?? "UserPick"
        };
    }

    private static HydraulicSupplyAnchor EnrichAnchor(
        HydraulicSupplyAnchor supplyAnchor,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        if (supplyAnchor?.IsSet == true)
        {
            return supplyAnchor;
        }

        if (schematicPipeRouting?.UsesUserSupplyAnchor == true)
        {
            return new HydraulicSupplyAnchor
            {
                IsSet = true,
                SupplyPoint = schematicPipeRouting.SupplyPoint,
                HeaderPoint = ProjectTrunkRouter.ResolveSupplyHeader(schematicPipeRouting),
                ElementLabel = schematicPipeRouting.UserSupplyAnchorLabel ?? string.Empty,
                SourceKind = "Persisted"
            };
        }

        return supplyAnchor;
    }

    private static void UpdateBuildingRiserSegment(
        SchematicPipeRoutingSummary summary,
        HydraulicSupplyAnchor supplyAnchor)
    {
        PipeSegment buildingRiser = summary.Segments?
            .FirstOrDefault(segment =>
                segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        if (buildingRiser == null)
        {
            return;
        }

        buildingRiser.Start = supplyAnchor.SupplyPoint;
        buildingRiser.End = supplyAnchor.HeaderPoint;
        buildingRiser.LengthFeet = OrthogonalLengthFeet(buildingRiser.Start, buildingRiser.End);
        summary.SupplyPoint = supplyAnchor.SupplyPoint;
    }

    private static PipePlacementSegmentResult FindPlacedSegment(
        PipePlacementSummary pipePlacementSummary,
        int revitElementId)
    {
        foreach (PipePlacementRoomResult roomResult in pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
        {
            PipePlacementSegmentResult match = roomResult.PlacedSegments?
                .FirstOrDefault(segment => segment.PlacedElementId == revitElementId);
            if (match != null)
            {
                return match;
            }
        }

        return null;
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
