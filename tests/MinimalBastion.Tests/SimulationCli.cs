using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Persistence;
using MinimalBastion.Simulation;

namespace MinimalBastion.Tests;

internal static class SimulationCli
{
    public static int Run(GameContent content, string[] args, bool deep)
    {
        var selectedStrategy = ReadValue(args, "--strategy");
        var saveFile = ReadValue(args, "--save-file");
        var saveData = saveFile is null ? null : ReadSaveFile(saveFile, content);
        var strategyPlanFile = ReadValue(args, "--strategy-plan");
        var strategies = selectedStrategy is not null && Enum.TryParse<AutoPlayerStrategy>(selectedStrategy, true, out var parsed)
            ? new[] { parsed }
            : Enum.GetValues<AutoPlayerStrategy>();
        var baseSeed = int.TryParse(ReadValue(args, "--seed"), out var parsedSeed) ? parsedSeed : 1337;
        var runsPerStrategy = int.TryParse(ReadValue(args, "--runs"), out var parsedRuns)
            ? Math.Clamp(parsedRuns, 1, 100)
            : deep ? 5 : 1;
        var selectedMap = saveData?.MapId ?? ReadValue(args, "--map");
        var maps = selectedMap is not null && content.Maps.ContainsKey(selectedMap)
            ? new[] { selectedMap }
            : deep ? content.Maps.Keys.OrderBy(x => x).ToArray() : new[] { content.Map.Id };
        var maximumWave = ResolveMaximumWave(args, GameConstants.CampaignWaveCount);
        var difficulties = ResolveDifficulties(saveData?.DifficultyId ?? ReadValue(args, "--difficulty"), content);
        var challenges = ResolveChallenges(saveData?.ChallengeId ?? ReadValue(args, "--challenge"), content);
        var forcedBuilds = ResolveForcedBuilds(ReadValue(args, "--force-build"), content);
        var useProtocols = !args.Any(arg => arg.Equals("--no-protocols", StringComparison.OrdinalIgnoreCase));
        var useApexUpgrades = !args.Any(arg => arg.Equals("--no-apex", StringComparison.OrdinalIgnoreCase));
        var useCounterSupport = !args.Any(arg => arg.Equals("--no-counter-support", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--no-counter-pressure", StringComparison.OrdinalIgnoreCase));
        var useCounterAttackers = !args.Any(arg => arg.Equals("--no-counter-attackers", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--no-counter-pressure", StringComparison.OrdinalIgnoreCase));
        var holdBuild = args.Any(arg => arg.Equals("--hold-build", StringComparison.OrdinalIgnoreCase));
        var holdFootprint = args.Any(arg => arg.Equals("--hold-footprint", StringComparison.OrdinalIgnoreCase));
        var summaryOnly = args.Any(arg => arg.Equals("--summary-only", StringComparison.OrdinalIgnoreCase));

        if (strategyPlanFile is not null)
        {
            if (saveData is null)
                throw new ArgumentException("--strategy-plan requires an inter-wave --save-file checkpoint.");
            var strategyPlan = StrategyArtifactStore.LoadPlan(strategyPlanFile);
            strategyPlan.ValidateForCheckpoint(saveData);
            var wave = saveData.Waves.CurrentWaveNumber + 1;
            var wavePlan = strategyPlan.FindWave(wave) ??
                throw new InvalidDataException($"Strategy '{strategyPlan.ArtifactId}' has no decision for wave {wave}.");
            var forcedBuild = ResolveForcedBuilds(ReadValue(args, "--force-build"), content).SingleOrDefault();
            return RunPlannedWave(content, args, saveData, strategyPlan, wavePlan, new SimulationOptions
            {
                Strategy = strategyPlan.DefaultStrategy,
                MapId = saveData.MapId,
                DifficultyId = saveData.DifficultyId,
                ChallengeId = saveData.ChallengeId,
                ForcedTowerId = forcedBuild?.TowerId,
                ForcedDoctrineId = forcedBuild?.DoctrineId,
                ForcedSpecializationId = forcedBuild?.SpecializationId,
                UseProtocols = useProtocols,
                UseApexUpgrades = useApexUpgrades,
                UseCounterSupport = useCounterSupport,
                UseCounterAttackers = useCounterAttackers,
                HoldBuild = holdBuild,
                HoldFootprint = holdFootprint
            });
        }

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
                    var options = new SimulationOptions
                    {
                        Strategy = strategy,
                        Seed = seed,
                        MapId = mapId,
                        DifficultyId = difficultyId,
                        ChallengeId = challengeId,
                        MaximumWave = maximumWave,
                        ContinueEndless = maximumWave > GameConstants.CampaignWaveCount,
                        ForcedTowerId = forcedBuild?.TowerId,
                        ForcedDoctrineId = forcedBuild?.DoctrineId,
                        ForcedSpecializationId = forcedBuild?.SpecializationId,
                        UseProtocols = useProtocols,
                        UseApexUpgrades = useApexUpgrades,
                        UseCounterSupport = useCounterSupport,
                        UseCounterAttackers = useCounterAttackers,
                        HoldBuild = holdBuild,
                        HoldFootprint = holdFootprint
                    };
                    var result = saveData is null
                        ? HeadlessSimulation.Run(content, options)
                        : HeadlessSimulation.Run(content, saveData, options);
                    runs.Add(result);
                    if (!summaryOnly)
                        Console.WriteLine($"{mapId,-15} {difficultyId,-8} {challengeId,-14} {strategy,-16} seed {seed,7}  {result.Result,-7}  wave {result.WaveReached,2}  lives {result.LivesRemaining,2}  spent {result.CreditsSpent,5}  towers {result.Towers.Values.Sum(x => x.Purchases),2}  plates {result.EmergencyDeployments,2}");
                }
            }

        var batch = new SimulationBatchResult { Runs = runs };
        Console.WriteLine();
        var extendedAudit = maximumWave > GameConstants.CampaignWaveCount;
        var outcomeLabel = extendedAudit ? $"reach wave {maximumWave}" : "wins";
        Console.WriteLine($"Runs {runs.Count}, {(extendedAudit ? $"wave-{maximumWave} targets reached" : "wins")} {batch.Wins}, rate {batch.WinRate:P1}, average wave {batch.AverageWaveReached:0.0}, average lives {batch.AverageLivesRemaining:0.0}.");
        if (extendedAudit) PrintExtendedProgress(runs, maximumWave);
        if (forcedBuilds.Count == 1 && forcedBuilds[0] is { } onlyForcedBuild)
            Console.WriteLine($"Forced requested path: {onlyForcedBuild.TowerId}:{onlyForcedBuild.DoctrineId}>{onlyForcedBuild.SpecializationId}");
        if (!useProtocols) Console.WriteLine("Protocol activations disabled for this control group.");
        else if (runs.Count > 0 && runs.All(run => !run.ProtocolsEnabled))
            Console.WriteLine("The selected mode disables Protocol activations.");
        if (!useApexUpgrades) Console.WriteLine("Apex purchases disabled for this control group.");
        if (!useCounterSupport) Console.WriteLine("Enemy support carriers disabled for this control group.");
        if (!useCounterAttackers) Console.WriteLine("Enemy attacking signals disabled for this control group.");
        if (holdBuild) Console.WriteLine("Checkpoint defenses held without purchases, upgrades, sales, or tactical actions.");
        else if (holdFootprint) Console.WriteLine("Checkpoint footprint held while upgrades and tactical actions remain available.");
        if (saveFile is not null) Console.WriteLine($"Simulation started from checkpoint: {Path.GetFullPath(saveFile)}");
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
        PrintRemainingFieldSummary(runs);
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

    private static int RunPlannedWave(
        GameContent content,
        string[] args,
        SaveGameData checkpoint,
        StrategyPlan strategyPlan,
        WavePlan wavePlan,
        SimulationOptions options)
    {
        var result = HeadlessSimulation.RunWave(content, checkpoint, options, strategyPlan, wavePlan);
        var failure = result.Simulation.FailureMargin;
        Console.WriteLine(
            $"{strategyPlan.ArtifactId} wave {wavePlan.Wave} seed {wavePlan.DecisionSeed}: " +
            $"{result.Simulation.Result}, lives {result.Simulation.LivesRemaining}, credits {result.Simulation.CreditsUnspent}");
        if (failure is not null)
            Console.WriteLine(
                $"Remaining pressure: {failure.TotalEnemyCount} enemies, {failure.TotalArmorAdjustedDurability:0.##} " +
                $"armor-adjusted durability ({failure.RemainingArmorAdjustedDurabilityFraction:P2}), " +
                $"furthest progress {failure.FurthestProgress:P2}.");

        var root = FindProjectRoot();
        var output = ReadValue(args, "--output") ?? Path.Combine(root, ".build", "balance",
            $"planned-wave-{wavePlan.Wave}.json");
        if (!Path.IsPathRooted(output)) output = Path.Combine(root, output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        File.WriteAllText(output, JsonSerializer.Serialize(result, jsonOptions));
        Console.WriteLine($"Planned-wave report: {output}");

        var nextCheckpointPath = ReadValue(args, "--next-checkpoint");
        if (nextCheckpointPath is not null)
        {
            if (result.NextCheckpoint is null)
                throw new InvalidOperationException("The planned wave did not produce a resumable inter-wave checkpoint.");
            if (!Path.IsPathRooted(nextCheckpointPath)) nextCheckpointPath = Path.Combine(root, nextCheckpointPath);
            StrategyArtifactStore.SaveCheckpoint(nextCheckpointPath,
                StrategyCheckpointArtifact.Create(strategyPlan.ArtifactId, result.NextCheckpoint), content);
            Console.WriteLine($"Next strategy checkpoint: {nextCheckpointPath}");
        }
        return result.Succeeded ? 0 : 2;
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
            return content.Challenges.Values.Where(x => !x.IsSandbox).Select(x => x.Id).ToArray();

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

    private static void PrintExtendedProgress(IEnumerable<SimulationRunResult> runs, int maximumWave)
    {
        var materialized = runs.ToArray();
        var total = SummarizeEndlessProgress(materialized);
        Console.WriteLine();
        Console.WriteLine(maximumWave <= GameConstants.CampaignWaveCount
            ? "CAMPAIGN PROGRESSION"
            : "APEX ENDLESS PROGRESSION");
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
        Console.WriteLine("MODE SUMMARY");
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
        Console.WriteLine("ARENA x MODE (success rate / average wave)");
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

    private static void PrintRemainingFieldSummary(IEnumerable<SimulationRunResult> runs)
    {
        var defeats = runs.Where(run => run.Result == "Defeat").ToArray();
        if (defeats.Length == 0) return;

        Console.WriteLine();
        Console.WriteLine("FIELD REMAINING AT DEFEAT");
        foreach (var waveGroup in defeats.GroupBy(run => run.WaveReached).OrderBy(group => group.Key))
        {
            var waveRuns = waveGroup.ToArray();
            Console.WriteLine(
                $"Wave {waveGroup.Key,2}: {waveRuns.Length,3} defeats  " +
                $"avg live {waveRuns.Average(run => run.RemainingEnemyCount),5:0.0}  " +
                $"avg queued {waveRuns.Average(run => run.QueuedEnemiesRemaining),5:0.0}  " +
                $"avg remaining durability {waveRuns.Average(run => run.RemainingHealth + run.RemainingShield),9:0}");

            foreach (var enemyGroup in waveRuns.SelectMany(run => run.RemainingEnemies)
                         .GroupBy(enemy => new { enemy.DisplayName, enemy.Rank, enemy.SignalRole })
                         .OrderByDescending(group => group.Sum(enemy => enemy.CurrentHealth + enemy.Shield))
                         .Take(4))
            {
                var count = enemyGroup.Sum(enemy => enemy.Count);
                var health = enemyGroup.Sum(enemy => enemy.CurrentHealth);
                var maxHealth = enemyGroup.Sum(enemy => enemy.MaxHealth);
                var shield = enemyGroup.Sum(enemy => enemy.Shield);
                var signal = enemyGroup.Key.SignalRole.Equals("None", StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : $" {enemyGroup.Key.SignalRole}";
                var rank = enemyGroup.Key.Rank.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                           enemyGroup.Key.DisplayName.StartsWith(enemyGroup.Key.Rank, StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : $"{enemyGroup.Key.Rank} ";
                Console.WriteLine(
                    $"  {rank}{enemyGroup.Key.DisplayName}{signal}: " +
                    $"avg count {count / (float)waveRuns.Length:0.0}, HP {(maxHealth <= 0 ? 0 : health / maxHealth):P0}, " +
                    $"avg shield {shield / waveRuns.Length:0}");
            }
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
                ApexUpgrades = group.Sum(x => x.ApexUpgrades),
                ApexSpent = group.Sum(x => x.ApexCreditsSpent),
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
            Console.WriteLine($"{row.Id,-20} picks {row.Picks,3}  upgrades {row.Upgrades,3}  apex {row.ApexUpgrades,3}/{row.ApexSpent,-6}  protocols {row.Overdrives,4}  direct {row.Damage,10:0}  assist {row.Assist,8:0}  control {row.SlowSeconds + row.StunSeconds,7:0}s  expose {row.ExposeSeconds,7:0}s  break {row.BreakSeconds,7:0}s  supported {row.SupportedSeconds,8:0}s  impact/credit {(row.Spent == 0 ? 0 : (row.Damage + row.Assist) / row.Spent),6:0.0}");
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

    private static SaveGameData ReadSaveFile(string path, GameContent content)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Simulation checkpoint was not found.", fullPath);
        using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (document.RootElement.TryGetProperty("checkpoint", out _))
            return StrategyArtifactStore.LoadCheckpoint(fullPath, content).Checkpoint;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        return document.Deserialize<SaveGameData>(options) ??
            throw new InvalidDataException("Simulation checkpoint is empty or malformed.");
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
