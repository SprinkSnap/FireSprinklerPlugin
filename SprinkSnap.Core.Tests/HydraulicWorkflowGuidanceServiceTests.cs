using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicWorkflowGuidanceServiceTests
{
    [Fact]
    public void HasPlacedPipeGeometry_ReturnsTrue_WhenPlacedSegmentsExist()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 3 }
        };

        Assert.True(HydraulicWorkflowGuidanceService.HasPlacedPipeGeometry(state));
    }

    [Fact]
    public void IsSchematicOnlyHydraulicsComplete_ReturnsTrue_WhenHydraulicsRanWithoutPlacedPipes()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { HydraulicsComplete = true },
            PipePlacementSummary = new PipePlacementSummary()
        };

        Assert.True(HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state));
    }

    [Fact]
    public void ShouldWarnMaterialsMissingHydraulics_ReturnsTrue_WhenDesignExistsButHydraulicsNotRun()
    {
        SprinkSnapProjectState state = CreateReadyForWorkflowState();
        state.SessionProgress.HydraulicsComplete = false;
        state.SessionProgress.MaterialsComplete = true;

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnMaterialsMissingHydraulics(state));
    }

    [Fact]
    public void ShouldWarnPipeSizingWithoutPlacement_ReturnsTrue_WhenWritebackOccurredWithoutPlacedPipes()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            PipePlacementSummary = new PipePlacementSummary()
        };
        HydraulicCalculationResult result = new HydraulicCalculationResult
        {
            UsesSchematicPipeSizingWriteback = true,
            SchematicWritebackSegmentCount = 2
        };

        Assert.True(HydraulicWorkflowGuidanceService.ShouldWarnPipeSizingWithoutPlacement(result, state));
    }

    private static SprinkSnapProjectState CreateReadyForWorkflowState()
    {
        return new SprinkSnapProjectState
        {
            SessionProgress =
            {
                ModelAnalysisComplete = true,
                HazardReviewComplete = true,
                SprinklerReviewComplete = true,
                WaterSupplyComplete = true,
                DesignGenerated = true,
                ClashDetectionComplete = true
            },
            Rooms =
            {
                new RoomInfo
                {
                    DesignerApproved = true,
                    ApprovedHazardClassification = HazardClassification.LightHazard,
                    SelectedSprinklerFamilyName = "VK302",
                    ProposedSprinklers = { new SprinklerPlacementCandidate() }
                }
            },
            ClashSummary = new ClashDetectionSummary { TotalClashes = 0 },
            ModelChangeAssessment = new ModelChangeAssessment { IsStale = false }
        };
    }
}
