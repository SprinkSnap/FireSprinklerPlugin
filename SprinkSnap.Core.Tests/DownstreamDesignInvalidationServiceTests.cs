using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class DownstreamDesignInvalidationServiceTests
{
    [Fact]
    public void InvalidateHydraulicResults_ClearsHydraulicStateWithoutRemovingPipeRouting()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            HydraulicResult = new HydraulicCalculationResult { TotalFlowGpm = 250, SystemDemandPsi = 42.0 },
            WaterSupplyValidation = new WaterSupplyValidationResult { IsAdequate = true },
            SchematicPipeRouting = new SchematicPipeRoutingSummary { TotalSegmentCount = 8 },
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 5 },
            SessionProgress =
            {
                HydraulicsComplete = true,
                MaterialsComplete = true,
                ReportsExported = true
            }
        };

        DownstreamDesignInvalidationService.InvalidateHydraulicResults(state);

        Assert.Equal(0, state.HydraulicResult.TotalFlowGpm);
        Assert.False(state.WaterSupplyValidation.IsAdequate);
        Assert.Equal(8, state.SchematicPipeRouting.TotalSegmentCount);
        Assert.Equal(5, state.PipePlacementSummary.PlacedSegmentCount);
        Assert.False(state.SessionProgress.HydraulicsComplete);
        Assert.False(state.SessionProgress.MaterialsComplete);
        Assert.False(state.SessionProgress.ReportsExported);
    }
}
