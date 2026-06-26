using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class HydraulicPipeSizingService
{
    public const int MaxSizingIterations = 4;

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

    public static double CalculateMinimumDiameterInches(
        double flowGpm,
        string segmentType,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (flowGpm <= 0)
        {
            return 0.0;
        }

        double velocityLimitFeetPerSecond = HydraulicVelocityValidator.ResolveVelocityLimitFeetPerSecond(
            segmentType,
            preferences);
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

    public static bool SegmentChainHasVelocityViolations(
        IEnumerable<HydraulicGraphSegment> segments,
        SprinkSnapProjectPreferences preferences = null)
    {
        foreach (HydraulicGraphSegment segment in segments ?? Enumerable.Empty<HydraulicGraphSegment>())
        {
            HydraulicVelocityCheck check = HydraulicVelocityValidator.Evaluate(
                segment.FlowGpm,
                segment.DiameterInches,
                segment.SegmentType,
                preferences);
            if (check.ExceedsLimit)
            {
                return true;
            }
        }

        return false;
    }

    public static int ApplyVelocityDrivenUpsizing(
        IList<HydraulicGraphSegment> segments,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (segments == null || segments.Count == 0)
        {
            return 0;
        }

        int appliedCount = 0;
        foreach (HydraulicGraphSegment segment in segments)
        {
            double suggestedDiameterInches = SuggestCompliantDiameterInches(
                segment.FlowGpm,
                segment.SegmentType,
                segment.DiameterInches,
                preferences);
            if (suggestedDiameterInches <= 0
                || suggestedDiameterInches <= segment.DiameterInches + 0.01)
            {
                continue;
            }

            segment.DiameterInches = suggestedDiameterInches;
            appliedCount++;
        }

        return appliedCount;
    }

    public static void RefreshFittingEquivalentLengths(
        IList<CriticalPathFitting> fittings,
        IList<HydraulicGraphSegment> segments)
    {
        if (fittings == null || segments == null)
        {
            return;
        }

        foreach (CriticalPathFitting fitting in fittings)
        {
            if (fitting.SegmentIndex < 0 || fitting.SegmentIndex >= segments.Count)
            {
                continue;
            }

            HydraulicGraphSegment segment = segments[fitting.SegmentIndex];
            double diameterInches = ResolveFittingDiameterInches(fitting, segment);
            fitting.EquivalentLengthFeet = FittingEquivalentLengthTable.GetEquivalentLengthFeet(
                fitting.Joint?.JointType ?? string.Empty,
                diameterInches);
        }
    }

    public static void SyncPathSummaryDiameters(LayoutLinkedHydraulicPath path)
    {
        if (path?.SegmentChain == null || path.SegmentChain.Count == 0)
        {
            return;
        }

        double branchDiameterInches = path.SegmentChain
            .Where(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
            .Select(segment => segment.DiameterInches)
            .DefaultIfEmpty(path.BranchDiameterInches)
            .Max();
        double mainDiameterInches = path.SegmentChain
            .Where(segment => !string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
            .Select(segment => segment.DiameterInches)
            .DefaultIfEmpty(path.MainDiameterInches)
            .Max();

        if (branchDiameterInches > 0)
        {
            path.BranchDiameterInches = branchDiameterInches;
        }

        if (mainDiameterInches > 0)
        {
            path.MainDiameterInches = mainDiameterInches;
        }
    }

    public static int ApplyFallbackPathUpsizing(
        LayoutLinkedHydraulicPath path,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (path?.CriticalPath == null || path.CriticalPath.Count == 0)
        {
            return 0;
        }

        int appliedCount = 0;
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
                node.DiameterInches,
                preferences);
            if (suggestedDiameterInches <= 0
                || suggestedDiameterInches <= node.DiameterInches + 0.01)
            {
                continue;
            }

            node.DiameterInches = suggestedDiameterInches;
            if (IsBranchSegment(node.SegmentType))
            {
                path.BranchDiameterInches = suggestedDiameterInches;
            }
            else
            {
                path.MainDiameterInches = suggestedDiameterInches;
            }

            appliedCount++;
        }

        return appliedCount;
    }

    public static double ResolveFittingDiameterInches(CriticalPathFitting fitting, HydraulicGraphSegment segment)
    {
        double jointDiameterInches = fitting.Joint?.DiameterInches ?? 0.0;
        if (jointDiameterInches <= 0)
        {
            return segment.DiameterInches;
        }

        return Math.Max(jointDiameterInches, segment.DiameterInches);
    }

    public static double SuggestCompliantDiameterInches(
        double flowGpm,
        string segmentType,
        double currentDiameterInches = 0,
        SprinkSnapProjectPreferences preferences = null)
    {
        double minimumDiameterInches = CalculateMinimumDiameterInches(flowGpm, segmentType, preferences);
        double compliantDiameterInches = RoundUpToStandardDiameterInches(minimumDiameterInches);
        if (currentDiameterInches <= 0)
        {
            return compliantDiameterInches;
        }

        HydraulicVelocityCheck currentCheck = HydraulicVelocityValidator.Evaluate(
            flowGpm,
            currentDiameterInches,
            segmentType,
            preferences);
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
                segmentType,
                preferences);
            if (!candidateCheck.ExceedsLimit)
            {
                return diameterInches;
            }
        }

        return StandardDiameterInches[StandardDiameterInches.Length - 1];
    }

    public static int ApplySegmentChainSuggestions(
        LayoutLinkedHydraulicPath path,
        SprinkSnapProjectPreferences preferences = null)
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
                segment.DiameterInches,
                preferences);
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

    public static int ApplyCriticalPathSuggestions(
        LayoutLinkedHydraulicPath path,
        SprinkSnapProjectPreferences preferences = null)
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
                node.DiameterInches,
                preferences);
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

    private static bool IsBranchSegment(string segmentType)
    {
        return string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase);
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
