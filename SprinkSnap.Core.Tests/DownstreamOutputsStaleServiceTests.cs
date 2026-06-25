using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class DownstreamOutputsStaleServiceTests
{
    [Fact]
    public void IsMaterialsTakeoffStale_ReturnsTrue_WhenHydraulicsCompleteAndMaterialsIncomplete()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress =
            {
                HydraulicsComplete = true,
                MaterialsComplete = false
            }
        };

        Assert.True(DownstreamOutputsStaleService.IsMaterialsTakeoffStale(state));
        Assert.True(DownstreamOutputsStaleService.IsDownstreamStaleActive(state));
        Assert.True(DownstreamOutputsStaleService.RequiresMaterialsRefreshBeforeExport(state));
    }

    [Fact]
    public void IsMaterialsTakeoffStale_ReturnsFalse_WhenMaterialsAreCurrent()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress =
            {
                HydraulicsComplete = true,
                MaterialsComplete = true
            }
        };

        Assert.False(DownstreamOutputsStaleService.IsMaterialsTakeoffStale(state));
        Assert.False(DownstreamOutputsStaleService.IsDownstreamStaleActive(state));
    }

    [Fact]
    public void GetBannerMessage_ReturnsEmpty_WhenTakeoffIsCurrent()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress =
            {
                HydraulicsComplete = true,
                MaterialsComplete = true
            }
        };

        Assert.Equal(string.Empty, DownstreamOutputsStaleService.GetBannerTitle(state));
        Assert.Equal(string.Empty, DownstreamOutputsStaleService.GetBannerMessage(state));
    }

    [Fact]
    public void GetBannerMessage_ReturnsGuidance_WhenTakeoffIsStale()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress =
            {
                HydraulicsComplete = true,
                MaterialsComplete = false
            }
        };

        Assert.Equal("Material takeoff out of date", DownstreamOutputsStaleService.GetBannerTitle(state));
        Assert.Contains("Refresh Takeoff", DownstreamOutputsStaleService.GetBannerMessage(state));
    }
}
