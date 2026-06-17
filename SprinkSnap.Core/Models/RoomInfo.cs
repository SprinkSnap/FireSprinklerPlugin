using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public sealed class RoomInfo
{
    public int RevitElementId { get; set; }

    public string UniqueId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public double AreaSquareFeet { get; set; }

    public double AreaSquareMeters { get; set; }

    public double VolumeCubicFeet { get; set; }

    public double VolumeCubicMeters { get; set; }

    public double HeightFeet { get; set; }

    public double FloorToFloorHeightFeet { get; set; }

    public double CeilingHeightFeet { get; set; }

    public string CeilingType { get; set; } = string.Empty;

    public string CeilingTileInformation { get; set; } = string.Empty;

    public string OccupancyClassification { get; set; } = string.Empty;

    public int LevelId { get; set; }

    public string LevelName { get; set; } = string.Empty;

    public double FloorElevationFeet { get; set; }

    public double CeilingElevationFeet { get; set; }

    public double DeckElevationFeet { get; set; }

    public double ElevationBelowDeckFeet { get; set; }

    public IList<RoomBoundaryLoop> Boundaries { get; set; } = new List<RoomBoundaryLoop>();

    public IReadOnlyList<Point2D> BoundaryPolygon { get; set; } = new List<Point2D>();

    public Point2D Centroid { get; set; } = new Point2D();

    public double PerimeterFeet { get; set; }

    public double WidthFeet { get; set; }

    public double LengthFeet { get; set; }

    public double AspectRatio { get; set; }

    public double Slope { get; set; }

    public bool HasFlatCeiling { get; set; }

    public bool HasSlopedCeiling { get; set; }

    public bool HasMultiSlopeCeiling { get; set; }

    public bool HasIrregularGeometry { get; set; }

    public string RoomShape { get; set; } = "Unknown";

    public string ExistingHazardClassification { get; set; } = string.Empty;

    public string SuggestedHazardClassification { get; set; } = HazardClassification.LightHazard;

    public string ApprovedHazardClassification { get; set; } = string.Empty;

    public bool DesignerApproved { get; set; }

    public string SuggestionReason { get; set; } = string.Empty;
}

