using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13HighCeilingDesignCriteriaAdjusterTests
{
    [Fact]
    public void Apply_OrdinaryHazardGroup1_IncreasesRemoteArea_WhenCeilingExceeds30Feet()
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.OrdinaryHazardGroup1);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            HazardClassification.OrdinaryHazardGroup1,
            baseCriteria,
            ceilingHeightFeet: 35,
            representativeKFactor: 5.6,
            usesExtendedCoverageK252OrGreater: false);

        Assert.True(adjusted.AppliesHighCeilingAdjustment);
        Assert.Equal(0.15, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(1950, adjusted.RemoteAreaSquareFeet, 0.1);
        Assert.Contains("19.2.3.2.5.2", adjusted.NfpaReference);
    }

    [Fact]
    public void Apply_OrdinaryHazardGroup2_Uses037Density_WhenCeilingBetween30And40Feet()
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.OrdinaryHazardGroup2);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            HazardClassification.OrdinaryHazardGroup2,
            baseCriteria,
            ceilingHeightFeet: 35,
            representativeKFactor: 5.6,
            usesExtendedCoverageK252OrGreater: false);

        Assert.True(adjusted.AppliesHighCeilingAdjustment);
        Assert.Equal(0.37, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(1500, adjusted.RemoteAreaSquareFeet);
    }

    [Fact]
    public void Apply_OrdinaryHazardGroup2_IncreasesDensityAndArea_WhenCeilingExceeds40Feet()
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.OrdinaryHazardGroup2);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            HazardClassification.OrdinaryHazardGroup2,
            baseCriteria,
            ceilingHeightFeet: 45,
            representativeKFactor: 5.6,
            usesExtendedCoverageK252OrGreater: false);

        Assert.Equal(0.45, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(1950, adjusted.RemoteAreaSquareFeet, 0.1);
    }

    [Fact]
    public void Apply_OrdinaryHazardGroup2_SkipsAreaIncrease_WhenExtendedCoverageK252IsUsed()
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.OrdinaryHazardGroup2);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            HazardClassification.OrdinaryHazardGroup2,
            baseCriteria,
            ceilingHeightFeet: 45,
            representativeKFactor: 25.2,
            usesExtendedCoverageK252OrGreater: true);

        Assert.Equal(0.45, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(1500, adjusted.RemoteAreaSquareFeet);
        Assert.Contains("K-25.2", adjusted.HighCeilingAdjustmentSummary);
    }

    [Theory]
    [InlineData(HazardClassification.ExtraHazardGroup1, 0.30)]
    [InlineData(HazardClassification.ExtraHazardGroup2, 0.40)]
    public void Apply_ExtraHazard_IncreasesDensityTo045_WhenCeilingExceeds30Feet(
        string hazard,
        double baseDensity)
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(hazard);
        Assert.Equal(baseDensity, baseCriteria.DesignDensityGpmPerSqFt);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            hazard,
            baseCriteria,
            ceilingHeightFeet: 32,
            representativeKFactor: 11.2,
            usesExtendedCoverageK252OrGreater: false);

        Assert.True(adjusted.AppliesHighCeilingAdjustment);
        Assert.Equal(0.45, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(baseCriteria.RemoteAreaSquareFeet, adjusted.RemoteAreaSquareFeet);
    }

    [Fact]
    public void Apply_LightHazard_LeavesTableValues_WhenCeilingExceeds30Feet()
    {
        Nfpa13HydraulicDesignCriteria baseCriteria = Nfpa13HydraulicDesignTable.GetCriteria(HazardClassification.LightHazard);

        Nfpa13HydraulicDesignCriteria adjusted = Nfpa13HighCeilingDesignCriteriaAdjuster.Apply(
            HazardClassification.LightHazard,
            baseCriteria,
            ceilingHeightFeet: 35,
            representativeKFactor: 5.6,
            usesExtendedCoverageK252OrGreater: false);

        Assert.False(adjusted.AppliesHighCeilingAdjustment);
        Assert.Equal(baseCriteria.DesignDensityGpmPerSqFt, adjusted.DesignDensityGpmPerSqFt);
        Assert.Equal(baseCriteria.RemoteAreaSquareFeet, adjusted.RemoteAreaSquareFeet);
    }

    [Fact]
    public void UsesExtendedCoverageK252OrGreater_RequiresExtendedCoverageFamily()
    {
        SprinklerFamilyInfo standardFamily = new SprinklerFamilyInfo
        {
            KFactor = 25.2,
            CoverageType = "Standard Coverage"
        };
        SprinklerFamilyInfo extendedFamily = new SprinklerFamilyInfo
        {
            KFactor = 25.2,
            CoverageType = "Extended Coverage"
        };

        Assert.False(Nfpa13HighCeilingDesignCriteriaAdjuster.UsesExtendedCoverageK252OrGreater(standardFamily));
        Assert.True(Nfpa13HighCeilingDesignCriteriaAdjuster.UsesExtendedCoverageK252OrGreater(extendedFamily));
    }
}
