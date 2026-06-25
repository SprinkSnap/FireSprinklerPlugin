using System;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;

namespace FireSprinklerPlugin.SprinkSnap.Revit.ExternalEvents;

public sealed class RevitNavigationExternalEventHandler : IExternalEventHandler
{
    private SprinklerClashRecord pendingClash;
    private int pendingRoomElementId;

    public static RevitNavigationExternalEventHandler Instance { get; } = new RevitNavigationExternalEventHandler();

    public static ExternalEvent ExternalEvent { get; private set; }

    public static void Register()
    {
        if (ExternalEvent == null)
        {
            ExternalEvent = Autodesk.Revit.UI.ExternalEvent.Create(Instance);
        }
    }

    public void RequestShowClash(SprinklerClashRecord clash)
    {
        pendingClash = clash;
        pendingRoomElementId = 0;
        ExternalEvent?.Raise();
    }

    public void RequestShowRoom(int roomRevitElementId)
    {
        pendingRoomElementId = roomRevitElementId;
        pendingClash = null;
        ExternalEvent?.Raise();
    }

    public void Execute(UIApplication application)
    {
        UIDocument uiDocument = application.ActiveUIDocument;
        if (uiDocument == null)
        {
            pendingClash = null;
            pendingRoomElementId = 0;
            return;
        }

        if (pendingClash != null)
        {
            RevitNavigationService.ShowClash(uiDocument, pendingClash);
        }
        else if (pendingRoomElementId > 0)
        {
            RevitNavigationService.ShowRoom(uiDocument, pendingRoomElementId);
        }

        pendingClash = null;
        pendingRoomElementId = 0;
    }

    public string GetName()
    {
        return "SprinkSnap Revit Navigation";
    }
}
