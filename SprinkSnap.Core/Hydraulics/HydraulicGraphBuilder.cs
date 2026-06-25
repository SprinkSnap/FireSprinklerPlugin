using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicGraphBuilder
{
    private const double MinimumBranchLengthFeet = 10.0;

    private const double MinimumMainLengthFeet = 20.0;

    public static Point3D ResolveSourcePoint(
        IEnumerable<RoomInfo> controllingRooms,
        SchematicPipeRoutingSummary schematicPipeRouting = null)
    {
        if (schematicPipeRouting?.UsesProjectTrunk == true)
        {
            return schematicPipeRouting.SupplyPoint;
        }

        List<RoomInfo> rooms = controllingRooms?.ToList() ?? new List<RoomInfo>();
        if (rooms.Count == 0)
        {
            return new Point3D();
        }

        return new Point3D(
            rooms.Average(room => room.Centroid.X),
            rooms.Average(room => room.Centroid.Y),
            rooms.Average(room => room.FloorElevationFeet));
    }

    public static IList<LayoutSprinklerPoint> CollectSprinklerPoints(
        IEnumerable<RoomInfo> controllingRooms,
        double defaultKFactor)
    {
        List<LayoutSprinklerPoint> points = new List<LayoutSprinklerPoint>();
        foreach (RoomInfo room in controllingRooms ?? Enumerable.Empty<RoomInfo>())
        {
            IList<SprinklerPlacementCandidate> candidates = room.ProposedSprinklers;
            if (candidates == null || candidates.Count == 0)
            {
                continue;
            }

            double roomKFactor = ResolveRoomKFactor(room, defaultKFactor);
            for (int index = 0; index < candidates.Count; index++)
            {
                SprinklerPlacementCandidate candidate = candidates[index];
                points.Add(new LayoutSprinklerPoint
                {
                    Room = room,
                    Location = candidate.Location ?? new Point3D(),
                    SprinklerIndex = index,
                    KFactor = roomKFactor
                });
            }
        }

        return points;
    }

    public static IList<LayoutSprinklerPoint> SelectOperatingSprinklers(
        IEnumerable<LayoutSprinklerPoint> sprinklerPoints,
        Point3D sourcePoint,
        int operatingSprinklerCount)
    {
        List<LayoutSprinklerPoint> points = sprinklerPoints?.ToList() ?? new List<LayoutSprinklerPoint>();
        if (points.Count == 0)
        {
            return points;
        }

        int count = Math.Min(Math.Max(operatingSprinklerCount, 1), points.Count);
        return points
            .OrderByDescending(point => HydraulicGeometry.DistanceFeet(point.Location, sourcePoint))
            .Take(count)
            .ToList();
    }

    public static LayoutLinkedHydraulicPath BuildPath(
        IEnumerable<RoomInfo> controllingRooms,
        IEnumerable<LayoutSprinklerPoint> operatingSprinklers,
        Point3D sourcePoint,
        double branchDiameterInches,
        double mainDiameterInches,
        SchematicPipeRoutingSummary schematicPipeRouting = null,
        PipePlacementSummary pipePlacementSummary = null,
        HydraulicSupplyAnchor supplyAnchor = null)
    {
        LayoutLinkedHydraulicPath path = new LayoutLinkedHydraulicPath
        {
            SourcePoint = sourcePoint,
            BranchDiameterInches = branchDiameterInches,
            MainDiameterInches = mainDiameterInches,
            OperatingSprinklers = operatingSprinklers?.ToList() ?? new List<LayoutSprinklerPoint>(),
            UsesUserSupplyAnchor = supplyAnchor?.IsSet == true
                || schematicPipeRouting?.UsesUserSupplyAnchor == true,
            UserSupplyAnchorLabel = supplyAnchor?.IsSet == true
                ? supplyAnchor.ElementLabel ?? string.Empty
                : schematicPipeRouting?.UserSupplyAnchorLabel ?? string.Empty
        };

        if (path.OperatingSprinklers.Count == 0)
        {
            path.Warnings.Add("No sprinkler layout coordinates were available. Using default branch and main lengths.");
            path.BranchLengthFeet = 60.0;
            path.MainLengthFeet = 120.0;
            path.TotalPipeLengthFeet = path.BranchLengthFeet + path.MainLengthFeet;
            return path;
        }

        path.UsesLayoutGeometry = true;
        path.MostRemoteSprinkler = path.OperatingSprinklers
            .OrderByDescending(point => HydraulicGeometry.DistanceFeet(point.Location, sourcePoint))
            .First();

        int remoteRoomId = path.MostRemoteSprinkler.Room?.RevitElementId ?? 0;
        HydraulicPipeLengthSource pipeLengths = PlacedPipeHydraulicResolver.Resolve(
            remoteRoomId,
            pipePlacementSummary,
            schematicPipeRouting,
            branchDiameterInches,
            mainDiameterInches);

        if (pipeLengths.DataSource == "Placed")
        {
            path.UsesPlacedPipeLengths = true;
            path.PipeLengthDataSource = pipeLengths.DataSource;
            path.BranchLengthFeet = pipeLengths.BranchLengthFeet;
            path.MainLengthFeet = pipeLengths.MainLengthFeet;
            path.TotalPipeLengthFeet = pipeLengths.TotalPipeLengthFeet;
            path.BranchDiameterInches = pipeLengths.BranchDiameterInches;
            path.MainDiameterInches = pipeLengths.MainDiameterInches;
            path.Warnings.Add(
                "Critical path pipe lengths and diameters derived from placed Revit geometry in room "
                + (path.MostRemoteSprinkler.Room?.Number ?? string.Empty)
                + ".");
        }
        else
        {
            IList<PipeSegment> roomSegments = SchematicPipeRoutingService.GetSegmentsForRoom(
                schematicPipeRouting,
                remoteRoomId);
            if (roomSegments.Count > 0)
            {
                path.PipeLengthDataSource = "Schematic";
                path.BranchLengthFeet = roomSegments
                    .Where(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
                    .Sum(segment => segment.LengthFeet);
                path.MainLengthFeet = roomSegments
                    .Where(segment => !string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
                    .Sum(segment => segment.LengthFeet);
                path.TotalPipeLengthFeet = roomSegments.Sum(segment => segment.LengthFeet);
                path.BranchDiameterInches = pipeLengths.BranchDiameterInches;
                path.MainDiameterInches = pipeLengths.MainDiameterInches;
                path.Warnings.Add(
                    "Critical path pipe lengths derived from schematic routing in room "
                    + (path.MostRemoteSprinkler.Room?.Number ?? string.Empty)
                    + ".");
            }
            else
            {
                path.PipeLengthDataSource = "Geometry";
                RoomInfo remoteRoom = path.MostRemoteSprinkler.Room;
                Point3D branchJunction = HydraulicGeometry.ResolveBranchJunction(remoteRoom);
                path.BranchLengthFeet = Math.Max(
                    MinimumBranchLengthFeet,
                    HydraulicGeometry.DistanceFeet(path.MostRemoteSprinkler.Location, branchJunction));

                double horizontalMain = HydraulicGeometry.HorizontalDistanceFeet(branchJunction, sourcePoint);
                double elevationMain = Math.Abs(branchJunction.Z - sourcePoint.Z);
                path.MainLengthFeet = Math.Max(MinimumMainLengthFeet, horizontalMain + elevationMain);
                path.TotalPipeLengthFeet = path.BranchLengthFeet + path.MainLengthFeet;
            }
        }

        List<RoomInfo> rooms = controllingRooms?.ToList() ?? new List<RoomInfo>();
        if (rooms.Count > 0 && path.MainLengthFeet < 25.0)
        {
            path.Warnings.Add("Calculated main length is short. Verify riser location and room geometry in Revit.");
        }

        HydraulicSegmentGraphBuilder.BuildSegmentChain(
            path,
            schematicPipeRouting,
            pipePlacementSummary,
            controllingRooms);
        if (path.UsesSegmentGraphHydraulics)
        {
            if (path.UsesProjectTrunk)
            {
                path.SourcePoint = schematicPipeRouting?.SupplyPoint ?? path.SourcePoint;
                path.Warnings.Add(
                    "Project trunk connects "
                    + (schematicPipeRouting?.ProjectTrunkRoomRevitElementIds?.Count ?? 0)
                    + " room(s) to a shared building riser at the hydraulic source.");
            }

            if (path.UsesPlacedPipeTopology)
            {
                path.Warnings.Add(
                    "Segment-graph critical path uses placed Revit pipe topology in room "
                    + (path.MostRemoteSprinkler.Room?.Number ?? string.Empty)
                    + " with "
                    + path.CriticalPathSegmentCount
                    + " segment(s).");
            }
            else if (path.UsesPlacedPipeLengths)
            {
                path.Warnings.Add(
                    "Segment-graph critical path uses placed Revit pipe lengths in room "
                    + (path.MostRemoteSprinkler.Room?.Number ?? string.Empty)
                    + " with "
                    + path.CriticalPathSegmentCount
                    + " segment(s).");
            }
            else if (!string.Equals(path.PipeLengthDataSource, "Geometry", StringComparison.OrdinalIgnoreCase))
            {
                path.Warnings.Add(
                    "Segment-graph critical path uses schematic routing in room "
                    + (path.MostRemoteSprinkler.Room?.Number ?? string.Empty)
                    + " with "
                    + path.CriticalPathSegmentCount
                    + " segment(s).");
            }
        }

        if (path.UsesUserSupplyAnchor)
        {
            path.Warnings.Add(
                "Hydraulic source anchored to user-selected supply "
                + (string.IsNullOrWhiteSpace(path.UserSupplyAnchorLabel)
                    ? "element"
                    : path.UserSupplyAnchorLabel)
                + ".");
        }

        int operatingRoomCount = path.OperatingSprinklers.Select(point => point.Room.RevitElementId).Distinct().Count();
        if (operatingRoomCount > 1 && !path.UsesProjectTrunk)
        {
            path.Warnings.Add(
                "Operating sprinklers span "
                + operatingRoomCount
                + " room(s). Segment graph traces the most remote head; trunk segments carry combined operating flow.");
        }

        return path;
    }

    private static double ResolveRoomKFactor(RoomInfo room, double defaultKFactor)
    {
        string sprinklerName = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
            ? room.AutoSelectedSprinklerName
            : room.SelectedSprinklerFamilyName;

        if (string.IsNullOrWhiteSpace(sprinklerName))
        {
            return defaultKFactor;
        }

        SprinklerFamilyInfo family = new SprinklerFamilySelector()
            .GetAvailableFamilies()
            .FirstOrDefault(item => string.Equals(item.DisplayName, sprinklerName, StringComparison.OrdinalIgnoreCase));

        return family?.KFactor > 0 ? family.KFactor : defaultKFactor;
    }
}
