using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Clash;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;
using FireSprinklerPlugin.SprinkSnap.Core.Placement;

namespace FireSprinklerPlugin.SprinkSnap.Core.Workflow;

public static class DownstreamDesignInvalidationService
{
    public static void InvalidateDownstreamDesign(
        SprinkSnapProjectState state,
        IEnumerable<int> changedRoomRevitElementIds = null,
        bool clearHazardApprovalsForChangedRooms = false)
    {
        if (state == null)
        {
            return;
        }

        state.ClashSummary = new ClashDetectionSummary();
        state.PlacementSummary = new SprinklerPlacementSummary();
        state.PipePlacementSummary = new PipePlacementSummary();
        state.SchematicPipeRouting = new SchematicPipeRoutingSummary();
        state.HydraulicResult = new HydraulicCalculationResult();
        state.WaterSupplyValidation = new WaterSupplyValidationResult();

        state.SessionProgress.ClashDetectionComplete = false;
        state.SessionProgress.SprinklersPlacedInRevit = false;
        state.SessionProgress.DesignGenerated = false;
        state.SessionProgress.HydraulicsComplete = false;
        state.SessionProgress.MaterialsComplete = false;
        state.SessionProgress.ReportsExported = false;

        InvalidateChangedRoomLayouts(state, changedRoomRevitElementIds, clearHazardApprovalsForChangedRooms);
    }

    private static void InvalidateChangedRoomLayouts(
        SprinkSnapProjectState state,
        IEnumerable<int> changedRoomRevitElementIds,
        bool clearHazardApprovalsForChangedRooms)
    {
        HashSet<int> changedRoomIds = changedRoomRevitElementIds?.ToHashSet() ?? new HashSet<int>();
        if (changedRoomIds.Count == 0)
        {
            changedRoomIds = state.ModelChangeAssessment?.ChangedRoomRevitElementIds?.ToHashSet()
                ?? new HashSet<int>();
        }

        if (changedRoomIds.Count == 0)
        {
            foreach (RoomInfo room in state.Rooms)
            {
                ClearRoomLayout(room, clearHazardApprovalsForChangedRooms);
            }

            return;
        }

        foreach (RoomInfo room in state.Rooms.Where(candidate => changedRoomIds.Contains(candidate.RevitElementId)))
        {
            ClearRoomLayout(room, clearHazardApprovalsForChangedRooms);
        }
    }

    private static void ClearRoomLayout(RoomInfo room, bool clearHazardApproval)
    {
        if (room == null)
        {
            return;
        }

        room.ProposedSprinklers.Clear();
        if (!clearHazardApproval)
        {
            return;
        }

        room.DesignerApproved = false;
        room.ApprovedHazardClassification = string.Empty;
    }
}
