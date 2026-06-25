using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicPipeSizingServiceTests
{
    [Theory]
    [InlineData(60.0, PipeSegmentTypes.Branch, 1.5)]
    [InlineData(30.0, PipeSegmentTypes.Branch, 0.0)]
    [InlineData(800.0, PipeSegmentTypes.Riser, 5.0)]
    public void SuggestCompliantDiameterInches_ReturnsNextStandardSize_WhenCurrentDiameterViolates(
        double flowGpm,
        string segmentType,
        double expectedSuggestedDiameterInches)
    {
        double currentDiameterInches = segmentType == PipeSegmentTypes.Branch ? 1.25 : 4.0;
        double suggestedDiameterInches = HydraulicPipeSizingService.SuggestCompliantDiameterInches(
            flowGpm,
            segmentType,
            currentDiameterInches);

        Assert.Equal(expectedSuggestedDiameterInches, suggestedDiameterInches, 2);
    }

    [Fact]
    public void RoundUpToStandardDiameterInches_UsesNominalPipeSizes()
    {
        Assert.Equal(1.5, HydraulicPipeSizingService.RoundUpToStandardDiameterInches(1.28));
        Assert.Equal(2.0, HydraulicPipeSizingService.RoundUpToStandardDiameterInches(1.51));
        Assert.Equal(12.0, HydraulicPipeSizingService.RoundUpToStandardDiameterInches(11.0));
    }

    [Fact]
    public void ApplySegmentChainSuggestions_AddsWarningsAndCountsSuggestions()
    {
        LayoutLinkedHydraulicPath path = new LayoutLinkedHydraulicPath
        {
            SegmentChain =
            {
                new HydraulicGraphSegment
                {
                    Description = "1.25\" branch drop #1",
                    SegmentType = PipeSegmentTypes.Branch,
                    DiameterInches = 1.25,
                    FlowGpm = 60.0,
                    LengthFeet = 12.0,
                    ExceedsVelocityLimit = true,
                    VelocityLimitFeetPerSecond = 15.0
                }
            }
        };

        int suggestions = HydraulicPipeSizingService.ApplySegmentChainSuggestions(path);

        Assert.Equal(1, suggestions);
        Assert.Equal(1.5, path.SegmentChain[0].SuggestedDiameterInches);
        Assert.Contains(path.Warnings, warning => warning.IndexOf("Suggest upsizing", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Calculate_PropagatesDiameterSuggestions_WhenVelocityViolationsExist()
    {
        RoomInfo room = new RoomInfo
        {
            RevitElementId = 101,
            Number = "101",
            Name = "Office",
            LevelName = "Level 1",
            FloorElevationFeet = 0,
            CeilingElevationFeet = 10,
            DesignerApproved = true,
            ApprovedHazardClassification = HazardClassification.LightHazard,
            ProposedSprinklers =
            {
                new SprinklerPlacementCandidate { Location = new Point3D(10, 12, 8.5) },
                new SprinklerPlacementCandidate { Location = new Point3D(20, 8, 8.5) }
            }
        };
        SchematicPipeRoutingSummary routing = new SchematicPipeRoutingSummary
        {
            Segments = SchematicPipeRouter.RouteRoom(room).ToList()
        };

        LayoutLinkedHydraulicPath path = LayoutLinkedHydraulicCalculator.Calculate(
            new[] { room },
            operatingSprinklerCount: 2,
            designFlowPerSprinklerGpm: 60.0,
            hoseStreamAllowanceGpm: 100.0,
            defaultKFactor: 5.6,
            branchDiameterInches: 1.25,
            mainDiameterInches: 4.0,
            routing,
            null,
            null,
            remoteAreaSquareFeet: 260.0,
            maxCoverageSquareFeet: 130.0);

        Assert.True(path.CriticalPathVelocityViolationCount > 0);
        Assert.True(path.CriticalPathDiameterSuggestionCount > 0);
        Assert.Contains(path.SegmentChain, segment => segment.SuggestedDiameterInches > segment.DiameterInches);
        Assert.Contains(path.CriticalPath, node => node.SuggestedDiameterInches > node.DiameterInches);
    }
}
