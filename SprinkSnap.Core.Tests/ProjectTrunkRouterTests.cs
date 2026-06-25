using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class ProjectTrunkRouterTests
{
    [Fact]
    public void AppendProjectTrunk_AddsBuildingRiserAndInterRoomMains_ForTwoRoomsOnSameLevel()
    {
        RoomInfo roomA = CreateRoom(101, "101", new Point3D(10, 10, 8.5));
        RoomInfo roomB = CreateRoom(202, "202", new Point3D(40, 30, 8.5));
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { roomA, roomB });

        Assert.True(routing.UsesProjectTrunk);
        Assert.Contains(
            routing.Segments,
            segment =>
                segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            routing.Segments,
            segment =>
                segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Main, System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, routing.ProjectTrunkRoomRevitElementIds.Count);
    }

    [Fact]
    public void Calculate_UsesProjectTrunkAndSharedSource_ForMultiRoomOperatingArea()
    {
        RoomInfo roomA = CreateRoom(101, "101", new Point3D(10, 10, 8.5), new Point3D(18, 14, 8.5));
        RoomInfo roomB = CreateRoom(202, "202", new Point3D(40, 30, 8.5), new Point3D(48, 34, 8.5));
        roomA.DesignerApproved = true;
        roomB.DesignerApproved = true;
        roomA.ApprovedHazardClassification = HazardClassification.LightHazard;
        roomB.ApprovedHazardClassification = HazardClassification.LightHazard;

        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { roomA, roomB });

        LayoutLinkedHydraulicPath path = LayoutLinkedHydraulicCalculator.Calculate(
            new[] { roomA, roomB },
            operatingSprinklerCount: 3,
            designFlowPerSprinklerGpm: 8.0,
            hoseStreamAllowanceGpm: 100.0,
            defaultKFactor: 5.6,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            routing,
            null);

        Assert.True(path.UsesProjectTrunk);
        Assert.True(path.UsesSegmentGraphHydraulics);
        Assert.Equal(routing.SupplyPoint.X, path.SourcePoint.X);
        Assert.Equal(routing.SupplyPoint.Y, path.SourcePoint.Y);
        Assert.Contains(
            path.SegmentChain,
            segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Main, System.StringComparison.OrdinalIgnoreCase)
                && (segment.Description ?? string.Empty).IndexOf("project trunk", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(
            path.SegmentChain,
            segment =>
                string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase)
                && (segment.Description ?? string.Empty).IndexOf("building riser", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(
            path.Warnings,
            warning => warning.IndexOf("Segment graph traces the most remote head", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void ResolveSourcePoint_UsesSupplyPoint_WhenProjectTrunkExists()
    {
        RoomInfo roomA = CreateRoom(101, "101", new Point3D(10, 10, 8.5));
        RoomInfo roomB = CreateRoom(202, "202", new Point3D(40, 30, 8.5));
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { roomA, roomB });

        Point3D source = HydraulicGraphBuilder.ResolveSourcePoint(new[] { roomA, roomB }, routing);

        Assert.Equal(routing.SupplyPoint.X, source.X);
        Assert.Equal(routing.SupplyPoint.Y, source.Y);
        Assert.Equal(routing.SupplyPoint.Z, source.Z);
    }

    private static RoomInfo CreateRoom(int id, string number, params Point3D[] headLocations)
    {
        return new RoomInfo
        {
            RevitElementId = id,
            Number = number,
            Name = "Room " + number,
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            ProposedSprinklers = headLocations
                .Select(location => new SprinklerPlacementCandidate { Location = location })
                .ToList()
        };
    }
}
