using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Mapping;

public sealed class SprinklerFamilyMappingOverride
{
    public string CatalogFamilyKey { get; set; } = string.Empty;

    public string RevitFamilySymbolId { get; set; } = string.Empty;

    public string RevitFamilyName { get; set; } = string.Empty;

    public string RevitTypeName { get; set; } = string.Empty;
}

public sealed class FamilyMappingRow
{
    public string CatalogFamilyKey { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Sin { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RevitFamilySymbolId { get; set; } = string.Empty;

    public string RevitFamilyName { get; set; } = string.Empty;

    public string RevitTypeName { get; set; } = string.Empty;

    public string MappingStatus { get; set; } = "Unmapped";

    public bool IsLoadedInProject { get; set; }
}

public sealed class PlacementPreflightRoomResult
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string SelectedSprinkler { get; set; } = string.Empty;

    public string MappingStatus { get; set; } = string.Empty;

    public bool CanPlace { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class LoadedRevitSymbolOption
{
    public static LoadedRevitSymbolOption Empty { get; } = new LoadedRevitSymbolOption
    {
        RevitFamilySymbolId = string.Empty,
        RevitFamilyName = string.Empty,
        RevitTypeName = string.Empty,
        DisplayName = "(None — select loaded Revit type)"
    };

    public string RevitFamilySymbolId { get; set; } = string.Empty;

    public string RevitFamilyName { get; set; } = string.Empty;

    public string RevitTypeName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class PlacementPreflightSummary
{
    public int ReadyRoomCount { get; set; }

    public int MappedRoomCount { get; set; }

    public int UnmappedRoomCount { get; set; }

    public int ExceptionRoomCount { get; set; }

    public bool CanPlaceAll { get; set; }

    public IList<PlacementPreflightRoomResult> Rooms { get; set; } = new List<PlacementPreflightRoomResult>();

    public IList<string> Messages { get; set; } = new List<string>();
}
