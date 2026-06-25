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

    public static IList<HydraulicGraphSegment> ResolveSegmentsForRoom(
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

        if (PlacedPipeGraphBuilder.RoomHasPlacedTopology(placedRoom))
        {
            return PlacedPipeGraphBuilder.BuildRoomSegments(
                placedRoom,
                roomRevitElementId,
                defaultBranchDiameterInches,
                defaultMainDiameterInches);
        }

        IList<PipeSegment> schematicSegments = SchematicPipeRoutingService.GetSegmentsForRoom(
            schematicPipeRouting,
            roomRevitElementId);
        if (schematicSegments.Count == 0)
        {
            return new List<HydraulicGraphSegment>();
        }

        string dataSource = placedRoom?.PlacedSegments != null && placedRoom.PlacedSegments.Count > 0
            ? "Placed"
            : "Schematic";

        List<HydraulicGraphSegment> segments = new List<HydraulicGraphSegment>();
        foreach (PipeSegment schematicSegment in schematicSegments)
        {
            PipePlacementSegmentResult placedMatch = FindPlacedMatch(placedRoom, schematicSegment);
            double lengthFeet = placedMatch != null && placedMatch.LengthFeet > 0
                ? placedMatch.LengthFeet
                : schematicSegment.LengthFeet;
            double diameterInches = placedMatch != null && placedMatch.DiameterInches > 0
                ? placedMatch.DiameterInches
                : schematicSegment.DiameterInches;

            segments.Add(new HydraulicGraphSegment
            {
                SegmentId = schematicSegment.Description,
                Start = schematicSegment.Start,
                End = schematicSegment.End,
                LengthFeet = lengthFeet,
                DiameterInches = diameterInches > 0
                    ? diameterInches
                    : IsBranchSegment(schematicSegment.SegmentType)
                        ? defaultBranchDiameterInches
                        : defaultMainDiameterInches,
                SegmentType = schematicSegment.SegmentType,
                RoomRevitElementId = schematicSegment.RoomRevitElementId,
                Description = schematicSegment.Description,
                DataSource = placedMatch != null ? "Placed" : dataSource
            });
        }

        return segments;
    }

    public static PipePlacementSegmentResult FindPlacedMatch(
        PipePlacementSummary pipePlacementSummary,
        PipeSegment schematicSegment)
    {
        if (schematicSegment == null || pipePlacementSummary?.RoomResults == null)
        {
            return null;
        }

        PipePlacementRoomResult placedRoom = pipePlacementSummary.RoomResults
            .Where(result => result.RoomRevitElementId == schematicSegment.RoomRevitElementId && schematicSegment.RoomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .Select(group => group.Last())
            .FirstOrDefault();

        return FindPlacedMatch(placedRoom, schematicSegment);
    }

    private static PipePlacementSegmentResult FindPlacedMatch(
        PipePlacementRoomResult placedRoom,
        PipeSegment schematicSegment)
    {
        if (placedRoom?.PlacedSegments == null || placedRoom.PlacedSegments.Count == 0)
        {
            return null;
        }

        string schematicDescription = schematicSegment.Description ?? string.Empty;
        string schematicIndex = ExtractDescriptionIndex(schematicDescription);
        if (!string.IsNullOrWhiteSpace(schematicIndex))
        {
            PipePlacementSegmentResult indexedMatch = placedRoom.PlacedSegments
                .FirstOrDefault(segment =>
                    string.Equals(segment.SegmentType, schematicSegment.SegmentType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ExtractDescriptionIndex(segment.Description ?? string.Empty), schematicIndex, StringComparison.OrdinalIgnoreCase));
            if (indexedMatch != null)
            {
                return indexedMatch;
            }
        }

        PipePlacementSegmentResult descriptionMatch = placedRoom.PlacedSegments
            .FirstOrDefault(segment =>
                string.Equals(segment.SegmentType, schematicSegment.SegmentType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(segment.Description)
                && schematicDescription.IndexOf(segment.Description, StringComparison.OrdinalIgnoreCase) >= 0);
        if (descriptionMatch != null)
        {
            return descriptionMatch;
        }

        descriptionMatch = placedRoom.PlacedSegments
            .FirstOrDefault(segment =>
                string.Equals(segment.SegmentType, schematicSegment.SegmentType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(segment.Description)
                && segment.Description.IndexOf(schematicDescription, StringComparison.OrdinalIgnoreCase) >= 0);
        if (descriptionMatch != null)
        {
            return descriptionMatch;
        }

        return placedRoom.PlacedSegments
            .FirstOrDefault(segment =>
                string.Equals(segment.SegmentType, schematicSegment.SegmentType, StringComparison.OrdinalIgnoreCase)
                && ContainsMatchingKeyword(schematicDescription, segment.Description));
    }

    private static bool ContainsMatchingKeyword(string left, string right)
    {
        string[] keywords = { "branch drop", "branch tie-in", "cross main", "riser", "main" };
        foreach (string keyword in keywords)
        {
            if ((left ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                && (right ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractDescriptionIndex(string description)
    {
        int hashIndex = (description ?? string.Empty).LastIndexOf('#');
        if (hashIndex < 0 || hashIndex >= description.Length - 1)
        {
            return string.Empty;
        }

        int endIndex = hashIndex + 1;
        while (endIndex < description.Length && char.IsDigit(description[endIndex]))
        {
            endIndex++;
        }

        return endIndex > hashIndex + 1
            ? description.Substring(hashIndex + 1, endIndex - hashIndex - 1)
            : string.Empty;
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
