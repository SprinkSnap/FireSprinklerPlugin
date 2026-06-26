using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
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
        SprinkSnapProjectState state = CreateModelBackedHydraulicsState();
        state.SessionProgress.MaterialsComplete = true;

        Assert.False(DownstreamOutputsStaleService.IsMaterialsTakeoffStale(state));
        Assert.False(DownstreamOutputsStaleService.IsDownstreamStaleActive(state));
    }

    [Fact]
    public void GetBannerMessage_ReturnsEmpty_WhenTakeoffIsCurrent()
    {
        SprinkSnapProjectState state = CreateModelBackedHydraulicsState();
        state.SessionProgress.MaterialsComplete = true;

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

    [Fact]
    public void BlocksMaterialsRefresh_WhenHydraulicsNotRun()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        state.SessionProgress.DesignGenerated = true;

        Assert.True(DownstreamOutputsStaleService.BlocksMaterialsRefresh(state));
        Assert.True(DownstreamOutputsStaleService.BlocksMaterialsExport(state));
        Assert.Equal(
            HydraulicWorkflowGuidanceService.MaterialsMissingHydraulicsMessage,
            DownstreamOutputsStaleService.GetMaterialsRefreshBlockMessage(state));
    }

    [Fact]
    public void GetReportExportBlockMessage_BlocksHydraulicReport_WhenHydraulicsNotRun()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();

        string message = DownstreamOutputsStaleService.GetReportExportBlockMessage(
            state,
            includeHydraulicReport: true,
            includeNodeDiagram: false,
            includeMaterialTakeoff: false);

        Assert.Equal(
            HydraulicWorkflowGuidanceService.HydraulicsRequiredBeforeReportExportMessage,
            message);
    }

    [Fact]
    public void GetReportExportBlockMessage_AllowsDesignSummaryOnly_WhenHydraulicsNotRun()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();

        string message = DownstreamOutputsStaleService.GetReportExportBlockMessage(
            state,
            includeHydraulicReport: false,
            includeNodeDiagram: false,
            includeMaterialTakeoff: false);

        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void GetReportExportBlockMessage_BlocksHydraulicReport_WhenSchematicOnly()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary()
        };

        string message = DownstreamOutputsStaleService.GetReportExportBlockMessage(
            state,
            includeHydraulicReport: true,
            includeNodeDiagram: false,
            includeMaterialTakeoff: false);

        Assert.Contains(HydraulicWorkflowGuidanceService.SchematicOnlyHydraulicsMessage, message);
        Assert.Contains(HydraulicWorkflowGuidanceService.HydraulicsPlacePipesActionMessage, message);
    }

    [Fact]
    public void GetReportExportBlockMessage_BlocksHydraulicReport_WhenPipesPlacedButResultStale()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 3 },
            HydraulicResult = new HydraulicCalculationResult
            {
                TotalFlowGpm = 100,
                UsesPlacedPipeLengths = false,
                UsesPlacedPipeTopology = false
            }
        };

        string message = DownstreamOutputsStaleService.GetReportExportBlockMessage(
            state,
            includeHydraulicReport: false,
            includeNodeDiagram: true,
            includeMaterialTakeoff: false);

        Assert.Equal(
            HydraulicWorkflowGuidanceService.ReRunHydraulicsAfterPlacementMessage,
            message);
    }

    [Fact]
    public void GetBannerTitle_ReturnsHydraulicsRequired_WhenMaterialsOpenedEarly()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        state.SessionProgress.DesignGenerated = true;

        Assert.Equal("Hydraulics required", DownstreamOutputsStaleService.GetBannerTitle(state));
        Assert.True(DownstreamOutputsStaleService.IsDownstreamStaleActive(state));
    }

    private static SprinkSnapProjectState CreateModelBackedHydraulicsState()
    {
        return new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 2 },
            HydraulicResult = new HydraulicCalculationResult
            {
                TotalFlowGpm = 100,
                UsesPlacedPipeLengths = true,
                UsesPlacedPipeTopology = true
            }
        };
    }
}
