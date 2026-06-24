using System.Windows;
using System.Windows.Controls;

namespace FireSprinklerPlugin.SprinkSnap.UI;

public partial class HazardClassificationView : UserControl
{
    public HazardClassificationView()
    {
        InitializeComponent();
    }

    public HazardClassificationView(HazardClassificationViewModel viewModel)
        : this()
    {
        Initialize(viewModel);
    }

    public void Initialize(HazardClassificationViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object sender, bool dialogResult)
    {
        Window hostWindow = Window.GetWindow(this);
        if (hostWindow == null)
        {
            return;
        }

        if (hostWindow is HazardClassificationWindow classificationWindow && classificationWindow.UseDialogResult)
        {
            classificationWindow.DialogResult = dialogResult;
        }

        hostWindow.Close();
    }
}
