using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class RemoteAreaSprinklerSelectorTests
{
    [Fact]
    public void SelectOperatingSprinklers_PrefersRemoteCrossMainBand_OverNearSourceHeads()
    {
        Point3D sourcePoint = new Point3D(0, 10, 0);
        List<LayoutSprinklerPoint> points = CreateLineOfHeads(
            new[] { 10.0, 20.0, 30.0, 40.0 },
            y: 10.0,
            nearSourceHeadY: 25.0);

        IList<LayoutSprinklerPoint> selected = RemoteAreaSprinklerSelector.SelectOperatingSprinklers(
            points,
            sourcePoint,
            operatingSprinklerCount: 3,
            remoteAreaSquareFeet: 260.0,
            maxCoverageSquareFeet: 130.0,
            null);

        Assert.Equal(3, selected.Count);
        Assert.True(selected.All(point => point.Location.X >= 20.0));
        Assert.DoesNotContain(selected, point => point.Location.Y > 15.0);
    }

    [Fact]
    public void SelectOperatingSprinklers_FallsBackToSourceDistance_WhenRemoteAreaInputsMissing()
    {
        Point3D sourcePoint = new Point3D(0, 0, 0);
        List<LayoutSprinklerPoint> points = CreateLineOfHeads(new[] { 5.0, 15.0, 25.0 }, y: 0.0);

        IList<LayoutSprinklerPoint> selected = RemoteAreaSprinklerSelector.SelectOperatingSprinklers(
            points,
            sourcePoint,
            operatingSprinklerCount: 2,
            remoteAreaSquareFeet: 0,
            maxCoverageSquareFeet: 0,
            null);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, point => point.Location.X == 25.0);
        Assert.Contains(selected, point => point.Location.X == 15.0);
    }

    [Fact]
    public void ResolveMostRemoteSprinkler_UsesSchematicPipeRun_NotStraightLineDistance()
    {
        RoomInfo room = CreateRoomWithHeads(
            new Point3D(10, 10, 8.5),
            new Point3D(50, 10, 8.5),
            new Point3D(30, 28, 8.5));
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        Point3D sourcePoint = new Point3D(30, 18, 0);
        List<LayoutSprinklerPoint> points = HydraulicGraphBuilder.CollectSprinklerPoints(new[] { room }, 5.6).ToList();

        LayoutSprinklerPoint mostRemote = RemoteAreaSprinklerSelector.ResolveMostRemoteSprinkler(
            points,
            sourcePoint,
            routing);

        Assert.Equal(50, mostRemote.Location.X, 1);
        Assert.Equal(10, mostRemote.Location.Y, 1);
    }

    [Fact]
    public void Calculate_UsesRemoteAreaSelection_WhenEnginePassesRemoteAreaInputs()
    {
        RoomInfo room = CreateRoomWithHeads(
            new Point3D(10, 12, 8.5),
            new Point3D(20, 8, 8.5),
            new Point3D(30, 12, 8.5),
            new Point3D(40, 8, 8.5));
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
            null,
            null,
            remoteAreaSquareFeet: 260.0,
            maxCoverageSquareFeet: 130.0);

        Assert.True(path.UsesRemoteAreaSelection);
        Assert.Contains(
            path.Warnings,
            warning => warning.IndexOf("remote-area rectangle", System.StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.True(path.MostRemoteSprinkler.Location.X >= path.OperatingSprinklers.Min(point => point.Location.X));
    }

    [Fact]
    public void EstimatePipeRunFeet_IncludesBranchCrossMainAndRiserSegments()
    {
        RoomInfo room = CreateRoomWithHeads(new Point3D(10, 12, 8.5), new Point3D(40, 8, 8.5));
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        LayoutSprinklerPoint remoteHead = HydraulicGraphBuilder.CollectSprinklerPoints(new[] { room }, 5.6)
            .OrderByDescending(point => point.Location.X)
            .First();

        double runFeet = RemoteAreaSprinklerSelector.EstimatePipeRunFeet(
            remoteHead,
            new Point3D(0, 10, 0),
            routing);

        Assert.True(runFeet > 20.0);
    }

    private static List<LayoutSprinklerPoint> CreateLineOfHeads(
        IEnumerable<double> xCoordinates,
        double y,
        double? nearSourceHeadY = null)
    {
        RoomInfo room = CreateRoom();
        List<LayoutSprinklerPoint> points = new List<LayoutSprinklerPoint>();
        int index = 0;
        foreach (double x in xCoordinates)
        {
            points.Add(new LayoutSprinklerPoint
            {
                Room = room,
                Location = new Point3D(x, y, 8.5),
                SprinklerIndex = index++,
                KFactor = 5.6
            });
        }

        if (nearSourceHeadY.HasValue)
        {
            points.Add(new LayoutSprinklerPoint
            {
                Room = room,
                Location = new Point3D(xCoordinates.Min(), nearSourceHeadY.Value, 8.5),
                SprinklerIndex = index,
                KFactor = 5.6
            });
        }

        return points;
    }

    private static RoomInfo CreateRoom()
    {
        return new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            Name = "Office",
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10
        };
    }

    private static RoomInfo CreateRoomWithHeads(params Point3D[] locations)
    {
        RoomInfo room = CreateRoom();
        room.ProposedSprinklers = locations
            .Select(location => new SprinklerPlacementCandidate { Location = location })
            .ToList();
        return room;
    }
}
