using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Core.Materials;

public sealed class MaterialTakeoffEngine : Engines.IMaterialTakeoffEngine
{
    private readonly ISprinklerFamilySelector familySelector;

    public MaterialTakeoffEngine(ISprinklerFamilySelector familySelector = null)
    {
        this.familySelector = familySelector ?? new SprinklerFamilySelector();
    }

    public IReadOnlyList<MaterialTakeoffItem> Generate(
        IEnumerable<RoomInfo> rooms,
        SprinklerPlacementSummary placementSummary = null,
        SchematicPipeRoutingSummary schematicPipeRouting = null,
        PipePlacementSummary pipePlacementSummary = null)
    {
        List<RoomInfo> roomList = rooms?.ToList() ?? new List<RoomInfo>();
        List<MaterialTakeoffItem> detailRows = new List<MaterialTakeoffItem>();

        foreach (RoomInfo room in roomList
                     .Where(room => GetSprinklerCount(room, placementSummary) > 0)
                     .OrderBy(room => room.LevelName)
                     .ThenBy(room => room.Number))
        {
            int quantity = GetSprinklerCount(room, placementSummary);
            string familyName = ResolveFamilyName(room);
            SprinklerFamilyInfo family = ResolveFamily(familyName);
            bool usingPlacedCount = IsUsingPlacedCount(room, placementSummary);

            detailRows.Add(new MaterialTakeoffItem
            {
                ItemType = "Sprinkler",
                RoomNumber = room.Number,
                RoomName = room.Name,
                LevelName = room.LevelName,
                Manufacturer = family?.Manufacturer ?? ExtractManufacturerPrefix(familyName),
                FamilyName = familyName,
                HazardClassification = ResolveHazard(room),
                Description = BuildDescription(familyName, family),
                Quantity = quantity,
                Unit = "EA",
                Source = usingPlacedCount ? "Placed" : "Proposed"
            });
        }

        IList<RoomFittingTakeoff> fittingTakeoffs = FittingTakeoffCalculator.Calculate(
            schematicPipeRouting,
            pipePlacementSummary);
        bool hasPipeData = (schematicPipeRouting?.Segments?.Count ?? 0) > 0;

        if (detailRows.Count == 0 && !hasPipeData && fittingTakeoffs.Count == 0)
        {
            return detailRows;
        }

        List<MaterialTakeoffItem> sprinklerSummaryRows = detailRows
            .Where(item => string.Equals(item.ItemType, "Sprinkler", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Manufacturer + "|" + item.FamilyName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                MaterialTakeoffItem first = group.First();
                return new MaterialTakeoffItem
                {
                    ItemType = "Summary",
                    Manufacturer = first.Manufacturer,
                    FamilyName = first.FamilyName,
                    Description = "Project total — " + first.FamilyName,
                    Quantity = group.Sum(item => item.Quantity),
                    Unit = "EA",
                    Source = "Project",
                    IsSummaryRow = true
                };
            })
            .ToList();

        detailRows.AddRange(sprinklerSummaryRows);
        AppendPipeRows(detailRows, schematicPipeRouting, pipePlacementSummary);
        AppendFittingRows(detailRows, fittingTakeoffs);
        return detailRows;
    }

    private static int GetSprinklerCount(RoomInfo room, SprinklerPlacementSummary placementSummary)
    {
        if (placementSummary?.RoomResults != null)
        {
            SprinklerPlacementRoomResult roomResult = placementSummary.RoomResults
                .FirstOrDefault(result => result.RoomRevitElementId == room.RevitElementId);
            if (roomResult?.PlacedCount > 0)
            {
                return roomResult.PlacedCount;
            }
        }

        return room.ProposedSprinklers?.Count ?? 0;
    }

    private static bool IsUsingPlacedCount(RoomInfo room, SprinklerPlacementSummary placementSummary)
    {
        if (placementSummary?.RoomResults == null)
        {
            return false;
        }

        SprinklerPlacementRoomResult roomResult = placementSummary.RoomResults
            .FirstOrDefault(result => result.RoomRevitElementId == room.RevitElementId);
        return roomResult?.PlacedCount > 0;
    }

    private static string ResolveFamilyName(RoomInfo room)
    {
        if (!string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName))
        {
            return room.SelectedSprinklerFamilyName;
        }

        if (!string.IsNullOrWhiteSpace(room.AutoSelectedSprinklerName))
        {
            return room.AutoSelectedSprinklerName;
        }

