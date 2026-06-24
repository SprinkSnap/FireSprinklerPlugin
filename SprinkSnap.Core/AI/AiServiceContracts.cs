using System.Threading;
using System.Threading.Tasks;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.AI;

public sealed class AiSuggestion<T>
{
    public T Value { get; set; }

    public double Confidence { get; set; }

    public string Reasoning { get; set; } = string.Empty;

    public bool RequiresDesignerApproval { get; set; } = true;
}

public interface IHazardAiAdvisor
{
    Task<AiSuggestion<string>> SuggestHazardAsync(RoomInfo room, CancellationToken cancellationToken);
}

public interface ISprinklerAiAdvisor
{
    Task<AiSuggestion<SprinklerFamilyInfo>> SuggestSprinklerAsync(
        RoomInfo room,
        ProjectSprinklerStandard projectStandard,
        CancellationToken cancellationToken);
}

public interface IAiExplanationService
{
    Task<string> ExplainRecommendationAsync(string deterministicResult, CancellationToken cancellationToken);
}

