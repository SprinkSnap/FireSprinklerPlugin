using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicCalculationPipelineService
{
    public static void ApplySyncSummaryFlags(
        HydraulicCalculationResult result,
        PipePlacementSummary syncSummary)
    {
        if (result == null || syncSummary == null)
        {
            return;
        }

        result.UsesRevitPipeDiameterSync = syncSummary.UsesRevitPipeDiameterSync;
        result.RevitPipeDiameterSyncCount = syncSummary.RevitPipeDiameterSyncCount;
        result.UsesRevitFittingDiameterSync = syncSummary.UsesRevitFittingDiameterSync;
        result.RevitFittingDiameterSyncCount = syncSummary.RevitFittingDiameterSyncCount;
        result.UsesPostSyncHydraulicReSolve = true;
    }

    public static void FinalizeSession(SprinkSnapProjectState state, HydraulicCalculationResult result)
    {
        if (state == null || result == null)
        {
            return;
        }

        state.HydraulicResult = result;
        state.SessionProgress.HydraulicsComplete = result.TotalFlowGpm > 0;
        state.SessionProgress.MaterialsComplete = false;
        state.SessionProgress.ReportsExported = false;
    }

    public static IList<string> CombinePipelineMessages(
        IList<string> remeasureMessages,
        IList<string> syncMessages)
    {
        List<string> messages = new List<string>();
        AppendMessages(messages, remeasureMessages);
        AppendMessages(messages, syncMessages);
        return messages;
    }

    private static void AppendMessages(IList<string> target, IList<string> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (string message in source)
        {
            if (!string.IsNullOrWhiteSpace(message) && !target.Contains(message))
            {
                target.Add(message);
            }
        }
    }
}
