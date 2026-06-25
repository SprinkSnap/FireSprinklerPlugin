using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicVelocityValidatorTests
{
    [Theory]
    [InlineData(30.0, 1.25, 7.83)]
    [InlineData(100.0, 4.0, 2.55)]
    public void CalculateVelocityFeetPerSecond_UsesStandardFormula(
        double flowGpm,
        double diameterInches,
        double expectedVelocity)
    {
        double velocity = HydraulicVelocityValidator.CalculateVelocityFeetPerSecond(flowGpm, diameterInches);
        Assert.Equal(expectedVelocity, velocity, 2);
    }

    [Theory]
    [InlineData(PipeSegmentTypes.Branch, 15.0)]
    [InlineData(PipeSegmentTypes.CrossMain, 20.0)]
    [InlineData(PipeSegmentTypes.Main, 20.0)]
    [InlineData(PipeSegmentTypes.Riser, 20.0)]
    public void ResolveVelocityLimitFeetPerSecond_UsesNfpa13Limits(
        string segmentType,
        double expectedLimit)
    {
        double limit = HydraulicVelocityValidator.ResolveVelocityLimitFeetPerSecond(segmentType);
        Assert.Equal(expectedLimit, limit);
    }

    [Fact]
    public void Evaluate_FlagsBranchSegment_WhenVelocityExceedsLimit()
    {
        HydraulicVelocityCheck check = HydraulicVelocityValidator.Evaluate(
            flowGpm: 60.0,
            diameterInches: 1.25,
            PipeSegmentTypes.Branch);

        Assert.True(check.ExceedsLimit);
        Assert.Equal(15.0, check.VelocityLimitFeetPerSecond);
        Assert.True(check.VelocityFeetPerSecond > 15.0);
    }

    [Fact]
    public void ValidateSegmentChain_AddsWarningsAndCountsViolations()
    {
        LayoutLinkedHydraulicPath path = new LayoutLinkedHydraulicPath
        {
            SegmentChain =
            {
                new HydraulicGraphSegment
                {
                    Description = "1.25\" branch drop #1",
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    FlowGpm = 60.0,
                    LengthFeet = 12.0
                },
                new HydraulicGraphSegment
                {
                    Description = "4\" riser",
                    SegmentType = PipeSegmentTypes.Riser,
                    DiameterInches = 4.0,
                    FlowGpm = 120.0,
                    LengthFeet = 10.0
                }
            }
        };

        int violations = HydraulicVelocityValidator.ValidateSegmentChain(path);

        Assert.Equal(1, violations);
        Assert.Equal(1, path.CriticalPathVelocityViolationCount);
        Assert.True(path.MaxCriticalPathVelocityFeetPerSecond > 15.0);
        Assert.Contains(path.Warnings, warning => warning.IndexOf("Velocity limit exceeded", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.True(path.SegmentChain[0].ExceedsVelocityLimit);
        Assert.False(path.SegmentChain[1].ExceedsVelocityLimit);
    }
}
