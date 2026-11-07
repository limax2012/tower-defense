using System.Text.Json.Serialization;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record PlanningScenario
{
    public string MapId { get; init; } = "";
    public string DifficultyId { get; init; } = DifficultyCatalog.DefaultId;
    public string ChallengeId { get; init; } = ChallengeCatalog.DefaultId;
    public int Seed { get; init; } = 1337;
    public bool CoOp { get; init; }
    public int Horizon { get; init; } = GameConstants.CampaignWaveCount;
    public bool Endless { get; init; }
    public string StartingProfile { get; init; } = "fresh";
    public string InformationProfile { get; init; } = "oracle";
    public string ExecutionProfile { get; init; } = "expert";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ForbidCombatSales { get; init; }
    [JsonIgnore]
    public string Key => $"{MapId}/{DifficultyId}/{ChallengeId}/{(CoOp ? "coop" : "solo")}/{(Endless ? "endless" : "campaign")}/{Horizon}/{StartingProfile}/{InformationProfile}/{ExecutionProfile}/{Seed}" +
        (ForbidCombatSales ? "/no-combat-sales" : "");

    internal static bool IsSale(GameCommandType type) =>
        type is GameCommandType.SellTower or GameCommandType.SellGenerator;

    internal bool ForbidsSale(GameCommandType type, bool duringWave) => ForbidCombatSales && duringWave && IsSale(type);
}

public sealed record PlanningExecutionProfile(string Id, int ReactionDelayTicks, int MinimumActionIntervalTicks,
    int MaximumCombatActionsPerWave, bool CombatPurchases, bool TacticalActions)
{
    public static IReadOnlyList<PlanningExecutionProfile> All { get; } =
    [
        new("expert", 0, 0, 5000, true, true),
        new("informed", 30, 15, 60, true, true),
        new("low-micro", 60, 60, 2, false, false)
    ];

    public static PlanningExecutionProfile Resolve(string id) => All.FirstOrDefault(profile => profile.Id == id)
        ?? throw new ArgumentException($"Unknown execution profile '{id}'.", nameof(id));
}

public sealed record ScenarioExclusion(string MapId, string DifficultyId, string ChallengeId, string Scope, string Reason);
public sealed record PlanningScenarioDiscovery(IReadOnlyList<PlanningScenario> Scenarios,
    IReadOnlyList<ScenarioExclusion> Exclusions, IReadOnlyList<string> Restrictions);

public static class PlanningScenarioRegistry
{
    public static PlanningScenarioDiscovery Discover(GameContent content, IEnumerable<int>? seeds = null,
        bool includeCoOp = false, int? endlessHorizon = null)
    {
        if (endlessHorizon is <= GameConstants.CampaignWaveCount)
            throw new ArgumentOutOfRangeException(nameof(endlessHorizon), "Endless evaluation needs an explicit horizon after the campaign.");
        var maps = content.Maps.Count == 0 ? new[] { content.Map } : content.Maps.Values.ToArray();
        var difficulties = content.Difficulties.Count == 0
            ? new[] { DifficultyCatalog.Resolve(content, null) } : content.Difficulties.Values.ToArray();
        var challenges = content.Challenges.Count == 0
            ? new[] { ChallengeCatalog.Resolve(content, null) } : content.Challenges.Values.ToArray();
        var seedSet = (seeds ?? new[] { 1337 }).Distinct().Order().ToArray();
        if (seedSet.Length == 0) throw new ArgumentException("At least one seed is required.", nameof(seeds));
        var scenarios = new List<PlanningScenario>();
        var exclusions = new List<ScenarioExclusion>();
        foreach (var map in maps.OrderBy(value => value.Id, StringComparer.Ordinal))
        foreach (var difficulty in difficulties.OrderBy(value => value.Id, StringComparer.Ordinal))
        foreach (var challenge in challenges.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            if (challenge.IsSandbox)
            {
                exclusions.Add(new(map.Id, difficulty.Id, challenge.Id, "solo", "Sandbox has unlimited resources and no campaign victory; excluded from competitive completion denominator."));
                exclusions.Add(new(map.Id, difficulty.Id, challenge.Id, "coop", "Sandbox is solo-only."));
                continue;
            }
            foreach (var seed in seedSet)
            foreach (var coOp in includeCoOp ? new[] { false, true } : new[] { false })
            {
                var scenario = new PlanningScenario { MapId = map.Id, DifficultyId = difficulty.Id,
                    ChallengeId = challenge.Id, Seed = seed, CoOp = coOp };
                scenarios.Add(scenario);
                if (endlessHorizon is { } horizon)
                    scenarios.Add(scenario with { Endless = true, Horizon = horizon });
            }
        }
        return new(scenarios, exclusions,
        [
            "The fresh profile is the GameSession constructor: authored starting credits/lives/inventory, empty board, challenge exclusions and wave-gated Apex. Persistent discovery does not change this economy.",
            "Seeds label matched experiments and candidate exploration; authored campaign wave generation is deterministic and repeated seeds are not independent generalization evidence.",
            "Endless outcomes are survival through the declared horizon, never full completion of an infinite mode.",
            includeCoOp ? "Co-op scenarios are separately labeled shared-economy command simulations; network latency and two-human coordination require separate measurements."
                : "Co-op is valid competitive content outside this solo audit scope, not an invalid combination.",
            "Maps are discovered from GameContent.Maps. Candidate geometry uses authored paths, build regions, restrictions, and power nodes; no map names or coordinate build orders."
        ]);
    }
}
