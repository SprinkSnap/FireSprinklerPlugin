using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class WaterSupplyCurveCalculator
{
    public static IList<WaterSupplyCurvePoint> BuildCurve(WaterSupplyInput input, int segments = 10)
    {
        List<WaterSupplyCurvePoint> curve = new List<WaterSupplyCurvePoint>();
        if (input?.StaticPressurePsi == null
            || input.ResidualPressurePsi == null
            || input.FlowAtResidualGpm == null
            || input.FlowAtResidualGpm <= 0)
        {
            return curve;
        }

        double maxFlow = input.FlowAtResidualGpm.Value * 1.2;
        for (int index = 0; index <= segments; index++)
        {
            double flow = maxFlow * index / segments;
            curve.Add(new WaterSupplyCurvePoint
            {
                FlowGpm = flow,
                PressurePsi = GetPressureAtFlow(input, flow)
            });
        }

        return curve;
    }

    public static double GetPressureAtFlow(WaterSupplyInput input, double flowGpm)
    {
        if (input?.StaticPressurePsi == null
            || input.ResidualPressurePsi == null
            || input.FlowAtResidualGpm == null
            || input.FlowAtResidualGpm <= 0)
        {
            return input?.ResidualPressurePsi ?? 0.0;
        }

        double slope = (input.StaticPressurePsi.Value - input.ResidualPressurePsi.Value) / input.FlowAtResidualGpm.Value;
        return input.StaticPressurePsi.Value - (slope * flowGpm);
    }
}
