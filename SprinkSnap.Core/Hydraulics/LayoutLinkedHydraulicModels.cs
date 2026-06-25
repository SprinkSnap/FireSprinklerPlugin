using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicGraphSegment
{
    public string SegmentId { get; set; } = string.Empty;

    public Point3D Start { get; set; } = new Point3D();

    public Point3D End { get; set; } = new Point3D();

    public double LengthFeet { get; set; }

    public double DiameterInches { get; set; }

    public string SegmentType { get; set; } = string.Empty;

    public int RoomRevitElementId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string DataSource { get; set; } = string.Empty;

    public double FlowGpm { get; set; }

    public double FrictionLossPsi { get; set; }

    public double DownstreamPressurePsi { get; set; }

    public double UpstreamPressurePsi { get; set; }
}

public sealed class LayoutSprinklerPoint
{
    public RoomInfo Room { get; set; }

    public Point3D Location { get; set; } = new Point3D();

    public int SprinklerIndex { get; set; }

    public double KFactor { get; set; }

    public string Label => BuildLabel();

    private string BuildLabel()
    {
        string roomLabel = string.IsNullOrWhiteSpace(Room?.Number) ? "Room" : Room.Number;
        return roomLabel + "-" + (SprinklerIndex + 1);
    }
}

public sealed class LayoutLinkedHydraulicPath
{
    public bool UsesLayoutGeometry { get; set; }

    public bool UsesPlacedPipeLengths { get; set; }

    public string PipeLengthDataSource { get; set; } = string.Empty;

    public Point3D SourcePoint { get; set; } = new Point3D();

    public LayoutSprinklerPoint MostRemoteSprinkler { get; set; }

    public IList<LayoutSprinklerPoint> OperatingSprinklers { get; set; } = new List<LayoutSprinklerPoint>();

    public double BranchLengthFeet { get; set; }

    public double MainLengthFeet { get; set; }

    public double TotalPipeLengthFeet { get; set; }

    public double BranchDiameterInches { get; set; }

    public double MainDiameterInches { get; set; }

    public double RemoteSprinklerPressurePsi { get; set; }

    public double BranchFrictionPsi { get; set; }

    public double MainFrictionPsi { get; set; }

    public double JunctionPressurePsi { get; set; }

    public double FittingFrictionPsi { get; set; }

    public int CriticalPathFittingCount { get; set; }

    public double CriticalPathDemandPsi { get; set; }

    public double CalculatedSprinklerFlowGpm { get; set; }

    public bool UsesSegmentGraphHydraulics { get; set; }

    public bool UsesPlacedPipeTopology { get; set; }

    public bool UsesUserSupplyAnchor { get; set; }

    public string UserSupplyAnchorLabel { get; set; } = string.Empty;

    public bool UsesProjectTrunk { get; set; }

    public IList<HydraulicGraphSegment> SegmentChain { get; set; } = new List<HydraulicGraphSegment>();

    public int CriticalPathSegmentCount { get; set; }

    public IList<HydraulicNode> CriticalPath { get; set; } = new List<HydraulicNode>();

    public IList<string> Warnings { get; set; } = new List<string>();
}
