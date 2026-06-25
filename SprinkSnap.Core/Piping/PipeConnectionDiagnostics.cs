using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipeConnectionDiagnostics
{
    public const int DefaultMaxDetailMessages = 5;

    public static string FormatSkippedIntent(
        string roomNumber,
        PipeConnectionKind kind,
        Point3D location,
        string reason)
    {
        string roomLabel = string.IsNullOrWhiteSpace(roomNumber) ? "Room ?" : "Room " + roomNumber;
        return roomLabel
            + ": skipped "
            + kind
            + " at ("
            + location.X.ToString("0.##")
            + ", "
            + location.Y.ToString("0.##")
            + ", "
            + location.Z.ToString("0.##")
            + ") — "
            + reason;
    }

    public static IList<string> BuildSkipSummaryMessages(
        int skippedConnectionCount,
        IEnumerable<string> detailMessages,
        int maxDetails = DefaultMaxDetailMessages)
    {
        List<string> messages = new List<string>();
        if (skippedConnectionCount <= 0)
        {
            return messages;
        }

        List<string> details = detailMessages?.Where(message => !string.IsNullOrWhiteSpace(message)).ToList()
            ?? new List<string>();
        messages.Add(
            skippedConnectionCount
            + " routing connection(s) could not be joined in Revit. Verify pipe fitting families and connector alignment.");

        int detailCount = Math.Min(details.Count, Math.Max(1, maxDetails));
        for (int index = 0; index < detailCount; index++)
        {
            messages.Add(details[index]);
        }

        int remaining = details.Count - detailCount;
        if (remaining > 0)
        {
            messages.Add("…and " + remaining + " more skipped connection(s).");
        }

        return messages;
    }
}
