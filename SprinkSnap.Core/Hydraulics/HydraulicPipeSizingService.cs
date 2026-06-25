using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicPipeSizingService
{
    private static readonly double[] StandardDiameterInches =
    {
        0.75,
        1.0,
        1.25,
        1.5,
        2.0,
        2.5,
        3.0,
        3.5,
        4.0,
        5.0,
        6.0,
        8.0,
        10.0,
        12.0
    };

    public static IReadOnlyList<double> GetStandardDiametersInches()
    {
        return StandardDiameterInches;
    }

    public static double CalculateMinimumDiameterInches(double flowGpm, string segmentType)
    {
        if (flowGpm <= 0)
        {
            return 0.0;
        }

        double velocityLimitFeetPerSecond = HydraulicVelocityValidator.ResolveVelocityLimitFeetPerSecond(segmentType);
        if (velocityLimitFeetPerSecond <= 0)
        {
            return 0.0;
        }

        return Math.Sqrt(0.408 * flowGpm / velocityLimitFeetPerSecond);
    }

    public static double RoundUpToStandardDiameterInches(double minimumDiameterInches)
    {
        if (minimumDiameterInches <= 0)
        {
            return 0.0;
        }

        foreach (double diameterInches in StandardDiameterInches)
        {
            if (diameterInches + 0.001 >= minimumDiameterInches)
            {
                return diameterInches;
            }
        }

        return StandardDiameterInches[StandardDiameterInches.Length - 1];
    }

    public static double SuggestCompliantDiameterInches(
        double flowGpm,
        string segmentType,
        double currentDiameterInches = 0)
    {
        double minimumDiameterInches = CalculateMinimumDiameterInches(flowGpm, segmentType);
        double compliantDiameterInches = RoundUpToStandardDiameterInches(minimumDiameterInches);
        if (currentDiameterInches <= 0)
        {
            return compliantDiameterInches;
        }

        HydraulicVelocityCheck currentCheck = HydraulicVelocityValidator.Evaluate(
            flowGpm,
            currentDiameterInches,
            segmentType);
        if (!currentCheck.ExceedsLimit)
        {
            return 0.0;
        }

        foreach (double diameterInches in StandardDiameterInches)
        {
            if (diameterInches + 0.001 < currentDiameterInches)
            {
                continue;
            }

            HydraulicVelocityCheck candidateCheck = HydraulicVelocityValidator.Evaluate(
                flowGpm,
                diameterInches,
                segmentType);
            if (!candidateCheck.ExceedsLimit)
            {
                return diameterInches;
            }
        }

        return StandardDiameterInches[StandardDiameterInches.Length - 1];
    }

    public static int ApplySegmentChainSuggestions(LayoutLinkedHydraulicPath path)
    {
        if (path?.SegmentChain == null || path.SegmentChain.Count == 0)
        {
            if (path != null)
            {
                path.CriticalPathDiameterSuggestionCount = 0;
            }

            return 0;
        }

        int suggestionCount = 0;
        foreach (HydraulicGraphSegment segment in path.SegmentChain)
        {
            double suggestedDiameterInches = SuggestCompliantDiameterInches(
                segment.FlowGpm,
                segment.SegmentType,
                segment.DiameterInches);
            segment.SuggestedDiameterInches = suggestedDiameterInches;
            if (suggestedDiameterInches <= 0
                || Math.Abs(suggestedDiameterInches - segment.DiameterInches) < 0.01)
            {
                continue;
            }

            suggestionCount++;
            path.Warnings.Add(
                "Suggest upsizing "
                + DescribeSegment(segment)
                + " from "
                + segment.DiameterInches.ToString("0.##")
                + "\" to "
                + suggestedDiameterInches.ToString("0.##")
                + "\" to meet "
                + segment.VelocityLimitFeetPerSecond.ToString("N0")
                + " ft/s velocity limit at "
                + segment.FlowGpm.ToString("N1")
                + " GPM.");
        }

        path.CriticalPathDiameterSuggestionCount = suggestionCount;
        if (suggestionCount > 0)
        {
            path.Warnings.Add(
                suggestionCount
                + " critical-path segment(s) have velocity-driven pipe upsizing suggestions.");
        }

        return suggestionCount;
    }

    public static int ApplyCriticalPathSuggestions(LayoutLinkedHydraulicPath path)
    {
        if (path?.CriticalPath == null || path.CriticalPath.Count == 0)
        {
            if (path != null)
            {
                path.CriticalPathDiameterSuggestionCount = 0;
            }

            return 0;
        }

        int suggestionCount = 0;
        foreach (HydraulicNode node in path.CriticalPath)
        {
            if (node.DiameterInches <= 0 || node.FlowGpm <= 0)
            {
                continue;
            }

            if (string.Equals(node.SegmentType, "Sprinkler", StringComparison.OrdinalIgnoreCase)
                || string.Equals(node.SegmentType, "Source", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double suggestedDiameterInches = SuggestCompliantDiameterInches(
                node.FlowGpm,
                node.SegmentType,
                node.DiameterInches);
            node.SuggestedDiameterInches = suggestedDiameterInches;
            if (suggestedDiameterInches <= 0
                || Math.Abs(suggestedDiameterInches - node.DiameterInches) < 0.01)
            {
                continue;
            }

            suggestionCount++;
            path.Warnings.Add(
                "Suggest upsizing "
                + node.NodeId
                + " from "
                + node.DiameterInches.ToString("0.##")
                + "\" to "
                + suggestedDiameterInches.ToString("0.##")
                + "\" to meet velocity limits.");
        }

        path.CriticalPathDiameterSuggestionCount = suggestionCount;
        return suggestionCount;
    }

    private static string DescribeSegment(HydraulicGraphSegment segment)
    {
        if (!string.IsNullOrWhiteSpace(segment.Description))
        {
            return segment.Description;
        }

        if (!string.IsNullOrWhiteSpace(segment.SegmentId))
        {
            return segment.SegmentId;
        }

        return segment.SegmentType ?? "segment";
    }
}
