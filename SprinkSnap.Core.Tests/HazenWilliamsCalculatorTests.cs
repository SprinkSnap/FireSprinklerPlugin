using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HazenWilliamsCalculatorTests
{
    [Theory]
    [InlineData(0, 4, 60)]
    [InlineData(100, 0, 60)]
    [InlineData(100, 4, 0)]
    [InlineData(-10, 4, 60)]
    public void FrictionLossPsi_ReturnsZero_WhenInputsAreNonPositive(
        double flowGpm,
        double diameterInches,
        double lengthFeet)
    {
        double loss = HazenWilliamsCalculator.FrictionLossPsi(flowGpm, diameterInches, lengthFeet);
        Assert.Equal(0.0, loss);
    }

    [Fact]
    public void FrictionLossPsi_MatchesReferenceFormula_ForKnownInputs()
    {
        double loss = HazenWilliamsCalculator.FrictionLossPsi(100, 4, 60, 120);
        Assert.InRange(loss, 0.225, 0.228);
    }

    [Fact]
    public void FrictionLossPsi_IncreasesWithFlow()
    {
        double lowerFlow = HazenWilliamsCalculator.FrictionLossPsi(50, 4, 60);
        double higherFlow = HazenWilliamsCalculator.FrictionLossPsi(150, 4, 60);
        Assert.True(higherFlow > lowerFlow);
    }
}
