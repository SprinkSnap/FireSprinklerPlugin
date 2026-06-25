using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class LayoutLinkedHydraulicCalculator
{
    private const int MaxIterations = 6;

    private const double FlowToleranceGpm = 0.25;

    public static LayoutLinkedHydraulicPath Calculate(
        IEnumerable<RoomInfo> controllingRooms,
        int operatingSprinklerCount,
        double designFlowPerSprinklerGpm,
        double hoseStreamAllowanceGpm,
        double defaultKFactor,
        double branchDiameterInches,
        double mainDiameterInches)
    {
        Point3D sourcePoint = HydraulicGraphBuilder.ResolveSourcePoint(controllingRooms);
        IList<LayoutSprinklerPoint> sprinklerPoints = HydraulicGraphBuilder.CollectSprinklerPoints(
            controllingRooms,
            defaultKFactor);
        IList<LayoutSprinklerPoint> operatingSprinklers = HydraulicGraphBuilder.SelectOperatingSprinklers(
            sprinklerPoints,
            sourcePoint,
            operatingSprinklerCount);

        LayoutLinkedHydraulicPath path = HydraulicGraphBuilder.BuildPath(
            controllingRooms,
            operatingSprinklers,
            sourcePoint,
            branchDiameterInches,
            mainDiameterInches);

        double targetSprinklerFlow = designFlowPerSprinklerGpm * Math.Max(operatingSprinklers.Count, 1);
        if (operatingSprinklers.Count == 0)
        {
            double remotePressure = Math.Pow(designFlowPerSprinklerGpm / Math.Max(defaultKFactor, 0.1), 2.0);
            path.RemoteSprinklerPressurePsi = remotePressure;
            path.BranchFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
                targetSprinklerFlow,
                branchDiameterInches,
                path.BranchLengthFeet);
            path.MainFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
                targetSprinklerFlow + hoseStreamAllowanceGpm,
                mainDiameterInches,
                path.MainLengthFeet);
            path.JunctionPressurePsi = remotePressure + path.BranchFrictionPsi;
            path.CalculatedSprinklerFlowGpm = designFlowPerSprinklerGpm * Math.Max(operatingSprinklerCount, 1);
            path.CriticalPath = BuildFallbackCriticalPath(
                path,
                designFlowPerSprinklerGpm,
                hoseStreamAllowanceGpm,
                operatingSprinklerCount);
            return path;
        }

        double flowPerHead = Math.Max(designFlowPerSprinklerGpm, 0.1);
        double remotePressurePsi = 0.0;
        double totalSprinklerFlow = 0.0;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            remotePressurePsi = Math.Pow(flowPerHead / Math.Max(operatingSprinklers[0].KFactor, 0.1), 2.0);
            totalSprinklerFlow = 0.0;
            foreach (LayoutSprinklerPoint sprinkler in operatingSprinklers)
            {
                totalSprinklerFlow += sprinkler.KFactor * Math.Sqrt(remotePressurePsi);
            }

            if (Math.Abs(totalSprinklerFlow - targetSprinklerFlow) <= FlowToleranceGpm)
            {
                break;
            }

            flowPerHead *= targetSprinklerFlow / Math.Max(totalSprinklerFlow, 0.1);
        }

        path.RemoteSprinklerPressurePsi = remotePressurePsi;
        path.CalculatedSprinklerFlowGpm = totalSprinklerFlow;
        path.BranchFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
            totalSprinklerFlow,
            path.BranchDiameterInches,
            path.BranchLengthFeet);
        path.JunctionPressurePsi = remotePressurePsi + path.BranchFrictionPsi;
        path.MainFrictionPsi = HazenWilliamsCalculator.FrictionLossPsi(
            totalSprinklerFlow + hoseStreamAllowanceGpm,
            path.MainDiameterInches,
            path.MainLengthFeet);

        path.CriticalPath = BuildCriticalPath(
            path,
            totalSprinklerFlow,
            hoseStreamAllowanceGpm,
            remotePressurePsi);

        path.Warnings.Add(
            "Layout-linked hydraulics solved "
            + operatingSprinklers.Count
            + " operating head(s) with Q = K√P and geometry-derived pipe lengths.");

        return path;
    }

    private static IList<HydraulicNode> BuildCriticalPath(
        LayoutLinkedHydraulicPath path,
        double totalSprinklerFlowGpm,
        double hoseStreamAllowanceGpm,
        double remotePressurePsi)
    {
        LayoutSprinklerPoint remote = path.MostRemoteSprinkler;
        Point3D branchJunction = remote?.Room == null
            ? path.SourcePoint
            : HydraulicGeometry.ResolveBranchJunction(remote.Room);

        string remoteLabel = remote?.Label ?? "Remote Sprinkler";
        double flowPerHead = remote == null
            ? totalSprinklerFlowGpm
            : totalSprinklerFlowGpm / Math.Max(path.OperatingSprinklers.Count, 1);

        List<HydraulicNode> nodes = new List<HydraulicNode>
        {
            new HydraulicNode
            {
                NodeId = "Remote " + remoteLabel,
                Location = remote?.Location ?? new Point3D(),
                PressurePsi = remotePressurePsi,
                FlowGpm = flowPerHead,
                LengthFeet = 0.0,
                DiameterInches = 0.0,
                FrictionLossPsi = 0.0,
                SegmentType = "Sprinkler"
            },
            new HydraulicNode
            {
                NodeId = "Branch to " + (remote?.Room?.Number ?? "junction"),
                Location = branchJunction,
                PressurePsi = path.JunctionPressurePsi,
                FlowGpm = totalSprinklerFlowGpm,
                LengthFeet = path.BranchLengthFeet,
                DiameterInches = path.BranchDiameterInches,
                FrictionLossPsi = path.BranchFrictionPsi,
                SegmentType = "Branch"
            },
            new HydraulicNode
            {
                NodeId = "Main to source",
                Location = path.SourcePoint,
                PressurePsi = path.JunctionPressurePsi + path.MainFrictionPsi,
                FlowGpm = totalSprinklerFlowGpm + hoseStreamAllowanceGpm,
                LengthFeet = path.MainLengthFeet,
                DiameterInches = path.MainDiameterInches,
                FrictionLossPsi = path.MainFrictionPsi,
                SegmentType = "Main"
            },
            new HydraulicNode
            {
                NodeId = "Riser / Source",
                Location = path.SourcePoint,
                PressurePsi = path.JunctionPressurePsi + path.MainFrictionPsi,
                FlowGpm = totalSprinklerFlowGpm + hoseStreamAllowanceGpm,
                LengthFeet = 0.0,
                DiameterInches = path.MainDiameterInches,
                FrictionLossPsi = 0.0,
                SegmentType = "Source"
            }
        };

        return nodes;
    }

    private static IList<HydraulicNode> BuildFallbackCriticalPath(
        LayoutLinkedHydraulicPath path,
        double designFlowPerSprinklerGpm,
        double hoseStreamAllowanceGpm,
        int operatingSprinklerCount)
    {
        double totalSprinklerFlow = designFlowPerSprinklerGpm * Math.Max(operatingSprinklerCount, 1);
        double remotePressure = path.RemoteSprinklerPressurePsi;

        return new List<HydraulicNode>
        {
            new HydraulicNode
            {
                NodeId = "Remote Sprinkler (estimated)",
                PressurePsi = remotePressure,
                FlowGpm = designFlowPerSprinklerGpm,
                SegmentType = "Sprinkler"
            },
            new HydraulicNode
            {
                NodeId = "Branch Segment",
                PressurePsi = remotePressure + path.BranchFrictionPsi,
                FlowGpm = totalSprinklerFlow,
                LengthFeet = path.BranchLengthFeet,
                DiameterInches = path.BranchDiameterInches,
                FrictionLossPsi = path.BranchFrictionPsi,
                SegmentType = "Branch"
            },
            new HydraulicNode
            {
                NodeId = "Main / Riser",
                PressurePsi = remotePressure + path.BranchFrictionPsi + path.MainFrictionPsi,
                FlowGpm = totalSprinklerFlow + hoseStreamAllowanceGpm,
                LengthFeet = path.MainLengthFeet,
                DiameterInches = path.MainDiameterInches,
                FrictionLossPsi = path.MainFrictionPsi,
                SegmentType = "Main"
            }
        };
    }
}
