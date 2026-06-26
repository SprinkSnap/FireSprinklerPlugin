using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class VelocityLimitDefaultsTests
{
    [Fact]
    public void ResolveBranchVelocityLimit_ReturnsPreferenceValue_WhenConfigured()
    {
        SprinkSnapProjectPreferences preferences = new SprinkSnapProjectPreferences
        {
            BranchVelocityLimitFeetPerSecond = 12.0
        };

        Assert.Equal(12.0, VelocityLimitDefaults.ResolveBranchVelocityLimitFeetPerSecond(preferences));
    }

    [Fact]
    public void ResolveMainVelocityLimit_FallsBackToDefault_WhenPreferenceMissing()
    {
        Assert.Equal(20.0, VelocityLimitDefaults.ResolveMainVelocityLimitFeetPerSecond(null));
        Assert.Equal(20.0, VelocityLimitDefaults.ResolveMainVelocityLimitFeetPerSecond(new SprinkSnapProjectPreferences
        {
            MainVelocityLimitFeetPerSecond = 0
        }));
    }

    [Fact]
    public void ResolveVelocityLimitFeetPerSecond_UsesPreferences_ForBranchAndMainSegments()
    {
        SprinkSnapProjectPreferences preferences = new SprinkSnapProjectPreferences
        {
            BranchVelocityLimitFeetPerSecond = 12.0,
            MainVelocityLimitFeetPerSecond = 18.0
        };

        Assert.Equal(12.0, HydraulicVelocityValidator.ResolveVelocityLimitFeetPerSecond(PipeSegmentTypes.Branch, preferences));
        Assert.Equal(18.0, HydraulicVelocityValidator.ResolveVelocityLimitFeetPerSecond(PipeSegmentTypes.Riser, preferences));
    }

    [Fact]
    public void SuggestCompliantDiameterInches_UsesStricterBranchLimit_FromPreferences()
    {
        SprinkSnapProjectPreferences strictPreferences = new SprinkSnapProjectPreferences
        {
            BranchVelocityLimitFeetPerSecond = 10.0
        };

        double defaultSuggestion = HydraulicPipeSizingService.SuggestCompliantDiameterInches(
            flowGpm: 60.0,
            segmentType: PipeSegmentTypes.Branch,
            currentDiameterInches: 1.25);
        double strictSuggestion = HydraulicPipeSizingService.SuggestCompliantDiameterInches(
            flowGpm: 60.0,
            segmentType: PipeSegmentTypes.Branch,
            currentDiameterInches: 1.25,
            strictPreferences);

        Assert.True(strictSuggestion > defaultSuggestion);
        Assert.Equal(2.0, strictSuggestion);
    }
}
