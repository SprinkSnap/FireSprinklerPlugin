using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class ReconciliationTruthfulnessTests
{
    [Fact]
    public void InvalidateDownstreamDesign_ClearsSummariesAndSessionFlags()
    {
        SprinkSnapProjectState state = CreateStateWithDownstreamArtifacts();

        DownstreamDesignInvalidationService.InvalidateDownstreamDesign(state);

        Assert.Equal(0, state.ClashSummary.TotalClashes);
        Assert.Equal(0, state.PlacementSummary.PlacedCount);
        Assert.Equal(0, state.PipePlacementSummary.PlacedSegmentCount);
        Assert.Equal(0, state.SchematicPipeRouting.TotalSegmentCount);
        Assert.False(state.SessionProgress.DesignGenerated);
        Assert.False(state.SessionProgress.ClashDetectionComplete);
        Assert.False(state.SessionProgress.SprinklersPlacedInRevit);
        Assert.False(state.SessionProgress.HydraulicsComplete);
        Assert.False(state.SessionProgress.MaterialsComplete);
        Assert.False(state.SessionProgress.ReportsExported);
    }

    [Fact]
    public void InvalidateDownstreamDesign_ClearsOnlyChangedRoomLayouts_WhenChangedRoomsProvided()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            Rooms =
            {
                new RoomInfo
                {
                    RevitElementId = 101,
                    Number = "101",
                    DesignerApproved = true,
                    ApprovedHazardClassification = HazardClassification.LightHazard,
                    ProposedSprinklers = { new SprinklerPlacementCandidate() }
                },
                new RoomInfo
                {
                    RevitElementId = 202,
                    Number = "202",
                    DesignerApproved = true,
                    ApprovedHazardClassification = HazardClassification.LightHazard,
                    ProposedSprinklers = { new SprinklerPlacementCandidate() }
                }
            }
        };

        DownstreamDesignInvalidationService.InvalidateDownstreamDesign(
            state,
            new[] { 101 },
            clearHazardApprovalsForChangedRooms: true);

        RoomInfo changedRoom = state.Rooms.Single(room => room.RevitElementId == 101);
        RoomInfo unchangedRoom = state.Rooms.Single(room => room.RevitElementId == 202);
        Assert.Empty(changedRoom.ProposedSprinklers);
        Assert.False(changedRoom.DesignerApproved);
        Assert.Equal(string.Empty, changedRoom.ApprovedHazardClassification);
        Assert.Single(unchangedRoom.ProposedSprinklers);
        Assert.True(unchangedRoom.DesignerApproved);
    }

    [Fact]
    public void IsDesignGenerated_IgnoresLegacyLayouts_WhenReconciliationRequired()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { ReconciliationRequired = true },
            Rooms =
            {
                new RoomInfo
                {
                    ProposedSprinklers = { new SprinklerPlacementCandidate() }
                }
            }
        };

        Assert.False(SprinkSnapWorkflowGate.IsDesignGenerated(state));
    }

    [Fact]
    public void IsClashDetectionComplete_IgnoresLegacySummary_WhenReconciliationRequired()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            SessionProgress = { ReconciliationRequired = true },
            ClashSummary = new ClashDetectionSummary { TotalClashes = 0 }
        };

        Assert.False(SprinkSnapWorkflowGate.IsClashDetectionComplete(state));
    }

    [Fact]
    public void BuildSteps_MarksDownstreamStepsIncomplete_AfterInvalidation()
    {
        SprinkSnapProjectState state = CreateStateWithDownstreamArtifacts();
        state.SessionProgress.ReconciliationRequired = true;
        state.ModelChangeAssessment = new ModelChangeAssessment { IsStale = false };

        DownstreamDesignInvalidationService.InvalidateDownstreamDesign(state);
        state.SessionProgress.ReconciliationRequired = true;

        IList<StaleModelReconciliationStep> steps = StaleModelReconciliationService.BuildSteps(state);

        Assert.False(steps.Single(step => step.WorkflowStep == SprinkSnapWorkflowStep.GenerateDesign).IsComplete);
        Assert.False(steps.Single(step => step.WorkflowStep == SprinkSnapWorkflowStep.ClashDetection).IsComplete);
        Assert.False(steps.Single(step => step.WorkflowStep == SprinkSnapWorkflowStep.PlaceSprinklers).IsComplete);
        Assert.False(steps.Single(step => step.WorkflowStep == SprinkSnapWorkflowStep.Hydraulics).IsComplete);
        Assert.False(steps.Single(step => step.WorkflowStep == SprinkSnapWorkflowStep.Materials).IsComplete);
    }

    [Fact]
    public void Evaluate_PlaceSprinklers_IsBlocked_WhenReconciliationRequired()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.ReconciliationRequired = true;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.PlaceSprinklers);

        Assert.False(access.IsUnlocked);
        Assert.Contains("reconciliation", access.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    private static SprinkSnapProjectState CreateStateWithDownstreamArtifacts()
    {
        return new SprinkSnapProjectState
        {
            SessionProgress =
            {
                DesignGenerated = true,
                ClashDetectionComplete = true,
                SprinklersPlacedInRevit = true,
                HydraulicsComplete = true,
                MaterialsComplete = true,
                ReportsExported = true
            },
            ClashSummary = new ClashDetectionSummary { TotalClashes = 2, UnresolvedClashes = 1 },
            PlacementSummary = new SprinklerPlacementSummary { PlacedCount = 4 },
            PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 6 },
            SchematicPipeRouting = new SchematicPipeRoutingSummary { TotalSegmentCount = 6 },
            HydraulicResult = new HydraulicCalculationResult { TotalFlowGpm = 120 },
            Rooms =
            {
                new RoomInfo
                {
                    RevitElementId = 101,
                    ProposedSprinklers = { new SprinklerPlacementCandidate() }
                }
            }
        };
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
