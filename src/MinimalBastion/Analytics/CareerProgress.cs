using MinimalBastion.Core;
using MinimalBastion.Persistence;

namespace MinimalBastion.Analytics;

public sealed record RunMedalDefinition(string Id, string DisplayName, string Description);

public sealed record CareerMedalProgress(
    RunMedalDefinition Definition,
    int EarnedCount)
{
    public bool IsUnlocked => EarnedCount > 0;
}

public sealed record CareerAchievement(
    string Id,
    string DisplayName,
    string Description,
    bool IsUnlocked,
    string Progress);

public sealed class CareerProgress
{
    public required IReadOnlyList<RunHistoryEntry> Runs { get; init; }
    public required IReadOnlyList<CareerMedalProgress> Medals { get; init; }
    public required IReadOnlyList<CareerAchievement> Achievements { get; init; }
    public int CampaignsSecured { get; init; }
    public int TotalMedals { get; init; }
    public int MedalTypesUnlocked { get; init; }
    public int MapsSecured { get; init; }
    public RunHistoryEntry? DeepestRun { get; init; }
    public RunHistoryEntry? FastestClear { get; init; }
    public RunHistoryEntry? LeanestClear { get; init; }
    public RunHistoryEntry? HighestReserveClear { get; init; }
}

public static class CareerProgression
{
    private static readonly string[] RequiredDirectives =
    [
        "standard",
        "close_quarters",
        "core_six",
        "no_reserves"
    ];

    private static readonly RunMedalDefinition[] MedalCatalog =
    [
        new("flawless", "Flawless", "Secure the campaign without a leak."),
        new("last_stand", "Last Stand", "Secure with three or fewer lives remaining."),
        new("lean_grid", "Lean Grid", "Secure with at most 18 towers in the final defense."),
        new("minimal_grid", "Minimal Grid", "Secure with at most 12 towers in the final defense."),
        new("specialist", "Specialist", "Secure using no more than four tower types."),
        new("full_spectrum", "Full Spectrum", "Secure using at least eight tower types."),
        new("pure_defense", "Pure Defense", "Secure without Plates, Forge, or Protocol activations."),
        new("no_retreat", "No Retreat", "Secure without selling a tower."),
        new("bare_metal", "Bare Metal", "Secure without buying a tower upgrade."),
        new("early_command", "Early Command", "Secure after earning at least 200 early-call credits."),
        new("war_chest", "War Chest", "Secure with at least 5,000 credits in reserve."),
        new("protocol_chain", "Protocol Chain", "Secure after activating at least 20 Protocols."),
        new("plate_ace", "Plate Ace", "Secure after Pulse Plates defeat at least 25 threats."),
        new("forge_fed", "Forge Fed", "Secure after producing at least 10 forged charges."),
        new("bastion", "Bastion", "Secure a campaign on Bastion difficulty."),
        new("allied_hold", "Allied Hold", "Secure a campaign in online co-op."),
        new("rapid_response", "Rapid Response", "Secure a campaign within 20 defense minutes."),
        new("apex_line", "Apex Line", "Finish with at least six Apex towers."),
        new("mastery", "Mastery", $"Reach authored wave {GameConstants.MasteryFinalWave}."),
        new("deep_endless", "Deep Endless", "Reach endless wave 50.")
    ];

    public static IReadOnlyList<RunMedalDefinition> AllMedals => MedalCatalog;

    public static IReadOnlyList<RunMedalDefinition> MedalsFor(RunHistoryEntry entry) =>
        MedalCatalog.Where(medal => EarnsMedal(entry, medal.Id)).ToArray();

