using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Engines;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;
using FireSprinklerPlugin.SprinkSnap.UI.Shell;

namespace FireSprinklerPlugin.SprinkSnap.UI.Modules;

public abstract class ModuleViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class AnalyzeModelModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IModelAnalysisEngine analysisEngine = new ModelAnalysisEngine();
    private string statusMessage = "Run model analysis to extract rooms, ceilings, and geometry issues.";

    public AnalyzeModelModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        Rooms = new ObservableCollection<RoomInfo>(context.ProjectState.Rooms);
        RunAnalysisCommand = new ModuleRelayCommand(_ => RunAnalysis());
        ExportJsonCommand = new ModuleRelayCommand(_ => ExportJson(), _ => context.ProjectState.Rooms.Count > 0);
        RefreshSummary();
    }

    public ObservableCollection<RoomInfo> Rooms { get; }

    public ICommand RunAnalysisCommand { get; }

    public ICommand ExportJsonCommand { get; }

    public int RoomCount => context.ProjectState.ModelAnalysis.RoomCount;

    public int SlopedCeilingCount => context.ProjectState.ModelAnalysis.SlopedCeilingCount;

    public int MissingCeilingCount => context.ProjectState.ModelAnalysis.MissingCeilingCount;

    public int ObstructionZoneCount => context.ProjectState.ModelAnalysis.ObstructionZoneCount;

    public int ExistingSprinklerCount => context.ProjectState.ModelAnalysis.ExistingSprinklerCount;

    public int LinkedModelCount => context.ProjectState.ModelAnalysis.LinkedModelCount;

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void RunAnalysis()
    {
        context.ProjectState.ModelAnalysis = analysisEngine.Analyze(context.ProjectState);
        context.ProjectState.ModelAnalysis.RoomCount = context.ProjectState.Rooms.Count;
        context.ProjectState.ModelAnalysis.ExistingSprinklerCount = context.ProjectState.ModelAnalysis.ExistingSprinklerCount;
        Rooms.Clear();
        foreach (RoomInfo room in context.ProjectState.Rooms)
        {
            Rooms.Add(room);
        }

        StatusMessage = context.IsPreviewMode
            ? "Preview analysis complete using sample room data."
            : "Revit model analysis complete. Review extracted rooms below.";
        context.ProjectState.SessionProgress.ModelAnalysisComplete = true;
        context.RequestWorkflowRefresh();
        RefreshSummary();
    }

    private void ExportJson()
    {
        string json = analysisEngine.ExportJson(context.ProjectState.ModelAnalysis);
        MessageBox.Show(
            json,
            "SprinkSnap Model Analysis JSON",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(RoomCount));
        OnPropertyChanged(nameof(SlopedCeilingCount));
        OnPropertyChanged(nameof(MissingCeilingCount));
        OnPropertyChanged(nameof(ObstructionZoneCount));
        OnPropertyChanged(nameof(ExistingSprinklerCount));
        OnPropertyChanged(nameof(LinkedModelCount));
    }
}

