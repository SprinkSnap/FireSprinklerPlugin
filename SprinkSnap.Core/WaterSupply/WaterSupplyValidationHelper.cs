using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

public static class WaterSupplyValidationHelper
{
    public const int StaleTestAgeWarningMonths = 12;

    public static WaterSupplyInputValidationResult ValidateInput(WaterSupplyInput input)
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

    public static bool HasInputValidationErrors(IEnumerable<string> warnings)
    {
        if (warnings == null)
        {
            return false;
        }

        foreach (string warning in warnings)
        {
            if (IsInputValidationMessage(warning))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasInputValidationErrors(WaterSupplyValidationResult result)
    {
        return result != null && HasInputValidationErrors(result.Warnings);
    }

    public static bool IsHydrantInputComplete(WaterSupplyInput input)
    {
        return input != null
            && input.StaticPressurePsi.HasValue
            && input.StaticPressurePsi.Value > 0
            && input.ResidualPressurePsi.HasValue
            && input.ResidualPressurePsi.Value > 0
            && input.FlowAtResidualGpm.HasValue
            && input.FlowAtResidualGpm.Value > 0
            && input.HydrantTestDate.HasValue;
    }

    private static bool IsInputValidationMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.StartsWith("Enter the measured", StringComparison.Ordinal)
            || message.StartsWith("Enter the hydrant", StringComparison.Ordinal)
            || message.StartsWith("Enter the flow at residual", StringComparison.Ordinal)
            || message.Contains("cannot be in the future", StringComparison.Ordinal)
            || message.Contains("cannot exceed static pressure", StringComparison.Ordinal)
            || message.Contains("Water supply input is required", StringComparison.Ordinal);
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
