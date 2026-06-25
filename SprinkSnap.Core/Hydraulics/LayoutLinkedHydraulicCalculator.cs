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
        PipePlacementSummary pipePlacementSummary = null,
        HydraulicSupplyAnchor supplyAnchor = null,
        double remoteAreaSquareFeet = 0,
        double maxCoverageSquareFeet = 0)
    {
        List<RoomInfo> roomList = controllingRooms?.ToList() ?? new List<RoomInfo>();
        HydraulicSupplyAnchorService.PrepareRouting(
            schematicPipeRouting,
            roomList,
            supplyAnchor,
            pipePlacementSummary);

        Point3D sourcePoint = HydraulicSupplyAnchorService.ResolveSourcePoint(
            roomList,
            schematicPipeRouting,
            supplyAnchor);
        IList<LayoutSprinklerPoint> sprinklerPoints = HydraulicGraphBuilder.CollectSprinklerPoints(
            controllingRooms,
            defaultKFactor);
        IList<LayoutSprinklerPoint> operatingSprinklers = HydraulicGraphBuilder.SelectOperatingSprinklers(
            sprinklerPoints,
            sourcePoint,
            operatingSprinklerCount,
            remoteAreaSquareFeet,
            maxCoverageSquareFeet,
            schematicPipeRouting);

        LayoutLinkedHydraulicPath path = HydraulicGraphBuilder.BuildPath(
            controllingRooms,
            operatingSprinklers,
            sourcePoint,
            branchDiameterInches,
            mainDiameterInches,
            schematicPipeRouting,
            pipePlacementSummary,
            supplyAnchor);
        path.UsesRemoteAreaSelection = remoteAreaSquareFeet > 0 && maxCoverageSquareFeet > 0;
        if (path.UsesRemoteAreaSelection)
        {
            path.Warnings.Add(
                "Operating sprinklers selected from an NFPA remote-area rectangle along the branch/cross-main layout axis.");
        }

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
                schematicPipeRouting,
                pipePlacementSummary,
                supplyAnchor);
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
            HydraulicVelocityValidator.ValidateFallbackCriticalPath(
                path,
                totalSprinklerFlow,
                hoseStreamAllowanceGpm);
            HydraulicPipeSizingService.ApplyCriticalPathSuggestions(path);
        }

        path.Warnings.Add(
            "Layout-linked hydraulics solved "
            + operatingSprinklers.Count
            + " operating head(s) with Q = K√P and "
            + (path.UsesSegmentGraphHydraulics
                ? path.CriticalPathSegmentCount + " segment-graph"
                : path.UsesPlacedPipeLengths ? "placed Revit" : path.PipeLengthDataSource.ToLowerInvariant())
            + " pipe lengths"
            + (path.CriticalPathFittingCount > 0
                ? " plus " + path.CriticalPathFittingCount + " fitting equivalent-length loss(es)."
                : "."));

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
        SchematicPipeRoutingSummary schematicPipeRouting,
        PipePlacementSummary pipePlacementSummary,
        HydraulicSupplyAnchor supplyAnchor)
    {
        HydraulicSegmentGraphBuilder.AssignSegmentFlows(
            path,
            headFlows,
            totalSprinklerFlowGpm,
            hoseStreamAllowanceGpm,
            schematicPipeRouting,
            pipePlacementSummary,
            supplyAnchor);

        IList<CriticalPathFitting> fittings = HydraulicCriticalPathFittingResolver.ResolveForSegmentChain(
            path.SegmentChain,
            schematicPipeRouting,
            pipePlacementSummary);
        ILookup<int, CriticalPathFitting> fittingsBySegment = fittings.ToLookup(fitting => fitting.SegmentIndex);

        double downstreamPressurePsi = remotePressurePsi;
        double branchFrictionPsi = 0.0;
        double mainFrictionPsi = 0.0;
        double fittingFrictionPsi = 0.0;

        for (int segmentIndex = 0; segmentIndex < path.SegmentChain.Count; segmentIndex++)
        {
            HydraulicGraphSegment segment = path.SegmentChain[segmentIndex];
            segment.DownstreamPressurePsi = downstreamPressurePsi;

            foreach (CriticalPathFitting fitting in fittingsBySegment[segmentIndex]
                .Where(item => item.AtSegmentStart)
                .OrderBy(item => item.Joint?.Description ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                downstreamPressurePsi = ApplyFittingFrictionLoss(
                    fitting,
                    segment,
                    downstreamPressurePsi,
                    ref fittingFrictionPsi);
            }

            segment.FrictionLossPsi = HazenWilliamsCalculator.FrictionLossPsi(
                segment.FlowGpm,
                segment.DiameterInches,
                segment.LengthFeet);
            downstreamPressurePsi += segment.FrictionLossPsi;

            if (IsBranchSegment(segment.SegmentType))
            {
                branchFrictionPsi += segment.FrictionLossPsi;
            }
            else
            {
                mainFrictionPsi += segment.FrictionLossPsi;
            }

            foreach (CriticalPathFitting fitting in fittingsBySegment[segmentIndex]
                .Where(item => !item.AtSegmentStart)
                .OrderBy(item => item.Joint?.Description ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                downstreamPressurePsi = ApplyFittingFrictionLoss(
                    fitting,
                    segment,
                    downstreamPressurePsi,
                    ref fittingFrictionPsi);
            }

            segment.UpstreamPressurePsi = downstreamPressurePsi;
        }

        path.BranchFrictionPsi = branchFrictionPsi;
        path.MainFrictionPsi = mainFrictionPsi;
        path.FittingFrictionPsi = fittingFrictionPsi;
        path.CriticalPathFittingCount = fittings.Count;
        path.JunctionPressurePsi = remotePressurePsi + branchFrictionPsi;
        path.CriticalPathDemandPsi = downstreamPressurePsi;
        path.CriticalPath = BuildSegmentCriticalPath(
            path,
            headFlows,
            hoseStreamAllowanceGpm,
            remotePressurePsi,
            fittings);
        HydraulicVelocityValidator.ValidateSegmentChain(path);
        HydraulicPipeSizingService.ApplySegmentChainSuggestions(path);
        ApplySegmentVelocityToCriticalPath(path);
    }

    private static void ApplySegmentVelocityToCriticalPath(LayoutLinkedHydraulicPath path)
    {
        if (path.CriticalPath == null || path.SegmentChain == null)
        {
            return;
        }

        Dictionary<string, HydraulicGraphSegment> segmentsByDescription = path.SegmentChain
            .GroupBy(segment => segment.Description ?? segment.SegmentId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (HydraulicNode node in path.CriticalPath)
        {
            if (node.DiameterInches <= 0 || node.FlowGpm <= 0)
            {
                continue;
            }

            HydraulicGraphSegment matchingSegment = path.SegmentChain.FirstOrDefault(segment =>
                string.Equals(segment.SegmentType, node.SegmentType, StringComparison.OrdinalIgnoreCase)
                && (
                    string.Equals(segment.Description, node.NodeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment.SegmentId, node.NodeId, StringComparison.OrdinalIgnoreCase)));
            if (matchingSegment == null && !string.IsNullOrWhiteSpace(node.NodeId))
            {
                segmentsByDescription.TryGetValue(node.NodeId, out matchingSegment);
            }

            if (matchingSegment != null)
            {
                node.VelocityFeetPerSecond = matchingSegment.VelocityFeetPerSecond;
                node.VelocityLimitFeetPerSecond = matchingSegment.VelocityLimitFeetPerSecond;
                node.ExceedsVelocityLimit = matchingSegment.ExceedsVelocityLimit;
                node.SuggestedDiameterInches = matchingSegment.SuggestedDiameterInches;
                continue;
            }

            HydraulicVelocityCheck check = HydraulicVelocityValidator.Evaluate(
                node.FlowGpm,
                node.DiameterInches,
                node.SegmentType);
            HydraulicVelocityValidator.ApplyVelocityToNode(node, check);
            node.SuggestedDiameterInches = HydraulicPipeSizingService.SuggestCompliantDiameterInches(
                node.FlowGpm,
                node.SegmentType,
                node.DiameterInches);
        }
    }

    private static double ApplyFittingFrictionLoss(
        CriticalPathFitting fitting,
        HydraulicGraphSegment segment,
        double downstreamPressurePsi,
        ref double fittingFrictionPsi)
    {
        double diameterInches = fitting.Joint?.DiameterInches > 0
            ? fitting.Joint.DiameterInches
            : segment.DiameterInches;
        fitting.FlowGpm = segment.FlowGpm;
        fitting.DownstreamPressurePsi = downstreamPressurePsi;
        fitting.FrictionLossPsi = HazenWilliamsCalculator.FrictionLossPsi(
            segment.FlowGpm,
            diameterInches,
            fitting.EquivalentLengthFeet);
        fitting.UpstreamPressurePsi = downstreamPressurePsi + fitting.FrictionLossPsi;
        fittingFrictionPsi += fitting.FrictionLossPsi;
        return fitting.UpstreamPressurePsi;
    }

    private static IList<HydraulicNode> BuildSegmentCriticalPath(
        LayoutLinkedHydraulicPath path,
        IDictionary<LayoutSprinklerPoint, double> headFlows,
        double hoseStreamAllowanceGpm,
        double remotePressurePsi,
        IList<CriticalPathFitting> fittings)
    {
        LayoutSprinklerPoint remote = path.MostRemoteSprinkler;
        double remoteFlow = headFlows.TryGetValue(remote, out double flow) ? flow : 0.0;
        ILookup<int, CriticalPathFitting> fittingsBySegment = fittings.ToLookup(fitting => fitting.SegmentIndex);
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

        foreach (CriticalPathFitting fitting in fittingsBySegment.SelectMany(group => group)
            .Where(item => item.SegmentIndex == 0 && item.AtSegmentStart)
            .OrderBy(item => item.Joint?.Description ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(CreateFittingNode(fitting));
        }

        for (int segmentIndex = 0; segmentIndex < path.SegmentChain.Count; segmentIndex++)
        {
            HydraulicGraphSegment segment = path.SegmentChain[segmentIndex];
            double pressureAfterPipePsi = segment.DownstreamPressurePsi + segment.FrictionLossPsi;
            nodes.Add(new HydraulicNode
            {
                NodeId = string.IsNullOrWhiteSpace(segment.SegmentId)
                    ? segment.Description
                    : segment.SegmentId,
                Location = segment.End,
                PressurePsi = pressureAfterPipePsi,
                FlowGpm = segment.FlowGpm,
                LengthFeet = segment.LengthFeet,
                DiameterInches = segment.DiameterInches,
                FrictionLossPsi = segment.FrictionLossPsi,
                SegmentType = segment.SegmentType
            });

            foreach (CriticalPathFitting fitting in fittingsBySegment[segmentIndex]
                .Where(item => !item.AtSegmentStart)
                .OrderBy(item => item.Joint?.Description ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(CreateFittingNode(fitting));
            }

            if (segmentIndex + 1 < path.SegmentChain.Count)
            {
                foreach (CriticalPathFitting fitting in fittingsBySegment[segmentIndex + 1]
                    .Where(item => item.AtSegmentStart)
                    .OrderBy(item => item.Joint?.Description ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    nodes.Add(CreateFittingNode(fitting));
                }
            }
        }

        HydraulicGraphSegment lastSegment = path.SegmentChain[path.SegmentChain.Count - 1];
        nodes.Add(new HydraulicNode
        {
            NodeId = "Riser / Source",
            Location = path.SourcePoint,
            PressurePsi = path.CriticalPathDemandPsi > 0
                ? path.CriticalPathDemandPsi
                : lastSegment.UpstreamPressurePsi,
            FlowGpm = lastSegment.FlowGpm,
            LengthFeet = 0.0,
            DiameterInches = lastSegment.DiameterInches,
            FrictionLossPsi = 0.0,
            SegmentType = "Source"
        });

        return nodes;
    }

    private static HydraulicNode CreateFittingNode(CriticalPathFitting fitting)
    {
        PipeJoint joint = fitting.Joint;
        return new HydraulicNode
        {
            NodeId = string.IsNullOrWhiteSpace(joint?.Description)
                ? (joint?.JointType ?? "Fitting")
                : joint.Description,
            Location = joint?.Location ?? new Point3D(),
            PressurePsi = fitting.UpstreamPressurePsi,
            FlowGpm = fitting.FlowGpm,
            LengthFeet = fitting.EquivalentLengthFeet,
            DiameterInches = joint?.DiameterInches ?? 0.0,
            FrictionLossPsi = fitting.FrictionLossPsi,
            SegmentType = joint?.JointType ?? "Fitting"
        };
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
