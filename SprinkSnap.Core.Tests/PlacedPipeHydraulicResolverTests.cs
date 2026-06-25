using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PlacedPipeHydraulicResolverTests
{
    [Fact]
    public void Resolve_UsesPlacedSegmentLengths_WhenPlacedDataExists()
    {
        PipePlacementSummary placementSummary = new PipePlacementSummary
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
                            SegmentType = PipeSegmentTypes.Branch,
                            DiameterInches = 1.25,
                            LengthFeet = 18.0
                        },
                        new PipePlacementSegmentResult
                        {
                            SegmentType = PipeSegmentTypes.CrossMain,
                            DiameterInches = 4.0,
                            LengthFeet = 42.0
                        }
                    }
                }
            }
        };

        HydraulicPipeLengthSource source = PlacedPipeHydraulicResolver.Resolve(
            101,
            placementSummary,
            null,
            1.25,
            4.0);

        Assert.True(source.UsesPlacedPipeLengths);
        Assert.Equal("Placed", source.DataSource);
        Assert.Equal(18.0, source.BranchLengthFeet);
        Assert.Equal(42.0, source.MainLengthFeet);
        Assert.Equal(1.25, source.BranchDiameterInches);
        Assert.Equal(4.0, source.MainDiameterInches);
    }

    [Fact]
    public void Resolve_FallsBackToSchematic_WhenNoPlacedSegmentsExist()
    {
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments =
            {
                new PipeSegment
                {
                    RoomRevitElementId = 202,
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    LengthFeet = 12.0
                },
                new PipeSegment
                {
                    RoomRevitElementId = 202,
                    SegmentType = PipeSegmentTypes.Riser,
                    DiameterInches = 4.0,
                    LengthFeet = 30.0
                }
            }
        };

        HydraulicPipeLengthSource source = PlacedPipeHydraulicResolver.Resolve(
            202,
            new PipePlacementSummary(),
            routing,
            1.25,
            4.0);

        Assert.False(source.UsesPlacedPipeLengths);
        Assert.Equal("Schematic", source.DataSource);
        Assert.Equal(12.0, source.BranchLengthFeet);
        Assert.Equal(30.0, source.MainLengthFeet);
    }
}
