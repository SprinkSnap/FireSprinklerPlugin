using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Data;

public sealed class SprinklerCatalogLoadResult
{
    public IList<SprinklerFamilyInfo> Families { get; set; } = new List<SprinklerFamilyInfo>();

    public IList<string> Categories { get; set; } = new List<string>();

    public string LibraryName { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    public IList<string> Messages { get; set; } = new List<string>();
}

public static class SprinklerCatalogLoader
{
    public const string DefaultCatalogFileName = "sprinkler_catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static SprinklerCatalogLoadResult Load(string catalogPath = null)
    {
        SprinklerCatalogLoadResult result = new SprinklerCatalogLoadResult();
        string resolvedPath = ResolveCatalogPath(catalogPath, result.Messages);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            result.Messages.Add("Sprinkler catalog file was not found. Using built-in fallback catalog.");
            return LoadEmbeddedFallback(result);
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            SprinklerCatalogDocument document = JsonSerializer.Deserialize<SprinklerCatalogDocument>(json, JsonOptions);
            if (document?.Sprinklers == null || document.Sprinklers.Count == 0)
            {
                result.Messages.Add("Sprinkler catalog file contained no sprinkler records. Using built-in fallback catalog.");
                return LoadEmbeddedFallback(result);
            }

            result.SourcePath = resolvedPath;
            result.SourceKind = string.IsNullOrWhiteSpace(catalogPath) ? "Default" : "External";
            result.LibraryName = string.IsNullOrWhiteSpace(document.LibraryName)
                ? "SprinkSnap Listed Family Library"
                : document.LibraryName;
            result.Categories = document.Categories?.Where(category => !string.IsNullOrWhiteSpace(category)).ToList()
                ?? new List<string>();
            result.Families = document.Sprinklers
                .Select(record => ToFamilyInfo(record, result.LibraryName))
                .Where(family => family != null)
                .ToList();
            result.Messages.Add(
                "Loaded "
                + result.Families.Count
                + " listed sprinkler famil"
                + (result.Families.Count == 1 ? "y" : "ies")
                + " from "
                + resolvedPath
                + ".");
            return result;
        }
        catch (Exception ex)
        {
            result.Messages.Add("Failed to load sprinkler catalog: " + ex.Message);
            return LoadEmbeddedFallback(result);
        }
    }

    public static string GetDefaultCatalogPath()
    {
        SprinklerCatalogLoadResult probe = new SprinklerCatalogLoadResult();
        return ResolveCatalogPath(null, probe.Messages);
    }

    private static SprinklerCatalogLoadResult LoadEmbeddedFallback(SprinklerCatalogLoadResult result)
    {
        string embeddedPath = ResolveCatalogPath(null, new List<string>());
        if (!string.IsNullOrWhiteSpace(embeddedPath) && File.Exists(embeddedPath))
        {
            try
            {
                string json = File.ReadAllText(embeddedPath);
                SprinklerCatalogDocument document = JsonSerializer.Deserialize<SprinklerCatalogDocument>(json, JsonOptions);
                if (document?.Sprinklers?.Count > 0)
                {
                    result.SourcePath = embeddedPath;
                    result.SourceKind = "Embedded";
                    result.LibraryName = document.LibraryName ?? "SprinkSnap Listed Family Library";
                    result.Categories = document.Categories?.ToList() ?? new List<string>();
                    result.Families = document.Sprinklers.Select(record => ToFamilyInfo(record, result.LibraryName))
                        .Where(family => family != null)
                        .ToList();
                    return result;
                }
            }
            catch
            {
                // Fall through to hardcoded fallback below.
            }
        }

        result.SourceKind = "BuiltInFallback";
        result.LibraryName = "SprinkSnap Built-In Fallback Catalog";
        result.Categories = SprinklerFamilySelector.VikingCategories.ToList();
        result.Families = SprinklerFamilySelector.CreateBuiltInFallbackFamilies().ToList();
        result.Messages.Add("Loaded " + result.Families.Count + " built-in fallback sprinkler families.");
        return result;
    }

    private static string ResolveCatalogPath(string catalogPath, IList<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath))
        {
            return catalogPath;
        }

        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            messages?.Add("Custom catalog path was not found: " + catalogPath);
        }

        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            string adjacentPath = Path.Combine(assemblyDirectory, "Data", DefaultCatalogFileName);
            if (File.Exists(adjacentPath))
            {
                return adjacentPath;
            }

            string flatPath = Path.Combine(assemblyDirectory, DefaultCatalogFileName);
            if (File.Exists(flatPath))
            {
                return flatPath;
            }
        }

        string repoPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "SprinkSnap.Core",
            "Data",
            DefaultCatalogFileName);
        if (File.Exists(repoPath))
        {
            return repoPath;
        }

        return null;
    }

    private static SprinklerFamilyInfo ToFamilyInfo(SprinklerCatalogRecord record, string libraryName)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Manufacturer) || string.IsNullOrWhiteSpace(record.Model))
        {
            return null;
        }

        double maxDistanceFromWall = record.MaxDistanceFromWallFeet > 0
            ? record.MaxDistanceFromWallFeet
            : record.MaxSpacingFeet / 2.0;

        string listedFamilyId = string.IsNullOrWhiteSpace(record.ListedFamilyId)
            ? record.Manufacturer
              + ":"
              + record.Model
              + ":"
              + record.Orientation
              + ":K"
              + record.KFactor.ToString("0.0")
            : record.ListedFamilyId;

        return new SprinklerFamilyInfo
        {
            ListedFamilyId = listedFamilyId,
            Manufacturer = record.Manufacturer,
            LibraryName = libraryName,
            Category = record.Category,
            Model = record.Model,
            Sin = string.IsNullOrWhiteSpace(record.Sin) ? record.Model : record.Sin,
            Series = record.Series,
            FamilyName = string.IsNullOrWhiteSpace(record.FamilyName)
                ? record.SprinklerType + " " + record.Orientation + " K" + record.KFactor.ToString("0.0")
                : record.FamilyName,
            SprinklerType = record.SprinklerType,
            KFactor = record.KFactor,
            Orientation = string.IsNullOrWhiteSpace(record.Orientation) ? "Pendent" : record.Orientation,
            ResponseType = record.ResponseType,
            CoverageType = record.CoverageType,
            StorageUse = record.StorageUse,
            MaxSpacingFeet = record.MaxSpacingFeet,
            MaxCoverageAreaSquareFeet = record.MaxCoverageAreaSquareFeet,
            MaxDistanceFromWallFeet = maxDistanceFromWall,
            SupportedHazardClassifications = record.AllowedHazards?.ToList() ?? new List<string>(),
            SupportedCeilingClassifications = record.AllowedCeilingTypes?.ToList() ?? new List<string>(),
            TemperatureRatings = record.TemperatureRatings?.ToList() ?? new List<string>(),
            FinishOptions = record.FinishOptions?.ToList() ?? new List<string>(),
            RevitFamilyPath = record.RevitFamilyPath,
            RevitTypeName = record.RevitTypeName,
            TechnicalDataSheetUrl = record.TechnicalDataSheetUrl,
            ListingNotes = record.ListingNotes,
            RecognitionSource = "Catalog"
        };
    }
}
