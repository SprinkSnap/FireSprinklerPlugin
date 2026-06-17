using System.Windows;
using FireSprinklerPlugin.SprinkSnap.UI;

namespace FireSprinklerPlugin.SprinkSnap.WpfPreview;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HazardClassificationViewModel viewModel = new HazardClassificationViewModel(
            PreviewSampleDataFactory.CreateRooms())
        {
            StaticPressurePsi = "72",
            ResidualPressurePsi = "48",
            FlowGpm = "1250"
        };

        HazardClassificationView view = new HazardClassificationView(viewModel)
        {
            Title = "SprinkSnap Hazard Classification Review - WPF Preview",
            UseDialogResult = false
        };

        MainWindow = view;
        view.Show();
    }
}

