using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
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
        SprinklerPlacementSummary placementSummary = null)
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

        if (detailRows.Count == 0)
        {
            return detailRows;
        }

        List<MaterialTakeoffItem> summaryRows = detailRows
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

        detailRows.AddRange(summaryRows);
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
}
