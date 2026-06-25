using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Placement;

public sealed class SprinklerPlacementSummary
{
    public int TotalCandidates { get; set; }

    public int PlacedCount { get; set; }

    public int SkippedRoomCount { get; set; }

    public int FailedCount { get; set; }

    public IList<SprinklerPlacementRoomResult> RoomResults { get; set; } = new List<SprinklerPlacementRoomResult>();

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class SprinklerPlacementRoomResult
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public int PlacedCount { get; set; }

    public int SkippedCount { get; set; }

    public IList<int> PlacedElementIds { get; set; } = new List<int>();

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
