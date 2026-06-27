using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public sealed class PipingSystemScenarioResult
{
    public string PipingSystemType { get; set; } = PipingSystemTypes.Tree;

    public string PipeSchedule { get; set; } = PipeScheduleTypes.Schedule40;

    public double HazenWilliamsC { get; set; } = PipeScheduleDefaults.Schedule40HazenWilliamsC;

    public string NfpaReference { get; set; } = string.Empty;

    public int SchematicSegmentCount { get; set; }

    public double SchematicPipeLengthFeet { get; set; }

    public HydraulicCalculationResult HydraulicResult { get; set; } = new HydraulicCalculationResult();

    public bool MeetsSupplyDemand => HydraulicResult.SafetyMarginPsi >= 0;

    public bool IsRecommended { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class PipingSystemHydraulicComparisonResult
{
    public IList<PipingSystemScenarioResult> Scenarios { get; set; } = new List<PipingSystemScenarioResult>();

    public string RecommendedPipingSystemType { get; set; } = PipingSystemTypes.Tree;

    public string RecommendedPipeSchedule { get; set; } = PipeScheduleTypes.Schedule40;

    public string ComparisonSummary { get; set; } = string.Empty;

    public string NfpaReference { get; set; } = string.Empty;

    public DateTime ComparedAtUtc { get; set; } = DateTime.UtcNow;
}
