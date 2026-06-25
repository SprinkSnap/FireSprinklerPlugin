using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SchematicPipeJointBuilderTests
{
    [Fact]
    public void BuildRoomJoints_IncludesValveElbowTeesAndBranchElbows()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });
        IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildRoomJoints(routing.Segments.ToList());

        Assert.Contains(joints, joint => joint.JointType == PipeJointTypes.Valve);
        Assert.Contains(joints, joint => joint.JointType == PipeJointTypes.Elbow && joint.DiameterInches > 3.0);
        Assert.True(joints.Count(joint => joint.JointType == PipeJointTypes.Tee) >= 2);
        Assert.True(joints.Count(joint =>
            joint.JointType == PipeJointTypes.Elbow && Math.Abs(joint.DiameterInches - 1.25) < 0.01) >= 2);
    }

    [Fact]
    public void BuildRoomJoints_UsesSegmentDiameter_WhenBranchSegmentsAreUpsized()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });
        foreach (PipeSegment segment in routing.Segments.Where(segment =>
                     string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase)))
        {
            segment.DiameterInches = 1.5;
        }

        IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildRoomJoints(routing.Segments.ToList());

        Assert.Contains(
            joints,
            joint => joint.JointType == PipeJointTypes.Elbow && Math.Abs(joint.DiameterInches - 1.5) < 0.01);
        Assert.Contains(
            joints,
            joint => joint.JointType == PipeJointTypes.Tee && Math.Abs(joint.DiameterInches - 1.5) < 0.01);
        Assert.DoesNotContain(
            joints,
            joint => (joint.JointType == PipeJointTypes.Elbow || joint.JointType == PipeJointTypes.Tee)
                && Math.Abs(joint.DiameterInches - 1.25) < 0.01);
    }

    [Fact]
    public void BuildFromRouting_ReturnsEmptyList_WhenNoSegments()
    {
        IList<PipeJoint> joints = SchematicPipeJointBuilder.BuildFromRouting(new SchematicPipeRoutingSummary());
        Assert.Empty(joints);
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
