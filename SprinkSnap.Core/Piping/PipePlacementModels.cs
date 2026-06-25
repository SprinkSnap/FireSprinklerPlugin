using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

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

    public int ConnectedJointCount { get; set; }

    public int ConnectedFittingCount { get; set; }

    public int SkippedConnectionCount { get; set; }

    public int TrunkSplitCount { get; set; }

    public IList<PipePlacementRoomResult> RoomResults { get; set; } = new List<PipePlacementRoomResult>();

    public bool UsesRevitPipeDiameterSync { get; set; }

    public int RevitPipeDiameterSyncCount { get; set; }

    public bool UsesRevitFittingDiameterSync { get; set; }

    public int RevitFittingDiameterSyncCount { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class PlacedFittingDiameterSyncPlan
{
    public IList<PlacedFittingDiameterSyncTarget> Targets { get; set; } = new List<PlacedFittingDiameterSyncTarget>();

    public int SkippedCount { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class PlacedFittingDiameterSyncTarget
{
    public int PlacedElementId { get; set; }

    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string JointType { get; set; } = string.Empty;

    public double TargetDiameterInches { get; set; }

    public double CurrentDiameterInches { get; set; }

    public string UpdatedDescription { get; set; } = string.Empty;
}

public sealed class PlacedFittingDiameterSyncSummary
{
    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public bool UsesRevitFittingDiameterSync { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class PlacedPipeDiameterSyncPlan
{
    public IList<PlacedPipeDiameterSyncTarget> Targets { get; set; } = new List<PlacedPipeDiameterSyncTarget>();

    public int SkippedCount { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}

public sealed class PlacedPipeDiameterSyncTarget
{
    public int PlacedElementId { get; set; }

    public int RoomRevitElementId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public string SegmentType { get; set; } = string.Empty;

    public double TargetDiameterInches { get; set; }

    public double CurrentDiameterInches { get; set; }

    public string UpdatedDescription { get; set; } = string.Empty;
}

public sealed class PlacedPipeDiameterSyncSummary
{
    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public bool UsesRevitPipeDiameterSync { get; set; }

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

    public IList<PipePlacementSegmentResult> PlacedSegments { get; set; } = new List<PipePlacementSegmentResult>();

    public IList<PipePlacementFittingResult> PlacedFittings { get; set; } = new List<PipePlacementFittingResult>();

    public int PlacedFittingCount { get; set; }

    public int SkippedFittingCount { get; set; }

    public int ConnectedJointCount { get; set; }

    public int SkippedConnectionCount { get; set; }

    public int ConnectedFittingCount { get; set; }

    public int TrunkSplitCount { get; set; }

    public IList<int> PlacedFittingElementIds { get; set; } = new List<int>();

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class PipePlacementSegmentResult
{
    public string SegmentType { get; set; } = string.Empty;

    public double DiameterInches { get; set; }

    public double LengthFeet { get; set; }

    public int PlacedElementId { get; set; }

    public string Description { get; set; } = string.Empty;

    public Point3D Start { get; set; } = new Point3D();

    public Point3D End { get; set; } = new Point3D();

    public bool HasTopology { get; set; }
}

public sealed class PipePlacementFittingResult
{
    public string JointType { get; set; } = string.Empty;

    public double DiameterInches { get; set; }

    public int PlacedElementId { get; set; }

    public string Description { get; set; } = string.Empty;
}
