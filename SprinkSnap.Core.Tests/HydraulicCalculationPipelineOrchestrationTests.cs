using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicCalculationPipelineOrchestrationTests
{
    [Fact]
    public void Run_CompletesWithSingleCalculation_WhenPreviewModeSkipsRemeasureAndSync()
    {
        SprinkSnapProjectState state = CreatePipelineState();
        FakeHydraulicEngine engine = new FakeHydraulicEngine();
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 120, usesWriteback: true));
        PipelineRunRecorder recorder = RunPipeline(
            state,
            engine,
            isPreviewMode: true,
            remeasureAvailable: true,
            syncAvailable: true);

        Assert.Equal(1, engine.CalculateCount);
        Assert.Equal(0, recorder.RemeasureCount);
        Assert.Equal(0, recorder.SyncCount);
        Assert.Equal(120, recorder.Completion.Result.TotalFlowGpm);
        Assert.False(state.SessionProgress.MaterialsComplete);
        Assert.False(state.SessionProgress.ReportsExported);
        Assert.Equal(1, recorder.PersistCount);
        Assert.Equal(1, recorder.WorkflowRefreshCount);
    }

    [Fact]
    public void Run_RemasuresBeforeCalculation_WhenPlacedGeometryExistsAndNotPreview()
    {
        SprinkSnapProjectState state = CreatePipelineState();
        state.PipePlacementSummary.PlacedSegmentCount = 4;
        FakeHydraulicEngine engine = new FakeHydraulicEngine();
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 140));
        PipelineRunRecorder recorder = RunPipeline(
            state,
            engine,
            isPreviewMode: false,
            remeasureAvailable: true,
            syncAvailable: false,
            remeasureSummary: CreateRemeasureSummary("Measured 4 placed pipe segments."));

        Assert.Equal(1, recorder.RemeasureCount);
        Assert.Equal(1, engine.CalculateCount);
        Assert.Contains("Measured 4 placed pipe segments.", recorder.Completion.PipelineMessages);
    }

    [Fact]
    public void Run_SyncsAndReSolves_WhenWritebackOccurredAndPlacedPipesExist()
    {
        SprinkSnapProjectState state = CreatePipelineState();
        state.PipePlacementSummary.PlacedSegmentCount = 3;
        FakeHydraulicEngine engine = new FakeHydraulicEngine();
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 150, usesWriteback: true));
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 155, usesWriteback: true));
        PipePlacementSummary syncSummary = new PipePlacementSummary
        {
            UsesRevitPipeDiameterSync = true,
            RevitPipeDiameterSyncCount = 2,
            UsesRevitFittingDiameterSync = true,
            RevitFittingDiameterSyncCount = 1,
            Messages = { "Updated 2 Revit pipes and 1 fitting." }
        };

        PipelineRunRecorder recorder = RunPipeline(
            state,
            engine,
            isPreviewMode: false,
            remeasureAvailable: false,
            syncAvailable: true,
            syncSummary: syncSummary);

        Assert.Equal(1, recorder.SyncCount);
        Assert.Equal(2, engine.CalculateCount);
        Assert.True(recorder.Completion.Result.UsesPostSyncHydraulicReSolve);
        Assert.True(recorder.Completion.Result.UsesRevitPipeDiameterSync);
        Assert.Equal(2, recorder.Completion.Result.RevitPipeDiameterSyncCount);
        Assert.True(recorder.Completion.Result.UsesRevitFittingDiameterSync);
        Assert.Equal(1, recorder.Completion.Result.RevitFittingDiameterSyncCount);
        Assert.Equal(155, recorder.Completion.Result.TotalFlowGpm);
        Assert.Contains("Updated 2 Revit pipes and 1 fitting.", recorder.Completion.PipelineMessages);
        Assert.True(recorder.StatusMessages.Exists(message =>
            message.Contains("Re-running hydraulics with synced Revit pipe diameters", System.StringComparison.Ordinal)));
    }

    [Fact]
    public void Run_SkipsSync_WhenWritebackDidNotOccur()
    {
        SprinkSnapProjectState state = CreatePipelineState();
        state.PipePlacementSummary.PlacedSegmentCount = 3;
        FakeHydraulicEngine engine = new FakeHydraulicEngine();
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 130, usesWriteback: false));

        PipelineRunRecorder recorder = RunPipeline(
            state,
            engine,
            isPreviewMode: false,
            remeasureAvailable: false,
            syncAvailable: true);

        Assert.Equal(0, recorder.SyncCount);
        Assert.Equal(1, engine.CalculateCount);
        Assert.False(recorder.Completion.Result.UsesPostSyncHydraulicReSolve);
    }

    [Fact]
    public void Run_ClearsDownstreamCompletionFlags_OnEachFinalizePass()
    {
        SprinkSnapProjectState state = CreatePipelineState();
        state.SessionProgress.MaterialsComplete = true;
        state.SessionProgress.ReportsExported = true;
        state.PipePlacementSummary.PlacedSegmentCount = 2;
        FakeHydraulicEngine engine = new FakeHydraulicEngine();
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 160, usesWriteback: true));
        engine.Enqueue(CreateCalculationResult(totalFlowGpm: 165, usesWriteback: true));

        RunPipeline(
            state,
            engine,
            isPreviewMode: false,
            remeasureAvailable: false,
            syncAvailable: true,
            syncSummary: new PipePlacementSummary
            {
                UsesRevitPipeDiameterSync = true,
                RevitPipeDiameterSyncCount = 1
            });

        Assert.True(state.SessionProgress.HydraulicsComplete);
        Assert.False(state.SessionProgress.MaterialsComplete);
        Assert.False(state.SessionProgress.ReportsExported);
    }

    private static PipelineRunRecorder RunPipeline(
        SprinkSnapProjectState state,
        FakeHydraulicEngine engine,
        bool isPreviewMode,
        bool remeasureAvailable,
        bool syncAvailable,
        PipePlacementSummary remeasureSummary = null,
        PipePlacementSummary syncSummary = null)
    {
        PipelineRunRecorder recorder = new PipelineRunRecorder();
        HydraulicCalculationPipelineOrchestrator orchestrator = new HydraulicCalculationPipelineOrchestrator();
        orchestrator.Run(
            new HydraulicCalculationPipelineRunRequest
            {
                ProjectState = state,
                IsPreviewMode = isPreviewMode,
                RemeasureAvailable = remeasureAvailable,
                SyncAvailable = syncAvailable,
                RequestRemeasurePlacedPipes = callback =>
                {
                    recorder.RemeasureCount++;
                    callback(remeasureSummary ?? new PipePlacementSummary());
                },
                RequestSyncPlacedPipeDiameters = callback =>
                {
                    recorder.SyncCount++;
                    callback(syncSummary ?? new PipePlacementSummary());
                },
                PersistToRevit = () => recorder.PersistCount++,
                RequestWorkflowRefresh = () => recorder.WorkflowRefreshCount++
            },
            engine,
            new HydraulicCalculationPipelineCallbacks
            {
                OnStatusChanged = status =>
                {
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        recorder.StatusMessages.Add(status);
                    }
                },
                OnCompleted = completion => recorder.Completion = completion
            });

        return recorder;
    }

    private static SprinkSnapProjectState CreatePipelineState()
    {
        return new SprinkSnapProjectState
        {
            Preferences = new SprinkSnapProjectPreferences(),
            SchematicPipeRouting = new SchematicPipeRoutingSummary
            {
                TotalSegmentCount = 5
            },
            PipePlacementSummary = new PipePlacementSummary(),
            WaterSupply = new WaterSupplyInput
            {
                StaticPressurePsi = 80,
                ResidualPressurePsi = 65,
                FlowAtResidualGpm = 1000
            },
            Rooms =
            {
                new RoomInfo
                {
                    DesignerApproved = true,
                    ApprovedHazardClassification = HazardClassification.LightHazard
                }
            }
        };
    }

    private static PipePlacementSummary CreateRemeasureSummary(string message)
    {
        return new PipePlacementSummary
        {
            PlacedSegmentCount = 4,
            Messages = { message }
        };
    }

    private static HydraulicCalculationResult CreateCalculationResult(
        double totalFlowGpm,
        bool usesWriteback = false)
    {
        return new HydraulicCalculationResult
        {
            TotalFlowGpm = totalFlowGpm,
            SystemDemandPsi = 52,
            UsesSchematicPipeSizingWriteback = usesWriteback,
            SchematicWritebackSegmentCount = usesWriteback ? 2 : 0
        };
    }

    private sealed class PipelineRunRecorder
    {
        public HydraulicCalculationPipelineCompletion Completion { get; set; } = new HydraulicCalculationPipelineCompletion();

        public List<string> StatusMessages { get; } = new List<string>();

        public int RemeasureCount { get; set; }

        public int SyncCount { get; set; }

        public int PersistCount { get; set; }

        public int WorkflowRefreshCount { get; set; }
    }

    private sealed class FakeHydraulicEngine : IHydraulicEngine
    {
        private readonly Queue<HydraulicCalculationResult> results = new Queue<HydraulicCalculationResult>();

        public int CalculateCount { get; private set; }

        public void Enqueue(HydraulicCalculationResult result)
        {
            results.Enqueue(result);
        }

        public HydraulicCalculationResult Calculate(
            IEnumerable<RoomInfo> rooms,
            WaterSupplyInput waterSupply,
            SprinklerPlacementSummary placementSummary = null,
            SchematicPipeRoutingSummary schematicPipeRouting = null,
            PipePlacementSummary pipePlacementSummary = null,
            HydraulicSupplyAnchor supplyAnchor = null,
            SprinkSnapProjectPreferences preferences = null)
        {
            CalculateCount++;
            if (results.Count > 0)
            {
                return results.Dequeue();
            }

            return CreateCalculationResult(totalFlowGpm: 100);
        }
    }
}
