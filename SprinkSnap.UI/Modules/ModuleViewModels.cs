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
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
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

    public bool IsModelStale => context.ProjectState.ModelChangeAssessment?.IsStale ?? false;

    public string ModelChangeSummary => context.ProjectState.ModelChangeAssessment?.Messages.Count > 0
        ? string.Join(" ", context.ProjectState.ModelChangeAssessment.Messages)
        : string.Empty;

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
        context.ProjectState.ModelChangeAssessment = new ModelChangeAssessment
        {
            HasBaseline = true,
            Messages = { "Analysis refreshed in the current SprinkSnap session." }
        };
        context.RequestPersistToRevit();
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
        OnPropertyChanged(nameof(IsModelStale));
        OnPropertyChanged(nameof(ModelChangeSummary));
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
            context.RequestPersistToRevit();
            context.RequestWorkflowRefresh();
        }
    }

    private static double? TryParse(string value)
    {
        return double.TryParse(value, out double parsed) ? parsed : null;
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
    private SprinklerClashRecord selectedClash;

    public ClashDetectionModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        Clashes = new ObservableCollection<SprinklerClashRecord>();
        DetectClashesCommand = new ModuleRelayCommand(_ => DetectClashes());
        ResolveClashesCommand = new ModuleRelayCommand(_ => ResolveClashes(), _ => TotalClashes > 0);
        ShowClashInRevitCommand = new ModuleRelayCommand(_ => ShowSelectedClashInRevit(), _ => SelectedClash != null);
        SyncFromState();
    }

    public ObservableCollection<SprinklerClashRecord> Clashes { get; }

    public ICommand DetectClashesCommand { get; }

    public ICommand ResolveClashesCommand { get; }

    public ICommand ShowClashInRevitCommand { get; }

    public SprinklerClashRecord SelectedClash
    {
        get => selectedClash;
        set
        {
            selectedClash = value;
            OnPropertyChanged();
        }
    }

    public int TotalClashes => context.ProjectState.ClashSummary?.TotalClashes ?? 0;

    public int ResolvedClashes => context.ProjectState.ClashSummary?.ResolvedClashes ?? 0;

    public int UnresolvedClashes => context.ProjectState.ClashSummary?.UnresolvedClashes ?? 0;

    public int HostClashCount => context.ProjectState.ClashSummary?.HostClashCount ?? 0;

    public int LinkedClashCount => context.ProjectState.ClashSummary?.LinkedClashCount ?? 0;

    public int LinkedModelsScannedCount => context.ProjectState.ClashSummary?.LinkedModelsScannedCount ?? 0;

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

        if (context.RequestClashDetection != null && !context.IsPreviewMode)
        {
            StatusMessage = "Scanning host and linked Revit models for sprinkler obstructions...";
            context.RequestClashDetection(summary =>
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyDetectionResult(summary));
            });
            return;
        }

        ApplyDetectionResult(clashEngine.Detect(context.ProjectState.Rooms));
    }

    private void ApplyDetectionResult(ClashDetectionSummary summary)
    {
        context.ProjectState.ClashSummary = summary;
        ApplySummary(summary);
        StatusMessage = summary.Messages.Count > 0
            ? string.Join(" ", summary.Messages)
            : "Clash detection complete.";
        context.ProjectState.SessionProgress.ClashDetectionComplete =
            SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState);
        context.RequestWorkflowRefresh();
    }

    private void ShowSelectedClashInRevit()
    {
        if (SelectedClash == null)
        {
            return;
        }

        if (context.IsPreviewMode)
        {
            StatusMessage = "Show in Revit is available inside the Revit SprinkSnap dockable pane.";
            return;
        }

        if (context.RequestShowClashInRevit == null)
        {
            StatusMessage = "Revit navigation is not connected for this session.";
            return;
        }

        context.RequestShowClashInRevit(SelectedClash);
        StatusMessage = "Zoomed to clash in Revit for room " + SelectedClash.RoomNumber + ".";
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
        context.GetOrCreateHazardViewModel().NotifyExternalRefresh();

        if (context.RequestClashDetection != null && !context.IsPreviewMode)
        {
            StatusMessage = "Layout updated. Rescanning Revit geometry for remaining clashes...";
            context.RequestClashDetection(rescanSummary =>
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyDetectionResult(rescanSummary));
            });
            return;
        }

        ApplySummary(summary);
        StatusMessage = summary.Messages.Count > 0
            ? string.Join(" ", summary.Messages)
            : "Layout updated after clash resolution.";
        context.ProjectState.SessionProgress.ClashDetectionComplete =
            SprinkSnapWorkflowGate.IsClashDetectionComplete(context.ProjectState);
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
        OnPropertyChanged(nameof(HostClashCount));
        OnPropertyChanged(nameof(LinkedClashCount));
        OnPropertyChanged(nameof(LinkedModelsScannedCount));
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

