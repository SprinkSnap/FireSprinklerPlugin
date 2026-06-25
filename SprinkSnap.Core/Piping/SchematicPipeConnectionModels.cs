using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public enum PipeConnectionKind
{
    Direct,

    Elbow,

    Tee,

    Takeoff
}

public sealed class PipeConnectionIntent
{
    public PipeConnectionKind Kind { get; set; }

    public int SegmentIndexA { get; set; }

    public bool IsStartA { get; set; }

    public int SegmentIndexB { get; set; }

    public bool IsStartB { get; set; }

    public int SegmentIndexC { get; set; } = -1;

    public bool IsStartC { get; set; }

    public Point3D Location { get; set; } = new Point3D();
}

public sealed class SchematicPipeConnectionPlan
{
    public IList<PipeConnectionIntent> Connections { get; set; } = new List<PipeConnectionIntent>();
}
