using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public sealed class Nfpa13CodeReference
{
    public string EditionYear { get; set; } = Nfpa13Edition.Year;

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
        string normalized = Nfpa13HydraulicDesignTable.NormalizeHazard(hazardClassification);
        switch (normalized)
        {
            case HazardClassification.LightHazard:
                return CreateHazardReference(
                    "Hazard Classification",
                    Nfpa13Edition.References.LightHazardOccupancy,
                    "Light Hazard Occupancies",
                    "Low quantity and combustibility of contents with low heat release rates.",
                    "Verify architect occupancy labels and future tenant fit-out before final approval.");
            case HazardClassification.OrdinaryHazardGroup1:
                return CreateHazardReference(
                    "Hazard Classification",
                    Nfpa13Edition.References.OrdinaryHazardGroup1,
                    "Ordinary Hazard Group 1",
                    "Moderate quantity and combustibility with moderate stockpile heights up to 8 ft (2.4 m).",
                    "Confirm storage is not above OH1 limits before accepting classification.");
            case HazardClassification.OrdinaryHazardGroup2:
                return CreateHazardReference(
                    "Hazard Classification",
                    Nfpa13Edition.References.OrdinaryHazardGroup2,
                    "Ordinary Hazard Group 2",
                    "Higher combustibility or moderate quantities with higher heat release rates.",
                    "Review ceiling height, obstructions, and commodity classification in storage areas.");
            case HazardClassification.ExtraHazardGroup1:
                return CreateHazardReference(
                    "Hazard Classification",
                    Nfpa13Edition.References.ExtraHazardGroup1,
                    "Extra Hazard Group 1",
                    "High combustibility or high heat release industrial processes.",
                    "Coordinate with process equipment obstructions and in-rack protection needs.");
            case HazardClassification.ExtraHazardGroup2:
                return CreateHazardReference(
                    "Hazard Classification",
                    Nfpa13Edition.References.ExtraHazardGroup2,
                    "Extra Hazard Group 2",
                    "Very high combustibility with substantial quantities and rapid heat release.",
                    "Expect tighter spacing, higher demand, and more frequent designer exceptions.");
            default:
                return CreateHazardReference(
                    "General",
                    Nfpa13Edition.References.HazardClassification,
                    "Occupancy Hazard Classification",
                    "Designer must classify each area based on contents, combustibility, and storage height.",
                    "SprinkSnap suggests only — designer approval is mandatory.");
        }
    }

    public static Nfpa13CodeReference GetSpacingReference()
    {
        return CreateReference(
            "Spacing",
            Nfpa13Edition.References.StandardSpraySpacingTable,
            "Standard Spray Spacing",
            "Maximum spacing and area of coverage depend on hazard and sprinkler listing.",
            "SprinkSnap validates listing constraints before placement.");
    }

    public static Nfpa13CodeReference GetObstructionReference()
    {
        return CreateReference(
            "Obstructions",
            Nfpa13Edition.References.ObstructionsToDischarge + " and "
            + Nfpa13Edition.References.StandardSprayObstructions,
            "Obstruction to Sprinkler Discharge",
            "Sprinklers must be located to avoid obstructions to discharge pattern development.",
            "Use Clash Detection after layout generation to resolve conflicts.");
    }

    public static Nfpa13CodeReference GetHydraulicsReference()
    {
        return CreateReference(
            "Hydraulics",
            Nfpa13Edition.References.HydraulicCalculationProcedures,
            "Hydraulic Calculation Procedures",
            "System demand must be calculated using Hazen-Williams with adequate safety margin. "
            + "Plot supply and demand on N^1.85 graph paper per "
            + Nfpa13Edition.References.HydraulicGraphSheet
            + ".",
            "Compare available water supply curve to calculated demand.");
    }

    public static Nfpa13CodeReference GetWaterSupplyReference()
    {
        return CreateReference(
            "Water Supply",
            Nfpa13Edition.References.WaterSupplyInformation,
            "Water Supply Information",
            "Design must be based on reliable water supply data including static and residual pressure, "
            + "flow at residual, and the date and time of the hydrant test.",
            "Enter hydrant test data before final hydraulic sign-off.");
    }

    public static Nfpa13CodeReference GetDesignCriteriaReference()
    {
        return CreateReference(
            "Hydraulic Design",
            Nfpa13Edition.References.DesignCriteriaTable + " / "
            + Nfpa13Edition.References.SinglePointDesignCriteria,
            "Single-Point Density/Area Design Criteria",
            "New and existing systems use single-point density and remote area values from Table 19.2.3.1.1 "
            + "per Section 19.2.3.1.1.",
            "Density/area curves were removed in the 2025 edition.");
    }

    public static IReadOnlyList<Nfpa13CodeReference> GetAllReferences()
    {
        return new List<Nfpa13CodeReference>
        {
            GetSpacingReference(),
            GetObstructionReference(),
            GetHydraulicsReference(),
            GetWaterSupplyReference(),
            GetDesignCriteriaReference(),
            GetHazardReference(HazardClassification.LightHazard)
        };
    }

    private static Nfpa13CodeReference CreateHazardReference(
        string topic,
        string section,
        string title,
        string summary,
        string designerNote)
    {
        return CreateReference(topic, section, title, summary, designerNote);
    }

    private static Nfpa13CodeReference CreateReference(
        string topic,
        string section,
        string title,
        string summary,
        string designerNote)
    {
        return new Nfpa13CodeReference
        {
            EditionYear = Nfpa13Edition.Year,
            Topic = topic,
            Section = section,
            Title = title,
            Summary = summary,
            DesignerNote = designerNote
        };
    }
}
