using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitSprinklerPlacementService
{
    public static SprinklerPlacementSummary Place(
        Document document,
        IEnumerable<RoomInfo> rooms,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        SprinklerPlacementSummary summary = new SprinklerPlacementSummary();
        if (document == null)
        {
            summary.Messages.Add("No Revit document is available for placement.");
            return summary;
        }

        List<RoomInfo> roomList = rooms?.ToList() ?? new List<RoomInfo>();
        List<SprinklerFamilyInfo> familyCatalog = catalog?.ToList() ?? new List<SprinklerFamilyInfo>();
        summary.TotalCandidates = roomList.Sum(room => room.ProposedSprinklers.Count);

        using (Transaction transaction = new Transaction(document, "SprinkSnap Place Sprinklers"))
        {
            transaction.Start();

            foreach (RoomInfo room in roomList)
            {
                SprinklerPlacementRoomResult roomResult = PlaceRoom(document, room, familyCatalog);
                summary.RoomResults.Add(roomResult);
                summary.PlacedCount += roomResult.PlacedCount;
                summary.SkippedRoomCount += roomResult.PlacedCount == 0 && room.ProposedSprinklers.Count > 0 ? 1 : 0;
                summary.FailedCount += roomResult.SkippedCount;
            }

            transaction.Commit();
        }

        if (summary.PlacedCount == 0)
        {
            summary.Messages.Add("No sprinklers were placed. Verify layout, sprinkler families loaded in Revit, and clash resolution.");
        }
        else
        {
            summary.Messages.Add(
                "Placed "
                + summary.PlacedCount
                + " sprinkler(s) across "
                + summary.RoomResults.Count(result => result.PlacedCount > 0)
                + " room(s).");
        }

        return summary;
    }

    private static SprinklerPlacementRoomResult PlaceRoom(
        Document document,
        RoomInfo room,
        IList<SprinklerFamilyInfo> catalog)
    {
        SprinklerPlacementRoomResult result = new SprinklerPlacementRoomResult
        {
            RoomRevitElementId = room.RevitElementId,
            RoomNumber = room.Number,
            RoomName = room.Name
        };

        if (room.RequiresExceptionReview)
        {
            result.Status = "Skipped";
            result.Message = "Room requires designer exception review before placement.";
            result.SkippedCount = room.ProposedSprinklers.Count;
            return result;
        }

        if (!room.DesignerApproved || string.IsNullOrWhiteSpace(room.ApprovedHazardClassification))
        {
            result.Status = "Skipped";
            result.Message = "Designer hazard approval is required before placement.";
            result.SkippedCount = room.ProposedSprinklers.Count;
            return result;
        }

        if (room.ProposedSprinklers.Count == 0)
        {
            result.Status = "Skipped";
            result.Message = "No proposed sprinkler locations in this room.";
            return result;
        }

        SprinklerFamilyInfo familyInfo = RevitFamilySymbolResolver.ResolveFamilyForRoom(room, catalog);
        FamilySymbol symbol = RevitFamilySymbolResolver.ResolveSymbol(document, familyInfo);
        if (symbol == null)
        {
            result.Status = "Failed";
            result.Message = "Could not resolve a loaded Revit sprinkler family for this room.";
            result.SkippedCount = room.ProposedSprinklers.Count;
            return result;
        }

        if (!symbol.IsActive)
        {
            symbol.Activate();
            document.Regenerate();
        }

        Level level = document.GetElement(new ElementId(room.LevelId)) as Level;
        if (level == null)
        {
            result.Status = "Failed";
            result.Message = "Room level could not be resolved for sprinkler placement.";
            result.SkippedCount = room.ProposedSprinklers.Count;
            return result;
        }

        foreach (SprinklerPlacementCandidate candidate in room.ProposedSprinklers)
        {
            try
            {
                XYZ location = new XYZ(candidate.Location.X, candidate.Location.Y, candidate.Location.Z);
                FamilyInstance instance = document.Create.NewFamilyInstance(
                    location,
                    symbol,
                    level,
                    StructuralType.NonStructural);

                TagSprinklerInstance(instance, room, familyInfo, candidate);
                result.PlacedElementIds.Add(instance.Id.IntegerValue);
                result.PlacedCount++;
            }
            catch (Exception ex)
            {
                result.SkippedCount++;
                result.Message = string.IsNullOrWhiteSpace(result.Message)
                    ? ex.Message
                    : result.Message + "; " + ex.Message;
            }
        }

        result.Status = result.PlacedCount > 0 ? "Placed" : "Failed";
        if (string.IsNullOrWhiteSpace(result.Message) && result.PlacedCount > 0)
        {
            result.Message = "Placed "
                + result.PlacedCount
                + " "
                + (familyInfo?.DisplayName ?? "sprinkler")
                + " head(s).";
        }

        return result;
    }

    private static void TagSprinklerInstance(
        FamilyInstance instance,
        RoomInfo room,
        SprinklerFamilyInfo familyInfo,
        SprinklerPlacementCandidate candidate)
    {
        SetParameter(instance, "Comments", "SprinkSnap Room " + room.Number + " | " + room.ApprovedHazardClassification);
        SetParameter(instance, "Mark", room.Number + "-SS");
        SetParameter(instance, "SS_RoomNumber", room.Number);
        SetParameter(instance, "SS_HazardClassification", room.ApprovedHazardClassification);

        if (familyInfo != null)
        {
            SetParameter(instance, "SS_Manufacturer", familyInfo.Manufacturer);
            SetParameter(instance, "SS_Model", familyInfo.Model);
            SetParameter(instance, "SS_SIN", familyInfo.Sin);
        }

        if (!string.IsNullOrWhiteSpace(candidate.Basis))
        {
            SetParameter(instance, "SS_PlacementBasis", candidate.Basis);
        }
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
}
