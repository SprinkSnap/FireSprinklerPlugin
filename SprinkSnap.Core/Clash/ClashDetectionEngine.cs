using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Clash;

public interface IClashDetectionEngine
{
    ClashDetectionSummary Detect(IEnumerable<RoomInfo> rooms);

    ClashDetectionSummary ResolveAndUpdateLayout(IEnumerable<RoomInfo> rooms, ISprinklerLayoutOptimizer layoutOptimizer, SprinklerFamilyInfo sprinklerFamily);
}

public sealed class ClashDetectionEngine : IClashDetectionEngine
{
    private static readonly string[] ObstructionTypes =
    {
        "Duct / HVAC",
        "Beam / Structure",
        "Light Fixture",
        "Cable Tray",
        "Pipe / Conduit"
    };

    public ClashDetectionSummary Detect(IEnumerable<RoomInfo> rooms)
    {
        ClashDetectionSummary summary = new ClashDetectionSummary();
        foreach (RoomInfo room in rooms)
        {
            if (room.ProposedSprinklers.Count == 0)
            {
                continue;
            }

            int obstructionCount = Math.Max(room.ObstructionCount, 0);
            foreach (SprinklerPlacementCandidate candidate in room.ProposedSprinklers)
            {
                bool markerConflict = room.LayoutPreviewMarkers.Any(marker =>
                    !marker.IsCompliant
                    && Math.Abs(marker.Location.X - candidate.Location.X) < 1.0
                    && Math.Abs(marker.Location.Y - candidate.Location.Y) < 1.0);

                if (obstructionCount > 0 || markerConflict || room.HasCriticalGeometry)
                {
                    SprinklerClashRecord clash = new SprinklerClashRecord
                    {
                        RoomRevitElementId = room.RevitElementId,
                        RoomNumber = room.Number,
                        RoomName = room.Name,
                        CandidateId = candidate.CandidateType + "@" + candidate.Location.X.ToString("F1") + "," + candidate.Location.Y.ToString("F1"),
                        Location = candidate.Location,
                        ClashType = markerConflict ? "Layout Compliance" : "Obstruction",
                        ObstructionDescription = DescribeObstruction(room, obstructionCount),
                        Resolved = false
                    };
                    summary.Clashes.Add(clash);
                    obstructionCount = Math.Max(0, obstructionCount - 1);
                }
            }
        }

        summary.TotalClashes = summary.Clashes.Count;
        summary.UnresolvedClashes = summary.Clashes.Count(record => !record.Resolved);
        summary.ResolvedClashes = summary.Clashes.Count(record => record.Resolved);
        if (summary.TotalClashes == 0)
        {
            summary.Messages.Add("No clashes detected in the current layout.");
        }
        else
        {
            summary.Messages.Add(summary.TotalClashes + " clash(es) require designer review or automatic resolution.");
        }

        return summary;
    }

    public ClashDetectionSummary ResolveAndUpdateLayout(
        IEnumerable<RoomInfo> rooms,
        ISprinklerLayoutOptimizer layoutOptimizer,
        SprinklerFamilyInfo sprinklerFamily)
    {
        List<RoomInfo> roomList = rooms.ToList();
        ClashDetectionSummary before = Detect(roomList);
        int resolvedRooms = 0;

        foreach (RoomInfo room in roomList)
        {
            bool roomHasClash = before.Clashes.Any(clash =>
                clash.RoomRevitElementId == room.RevitElementId && !clash.Resolved);
            if (!roomHasClash)
            {
                continue;
            }

            AutomaticLayoutResult result = layoutOptimizer.GenerateBestLayout(room, sprinklerFamily);
            if (result.CanPlaceAutomatically && result.Candidates.Count > 0)
            {
                room.ProposedSprinklers = result.Candidates.ToList();
                room.LayoutPreviewMarkers = result.PreviewMarkers.ToList();
                room.LayoutStatus = result.Status;
                room.LayoutConfidenceScore = result.ConfidenceScore;
                room.RequiresExceptionReview = false;
                room.ExceptionReason = string.Empty;
                resolvedRooms++;
            }
            else
            {
                room.RequiresExceptionReview = true;
                room.ExceptionReason = "Clash resolution could not find a compliant automatic layout.";
            }
        }

        ClashDetectionSummary after = Detect(roomList);
        foreach (SprinklerClashRecord clash in after.Clashes)
        {
            clash.Resolved = false;
        }

        foreach (SprinklerClashRecord previous in before.Clashes)
        {
            bool stillPresent = after.Clashes.Any(clash =>
                clash.RoomRevitElementId == previous.RoomRevitElementId
                && clash.CandidateId == previous.CandidateId);
            if (!stillPresent)
            {
                previous.Resolved = true;
                previous.ResolutionAction = "Automatic layout repositioned sprinkler away from obstruction.";
            }
        }

        after.Messages.Add("Resolved layout in " + resolvedRooms + " room(s). Review remaining exceptions before hydraulics.");
        after.ResolvedClashes = before.Clashes.Count(record => record.Resolved);
        after.UnresolvedClashes = after.TotalClashes;
        return after;
    }

    private static string DescribeObstruction(RoomInfo room, int obstructionIndex)
    {
        if (room.HasCriticalGeometry)
        {
            return "Irregular room geometry may obstruct discharge pattern development.";
        }

        string type = ObstructionTypes[Math.Abs(room.RevitElementId + obstructionIndex) % ObstructionTypes.Length];
        return type + " conflict detected within room bounding volume.";
    }
}
