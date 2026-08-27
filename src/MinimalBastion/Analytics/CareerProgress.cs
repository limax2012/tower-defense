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
    string Category,
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
    public int AchievementsUnlocked { get; init; }
    public int MapsSecured { get; init; }
    public RunHistoryEntry? DeepestRun { get; init; }
    public RunHistoryEntry? FastestClear { get; init; }
    public RunHistoryEntry? LeanestClear { get; init; }
    public RunHistoryEntry? HighestReserveClear { get; init; }
}

public static class CareerProgression
{
    private static readonly string[] RequiredMaps =
    [
        "foundry_loop",
        "crosswind_basin",
        "prism_circuit",
        "relay_divide"
    ];

    private static readonly string[] RequiredDirectives =
    [
        "standard",
        "close_quarters",
        "core_six",
        "no_reserves"
    ];

    private static readonly string[] RequiredDifficulties =
    [
        "easy",
        "normal",
        "hard",
        "bastion"
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
        new("mastery", "Apex Command", "Secure the campaign with at least one Apex tower."),
        new("deep_endless", "Deep Endless", "Reach endless wave 50."),
        new("iron_bastion", "Iron Bastion", "Secure Bastion without a leak."),
        new("gauntlet_bastion", "Signal Bastion", "Secure Signal Gauntlet on Bastion."),
        new("core_bastion", "Core Bastion", "Secure Core Six on Bastion."),
        new("entrenched_bastion", "Entrenched Bastion", "Secure Entrenched on Bastion."),
        new("tactical_triad", "Tactical Triad", "Secure with 10 Protocols, 10 Plate kills, and 5 forged charges."),
        new("bastion_mastery", "Bastion Apex", "Secure Bastion with at least one Apex tower."),
        new("endless_75", "Endless 75", "Reach endless wave 75."),
        new("century_hold", "Century Hold", "Reach endless wave 100.")
    ];

    public static IReadOnlyList<RunMedalDefinition> AllMedals => MedalCatalog;

    public static IReadOnlyList<RunMedalDefinition> MedalsFor(RunHistoryEntry entry) =>
        MedalCatalog.Where(medal => EarnsMedal(entry, medal.Id)).ToArray();

