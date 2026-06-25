using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

public static class HydrantTestCsvImporter
{
    private static readonly string[] StaticHeaders =
    {
        "static",
        "staticpressure",
        "static_pressure",
        "staticpressurepsi",
        "static_psi",
        "static pressure",
        "static pressure psi",
        "staticpressure(psi)"
    };

    private static readonly string[] ResidualHeaders =
    {
        "residual",
        "residualpressure",
        "residual_pressure",
        "residualpressurepsi",
        "residual_psi",
        "residual pressure",
        "residual pressure psi",
        "residualpressure(psi)"
    };

    private static readonly string[] FlowHeaders =
    {
        "flow",
        "flowatresidual",
        "flow_at_residual",
        "flowatresidualgpm",
        "flow_gpm",
        "flow at residual",
        "flow at residual gpm",
        "flow(gpm)",
        "test flow",
        "pitot flow"
    };

    private static readonly string[] DateHeaders =
    {
        "date",
        "testdate",
        "test_date",
        "hydranttestdate",
        "hydrant_test_date",
        "hydrant test date",
        "test date"
    };

    public static HydrantTestImportResult Import(string filePath)
    {
        HydrantTestImportResult result = new HydrantTestImportResult
        {
            SourcePath = filePath ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.Errors.Add("Select an existing CSV file to import.");
            return result;
        }

        try
        {
            List<string[]> rows = File.ReadAllLines(filePath)
                .Select(ParseRow)
                .Where(row => row.Length > 0 && row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .ToList();

            if (rows.Count == 0)
            {
                result.Errors.Add("The CSV file does not contain any data rows.");
                return result;
            }

            Dictionary<string, string> values = TryParseHeaderRowFormat(rows)
                ?? TryParseKeyValueFormat(rows)
                ?? TryParsePositionalFormat(rows);

            if (values == null || values.Count == 0)
            {
                result.Errors.Add(
                    "Could not recognize hydrant test columns. Include headers such as Static Pressure, Residual Pressure, Flow at Residual, and Test Date.");
                return result;
            }

            if (!TryGetValue(values, StaticHeaders, out double? staticPsi))
            {
                result.Errors.Add("Static pressure was not found in the CSV.");
            }
            else
            {
                result.Input.StaticPressurePsi = staticPsi;
            }

            if (!TryGetValue(values, ResidualHeaders, out double? residualPsi))
            {
                result.Errors.Add("Residual pressure was not found in the CSV.");
            }
            else
            {
                result.Input.ResidualPressurePsi = residualPsi;
            }

            if (!TryGetValue(values, FlowHeaders, out double? flowGpm))
            {
                result.Errors.Add("Flow at residual was not found in the CSV.");
            }
            else
            {
                result.Input.FlowAtResidualGpm = flowGpm;
            }

            if (TryGetDate(values, out DateTime? testDate))
            {
                result.Input.HydrantTestDate = testDate;
            }
            else
            {
                result.Warnings.Add("Test date was not found; you can enter it manually after import.");
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            if (result.Input.StaticPressurePsi <= result.Input.ResidualPressurePsi)
            {
                result.Warnings.Add("Static pressure is not greater than residual pressure. Verify imported values.");
            }

            if (result.Input.FlowAtResidualGpm <= 0)
            {
                result.Errors.Add("Flow at residual must be greater than zero.");
                return result;
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    private static Dictionary<string, string> TryParseHeaderRowFormat(List<string[]> rows)
    {
        string[] headers = rows[0].Select(NormalizeHeader).ToArray();
        if (!headers.Any(IsKnownHeader))
        {
            return null;
        }

        string[] dataRow = rows.Skip(1).FirstOrDefault(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)));
        if (dataRow == null)
        {
            return null;
        }

        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int columnCount = Math.Min(headers.Length, dataRow.Length);
        for (int index = 0; index < columnCount; index++)
        {
            if (string.IsNullOrWhiteSpace(headers[index]))
            {
                continue;
            }

            values[headers[index]] = dataRow[index]?.Trim() ?? string.Empty;
        }

        return values;
    }

    private static Dictionary<string, string> TryParseKeyValueFormat(List<string[]> rows)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int mappedRows = 0;

        foreach (string[] row in rows)
        {
            if (row.Length < 2)
            {
                continue;
            }

            string key = NormalizeHeader(row[0]);
            if (!IsKnownHeader(key))
            {
                continue;
            }

            values[key] = row[1].Trim();
            mappedRows++;
        }

        return mappedRows >= 2 ? values : null;
    }

    private static Dictionary<string, string> TryParsePositionalFormat(List<string[]> rows)
    {
        if (rows.Count != 1 || rows[0].Length < 3)
        {
            return null;
        }

        string[] row = rows[0];
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StaticHeaders[0]] = row[0],
            [ResidualHeaders[0]] = row[1],
            [FlowHeaders[0]] = row[2]
        };

        if (row.Length >= 4)
        {
            values[DateHeaders[0]] = row[3];
        }

        return values;
    }

    private static bool TryGetValue(
        Dictionary<string, string> values,
        IEnumerable<string> aliases,
        out double? parsedValue)
    {
        parsedValue = null;
        foreach (KeyValuePair<string, string> entry in values)
        {
            if (!aliases.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryParseDouble(entry.Value, out double number))
            {
                parsedValue = number;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDate(Dictionary<string, string> values, out DateTime? parsedDate)
    {
        parsedDate = null;
        foreach (KeyValuePair<string, string> entry in values)
        {
            if (!DateHeaders.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (DateTime.TryParse(entry.Value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime date)
                || DateTime.TryParse(entry.Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            {
                parsedDate = date.Date;
                return true;
            }
        }

        return false;
    }

    private static bool IsKnownHeader(string header)
    {
        return StaticHeaders.Contains(header, StringComparer.OrdinalIgnoreCase)
            || ResidualHeaders.Contains(header, StringComparer.OrdinalIgnoreCase)
            || FlowHeaders.Contains(header, StringComparer.OrdinalIgnoreCase)
            || DateHeaders.Contains(header, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeHeader(string value)
    {
        return (value ?? string.Empty).Trim().Trim('"').ToLowerInvariant();
    }

    private static string[] ParseRow(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Array.Empty<string>();
        }

        List<string> cells = new List<string>();
        bool inQuotes = false;
        string current = string.Empty;

        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if ((character == ',' || character == '\t' || character == ';') && !inQuotes)
            {
                cells.Add(current.Trim());
                current = string.Empty;
                continue;
            }

            current += character;
        }

        cells.Add(current.Trim());
        return cells.ToArray();
    }

    private static bool TryParseDouble(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
