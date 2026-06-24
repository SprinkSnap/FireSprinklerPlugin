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

        ModulePanels = new ObservableCollection<SprinkSnapModulePanel>
        {
            new SprinkSnapModulePanel("Analyze Model", "Extract rooms, spaces, ceilings, levels, linked models, phases, obstructions, and existing sprinklers.", "Analyze Revit Model"),
            new SprinkSnapModulePanel("Hazard Review", "Review AI-suggested NFPA 13 hazard classifications with confidence, reasoning, and designer override.", "Open Hazard Review"),
            new SprinkSnapModulePanel("Sprinkler Review", "Select project manufacturer standards, review recommended heads, and override compatible room heads.", "Review Sprinklers"),
            new SprinkSnapModulePanel("Water Supply", "Enter hydrant test data, static pressure, residual pressure, and flow at residual.", "Enter Water Supply"),
            new SprinkSnapModulePanel("Generate Design", "Generate sprinkler layout candidates after analysis, hazard approvals, sprinkler selections, and water supply are complete.", "Generate Sprinkler Design"),
            new SprinkSnapModulePanel("Hydraulics", "Build the hydraulic network, calculate demand, critical path, pressure loss, and safety margin.", "Run Hydraulics"),
            new SprinkSnapModulePanel("Materials", "Generate sprinkler, pipe, fitting, valve, and riser material takeoff quantities.", "Open Takeoff"),
            new SprinkSnapModulePanel("Reports", "Export design summary, hydraulic report, node diagram, and material takeoff PDFs.", "Export Reports"),
            new SprinkSnapModulePanel("Settings", "Manage company standards, manufacturer catalogs, Revit family mappings, and AI service settings.", "Open Settings")
        };
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<WorkflowStepState> WorkflowSteps { get; }

    public ObservableCollection<string> NavigationItems { get; }

    public ObservableCollection<SprinkSnapModulePanel> ModulePanels { get; }

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
        "Choose a workflow panel below or from the left navigation. Each panel will host its production workspace inside this center area.";

    public string ComplianceStatus => "Designer approval required";

    public string WarningSummary => "Run Analyze Model to populate warnings and exceptions.";

    public string ProgressSummary => "Workflow not started.";

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class SprinkSnapModulePanel
{
    public SprinkSnapModulePanel(string title, string description, string actionText)
    {
        Title = title;
        Description = description;
        ActionText = actionText;
    }

    public string Title { get; set; }

    public string Description { get; set; }

    public string ActionText { get; set; }
}

