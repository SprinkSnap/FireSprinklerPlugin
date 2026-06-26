using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class Nfpa13CodeReferenceLibraryTests
{
    [Theory]
    [InlineData(HazardClassification.LightHazard, "4.3.2")]
    [InlineData("OH1", "4.3.3.1")]
    [InlineData("OH2", "4.3.3.2")]
    [InlineData("EH1", "4.3.4.1")]
    [InlineData("EH2", "4.3.4.2")]
    public void GetHazardReference_UsesNfpa13_2025Sections(string hazard, string expectedSectionNumber)
    {
        Nfpa13CodeReference reference = Nfpa13CodeReferenceLibrary.GetHazardReference(hazard);

        Assert.Equal(Nfpa13Edition.Year, reference.EditionYear);
        Assert.Contains(Nfpa13Edition.Year, reference.Section);
        Assert.Contains(expectedSectionNumber, reference.Section);
    }

    [Fact]
    public void GetDesignCriteriaReference_UsesTable1920311()
    {
        Nfpa13CodeReference reference = Nfpa13CodeReferenceLibrary.GetDesignCriteriaReference();

        Assert.Contains("19.2.3.1.1", reference.Section);
        Assert.Contains(Nfpa13Edition.Year, reference.Section);
    }

    [Fact]
    public void GetHydraulicsReference_IncludesGraphSheetSection()
    {
        Nfpa13CodeReference reference = Nfpa13CodeReferenceLibrary.GetHydraulicsReference();

        Assert.Contains("28.2", reference.Section);
        Assert.Contains("28.4.1.4", reference.Summary);
    }

    [Fact]
    public void GetAllReferences_Includes2025EditionOnEveryEntry()
    {
        foreach (Nfpa13CodeReference reference in Nfpa13CodeReferenceLibrary.GetAllReferences())
        {
            Assert.Equal(Nfpa13Edition.Year, reference.EditionYear);
            Assert.Contains(Nfpa13Edition.Year, reference.Section);
        }
    }
}
