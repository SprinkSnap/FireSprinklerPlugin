using System.Collections.Generic;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core;

public interface IHazardClassifier
{
    HazardClassificationResult SuggestClassification(RoomInfo room);
}

public sealed class HazardClassifier : IHazardClassifier
{
    private readonly IReadOnlyList<IHazardClassificationRule> rules;

    public HazardClassifier()
        : this(NFPA13Rules.DefaultHazardRules)
    {
    }

    public HazardClassifier(IReadOnlyList<IHazardClassificationRule> rules)
    {
        this.rules = rules;
    }

    public HazardClassificationResult SuggestClassification(RoomInfo room)
    {
        foreach (IHazardClassificationRule rule in rules)
        {
            HazardClassificationResult result = rule.Evaluate(room);
            if (result != null)
            {
                return result;
            }
        }

        return new HazardClassificationResult();
    }
}

