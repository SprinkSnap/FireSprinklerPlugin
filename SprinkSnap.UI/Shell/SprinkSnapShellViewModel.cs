using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellViewModel : INotifyPropertyChanged
{
    private string selectedNavigationItem = "Analyze Model";
    private SprinkSnapModulePanel selectedModulePanel;
    private string actionFeedback = "Select a workflow panel or click an action button to preview the module workspace.";

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

        selectedModulePanel = ModulePanels[0];
        ModuleCapabilities = new ObservableCollection<string>();
        OpenModuleCommand = new ShellRelayCommand(OpenModule);
        UpdateModuleCapabilities();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<WorkflowStepState> WorkflowSteps { get; }

    public ObservableCollection<string> NavigationItems { get; }

    public ObservableCollection<SprinkSnapModulePanel> ModulePanels { get; }

    public ObservableCollection<string> ModuleCapabilities { get; }

    public ICommand OpenModuleCommand { get; }

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
            selectedModulePanel = FindModulePanel(value);
            actionFeedback = "Opened " + selectedModulePanel.Title + " workspace from navigation.";
            UpdateModuleCapabilities();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MainWorkspaceTitle));
            OnPropertyChanged(nameof(MainWorkspaceDescription));
            OnPropertyChanged(nameof(SelectedModulePanel));
            OnPropertyChanged(nameof(CurrentActionText));
            OnPropertyChanged(nameof(ActionFeedback));
        }
    }

    public SprinkSnapModulePanel SelectedModulePanel => selectedModulePanel;

    public string MainWorkspaceTitle => selectedModulePanel.Title;

    public string MainWorkspaceDescription => selectedModulePanel.Description;

    public string CurrentActionText => selectedModulePanel.ActionText;

    public string ActionFeedback
    {
        get => actionFeedback;
        set
        {
            if (actionFeedback == value)
            {
                return;
            }

            actionFeedback = value;
            OnPropertyChanged();
        }
    }

    public string ComplianceStatus => "Designer approval required";

    public string WarningSummary => "Run Analyze Model to populate warnings and exceptions.";

    public string ProgressSummary => "Workflow not started.";

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OpenModule(object parameter)
    {
        SprinkSnapModulePanel panel = parameter as SprinkSnapModulePanel
            ?? FindModulePanel(parameter as string ?? SelectedNavigationItem);

        selectedModulePanel = panel;
        selectedNavigationItem = panel.Title;
        actionFeedback = panel.ActionText + " selected. The production module view will load here inside Revit.";
        UpdateModuleCapabilities();

        OnPropertyChanged(nameof(SelectedNavigationItem));
        OnPropertyChanged(nameof(SelectedModulePanel));
        OnPropertyChanged(nameof(MainWorkspaceTitle));
        OnPropertyChanged(nameof(MainWorkspaceDescription));
        OnPropertyChanged(nameof(CurrentActionText));
        OnPropertyChanged(nameof(ActionFeedback));
    }

    private SprinkSnapModulePanel FindModulePanel(string title)
    {
        foreach (SprinkSnapModulePanel panel in ModulePanels)
        {
            if (panel.Title == title)
            {
                return panel;
            }
        }

        return ModulePanels[0];
    }

    private void UpdateModuleCapabilities()
    {
        ModuleCapabilities.Clear();
        foreach (string item in selectedModulePanel.Capabilities)
        {
            ModuleCapabilities.Add(item);
        }
    }
}

public sealed class SprinkSnapModulePanel
{
    public SprinkSnapModulePanel(string title, string description, string actionText)
    {
        Title = title;
        Description = description;
        ActionText = actionText;
        Capabilities = CreateCapabilities(title);
    }

    public string Title { get; set; }

    public string Description { get; set; }

    public string ActionText { get; set; }

    public ObservableCollection<string> Capabilities { get; }

    private static ObservableCollection<string> CreateCapabilities(string title)
    {
        switch (title)
        {
            case "Analyze Model":
                return new ObservableCollection<string>
                {
                    "Extract rooms, spaces, levels, phases, linked models, ceilings, and existing sprinklers.",
                    "Detect missing ceilings, sloped ceilings, irregular rooms, obstructions, and conflicts.",
                    "Export model analysis summary to JSON."
                };
            case "Hazard Review":
                return new ObservableCollection<string>
                {
                    "Show AI-suggested hazard classification with confidence and reasoning.",
                    "Support Accept All, Accept Above 90%, bulk apply, and manual override.",
                    "Require designer approval before layout generation."
                };
            case "Sprinkler Review":
                return new ObservableCollection<string>
                {
                    "Set project default manufacturer and listed sprinkler criteria.",
                    "Auto-recommend compatible heads by room hazard, ceiling, geometry, and listing constraints.",
                    "Allow room-level override to another compatible manufacturer/model."
                };
            case "Water Supply":
                return new ObservableCollection<string>
                {
                    "Enter static pressure, residual pressure, flow at residual, and hydrant test date.",
                    "Import future PDF/CSV hydrant test data.",
                    "Generate water supply curve and adequacy warnings."
                };
            case "Generate Design":
                return new ObservableCollection<string>
                {
                    "Verify analysis, hazard approvals, sprinkler approvals, and water supply.",
                    "Generate sprinkler candidates, branch lines, mains, cross mains, and riser connections.",
                    "Show deterministic progress and block generation when critical data is missing."
                };
            case "Hydraulics":
                return new ObservableCollection<string>
                {
                    "Build hydraulic node graph and critical path.",
                    "Run deterministic Hazen-Williams calculations.",
                    "Display flow demand, available pressure, system demand, and safety margin."
                };
            case "Materials":
                return new ObservableCollection<string>
                {
                    "Count sprinklers, pipe lengths, pipe diameters, fittings, valves, and riser assemblies.",
                    "Display material takeoff in a DataGrid.",
                    "Export takeoff to PDF and Excel."
                };
            case "Reports":
                return new ObservableCollection<string>
                {
                    "Generate design summary PDF.",
                    "Generate hydraulic report, node diagram, and material takeoff PDFs.",
                    "Export all reports from one workflow button."
                };
            default:
                return new ObservableCollection<string>
                {
                    "Manage company standards, manufacturer catalogs, family mappings, and AI service settings.",
                    "Configure default manufacturer preferences and alternate manufacturer policy.",
                    "Configure deterministic design guardrails and approval requirements."
                };
        }
    }
}

public sealed class ShellRelayCommand : ICommand
{
    private readonly System.Action<object> execute;

    public ShellRelayCommand(System.Action<object> execute)
    {
        this.execute = execute;
    }

    public event System.EventHandler CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object parameter)
    {
        return true;
    }

    public void Execute(object parameter)
    {
        execute(parameter);
    }
}

