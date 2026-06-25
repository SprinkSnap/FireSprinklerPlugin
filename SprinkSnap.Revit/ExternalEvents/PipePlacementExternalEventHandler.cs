using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class PipePlacementExternalEventHandler : IExternalEventHandler
{
    private SprinkSnapShellContext pendingContext;
    private Document pendingDocument;
    private Action<PipePlacementSummary> pendingCallback;

    public static PipePlacementExternalEventHandler Instance { get; } = new PipePlacementExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestPlacement(
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
            Messages = { "Pipe placement could not run because the Revit session was unavailable." }
        };

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                summary = RevitPipePlacementService.Place(
                    pendingDocument,
                    pendingContext.ProjectState.Rooms,
                    pendingContext.ProjectState.SchematicPipeRouting);

                pendingContext.ProjectState.PipePlacementSummary = summary;
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
        return "SprinkSnap Place Schematic Pipes";
    }
}
