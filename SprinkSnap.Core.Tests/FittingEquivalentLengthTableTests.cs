using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class FittingEquivalentLengthTableTests
{
    [Theory]
    [InlineData(PipeJointTypes.Elbow, 1.25, 2.5)]
    [InlineData(PipeJointTypes.Elbow, 4.0, 11.0)]
    [InlineData(PipeJointTypes.Tee, 1.25, 4.0)]
    [InlineData(PipeJointTypes.Tee, 4.0, 14.0)]
    [InlineData(PipeJointTypes.Valve, 1.25, 8.0)]
    [InlineData(PipeJointTypes.Valve, 4.0, 28.0)]
    public void GetEquivalentLengthFeet_ReturnsStandardValuesAtAnchorDiameters(
        string jointType,
        double diameterInches,
        double expectedLengthFeet)
    {
        double lengthFeet = FittingEquivalentLengthTable.GetEquivalentLengthFeet(jointType, diameterInches);
        Assert.Equal(expectedLengthFeet, lengthFeet, 3);
    }

    [Fact]
    public void GetEquivalentLengthFeet_InterpolatesBetweenAnchorDiameters()
    {
        double lengthFeet = FittingEquivalentLengthTable.GetEquivalentLengthFeet(PipeJointTypes.Elbow, 2.625);
        Assert.InRange(lengthFeet, 2.5, 11.0);
        Assert.True(lengthFeet > 2.5);
        Assert.True(lengthFeet < 11.0);
    }

    [Fact]
    public void GetEquivalentLengthFeet_ReturnsZero_WhenDiameterMissing()
    {
        Assert.Equal(0.0, FittingEquivalentLengthTable.GetEquivalentLengthFeet(PipeJointTypes.Elbow, 0.0));
    }
}
