using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicPipeRouterSystemTypeTests
{
    [Fact]
    public void RouteRoom_GridAddsDualCrossMains_ForMultiRowLayout()
    {
        RoomInfo room = CreateRoomWithGridLayout();

        IList<PipeSegment> treeSegments = SchematicPipeRouter.RouteRoom(
            room,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            pipingSystemType: PipingSystemTypes.Tree);
        IList<PipeSegment> gridSegments = SchematicPipeRouter.RouteRoom(
            room,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            pipingSystemType: PipingSystemTypes.Grid);

        Assert.Contains(gridSegments, segment => (segment.Description ?? string.Empty).Contains("grid cross main (lower)"));
        Assert.Contains(gridSegments, segment => (segment.Description ?? string.Empty).Contains("grid cross main (upper)"));
        Assert.Contains(gridSegments, segment => (segment.Description ?? string.Empty).Contains("grid feeder"));
        Assert.DoesNotContain(treeSegments, segment => (segment.Description ?? string.Empty).Contains("grid cross main"));
    }

    [Fact]
    public void RouteProject_RecordsSystemTypeAndScheduleOnSummary()
    {
        RoomInfo room = CreateRoomWithGridLayout();
        SprinkSnapProjectPreferences preferences = new SprinkSnapProjectPreferences
        {
            PipingSystemType = PipingSystemTypes.Grid,
            DefaultPipeSchedule = PipeScheduleTypes.Schedule10
        };

        SchematicPipeRoutingSummary summary = SchematicPipeRouter.RouteProject(new[] { room }, preferences);

        Assert.Equal(PipingSystemTypes.Grid, summary.PipingSystemType);
        Assert.Equal(PipeScheduleTypes.Schedule10, summary.PipeSchedule);
    }

    private static RoomInfo CreateRoomWithGridLayout()
    {
        return new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            DesignerApproved = true,
            ApprovedHazardClassification = HazardClassification.LightHazard,
            LengthFeet = 40,
            WidthFeet = 30,
            AreaSquareFeet = 1200,
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            BoundaryPolygon = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(40, 0),
                new Point2D(40, 30),
                new Point2D(0, 30)
            },
            ProposedSprinklers =
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 8, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(30, 8, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(10, 22, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(30, 22, 9) }
            }
        };
    }
}

public sealed class PipingSystemHydraulicComparisonServiceTests
{
    [Fact]
    public void CompareTreeAndGrid_ReturnsBothScenarios_WithRecommendation()
    {
        SprinkSnapProjectState state = CreateComparisonReadyState();
        HydraulicEngine engine = new HydraulicEngine();

        PipingSystemHydraulicComparisonResult comparison =
            PipingSystemHydraulicComparisonService.CompareTreeAndGrid(state, engine);

        Assert.Equal(2, comparison.Scenarios.Count);
        Assert.Contains(comparison.Scenarios, scenario => scenario.PipingSystemType == PipingSystemTypes.Tree);
        Assert.Contains(comparison.Scenarios, scenario => scenario.PipingSystemType == PipingSystemTypes.Grid);
        Assert.Single(comparison.Scenarios, scenario => scenario.IsRecommended);
        Assert.False(string.IsNullOrWhiteSpace(comparison.ComparisonSummary));
    }

    [Fact]
    public void ApplyScenario_UpdatesProjectRoutingPreferencesAndHydraulicResult()
    {
        SprinkSnapProjectState state = CreateComparisonReadyState();
        HydraulicEngine engine = new HydraulicEngine();
        PipingSystemHydraulicComparisonResult comparison =
            PipingSystemHydraulicComparisonService.CompareTreeAndGrid(state, engine);
        PipingSystemScenarioResult gridScenario = comparison.Scenarios
            .First(scenario => scenario.PipingSystemType == PipingSystemTypes.Grid);

        PipingSystemHydraulicComparisonService.ApplyScenario(state, gridScenario, engine);

        Assert.Equal(PipingSystemTypes.Grid, state.Preferences.PipingSystemType);
        Assert.Equal(PipeScheduleTypes.Schedule10, state.Preferences.DefaultPipeSchedule);
        Assert.Equal(PipingSystemTypes.Grid, state.SchematicPipeRouting.PipingSystemType);
        Assert.True(state.HydraulicResult.TotalFlowGpm > 0);
        Assert.True(state.SessionProgress.HydraulicsComplete);
    }

    private static SprinkSnapProjectState CreateComparisonReadyState()
    {
        RoomInfo room = new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            DesignerApproved = true,
            ApprovedHazardClassification = HazardClassification.LightHazard,
            SelectedSprinklerFamilyName = "VK302",
            LengthFeet = 40,
            WidthFeet = 30,
            AreaSquareFeet = 1200,
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            BoundaryPolygon = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(40, 0),
                new Point2D(40, 30),
                new Point2D(0, 30)
            },
            ProposedSprinklers =
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 8, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(30, 8, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(10, 22, 9) },
                new SprinklerPlacementCandidate { Location = new Point3D(30, 22, 9) }
            }
        };

        return new SprinkSnapProjectState
        {
            WaterSupply = new WaterSupplyInput
            {
                StaticPressurePsi = 80,
                ResidualPressurePsi = 65,
                FlowAtResidualGpm = 1200
            },
            Preferences = new SprinkSnapProjectPreferences(),
            Rooms = { room }
        };
    }
}
