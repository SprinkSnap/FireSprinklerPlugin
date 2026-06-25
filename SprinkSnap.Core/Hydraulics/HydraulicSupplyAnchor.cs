using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicSupplyAnchor
{
    public bool IsSet { get; set; }

    public int RevitElementId { get; set; }

    public string ElementLabel { get; set; } = string.Empty;

    public Point3D SupplyPoint { get; set; } = new Point3D();

    public Point3D HeaderPoint { get; set; } = new Point3D();

    public string SourceKind { get; set; } = string.Empty;
}
