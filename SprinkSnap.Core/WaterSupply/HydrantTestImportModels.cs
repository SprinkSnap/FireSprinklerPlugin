using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.WaterSupply;

public sealed class HydrantTestImportResult
{
    public bool Success { get; set; }

    public WaterSupplyInput Input { get; set; } = new WaterSupplyInput();

    public string SourcePath { get; set; } = string.Empty;

    public IList<string> Warnings { get; set; } = new List<string>();

    public IList<string> Errors { get; set; } = new List<string>();
}
