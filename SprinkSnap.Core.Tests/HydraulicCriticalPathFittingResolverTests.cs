using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicCriticalPathFittingResolverTests
{
    [Fact]
    public void ResolveForSegmentChain_MatchesJointsOnSegmentEndpoints()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        LayoutLinkedHydraulicPath path = CreatePathForRemoteHead(room, sprinklerIndex: 1);
        HydraulicSegmentGraphBuilder.BuildSegmentChain(path, routing, null);

        IList<CriticalPathFitting> fittings = HydraulicCriticalPathFittingResolver.ResolveForSegmentChain(
            path.SegmentChain,
            routing,
            null);

        Assert.NotEmpty(fittings);
        Assert.True(fittings.All(fitting => fitting.EquivalentLengthFeet > 0));
        Assert.Contains(fittings, fitting => fitting.Joint.JointType == PipeJointTypes.Valve);
    }

    [Fact]
    public void ResolveForSegmentChain_MarksPlacedFittings_WhenPlacementSummaryMatches()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        PipeJoint placedValve = SchematicPipeJointBuilder.BuildRoomJoints(routing.Segments.ToList())
            .First(joint => joint.JointType == PipeJointTypes.Valve);
        PipePlacementSummary placement = new PipePlacementSummary
        {
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    PlacedFittings =
                    {
                        new PipePlacementFittingResult
                        {
                            JointType = placedValve.JointType,
                            DiameterInches = placedValve.DiameterInches
                        }
                    }
                }
            }
        };
        LayoutLinkedHydraulicPath path = CreatePathForRemoteHead(room, sprinklerIndex: 1);
        HydraulicSegmentGraphBuilder.BuildSegmentChain(path, routing, placement);

        IList<CriticalPathFitting> fittings = HydraulicCriticalPathFittingResolver.ResolveForSegmentChain(
            path.SegmentChain,
            routing,
            placement);

        Assert.Contains(fittings, fitting =>
            fitting.Joint.JointType == PipeJointTypes.Valve
            && string.Equals(fitting.DataSource, "Placed", System.StringComparison.OrdinalIgnoreCase));
    }

    private static LayoutLinkedHydraulicPath CreatePathForRemoteHead(RoomInfo room, int sprinklerIndex)
    {
        LayoutSprinklerPoint remote = new LayoutSprinklerPoint
        {
            Room = room,
            Location = room.ProposedSprinklers[sprinklerIndex].Location,
            SprinklerIndex = sprinklerIndex,
            KFactor = 5.6
        };

        return new LayoutLinkedHydraulicPath
        {
            SourcePoint = HydraulicGraphBuilder.ResolveSourcePoint(new[] { room }),
            MostRemoteSprinkler = remote,
            OperatingSprinklers = new List<LayoutSprinklerPoint> { remote },
            BranchDiameterInches = 1.25,
            MainDiameterInches = 4.0,
            UsesLayoutGeometry = true
        };
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
