using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ISprinklerFamilySelector
{
    IReadOnlyList<SprinklerFamilyInfo> GetAvailableFamilies();
}

public sealed class SprinklerFamilySelector : ISprinklerFamilySelector
{
    private readonly ISprinklerFamilySelector inner;

    public SprinklerFamilySelector(ISprinklerFamilySelector inner = null)
    {
        this.inner = inner ?? SprinklerCatalogService.Default;
    }

    public static IReadOnlyList<string> VikingCategories { get; } = new[]
    {
        "Attic Sprinklers",
        "Extended Coverage",
        "Institutional Sprinklers",
        "Interstitial Concealed Sprinklers",
        "Residential Sprinklers",
        "Solid Cone Spray Nozzles",
        "Standard Spray Quick Response",
        "Standard Spray Standard Response",
        "Storage CMDA",
        "Storage CMSA",
        "Storage ESFR",
        "Storage In-Rack",
        "Window HSW",
        "Window Pendent",
        "Window Sprinklers"
    };

    public IReadOnlyList<SprinklerFamilyInfo> GetAvailableFamilies()
    {
        return inner.GetAvailableFamilies();
    }

    public static IReadOnlyList<string> GetCatalogCategories()
    {
        IReadOnlyList<string> categories = SprinklerCatalogService.Default.GetCategories();
        return categories?.Count > 0 ? categories : VikingCategories;
    }

    internal static IReadOnlyList<SprinklerFamilyInfo> CreateBuiltInFallbackFamilies()
    {
        return new List<SprinklerFamilyInfo>
        {
            CreateSprinkler(
                "Viking",
                "Standard Spray Quick Response",
                "Microfast",
                "VK302",
                "VK302",
                "Standard Spray",
                "QR Pendent K5.6",
                "Pendent",
                "Quick Response",
                "Standard Coverage",
                "Non-storage",
                5.6,
                15.0,
                225.0,
                "Libraries/Viking/VK302/Viking_VK302.rfa",
                "VK302 QR Pendent K5.6",
                "https://www.vikinggroupinc.com",
                "UL/FM listed standard spray QR pendent for light and ordinary hazard occupancies.",
                HazardClassification.LightHazard,
                HazardClassification.OrdinaryHazardGroup1,
                HazardClassification.OrdinaryHazardGroup2)
        };
    }

    private static SprinklerFamilyInfo CreateSprinkler(
        string manufacturer,
        string category,
        string series,
        string model,
        string sin,
        string sprinklerType,
        string familyName,
        string orientation,
        string responseType,
        string coverageType,
        string storageUse,
        double kFactor,
        double maxSpacingFeet,
        double maxCoverageAreaSquareFeet,
        string revitFamilyPath,
        string revitTypeName,
        string technicalDataSheetUrl,
        string listingNotes,
        params string[] supportedHazards)
    {
        return new SprinklerFamilyInfo
        {
            Manufacturer = manufacturer,
            ListedFamilyId = manufacturer + ":" + model + ":" + orientation + ":K" + kFactor.ToString("0.0"),
            Category = category,
            Series = series,
            Model = model,
            Sin = sin,
            SprinklerType = sprinklerType,
            LibraryName = "SprinkSnap Built-In Fallback Catalog",
            FamilyName = familyName,
            Orientation = orientation,
            ResponseType = responseType,
            CoverageType = coverageType,
            StorageUse = storageUse,
            KFactor = kFactor,
            MaxSpacingFeet = maxSpacingFeet,
            MaxCoverageAreaSquareFeet = maxCoverageAreaSquareFeet,
            MaxDistanceFromWallFeet = maxSpacingFeet / 2.0,
            RevitFamilyPath = revitFamilyPath,
            RevitTypeName = revitTypeName,
            TechnicalDataSheetUrl = technicalDataSheetUrl,
            TemperatureRatings = new List<string> { "155F", "200F" },
            FinishOptions = new List<string> { "Brass", "Chrome", "White" },
            SupportedHazardClassifications = supportedHazards.ToList(),
            SupportedCeilingClassifications = new List<string>
            {
                CeilingClassification.Flat,
                CeilingClassification.TBarSuspended,
                CeilingClassification.OpenStructure
            },
            ListingNotes = listingNotes,
            RecognitionSource = "BuiltInFallback"
        };
    }
}
