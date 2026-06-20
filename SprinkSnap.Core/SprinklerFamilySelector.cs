using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ISprinklerFamilySelector
{
    IReadOnlyList<SprinklerFamilyInfo> GetAvailableFamilies();
}

public sealed class SprinklerFamilySelector : ISprinklerFamilySelector
{
    public IReadOnlyList<SprinklerFamilyInfo> GetAvailableFamilies()
    {
        return DefaultFamilies;
    }

    public static IReadOnlyList<SprinklerFamilyInfo> DefaultFamilies { get; } = new List<SprinklerFamilyInfo>
    {
        CreateStandardSpray(
            "Generic Listed Library",
            "Standard Spray Pendent K5.6",
            "Pendent",
            5.6,
            15.0,
            225.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2),
        CreateStandardSpray(
            "Generic Listed Library",
            "Standard Spray Upright K8.0",
            "Upright",
            8.0,
            15.0,
            130.0,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2,
            HazardClassification.ExtraHazardGroup1),
        CreateStandardSpray(
            "Generic Listed Library",
            "Extra Hazard Upright K11.2",
            "Upright",
            11.2,
            12.0,
            100.0,
            HazardClassification.ExtraHazardGroup1,
            HazardClassification.ExtraHazardGroup2)
    };

    private static SprinklerFamilyInfo CreateStandardSpray(
        string manufacturer,
        string familyName,
        string orientation,
        double kFactor,
        double maxSpacingFeet,
        double maxCoverageAreaSquareFeet,
        params string[] supportedHazards)
    {
        return new SprinklerFamilyInfo
        {
            Manufacturer = manufacturer,
            LibraryName = "Default SprinkSnap Listed Family Library",
            FamilyName = familyName,
            Orientation = orientation,
            KFactor = kFactor,
            MaxSpacingFeet = maxSpacingFeet,
            MaxCoverageAreaSquareFeet = maxCoverageAreaSquareFeet,
            MaxDistanceFromWallFeet = maxSpacingFeet / 2.0,
            SupportedHazardClassifications = supportedHazards.ToList(),
            SupportedCeilingClassifications = new List<string>
            {
                CeilingClassification.Flat,
                CeilingClassification.TBarSuspended,
                CeilingClassification.OpenStructure
            },
            ListingNotes = "Placeholder listed-family constraints. Replace with manufacturer-specific Revit family metadata before production placement."
        };
    }
}

