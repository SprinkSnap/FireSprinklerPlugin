using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicSupplyAnchorServiceTests
{
    [Fact]
    public void ResolveSourcePoint_UsesUserAnchor_WhenAnchorIsSet()
    {
        RoomInfo room = CreateRoom(101, new Point3D(30, 30, 8.5));
        HydraulicSupplyAnchor anchor = new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = 5001,
            ElementLabel = "Riser 1",
            SupplyPoint = new Point3D(5, 5, 0),
            HeaderPoint = new Point3D(5, 5, 9),
            SourceKind = "UserPick"
        };

        Point3D source = HydraulicSupplyAnchorService.ResolveSourcePoint(
            new[] { room },
            null,
            anchor);

        Assert.Equal(5, source.X);
        Assert.Equal(5, source.Y);
        Assert.Equal(0, source.Z);
    }

    [Fact]
    public void PrepareRouting_RepositionsProjectTrunk_WhenUserAnchorIsSet()
    {
        RoomInfo roomA = CreateRoom(101, new Point3D(10, 10, 8.5));
        RoomInfo roomB = CreateRoom(202, new Point3D(40, 30, 8.5));
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { roomA, roomB });
        HydraulicSupplyAnchor anchor = new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = 9001,
            ElementLabel = "Building Riser",
            SupplyPoint = new Point3D(2, 3, 0),
            HeaderPoint = new Point3D(2, 3, 9),
            SourceKind = "UserPick"
        };

        HydraulicSupplyAnchorService.PrepareRouting(routing, new[] { roomA, roomB }, anchor, null);

        Assert.True(routing.UsesUserSupplyAnchor);
        Assert.True(routing.UsesProjectTrunk);
        Assert.Equal(2, routing.SupplyPoint.X);
        Assert.Equal(3, routing.SupplyPoint.Y);
        PipeSegment buildingRiser = Assert.Single(
            routing.Segments,
            segment =>
                segment.RoomRevitElementId == ProjectTrunkRouter.ProjectScopeRoomRevitElementId
                && string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, buildingRiser.Start.X);
        Assert.Equal(3, buildingRiser.Start.Y);
        Assert.Equal(2, buildingRiser.End.X);
        Assert.Equal(3, buildingRiser.End.Y);
    }

    [Fact]
    public void EnrichFromPlacedPipes_UsesMeasuredEndpoints_ForAnchoredElement()
    {
        HydraulicSupplyAnchor anchor = new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = 7001,
            ElementLabel = "Placed riser",
            SupplyPoint = new Point3D(0, 0, 0),
            HeaderPoint = new Point3D(0, 0, 0),
            SourceKind = "UserPick"
        };
        PipePlacementSummary placement = new PipePlacementSummary
        {
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = 101,
                    PlacedSegments =
                    {
                        new PipePlacementSegmentResult
                        {
                            PlacedElementId = 7001,
                            SegmentType = PipeSegmentTypes.Riser,
                            LengthFeet = 12.0,
                            HasTopology = true,
                            Start = new Point3D(8, 8, 0),
                            End = new Point3D(8, 8, 12)
                        }
                    }
                }
            }
        };

        HydraulicSupplyAnchor enriched = HydraulicSupplyAnchorService.EnrichFromPlacedPipes(anchor, placement);

        Assert.Equal(8, enriched.SupplyPoint.X);
        Assert.Equal(12, enriched.HeaderPoint.Z);
        Assert.Equal("PlacedPipe", enriched.SourceKind);
    }

    [Fact]
    public void Calculate_UsesUserSupplyAnchor_InHydraulicResult()
    {
        RoomInfo room = CreateRoom(101, new Point3D(10, 12, 8.5), new Point3D(20, 8, 8.5));
        room.DesignerApproved = true;
        room.ApprovedHazardClassification = HazardClassification.LightHazard;
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };
        HydraulicSupplyAnchor anchor = new HydraulicSupplyAnchor
        {
            IsSet = true,
            RevitElementId = 8001,
            ElementLabel = "Main Riser",
            SupplyPoint = new Point3D(10, 12, 0),
            HeaderPoint = new Point3D(10, 12, 9),
            SourceKind = "UserPick"
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
            anchor);

        Assert.True(path.UsesUserSupplyAnchor);
        Assert.Equal(10, path.SourcePoint.X);
        Assert.Equal(12, path.SourcePoint.Y);
        Assert.Contains(
            path.Warnings,
            warning => warning.IndexOf("user-selected supply", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static RoomInfo CreateRoom(int id, params Point3D[] headLocations)
    {
        List<SprinklerPlacementCandidate> heads = new List<SprinklerPlacementCandidate>();
        foreach (Point3D location in headLocations)
        {
            heads.Add(new SprinklerPlacementCandidate { Location = location });
        }

        return new RoomInfo
        {
            RevitElementId = id,
            Number = id.ToString(),
            Name = "Room " + id,
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            ProposedSprinklers = heads
        };
    }
}
