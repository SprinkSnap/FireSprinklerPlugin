using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public sealed class WaterSupplyInputValidationResult
{
    public bool IsCompliant { get; set; }

    public string NfpaReference { get; set; } = Nfpa13Edition.References.WaterSupplyInformation;

    public IList<string> Errors { get; set; } = new List<string>();

    public IList<string> Warnings { get; set; } = new List<string>();

    public string Summary { get; set; } = string.Empty;
}
