using System;
using System.Collections.Generic;
using System.Linq;

namespace FireSprinklerPlugin.SprinkSnap.Core.Clash;

public static class LinkedModelScanOptionService
{
    public static IList<LinkedModelScanOption> MergeDiscoveredWithExisting(
        IEnumerable<LinkedModelScanOption> discovered,
        IEnumerable<LinkedModelScanOption> existing)
    {
        Dictionary<int, LinkedModelScanOption> previousById = new Dictionary<int, LinkedModelScanOption>();
        foreach (LinkedModelScanOption option in existing ?? Array.Empty<LinkedModelScanOption>())
        {
            if (option.LinkInstanceId > 0)
            {
                previousById[option.LinkInstanceId] = option;
            }
        }

        List<LinkedModelScanOption> merged = new List<LinkedModelScanOption>();
        foreach (LinkedModelScanOption discoveredOption in discovered ?? Array.Empty<LinkedModelScanOption>())
        {
            if (previousById.TryGetValue(discoveredOption.LinkInstanceId, out LinkedModelScanOption previous))
            {
                discoveredOption.IncludeInClashScan = previous.IncludeInClashScan;
            }

            merged.Add(discoveredOption);
        }

        return merged;
    }
}
