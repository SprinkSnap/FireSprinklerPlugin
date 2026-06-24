using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Data;

public sealed class SprinklerCatalogDocument
{
    public string SchemaVersion { get; set; } = "1.0";

    public IList<SprinklerCatalogRecord> Sprinklers { get; set; } = new List<SprinklerCatalogRecord>();
}

public sealed class SprinklerCatalogRecord
{
    public string ListedFamilyId { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Sin { get; set; } = string.Empty;

    public string SprinklerType { get; set; } = string.Empty;

    public string ResponseType { get; set; } = string.Empty;

    public string Orientation { get; set; } = string.Empty;

    public double KFactor { get; set; }

    public string CoverageType { get; set; } = string.Empty;

    public string StorageUse { get; set; } = string.Empty;

    public IList<string> AllowedHazards { get; set; } = new List<string>();

    public IList<string> AllowedCeilingTypes { get; set; } = new List<string>();

    public double MaxSpacingFeet { get; set; }

    public double MaxCoverageAreaSquareFeet { get; set; }

    public IList<string> TemperatureRatings { get; set; } = new List<string>();

    public IList<string> FinishOptions { get; set; } = new List<string>();

    public string RevitFamilyPath { get; set; } = string.Empty;

    public string RevitTypeName { get; set; } = string.Empty;

    public string TechnicalDataSheetUrl { get; set; } = string.Empty;
}

public sealed class SprinkSnapProjectPreferences
{
    public string PreferredManufacturer { get; set; } = "Viking";

    public string DefaultCategory { get; set; } = "Standard Spray Quick Response";

    public string DefaultOrientation { get; set; } = "Pendent";

    public double DefaultKFactor { get; set; } = 5.6;

    public bool AllowAlternateManufacturers { get; set; } = true;
}

