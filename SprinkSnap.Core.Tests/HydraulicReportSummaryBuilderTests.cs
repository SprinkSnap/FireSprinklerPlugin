using System.Collections.Generic;
using System.IO;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Reports;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class HydraulicReportSummaryBuilderTests
{
    [Fact]
    public void BuildRows_IncludesParityFields_WhenHydraulicFlagsAreSet()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        HydraulicCalculationResult result = new HydraulicCalculationResult
        {
            UsesSegmentGraphHydraulics = true,
            UsesRemoteAreaSelection = true,
            UsesPlacedPipeTopology = true,
            UsesProjectTrunk = true,
            CriticalPathFittingCount = 7
        };

        Dictionary<string, string> rows = HydraulicReportSummaryBuilder.BuildRows(state, result)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal("Yes", rows["Segment graph hydraulics"]);
        Assert.Equal("Yes", rows["Remote area selection"]);
        Assert.Equal("Yes", rows["Placed pipe topology"]);
        Assert.Equal("Yes", rows["Project trunk"]);
        Assert.Equal("7", rows["Critical path fitting count"]);
    }

    [Fact]
    public void BuildRows_ReportsNo_WhenHydraulicFlagsAreUnset()
    {
        SprinkSnapProjectState state = new SprinkSnapProjectState();
        HydraulicCalculationResult result = new HydraulicCalculationResult();

        Dictionary<string, string> rows = HydraulicReportSummaryBuilder.BuildRows(state, result)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal("No", rows["Segment graph hydraulics"]);
        Assert.Equal("No", rows["Remote area selection"]);
        Assert.Equal("No", rows["Placed pipe topology"]);
        Assert.Equal("No", rows["Project trunk"]);
        Assert.Equal("0", rows["Critical path fitting count"]);
    }

    [Fact]
    public void BuildNodeDiagramMethodologySummary_IncludesParityFlags()
    {
        HydraulicCalculationResult result = new HydraulicCalculationResult
        {
            UsesSegmentGraphHydraulics = true,
            UsesRemoteAreaSelection = true,
            UsesPlacedPipeTopology = true,
            UsesProjectTrunk = true,
            CriticalPathFittingCount = 4
        };

        string summary = HydraulicReportSummaryBuilder.BuildNodeDiagramMethodologySummary(result);

        Assert.Contains("segment graph Yes", summary);
        Assert.Contains("remote area selection Yes", summary);
        Assert.Contains("placed pipe topology Yes", summary);
        Assert.Contains("project trunk Yes", summary);
        Assert.Contains("critical-path fittings 4", summary);
    }
}

public sealed class SprinkSnapPdfReportExporterTests
{
    [Fact]
    public void ExportAll_GeneratesHydraulicReportPdf_WithParityFields()
    {
        string outputFolder = Path.Combine(Path.GetTempPath(), "SprinkSnap_ReportTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(outputFolder);

        try
        {
            SprinkSnapProjectState state = new SprinkSnapProjectState
            {
                WaterSupply = new WaterSupplyInput
                {
                    StaticPressurePsi = 80,
                    ResidualPressurePsi = 65,
                    FlowAtResidualGpm = 1200
                }
            };

            HydraulicCalculationResult result = new HydraulicCalculationResult
            {
                ControllingHazardClassification = "Light Hazard",
                NfpaReference = "NFPA 13 test",
                TotalFlowGpm = 150,
                UsesSegmentGraphHydraulics = true,
                UsesRemoteAreaSelection = true,
                UsesPlacedPipeTopology = true,
                UsesProjectTrunk = true,
                CriticalPathFittingCount = 5
            };

            ReportExportRequest request = new ReportExportRequest
            {
                OutputFolder = outputFolder,
                IncludeDesignSummary = false,
                IncludeHydraulicReport = true,
                IncludeNodeDiagram = true,
                IncludeMaterialTakeoff = false
            };

            ReportExportResult exportResult = SprinkSnapPdfReportExporter.ExportAll(
                state,
                result,
                new List<MaterialTakeoffItem>(),
                request);

            Assert.Empty(exportResult.Errors);
            Assert.Equal(2, exportResult.ExportedFiles.Count);
            Assert.All(exportResult.ExportedFiles, path => Assert.True(File.Exists(path)));
            Assert.Contains(exportResult.ExportedFiles, path => path.EndsWith("SprinkSnap_Hydraulic_Report.pdf"));
            Assert.Contains(exportResult.ExportedFiles, path => path.EndsWith("SprinkSnap_Node_Diagram.pdf"));
        }
        finally
        {
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, recursive: true);
            }
        }
    }
}
