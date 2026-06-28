using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

namespace FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

public sealed class Nfpa13WaterSupplyInputValidationResult
{
    public bool IsCompliant { get; set; }

    public string NfpaReference { get; set; } = Nfpa13Edition.References.WaterSupplyInformation;

    public IList<string> Errors { get; set; } = new List<string>();

    public IList<string> Warnings { get; set; } = new List<string>();

    public string Summary { get; set; } = string.Empty;
}

public static class Nfpa13WaterSupplyValidator
{
    public const int StaleTestAgeWarningMonths = WaterSupplyValidationHelper.StaleTestAgeWarningMonths;

    public static Nfpa13WaterSupplyInputValidationResult ValidateInput(WaterSupplyInput input)
    {
        WaterSupplyInputValidationResult validation = WaterSupplyValidationHelper.ValidateInput(input);
        return new Nfpa13WaterSupplyInputValidationResult
        {
            IsCompliant = validation.IsCompliant,
            NfpaReference = validation.NfpaReference,
            Errors = validation.Errors.ToList(),
            Warnings = validation.Warnings.ToList(),
            Summary = validation.Summary
        };
    }
}