        return "Unassigned sprinkler";
    }

    private static string ResolveHazard(RoomInfo room)
    {
        if (!string.IsNullOrWhiteSpace(room.ApprovedHazardClassification))
        {
            return room.ApprovedHazardClassification;
        }

        return room.SuggestedHazardClassification ?? string.Empty;
    }

    private SprinklerFamilyInfo ResolveFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

        return familySelector.GetAvailableFamilies()
            .FirstOrDefault(family => string.Equals(family.DisplayName, familyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(family.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(family.Model, familyName, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDescription(string familyName, SprinklerFamilyInfo family)
    {
        if (family == null)
        {
            return familyName;
        }

        return family.DisplayName;
    }

    private static string ExtractManufacturerPrefix(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return string.Empty;
        }

        int separatorIndex = familyName.IndexOf(' ');
        return separatorIndex > 0 ? familyName.Substring(0, separatorIndex) : string.Empty;
    }

    private static void AppendPipeRows(
        List<MaterialTakeoffItem> detailRows,
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary)
    {
        List<PipeSegment> segments = schematicPipeRouting?.Segments?.ToList() ?? new List<PipeSegment>();
        if (segments.Count == 0)
        {
            return;
        }

        Dictionary<int, PipePlacementRoomResult> placedRooms = (pipePlacementSummary?.RoomResults ?? new List<PipePlacementRoomResult>())
            .Where(result => result.RoomRevitElementId > 0)
            .GroupBy(result => result.RoomRevitElementId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (IGrouping<string, PipeSegment> group in segments
                     .GroupBy(segment => segment.RoomRevitElementId + "|" + segment.SegmentType + "|" + segment.DiameterInches.ToString("0.##"))
                     .OrderBy(group => group.First().RoomNumber)
                     .ThenBy(group => group.First().SegmentType))
        {
            PipeSegment first = group.First();
            double schematicLength = group.Sum(segment => segment.LengthFeet);
            placedRooms.TryGetValue(first.RoomRevitElementId, out PipePlacementRoomResult placedRoom);
            bool usesPlaced = PlacedPipeTakeoffCalculator.UsesPlacedLengthForGroup(
                first.SegmentType,
                first.DiameterInches,
                placedRoom);
            double quantity = PlacedPipeTakeoffCalculator.ResolvePipeLengthFeet(
                first.SegmentType,
                first.DiameterInches,
                schematicLength,
                placedRoom);

            detailRows.Add(new MaterialTakeoffItem
            {
                ItemType = "Pipe",
                RoomNumber = first.RoomNumber,
                RoomName = first.RoomName,
                LevelName = first.LevelName,
                FamilyName = first.DiameterInches.ToString("0.##") + "\" " + first.SegmentType,
                HazardClassification = string.Empty,
                Description = first.SegmentType + " — " + first.DiameterInches.ToString("0.##") + "\" pipe",
                Quantity = quantity,
                Unit = "FT",
                Source = usesPlaced ? "Placed" : "Schematic"
            });
        }

        AppendQuantitySummaries(
            detailRows,
            detailRows.Where(item => string.Equals(item.ItemType, "Pipe", StringComparison.OrdinalIgnoreCase)),
            item => item.FamilyName,
            item => item.FamilyName,
            "Project total — ");
    }

    private static void AppendFittingRows(
        List<MaterialTakeoffItem> detailRows,
        IList<RoomFittingTakeoff> fittingTakeoffs)
    {
        if (fittingTakeoffs == null || fittingTakeoffs.Count == 0)
        {
            return;
        }

        foreach (RoomFittingTakeoff takeoff in fittingTakeoffs)
        {
            string source = takeoff.UsesPlacedFittings ? "Placed" : "Schematic";
            AddFittingRow(detailRows, takeoff, "Fitting", "1.25\" Elbow", takeoff.Elbow125Count, source);
            AddFittingRow(detailRows, takeoff, "Fitting", "1.25\" Tee", takeoff.Tee125Count, source);
            AddFittingRow(detailRows, takeoff, "Fitting", "4\" Elbow", takeoff.Elbow4InchCount, source);
            AddFittingRow(detailRows, takeoff, "Riser Assembly", "Wet riser assembly", takeoff.RiserAssemblyCount, source);
            AddFittingRow(detailRows, takeoff, "Valve", "OS&Y control valve", takeoff.ValveCount, source);
        }

        AppendQuantitySummaries(
            detailRows,
            detailRows.Where(item =>
                string.Equals(item.ItemType, "Fitting", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ItemType, "Valve", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ItemType, "Riser Assembly", StringComparison.OrdinalIgnoreCase)),
            item => item.ItemType + "|" + item.FamilyName,
            item => item.FamilyName,
            "Project total — ");
    }

    private static void AddFittingRow(
        List<MaterialTakeoffItem> detailRows,
        RoomFittingTakeoff takeoff,
        string itemType,
        string familyName,
        int quantity,
        string source)
    {
        if (quantity <= 0)
        {
            return;
        }

        detailRows.Add(new MaterialTakeoffItem
        {
            ItemType = itemType,
            RoomNumber = takeoff.RoomNumber,
            RoomName = takeoff.RoomName,
            LevelName = takeoff.LevelName,
            FamilyName = familyName,
            Description = familyName + " (" + itemType.ToLowerInvariant() + ")",
            Quantity = quantity,
            Unit = "EA",
            Source = source
        });
    }

    private static void AppendQuantitySummaries(
        List<MaterialTakeoffItem> detailRows,
        IEnumerable<MaterialTakeoffItem> items,
        Func<MaterialTakeoffItem, string> groupKeySelector,
        Func<MaterialTakeoffItem, string> familyNameSelector,
        string descriptionPrefix)
    {
        foreach (IGrouping<string, MaterialTakeoffItem> group in items
                     .GroupBy(groupKeySelector)
                     .OrderBy(group => group.Key))
        {
            MaterialTakeoffItem first = group.First();
            detailRows.Add(new MaterialTakeoffItem
            {
                ItemType = "Summary",
                FamilyName = familyNameSelector(first),
                Description = descriptionPrefix + familyNameSelector(first),
                Quantity = group.Sum(item => item.Quantity),
                Unit = first.Unit,
                Source = "Project",
                IsSummaryRow = true
            });
        }
    }
}
