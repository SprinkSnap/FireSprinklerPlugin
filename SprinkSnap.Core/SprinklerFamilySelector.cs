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
        CreateSprinkler(
            "Viking",
            "Standard Spray Quick Response",
            "VK302",
            "VK302",
            "QR Pendent K5.6",
            "Pendent",
            "Quick Response",
            "Standard Coverage",
            5.6,
            15.0,
            225.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2),
        CreateSprinkler(
            "Tyco",
            "Standard Spray Quick Response",
            "TY-FRB",
            "TY-FRB",
            "QR Pendent K5.6",
            "Pendent",
            "Quick Response",
            "Standard Coverage",
            5.6,
            15.0,
            225.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2),
        CreateSprinkler(
            "Reliable",
            "Standard Spray Quick Response",
            "F1FR56",
            "F1FR56",
            "QR Pendent K5.6",
            "Pendent",
            "Quick Response",
            "Standard Coverage",
            5.6,
            15.0,
            225.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2),
        CreateSprinkler(
            "Victaulic",
            "Standard Spray Quick Response",
            "V2703",
            "V2703",
            "QR Pendent K5.6",
            "Pendent",
            "Quick Response",
            "Standard Coverage",
            5.6,
            15.0,
            225.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2),
        CreateSprinkler(
            "Viking",
            "Standard Spray Standard Response",
            "VK100",
            "VK100",
            "SR Upright K5.6",
            "Upright",
            "Standard Response",
            "Standard Coverage",
            5.6,
            15.0,
            130.0,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2,
            HazardClassification.ExtraHazardGroup1),
        CreateSprinkler(
            "Tyco",
            "Storage CMDA",
            "TY-B",
            "TY-B",
            "CMDA Upright K8.0",
            "Upright",
            "Standard Response",
            "Storage",
            8.0,
            15.0,
            130.0,
            HazardClassification.OrdinaryHazardGroup1,
            HazardClassification.OrdinaryHazardGroup2,
            HazardClassification.ExtraHazardGroup1),
        CreateSprinkler(
            "Viking",
            "Storage CMSA",
            "VK590",
            "VK590",
            "CMSA Upright K11.2",
            "Upright",
            "Standard Response",
            "Storage",
            11.2,
            12.0,
            100.0,
            HazardClassification.ExtraHazardGroup1,
            HazardClassification.ExtraHazardGroup2),
        CreateSprinkler(
            "Reliable",
            "Storage ESFR",
            "JL-17",
            "JL-17",
            "ESFR Pendent K16.8",
            "Pendent",
            "Fast Response",
            "Storage",
            16.8,
            12.0,
            100.0,
            HazardClassification.ExtraHazardGroup1,
            HazardClassification.ExtraHazardGroup2),
        CreateSprinkler(
            "Viking",
            "Residential Sprinklers",
            "VK468",
            "VK468",
            "Residential Pendent K4.9",
            "Pendent",
            "Fast Response",
            "Residential",
            4.9,
            16.0,
            256.0,
            HazardClassification.LightHazard),
        CreateSprinkler(
            "Viking",
            "Window Sprinklers",
            "VK960",
            "VK960",
            "Window HSW K5.6",
            "Horizontal Sidewall",
            "Quick Response",
            "Window",
            5.6,
            8.0,
            80.0,
            HazardClassification.LightHazard,
            HazardClassification.OrdinaryHazardGroup1)
    };

    private static SprinklerFamilyInfo CreateSprinkler(
        string manufacturer,
        string category,
        string model,
        string sin,
        string familyName,
        string orientation,
        string responseType,
        string coverageType,
        double kFactor,
        double maxSpacingFeet,
        double maxCoverageAreaSquareFeet,
        params string[] supportedHazards)
    {
        return new SprinklerFamilyInfo
        {
            Manufacturer = manufacturer,
            Category = category,
            Model = model,
            Sin = sin,
            LibraryName = "Default SprinkSnap Listed Family Library",
            FamilyName = familyName,
            Orientation = orientation,
            ResponseType = responseType,
            CoverageType = coverageType,
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

