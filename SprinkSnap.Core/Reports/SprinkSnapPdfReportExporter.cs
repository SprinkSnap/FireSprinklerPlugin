using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FireSprinklerPlugin.SprinkSnap.Core.Reports;

public static class SprinkSnapPdfReportExporter
{
    private static bool licenseConfigured;

    public static ReportExportResult ExportAll(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff,
        ReportExportRequest request)
    {
        EnsureLicense();
        ReportExportResult result = new ReportExportResult();

        if (request == null || string.IsNullOrWhiteSpace(request.OutputFolder))
        {
            result.Errors.Add("Select an output folder before exporting reports.");
            return result;
        }

        string exportFolder = Path.Combine(
            request.OutputFolder,
            "SprinkSnap_Export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(exportFolder);
        result.ExportFolder = exportFolder;

        try
        {
            if (request.IncludeDesignSummary)
            {
                string path = Path.Combine(exportFolder, "SprinkSnap_Design_Summary.pdf");
                GenerateDesignSummary(projectState, path);
                result.ExportedFiles.Add(path);
            }

            if (request.IncludeHydraulicReport)
            {
                string path = Path.Combine(exportFolder, "SprinkSnap_Hydraulic_Report.pdf");
                GenerateHydraulicReport(projectState, hydraulicResult, path);
                result.ExportedFiles.Add(path);
            }

            if (request.IncludeNodeDiagram)
            {
                string path = Path.Combine(exportFolder, "SprinkSnap_Node_Diagram.pdf");
                GenerateNodeDiagram(hydraulicResult, path);
                result.ExportedFiles.Add(path);
            }

            if (request.IncludeMaterialTakeoff)
            {
                string path = Path.Combine(exportFolder, "SprinkSnap_Material_Takeoff.pdf");
                GenerateMaterialTakeoff(materialTakeoff, path);
                result.ExportedFiles.Add(path);
            }

            if (result.ExportedFiles.Count == 0)
            {
                result.Errors.Add("No report types were selected for export.");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private static void EnsureLicense()
    {
        if (licenseConfigured)
        {
            return;
        }

        QuestPDF.Settings.License = LicenseType.Community;
        licenseConfigured = true;
    }

    private static void GenerateDesignSummary(SprinkSnapProjectState projectState, string outputPath)
    {
        List<RoomInfo> rooms = projectState.Rooms?.ToList() ?? new List<RoomInfo>();
        ClashDetectionSummary clashSummary = projectState.ClashSummary ?? new ClashDetectionSummary();
        SprinklerPlacementSummary placementSummary = projectState.PlacementSummary ?? new SprinklerPlacementSummary();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Element(header => ComposeHeader(header, "Design Summary"));
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("Project overview").FontSize(14).SemiBold();
                    column.Item().Text("Rooms analyzed: " + rooms.Count);
                    column.Item().Text("Linked models: " + projectState.ModelAnalysis.LinkedModelCount);
                    column.Item().Text("Existing sprinklers in model: " + projectState.ModelAnalysis.ExistingSprinklerCount);
                    column.Item().Text("Total clashes: " + clashSummary.TotalClashes + " (host " + clashSummary.HostClashCount + ", linked " + clashSummary.LinkedClashCount + ")");
                    column.Item().Text("Sprinklers placed in Revit: " + placementSummary.PlacedCount);

                    column.Item().PaddingTop(8).Text("Room design table").FontSize(14).SemiBold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(45);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Room");
                            header.Cell().Element(CellStyle).Text("Name");
                            header.Cell().Element(CellStyle).Text("Hazard");
                            header.Cell().Element(CellStyle).Text("Sprinkler");
                            header.Cell().Element(CellStyle).Text("Layout");
                            header.Cell().Element(CellStyle).Text("Heads");
                        });

                        foreach (RoomInfo room in rooms.OrderBy(item => item.Number))
                        {
                            table.Cell().Element(CellStyle).Text(room.Number);
                            table.Cell().Element(CellStyle).Text(room.Name);
                            table.Cell().Element(CellStyle).Text(room.ApprovedHazardClassification);
                            table.Cell().Element(CellStyle).Text(string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
                                ? room.AutoSelectedSprinklerName
                                : room.SelectedSprinklerFamilyName);
                            table.Cell().Element(CellStyle).Text(room.LayoutStatus);
                            table.Cell().Element(CellStyle).Text(room.ProposedSprinklers.Count.ToString());
                        }
                    });
                });
                page.Footer().Element(footer => ComposeFooter(footer, "NFPA 13 Chapters 5, 10, and 19 — designer review required."));
            });
        }).GeneratePdf(outputPath);
    }

    private static void GenerateHydraulicReport(
        SprinkSnapProjectState projectState,
        HydraulicCalculationResult hydraulicResult,
        string outputPath)
    {
        WaterSupplyInput supply = projectState.WaterSupply ?? new WaterSupplyInput();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Element(header => ComposeHeader(header, "Hydraulic Calculation Report"));
                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Controlling hazard: " + hydraulicResult.ControllingHazardClassification).FontSize(12).SemiBold();
                    column.Item().Text(hydraulicResult.NfpaReference);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        AddRow(table, "Design density", hydraulicResult.DesignDensityGpmPerSqFt.ToString("N2") + " gpm/sq ft");
                        AddRow(table, "Remote area", hydraulicResult.RemoteAreaSquareFeet.ToString("N0") + " sq ft");
                        AddRow(table, "Sprinkler demand", hydraulicResult.SprinklerDemandFlowGpm.ToString("N1") + " GPM");
                        AddRow(table, "Hose stream allowance", hydraulicResult.HoseStreamAllowanceGpm.ToString("N0") + " GPM");
                        AddRow(table, "Total calculated flow", hydraulicResult.TotalFlowGpm.ToString("N1") + " GPM");
                        AddRow(table, "Equivalent K-factor", hydraulicResult.EquivalentKFactor.ToString("N1"));
                        AddRow(table, "System demand pressure", hydraulicResult.SystemDemandPsi.ToString("N1") + " PSI");
                        AddRow(table, "Available residual pressure", hydraulicResult.AvailablePressurePsi.ToString("N1") + " PSI");
                        AddRow(table, "Safety margin", hydraulicResult.SafetyMarginPsi.ToString("N1") + " PSI");
                        AddRow(table, "Static pressure (test)", FormatNullable(supply.StaticPressurePsi, "PSI"));
                        AddRow(table, "Residual pressure (test)", FormatNullable(supply.ResidualPressurePsi, "PSI"));
                        AddRow(table, "Flow at residual", FormatNullable(supply.FlowAtResidualGpm, "GPM"));
                    });

                    if (hydraulicResult.Warnings.Count > 0)
                    {
                        column.Item().PaddingTop(10).Text("Warnings").FontSize(12).SemiBold();
                        foreach (string warning in hydraulicResult.Warnings)
                        {
                            column.Item().Text("• " + warning);
                        }
                    }
                });
                page.Footer().Element(footer => ComposeFooter(footer, "NFPA 13 Chapter 19 — hydraulic calculation summary for designer review."));
            });
        }).GeneratePdf(outputPath);
    }

    private static void GenerateNodeDiagram(HydraulicCalculationResult hydraulicResult, string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Element(header => ComposeHeader(header, "Hydraulic Node Diagram"));
                page.Content().Column(column =>
                {
                    column.Item().Text("Simplified critical path from remote sprinkler to source.").Italic();
                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Node");
                            header.Cell().Element(CellStyle).Text("Pressure (PSI)");
                            header.Cell().Element(CellStyle).Text("Flow (GPM)");
                        });

                        foreach (HydraulicNode node in hydraulicResult.CriticalPath ?? new List<HydraulicNode>())
                        {
                            table.Cell().Element(CellStyle).Text(node.NodeId);
                            table.Cell().Element(CellStyle).Text(node.PressurePsi.ToString("N1"));
                            table.Cell().Element(CellStyle).Text(node.FlowGpm.ToString("N1"));
                        }
                    });
                });
                page.Footer().Element(footer => ComposeFooter(footer, "Schematic summary — not a stamped hydraulic node diagram."));
            });
        }).GeneratePdf(outputPath);
    }

    private static void GenerateMaterialTakeoff(IReadOnlyList<MaterialTakeoffItem> materialTakeoff, string outputPath)
    {
        List<MaterialTakeoffItem> items = materialTakeoff?.ToList() ?? new List<MaterialTakeoffItem>();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Element(header => ComposeHeader(header, "Material Takeoff"));
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Type");
                        header.Cell().Element(CellStyle).Text("Description");
                        header.Cell().Element(CellStyle).Text("Quantity");
                        header.Cell().Element(CellStyle).Text("Unit");
                    });

                    foreach (MaterialTakeoffItem item in items)
                    {
                        table.Cell().Element(CellStyle).Text(item.ItemType);
                        table.Cell().Element(CellStyle).Text(item.Description);
                        table.Cell().Element(CellStyle).Text(item.Quantity.ToString("N0"));
                        table.Cell().Element(CellStyle).Text(item.Unit);
                    }
                });
                page.Footer().Element(footer => ComposeFooter(footer, "Preliminary takeoff — verify in Revit before procurement."));
            });
        }).GeneratePdf(outputPath);
    }

    private static void ComposeHeader(IContainer container, string title)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("SprinkSnap AI").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text(title).FontSize(12);
            });
            row.ConstantItem(120).AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        });
    }

    private static void ComposeFooter(IContainer container, string note)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span(note).FontSize(8);
            text.Span(" | Exported ").FontSize(8);
            text.Span(DateTime.Now.ToString("f")).FontSize(8);
        });
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellStyle).Text(label).SemiBold();
        table.Cell().Element(CellStyle).Text(value);
    }

    private static string FormatNullable(double? value, string unit)
    {
        return value.HasValue ? value.Value.ToString("N1") + " " + unit : "Not entered";
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(3);
    }
}
