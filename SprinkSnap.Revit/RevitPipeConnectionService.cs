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

    public int TrunkSplitCount { get; set; }

    public IList<string> SkippedConnectionDetails { get; set; } = new List<string>();

    public IList<string> Messages { get; set; } = new List<string>();
}

public static class RevitPipeConnectionService
{
    private const double ConnectorToleranceFeet = 0.35;

    public static RevitPipeConnectionResult ConnectRoomPipes(
        Document document,
        IList<PipeSegment> segments,
        IList<PlacedPipeRecord> placedPipes,
        string roomNumber,
        PipePlacementRoomResult placementResult = null)
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
            ExecuteIntent(document, intent, pipesBySegmentIndex, roomNumber, placementResult, result);
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
                    RecordSkippedConnection(
                        result,
                        intent,
                        roomNumber,
                        currentTrunk == null ? "cross main pipe was not found" : "branch pipe was not found");
                    continue;
                }

                if (TryCreateTakeoffConnection(
                        document,
                        ref currentTrunk,
                        branchRecord.Pipe,
                        ToXyz(intent.Location),
                        roomNumber,
                        placementResult,
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
                    RecordSkippedConnection(
                        result,
                        intent,
                        roomNumber,
                        "could not create takeoff or tee at branch tie-in");
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

        foreach (string skipMessage in PipeConnectionDiagnostics.BuildSkipSummaryMessages(
                     result.SkippedConnectionCount,
                     result.SkippedConnectionDetails))
        {
            result.Messages.Add(skipMessage);
        }

        return result;
    }

    private static void ExecuteIntent(
        Document document,
        PipeConnectionIntent intent,
        Dictionary<int, PlacedPipeRecord> pipesBySegmentIndex,
        string roomNumber,
        PipePlacementRoomResult placementResult,
        RevitPipeConnectionResult result)
    {
        if (!pipesBySegmentIndex.TryGetValue(intent.SegmentIndexA, out PlacedPipeRecord pipeA)
            || !pipesBySegmentIndex.TryGetValue(intent.SegmentIndexB, out PlacedPipeRecord pipeB))
        {
            RecordSkippedConnection(
                result,
                intent,
                roomNumber,
                "one or both pipe segments were not placed");
            return;
        }

        XYZ location = ToXyz(intent.Location);
        try
        {
            bool connected = false;
            switch (intent.Kind)
            {
                case PipeConnectionKind.Direct:
                    connected = TryConnectDirect(pipeA.Pipe, pipeB.Pipe, location, out string directReason);
                    if (!connected)
                    {
                        RecordSkippedConnection(result, intent, roomNumber, directReason);
                        return;
                    }

                    break;
                case PipeConnectionKind.Elbow:
                    connected = TryCreateElbow(document, pipeA.Pipe, pipeB.Pipe, location, roomNumber, placementResult, result, out string elbowReason);
                    if (!connected)
                    {
                        RecordSkippedConnection(result, intent, roomNumber, elbowReason);
                        return;
                    }

                    break;
                case PipeConnectionKind.Tee:
                    if (pipesBySegmentIndex.TryGetValue(intent.SegmentIndexC, out PlacedPipeRecord pipeC))
                    {
                        connected = TryCreateTee(document, pipeA.Pipe, pipeB.Pipe, pipeC.Pipe, location, roomNumber, placementResult, result, out string teeReason);
                        if (!connected)
                        {
                            RecordSkippedConnection(result, intent, roomNumber, teeReason);
                            return;
                        }
                    }
                    else
                    {
                        RecordSkippedConnection(result, intent, roomNumber, "branch pipe segment was not placed");
                        return;
                    }

                    break;
            }

            if (connected)
            {
                result.ConnectedJointCount++;
            }
        }
        catch (Exception ex)
        {
            RecordSkippedConnection(result, intent, roomNumber, ex.Message);
        }
    }

    private static void RecordSkippedConnection(
        RevitPipeConnectionResult result,
        PipeConnectionIntent intent,
        string roomNumber,
        string reason)
    {
        result.SkippedConnectionCount++;
        result.SkippedConnectionDetails.Add(
            PipeConnectionDiagnostics.FormatSkippedIntent(roomNumber, intent.Kind, intent.Location, reason));
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

    private static bool TryConnectDirect(Pipe firstPipe, Pipe secondPipe, XYZ location, out string failureReason)
    {
        failureReason = string.Empty;
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        if (firstConnector == null || secondConnector == null)
        {
            failureReason = "open pipe connector not found near joint";
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
        PipePlacementRoomResult placementResult,
        RevitPipeConnectionResult result,
        out string failureReason)
    {
        failureReason = string.Empty;
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        if (firstConnector == null || secondConnector == null)
        {
            failureReason = "open pipe connector not found near elbow";
            return false;
        }

        double diameterInches = ResolveElbowDiameterInches(firstPipe, secondPipe);
        FamilyInstance fitting = document.Create.NewElbowFitting(firstConnector, secondConnector);
        if (fitting != null)
        {
            string description = diameterInches.ToString("0.##") + "\" elbow at connector routing joint";
            TagFitting(fitting, PipeJointTypes.Elbow, roomNumber, diameterInches, description);
            RecordConnectedFitting(placementResult, fitting, PipeJointTypes.Elbow, diameterInches, description);
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
        PipePlacementRoomResult placementResult,
        RevitPipeConnectionResult result,
        out string failureReason)
    {
        failureReason = string.Empty;
        Connector firstConnector = FindOpenConnectorNear(firstPipe, location);
        Connector secondConnector = FindOpenConnectorNear(secondPipe, location);
        Connector branchConnector = FindOpenConnectorNear(branchPipe, location);
        if (firstConnector == null || secondConnector == null || branchConnector == null)
        {
            failureReason = "open pipe connector not found near tee";
            return false;
        }

        double diameterInches = ResolveTeeDiameterInches(branchPipe, firstPipe, secondPipe);
        FamilyInstance tee = document.Create.NewTeeFitting(firstConnector, secondConnector, branchConnector);
        if (tee == null)
        {
            failureReason = "Revit could not create tee fitting";
            return false;
        }

        string description = diameterInches.ToString("0.##") + "\" tee at connector routing joint";
        TagFitting(tee, PipeJointTypes.Tee, roomNumber, diameterInches, description);
        RecordConnectedFitting(placementResult, tee, PipeJointTypes.Tee, diameterInches, description);
        result.CreatedFittingCount++;
        return true;
    }

    private static bool TryCreateTakeoffConnection(
        Document document,
        ref Pipe trunkPipe,
        Pipe branchPipe,
        XYZ location,
        string roomNumber,
        PipePlacementRoomResult placementResult,
        RevitPipeConnectionResult result)
    {
        double diameterInches = ResolveTeeDiameterInches(branchPipe, trunkPipe);
        if (TrySplitTrunkAtLocation(document, trunkPipe, location, out Pipe upstreamPipe, out Pipe downstreamPipe))
        {
            result.TrunkSplitCount++;
            trunkPipe = downstreamPipe ?? trunkPipe;
            Connector upstreamConnector = FindOpenConnectorNear(upstreamPipe, location);
            Connector downstreamConnector = FindOpenConnectorNear(downstreamPipe, location);
            Connector branchConnector = FindOpenConnectorNear(branchPipe, location);
            if (upstreamConnector != null && downstreamConnector != null && branchConnector != null)
            {
                FamilyInstance tee = document.Create.NewTeeFitting(upstreamConnector, downstreamConnector, branchConnector);
                if (tee != null)
                {
                    string description = diameterInches.ToString("0.##") + "\" tee at cross main tie-in";
                    TagFitting(tee, PipeJointTypes.Tee, roomNumber, diameterInches, description);
                    RecordConnectedFitting(placementResult, tee, PipeJointTypes.Tee, diameterInches, description);
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

        string takeoffDescription = diameterInches.ToString("0.##") + "\" takeoff at cross main tie-in";
        TagFitting(takeoff, PipeJointTypes.Tee, roomNumber, diameterInches, takeoffDescription);
        RecordConnectedFitting(placementResult, takeoff, PipeJointTypes.Tee, diameterInches, takeoffDescription);
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

    private static void TagFitting(
        FamilyInstance fitting,
        string jointType,
        string roomNumber,
        double diameterInches,
        string description)
    {
        if (fitting == null)
        {
            return;
        }

        SetParameter(
            fitting,
            "Comments",
            "SprinkSnap Schematic Fitting | Room "
            + roomNumber
            + " | "
            + jointType
            + " | "
            + diameterInches.ToString("0.##")
            + "\" | connected");
        SetParameter(fitting, "SS_RoomNumber", roomNumber);
        SetParameter(fitting, "SS_SegmentType", jointType);
        SetParameter(fitting, "SS_PlacementBasis", description);
    }

    private static void RecordConnectedFitting(
        PipePlacementRoomResult placementResult,
        FamilyInstance fitting,
        string jointType,
        double diameterInches,
        string description)
    {
        if (placementResult == null || fitting == null)
        {
            return;
        }

        placementResult.PlacedFittingElementIds.Add(fitting.Id.IntegerValue);
        placementResult.PlacedFittings.Add(new PipePlacementFittingResult
        {
            JointType = jointType,
            DiameterInches = diameterInches,
            PlacedElementId = fitting.Id.IntegerValue,
            Description = description
        });
        placementResult.PlacedFittingCount++;
    }

    private static double ResolveElbowDiameterInches(Pipe firstPipe, Pipe secondPipe)
    {
        double firstDiameter = ReadPipeDiameterInches(firstPipe);
        double secondDiameter = ReadPipeDiameterInches(secondPipe);
        return Math.Max(firstDiameter, secondDiameter);
    }

    private static double ResolveTeeDiameterInches(Pipe branchPipe, params Pipe[] runPipes)
    {
        double branchDiameter = ReadPipeDiameterInches(branchPipe);
        if (branchDiameter > 0)
        {
            return branchDiameter;
        }

        return runPipes
            .Select(ReadPipeDiameterInches)
            .Where(diameter => diameter > 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static double ReadPipeDiameterInches(Pipe pipe)
    {
        Parameter diameter = pipe?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
        if (diameter != null && diameter.HasValue)
        {
            return diameter.AsDouble();
        }

        if (pipe?.PipeType != null)
        {
            Parameter typeDiameter = pipe.PipeType.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (typeDiameter != null && typeDiameter.HasValue)
            {
                return typeDiameter.AsDouble();
            }
        }

        return 0.0;
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
