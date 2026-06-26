using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class VelocityLimitDefaults
{
    public const double DefaultBranchVelocityLimitFeetPerSecond = 15.0;

    public const double DefaultMainVelocityLimitFeetPerSecond = 20.0;

    public static double ResolveBranchVelocityLimitFeetPerSecond(SprinkSnapProjectPreferences preferences)
    {
        double value = preferences?.BranchVelocityLimitFeetPerSecond ?? 0;
        return value > 0 ? value : DefaultBranchVelocityLimitFeetPerSecond;
    }

    public static double ResolveMainVelocityLimitFeetPerSecond(SprinkSnapProjectPreferences preferences)
    {
        double value = preferences?.MainVelocityLimitFeetPerSecond ?? 0;
        return value > 0 ? value : DefaultMainVelocityLimitFeetPerSecond;
    }
}
