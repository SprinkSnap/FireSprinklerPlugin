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

    public static ClashDetectionSummary Detect(
        Document document,
        IEnumerable<RoomInfo> rooms,
        IEnumerable<LinkedModelScanOption> linkedModelOptions = null)
    {
        ClashDetectionSummary summary = new ClashDetectionSummary();
        if (document == null)
        {
            summary.Messages.Add("No Revit document is available for geometry clash detection.");
            return summary;
        }

        ElementCategoryFilter categoryFilter = BuildObstructionCategoryFilter();
        HashSet<string> dedupeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IList<RevitLinkInstance> linkedModelsToScan = ResolveLinkedModelsToScan(document, linkedModelOptions);
        summary.LinkedModelsScannedCount = linkedModelsToScan.Count;

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
                XYZ pointHost = new XYZ(candidate.Location.X, candidate.Location.Y, candidate.Location.Z);

                IList<Element> hostObstructions = FindObstructionsInDocument(
                    document,
                    room,
                    pointHost,
                    roomBounds,
                    categoryFilter);

                if (hostObstructions.Count == 0 && linkedModelsToScan.Count == 0 && room.HasCriticalGeometry)
                {
                    AddClash(summary, dedupeKeys, room, candidate, null, null, "Irregular Geometry",
                        "Irregular room geometry may obstruct discharge pattern development.");
                    continue;
                }

                foreach (Element obstruction in hostObstructions)
                {
                    AddClash(summary, dedupeKeys, room, candidate, null, obstruction,
                        DescribeCategory(obstruction),
                        DescribeElement(obstruction));
                }

                foreach (RevitLinkInstance linkInstance in linkedModelsToScan)
                {
                    IList<Element> linkedObstructions = FindObstructionsInLinkedModel(
                        linkInstance,
                        pointHost,
                        categoryFilter);

                    foreach (Element obstruction in linkedObstructions)
                    {
                        AddClash(summary, dedupeKeys, room, candidate, linkInstance, obstruction,
                            DescribeCategory(obstruction),
                            DescribeElement(obstruction));
                    }
                }
            }
        }

        FinalizeSummary(summary);
        return summary;
    }

    private static IList<RevitLinkInstance> ResolveLinkedModelsToScan(
        Document document,
        IEnumerable<LinkedModelScanOption> linkedModelOptions)
    {
        Dictionary<int, LinkedModelScanOption> optionById = (linkedModelOptions ?? Array.Empty<LinkedModelScanOption>())
            .Where(option => option.LinkInstanceId > 0)
            .GroupBy(option => option.LinkInstanceId)
            .ToDictionary(group => group.Key, group => group.Last());

        List<RevitLinkInstance> linksToScan = new List<RevitLinkInstance>();
        foreach (RevitLinkInstance linkInstance in new FilteredElementCollector(document)
                     .OfClass(typeof(RevitLinkInstance))
                     .Cast<RevitLinkInstance>())
        {
            if (linkInstance.GetLinkDocument() == null)
            {
                continue;
            }

            if (optionById.TryGetValue(linkInstance.Id.IntegerValue, out LinkedModelScanOption option)
                && !option.IncludeInClashScan)
            {
                continue;
            }

            linksToScan.Add(linkInstance);
        }

        return linksToScan
            .OrderBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IList<Element> FindObstructionsInDocument(
        Document document,
        RoomInfo room,
        XYZ point,
        BoundingBoxXYZ roomBounds,
        ElementCategoryFilter categoryFilter)
    {
        Outline clearanceOutline = BuildClearanceOutline(point);

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

    private static IList<Element> FindObstructionsInLinkedModel(
        RevitLinkInstance linkInstance,
        XYZ pointHost,
        ElementCategoryFilter categoryFilter)
    {
        Document linkDocument = linkInstance.GetLinkDocument();
        if (linkDocument == null)
        {
            return new List<Element>();
        }

        Transform inverseTransform = linkInstance.GetTotalTransform().Inverse;
        XYZ pointLink = inverseTransform.OfPoint(pointHost);
        Outline clearanceOutline = BuildClearanceOutline(pointLink);
        LogicalAndFilter compoundFilter = new LogicalAndFilter(
            new BoundingBoxIntersectsFilter(clearanceOutline),
            categoryFilter);

        return new FilteredElementCollector(linkDocument)
            .WhereElementIsNotElementType()
            .WherePasses(compoundFilter)
            .ToElements()
            .Where(element => !IsSprinkler(element))
            .Where(element => IsNearHeadLocation(element, pointLink))
            .ToList();
    }

    private static Outline BuildClearanceOutline(XYZ point)
    {
        return new Outline(
            new XYZ(point.X - ClearanceHalfSizeFeet, point.Y - ClearanceHalfSizeFeet, point.Z - ClearanceHalfSizeFeet),
            new XYZ(point.X + ClearanceHalfSizeFeet, point.Y + ClearanceHalfSizeFeet, point.Z + ClearanceHalfSizeFeet));
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
        RevitLinkInstance linkInstance,
        Element obstruction,
        string clashType,
        string description)
    {
        string candidateId = candidate.CandidateType + "@" + candidate.Location.X.ToString("F1") + "," + candidate.Location.Y.ToString("F1");
        int obstructionId = obstruction?.Id.IntegerValue ?? 0;
        int linkInstanceId = linkInstance?.Id.IntegerValue ?? 0;
        string dedupeKey = room.RevitElementId + "|" + candidateId + "|" + linkInstanceId + "|" + obstructionId + "|" + clashType;
        if (!dedupeKeys.Add(dedupeKey))
        {
            return;
        }

        bool isLinked = linkInstance != null;
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
            DetectionSource = isLinked ? "Linked: " + linkInstance.Name : "Host Model",
            IsLinkedModelClash = isLinked,
            LinkedModelInstanceId = linkInstanceId,
            LinkedModelName = isLinked ? linkInstance.Name : string.Empty,
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

    internal static void FinalizeSummary(ClashDetectionSummary summary)
    {
        summary.HostClashCount = summary.Clashes.Count(record => !record.IsLinkedModelClash);
        summary.LinkedClashCount = summary.Clashes.Count(record => record.IsLinkedModelClash);
        summary.TotalClashes = summary.Clashes.Count;
        summary.UnresolvedClashes = summary.Clashes.Count(record => !record.Resolved);
        summary.ResolvedClashes = summary.Clashes.Count(record => record.Resolved);

        if (summary.TotalClashes == 0)
        {
            if (summary.LinkedModelsScannedCount > 0)
            {
                summary.Messages.Add(
                    "No clashes detected in the host model or "
                    + summary.LinkedModelsScannedCount
                    + " linked model(s).");
            }
            else
            {
                summary.Messages.Add("No clashes detected in the host model. Enable linked models in Settings to scan MEP coordination links.");
            }

            return;
        }

        summary.Messages.Add(
            summary.TotalClashes
            + " clash(es) found ("
            + summary.HostClashCount
            + " host, "
            + summary.LinkedClashCount
            + " linked across "
            + summary.LinkedModelsScannedCount
            + " linked model(s)).");
    }
}
