using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class PipeDiameterSyncExternalEventHandler : IExternalEventHandler
{
    private Document pendingDocument;
    private SprinkSnapShellContext pendingContext;
    private Action<PipePlacementSummary> pendingCallback;

    public static PipeDiameterSyncExternalEventHandler Instance { get; } = new PipeDiameterSyncExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestSync(
        Document document,
        SprinkSnapShellContext context,
        Action<PipePlacementSummary> callback)
    {
        pendingDocument = document;
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        PipePlacementSummary summary = new PipePlacementSummary
        {
            Messages = { "Pipe diameter sync could not run because the Revit session was unavailable." }
        };

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                PlacedPipeDiameterSyncPlan plan = SchematicPipeRevitDiameterSyncPlanner.BuildPlan(
                    pendingContext.ProjectState.SchematicPipeRouting,
                    pendingContext.ProjectState.PipePlacementSummary);

                PlacedPipeDiameterSyncSummary syncSummary = RevitPipeDiameterSyncService.Sync(pendingDocument, plan);
                summary = RevitPipeMeasurementService.Measure(
                    pendingDocument,
                    pendingContext.ProjectState.Rooms,
                    pendingContext.ProjectState.SchematicPipeRouting);

                summary.UsesRevitPipeDiameterSync = syncSummary.UsesRevitPipeDiameterSync;
                summary.RevitPipeDiameterSyncCount = syncSummary.UpdatedCount;
                foreach (string message in syncSummary.Messages)
                {
                    if (!summary.Messages.Contains(message))
                    {
                        summary.Messages.Add(message);
                    }
                }

                pendingContext.ProjectState.PipePlacementSummary = summary;
                if (pendingContext.ProjectState.HydraulicResult != null)
                {
                    pendingContext.ProjectState.HydraulicResult.UsesRevitPipeDiameterSync = syncSummary.UsesRevitPipeDiameterSync;
                    pendingContext.ProjectState.HydraulicResult.RevitPipeDiameterSyncCount = syncSummary.UpdatedCount;
                }

                RevitSessionPersistence.Save(pendingDocument, pendingContext.ProjectState);
                pendingContext.RequestWorkflowRefresh();
            }
        }
        catch (Exception ex)
        {
            summary.Messages.Add(ex.Message);
        }

        pendingCallback?.Invoke(summary);
        pendingDocument = null;
        pendingContext = null;
        pendingCallback = null;
    }

    public string GetName()
    {
        return "SprinkSnap Sync Placed Pipe Diameters";
    }
}
