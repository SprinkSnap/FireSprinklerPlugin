using FireSprinklerPlugin.SprinkSnap.Core.Materials;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PlacedPipeTakeoffCalculatorTests
{
    [Fact]
    public void ResolvePipeLengthFeet_UsesMeasuredSegmentLengths_WhenPlacedSegmentsExist()
    {
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            PlacedSegmentCount = 2,
            PlacedLengthFeet = 42.5,
            PlacedSegments =
            {
                new PipePlacementSegmentResult
                {
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    LengthFeet = 12.5
                },
                new PipePlacementSegmentResult
                {
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    LengthFeet = 8.0
                },
                new PipePlacementSegmentResult
                {
                    SegmentType = PipeSegmentTypes.CrossMain,
                    DiameterInches = 4.0,
                    LengthFeet = 22.0
                }
            }
        };

        double branchLength = PlacedPipeTakeoffCalculator.ResolvePipeLengthFeet(
            PipeSegmentTypes.Branch,
            1.25,
            18.0,
            placedRoom);
        double crossMainLength = PlacedPipeTakeoffCalculator.ResolvePipeLengthFeet(
            PipeSegmentTypes.CrossMain,
            4.0,
            20.0,
            placedRoom);

        Assert.Equal(20.5, branchLength);
        Assert.Equal(22.0, crossMainLength);
        Assert.True(PlacedPipeTakeoffCalculator.UsesPlacedLengthForGroup(
            PipeSegmentTypes.CrossMain,
            4.0,
            placedRoom));
    }

    [Fact]
    public void ResolvePipeLengthFeet_FallsBackToSchematic_WhenNoPlacedSegments()
    {
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            PlacedSegmentCount = 1,
            PlacedLengthFeet = 10.0
        };

        double length = PlacedPipeTakeoffCalculator.ResolvePipeLengthFeet(
            PipeSegmentTypes.Riser,
            4.0,
            15.5,
            placedRoom);

        Assert.Equal(15.5, length);
        Assert.False(PlacedPipeTakeoffCalculator.UsesPlacedLengthForGroup(
            PipeSegmentTypes.Riser,
            4.0,
            placedRoom));
    }

    [Fact]
    public void ResolvePipeLengthFeet_MatchesPlacedSegmentByDescription_WhenSchematicDiameterIsUpsized()
    {
        PipeSegment schematicSegment = new PipeSegment
        {
            RoomRevitElementId = 101,
            SegmentType = PipeSegmentTypes.Branch,
            DiameterInches = 1.5,
            Description = "1.5\" branch drop #1",
            LengthFeet = 10.0
        };
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            RoomRevitElementId = 101,
            PlacedSegmentCount = 1,
            PlacedSegments =
            {
                new PipePlacementSegmentResult
                {
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    Description = "1.25\" branch drop #1",
                    LengthFeet = 11.5
                }
            }
        };

        double length = PlacedPipeTakeoffCalculator.ResolvePipeLengthFeet(schematicSegment, 10.0, placedRoom);

        Assert.Equal(11.5, length);
        Assert.True(PlacedPipeTakeoffCalculator.UsesPlacedLength(schematicSegment, placedRoom));
    }
}
