using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface IHazardClassificationRule
{
    string Name { get; }

    HazardClassificationResult Evaluate(RoomInfo room);
}

public sealed class KeywordHazardRule : IHazardClassificationRule
{
    private readonly IReadOnlyList<string> keywords;
    private readonly string classification;
    private readonly string reason;
    private readonly double confidence;

    public KeywordHazardRule(
        string name,
        IEnumerable<string> keywords,
        string classification,
        string reason,
        double confidence)
    {
        Name = name;
        this.keywords = keywords.ToList();
        this.classification = classification;
        this.reason = reason;
        this.confidence = confidence;
    }

    public string Name { get; }

    public HazardClassificationResult Evaluate(RoomInfo room)
    {
        string searchText = string.Join(
            " ",
            room.Name,
            room.Number,
            room.OccupancyClassification,
            room.CeilingType);

        foreach (string keyword in keywords)
        {
            if (searchText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new HazardClassificationResult
                {
                    SuggestedClassification = classification,
                    RuleName = Name,
                    Reason = reason + " Matched keyword: " + keyword + ".",
                    Confidence = confidence
                };
            }
        }

        return null;
    }
}

public sealed class DefaultLightHazardRule : IHazardClassificationRule
{
    public string Name => "Default Light Hazard Review";

    public HazardClassificationResult Evaluate(RoomInfo room)
    {
        return new HazardClassificationResult
        {
            SuggestedClassification = HazardClassification.LightHazard,
            RuleName = Name,
            Reason = "No higher-hazard rule matched. Treat as a Light Hazard suggestion until designer approval.",
            Confidence = 0.25
        };
    }
}

public static class NFPA13Rules
{
    // NFPA 13 (2025) classifications remain suggestion-only here. Designer approval is mandatory before
    // sprinkler placement because actual hazard classification depends on contents, heat release,
    // storage height, process hazards, and owner-specific use.
    public static IReadOnlyList<IHazardClassificationRule> DefaultHazardRules { get; } = new List<IHazardClassificationRule>
    {
        new KeywordHazardRule(
            "Extra Hazard Group 2 Process Keywords",
            new[] { "industrial process", "flammable liquid", "paint spray", "explosive", "chemical process" },
            HazardClassification.ExtraHazardGroup2,
            "Industrial process or high heat-release content may require Extra Hazard Group 2 review.",
            0.80),
        new KeywordHazardRule(
            "Extra Hazard Group 1 Workshop Keywords",
            new[] { "workshop", "fabrication", "woodworking", "machine shop", "repair shop" },
            HazardClassification.ExtraHazardGroup1,
            "Workshop or fabrication use commonly requires Extra Hazard Group 1 review.",
            0.75),
        new KeywordHazardRule(
            "Ordinary Hazard Group 2 Equipment Keywords",
            new[] { "mechanical", "electrical", "boiler", "generator", "equipment", "laundry", "kitchen" },
            HazardClassification.OrdinaryHazardGroup2,
            "Equipment or service use commonly requires Ordinary Hazard Group 2 review.",
            0.70),
        new KeywordHazardRule(
            "Ordinary Hazard Group 1 Storage Keywords",
            new[] { "storage", "stock", "supply", "janitor", "receiving", "warehouse" },
            HazardClassification.OrdinaryHazardGroup1,
            "Storage or support use commonly requires Ordinary Hazard Group 1 review unless contents justify a higher hazard.",
            0.65),
        new KeywordHazardRule(
            "Light Hazard Assembly and Office Keywords",
            new[] { "office", "conference", "classroom", "meeting", "lobby", "corridor", "toilet", "restroom", "reception" },
            HazardClassification.LightHazard,
            "Office, education, and similar low-combustibility occupancies commonly align with Light Hazard review.",
            0.60),
        new DefaultLightHazardRule()
    };
}

