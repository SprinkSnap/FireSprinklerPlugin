using System.IO;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydrantTestCsvImporterTests
{
    [Theory]
    [InlineData("hydrant_header_row.csv")]
    [InlineData("hydrant_key_value.csv")]
    [InlineData("hydrant_positional.csv")]
    public void Import_ParsesKnownCsvFormats(string fileName)
    {
        string path = Path.Combine(TestDataPath.Resolve(), fileName);
        HydrantTestImportResult result = HydrantTestCsvImporter.Import(path);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(85, result.Input.StaticPressurePsi);
        Assert.Equal(65, result.Input.ResidualPressurePsi);
        Assert.Equal(1200, result.Input.FlowAtResidualGpm);
        Assert.Equal(new DateTime(2024, 4, 15), result.Input.HydrantTestDate);
    }

    [Fact]
    public void Import_ReturnsError_WhenFileDoesNotExist()
    {
        HydrantTestImportResult result = HydrantTestCsvImporter.Import(Path.Combine(TestDataPath.Resolve(), "missing.csv"));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.IndexOf("existing CSV", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Import_ReturnsError_WhenRequiredColumnsAreMissing()
    {
        string path = Path.Combine(TestDataPath.Resolve(), "hydrant_invalid.csv");
        File.WriteAllText(path, "Column A,Column B\n1,2");

        try
        {
            HydrantTestImportResult result = HydrantTestCsvImporter.Import(path);
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
