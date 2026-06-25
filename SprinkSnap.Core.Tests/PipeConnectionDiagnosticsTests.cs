using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class PipeConnectionDiagnosticsTests
{
    [Fact]
    public void FormatSkippedIntent_IncludesKindLocationAndReason()
    {
        string message = PipeConnectionDiagnostics.FormatSkippedIntent(
            "101",
            PipeConnectionKind.Takeoff,
            new Point3D(10, 12, 9),
            "open pipe connector not found near tee");

        Assert.Contains("Room 101", message);
        Assert.Contains("Takeoff", message);
        Assert.Contains("10", message);
        Assert.Contains("connector not found", message);
    }

    [Fact]
    public void BuildSkipSummaryMessages_IncludesCountAndDetails()
    {
        IList<string> messages = PipeConnectionDiagnostics.BuildSkipSummaryMessages(
            3,
            new[]
            {
                "Room 101: skipped Elbow at (1, 2, 3) — test",
                "Room 102: skipped Tee at (4, 5, 6) — test"
            });

        Assert.True(messages.Count >= 2);
        Assert.Contains(messages, message => message.Contains("3 routing connection"));
        Assert.Contains(messages, message => message.Contains("Room 101"));
    }

    [Fact]
    public void BuildSkipSummaryMessages_TruncatesLongDetailLists()
    {
        IList<string> messages = PipeConnectionDiagnostics.BuildSkipSummaryMessages(
            8,
            Enumerable.Range(1, 8).Select(index => "detail " + index).ToList(),
            maxDetails: 3);

        Assert.Contains(messages, message => message.Contains("…and 5 more"));
    }
}
