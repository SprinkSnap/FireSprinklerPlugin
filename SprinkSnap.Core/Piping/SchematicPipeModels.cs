using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipeSegmentTypes
{
    public const string Branch = "Branch";

    public const string CrossMain = "Cross Main";

    public const string Main = "Main";

    public const string Riser = "Riser";
}

public sealed class PipeSegment
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public string SegmentType { get; set; } = string.Empty;

    public double DiameterInches { get; set; }

    public Point3D Start { get; set; } = new Point3D();

    public Point3D End { get; set; } = new Point3D();

    public double LengthFeet { get; set; }

    public string Description { get; set; } = string.Empty;
}

public sealed class SchematicPipeRoutingSummary
{
    public IList<PipeSegment> Segments { get; set; } = new List<PipeSegment>();

    public int TotalSegmentCount { get; set; }

    public double TotalLengthFeet { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}
