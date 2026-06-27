using System;
using System.Collections.Generic;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipingSystemTypes
{
    public const string Tree = "Tree";

    public const string Grid = "Grid";

    public static IReadOnlyList<string> All { get; } = new[] { Tree, Grid };

    public static string Normalize(string systemType)
    {
        if (string.Equals(systemType, Grid, StringComparison.OrdinalIgnoreCase))
        {
            return Grid;
        }

        return Tree;
    }

    public static bool IsGrid(string systemType)
    {
        return string.Equals(Normalize(systemType), Grid, StringComparison.Ordinal);
    }
}

public static class PipeScheduleTypes
{
    public const string Schedule40 = "Schedule 40";

    public const string Schedule10 = "Schedule 10";

    public static IReadOnlyList<string> All { get; } = new[] { Schedule40, Schedule10 };

    public static string Normalize(string schedule)
    {
        if (string.Equals(schedule, Schedule10, StringComparison.OrdinalIgnoreCase)
            || schedule.Contains("10", StringComparison.OrdinalIgnoreCase))
        {
            return Schedule10;
        }

        return Schedule40;
    }
}
