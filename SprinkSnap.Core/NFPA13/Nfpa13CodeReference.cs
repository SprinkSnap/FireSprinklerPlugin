using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public sealed class Nfpa13CodeReference
{
    public string Topic { get; set; } = string.Empty;

    public string Section { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DesignerNote { get; set; } = string.Empty;
}

public static class Nfpa13CodeReferenceLibrary
{
    public static IReadOnlyList<Nfpa13CodeReference> GetReferencesForTopic(string topic)
    {
        List<Nfpa13CodeReference> all = GetAllReferences().ToList();
        if (string.IsNullOrWhiteSpace(topic))
        {
            return all;
        }

        List<Nfpa13CodeReference> matches = new List<Nfpa13CodeReference>();
        foreach (Nfpa13CodeReference reference in all)
        {
            if (reference.Topic.Contains(topic, System.StringComparison.OrdinalIgnoreCase)
                || reference.Section.Contains(topic, System.StringComparison.OrdinalIgnoreCase)
                || reference.Title.Contains(topic, System.StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(reference);
            }
        }

        return matches.Count > 0 ? matches : all;
    }

    public static Nfpa13CodeReference GetHazardReference(string hazardClassification)
    {
        switch (hazardClassification)
        {
            case "Light Hazard":
                return new Nfpa13CodeReference
                {
                    Topic = "Hazard Classification",
                    Section = "NFPA 13 Section 5.3",
                    Title = "Light Hazard Occupancies",
                    Summary = "Low quantity and combustibility of contents with low heat release rates.",
                    DesignerNote = "Verify architect occupancy labels and future tenant fit-out before final approval."
                };
            case "Ordinary Hazard Group 1":
                return new Nfpa13CodeReference
                {
                    Topic = "Hazard Classification",
                    Section = "NFPA 13 Section 5.4.1",
                    Title = "Ordinary Hazard Group 1",
                    Summary = "Moderate quantity and combustibility with moderate stockpile heights.",
                    DesignerNote = "Confirm storage is not above OH1 limits before accepting classification."
                };
            case "Ordinary Hazard Group 2":
                return new Nfpa13CodeReference
                {
                    Topic = "Hazard Classification",
                    Section = "NFPA 13 Section 5.4.2",
                    Title = "Ordinary Hazard Group 2",
                    Summary = "Higher combustibility or moderate quantities with higher heat release rates.",
                    DesignerNote = "Review ceiling height, obstructions, and commodity classification in storage areas."
                };
            case "Extra Hazard Group 1":
                return new Nfpa13CodeReference
                {
                    Topic = "Hazard Classification",
                    Section = "NFPA 13 Section 5.5.1",
                    Title = "Extra Hazard Group 1",
                    Summary = "High combustibility or high heat release industrial processes.",
                    DesignerNote = "Coordinate with process equipment obstructions and in-rack protection needs."
                };
            case "Extra Hazard Group 2":
                return new Nfpa13CodeReference
                {
                    Topic = "Hazard Classification",
                    Section = "NFPA 13 Section 5.5.2",
                    Title = "Extra Hazard Group 2",
                    Summary = "Very high combustibility with substantial quantities and rapid heat release.",
                    DesignerNote = "Expect tighter spacing, higher demand, and more frequent designer exceptions."
                };
            default:
                return new Nfpa13CodeReference
                {
                    Topic = "General",
                    Section = "NFPA 13 Chapter 5",
                    Title = "Occupancy Hazard Classification",
                    Summary = "Designer must classify each area based on contents, combustibility, and storage height.",
                    DesignerNote = "SprinkSnap suggests only — designer approval is mandatory."
                };
        }
    }

    public static IReadOnlyList<Nfpa13CodeReference> GetAllReferences()
    {
        return new List<Nfpa13CodeReference>
        {
            new Nfpa13CodeReference
            {
                Topic = "Spacing",
                Section = "NFPA 13 Table 10.2.4.2.1(a)",
                Title = "Standard Spray Spacing",
                Summary = "Maximum spacing and area of coverage depend on hazard and sprinkler listing.",
                DesignerNote = "SprinkSnap validates listing constraints before placement."
            },
            new Nfpa13CodeReference
            {
                Topic = "Obstructions",
                Section = "NFPA 13 Section 10.2.6",
                Title = "Obstruction to Sprinkler Discharge",
                Summary = "Sprinklers must be located to avoid obstructions to discharge pattern development.",
                DesignerNote = "Use Clash Detection after layout generation to resolve conflicts."
            },
            new Nfpa13CodeReference
            {
                Topic = "Hydraulics",
                Section = "NFPA 13 Chapter 28",
                Title = "Hydraulic Calculation Procedures",
                Summary = "System demand must be calculated using Hazen-Williams with adequate safety margin.",
                DesignerNote = "Compare available water supply curve to calculated demand."
            },
            new Nfpa13CodeReference
            {
                Topic = "Water Supply",
                Section = "NFPA 13 Section 24.2",
                Title = "Water Supply Information",
                Summary = "Design must be based on reliable water supply data including static and residual pressure.",
                DesignerNote = "Enter hydrant test data before final hydraulic sign-off."
            },
            GetHazardReference("Light Hazard")
        };
    }
}
