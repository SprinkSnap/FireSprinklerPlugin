using System.Windows;
using FireSprinklerPlugin.SprinkSnap.UI;
using FireSprinklerPlugin.SprinkSnap.UI.Modules;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public static class ModuleWorkspaceFactory
{
    public static FrameworkElement CreateWorkspace(string moduleTitle, SprinkSnapShellContext context)
    {
        switch (moduleTitle)
        {
            case "Analyze Model":
                return new AnalyzeModelModuleView
                {
                    DataContext = new AnalyzeModelModuleViewModel(context)
                };

            case "Hazard Review":
                return CreateHazardWorkspace(context, selectedTabIndex: 1);

            case "Sprinkler Review":
                return CreateHazardWorkspace(context, selectedTabIndex: 0);

            case "Water Supply":
                return new WaterSupplyModuleView
                {
                    DataContext = new WaterSupplyModuleViewModel(context)
                };

            case "Generate Design":
                return new GenerateDesignModuleView
                {
                    DataContext = new GenerateDesignModuleViewModel(context)
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
                return CreateHazardWorkspace(context, selectedTabIndex: 0);
        }
    }

    private static HazardClassificationView CreateHazardWorkspace(SprinkSnapShellContext context, int selectedTabIndex)
    {
        HazardClassificationViewModel viewModel = context.GetOrCreateHazardViewModel();
        viewModel.IsEmbeddedInShell = true;
        viewModel.SelectedTabIndex = selectedTabIndex;

        HazardClassificationView view = new HazardClassificationView();
        view.Initialize(viewModel);
        return view;
    }
}
