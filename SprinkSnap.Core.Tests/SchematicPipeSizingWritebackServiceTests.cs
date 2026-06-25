using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicPipeSizingWritebackServiceTests
{
    [Fact]
    public void WriteBackAppliedSizing_UpdatesMatchingSchematicSegments()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        PipeSegment branchDrop = routing.Segments.First(segment =>
            (segment.Description ?? string.Empty).IndexOf("branch drop #1", System.StringComparison.OrdinalIgnoreCase) >= 0);
        LayoutLinkedHydraulicPath path = new LayoutLinkedHydraulicPath
        {
            UsesAppliedPipeSizing = true,
            UsesSegmentGraphHydraulics = true,
            MostRemoteSprinkler = new LayoutSprinklerPoint
            {
                Room = room,
                SprinklerIndex = 0
            },
            SegmentChain =
            {
                new HydraulicGraphSegment
                {
                    RoomRevitElementId = room.RevitElementId,
                    SegmentType = PipeSegmentTypes.Branch,
                    Description = branchDrop.Description,
                    SegmentId = branchDrop.Description,
                    Start = branchDrop.Start,
                    End = branchDrop.End,
                    DiameterInches = 1.5
                }
            }
        };

        int updatedCount = SchematicPipeSizingWritebackService.WriteBackAppliedSizing(routing, path);

        Assert.True(updatedCount > 0);
        Assert.True(path.UsesSchematicPipeSizingWriteback);
        Assert.Equal(1.5, branchDrop.DiameterInches);
        Assert.StartsWith("1.5", branchDrop.Description);
        Assert.True(routing.UsesAppliedPipeSizing);
    }

    [Fact]
    public void Calculate_WritesSizedDiametersBackToSchematicRouting_WhenVelocitySizingApplies()
    {
        RoomInfo room = new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            Name = "Office",
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            DesignerApproved = true,
            ApprovedHazardClassification = HazardClassification.LightHazard,
            ProposedSprinklers =
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
            }
        };
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };

        LayoutLinkedHydraulicPath path = LayoutLinkedHydraulicCalculator.Calculate(
            new[] { room },
            operatingSprinklerCount: 2,
            designFlowPerSprinklerGpm: 60.0,
            hoseStreamAllowanceGpm: 100.0,
            defaultKFactor: 5.6,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            routing,
            null,
            null,
            remoteAreaSquareFeet: 260.0,
            maxCoverageSquareFeet: 130.0);

        Assert.True(path.UsesAppliedPipeSizing);
        Assert.True(path.UsesSchematicPipeSizingWriteback);
        Assert.True(routing.UsesAppliedPipeSizing);
        Assert.Contains(
            routing.Segments,
            segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)
                && segment.DiameterInches >= 1.5);
        Assert.Contains(path.Warnings, warning => warning.IndexOf("schematic routing", System.StringComparison.OrdinalIgnoreCase) >= 0);
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
