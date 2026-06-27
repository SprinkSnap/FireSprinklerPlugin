using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Reports;

public static class HydraulicReportSummaryBuilder
{
    public static IReadOnlyList<KeyValuePair<string, string>> BuildRows(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult)
    {
        WaterSupplyInput supply = projectState?.WaterSupply ?? new WaterSupplyInput();
        hydraulicResult ??= new HydraulicCalculationResult();

        List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Design density", hydraulicResult.DesignDensityGpmPerSqFt.ToString("N2") + " gpm/sq ft"),
            new KeyValuePair<string, string>("Remote area", hydraulicResult.RemoteAreaSquareFeet.ToString("N0") + " sq ft"),
            new KeyValuePair<string, string>("Controlling ceiling height", hydraulicResult.ControllingCeilingHeightFeet > 0
                ? hydraulicResult.ControllingCeilingHeightFeet.ToString("N1") + " ft"
                : "Not available"),
            new KeyValuePair<string, string>("High-ceiling adjustment", hydraulicResult.UsesHighCeilingAdjustment ? "Yes" : "No"),
            new KeyValuePair<string, string>(
                "High-ceiling sprinkler selection",
                hydraulicResult.HighCeilingSprinklerSelectionCompliant ? "Compliant" : "Review required"),
            new KeyValuePair<string, string>(
                "High-ceiling sprinkler notes",
                string.IsNullOrWhiteSpace(hydraulicResult.HighCeilingSprinklerViolationSummary)
                    ? "No Section 19.2.3.2.5.1 violations in controlling rooms."
                    : hydraulicResult.HighCeilingSprinklerViolationSummary),
            new KeyValuePair<string, string>("Operating sprinklers", hydraulicResult.OperatingSprinklerCount.ToString()),
            new KeyValuePair<string, string>("Flow per operating sprinkler", hydraulicResult.FlowPerOperatingSprinklerGpm.ToString("N1") + " GPM"),
            new KeyValuePair<string, string>("Max coverage per head", hydraulicResult.MaxCoverageSquareFeet.ToString("N0") + " sq ft"),
            new KeyValuePair<string, string>("Sprinkler demand", hydraulicResult.SprinklerDemandFlowGpm.ToString("N1") + " GPM"),
            new KeyValuePair<string, string>("Hose stream allowance", hydraulicResult.HoseStreamAllowanceGpm.ToString("N0") + " GPM"),
            new KeyValuePair<string, string>("Total calculated flow", hydraulicResult.TotalFlowGpm.ToString("N1") + " GPM"),
            new KeyValuePair<string, string>("Equivalent K-factor", hydraulicResult.EquivalentKFactor.ToString("N1")),
            new KeyValuePair<string, string>("Layout-linked hydraulics", hydraulicResult.UsesLayoutLinkedHydraulics ? "Yes" : "Estimated"),
            new KeyValuePair<string, string>("Segment graph hydraulics", hydraulicResult.UsesSegmentGraphHydraulics ? "Yes" : "No"),
            new KeyValuePair<string, string>("Remote area selection", hydraulicResult.UsesRemoteAreaSelection ? "Yes" : "No"),
            new KeyValuePair<string, string>("Placed pipe topology", hydraulicResult.UsesPlacedPipeTopology ? "Yes" : "No"),
            new KeyValuePair<string, string>("Project trunk", hydraulicResult.UsesProjectTrunk ? "Yes" : "No"),
            new KeyValuePair<string, string>("Pipe length source", string.IsNullOrWhiteSpace(hydraulicResult.PipeLengthDataSource)
                ? "Geometry"
                : hydraulicResult.PipeLengthDataSource),
            new KeyValuePair<string, string>("Uses placed pipe lengths", hydraulicResult.UsesPlacedPipeLengths ? "Yes" : "No"),
            new KeyValuePair<string, string>("Remote sprinkler", string.IsNullOrWhiteSpace(hydraulicResult.RemoteSprinklerLabel)
                ? "Not available"
                : hydraulicResult.RemoteSprinklerLabel),
            new KeyValuePair<string, string>("Branch length", hydraulicResult.BranchLengthFeet.ToString("N0") + " ft"),
            new KeyValuePair<string, string>("Main length", hydraulicResult.MainLengthFeet.ToString("N0") + " ft"),
            new KeyValuePair<string, string>("Total pipe length", hydraulicResult.TotalPipeLengthFeet.ToString("N0") + " ft"),
            new KeyValuePair<string, string>("Critical path fitting count", hydraulicResult.CriticalPathFittingCount.ToString()),
            new KeyValuePair<string, string>("Fitting friction loss", hydraulicResult.FittingFrictionPsi.ToString("N1") + " PSI"),
            new KeyValuePair<string, string>("Max critical-path velocity", hydraulicResult.MaxCriticalPathVelocityFeetPerSecond.ToString("N1") + " ft/s"),
            new KeyValuePair<string, string>("Velocity violations", hydraulicResult.CriticalPathVelocityViolationCount.ToString()),
            new KeyValuePair<string, string>("Diameter suggestions", hydraulicResult.CriticalPathDiameterSuggestionCount.ToString()),
            new KeyValuePair<string, string>("Applied pipe sizing", hydraulicResult.UsesAppliedPipeSizing ? "Yes" : "No"),
            new KeyValuePair<string, string>("Upsized segments", hydraulicResult.AppliedPipeSizingSegmentCount.ToString()),
            new KeyValuePair<string, string>("Schematic writeback", hydraulicResult.UsesSchematicPipeSizingWriteback ? "Yes" : "No"),
            new KeyValuePair<string, string>("Schematic segments updated", hydraulicResult.SchematicWritebackSegmentCount.ToString()),
            new KeyValuePair<string, string>("Revit diameter sync", hydraulicResult.UsesRevitPipeDiameterSync ? "Yes" : "No"),
            new KeyValuePair<string, string>("Revit pipes updated", hydraulicResult.RevitPipeDiameterSyncCount.ToString()),
            new KeyValuePair<string, string>("Revit fitting sync", hydraulicResult.UsesRevitFittingDiameterSync ? "Yes" : "No"),
            new KeyValuePair<string, string>("Revit fittings updated", hydraulicResult.RevitFittingDiameterSyncCount.ToString()),
            new KeyValuePair<string, string>("Post-sync re-solve", hydraulicResult.UsesPostSyncHydraulicReSolve ? "Yes" : "No"),
            new KeyValuePair<string, string>("System demand pressure", hydraulicResult.SystemDemandPsi.ToString("N1") + " PSI"),
            new KeyValuePair<string, string>("Demand flow (chart)", hydraulicResult.DemandFlowGpm.ToString("N1") + " GPM"),
            new KeyValuePair<string, string>("Available pressure at demand flow", hydraulicResult.AvailablePressurePsi.ToString("N1") + " PSI"),
            new KeyValuePair<string, string>("Safety margin", hydraulicResult.SafetyMarginPsi.ToString("N1") + " PSI"),
            new KeyValuePair<string, string>("Static pressure (test)", FormatNullable(supply.StaticPressurePsi, "PSI")),
            new KeyValuePair<string, string>("Residual pressure (test)", FormatNullable(supply.ResidualPressurePsi, "PSI")),
            new KeyValuePair<string, string>("Flow at residual", FormatNullable(supply.FlowAtResidualGpm, "GPM"))
        };

        return rows;
    }

    public static string BuildNodeDiagramMethodologySummary(HydraulicCalculationResult hydraulicResult)
    {
        hydraulicResult ??= new HydraulicCalculationResult();

        return "Methodology: segment graph "
            + (hydraulicResult.UsesSegmentGraphHydraulics ? "Yes" : "No")
            + ", remote area selection "
            + (hydraulicResult.UsesRemoteAreaSelection ? "Yes" : "No")
            + ", placed pipe topology "
            + (hydraulicResult.UsesPlacedPipeTopology ? "Yes" : "No")
            + ", project trunk "
            + (hydraulicResult.UsesProjectTrunk ? "Yes" : "No")
            + ", critical-path fittings "
            + hydraulicResult.CriticalPathFittingCount
            + ".";
    }

    private static string FormatNullable(double? value, string unit)
    {
        return value.HasValue ? value.Value.ToString("N1") + " " + unit : "Not entered";
    }
}
