using System;
using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public static class HazardClassification
{
    public const string LightHazard = "Light Hazard";
    public const string OrdinaryHazardGroup1 = "OH1";
    public const string OrdinaryHazardGroup2 = "OH2";
    public const string ExtraHazardGroup1 = "EH1";
    public const string ExtraHazardGroup2 = "EH2";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        LightHazard,
        OrdinaryHazardGroup1,
        OrdinaryHazardGroup2,
        ExtraHazardGroup1,
        ExtraHazardGroup2
    };

    public static bool IsSupported(string value)
    {
        foreach (string classification in All)
        {
            if (string.Equals(classification, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class HazardClassificationResult
{
    public string SuggestedClassification { get; set; } = HazardClassification.LightHazard;

    public string RuleName { get; set; } = "Default";

    public string Reason { get; set; } = "No higher-hazard rule matched; designer review is required.";

    public double Confidence { get; set; }
}

