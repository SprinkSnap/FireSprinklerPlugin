using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public sealed class RevitPipeConnectionResult
{
    public int ConnectedJointCount { get; set; }

    public int SkippedConnectionCount { get; set; }

    public int CreatedFittingCount { get; set; }

    public IList<string> Messages { get; set; } = new List<string>();
}

public static class RevitPipeConnectionService
{
    private const double ConnectorToleranceFeet = 0.35;

    public static RevitPipeConnectionResult ConnectRoomPipes(
        Document document,
        IList<PipeSegment> segments,
        IList<PlacedPipeRecord> placedPipes,
        string roomNumber)
    {
        RevitPipeConnectionResult result = new RevitPipeConnectionResult();
        if (document == null || segments == null || segments.Count == 0 || placedPipes == null || placedPipes.Count == 0)
        {
            return result;
        }

        SchematicPipeConnectionPlan plan = SchematicPipeConnectionPlanner.Plan(segments);
        Dictionary<int, PlacedPipeRecord> pipesBySegmentIndex = placedPipes
            .GroupBy(record => record.SegmentIndex)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (PipeConnectionIntent intent in plan.Connections
                     .Where(intent => intent.Kind != PipeConnectionKind.Takeoff)
                     .OrderBy(intent => ConnectionPriority(intent.Kind)))
        {
            ExecuteIntent(document, intent, pipesBySegmentIndex, roomNumber, result);
        }

        foreach (IGrouping<int, PipeConnectionIntent> takeoffGroup in plan.Connections
                     .Where(intent => intent.Kind == PipeConnectionKind.Takeoff)
                     .GroupBy(intent => intent.SegmentIndexA)
                     .OrderBy(group => group.Key))
        {
            Pipe currentTrunk = pipesBySegmentIndex.TryGetValue(takeoffGroup.Key, out PlacedPipeRecord trunkRecord)
                ? trunkRecord.Pipe
                : null;
            foreach (PipeConnectionIntent intent in takeoffGroup.OrderBy(item => item.Location.X))
            {
                if (currentTrunk == null
                    || !pipesBySegmentIndex.TryGetValue(intent.SegmentIndexB, out PlacedPipeRecord branchRecord))
                {
                    result.SkippedConnectionCount++;
                    continue;
                }

                if (TryCreateTakeoffConnection(
                        document,
                        ref currentTrunk,
                        branchRecord.Pipe,
                        ToXyz(intent.Location),
                        roomNumber,
                        result))
                {
                    result.ConnectedJointCount++;
                    pipesBySegmentIndex[takeoffGroup.Key] = new PlacedPipeRecord
                    {
                        SegmentIndex = takeoffGroup.Key,
                        Segment = trunkRecord.Segment,
                        Pipe = currentTrunk
                    };
                }
                else
                {
                    result.SkippedConnectionCount++;
                }
            }
        }

        if (result.ConnectedJointCount > 0)
        {
            result.Messages.Add(
                "Connected "
                + result.ConnectedJointCount
                + " routing joint(s) with "
                + result.CreatedFittingCount
                + " Revit fitting(s).");
        }

        return result;
    }

    private static void ExecuteIntent(
        Document document,
        PipeConnectionIntent intent,
        Dictionary<int, PlacedPipeRecord> pipesBySegmentIndex,
        string roomNumber,
        RevitPipeConnectionResult result)
    {
        if (!pipesBySegmentIndex.TryGetValue(intent.SegmentIndexA, out PlacedPipeRecord pipeA)
            || !pipesBySegmentIndex.TryGetValue(intent.SegmentIndexB, out PlacedPipeRecord pipeB))
        {
            result.SkippedConnectionCount++;
            return;
        }

        XYZ location = ToXyz(intent.Location);
        try
        {
            bool connected = false;
            switch (intent.Kind)
            {
                case PipeConnectionKind.Direct:
                    connected = TryConnectDirect(pipeA.Pipe, pipeB.Pipe, location);
                    break;
                case PipeConnectionKind.Elbow:
                    connected = TryCreateElbow(document, pipeA.Pipe, pipeB.Pipe, location, roomNumber, result);
                    break;
                case PipeConnectionKind.Tee:
                    if (pipesBySegmentIndex.TryGetValue(intent.SegmentIndexC, out PlacedPipeRecord pipeC))
                    {
                        connected = TryCreateTee(document, pipeA.Pipe, pipeB.Pipe, pipeC.Pipe, location, roomNumber, result);
                    }

                    break;
            }

            if (connected)
            {
                result.ConnectedJointCount++;
            }
            else
            {
                result.SkippedConnectionCount++;
            }
        }
        catch (Exception ex)
        {
            result.SkippedConnectionCount++;
            result.Messages.Add(ex.Message);
        }
    }

    private static int ConnectionPriority(PipeConnectionKind kind)
    {
        switch (kind)
        {
            case PipeConnectionKind.Direct:
                return 0;
            case PipeConnectionKind.Elbow:
                return 1;
            case PipeConnectionKind.Tee:
                return 2;
            default:
                return 3;
        }
    }

    private static bool TryConnectDirect(Pipe firstPipe, Pipe secondPipe, XYZ location)
    {
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        if (firstConnector == null || secondConnector == null)
        {
            return false;
        }

        firstConnector.ConnectTo(secondConnector);
        return true;
    }

