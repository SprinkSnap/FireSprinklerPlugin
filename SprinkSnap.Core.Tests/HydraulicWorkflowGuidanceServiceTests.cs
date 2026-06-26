using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicWorkflowGuidanceServiceTests
{
    [Fact]
    public void HasPlacedPipeGeometry_ReturnsTrue_WhenSegmentsPlaced()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 3 }
        };

        Assert.True(HydraulicWorkflowGuidanceService.HasPlacedPipeGeometry(state));
    }

    [Fact]
    public void ShouldWarnPipePlacementNeeded_WhenRoutingExistsWithoutPlacement()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SchematicPipeRouting = new SchematicPipeRoutingSummary { TotalSegmentCount = 12 },
            PipePlacementSummary = new PipePlacementSummary()
        };

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnPipePlacementNeeded(state));
        Assert.True(HydraulicWorkflowGuidanceService.IsPipePlacementGuidanceActive(state));
    }

    [Fact]
    public void ShouldWarnMaterialsMissingHydraulics_WhenDesignExistsWithoutHydraulics()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        state.SessionProgress.DesignGenerated = true;
        state.SessionProgress.HydraulicsComplete = false;

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnMaterialsMissingHydraulics(state));
    }

    [Fact]
    public void IsSchematicOnlyHydraulicsComplete_WhenHydraulicsRanWithoutPlacedPipes()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary()
        };

        Assert.True(HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state));
    }

    [Fact]
    public void ShouldWarnReRunHydraulicsAfterPipePlacement_WhenResultUsesSchematicGeometry()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 4 },
            HydraulicResult = new HydraulicCalculationResult
            {
                TotalFlowGpm = 120,
                UsesPlacedPipeLengths = false,
                UsesPlacedPipeTopology = false
            }
        };

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnReRunHydraulicsAfterPipePlacement(state));
    }

    [Fact]
    public void ShouldWarnPipeSizingWithoutPlacement_WhenSizingAppliedWithoutRevitPipes()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            PipePlacementSummary = new PipePlacementSummary()
        };
        HydraulicCalculationResult result = new HydraulicCalculationResult
        {
            UsesAppliedPipeSizing = true,
            AppliedPipeSizingSegmentCount = 2
        };

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnPipeSizingWithoutPlacement(result, state));
    }

    [Fact]
    public void GetHydraulicWorkflowActionMessage_IncludesPlacePipesAction_ForSchematicOnlyHydraulics()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary()
        };

        string message = HydraulicWorkflowGuidanceService.GetHydraulicWorkflowActionMessage(state);

        Assert.Contains(HydraulicWorkflowGuidanceService.HydraulicsPlacePipesActionMessage, message);
    }
}
