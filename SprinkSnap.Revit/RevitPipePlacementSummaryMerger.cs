using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

internal static class RevitPipePlacementSummaryMerger
{
    public static void ApplyRoutingMetadata(PipePlacementSummary target, PipePlacementSummary routingSummary)
    {
        if (target == null || routingSummary == null)
        {
            return;
        }

        target.ConnectedJointCount = routingSummary.ConnectedJointCount;
        target.ConnectedFittingCount = routingSummary.ConnectedFittingCount;
        target.SkippedConnectionCount = routingSummary.SkippedConnectionCount;
        target.TrunkSplitCount = routingSummary.TrunkSplitCount;
        target.TotalFittingCount = routingSummary.TotalFittingCount;
        target.SkippedFittingCount = routingSummary.SkippedFittingCount;
        target.FailedSegmentCount = routingSummary.FailedSegmentCount;
        target.SkippedSegmentCount = routingSummary.SkippedSegmentCount;
        target.TotalSegments = routingSummary.TotalSegments;

        Dictionary<string, PipePlacementRoomResult> routingRooms = routingSummary.RoomResults
            .Where(room => !string.IsNullOrWhiteSpace(room.RoomNumber))
            .GroupBy(room => room.RoomNumber, System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.OrdinalIgnoreCase);

        foreach (PipePlacementRoomResult measuredRoom in target.RoomResults)
        {
            if (!routingRooms.TryGetValue(measuredRoom.RoomNumber, out PipePlacementRoomResult routingRoom))
            {
                continue;
            }

            measuredRoom.ConnectedJointCount = routingRoom.ConnectedJointCount;
            measuredRoom.ConnectedFittingCount = routingRoom.ConnectedFittingCount;
            measuredRoom.SkippedConnectionCount = routingRoom.SkippedConnectionCount;
            measuredRoom.TrunkSplitCount = routingRoom.TrunkSplitCount;
            if (!string.IsNullOrWhiteSpace(routingRoom.Message))
            {
                measuredRoom.Message = routingRoom.Message;
            }
        }

        List<string> routingMessages = routingSummary.Messages?
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList() ?? new List<string>();
        foreach (string message in routingMessages)
        {
            if (!target.Messages.Contains(message))
            {
                target.Messages.Add(message);
            }
        }
    }
}