public sealed class PlaceSprinklersModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private string statusMessage = "Run pre-flight validation, then place approved sprinkler layouts in Revit.";
    private bool allowUnmappedPlacement;
    private bool hasValidatedPreflight;

    public PlaceSprinklersModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        RoomResults = new ObservableCollection<SprinklerPlacementRoomResult>();
        PreflightRooms = new ObservableCollection<PlacementPreflightRoomResult>();
        ValidateCommand = new ModuleRelayCommand(_ => RunPreflightValidation());
        PlaceInRevitCommand = new ModuleRelayCommand(_ => PlaceInRevit());
        SyncFromState();
    }

    public ObservableCollection<SprinklerPlacementRoomResult> RoomResults { get; }

    public ObservableCollection<PlacementPreflightRoomResult> PreflightRooms { get; }

    public ICommand ValidateCommand { get; }

    public ICommand PlaceInRevitCommand { get; }

    public int TotalCandidates => context.ProjectState.PlacementSummary?.TotalCandidates
        ?? context.ProjectState.Rooms.Sum(room => room.ProposedSprinklers.Count);

    public int PlacedCount => context.ProjectState.PlacementSummary?.PlacedCount ?? 0;

    public int SkippedRoomCount => context.ProjectState.PlacementSummary?.SkippedRoomCount ?? 0;

    public int ReadyRoomCount => context.ProjectState.PlacementPreflight?.ReadyRoomCount ?? 0;

    public int MappedRoomCount => context.ProjectState.PlacementPreflight?.MappedRoomCount ?? 0;

    public int UnmappedRoomCount => context.ProjectState.PlacementPreflight?.UnmappedRoomCount ?? 0;

    public int ExceptionRoomCount => context.ProjectState.PlacementPreflight?.ExceptionRoomCount ?? 0;

    public bool CanPlaceAll => context.ProjectState.PlacementPreflight?.CanPlaceAll ?? false;

    public bool AllowUnmappedPlacement
    {
        get => allowUnmappedPlacement;
        set
        {
            allowUnmappedPlacement = value;
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

    private void RunPreflightValidation()
    {
        ApplyPreflight(context.ProjectState.PlacementPreflight
            ?? SprinklerFamilyMappingService.ValidatePlacementReadiness(
                context.ProjectState.Rooms,
                context.SprinklerFamilies));
        hasValidatedPreflight = true;
        StatusMessage = context.ProjectState.PlacementPreflight.Messages.Count > 0
            ? string.Join(" ", context.ProjectState.PlacementPreflight.Messages)
            : "Pre-flight validation complete.";
    }

    private void PlaceInRevit()
    {
        if (!hasValidatedPreflight)
        {
            RunPreflightValidation();
        }

        if (ReadyRoomCount == 0)
        {
            StatusMessage = "No rooms are ready for placement. Complete hazard approval, layout, family mapping, and clash resolution first.";
            return;
        }

        if (!CanPlaceAll && !AllowUnmappedPlacement)
        {
            StatusMessage = "Pre-flight blocked placement: map all sprinkler families in Settings or enable the override after reviewing unmapped rooms.";
            return;
        }

        if (context.IsPreviewMode)
        {
            StatusMessage = "WpfPreview cannot write to Revit. Open SprinkSnap in Revit and use Place Sprinklers from the ribbon or this module.";
            return;
        }

        if (context.RequestPlaceSprinklers == null)
        {
            StatusMessage = "Revit placement is not connected for this session.";
            return;
        }

        context.ApplyFamilyMapping();
        StatusMessage = "Placing sprinklers in Revit...";
        context.RequestPlaceSprinklers(summary =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplySummary(summary);
                StatusMessage = summary.Messages.Count > 0
                    ? string.Join(" ", summary.Messages)
                    : "Placement complete.";
                context.RequestWorkflowRefresh();
            });
        });
    }

    private void ApplySummary(SprinklerPlacementSummary summary)
    {
        context.ProjectState.PlacementSummary = summary;
        RoomResults.Clear();
        foreach (SprinklerPlacementRoomResult roomResult in summary.RoomResults)
        {
            RoomResults.Add(roomResult);
        }

        OnPropertyChanged(nameof(TotalCandidates));
        OnPropertyChanged(nameof(PlacedCount));
        OnPropertyChanged(nameof(SkippedRoomCount));
    }

    private void ApplyPreflight(PlacementPreflightSummary summary)
    {
        context.ProjectState.PlacementPreflight = summary;
        PreflightRooms.Clear();
        foreach (PlacementPreflightRoomResult roomResult in summary.Rooms)
        {
            PreflightRooms.Add(roomResult);
        }

        OnPropertyChanged(nameof(ReadyRoomCount));
        OnPropertyChanged(nameof(MappedRoomCount));
        OnPropertyChanged(nameof(UnmappedRoomCount));
        OnPropertyChanged(nameof(ExceptionRoomCount));
        OnPropertyChanged(nameof(CanPlaceAll));
    }

    private void SyncFromState()
    {
        if (context.ProjectState.PlacementPreflight?.Rooms.Count > 0)
        {
            ApplyPreflight(context.ProjectState.PlacementPreflight);
            hasValidatedPreflight = true;
        }
        else if (context.ProjectState.Rooms.Count > 0)
        {
            RunPreflightValidation();
        }

        if (context.ProjectState.PlacementSummary != null
            && context.ProjectState.PlacementSummary.RoomResults.Count > 0)
        {
            ApplySummary(context.ProjectState.PlacementSummary);
            if (context.ProjectState.PlacementSummary.Messages.Count > 0)
            {
                StatusMessage = string.Join(" ", context.ProjectState.PlacementSummary.Messages);
            }
        }
    }
}

