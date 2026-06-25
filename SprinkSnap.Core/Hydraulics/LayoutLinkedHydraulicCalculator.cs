using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

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
        double mainDiameterInches,
        SchematicPipeRoutingSummary schematicPipeRouting = null,
        PipePlacementSummary pipePlacementSummary = null)
    {
        Point3D sourcePoint = HydraulicGraphBuilder.ResolveSourcePoint(controllingRooms, schematicPipeRouting);
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
            mainDiameterInches,
            schematicPipeRouting,
            pipePlacementSummary);

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

        double remotePressurePsi = 0.0;
        double totalSprinklerFlow = 0.0;
        Dictionary<LayoutSprinklerPoint, double> headFlows = new Dictionary<LayoutSprinklerPoint, double>();

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            remotePressurePsi = SolveRemotePressureForTargetFlow(
                operatingSprinklers,
                targetSprinklerFlow);
            totalSprinklerFlow = 0.0;
            headFlows.Clear();
            foreach (LayoutSprinklerPoint sprinkler in operatingSprinklers)
            {
                double headFlow = sprinkler.KFactor * Math.Sqrt(remotePressurePsi);
                headFlows[sprinkler] = headFlow;
                totalSprinklerFlow += headFlow;
            }

            if (Math.Abs(totalSprinklerFlow - targetSprinklerFlow) <= FlowToleranceGpm)
            {
                break;
            }
        }

        path.RemoteSprinklerPressurePsi = remotePressurePsi;
        path.CalculatedSprinklerFlowGpm = totalSprinklerFlow;

        if (path.UsesSegmentGraphHydraulics && path.SegmentChain.Count > 0)
        {
            SolveSegmentGraphHydraulics(
                path,
                headFlows,
                totalSprinklerFlow,
                hoseStreamAllowanceGpm,
                remotePressurePsi,
                schematicPipeRouting);
        }
        else
        {
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
        }

        path.Warnings.Add(
            "Layout-linked hydraulics solved "
            + operatingSprinklers.Count
            + " operating head(s) with Q = K√P and "
            + (path.UsesSegmentGraphHydraulics
                ? path.CriticalPathSegmentCount + " segment-graph"
                : path.UsesPlacedPipeLengths ? "placed Revit" : path.PipeLengthDataSource.ToLowerInvariant())
            + " pipe lengths.");

        return path;
    }

    private static double SolveRemotePressureForTargetFlow(
        IList<LayoutSprinklerPoint> operatingSprinklers,
        double targetSprinklerFlow)
    {
        double lowPressure = 0.01;
        double highPressure = 400.0;
        for (int iteration = 0; iteration < 24; iteration++)
        {
            double midPressure = (lowPressure + highPressure) / 2.0;
            double totalFlow = operatingSprinklers.Sum(sprinkler => sprinkler.KFactor * Math.Sqrt(midPressure));
            if (totalFlow < targetSprinklerFlow)
            {
                lowPressure = midPressure;
            }
            else
            {
                highPressure = midPressure;
            }
        }

        return (lowPressure + highPressure) / 2.0;
    }

    private static void SolveSegmentGraphHydraulics(
        LayoutLinkedHydraulicPath path,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        double totalSprinklerFlowGpm,
        double hoseStreamAllowanceGpm,
        double remotePressurePsi,
        SchematicPipeRoutingSummary schematicPipeRouting)
    {
        HydraulicSegmentGraphBuilder.AssignSegmentFlows(
            path,
            headFlows,
            totalSprinklerFlowGpm,
            hoseStreamAllowanceGpm,
            schematicPipeRouting);

        double downstreamPressurePsi = remotePressurePsi;
        double branchFrictionPsi = 0.0;
        double mainFrictionPsi = 0.0;

        foreach (HydraulicGraphSegment segment in path.SegmentChain)
        {
            segment.DownstreamPressurePsi = downstreamPressurePsi;
            segment.FrictionLossPsi = HazenWilliamsCalculator.FrictionLossPsi(
                segment.FlowGpm,
                segment.DiameterInches,
                segment.LengthFeet);
            segment.UpstreamPressurePsi = downstreamPressurePsi + segment.FrictionLossPsi;
            downstreamPressurePsi = segment.UpstreamPressurePsi;

            if (IsBranchSegment(segment.SegmentType))
            {
                branchFrictionPsi += segment.FrictionLossPsi;
            }
            else
            {
                mainFrictionPsi += segment.FrictionLossPsi;
            }
        }

        path.BranchFrictionPsi = branchFrictionPsi;
        path.MainFrictionPsi = mainFrictionPsi;
        path.JunctionPressurePsi = remotePressurePsi + branchFrictionPsi;
        path.CriticalPath = BuildSegmentCriticalPath(
            path,
            headFlows,
            hoseStreamAllowanceGpm,
            remotePressurePsi);
    }

    private static IList<HydraulicNode> BuildSegmentCriticalPath(
        LayoutLinkedHydraulicPath path,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        double hoseStreamAllowanceGpm,
        double remotePressurePsi)
    {
        LayoutSprinklerPoint remote = path.MostRemoteSprinkler;
        double remoteFlow = headFlows.TryGetValue(remote, out double flow) ? flow : 0.0;
        List<HydraulicNode> nodes = new List<HydraulicNode>
        {
            new HydraulicNode
            {
                NodeId = "Remote " + (remote?.Label ?? "Sprinkler"),
                Location = remote?.Location ?? new Point3D(),
                PressurePsi = remotePressurePsi,
                FlowGpm = remoteFlow,
                LengthFeet = 0.0,
                DiameterInches = 0.0,
                FrictionLossPsi = 0.0,
                SegmentType = "Sprinkler"
            }
        };

        foreach (HydraulicGraphSegment segment in path.SegmentChain)
        {
            nodes.Add(new HydraulicNode
            {
                NodeId = string.IsNullOrWhiteSpace(segment.SegmentId)
                    ? segment.Description
                    : segment.SegmentId,
                Location = segment.End,
                PressurePsi = segment.UpstreamPressurePsi,
                FlowGpm = segment.FlowGpm,
                LengthFeet = segment.LengthFeet,
                DiameterInches = segment.DiameterInches,
                FrictionLossPsi = segment.FrictionLossPsi,
                SegmentType = segment.SegmentType
            });
        }

        HydraulicGraphSegment lastSegment = path.SegmentChain[path.SegmentChain.Count - 1];
        nodes.Add(new HydraulicNode
        {
            NodeId = "Riser / Source",
            Location = path.SourcePoint,
            PressurePsi = lastSegment.UpstreamPressurePsi,
            FlowGpm = lastSegment.FlowGpm,
            LengthFeet = 0.0,
            DiameterInches = lastSegment.DiameterInches,
            FrictionLossPsi = 0.0,
            SegmentType = "Source"
        });

        return nodes;
    }

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
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
