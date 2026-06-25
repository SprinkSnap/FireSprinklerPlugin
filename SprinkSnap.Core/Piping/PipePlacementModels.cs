using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public sealed class PipePlacementSummary
{
    public int TotalSegments { get; set; }

    public int PlacedSegmentCount { get; set; }

    public int SkippedSegmentCount { get; set; }

    public int FailedSegmentCount { get; set; }

    public double PlacedLengthFeet { get; set; }

    public int TotalFittingCount { get; set; }

    public int PlacedFittingCount { get; set; }

    public int SkippedFittingCount { get; set; }

    public IList<PipePlacementRoomResult> RoomResults { get; set; } = new List<PipePlacementRoomResult>();

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class PipePlacementRoomResult
{
    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public int PlacedSegmentCount { get; set; }

    public int SkippedSegmentCount { get; set; }

    public double PlacedLengthFeet { get; set; }

    public IList<int> PlacedElementIds { get; set; } = new List<int>();

    public int PlacedFittingCount { get; set; }

    public int SkippedFittingCount { get; set; }

    public IList<int> PlacedFittingElementIds { get; set; } = new List<int>();

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
