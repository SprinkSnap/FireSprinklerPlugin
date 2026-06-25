using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitPipeMeasurementService
{
    public static PipePlacementSummary Measure(
        Document document,
        IEnumerable<RoomInfo> rooms,
        SchematicPipeRoutingSummary schematicRouting = null)
    {
        PipePlacementSummary summary = new PipePlacementSummary();
        if (document == null)
        {
            summary.Messages.Add("No Revit document is available to measure placed pipes.");
            return summary;
        }

        List<PipeSegment> schematicSegments = schematicRouting?.Segments?.ToList() ?? new List<PipeSegment>();
        summary.TotalSegments = schematicSegments.Count;

        Dictionary<string, RoomInfo> roomsByNumber = (rooms ?? Enumerable.Empty<RoomInfo>())
            .Where(room => !string.IsNullOrWhiteSpace(room.Number))
            .GroupBy(room => room.Number, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, PipePlacementRoomResult> roomResults =
            new Dictionary<string, PipePlacementRoomResult>(StringComparer.OrdinalIgnoreCase);

        IList<Element> pipes = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeCurves)
            .WhereElementIsNotElementType()
            .ToElements();

        foreach (Element element in pipes)
        {
            if (!TryReadSprinkSnapPipe(element, out string roomNumber, out string segmentType))
            {
                continue;
            }

            Pipe pipe = element as Pipe;
            if (pipe == null)
            {
                continue;
            }

            double lengthFeet = ReadPipeLengthFeet(pipe);
            if (lengthFeet <= 0)
            {
                continue;
            }

            bool hasTopology = TryReadPipeEndpoints(pipe, out Point3D start, out Point3D end);
            PipePlacementRoomResult roomResult = GetOrCreateRoomResult(
                roomResults,
                roomsByNumber,
                roomNumber);
            roomResult.PlacedSegments.Add(new PipePlacementSegmentResult
            {
                SegmentType = segmentType,
                DiameterInches = ReadPipeDiameterInches(pipe),
                LengthFeet = lengthFeet,
                PlacedElementId = pipe.Id.IntegerValue,
                Description = ReadParameterValue(pipe, "SS_PlacementBasis"),
                Start = start,
                End = end,
                HasTopology = hasTopology
            });
            roomResult.PlacedElementIds.Add(pipe.Id.IntegerValue);
            roomResult.PlacedSegmentCount++;
            roomResult.PlacedLengthFeet += lengthFeet;
        }

        IList<Element> fittings = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeFitting)
            .WhereElementIsNotElementType()
            .ToElements();
        IList<Element> accessories = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_PipeAccessory)
            .WhereElementIsNotElementType()
            .ToElements();

        foreach (Element element in fittings.Concat(accessories))
        {
            if (!TryReadSprinkSnapFitting(element, out string roomNumber, out string jointType, out double diameterInches))
            {
                continue;
            }

            PipePlacementRoomResult roomResult = GetOrCreateRoomResult(
                roomResults,
                roomsByNumber,
                roomNumber);
            roomResult.PlacedFittingElementIds.Add(element.Id.IntegerValue);
            roomResult.PlacedFittings.Add(new PipePlacementFittingResult
            {
                JointType = jointType,
                DiameterInches = diameterInches,
                PlacedElementId = element.Id.IntegerValue,
                Description = ReadParameterValue(element, "SS_PlacementBasis")
            });
            roomResult.PlacedFittingCount++;
        }

        foreach (PipePlacementRoomResult roomResult in roomResults.Values.OrderBy(result => result.RoomNumber))
        {
            roomResult.Status = roomResult.PlacedSegmentCount > 0 ? "Measured" : "No Pipes";
            if (roomResult.PlacedSegmentCount > 0)
            {
                roomResult.Message = "Measured "
                    + roomResult.PlacedSegmentCount
                    + " SprinkSnap pipe segment(s) totaling "
                    + roomResult.PlacedLengthFeet.ToString("N1")
                    + " ft from Revit geometry.";
            }

            summary.RoomResults.Add(roomResult);
            summary.PlacedSegmentCount += roomResult.PlacedSegmentCount;
            summary.PlacedLengthFeet += roomResult.PlacedLengthFeet;
            summary.PlacedFittingCount += roomResult.PlacedFittingCount;
        }

        if (summary.PlacedSegmentCount == 0)
        {
            summary.Messages.Add(
                "No SprinkSnap-tagged pipes were found in the Revit model. Place schematic pipes first or verify SS_RoomNumber / Comments tags.");
            return summary;
        }

        summary.Messages.Add(
            "Re-measured "
            + summary.PlacedSegmentCount
            + " SprinkSnap pipe segment(s) totaling "
            + summary.PlacedLengthFeet.ToString("N1")
            + " ft across "
            + summary.RoomResults.Count(result => result.PlacedSegmentCount > 0)
            + " room(s) from current Revit geometry.");

        if (summary.PlacedFittingCount > 0)
        {
            summary.Messages.Add(
                "Counted "
                + summary.PlacedFittingCount
                + " SprinkSnap fitting(s) and valve(s) in the model.");
        }

        return summary;
    }

    private static PipePlacementRoomResult GetOrCreateRoomResult(
        Dictionary<string, PipePlacementRoomResult> roomResults,
        Dictionary<string, RoomInfo> roomsByNumber,
        string roomNumber)
    {
        if (roomResults.TryGetValue(roomNumber, out PipePlacementRoomResult existing))
        {
            return existing;
        }

        roomsByNumber.TryGetValue(roomNumber, out RoomInfo room);
        PipePlacementRoomResult created = new PipePlacementRoomResult
        {
            RoomRevitElementId = room?.RevitElementId ?? 0,
            RoomNumber = roomNumber,
            RoomName = room?.Name ?? string.Empty
        };
        roomResults[roomNumber] = created;
        return created;
    }

    private static bool TryReadSprinkSnapPipe(Element element, out string roomNumber, out string segmentType)
    {
        roomNumber = ReadParameterValue(element, "SS_RoomNumber");
        segmentType = ReadParameterValue(element, "SS_SegmentType");

        string comments = ReadParameterValue(element, "Comments");
        if (!comments.Contains("SprinkSnap Schematic Pipe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            roomNumber = TryParseRoomNumberFromComments(comments);
        }

        if (string.IsNullOrWhiteSpace(segmentType))
        {
            segmentType = TryParseSegmentTypeFromComments(comments);
        }

        return !string.IsNullOrWhiteSpace(roomNumber);
    }

    private static bool TryReadSprinkSnapFitting(
        Element element,
        out string roomNumber,
        out string jointType,
        out double diameterInches)
    {
        roomNumber = ReadParameterValue(element, "SS_RoomNumber");
        jointType = ReadParameterValue(element, "SS_SegmentType");
        diameterInches = 0.0;

        string comments = ReadParameterValue(element, "Comments");
        if (!comments.Contains("SprinkSnap Schematic Fitting", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            roomNumber = TryParseRoomNumberFromComments(comments);
        }

        if (string.IsNullOrWhiteSpace(jointType))
        {
            jointType = TryParseSegmentTypeFromComments(comments);
        }

        if (!TryParseDiameterFromComments(comments, out diameterInches))
        {
            diameterInches = 0.0;
        }

        return !string.IsNullOrWhiteSpace(roomNumber) && !string.IsNullOrWhiteSpace(jointType);
    }

    private static string TryParseRoomNumberFromComments(string comments)
    {
        const string marker = "Room ";
        int start = comments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        int end = comments.IndexOf('|', start);
        string roomNumber = end >= 0 ? comments.Substring(start, end - start) : comments.Substring(start);
        return roomNumber.Trim();
    }

    private static string TryParseSegmentTypeFromComments(string comments)
    {
        string[] parts = comments.Split('|');
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        for (int index = parts.Length - 1; index >= 2; index--)
        {
            string token = parts[index].Trim();
            if (string.Equals(token, "connected", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryParseDiameterToken(token, out _))
            {
                return parts[index - 1].Trim();
            }
        }

        if (parts.Length >= 4 && TryParseDiameterToken(parts[parts.Length - 1], out _))
        {
            return parts[parts.Length - 2].Trim();
        }

        return parts[parts.Length - 1].Trim();
    }

    private static bool TryParseDiameterFromComments(string comments, out double diameterInches)
    {
        diameterInches = 0.0;
        string[] parts = comments.Split('|');
        if (parts.Length < 4)
        {
            return false;
        }

        for (int index = parts.Length - 1; index >= 2; index--)
        {
            if (TryParseDiameterToken(parts[index], out diameterInches))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDiameterToken(string token, out double diameterInches)
    {
        string cleaned = token.Trim().TrimEnd('"').Trim();
        return double.TryParse(cleaned, out diameterInches) && diameterInches > 0;
    }

    private static string ReadParameterValue(Element element, string parameterName)
    {
        Parameter parameter = element?.LookupParameter(parameterName);
        if (parameter == null)
        {
            return string.Empty;
        }

        return parameter.AsString() ?? parameter.AsValueString() ?? string.Empty;
    }

    private static double ReadPipeLengthFeet(Pipe pipe)
    {
        if (pipe?.Location is LocationCurve locationCurve && locationCurve.Curve != null)
        {
            return locationCurve.Curve.Length;
        }

        return 0.0;
    }

    private static bool TryReadPipeEndpoints(Pipe pipe, out Point3D start, out Point3D end)
    {
        start = new Point3D();
        end = new Point3D();
        if (pipe?.Location is not LocationCurve locationCurve || locationCurve.Curve == null)
        {
            return false;
        }

        XYZ curveStart = locationCurve.Curve.GetEndPoint(0);
        XYZ curveEnd = locationCurve.Curve.GetEndPoint(1);
        start = new Point3D(curveStart.X, curveStart.Y, curveStart.Z);
        end = new Point3D(curveEnd.X, curveEnd.Y, curveEnd.Z);
        return true;
    }

    private static double ReadPipeDiameterInches(Pipe pipe)
    {
        Parameter diameter = pipe?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        if (diameter != null && diameter.HasValue)
        {
            return diameter.AsDouble();
        }

        if (pipe?.PipeType != null)
        {
            Parameter typeDiameter = pipe.PipeType.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (typeDiameter != null && typeDiameter.HasValue)
            {
                return typeDiameter.AsDouble();
            }
        }

        return 0.0;
    }
}
