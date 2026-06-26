using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicCalculationPipelineOrchestrator
{
    public void Run(
        HydraulicCalculationPipelineRunRequest request,
        IHydraulicEngine hydraulicEngine,
        HydraulicCalculationPipelineCallbacks callbacks)
    {
        if (request?.ProjectState == null || hydraulicEngine == null || callbacks == null)
        {
            return;
        }

        if (HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
                request.ProjectState.SchematicPipeRouting,
                request.ProjectState.PipePlacementSummary,
                request.IsPreviewMode,
                request.RemeasureAvailable))
        {
            callbacks.OnStatusChanged?.Invoke("Re-measuring placed pipe lengths from Revit before hydraulic calculation...");
            request.RequestRemeasurePlacedPipes?.Invoke(summary =>
            {
                request.ProjectState.PipePlacementSummary = summary;
                ExecuteCalculationStep(
                    request,
                    hydraulicEngine,
                    callbacks,
                    remeasureMessages: summary.Messages,
                    skipDiameterSync: false,
                    priorSyncMessages: null,
                    syncSummaryForFlags: null);
            });
            return;
        }

        ExecuteCalculationStep(
            request,
            hydraulicEngine,
            callbacks,
            remeasureMessages: null,
            skipDiameterSync: false,
            priorSyncMessages: null,
            syncSummaryForFlags: null);
    }

    private static void ExecuteCalculationStep(
        HydraulicCalculationPipelineRunRequest request,
        IHydraulicEngine hydraulicEngine,
        HydraulicCalculationPipelineCallbacks callbacks,
        IList<string> remeasureMessages,
        bool skipDiameterSync,
        IList<string> priorSyncMessages,
        PipePlacementSummary syncSummaryForFlags)
    {
        SprinkSnapProjectState state = request.ProjectState;
        HydraulicCalculationResult result = hydraulicEngine.Calculate(
            state.Rooms,
            state.WaterSupply,
            state.PlacementSummary,
            state.SchematicPipeRouting,
            state.PipePlacementSummary,
            state.HydraulicSupplyAnchor,
            state.Preferences);

        if (syncSummaryForFlags != null)
        {
            HydraulicCalculationPipelineService.ApplySyncSummaryFlags(result, syncSummaryForFlags);
        }

        HydraulicCalculationPipelineService.FinalizeSession(state, result);
        request.PersistToRevit?.Invoke();
        request.RequestWorkflowRefresh?.Invoke();

        if (!skipDiameterSync
            && HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
                result,
                state.PipePlacementSummary,
                request.IsPreviewMode,
                request.SyncAvailable))
        {
            callbacks.OnStatusChanged?.Invoke(
                "Syncing velocity-sized pipe and fitting diameters to placed Revit elements...");
            request.RequestSyncPlacedPipeDiameters?.Invoke(summary =>
            {
                state.PipePlacementSummary = summary;

                if (HydraulicsPipeDataRefreshPolicy.ShouldReSolveAfterDiameterSync(
                        diameterSyncWasAttempted: true,
                        request.IsPreviewMode))
                {
                    callbacks.OnStatusChanged?.Invoke("Re-running hydraulics with synced Revit pipe diameters...");
                    ExecuteCalculationStep(
                        request,
                        hydraulicEngine,
                        callbacks,
                        remeasureMessages,
                        skipDiameterSync: true,
                        priorSyncMessages: summary.Messages,
                        syncSummaryForFlags: summary);
                    return;
                }

                HydraulicCalculationResult latestResult = state.HydraulicResult ?? result;
                CompletePipeline(
                    callbacks,
                    latestResult,
                    HydraulicCalculationPipelineService.CombinePipelineMessages(remeasureMessages, summary.Messages));
            });
            return;
        }

        CompletePipeline(
            callbacks,
            result,
            HydraulicCalculationPipelineService.CombinePipelineMessages(remeasureMessages, priorSyncMessages));
    }

    private static void CompletePipeline(
        HydraulicCalculationPipelineCallbacks callbacks,
        HydraulicCalculationResult result,
        IList<string> pipelineMessages)
    {
        callbacks.OnCompleted?.Invoke(new HydraulicCalculationPipelineCompletion
        {
            Result = result,
            PipelineMessages = pipelineMessages ?? new List<string>()
        });
    }
}