public sealed class FamilyMappingRowViewModel : ModuleViewModelBase
{
    private readonly IList<LoadedRevitSymbolOption> loadedRevitSymbols;
    private LoadedRevitSymbolOption selectedRevitSymbol;

    public FamilyMappingRowViewModel(
        FamilyMappingRow row,
        IList<LoadedRevitSymbolOption> loadedRevitSymbols)
    {
        this.loadedRevitSymbols = loadedRevitSymbols ?? new List<LoadedRevitSymbolOption>();
        CatalogFamilyKey = row.CatalogFamilyKey;
        Manufacturer = row.Manufacturer;
        Model = row.Model;
        Sin = row.Sin;
        DisplayName = row.DisplayName;
        MappingStatus = row.MappingStatus;
        IsLoadedInProject = row.IsLoadedInProject;
        RevitFamilySymbolId = row.RevitFamilySymbolId;
        RevitFamilyName = row.RevitFamilyName;
        RevitTypeName = row.RevitTypeName;
        selectedRevitSymbol = ResolveSelectedSymbol(row.RevitFamilySymbolId);
    }

    public string CatalogFamilyKey { get; }

    public string Manufacturer { get; }

    public string Model { get; }

    public string Sin { get; }

    public string DisplayName { get; }

    public string MappingStatus
    {
        get => mappingStatus;
        private set
        {
            mappingStatus = value;
            OnPropertyChanged();
        }
    }

