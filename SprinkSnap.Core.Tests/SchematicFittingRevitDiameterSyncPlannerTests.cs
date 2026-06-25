using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicFittingRevitDiameterSyncPlannerTests
{
    [Fact]
    public void BuildPlan_ReturnsTargets_WhenPlacedFittingsAreSmallerThanSchematicJoints()
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

        IList<PipeJoint> schematicJoints = SchematicPipeJointBuilder.BuildRoomJoints(routing.Segments.ToList());
        PipeJoint branchElbow = schematicJoints.First(joint =>
            joint.JointType == PipeJointTypes.Elbow
            && (joint.Description ?? string.Empty).IndexOf("branch drop", System.StringComparison.OrdinalIgnoreCase) >= 0);

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
                            PlacedElementId = 9101,
                            Description = "1.25\" elbow at branch drop"
                        }
                    }
                }
            }
        };

        PlacedFittingDiameterSyncPlan plan = SchematicFittingRevitDiameterSyncPlanner.BuildPlan(routing, placement);

        Assert.Contains(plan.Targets, target => target.PlacedElementId == 9101);
        PlacedFittingDiameterSyncTarget elbowTarget = plan.Targets.First(target => target.PlacedElementId == 9101);
        Assert.Equal(1.5, elbowTarget.TargetDiameterInches);
        Assert.Equal(1.25, elbowTarget.CurrentDiameterInches);
        Assert.Equal(branchElbow.Description, elbowTarget.UpdatedDescription);
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
                            PlacedElementId = 9101,
                            Description = "1.25\" elbow at branch drop"
                        }
                    }
                }
            }
        };

        PlacedFittingDiameterSyncPlan plan = SchematicFittingRevitDiameterSyncPlanner.BuildPlan(routing, placement);

        Assert.Empty(plan.Targets);
        Assert.Contains(plan.Messages, message => message.IndexOf("velocity-sized", System.StringComparison.OrdinalIgnoreCase) >= 0);
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
