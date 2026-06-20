using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public interface IRoomExtractor
{
    IReadOnlyList<RoomInfo> ExtractRooms(Document document);
}

public sealed class RoomExtractor : IRoomExtractor
{
    private readonly IRoomBoundaryExtractor boundaryExtractor;
    private readonly IRoomAnalyzer roomAnalyzer;
    private readonly IHazardClassifier hazardClassifier;
    private readonly IHazardClassificationParameterStorage parameterStorage;
    private readonly ICeilingIntelligenceService ceilingIntelligenceService;

    public RoomExtractor(
        IRoomBoundaryExtractor boundaryExtractor,
        IRoomAnalyzer roomAnalyzer,
        IHazardClassifier hazardClassifier,
        IHazardClassificationParameterStorage parameterStorage)
    {
        this.boundaryExtractor = boundaryExtractor;
        this.roomAnalyzer = roomAnalyzer;
        this.hazardClassifier = hazardClassifier;
        this.parameterStorage = parameterStorage;
        ceilingIntelligenceService = new CeilingIntelligenceService();
    }

    public IReadOnlyList<RoomInfo> ExtractRooms(Document document)
    {
        List<Level> levels = new FilteredElementCollector(document)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(level => level.Elevation)
            .ToList();

        var rooms = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .ToList();

        List<RoomInfo> roomInfos = new List<RoomInfo>();
        foreach (Room room in rooms)
        {
            if (room.Area <= 0.0)
            {
                continue;
            }

            RoomInfo roomInfo = ExtractRoom(document, room, levels);
            roomAnalyzer.Analyze(roomInfo);
            ApplyCeilingIntelligence(roomInfo);

            HazardClassificationResult suggestion = hazardClassifier.SuggestClassification(roomInfo);
            roomInfo.SuggestedHazardClassification = suggestion.SuggestedClassification;
            roomInfo.SuggestionReason = suggestion.Reason;

            if (!string.IsNullOrWhiteSpace(roomInfo.ExistingHazardClassification)
                && HazardClassification.IsSupported(roomInfo.ExistingHazardClassification))
            {
                roomInfo.ApprovedHazardClassification = roomInfo.ExistingHazardClassification;
                roomInfo.DesignerApproved = true;
            }
            else
            {
                roomInfo.ApprovedHazardClassification = roomInfo.SuggestedHazardClassification;
                roomInfo.DesignerApproved = false;
            }

            roomInfos.Add(roomInfo);
        }

        return roomInfos;
    }

    private RoomInfo ExtractRoom(Document document, Room room, IReadOnlyList<Level> levels)
    {
        Level level = document.GetElement(room.LevelId) as Level;
        double floorElevation = level?.Elevation ?? 0.0;
        double floorToFloorHeight = CalculateFloorToFloorHeight(level, levels);
        double roomHeight = GetRoomHeight(room);
        double ceilingHeight = GetDoubleParameter(room, "Ceiling Height", roomHeight);
        double ceilingElevation = floorElevation + ceilingHeight;
        double deckElevation = floorElevation + (floorToFloorHeight > 0.0 ? floorToFloorHeight : roomHeight);
        double roomVolume = GetRoomVolume(room);

        return new RoomInfo
        {
            RevitElementId = room.Id.IntegerValue,
            UniqueId = room.UniqueId,
            Name = GetParameterDisplayValue(room.get_Parameter(BuiltInParameter.ROOM_NAME), room.Name),
            Number = GetParameterDisplayValue(room.get_Parameter(BuiltInParameter.ROOM_NUMBER), room.Number),
            AreaSquareFeet = room.Area,
            AreaSquareMeters = ConvertAreaToSquareMeters(room.Area),
            VolumeCubicFeet = roomVolume,
            VolumeCubicMeters = ConvertVolumeToCubicMeters(roomVolume),
            HeightFeet = roomHeight,
            FloorToFloorHeightFeet = floorToFloorHeight,
            CeilingHeightFeet = ceilingHeight,
            CeilingType = GetStringParameter(room, "Ceiling Type", "Ceiling Finish", "Finish Ceiling Type"),
            CeilingTileInformation = GetStringParameter(room, "Ceiling Tile Information", "Ceiling Tile", "ACT Type"),
            OccupancyClassification = GetStringParameter(room, "Occupancy Classification", "Occupancy", "Room Occupancy"),
            LevelId = room.LevelId.IntegerValue,
            LevelName = level?.Name ?? string.Empty,
            FloorElevationFeet = floorElevation,
            CeilingElevationFeet = ceilingElevation,
            DeckElevationFeet = deckElevation,
            ElevationBelowDeckFeet = Math.Max(0.0, deckElevation - ceilingElevation),
            Boundaries = boundaryExtractor.ExtractBoundaryLoops(room).ToList(),
            PerimeterFeet = GetDoubleParameter(room, "Perimeter", 0.0),
            ObstructionCount = CountPotentialObstructions(document, room),
            ExistingHazardClassification = parameterStorage.Read(room)
        };
    }