    private string mappingStatus;

    public bool IsLoadedInProject { get; }

    public string RevitFamilySymbolId
    {
        get => revitFamilySymbolId;
        private set
        {
            revitFamilySymbolId = value;
            OnPropertyChanged();
        }
    }

    private string revitFamilySymbolId;

    public string RevitFamilyName
    {
        get => revitFamilyName;
        private set
        {
            revitFamilyName = value;
            OnPropertyChanged();
        }
    }

    private string revitFamilyName;

    public string RevitTypeName
    {
        get => revitTypeName;
        private set
        {
            revitTypeName = value;
            OnPropertyChanged();
        }
    }

    private string revitTypeName;

    public IEnumerable<LoadedRevitSymbolOption> LoadedRevitSymbols => loadedRevitSymbols;

    public LoadedRevitSymbolOption SelectedRevitSymbol
    {
        get => selectedRevitSymbol;
        set
        {
            if (ReferenceEquals(selectedRevitSymbol, value))
            {
                return;
            }

            selectedRevitSymbol = value ?? LoadedRevitSymbolOption.Empty;
            RevitFamilySymbolId = selectedRevitSymbol.RevitFamilySymbolId;
            RevitFamilyName = selectedRevitSymbol.RevitFamilyName;
            RevitTypeName = selectedRevitSymbol.RevitTypeName;
            MappingStatus = string.IsNullOrWhiteSpace(RevitFamilySymbolId) ? "Needs Mapping" : "Mapped";
            OnPropertyChanged();
        }
    }

    private LoadedRevitSymbolOption ResolveSelectedSymbol(string symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
        {
            return LoadedRevitSymbolOption.Empty;
        }

        return loadedRevitSymbols.FirstOrDefault(option =>
                   string.Equals(option.RevitFamilySymbolId, symbolId, StringComparison.OrdinalIgnoreCase))
               ?? new LoadedRevitSymbolOption
               {
                   RevitFamilySymbolId = symbolId,
                   RevitFamilyName = RevitFamilyName,
                   RevitTypeName = RevitTypeName,
                   DisplayName = RevitFamilyName + " : " + RevitTypeName
               };
    }
}

public sealed class LinkedModelScanOptionViewModel : ModuleViewModelBase
{
    private bool includeInClashScan;

    public LinkedModelScanOptionViewModel(LinkedModelScanOption option)
    {
        LinkInstanceId = option.LinkInstanceId;
        LinkName = option.LinkName;
        DocumentTitle = option.DocumentTitle;
        IsLoaded = option.IsLoaded;
        includeInClashScan = option.IncludeInClashScan;
    }

    public int LinkInstanceId { get; }

    public string LinkName { get; }

    public string DocumentTitle { get; }

    public bool IsLoaded { get; }

    public string LoadStatus => IsLoaded ? "Loaded" : "Not loaded";

    public bool IncludeInClashScan
    {
        get => includeInClashScan;
        set
        {
            includeInClashScan = value;
            OnPropertyChanged();
        }
    }

    public LinkedModelScanOption ToOption()
    {
        return new LinkedModelScanOption
        {
            LinkInstanceId = LinkInstanceId,
            LinkName = LinkName,
            DocumentTitle = DocumentTitle,
            IsLoaded = IsLoaded,
            IncludeInClashScan = IncludeInClashScan
        };
    }
}

public sealed class SettingsModuleViewModel : ModuleViewModelBase
{
    private readonly SprinkSnapShellContext context;
    private string defaultManufacturer = "Viking";
    private bool allowAlternateManufacturers = true;
    private string aiServiceEndpoint = string.Empty;
    private string statusMessage = "Configure project standards, map catalog families to loaded Revit types, and save.";

