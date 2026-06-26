using System;
using System.Windows;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.UI.Modules;

public sealed class HydraulicCalculationPipelineRunner
{
    private readonly HydraulicCalculationPipelineOrchestrator orchestrator = new HydraulicCalculationPipelineOrchestrator();

    public void Run(
        SprinkSnapShellContext context,
        IHydraulicEngine hydraulicEngine,
        HydraulicCalculationPipelineCallbacks callbacks)
    {
        orchestrator.Run(
            BuildRequest(context),
            hydraulicEngine,
            callbacks);
    }

    private static HydraulicCalculationPipelineRunRequest BuildRequest(SprinkSnapShellContext context)
    {
        return new HydraulicCalculationPipelineRunRequest
        {
            ProjectState = context.ProjectState,
            IsPreviewMode = context.IsPreviewMode,
            RemeasureAvailable = context.RequestRemeasurePlacedPipes != null,
            SyncAvailable = context.RequestSyncPlacedPipeDiameters != null,
            RequestRemeasurePlacedPipes = callback => context.RequestRemeasurePlacedPipes?.Invoke(WrapOnUiThread(callback)),
            RequestSyncPlacedPipeDiameters = callback => context.RequestSyncPlacedPipeDiameters?.Invoke(WrapOnUiThread(callback)),
            PersistToRevit = () => context.RequestPersistToRevit(),
            RequestWorkflowRefresh = () => context.RequestWorkflowRefresh()
        };
    }

    private static Action<PipePlacementSummary> WrapOnUiThread(Action<PipePlacementSummary> callback)
    {
        return summary =>
        {
            Application.Current?.Dispatcher.Invoke(() => callback(summary));
        };
    }
}
