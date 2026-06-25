using System;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class HydraulicSupplyPickExternalEventHandler : IExternalEventHandler
{
    private SprinkSnapShellContext pendingContext;
    private Action<HydraulicSupplyAnchor> pendingCallback;

    public static HydraulicSupplyPickExternalEventHandler Instance { get; } = new HydraulicSupplyPickExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestPick(SprinkSnapShellContext context, Action<HydraulicSupplyAnchor> callback)
    {
        pendingContext = context;
        pendingCallback = callback;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        HydraulicSupplyAnchor anchor = new HydraulicSupplyAnchor();
        try
        {
            UIDocument uiDocument = application?.ActiveUIDocument;
            if (uiDocument != null && pendingContext != null)
            {
                anchor = RevitHydraulicSupplyPickService.PickSupplyAnchor(uiDocument);
                if (anchor.IsSet)
                {
                    pendingContext.ProjectState.HydraulicSupplyAnchor = anchor;
                    RevitSessionPersistence.Save(uiDocument.Document, pendingContext.ProjectState);
                    pendingContext.RequestWorkflowRefresh();
                }
            }
        }
        catch (Exception ex)
        {
            anchor = new HydraulicSupplyAnchor
            {
                IsSet = false,
                ElementLabel = ex.Message
            };
        }

        pendingCallback?.Invoke(anchor);
        pendingContext = null;
        pendingCallback = null;
    }

    public string GetName()
    {
        return "SprinkSnap Pick Hydraulic Supply";
    }
}