    public SettingsModuleViewModel(SprinkSnapShellContext context)
    {
        this.context = context;
        FamilyMappingRows = new ObservableCollection<FamilyMappingRowViewModel>();
        LoadedRevitSymbols = new ObservableCollection<LoadedRevitSymbolOption>();
        LinkedModelScanOptions = new ObservableCollection<LinkedModelScanOptionViewModel>();
        SaveCommand = new ModuleRelayCommand(_ => Save());
        defaultManufacturer = context.SprinklerFamilies.FirstOrDefault()?.Manufacturer ?? "Viking";
        RefreshFamilyMappingGrid();
        RefreshLinkedModelOptions();
    }

    public ObservableCollection<LinkedModelScanOptionViewModel> LinkedModelScanOptions { get; }

    public ICommand SaveCommand { get; }

    public ObservableCollection<FamilyMappingRowViewModel> FamilyMappingRows { get; }

    public ObservableCollection<LoadedRevitSymbolOption> LoadedRevitSymbols { get; }

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

    private void RefreshLinkedModelOptions()
    {
        LinkedModelScanOptions.Clear();
        foreach (LinkedModelScanOption option in context.ProjectState.LinkedModelScanOptions)
        {
            LinkedModelScanOptions.Add(new LinkedModelScanOptionViewModel(option));
        }
    }

    private void RefreshFamilyMappingGrid()
    {
        LoadedRevitSymbols.Clear();
        foreach (LoadedRevitSymbolOption option in SprinklerFamilyMappingService.GetLoadedRevitSymbolOptions(context.SprinklerFamilies))
        {
            LoadedRevitSymbols.Add(option);
        }

        IList<LoadedRevitSymbolOption> symbolOptions = LoadedRevitSymbols.ToList();
        FamilyMappingRows.Clear();
        foreach (FamilyMappingRow row in SprinklerFamilyMappingService.BuildMappingRows(
                     context.SprinklerFamilies,
                     context.ProjectState.FamilyMappingOverrides))
        {
            FamilyMappingRows.Add(new FamilyMappingRowViewModel(row, symbolOptions));
        }
    }

    private void Save()
    {
        HazardClassificationViewModel hazardViewModel = context.GetOrCreateHazardViewModel();
        hazardViewModel.SelectedManufacturer = DefaultManufacturer;
        hazardViewModel.AllowAlternateManufacturers = AllowAlternateManufacturers;

        context.ProjectState.FamilyMappingOverrides.Clear();
        foreach (FamilyMappingRowViewModel row in FamilyMappingRows)
        {
            if (string.IsNullOrWhiteSpace(row.RevitFamilySymbolId))
            {
                continue;
            }

            context.ProjectState.FamilyMappingOverrides.Add(new SprinklerFamilyMappingOverride
            {
                CatalogFamilyKey = row.CatalogFamilyKey,
                RevitFamilySymbolId = row.RevitFamilySymbolId,
                RevitFamilyName = row.RevitFamilyName,
                RevitTypeName = row.RevitTypeName
            });
        }

        context.ApplyFamilyMapping();
        RefreshFamilyMappingGrid();

        context.ProjectState.LinkedModelScanOptions.Clear();
        foreach (LinkedModelScanOptionViewModel option in LinkedModelScanOptions)
        {
            context.ProjectState.LinkedModelScanOptions.Add(option.ToOption());
        }

        RefreshLinkedModelOptions();
        context.ProjectState.SessionProgress.SprinklerReviewComplete =
            SprinkSnapWorkflowGate.IsSprinklerReviewComplete(context.ProjectState);
        context.RequestPersistToRevit();
        context.RequestWorkflowRefresh();

        int mappedCount = FamilyMappingRows.Count(row => row.MappingStatus.StartsWith("Mapped", StringComparison.OrdinalIgnoreCase));
        int linkedScanCount = LinkedModelScanOptions.Count(option => option.IncludeInClashScan && option.IsLoaded);
        StatusMessage = "Settings saved. "
            + mappedCount
            + " catalog family mapping(s) and "
            + linkedScanCount
            + " linked model(s) enabled for clash detection.";
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
