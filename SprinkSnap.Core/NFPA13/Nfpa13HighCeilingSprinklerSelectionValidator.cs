using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public sealed class Nfpa13HighCeilingSprinklerSelectionResult
{
    public bool IsCompliant { get; set; } = true;

    public bool AppliesHighCeilingRules { get; set; }

    public double CeilingHeightFeet { get; set; }

    public string HazardClassification { get; set; } = string.Empty;

    public string NfpaReference { get; set; } = Nfpa13Edition.References.HighCeilingSprinklerSelection;

    public IList<string> Violations { get; set; } = new List<string>();

    public string Summary { get; set; } = string.Empty;
}

public static class Nfpa13HighCeilingSprinklerSelectionValidator
{
    public const double MinimumKFactorForOh2PlusHighCeiling = 11.2;

    public const double MaximumExtendedCoverageKFactorForHighCeiling = 22.4;

    public static Nfpa13HighCeilingSprinklerSelectionResult Validate(RoomInfo room, SprinklerFamilyInfo family)
    {
        Nfpa13HighCeilingSprinklerSelectionResult result = new Nfpa13HighCeilingSprinklerSelectionResult
        {
            CeilingHeightFeet = room?.CeilingHeightFeet ?? 0.0,
            HazardClassification = room?.ApprovedHazardClassification ?? string.Empty
        };

        if (family == null)
        {
            result.IsCompliant = false;
            result.Violations.Add("A listed sprinkler family must be selected before high-ceiling validation.");
            result.Summary = string.Join(" ", result.Violations);
            return result;
        }

        if (room == null || room.CeilingHeightFeet <= Nfpa13HighCeilingDesignCriteriaAdjuster.HighCeilingThresholdFeet)
        {
            result.Summary = "High-ceiling sprinkler selection rules do not apply at this ceiling height.";
            return result;
        }

        result.AppliesHighCeilingRules = true;
        string normalizedHazard = Nfpa13HydraulicDesignTable.NormalizeHazard(room.ApprovedHazardClassification);

        if (IsOrdinaryHazardGroup1OrHigher(normalizedHazard) && IsSidewallSprinkler(family))
        {
            result.Violations.Add(
                "Sidewall sprinklers are not permitted for "
                + DescribeHazard(normalizedHazard)
                + " occupancies with ceiling heights above 30 ft ("
                + Nfpa13Edition.Section("10.3.2")
                + ").");
        }

        if (IsOrdinaryHazardGroup2OrHigher(normalizedHazard))
        {
            if (family.KFactor < MinimumKFactorForOh2PlusHighCeiling)
            {
                result.Violations.Add(
                    "Sprinklers with K-factor below 11.2 are not permitted for "
                    + DescribeHazard(normalizedHazard)
                    + " occupancies with ceiling heights above 30 ft ("
                    + Nfpa13Edition.Section("9.4.5")
                    + ").");
            }

            if (Nfpa13HighCeilingDesignCriteriaAdjuster.IsExtendedCoverageFamily(family)
                && family.KFactor <= MaximumExtendedCoverageKFactorForHighCeiling)
            {
                result.Violations.Add(
                    "Extended-coverage sprinklers with K-factor of 22.4 or less are not permitted for "
                    + DescribeHazard(normalizedHazard)
                    + " occupancies with ceiling heights above 30 ft ("
                    + Nfpa13Edition.Section("11.2.1.1")
                    + ").");
            }
        }

        if (normalizedHazard == HazardClassification.OrdinaryHazardGroup2
            && IsStandardResponseStandardCoverage(family))
        {
            result.Violations.Add(
                "Standard-response standard-coverage sprinklers are not permitted for Ordinary Hazard Group 2 "
                + "occupancies with ceiling heights above 30 ft. Use quick-response sprinklers with K-11.2 or larger ("
                + Nfpa13Edition.Section("10.2.5")
                + ").");
        }

        result.IsCompliant = result.Violations.Count == 0;
        result.Summary = result.IsCompliant
            ? "Selected sprinkler complies with "
              + Nfpa13Edition.References.HighCeilingSprinklerSelection
              + " for "
              + room.CeilingHeightFeet.ToString("N0")
              + " ft ceiling."
            : string.Join(" ", result.Violations);

        return result;
    }

    public static bool IsCompliant(RoomInfo room, SprinklerFamilyInfo family)
    {
        return Validate(room, family).IsCompliant;
    }

    private static bool IsOrdinaryHazardGroup1OrHigher(string normalizedHazard)
    {
        return normalizedHazard == HazardClassification.OrdinaryHazardGroup1
            || IsOrdinaryHazardGroup2OrHigher(normalizedHazard);
    }

    private static bool IsOrdinaryHazardGroup2OrHigher(string normalizedHazard)
    {
        return normalizedHazard == HazardClassification.OrdinaryHazardGroup2
            || normalizedHazard == HazardClassification.ExtraHazardGroup1
            || normalizedHazard == HazardClassification.ExtraHazardGroup2;
    }

    private static bool IsSidewallSprinkler(SprinklerFamilyInfo family)
    {
        return ContainsIgnoreCase(family.Orientation, "Sidewall")
            || ContainsIgnoreCase(family.Category, "Sidewall");
    }

    private static bool IsStandardResponseStandardCoverage(SprinklerFamilyInfo family)
    {
        return IsStandardResponse(family) && IsStandardCoverage(family);
    }

    private static bool IsStandardResponse(SprinklerFamilyInfo family)
    {
        return ContainsIgnoreCase(family.ResponseType, "Standard Response");
    }

    private static bool IsStandardCoverage(SprinklerFamilyInfo family)
    {
        return string.IsNullOrWhiteSpace(family.CoverageType)
            || ContainsIgnoreCase(family.CoverageType, "Standard Coverage");
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeHazard(string normalizedHazard)
    {
        switch (normalizedHazard)
        {
            case HazardClassification.OrdinaryHazardGroup1:
                return "Ordinary Hazard Group 1";
            case HazardClassification.OrdinaryHazardGroup2:
                return "Ordinary Hazard Group 2";
            case HazardClassification.ExtraHazardGroup1:
                return "Extra Hazard Group 1";
            case HazardClassification.ExtraHazardGroup2:
                return "Extra Hazard Group 2";
            default:
                return normalizedHazard;
        }
    }
}
