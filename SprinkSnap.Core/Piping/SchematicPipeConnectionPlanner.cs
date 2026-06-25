using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeConnectionPlanner
{
    private const double LocationToleranceFeet = 0.15;

    private const double CollinearDotThreshold = 0.9;

    public static SchematicPipeConnectionPlan Plan(IList<PipeSegment> segments)
    {
        SchematicPipeConnectionPlan plan = new SchematicPipeConnectionPlan();
        if (segments == null || segments.Count == 0)
        {
            return plan;
        }

        List<EndpointRef> endpoints = BuildEndpoints(segments);
        foreach (IGrouping<string, EndpointRef> group in endpoints.GroupBy(endpoint => BuildLocationKey(endpoint.Point)))
        {
            List<EndpointRef> refs = group.ToList();
            if (refs.Count < 2)
            {
                continue;
            }

            if (refs.Count == 2)
            {
                PipeConnectionIntent intent = CreatePairIntent(refs[0], refs[1]);
                if (intent != null)
                {
                    plan.Connections.Add(intent);
                }

                continue;
            }

            PipeConnectionIntent teeIntent = CreateTeeIntent(refs);
            if (teeIntent != null)
            {
                plan.Connections.Add(teeIntent);
                continue;
            }

            AddPerpendicularPairIntents(refs, plan, segments);
        }

        AddTakeoffIntents(segments, plan);

        return plan;
    }

    private static void AddTakeoffIntents(IList<PipeSegment> segments, SchematicPipeConnectionPlan plan)
    {
        List<PipeSegment> crossMains = segments
            .Where(segment => string.Equals(segment.SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (crossMains.Count == 0)
        {
            return;
        }

        for (int index = 0; index < segments.Count; index++)
        {
            PipeSegment segment = segments[index];
            if (!string.Equals(segment.SegmentType, PipeSegmentTypes.Branch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string description = segment.Description ?? string.Empty;
            if (description.IndexOf("branch tie-in", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Point3D tieInPoint = segment.End;
            int crossMainIndex = FindCrossMainIndexForPoint(crossMains, segments, tieInPoint);
            if (crossMainIndex < 0)
            {
                continue;
            }

            PipeSegment crossMain = segments[crossMainIndex];
            bool onInterior = IsPointOnSegmentInterior(crossMain, tieInPoint);
            bool onEndpoint = IsPointOnSegment(crossMain, tieInPoint) && !onInterior;
            if (!onInterior && !(onEndpoint && CountSegmentsAtLocation(segments, tieInPoint) > 2))
            {
                continue;
            }

            plan.Connections.Add(new PipeConnectionIntent
            {
                Kind = PipeConnectionKind.Takeoff,
                SegmentIndexA = crossMainIndex,
                SegmentIndexB = index,
                Location = ClonePoint(tieInPoint)
            });
        }
    }

    private static int FindCrossMainIndexForPoint(
        IList<PipeSegment> crossMains,
        IList<PipeSegment> segments,
        Point3D point)
    {
        foreach (PipeSegment crossMain in crossMains)
        {
            if (IsPointOnSegment(crossMain, point))
            {
                return segments.IndexOf(crossMain);
            }
        }

        return -1;
    }

    private static bool IsPointOnSegmentInterior(PipeSegment segment, Point3D point)
    {
        Point3D start = segment.Start;
        Point3D end = segment.End;
        Point3D axis = Subtract(end, start);
        double lengthSquared = (axis.X * axis.X) + (axis.Y * axis.Y) + (axis.Z * axis.Z);
        if (lengthSquared <= 0.0001)
        {
            return LocationsMatch(start, point);
        }

        double parameter = (((point.X - start.X) * axis.X)
            + ((point.Y - start.Y) * axis.Y)
            + ((point.Z - start.Z) * axis.Z)) / lengthSquared;
        if (parameter <= 0.05 || parameter >= 0.95)
        {
            return false;
        }

        Point3D projected = new Point3D(
            start.X + (axis.X * parameter),
            start.Y + (axis.Y * parameter),
            start.Z + (axis.Z * parameter));
        return LocationsMatch(projected, point);
    }

    private static bool IsPointOnSegment(PipeSegment segment, Point3D point)
    {
        Point3D start = segment.Start;
        Point3D end = segment.End;
        Point3D axis = Subtract(end, start);
        double lengthSquared = (axis.X * axis.X) + (axis.Y * axis.Y) + (axis.Z * axis.Z);
        if (lengthSquared <= 0.0001)
        {
            return LocationsMatch(start, point);
        }

        double parameter = (((point.X - start.X) * axis.X)
            + ((point.Y - start.Y) * axis.Y)
            + ((point.Z - start.Z) * axis.Z)) / lengthSquared;
        if (parameter < -0.05 || parameter > 1.05)
        {
            return false;
        }

        Point3D projected = new Point3D(
            start.X + (axis.X * parameter),
            start.Y + (axis.Y * parameter),
            start.Z + (axis.Z * parameter));
        return LocationsMatch(projected, point);
    }

    private static bool LocationsMatch(Point3D left, Point3D right)
    {
        return Math.Abs(left.X - right.X) <= LocationToleranceFeet
            && Math.Abs(left.Y - right.Y) <= LocationToleranceFeet
            && Math.Abs(left.Z - right.Z) <= LocationToleranceFeet;
    }

    private static List<EndpointRef> BuildEndpoints(IList<PipeSegment> segments)
    {
        List<EndpointRef> endpoints = new List<EndpointRef>();
        for (int index = 0; index < segments.Count; index++)
        {
            PipeSegment segment = segments[index];
            endpoints.Add(new EndpointRef
            {
                SegmentIndex = index,
                IsStart = true,
                Point = segment.Start,
                Direction = Normalize(Subtract(segment.End, segment.Start))
            });
            endpoints.Add(new EndpointRef
            {
                SegmentIndex = index,
                IsStart = false,
                Point = segment.End,
                Direction = Normalize(Subtract(segment.Start, segment.End))
            });
        }

        return endpoints;
    }

    private static PipeConnectionIntent CreatePairIntent(EndpointRef first, EndpointRef second)
    {
        if (first.SegmentIndex == second.SegmentIndex)
        {
            return null;
        }

        double alignment = Dot(first.Direction, second.Direction);
        PipeConnectionKind kind = alignment >= CollinearDotThreshold
            ? PipeConnectionKind.Direct
            : PipeConnectionKind.Elbow;

        return new PipeConnectionIntent
        {
            Kind = kind,
            SegmentIndexA = first.SegmentIndex,
            IsStartA = first.IsStart,
            SegmentIndexB = second.SegmentIndex,
            IsStartB = second.IsStart,
            Location = ClonePoint(first.Point)
        };
    }

    private static PipeConnectionIntent CreateTeeIntent(IList<EndpointRef> refs)
    {
        for (int runA = 0; runA < refs.Count; runA++)
        {
            for (int runB = runA + 1; runB < refs.Count; runB++)
            {
                EndpointRef first = refs[runA];
                EndpointRef second = refs[runB];
                if (first.SegmentIndex == second.SegmentIndex)
                {
                    continue;
                }

                if (Dot(first.Direction, second.Direction) < CollinearDotThreshold)
                {
                    continue;
                }

                EndpointRef branch = refs.FirstOrDefault(candidate =>
                    candidate.SegmentIndex != first.SegmentIndex
                    && candidate.SegmentIndex != second.SegmentIndex
                    && Math.Abs(Dot(candidate.Direction, first.Direction)) < CollinearDotThreshold);
                if (branch == null)
                {
                    branch = refs.FirstOrDefault(candidate =>
                        candidate.SegmentIndex != first.SegmentIndex
                        && candidate.SegmentIndex != second.SegmentIndex);
                }

                if (branch == null)
                {
                    continue;
                }

                return new PipeConnectionIntent
                {
                    Kind = PipeConnectionKind.Tee,
                    SegmentIndexA = first.SegmentIndex,
                    IsStartA = first.IsStart,
                    SegmentIndexB = second.SegmentIndex,
                    IsStartB = second.IsStart,
                    SegmentIndexC = branch.SegmentIndex,
                    IsStartC = branch.IsStart,
                    Location = ClonePoint(first.Point)
                };
            }
        }

        return null;
    }

    private static void AddPerpendicularPairIntents(
        IList<EndpointRef> refs,
        SchematicPipeConnectionPlan plan,
        IList<PipeSegment> segments)
    {
        EndpointRef riserRef = refs.FirstOrDefault(reference =>
            string.Equals(segments[reference.SegmentIndex].SegmentType, PipeSegmentTypes.Riser, StringComparison.OrdinalIgnoreCase));
        EndpointRef crossMainRef = refs.FirstOrDefault(reference =>
            string.Equals(segments[reference.SegmentIndex].SegmentType, PipeSegmentTypes.CrossMain, StringComparison.OrdinalIgnoreCase));
        if (riserRef == null || crossMainRef == null)
        {
            return;
        }

        PipeConnectionIntent intent = CreatePairIntent(riserRef, crossMainRef);
        if (intent != null && intent.Kind == PipeConnectionKind.Elbow)
        {
            plan.Connections.Add(intent);
        }
    }

    private static int CountSegmentsAtLocation(IList<PipeSegment> segments, Point3D location)
    {
        int count = 0;
        for (int index = 0; index < segments.Count; index++)
        {
            PipeSegment segment = segments[index];
            if (LocationsMatch(segment.Start, location) || LocationsMatch(segment.End, location))
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildLocationKey(Point3D point)
    {
        return Math.Round(point.X / LocationToleranceFeet).ToString("0")
            + "|"
            + Math.Round(point.Y / LocationToleranceFeet).ToString("0")
            + "|"
            + Math.Round(point.Z / LocationToleranceFeet).ToString("0");
    }

    private static Point3D ClonePoint(Point3D point)
    {
        return new Point3D(point.X, point.Y, point.Z);
    }

    private static Point3D Subtract(Point3D end, Point3D start)
    {
        return new Point3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
    }

    private static Point3D Normalize(Point3D vector)
    {
        double length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (length <= 0.0001)
        {
            return new Point3D();
        }

        return new Point3D(vector.X / length, vector.Y / length, vector.Z / length);
    }

    private static double Dot(Point3D left, Point3D right)
    {
        return Math.Abs((left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z));
    }

    private sealed class EndpointRef
    {
        public int SegmentIndex { get; set; }

        public bool IsStart { get; set; }

        public Point3D Point { get; set; }

        public Point3D Direction { get; set; }
    }
}
