using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class StaleModelReconciliationServiceTests
{
    [Fact]
    public void IsReconciliationActive_ReturnsTrue_WhenModelIsStale()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            ModelChangeAssessment = new ModelChangeAssessment { IsStale = true }
        };

        Assert.True(StaleModelReconciliationService.IsReconciliationActive(state));
    }

    [Fact]
    public void BuildSteps_MarksAnalyzeFirst_WhenModelIsStale()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            ModelChangeAssessment = new ModelChangeAssessment { IsStale = true },
            SessionProgress = { ReconciliationRequired = true }
        };

        StaleModelReconciliationStep firstStep = StaleModelReconciliationService.BuildSteps(state).First();

        Assert.Equal(SprinkSnapWorkflowStep.AnalyzeModel, firstStep.WorkflowStep);
        Assert.False(firstStep.IsComplete);
        Assert.True(firstStep.IsCurrent);
    }

    [Fact]
    public void Evaluate_Hydraulics_IsBlocked_WhenReconciliationRequired()
    {
        SprinkSnapProjectState state = CreateReadyForHydraulicsState();
        state.SessionProgress.ReconciliationRequired = true;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.False(access.IsUnlocked);
        Assert.Contains("reconciliation", access.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateReconciliationState_ClearsFlag_WhenAllStepsComplete()
    {
        SprinkSnapProjectState state = CreateFullyReconciledState();
        state.SessionProgress.ReconciliationRequired = true;

        StaleModelReconciliationService.UpdateReconciliationState(state);

        Assert.False(state.SessionProgress.ReconciliationRequired);
    }

    private static SprinkSnapProjectState CreateReadyForHydraulicsState()
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
            ModelChangeAssessment = new ModelChangeAssessment { IsStale = false },
            WaterSupplyValidation = new WaterSupplyValidationResult { InputIsCompliant = true, IsAdequate = true }
        };
    }

    private static SprinkSnapProjectState CreateFullyReconciledState()
    {
        SprinkSnapProjectState state = CreateReadyForHydraulicsState();
        state.SessionProgress.SprinklersPlacedInRevit = true;
        state.SessionProgress.HydraulicsComplete = true;
        state.SessionProgress.MaterialsComplete = true;
        state.PlacementSummary = new SprinklerPlacementSummary { PlacedCount = 1 };
        return state;
    }
}
