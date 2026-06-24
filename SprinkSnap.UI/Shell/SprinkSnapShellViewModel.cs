using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellViewModel : INotifyPropertyChanged
{
    private string selectedNavigationItem = "Analyze Model";

    public SprinkSnapShellViewModel()
    {
        WorkflowSteps = new ObservableCollection<WorkflowStepState>
        {
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.AnalyzeModel, Status = WorkflowStepStatus.NotStarted, Summary = "Model analysis" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.HazardReview, Status = WorkflowStepStatus.NotStarted, Summary = "Hazard approval" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.SprinklerReview, Status = WorkflowStepStatus.NotStarted, Summary = "Sprinkler selection" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.WaterSupply, Status = WorkflowStepStatus.NotStarted, Summary = "Water supply" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.GenerateDesign, Status = WorkflowStepStatus.NotStarted, Summary = "Generate design" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.Hydraulics, Status = WorkflowStepStatus.NotStarted, Summary = "Hydraulics" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.Materials, Status = WorkflowStepStatus.NotStarted, Summary = "Materials" },
            new WorkflowStepState { Step = SprinkSnapWorkflowStep.Reports, Status = WorkflowStepStatus.NotStarted, Summary = "Reports" }
        };

        NavigationItems = new ObservableCollection<string>
        {
            "Analyze Model",
            "Hazard Review",
            "Sprinkler Review",
            "Water Supply",
            "Generate Design",
            "Hydraulics",
            "Materials",
            "Reports",
            "Settings"
        };
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<WorkflowStepState> WorkflowSteps { get; }

    public ObservableCollection<string> NavigationItems { get; }

    public string SelectedNavigationItem
    {
        get => selectedNavigationItem;
        set
        {
            if (selectedNavigationItem == value)
            {
                return;
            }

            selectedNavigationItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MainWorkspaceTitle));
            OnPropertyChanged(nameof(MainWorkspaceDescription));
        }
    }

    public string MainWorkspaceTitle => SelectedNavigationItem;

    public string MainWorkspaceDescription =>
        "This production shell hosts the " + SelectedNavigationItem + " workflow. Module-specific WPF views plug into this center workspace.";

    public string ComplianceStatus => "Designer approval required";

    public string WarningSummary => "Run Analyze Model to populate warnings and exceptions.";

    public string ProgressSummary => "Workflow not started.";

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