    public static CareerProgress Analyze(IEnumerable<RunHistoryEntry> entries)
    {
        var runs = entries.OrderByDescending(entry => entry.CompletedAtUtc).ToArray();
        var secured = runs.Where(SecuredCampaign).ToArray();
        var mapsSecured = DistinctMaps(secured);
        var bastionSecured = secured.Where(IsBastion).ToArray();
        var bastionMapsSecured = DistinctMaps(bastionSecured);
        var challengesSecured = secured.Select(entry => entry.ChallengeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directivesSecured = RequiredDirectives.Count(challengesSecured.Contains);
        var deepestWave = runs.Select(entry => entry.CurrentWave).DefaultIfEmpty(0).Max();
        var masteryMaps = runs.Where(entry => entry.CurrentWave >= GameConstants.MasteryFinalWave)
            .Select(entry => entry.MapId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var coOpSecured = secured.Count(entry => entry.IsCoOp);
        var totalProtocols = runs.Sum(entry => (long)entry.ProtocolActivations);
        var totalPlateKills = runs.Sum(entry => (long)entry.PlateKills);
        var totalForgedCharges = runs.Sum(entry => (long)entry.ForgedCharges);

        var medals = MedalCatalog.Select(definition => new CareerMedalProgress(definition,
                runs.Count(entry => EarnsMedal(entry, definition.Id))))
            .ToArray();
        var medalCounts = medals.ToDictionary(medal => medal.Definition.Id, medal => medal.EarnedCount,
            StringComparer.OrdinalIgnoreCase);
        var totalMedals = medals.Sum(medal => medal.EarnedCount);

        var achievements = new[]
        {
            Tracked("first_hold", "First Hold", "Secure any campaign.", secured.Length, 1),
            Tracked("seasoned_guard", "Seasoned Guard", "Secure 10 campaigns.", secured.Length, 10),
            Tracked("veteran_guard", "Veteran Guard", "Secure 25 campaigns.", secured.Length, 25),
            Tracked("cartographer", "Cartographer", "Secure all four arenas.", mapsSecured, 4),
            Tracked("bastion_clear", "Bastion Clear", "Secure any arena on Bastion difficulty.", bastionSecured.Length, 1),
            Tracked("bastion_circuit", "Bastion Circuit", "Secure all four arenas on Bastion.", bastionMapsSecured, 4),
            Tracked("directive_master", "Directive Master", "Secure Standard, Gauntlet, Core Six, and Entrenched.", directivesSecured, 4),
            Tracked("gauntlet_victor", "Signal Breaker", "Secure Signal Gauntlet.", SecuredDirective(secured, "close_quarters"), 1),
            Tracked("core_commander", "Core Commander", "Secure Core Six.", SecuredDirective(secured, "core_six"), 1),
            Tracked("entrenched_hold", "Entrenched Hold", "Secure Entrenched.", SecuredDirective(secured, "no_reserves"), 1),
            Tracked("untouched", "Untouched", "Earn the Flawless medal.", MedalCount(medalCounts, "flawless"), 1),
            Tracked("perfect_five", "Perfect Five", "Earn Flawless in five runs.", MedalCount(medalCounts, "flawless"), 5),
            Tracked("lean_architect", "Lean Architect", "Earn the Lean Grid medal.", MedalCount(medalCounts, "lean_grid"), 1),
            Tracked("minimal_architect", "Minimal Architect", "Earn the Minimal Grid medal.", MedalCount(medalCounts, "minimal_grid"), 1),
            Tracked("specialist_seal", "Specialist Seal", "Earn the Specialist medal.", MedalCount(medalCounts, "specialist"), 1),
            Tracked("arsenal_master", "Arsenal Master", "Earn the Full Spectrum medal.", MedalCount(medalCounts, "full_spectrum"), 1),
            Tracked("pure_defender", "Pure Defender", "Earn the Pure Defense medal.", MedalCount(medalCounts, "pure_defense"), 1),
            Tracked("last_stand", "Against the Brink", "Earn the Last Stand medal.", MedalCount(medalCounts, "last_stand"), 1),
            Tracked("early_commander", "Early Commander", "Earn the Early Command medal.", MedalCount(medalCounts, "early_command"), 1),
            Tracked("quartermaster", "Quartermaster", "Earn the War Chest medal.", MedalCount(medalCounts, "war_chest"), 1),
            Tracked("protocol_authority", "Protocol Authority", "Activate 250 Protocols across retained runs.", totalProtocols, 250),
            Tracked("plate_engineer", "Plate Engineer", "Defeat 500 threats with Pulse Plates.", totalPlateKills, 500),
            Tracked("forge_network", "Forge Network", "Produce 100 forged charges.", totalForgedCharges, 100),
            Tracked("apex_command", "Apex Command", "Earn the Apex Line medal.", MedalCount(medalCounts, "apex_line"), 1),
            Tracked("mastery_30", "Mastery 30", $"Reach authored wave {GameConstants.MasteryFinalWave}.", deepestWave, GameConstants.MasteryFinalWave),
            Tracked("mastery_circuit", "Mastery Circuit", "Reach wave 30 on all four arenas.", masteryMaps, 4),
            Tracked("endless_50", "Endless 50", "Reach endless wave 50.", deepestWave, 50),
            Tracked("endless_75", "Endless 75", "Reach endless wave 75.", deepestWave, 75),
            Tracked("endless_100", "Endless 100", "Reach endless wave 100.", deepestWave, 100),
            Tracked("allied_victory", "Allied Victory", "Secure an online co-op campaign.", coOpSecured, 1),
            Tracked("allied_veteran", "Allied Veteran", "Secure five online co-op campaigns.", coOpSecured, 5),
            Tracked("decorated", "Decorated", "Earn 50 run medals.", totalMedals, 50)
        };

        return new CareerProgress
        {
            Runs = runs,
            Medals = medals,
            Achievements = achievements,
            CampaignsSecured = secured.Length,
            TotalMedals = totalMedals,
            MedalTypesUnlocked = medals.Count(medal => medal.IsUnlocked),
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

    private static bool EarnsMedal(RunHistoryEntry entry, string medalId)
    {
        var secured = SecuredCampaign(entry);
        var finalTowerCount = entry.FinalLayout?.Towers.Count ?? int.MaxValue;
        var towerTypes = entry.Towers.Count(tower => tower.Purchases > 0);
        var towerSales = entry.Towers.Sum(tower => tower.Sales);
        var towerUpgrades = entry.Towers.Sum(tower => tower.Upgrades);
        var apexTowers = entry.FinalLayout?.Towers.Count(tower => tower.IsApex) ?? 0;
        var noTacticalSystems = entry.PlateDeployments == 0 && entry.ForgePurchases == 0 &&
                                entry.ProtocolActivations == 0;

        return medalId switch
        {
            "flawless" => secured && entry.Leaks == 0,
            "last_stand" => secured && entry.Lives is > 0 and <= 3,
            "lean_grid" => secured && finalTowerCount <= 18,
            "minimal_grid" => secured && finalTowerCount <= 12,
            "specialist" => secured && towerTypes is > 0 and <= 4,
            "full_spectrum" => secured && towerTypes >= 8,
            "pure_defense" => secured && noTacticalSystems,
            "no_retreat" => secured && towerSales == 0,
            "bare_metal" => secured && towerTypes > 0 && towerUpgrades == 0,
            "early_command" => secured && entry.EarlyCallCredits >= 200,
            "war_chest" => secured && entry.CreditsRemaining >= 5_000,
            "protocol_chain" => secured && entry.ProtocolActivations >= 20,
            "plate_ace" => secured && entry.PlateKills >= 25,
            "forge_fed" => secured && entry.ForgedCharges >= 10,
            "bastion" => secured && IsBastion(entry),
            "allied_hold" => secured && entry.IsCoOp,
            "rapid_response" => secured && entry.DefenseSeconds is > 0 and <= 1_200,
            "apex_line" => apexTowers >= 6,
            "mastery" => entry.CurrentWave >= GameConstants.MasteryFinalWave,
            "deep_endless" => entry.CurrentWave >= 50,
            _ => false
        };
    }

    private static bool IsBastion(RunHistoryEntry entry) =>
        entry.DifficultyId.Equals("bastion", StringComparison.OrdinalIgnoreCase);

    private static int DistinctMaps(IEnumerable<RunHistoryEntry> entries) =>
        entries.Select(entry => entry.MapId).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static int SecuredDirective(IEnumerable<RunHistoryEntry> entries, string challengeId) =>
        entries.Count(entry => entry.ChallengeId.Equals(challengeId, StringComparison.OrdinalIgnoreCase));

    private static int MedalCount(IReadOnlyDictionary<string, int> counts, string medalId) =>
        counts.GetValueOrDefault(medalId);

    private static CareerAchievement Tracked(string id, string name, string description, long current, long target) =>
        new(id, name, description, current >= target, $"{Math.Min(current, target):N0}/{target:N0}");
}
