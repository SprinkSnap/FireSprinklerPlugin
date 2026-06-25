using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicSegmentGraphBuilderTests
{
    [Fact]
    public void BuildSegmentChain_OrdersRemoteHeadThroughBranchCrossMainAndRiser()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        IList<PipeSegment> routedSegments = SchematicPipeRouter.RouteRoom(room);
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = routedSegments.ToList()
        };

        LayoutLinkedHydraulicPath path = CreatePathForRemoteHead(room, sprinklerIndex: 1);
        HydraulicSegmentGraphBuilder.BuildSegmentChain(path, routing, null);

        Assert.True(path.UsesSegmentGraphHydraulics);
        Assert.True(path.CriticalPathSegmentCount >= 3);
        Assert.Contains(path.SegmentChain, segment =>
            (segment.Description ?? string.Empty).IndexOf("branch drop #2", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(path.SegmentChain, segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));
        Assert.True(path.BranchLengthFeet > 0);
        Assert.True(path.MainLengthFeet > 0);
    }

    [Fact]
    public void ResolveSegmentsForRoom_UsesPlacedLengths_WhenDescriptionsMatch()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        PipePlacementSummary placement = new PipePlacementSummary
        {
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    PlacedSegments =
                    {
                        new PipePlacementSegmentResult
                        {
                            SegmentType = PipeSegmentTypes.Branch,
                            Description = "1.25\" branch drop #1",
                            DiameterInches = 1.25,
                            LengthFeet = 22.0
                        },
                        new PipePlacementSegmentResult
                        {
                            SegmentType = PipeSegmentTypes.Riser,
                            Description = "4\" riser",
                            DiameterInches = 4.0,
                            LengthFeet = 33.0
                        }
                    }
                }
            }
        };

        IList<HydraulicGraphSegment> segments = PlacedPipeHydraulicResolver.ResolveSegmentsForRoom(
            room.RevitElementId,
            placement,
            routing,
            1.25,
            4.0);

        HydraulicGraphSegment branchDrop = segments.First(segment =>
            (segment.Description ?? string.Empty).IndexOf("branch drop #1", System.StringComparison.OrdinalIgnoreCase) >= 0);
        HydraulicGraphSegment riser = segments.First(segment =>
            string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(22.0, branchDrop.LengthFeet);
        Assert.Equal(33.0, riser.LengthFeet);
        Assert.Equal("Placed", branchDrop.DataSource);
    }

    [Fact]
    public void Calculate_ProducesPerSegmentCriticalPath_ForSchematicRouting()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        room.DesignerApproved = true;
        room.ApprovedHazardClassification = HazardClassification.LightHazard;
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };

        LayoutLinkedHydraulicPath path = LayoutLinkedHydraulicCalculator.Calculate(
            new[] { room },
            operatingSprinklerCount: 2,
            designFlowPerSprinklerGpm: 8.0,
            hoseStreamAllowanceGpm: 100.0,
            defaultKFactor: 5.6,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            routing,
            null);

        Assert.True(path.UsesSegmentGraphHydraulics);
        Assert.True(path.CriticalPath.Count > 4);
        Assert.Contains(path.CriticalPath, node => string.Equals(node.SegmentType, "Sprinkler", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(path.CriticalPath, node => string.Equals(node.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));
        Assert.True(path.JunctionPressurePsi > path.RemoteSprinklerPressurePsi);
        Assert.True(path.BranchFrictionPsi > 0);
        Assert.True(path.MainFrictionPsi > 0);
        Assert.True(path.CriticalPathFittingCount > 0);
        Assert.True(path.FittingFrictionPsi > 0);
        Assert.True(path.CriticalPathDemandPsi > path.JunctionPressurePsi + path.MainFrictionPsi);
        Assert.Contains(path.CriticalPath, node => string.Equals(node.SegmentType, PipeJointTypes.Valve, System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, path.CriticalPathVelocityViolationCount);
        Assert.True(path.MaxCriticalPathVelocityFeetPerSecond > 0);
        Assert.Contains(path.CriticalPath, node => node.VelocityFeetPerSecond > 0 && node.DiameterInches > 0);
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
