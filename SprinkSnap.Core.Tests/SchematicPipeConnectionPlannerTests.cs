using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicPipeConnectionPlannerTests
{
    [Fact]
    public void Plan_IncludesElbowAtRiserCrossMainAndBranchConnections()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        IList<PipeSegment> segments = SchematicPipeRouter.RouteRoom(room);
        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(segments);

        Assert.Contains(plan.Connections, intent => intent.Kind == PipeConnectionKind.Elbow);
        Assert.Contains(
            plan.Connections,
            intent => intent.Kind == PipeConnectionKind.Takeoff || intent.Kind == PipeConnectionKind.Tee);
    }

    [Fact]
    public void Plan_CreatesTakeoff_WhenBranchTieInIsOnCrossMainInterior()
    {
        List<PipeSegment> segments = new List<PipeSegment>
        {
            new PipeSegment
            {
                SegmentType = PipeSegmentTypes.CrossMain,
                Start = new Point3D(0, 0, 10),
                End = new Point3D(30, 0, 10),
                Description = "4\" cross main"
            },
            new PipeSegment
            {
                SegmentType = PipeSegmentTypes.Branch,
                Start = new Point3D(15, 5, 10),
                End = new Point3D(15, 0, 10),
                Description = "1.25\" branch tie-in #1"
            }
        };

        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(segments);

        Assert.Contains(plan.Connections, intent => intent.Kind == PipeConnectionKind.Takeoff);
    }

    [Fact]
    public void Plan_ReturnsEmptyPlan_WhenNoSegments()
    {
        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(new List<PipeSegment>());
        Assert.Empty(plan.Connections);
    }

    [Fact]
    public void Plan_CreatesDirectConnection_ForCollinearSegments()
    {
        List<PipeSegment> segments = new List<PipeSegment>
        {
            new PipeSegment
            {
                SegmentType = PipeSegmentTypes.CrossMain,
                Start = new Point3D(0, 0, 10),
                End = new Point3D(10, 0, 10),
                Description = "first run"
            },
            new PipeSegment
            {
                SegmentType = PipeSegmentTypes.CrossMain,
                Start = new Point3D(10, 0, 10),
                End = new Point3D(20, 0, 10),
                Description = "second run"
            }
        };

        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(segments);

        Assert.Contains(plan.Connections, intent => intent.Kind == PipeConnectionKind.Direct);
    }

    [Fact]
    public void Plan_CreatesElbowAtBranchJunction()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        IList<PipeSegment> segments = SchematicPipeRouter.RouteRoom(room);
        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(segments);

        IList<PipeSegment> branchDrops = segments
            .Where(segment => (segment.Description ?? string.Empty).IndexOf("branch drop", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        IList<PipeSegment> branchTieIns = segments
            .Where(segment => (segment.Description ?? string.Empty).IndexOf("branch tie-in", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        Assert.NotEmpty(branchDrops);
        Assert.NotEmpty(branchTieIns);

        Point3D junction = branchDrops[0].End;
        Assert.Contains(plan.Connections, intent =>
            intent.Kind == PipeConnectionKind.Elbow
            && Math.Abs(intent.Location.X - junction.X) < 0.2
            && Math.Abs(intent.Location.Y - junction.Y) < 0.2
            && Math.Abs(intent.Location.Z - junction.Z) < 0.2);
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
