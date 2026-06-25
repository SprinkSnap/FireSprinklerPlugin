using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitPipeDiameterSyncService
{
    public static PlacedPipeDiameterSyncSummary Sync(Document document, PlacedPipeDiameterSyncPlan plan)
    {
        PlacedPipeDiameterSyncSummary summary = new PlacedPipeDiameterSyncSummary
        {
            SkippedCount = plan?.SkippedCount ?? 0
        };

        if (document == null)
        {
            summary.Messages.Add("Revit document was unavailable for pipe diameter sync.");
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

        using (Transaction transaction = new Transaction(document, "SprinkSnap Sync Pipe Diameters"))
        {
            transaction.Start();
            foreach (PlacedPipeDiameterSyncTarget target in plan.Targets)
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
                        "Element "
                        + target.PlacedElementId
                        + ": "
                        + ex.Message);
                }
            }

            transaction.Commit();
        }

        summary.UsesRevitPipeDiameterSync = summary.UpdatedCount > 0;
        if (summary.UpdatedCount > 0)
        {
            summary.Messages.Add(
                "Updated "
                + summary.UpdatedCount
                + " placed Revit pipe(s) to match velocity-sized schematic diameters.");
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

    private static bool TrySyncTarget(Document document, PlacedPipeDiameterSyncTarget target)
    {
        if (target.PlacedElementId <= 0 || target.TargetDiameterInches <= 0)
        {
            return false;
        }

        Pipe pipe = document.GetElement(new ElementId(target.PlacedElementId)) as Pipe;
        if (pipe == null)
        {
            return false;
        }

        if (!IsSprinkSnapPipeForRoom(pipe, target.RoomNumber))
        {
            return false;
        }

        double currentDiameterInches = GetPipeDiameterInches(pipe);
        if (currentDiameterInches > 0 && target.TargetDiameterInches <= currentDiameterInches + 0.01)
        {
            return false;
        }

        PipeType pipeType = RevitPipeTypeResolver.ResolvePipeType(document, target.TargetDiameterInches);
        if (pipeType == null)
        {
            return false;
        }

        if (pipe.GetTypeId() != pipeType.Id)
        {
            pipe.ChangeTypeId(pipeType.Id);
        }

        UpdatePlacementBasis(pipe, target.UpdatedDescription);
        return true;
    }

    private static void UpdatePlacementBasis(Element element, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        Parameter parameter = element.LookupParameter("SS_PlacementBasis");
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
        {
            return;
        }

        parameter.Set(description);
    }

    private static double GetPipeDiameterInches(Pipe pipe)
    {
        Parameter parameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        return parameter?.AsDouble() ?? 0.0;
    }

    private static bool IsSprinkSnapPipeForRoom(Element element, string roomNumber)
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
        return commentValue.IndexOf("SprinkSnap Schematic Pipe", StringComparison.OrdinalIgnoreCase) >= 0
            && (string.IsNullOrWhiteSpace(roomNumber)
                || commentValue.IndexOf("Room " + roomNumber, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
