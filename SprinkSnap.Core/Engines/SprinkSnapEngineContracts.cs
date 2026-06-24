using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Engines;

public interface IModelAnalysisEngine
{
    ModelAnalysisSummary Analyze(SprinkSnapProjectState projectState);

    string ExportJson(ModelAnalysisSummary summary);
}

public interface IHazardClassificationEngine
{
    HazardClassificationResult Classify(RoomInfo room);
}

public interface IManufacturerRecommendationEngine
{
    CompatibleSprinklerSelection Recommend(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog,
        ProjectSprinklerStandard projectStandard);
}

public interface IWaterSupplyEngine
{
    WaterSupplyValidationResult Validate(WaterSupplyInput input, HydraulicCalculationResult demand);
}

public interface ILayoutEngine
{
    AutomaticLayoutResult Generate(RoomInfo room, SprinklerFamilyInfo sprinklerFamily);
}

public interface IHydraulicEngine
{
    HydraulicCalculationResult Calculate(IEnumerable<RoomInfo> rooms, WaterSupplyInput waterSupply);
}

public interface IMaterialTakeoffEngine
{
    IReadOnlyList<MaterialTakeoffItem> Generate(IEnumerable<RoomInfo> rooms);
}

public interface IReportEngine
{
    IReadOnlyList<string> ExportAll(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff,
        ReportExportRequest request);
}

