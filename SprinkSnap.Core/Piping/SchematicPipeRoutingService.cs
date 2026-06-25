using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Piping;

namespace FireSprinklerPlugin.SprinkSnap.Core.Piping;

public static class SchematicPipeRoutingService
{
    public static SchematicPipeRoutingSummary RefreshProjectRouting(SprinkSnapProjectState projectState)
    {
        SchematicPipeRoutingSummary summary = SchematicPipeRouter.RouteProject(projectState?.Rooms);
        if (projectState != null)
        {
            projectState.SchematicPipeRouting = summary;
            if (summary.TotalSegmentCount > 0)
            {
                projectState.SessionProgress.DesignGenerated = true;
            }
        }

        return summary;
    }

    public static IList<PipeSegment> GetSegmentsForRoom(
        SchematicPipeRoutingSummary summary,
        int roomRevitElementId)
    {
        return summary?.Segments?
            .Where(segment => segment.RoomRevitElementId == roomRevitElementId)
            .ToList() ?? new List<PipeSegment>();
    }
}
