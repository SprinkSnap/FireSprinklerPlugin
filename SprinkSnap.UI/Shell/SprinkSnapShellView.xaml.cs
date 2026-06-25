using System.Windows.Controls;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public partial class SprinkSnapShellView : UserControl
{
    private SprinkSnapShellContext boundContext;

    public SprinkSnapShellView(SprinkSnapShellContext context = null)
    {
        InitializeComponent();
        boundContext = context;
        DataContext = new SprinkSnapShellViewModel(context);
    }

    public SprinkSnapShellViewModel ViewModel => DataContext as SprinkSnapShellViewModel;

    public void AttachContext(SprinkSnapShellContext context)
    {
        if (context == null)
        {
            return;
        }

        boundContext = context;
        if (ViewModel == null)
        {
            DataContext = new SprinkSnapShellViewModel(context);
            return;
        }

        ViewModel.AttachContext(context);
    }

    public void OpenModule(SprinkSnapWorkflowStep step)
    {
        ViewModel?.OpenInitialModule(MapStepToTitle(step));
    }

    private static string MapStepToTitle(SprinkSnapWorkflowStep step)
    {
        switch (step)
        {
            case SprinkSnapWorkflowStep.HazardReview:
                return "Hazard Review";
            case SprinkSnapWorkflowStep.SprinklerReview:
                return "Sprinkler Review";
            case SprinkSnapWorkflowStep.WaterSupply:
                return "Water Supply";
            case SprinkSnapWorkflowStep.GenerateDesign:
                return "Generate Design";
            case SprinkSnapWorkflowStep.ClashDetection:
                return "Clash Detection";
            case SprinkSnapWorkflowStep.Hydraulics:
                return "Hydraulics";
            case SprinkSnapWorkflowStep.Materials:
                return "Materials";
            case SprinkSnapWorkflowStep.Reports:
                return "Reports";
            case SprinkSnapWorkflowStep.Settings:
                return "Settings";
            default:
                return "Analyze Model";
        }
    }
}
