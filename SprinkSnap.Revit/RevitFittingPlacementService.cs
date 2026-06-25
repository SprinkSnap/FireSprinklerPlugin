using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitFittingPlacementService
{
    public static void PlaceRoomJoints(
        Document document,
        RoomInfo room,
        IList<PipeJoint> joints,
        Level level,
        PipePlacementRoomResult result)
    {
        if (document == null || joints == null || joints.Count == 0 || level == null || result == null)
        {
            return;
        }

        PipeJoint first = joints.First();
        int removedExisting = RemoveSprinkSnapPlacedFittings(document, room, first.RoomNumber);
        if (removedExisting > 0)
        {
            result.Message = AppendMessage(result.Message, "Removed " + removedExisting + " previous SprinkSnap fitting(s) before re-placing.");
        }

        foreach (PipeJoint joint in joints)
        {
            FamilySymbol symbol = RevitFittingTypeResolver.ResolveFitting(document, joint.JointType, joint.DiameterInches);
            if (symbol == null)
            {
                result.SkippedFittingCount++;
                result.Message = AppendMessage(
                    result.Message,
                    "No Revit family found for " + joint.JointType + " (" + joint.DiameterInches.ToString("0.##") + "\").");
                continue;
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                document.Regenerate();
            }

            try
            {
                XYZ location = ToXyz(joint.Location);
                FamilyInstance instance = document.Create.NewFamilyInstance(
                    location,
                    symbol,
                    level,
                    StructuralType.NonStructural);
                TagFittingInstance(instance, joint, room);
                result.PlacedFittingElementIds.Add(instance.Id.IntegerValue);
                result.PlacedFittingCount++;
            }
            catch (Exception ex)
            {
                result.SkippedFittingCount++;
                result.Message = AppendMessage(result.Message, ex.Message);
            }
        }
    }

    private static void TagFittingInstance(FamilyInstance instance, PipeJoint joint, RoomInfo room)
    {
        string roomNumber = room?.Number ?? joint.RoomNumber;
        SetParameter(instance, "Comments", "SprinkSnap Schematic Fitting | Room " + roomNumber + " | " + joint.JointType);
        SetParameter(instance, "Mark", roomNumber + "-" + joint.JointType.ToUpperInvariant());
        SetParameter(instance, "SS_RoomNumber", roomNumber);
        SetParameter(instance, "SS_SegmentType", joint.JointType);
        SetParameter(instance, "SS_PlacementBasis", joint.Description);
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

    private static XYZ ToXyz(Point3D point)
    {
        return new XYZ(point.X, point.Y, point.Z);
    }

    private static string AppendMessage(string existing, string addition)
    {
        return string.IsNullOrWhiteSpace(existing) ? addition : existing + "; " + addition;
    }

    private static int RemoveSprinkSnapPlacedFittings(Document document, RoomInfo room, string roomNumber)
    {
        BoundingBoxXYZ bounds = ResolveRoomBounds(document, room);
        if (bounds == null)
        {
            return 0;
        }

        int removed = 0;
        IList<Element> fittings = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeFitting)
            .WhereElementIsNotElementType()
            .WherePasses(new BoundingBoxIntersectsFilter(new Outline(bounds.Min, bounds.Max)))
            .ToElements();

        IList<Element> accessories = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeAccessory)
            .WhereElementIsNotElementType()
            .WherePasses(new BoundingBoxIntersectsFilter(new Outline(bounds.Min, bounds.Max)))
            .ToElements();

        foreach (Element element in fittings.Concat(accessories))
        {
            if (!IsSprinkSnapFittingForRoom(element, roomNumber))
            {
                continue;
            }

            document.Delete(element.Id);
            removed++;
        }

        return removed;
    }

    private static BoundingBoxXYZ ResolveRoomBounds(Document document, RoomInfo room)
    {
        if (room?.RevitElementId > 0)
        {
            Room revitRoom = document.GetElement(new ElementId(room.RevitElementId)) as Room;
            BoundingBoxXYZ roomBounds = revitRoom?.get_BoundingBox(null);
            if (roomBounds != null)
            {
                return roomBounds;
            }
        }

        return null;
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
