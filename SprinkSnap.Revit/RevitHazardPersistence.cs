using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitHazardPersistence
{
    public static int SaveApprovedHazards(Document document, IEnumerable<RoomInfo> rooms)
    {
        IHazardClassificationParameterStorage parameterStorage = new HazardClassificationParameterStorage();
        Dictionary<ElementId, string> approvedClassifications = rooms
            .Where(room => room.DesignerApproved && !string.IsNullOrWhiteSpace(room.ApprovedHazardClassification))
            .ToDictionary(
                room => new ElementId(room.RevitElementId),
                room => room.ApprovedHazardClassification);

        if (approvedClassifications.Count == 0)
        {
            return 0;
        }

        using (Transaction transaction = new Transaction(document, "Save SprinkSnap Hazard Classifications"))
        {
            transaction.Start();
            parameterStorage.EnsureRoomParameterBinding(document);
            document.Regenerate();
            parameterStorage.Write(document, approvedClassifications);
            transaction.Commit();
        }

        return approvedClassifications.Count;
    }
}
