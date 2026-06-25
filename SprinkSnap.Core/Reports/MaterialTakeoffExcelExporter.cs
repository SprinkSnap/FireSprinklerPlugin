using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Reports;

public static class MaterialTakeoffExcelExporter
{
    public static void Export(IReadOnlyList<MaterialTakeoffItem> materialTakeoff, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        List<MaterialTakeoffItem> items = materialTakeoff?.ToList() ?? new List<MaterialTakeoffItem>();
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using XLWorkbook workbook = new XLWorkbook();
        IXLWorksheet worksheet = workbook.Worksheets.Add("Material Takeoff");

        worksheet.Cell(1, 1).Value = "SprinkSnap AI — Material Takeoff";
        worksheet.Range(1, 1, 1, 11).Merge().Style
            .Font.SetBold()
            .Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        worksheet.Cell(2, 1).Value = "Exported";
        worksheet.Cell(2, 2).Value = DateTime.Now;
        worksheet.Cell(2, 2).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

        int headerRow = 4;
        string[] headers =
        {
            "Type",
            "Room #",
            "Room Name",
            "Level",
            "Manufacturer",
            "Family",
            "Hazard",
            "Source",
            "Description",
            "Quantity",
            "Unit"
        };

        for (int column = 0; column < headers.Length; column++)
        {
            IXLCell headerCell = worksheet.Cell(headerRow, column + 1);
            headerCell.Value = headers[column];
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
            headerCell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        int row = headerRow + 1;
        foreach (MaterialTakeoffItem item in items)
        {
            worksheet.Cell(row, 1).Value = item.ItemType;
            worksheet.Cell(row, 2).Value = item.RoomNumber;
            worksheet.Cell(row, 3).Value = item.RoomName;
            worksheet.Cell(row, 4).Value = item.LevelName;
            worksheet.Cell(row, 5).Value = item.Manufacturer;
            worksheet.Cell(row, 6).Value = item.FamilyName;
            worksheet.Cell(row, 7).Value = item.HazardClassification;
            worksheet.Cell(row, 8).Value = item.Source;
            worksheet.Cell(row, 9).Value = item.Description;
            worksheet.Cell(row, 10).Value = item.Quantity;
            worksheet.Cell(row, 11).Value = item.Unit;

            if (item.IsSummaryRow)
            {
                worksheet.Range(row, 1, row, 11).Style.Font.Bold = true;
                worksheet.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
            }

            row++;
        }

        if (items.Count > 0)
        {
            worksheet.Range(headerRow + 1, 10, row - 1, 10).Style.NumberFormat.Format = "#,##0";
        }

        worksheet.Columns(1, headers.Length).AdjustToContents();
        worksheet.SheetView.FreezeRows(headerRow);
        workbook.SaveAs(outputPath);
    }
}
