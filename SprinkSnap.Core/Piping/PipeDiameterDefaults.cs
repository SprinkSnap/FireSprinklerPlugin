using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class PipeDiameterDefaults
{
    public const double DefaultBranchDiameterInches = 1.25;

    public const double DefaultMainDiameterInches = 4.0;

    public static double ResolveBranchDiameterInches(SprinkSnapProjectPreferences preferences)
    {
        double value = preferences?.DefaultBranchDiameterInches ?? 0;
        return value > 0 ? value : DefaultBranchDiameterInches;
    }

    public static double ResolveMainDiameterInches(SprinkSnapProjectPreferences preferences)
    {
        double value = preferences?.DefaultMainDiameterInches ?? 0;
        return value > 0 ? value : DefaultMainDiameterInches;
    }

    public static string FormatDiameterLabel(double diameterInches)
    {
        return diameterInches.ToString("0.##") + "\"";
    }
}
