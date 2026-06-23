using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public sealed class ProjectSprinklerStandard
{
    public string PreferredManufacturer { get; set; } = string.Empty;

    public string PreferredCategory { get; set; } = string.Empty;

    public string PreferredOrientation { get; set; } = string.Empty;

    public double? PreferredKFactor { get; set; }

    public bool AllowAlternateManufacturers { get; set; } = true;
}

public sealed class CompatibleSprinklerSelection
{
    public SprinklerFamilyInfo SelectedFamily { get; set; }

    public IList<SprinklerFamilyInfo> CompatibleFamilies { get; set; } = new List<SprinklerFamilyInfo>();

    public IList<SprinklerFamilyInfo> AlternateFamilies { get; set; } = new List<SprinklerFamilyInfo>();

    public string Status { get; set; } = "Not Evaluated";

    public string Reason { get; set; } = string.Empty;
}

public interface ICompatibleSprinklerSelector
{
    CompatibleSprinklerSelection SelectForRoom(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog,
        ProjectSprinklerStandard projectStandard);
}

public sealed class CompatibleSprinklerSelector : ICompatibleSprinklerSelector
{
    public CompatibleSprinklerSelection SelectForRoom(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog,
        ProjectSprinklerStandard projectStandard)
    {
        List<SprinklerFamilyInfo> compatibleFamilies = catalog
            .Where(family => IsCompatible(room, family))
            .OrderBy(family => PreferenceScore(family, projectStandard))
            .ThenBy(family => family.Manufacturer)
            .ThenBy(family => family.Category)
            .ThenBy(family => family.Model)
            .ToList();

        CompatibleSprinklerSelection selection = new CompatibleSprinklerSelection
        {
            CompatibleFamilies = compatibleFamilies
        };

        if (compatibleFamilies.Count == 0)
        {
            selection.Status = "Review";
            selection.Reason = "No compatible listed sprinkler was found for the room hazard, ceiling classification, and current project standard.";
            return selection;
        }

        SprinklerFamilyInfo selectedFamily = compatibleFamilies[0];
        selection.SelectedFamily = selectedFamily;
        selection.AlternateFamilies = compatibleFamilies.Skip(1).ToList();

        if (selection.AlternateFamilies.Count > 0)
        {
            selection.Status = "Compatible - Alternates Available";
            selection.Reason = "Auto-selected the best match by listing compatibility and project standard preference. Alternates are available.";
        }
        else
        {
            selection.Status = "Compatible";
            selection.Reason = "Auto-selected the only compatible listed sprinkler for this room.";
        }

        return selection;
    }

    private static bool IsCompatible(RoomInfo room, SprinklerFamilyInfo family)
    {
        if (!HazardClassification.IsSupported(room.ApprovedHazardClassification))
        {
            return false;
        }

        if (!family.SupportedHazardClassifications.Contains(room.ApprovedHazardClassification))
        {
            return false;
        }

        if (!family.SupportedCeilingClassifications.Contains(room.CeilingClassification))
        {
            return false;
        }

        return true;
    }

    private static int PreferenceScore(SprinklerFamilyInfo family, ProjectSprinklerStandard projectStandard)
    {
        int score = 0;

        if (!Matches(projectStandard.PreferredManufacturer, family.Manufacturer))
        {
            score += projectStandard.AllowAlternateManufacturers ? 100 : 10000;
        }

        if (!Matches(projectStandard.PreferredCategory, family.Category))
        {
            score += 20;
        }

        if (!Matches(projectStandard.PreferredOrientation, family.Orientation))
        {
            score += 10;
        }

        if (projectStandard.PreferredKFactor.HasValue
            && Math.Abs(projectStandard.PreferredKFactor.Value - family.KFactor) > 0.001)
        {
            score += 5;
        }

        return score;
    }

    private static bool Matches(string preferredValue, string actualValue)
    {
        return string.IsNullOrWhiteSpace(preferredValue)
            || string.Equals(preferredValue, "All", StringComparison.Ordinal)
            || string.Equals(preferredValue, actualValue, StringComparison.Ordinal);
    }
}

