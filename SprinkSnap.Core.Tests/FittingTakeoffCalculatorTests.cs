using System.Linq;
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
        Assert.Equal(1, CountFittings(takeoff, PipeJointTypes.Elbow, 4.0));
        Assert.Equal(2, CountFittings(takeoff, PipeJointTypes.Elbow, 1.25));
        Assert.Equal(2, CountFittings(takeoff, PipeJointTypes.Tee, 1.25));
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
        Assert.True(CountFittings(takeoff, PipeJointTypes.Elbow, 4.0) >= 1);
        Assert.True(CountFittings(takeoff, PipeJointTypes.Tee, 1.25) >= 2);
        Assert.Equal(1, takeoff.ValveCount);
    }

    [Fact]
    public void Calculate_UsesUpsizedBranchDiameter_WhenSchematicSegmentsAreUpsized()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });
        foreach (PipeSegment segment in routing.Segments.Where(segment =>
                     string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)))
        {
            segment.DiameterInches = 1.5;
            if ((segment.Description ?? string.Empty).Contains('"'))
            {
                segment.Description = "1.5" + segment.Description.Substring(segment.Description.IndexOf('"'));
            }
        }

        IList<RoomFittingTakeoff> takeoffs = FittingTakeoffCalculator.Calculate(routing);

        RoomFittingTakeoff takeoff = Assert.Single(takeoffs);
        Assert.True(CountFittings(takeoff, PipeJointTypes.Elbow, 1.5) >= 2);
        Assert.True(CountFittings(takeoff, PipeJointTypes.Tee, 1.5) >= 2);
        Assert.Equal(0, CountFittings(takeoff, PipeJointTypes.Tee, 1.25));
    }

    private static int CountFittings(RoomFittingTakeoff takeoff, string jointType, double diameterInches)
    {
        return takeoff.FittingCounts
            .Where(entry => string.Equals(entry.JointType, jointType, System.StringComparison.OrdinalIgnoreCase)
                && System.Math.Abs(entry.DiameterInches - diameterInches) < 0.01)
            .Sum(entry => entry.Count);
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
