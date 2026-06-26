using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public static class Nfpa13HighCeilingDesignCriteriaAdjuster
{
    public const double HighCeilingThresholdFeet = 30.0;

    public const double Oh2UpperCeilingThresholdFeet = 40.0;

    public const double AreaIncreaseFactor = 1.30;

    public const double Oh2MidCeilingMinimumDensityGpmPerSqFt = 0.37;

    public const double HighCeilingMinimumDensityGpmPerSqFt = 0.45;

    public const double ExtendedCoverageAreaExemptionKFactor = 25.2;

    public static bool RequiresHighCeilingEvaluation(string normalizedOrDisplayHazard)
    {
        string normalized = Nfpa13HydraulicDesignTable.NormalizeHazard(normalizedOrDisplayHazard);
        return normalized == HazardClassification.OrdinaryHazardGroup1
            || normalized == HazardClassification.OrdinaryHazardGroup2
            || normalized == HazardClassification.ExtraHazardGroup1
            || normalized == HazardClassification.ExtraHazardGroup2;
    }

    public static Nfpa13HydraulicDesignCriteria Apply(
        string normalizedHazard,
        Nfpa13HydraulicDesignCriteria baseCriteria,
        double ceilingHeightFeet,
        double representativeKFactor,
        bool usesExtendedCoverageK252OrGreater)
    {
        if (baseCriteria == null)
        {
            throw new ArgumentNullException(nameof(baseCriteria));
        }

        Nfpa13HydraulicDesignCriteria adjusted = Clone(baseCriteria);
        if (ceilingHeightFeet <= HighCeilingThresholdFeet)
        {
            return adjusted;
        }

        switch (normalizedHazard)
        {
            case HazardClassification.OrdinaryHazardGroup1:
                adjusted.RemoteAreaSquareFeet = IncreaseAreaByThirtyPercent(adjusted.RemoteAreaSquareFeet);
                adjusted.AppliesHighCeilingAdjustment = true;
                adjusted.HighCeilingAdjustmentSummary =
                    "Remote area increased 30% for ceiling height "
                    + ceilingHeightFeet.ToString("N0")
                    + " ft (>30 ft).";
                break;
            case HazardClassification.OrdinaryHazardGroup2:
                ApplyOrdinaryHazardGroup2Adjustments(
                    adjusted,
                    ceilingHeightFeet,
                    usesExtendedCoverageK252OrGreater);
                break;
            case HazardClassification.ExtraHazardGroup1:
            case HazardClassification.ExtraHazardGroup2:
                if (adjusted.DesignDensityGpmPerSqFt < HighCeilingMinimumDensityGpmPerSqFt)
                {
                    adjusted.DesignDensityGpmPerSqFt = HighCeilingMinimumDensityGpmPerSqFt;
                    adjusted.AppliesHighCeilingAdjustment = true;
                    adjusted.HighCeilingAdjustmentSummary =
                        "Design density increased to 0.45 gpm/sq ft for ceiling height "
                        + ceilingHeightFeet.ToString("N0")
                        + " ft (>30 ft).";
                }

                break;
        }

        if (adjusted.AppliesHighCeilingAdjustment)
        {
            adjusted.NfpaReference = AppendHighCeilingReference(adjusted.NfpaReference);
        }

        return adjusted;
    }

    public static bool UsesExtendedCoverageK252OrGreater(SprinklerFamilyInfo family)
    {
        if (family == null || family.KFactor < ExtendedCoverageAreaExemptionKFactor)
        {
            return false;
        }

        return IsExtendedCoverageFamily(family);
    }

    public static bool IsExtendedCoverageFamily(SprinklerFamilyInfo family)
    {
        if (family == null)
        {
            return false;
        }

        return ContainsExtendedCoverage(family.CoverageType)
            || ContainsExtendedCoverage(family.Category);
    }

    private static void ApplyOrdinaryHazardGroup2Adjustments(
        Nfpa13HydraulicDesignCriteria adjusted,
        double ceilingHeightFeet,
        bool usesExtendedCoverageK252OrGreater)
    {
        if (ceilingHeightFeet > Oh2UpperCeilingThresholdFeet)
        {
            adjusted.DesignDensityGpmPerSqFt = Math.Max(
                adjusted.DesignDensityGpmPerSqFt,
                HighCeilingMinimumDensityGpmPerSqFt);

            if (!usesExtendedCoverageK252OrGreater)
            {
                adjusted.RemoteAreaSquareFeet = IncreaseAreaByThirtyPercent(adjusted.RemoteAreaSquareFeet);
                adjusted.HighCeilingAdjustmentSummary =
                    "Design density increased to 0.45 gpm/sq ft and remote area increased 30% for ceiling height "
                    + ceilingHeightFeet.ToString("N0")
                    + " ft (>40 ft).";
            }
            else
            {
                adjusted.HighCeilingAdjustmentSummary =
                    "Design density increased to 0.45 gpm/sq ft for ceiling height "
                    + ceilingHeightFeet.ToString("N0")
                    + " ft (>40 ft). Extended-coverage K-25.2+ sprinkler — remote area not increased.";
            }

            adjusted.AppliesHighCeilingAdjustment = true;
            return;
        }

        adjusted.DesignDensityGpmPerSqFt = Math.Max(
            adjusted.DesignDensityGpmPerSqFt,
            Oh2MidCeilingMinimumDensityGpmPerSqFt);
        adjusted.AppliesHighCeilingAdjustment = true;
        adjusted.HighCeilingAdjustmentSummary =
            "Design density increased to 0.37 gpm/sq ft for ceiling height "
            + ceilingHeightFeet.ToString("N0")
            + " ft (>30 ft and ≤40 ft).";
    }

    private static double IncreaseAreaByThirtyPercent(double remoteAreaSquareFeet)
    {
        return remoteAreaSquareFeet * AreaIncreaseFactor;
    }

    private static string AppendHighCeilingReference(string nfpaReference)
    {
        string highCeilingReference = Nfpa13Edition.References.HighCeilingDesignCriteria;
        if (string.IsNullOrWhiteSpace(nfpaReference))
        {
            return highCeilingReference;
        }

        if (nfpaReference.Contains(highCeilingReference, StringComparison.OrdinalIgnoreCase))
        {
            return nfpaReference;
        }

        return nfpaReference + " / " + highCeilingReference;
    }

    private static bool ContainsExtendedCoverage(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("Extended", StringComparison.OrdinalIgnoreCase);
    }

    private static Nfpa13HydraulicDesignCriteria Clone(Nfpa13HydraulicDesignCriteria source)
    {
        return new Nfpa13HydraulicDesignCriteria
        {
            HazardClassification = source.HazardClassification,
            DesignDensityGpmPerSqFt = source.DesignDensityGpmPerSqFt,
            RemoteAreaSquareFeet = source.RemoteAreaSquareFeet,
            HoseStreamAllowanceGpm = source.HoseStreamAllowanceGpm,
            NfpaReference = source.NfpaReference,
            AppliesHighCeilingAdjustment = false,
            HighCeilingAdjustmentSummary = string.Empty
        };
    }
}
