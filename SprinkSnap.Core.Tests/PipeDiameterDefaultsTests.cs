using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PipeDiameterDefaultsTests
{
    [Fact]
    public void ResolveBranchDiameterInches_ReturnsPreferenceValue_WhenConfigured()
    {
        SprinkSnapProjectPreferences preferences = new SprinkSnapProjectPreferences
        {
            DefaultBranchDiameterInches = 1.5
        };

        Assert.Equal(1.5, PipeDiameterDefaults.ResolveBranchDiameterInches(preferences));
    }

    [Fact]
    public void ResolveMainDiameterInches_FallsBackToDefault_WhenPreferenceMissing()
    {
        Assert.Equal(4.0, PipeDiameterDefaults.ResolveMainDiameterInches(null));
        Assert.Equal(4.0, PipeDiameterDefaults.ResolveMainDiameterInches(new SprinkSnapProjectPreferences
        {
            DefaultMainDiameterInches = 0
        }));
    }

    [Fact]
    public void RouteRoom_UsesPreferenceDiameters_WhenProvided()
    {
        RoomInfo room = new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            Name = "Office",
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            ProposedSprinklers =
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
            }
        };

        SprinkSnapProjectPreferences preferences = new SprinkSnapProjectPreferences
        {
            DefaultBranchDiameterInches = 1.5,
            DefaultMainDiameterInches = 6.0
        };

        IList<PipeSegment> segments = SchematicPipeRouter.RouteRoom(room, preferences);

        Assert.Contains(
            segments,
            segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)
                && segment.DiameterInches == 1.5);
        Assert.Contains(
            segments,
            segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase)
                && segment.DiameterInches == 6.0);
    }

    [Fact]
    public void RefreshProjectRouting_UsesPreferenceDiameters_FromProjectState()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState
        {
            Preferences = new SprinkSnapProjectPreferences
            {
                DefaultBranchDiameterInches = 1.5,
                DefaultMainDiameterInches = 6.0
            },
            Rooms =
            {
                new RoomInfo
                {
                    RevitElementId = 101,
                    CeilingElevationFeet = 10,
                    ProposedSprinklers =
                    {
                        new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                        new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
                    }
                }
            }
        };

        SchematicPipeRoutingSummary routing = SchematicPipeRoutingService.RefreshProjectRouting(state);

        Assert.Contains(
            routing.Segments,
            segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, System.StringComparison.OrdinalIgnoreCase)
                && segment.DiameterInches == 1.5);
        Assert.Contains(
            routing.Segments,
            segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Riser, System.StringComparison.OrdinalIgnoreCase)
                && segment.DiameterInches == 6.0);
    }
}
