using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;

public static class RemoteAreaHydraulicCalculator
{
    private const double DefaultMaxCoverageSquareFeet = 130.0;

    private const int MinimumOperatingSprinklers = 2;

    public static int CalculateOperatingSprinklerCount(
        double remoteAreaSquareFeet,
        double maxCoverageSquareFeet,
        int availableHeadCount)
    {
        int byCoverage = (int)Math.Ceiling(remoteAreaSquareFeet / Math.Max(maxCoverageSquareFeet, 1.0));
        int operatingCount = Math.Max(MinimumOperatingSprinklers, byCoverage);
        if (availableHeadCount > 0)
        {
            operatingCount = Math.Min(operatingCount, Math.Max(availableHeadCount, MinimumOperatingSprinklers));
        }

        return operatingCount;
    }

    public static double ResolveMaxCoverageSquareFeet(SprinklerFamilyInfo family)
    {
        if (family != null && family.MaxCoverageAreaSquareFeet > 0)
        {
            return family.MaxCoverageAreaSquareFeet;
        }

        return DefaultMaxCoverageSquareFeet;
    }

    public static SprinklerFamilyInfo ResolveRepresentativeFamily(RoomInfo room)
    {
        string sprinklerName = string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName)
            ? room.AutoSelectedSprinklerName
            : room.SelectedSprinklerFamilyName;

        if (string.IsNullOrWhiteSpace(sprinklerName))
        {
            return null;
        }

        return new SprinklerFamilySelector()
            .GetAvailableFamilies()
            .FirstOrDefault(item => string.Equals(item.DisplayName, sprinklerName, StringComparison.OrdinalIgnoreCase));
    }
}
