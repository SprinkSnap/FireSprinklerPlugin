using FireSprinklerPlugin.SprinkSnap.Core.Materials;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PlacedFittingTakeoffCalculatorTests
{
    [Fact]
    public void CountFittings_UsesPlacedFittingRecords_WhenAvailable()
    {
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            PlacedFittings =
            {
                new PipePlacementFittingResult { JointType = PipeJointTypes.Elbow, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Elbow, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Tee, DiameterInches = 1.25 },
                new PipePlacementFittingResult { JointType = PipeJointTypes.Valve, DiameterInches = 4.0 }
            }
        };

        Assert.True(PlacedFittingTakeoffCalculator.UsesPlacedFittingCounts(placedRoom));
        Assert.Equal(2, PlacedFittingTakeoffCalculator.CountFittings(placedRoom, PipeJointTypes.Elbow, 1.25));
        Assert.Equal(1, PlacedFittingTakeoffCalculator.CountFittings(placedRoom, PipeJointTypes.Tee, 1.25));
        Assert.Equal(1, PlacedFittingTakeoffCalculator.CountValves(placedRoom));
    }

    [Fact]
    public void CountRiserAssemblies_UsesPlacedRiserSegments_WhenAvailable()
    {
        PipePlacementRoomResult placedRoom = new PipePlacementRoomResult
        {
            PlacedSegments =
            {
                new PipePlacementSegmentResult
                {
                    SegmentType = PipeSegmentTypes.Riser,
                    DiameterInches = 4.0,
                    LengthFeet = 12.0
                }
            }
        };

        Assert.Equal(1, PlacedFittingTakeoffCalculator.CountRiserAssemblies(placedRoom, schematicHasRiser: false));
    }
}
