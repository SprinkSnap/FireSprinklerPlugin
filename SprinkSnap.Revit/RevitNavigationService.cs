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

        Document document = uiDocument.Document;
        if (clash.IsLinkedModelClash
            && clash.LinkedModelInstanceId > 0
            && clash.ObstructionElementId > 0)
        {
            RevitLinkInstance linkInstance = document.GetElement(new ElementId(clash.LinkedModelInstanceId)) as RevitLinkInstance;
            Document linkDocument = linkInstance?.GetLinkDocument();
            Element linkedElement = linkDocument?.GetElement(new ElementId(clash.ObstructionElementId));
            if (linkInstance != null && linkedElement != null)
            {
                try
                {
                    Reference linkReference = new Reference(linkedElement).CreateLinkReference(linkInstance);
                    uiDocument.ShowElements(linkReference);
                    uiDocument.Selection.SetReferences(new List<Reference> { linkReference });
                    uiDocument.RefreshActiveView();
                    return;
                }
                catch
                {
                    ShowElements(uiDocument, new List<ElementId> { linkInstance.Id });
                    return;
                }
            }
        }

        List<ElementId> elementIds = new List<ElementId>();
        if (clash.RoomRevitElementId > 0)
        {
            elementIds.Add(new ElementId(clash.RoomRevitElementId));
        }

        if (clash.ObstructionElementId > 0)
        {
            ElementId obstructionId = new ElementId(clash.ObstructionElementId);
            if (document.GetElement(obstructionId) != null)
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
