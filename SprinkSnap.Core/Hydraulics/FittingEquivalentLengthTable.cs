using System;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

/// <summary>
/// Interpolated fitting equivalent lengths aligned with NFPA 13 (2025) Table 28.2.3.1.1 anchor values.
/// </summary>
public static class FittingEquivalentLengthTable
{
    public static double GetEquivalentLengthFeet(string jointType, double diameterInches)
    {
        if (diameterInches <= 0)
        {
            return 0.0;
        }

        if (string.Equals(jointType, PipeJointTypes.Valve, StringComparison.OrdinalIgnoreCase))
        {
            return Interpolate(diameterInches, 1.25, 8.0, 4.0, 28.0);
        }

        if (string.Equals(jointType, PipeJointTypes.Tee, StringComparison.OrdinalIgnoreCase))
        {
            return Interpolate(diameterInches, 1.25, 4.0, 4.0, 14.0);
        }

        return Interpolate(diameterInches, 1.25, 2.5, 4.0, 11.0);
    }

    private static double Interpolate(
        double diameterInches,
        double smallDiameterInches,
        double smallLengthFeet,
        double largeDiameterInches,
        double largeLengthFeet)
    {
        if (diameterInches <= smallDiameterInches)
        {
            return smallLengthFeet;
        }

        if (diameterInches >= largeDiameterInches)
        {
            return largeLengthFeet;
        }

        double ratio = (diameterInches - smallDiameterInches) / (largeDiameterInches - smallDiameterInches);
        return smallLengthFeet + (ratio * (largeLengthFeet - smallLengthFeet));
    }
}
