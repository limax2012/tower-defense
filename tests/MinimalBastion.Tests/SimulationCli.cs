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
        if (args.Any(arg => arg.Equals("--replay-manifest", StringComparison.OrdinalIgnoreCase) ||
                            arg.StartsWith("--replay-manifest=", StringComparison.OrdinalIgnoreCase)))
            return RunStrategyReplay(content, args);
        if (args.Any(arg => arg.Equals("--optimize-strategy", StringComparison.OrdinalIgnoreCase)))
            return RunStrategyOptimization(content, args);

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
                $"Unresolved pressure: {failure.UnresolvedEnemyCount} enemies, " +
                $"{failure.UnresolvedArmorAdjustedDurability:0.##} armor-adjusted durability " +
                $"({failure.UnresolvedArmorAdjustedDurabilityFraction:P2}), " +
                $"furthest progress {failure.UnresolvedFurthestProgress:P2}.");

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
                StrategyCheckpointArtifact.Create(strategyPlan.ArtifactId, result.NextCheckpoint, content), content);
            Console.WriteLine($"Next strategy checkpoint: {nextCheckpointPath}");
        }
        return result.Succeeded ? 0 : 2;
    }

    private static int RunStrategyReplay(GameContent content, string[] args)
    {
        ValidateReplayArguments(args);
        var manifestValue = ReadValue(args, "--replay-manifest") ??
                            throw new ArgumentException("--replay-manifest requires a campaign-search.json path.");
        var manifestPath = Path.GetFullPath(manifestValue);
        var manifest = CampaignSearchArtifactStore.LoadManifest(manifestPath);
        if (manifest.SchemaVersion != CampaignSearchManifest.CurrentSchemaVersion ||
            manifest.SchemaVersion != 5)
            throw new InvalidDataException("Fresh strategy replay requires a schema-v5 campaign manifest.");
        if (manifest.Status != CampaignSearchStatus.CampaignCompleted)
            throw new InvalidDataException("Fresh strategy replay requires a completed campaign search manifest.");

        var plan = CampaignSearchArtifactStore.LoadFinalStrategy(content, manifestPath);
        var envelope = StrategyReplayEnvelope.Create(
            plan,
            manifest.FinalStrategyFingerprint ??
            throw new InvalidDataException("Completed campaign manifest is missing its final strategy fingerprint."),
            manifest.BuildFingerprint ??
            throw new InvalidDataException("Completed campaign manifest is missing its build fingerprint."),
            manifest.SimulationSettings);
        var result = HeadlessSimulation.ReplayStrategy(content, envelope);

        Console.WriteLine(
            $"Fresh replay {result.StrategyArtifactId}: {result.Result}, " +
            $"completed {result.CompletedWaveCount}/{plan.Waves.Count} waves, " +
            $"lives {result.FinalSimulation?.LivesRemaining ?? 0}.");
        Console.WriteLine(
            $"Provenance: plan {result.StrategyFingerprint}, build {result.ContentBuildFingerprint}, " +
            $"replay {result.ReplayFingerprint}.");
        foreach (var wave in result.WaveRuns)
        {
            var delta = wave.Deltas;
            Console.WriteLine(
                $"Wave {wave.WavePlan.Wave,2} seed {wave.WavePlan.DecisionSeed,10}: " +
                $"{wave.Simulation.Result,-9} credits {delta.StartingCredits} +{delta.CreditsEarned} earned " +
                $"+{delta.SaleCreditsRecovered} sales -{delta.CreditsSpent} spent = {delta.EndingCredits}; " +
                $"kills {delta.Kills}, leaks {delta.Leaks}, early {delta.EarlyStartCreditsEarned}.");
            Console.WriteLine(
                $"  towers +{delta.TowerPurchases}/up {delta.TowerUpgrades}/apex {delta.ApexUpgrades}/" +
                $"sold {delta.TowerSales}; plates {delta.PulsePlateDeployments} " +
                $"({delta.EmergencyDirectPurchases} direct, {delta.EmergencyTriggers} triggers, " +
                $"{delta.EmergencyDamage:0.###} damage); protocols {delta.ProtocolActivations}, " +
                $"generators +{delta.GeneratorPurchases}/up {delta.GeneratorUpgrades}/charges {delta.GeneratedCharges}.");
        }
        PrintExactFailure(result.WaveRuns.LastOrDefault(wave => !wave.Succeeded));

        var outputValue = ReadValue(args, "--output");
        var output = outputValue is null
            ? Path.Combine(Path.GetDirectoryName(manifestPath)!, "campaign-replay.json")
            : Path.GetFullPath(outputValue);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        File.WriteAllText(output, JsonSerializer.Serialize(result, jsonOptions));
        Console.WriteLine($"Fresh replay report: {output}");
        return result.CampaignCleared ? 0 : 2;
    }

    private static void ValidateReplayArguments(string[] args)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            var separator = argument.IndexOf('=');
            var name = separator < 0 ? argument : argument[..separator];
            if (!name.Equals("--replay-manifest", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("--output", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"'{name}' cannot be combined with --replay-manifest; replay uses the recorded execution settings.");
            if (!seen.Add(name))
                throw new ArgumentException($"Replay option '{name}' can only be supplied once.");
            if (separator >= 0)
            {
                if (separator == argument.Length - 1)
                    throw new ArgumentException($"Replay option '{name}' requires a value.");
                continue;
            }
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Replay option '{name}' requires a value.");
            index++;
        }
    }

    private static int RunStrategyOptimization(GameContent content, string[] args)
    {
        var root = FindProjectRoot();
        var resumeManifestPath = ReadValue(args, "--resume-manifest");
        var resumeSearchPath = ReadValue(args, "--resume-search");
        var resumeCheckpointPath = ReadValue(args, "--resume-checkpoint");
        var resumePlanPath = ReadValue(args, "--resume-plan");
        var resumeModes = new[] { resumeManifestPath, resumeSearchPath, resumeCheckpointPath }.Count(path => path is not null);
        if (resumeModes > 1)
            throw new ArgumentException("Choose only one of --resume-manifest, --resume-search, or --resume-checkpoint.");
        if ((resumeCheckpointPath is null) != (resumePlanPath is null))
            throw new ArgumentException("--resume-checkpoint and --resume-plan must be supplied together.");

        CampaignSearchManifest? resumeManifest = null;
        CampaignSearchResumeState? resumeState = null;
        StrategyPlan? preferredPlan = null;
        IReadOnlyList<CheckpointSearchState> frontier;
        if (resumeManifestPath is not null)
        {
            resumeManifestPath = Path.GetFullPath(resumeManifestPath);
            resumeManifest = CampaignSearchArtifactStore.LoadManifest(resumeManifestPath);
            if (resumeManifest.Status == CampaignSearchStatus.CampaignCompleted)
            {
                var completedPlan = CampaignSearchArtifactStore.LoadFinalStrategy(content, resumeManifestPath);
                ValidateResumeSelectors(content, args, resumeManifest.MapId, resumeManifest.DifficultyId,
                    resumeManifest.ChallengeId, resumeManifest.BaseSeed, completedPlan.DefaultStrategy);
                Console.WriteLine($"Campaign search '{resumeManifest.ArtifactId}' is already complete.");
                return 0;
            }
            resumeState = CampaignSearchArtifactStore.LoadResumeState(content, resumeManifestPath);
            frontier = resumeState.Frontier;
            if (frontier.Count == 0)
                throw new InvalidDataException("Resume manifest does not contain a usable frontier.");
            preferredPlan = frontier[0].Strategy;
        }
        else if (resumeSearchPath is not null)
        {
            var trace = StrategyArtifactStore.LoadSearchResult(Path.GetFullPath(resumeSearchPath), content);
            frontier = trace.RetainedStates;
            if (frontier.Count == 0)
                throw new InvalidDataException("Resume search trace has no retained states; use its campaign manifest retry frontier.");
            preferredPlan = frontier[0].Strategy;
        }
        else if (resumeCheckpointPath is not null && resumePlanPath is not null)
        {
            var checkpoint = ReadSaveFile(resumeCheckpointPath, content);
            preferredPlan = StrategyArtifactStore.LoadPlan(resumePlanPath);
            preferredPlan.ValidateForCheckpoint(checkpoint);
            var prefix = preferredPlan with
            {
                Waves = preferredPlan.Waves.Where(wave => wave.Wave <= checkpoint.Waves.CurrentWaveNumber).ToArray()
            };
            frontier = [CheckpointSearchState.Create(content, prefix, checkpoint)];
        }
        else
        {
            var mapId = ReadValue(args, "--map") ?? content.Map.Id;
            if (!content.Maps.ContainsKey(mapId))
                throw new ArgumentException($"Unknown map '{mapId}'.");
            var difficultyId = ResolveDifficulties(ReadValue(args, "--difficulty"), content).Single();
            var challengeId = ResolveChallenges(ReadValue(args, "--challenge"), content).Single();
            var baseSeed = ParseInt(args, "--seed", 1337, int.MinValue, int.MaxValue);
            var strategy = ParseStrategy(ReadValue(args, "--strategy"), AutoPlayerStrategy.Experienced);
            var checkpoint = new GameSession(content, mapId, difficultyId, challengeId).CaptureSaveGame();
            preferredPlan = new StrategyPlan
            {
                ArtifactId = $"{mapId}-{difficultyId}-{challengeId}-seed-{baseSeed}",
                MapId = mapId,
                DifficultyId = difficultyId,
                ChallengeId = challengeId,
                BaseSeed = baseSeed,
                DefaultStrategy = strategy
            };
            frontier = [CheckpointSearchState.Create(content, preferredPlan, checkpoint)];
        }

        if (resumeModes > 0)
        {
            var context = frontier[0].Strategy;
            ValidateResumeSelectors(
                content,
                args,
                resumeManifest?.MapId ?? context.MapId,
                resumeManifest?.DifficultyId ?? context.DifficultyId,
                resumeManifest?.ChallengeId ?? context.ChallengeId,
                resumeManifest?.BaseSeed ?? context.BaseSeed,
                resumeManifest?.DefaultStrategy ?? context.DefaultStrategy);
        }

        var strategySeed = frontier[0].Strategy.BaseSeed;
        var baseSeedValue = strategySeed;
        var maximumWaveDefault = resumeManifest?.MaximumWave ?? GameConstants.CampaignWaveCount;
        var maximumWave = ParseInt(args, "--max-wave", maximumWaveDefault, 1, int.MaxValue);
        var beamWidth = ParseInt(args, "--beam-width", resumeManifest?.BeamWidth ?? 3, 1, 32);
        var candidateCount = ParseInt(args, "--candidates", resumeManifest?.CandidateCount ?? 6, 1, 128);
        var broadeningRounds = ParseInt(args, "--broaden-rounds", resumeManifest?.BroadeningRounds ?? 1, 0, 8);
        var startingRound = ParseInt(args, "--start-round", resumeManifest?.NextBroadeningRound ?? 0, 0, 1000);
        var backtrackDepth = ParseInt(args, "--backtrack-depth", resumeManifest?.BacktrackDepth ?? 2,
            0, GameConstants.CampaignWaveCount);
        var recoveryAttempts = ParseInt(args, "--recovery-attempts",
            resumeManifest?.MaximumRecoveryAttempts ?? 8, 0, 1000);
        var bundleText = ReadValue(args, "--bundles");
        var bundleIds = bundleText is null ? resumeManifest?.BundleIds ?? Array.Empty<string>() : SplitList(bundleText);
        var parameterOverrides = HasParameterArguments(args)
            ? ReadParameterOverrides(args)
            : resumeManifest?.ParameterOverrides ?? new SortedDictionary<string, double>(StringComparer.Ordinal);
        var artifactDirectory = ReadValue(args, "--artifact-dir");
        if (artifactDirectory is null)
        {
            artifactDirectory = resumeManifestPath is null
                ? Path.Combine(root, ".build", "balance", "strategy-search", frontier[0].Strategy.ArtifactId)
                : Path.Combine(Path.GetDirectoryName(resumeManifestPath)!, $"resume-{startingRound:D3}");
        }
        else if (!Path.IsPathRooted(artifactDirectory))
            artifactDirectory = Path.Combine(root, artifactDirectory);

        var forcedBuildText = ReadValue(args, "--force-build");
        var forcedBuild = ParseForcedBuild(forcedBuildText, content);
        var storedSimulation = resumeManifest?.SimulationSettings;
        var forcedTowerId = forcedBuild?.TowerId ?? (forcedBuildText is null ? storedSimulation?.ForcedTowerId : null);
        var forcedDoctrineId = forcedBuild?.DoctrineId ?? (forcedBuildText is null ? storedSimulation?.ForcedDoctrineId : null);
        var forcedSpecializationId = forcedBuild?.SpecializationId ??
                                     (forcedBuildText is null ? storedSimulation?.ForcedSpecializationId : null);
        var result = CampaignStrategyOptimizer.Search(
            content,
            frontier,
            new SimulationOptions
            {
                Strategy = frontier[0].Strategy.DefaultStrategy,
                StepSeconds = ParseFloat(args, "--step-seconds", storedSimulation?.StepSeconds ?? 0.05f, 0.01f, 0.1f),
                MaximumSimulatedSeconds = ParseFloat(args, "--maximum-seconds",
                    storedSimulation?.MaximumSimulatedSeconds ?? 3600, 1, 86_400),
                ForcedTowerId = forcedTowerId,
                ForcedDoctrineId = forcedDoctrineId,
                ForcedSpecializationId = forcedSpecializationId,
                UseProtocols = (storedSimulation?.UseProtocols ?? true) && !HasFlag(args, "--no-protocols"),
                UseApexUpgrades = (storedSimulation?.UseApexUpgrades ?? true) && !HasFlag(args, "--no-apex"),
                UseCounterSupport = (storedSimulation?.UseCounterSupport ?? true) &&
                                    !HasFlag(args, "--no-counter-support") && !HasFlag(args, "--no-counter-pressure"),
                UseCounterAttackers = (storedSimulation?.UseCounterAttackers ?? true) &&
                                      !HasFlag(args, "--no-counter-attackers") && !HasFlag(args, "--no-counter-pressure"),
                HoldBuild = (storedSimulation?.HoldBuild ?? false) || HasFlag(args, "--hold-build"),
                HoldFootprint = (storedSimulation?.HoldFootprint ?? false) || HasFlag(args, "--hold-footprint")
            },
            new CampaignSearchOptions
            {
                BaseSeed = baseSeedValue,
                BeamWidth = beamWidth,
                CandidateCount = candidateCount,
                MaximumWave = maximumWave,
                BroadeningRounds = broadeningRounds,
                StartingBroadeningRound = startingRound,
                InProgressWave = resumeManifest is null
                    ? 0
                    : resumeManifest.InProgressWave != 0
                        ? resumeManifest.InProgressWave
                        : resumeManifest.SchemaVersion < 4 ? resumeManifest.PendingWave : 0,
                BacktrackDepth = backtrackDepth,
                MaximumRecoveryAttempts = recoveryAttempts,
                RecoveryAttemptOffset = resumeManifest?.RecoveryAttemptOffset ?? 0,
                PolicyId = ReadValue(args, "--policy-id") ?? resumeManifest?.PolicyId ?? "experienced-search",
                BundleIds = bundleIds,
                ParameterOverrides = parameterOverrides,
                PreviouslyEvaluatedConfigurationFingerprints =
                    resumeState?.EvaluatedConfigurationFingerprints ??
                    resumeManifest?.EvaluatedConfigurationFingerprints ?? Array.Empty<string>(),
                PendingFrontier = resumeState?.PendingFrontier ?? Array.Empty<CheckpointSearchState>(),
                RecoveryArchive = resumeState?.RecoveryArchive ?? Array.Empty<CampaignRecoveryArchiveLayerState>(),
                ResumeManifest = resumeManifest,
                ArtifactDirectory = artifactDirectory
            },
            preferredPlan);

        foreach (var attempt in result.WaveAttempts)
            Console.WriteLine(
                $"Wave {attempt.Wave,2} recovery {attempt.RecoveryAttempt,3} round {attempt.BroadeningRound,3}: " +
                $"candidates {attempt.CandidateCount,3}, " +
                $"evaluations {attempt.Evaluations,4}, successes {attempt.SuccessfulEvaluations,3}, " +
                $"retained {attempt.RetainedStates,2}, failures {attempt.Failures,3}.");
        foreach (var recovery in result.Manifest.RecoveryAttempts)
            Console.WriteLine(
                $"Recovery {recovery.Attempt}: wave {recovery.BlockingWave} dead end -> " +
                $"wave {recovery.RecoveredWave} alternate frontier (depth {recovery.Depth}, " +
                $"states {recovery.CheckpointFingerprints.Count}).");
        if (result.Manifest.PendingFrontierArtifacts.Count > 0)
            Console.WriteLine(
                $"Pending beam: wave {result.Manifest.PendingWave}, " +
                $"states {result.Manifest.PendingFrontierArtifacts.Count}/{result.Manifest.BeamWidth}.");
        var populatedArchiveLayers = result.Manifest.RecoveryArchive.Count(layer => layer.RemainingStateCount > 0);
        Console.WriteLine(
            $"Recovery archive: {result.Manifest.RecoveryArchive.Sum(layer => layer.RemainingStateCount)} states " +
            $"across {populatedArchiveLayers} populated layers, " +
            $"{result.Manifest.RecoveryArchive.Sum(layer => layer.DistinctDecisionCount)} decision variants, " +
            $"{result.Manifest.RecoveryArchive.Sum(layer => layer.ExcludedStateCount)} excluded identities.");
        Console.WriteLine(
            $"Strategy search {result.Status}: completed wave {result.LastCompletedWave}, " +
            $"evaluations {result.TotalEvaluations}, resumable states {result.ResumeFrontier.Count}.");
        PrintExactFailure(result.BestFailure);
        Console.WriteLine($"Campaign search manifest: {Path.Combine(Path.GetFullPath(artifactDirectory), "campaign-search.json")}");
        if (result.Manifest.FinalStrategyPath is { } finalStrategyPath)
            Console.WriteLine($"Strategy artifact: {Path.Combine(Path.GetFullPath(artifactDirectory), finalStrategyPath)}");
        return result.Status is CampaignSearchStatus.CampaignCompleted or CampaignSearchStatus.WaveLimitReached ? 0 : 2;
    }

    private static void ValidateResumeSelectors(
        GameContent content,
        string[] args,
        string mapId,
        string difficultyId,
        string challengeId,
        int baseSeed,
        AutoPlayerStrategy? strategy)
    {
        if (ReadValue(args, "--map") is { } requestedMap &&
            !requestedMap.Equals(mapId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"A resumed campaign search must retain map '{mapId}'.");
        if (ReadValue(args, "--difficulty") is { } requestedDifficulty)
        {
            var resolved = ResolveDifficulties(requestedDifficulty, content);
            if (resolved.Count != 1 || !resolved[0].Equals(difficultyId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"A resumed campaign search must retain difficulty '{difficultyId}'.");
        }
        if (ReadValue(args, "--challenge") is { } requestedChallenge)
        {
            var resolved = ResolveChallenges(requestedChallenge, content);
            if (resolved.Count != 1 || !resolved[0].Equals(challengeId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"A resumed campaign search must retain challenge '{challengeId}'.");
        }
        if (ReadValue(args, "--seed") is { } requestedSeed &&
            (!int.TryParse(requestedSeed, out var resolvedSeed) || resolvedSeed != baseSeed))
            throw new ArgumentException($"A resumed campaign search must retain seed {baseSeed}.");
        if (ReadValue(args, "--strategy") is { } requestedStrategy)
        {
            var resolved = ParseStrategy(requestedStrategy, AutoPlayerStrategy.Experienced);
            if (strategy is null || resolved != strategy.Value)
                throw new ArgumentException($"A resumed campaign search must retain strategy '{strategy}'.");
        }
    }

    private static void PrintExactFailure(StrategyReplayWaveResult? failedWave)
    {
        if (failedWave is null) return;
        var simulation = failedWave.Simulation;
        PrintExactFailure(new CheckpointWaveFailure
        {
            ParentCheckpointFingerprint = "fresh-session",
            WavePlan = failedWave.WavePlan,
            Result = simulation.Result,
            LivesRemaining = simulation.LivesRemaining,
            CreditsUnspent = simulation.CreditsUnspent,
            FailureMargin = simulation.FailureMargin,
            RemainingEnemies = simulation.RemainingEnemies,
            QueuedEnemies = simulation.QueuedEnemies,
            FatalEscapedEnemies = simulation.FatalEscapedEnemies,
            PulsePlateDeployments = simulation.PulsePlateDeployments,
            ProtocolActivations = simulation.ProtocolActivations
        });
    }

    private static void PrintExactFailure(CheckpointWaveFailure? failure)
    {
        if (failure is null) return;
        Console.WriteLine($"Best failed wave {failure.WavePlan.Wave}, seed {failure.WavePlan.DecisionSeed}: {failure.Result}.");
        if (failure.FailureMargin is not { } margin)
        {
            Console.WriteLine("No normalized failure margin was available.");
            return;
        }
        Console.WriteLine(
            $"Live {margin.LiveEnemyCount}: health {margin.LiveHealth:0.###}, shield {margin.LiveShield:0.###}, " +
            $"armor-adjusted {margin.LiveArmorAdjustedDurability:0.###}; queued {margin.QueuedEnemyCount}: " +
            $"health {margin.QueuedHealth:0.###}, shield {margin.QueuedShield:0.###}, " +
            $"armor-adjusted {margin.QueuedArmorAdjustedDurability:0.###}.");
        Console.WriteLine(
            $"Fatal-frame escapes {margin.FatalEscapedEnemyCount}: health {margin.FatalEscapedHealth:0.###}, " +
            $"shield {margin.FatalEscapedShield:0.###}, " +
            $"armor-adjusted {margin.FatalEscapedArmorAdjustedDurability:0.###}.");
        Console.WriteLine(
            $"Unresolved armor-adjusted {margin.UnresolvedArmorAdjustedDurability:0.###}/" +
            $"{margin.WaveArmorAdjustedDurability:0.###} " +
            $"({margin.UnresolvedArmorAdjustedDurabilityFraction:P3}); enemies " +
            $"{margin.UnresolvedEnemyCount}/{margin.WaveEnemyCount} ({margin.UnresolvedEnemyFraction:P3}); " +
            $"furthest progress {margin.UnresolvedFurthestProgress:P3}.");
        foreach (var enemy in failure.RemainingEnemies)
            Console.WriteLine(
                $"  LIVE {enemy.Count} {enemy.Rank} {enemy.DisplayName} {enemy.SignalRole}: " +
                $"health {enemy.CurrentHealth:0.###}, shield {enemy.Shield:0.###}, " +
                $"armor-adjusted {enemy.ArmorAdjustedDurability:0.###}, progress {enemy.FurthestProgress:P3}");
        foreach (var enemy in failure.QueuedEnemies)
            Console.WriteLine(
                $"  QUEUED {enemy.Count} {enemy.Rank} {enemy.DisplayName} {enemy.SignalRole}: " +
                $"health {enemy.CurrentHealth:0.###}, shield {enemy.Shield:0.###}, " +
                $"armor-adjusted {enemy.ArmorAdjustedDurability:0.###}");
        foreach (var enemy in failure.FatalEscapedEnemies)
            Console.WriteLine(
                $"  FATAL-ESCAPE {enemy.Rank} {enemy.DisplayName} {enemy.SignalRole}: " +
                $"health {enemy.CurrentHealth:0.###}/{enemy.MaxHealth:0.###}, shield {enemy.Shield:0.###}, " +
                $"armor-adjusted {enemy.ArmorAdjustedDurability:0.###}, progress {enemy.Progress:P3}");
        foreach (var deployment in failure.PulsePlateDeployments)
            Console.WriteLine(
                $"  PLATE #{deployment.PlateId} t={deployment.WaveElapsedSeconds:0.###}s " +
                $"{(deployment.DirectPurchase ? $"direct {deployment.Cost}" : "stored")}: " +
                $"path {deployment.PathProgress:P3}, lead {deployment.LeadProgress:P3}, " +
                $"live {deployment.LiveEnemyCount}, queued {deployment.QueuedEnemyCount}, " +
                $"triggers {deployment.TriggerCount}, hits {deployment.HitCount}, " +
                $"kills {deployment.KillCount}, damage {deployment.Damage:0.###}");
        foreach (var activation in failure.ProtocolActivations)
        {
            var source = activation.IsAutonomous
                ? activation.IsApex ? "APEX-AUTO" : "AUTO"
                : "MANUAL";
            Console.WriteLine(
                $"  PROTOCOL {source} tower #{activation.TowerId} {activation.TowerType} " +
                $"target {activation.TargetMode} t={activation.WaveElapsedSeconds:0.###}s: " +
                $"live {activation.LiveEnemyCount}, queued {activation.QueuedEnemyCount}, " +
                $"lead {activation.LeadProgress:P3}, ranked {activation.RankedEnemyCount} " +
                $"({activation.RankedArmorAdjustedDurability:0.###} AA), " +
                $"signals {activation.SignalEnemyCount}");
        }
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
                $"avg fatal {waveRuns.Average(run => run.FatalEscapedEnemyCount),5:0.0}  " +
                $"avg unresolved AA {waveRuns.Average(run => run.UnresolvedArmorAdjustedDurability),9:0}  " +
                $"avg fraction {waveRuns.Average(run =>
                    run.FailureMargin?.UnresolvedArmorAdjustedDurabilityFraction ?? 0):P3}");

            var composition = waveRuns.SelectMany(run =>
                run.RemainingEnemies.Select(enemy => new FailureCompositionEntry(
                    "LIVE", enemy.DisplayName, enemy.Rank, enemy.SignalRole, enemy.Count,
                    enemy.CurrentHealth, enemy.MaxHealth, enemy.Shield, enemy.ArmorAdjustedDurability,
                    enemy.FurthestProgress))
                .Concat(run.QueuedEnemies.Select(enemy => new FailureCompositionEntry(
                    "QUEUED", enemy.DisplayName, enemy.Rank, enemy.SignalRole, enemy.Count,
                    enemy.CurrentHealth, enemy.MaxHealth, enemy.Shield, enemy.ArmorAdjustedDurability,
                    enemy.FurthestProgress)))
                .Concat(run.FatalEscapedEnemies.Select(enemy => new FailureCompositionEntry(
                    "FATAL", enemy.DisplayName, enemy.Rank, enemy.SignalRole, 1,
                    enemy.CurrentHealth, enemy.MaxHealth, enemy.Shield, enemy.ArmorAdjustedDurability,
                    enemy.Progress))));
            foreach (var enemyGroup in composition
                         .GroupBy(enemy => new { enemy.State, enemy.DisplayName, enemy.Rank, enemy.SignalRole })
                         .OrderByDescending(group => group.Sum(enemy => enemy.ArmorAdjustedDurability))
                         .ThenBy(group => group.Key.State, StringComparer.Ordinal)
                         .ThenBy(group => group.Key.DisplayName, StringComparer.Ordinal))
            {
                var count = enemyGroup.Sum(enemy => enemy.Count);
                var health = enemyGroup.Sum(enemy => enemy.CurrentHealth);
                var maxHealth = enemyGroup.Sum(enemy => enemy.MaxHealth);
                var shield = enemyGroup.Sum(enemy => enemy.Shield);
                var armorAdjusted = enemyGroup.Sum(enemy => enemy.ArmorAdjustedDurability);
                var furthestProgress = enemyGroup.Max(enemy => enemy.Progress);
                var signal = enemyGroup.Key.SignalRole.Equals("None", StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : $" {enemyGroup.Key.SignalRole}";
                var rank = enemyGroup.Key.Rank.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                           enemyGroup.Key.DisplayName.StartsWith(enemyGroup.Key.Rank, StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : $"{enemyGroup.Key.Rank} ";
                Console.WriteLine(
                    $"  {enemyGroup.Key.State,-6} {rank}{enemyGroup.Key.DisplayName}{signal}: " +
                    $"avg count {count / (float)waveRuns.Length:0.0}, HP {(maxHealth <= 0 ? 0 : health / maxHealth):P0}, " +
                    $"avg shield {shield / waveRuns.Length:0}, " +
                    $"avg AA {armorAdjusted / waveRuns.Length:0}, furthest {furthestProgress:P1}");
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

    private sealed record FailureCompositionEntry(
        string State,
        string DisplayName,
        string Rank,
        string SignalRole,
        int Count,
        float CurrentHealth,
        float MaxHealth,
        float Shield,
        float ArmorAdjustedDurability,
        float Progress);

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

    private static bool HasFlag(string[] args, string name) =>
        args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static int ParseInt(string[] args, string name, int fallback, int minimum, int maximum)
    {
        var value = ReadValue(args, name);
        if (value is null) return fallback;
        if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
            throw new ArgumentException($"{name} must be an integer from {minimum} through {maximum}.");
        return parsed;
    }

    private static float ParseFloat(string[] args, string name, float fallback, float minimum, float maximum)
    {
        var value = ReadValue(args, name);
        if (value is null) return fallback;
        if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
            !float.IsFinite(parsed) || parsed < minimum || parsed > maximum)
            throw new ArgumentException($"{name} must be a number from {minimum} through {maximum}.");
        return parsed;
    }

    private static AutoPlayerStrategy ParseStrategy(string? value, AutoPlayerStrategy fallback)
    {
        if (value is null) return fallback;
        if (!Enum.TryParse<AutoPlayerStrategy>(value, true, out var strategy))
            throw new ArgumentException($"Unknown strategy '{value}'.");
        return strategy;
    }

    private static IReadOnlyList<string> SplitList(string? value) => string.IsNullOrWhiteSpace(value)
        ? Array.Empty<string>()
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyDictionary<string, double> ReadParameterOverrides(string[] args)
    {
        var parameters = new SortedDictionary<string, double>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            string? assignment = null;
            if (args[index].StartsWith("--parameter=", StringComparison.OrdinalIgnoreCase))
                assignment = args[index]["--parameter=".Length..];
            else if (args[index].Equals("--parameter", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                assignment = args[++index];
            if (assignment is null) continue;
            var separator = assignment.IndexOf('=');
            if (separator <= 0 || separator == assignment.Length - 1 ||
                !double.TryParse(assignment[(separator + 1)..], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
                throw new ArgumentException("--parameter must use name=value with a finite numeric value.");
            var name = assignment[..separator];
            if (!CampaignWavePlanGenerator.SupportedParameterNames.Contains(name))
                throw new ArgumentException($"Unsupported WavePlan parameter '{name}'.");
            parameters[name] = parsed;
        }
        return parameters;
    }

    private static bool HasParameterArguments(string[] args) => args.Any(arg =>
        arg.Equals("--parameter", StringComparison.OrdinalIgnoreCase) ||
        arg.StartsWith("--parameter=", StringComparison.OrdinalIgnoreCase));

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
