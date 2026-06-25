using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicSegmentGraphBuilder
{
    private const double LocationToleranceFeet = 0.15;

    private const double MinimumBranchLengthFeet = 10.0;

    private const double MinimumMainLengthFeet = 20.0;

    public static void BuildSegmentChain(
        LayoutLinkedHydraulicPath path,
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary,
        IEnumerable<RoomInfo> controllingRooms = null)
    {
        if (path.OperatingSprinklers.Count == 0 || path.MostRemoteSprinkler == null)
        {
            return;
        }

        ProjectTrunkRouter.EnsureProjectTrunk(
            schematicPipeRouting,
            controllingRooms ?? path.OperatingSprinklers.Select(point => point.Room));

        LayoutSprinklerPoint remote = path.MostRemoteSprinkler;
        int remoteRoomId = remote.Room?.RevitElementId ?? 0;
        IList<HydraulicGraphSegment> roomSegments = PlacedPipeHydraulicResolver.ResolveSegmentsForRoom(
            remoteRoomId,
            pipePlacementSummary,
            schematicPipeRouting,
            path.BranchDiameterInches,
            path.MainDiameterInches);

        IList<HydraulicGraphSegment> chain;
        if (roomSegments.Count > 0)
        {
            chain = BuildChainFromRoomSegments(
                remote,
                roomSegments,
                path.SourcePoint,
                schematicPipeRouting);
            path.PipeLengthDataSource = roomSegments[0].DataSource;
            path.UsesPlacedPipeLengths = string.Equals(
                roomSegments[0].DataSource,
                "Placed",
                StringComparison.OrdinalIgnoreCase);
            path.UsesPlacedPipeTopology = chain.Any(segment =>
                string.Equals(segment.DataSource, "Placed", StringComparison.OrdinalIgnoreCase))
                && PlacedPipeGraphBuilder.RoomHasPlacedTopology(FindPlacedRoom(pipePlacementSummary, remoteRoomId));
        }
        else
        {
            chain = BuildGeometryChain(remote, path.SourcePoint, path.BranchDiameterInches, path.MainDiameterInches);
            path.PipeLengthDataSource = "Geometry";
        }

        if (chain.Count == 0)
        {
            return;
        }

        path.SegmentChain = chain;
        path.UsesSegmentGraphHydraulics = true;
        path.UsesProjectTrunk = schematicPipeRouting?.UsesProjectTrunk == true;
        path.CriticalPathSegmentCount = chain.Count;
        path.BranchLengthFeet = chain
            .Where(segment => IsBranchSegment(segment.SegmentType))
            .Sum(segment => segment.LengthFeet);
        path.MainLengthFeet = chain
            .Where(segment => !IsBranchSegment(segment.SegmentType))
            .Sum(segment => segment.LengthFeet);
        path.TotalPipeLengthFeet = chain.Sum(segment => segment.LengthFeet);
        path.BranchDiameterInches = ResolveDominantDiameter(
            chain.Where(segment => IsBranchSegment(segment.SegmentType)),
            path.BranchDiameterInches);
        path.MainDiameterInches = ResolveDominantDiameter(
            chain.Where(segment => !IsBranchSegment(segment.SegmentType)),
            path.MainDiameterInches);
    }

    public static void AssignSegmentFlows(
        LayoutLinkedHydraulicPath path,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        double totalSprinklerFlowGpm,
        double hoseStreamAllowanceGpm,
        SchematicPipeRoutingSummary schematicPipeRouting = null)
    {
        if (path.SegmentChain == null || path.SegmentChain.Count == 0)
        {
            return;
        }

        LayoutSprinklerPoint remote = path.MostRemoteSprinkler;
        int remoteRoomId = remote?.Room?.RevitElementId ?? 0;
        double remoteHeadFlow = headFlows.TryGetValue(remote, out double remoteFlow)
            ? remoteFlow
            : totalSprinklerFlowGpm / Math.Max(path.OperatingSprinklers.Count, 1);

        IList<TrunkContributionPoint> trunkPoints = path.UsesProjectTrunk
            ? BuildProjectTrunkContributionPoints(path.OperatingSprinklers, headFlows, schematicPipeRouting)
            : BuildTrunkContributionPoints(
                path.OperatingSprinklers.Where(point => (point.Room?.RevitElementId ?? 0) == remoteRoomId).ToList(),
                headFlows,
                path.SegmentChain);

        foreach (HydraulicGraphSegment segment in path.SegmentChain)
        {
            if (IsBranchSegment(segment.SegmentType))
            {
                segment.FlowGpm = remoteHeadFlow;
                continue;
            }

            if (string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase))
            {
                segment.FlowGpm = totalSprinklerFlowGpm + hoseStreamAllowanceGpm;
                continue;
            }

            Point3D supplyHeader = path.UsesProjectTrunk
                ? ProjectTrunkRouter.ResolveSupplyHeader(schematicPipeRouting)
                : new Point3D();
            segment.FlowGpm = ResolveTrunkFlowForSegment(
                segment,
                trunkPoints,
                totalSprinklerFlowGpm,
                path.UsesProjectTrunk,
                supplyHeader);
        }
    }

    private static IList<HydraulicGraphSegment> BuildChainFromRoomSegments(
        LayoutSprinklerPoint remoteSprinkler,
        IList<HydraulicGraphSegment> roomSegments,
        Point3D sourcePoint,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        List<HydraulicGraphSegment> chain;
        if (RoomSegmentsUsePlacedTopology(roomSegments))
        {
            chain = PlacedPipeGraphBuilder.TraceCriticalPath(remoteSprinkler, roomSegments, sourcePoint).ToList();
        }
        else
        {
            chain = BuildSchematicChainFromRoomSegments(remoteSprinkler, roomSegments, sourcePoint).ToList();
        }

        if (schematicPipeRouting?.UsesProjectTrunk == true)
        {
            chain.AddRange(BuildProjectTrunkSegments(remoteSprinkler.Room?.RevitElementId ?? 0, schematicPipeRouting));
        }

        return chain.Where(segment => segment.LengthFeet > 0.01).ToList();
    }

    private static IList<HydraulicGraphSegment> BuildSchematicChainFromRoomSegments(
        LayoutSprinklerPoint remoteSprinkler,
        IList<HydraulicGraphSegment> roomSegments,
        Point3D sourcePoint)
    {
        List<HydraulicGraphSegment> chain = new List<HydraulicGraphSegment>();
        HydraulicGraphSegment branchDrop = FindBranchDropSegment(remoteSprinkler, roomSegments);
        if (branchDrop != null)
        {
            chain.Add(CloneSegment(branchDrop));
        }

        HydraulicGraphSegment branchTieIn = FindBranchTieInSegment(remoteSprinkler, roomSegments, branchDrop);
        if (branchTieIn != null)
        {
            chain.Add(CloneSegment(branchTieIn));
        }

        Point3D tieInPoint = branchTieIn?.End ?? branchDrop?.End ?? remoteSprinkler.Location;
        HydraulicGraphSegment riser = roomSegments
            .FirstOrDefault(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        Point3D roomTap = riser?.End ?? riser?.Start ?? sourcePoint;

        IList<HydraulicGraphSegment> crossMainSegments = BuildCrossMainSegments(
            tieInPoint,
            roomTap,
            roomSegments);
        chain.AddRange(crossMainSegments);

        if (riser != null)
        {
            chain.Add(CloneSegment(riser));
        }

        return chain;
    }

    private static bool RoomSegmentsUsePlacedTopology(IList<HydraulicGraphSegment> roomSegments)
    {
        return roomSegments != null
            && roomSegments.Count > 0
            && roomSegments.All(segment => string.Equals(segment.DataSource, "Placed", StringComparison.OrdinalIgnoreCase));
    }

    private static PipePlacementRoomResult FindPlacedRoom(PipePlacementSummary pipePlacementSummary, int roomRevitElementId)
    {
        return (pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId == roomRevitElementId && roomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .Select(group => group.Last())
            .FirstOrDefault();
    }

    private static IList<HydraulicGraphSegment> BuildProjectTrunkSegments(
        int remoteRoomRevitElementId,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        List<HydraulicGraphSegment> segments = new List<HydraulicGraphSegment>();
        RoomTrunkTap roomTap = ProjectTrunkRouter.FindRoomTap(schematicPipeRouting, remoteRoomRevitElementId);
        Point3D supplyHeader = ProjectTrunkRouter.ResolveSupplyHeader(schematicPipeRouting);
        if (roomTap != null && !PointsMatch(roomTap.HeaderPoint, supplyHeader))
        {
            PipeSegment projectMain = schematicPipeRouting.Segments?
                .FirstOrDefault(segment =>
                    segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                    && string.Equals(segment.SegmentType, PipeSegmentTypes.Main, StringComparison.OrdinalIgnoreCase)
                    && PointsMatch(segment.Start, roomTap.HeaderPoint));
            if (projectMain == null)
            {
                projectMain = schematicPipeRouting.Segments?
                    .FirstOrDefault(segment =>
                        segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                        && string.Equals(segment.SegmentType, PipeSegmentTypes.Main, StringComparison.OrdinalIgnoreCase)
                        && (segment.Description ?? string.Empty).IndexOf("project trunk", StringComparison.OrdinalIgnoreCase) >= 0
                        && (PointsMatch(segment.Start, roomTap.HeaderPoint) || segment.Start.X == roomTap.HeaderPoint.X));
            }

            if (projectMain != null)
            {
                segments.Add(ConvertProjectSegment(projectMain));
            }
            else
            {
                segments.Add(new HydraulicGraphSegment
                {
                    SegmentId = "Project trunk to supply",
                    SegmentType = PipeSegmentTypes.Main,
                    Start = roomTap.HeaderPoint,
                    End = supplyHeader,
                    LengthFeet = OrthogonalLengthFeet(roomTap.HeaderPoint, supplyHeader),
                    DiameterInches = 4.0,
                    Description = "Project trunk to supply",
                    DataSource = "Schematic"
                });
            }
        }

        PipeSegment buildingRiser = schematicPipeRouting.Segments?
            .FirstOrDefault(segment =>
                segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        if (buildingRiser != null)
        {
            segments.Add(ConvertProjectSegment(buildingRiser));
        }

        return segments;
    }

    private static HydraulicGraphSegment ConvertProjectSegment(PipeSegment segment)
    {
        return new HydraulicGraphSegment
        {
            SegmentId = segment.Description,
            Start = segment.Start,
            End = segment.End,
            LengthFeet = segment.LengthFeet,
            DiameterInches = segment.DiameterInches,
            SegmentType = segment.SegmentType,
            RoomRevitElementId = segment.RoomRevitElementId,
            Description = segment.Description,
            DataSource = "Schematic"
        };
    }

    private static IList<TrunkContributionPoint> BuildProjectTrunkContributionPoints(
        IEnumerable<LayoutSprinklerPoint> operatingSprinklers,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        List<TrunkContributionPoint> points = new List<TrunkContributionPoint>();
        foreach (LayoutSprinklerPoint head in operatingSprinklers ?? Enumerable.Empty<LayoutSprinklerPoint>())
        {
            double flow = headFlows.TryGetValue(head, out double headFlow) ? headFlow : 0.0;
            int roomId = head.Room?.RevitElementId ?? 0;
            points.Add(new TrunkContributionPoint
            {
                Location = head.Location,
                DistanceFromRiserFeet = ProjectTrunkRouter.ComputePathDistanceFromSupplyFeet(
                    schematicPipeRouting,
                    roomId,
                    head.Location),
                FlowGpm = flow
            });
        }

        return points
            .OrderByDescending(point => point.DistanceFromRiserFeet)
            .ToList();
    }

    private static double OrthogonalLengthFeet(Point3D start, Point3D end)
    {
        return Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y) + Math.Abs(end.Z - start.Z);
    }

    private static IList<HydraulicGraphSegment> BuildCrossMainSegments(
        Point3D tieInPoint,
        Point3D riserTop,
        IList<HydraulicGraphSegment> roomSegments)
    {
        List<HydraulicGraphSegment> segments = new List<HydraulicGraphSegment>();
        HydraulicGraphSegment crossMain = roomSegments
            .FirstOrDefault(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));
        if (crossMain == null)
        {
            double partialLength = HorizontalDistanceFeet(tieInPoint, riserTop);
            if (partialLength > 0.01)
            {
                segments.Add(new HydraulicGraphSegment
                {
                    SegmentId = "Cross main to riser",
                    SegmentType = PipeSegmentTypes.CrossMain,
                    Start = tieInPoint,
                    End = riserTop,
                    LengthFeet = partialLength,
                    DiameterInches = 4.0,
                    Description = "Cross main run to riser",
                    DataSource = "Schematic"
                });
            }

            return segments;
        }

        double runLength = HorizontalDistanceFeet(tieInPoint, riserTop);
        if (runLength <= 0.01)
        {
            return segments;
        }

        double fullLength = crossMain.LengthFeet > 0.01
            ? crossMain.LengthFeet
            : HorizontalDistanceFeet(crossMain.Start, crossMain.End);
        double scaledLength = fullLength > 0.01
            ? runLength * (fullLength / Math.Max(HorizontalDistanceFeet(crossMain.Start, crossMain.End), 0.01))
            : runLength;

        segments.Add(new HydraulicGraphSegment
        {
            SegmentId = crossMain.SegmentId,
            SegmentType = crossMain.SegmentType,
            Start = tieInPoint,
            End = riserTop,
            LengthFeet = scaledLength,
            DiameterInches = crossMain.DiameterInches,
            RoomRevitElementId = crossMain.RoomRevitElementId,
            Description = crossMain.Description,
            DataSource = crossMain.DataSource
        });

        return segments;
    }

    private static IList<HydraulicGraphSegment> BuildGeometryChain(
        LayoutSprinklerPoint remoteSprinkler,
        Point3D sourcePoint,
        double branchDiameterInches,
        double mainDiameterInches)
    {
        RoomInfo remoteRoom = remoteSprinkler.Room;
        Point3D branchJunction = remoteRoom == null
            ? sourcePoint
            : HydraulicGeometry.ResolveBranchJunction(remoteRoom);
        double branchLength = Math.Max(
            MinimumBranchLengthFeet,
            HydraulicGeometry.DistanceFeet(remoteSprinkler.Location, branchJunction));
        double mainLength = Math.Max(
            MinimumMainLengthFeet,
            HydraulicGeometry.HorizontalDistanceFeet(branchJunction, sourcePoint)
            + Math.Abs(branchJunction.Z - sourcePoint.Z));

        return new List<HydraulicGraphSegment>
        {
            new HydraulicGraphSegment
            {
                SegmentId = "Branch to junction",
                SegmentType = PipeSegmentTypes.Branch,
                Start = remoteSprinkler.Location,
                End = branchJunction,
                LengthFeet = branchLength,
                DiameterInches = branchDiameterInches,
                Description = "Geometry branch",
                DataSource = "Geometry"
            },
            new HydraulicGraphSegment
            {
                SegmentId = "Main to source",
                SegmentType = PipeSegmentTypes.Main,
                Start = branchJunction,
                End = sourcePoint,
                LengthFeet = mainLength,
                DiameterInches = mainDiameterInches,
                Description = "Geometry main",
                DataSource = "Geometry"
            }
        };
    }

    private static HydraulicGraphSegment FindBranchDropSegment(
        LayoutSprinklerPoint remoteSprinkler,
        IList<HydraulicGraphSegment> roomSegments)
    {
        string indexedDescription = "branch drop #" + (remoteSprinkler.SprinklerIndex + 1);
        HydraulicGraphSegment indexedDrop = roomSegments
            .FirstOrDefault(segment =>
                IsBranchSegment(segment.SegmentType)
                && (segment.Description ?? string.Empty).IndexOf(indexedDescription, StringComparison.OrdinalIgnoreCase) >= 0);
        if (indexedDrop != null)
        {
            return indexedDrop;
        }

        return roomSegments
            .Where(segment =>
                IsBranchSegment(segment.SegmentType)
                && (segment.Description ?? string.Empty).IndexOf("branch drop", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(segment => HydraulicGeometry.DistanceFeet(remoteSprinkler.Location, segment.Start))
            .FirstOrDefault();
    }

    private static HydraulicGraphSegment FindBranchTieInSegment(
        LayoutSprinklerPoint remoteSprinkler,
        IList<HydraulicGraphSegment> roomSegments,
        HydraulicGraphSegment branchDrop)
    {
        string indexedDescription = "branch tie-in #" + (remoteSprinkler.SprinklerIndex + 1);
        HydraulicGraphSegment indexedTieIn = roomSegments
            .FirstOrDefault(segment =>
                IsBranchSegment(segment.SegmentType)
                && (segment.Description ?? string.Empty).IndexOf(indexedDescription, StringComparison.OrdinalIgnoreCase) >= 0);
        if (indexedTieIn != null)
        {
            return indexedTieIn;
        }

        if (branchDrop != null)
        {
            return roomSegments
                .Where(segment =>
                    IsBranchSegment(segment.SegmentType)
                    && (segment.Description ?? string.Empty).IndexOf("branch tie-in", StringComparison.OrdinalIgnoreCase) >= 0
                    && PointsMatch(segment.Start, branchDrop.End))
                .FirstOrDefault();
        }

        return roomSegments
            .FirstOrDefault(segment =>
                IsBranchSegment(segment.SegmentType)
                && (segment.Description ?? string.Empty).IndexOf("branch tie-in", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IList<TrunkContributionPoint> BuildTrunkContributionPoints(
        IList<LayoutSprinklerPoint> roomHeads,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        IList<HydraulicGraphSegment> segmentChain)
    {
        HydraulicGraphSegment riser = segmentChain
            .LastOrDefault(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        Point3D riserAnchor = riser?.Start ?? riser?.End ?? new Point3D();

        List<TrunkContributionPoint> points = new List<TrunkContributionPoint>();
        foreach (LayoutSprinklerPoint head in roomHeads)
        {
            double flow = headFlows.TryGetValue(head, out double headFlow) ? headFlow : 0.0;
            Point3D tieInPoint = new Point3D(head.Location.X, riserAnchor.Y, riserAnchor.Z);
            points.Add(new TrunkContributionPoint
            {
                Location = tieInPoint,
                DistanceFromRiserFeet = HorizontalDistanceFeet(tieInPoint, riserAnchor),
                FlowGpm = flow
            });
        }

        return points
            .OrderByDescending(point => point.DistanceFromRiserFeet)
            .ToList();
    }

    private static double ResolveTrunkFlowForSegment(
        HydraulicGraphSegment segment,
        IList<TrunkContributionPoint> trunkPoints,
        double totalSprinklerFlowGpm,
        bool usesProjectTrunk,
        Point3D supplyHeader)
    {
        if (trunkPoints.Count == 0)
        {
            return totalSprinklerFlowGpm;
        }

        if (usesProjectTrunk)
        {
            double upstreamDistanceFromSupply = OrthogonalLengthFeet(supplyHeader, segment.End);
            double cumulativeFlow = 0.0;
            foreach (TrunkContributionPoint point in trunkPoints)
            {
                if (point.DistanceFromRiserFeet + LocationToleranceFeet >= upstreamDistanceFromSupply)
                {
                    cumulativeFlow += point.FlowGpm;
                }
            }

            return cumulativeFlow > 0.01 ? cumulativeFlow : totalSprinklerFlowGpm;
        }

        double downstreamDistance = Math.Max(
            HorizontalDistanceFeet(segment.Start, segment.End),
            HorizontalDistanceFeet(segment.End, trunkPoints.Last().Location));

        double roomCumulativeFlow = 0.0;
        foreach (TrunkContributionPoint point in trunkPoints)
        {
            if (point.DistanceFromRiserFeet + LocationToleranceFeet >= downstreamDistance)
            {
                roomCumulativeFlow += point.FlowGpm;
            }
        }

        return roomCumulativeFlow > 0.01 ? roomCumulativeFlow : totalSprinklerFlowGpm;
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

    private static double HorizontalDistanceFeet(Point3D from, Point3D to)
    {
        return HydraulicGeometry.HorizontalDistanceFeet(from, to);
    }

    private static double ResolveDominantDiameter(IEnumerable<HydraulicGraphSegment> segments, double fallbackDiameterInches)
    {
        HydraulicGraphSegment dominant = segments
            .GroupBy(segment => segment.DiameterInches)
            .OrderByDescending(group => group.Sum(segment => segment.LengthFeet))
            .Select(group => group.First())
            .FirstOrDefault();

        return dominant != null && dominant.DiameterInches > 0
            ? dominant.DiameterInches
            : fallbackDiameterInches;
    }

    private sealed class TrunkContributionPoint
    {
        public Point3D Location { get; set; } = new Point3D();

        public double DistanceFromRiserFeet { get; set; }

        public double FlowGpm { get; set; }
    }
}
