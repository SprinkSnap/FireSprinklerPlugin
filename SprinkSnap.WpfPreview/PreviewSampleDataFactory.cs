using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.WpfPreview;

public static class PreviewSampleDataFactory
{
    public static SprinkSnapProjectState CreateProjectState()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        foreach (RoomInfo room in CreateRooms())
        {
            state.Rooms.Add(room);
        }

        state.WaterSupply.StaticPressurePsi = 85;
        state.WaterSupply.ResidualPressurePsi = 65;
        state.WaterSupply.FlowAtResidualGpm = 1200;
        state.WaterSupply.HydrantTestDate = System.DateTime.Today.AddMonths(-2);
        state.ModelAnalysis = new ModelAnalysisEngine().Analyze(state);
        state.ModelAnalysis.LinkedModelCount = 1;
        state.ModelAnalysis.ExistingSprinklerCount = 12;
        state.SessionProgress.ModelAnalysisComplete = true;
        return state;
    }

    public static IReadOnlyList<RoomInfo> CreateRooms()
    {
        IRoomAnalyzer analyzer = new RoomAnalyzer();
        IHazardClassifier classifier = new HazardClassifier();

        List<RoomInfo> rooms = new List<RoomInfo>
        {
            CreateRoom(101, "101", "Open Office", "Business", "ACT Ceiling", 0, 0, 32, 24, 10, 14),
            CreateRoom(102, "102", "Conference Room", "Assembly", "ACT Ceiling", 36, 0, 20, 18, 10, 14),
            CreateRoom(201, "201", "Storage Room", "Storage", "Gypsum Board", 0, 30, 18, 16, 11, 15),
            CreateRoom(202, "202", "Mechanical Room", "Mechanical", "Open to Structure", 24, 30, 22, 20, 14, 16),
            CreateRoom(301, "301", "Workshop", "Fabrication", "Open to Structure", 0, 56, 34, 28, 16, 18),
            CreateRoom(302, "302", "Industrial Process Area", "Industrial Process", "Open to Structure", 40, 56, 42, 30, 18, 22)
        };

        foreach (RoomInfo room in rooms)
        {
            analyzer.Analyze(room);
            HazardClassificationResult suggestion = classifier.SuggestClassification(room);
            room.SuggestedHazardClassification = suggestion.SuggestedClassification;
            room.SuggestionReason = suggestion.Reason;
            room.ApprovedHazardClassification = room.ExistingHazardClassification;
            room.DesignerApproved = HazardClassification.IsSupported(room.ExistingHazardClassification);
        }

        return rooms;
    }

    private static RoomInfo CreateRoom(
        int elementId,
        string number,
        string name,
        string occupancy,
        string ceilingType,
        double originX,
        double originY,
        double width,
        double length,
        double ceilingHeight,
        double deckHeight)
    {
        RoomBoundaryLoop boundary = new RoomBoundaryLoop
        {
            Points = new List<Point3D>
            {
                new Point3D(originX, originY, 0),
                new Point3D(originX + width, originY, 0),
                new Point3D(originX + width, originY + length, 0),
                new Point3D(originX, originY + length, 0),
                new Point3D(originX, originY, 0)
            }
        };

        return new RoomInfo
        {
            RevitElementId = elementId,
            UniqueId = "preview-" + elementId,
            Number = number,
            Name = name,
            OccupancyClassification = occupancy,
            CeilingType = ceilingType,
            CeilingTileInformation = ceilingType.Contains("ACT") ? "2x2 mineral fiber tile" : string.Empty,
            LevelId = 1,
            LevelName = "Preview Level 1",
            FloorElevationFeet = 0,
            HeightFeet = ceilingHeight,
            FloorToFloorHeightFeet = deckHeight,
            CeilingHeightFeet = ceilingHeight,
            CeilingElevationFeet = ceilingHeight,
            DeckElevationFeet = deckHeight,
            ElevationBelowDeckFeet = deckHeight - ceilingHeight,
            VolumeCubicFeet = width * length * ceilingHeight,
            Boundaries = new List<RoomBoundaryLoop> { boundary },
            ExistingHazardClassification = elementId == 202 ? HazardClassification.OrdinaryHazardGroup2 : string.Empty
        };
    }
}

