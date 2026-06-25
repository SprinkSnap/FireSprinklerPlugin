using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitSprinklerCatalogMerger
{
    public static IList<SprinklerFamilyInfo> Merge(
        IEnumerable<SprinklerFamilyInfo> catalogFamilies,
        IEnumerable<SprinklerFamilyInfo> loadedFamilies)
    {
        Dictionary<string, SprinklerFamilyInfo> mergedFamilies = new Dictionary<string, SprinklerFamilyInfo>(
            StringComparer.OrdinalIgnoreCase);

        foreach (SprinklerFamilyInfo family in catalogFamilies)
        {
            mergedFamilies[CreateSprinklerFamilyKey(family)] = family;
        }

        foreach (SprinklerFamilyInfo family in loadedFamilies)
        {
            mergedFamilies[CreateSprinklerFamilyKey(family)] = family;
        }

        return mergedFamilies.Values
            .OrderBy(family => family.Manufacturer)
            .ThenBy(family => family.Category)
            .ThenBy(family => family.Model)
            .ToList();
    }

    public static string CreateSprinklerFamilyKey(SprinklerFamilyInfo family)
    {
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
}
