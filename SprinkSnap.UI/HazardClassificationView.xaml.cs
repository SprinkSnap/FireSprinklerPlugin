using System.Windows;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public partial class HazardClassificationView : Window
{
    public HazardClassificationView(HazardClassificationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
    }

    public bool UseDialogResult { get; set; } = true;

    private void OnRequestClose(object sender, bool dialogResult)
    {
        if (UseDialogResult)
        {
            DialogResult = dialogResult;
        }

        Close();
    }
}