public sealed class WaterSupplyModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IWaterSupplyEngine waterSupplyEngine = new WaterSupplyEngine();
    private readonly IHydraulicEngine hydraulicEngine = new HydraulicEngine();
    private string staticPressurePsi = string.Empty;
    private string residualPressurePsi = string.Empty;
    private string flowGpm = string.Empty;
    private string hydrantTestDate = string.Empty;
    private string validationSummary = "Enter hydrant test data and click Validate Supply.";

    public WaterSupplyModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        LoadFromState();
        ValidateCommand = new ModuleRelayCommand(_ => ValidateSupply());
    }

    public ICommand ValidateCommand { get; }

    public string StaticPressurePsi
    {
        get => staticPressurePsi;
        set
        {
            staticPressurePsi = value;
            OnPropertyChanged();
        }
    }

    public string ResidualPressurePsi
    {
        get => residualPressurePsi;
        set
        {
            residualPressurePsi = value;
            OnPropertyChanged();
        }
    }

    public string FlowGpm
    {
        get => flowGpm;
        set
        {
            flowGpm = value;
            OnPropertyChanged();
        }
    }

    public string HydrantTestDate
    {
        get => hydrantTestDate;
        set
        {
            hydrantTestDate = value;
            OnPropertyChanged();
        }
    }

    public string ValidationSummary
    {
        get => validationSummary;
        private set
        {
            validationSummary = value;
            OnPropertyChanged();
        }
    }

    private void LoadFromState()
    {
        WaterSupplyInput input = context.ProjectState.WaterSupply;
        StaticPressurePsi = input.StaticPressurePsi?.ToString() ?? string.Empty;
        ResidualPressurePsi = input.ResidualPressurePsi?.ToString() ?? string.Empty;
        FlowGpm = input.FlowAtResidualGpm?.ToString() ?? string.Empty;
        HydrantTestDate = input.HydrantTestDate?.ToShortDateString() ?? string.Empty;
    }

    private void ValidateSupply()
    {
        WaterSupplyInput input = context.ProjectState.WaterSupply;
        input.StaticPressurePsi = TryParse(StaticPressurePsi);
        input.ResidualPressurePsi = TryParse(ResidualPressurePsi);
        input.FlowAtResidualGpm = TryParse(FlowGpm);
        input.HydrantTestDate = DateTime.TryParse(HydrantTestDate, out DateTime testDate) ? testDate : null;

        HydraulicCalculationResult demand = hydraulicEngine.Calculate(context.ProjectState.Rooms, input);
        WaterSupplyValidationResult result = waterSupplyEngine.Validate(input, demand);

        ValidationSummary = result.IsAdequate
            ? "Water supply is adequate. Safety margin: " + result.SafetyMarginPsi.ToString("N1") + " PSI."
            : "Water supply warning: " + string.Join(" ", result.Warnings);

        if (input.StaticPressurePsi.HasValue
            && input.ResidualPressurePsi.HasValue
            && input.FlowAtResidualGpm.HasValue)
        {
            context.ProjectState.SessionProgress.WaterSupplyComplete = true;
            context.RequestWorkflowRefresh();
        }
    }

    private static double? TryParse(string value)
    {
        return double.TryParse(value, out double parsed) ? parsed : null;
    }
}

public sealed class GenerateDesignModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly ILayoutEngine layoutEngine = new LayoutEngine();
    private string statusMessage = "Verify prerequisites, then generate sprinkler layout candidates.";

    public GenerateDesignModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        GenerateCommand = new ModuleRelayCommand(_ => GenerateDesign());
    }

    public ICommand GenerateCommand { get; }

    public int RoomCount => context.ProjectState.Rooms.Count;

    public int ApprovedRoomCount => context.ProjectState.Rooms.Count(room => room.DesignerApproved);

    public bool HasWaterSupply =>
        context.ProjectState.WaterSupply.StaticPressurePsi.HasValue
        && context.ProjectState.WaterSupply.ResidualPressurePsi.HasValue
        && context.ProjectState.WaterSupply.FlowAtResidualGpm.HasValue;

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void GenerateDesign()
    {
        if (context.ProjectState.Rooms.Count == 0)
        {
            StatusMessage = "Run Analyze Model first to extract rooms.";
            return;
        }

        if (!HasWaterSupply)
        {
            StatusMessage = "Enter water supply data before generating design.";
            return;
        }

        SprinklerFamilyInfo sprinkler = context.SprinklerFamilies.FirstOrDefault();
        if (sprinkler == null)
        {
            StatusMessage = "No sprinkler catalog families are available.";
            return;
        }

        int generatedRooms = 0;
        foreach (RoomInfo room in context.ProjectState.Rooms)
        {
            AutomaticLayoutResult result = layoutEngine.Generate(room, sprinkler);
            room.ProposedSprinklers.Clear();
            foreach (SprinklerPlacementCandidate candidate in result.Candidates)
            {
                room.ProposedSprinklers.Add(candidate);
            }

            if (room.ProposedSprinklers.Count > 0)
            {
                generatedRooms++;
            }
        }

        StatusMessage = "Generated layout candidates for " + generatedRooms + " room(s). Open Hazard Review to validate.";
        context.ProjectState.SessionProgress.DesignGenerated = generatedRooms > 0;
        context.RequestWorkflowRefresh();
        OnPropertyChanged(nameof(RoomCount));
        OnPropertyChanged(nameof(ApprovedRoomCount));
        OnPropertyChanged(nameof(HasWaterSupply));
    }
}

