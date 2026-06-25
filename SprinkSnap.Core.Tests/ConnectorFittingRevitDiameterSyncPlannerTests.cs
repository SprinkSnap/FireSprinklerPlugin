using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class ConnectorFittingRevitDiameterSyncPlannerTests
{
    [Fact]
    public void BuildPlan_ReturnsTargets_ForConnectorRoutedFittingsBelowUpsizedBranchDiameter()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            UsesAppliedPipeSizing = true,
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        foreach (PipeSegment segment in routing.Segments.Where(segment =>
                     string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)))
        {
            segment.DiameterInches = 1.5;
        }

        PipePlacementSummary placement = new PipePlacementSummary
        {
            PlacedFittingCount = 1,
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    RoomNumber = room.Number,
                    PlacedFittings =
                    {
                        new PipePlacementFittingResult
                        {
                            JointType = PipeJointTypes.Elbow,
                            DiameterInches = 1.25,
                            PlacedElementId = 9201,
                            Description = "1.25\" elbow at connector routing joint"
                        }
                    }
                }
            }
        };

        PlacedFittingDiameterSyncPlan plan = ConnectorFittingRevitDiameterSyncPlanner.BuildPlan(routing, placement);

        PlacedFittingDiameterSyncTarget target = Assert.Single(plan.Targets);
        Assert.Equal(9201, target.PlacedElementId);
        Assert.Equal(1.5, target.TargetDiameterInches);
        Assert.StartsWith("1.5", target.UpdatedDescription);
    }

    [Fact]
    public void BuildPlan_SkipsFittingsAlreadyHandledBySchematicPlan()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            UsesAppliedPipeSizing = true,
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        foreach (PipeSegment segment in routing.Segments.Where(segment =>
                     string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)))
        {
            segment.DiameterInches = 1.5;
        }

        PipePlacementSummary placement = new PipePlacementSummary
        {
            PlacedFittingCount = 1,
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    PlacedFittings =
                    {
                        new PipePlacementFittingResult
                        {
                            JointType = PipeJointTypes.Elbow,
                            DiameterInches = 1.25,
                            PlacedElementId = 9201,
                            Description = "1.25\" elbow at connector routing joint"
                        }
                    }
                }
            }
        };

        PlacedFittingDiameterSyncPlan plan = ConnectorFittingRevitDiameterSyncPlanner.BuildPlan(
            routing,
            placement,
            new HashSet<int> { 9201 });

        Assert.Empty(plan.Targets);
    }

    [Fact]
    public void BuildCombinedPlan_IncludesSchematicAndConnectorTargets()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            UsesAppliedPipeSizing = true,
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        foreach (PipeSegment segment in routing.Segments.Where(segment =>
                     string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)))
        {
            segment.DiameterInches = 1.5;
            if ((segment.Description ?? string.Empty).Contains('"'))
            {
                segment.Description = "1.5" + segment.Description.Substring(segment.Description.IndexOf('"'));
            }
        }

        PipePlacementSummary placement = new PipePlacementSummary
        {
            PlacedFittingCount = 2,
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    RoomNumber = room.Number,
                    PlacedFittings =
                    {
                        new PipePlacementFittingResult
                        {
                            JointType = PipeJointTypes.Elbow,
                            DiameterInches = 1.25,
                            PlacedElementId = 9101,
                            Description = "1.25\" elbow at branch drop"
                        },
                        new PipePlacementFittingResult
                        {
                            JointType = PipeJointTypes.Elbow,
                            DiameterInches = 1.25,
                            PlacedElementId = 9201,
                            Description = "1.25\" elbow at connector routing joint"
                        }
                    }
                }
            }
        };

        PlacedFittingDiameterSyncPlan plan = FittingDiameterSyncPlanComposer.BuildCombinedPlan(routing, placement);

        Assert.Equal(2, plan.Targets.Count);
        Assert.Contains(plan.Targets, target => target.PlacedElementId == 9101);
        Assert.Contains(plan.Targets, target => target.PlacedElementId == 9201);
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
