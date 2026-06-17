namespace FireSprinklerPlugin.SprinkSnap.Core.Models;

public sealed class WaterDemandInfo
{
    public double? StaticPressurePsi { get; set; }

    public double? ResidualPressurePsi { get; set; }

    public double? FlowGpm { get; set; }

    public bool HasAnyValue =>
        StaticPressurePsi.HasValue
        || ResidualPressurePsi.HasValue
        || FlowGpm.HasValue;
}

