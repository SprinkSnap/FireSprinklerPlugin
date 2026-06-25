using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Data;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Hydraulics;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Workflow;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellContext
{
    private HazardClassificationViewModel hazardViewModel;

    public SprinkSnapShellContext(
        SprinkSnapProjectState projectState,
        IEnumerable<SprinklerFamilyInfo> sprinklerFamilies = null)
    {
        ProjectState = projectState ?? new SprinkSnapProjectState();
        string catalogPath = ProjectState.Preferences?.CatalogPath;
        SprinklerCatalogService.Default.Reload(string.IsNullOrWhiteSpace(catalogPath) ? null : catalogPath);
        SprinklerFamilies = sprinklerFamilies?.ToList()
            ?? SprinklerCatalogService.Default.GetAvailableFamilies().ToList();
    }

    public event EventHandler WorkflowChanged;

    public SprinkSnapProjectState ProjectState { get; }

    public IList<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public bool IsPreviewMode { get; set; }

    public string DocumentKey { get; private set; } = string.Empty;

    public string DocumentTitle { get; private set; } = string.Empty;

    public Action PersistToRevitRequested { get; set; }

    public Action<Action<SprinklerPlacementSummary>> RequestPlaceSprinklers { get; set; }

    public Action<Action<PipePlacementSummary>> RequestPlacePipes { get; set; }

    public Action<Action<PipePlacementSummary>> RequestRemeasurePlacedPipes { get; set; }

    public Action<Action<ClashDetectionSummary>> RequestClashDetection { get; set; }

    public Action<Action<RevitProjectLoadResult>> RequestReanalyze { get; set; }

    public Action<Action<IList<LoadedRevitSymbolOption>>> RequestRefreshLoadedSprinklerSymbols { get; set; }

    public Action<SprinklerClashRecord> RequestShowClashInRevit { get; set; }

    public Action<int> RequestShowRoomInRevit { get; set; }

    public Action<Action<HydraulicSupplyAnchor>> RequestPickHydraulicSupplyAnchor { get; set; }

    public Action<SprinkSnapWorkflowStep> RequestNavigateToWorkflowStep { get; set; }

    public static SprinkSnapShellContext CreateEmpty()
    {
        return new SprinkSnapShellContext(new SprinkSnapProjectState());
    }

    public void RequestWorkflowRefresh()
    {
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestPersistToRevit()
    {
        PersistToRevitRequested?.Invoke();
    }

    public void NavigateToWorkflowStep(SprinkSnapWorkflowStep step)
    {
        RequestNavigateToWorkflowStep?.Invoke(step);
    }

    public void ApplyRevitLoad(RevitProjectLoadResult loadResult, bool markAnalysisComplete)
    {
        if (loadResult == null)
        {
            return;
        }

        DocumentKey = loadResult.DocumentKey ?? string.Empty;
        DocumentTitle = loadResult.DocumentTitle ?? string.Empty;

        ProjectState.Rooms.Clear();
        foreach (RoomInfo room in loadResult.Rooms)
        {
            ProjectState.Rooms.Add(room);
        }

        if (loadResult.ModelAnalysis != null)
        {
            ProjectState.ModelAnalysis = loadResult.ModelAnalysis;
        }

        int linkedModelCount = ProjectState.ModelAnalysis?.LinkedModelCount ?? 0;
        if (loadResult.SessionSnapshot != null)
        {
            SprinkSnapSessionPersistenceService.ApplySnapshot(ProjectState, loadResult.SessionSnapshot);
        }

        string catalogPath = ProjectState.Preferences?.CatalogPath;
        SprinklerCatalogService.Default.Reload(string.IsNullOrWhiteSpace(catalogPath) ? null : catalogPath);

        SprinklerFamilies.Clear();
        foreach (SprinklerFamilyInfo family in loadResult.SprinklerFamilies)
        {
            SprinklerFamilies.Add(family);
        }

        ProjectState.LoadedRevitSprinklerSymbols.Clear();
        foreach (LoadedRevitSymbolOption symbol in loadResult.LoadedRevitSprinklerSymbols)
        {
            ProjectState.LoadedRevitSprinklerSymbols.Add(symbol);
        }

        ProjectState.LinkedModelScanOptions = LinkedModelScanOptionService.MergeDiscoveredWithExisting(
            loadResult.LinkedModelScanOptions,
            ProjectState.LinkedModelScanOptions).ToList();

        ProjectState.ModelChangeAssessment = loadResult.SessionSnapshot != null
            ? SprinkSnapSessionPersistenceService.AssessModelChangeFromSnapshot(
                loadResult.SessionSnapshot,
                ProjectState.Rooms,
                linkedModelCount)
            : new ModelChangeAssessment();

        if (ProjectState.ModelChangeAssessment.IsStale)
        {
            ProjectState.SessionProgress.ReconciliationRequired = true;
            foreach (string message in ProjectState.ModelChangeAssessment.Messages)
            {
                if (!ProjectState.ModelAnalysis.Warnings.Contains(message))
                {
                    ProjectState.ModelAnalysis.Warnings.Add(message);
                }
            }

            DownstreamDesignInvalidationService.InvalidateDownstreamDesign(ProjectState);
        }

        if (markAnalysisComplete && ProjectState.Rooms.Count > 0)
        {
            ProjectState.SessionProgress.ModelAnalysisComplete = true;
        }

        ApplyFamilyMapping(refreshHazardViewModel: false);
        ResetHazardViewModel();
        RequestWorkflowRefresh();
    }

    public void ApplyFamilyMapping(bool refreshHazardViewModel = true)
    {
        SprinklerFamilyMappingService.ApplyOverrides(
            SprinklerFamilies,
            ProjectState.FamilyMappingOverrides);
        SprinklerFamilyMappingService.UpdateRoomMappingStatuses(ProjectState.Rooms, SprinklerFamilies);
        ProjectState.PlacementPreflight = SprinklerFamilyMappingService.ValidatePlacementReadiness(
            ProjectState.Rooms,
            SprinklerFamilies);

        if (refreshHazardViewModel && hazardViewModel != null)
        {
            hazardViewModel.RefreshFamilyMappingStatuses();
        }
    }

    public SprinklerCatalogLoadResult ReloadCatalogFamilies(string optionalCatalogPath = null)
    {
        string catalogPath = optionalCatalogPath ?? ProjectState.Preferences?.CatalogPath;
        SprinklerCatalogLoadResult loadResult = SprinklerCatalogService.Default.Reload(
            string.IsNullOrWhiteSpace(catalogPath) ? null : catalogPath);

        if (ProjectState.Preferences != null)
        {
            ProjectState.Preferences.CatalogPath = catalogPath ?? string.Empty;
        }

        Dictionary<string, SprinklerFamilyInfo> loadedFamilies = SprinklerFamilies
            .Where(family => family.IsLoadedInProject)
            .GroupBy(family => family.ListedFamilyId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        SprinklerFamilies.Clear();
        foreach (SprinklerFamilyInfo family in loadResult.Families)
        {
            if (!string.IsNullOrWhiteSpace(family.ListedFamilyId)
                && loadedFamilies.TryGetValue(family.ListedFamilyId, out SprinklerFamilyInfo loadedFamily))
            {
                family.IsLoadedInProject = loadedFamily.IsLoadedInProject;
                family.RevitFamilyName = loadedFamily.RevitFamilyName;
                family.RevitFamilySymbolId = loadedFamily.RevitFamilySymbolId;
                family.RecognitionSource = loadedFamily.RecognitionSource;
            }

            SprinklerFamilies.Add(family);
        }

        ApplyFamilyMapping(refreshHazardViewModel: false);
        ResetHazardViewModel();
        RequestWorkflowRefresh();
        return loadResult;
    }

    public void ApplyLoadedSprinklerSymbols(IEnumerable<LoadedRevitSymbolOption> symbols)
    {
        ProjectState.LoadedRevitSprinklerSymbols.Clear();
        foreach (LoadedRevitSymbolOption symbol in symbols ?? Array.Empty<LoadedRevitSymbolOption>())
        {
            ProjectState.LoadedRevitSprinklerSymbols.Add(symbol);
        }

        ApplyFamilyMapping(refreshHazardViewModel: false);
        ResetHazardViewModel();
        RequestWorkflowRefresh();
    }

    public void ApplyPostReanalysisInvalidation()
    {
        List<int> changedRoomIds = ProjectState.ModelChangeAssessment?.ChangedRoomRevitElementIds?.ToList()
            ?? new List<int>();

        DownstreamDesignInvalidationService.InvalidateDownstreamDesign(
            ProjectState,
            changedRoomIds,
            clearHazardApprovalsForChangedRooms: true);
        ProjectState.SessionProgress.ReconciliationRequired = true;

        ProjectState.ModelChangeAssessment = new ModelChangeAssessment
        {
            HasBaseline = true,
            IsStale = false,
            ChangedRoomCount = changedRoomIds.Count,
            ChangedRoomRevitElementIds = changedRoomIds,
            Messages =
            {
                "Revit model re-analyzed. Downstream design, clash, hydraulics, and materials results were cleared.",
                changedRoomIds.Count > 0
                    ? changedRoomIds.Count + " changed room(s) still require hazard review."
                    : "Work through the reconciliation checklist before exporting or placing."
            }
        };

        ApplyModelChangeAssessmentToHazardViewModel();
        ResetHazardViewModel();
    }

    private void ApplyModelChangeAssessmentToHazardViewModel()
    {
        if (hazardViewModel != null)
        {
            hazardViewModel.ApplyModelChangeAssessment(ProjectState.ModelChangeAssessment);
        }
    }

    public HazardClassificationViewModel GetOrCreateHazardViewModel()
    {
        if (hazardViewModel != null)
        {
            return hazardViewModel;
        }

        hazardViewModel = new HazardClassificationViewModel(ProjectState.Rooms, SprinklerFamilies)
        {
            IsEmbeddedInShell = true,
            EmbeddedProjectState = ProjectState,
            PersistToRevitRequested = PersistToRevitRequested,
            ShowRoomInRevitRequested = roomId => RequestShowRoomInRevit?.Invoke(roomId)
        };
        hazardViewModel.ApplyModelChangeAssessment(ProjectState.ModelChangeAssessment);
        hazardViewModel.WorkflowProgressChanged += (_, _) => RequestWorkflowRefresh();
        foreach (RoomHazardReviewItem roomItem in hazardViewModel.Rooms)
        {
            roomItem.UpdatePipeRouting(ProjectState.SchematicPipeRouting);
        }

        return hazardViewModel;
    }

    public void ResetHazardViewModel()
    {
        hazardViewModel = null;
    }
}

/// <summary>
/// DTO passed from Revit into the shell when a document is loaded.
/// Defined in UI so WpfPreview can stay decoupled from Revit assemblies.
/// </summary>
public sealed class RevitProjectLoadResult
{
    public IList<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();

    public IList<SprinklerFamilyInfo> SprinklerFamilies { get; set; } = new List<SprinklerFamilyInfo>();

    public IList<LoadedRevitSymbolOption> LoadedRevitSprinklerSymbols { get; set; } = new List<LoadedRevitSymbolOption>();

    public ModelAnalysisSummary ModelAnalysis { get; set; } = new ModelAnalysisSummary();

    public string DocumentKey { get; set; } = string.Empty;

    public string DocumentTitle { get; set; } = string.Empty;

    public IList<LinkedModelScanOption> LinkedModelScanOptions { get; set; } = new List<LinkedModelScanOption>();

    public SprinkSnapSessionSnapshot SessionSnapshot { get; set; }
}
