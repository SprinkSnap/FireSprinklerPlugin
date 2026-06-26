using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using FireSprinklerPlugin.SprinkSnap.UI;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellViewModel : INotifyPropertyChanged
{
    private SprinkSnapShellContext context;
    private HazardClassificationViewModel subscribedHazardViewModel;
    private SprinkSnapModulePanel selectedModulePanel;
    private string actionFeedback = "Select a workflow panel to open the module workspace.";
    private FrameworkElement activeModuleContent;
    private bool showModuleDashboard = true;

    public SprinkSnapShellViewModel(SprinkSnapShellContext context = null)
    {
        this.context = context ?? SprinkSnapShellContext.CreateEmpty();
        this.context.WorkflowChanged += OnContextWorkflowChanged;
        this.context.RequestNavigateToWorkflowStep = OpenWorkflowStep;

        WorkflowSteps = new ObservableCollection<WorkflowStepState>();
        ModulePanels = new ObservableCollection<SprinkSnapModulePanel>
        {
            CreatePanel("Analyze Model", SprinkSnapWorkflowStep.AnalyzeModel, "Extract rooms, spaces, ceilings, levels, linked models, phases, obstructions, and existing sprinklers.", "Analyze Revit Model"),
            CreatePanel("Hazard Review", SprinkSnapWorkflowStep.HazardReview, "Review AI-suggested " + Nfpa13Edition.ShortLabel + " hazard classifications with confidence, reasoning, and designer override.", "Open Hazard Review"),
            CreatePanel("Sprinkler Review", SprinkSnapWorkflowStep.SprinklerReview, "Select project manufacturer standards, review recommended heads, and override compatible room heads.", "Review Sprinklers"),
            CreatePanel("Water Supply", SprinkSnapWorkflowStep.WaterSupply, "Enter hydrant test data, static pressure, residual pressure, and flow at residual.", "Enter Water Supply"),
            CreatePanel("Generate Design", SprinkSnapWorkflowStep.GenerateDesign, "Generate sprinkler layout candidates after analysis, hazard approvals, sprinkler selections, and water supply are complete.", "Generate Sprinkler Design"),
            CreatePanel("Clash Detection", SprinkSnapWorkflowStep.ClashDetection, "Detect sprinkler conflicts with ducts, beams, lights, and geometry — then update layout per " + Nfpa13Edition.References.ObstructionsToDischarge + ".", "Run Clash Detection"),
            CreatePanel("Place Sprinklers", SprinkSnapWorkflowStep.PlaceSprinklers, "Create Revit sprinkler family instances from approved layout candidates after clash resolution.", "Place in Revit"),
            CreatePanel("Hydraulics", SprinkSnapWorkflowStep.Hydraulics, "Build the hydraulic network, calculate demand, critical path, pressure loss, and safety margin.", "Run Hydraulics"),
            CreatePanel("Materials", SprinkSnapWorkflowStep.Materials, "Generate sprinkler, pipe, fitting, valve, and riser material takeoff quantities.", "Open Takeoff"),
            CreatePanel("Reports", SprinkSnapWorkflowStep.Reports, "Export design summary, hydraulic report, node diagram, and material takeoff PDFs.", "Export Reports"),
            CreatePanel("Settings", SprinkSnapWorkflowStep.Settings, "Manage company standards, manufacturer catalogs, Revit family mappings, and AI service settings.", "Open Settings")
        };

        AiAssistant = new AiAssistantViewModel(this.context);
        NfpaReferences = new ObservableCollection<Nfpa13CodeReference>(Nfpa13CodeReferenceLibrary.GetAllReferences());
        SubscribeToHazardViewModel(this.context.GetOrCreateHazardViewModel());

        selectedModulePanel = ModulePanels[0];
        ModuleCapabilities = new ObservableCollection<string>();
        OpenModuleCommand = new ShellRelayCommand(OpenModule, CanOpenModule);
        ShowDashboardCommand = new ShellRelayCommand(_ => ShowDashboard());
        OpenReconciliationStepCommand = new ShellRelayCommand(OpenReconciliationStep);
        ReconciliationSteps = new ObservableCollection<StaleModelReconciliationStep>();
        RefreshWorkflowGates();
        UpdateModuleCapabilities();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public SprinkSnapShellContext Context => context;

    public string DocumentTitle => string.IsNullOrWhiteSpace(context.DocumentTitle)
        ? "SprinkSnap AI"
        : context.DocumentTitle;

    public ObservableCollection<WorkflowStepState> WorkflowSteps { get; }

    public ObservableCollection<SprinkSnapModulePanel> ModulePanels { get; }

    public ObservableCollection<string> ModuleCapabilities { get; }

    public AiAssistantViewModel AiAssistant { get; private set; }

    public HazardClassificationViewModel HazardViewModel => context.GetOrCreateHazardViewModel();

    public ObservableCollection<Nfpa13CodeReference> NfpaReferences { get; }

    public ICommand OpenModuleCommand { get; }

    public ICommand ShowDashboardCommand { get; }

    public ICommand OpenReconciliationStepCommand { get; }

    public ObservableCollection<StaleModelReconciliationStep> ReconciliationSteps { get; }

    public bool IsReconciliationActive => StaleModelReconciliationService.IsReconciliationActive(context.ProjectState);

    public string ReconciliationBannerTitle => StaleModelReconciliationService.GetBannerTitle(context.ProjectState);

    public string ReconciliationBannerMessage => StaleModelReconciliationService.GetBannerMessage(context.ProjectState);

    public FrameworkElement ActiveModuleContent
    {
        get => activeModuleContent;
        private set
        {
            activeModuleContent = value;
            OnPropertyChanged();
        }
    }

    public bool ShowModuleDashboard
    {
        get => showModuleDashboard;
        private set
        {
            showModuleDashboard = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowModuleWorkspace));
        }
    }

    public bool ShowModuleWorkspace => !ShowModuleDashboard;

    public SprinkSnapModulePanel SelectedModulePanel
    {
        get => selectedModulePanel;
        set
        {
            if (value == null || ReferenceEquals(selectedModulePanel, value))
            {
                return;
            }

            if (!value.IsUnlocked)
            {
                actionFeedback = value.BlockReason;
                OnPropertyChanged(nameof(ActionFeedback));
                OnPropertyChanged();
                return;
            }

            selectedModulePanel = value;
            LoadModuleWorkspace(value);
        }
    }

    public string MainWorkspaceTitle => selectedModulePanel?.Title ?? "SprinkSnap AI";

    public string MainWorkspaceDescription => selectedModulePanel?.Description ?? string.Empty;

    public string CurrentActionText => selectedModulePanel?.ActionText ?? "Open Module";

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

    public string ComplianceStatus
    {
        get
        {
            if (StaleModelReconciliationService.IsReconciliationActive(context.ProjectState))
            {
                return ReconciliationBannerTitle;
            }

            if (!SprinkSnapWorkflowGate.IsAnalyzeComplete(context.ProjectState))
            {
                return "Workflow not started. Begin with Analyze Model.";
            }

            if (!SprinkSnapWorkflowGate.IsHazardReviewComplete(context.ProjectState))
            {
                return "Designer hazard approval required.";
            }

            if (SprinkSnapWorkflowGate.IsDesignGenerated(context.ProjectState)
                && !SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState))
            {
                return "Layout generated. Run Clash Detection before hydraulics.";
            }

            return "Core prerequisites satisfied. Continue through design, clash resolution, and hydraulics.";
        }
    }

    public string WarningSummary
    {
        get
        {
            int lockedCount = ModulePanels.Count(panel => !panel.IsUnlocked);
            if (lockedCount > 0)
            {
                return lockedCount + " module(s) remain locked until earlier workflow steps are complete.";
            }

            return context.ProjectState.Warnings.Count > 0
                ? string.Join("; ", context.ProjectState.Warnings.Select(warning => warning.Message))
                : "No active workflow warnings.";
        }
    }

    public string ProgressSummary
    {
        get
        {
            int completeCount = ModulePanels.Count(panel => panel.IsComplete);
            return completeCount + " of " + ModulePanels.Count + " modules complete in this session.";
        }
    }

    public void SetActionFeedback(string feedback)
    {
        ActionFeedback = feedback;
    }

    public void AttachContext(SprinkSnapShellContext newContext)
    {
        if (newContext == null || ReferenceEquals(context, newContext))
        {
            return;
        }

        context.WorkflowChanged -= OnContextWorkflowChanged;
        UnsubscribeFromHazardViewModel();

        context = newContext;
        context.WorkflowChanged += OnContextWorkflowChanged;
        context.RequestNavigateToWorkflowStep = OpenWorkflowStep;

        AiAssistant = new AiAssistantViewModel(context);
        OnPropertyChanged(nameof(AiAssistant));
        OnPropertyChanged(nameof(HazardViewModel));
        OnPropertyChanged(nameof(Context));
        OnPropertyChanged(nameof(DocumentTitle));

        SubscribeToHazardViewModel(context.GetOrCreateHazardViewModel());

        ShowModuleDashboard = true;
        ActiveModuleContent = null;
        if (StaleModelReconciliationService.IsReconciliationActive(context.ProjectState))
        {
            actionFeedback = StaleModelReconciliationService.GetBannerMessage(context.ProjectState);
        }
        else
        {
            actionFeedback = string.IsNullOrWhiteSpace(context.DocumentTitle)
                ? "Select a workflow panel to open the module workspace."
                : "Revit project loaded: " + context.DocumentTitle + ". Select a workflow panel to continue.";
        }
        RefreshWorkflowGates();
        UpdateModuleCapabilities();

        OnPropertyChanged(nameof(ActionFeedback));
        OnPropertyChanged(nameof(ShowModuleDashboard));
        OnPropertyChanged(nameof(ShowModuleWorkspace));
        OnPropertyChanged(nameof(ActiveModuleContent));
    }

    public void OpenInitialModule(string moduleTitle)
    {
        SprinkSnapModulePanel panel = FindModulePanel(moduleTitle);
        if (!panel.IsUnlocked)
        {
            actionFeedback = panel.BlockReason;
            OnPropertyChanged(nameof(ActionFeedback));
            return;
        }

        selectedModulePanel = panel;
        LoadModuleWorkspace(panel);
    }

    public void RefreshWorkflowGates()
    {
        StaleModelReconciliationService.UpdateReconciliationState(context.ProjectState);

        SprinkSnapSessionProgress progress = context.ProjectState.SessionProgress;
        progress.HazardReviewComplete = SprinkSnapWorkflowGate.IsHazardReviewComplete(context.ProjectState);
        progress.SprinklerReviewComplete = SprinkSnapWorkflowGate.IsSprinklerReviewComplete(context.ProjectState);
        progress.WaterSupplyComplete = SprinkSnapWorkflowGate.IsWaterSupplyComplete(context.ProjectState);
        progress.DesignGenerated = SprinkSnapWorkflowGate.IsDesignGenerated(context.ProjectState);
        progress.ClashDetectionComplete = SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState);
        progress.SprinklersPlacedInRevit = SprinkSnapWorkflowGate.IsSprinklersPlacedInRevit(context.ProjectState);

        WorkflowSteps.Clear();
        foreach (SprinkSnapModulePanel panel in ModulePanels)
        {
            WorkflowModuleAccess access = SprinkSnapWorkflowGate.Evaluate(context.ProjectState, panel.Step);
            panel.ApplyAccess(access);
            WorkflowSteps.Add(new WorkflowStepState
            {
                Step = panel.Step,
                Status = access.Status,
                Summary = panel.Title + " • " + access.StatusLabel
            });
        }

        OnPropertyChanged(nameof(ComplianceStatus));
        OnPropertyChanged(nameof(WarningSummary));
        OnPropertyChanged(nameof(ProgressSummary));
        OnPropertyChanged(nameof(SelectedModulePanel));
        OnPropertyChanged(nameof(IsReconciliationActive));
        OnPropertyChanged(nameof(ReconciliationBannerTitle));
        OnPropertyChanged(nameof(ReconciliationBannerMessage));
        RefreshReconciliationSteps();
    }

    public void OpenWorkflowStep(SprinkSnapWorkflowStep step)
    {
        SprinkSnapModulePanel panel = ModulePanels.FirstOrDefault(candidate => candidate.Step == step);
        if (panel == null)
        {
            return;
        }

        if (!panel.IsUnlocked)
        {
            actionFeedback = string.IsNullOrWhiteSpace(panel.BlockReason)
                ? ReconciliationBannerMessage
                : panel.BlockReason;
            OnPropertyChanged(nameof(ActionFeedback));
            return;
        }

        selectedModulePanel = panel;
        LoadModuleWorkspace(panel);
    }

    private void OpenReconciliationStep(object parameter)
    {
        if (parameter is StaleModelReconciliationStep step)
        {
            OpenWorkflowStep(step.WorkflowStep);
        }
    }

    private void RefreshReconciliationSteps()
    {
        ReconciliationSteps.Clear();
        if (!IsReconciliationActive)
        {
            return;
        }

        foreach (StaleModelReconciliationStep step in StaleModelReconciliationService.BuildSteps(context.ProjectState))
        {
            ReconciliationSteps.Add(step);
        }
    }

    private static SprinkSnapModulePanel CreatePanel(
        string title,
        SprinkSnapWorkflowStep step,
        string description,
        string actionText)
    {
        return new SprinkSnapModulePanel(title, step, description, actionText);
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool CanOpenModule(object parameter)
    {
        SprinkSnapModulePanel panel = parameter as SprinkSnapModulePanel ?? selectedModulePanel;
        return panel != null && panel.IsUnlocked;
    }

    private void OpenModule(object parameter)
    {
        SprinkSnapModulePanel panel = parameter as SprinkSnapModulePanel
            ?? FindModulePanel(parameter as string ?? selectedModulePanel?.Title);

        if (panel == null)
        {
            return;
        }

        if (!panel.IsUnlocked)
        {
            actionFeedback = panel.BlockReason;
            OnPropertyChanged(nameof(ActionFeedback));
            return;
        }

        selectedModulePanel = panel;
        LoadModuleWorkspace(panel);
    }

    private void LoadModuleWorkspace(SprinkSnapModulePanel panel)
    {
        ActiveModuleContent = ModuleWorkspaceFactory.CreateWorkspace(panel.Title, context);
        ShowModuleDashboard = false;
        actionFeedback = panel.Title + " workspace loaded.";
        UpdateModuleCapabilities();
        RefreshWorkflowGates();

        OnPropertyChanged(nameof(SelectedModulePanel));
        OnPropertyChanged(nameof(MainWorkspaceTitle));
        OnPropertyChanged(nameof(MainWorkspaceDescription));
        OnPropertyChanged(nameof(CurrentActionText));
        OnPropertyChanged(nameof(ActionFeedback));
    }

    private void ShowDashboard()
    {
        ShowModuleDashboard = true;
        actionFeedback = "Select an unlocked workflow panel to open the module workspace.";
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
        if (selectedModulePanel == null)
        {
            return;
        }

        foreach (string item in selectedModulePanel.Capabilities)
        {
            ModuleCapabilities.Add(item);
        }

        if (!selectedModulePanel.IsUnlocked && !string.IsNullOrWhiteSpace(selectedModulePanel.BlockReason))
        {
            ModuleCapabilities.Insert(0, selectedModulePanel.BlockReason);
        }
    }

    private void OnContextWorkflowChanged(object sender, EventArgs e)
    {
        RefreshWorkflowGates();
    }

    private void SubscribeToHazardViewModel(HazardClassificationViewModel hazardViewModel)
    {
        subscribedHazardViewModel = hazardViewModel;
        if (subscribedHazardViewModel != null)
        {
            subscribedHazardViewModel.PropertyChanged += OnHazardViewModelPropertyChanged;
        }
    }

    private void UnsubscribeFromHazardViewModel()
    {
        if (subscribedHazardViewModel != null)
        {
            subscribedHazardViewModel.PropertyChanged -= OnHazardViewModelPropertyChanged;
            subscribedHazardViewModel = null;
        }
    }

    private void OnHazardViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && e.PropertyName.StartsWith("ActiveCodeReference", StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HazardViewModel));
        }
    }
}

