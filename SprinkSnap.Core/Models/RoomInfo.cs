using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core;

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

    public bool CeilingGridDetected { get; set; }

    public int ObstructionCount { get; set; }

    public bool HasCriticalGeometry { get; set; }

    public string CeilingClassification { get; set; } =
        FireSprinklerPlugin.SprinkSnap.Core.Models.CeilingClassification.Uncertain;

    public string CeilingIntelligenceSummary { get; set; } = string.Empty;

    public string LayoutStatus { get; set; } =
        FireSprinklerPlugin.SprinkSnap.Core.Models.LayoutStatus.NotStarted;

    public double LayoutConfidenceScore { get; set; }

    public bool RequiresExceptionReview { get; set; }

    public string ExceptionReason { get; set; } = string.Empty;

    public string SelectedSprinklerFamilyName { get; set; } = string.Empty;

    public string AutoSelectedSprinklerName { get; set; } = string.Empty;

    public string SprinklerSelectionStatus { get; set; } = "Not Evaluated";

    public string SprinklerSelectionReason { get; set; } = string.Empty;

    public int CompatibleSprinklerCount { get; set; }

    public string AlternateSprinklerSummary { get; set; } = string.Empty;

    public string RevitFamilyMappingStatus { get; set; } = "Not Evaluated";

    public IList<SprinklerPlacementCandidate> ProposedSprinklers { get; set; } = new List<SprinklerPlacementCandidate>();

    public IList<LayoutMarker> LayoutPreviewMarkers { get; set; } = new List<LayoutMarker>();

    public string ExistingHazardClassification { get; set; } = string.Empty;

    public string SuggestedHazardClassification { get; set; } = HazardClassification.LightHazard;

    public string ApprovedHazardClassification { get; set; } = string.Empty;

    public bool DesignerApproved { get; set; }

    public string SuggestionReason { get; set; } = string.Empty;
}