public sealed class HydraulicsModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IHydraulicEngine hydraulicEngine = new HydraulicEngine();
    private HydraulicCalculationResult result = new HydraulicCalculationResult();

    public HydraulicsModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        CalculateCommand = new ModuleRelayCommand(_ => Calculate());
    }

    public ICommand CalculateCommand { get; }

    public double TotalFlowGpm => result.TotalFlowGpm;

    public double SystemDemandPsi => result.SystemDemandPsi;

    public double AvailablePressurePsi => result.AvailablePressurePsi;

    public double SafetyMarginPsi => result.SafetyMarginPsi;

    public string WarningSummary => result.Warnings.Count == 0
        ? "No hydraulic warnings."
        : string.Join(Environment.NewLine, result.Warnings);

    private void Calculate()
    {
        result = hydraulicEngine.Calculate(context.ProjectState.Rooms, context.ProjectState.WaterSupply);
        context.ProjectState.SessionProgress.HydraulicsComplete = true;
        context.RequestWorkflowRefresh();
        OnPropertyChanged(nameof(TotalFlowGpm));
        OnPropertyChanged(nameof(SystemDemandPsi));
        OnPropertyChanged(nameof(AvailablePressurePsi));
        OnPropertyChanged(nameof(SafetyMarginPsi));
        OnPropertyChanged(nameof(WarningSummary));
    }
}

public sealed class MaterialsModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IMaterialTakeoffEngine takeoffEngine = new MaterialTakeoffEngine();

    public MaterialsModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        Items = new ObservableCollection<MaterialTakeoffItem>(takeoffEngine.Generate(context.ProjectState.Rooms));
        RefreshCommand = new ModuleRelayCommand(_ => Refresh());
    }

    public ObservableCollection<MaterialTakeoffItem> Items { get; }

    public ICommand RefreshCommand { get; }

    private void Refresh()
    {
        Items.Clear();
        foreach (MaterialTakeoffItem item in takeoffEngine.Generate(context.ProjectState.Rooms))
        {
            Items.Add(item);
        }

        context.ProjectState.SessionProgress.MaterialsComplete = Items.Count > 0;
        context.RequestWorkflowRefresh();
    }
}

public sealed class ReportsModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IReportEngine reportEngine = new ReportEngine();
    private string outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string statusMessage = "Select report options and export PDF summaries.";

    public ReportsModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        ExportCommand = new ModuleRelayCommand(_ => ExportReports());
    }

    public ICommand ExportCommand { get; }

    public string OutputFolder
    {
        get => outputFolder;
        set
        {
            outputFolder = value;
            OnPropertyChanged();
        }
    }

    public bool IncludeDesignSummary { get; set; } = true;

    public bool IncludeHydraulicReport { get; set; } = true;

    public bool IncludeNodeDiagram { get; set; } = true;

    public bool IncludeMaterialTakeoff { get; set; } = true;

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void ExportReports()
    {
        ReportExportRequest request = new ReportExportRequest
        {
            OutputFolder = OutputFolder,
            IncludeDesignSummary = IncludeDesignSummary,
            IncludeHydraulicReport = IncludeHydraulicReport,
            IncludeNodeDiagram = IncludeNodeDiagram,
            IncludeMaterialTakeoff = IncludeMaterialTakeoff
        };

        HydraulicCalculationResult hydraulicResult = new HydraulicEngine()
            .Calculate(context.ProjectState.Rooms, context.ProjectState.WaterSupply);
        IReadOnlyList<MaterialTakeoffItem> materialTakeoff = new MaterialTakeoffEngine()
            .Generate(context.ProjectState.Rooms);
        IReadOnlyList<string> reports = reportEngine.ExportAll(
            context.ProjectState,
            hydraulicResult,
            materialTakeoff,
            request);

        StatusMessage = "Prepared reports in " + OutputFolder + ": " + string.Join(", ", reports);
        context.ProjectState.SessionProgress.ReportsExported = reports.Count > 0;
        context.RequestWorkflowRefresh();
    }
}

