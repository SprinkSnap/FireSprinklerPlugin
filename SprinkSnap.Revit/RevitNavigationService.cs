using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitNavigationService
{
    public static void ShowClash(UIDocument uiDocument, SprinklerClashRecord clash)
    {
        if (uiDocument == null || clash == null)
        {
            return;
        }

        List<ElementId> elementIds = new List<ElementId>();
        if (clash.RoomRevitElementId > 0)
        {
            elementIds.Add(new ElementId(clash.RoomRevitElementId));
        }

        if (clash.ObstructionElementId > 0)
        {
            ElementId obstructionId = new ElementId(clash.ObstructionElementId);
            if (uiDocument.Document.GetElement(obstructionId) != null)
            {
                elementIds.Add(obstructionId);
            }
        }

        ShowElements(uiDocument, elementIds);
    }

    public static void ShowRoom(UIDocument uiDocument, int roomRevitElementId)
    {
        if (uiDocument == null || roomRevitElementId <= 0)
        {
            return;
        }

        ShowElements(uiDocument, new List<ElementId> { new ElementId(roomRevitElementId) });
    }

    private static void ShowElements(UIDocument uiDocument, ICollection<ElementId> elementIds)
    {
        if (elementIds == null || elementIds.Count == 0)
        {
            return;
        }

        uiDocument.Selection.SetElementIds(elementIds);
        uiDocument.ShowElements(elementIds);
        uiDocument.RefreshActiveView();
    }
}
