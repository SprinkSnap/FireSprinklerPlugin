using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeSizingWritebackService
{
    private const double LocationToleranceFeet = 0.15;

    public static int WriteBackAppliedSizing(
        SchematicPipeRoutingSummary schematicPipeRouting,
        LayoutLinkedHydraulicPath path)
    {
        if (path?.UsesAppliedPipeSizing != true
            || schematicPipeRouting?.Segments == null
            || schematicPipeRouting.Segments.Count == 0)
        {
            return 0;
        }

        int updatedCount = path.UsesSegmentGraphHydraulics && path.SegmentChain?.Count > 0
            ? WriteBackSegmentChain(schematicPipeRouting, path.SegmentChain)
            : WriteBackFallbackSummary(schematicPipeRouting, path);

        if (updatedCount <= 0)
        {
            return 0;
        }

        schematicPipeRouting.UsesAppliedPipeSizing = true;
        schematicPipeRouting.AppliedPipeSizingSegmentCount = updatedCount;
        path.UsesSchematicPipeSizingWriteback = true;
        path.SchematicWritebackSegmentCount = updatedCount;
        path.Warnings.Add(
            "Wrote velocity-sized pipe diameters back to "
            + updatedCount
            + " schematic routing segment(s) for material takeoff and downstream design.");

        if (!schematicPipeRouting.Messages.Any(message =>
                message.IndexOf("velocity-sized pipe diameters", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            schematicPipeRouting.Messages.Add(
                "Updated "
                + updatedCount
                + " schematic pipe segment(s) with velocity-sized diameters from hydraulic calculation.");
        }

        return updatedCount;
    }

    private static int WriteBackSegmentChain(
        SchematicPipeRoutingSummary schematicPipeRouting,
        IList<HydraulicGraphSegment> segmentChain)
    {
        HashSet<int> affectedRoomIds = new HashSet<int>();
        int updatedCount = 0;

        foreach (HydraulicGraphSegment graphSegment in segmentChain)
        {
            PipeSegment matchingSegment = FindMatchingSegment(schematicPipeRouting.Segments, graphSegment);
            if (matchingSegment == null)
            {
                continue;
            }

            if (TryUpdateSegmentDiameter(matchingSegment, graphSegment.DiameterInches))
            {
                updatedCount++;
            }

            if (graphSegment.RoomRevitElementId > 0)
            {
                affectedRoomIds.Add(graphSegment.RoomRevitElementId);
            }
        }

        Dictionary<int, RoomDiameterTargets> roomTargets = BuildRoomDiameterTargets(segmentChain, affectedRoomIds);
        updatedCount += PropagateRoomDiameterTargets(schematicPipeRouting.Segments, roomTargets);
        updatedCount += PropagateProjectTrunkDiameters(schematicPipeRouting.Segments, segmentChain);
        return updatedCount;
    }

    private static int WriteBackFallbackSummary(
        SchematicPipeRoutingSummary schematicPipeRouting,
        LayoutLinkedHydraulicPath path)
    {
        int remoteRoomId = path.MostRemoteSprinkler?.Room?.RevitElementId ?? 0;
        if (remoteRoomId <= 0)
        {
            return 0;
        }

        int updatedCount = 0;
        foreach (PipeSegment segment in schematicPipeRouting.Segments.Where(segment => segment.RoomRevitElementId == remoteRoomId))
        {
            double targetDiameterInches = IsBranchSegment(segment.SegmentType)
                ? path.BranchDiameterInches
                : path.MainDiameterInches;
            if (TryUpdateSegmentDiameter(segment, targetDiameterInches))
            {
                updatedCount++;
            }
        }

        return updatedCount;
    }

    private static Dictionary<int, RoomDiameterTargets> BuildRoomDiameterTargets(
        IEnumerable<HydraulicGraphSegment> segmentChain,
        IEnumerable<int> affectedRoomIds)
    {
        Dictionary<int, RoomDiameterTargets> targets = new Dictionary<int, RoomDiameterTargets>();
        foreach (int roomRevitElementId in affectedRoomIds)
        {
            IEnumerable<HydraulicGraphSegment> roomSegments = segmentChain
                .Where(segment => segment.RoomRevitElementId == roomRevitElementId);
            targets[roomRevitElementId] = new RoomDiameterTargets
            {
                BranchDiameterInches = roomSegments
                    .Where(segment => IsBranchSegment(segment.SegmentType))
                    .Select(segment => segment.DiameterInches)
                    .DefaultIfEmpty(0)
                    .Max(),
                MainDiameterInches = roomSegments
                    .Where(segment => !IsBranchSegment(segment.SegmentType))
                    .Select(segment => segment.DiameterInches)
                    .DefaultIfEmpty(0)
                    .Max()
            };
        }

        return targets;
    }

    private static int PropagateRoomDiameterTargets(
        IList<PipeSegment> segments,
        IDictionary<int, RoomDiameterTargets> roomTargets)
    {
        int updatedCount = 0;
        foreach (KeyValuePair<int, RoomDiameterTargets> roomTarget in roomTargets)
        {
            foreach (PipeSegment segment in segments.Where(segment => segment.RoomRevitElementId == roomTarget.Key))
            {
                double targetDiameterInches = IsBranchSegment(segment.SegmentType)
                    ? roomTarget.Value.BranchDiameterInches
                    : roomTarget.Value.MainDiameterInches;
                if (targetDiameterInches <= 0)
                {
                    continue;
                }

                if (TryUpdateSegmentDiameter(segment, targetDiameterInches))
                {
                    updatedCount++;
                }
            }
        }

        return updatedCount;
    }

    private static int PropagateProjectTrunkDiameters(
        IList<PipeSegment> segments,
        IEnumerable<HydraulicGraphSegment> segmentChain)
    {
        double projectMainDiameterInches = segmentChain
            .Where(segment => segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId)
            .Where(segment => !IsBranchSegment(segment.SegmentType))
            .Select(segment => segment.DiameterInches)
            .DefaultIfEmpty(0)
            .Max();
        if (projectMainDiameterInches <= 0)
        {
            return 0;
        }

        int updatedCount = 0;
        foreach (PipeSegment segment in segments.Where(segment =>
                     segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId))
        {
            if (TryUpdateSegmentDiameter(segment, projectMainDiameterInches))
            {
                updatedCount++;
            }
        }

        return updatedCount;
    }

    private static PipeSegment FindMatchingSegment(
        IEnumerable<PipeSegment> segments,
        HydraulicGraphSegment graphSegment)
    {
        List<PipeSegment> candidates = segments
            .Where(segment =>
                segment.RoomRevitElementId == graphSegment.RoomRevitElementId
                && string.Equals(segment.SegmentType, graphSegment.SegmentType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PipeSegment descriptionMatch = candidates.FirstOrDefault(segment =>
            string.Equals(segment.Description, graphSegment.Description, StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment.Description, graphSegment.SegmentId, StringComparison.OrdinalIgnoreCase));
        if (descriptionMatch != null)
        {
            return descriptionMatch;
        }

        return candidates.FirstOrDefault(segment =>
            PointsMatch(segment.Start, graphSegment.Start)
            && PointsMatch(segment.End, graphSegment.End));
    }

    private static bool TryUpdateSegmentDiameter(PipeSegment segment, double diameterInches)
    {
        if (segment == null || diameterInches <= 0 || diameterInches <= segment.DiameterInches + 0.01)
        {
            return false;
        }

        segment.DiameterInches = diameterInches;
        UpdateDescriptionDiameter(segment, diameterInches);
        return true;
    }

    private static void UpdateDescriptionDiameter(PipeSegment segment, double diameterInches)
    {
        string description = segment.Description ?? string.Empty;
        int inchMarkIndex = description.IndexOf('"');
        if (inchMarkIndex <= 0)
        {
            return;
        }

        segment.Description = diameterInches.ToString("0.##") + description.Substring(inchMarkIndex);
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

    private sealed class RoomDiameterTargets
    {
        public double BranchDiameterInches { get; set; }

        public double MainDiameterInches { get; set; }
    }
}
