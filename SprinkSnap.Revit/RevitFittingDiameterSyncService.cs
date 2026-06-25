using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitFittingDiameterSyncService
{
    public static PlacedFittingDiameterSyncSummary Sync(Document document, PlacedFittingDiameterSyncPlan plan)
    {
        PlacedFittingDiameterSyncSummary summary = new PlacedFittingDiameterSyncSummary
        {
            SkippedCount = plan?.SkippedCount ?? 0
        };

        if (document == null)
        {
            summary.Messages.Add("Revit document was unavailable for fitting diameter sync.");
            return summary;
        }

        if (plan?.Targets == null || plan.Targets.Count == 0)
        {
            foreach (string message in plan?.Messages ?? Array.Empty<string>())
            {
                summary.Messages.Add(message);
            }

            return summary;
        }

        using (Transaction transaction = new Transaction(document, "SprinkSnap Sync Fitting Diameters"))
        {
            transaction.Start();
            foreach (PlacedFittingDiameterSyncTarget target in plan.Targets)
            {
                try
                {
                    if (TrySyncTarget(document, target))
                    {
                        summary.UpdatedCount++;
                    }
                    else
                    {
                        summary.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    summary.FailedCount++;
                    summary.Messages.Add(
                        "Fitting "
                        + target.PlacedElementId
                        + ": "
                        + ex.Message);
                }
            }

            transaction.Commit();
        }

        summary.UsesRevitFittingDiameterSync = summary.UpdatedCount > 0;
        if (summary.UpdatedCount > 0)
        {
            summary.Messages.Add(
                "Updated "
                + summary.UpdatedCount
                + " placed Revit fitting(s) to match velocity-sized schematic diameters.");
        }

        foreach (string message in plan.Messages)
        {
            if (!summary.Messages.Contains(message))
            {
                summary.Messages.Add(message);
            }
        }

        return summary;
    }

    private static bool TrySyncTarget(Document document, PlacedFittingDiameterSyncTarget target)
    {
        if (target.PlacedElementId <= 0 || target.TargetDiameterInches <= 0)
        {
            return false;
        }

        Element element = document.GetElement(new ElementId(target.PlacedElementId));
        if (element == null || !IsSprinkSnapFittingForRoom(element, target.RoomNumber))
        {
            return false;
        }

        FamilyInstance instance = element as FamilyInstance;
        if (instance == null)
        {
            return false;
        }

        double currentDiameterInches = ReadFittingDiameterInches(instance);
        if (currentDiameterInches > 0 && target.TargetDiameterInches <= currentDiameterInches + 0.01)
        {
            return false;
        }

        FamilySymbol symbol = RevitFittingTypeResolver.ResolveFitting(
            document,
            target.JointType,
            target.TargetDiameterInches);
        if (symbol == null)
        {
            return false;
        }

        if (!symbol.IsActive)
        {
            symbol.Activate();
            document.Regenerate();
        }

        if (instance.GetTypeId() != symbol.Id)
        {
            instance.ChangeTypeId(symbol.Id);
        }

        UpdateFittingTags(instance, target);
        return true;
    }

    private static void UpdateFittingTags(Element element, PlacedFittingDiameterSyncTarget target)
    {
        string diameterText = target.TargetDiameterInches.ToString("0.##");
        string description = string.IsNullOrWhiteSpace(target.UpdatedDescription)
            ? diameterText + "\" " + target.JointType
            : target.UpdatedDescription;

        SetParameter(element, "SS_PlacementBasis", description);
        SetParameter(
            element,
            "Comments",
            "SprinkSnap Schematic Fitting | Room "
            + target.RoomNumber
            + " | "
            + target.JointType
            + " | "
            + diameterText
            + "\"");
    }

    private static void SetParameter(Element element, string parameterName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Parameter parameter = element.LookupParameter(parameterName);
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
        {
            return;
        }

        parameter.Set(value);
    }

    private static double ReadFittingDiameterInches(FamilyInstance instance)
    {
        Parameter parameter = instance.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
            ?? instance.LookupParameter("Diameter")
            ?? instance.Symbol?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        return parameter?.AsDouble() ?? 0.0;
    }

    private static bool IsSprinkSnapFittingForRoom(Element element, string roomNumber)
    {
        Parameter roomParameter = element.LookupParameter("SS_RoomNumber");
        string ssRoom = roomParameter?.AsString() ?? roomParameter?.AsValueString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(roomNumber)
            && string.Equals(ssRoom, roomNumber, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Parameter comments = element.LookupParameter("Comments");
        string commentValue = comments?.AsString() ?? comments?.AsValueString() ?? string.Empty;
        return commentValue.IndexOf("SprinkSnap Schematic Fitting", StringComparison.OrdinalIgnoreCase) >= 0
            && (string.IsNullOrWhiteSpace(roomNumber)
                || commentValue.IndexOf("Room " + roomNumber, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
