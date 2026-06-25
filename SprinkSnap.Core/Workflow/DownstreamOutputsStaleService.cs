using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public static class DownstreamOutputsStaleService
{
    public const string MaterialsRefreshRequiredMessage =
        "Hydraulics changed pipe and fitting sizes. Refresh material takeoff in the Materials module before exporting Excel or submittal reports.";

    public const string PostHydraulicsRefreshPrompt =
        "Refresh material takeoff before exporting submittal reports.";

    public static bool IsMaterialsTakeoffStale(SprinkSnapProjectState state)
    {
        return state?.SessionProgress.HydraulicsComplete == true
            && !state.SessionProgress.MaterialsComplete;
    }

    public static bool RequiresMaterialsRefreshBeforeExport(SprinkSnapProjectState state)
    {
        return IsMaterialsTakeoffStale(state);
    }

    public static bool IsDownstreamStaleActive(SprinkSnapProjectState state)
    {
        return IsMaterialsTakeoffStale(state);
    }

    public static string GetBannerTitle(SprinkSnapProjectState state)
    {
        if (IsMaterialsTakeoffStale(state))
        {
            return "Material takeoff out of date";
        }

        return string.Empty;
    }

    public static string GetBannerMessage(SprinkSnapProjectState state)
    {
        if (IsMaterialsTakeoffStale(state))
        {
            return "Hydraulics updated pipe diameters, fitting sizes, and quantities. "
                + "Click Refresh Takeoff to regenerate the BOM before exporting Excel or reports.";
        }

        return string.Empty;
    }
}
