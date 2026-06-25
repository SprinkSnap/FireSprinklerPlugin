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
