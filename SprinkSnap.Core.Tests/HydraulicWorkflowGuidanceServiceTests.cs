using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicWorkflowGuidanceServiceTests
{
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
}
