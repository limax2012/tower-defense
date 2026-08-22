using MinimalBastion.Core;
using MinimalBastion.Persistence;

namespace MinimalBastion.Analytics;

public sealed record RunMedalDefinition(string Id, string DisplayName, string Description);

public sealed record CareerAchievement(
    string Id,
    string DisplayName,
    string Description,
    bool IsUnlocked,
    string Progress);

public sealed class CareerProgress
{
    public required IReadOnlyList<RunHistoryEntry> Runs { get; init; }
    public required IReadOnlyList<CareerAchievement> Achievements { get; init; }
    public int CampaignsSecured { get; init; }
    public int TotalMedals { get; init; }
    public int MapsSecured { get; init; }
    public RunHistoryEntry? DeepestRun { get; init; }
    public RunHistoryEntry? FastestClear { get; init; }
    public RunHistoryEntry? LeanestClear { get; init; }
    public RunHistoryEntry? HighestReserveClear { get; init; }
}

public static class CareerProgression
{
    private static readonly RunMedalDefinition[] MedalCatalog =
    [
        new("flawless", "Flawless", "Secure the campaign without a leak."),
        new("lean_grid", "Lean Grid", "Secure the campaign with at most 18 towers in the final defense."),
        new("specialist", "Specialist", "Secure the campaign using no more than four tower types."),
        new("pure_defense", "Pure Defense", "Secure the campaign without Plates, Forge, or Protocol activations."),
        new("bastion", "Bastion", "Secure a campaign on Bastion difficulty."),
        new("mastery", "Mastery", $"Reach authored wave {GameConstants.MasteryFinalWave}."),
        new("deep_endless", "Deep Endless", "Reach endless wave 50.")
    ];

    public static IReadOnlyList<RunMedalDefinition> AllMedals => MedalCatalog;

    public static IReadOnlyList<RunMedalDefinition> MedalsFor(RunHistoryEntry entry)
    {
        var secured = SecuredCampaign(entry);
        var finalTowerCount = entry.FinalLayout?.Towers.Count ?? int.MaxValue;
        var towerTypes = entry.Towers.Count(tower => tower.Purchases > 0);
        var noTacticalSystems = entry.PlateDeployments == 0 && entry.ForgePurchases == 0 && entry.ProtocolActivations == 0;
        return MedalCatalog.Where(medal => medal.Id switch
        {
            "flawless" => secured && entry.Leaks == 0,
            "lean_grid" => secured && finalTowerCount <= 18,
            "specialist" => secured && towerTypes is > 0 and <= 4,
            "pure_defense" => secured && noTacticalSystems,
            "bastion" => secured && entry.DifficultyId.Equals("bastion", StringComparison.OrdinalIgnoreCase),
            "mastery" => entry.CurrentWave >= GameConstants.MasteryFinalWave,
            "deep_endless" => entry.CurrentWave >= 50,
            _ => false
        }).ToArray();
    }

    public static CareerProgress Analyze(IEnumerable<RunHistoryEntry> entries)
    {
        var runs = entries.OrderByDescending(entry => entry.CompletedAtUtc).ToArray();
        var secured = runs.Where(SecuredCampaign).ToArray();
        var mapsSecured = secured.Select(entry => entry.MapId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var challengesSecured = secured.Select(entry => entry.ChallengeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bastionSecured = secured.Any(entry => entry.DifficultyId.Equals("bastion", StringComparison.OrdinalIgnoreCase));
        var flawless = secured.Any(entry => entry.Leaks == 0);
        var lean = secured.Any(entry => (entry.FinalLayout?.Towers.Count ?? int.MaxValue) <= 18);
        var deepestWave = runs.Select(entry => entry.CurrentWave).DefaultIfEmpty(0).Max();

        var achievements = new[]
        {
            Achievement("first_hold", "First Hold", "Secure any campaign.", secured.Length > 0, $"{Math.Min(secured.Length, 1)}/1"),
            Achievement("cartographer", "Cartographer", "Secure all four arenas.", mapsSecured >= 4, $"{Math.Min(mapsSecured, 4)}/4"),
            Achievement("bastion_clear", "Bastion Clear", "Secure any arena on Bastion difficulty.", bastionSecured, bastionSecured ? "1/1" : "0/1"),
            Achievement("directive_master", "Directive Master", "Secure Standard, Gauntlet, Core Six, and Entrenched.",
                new[] { "standard", "close_quarters", "core_six", "no_reserves" }.All(challengesSecured.Contains),
                $"{new[] { "standard", "close_quarters", "core_six", "no_reserves" }.Count(challengesSecured.Contains)}/4"),
            Achievement("untouched", "Untouched", "Secure a campaign without a leak.", flawless, flawless ? "1/1" : "0/1"),
            Achievement("lean_architect", "Lean Architect", "Earn the Lean Grid medal.", lean, lean ? "1/1" : "0/1"),
            Achievement("mastery_30", "Mastery 30", $"Reach authored wave {GameConstants.MasteryFinalWave}.",
                deepestWave >= GameConstants.MasteryFinalWave, $"{Math.Min(deepestWave, GameConstants.MasteryFinalWave)}/{GameConstants.MasteryFinalWave}"),
            Achievement("endless_50", "Endless 50", "Reach endless wave 50.", deepestWave >= 50, $"{Math.Min(deepestWave, 50)}/50")
        };

        return new CareerProgress
        {
            Runs = runs,
            Achievements = achievements,
            CampaignsSecured = secured.Length,
            TotalMedals = runs.Sum(entry => MedalsFor(entry).Count),
            MapsSecured = mapsSecured,
            DeepestRun = runs.OrderByDescending(entry => entry.CurrentWave).ThenByDescending(entry => entry.Lives).FirstOrDefault(),
            FastestClear = secured.Where(entry => entry.DefenseSeconds > 0).OrderBy(entry => entry.DefenseSeconds).FirstOrDefault(),
            LeanestClear = secured.Where(entry => entry.FinalLayout is not null)
                .OrderBy(entry => entry.FinalLayout!.Towers.Count).ThenByDescending(entry => entry.Lives).FirstOrDefault(),
            HighestReserveClear = secured.OrderByDescending(entry => entry.CreditsRemaining).FirstOrDefault()
        };
    }

    public static bool SecuredCampaign(RunHistoryEntry entry) =>
        entry.Victory || entry.IsEndless && entry.CurrentWave >= GameConstants.CampaignWaveCount;

    private static CareerAchievement Achievement(string id, string name, string description, bool unlocked, string progress) =>
        new(id, name, description, unlocked, progress);
}
