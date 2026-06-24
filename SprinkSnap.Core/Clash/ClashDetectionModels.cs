using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Clash;

public sealed class SprinklerClashRecord
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string CandidateId { get; set; } = string.Empty;

    public Point3D Location { get; set; } = new Point3D();

    public string ClashType { get; set; } = string.Empty;

    public string ObstructionDescription { get; set; } = string.Empty;

    public string NfpaReference { get; set; } = "NFPA 13 Section 10.2.6";

    public bool Resolved { get; set; }

    public string ResolutionAction { get; set; } = string.Empty;
}

public sealed class ClashDetectionSummary
{
    public int TotalClashes { get; set; }

    public int ResolvedClashes { get; set; }

    public int UnresolvedClashes { get; set; }

    public IList<SprinklerClashRecord> Clashes { get; set; } = new List<SprinklerClashRecord>();

    public IList<string> Messages { get; set; } = new List<string>();
}
