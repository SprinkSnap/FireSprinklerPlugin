using System;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class WaterSupplyEngineTests
{
    private readonly WaterSupplyEngine engine = new WaterSupplyEngine();

    [Fact]
    public void ValidateInput_ReturnsErrors_WhenHydrantTestIsIncomplete()
    {
        WaterSupplyInputValidationResult result = engine.ValidateInput(new WaterSupplyInput());

        Assert.False(result.IsCompliant);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_ReturnsInputErrors_WhenHydrantTestIsIncomplete()
    {
        WaterSupplyValidationResult result = engine.Validate(new WaterSupplyInput(), CreateDemand(100, 50));

        Assert.True(WaterSupplyValidationHelper.HasInputValidationErrors(result));
        Assert.False(result.IsAdequate);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Validate_ReturnsAdequate_WhenSupplyExceedsDemand()
    {
        WaterSupplyInput input = CreateValidInput();
        HydraulicCalculationResult demand = CreateDemand(totalFlowGpm: 500, systemDemandPsi: 50);

        WaterSupplyValidationResult result = engine.Validate(input, demand);

        Assert.False(WaterSupplyValidationHelper.HasInputValidationErrors(result));
        Assert.True(result.IsAdequate);
        Assert.True(result.SafetyMarginPsi >= 0);
        Assert.NotEmpty(result.Curve);
    }

    [Fact]
    public void Validate_ReturnsInadequate_WhenDemandExceedsSupply()
    {
        WaterSupplyInput input = CreateValidInput();
        HydraulicCalculationResult demand = CreateDemand(totalFlowGpm: 2500, systemDemandPsi: 80);

        WaterSupplyValidationResult result = engine.Validate(input, demand);

        Assert.False(WaterSupplyValidationHelper.HasInputValidationErrors(result));
        Assert.False(result.IsAdequate);
        Assert.Contains(result.Warnings, warning => warning.Contains(Nfpa13Edition.References.HydraulicGraphSheet));
    }

    private static WaterSupplyInput CreateValidInput()
    {
        return new WaterSupplyInput
        {
            StaticPressurePsi = 85,
            ResidualPressurePsi = 65,
            FlowAtResidualGpm = 1200,
            HydrantTestDate = DateTime.Today.AddMonths(-2)
        };
    }

    private static HydraulicCalculationResult CreateDemand(double totalFlowGpm, double systemDemandPsi)
    {
        return new HydraulicCalculationResult
        {
            TotalFlowGpm = totalFlowGpm,
            SystemDemandPsi = systemDemandPsi,
            DemandFlowGpm = totalFlowGpm,
            DemandPressurePsi = systemDemandPsi
        };
    }
}