    public static CareerProgress Analyze(IEnumerable<RunHistoryEntry> entries)
    {
        var runs = entries.OrderByDescending(entry => entry.CompletedAtUtc).ToArray();
        var secured = runs.Where(SecuredCampaign).ToArray();
        var mapsSecured = DistinctRequiredMaps(secured);
        var bastionSecured = secured.Where(IsBastion).ToArray();
        var bastionMapsSecured = DistinctRequiredMaps(bastionSecured);
        var challengesSecured = secured.Select(entry => entry.ChallengeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directivesSecured = RequiredDirectives.Count(challengesSecured.Contains);
        var difficultiesSecured = RequiredDifficulties.Count(difficulty =>
            secured.Any(entry => entry.DifficultyId.Equals(difficulty, StringComparison.OrdinalIgnoreCase)));
        var mapDifficultyPairs = DistinctPairs(secured, entry => entry.MapId, entry => entry.DifficultyId,
            RequiredMaps, RequiredDifficulties);
        var mapDirectivePairs = DistinctPairs(secured, entry => entry.MapId, entry => entry.ChallengeId,
            RequiredMaps, RequiredDirectives);
        var deepestWave = runs.Select(entry => entry.CurrentWave).DefaultIfEmpty(0).Max();
        var apexRuns = secured.Where(entry => entry.FinalLayout?.Towers.Any(tower => tower.IsApex) == true).ToArray();
        var apexMaps = DistinctRequiredMaps(apexRuns);
        var apexDirectives = RequiredDirectives.Count(directive =>
            apexRuns.Any(entry => entry.ChallengeId.Equals(directive, StringComparison.OrdinalIgnoreCase)));
        var apexMapDirectivePairs = DistinctPairs(apexRuns, entry => entry.MapId, entry => entry.ChallengeId,
            RequiredMaps, RequiredDirectives);
        var bastionApexRuns = apexRuns.Where(IsBastion).ToArray();
        var bastionApexMaps = DistinctRequiredMaps(bastionApexRuns);
        var bastionApexMapDirectivePairs = DistinctPairs(bastionApexRuns, entry => entry.MapId,
            entry => entry.ChallengeId, RequiredMaps, RequiredDirectives);
        var wave50Runs = runs.Where(entry => entry.CurrentWave >= 50).ToArray();
        var wave50Maps = DistinctRequiredMaps(wave50Runs);
        var wave50Directives = RequiredDirectives.Count(directive =>
            wave50Runs.Any(entry => entry.ChallengeId.Equals(directive, StringComparison.OrdinalIgnoreCase)));
        var wave50MapDirectivePairs = DistinctPairs(wave50Runs, entry => entry.MapId, entry => entry.ChallengeId,
            RequiredMaps, RequiredDirectives);
        var wave75Maps = DistinctRequiredMaps(runs.Where(entry => entry.CurrentWave >= 75));
        var wave100Maps = DistinctRequiredMaps(runs.Where(entry => entry.CurrentWave >= 100));
        var coOpSecured = secured.Count(entry => entry.IsCoOp);
        var coOpMaps = DistinctRequiredMaps(secured.Where(entry => entry.IsCoOp));
        var totalProtocols = runs.Sum(entry => (long)entry.ProtocolActivations);
        var totalPlateKills = runs.Sum(entry => (long)entry.PlateKills);
        var totalForgedCharges = runs.Sum(entry => (long)entry.ForgedCharges);
        var towerTypesUsed = secured.SelectMany(entry => entry.Towers)
            .Where(tower => tower.Purchases > 0)
            .Select(tower => tower.TowerId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var finalTowers = secured.Where(entry => entry.FinalLayout is not null)
            .SelectMany(entry => entry.FinalLayout!.Towers)
            .ToArray();
        var doctrinesSeen = finalTowers.Where(tower => !string.IsNullOrWhiteSpace(tower.DoctrineId))
            .Select(tower => $"{tower.DefinitionId}:{tower.DoctrineId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var rolesSeen = finalTowers.Where(tower => !string.IsNullOrWhiteSpace(tower.SpecializationId))
            .Select(tower => $"{tower.DefinitionId}:{tower.SpecializationId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var designsSeen = finalTowers.Where(tower => !string.IsNullOrWhiteSpace(tower.DoctrineId) &&
                                                      !string.IsNullOrWhiteSpace(tower.SpecializationId))
            .Select(tower => $"{tower.DefinitionId}:{tower.DoctrineId}:{tower.SpecializationId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var apexTypes = finalTowers.Where(tower => tower.IsApex)
            .Select(tower => tower.DefinitionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var medals = MedalCatalog.Select(definition => new CareerMedalProgress(definition,
                runs.Count(entry => EarnsMedal(entry, definition.Id))))
            .ToArray();
        var medalCounts = medals.ToDictionary(medal => medal.Definition.Id, medal => medal.EarnedCount,
            StringComparer.OrdinalIgnoreCase);
        var totalMedals = medals.Sum(medal => medal.EarnedCount);

        var medalTypesUnlocked = medals.Count(medal => medal.IsUnlocked);
        var precisionHonors = MedalGroupCount(medalCounts, "flawless", "lean_grid", "minimal_grid", "specialist");
        var tacticalHonors = MedalGroupCount(medalCounts, "early_command", "protocol_chain", "plate_ace", "forge_fed");
        var bastionHonors = MedalGroupCount(medalCounts, "bastion", "iron_bastion", "gauntlet_bastion", "core_bastion", "entrenched_bastion", "bastion_mastery");
        var enduranceHonors = MedalGroupCount(medalCounts, "mastery", "deep_endless", "endless_75", "century_hold");
        var totalCommandRequirements =
            (medalTypesUnlocked >= MedalCatalog.Length ? 1 : 0) +
            (mapDifficultyPairs >= 16 ? 1 : 0) +
            (bastionApexMapDirectivePairs >= 16 ? 1 : 0) +
            (designsSeen >= 40 ? 1 : 0) +
            (wave100Maps >= 4 ? 1 : 0) +
            (secured.Length >= 50 ? 1 : 0);

        var achievements = new[]
        {
            Tracked("Career", "first_hold", "First Hold", "Secure any campaign.", secured.Length, 1),
            Tracked("Career", "field_tested", "Field Tested", "Secure five campaigns.", secured.Length, 5),
            Tracked("Career", "seasoned_guard", "Seasoned Guard", "Secure ten campaigns.", secured.Length, 10),
            Tracked("Career", "veteran_guard", "Veteran Guard", "Secure 25 campaigns.", secured.Length, 25),
            Tracked("Career", "eternal_guard", "Eternal Guard", "Secure 50 campaigns.", secured.Length, 50),
            Tracked("Career", "cartographer", "Cartographer", "Secure all four arenas.", mapsSecured, 4),
            Tracked("Career", "difficulty_ladder", "Difficulty Ladder", "Secure campaigns on every difficulty.", difficultiesSecured, 4),
            Tracked("Career", "world_tour", "World Tour", "Secure every arena on every difficulty.", mapDifficultyPairs, 16),

            Tracked("Directives", "standard_circuit", "Standard Circuit", "Secure Standard on all four arenas.", SecuredMapsForDirective(secured, "standard"), 4),
            Tracked("Directives", "signal_circuit", "Signal Circuit", "Secure Signal Gauntlet on all four arenas.", SecuredMapsForDirective(secured, "close_quarters"), 4),
            Tracked("Directives", "core_circuit", "Core Circuit", "Secure Core Six on all four arenas.", SecuredMapsForDirective(secured, "core_six"), 4),
            Tracked("Directives", "entrenched_circuit", "Entrenched Circuit", "Secure Entrenched on all four arenas.", SecuredMapsForDirective(secured, "no_reserves"), 4),
            Tracked("Directives", "directive_master", "Directive Master", "Secure all four directives.", directivesSecured, 4),
            Tracked("Directives", "doctrine_grid", "Doctrine Grid", "Secure every arena and directive pairing.", mapDirectivePairs, 16),
            Tracked("Directives", "bastion_circuit", "Bastion Circuit", "Secure all four arenas on Bastion.", bastionMapsSecured, 4),
            Tracked("Directives", "bastion_doctrine", "Bastion Doctrine", "Secure all four directives on Bastion.", RequiredDirectives.Count(directive => bastionSecured.Any(entry => entry.ChallengeId.Equals(directive, StringComparison.OrdinalIgnoreCase))), 4),

            Tracked("Apex", "mastery_veteran", "Apex Veteran", "Secure five campaigns with an Apex tower.", apexRuns.Length, 5),
            Tracked("Apex", "mastery_explorer", "Apex Explorer", "Secure two arenas with an Apex tower.", apexMaps, 2),
            Tracked("Apex", "mastery_circuit", "Apex Circuit", "Secure all four arenas with an Apex tower.", apexMaps, 4),
            Tracked("Apex", "mastery_directives", "Apex Directives", "Secure every directive with an Apex tower.", apexDirectives, 4),
            Tracked("Apex", "mastery_grid", "Apex Grid", "Secure every arena and directive pairing with an Apex tower.", apexMapDirectivePairs, 16),
            Tracked("Apex", "bastion_mastery_veteran", "Bastion Apex Veteran", "Secure five Bastion campaigns with an Apex tower.", bastionApexRuns.Length, 5),
            Tracked("Apex", "bastion_mastery_circuit", "Bastion Apex Circuit", "Secure every arena on Bastion with an Apex tower.", bastionApexMaps, 4),
            Tracked("Apex", "bastion_mastery_matrix", "Bastion Apex Matrix", "Secure every arena and directive pairing on Bastion with an Apex tower.", bastionApexMapDirectivePairs, 16),

            Tracked("Endurance", "deep_explorer", "Deep Explorer", "Reach wave 50 on two arenas.", wave50Maps, 2),
            Tracked("Endurance", "deep_circuit", "Deep Circuit", "Reach wave 50 on all four arenas.", wave50Maps, 4),
            Tracked("Endurance", "endless_directives", "Endless Directives", "Reach wave 50 with every directive.", wave50Directives, 4),
            Tracked("Endurance", "endless_matrix", "Endless Matrix", "Reach wave 50 for every arena and directive pairing.", wave50MapDirectivePairs, 16),
            Tracked("Endurance", "deep_75_circuit", "Deep 75 Circuit", "Reach wave 75 on all four arenas.", wave75Maps, 4),
            Tracked("Endurance", "century_circuit", "Century Circuit", "Reach wave 100 on all four arenas.", wave100Maps, 4),
            Tracked("Endurance", "eternal_run", "Eternal Run", "Reach endless wave 150.", deepestWave, 150),
            Tracked("Endurance", "last_light", "Last Light", "Reach endless wave 200.", deepestWave, 200),

            Tracked("Arsenal", "full_arsenal", "Full Arsenal", "Deploy every tower type across secured campaigns.", towerTypesUsed, 10),
            Tracked("Arsenal", "doctrine_scholar", "Doctrine Scholar", "Archive ten distinct tower and Tier 2 doctrine pairings.", doctrinesSeen, 10),
            Tracked("Arsenal", "doctrine_complete", "Doctrine Complete", "Archive both Tier 2 doctrines for every tower.", doctrinesSeen, 20),
            Tracked("Arsenal", "role_scholar", "Role Scholar", "Archive ten distinct tower and final-role pairings.", rolesSeen, 10),
            Tracked("Arsenal", "role_complete", "Role Complete", "Archive both final roles for every tower.", rolesSeen, 20),
            Tracked("Arsenal", "design_scholar", "Design Scholar", "Archive 20 distinct doctrine and final-role tower designs.", designsSeen, 20),
            Tracked("Arsenal", "design_archive", "Design Archive", "Archive all 40 doctrine and final-role tower designs.", designsSeen, 40),
            Tracked("Arsenal", "apex_arsenal", "Apex Arsenal", "Finish runs with every tower type promoted to Apex.", apexTypes, 10),

            Tracked("Operations", "protocol_authority", "Protocol Authority", "Activate 250 Protocols across retained runs.", totalProtocols, 250),
            Tracked("Operations", "protocol_command", "Protocol Command", "Activate 1,000 Protocols across retained runs.", totalProtocols, 1_000),
            Tracked("Operations", "plate_engineer", "Plate Engineer", "Defeat 500 threats with Pulse Plates.", totalPlateKills, 500),
            Tracked("Operations", "plate_corps", "Plate Corps", "Defeat 2,500 threats with Pulse Plates.", totalPlateKills, 2_500),
            Tracked("Operations", "forge_network", "Forge Network", "Produce 100 forged charges.", totalForgedCharges, 100),
            Tracked("Operations", "industrial_network", "Industrial Network", "Produce 500 forged charges.", totalForgedCharges, 500),
            Tracked("Operations", "allied_veteran", "Allied Veteran", "Secure five online co-op campaigns.", coOpSecured, 5),
            Tracked("Operations", "allied_circuit", "Allied Circuit", "Secure all four arenas in online co-op.", coOpMaps, 4),

            Tracked("Honors", "medal_collector", "Medal Collector", "Discover seven distinct run-medal types.", medalTypesUnlocked, 7),
            Tracked("Honors", "full_honors", "Full Honors", "Discover every run-medal type.", medalTypesUnlocked, MedalCatalog.Length),
            Tracked("Honors", "decorated", "Decorated", "Earn 50 run medals across retained runs.", totalMedals, 50),
            Tracked("Honors", "precision_honors", "Precision Honors", "Discover Flawless, Lean Grid, Minimal Grid, and Specialist.", precisionHonors, 4),
            Tracked("Honors", "tactical_honors", "Tactical Honors", "Discover Early Command, Protocol Chain, Plate Ace, and Forge Fed.", tacticalHonors, 4),
            Tracked("Honors", "bastion_honors", "Bastion Honors", "Discover all six Bastion run medals.", bastionHonors, 6),
            Tracked("Honors", "endurance_honors", "Endurance Honors", "Discover Apex Command, Deep Endless, Endless 75, and Century Hold.", enduranceHonors, 4),
            Tracked("Honors", "total_command", "Total Command", "Complete the medal, campaign, Apex, design, endurance, and service records.", totalCommandRequirements, 6)
        };

        return new CareerProgress
        {
            Runs = runs,
            Medals = medals,
            Achievements = achievements,
            CampaignsSecured = secured.Length,
            TotalMedals = totalMedals,
            MedalTypesUnlocked = medalTypesUnlocked,
            AchievementsUnlocked = achievements.Count(achievement => achievement.IsUnlocked),
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
            "mastery" => secured && apexTowers >= 1,
            "deep_endless" => entry.CurrentWave >= 50,
            "iron_bastion" => secured && IsBastion(entry) && entry.Leaks == 0,
            "gauntlet_bastion" => secured && IsBastion(entry) && IsDirective(entry, "close_quarters"),
            "core_bastion" => secured && IsBastion(entry) && IsDirective(entry, "core_six"),
            "entrenched_bastion" => secured && IsBastion(entry) && IsDirective(entry, "no_reserves"),
            "tactical_triad" => secured && entry.ProtocolActivations >= 10 && entry.PlateKills >= 10 && entry.ForgedCharges >= 5,
            "bastion_mastery" => secured && IsBastion(entry) && apexTowers >= 1,
            "endless_75" => entry.CurrentWave >= 75,
            "century_hold" => entry.CurrentWave >= 100,
            _ => false
        };
    }

    private static bool IsBastion(RunHistoryEntry entry) =>
        entry.DifficultyId.Equals("bastion", StringComparison.OrdinalIgnoreCase);

    private static int DistinctRequiredMaps(IEnumerable<RunHistoryEntry> entries) =>
        RequiredMaps.Count(mapId => entries.Any(entry => entry.MapId.Equals(mapId, StringComparison.OrdinalIgnoreCase)));

    private static int SecuredMapsForDirective(IEnumerable<RunHistoryEntry> entries, string challengeId) =>
        DistinctRequiredMaps(entries.Where(entry => IsDirective(entry, challengeId)));

    private static bool IsDirective(RunHistoryEntry entry, string challengeId) =>
        entry.ChallengeId.Equals(challengeId, StringComparison.OrdinalIgnoreCase);

    private static int DistinctPairs(
        IEnumerable<RunHistoryEntry> entries,
        Func<RunHistoryEntry, string> first,
        Func<RunHistoryEntry, string> second,
        IReadOnlyCollection<string>? allowedFirst,
        IReadOnlyCollection<string>? allowedSecond)
    {
        return entries.Where(entry =>
                (allowedFirst is null || allowedFirst.Contains(first(entry), StringComparer.OrdinalIgnoreCase)) &&
                (allowedSecond is null || allowedSecond.Contains(second(entry), StringComparer.OrdinalIgnoreCase)))
            .Select(entry => $"{first(entry)}\n{second(entry)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static int MedalCount(IReadOnlyDictionary<string, int> counts, string medalId) =>
        counts.GetValueOrDefault(medalId);

    private static int MedalGroupCount(IReadOnlyDictionary<string, int> counts, params string[] medalIds) =>
        medalIds.Count(medalId => MedalCount(counts, medalId) > 0);

    private static CareerAchievement Tracked(string category, string id, string name, string description, long current, long target) =>
        new(id, category, name, description, current >= target, $"{Math.Min(current, target):N0}/{target:N0}");
}
