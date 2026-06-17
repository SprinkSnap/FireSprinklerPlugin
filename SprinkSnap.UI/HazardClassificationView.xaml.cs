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

    private void OnRequestClose(object sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}

