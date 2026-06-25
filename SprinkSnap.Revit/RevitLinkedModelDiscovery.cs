using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitLinkedModelDiscovery
{
    public static IList<LinkedModelScanOption> Discover(Document document)
    {
        List<LinkedModelScanOption> options = new List<LinkedModelScanOption>();
        if (document == null)
        {
            return options;
        }

        IList<RevitLinkInstance> linkInstances = new FilteredElementCollector(document)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .ToList();

        foreach (RevitLinkInstance linkInstance in linkInstances)
        {
            Document linkDocument = linkInstance.GetLinkDocument();
            options.Add(new LinkedModelScanOption
            {
                LinkInstanceId = linkInstance.Id.IntegerValue,
                LinkName = linkInstance.Name,
                DocumentTitle = linkDocument?.Title ?? linkInstance.Name,
                IsLoaded = linkDocument != null,
                IncludeInClashScan = linkDocument != null && SuggestIncludeByName(linkInstance.Name)
            });
        }

        return options;
    }

    private static bool SuggestIncludeByName(string linkName)
    {
        if (string.IsNullOrWhiteSpace(linkName))
        {
            return true;
        }

        string normalized = linkName.ToUpperInvariant();
        if (normalized.Contains("ARCH")
            || normalized.Contains("STRUCT")
            || normalized.Contains("SITE")
            || normalized.Contains("CIVIL"))
        {
            return false;
        }

        return true;
    }
}
