using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

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

    public double CalculatedSprinklerFlowGpm { get; set; }

    public IList<HydraulicNode> CriticalPath { get; set; } = new List<HydraulicNode>();

    public IList<string> Warnings { get; set; } = new List<string>();
}
