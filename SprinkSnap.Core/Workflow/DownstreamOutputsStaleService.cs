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

    public static bool RequiresHydraulicsBeforeMaterials(SprinkSnapProjectState state)
    {
        return HydraulicWorkflowGuidanceService.ShouldWarnMaterialsMissingHydraulics(state);
    }

    public static bool BlocksMaterialsRefresh(SprinkSnapProjectState state)
    {
        return RequiresHydraulicsBeforeMaterials(state);
    }

    public static bool BlocksMaterialsExport(SprinkSnapProjectState state)
    {
        return RequiresHydraulicsBeforeMaterials(state)
            || RequiresMaterialsRefreshBeforeExport(state);
    }

    public static string GetMaterialsRefreshBlockMessage(SprinkSnapProjectState state)
    {
        if (RequiresHydraulicsBeforeMaterials(state))
        {
            return HydraulicWorkflowGuidanceService.MaterialsMissingHydraulicsMessage;
        }

        return string.Empty;
    }

    public static string GetMaterialsExportBlockMessage(SprinkSnapProjectState state)
    {
        if (RequiresHydraulicsBeforeMaterials(state))
        {
            return HydraulicWorkflowGuidanceService.MaterialsMissingHydraulicsMessage;
        }

        if (RequiresMaterialsRefreshBeforeExport(state))
        {
            return MaterialsRefreshRequiredMessage;
        }

        return string.Empty;
    }

    public static string GetReportExportBlockMessage(
        SprinkSnapProjectState state,
        bool includeHydraulicReport,
        bool includeNodeDiagram,
        bool includeMaterialTakeoff)
    {
        if (includeHydraulicReport || includeNodeDiagram)
        {
            if (state?.SessionProgress.HydraulicsComplete != true)
            {
                return HydraulicWorkflowGuidanceService.HydraulicsRequiredBeforeReportExportMessage;
            }

            if (HydraulicWorkflowGuidanceService.ShouldWarnReRunHydraulicsAfterPipePlacement(state))
            {
                return HydraulicWorkflowGuidanceService.ReRunHydraulicsAfterPlacementMessage;
            }

            if (HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state))
            {
                return HydraulicWorkflowGuidanceService.SchematicOnlyHydraulicsMessage;
            }
        }

        if (includeMaterialTakeoff)
        {
            if (RequiresHydraulicsBeforeMaterials(state))
            {
                return HydraulicWorkflowGuidanceService.MaterialsMissingHydraulicsMessage;
            }

            if (RequiresMaterialsRefreshBeforeExport(state))
            {
                return MaterialsRefreshRequiredMessage;
            }
        }

        return string.Empty;
    }

    public static bool IsDownstreamStaleActive(SprinkSnapProjectState state)
    {
        return IsMaterialsTakeoffStale(state)
            || RequiresHydraulicsBeforeMaterials(state)
            || HydraulicWorkflowGuidanceService.ShouldWarnReRunHydraulicsAfterPipePlacement(state)
            || HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state);
    }

    public static string GetBannerTitle(SprinkSnapProjectState state)
    {
        if (RequiresHydraulicsBeforeMaterials(state))
        {
            return "Hydraulics required";
        }

        if (IsMaterialsTakeoffStale(state))
        {
            return "Material takeoff out of date";
        }

        if (HydraulicWorkflowGuidanceService.ShouldWarnReRunHydraulicsAfterPipePlacement(state))
        {
            return "Hydraulics out of date";
        }

        if (HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state))
        {
            return "Schematic-only hydraulics";
        }

        return string.Empty;
    }

    public static string GetBannerMessage(SprinkSnapProjectState state)
    {
        if (RequiresHydraulicsBeforeMaterials(state))
        {
            return HydraulicWorkflowGuidanceService.MaterialsMissingHydraulicsMessage;
        }

        if (IsMaterialsTakeoffStale(state))
        {
            return "Hydraulics updated pipe diameters, fitting sizes, and quantities. "
                + "Click Refresh Takeoff to regenerate the BOM before exporting Excel or reports.";
        }

        if (HydraulicWorkflowGuidanceService.ShouldWarnReRunHydraulicsAfterPipePlacement(state))
        {
            return HydraulicWorkflowGuidanceService.ReRunHydraulicsAfterPlacementMessage;
        }

        if (HydraulicWorkflowGuidanceService.IsSchematicOnlyHydraulicsComplete(state))
        {
            return HydraulicWorkflowGuidanceService.SchematicOnlyHydraulicsMessage;
        }

        return string.Empty;
    }
}
