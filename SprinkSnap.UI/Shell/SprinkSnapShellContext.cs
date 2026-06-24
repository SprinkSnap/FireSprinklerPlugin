using System;
using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.UI.Shell;

public sealed class SprinkSnapShellContext
{
    private HazardClassificationViewModel hazardViewModel;

    public SprinkSnapShellContext(
        SprinkSnapProjectState projectState,
        IReadOnlyList<SprinklerFamilyInfo> sprinklerFamilies = null)
    {
        ProjectState = projectState ?? new SprinkSnapProjectState();
        SprinklerFamilies = sprinklerFamilies?.ToList()
            ?? new SprinklerFamilySelector().GetAvailableFamilies().ToList();
    }

    public event EventHandler WorkflowChanged;

    public SprinkSnapProjectState ProjectState { get; }

    public IReadOnlyList<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public bool IsPreviewMode { get; set; }

    public static SprinkSnapShellContext CreateEmpty()
    {
        return new SprinkSnapShellContext(new SprinkSnapProjectState());
    }

    public void RequestWorkflowRefresh()
    {
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public HazardClassificationViewModel GetOrCreateHazardViewModel()
    {
        if (hazardViewModel != null)
        {
            return hazardViewModel;
        }

        hazardViewModel = new HazardClassificationViewModel(ProjectState.Rooms, SprinklerFamilies)
        {
            IsEmbeddedInShell = true
        };
        hazardViewModel.WorkflowProgressChanged += (_, _) => RequestWorkflowRefresh();

        return hazardViewModel;
    }
}