public sealed class ClashDetectionModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private readonly IClashDetectionEngine clashEngine = new ClashDetectionEngine();
    private readonly ISprinklerLayoutOptimizer layoutOptimizer = new SprinklerLayoutOptimizer();
    private string statusMessage = "Run clash detection after generating sprinkler layout.";

    public ClashDetectionModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        Clashes = new ObservableCollection<SprinklerClashRecord>();
        DetectClashesCommand = new ModuleRelayCommand(_ => DetectClashes());
        ResolveClashesCommand = new ModuleRelayCommand(_ => ResolveClashes(), _ => TotalClashes > 0);
        SyncFromState();
    }

    public ObservableCollection<SprinklerClashRecord> Clashes { get; }

    public ICommand DetectClashesCommand { get; }

    public ICommand ResolveClashesCommand { get; }

    public int TotalClashes => context.ProjectState.ClashSummary?.TotalClashes ?? 0;

    public int ResolvedClashes => context.ProjectState.ClashSummary?.ResolvedClashes ?? 0;

    public int UnresolvedClashes => context.ProjectState.ClashSummary?.UnresolvedClashes ?? 0;

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void DetectClashes()
    {
        if (context.ProjectState.Rooms.Count == 0)
        {
            StatusMessage = "Generate sprinkler design first — no rooms with layout candidates found.";
            return;
        }

        ClashDetectionSummary summary = clashEngine.Detect(context.ProjectState.Rooms);
        context.ProjectState.ClashSummary = summary;
        ApplySummary(summary);
        StatusMessage = summary.Messages.Count > 0
            ? string.Join(" ", summary.Messages)
            : "Clash detection complete.";
        context.ProjectState.SessionProgress.ClashDetectionComplete =
            SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState);
        context.RequestWorkflowRefresh();
    }

    private void ResolveClashes()
    {
        SprinklerFamilyInfo sprinkler = context.SprinklerFamilies.FirstOrDefault()
            ?? context.GetOrCreateHazardViewModel().SelectedSprinklerFamily;
        if (sprinkler == null)
        {
            StatusMessage = "Select a project sprinkler standard before resolving clashes.";
            return;
        }

        ClashDetectionSummary summary = clashEngine.ResolveAndUpdateLayout(
            context.ProjectState.Rooms,
            layoutOptimizer,
            sprinkler);
        context.ProjectState.ClashSummary = summary;
        ApplySummary(summary);
        StatusMessage = summary.Messages.Count > 0
            ? string.Join(" ", summary.Messages)
            : "Layout updated after clash resolution.";
        context.ProjectState.SessionProgress.ClashDetectionComplete =
            SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState);
        context.GetOrCreateHazardViewModel().NotifyExternalRefresh();
        context.RequestWorkflowRefresh();
    }

    private void ApplySummary(ClashDetectionSummary summary)
    {
        Clashes.Clear();
        foreach (SprinklerClashRecord clash in summary.Clashes)
        {
            Clashes.Add(clash);
        }

        OnPropertyChanged(nameof(TotalClashes));
        OnPropertyChanged(nameof(ResolvedClashes));
        OnPropertyChanged(nameof(UnresolvedClashes));
    }

    private void SyncFromState()
    {
        if (context.ProjectState.ClashSummary != null)
        {
            ApplySummary(context.ProjectState.ClashSummary);
            if (context.ProjectState.ClashSummary.Messages.Count > 0)
            {
                StatusMessage = string.Join(" ", context.ProjectState.ClashSummary.Messages);
            }
        }
    }
}

public sealed class SettingsModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private string defaultManufacturer = "Viking";
    private bool allowAlternateManufacturers = true;
    private string aiServiceEndpoint = string.Empty;
    private string statusMessage = "Configure project standards and AI service settings.";

    public SettingsModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        SaveCommand = new ModuleRelayCommand(_ => Save());
        defaultManufacturer = context.SprinklerFamilies.FirstOrDefault()?.Manufacturer ?? "Viking";
    }

    public ICommand SaveCommand { get; }

    public ObservableCollection<string> ManufacturerOptions { get; } =
        new ObservableCollection<string> { "Viking", "Tyco", "Reliable", "Victaulic" };

    public string DefaultManufacturer
    {
        get => defaultManufacturer;
        set
        {
            defaultManufacturer = value;
            OnPropertyChanged();
        }
    }

    public bool AllowAlternateManufacturers
    {
        get => allowAlternateManufacturers;
        set
        {
            allowAlternateManufacturers = value;
            OnPropertyChanged();
        }
    }

    public string AiServiceEndpoint
    {
        get => aiServiceEndpoint;
        set
        {
            aiServiceEndpoint = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    private void Save()
    {
        HazardClassificationViewModel hazardViewModel = context.GetOrCreateHazardViewModel();
        hazardViewModel.SelectedManufacturer = DefaultManufacturer;
        hazardViewModel.AllowAlternateManufacturers = AllowAlternateManufacturers;
        context.ProjectState.SessionProgress.SprinklerReviewComplete =
            SprinkSnapWorkflowGate.IsSprinklerReviewComplete(context.ProjectState);
        context.RequestWorkflowRefresh();
        StatusMessage = "Project settings saved for this SprinkSnap session.";
    }
}

public sealed class ModuleRelayCommand : ICommand
{
    private readonly Action<object> execute;
    private readonly Predicate<object> canExecute;

    public ModuleRelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
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