public sealed class SprinkSnapModulePanel : INotifyPropertyChanged
{
    public SprinkSnapModulePanel(string title, SprinkSnapWorkflowStep step, string description, string actionText)
    {
        Title = title;
        Step = step;
        Description = description;
        ActionText = actionText;
        Capabilities = CreateCapabilities(title);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Title { get; }

    public SprinkSnapWorkflowStep Step { get; }

    public string Description { get; }

    public string ActionText { get; }

    public ObservableCollection<string> Capabilities { get; }

    public bool IsUnlocked { get; private set; } = true;

    public bool IsComplete { get; private set; }

    public WorkflowStepStatus Status { get; private set; } = WorkflowStepStatus.NotStarted;

    public string StatusLabel { get; private set; } = "Ready";

    public string BlockReason { get; private set; } = string.Empty;

    public Brush StatusBrush
    {
        get
        {
            switch (Status)
            {
                case WorkflowStepStatus.Complete:
                    return new SolidColorBrush(Color.FromRgb(21, 128, 61));
                case WorkflowStepStatus.Blocked:
                    return new SolidColorBrush(Color.FromRgb(180, 83, 9));
                case WorkflowStepStatus.Warning:
                    return new SolidColorBrush(Color.FromRgb(217, 119, 6));
                case WorkflowStepStatus.InProgress:
                    return new SolidColorBrush(Color.FromRgb(37, 99, 235));
                default:
                    return new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }
    }

    public void ApplyAccess(WorkflowModuleAccess access)
    {
        IsUnlocked = access.IsUnlocked;
        IsComplete = access.IsComplete;
        Status = access.Status;
        StatusLabel = access.StatusLabel;
        BlockReason = access.BlockReason;

        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(BlockReason));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
                    "Import hydrant test data from CSV.",
                    "Generate water supply curve and adequacy warnings."
                };
            case "Generate Design":
                return new ObservableCollection<string>
                {
                    "Verify analysis, hazard approvals, sprinkler approvals, and water supply.",
                    "Generate sprinkler candidates, branch lines, mains, cross mains, and riser connections.",
                    "Show deterministic progress and block generation when critical data is missing."
                };
            case "Clash Detection":
                return new ObservableCollection<string>
                {
                    "Detect conflicts with ducts, beams, lights, cable trays, and pipes.",
                    "Reference " + Nfpa13Edition.References.ObstructionsToDischarge + " obstruction rules for each clash.",
                    "Automatically reposition sprinklers and update layout before hydraulics."
                };
            case "Place Sprinklers":
                return new ObservableCollection<string>
                {
                    "Place listed sprinkler families at approved layout coordinates in Revit.",
                    "Create schematic branch, cross main, and riser pipe segments from routing.",
                    "Tag placed heads and pipes with room number and SprinkSnap metadata."
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
    private readonly Predicate<object> canExecute;

    public ShellRelayCommand(System.Action<object> execute, Predicate<object> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event System.EventHandler CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object parameter)
    {
        return canExecute == null || canExecute(parameter);
    }

    public void Execute(object parameter)
    {
        execute(parameter);
    }
}
