using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.Clash;

public sealed class LinkedModelScanOption
{
    public int LinkInstanceId { get; set; }

    public string LinkName { get; set; } = string.Empty;

    public string DocumentTitle { get; set; } = string.Empty;

    public bool IsLoaded { get; set; }

    public bool IncludeInClashScan { get; set; } = true;
}

public sealed class SprinklerClashRecord
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string CandidateId { get; set; } = string.Empty;

    public Point3D Location { get; set; } = new Point3D();

    public string ClashType { get; set; } = string.Empty;

    public string ObstructionDescription { get; set; } = string.Empty;

    public int ObstructionElementId { get; set; }

    public string ObstructionCategory { get; set; } = string.Empty;

    public string DetectionSource { get; set; } = string.Empty;

    public bool IsLinkedModelClash { get; set; }

    public int LinkedModelInstanceId { get; set; }

    public string LinkedModelName { get; set; } = string.Empty;

    public string NfpaReference { get; set; } = Nfpa13Edition.References.ObstructionsToDischarge;

    public bool Resolved { get; set; }

    public string ResolutionAction { get; set; } = string.Empty;
}

public sealed class ClashDetectionSummary
{
    public int TotalClashes { get; set; }

    public int HostClashCount { get; set; }

    public int LinkedClashCount { get; set; }

    public int LinkedModelsScannedCount { get; set; }

    public int ResolvedClashes { get; set; }

    public int UnresolvedClashes { get; set; }

    public IList<SprinklerClashRecord> Clashes { get; set; } = new List<SprinklerClashRecord>();

    public IList<string> Messages { get; set; } = new List<string>();
}
