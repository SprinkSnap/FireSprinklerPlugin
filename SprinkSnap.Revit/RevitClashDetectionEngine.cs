using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitClashDetectionEngine
{
    private const double ClearanceHalfSizeFeet = 0.75;

    private static readonly BuiltInCategory[] ObstructionCategories =
    {
        BuiltInCategory.OST_DuctCurves,
        BuiltInCategory.OST_FlexDuctCurves,
        BuiltInCategory.OST_PipeCurves,
        BuiltInCategory.OST_FlexPipeCurves,
        BuiltInCategory.OST_CableTray,
        BuiltInCategory.OST_Conduit,
        BuiltInCategory.OST_StructuralFraming,
        BuiltInCategory.OST_StructuralColumns,
        BuiltInCategory.OST_LightingFixtures,
        BuiltInCategory.OST_MechanicalEquipment,
        BuiltInCategory.OST_DuctAccessory,
        BuiltInCategory.OST_PipeAccessory
    };

    public static ClashDetectionSummary Detect(Document document, IEnumerable<RoomInfo> rooms)
    {
        ClashDetectionSummary summary = new ClashDetectionSummary();
        if (document == null)
        {
            summary.Messages.Add("No Revit document is available for geometry clash detection.");
            return summary;
        }

        ElementCategoryFilter categoryFilter = BuildObstructionCategoryFilter();
        HashSet<string> dedupeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (RoomInfo room in rooms ?? Array.Empty<RoomInfo>())
        {
            if (room.ProposedSprinklers.Count == 0)
            {
                continue;
            }

            Room revitRoom = document.GetElement(new ElementId(room.RevitElementId)) as Room;
            BoundingBoxXYZ roomBounds = revitRoom?.get_BoundingBox(null);

            foreach (SprinklerPlacementCandidate candidate in room.ProposedSprinklers)
            {
                IList<Element> obstructions = FindObstructions(
                    document,
                    room,
                    candidate,
                    roomBounds,
                    categoryFilter);

                if (obstructions.Count == 0 && room.HasCriticalGeometry)
                {
                    AddClash(summary, dedupeKeys, room, candidate, null, "Irregular Geometry",
                        "Irregular room geometry may obstruct discharge pattern development.");
                    continue;
                }

                foreach (Element obstruction in obstructions)
                {
                    AddClash(summary, dedupeKeys, room, candidate, obstruction,
                        DescribeCategory(obstruction),
                        DescribeElement(obstruction));
                }
            }
        }

        FinalizeSummary(summary, "Revit geometry");
        return summary;
    }

    private static IList<Element> FindObstructions(
        Document document,
        RoomInfo room,
        SprinklerPlacementCandidate candidate,
        BoundingBoxXYZ roomBounds,
        ElementCategoryFilter categoryFilter)
    {
        XYZ point = new XYZ(candidate.Location.X, candidate.Location.Y, candidate.Location.Z);
        Outline clearanceOutline = new Outline(
            new XYZ(point.X - ClearanceHalfSizeFeet, point.Y - ClearanceHalfSizeFeet, point.Z - ClearanceHalfSizeFeet),
            new XYZ(point.X + ClearanceHalfSizeFeet, point.Y + ClearanceHalfSizeFeet, point.Z + ClearanceHalfSizeFeet));

        IList<ElementFilter> filters = new List<ElementFilter>
        {
            new BoundingBoxIntersectsFilter(clearanceOutline),
            categoryFilter
        };

        if (roomBounds != null)
        {
            filters.Add(new BoundingBoxIntersectsFilter(new Outline(roomBounds.Min, roomBounds.Max)));
        }

        LogicalAndFilter compoundFilter = new LogicalAndFilter(filters);

        return new FilteredElementCollector(document)
            .WhereElementIsNotElementType()
            .WherePasses(compoundFilter)
            .ToElements()
            .Where(element => element.Id.IntegerValue != room.RevitElementId)
            .Where(element => !IsSprinkler(element))
            .Where(element => IsNearHeadLocation(element, point))
            .ToList();
    }

    private static bool IsNearHeadLocation(Element element, XYZ point)
    {
        BoundingBoxXYZ bounds = element.get_BoundingBox(null);
        if (bounds == null)
        {
            return true;
        }

        return bounds.Min.X - ClearanceHalfSizeFeet <= point.X
            && bounds.Max.X + ClearanceHalfSizeFeet >= point.X
            && bounds.Min.Y - ClearanceHalfSizeFeet <= point.Y
            && bounds.Max.Y + ClearanceHalfSizeFeet >= point.Y
            && bounds.Min.Z - ClearanceHalfSizeFeet <= point.Z
            && bounds.Max.Z + ClearanceHalfSizeFeet >= point.Z;
    }

    private static void AddClash(
        ClashDetectionSummary summary,
        HashSet<string> dedupeKeys,
        RoomInfo room,
        SprinklerPlacementCandidate candidate,
        Element obstruction,
        string clashType,
        string description)
    {
        string candidateId = candidate.CandidateType + "@" + candidate.Location.X.ToString("F1") + "," + candidate.Location.Y.ToString("F1");
        int obstructionId = obstruction?.Id.IntegerValue ?? 0;
        string dedupeKey = room.RevitElementId + "|" + candidateId + "|" + obstructionId + "|" + clashType;
        if (!dedupeKeys.Add(dedupeKey))
        {
            return;
        }

        summary.Clashes.Add(new SprinklerClashRecord
        {
            RoomRevitElementId = room.RevitElementId,
            RoomNumber = room.Number,
            RoomName = room.Name,
            CandidateId = candidateId,
            Location = candidate.Location,
            ClashType = clashType,
            ObstructionDescription = description,
            ObstructionElementId = obstructionId,
            ObstructionCategory = obstruction?.Category?.Name ?? clashType,
            DetectionSource = "Revit Geometry",
            Resolved = false
        });
    }

    private static ElementCategoryFilter BuildObstructionCategoryFilter()
    {
        IList<ElementFilter> categoryFilters = ObstructionCategories
            .Select(category => new ElementCategoryFilter(category) as ElementFilter)
            .ToList();

        return new LogicalOrFilter(categoryFilters);
    }

    private static bool IsSprinkler(Element element)
    {
        return element.Category != null
            && element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Sprinklers;
    }

    private static string DescribeCategory(Element element)
    {
        return element?.Category?.Name ?? "Obstruction";
    }

    private static string DescribeElement(Element element)
    {
        if (element == null)
        {
            return string.Empty;
        }

        if (element is FamilyInstance familyInstance)
        {
            return familyInstance.Symbol.FamilyName
                + " : "
                + familyInstance.Symbol.Name
                + " (Id "
                + element.Id.IntegerValue
                + ")";
        }

        return element.Name + " (Id " + element.Id.IntegerValue + ")";
    }

    internal static void FinalizeSummary(ClashDetectionSummary summary, string sourceLabel)
    {
        summary.TotalClashes = summary.Clashes.Count;
        summary.UnresolvedClashes = summary.Clashes.Count(record => !record.Resolved);
        summary.ResolvedClashes = summary.Clashes.Count(record => record.Resolved);

        if (summary.TotalClashes == 0)
        {
            summary.Messages.Add("No clashes detected using " + sourceLabel + " in the host model.");
        }
        else
        {
            summary.Messages.Add(summary.TotalClashes + " clash(es) found using " + sourceLabel + ". Review linked models separately if needed.");
        }
    }
}
