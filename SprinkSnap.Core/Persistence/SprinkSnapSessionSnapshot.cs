using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Core.Persistence;

public sealed class ModelAnalysisFingerprint
{
    public string Hash { get; set; } = string.Empty;

    public DateTime CapturedUtc { get; set; }

    public int RoomCount { get; set; }

    public int LinkedModelCount { get; set; }
}

public sealed class PersistedRoomSnapshot
{
    public int RevitElementId { get; set; }

    public string Number { get; set; } = string.Empty;

    public double AreaSquareFeet { get; set; }

    public double CeilingHeightFeet { get; set; }

    public string ApprovedHazardClassification { get; set; } = string.Empty;

    public string SuggestedHazardClassification { get; set; } = string.Empty;

    public bool DesignerApproved { get; set; }

    public string SelectedSprinklerFamilyName { get; set; } = string.Empty;

    public string AutoSelectedSprinklerName { get; set; } = string.Empty;

    public string SprinklerSelectionStatus { get; set; } = string.Empty;

    public string SprinklerSelectionReason { get; set; } = string.Empty;

    public string RevitFamilyMappingStatus { get; set; } = string.Empty;

    public string LayoutStatus { get; set; } = string.Empty;

    public double LayoutConfidenceScore { get; set; }

    public bool RequiresExceptionReview { get; set; }

    public string ExceptionReason { get; set; } = string.Empty;

    public IList<SprinklerPlacementCandidate> ProposedSprinklers { get; set; } = new List<SprinklerPlacementCandidate>();
}

public sealed class SprinkSnapSessionSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTime SavedUtc { get; set; }

    public string DocumentKey { get; set; } = string.Empty;

    public ModelAnalysisFingerprint Fingerprint { get; set; } = new ModelAnalysisFingerprint();

    public SprinkSnapSessionProgress SessionProgress { get; set; } = new SprinkSnapSessionProgress();

    public WaterSupplyInput WaterSupply { get; set; } = new WaterSupplyInput();

    public IList<SprinklerFamilyMappingOverride> FamilyMappingOverrides { get; set; } = new List<SprinklerFamilyMappingOverride>();

    public IList<LinkedModelScanOption> LinkedModelScanOptions { get; set; } = new List<LinkedModelScanOption>();

    public IList<PersistedRoomSnapshot> Rooms { get; set; } = new List<PersistedRoomSnapshot>();

    public ClashDetectionSummary ClashSummary { get; set; } = new ClashDetectionSummary();

    public SprinklerPlacementSummary PlacementSummary { get; set; } = new SprinklerPlacementSummary();
}

public sealed class ModelChangeAssessment
{
    public bool HasBaseline { get; set; }

    public bool IsStale { get; set; }

    public int ChangedRoomCount { get; set; }

    public int AddedRoomCount { get; set; }

    public int RemovedRoomCount { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();

    public IList<int> ChangedRoomRevitElementIds { get; set; } = new List<int>();
}
