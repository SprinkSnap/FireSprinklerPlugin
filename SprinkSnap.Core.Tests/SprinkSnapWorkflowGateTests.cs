using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SprinkSnapWorkflowGateTests
{
    [Fact]
    public void IsModelStale_ReturnsTrue_WhenAssessmentIsStale()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            ModelChangeAssessment = new ModelChangeAssessment { IsStale = true }
        };

        Assert.True(SprinkSnapWorkflowGate.IsModelStale(state));
    }

    [Fact]
    public void Evaluate_PlaceSprinklers_IsBlocked_WhenModelIsStale()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.ModelChangeAssessment = new ModelChangeAssessment { IsStale = true };

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.PlaceSprinklers);

        Assert.False(access.IsUnlocked);
        Assert.Equal(SprinkSnapWorkflowGate.StaleModelBlockReason, access.BlockReason);
    }

    [Fact]
    public void Evaluate_PlaceSprinklers_IsUnlocked_WhenClashCompleteAndModelFresh()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.PlaceSprinklers);

        Assert.True(access.IsUnlocked);
        Assert.True(string.IsNullOrWhiteSpace(access.BlockReason));
    }

    [Fact]
    public void IsHazardReviewComplete_RequiresApprovedHazardForAllRooms()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            Rooms =
            {
                new RoomInfo
                {
                    DesignerApproved = true,
                    ApprovedHazardClassification = HazardClassification.LightHazard
                },
                new RoomInfo
                {
                    DesignerApproved = false,
                    ApprovedHazardClassification = HazardClassification.LightHazard
                }
            }
        };

        Assert.False(SprinkSnapWorkflowGate.IsHazardReviewComplete(state));
    }

    [Fact]
    public void Evaluate_Hydraulics_IsBlocked_WhenWaterSupplyIsIncomplete()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.WaterSupplyComplete = false;
        state.WaterSupply = new WaterSupplyInput();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.False(access.IsUnlocked);
        Assert.Contains("water supply", access.BlockReason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Hydraulics_IsUnlocked_WhenWaterSupplyAndClashAreComplete()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.True(access.IsUnlocked);
        Assert.True(string.IsNullOrWhiteSpace(access.BlockReason));
    }

    [Fact]
    public void Evaluate_Materials_ShowsWarning_WhenHydraulicsCompleteAndTakeoffStale()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = true;
        state.SessionProgress.MaterialsComplete = false;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Materials);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Refresh needed", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_Reports_ShowsWarning_WhenMaterialTakeoffIsStale()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = true;
        state.SessionProgress.MaterialsComplete = false;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Reports);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Refresh materials", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_Hydraulics_ShowsWarning_WhenCompleteWithoutPlacedPipeGeometry()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = true;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.True(access.IsUnlocked);
        Assert.True(access.IsComplete);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Place pipes", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_Materials_ShowsWarning_WhenHydraulicsHasNotBeenRun()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = false;
        state.SessionProgress.MaterialsComplete = true;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Materials);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Run hydraulics", access.StatusLabel);
    }

    private static SprinkSnapProjectState CreateReadyForPlacementState()
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
