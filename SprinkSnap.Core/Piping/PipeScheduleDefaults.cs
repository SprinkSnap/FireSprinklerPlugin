using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipeScheduleDefaults
{
    public const double Schedule40HazenWilliamsC = 120.0;

    public const double Schedule10HazenWilliamsC = 120.0;

    public static string ResolveDefaultScheduleForSystem(string pipingSystemType)
    {
        return PipingSystemTypes.IsGrid(pipingSystemType)
            ? PipeScheduleTypes.Schedule10
            : PipeScheduleTypes.Schedule40;
    }

    public static string ResolvePipeSchedule(SprinkSnapProjectPreferences preferences)
    {
        if (preferences == null || string.IsNullOrWhiteSpace(preferences.DefaultPipeSchedule))
        {
            return ResolveDefaultScheduleForSystem(preferences?.PipingSystemType);
        }

        return PipeScheduleTypes.Normalize(preferences.DefaultPipeSchedule);
    }

    public static string ResolvePipingSystemType(SprinkSnapProjectPreferences preferences)
    {
        return PipingSystemTypes.Normalize(preferences?.PipingSystemType);
    }

    public static double ResolveHazenWilliamsC(SprinkSnapProjectPreferences preferences)
    {
        if (preferences?.HazenWilliamsC > 0)
        {
            return preferences.HazenWilliamsC;
        }

        return PipeScheduleTypes.Normalize(ResolvePipeSchedule(preferences)) == PipeScheduleTypes.Schedule10
            ? Schedule10HazenWilliamsC
            : Schedule40HazenWilliamsC;
    }

    public static string BuildSystemSummary(SprinkSnapProjectPreferences preferences)
    {
        string systemType = ResolvePipingSystemType(preferences);
        string schedule = ResolvePipeSchedule(preferences);
        return systemType
            + " / "
            + schedule
            + " ("
            + Nfpa13Edition.References.HazenWilliamsCFactors
            + " C="
            + ResolveHazenWilliamsC(preferences).ToString("0")
            + ")";
    }

    public static void ApplyRecommendedDefaults(SprinkSnapProjectPreferences preferences)
    {
        if (preferences == null)
        {
            return;
        }

        preferences.PipingSystemType = PipingSystemTypes.Normalize(preferences.PipingSystemType);
        if (string.IsNullOrWhiteSpace(preferences.DefaultPipeSchedule))
        {
            preferences.DefaultPipeSchedule = ResolveDefaultScheduleForSystem(preferences.PipingSystemType);
        }
        else
        {
            preferences.DefaultPipeSchedule = PipeScheduleTypes.Normalize(preferences.DefaultPipeSchedule);
        }

        if (preferences.HazenWilliamsC <= 0)
        {
            preferences.HazenWilliamsC = ResolveHazenWilliamsC(preferences);
        }
    }
}
