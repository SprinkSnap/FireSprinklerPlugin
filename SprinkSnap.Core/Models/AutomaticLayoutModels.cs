using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public static class CeilingClassification
{
    public const string Flat = "Flat";
    public const string Sloped = "Sloped";
    public const string TBarSuspended = "T-bar/Suspended";
    public const string OpenStructure = "Open Structure";
    public const string Mixed = "Mixed";
    public const string Uncertain = "Uncertain";
}

public static class LayoutStatus
{
    public const string NotStarted = "Not Started";
    public const string Ready = "Ready";
    public const string Compliant = "Compliant";
    public const string NonCompliant = "Noncompliant";
    public const string ReviewRequired = "Review Required";
}

public sealed class CeilingIntelligenceResult
{
    public string Classification { get; set; } = CeilingClassification.Uncertain;

    public string Summary { get; set; } = "Ceiling data has not been analyzed.";

    public double ConfidenceScore { get; set; }

    public bool RequiresReview { get; set; } = true;
}

public sealed class SprinklerFamilyInfo
{
    public string Manufacturer { get; set; } = string.Empty;

    public string LibraryName { get; set; } = string.Empty;

    public string FamilyName { get; set; } = string.Empty;

    public string DisplayName => Manufacturer + " - " + FamilyName;

    public double KFactor { get; set; }

    public string Orientation { get; set; } = "Pendent";

    public double MaxSpacingFeet { get; set; }

    public double MaxCoverageAreaSquareFeet { get; set; }

    public double MaxDistanceFromWallFeet { get; set; }

    public IList<string> SupportedHazardClassifications { get; set; } = new List<string>();

    public IList<string> SupportedCeilingClassifications { get; set; } = new List<string>();

    public string ListingNotes { get; set; } = string.Empty;
}

public sealed class LayoutMarker
{
    public Point3D Location { get; set; } = new Point3D();

    public bool IsCompliant { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class LayoutValidationResult
{
    public bool IsCompliant { get; set; }

    public string Status { get; set; } = LayoutStatus.NotStarted;

    public double ConfidenceScore { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();

    public IList<LayoutMarker> Markers { get; set; } = new List<LayoutMarker>();
}

public sealed class AutomaticLayoutResult
{
    public bool CanPlaceAutomatically { get; set; }

    public string Status { get; set; } = LayoutStatus.NotStarted;

    public double ConfidenceScore { get; set; }

    public IList<SprinklerPlacementCandidate> Candidates { get; set; } = new List<SprinklerPlacementCandidate>();

    public IList<LayoutMarker> PreviewMarkers { get; set; } = new List<LayoutMarker>();

    public IList<string> Messages { get; set; } = new List<string>();
}

