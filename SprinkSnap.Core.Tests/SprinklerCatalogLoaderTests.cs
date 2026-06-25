using System.IO;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SprinklerCatalogLoaderTests
{
    [Fact]
    public void Load_ReadsCustomCatalogJson()
    {
        string path = Path.Combine(TestDataPath.Resolve(), "test_sprinkler_catalog.json");
        SprinklerCatalogLoadResult result = SprinklerCatalogLoader.Load(path);

        Assert.Equal("External", result.SourceKind);
        Assert.Equal("Unit Test Catalog", result.LibraryName);
        Assert.Single(result.Families);
        Assert.Equal("Test:VK999:Pendent:K5.6", result.Families[0].ListedFamilyId);
        Assert.Equal(5.6, result.Families[0].KFactor);
        Assert.Equal("Unit test catalog entry.", result.Families[0].ListingNotes);
        Assert.Contains("Standard Spray Quick Response", result.Categories);
    }

    [Fact]
    public void Load_UsesBuiltInFallback_WhenCatalogJsonIsInvalid()
    {
        string path = Path.Combine(TestDataPath.Resolve(), "invalid-catalog.json");
        File.WriteAllText(path, "{ not valid json ");

        try
        {
            SprinklerCatalogLoadResult result = SprinklerCatalogLoader.Load(path);
            Assert.NotEmpty(result.Families);
            Assert.Contains(result.Messages, message =>
                message.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Failed to load", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReadsShippedCatalog_WhenDefaultPathExists()
    {
        string shippedCatalog = Path.GetFullPath(Path.Combine(
            TestDataPath.Resolve(),
            "..",
            "..",
            "..",
            "..",
            "SprinkSnap.Core",
            "Data",
            "sprinkler_catalog.json"));

        if (!File.Exists(shippedCatalog))
        {
            return;
        }

        SprinklerCatalogLoadResult result = SprinklerCatalogLoader.Load(shippedCatalog);
        Assert.True(result.Families.Count >= 10);
        Assert.Contains(result.Families, family => family.Manufacturer == "Viking" && family.Model == "VK302");
    }
}
