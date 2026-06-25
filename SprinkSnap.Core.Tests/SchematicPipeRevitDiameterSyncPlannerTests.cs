using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicPipeRevitDiameterSyncPlannerTests
{
    [Fact]
    public void BuildPlan_ReturnsTargets_WhenPlacedPipesAreSmallerThanSchematic()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            UsesAppliedPipeSizing = true,
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        PipeSegment branchDrop = routing.Segments.First(segment =>
            (segment.Description ?? string.Empty).IndexOf("branch drop #1", System.StringComparison.OrdinalIgnoreCase) >= 0);
        branchDrop.DiameterInches = 1.5;
        branchDrop.Description = "1.5\" branch drop #1";

        PipePlacementSummary placement = new PipePlacementSummary
        {
            PlacedSegmentCount = 1,
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    RoomNumber = room.Number,
                    PlacedSegments =
                    {
                        new PipePlacementSegmentResult
                        {
                            SegmentType = PipeSegmentTypes.Branch,
                            DiameterInches = 1.25,
                            PlacedElementId = 9001,
                            Description = "1.25\" branch drop #1"
                        }
                    }
                }
            }
        };

        PlacedPipeDiameterSyncPlan plan = SchematicPipeRevitDiameterSyncPlanner.BuildPlan(routing, placement);

        Assert.Single(plan.Targets);
        Assert.Equal(9001, plan.Targets[0].PlacedElementId);
        Assert.Equal(1.5, plan.Targets[0].TargetDiameterInches);
        Assert.Equal(1.25, plan.Targets[0].CurrentDiameterInches);
        Assert.Equal("1.5\" branch drop #1", plan.Targets[0].UpdatedDescription);
    }

    [Fact]
    public void BuildPlan_ReturnsEmpty_WhenSchematicSizingWasNotApplied()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        PipePlacementSummary placement = new PipePlacementSummary
        {
            PlacedSegmentCount = 1,
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
                            DiameterInches = 1.25,
                            PlacedElementId = 9001,
                            Description = "1.25\" branch drop #1"
                        }
                    }
                }
            }
        };

        PlacedPipeDiameterSyncPlan plan = SchematicPipeRevitDiameterSyncPlanner.BuildPlan(routing, placement);

        Assert.Empty(plan.Targets);
        Assert.Contains(plan.Messages, message => message.IndexOf("velocity-sized", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void ShouldSyncPlacedPipeDiametersAfterCalculation_ReturnsTrue_WhenWritebackOccurredAndPipesArePlaced()
    {
        bool shouldSync = HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
            new HydraulicCalculationResult { UsesSchematicPipeSizingWriteback = true },
            new PipePlacementSummary { PlacedSegmentCount = 2 },
            isPreviewMode: false,
            syncAvailable: true);

        Assert.True(shouldSync);
    }

    [Fact]
    public void ShouldSyncPlacedPipeDiametersAfterCalculation_ReturnsFalse_InPreviewMode()
    {
        bool shouldSync = HydraulicsPipeDataRefreshPolicy.ShouldSyncPlacedPipeDiametersAfterCalculation(
            new HydraulicCalculationResult { UsesSchematicPipeSizingWriteback = true },
            new PipePlacementSummary { PlacedSegmentCount = 2 },
            isPreviewMode: true,
            syncAvailable: true);

        Assert.False(shouldSync);
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
