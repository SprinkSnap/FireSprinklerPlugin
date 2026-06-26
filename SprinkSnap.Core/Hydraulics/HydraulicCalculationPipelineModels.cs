using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicCalculationPipelineCompletion
{
    public HydraulicCalculationResult Result { get; set; } = new HydraulicCalculationResult();

    public IList<string> PipelineMessages { get; set; } = new List<string>();
}

public sealed class HydraulicCalculationPipelineCallbacks
{
    public Action<string> OnStatusChanged { get; set; }

    public Action<HydraulicCalculationPipelineCompletion> OnCompleted { get; set; }
}

public sealed class HydraulicCalculationPipelineRunRequest
{
    public SprinkSnapProjectState ProjectState { get; set; }

    public bool IsPreviewMode { get; set; }

    public bool RemeasureAvailable { get; set; }

    public bool SyncAvailable { get; set; }

    public Action<Action<PipePlacementSummary>> RequestRemeasurePlacedPipes { get; set; }

    public Action<Action<PipePlacementSummary>> RequestSyncPlacedPipeDiameters { get; set; }

    public Action PersistToRevit { get; set; }

    public Action RequestWorkflowRefresh { get; set; }
}