    private static bool TryCreateElbow(
        Document document,
        Pipe firstPipe,
        Pipe secondPipe,
        XYZ location,
        string roomNumber,
        RevitPipeConnectionResult result)
    {
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        if (firstConnector == null || secondConnector == null)
        {
            return false;
        }

        FamilyInstance fitting = document.Create.NewElbowFitting(firstConnector, secondConnector);
        if (fitting != null)
        {
            TagFitting(fitting, "Elbow", roomNumber);
            result.CreatedFittingCount++;
            return true;
        }

        firstConnector.ConnectTo(secondConnector);
        return true;
    }

    private static bool TryCreateTee(
        Document document,
        Pipe firstPipe,
        Pipe secondPipe,
        Pipe branchPipe,
        XYZ location,
        string roomNumber,
        RevitPipeConnectionResult result)
    {
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        Connector branchConnector = FindOpenConnectorNear(branchPipe, location);
        if (firstConnector == null || secondConnector == null || branchConnector == null)
        {
            return false;
        }

        FamilyInstance tee = document.Create.NewTeeFitting(firstConnector, secondConnector, branchConnector);
        if (tee == null)
        {
            return false;
        }

        TagFitting(tee, "Tee", roomNumber);
        result.CreatedFittingCount++;
        return true;
    }

    private static bool TryCreateTakeoffConnection(
        Document document,
        ref Pipe trunkPipe,
        Pipe branchPipe,
        XYZ location,
        string roomNumber,
        RevitPipeConnectionResult result)
    {
        if (TrySplitTrunkAtLocation(document, trunkPipe, location, out Pipe upstreamPipe, out Pipe downstreamPipe))
        {
            trunkPipe = downstreamPipe ?? trunkPipe;
            Connector upstreamConnector = FindOpenConnectorNear(upstreamPipe, location);
            Connector downstreamConnector = FindOpenConnectorNear(downstreamPipe, location);
            Connector branchConnector = FindOpenConnectorNear(branchPipe, location);
            if (upstreamConnector != null && downstreamConnector != null && branchConnector != null)
            {
                FamilyInstance tee = document.Create.NewTeeFitting(upstreamConnector, downstreamConnector, branchConnector);
                if (tee != null)
                {
                    TagFitting(tee, "Tee", roomNumber);
                    result.CreatedFittingCount++;
                    return true;
                }
            }
        }

        Connector trunkConnector = FindOpenConnectorNear(trunkPipe, location);
        Connector branchConnectorOnly = FindOpenConnectorNear(branchPipe, location);
        if (trunkConnector == null || branchConnectorOnly == null)
        {
            return false;
        }

        FamilyInstance takeoff = document.Create.NewTakeoffFitting(branchConnectorOnly, trunkConnector);
        if (takeoff == null)
        {
            return false;
        }

        TagFitting(takeoff, "Tee", roomNumber);
        result.CreatedFittingCount++;
        return true;
    }

    private static bool TrySplitTrunkAtLocation(
        Document document,
        Pipe trunkPipe,
        XYZ location,
        out Pipe upstreamPipe,
        out Pipe downstreamPipe)
    {
        upstreamPipe = trunkPipe;
        downstreamPipe = null;

        LocationCurve locationCurve = trunkPipe?.Location as LocationCurve;
        if (locationCurve?.Curve == null)
        {
            return false;
        }

        Curve curve = locationCurve.Curve;
        IntersectionResult projection = curve.Project(location);
        if (projection == null)
        {
            return false;
        }

        XYZ breakPoint = projection.XYZPoint;
        if (breakPoint.DistanceTo(curve.GetEndPoint(0)) < ConnectorToleranceFeet
            || breakPoint.DistanceTo(curve.GetEndPoint(1)) < ConnectorToleranceFeet)
        {
            return false;
        }

        ElementId downstreamId = PlumbingUtils.BreakCurve(document, trunkPipe.Id, breakPoint);
        upstreamPipe = document.GetElement(trunkPipe.Id) as Pipe;
        downstreamPipe = document.GetElement(downstreamId) as Pipe;
        if (upstreamPipe == null || downstreamPipe == null)
        {
            return false;
        }

        document.Regenerate();
        return true;
    }

    private static Connector FindOpenConnectorNear(Pipe pipe, XYZ location)
    {
        if (pipe?.ConnectorManager == null)
        {
            return null;
        }

        Connector bestConnector = null;
        double bestDistance = ConnectorToleranceFeet;
        foreach (Connector connector in pipe.ConnectorManager.Connectors)
        {
            if (connector == null || !connector.IsValidObject || connector.IsConnected)
            {
                continue;
            }

            double distance = connector.Origin.DistanceTo(location);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestConnector = connector;
            }
        }

        return bestConnector;
    }

    private static void TagFitting(FamilyInstance fitting, string jointType, string roomNumber)
    {
        if (fitting == null)
        {
            return;
        }

        SetParameter(fitting, "Comments", "SprinkSnap Schematic Fitting | Room " + roomNumber + " | " + jointType + " | connected");
        SetParameter(fitting, "SS_RoomNumber", roomNumber);
        SetParameter(fitting, "SS_SegmentType", jointType);
    }

    private static void SetParameter(Element element, string parameterName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Parameter parameter = element.LookupParameter(parameterName);
        if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
        {
            return;
        }

        parameter.Set(value);
    }

    private static XYZ ToXyz(Point3D point)
    {
        return new XYZ(point.X, point.Y, point.Z);
    }
}

public sealed class PlacedPipeRecord
{
    public int SegmentIndex { get; set; }

    public PipeSegment Segment { get; set; }

    public Pipe Pipe { get; set; }
}
