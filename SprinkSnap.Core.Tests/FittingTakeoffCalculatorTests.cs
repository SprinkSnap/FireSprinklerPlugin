using FireSprinklerPlugin.SprinkSnap.Core.Materials;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class FittingTakeoffCalculatorTests
{
    [Fact]
    public void Calculate_UsesConnectorFittingsInBom_WhenOnlyValvePlacedAsAccessory()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            RoomRevitElementId = room.RevitElementId,
            RoomNumber = room.Number,
            PlacedSegmentCount = 5,
            ConnectedJointCount = 4,
            ConnectedFittingCount = 4,
            PlacedFittings =
            {
                new PipePlacementFittingResult { JointType = PipeJointTypes.Elbow, DiameterInches = 4.0 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Elbow, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Elbow, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Tee, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Tee, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Valve, DiameterInches = 4.0 }
            },
            PlacedSegments =
            {
                new PipePlacementSegmentResult { SegmentType = PipeSegmentTypes.Riser, DiameterInches = 4.0, LengthFeet = 9.0 }
            }
        };
        PipePlacementSummary placementSummary = new PipePlacementSummary
        {
            RoomResults = { placedRoom }
        };

        IList<RoomFittingTakeoff> takeoffs = FittingTakeoffCalculator.Calculate(routing, placementSummary);

        RoomFittingTakeoff takeoff = Assert.Single(takeoffs);
        Assert.True(takeoff.UsesPlacedFittings);
        Assert.Equal(1, takeoff.Elbow4InchCount);
        Assert.Equal(2, takeoff.Elbow125Count);
        Assert.Equal(2, takeoff.Tee125Count);
        Assert.Equal(1, takeoff.ValveCount);
        Assert.Equal(1, takeoff.RiserAssemblyCount);
    }

    [Fact]
    public void Calculate_FallsBackToSchematicCounts_WhenNoPlacedFittingsExist()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });

        IList<RoomFittingTakeoff> takeoffs = FittingTakeoffCalculator.Calculate(routing);

        RoomFittingTakeoff takeoff = Assert.Single(takeoffs);
        Assert.False(takeoff.UsesPlacedFittings);
        Assert.True(takeoff.Elbow4InchCount >= 1);
        Assert.True(takeoff.Tee125Count >= 2);
        Assert.Equal(1, takeoff.ValveCount);
    }

    private static RoomInfo CreateRoomWithTwoHeads()
    {
        return new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            Name = "Office",
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            ProposedSprinklers = new List<SprinklerPlacementCandidate>
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
            }
        };
    }
}
