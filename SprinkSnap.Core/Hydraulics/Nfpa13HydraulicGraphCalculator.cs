using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

/// <summary>
/// Builds NFPA 13 hydraulic graph curves for N^1.85 semi-log graph paper
/// (flow raised to the 1.85 power on the horizontal axis, pressure on the vertical axis).
/// </summary>
public static class Nfpa13HydraulicGraphCalculator
{
    public const double FlowAxisExponent = 1.85;

    private const double SupplyFlowExponent = 1.0 / 0.54;

    public static double ScaleFlowForGraph(double flowGpm)
    {
        if (flowGpm <= 0)
        {
            return 0.0;
        }

        return Math.Pow(flowGpm, FlowAxisExponent);
    }

    public static IList<WaterSupplyCurvePoint> BuildSupplyCurve(WaterSupplyInput input, int segments = 10)
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
                PressurePsi = GetSupplyPressureAtFlow(input, flow)
            });
        }

        return curve;
    }

    public static double GetSupplyPressureAtFlow(WaterSupplyInput input, double flowGpm)
    {
        if (input?.StaticPressurePsi == null
            || input.ResidualPressurePsi == null
            || input.FlowAtResidualGpm == null
            || input.FlowAtResidualGpm <= 0)
        {
            return input?.ResidualPressurePsi ?? 0.0;
        }

        if (flowGpm <= 0)
        {
            return input.StaticPressurePsi.Value;
        }

        double residualFlow = input.FlowAtResidualGpm.Value;
        double staticPressure = input.StaticPressurePsi.Value;
        double residualPressure = input.ResidualPressurePsi.Value;
        double availableHead = staticPressure - residualPressure;

        if (flowGpm <= residualFlow)
        {
            double slope = availableHead / residualFlow;
            return staticPressure - (slope * flowGpm);
        }

        double flowRatio = flowGpm / residualFlow;
        double headLoss = availableHead * Math.Pow(flowRatio, SupplyFlowExponent);
        return staticPressure - headLoss;
    }

    public static IList<WaterSupplyCurvePoint> BuildDemandCurve(
        double sprinklerFlowGpm,
        double sprinklerDemandPressurePsi,
        double totalFlowGpm,
        double totalDemandPressurePsi)
    {
        List<WaterSupplyCurvePoint> curve = new List<WaterSupplyCurvePoint>();
        if (sprinklerFlowGpm <= 0 || sprinklerDemandPressurePsi <= 0)
        {
            return curve;
        }

        curve.Add(new WaterSupplyCurvePoint
        {
            FlowGpm = sprinklerFlowGpm,
            PressurePsi = sprinklerDemandPressurePsi
        });

        if (totalFlowGpm > sprinklerFlowGpm && totalDemandPressurePsi > 0)
        {
            curve.Add(new WaterSupplyCurvePoint
            {
                FlowGpm = totalFlowGpm,
                PressurePsi = totalDemandPressurePsi
            });
        }

        return curve;
    }

    public static double ComputeSprinklerDemandPressureAtSource(
        double totalDemandPressurePsi,
        double sprinklerFlowGpm,
        double totalFlowGpm,
        double mainDiameterInches,
        double mainLengthFeet)
    {
        if (totalDemandPressurePsi <= 0 || sprinklerFlowGpm <= 0)
        {
            return totalDemandPressurePsi;
        }

        if (totalFlowGpm <= sprinklerFlowGpm)
        {
            return totalDemandPressurePsi;
        }

        double frictionAtTotalFlow = HazenWilliamsCalculator.FrictionLossPsi(
            totalFlowGpm,
            mainDiameterInches,
            mainLengthFeet);
        double frictionAtSprinklerFlow = HazenWilliamsCalculator.FrictionLossPsi(
            sprinklerFlowGpm,
            mainDiameterInches,
            mainLengthFeet);

        return totalDemandPressurePsi - (frictionAtTotalFlow - frictionAtSprinklerFlow);
    }
}
