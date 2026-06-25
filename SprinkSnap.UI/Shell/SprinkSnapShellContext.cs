using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellContext
{
    private HazardClassificationViewModel hazardViewModel;

    public SprinkSnapShellContext(
        SprinkSnapProjectState projectState,
        IEnumerable<SprinklerFamilyInfo> sprinklerFamilies = null)
    {
        ProjectState = projectState ?? new SprinkSnapProjectState();
        SprinklerFamilies = sprinklerFamilies?.ToList()
            ?? new SprinklerFamilySelector().GetAvailableFamilies().ToList();
    }

    public event EventHandler WorkflowChanged;

    public SprinkSnapProjectState ProjectState { get; }

    public IList<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public bool IsPreviewMode { get; set; }

    public string DocumentKey { get; private set; } = string.Empty;

    public string DocumentTitle { get; private set; } = string.Empty;

    public Action PersistToRevitRequested { get; set; }

    public Action<Action<SprinklerPlacementSummary>> RequestPlaceSprinklers { get; set; }

    public Action<Action<ClashDetectionSummary>> RequestClashDetection { get; set; }

    public Action<SprinklerClashRecord> RequestShowClashInRevit { get; set; }

    public Action<int> RequestShowRoomInRevit { get; set; }

    public static SprinkSnapShellContext CreateEmpty()
    {
        return new SprinkSnapShellContext(new SprinkSnapProjectState());
    }

    public void RequestWorkflowRefresh()
    {
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
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

        SprinklerFamilies.Clear();
        foreach (SprinklerFamilyInfo family in loadResult.SprinklerFamilies)
        {
            SprinklerFamilies.Add(family);
        }

        if (loadResult.ModelAnalysis != null)
        {
            ProjectState.ModelAnalysis = loadResult.ModelAnalysis;
        }

        ProjectState.LinkedModelScanOptions = LinkedModelScanOptionService.MergeDiscoveredWithExisting(
            loadResult.LinkedModelScanOptions,
            ProjectState.LinkedModelScanOptions).ToList();

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

    public HazardClassificationViewModel GetOrCreateHazardViewModel()
    {
        if (hazardViewModel != null)
        {
            return hazardViewModel;
        }

        hazardViewModel = new HazardClassificationViewModel(ProjectState.Rooms, SprinklerFamilies)
        {
            IsEmbeddedInShell = true,
            PersistToRevitRequested = PersistToRevitRequested,
            ShowRoomInRevitRequested = roomId => RequestShowRoomInRevit?.Invoke(roomId)
        };
        hazardViewModel.WorkflowProgressChanged += (_, _) => RequestWorkflowRefresh();

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

    public ModelAnalysisSummary ModelAnalysis { get; set; } = new ModelAnalysisSummary();

    public string DocumentKey { get; set; } = string.Empty;

    public string DocumentTitle { get; set; } = string.Empty;

    public IList<LinkedModelScanOption> LinkedModelScanOptions { get; set; } = new List<LinkedModelScanOption>();
}
