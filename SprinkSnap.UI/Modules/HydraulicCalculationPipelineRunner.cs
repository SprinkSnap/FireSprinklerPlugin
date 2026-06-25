using System.Collections.Generic;
using System.Windows;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.UI.Modules;

public sealed class HydraulicCalculationPipelineCompletion
{
    public HydraulicCalculationResult Result { get; set; } = new HydraulicCalculationResult();

    public IList<string> PipelineMessages { get; set; } = new List<string>();
}

public sealed class HydraulicCalculationPipelineCallbacks
{
    public System.Action<string> OnStatusChanged { get; set; }

    public System.Action<HydraulicCalculationPipelineCompletion> OnCompleted { get; set; }
}

public sealed class HydraulicCalculationPipelineRunner
{
    public void Run(
        SprinkSnapShellContext context,
        IHydraulicEngine hydraulicEngine,
        HydraulicCalculationPipelineCallbacks callbacks)
    {
        if (HydraulicsPipeDataRefreshPolicy.ShouldRemeasureBeforeCalculation(
                context.ProjectState.SchematicPipeRouting,
                context.ProjectState.PipePlacementSummary,
                context.IsPreviewMode,
                context.RequestRemeasurePlacedPipes != null))
        {
            callbacks.OnStatusChanged?.Invoke("Re-measuring placed pipe lengths from Revit before hydraulic calculation...");
            context.RequestRemeasurePlacedPipes(summary =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    context.ProjectState.PipePlacementSummary = summary;
                    ExecuteCalculationStep(
                        context,
                        hydraulicEngine,
                        callbacks,
                        remeasureMessages: summary.Messages,
                        skipDiameterSync: false,
                        priorSyncMessages: null,
                        syncSummaryForFlags: null);
                });
            });
            return;
        }

        ExecuteCalculationStep(
            context,
            hydraulicEngine,
            callbacks,
            remeasureMessages: null,
            skipDiameterSync: false,
            priorSyncMessages: null,
            syncSummaryForFlags: null);
    }

    private static void ExecuteCalculationStep(
        SprinkSnapShellContext context,
        IHydraulicEngine hydraulicEngine,
        HydraulicCalculationPipelineCallbacks callbacks,
        IList<string> remeasureMessages,
        bool skipDiameterSync,
        IList<string> priorSyncMessages,
        PipePlacementSummary syncSummaryForFlags)
    {
        HydraulicCalculationResult result = hydraulicEngine.Calculate(
            context.ProjectState.Rooms,
            context.ProjectState.WaterSupply,
            context.ProjectState.PlacementSummary,
            context.ProjectState.SchematicPipeRouting,
            context.ProjectState.PipePlacementSummary,
            context.ProjectState.HydraulicSupplyAnchor);

        if (syncSummaryForFlags != null)
        {
            HydraulicCalculationPipelineService.ApplySyncSummaryFlags(result, syncSummaryForFlags);
        }

        HydraulicCalculationPipelineService.FinalizeSession(context.ProjectState, result);
        context.RequestPersistToRevit();
        context.RequestWorkflowRefresh();

        if (!skipDiameterSync
            && HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
                result,
                context.ProjectState.PipePlacementSummary,
                context.IsPreviewMode,
                context.RequestSyncPlacedPipeDiameters != null))
        {
            callbacks.OnStatusChanged?.Invoke(
                "Syncing velocity-sized pipe and fitting diameters to placed Revit elements...");
            context.RequestSyncPlacedPipeDiameters(summary =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    context.ProjectState.PipePlacementSummary = summary;

                    if (HydraulicsPipeDataRefreshPolicy.ShouldReSolveAfterDiameterSync(
                            diameterSyncWasAttempted: true,
                            context.IsPreviewMode))
                    {
                        callbacks.OnStatusChanged?.Invoke("Re-running hydraulics with synced Revit pipe diameters...");
                        ExecuteCalculationStep(
                            context,
                            hydraulicEngine,
                            callbacks,
                            remeasureMessages,
                            skipDiameterSync: true,
                            priorSyncMessages: summary.Messages,
                            syncSummaryForFlags: summary);
                        return;
                    }

                    HydraulicCalculationResult latestResult = context.ProjectState.HydraulicResult ?? result;
                    CompletePipeline(
                        callbacks,
                        latestResult,
                        HydraulicCalculationPipelineService.CombinePipelineMessages(remeasureMessages, summary.Messages));
                });
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
