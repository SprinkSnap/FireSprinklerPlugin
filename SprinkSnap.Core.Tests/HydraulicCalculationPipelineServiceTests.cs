using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicCalculationPipelineServiceTests
{
    [Fact]
    public void ApplySyncSummaryFlags_CopiesRevitSyncFieldsAndMarksPostSyncReSolve()
    {
        HydraulicCalculationResult result = new HydraulicCalculationResult();
        PipePlacementSummary summary = new PipePlacementSummary
        {
            UsesRevitPipeDiameterSync = true,
            RevitPipeDiameterSyncCount = 3,
            UsesRevitFittingDiameterSync = true,
            RevitFittingDiameterSyncCount = 2
        };

        HydraulicCalculationPipelineService.ApplySyncSummaryFlags(result, summary);

        Assert.True(result.UsesRevitPipeDiameterSync);
        Assert.Equal(3, result.RevitPipeDiameterSyncCount);
        Assert.True(result.UsesRevitFittingDiameterSync);
        Assert.Equal(2, result.RevitFittingDiameterSyncCount);
        Assert.True(result.UsesPostSyncHydraulicReSolve);
    }

    [Fact]
    public void FinalizeSession_StoresResultAndClearsDownstreamCompletionFlags()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress =
            {
                MaterialsComplete = true,
                ReportsExported = true
            }
        };
        HydraulicCalculationResult result = new HydraulicCalculationResult
        {
            TotalFlowGpm = 180,
            SystemDemandPsi = 52.0
        };

        HydraulicCalculationPipelineService.FinalizeSession(state, result);

        Assert.Equal(180, state.HydraulicResult.TotalFlowGpm);
        Assert.True(state.SessionProgress.HydraulicsComplete);
        Assert.False(state.SessionProgress.MaterialsComplete);
        Assert.False(state.SessionProgress.ReportsExported);
    }

    [Fact]
    public void CombinePipelineMessages_MergesDistinctMessagesInOrder()
    {
        IList<string> combined = HydraulicCalculationPipelineService.CombinePipelineMessages(
            new[] { "Measured 4 pipes." },
            new[] { "Updated 2 Revit pipes.", "Measured 4 pipes." });

        Assert.Equal(2, combined.Count);
        Assert.Equal("Measured 4 pipes.", combined[0]);
        Assert.Equal("Updated 2 Revit pipes.", combined[1]);
    }
}
