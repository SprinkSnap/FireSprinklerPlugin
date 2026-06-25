using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PlacedPipeGraphBuilderTests
{
    [Fact]
    public void RoomHasPlacedTopology_ReturnsTrue_WhenMajorityOfSegmentsHaveEndpoints()
    {
        PipePlacementRoomResult room = new PipePlacementRoomResult
        {
            PlacedSegments =
            {
                CreatePlacedSegment(PipeSegmentTypes.Branch, new Point3D(20, 8, 8.5), new Point3D(20, 8, 9), true),
                CreatePlacedSegment(PipeSegmentTypes.Branch, new Point3D(20, 8, 9), new Point3D(20, 12, 9), true),
                CreatePlacedSegment(PipeSegmentTypes.CrossMain, new Point3D(10, 12, 9), new Point3D(20, 12, 9), true),
                CreatePlacedSegment(PipeSegmentTypes.Riser, new Point3D(10, 12, 0), new Point3D(10, 12, 9), true)
            }
        };

        Assert.True(PlacedPipeGraphBuilder.RoomHasPlacedTopology(room));
    }

    [Fact]
    public void TraceCriticalPath_FollowsPlacedEndpoints_FromRemoteHeadToRiser()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        IList<PipeSegment> schematicSegments = SchematicPipeRouter.RouteRoom(room);
        IList<HydraulicGraphSegment> placedSegments = BuildPlacedSegmentsFromSchematic(schematicSegments);
        LayoutSprinklerPoint remote = new LayoutSprinklerPoint
        {
            Room = room,
            Location = room.ProposedSprinklers[1].Location,
            SprinklerIndex = 1,
            KFactor = 5.6
        };
        Point3D source = new Point3D(10, 12, 0);

        IList<HydraulicGraphSegment> chain = PlacedPipeGraphBuilder.TraceCriticalPath(
            remote,
            placedSegments,
            source);

        Assert.True(chain.Count >= 3);
        Assert.Equal(PipeSegmentTypes.Branch, chain[0].SegmentType);
        Assert.Contains(chain, segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Placed", chain[0].DataSource);
    }

    [Fact]
    public void Calculate_UsesPlacedTopology_WhenEndpointsAreAvailable()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        room.DesignerApproved = true;
        room.ApprovedHazardClassification = HazardClassification.LightHazard;
        IList<PipeSegment> schematicSegments = SchematicPipeRouter.RouteRoom(room);
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = schematicSegments.ToList()
        };
        PipePlacementSummary placement = CreatePlacementSummary(room, schematicSegments, lengthOffsetFeet: 5.0);

        LayoutLinkedHydraulicPath path = LayoutLinkedHydraulicCalculator.Calculate(
            new[] { room },
            operatingSprinklerCount: 2,
            designFlowPerSprinklerGpm: 8.0,
            hoseStreamAllowanceGpm: 100.0,
            defaultKFactor: 5.6,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            routing,
            placement);

        Assert.True(path.UsesPlacedPipeTopology);
        Assert.True(path.UsesPlacedPipeLengths);
        Assert.Contains(
            path.SegmentChain,
            segment => segment.LengthFeet > schematicSegments.Max(schematic => schematic.LengthFeet));
        Assert.Contains(
            path.Warnings,
            warning => warning.IndexOf("placed Revit pipe topology", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void ResolveSegmentsForRoom_PrefersPlacedTopology_OverDescriptionMatching()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        IList<PipeSegment> schematicSegments = SchematicPipeRouter.RouteRoom(room);
        PipePlacementSummary placement = CreatePlacementSummary(room, schematicSegments, lengthOffsetFeet: 3.5);

        IList<HydraulicGraphSegment> segments = PlacedPipeHydraulicResolver.ResolveSegmentsForRoom(
            room.RevitElementId,
            placement,
            new SchematicPipeRoutingSummary { Segments = schematicSegments.ToList() },
            1.25,
            4.0);

        Assert.All(segments, segment => Assert.Equal("Placed", segment.DataSource));
        Assert.Equal(schematicSegments[0].Start.X, segments[0].Start.X);
        Assert.True(segments[0].LengthFeet > schematicSegments[0].LengthFeet);
    }

    private static PipePlacementSummary CreatePlacementSummary(
        RoomInfo room,
        IList<PipeSegment> schematicSegments,
        double lengthOffsetFeet)
    {
        PipePlacementRoomResult roomResult = new PipePlacementRoomResult
        {
            RoomRevitElementId = room.RevitElementId,
            RoomNumber = room.Number
        };
        foreach (PipeSegment schematicSegment in schematicSegments)
        {
            roomResult.PlacedSegments.Add(new PipePlacementSegmentResult
            {
                SegmentType = schematicSegment.SegmentType,
                DiameterInches = schematicSegment.DiameterInches,
                LengthFeet = schematicSegment.LengthFeet + lengthOffsetFeet,
                Description = schematicSegment.Description,
                Start = schematicSegment.Start,
                End = schematicSegment.End,
                HasTopology = true
            });
        }

        return new PipePlacementSummary
        {
            RoomResults = { roomResult }
        };
    }

    private static IList<HydraulicGraphSegment> BuildPlacedSegmentsFromSchematic(IList<PipeSegment> schematicSegments)
    {
        return schematicSegments
            .Select(segment => new HydraulicGraphSegment
            {
                SegmentId = segment.Description,
                SegmentType = segment.SegmentType,
                Description = segment.Description,
                Start = segment.Start,
                End = segment.End,
                LengthFeet = segment.LengthFeet,
                DiameterInches = segment.DiameterInches,
                DataSource = "Placed"
            })
            .ToList();
    }

    private static PipePlacementSegmentResult CreatePlacedSegment(
        string segmentType,
        Point3D start,
        Point3D end,
        bool hasTopology)
    {
        return new PipePlacementSegmentResult
        {
            SegmentType = segmentType,
            Start = start,
            End = end,
            LengthFeet = System.Math.Abs(end.X - start.X) + System.Math.Abs(end.Y - start.Y) + System.Math.Abs(end.Z - start.Z),
            DiameterInches = string.Equals(segmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase) ? 1.25 : 4.0,
            HasTopology = hasTopology
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
