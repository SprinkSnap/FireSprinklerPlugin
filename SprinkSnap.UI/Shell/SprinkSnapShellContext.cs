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

    public SprinkSnapProjectState ProjectState { get; }

    public IReadOnlyList<SprinklerFamilyInfo> SprinklerFamilies { get; }

    public bool IsPreviewMode { get; set; }

    public static SprinkSnapShellContext CreateEmpty()
    {
        return new SprinkSnapShellContext(new SprinkSnapProjectState());
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

        return hazardViewModel;
    }
}
