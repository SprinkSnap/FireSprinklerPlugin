using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
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

    [Fact]
    public void ShouldReSolveAfterDiameterSync_ReturnsTrue_WhenSyncWasAttemptedInRevit()
    {
        bool shouldReSolve = HydraulicsPipeDataRefreshPolicy.ShouldReSolveAfterDiameterSync(
            diameterSyncWasAttempted: true,
            isPreviewMode: false);

        Assert.True(shouldReSolve);
    }

    [Fact]
    public void ShouldReSolveAfterDiameterSync_ReturnsFalse_InPreviewMode()
    {
        bool shouldReSolve = HydraulicsPipeDataRefreshPolicy.ShouldReSolveAfterDiameterSync(
            diameterSyncWasAttempted: true,
            isPreviewMode: true);

        Assert.False(shouldReSolve);
    }

    [Fact]
    public void ShouldReSolveAfterDiameterSync_ReturnsFalse_WhenSyncWasNotAttempted()
    {
        bool shouldReSolve = HydraulicsPipeDataRefreshPolicy.ShouldReSolveAfterDiameterSync(
            diameterSyncWasAttempted: false,
            isPreviewMode: false);

        Assert.False(shouldReSolve);
    }

    [Fact]
    public void ShouldSyncPlacedPipeDiametersAfterCalculation_ReturnsTrue_WhenWritebackOccurredAndPipesPlaced()
    {
        bool shouldSync = HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
            new HydraulicCalculationResult { UsesSchematicPipeSizingWriteback = true },
            new PipePlacementSummary { PlacedSegmentCount = 2 },
            isPreviewMode: false,
            syncAvailable: true);

        Assert.True(shouldSync);
    }

    [Fact]
    public void ShouldSyncPlacedPipeDiametersAfterCalculation_ReturnsFalse_InPreviewMode()
    {
        bool shouldSync = HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
            new HydraulicCalculationResult { UsesSchematicPipeSizingWriteback = true },
            new PipePlacementSummary { PlacedSegmentCount = 2 },
            isPreviewMode: true,
            syncAvailable: true);

        Assert.False(shouldSync);
    }
}
