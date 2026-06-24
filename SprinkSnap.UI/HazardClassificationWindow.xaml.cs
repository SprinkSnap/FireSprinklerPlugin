using System.Windows;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public sealed class HazardClassificationWindow : Window
{
    public HazardClassificationWindow(HazardClassificationViewModel viewModel)
    {
        Title = "SprinkSnap AI-Assisted Layout Review";
        Width = 1440;
        Height = 860;
        MinWidth = 1180;
        MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new HazardClassificationView(viewModel);
        UseDialogResult = true;
    }

    public bool UseDialogResult { get; set; }
}
