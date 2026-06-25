using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public enum SprinkSnapWorkflowStep
{
    AnalyzeModel,
    HazardReview,
    SprinklerReview,
    WaterSupply,
    GenerateDesign,
    ClashDetection,
    PlaceSprinklers,
    Hydraulics,
    Materials,
    Reports,
    Settings
}

public enum WorkflowStepStatus
{
    NotStarted,
    InProgress,
    Complete,
    Warning,
    Blocked
}

public sealed class WorkflowStepState
{
    public SprinkSnapWorkflowStep Step { get; set; }

    public WorkflowStepStatus Status { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class SprinkSnapSessionProgress
{
    public bool ModelAnalysisComplete { get; set; }

    public bool HazardReviewComplete { get; set; }

    public bool SprinklerReviewComplete { get; set; }

    public bool WaterSupplyComplete { get; set; }

    public bool DesignGenerated { get; set; }

    public bool ClashDetectionComplete { get; set; }

    public bool SprinklersPlacedInRevit { get; set; }

    public bool HydraulicsComplete { get; set; }

    public bool MaterialsComplete { get; set; }

    public bool ReportsExported { get; set; }

    public bool ReconciliationRequired { get; set; }
}

public sealed class SprinkSnapProjectState
{
    public IList<WorkflowStepState> Workflow { get; set; } = new List<WorkflowStepState>();

    public ModelAnalysisSummary ModelAnalysis { get; set; } = new ModelAnalysisSummary();

    public WaterSupplyInput WaterSupply { get; set; } = new WaterSupplyInput();

    public SprinkSnapSessionProgress SessionProgress { get; set; } = new SprinkSnapSessionProgress();

    public IList<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();

    public IList<ComplianceWarning> Warnings { get; set; } = new List<ComplianceWarning>();

    public IList<DesignException> Exceptions { get; set; } = new List<DesignException>();

    public ClashDetectionSummary ClashSummary { get; set; } = new ClashDetectionSummary();

    public SprinklerPlacementSummary PlacementSummary { get; set; } = new SprinklerPlacementSummary();

    public IList<SprinklerFamilyMappingOverride> FamilyMappingOverrides { get; set; } = new List<SprinklerFamilyMappingOverride>();

    public PlacementPreflightSummary PlacementPreflight { get; set; } = new PlacementPreflightSummary();

    public IList<LinkedModelScanOption> LinkedModelScanOptions { get; set; } = new List<LinkedModelScanOption>();

    public ModelChangeAssessment ModelChangeAssessment { get; set; } = new ModelChangeAssessment();

    public HydraulicCalculationResult HydraulicResult { get; set; } = new HydraulicCalculationResult();

    public WaterSupplyValidationResult WaterSupplyValidation { get; set; } = new WaterSupplyValidationResult();

    public ReportExportRequest ReportExport { get; set; } = new ReportExportRequest();

    public SchematicPipeRoutingSummary SchematicPipeRouting { get; set; } = new SchematicPipeRoutingSummary();

    public PipePlacementSummary PipePlacementSummary { get; set; } = new PipePlacementSummary();

    public bool PlaceSchematicFittingsWithPipes { get; set; } = true;

    public HydraulicSupplyAnchor HydraulicSupplyAnchor { get; set; } = new HydraulicSupplyAnchor();

    public SprinkSnapProjectPreferences Preferences { get; set; } = new SprinkSnapProjectPreferences();

    public IList<LoadedRevitSymbolOption> LoadedRevitSprinklerSymbols { get; set; } = new List<LoadedRevitSymbolOption>();
}

public sealed class ComplianceWarning
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

public sealed class DesignException
{
    public int? RevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string RequiredAction { get; set; } = string.Empty;
}

public sealed class ModelAnalysisSummary
{
    public int RoomCount { get; set; }

    public int SpaceCount { get; set; }

    public int SlopedCeilingCount { get; set; }

    public int MissingCeilingCount { get; set; }

    public int LinkedModelCount { get; set; }

    public int ObstructionZoneCount { get; set; }

    public int ExistingSprinklerCount { get; set; }

    public IList<string> RevitPhases { get; set; } = new List<string>();

    public IList<string> Warnings { get; set; } = new List<string>();
}

public sealed class WaterSupplyInput
{
    public double? StaticPressurePsi { get; set; }

    public double? ResidualPressurePsi { get; set; }

    public double? FlowAtResidualGpm { get; set; }

    public DateTime? HydrantTestDate { get; set; }

    public string ImportedSourcePath { get; set; } = string.Empty;
}

public sealed class WaterSupplyCurvePoint
{
    public double FlowGpm { get; set; }

    public double PressurePsi { get; set; }
}

public sealed class WaterSupplyValidationResult
{
    public bool IsAdequate { get; set; }

    public double SafetyMarginPsi { get; set; }

    public IList<WaterSupplyCurvePoint> Curve { get; set; } = new List<WaterSupplyCurvePoint>();

    public IList<string> Warnings { get; set; } = new List<string>();
}

public sealed class HydraulicNode
{
    public string NodeId { get; set; } = string.Empty;

    public Point3D Location { get; set; } = new Point3D();

    public double FlowGpm { get; set; }

    public double PressurePsi { get; set; }

    public double LengthFeet { get; set; }

    public double DiameterInches { get; set; }

    public double FrictionLossPsi { get; set; }

    public double VelocityFeetPerSecond { get; set; }

    public double VelocityLimitFeetPerSecond { get; set; }

    public bool ExceedsVelocityLimit { get; set; }

    public double SuggestedDiameterInches { get; set; }

    public string SegmentType { get; set; } = string.Empty;
}

public sealed class HydraulicCalculationResult
{
    public string ControllingHazardClassification { get; set; } = string.Empty;

    public double DesignDensityGpmPerSqFt { get; set; }

    public double RemoteAreaSquareFeet { get; set; }

    public double SprinklerDemandFlowGpm { get; set; }

    public double HoseStreamAllowanceGpm { get; set; }

    public double TotalFlowGpm { get; set; }

    public double SystemDemandPsi { get; set; }

    public double AvailablePressurePsi { get; set; }

    public double SafetyMarginPsi { get; set; }

    public double EquivalentKFactor { get; set; }

    public int OperatingSprinklerCount { get; set; }

    public double FlowPerOperatingSprinklerGpm { get; set; }

    public double MaxCoverageSquareFeet { get; set; }

    public double DemandFlowGpm { get; set; }

    public double DemandPressurePsi { get; set; }

    public IList<WaterSupplyCurvePoint> SupplyCurve { get; set; } = new List<WaterSupplyCurvePoint>();

    public bool UsesLayoutLinkedHydraulics { get; set; }

    public bool UsesPlacedPipeLengths { get; set; }

    public string PipeLengthDataSource { get; set; } = string.Empty;

    public double BranchLengthFeet { get; set; }

    public double MainLengthFeet { get; set; }

    public double TotalPipeLengthFeet { get; set; }

    public bool UsesSegmentGraphHydraulics { get; set; }

    public bool UsesPlacedPipeTopology { get; set; }

    public bool UsesProjectTrunk { get; set; }

    public bool UsesRemoteAreaSelection { get; set; }

    public int CriticalPathSegmentCount { get; set; }

    public double FittingFrictionPsi { get; set; }

    public int CriticalPathFittingCount { get; set; }

    public int CriticalPathVelocityViolationCount { get; set; }

    public double MaxCriticalPathVelocityFeetPerSecond { get; set; }

    public int CriticalPathDiameterSuggestionCount { get; set; }

    public bool UsesUserSupplyAnchor { get; set; }

    public string UserSupplyAnchorLabel { get; set; } = string.Empty;

    public string RemoteSprinklerLabel { get; set; } = string.Empty;

    public string NfpaReference { get; set; } = string.Empty;

    public IList<HydraulicNode> CriticalPath { get; set; } = new List<HydraulicNode>();

    public IList<string> Warnings { get; set; } = new List<string>();
}

public sealed class MaterialTakeoffItem
{
    public string ItemType { get; set; } = string.Empty;

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string FamilyName { get; set; } = string.Empty;

    public string HazardClassification { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;

    public bool IsSummaryRow { get; set; }
}

public sealed class ReportExportRequest
{
    public string OutputFolder { get; set; } = string.Empty;

    public bool IncludeDesignSummary { get; set; } = true;

    public bool IncludeHydraulicReport { get; set; } = true;

    public bool IncludeNodeDiagram { get; set; } = true;

    public bool IncludeMaterialTakeoff { get; set; } = true;
}

