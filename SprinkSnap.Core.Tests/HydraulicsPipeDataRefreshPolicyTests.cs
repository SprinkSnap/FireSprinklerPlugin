using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicsPipeDataRefreshPolicyTests
{
    [Fact]
    public void ShouldRemeasure_ReturnsFalse_InPreviewMode()
    {
        bool shouldRemeasure = HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
            new SchematicPipeRoutingSummary { TotalSegmentCount = 5 },
            new PipePlacementSummary { PlacedSegmentCount = 3 },
            isPreviewMode: true,
            remeasureAvailable: true);

        Assert.False(shouldRemeasure);
    }

    [Fact]
    public void ShouldRemeasure_ReturnsTrue_WhenSchematicRoutingExists()
    {
        bool shouldRemeasure = HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
            new SchematicPipeRoutingSummary { TotalSegmentCount = 4 },
            new PipePlacementSummary(),
            isPreviewMode: false,
            remeasureAvailable: true);

        Assert.True(shouldRemeasure);
    }

    [Fact]
    public void ShouldRemeasure_ReturnsTrue_WhenPlacedPipesExist()
    {
        bool shouldRemeasure = HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
            new SchematicPipeRoutingSummary(),
            new PipePlacementSummary { PlacedSegmentCount = 2 },
            isPreviewMode: false,
            remeasureAvailable: true);

        Assert.True(shouldRemeasure);
    }

    [Fact]
    public void ShouldRemeasure_ReturnsFalse_WhenNoPipeDataAndRemeasureUnavailable()
    {
        bool shouldRemeasure = HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
            new SchematicPipeRoutingSummary(),
            new PipePlacementSummary(),
            isPreviewMode: false,
            remeasureAvailable: false);

        Assert.False(shouldRemeasure);
    }
}
