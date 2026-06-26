using System;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public sealed class Nfpa13HydraulicDesignCriteria
{
    public string HazardClassification { get; set; } = string.Empty;

    public double DesignDensityGpmPerSqFt { get; set; }

    public double RemoteAreaSquareFeet { get; set; }

    public double HoseStreamAllowanceGpm { get; set; }

    public string NfpaReference { get; set; } = string.Empty;
}

public static class Nfpa13HydraulicDesignTable
{
    public static Nfpa13HydraulicDesignCriteria GetCriteria(string hazardClassification)
    {
        string normalized = NormalizeHazard(hazardClassification);
        switch (normalized)
        {
            case HazardClassification.OrdinaryHazardGroup1:
                return Create("Ordinary Hazard Group 1", 0.15, 1500, 250, BuildDesignCriteriaReference());
            case HazardClassification.OrdinaryHazardGroup2:
                return Create("Ordinary Hazard Group 2", 0.20, 1500, 250, BuildDesignCriteriaReference());
            case HazardClassification.ExtraHazardGroup1:
                return Create("Extra Hazard Group 1", 0.30, 2500, 500, BuildDesignCriteriaReference());
            case HazardClassification.ExtraHazardGroup2:
                return Create("Extra Hazard Group 2", 0.40, 2500, 500, BuildDesignCriteriaReference());
            default:
                return Create("Light Hazard", 0.10, 1500, 100, BuildDesignCriteriaReference());
        }
    }

    public static string NormalizeHazard(string hazardClassification)
    {
        if (string.IsNullOrWhiteSpace(hazardClassification))
        {
            return HazardClassification.LightHazard;
        }

        if (string.Equals(hazardClassification, HazardClassification.LightHazard, StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("Light", StringComparison.OrdinalIgnoreCase))
        {
            return HazardClassification.LightHazard;
        }

        if (string.Equals(hazardClassification, HazardClassification.OrdinaryHazardGroup1, StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("OH1", StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("Ordinary Hazard Group 1", StringComparison.OrdinalIgnoreCase))
        {
            return HazardClassification.OrdinaryHazardGroup1;
        }

        if (string.Equals(hazardClassification, HazardClassification.OrdinaryHazardGroup2, StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("OH2", StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("Ordinary Hazard Group 2", StringComparison.OrdinalIgnoreCase))
        {
            return HazardClassification.OrdinaryHazardGroup2;
        }

        if (string.Equals(hazardClassification, HazardClassification.ExtraHazardGroup1, StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("EH1", StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("Extra Hazard Group 1", StringComparison.OrdinalIgnoreCase))
        {
            return HazardClassification.ExtraHazardGroup1;
        }

        if (string.Equals(hazardClassification, HazardClassification.ExtraHazardGroup2, StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("EH2", StringComparison.OrdinalIgnoreCase)
            || hazardClassification.Contains("Extra Hazard Group 2", StringComparison.OrdinalIgnoreCase))
        {
            return HazardClassification.ExtraHazardGroup2;
        }

        return hazardClassification;
    }

    private static string BuildDesignCriteriaReference()
    {
        return Nfpa13Edition.References.DesignCriteriaTable
            + " / "
            + Nfpa13Edition.References.SinglePointDesignCriteria;
    }

    private static Nfpa13HydraulicDesignCriteria Create(
        string displayName,
        double density,
        double remoteArea,
        double hoseStream,
        string nfpaReference)
    {
        return new Nfpa13HydraulicDesignCriteria
        {
            HazardClassification = displayName,
            DesignDensityGpmPerSqFt = density,
            RemoteAreaSquareFeet = remoteArea,
            HoseStreamAllowanceGpm = hoseStream,
            NfpaReference = nfpaReference
        };
    }
}
