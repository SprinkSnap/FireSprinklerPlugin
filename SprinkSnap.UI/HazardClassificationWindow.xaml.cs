using System.Windows;
using System.Windows.Controls;

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

        viewModel.RequestClose += (_, accepted) =>
        {
            DialogResult = accepted;
            Close();
        };

        DockPanel root = new DockPanel();
        Button cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        DockPanel.SetDock(cancelButton, Dock.Bottom);
        root.Children.Add(cancelButton);
        root.Children.Add(new Modules.HazardReviewModuleView
        {
            DataContext = viewModel
        });

        Content = root;
        UseDialogResult = true;
    }

    public bool UseDialogResult { get; set; }
}
