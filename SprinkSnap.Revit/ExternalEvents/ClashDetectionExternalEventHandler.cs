using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class ClashDetectionExternalEventHandler : IExternalEventHandler
{
    private Document pendingDocument;
    private SprinkSnapShellContext pendingContext;
    private Action<ClashDetectionSummary> pendingCallback;

    public static ClashDetectionExternalEventHandler Instance { get; } = new ClashDetectionExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestDetect(
        Document document,
        SprinkSnapShellContext context,
        Action<ClashDetectionSummary> callback)
    {
        pendingDocument = document;
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        ClashDetectionSummary summary = new ClashDetectionSummary
        {
            Messages = { "Clash detection could not run because the Revit session was unavailable." }
        };

        try
        {
            if (pendingDocument != null && pendingContext != null)
            {
                summary = RevitClashDetectionEngine.Detect(
                    pendingDocument,
                    pendingContext.ProjectState.Rooms);

                pendingContext.ProjectState.ClashSummary = summary;
                pendingContext.ProjectState.SessionProgress.ClashDetectionComplete =
                    SprinkSnapWorkflowGate.IsClashDetectionComplete(pendingContext.ProjectState);
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
        return "SprinkSnap Clash Detection";
    }
}
