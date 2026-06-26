using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public sealed class HydraulicVelocityCheck
{
    public double VelocityFeetPerSecond { get; set; }

    public double VelocityLimitFeetPerSecond { get; set; }

    public bool ExceedsLimit { get; set; }
}

public static class HydraulicVelocityValidator
{
    public const double BranchVelocityLimitFeetPerSecond = VelocityLimitDefaults.DefaultBranchVelocityLimitFeetPerSecond;

    public const double MainVelocityLimitFeetPerSecond = VelocityLimitDefaults.DefaultMainVelocityLimitFeetPerSecond;

    public static double CalculateVelocityFeetPerSecond(double flowGpm, double diameterInches)
    {
        if (flowGpm <= 0 || diameterInches <= 0)
        {
            return 0.0;
        }

        return 0.408 * flowGpm / (diameterInches * diameterInches);
    }

    public static double ResolveVelocityLimitFeetPerSecond(
        string segmentType,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (string.Equals(segmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
        {
            return VelocityLimitDefaults.ResolveBranchVelocityLimitFeetPerSecond(preferences);
        }

        return VelocityLimitDefaults.ResolveMainVelocityLimitFeetPerSecond(preferences);
    }

    public static HydraulicVelocityCheck Evaluate(
        double flowGpm,
        double diameterInches,
        string segmentType,
        SprinkSnapProjectPreferences preferences = null)
    {
        double velocityFeetPerSecond = CalculateVelocityFeetPerSecond(flowGpm, diameterInches);
        double velocityLimitFeetPerSecond = ResolveVelocityLimitFeetPerSecond(segmentType, preferences);
        return new HydraulicVelocityCheck
        {
            VelocityFeetPerSecond = velocityFeetPerSecond,
            VelocityLimitFeetPerSecond = velocityLimitFeetPerSecond,
            ExceedsLimit = velocityFeetPerSecond > velocityLimitFeetPerSecond
        };
    }

    public static int ValidateSegmentChain(
        LayoutLinkedHydraulicPath path,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (path == null)
        {
            return 0;
        }

        if (path.SegmentChain == null || path.SegmentChain.Count == 0)
        {
            path.CriticalPathVelocityViolationCount = 0;
            path.MaxCriticalPathVelocityFeetPerSecond = 0.0;
            return 0;
        }

        double branchLimit = VelocityLimitDefaults.ResolveBranchVelocityLimitFeetPerSecond(preferences);
        double mainLimit = VelocityLimitDefaults.ResolveMainVelocityLimitFeetPerSecond(preferences);
        int violationCount = 0;
        double maxVelocityFeetPerSecond = 0.0;
        foreach (HydraulicGraphSegment segment in path.SegmentChain)
        {
            HydraulicVelocityCheck check = Evaluate(segment.FlowGpm, segment.DiameterInches, segment.SegmentType, preferences);
            segment.VelocityFeetPerSecond = check.VelocityFeetPerSecond;
            segment.VelocityLimitFeetPerSecond = check.VelocityLimitFeetPerSecond;
            segment.ExceedsVelocityLimit = check.ExceedsLimit;
            maxVelocityFeetPerSecond = Math.Max(maxVelocityFeetPerSecond, check.VelocityFeetPerSecond);

            if (!check.ExceedsLimit)
            {
                continue;
            }

            violationCount++;
            path.Warnings.Add(
                "Velocity limit exceeded on "
                + DescribeSegment(segment)
                + ": "
                + check.VelocityFeetPerSecond.ToString("N1")
                + " ft/s > "
                + check.VelocityLimitFeetPerSecond.ToString("N0")
                + " ft/s at "
                + segment.FlowGpm.ToString("N1")
                + " GPM through "
                + segment.DiameterInches.ToString("0.##")
                + "\" pipe.");
        }

        path.CriticalPathVelocityViolationCount = violationCount;
        path.MaxCriticalPathVelocityFeetPerSecond = maxVelocityFeetPerSecond;
        if (violationCount > 0)
        {
            path.Warnings.Add(
                violationCount
                + " critical-path segment(s) exceed velocity limits ("
                + branchLimit.ToString("N0")
                + " ft/s branch, "
                + mainLimit.ToString("N0")
                + " ft/s main/riser).");
        }

        return violationCount;
    }

    public static int ValidateFallbackCriticalPath(
        LayoutLinkedHydraulicPath path,
        double totalSprinklerFlowGpm,
        double hoseStreamAllowanceGpm,
        SprinkSnapProjectPreferences preferences = null)
    {
        if (path?.CriticalPath == null || path.CriticalPath.Count == 0)
        {
            path.CriticalPathVelocityViolationCount = 0;
            path.MaxCriticalPathVelocityFeetPerSecond = 0.0;
            return 0;
        }

        int violationCount = 0;
        double maxVelocityFeetPerSecond = 0.0;
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

            HydraulicVelocityCheck check = Evaluate(node.FlowGpm, node.DiameterInches, node.SegmentType, preferences);
            ApplyVelocityToNode(node, check);
            maxVelocityFeetPerSecond = Math.Max(maxVelocityFeetPerSecond, check.VelocityFeetPerSecond);
            if (!check.ExceedsLimit)
            {
                continue;
            }

            violationCount++;
            path.Warnings.Add(
                "Velocity limit exceeded on "
                + node.NodeId
                + ": "
                + check.VelocityFeetPerSecond.ToString("N1")
                + " ft/s > "
                + check.VelocityLimitFeetPerSecond.ToString("N0")
                + " ft/s.");
        }

        path.CriticalPathVelocityViolationCount = violationCount;
        path.MaxCriticalPathVelocityFeetPerSecond = maxVelocityFeetPerSecond;
        return violationCount;
    }

    public static void ApplyVelocityToNode(HydraulicNode node, HydraulicVelocityCheck check)
    {
        node.VelocityFeetPerSecond = check.VelocityFeetPerSecond;
        node.VelocityLimitFeetPerSecond = check.VelocityLimitFeetPerSecond;
        node.ExceedsVelocityLimit = check.ExceedsLimit;
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
