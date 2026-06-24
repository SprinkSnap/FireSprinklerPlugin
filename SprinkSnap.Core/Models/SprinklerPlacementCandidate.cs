using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public sealed class SprinklerPlacementCandidate
{
    public Point3D Location { get; set; } = new Point3D();

    public string CandidateType { get; set; } = "Automatic Layout Candidate";

    public string Basis { get; set; } = string.Empty;
}
