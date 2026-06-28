using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
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
        state.WaterSupplyValidation = new WaterSupplyValidationResult();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.False(access.IsUnlocked);
        Assert.Contains("water supply", access.BlockReason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsWaterSupplyComplete_RequiresSessionValidationFlag()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.WaterSupplyComplete = false;

        Assert.False(SprinkSnapWorkflowGate.IsWaterSupplyComplete(state));

        state.SessionProgress.WaterSupplyComplete = true;

        Assert.True(SprinkSnapWorkflowGate.IsWaterSupplyComplete(state));
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
    public void Evaluate_Materials_ShowsWarning_WhenHydraulicsNotRun()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = false;

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Materials);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Run hydraulics", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_PlaceSprinklers_ShowsWarning_WhenSchematicRoutingExistsWithoutPlacedPipes()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SchematicPipeRouting = new SchematicPipeRoutingSummary { TotalSegmentCount = 8 };
        state.PipePlacementSummary = new PipePlacementSummary();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.PlaceSprinklers);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Place pipes", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_Hydraulics_ShowsWarning_WhenCompleteWithoutPlacedPipeGeometry()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = true;
        state.PipePlacementSummary = new PipePlacementSummary();

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.True(access.IsUnlocked);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Place pipes", access.StatusLabel);
    }

    [Fact]
    public void Evaluate_Hydraulics_ShowsReRunWarning_WhenPipesPlacedButResultIsStale()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.SessionProgress.HydraulicsComplete = true;
        state.PipePlacementSummary = new PipePlacementSummary { PlacedSegmentCount = 5 };
        state.HydraulicResult = new HydraulicCalculationResult
        {
            TotalFlowGpm = 95,
            UsesPlacedPipeLengths = false,
            UsesPlacedPipeTopology = false
        };

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.Hydraulics);

        Assert.True(access.IsUnlocked);
        Assert.False(access.IsComplete);
        Assert.Equal(WorkflowStepStatus.Warning, access.Status);
        Assert.Equal("Re-run hydraulics", access.StatusLabel);
    }

    [Fact]
    public void IsSprinklerReviewComplete_ReturnsFalse_WhenHighCeilingSelectionViolatesNfpa13()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.Rooms[0].CeilingHeightFeet = 35;
        state.Rooms[0].ApprovedHazardClassification = HazardClassification.OrdinaryHazardGroup2;
        state.Rooms[0].SelectedSprinklerFamilyName = "VK302";

        Assert.False(SprinkSnapWorkflowGate.IsSprinklerReviewComplete(state));
    }

    [Fact]
    public void Evaluate_GenerateDesign_BlocksWithHighCeilingReason_WhenSelectionsViolateNfpa13()
    {
        SprinkSnapProjectState state = CreateReadyForPlacementState();
        state.Rooms[0].CeilingHeightFeet = 35;
        state.Rooms[0].ApprovedHazardClassification = HazardClassification.OrdinaryHazardGroup2;
        state.Rooms[0].SelectedSprinklerFamilyName = "VK302";

        WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(state, SprinkSnapWorkflowStep.GenerateDesign);

        Assert.False(access.IsUnlocked);
        Assert.Equal(Nfpa13HighCeilingSprinklerSelectionService.ProjectViolationBlockReason, access.BlockReason);
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
