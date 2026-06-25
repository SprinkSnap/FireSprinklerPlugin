using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicPipeLengthSource
{
    public double BranchLengthFeet { get; set; }

    public double MainLengthFeet { get; set; }

    public double TotalPipeLengthFeet { get; set; }

    public double BranchDiameterInches { get; set; }

    public double MainDiameterInches { get; set; }

    public string DataSource { get; set; } = "Geometry";

    public bool UsesPlacedPipeLengths { get; set; }
}

public static class PlacedPipeHydraulicResolver
{
    public static HydraulicPipeLengthSource Resolve(
        int roomRevitElementId,
        PipePlacementSummary pipePlacementSummary,
        SchematicPipeRoutingSummary schematicPipeRouting,
        double defaultBranchDiameterInches,
        double defaultMainDiameterInches)
    {
        PipePlacementRoomResult placedRoom = (pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId == roomRevitElementId && roomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .Select(group => group.Last())
            .FirstOrDefault();

        if (placedRoom?.PlacedSegments != null && placedRoom.PlacedSegments.Count > 0)
        {
            return ResolveFromPlacedSegments(
                placedRoom,
                defaultBranchDiameterInches,
                defaultMainDiameterInches);
        }

        IList<PipeSegment> schematicSegments = SchematicPipeRoutingService.GetSegmentsForRoom(
            schematicPipeRouting,
            roomRevitElementId);
        if (schematicSegments.Count > 0)
        {
            return ResolveFromSchematicSegments(
                schematicSegments,
                defaultBranchDiameterInches,
                defaultMainDiameterInches);
        }

        return new HydraulicPipeLengthSource
        {
            BranchDiameterInches = defaultBranchDiameterInches,
            MainDiameterInches = defaultMainDiameterInches,
            DataSource = "Geometry"
        };
    }

    private static HydraulicPipeLengthSource ResolveFromPlacedSegments(
        PipePlacementRoomResult placedRoom,
        double defaultBranchDiameterInches,
        double defaultMainDiameterInches)
    {
        List<PipePlacementSegmentResult> branchSegments = placedRoom.PlacedSegments
            .Where(segment => IsBranchSegment(segment.SegmentType))
            .ToList();
        List<PipePlacementSegmentResult> mainSegments = placedRoom.PlacedSegments
            .Where(segment => !IsBranchSegment(segment.SegmentType))
            .ToList();

        double branchLength = branchSegments.Sum(segment => segment.LengthFeet);
        double mainLength = mainSegments.Sum(segment => segment.LengthFeet);
        if (branchLength <= 0 && mainLength <= 0)
        {
            branchLength = placedRoom.PlacedLengthFeet;
        }

        return new HydraulicPipeLengthSource
        {
            BranchLengthFeet = branchLength,
            MainLengthFeet = mainLength,
            TotalPipeLengthFeet = branchLength + mainLength,
            BranchDiameterInches = ResolveDominantDiameter(branchSegments, defaultBranchDiameterInches),
            MainDiameterInches = ResolveDominantDiameter(mainSegments, defaultMainDiameterInches),
            DataSource = "Placed",
            UsesPlacedPipeLengths = true
        };
    }

    private static HydraulicPipeLengthSource ResolveFromSchematicSegments(
        IList<PipeSegment> schematicSegments,
        double defaultBranchDiameterInches,
        double defaultMainDiameterInches)
    {
        List<PipeSegment> branchSegments = schematicSegments
            .Where(segment => IsBranchSegment(segment.SegmentType))
            .ToList();
        List<PipeSegment> mainSegments = schematicSegments
            .Where(segment => !IsBranchSegment(segment.SegmentType))
            .ToList();

        return new HydraulicPipeLengthSource
        {
            BranchLengthFeet = branchSegments.Sum(segment => segment.LengthFeet),
            MainLengthFeet = mainSegments.Sum(segment => segment.LengthFeet),
            TotalPipeLengthFeet = schematicSegments.Sum(segment => segment.LengthFeet),
            BranchDiameterInches = ResolveDominantDiameter(branchSegments, defaultBranchDiameterInches),
            MainDiameterInches = ResolveDominantDiameter(mainSegments, defaultMainDiameterInches),
            DataSource = "Schematic",
            UsesPlacedPipeLengths = false
        };
    }

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
    }

    private static double ResolveDominantDiameter(
        IEnumerable<PipePlacementSegmentResult> segments,
        double fallbackDiameterInches)
    {
        PipePlacementSegmentResult dominant = segments
            .GroupBy(segment => segment.DiameterInches)
            .OrderByDescending(group => group.Sum(segment => segment.LengthFeet))
            .Select(group => group.First())
            .FirstOrDefault();

        return dominant != null && dominant.DiameterInches > 0
            ? dominant.DiameterInches
            : fallbackDiameterInches;
    }

    private static double ResolveDominantDiameter(
        IEnumerable<PipeSegment> segments,
        double fallbackDiameterInches)
    {
        PipeSegment dominant = segments
            .GroupBy(segment => segment.DiameterInches)
            .OrderByDescending(group => group.Sum(segment => segment.LengthFeet))
            .Select(group => group.First())
            .FirstOrDefault();

        return dominant != null && dominant.DiameterInches > 0
            ? dominant.DiameterInches
            : fallbackDiameterInches;
    }
}
