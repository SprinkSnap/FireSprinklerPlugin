using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Materials;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SegmentDrivenTakeoffTests
{
    [Fact]
    public void MaterialTakeoff_UsesPlacedPipeLength_WhenSchematicBranchIsUpsized()
    {
        RoomInfo room = CreateRoomWithTwoHeads();
        SchematicPipeRoutingSummary routing = SchematicPipeRouter.RouteProject(new[] { room });
        PipeSegment branchDrop = routing.Segments.First(segment =>
            (segment.Description ?? string.Empty).IndexOf("branch drop #1", System.StringComparison.OrdinalIgnoreCase) >= 0);
        branchDrop.DiameterInches = 1.5;
        branchDrop.Description = "1.5\" branch drop #1";

        PipePlacementSummary placementSummary = new PipePlacementSummary
        {
            PlacedSegmentCount = 1,
            RoomResults =
            {
                new PipePlacementRoomResult
                {
                    RoomRevitElementId = room.RevitElementId,
                    RoomNumber = room.Number,
                    PlacedSegmentCount = 1,
                    PlacedSegments =
                    {
                        new PipePlacementSegmentResult
                        {
                            SegmentType = PipeSegmentTypes.Branch,
                            DiameterInches = 1.25,
                            Description = "1.25\" branch drop #1",
                            LengthFeet = 13.0
                        }
                    }
                }
            }
        };

        MaterialTakeoffEngine engine = new MaterialTakeoffEngine();
        IReadOnlyList<MaterialTakeoffItem> items = engine.Generate(
            new[] { room },
            null,
            routing,
            placementSummary);

        MaterialTakeoffItem pipeRow = items.First(item =>
            string.Equals(item.ItemType, "Pipe", System.StringComparison.OrdinalIgnoreCase)
            && item.FamilyName.IndexOf("1.5", System.StringComparison.OrdinalIgnoreCase) >= 0
            && item.FamilyName.IndexOf("Branch", System.StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.Equal("Placed", pipeRow.Source);
        Assert.Equal(13.0, pipeRow.Quantity);
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
            DesignerApproved = true,
            ApprovedHazardClassification = HazardClassification.LightHazard,
            ProposedSprinklers = new List<SprinklerPlacementCandidate>
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
            }
        };
    }
}
