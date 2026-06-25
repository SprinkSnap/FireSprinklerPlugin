using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
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
}

public sealed class HydraulicCalculationResult
{
    public double TotalFlowGpm { get; set; }

    public double SystemDemandPsi { get; set; }

    public double AvailablePressurePsi { get; set; }

    public double SafetyMarginPsi { get; set; }

    public IList<HydraulicNode> CriticalPath { get; set; } = new List<HydraulicNode>();

    public IList<string> Warnings { get; set; } = new List<string>();
}

public sealed class MaterialTakeoffItem
{
    public string ItemType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public string Unit { get; set; } = string.Empty;
}

public sealed class ReportExportRequest
{
    public string OutputFolder { get; set; } = string.Empty;

    public bool IncludeDesignSummary { get; set; } = true;

    public bool IncludeHydraulicReport { get; set; } = true;

    public bool IncludeNodeDiagram { get; set; } = true;

    public bool IncludeMaterialTakeoff { get; set; } = true;
}

