using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public interface ISprinklerFamilyScanner
{
    IReadOnlyList<SprinklerFamilyInfo> ScanLoadedSprinklerFamilies(Document document);
}

public sealed class SprinklerFamilyScanner : ISprinklerFamilyScanner
{
    private readonly IReadOnlyList<SprinklerFamilyInfo> catalog;

    public SprinklerFamilyScanner()
        : this(new SprinklerFamilySelector().GetAvailableFamilies())
    {
    }

    public SprinklerFamilyScanner(IReadOnlyList<SprinklerFamilyInfo> catalog)
    {
        this.catalog = catalog;
    }

    public IReadOnlyList<SprinklerFamilyInfo> ScanLoadedSprinklerFamilies(Document document)
    {
        List<SprinklerFamilyInfo> recognizedFamilies = new List<SprinklerFamilyInfo>();

        IEnumerable<FamilySymbol> sprinklerSymbols = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Sprinklers)
            .WhereElementIsElementType()
            .OfType<FamilySymbol>();

        foreach (FamilySymbol symbol in sprinklerSymbols)
        {
            SprinklerFamilyInfo metadataFamily = ReadSprinkSnapMetadata(symbol);
            if (metadataFamily != null)
            {
                recognizedFamilies.Add(metadataFamily);
                continue;
            }

            SprinklerFamilyInfo catalogMatch = MatchCatalogByName(symbol);
            if (catalogMatch != null)
            {
                recognizedFamilies.Add(CloneAsLoadedFamily(catalogMatch, symbol, "Catalog name match"));
                continue;
            }

            recognizedFamilies.Add(CreateUnknownFamily(symbol));
        }

        return recognizedFamilies;
    }

    private static SprinklerFamilyInfo ReadSprinkSnapMetadata(FamilySymbol symbol)
    {
        string manufacturer = ReadString(symbol, "SS_Manufacturer");
        string model = ReadString(symbol, "SS_Model");
        string sin = ReadString(symbol, "SS_SIN");

        if (string.IsNullOrWhiteSpace(manufacturer)
            || string.IsNullOrWhiteSpace(model)
            || string.IsNullOrWhiteSpace(sin))
        {
            return null;
        }

        return new SprinklerFamilyInfo
        {
            ListedFamilyId = ReadString(symbol, "SS_ListedFamilyId"),
            Manufacturer = manufacturer,
            Category = ReadString(symbol, "SS_Category"),
            Model = model,
            Sin = sin,
            FamilyName = symbol.FamilyName,
            RevitFamilyName = symbol.FamilyName,
            RevitTypeName = symbol.Name,
            RevitFamilySymbolId = symbol.Id.IntegerValue.ToString(CultureInfo.InvariantCulture),
            KFactor = ReadDouble(symbol, "SS_KFactor"),
            Orientation = ReadString(symbol, "SS_Orientation"),
            ResponseType = ReadString(symbol, "SS_ResponseType"),
            CoverageType = ReadString(symbol, "SS_CoverageType"),
            MaxSpacingFeet = ReadDouble(symbol, "SS_MaxSpacingFt"),
            MaxCoverageAreaSquareFeet = ReadDouble(symbol, "SS_MaxCoverageAreaSqFt"),
            TechnicalDataSheetUrl = ReadString(symbol, "SS_TechnicalDataSheetUrl"),
            SupportedHazardClassifications = SplitList(ReadString(symbol, "SS_HazardCompatibility")),
            SupportedCeilingClassifications = SplitList(ReadString(symbol, "SS_CeilingCompatibility")),
            IsLoadedInProject = true,
            RecognitionSource = "SprinkSnap shared parameters"
        };
    }

    private SprinklerFamilyInfo MatchCatalogByName(FamilySymbol symbol)
    {
        string familyAndTypeName = (symbol.FamilyName + " " + symbol.Name).ToUpperInvariant();

        return catalog.FirstOrDefault(family =>
            ContainsToken(familyAndTypeName, family.Manufacturer)
            && (ContainsToken(familyAndTypeName, family.Model)
                || ContainsToken(familyAndTypeName, family.Sin)));
    }

    private static SprinklerFamilyInfo CloneAsLoadedFamily(
        SprinklerFamilyInfo catalogFamily,
        FamilySymbol symbol,
        string recognitionSource)
    {
        return new SprinklerFamilyInfo
        {
            ListedFamilyId = catalogFamily.ListedFamilyId,
            Manufacturer = catalogFamily.Manufacturer,
            LibraryName = catalogFamily.LibraryName,
            Category = catalogFamily.Category,
            Series = catalogFamily.Series,
            Model = catalogFamily.Model,
            Sin = catalogFamily.Sin,
            SprinklerType = catalogFamily.SprinklerType,
            FamilyName = catalogFamily.FamilyName,
            KFactor = catalogFamily.KFactor,
            Orientation = catalogFamily.Orientation,
            ResponseType = catalogFamily.ResponseType,
            CoverageType = catalogFamily.CoverageType,
            StorageUse = catalogFamily.StorageUse,
            MaxSpacingFeet = catalogFamily.MaxSpacingFeet,
            MaxCoverageAreaSquareFeet = catalogFamily.MaxCoverageAreaSquareFeet,
            MaxDistanceFromWallFeet = catalogFamily.MaxDistanceFromWallFeet,
            SupportedHazardClassifications = catalogFamily.SupportedHazardClassifications.ToList(),
            SupportedCeilingClassifications = catalogFamily.SupportedCeilingClassifications.ToList(),
            TemperatureRatings = catalogFamily.TemperatureRatings.ToList(),
            FinishOptions = catalogFamily.FinishOptions.ToList(),
            RevitFamilyPath = catalogFamily.RevitFamilyPath,
            RevitTypeName = symbol.Name,
            TechnicalDataSheetUrl = catalogFamily.TechnicalDataSheetUrl,
            IsLoadedInProject = true,
            RevitFamilyName = symbol.FamilyName,
            RevitFamilySymbolId = symbol.Id.IntegerValue.ToString(CultureInfo.InvariantCulture),
            RecognitionSource = recognitionSource,
            ListingNotes = catalogFamily.ListingNotes
        };
    }

    private static SprinklerFamilyInfo CreateUnknownFamily(FamilySymbol symbol)
    {
        return new SprinklerFamilyInfo
        {
            Manufacturer = "Unknown",
            Category = "Unmapped Revit Family",
            Model = symbol.Name,
            Sin = string.Empty,
            FamilyName = symbol.FamilyName,
            RevitFamilyName = symbol.FamilyName,
            RevitTypeName = symbol.Name,
            RevitFamilySymbolId = symbol.Id.IntegerValue.ToString(CultureInfo.InvariantCulture),
            IsLoadedInProject = true,
            RecognitionSource = "Unmapped loaded Revit family",
            ListingNotes = "Map this family/type to a SprinkSnap catalog record before using it for automatic layout."
        };
    }

    private static string ReadString(Element element, string parameterName)
    {
        Parameter parameter = element.LookupParameter(parameterName);
        return parameter?.AsString() ?? parameter?.AsValueString() ?? string.Empty;
    }

    private static double ReadDouble(Element element, string parameterName)
    {
        Parameter parameter = element.LookupParameter(parameterName);
        if (parameter == null)
        {
            return 0.0;
        }

        if (parameter.StorageType == StorageType.Double)
        {
            return parameter.AsDouble();
        }

        return double.TryParse(parameter.AsString() ?? parameter.AsValueString(), out double value)
            ? value
            : 0.0;
    }

    private static IList<string> SplitList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();
    }

    private static bool ContainsToken(string text, string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && text.IndexOf(token.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

