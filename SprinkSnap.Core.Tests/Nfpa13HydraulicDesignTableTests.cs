using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13HydraulicDesignTableTests
{
    [Theory]
    [InlineData(HazardClassification.LightHazard, 0.10, 1500, 100)]
    [InlineData(HazardClassification.OrdinaryHazardGroup1, 0.15, 1500, 250)]
    [InlineData(HazardClassification.OrdinaryHazardGroup2, 0.20, 1500, 250)]
    [InlineData(HazardClassification.ExtraHazardGroup1, 0.30, 2500, 500)]
    [InlineData(HazardClassification.ExtraHazardGroup2, 0.40, 2500, 500)]
    public void GetCriteria_ReturnsExpectedTableValues(
        string hazard,
        double expectedDensity,
        double expectedRemoteArea,
        double expectedHoseStream)
    {
        Nfpa13HydraulicDesignCriteria criteria = Nfpa13HydraulicDesignTable.GetCriteria(hazard);

        Assert.Equal(expectedDensity, criteria.DesignDensityGpmPerSqFt);
        Assert.Equal(expectedRemoteArea, criteria.RemoteAreaSquareFeet);
        Assert.Equal(expectedHoseStream, criteria.HoseStreamAllowanceGpm);
        Assert.Contains(Nfpa13Edition.Year, criteria.NfpaReference);
        Assert.Contains("19.2.3.1.1", criteria.NfpaReference);
    }

    [Theory]
    [InlineData("OH1", HazardClassification.OrdinaryHazardGroup1)]
    [InlineData("ordinary hazard group 2", HazardClassification.OrdinaryHazardGroup2)]
    [InlineData("EH1", HazardClassification.ExtraHazardGroup1)]
    [InlineData("", HazardClassification.LightHazard)]
    public void NormalizeHazard_MapsAliases(string input, string expected)
    {
        string normalized = Nfpa13HydraulicDesignTable.NormalizeHazard(input);
        Assert.Equal(expected, normalized);
    }
}
