using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Mapping;

public static class SprinklerFamilyMappingService
{
    public static string GetCatalogFamilyKey(SprinklerFamilyInfo family)
    {
        if (family == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(family.ListedFamilyId))
        {
            return family.ListedFamilyId;
        }

        return family.Manufacturer
            + "|"
            + family.Model
            + "|"
            + family.Sin
            + "|"
            + family.Orientation
            + "|"
            + family.KFactor.ToString("0.0");
    }

    public static void ApplyOverrides(
        IEnumerable<SprinklerFamilyInfo> catalog,
        IEnumerable<SprinklerFamilyMappingOverride> overrides)
    {
        if (catalog == null || overrides == null)
        {
            return;
        }

        Dictionary<string, SprinklerFamilyMappingOverride> overrideMap = overrides
            .Where(item => !string.IsNullOrWhiteSpace(item.CatalogFamilyKey))
            .GroupBy(item => item.CatalogFamilyKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (SprinklerFamilyInfo family in catalog)
        {
            if (!overrideMap.TryGetValue(GetCatalogFamilyKey(family), out SprinklerFamilyMappingOverride mappingOverride))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mappingOverride.RevitFamilySymbolId))
            {
                family.RevitFamilySymbolId = mappingOverride.RevitFamilySymbolId;
            }

            if (!string.IsNullOrWhiteSpace(mappingOverride.RevitFamilyName))
            {
                family.RevitFamilyName = mappingOverride.RevitFamilyName;
            }

            if (!string.IsNullOrWhiteSpace(mappingOverride.RevitTypeName))
            {
                family.RevitTypeName = mappingOverride.RevitTypeName;
            }

            family.IsLoadedInProject = true;
            family.RecognitionSource = "Designer family mapping";
        }
    }

    public static IList<LoadedRevitSymbolOption> GetLoadedRevitSymbolOptions(IEnumerable<SprinklerFamilyInfo> catalog)
    {
        List<LoadedRevitSymbolOption> options = new List<LoadedRevitSymbolOption> { LoadedRevitSymbolOption.Empty };
        HashSet<string> seenSymbolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SprinklerFamilyInfo family in catalog ?? Array.Empty<SprinklerFamilyInfo>())
        {
            if (!family.IsLoadedInProject || string.IsNullOrWhiteSpace(family.RevitFamilySymbolId))
            {
                continue;
            }

            if (!seenSymbolIds.Add(family.RevitFamilySymbolId))
            {
                continue;
            }

            options.Add(new LoadedRevitSymbolOption
            {
                RevitFamilySymbolId = family.RevitFamilySymbolId,
                RevitFamilyName = string.IsNullOrWhiteSpace(family.RevitFamilyName)
                    ? family.FamilyName
                    : family.RevitFamilyName,
                RevitTypeName = family.RevitTypeName,
                DisplayName = (string.IsNullOrWhiteSpace(family.RevitFamilyName)
                        ? family.FamilyName
                        : family.RevitFamilyName)
                    + " : "
                    + family.RevitTypeName
            });
        }

        return options
            .Skip(1)
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Prepend(LoadedRevitSymbolOption.Empty)
            .ToList();
    }

    public static IList<FamilyMappingRow> BuildMappingRows(
        IEnumerable<SprinklerFamilyInfo> catalog,
        IEnumerable<SprinklerFamilyMappingOverride> overrides)
    {
        List<SprinklerFamilyInfo> catalogList = catalog?.ToList() ?? new List<SprinklerFamilyInfo>();
        ApplyOverrides(catalogList, overrides);

        Dictionary<string, SprinklerFamilyInfo> uniqueFamilies = new Dictionary<string, SprinklerFamilyInfo>(
            StringComparer.OrdinalIgnoreCase);
        foreach (SprinklerFamilyInfo family in catalogList)
        {
            string key = GetCatalogFamilyKey(family);
            if (!uniqueFamilies.ContainsKey(key))
            {
                uniqueFamilies[key] = family;
            }
        }

        List<FamilyMappingRow> rows = new List<FamilyMappingRow>();
        foreach (SprinklerFamilyInfo family in uniqueFamilies.Values.OrderBy(item => item.Manufacturer).ThenBy(item => item.Model))
        {
            rows.Add(new FamilyMappingRow
            {
                CatalogFamilyKey = GetCatalogFamilyKey(family),
                Manufacturer = family.Manufacturer,
                Model = family.Model,
                Sin = family.Sin,
                DisplayName = family.DisplayName,
                RevitFamilySymbolId = family.RevitFamilySymbolId,
                RevitFamilyName = string.IsNullOrWhiteSpace(family.RevitFamilyName) ? family.FamilyName : family.RevitFamilyName,
                RevitTypeName = family.RevitTypeName,
                MappingStatus = EvaluateFamilyMappingStatus(family),
                IsLoadedInProject = family.IsLoadedInProject
            });
        }

        return rows;
    }

    public static SprinklerFamilyInfo ResolveFamilyForRoom(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        List<SprinklerFamilyInfo> families = catalog?.ToList() ?? new List<SprinklerFamilyInfo>();
        if (families.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName))
        {
            SprinklerFamilyInfo byDisplayName = families.FirstOrDefault(family =>
                string.Equals(family.DisplayName, room.SelectedSprinklerFamilyName, StringComparison.OrdinalIgnoreCase));
            if (byDisplayName != null)
            {
                return byDisplayName;
            }
        }

        if (!string.IsNullOrWhiteSpace(room.AutoSelectedSprinklerName))
        {
            return families.FirstOrDefault(family =>
                string.Equals(family.DisplayName, room.AutoSelectedSprinklerName, StringComparison.OrdinalIgnoreCase));
        }

        return families.FirstOrDefault(family => family.IsLoadedInProject) ?? families.FirstOrDefault();
    }

    public static bool IsFamilyMappedForPlacement(SprinklerFamilyInfo family)
    {
        if (family == null)
        {
            return false;
        }

        if (string.Equals(family.Manufacturer, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(family.RevitFamilySymbolId);
        }

        return !string.IsNullOrWhiteSpace(family.RevitFamilySymbolId)
            || (family.IsLoadedInProject
                && !string.IsNullOrWhiteSpace(family.RevitFamilyName)
                && !string.IsNullOrWhiteSpace(family.RevitTypeName));
    }

    public static string EvaluateFamilyMappingStatus(SprinklerFamilyInfo family)
    {
        if (family == null)
        {
            return "Unmapped";
        }

        if (IsFamilyMappedForPlacement(family))
        {
            return family.IsLoadedInProject ? "Mapped" : "Mapped (Catalog)";
        }

        if (string.Equals(family.Manufacturer, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Unmapped Revit Family";
        }

        return "Needs Mapping";
    }

    public static void UpdateRoomMappingStatuses(
        IEnumerable<RoomInfo> rooms,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        foreach (RoomInfo room in rooms ?? Array.Empty<RoomInfo>())
        {
            SprinklerFamilyInfo family = ResolveFamilyForRoom(room, catalog);
            room.RevitFamilyMappingStatus = family == null
                ? "No Sprinkler Selected"
                : EvaluateFamilyMappingStatus(family);
        }
    }

    public static PlacementPreflightSummary ValidatePlacementReadiness(
        IEnumerable<RoomInfo> rooms,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        PlacementPreflightSummary summary = new PlacementPreflightSummary();
        foreach (RoomInfo room in rooms ?? Array.Empty<RoomInfo>())
        {
            PlacementPreflightRoomResult roomResult = new PlacementPreflightRoomResult
            {
                RoomRevitElementId = room.RevitElementId,
                RoomNumber = room.Number,
                RoomName = room.Name,
                SelectedSprinkler = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
                    ? room.AutoSelectedSprinklerName
                    : room.SelectedSprinklerFamilyName
            };

            if (room.RequiresExceptionReview)
            {
                roomResult.MappingStatus = "Exception";
                roomResult.CanPlace = false;
                roomResult.Message = "Room requires designer exception review.";
                summary.ExceptionRoomCount++;
                summary.Rooms.Add(roomResult);
                continue;
            }

            if (!room.DesignerApproved)
            {
                roomResult.MappingStatus = "Not Approved";
                roomResult.CanPlace = false;
                roomResult.Message = "Hazard approval required.";
                summary.Rooms.Add(roomResult);
                continue;
            }

            if (room.ProposedSprinklers.Count == 0)
            {
                roomResult.MappingStatus = "No Layout";
                roomResult.CanPlace = false;
                roomResult.Message = "No proposed sprinkler locations.";
                summary.Rooms.Add(roomResult);
                continue;
            }

            SprinklerFamilyInfo family = ResolveFamilyForRoom(room, catalog);
            roomResult.MappingStatus = family == null
                ? "No Sprinkler Selected"
                : EvaluateFamilyMappingStatus(family);
            room.RevitFamilyMappingStatus = roomResult.MappingStatus;

            if (family != null && IsFamilyMappedForPlacement(family))
            {
                roomResult.CanPlace = true;
                roomResult.Message = "Ready for Revit placement.";
                summary.ReadyRoomCount++;
                summary.MappedRoomCount++;
            }
            else
            {
                roomResult.CanPlace = false;
                roomResult.Message = family == null
                    ? "Select a sprinkler in Sprinkler Review."
                    : "Map the listed family to a loaded Revit type in Settings.";
                summary.UnmappedRoomCount++;
            }

            summary.Rooms.Add(roomResult);
        }

        summary.CanPlaceAll = summary.ReadyRoomCount > 0 && summary.UnmappedRoomCount == 0 && summary.ExceptionRoomCount == 0;
        if (summary.UnmappedRoomCount > 0)
        {
            summary.Messages.Add(summary.UnmappedRoomCount + " room(s) have unmapped sprinkler families.");
        }

        if (summary.ExceptionRoomCount > 0)
        {
            summary.Messages.Add(summary.ExceptionRoomCount + " room(s) require exception review.");
        }

        if (summary.ReadyRoomCount > 0)
        {
            summary.Messages.Add(summary.ReadyRoomCount + " room(s) are ready for placement.");
        }

        return summary;
    }
}
