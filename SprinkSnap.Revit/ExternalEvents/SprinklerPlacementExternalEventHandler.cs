using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class SprinklerPlacementExternalEventHandler : IExternalEventHandler
{
    private SprinkSnapShellContext pendingContext;
    private Document pendingDocument;
    private Action<SprinklerPlacementSummary> pendingCallback;

    public static SprinklerPlacementExternalEventHandler Instance { get; } = new SprinklerPlacementExternalEventHandler();

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
        Action<SprinklerPlacementSummary> callback)
    {
        pendingDocument = document;
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        SprinklerPlacementSummary summary = new SprinklerPlacementSummary
        {
            Messages = { "Placement could not run because the Revit session was unavailable." }
        };

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                summary = RevitSprinklerPlacementService.Place(
                    pendingDocument,
                    pendingContext.ProjectState.Rooms,
                    pendingContext.SprinklerFamilies);

                pendingContext.ProjectState.PlacementSummary = summary;
                pendingContext.ProjectState.SessionProgress.SprinklersPlacedInRevit =
                    SprinkSnapWorkflowGate.IsSprinklersPlacedInRevit(pendingContext.ProjectState);
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
        return "SprinkSnap Place Sprinklers";
    }
}
