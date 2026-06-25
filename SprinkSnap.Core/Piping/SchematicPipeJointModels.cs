using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipeJointTypes
{
    public const string Elbow = "Elbow";

    public const string Tee = "Tee";

    public const string Valve = "Valve";
}

public sealed class PipeJoint
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public string JointType { get; set; } = string.Empty;

    public double DiameterInches { get; set; }

    public Point3D Location { get; set; } = new Point3D();

    public string Description { get; set; } = string.Empty;
}
