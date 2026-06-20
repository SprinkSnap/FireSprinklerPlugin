using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface ISprinklerPlacementCandidateGenerator
{
    IReadOnlyList<SprinklerPlacementCandidate> GenerateCandidates(RoomInfo room);
}

public sealed class SprinklerPlacementCandidate
{
    public Point3D Location { get; set; } = new Point3D();

    public string CandidateType { get; set; } = "Automatic Layout Candidate";

    public string Basis { get; set; } = string.Empty;
}

public sealed class SprinklerPlacementCandidateGenerator : ISprinklerPlacementCandidateGenerator
{
    private readonly ISprinklerLayoutOptimizer optimizer;

    public SprinklerPlacementCandidateGenerator()
        : this(new SprinklerLayoutOptimizer())
    {
    }

    public SprinklerPlacementCandidateGenerator(ISprinklerLayoutOptimizer optimizer)
    {
        this.optimizer = optimizer;
    }

    public IReadOnlyList<SprinklerPlacementCandidate> GenerateCandidates(RoomInfo room)
    {
        SprinklerFamilyInfo family = SprinklerFamilySelector.DefaultFamilies.FirstOrDefault(candidateFamily =>
            candidateFamily.SupportedHazardClassifications.Contains(room.ApprovedHazardClassification)
            && candidateFamily.SupportedCeilingClassifications.Contains(room.CeilingClassification));

        if (family == null)
        {
            return new List<SprinklerPlacementCandidate>();
        }

        return optimizer.GenerateBestLayout(room, family).Candidates.ToList();
    }
}

