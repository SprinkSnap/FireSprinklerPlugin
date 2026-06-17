using System;
using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ISprinklerPlacementCandidateGenerator
{
    IReadOnlyList<SprinklerPlacementCandidate> GenerateCandidates(RoomInfo room);
}

public sealed class SprinklerPlacementCandidate
{
    public Point3D Location { get; set; } = new Point3D();

    public string CandidateType { get; set; } = "Conceptual";

    public string Basis { get; set; } = string.Empty;
}

public sealed class SprinklerPlacementCandidateGenerator : ISprinklerPlacementCandidateGenerator
{
    private const double MinimumBelowDeckFeet = 1.0;

    public IReadOnlyList<SprinklerPlacementCandidate> GenerateCandidates(RoomInfo room)
    {
        List<SprinklerPlacementCandidate> candidates = new List<SprinklerPlacementCandidate>();

        if (room.BoundaryPolygon.Count < 3)
        {
            return candidates;
        }

        double targetElevation = CalculateTargetSprinklerElevation(room);
        candidates.Add(new SprinklerPlacementCandidate
        {
            Location = new Point3D(room.Centroid.X, room.Centroid.Y, targetElevation),
            CandidateType = "Room Centroid",
            Basis = "Design-ready placeholder point. Future NFPA 13 engine will expand into spacing, obstruction, and branch-line candidates."
        });

        return candidates;
    }

    private static double CalculateTargetSprinklerElevation(RoomInfo room)
    {
        if (room.DeckElevationFeet > 0.0)
        {
            return room.DeckElevationFeet - MinimumBelowDeckFeet;
        }

        if (room.CeilingElevationFeet > 0.0)
        {
            return room.CeilingElevationFeet;
        }

        return Math.Max(room.FloorElevationFeet, room.FloorElevationFeet + room.HeightFeet - MinimumBelowDeckFeet);
    }
}

