using System.Windows;
using FireSprinklerPlugin.SprinkSnap.UI;
using FireSprinklerPlugin.SprinkSnap.UI.Modules;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public static class ModuleWorkspaceFactory
{
    public static FrameworkElement CreateWorkspace(string moduleTitle, SprinkSnapShellContext context)
    {
        HazardClassificationViewModel hazardViewModel = context.GetOrCreateHazardViewModel();

        switch (moduleTitle)
        {
            case "Analyze Model":
                return new AnalyzeModelModuleView
                {
                    DataContext = new AnalyzeModelModuleViewModel(context)
                };

            case "Hazard Review":
                return new HazardReviewModuleView
                {
                    DataContext = hazardViewModel
                };

            case "Sprinkler Review":
                return new SprinklerReviewModuleView
                {
                    DataContext = hazardViewModel
                };

            case "Water Supply":
                return new WaterSupplyModuleView
                {
                    DataContext = new WaterSupplyModuleViewModel(context)
                };

            case "Generate Design":
                return new LayoutReviewModuleView
                {
                    DataContext = hazardViewModel
                };

            case "Clash Detection":
                return new ClashDetectionModuleView
                {
                    DataContext = new ClashDetectionModuleViewModel(context)
                };

            case "Hydraulics":
                return new HydraulicsModuleView
                {
                    DataContext = new HydraulicsModuleViewModel(context)
                };

            case "Materials":
                return new MaterialsModuleView
                {
                    DataContext = new MaterialsModuleViewModel(context)
                };

            case "Reports":
                return new ReportsModuleView
                {
                    DataContext = new ReportsModuleViewModel(context)
                };

            case "Settings":
                return new SettingsModuleView
                {
                    DataContext = new SettingsModuleViewModel(context)
                };

            default:
                return new AnalyzeModelModuleView
                {
                    DataContext = new AnalyzeModelModuleViewModel(context)
                };
        }
    }
}
