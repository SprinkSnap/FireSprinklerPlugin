using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Engines;

public sealed class ModelAnalysisEngine : IModelAnalysisEngine
{
    public ModelAnalysisSummary Analyze(SprinkSnapProjectState projectState)
    {
        return new ModelAnalysisSummary
        {
            RoomCount = projectState.Rooms.Count,
            SlopedCeilingCount = projectState.Rooms.Count(room => room.HasSlopedCeiling),
            MissingCeilingCount = projectState.Rooms.Count(room => room.CeilingHeightFeet <= 0),
            ObstructionZoneCount = projectState.Rooms.Sum(room => room.ObstructionCount),
            Warnings = projectState.Warnings.Select(warning => warning.Message).ToList()
        };
    }

    public string ExportJson(ModelAnalysisSummary summary)
    {
        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class HazardClassificationEngine : IHazardClassificationEngine
{
    private readonly IHazardClassifier classifier = new HazardClassifier();

    public HazardClassificationResult Classify(RoomInfo room)
    {
        return classifier.SuggestClassification(room);
    }
}

public sealed class ManufacturerRecommendationEngine : IManufacturerRecommendationEngine
{
    private readonly ICompatibleSprinklerSelector selector = new CompatibleSprinklerSelector();

    public CompatibleSprinklerSelection Recommend(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog,
        ProjectSprinklerStandard projectStandard)
    {
        return selector.SelectForRoom(room, catalog, projectStandard);
    }
}

public sealed class WaterSupplyEngine : IWaterSupplyEngine
{
    public WaterSupplyValidationResult Validate(WaterSupplyInput input, HydraulicCalculationResult demand)
    {
        WaterSupplyValidationResult result = new WaterSupplyValidationResult();

        if (!input.StaticPressurePsi.HasValue || !input.ResidualPressurePsi.HasValue || !input.FlowAtResidualGpm.HasValue)
        {
            result.Warnings.Add("Static pressure, residual pressure, and flow at residual are required.");
            return result;
        }

        result.Curve.Add(new WaterSupplyCurvePoint { FlowGpm = 0, PressurePsi = input.StaticPressurePsi.Value });
        result.Curve.Add(new WaterSupplyCurvePoint { FlowGpm = input.FlowAtResidualGpm.Value, PressurePsi = input.ResidualPressurePsi.Value });
        result.SafetyMarginPsi = input.ResidualPressurePsi.Value - demand.SystemDemandPsi;
        result.IsAdequate = result.SafetyMarginPsi >= 0;

        if (!result.IsAdequate)
        {
            result.Warnings.Add("Available water supply is below calculated system demand.");
        }

        return result;
    }
}

public sealed class LayoutEngine : ILayoutEngine
{
    private readonly ISprinklerLayoutOptimizer optimizer = new SprinklerLayoutOptimizer();

    public AutomaticLayoutResult Generate(RoomInfo room, SprinklerFamilyInfo sprinklerFamily)
    {
        return optimizer.GenerateBestLayout(room, sprinklerFamily);
    }
}

public sealed class HydraulicEngine : IHydraulicEngine
{
    public HydraulicCalculationResult Calculate(IEnumerable<RoomInfo> rooms, WaterSupplyInput waterSupply)
    {
        List<RoomInfo> roomList = rooms.ToList();
        double totalFlow = Math.Max(0, roomList.Sum(room => room.ProposedSprinklers.Count) * 25.0);
        double demandPressure = totalFlow > 0 ? 35.0 : 0.0;
        double availablePressure = waterSupply.ResidualPressurePsi ?? 0.0;

        return new HydraulicCalculationResult
        {
            TotalFlowGpm = totalFlow,
            SystemDemandPsi = demandPressure,
            AvailablePressurePsi = availablePressure,
            SafetyMarginPsi = availablePressure - demandPressure,
            Warnings = totalFlow <= 0 ? new List<string> { "No sprinkler candidates available for hydraulic calculation." } : new List<string>()
        };
    }
}

public sealed class MaterialTakeoffEngine : IMaterialTakeoffEngine
{
    public IReadOnlyList<MaterialTakeoffItem> Generate(IEnumerable<RoomInfo> rooms)
    {
        int sprinklerCount = rooms.Sum(room => room.ProposedSprinklers.Count);
        return new List<MaterialTakeoffItem>
        {
            new MaterialTakeoffItem
            {
                ItemType = "Sprinkler",
                Description = "Proposed sprinkler heads",
                Quantity = sprinklerCount,
                Unit = "EA"
            }
        };
    }
}

public sealed class ReportEngine : IReportEngine
{
    public IReadOnlyList<string> ExportAll(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff,
        ReportExportRequest request)
    {
        List<string> reports = new List<string>();
        if (request.IncludeDesignSummary) reports.Add("Design Summary PDF");
        if (request.IncludeHydraulicReport) reports.Add("Hydraulic Report PDF");
        if (request.IncludeNodeDiagram) reports.Add("Node Diagram PDF");
        if (request.IncludeMaterialTakeoff) reports.Add("Material Takeoff PDF");
        return reports;
    }
}

