using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13HydraulicGraphCalculatorTests
{
    private static WaterSupplyInput CreateSampleSupply()
    {
        return new WaterSupplyInput
        {
            StaticPressurePsi = 80,
            ResidualPressurePsi = 50,
            FlowAtResidualGpm = 1000
        };
    }

    [Fact]
    public void GetSupplyPressureAtFlow_ReturnsStaticPressure_AtZeroFlow()
    {
        double pressure = Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(CreateSampleSupply(), 0);
        Assert.Equal(80, pressure, 3);
    }

    [Fact]
    public void GetSupplyPressureAtFlow_ReturnsResidualPressure_AtTestFlow()
    {
        double pressure = Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(CreateSampleSupply(), 1000);
        Assert.Equal(50, pressure, 3);
    }

    [Fact]
    public void GetSupplyPressureAtFlow_UsesLinearSegment_BetweenStaticAndResidual()
    {
        double pressure = Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(CreateSampleSupply(), 500);
        Assert.Equal(65, pressure, 3);
    }

    [Fact]
    public void GetSupplyPressureAtFlow_DropsBelowResidual_WhenFlowExceedsTestPoint()
    {
        WaterSupplyInput input = CreateSampleSupply();
        double atTestFlow = Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(input, 1000);
        double aboveTestFlow = Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(input, 1200);

        Assert.True(aboveTestFlow < atTestFlow);
    }

    [Fact]
    public void BuildSupplyCurve_IncludesStaticAndResidualPoints()
    {
        IList<WaterSupplyCurvePoint> curve = Nfpa13HydraulicGraphCalculator.BuildSupplyCurve(CreateSampleSupply(), 10);

        Assert.Equal(11, curve.Count);
        Assert.Equal(0, curve[0].FlowGpm, 3);
        Assert.Equal(80, curve[0].PressurePsi, 3);
        Assert.Equal(1000, curve[10].FlowGpm / 1.2, 0.1);
    }

    [Fact]
    public void BuildDemandCurve_ReturnsSprinklerAndTotalPoints_WhenHoseStreamIsPresent()
    {
        IList<WaterSupplyCurvePoint> curve = Nfpa13HydraulicGraphCalculator.BuildDemandCurve(
            sprinklerFlowGpm: 150,
            sprinklerDemandPressurePsi: 42,
            totalFlowGpm: 250,
            totalDemandPressurePsi: 48);

        Assert.Equal(2, curve.Count);
        Assert.Equal(150, curve[0].FlowGpm, 3);
        Assert.Equal(42, curve[0].PressurePsi, 3);
        Assert.Equal(250, curve[1].FlowGpm, 3);
        Assert.Equal(48, curve[1].PressurePsi, 3);
    }

    [Fact]
    public void ComputeSprinklerDemandPressureAtSource_SubtractsHoseStreamMainFriction()
    {
        double sprinklerDemand = Nfpa13HydraulicGraphCalculator.ComputeSprinklerDemandPressureAtSource(
            totalDemandPressurePsi: 55,
            sprinklerFlowGpm: 150,
            totalFlowGpm: 250,
            mainDiameterInches: 4,
            mainLengthFeet: 120);

        Assert.True(sprinklerDemand < 55);
        Assert.True(sprinklerDemand > 0);
    }

    [Fact]
    public void ScaleFlowForGraph_UsesOnePointEightyFiveExponent()
    {
        double scaled = Nfpa13HydraulicGraphCalculator.ScaleFlowForGraph(10);
        Assert.Equal(Math.Pow(10, 1.85), scaled, 3);
    }
}