    private void ApplyCeilingIntelligence(RoomInfo roomInfo)
    {
        CeilingIntelligenceResult ceilingResult = ceilingIntelligenceService.Analyze(roomInfo);
        roomInfo.CeilingClassification = ceilingResult.Classification;
        roomInfo.CeilingIntelligenceSummary = ceilingResult.Summary;
        roomInfo.LayoutConfidenceScore = ceilingResult.ConfidenceScore;
        roomInfo.RequiresExceptionReview = roomInfo.RequiresExceptionReview || ceilingResult.RequiresReview;
        if (ceilingResult.RequiresReview && string.IsNullOrWhiteSpace(roomInfo.ExceptionReason))
        {
            roomInfo.ExceptionReason = ceilingResult.Summary;
        }

        if (roomInfo.ObstructionCount > 0)
        {
            roomInfo.LayoutConfidenceScore = Math.Min(roomInfo.LayoutConfidenceScore, 0.55);
            roomInfo.RequiresExceptionReview = true;
            roomInfo.ExceptionReason = roomInfo.ObstructionCount
                + " potential obstruction(s) detected from Revit categories. Obstruction clearance review is required.";
        }
    }

    private static string GetParameterDisplayValue(Parameter parameter, string fallbackValue)
    {
        if (parameter == null)
        {
            return fallbackValue ?? string.Empty;
        }

        string value = parameter.AsValueString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = parameter.AsString();
        }

        return string.IsNullOrWhiteSpace(value) ? fallbackValue ?? string.Empty : value;
    }

    private static string GetStringParameter(Element element, params string[] parameterNames)
    {
        foreach (string parameterName in parameterNames)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter == null)
            {
                continue;
            }

            string value = parameter.AsString();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = parameter.AsValueString();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static double GetDoubleParameter(Element element, string parameterName, double fallbackValue)
    {
        Parameter parameter = element.LookupParameter(parameterName);
        if (parameter == null)
        {
            return fallbackValue;
        }

        if (parameter.StorageType == StorageType.Double)
        {
            return parameter.AsDouble();
        }

        if (double.TryParse(parameter.AsValueString(), NumberStyles.Float, CultureInfo.CurrentCulture, out double parsedValue))
        {
            return parsedValue;
        }

        return fallbackValue;
    }

    private static double GetRoomHeight(Room room)
    {
        if (Enum.TryParse("ROOM_HEIGHT", out BuiltInParameter roomHeightBuiltInParameter))
        {
            Parameter heightParameter = room.get_Parameter(roomHeightBuiltInParameter);
            if (heightParameter != null && heightParameter.StorageType == StorageType.Double)
            {
                return heightParameter.AsDouble();
            }
        }

        return room.UnboundedHeight;
    }

    private static double GetRoomVolume(Room room)
    {
        try
        {
            return room.Volume;
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            return 0.0;
        }
    }

    private static double CalculateFloorToFloorHeight(Level roomLevel, IReadOnlyList<Level> levels)
    {
        if (roomLevel == null)
        {
            return 0.0;
        }

        Level nextLevel = levels
            .Where(level => level.Elevation > roomLevel.Elevation)
            .OrderBy(level => level.Elevation)
            .FirstOrDefault();

        return nextLevel == null ? 0.0 : nextLevel.Elevation - roomLevel.Elevation;
    }

    private static int CountPotentialObstructions(Document document, Room room)
    {
        BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
        if (roomBox == null)
        {
            return 0;
        }

        BuiltInCategory[] obstructionCategories =
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_GenericModel
        };

        int count = 0;
        foreach (BuiltInCategory category in obstructionCategories)
        {
            count += new FilteredElementCollector(document)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Where(element => BoundingBoxesIntersect(roomBox, element.get_BoundingBox(null)))
                .Count();
        }

        return count;
    }

    private static bool BoundingBoxesIntersect(BoundingBoxXYZ first, BoundingBoxXYZ second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return first.Min.X <= second.Max.X
            && first.Max.X >= second.Min.X
            && first.Min.Y <= second.Max.Y
            && first.Max.Y >= second.Min.Y
            && first.Min.Z <= second.Max.Z
            && first.Max.Z >= second.Min.Z;
    }

    private static double ConvertAreaToSquareMeters(double areaSquareFeet)
    {
        return UnitUtils.ConvertFromInternalUnits(areaSquareFeet, UnitTypeId.SquareMeters);
    }

    private static double ConvertVolumeToCubicMeters(double volumeCubicFeet)
    {
        return UnitUtils.ConvertFromInternalUnits(volumeCubicFeet, UnitTypeId.CubicMeters);
    }
}

