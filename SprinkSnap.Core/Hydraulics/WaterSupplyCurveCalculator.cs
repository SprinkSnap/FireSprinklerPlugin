using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class WaterSupplyCurveCalculator
{
    public static IList<WaterSupplyCurvePoint> BuildCurve(WaterSupplyInput input, int segments = 10)
    {
        return Nfpa13HydraulicGraphCalculator.BuildSupplyCurve(input, segments);
    }

    public static double GetPressureAtFlow(WaterSupplyInput input, double flowGpm)
    {
        return Nfpa13HydraulicGraphCalculator.GetSupplyPressureAtFlow(input, flowGpm);
    }
}
