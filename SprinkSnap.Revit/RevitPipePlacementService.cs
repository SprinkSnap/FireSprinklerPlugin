using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Plumbing;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitPipePlacementService
{
    private const double MinimumSegmentLengthFeet = 0.1;

    public static PipePlacementSummary Place(
        Document document,
        IEnumerable<RoomInfo> rooms,
        SchematicPipeRoutingSummary schematicRouting)
    {
        PipePlacementSummary summary = new PipePlacementSummary();
        if (document == null)
        {
            summary.Messages.Add("No Revit document is available for pipe placement.");
            return summary;
        }

        List<PipeSegment> segments = schematicRouting?.Segments?.ToList() ?? new List<PipeSegment>();
        summary.TotalSegments = segments.Count;
        if (segments.Count == 0)
        {
            summary.Messages.Add("Generate schematic pipe routing in Generate Design before placing pipes in Revit.");
            return summary;
        }

        PipingSystemType systemType = RevitPipeTypeResolver.ResolveSystemType(document);
        if (systemType == null)
        {
            summary.Messages.Add("No piping system type was found in the Revit document.");
            return summary;
        }

        Dictionary<int, RoomInfo> roomsById = (rooms ?? Enumerable.Empty<RoomInfo>())
            .Where(room => room.RevitElementId > 0)
            .GroupBy(room => room.RevitElementId)
            .ToDictionary(group => group.Key, group => group.First());

        using (Transaction transaction = new Transaction(document, "SprinkSnap Place Schematic Pipes"))
        {
            transaction.Start();

            foreach (IGrouping<int, PipeSegment> roomGroup in segments.GroupBy(segment => segment.RoomRevitElementId))
            {
                roomsById.TryGetValue(roomGroup.Key, out RoomInfo room);
                PipePlacementRoomResult roomResult = PlaceRoomSegments(
                    document,
                    room,
                    roomGroup.ToList(),
                    systemType);
                summary.RoomResults.Add(roomResult);
                summary.PlacedSegmentCount += roomResult.PlacedSegmentCount;
                summary.SkippedSegmentCount += roomResult.SkippedSegmentCount;
                summary.PlacedLengthFeet += roomResult.PlacedLengthFeet;
            }

            transaction.Commit();
        }

        summary.FailedSegmentCount = summary.TotalSegments - summary.PlacedSegmentCount - summary.SkippedSegmentCount;
        if (summary.PlacedSegmentCount == 0)
        {
            summary.Messages.Add("No schematic pipes were placed. Verify routing, loaded pipe types, and room levels in Revit.");
        }
        else
        {
            summary.Messages.Add(
                "Placed "
                + summary.PlacedSegmentCount
                + " schematic pipe segment(s) totaling "
                + summary.PlacedLengthFeet.ToString("N0")
                + " ft across "
                + summary.RoomResults.Count(result => result.PlacedSegmentCount > 0)
                + " room(s).");
        }

        return summary;
    }

    private static PipePlacementRoomResult PlaceRoomSegments(
        Document document,
        RoomInfo room,
        IList<PipeSegment> segments,
        PipingSystemType systemType)
    {
        PipeSegment first = segments.First();
        PipePlacementRoomResult result = new PipePlacementRoomResult
        {
            RoomRevitElementId = first.RoomRevitElementId,
            RoomNumber = first.RoomNumber,
            RoomName = first.RoomName
        };

        if (room != null && room.RequiresExceptionReview)
        {
            result.Status = "Skipped";
            result.Message = "Room requires designer exception review before pipe placement.";
            result.SkippedSegmentCount = segments.Count;
            return result;
        }

        Level level = ResolveLevel(document, room, first);
        if (level == null)
        {
            result.Status = "Failed";
            result.Message = "Could not resolve a Revit level for schematic pipe placement.";
            result.SkippedSegmentCount = segments.Count;
            return result;
        }

        int removedExisting = RemoveSprinkSnapPlacedPipes(document, room, first.RoomNumber);
        if (removedExisting > 0)
        {
            result.Message = "Removed " + removedExisting + " previous SprinkSnap pipe segment(s) before re-placing.";
        }

        foreach (PipeSegment segment in segments)
        {
            if (segment.LengthFeet < MinimumSegmentLengthFeet)
            {
                result.SkippedSegmentCount++;
                continue;
            }

            PipeType pipeType = RevitPipeTypeResolver.ResolvePipeType(document, segment.DiameterInches);
            if (pipeType == null)
            {
                result.SkippedSegmentCount++;
                result.Message = AppendMessage(result.Message, "No pipe type found for " + segment.DiameterInches.ToString("0.##") + "\" diameter.");
                continue;
            }

            try
            {
                XYZ start = ToXyz(segment.Start);
                XYZ end = ToXyz(segment.End);
                Pipe pipe = Pipe.Create(document, systemType.Id, pipeType.Id, level.Id, start, end);
                TagPipeInstance(pipe, segment, room);
                result.PlacedElementIds.Add(pipe.Id.IntegerValue);
                result.PlacedSegmentCount++;
                result.PlacedLengthFeet += segment.LengthFeet;
            }
            catch (Exception ex)
            {
                result.SkippedSegmentCount++;
                result.Message = AppendMessage(result.Message, ex.Message);
            }
        }

        result.Status = result.PlacedSegmentCount > 0 ? "Placed" : "Failed";
        if (string.IsNullOrWhiteSpace(result.Message) && result.PlacedSegmentCount > 0)
        {
            result.Message = "Placed " + result.PlacedSegmentCount + " schematic pipe segment(s).";
        }

        return result;
    }

    private static Level ResolveLevel(Document document, RoomInfo room, PipeSegment segment)
    {
        if (room != null && room.LevelId > 0)
        {
            Level level = document.GetElement(new ElementId(room.LevelId)) as Level;
            if (level != null)
            {
                return level;
            }
        }

        if (!string.IsNullOrWhiteSpace(segment.LevelName))
        {
            Level namedLevel = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(level => string.Equals(level.Name, segment.LevelName, StringComparison.OrdinalIgnoreCase));
            if (namedLevel != null)
            {
                return namedLevel;
            }
        }

        return new FilteredElementCollector(document)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(level => Math.Abs(level.Elevation - segment.Start.Z))
            .FirstOrDefault();
    }

    private static void TagPipeInstance(Pipe pipe, PipeSegment segment, RoomInfo room)
    {
        string roomNumber = room?.Number ?? segment.RoomNumber;
        SetParameter(pipe, "Comments", "SprinkSnap Schematic Pipe | Room " + roomNumber + " | " + segment.SegmentType);
        SetParameter(pipe, "Mark", roomNumber + "-PIPE");
        SetParameter(pipe, "SS_RoomNumber", roomNumber);
        SetParameter(pipe, "SS_SegmentType", segment.SegmentType);
        SetParameter(pipe, "SS_PlacementBasis", segment.Description);
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

    private static int RemoveSprinkSnapPlacedPipes(Document document, RoomInfo room, string roomNumber)
    {
        BoundingBoxXYZ bounds = ResolveRoomBounds(document, room);
        if (bounds == null)
        {
            return 0;
        }

        int removed = 0;
        IList<Element> pipes = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeCurves)
            .WhereElementIsNotElementType()
            .WherePasses(new BoundingBoxIntersectsFilter(new Outline(bounds.Min, bounds.Max)))
            .ToElements();

        foreach (Element element in pipes)
        {
            if (!IsSprinkSnapPipeForRoom(element, roomNumber))
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
