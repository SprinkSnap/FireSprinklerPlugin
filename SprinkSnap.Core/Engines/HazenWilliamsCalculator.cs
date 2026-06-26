using System;

namespace FireSprinklerPlugin.SprinkSnap.Core.Engines;

internal static class HazenWilliamsCalculator
{
    /// <summary>
    /// US Hazen-Williams friction loss (PSI) for a pipe segment.
    /// Q in GPM, D in inches, L in feet, C is Hazen-Williams coefficient (default 120 per Table 28.2.3.2.1).
    /// </summary>
    public static double FrictionLossPsi(double flowGpm, double diameterInches, double lengthFeet, double hazenWilliamsC = 120.0)
    {
        if (flowGpm <= 0 || diameterInches <= 0 || lengthFeet <= 0)
        {
            return 0.0;
        }

        double numerator = 4.52 * Math.Pow(flowGpm, 1.85) * lengthFeet;
        double denominator = Math.Pow(hazenWilliamsC, 1.85) * Math.Pow(diameterInches, 4.87);
        return numerator / denominator;
    }
}
