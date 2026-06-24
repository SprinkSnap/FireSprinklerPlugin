using System.Windows.Controls;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public partial class SprinkSnapShellView : UserControl
{
    public SprinkSnapShellView()
    {
        InitializeComponent();
        DataContext = new SprinkSnapShellViewModel();
    }
}

