using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

public sealed class WaterSupplyInputValidationResult
{
    public bool IsCompliant { get; set; }

    public string NfpaReference { get; set; } = Nfpa13Edition.References.WaterSupplyInformation;

    public IList<string> Errors { get; set; } = new List<string>();

    public IList<string> Warnings { get; set; } = new List<string>();

    public string Summary { get; set; } = string.Empty;
}

public static class WaterSupplyInputValidator
{
    public const int StaleTestAgeWarningMonths = 12;

    public static WaterSupplyInputValidationResult Validate(WaterSupplyInput input)
    {
        WaterSupplyInputValidationResult result = new WaterSupplyInputValidationResult();

        if (input == null)
        {
            result.Errors.Add("Water supply input is required.");
            result.Summary = BuildSummary(result);
            return result;
        }

        if (!input.StaticPressurePsi.HasValue || input.StaticPressurePsi.Value <= 0)
        {
            result.Errors.Add("Enter the measured static pressure (PSI) from the hydrant flow test.");
        }

        if (!input.ResidualPressurePsi.HasValue || input.ResidualPressurePsi.Value <= 0)
        {
            result.Errors.Add("Enter the measured residual pressure (PSI) at the test flow rate.");
        }

        if (!input.FlowAtResidualGpm.HasValue || input.FlowAtResidualGpm.Value <= 0)
        {
            result.Errors.Add("Enter the flow at residual (GPM) from the hydrant flow test.");
        }

        if (!input.HydrantTestDate.HasValue)
        {
            result.Errors.Add(
                "Enter the hydrant test date. "
                + Nfpa13Edition.References.WaterSupplyInformation
                + " requires the date and time of the test.");
        }
        else if (input.HydrantTestDate.Value.Date > DateTime.Today)
        {
            result.Errors.Add("Hydrant test date cannot be in the future.");
        }
        else if (input.HydrantTestDate.Value.Date < DateTime.Today.AddMonths(-StaleTestAgeWarningMonths))
        {
            result.Warnings.Add(
                "Hydrant test date is older than "
                + StaleTestAgeWarningMonths
                + " months. Confirm current water supply data before hydraulic sign-off.");
        }

        if (input.StaticPressurePsi.HasValue
            && input.ResidualPressurePsi.HasValue
            && input.ResidualPressurePsi.Value > input.StaticPressurePsi.Value)
        {
            result.Errors.Add("Residual pressure cannot exceed static pressure on the hydrant test report.");
        }

        result.IsCompliant = result.Errors.Count == 0;
        result.Summary = BuildSummary(result);
        return result;
    }

    private static string BuildSummary(WaterSupplyInputValidationResult result)
    {
        if (result.IsCompliant)
        {
            return "Hydrant test input complies with "
                + result.NfpaReference
                + ".";
        }

        return string.Join(" ", result.Errors);
    }
}
