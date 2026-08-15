using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalBastion.Data;
using MinimalBastion.Simulation;

namespace MinimalBastion.Tests;

internal static class SimulationCli
{
    public static int Run(GameContent content, string[] args, bool deep)
    {
        var selectedStrategy = ReadValue(args, "--strategy");
        var strategies = selectedStrategy is not null && Enum.TryParse<AutoPlayerStrategy>(selectedStrategy, true, out var parsed)
            ? new[] { parsed }
            : Enum.GetValues<AutoPlayerStrategy>();
        var baseSeed = int.TryParse(ReadValue(args, "--seed"), out var parsedSeed) ? parsedSeed : 1337;
        var runsPerStrategy = int.TryParse(ReadValue(args, "--runs"), out var parsedRuns)
            ? Math.Clamp(parsedRuns, 1, 100)
            : deep ? 5 : 1;
        var selectedMap = ReadValue(args, "--map");
        var maps = selectedMap is not null && content.Maps.ContainsKey(selectedMap)
            ? new[] { selectedMap }
            : deep ? content.Maps.Keys.OrderBy(x => x).ToArray() : new[] { content.Map.Id };
        var maximumWave = ResolveMaximumWave(args, content.Waves.Waves.Count);
        var difficulties = ResolveDifficulties(ReadValue(args, "--difficulty"), content);
        var challenges = ResolveChallenges(ReadValue(args, "--challenge"), content);
        var forcedBuilds = ResolveForcedBuilds(ReadValue(args, "--force-build"), content);
        var useProtocols = !args.Any(arg => arg.Equals("--no-protocols", StringComparison.OrdinalIgnoreCase));
        var summaryOnly = args.Any(arg => arg.Equals("--summary-only", StringComparison.OrdinalIgnoreCase));

        var runs = new List<SimulationRunResult>();
        foreach (var mapId in maps)
        foreach (var difficultyId in difficulties)
        foreach (var challengeId in challenges)
        foreach (var forcedBuild in forcedBuilds)
            foreach (var strategy in strategies)
            {
                for (var index = 0; index < runsPerStrategy; index++)
                {
                    var seed = baseSeed + index * 7919;
                    var result = HeadlessSimulation.Run(content, new SimulationOptions
                    {
                        Strategy = strategy,
                        Seed = seed,
                        MapId = mapId,
                        DifficultyId = difficultyId,
                        ChallengeId = challengeId,
                        MaximumWave = maximumWave,
                        ContinueEndless = maximumWave > content.Waves.Waves.Count,
                        ForcedTowerId = forcedBuild?.TowerId,
                        ForcedDoctrineId = forcedBuild?.DoctrineId,
                        ForcedSpecializationId = forcedBuild?.SpecializationId,
                        UseProtocols = useProtocols
                    });
                    runs.Add(result);
                    if (!summaryOnly)
                        Console.WriteLine($"{mapId,-15} {difficultyId,-8} {challengeId,-14} {strategy,-16} seed {seed,7}  {result.Result,-7}  wave {result.WaveReached,2}  lives {result.LivesRemaining,2}  spent {result.CreditsSpent,5}  towers {result.Towers.Values.Sum(x => x.Purchases),2}  plates {result.EmergencyDeployments,2}");
                }
            }

        var batch = new SimulationBatchResult { Runs = runs };
        Console.WriteLine();
        var endlessAudit = maximumWave > content.Waves.Waves.Count;
        var outcomeLabel = endlessAudit ? $"reach wave {maximumWave}" : "wins";
        Console.WriteLine($"Runs {runs.Count}, {(endlessAudit ? $"wave-{maximumWave} targets reached" : "wins")} {batch.Wins}, rate {batch.WinRate:P1}, average wave {batch.AverageWaveReached:0.0}, average lives {batch.AverageLivesRemaining:0.0}.");
        if (endlessAudit) PrintEndlessProgress(runs);
        if (forcedBuilds.Count == 1 && forcedBuilds[0] is { } onlyForcedBuild)
            Console.WriteLine($"Forced requested path: {onlyForcedBuild.TowerId}:{onlyForcedBuild.DoctrineId}>{onlyForcedBuild.SpecializationId}");
        if (!useProtocols) Console.WriteLine("Protocol activations disabled for this control group.");
        PrintStrategySummary(runs, outcomeLabel);
        PrintDifficultySummary(runs, outcomeLabel);
        PrintChallengeSummary(runs, outcomeLabel);
        PrintMapSummary(runs, outcomeLabel);
        PrintArenaDifficultyMatrix(runs, content);
        PrintArenaChallengeMatrix(runs, content);
        PrintForcedBuildSummary(runs, outcomeLabel);
        PrintForcedBuildArenaMatrix(runs);
        PrintTowerSummary(runs);
        PrintDoctrineSummary(runs);
        PrintSpecializationSummary(runs);
        PrintBuildPathSummary(runs);
        Console.WriteLine($"Early calls earned {runs.Sum(x => x.EarlyStartCreditsEarned)} credits; overdrives {runs.Sum(x => x.Overdrives)}. Emergency defenses: {runs.Sum(x => x.EmergencyDeployments)} deployed, {runs.Sum(x => x.EmergencyTriggers)} triggers, {runs.Sum(x => x.EmergencyKills)} kills, {runs.Sum(x => x.EmergencyDamage):0} damage; generators {runs.Sum(x => x.GeneratorPurchases)}.");

        var root = FindProjectRoot();
        var output = ReadValue(args, "--output") ?? Path.Combine(root, ".build", "balance", deep ? "full-latest.json" : "latest.json");
        if (!Path.IsPathRooted(output)) output = Path.Combine(root, output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(output, JsonSerializer.Serialize(batch, jsonOptions));
        Console.WriteLine($"Machine-readable report: {output}");
        return 0;
    }

    internal static int ResolveMaximumWave(string[] args, int campaignWaveCount)
    {
        return int.TryParse(ReadValue(args, "--max-wave"), out var parsedMaximumWave)
            ? Math.Max(1, parsedMaximumWave)
            : Math.Max(1, campaignWaveCount);
    }

    internal static IReadOnlyList<string> ResolveDifficulties(string? selectedDifficulty, GameContent content)
    {
        if (selectedDifficulty?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
            return content.Difficulties.Values.Select(x => x.Id).ToArray();

        var difficultyId = selectedDifficulty ?? DifficultyCatalog.LegacyId;
        if (!content.Difficulties.ContainsKey(difficultyId))
            throw new ArgumentException($"Unknown difficulty '{difficultyId}'. Choose one of: all, {string.Join(", ", content.Difficulties.Keys.OrderBy(x => x))}.");
        return new[] { content.Difficulties[difficultyId].Id };
    }

    internal static IReadOnlyList<string> ResolveChallenges(string? selectedChallenge, GameContent content)
    {
        if (selectedChallenge?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
            return content.Challenges.Values.Select(x => x.Id).ToArray();

        var challengeId = selectedChallenge ?? ChallengeCatalog.DefaultId;
        if (!content.Challenges.ContainsKey(challengeId))
            throw new ArgumentException($"Unknown challenge '{challengeId}'. Choose one of: all, {string.Join(", ", content.Challenges.Keys.OrderBy(x => x))}.");
        return new[] { content.Challenges[challengeId].Id };
    }

    internal static ForcedBuildPath? ParseForcedBuild(string? value, GameContent content)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var colon = value.IndexOf(':');
        var arrow = value.IndexOf('>');
        if (colon <= 0 || arrow <= colon + 1 || arrow >= value.Length - 1)
            throw new ArgumentException("Forced build must use tower:doctrine>specialization.");
        var towerId = value[..colon];
        var doctrineId = value[(colon + 1)..arrow];
        var specializationId = value[(arrow + 1)..];
        if (!content.Towers.TryGetValue(towerId, out var tower) ||
            !tower.Tier2Doctrines.Any(doctrine => doctrine.Id.Equals(doctrineId, StringComparison.OrdinalIgnoreCase)) ||
            !tower.Specializations.Any(specialization => specialization.Id.Equals(specializationId, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Unknown forced build '{value}'.");
        return new ForcedBuildPath(tower.Id, doctrineId, specializationId);
    }

    internal static IReadOnlyList<ForcedBuildPath?> ResolveForcedBuilds(string? value, GameContent content)
    {
        if (string.IsNullOrWhiteSpace(value)) return new ForcedBuildPath?[] { null };
        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
            return content.Towers.Values.SelectMany(BuildPathsForTower).Cast<ForcedBuildPath?>().ToArray();

        const string allSuffix = ":all";
        if (value.EndsWith(allSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var towerId = value[..^allSuffix.Length];
            if (!content.Towers.TryGetValue(towerId, out var tower))
                throw new ArgumentException($"Unknown tower '{towerId}' for forced build sweep.");
            return BuildPathsForTower(tower).Cast<ForcedBuildPath?>().ToArray();
        }

        return new ForcedBuildPath?[] { ParseForcedBuild(value, content) };
    }

    private static IEnumerable<ForcedBuildPath> BuildPathsForTower(TowerDefinition tower) =>
        tower.Tier2Doctrines.SelectMany(doctrine => tower.Specializations.Select(specialization =>
            new ForcedBuildPath(tower.Id, doctrine.Id, specialization.Id)));

    internal sealed record ForcedBuildPath(string TowerId, string DoctrineId, string SpecializationId);

    internal static EndlessProgressSummary SummarizeEndlessProgress(IEnumerable<SimulationRunResult> runs)
    {
        var materialized = runs.ToArray();
        var campaignClears = materialized.Where(run => run.CampaignCleared).ToArray();
        return new EndlessProgressSummary(
            materialized.Length,
            campaignClears.Length,
            materialized.Count(run => run.Result == "WaveLimit"),
            materialized.Length == 0 ? 0 : materialized.Max(run => run.WaveReached),
            campaignClears.Length == 0 ? 0 : (float)campaignClears.Average(run => run.EndlessDepth));
    }

    private static void PrintEndlessProgress(IEnumerable<SimulationRunResult> runs)
    {
        var materialized = runs.ToArray();
        var total = SummarizeEndlessProgress(materialized);
        Console.WriteLine();
        Console.WriteLine("ENDLESS PROGRESSION");
        Console.WriteLine($"Campaign clears {total.CampaignClears}/{total.Runs}; target reaches {total.TargetReaches}; deepest wave {total.DeepestWave}; average depth after a clear {total.AverageEndlessDepth:0.0}.");
        Console.WriteLine("BY ARENA (campaign clears / deepest wave / average clear depth)");
        foreach (var group in materialized.GroupBy(run => run.MapId).OrderBy(group => group.Key))
        {
            var row = SummarizeEndlessProgress(group);
            Console.WriteLine($"{group.Key,-18} {row.CampaignClears,2}/{row.Runs,-2} clears  deepest {row.DeepestWave,2}  depth {row.AverageEndlessDepth,4:0.0}");
        }
    }

    internal sealed record EndlessProgressSummary(
        int Runs,
        int CampaignClears,
        int TargetReaches,
        int DeepestWave,
        float AverageEndlessDepth);

    private static void PrintMapSummary(IEnumerable<SimulationRunResult> runs, string outcomeLabel)
    {
        Console.WriteLine();
        Console.WriteLine("MAP SUMMARY");
        foreach (var group in runs.GroupBy(x => x.MapId).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-18} {group.Count(x => x.Won),2}/{group.Count(),-2} {outcomeLabel}  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintStrategySummary(IEnumerable<SimulationRunResult> runs, string outcomeLabel)
    {
        Console.WriteLine();
        Console.WriteLine("STRATEGY SUMMARY");
        foreach (var group in runs.GroupBy(x => x.Strategy).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-16} {group.Count(x => x.Won),2}/{group.Count(),-2} {outcomeLabel}  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintDifficultySummary(IEnumerable<SimulationRunResult> runs, string outcomeLabel)
    {
        Console.WriteLine();
        Console.WriteLine("DIFFICULTY SUMMARY");
        foreach (var group in runs.GroupBy(x => x.DifficultyId).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-18} {group.Count(x => x.Won),2}/{group.Count(),-2} {outcomeLabel}  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintChallengeSummary(IEnumerable<SimulationRunResult> runs, string outcomeLabel)
    {
        var materialized = runs.ToArray();
        if (materialized.Select(x => x.ChallengeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2) return;
        Console.WriteLine();
        Console.WriteLine("CHALLENGE SUMMARY");
        foreach (var group in materialized.GroupBy(x => x.ChallengeId).OrderBy(x => x.Key))
            Console.WriteLine($"{group.Key,-18} {group.Count(x => x.Won),2}/{group.Count(),-2} {outcomeLabel}  avg wave {group.Average(x => x.WaveReached),4:0.0}  avg lives {group.Average(x => x.LivesRemaining),4:0.0}");
    }

    private static void PrintArenaDifficultyMatrix(IEnumerable<SimulationRunResult> runs, GameContent content)
    {
        var materialized = runs.ToArray();
        var difficultyIds = content.Difficulties.Values.Select(x => x.Id)
            .Where(id => materialized.Any(run => run.DifficultyId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (difficultyIds.Length < 2) return;

        Console.WriteLine();
        Console.WriteLine("ARENA x DIFFICULTY (success rate / average wave)");
        Console.Write($"{"Arena",-18}");
        foreach (var difficultyId in difficultyIds) Console.Write($"  {difficultyId,-15}");
        Console.WriteLine();
        foreach (var mapGroup in materialized.GroupBy(x => x.MapId).OrderBy(x => x.Key))
        {
            Console.Write($"{mapGroup.Key,-18}");
            foreach (var difficultyId in difficultyIds)
            {
                var cell = mapGroup.Where(run => run.DifficultyId.Equals(difficultyId, StringComparison.OrdinalIgnoreCase)).ToArray();
                var value = cell.Length == 0 ? "-" : $"{cell.Count(run => run.Won) / (float)cell.Length:P0} / {cell.Average(run => run.WaveReached):0.0}";
                Console.Write($"  {value,-15}");
            }
            Console.WriteLine();
        }
    }

    private static void PrintArenaChallengeMatrix(IEnumerable<SimulationRunResult> runs, GameContent content)
    {
        var materialized = runs.ToArray();
        var challengeIds = content.Challenges.Values.Select(x => x.Id)
            .Where(id => materialized.Any(run => run.ChallengeId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (challengeIds.Length < 2) return;

        Console.WriteLine();
        Console.WriteLine("ARENA x DIRECTIVE (success rate / average wave)");
        Console.Write($"{"Arena",-18}");
        foreach (var challengeId in challengeIds) Console.Write($"  {challengeId,-15}");
        Console.WriteLine();
        foreach (var mapGroup in materialized.GroupBy(x => x.MapId).OrderBy(x => x.Key))
        {
            Console.Write($"{mapGroup.Key,-18}");
            foreach (var challengeId in challengeIds)
            {
                var cell = mapGroup.Where(run => run.ChallengeId.Equals(challengeId, StringComparison.OrdinalIgnoreCase)).ToArray();
                var value = cell.Length == 0 ? "-" : $"{cell.Count(run => run.Won) / (float)cell.Length:P0} / {cell.Average(run => run.WaveReached):0.0}";
                Console.Write($"  {value,-15}");
            }
            Console.WriteLine();
        }
    }

    internal static IReadOnlyList<ForcedBuildSummary> SummarizeForcedBuilds(IEnumerable<SimulationRunResult> runs)
    {
        var materialized = runs.Where(run => run.ForcedBuildPath is not null).ToArray();
        if (materialized.Length == 0) return Array.Empty<ForcedBuildSummary>();

        return materialized.GroupBy(run => run.ForcedBuildPath!).OrderBy(group => group.Key).Select(group =>
        {
            var groupedRuns = group.ToArray();
            var towerId = groupedRuns[0].ForcedTowerId!;
            var entries = groupedRuns.Select(run =>
            {
                run.Towers.TryGetValue(towerId, out var metrics);
                return new
                {
                    Run = run,
                    Metrics = metrics,
                    CompletedTowers = ForcedPathCompletionCount(run)
                };
            }).ToArray();
            var completedEntries = entries.Where(entry => entry.CompletedTowers > 0).ToArray();
            var completedSpent = completedEntries.Sum(entry => entry.Metrics!.CreditsSpent);
            var completedImpact = completedEntries.Sum(entry => entry.Metrics!.ContributionDamage);
            return new ForcedBuildSummary(
                group.Key,
                groupedRuns.Length,
                groupedRuns.Count(run => run.Won),
                (float)groupedRuns.Average(run => run.WaveReached),
                (float)groupedRuns.Average(run => run.LivesRemaining),
                completedEntries.Length,
                completedEntries.Count(entry => entry.Run.Won),
                completedEntries.Sum(entry => entry.CompletedTowers),
                completedSpent == 0 ? 0 : completedImpact / completedSpent);
        }).ToArray();
    }

    private static void PrintForcedBuildSummary(IEnumerable<SimulationRunResult> runs, string outcomeLabel)
    {
        var rows = SummarizeForcedBuilds(runs);
        if (rows.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine("FORCED BUILD PATHS");
        foreach (var row in rows)
            Console.WriteLine($"{row.Path,-58} {row.Wins,2}/{row.Runs,-2} {outcomeLabel}  avg wave {row.AverageWave,4:0.0}  lives {row.AverageLives,4:0.0}  complete {row.CompletedRuns,2}/{row.Runs,-2} runs ({row.CompletedWins,2} successful, {row.CompletedTowers,3} towers)  complete impact/credit {row.CompletedImpactPerCredit,6:0.0}");
    }

    internal static IReadOnlyList<ForcedBuildArenaSummary> SummarizeForcedBuildsByArena(IEnumerable<SimulationRunResult> runs) =>
        runs.Where(run => run.ForcedBuildPath is not null)
            .GroupBy(run => (Path: run.ForcedBuildPath!, run.MapId))
            .OrderBy(group => group.Key.Path)
            .ThenBy(group => group.Key.MapId)
            .Select(group =>
            {
                var groupedRuns = group.ToArray();
                return new ForcedBuildArenaSummary(
                    group.Key.Path,
                    group.Key.MapId,
                    groupedRuns.Length,
                    groupedRuns.Count(run => run.Won),
                    groupedRuns.Count(run => ForcedPathCompletionCount(run) > 0),
                    (float)groupedRuns.Average(run => run.WaveReached));
            }).ToArray();

    private static void PrintForcedBuildArenaMatrix(IEnumerable<SimulationRunResult> runs)
    {
        var rows = SummarizeForcedBuildsByArena(runs);
        var mapIds = rows.Select(row => row.MapId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id).ToArray();
        if (mapIds.Length < 2) return;

        Console.WriteLine();
        Console.WriteLine("FORCED PATH x ARENA (success rate / completion rate / average wave)");
        Console.Write($"{"Path",-58}");
        foreach (var mapId in mapIds) Console.Write($"  {mapId,-22}");
        Console.WriteLine();
        foreach (var pathGroup in rows.GroupBy(row => row.Path).OrderBy(group => group.Key))
        {
            Console.Write($"{pathGroup.Key,-58}");
            foreach (var mapId in mapIds)
            {
                var cell = pathGroup.FirstOrDefault(row => row.MapId.Equals(mapId, StringComparison.OrdinalIgnoreCase));
                var value = cell is null
                    ? "-"
                    : $"{cell.Wins / (float)cell.Runs:P0} / {cell.CompletedRuns / (float)cell.Runs:P0} / {cell.AverageWave:0.0}";
                Console.Write($"  {value,-22}");
            }
            Console.WriteLine();
        }
    }

    private static int ForcedPathCompletionCount(SimulationRunResult run)
    {
        if (run.ForcedBuildPath is null || run.ForcedTowerId is null ||
            !run.Towers.TryGetValue(run.ForcedTowerId, out var metrics)) return 0;
        var branchPath = run.ForcedBuildPath[(run.ForcedTowerId.Length + 1)..];
        return metrics.BuildPaths.GetValueOrDefault(branchPath);
    }

    internal sealed record ForcedBuildSummary(
        string Path,
        int Runs,
        int Wins,
        float AverageWave,
        float AverageLives,
        int CompletedRuns,
        int CompletedWins,
        int CompletedTowers,
        float CompletedImpactPerCredit);

    internal sealed record ForcedBuildArenaSummary(
        string Path,
        string MapId,
        int Runs,
        int Wins,
        int CompletedRuns,
        float AverageWave);

    private static void PrintTowerSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("TOWER USAGE");
        var towerRows = runs
            .SelectMany(run => run.Towers.Values)
            .GroupBy(x => x.TowerId)
            .Select(group => new
            {
                Id = group.Key,
                Picks = group.Sum(x => x.Purchases),
                Upgrades = group.Sum(x => x.Upgrades),
                Damage = group.Sum(x => x.Damage),
                Assist = group.Sum(x => x.SupportDamageEquivalent + x.ExposeDamageEquivalent + x.ArmorBreakDamageEquivalent),
                SlowSeconds = group.Sum(x => x.StatusEnemySeconds.GetValueOrDefault("Slow")),
                StunSeconds = group.Sum(x => x.StatusEnemySeconds.GetValueOrDefault("Stun")),
                ExposeSeconds = group.Sum(x => x.StatusEnemySeconds.GetValueOrDefault("Exposed")),
                BreakSeconds = group.Sum(x => x.StatusEnemySeconds.GetValueOrDefault("ArmorBreak")),
                SupportedSeconds = group.Sum(x => x.SupportedAttackSeconds),
                Overdrives = group.Sum(x => x.Overdrives),
                Spent = group.Sum(x => x.CreditsSpent)
            })
            .OrderByDescending(x => x.Damage + x.Assist);
        foreach (var row in towerRows)
            Console.WriteLine($"{row.Id,-20} picks {row.Picks,3}  upgrades {row.Upgrades,3}  protocols {row.Overdrives,4}  direct {row.Damage,10:0}  assist {row.Assist,8:0}  control {row.SlowSeconds + row.StunSeconds,7:0}s  expose {row.ExposeSeconds,7:0}s  break {row.BreakSeconds,7:0}s  supported {row.SupportedSeconds,8:0}s  impact/credit {(row.Spent == 0 ? 0 : (row.Damage + row.Assist) / row.Spent),6:0.0}");
    }

    private static void PrintSpecializationSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("FINAL SPECIALIZATIONS");
        var rows = runs
            .SelectMany(run => run.Towers.Values.SelectMany(tower => tower.Specializations.Select(choice => new
            {
                Tower = tower.TowerId,
                Choice = choice.Key,
                Picks = choice.Value,
                WinningPicks = run.Won ? choice.Value : 0
            })))
            .GroupBy(row => (row.Tower, row.Choice))
            .Select(group => new
            {
                group.Key.Tower,
                group.Key.Choice,
                Picks = group.Sum(row => row.Picks),
                WinningPicks = group.Sum(row => row.WinningPicks)
            })
            .OrderBy(row => row.Tower)
            .ThenBy(row => row.Choice);
        foreach (var row in rows)
            Console.WriteLine($"{row.Tower,-18} {row.Choice,-20} picks {row.Picks,4}  in successful runs {row.WinningPicks,4}");
    }

    private static void PrintDoctrineSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("TIER 2 DOCTRINES");
        var rows = runs
            .SelectMany(run => run.Towers.Values.SelectMany(tower => tower.Doctrines.Select(choice => new
            {
                Tower = tower.TowerId,
                Choice = choice.Key,
                Picks = choice.Value,
                WinningPicks = run.Won ? choice.Value : 0
            })))
            .GroupBy(row => (row.Tower, row.Choice))
            .Select(group => new
            {
                group.Key.Tower,
                group.Key.Choice,
                Picks = group.Sum(row => row.Picks),
                WinningPicks = group.Sum(row => row.WinningPicks)
            })
            .OrderBy(row => row.Tower)
            .ThenBy(row => row.Choice);
        foreach (var row in rows)
            Console.WriteLine($"{row.Tower,-18} {row.Choice,-20} picks {row.Picks,4}  in successful runs {row.WinningPicks,4}");
    }

    private static void PrintBuildPathSummary(IEnumerable<SimulationRunResult> runs)
    {
        Console.WriteLine();
        Console.WriteLine("COMPLETED BUILD PATHS");
        var rows = runs
            .SelectMany(run => run.Towers.Values.SelectMany(tower => tower.BuildPaths.Select(choice => new
            {
                Tower = tower.TowerId,
                Path = choice.Key,
                Picks = choice.Value,
                WinningPicks = run.Won ? choice.Value : 0
            })))
            .GroupBy(row => (row.Tower, row.Path))
            .Select(group => new
            {
                group.Key.Tower,
                group.Key.Path,
                Picks = group.Sum(row => row.Picks),
                WinningPicks = group.Sum(row => row.WinningPicks)
            })
            .OrderBy(row => row.Tower)
            .ThenBy(row => row.Path);
        foreach (var row in rows)
            Console.WriteLine($"{row.Tower,-18} {row.Path,-42} picks {row.Picks,4}  in successful runs {row.WinningPicks,4}");
    }

    private static string? ReadValue(string[] args, string name)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return args[index][(name.Length + 1)..];
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length) return args[index + 1];
        }
        return null;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MinimalBastion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
