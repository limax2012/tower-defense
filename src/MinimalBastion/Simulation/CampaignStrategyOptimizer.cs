using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Persistence;

namespace MinimalBastion.Simulation;

public sealed record CampaignCandidateBundle(
    string Id,
    string EconomyProfileId,
    string PlacementProfileId,
    string TargetingProfileId,
    string TacticalProfileId,
    IReadOnlyDictionary<string, double> Parameters);

public sealed class CampaignSearchOptions
{
    public int BaseSeed { get; init; } = 1337;
    public int BeamWidth { get; init; } = 3;
    public int CandidateCount { get; init; } = 6;
    public int MaximumWave { get; init; } = GameConstants.CampaignWaveCount;
    public int BroadeningRounds { get; init; } = 1;
    public int StartingBroadeningRound { get; init; }
    public string PolicyId { get; init; } = "experienced-search";
    public IReadOnlyList<string> BundleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, double> ParameterOverrides { get; init; } =
        new SortedDictionary<string, double>(StringComparer.Ordinal);
    public string? ArtifactDirectory { get; init; }

    public void Validate()
    {
        if (BeamWidth is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(BeamWidth));
        if (CandidateCount is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(CandidateCount));
        if (MaximumWave <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumWave));
        if (BroadeningRounds is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(BroadeningRounds));
        if (StartingBroadeningRound is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(StartingBroadeningRound));
        if (string.IsNullOrWhiteSpace(PolicyId) || PolicyId.Length > 128)
            throw new InvalidDataException("Campaign search policy ID is invalid.");
        if (BundleIds is null || ParameterOverrides is null)
            throw new InvalidDataException("Campaign search candidate configuration is missing.");
        if (ParameterOverrides.Count > 64 || ParameterOverrides.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 128 || !double.IsFinite(entry.Value) ||
                !CampaignWavePlanGenerator.SupportedParameterNames.Contains(entry.Key)))
            throw new InvalidDataException("Campaign search parameter overrides are invalid.");
    }
}

public enum CampaignSearchStatus
{
    Running,
    WaveLimitReached,
    CampaignCompleted,
    FrontierExhausted
}

public sealed record CampaignSearchWaveArtifact(
    int Wave,
    int BroadeningRound,
    int CandidateCount,
    int Evaluations,
    int SuccessfulEvaluations,
    int RetainedStates,
    int CampaignCompletions,
    int Failures,
    string? TracePath,
    CheckpointWaveFailure? BestFailure);

public sealed record CampaignFrontierArtifact(
    string CheckpointFingerprint,
    string CheckpointPath,
    string StrategyPath);

public sealed record CampaignSimulationSettings(
    float StepSeconds,
    float MaximumSimulatedSeconds,
    string? ForcedTowerId,
    string? ForcedDoctrineId,
    string? ForcedSpecializationId,
    bool UseProtocols,
    bool UseApexUpgrades,
    bool UseCounterSupport,
    bool UseCounterAttackers,
    bool HoldBuild,
    bool HoldFootprint)
{
    public static CampaignSimulationSettings From(SimulationOptions options) => new(
        options.StepSeconds,
        options.MaximumSimulatedSeconds,
        options.ForcedTowerId,
        options.ForcedDoctrineId,
        options.ForcedSpecializationId,
        options.UseProtocols,
        options.UseApexUpgrades,
        options.UseCounterSupport,
        options.UseCounterAttackers,
        options.HoldBuild,
        options.HoldFootprint);
}

public sealed record CampaignSearchManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ArtifactId { get; init; }
    public required string MapId { get; init; }
    public required string DifficultyId { get; init; }
    public required string ChallengeId { get; init; }
    public required int BaseSeed { get; init; }
    public required int StartingWave { get; init; }
    public required int LastCompletedWave { get; init; }
    public required int MaximumWave { get; init; }
    public required int BeamWidth { get; init; }
    public required int CandidateCount { get; init; }
    public required int BroadeningRounds { get; init; }
    public required string PolicyId { get; init; }
    public required IReadOnlyList<string> BundleIds { get; init; }
    public required IReadOnlyDictionary<string, double> ParameterOverrides { get; init; }
    public required CampaignSimulationSettings SimulationSettings { get; init; }
    public required int NextBroadeningRound { get; init; }
    public required int TotalEvaluations { get; init; }
    public required CampaignSearchStatus Status { get; init; }
    public required IReadOnlyList<CampaignSearchWaveArtifact> WaveAttempts { get; init; }
    public required IReadOnlyList<CampaignFrontierArtifact> FrontierArtifacts { get; init; }
    public CheckpointWaveFailure? BestFailure { get; init; }
    public string? FinalStrategyPath { get; init; }
}

public sealed class CampaignSearchRunResult
{
    public required CampaignSearchStatus Status { get; init; }
    public required int LastCompletedWave { get; init; }
    public required int TotalEvaluations { get; init; }
    public required IReadOnlyList<CampaignSearchWaveArtifact> WaveAttempts { get; init; }
    public required IReadOnlyList<CheckpointSearchState> ResumeFrontier { get; init; }
    public required IReadOnlyList<CheckpointCampaignCompletion> CampaignCompletions { get; init; }
    public StrategyPlan? FinalStrategy { get; init; }
    public CheckpointWaveFailure? BestFailure { get; init; }
    public required CampaignSearchManifest Manifest { get; init; }
}

public static class CampaignWavePlanGenerator
{
    public static readonly IReadOnlySet<string> SupportedParameterNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "purchaseBias", "upgradeBias", "reserveMultiplier", "reserveCredits", "coverageWeight", "nodeWeight",
        "clusterWeight", "plateProgressOffset", "activePlateLimit", "directPlateLimit", "protocolMinimumEnemies",
        "apexLimit", "saleLimit"
    };

    public static readonly IReadOnlyList<CampaignCandidateBundle> DefaultBundles =
    [
        Bundle("balanced-coverage", "balanced", "coverage", "split", "adaptive",
            ("purchaseBias", 1.0), ("upgradeBias", 1.0), ("reserveMultiplier", 1.0),
            ("coverageWeight", 1.2), ("nodeWeight", 1.15), ("clusterWeight", 1.0),
            ("plateProgressOffset", 0.0), ("activePlateLimit", 6), ("directPlateLimit", 4),
            ("protocolMinimumEnemies", 5), ("apexLimit", 1), ("saleLimit", 1)),
        Bundle("mature-nodes", "mature", "nodes", "armored", "plates",
            ("purchaseBias", 0.82), ("upgradeBias", 1.28), ("reserveMultiplier", 0.85),
            ("coverageWeight", 1.05), ("nodeWeight", 1.5), ("clusterWeight", 0.9),
            ("plateProgressOffset", -0.06), ("activePlateLimit", 7), ("directPlateLimit", 5),
            ("protocolMinimumEnemies", 5), ("apexLimit", 1), ("saleLimit", 1)),
        Bundle("invest-clusters", "invest", "clusters", "support", "protocols",
            ("purchaseBias", 0.92), ("upgradeBias", 1.15), ("reserveMultiplier", 0.72),
            ("coverageWeight", 1.25), ("nodeWeight", 1.05), ("clusterWeight", 1.45),
            ("plateProgressOffset", 0.03), ("activePlateLimit", 5), ("directPlateLimit", 3),
            ("protocolMinimumEnemies", 3), ("apexLimit", 1), ("saleLimit", 1)),
        Bundle("apex-precise", "apex", "precise", "strongest", "conserve",
            ("purchaseBias", 0.7), ("upgradeBias", 1.35), ("reserveMultiplier", 1.3),
            ("reserveCredits", 180), ("coverageWeight", 1.3), ("nodeWeight", 1.25),
            ("clusterWeight", 1.1), ("plateProgressOffset", 0.08), ("activePlateLimit", 4),
            ("directPlateLimit", 2), ("protocolMinimumEnemies", 7), ("apexLimit", 1),
            ("saleLimit", 2)),
        Bundle("reserve-explore", "reserve", "explore", "first", "conserve",
            ("purchaseBias", 0.9), ("upgradeBias", 1.05), ("reserveMultiplier", 1.55),
            ("reserveCredits", 240), ("coverageWeight", 1.0), ("nodeWeight", 1.0),
            ("clusterWeight", 0.85), ("plateProgressOffset", 0.1), ("activePlateLimit", 3),
            ("directPlateLimit", 1), ("protocolMinimumEnemies", 8), ("apexLimit", 1),
            ("saleLimit", 2)),
        Bundle("plate-coverage", "balanced", "coverage", "first", "plates",
            ("purchaseBias", 0.95), ("upgradeBias", 1.08), ("reserveMultiplier", 0.78),
            ("coverageWeight", 1.35), ("nodeWeight", 1.2), ("clusterWeight", 1.05),
            ("plateProgressOffset", -0.1), ("activePlateLimit", 9), ("directPlateLimit", 7),
            ("protocolMinimumEnemies", 5), ("apexLimit", 1), ("saleLimit", 1)),
        Bundle("protocol-support", "invest", "clusters", "support", "protocols",
            ("purchaseBias", 1.0), ("upgradeBias", 1.1), ("reserveMultiplier", 0.7),
            ("coverageWeight", 1.2), ("nodeWeight", 1.1), ("clusterWeight", 1.35),
            ("plateProgressOffset", 0.04), ("activePlateLimit", 5), ("directPlateLimit", 2),
            ("protocolMinimumEnemies", 2), ("apexLimit", 1), ("saleLimit", 1)),
        Bundle("armor-nodes", "mature", "nodes", "armored", "adaptive",
            ("purchaseBias", 0.86), ("upgradeBias", 1.22), ("reserveMultiplier", 0.9),
            ("coverageWeight", 1.1), ("nodeWeight", 1.55), ("clusterWeight", 0.95),
            ("plateProgressOffset", -0.02), ("activePlateLimit", 6), ("directPlateLimit", 4),
            ("protocolMinimumEnemies", 4), ("apexLimit", 1), ("saleLimit", 1))
    ];

    public static IReadOnlyList<WavePlan> Generate(
        int wave,
        CampaignSearchOptions options,
        int broadeningRound,
        int candidateCount,
        WavePlan? preferredPlan = null)
    {
        options.Validate();
        if (wave <= 0) throw new ArgumentOutOfRangeException(nameof(wave));
        if (broadeningRound < 0) throw new ArgumentOutOfRangeException(nameof(broadeningRound));
        if (candidateCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(candidateCount));
        if (preferredPlan is not null && preferredPlan.Wave != wave)
            throw new ArgumentException("Preferred candidate targets a different wave.", nameof(preferredPlan));

        var bundles = ResolveBundles(options.BundleIds);
        var candidates = new Dictionary<string, WavePlan>(StringComparer.Ordinal);
        if (preferredPlan is not null)
        {
            preferredPlan.Validate();
            candidates[preferredPlan.StableKey] = preferredPlan;
        }

        var ordinal = 0;
        while (candidates.Count < candidateCount)
        {
            var bundleIndex = PositiveModulo(wave + broadeningRound * 3 + ordinal, bundles.Count);
            var bundle = bundles[bundleIndex];
            var parameters = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var parameter in bundle.Parameters) parameters[parameter.Key] = parameter.Value;
            foreach (var parameter in options.ParameterOverrides) parameters[parameter.Key] = parameter.Value;
            var plan = new WavePlan
            {
                Wave = wave,
                DecisionSeed = DecisionSeed(options.BaseSeed, wave, broadeningRound, ordinal),
                PolicyId = options.PolicyId,
                EconomyProfileId = bundle.EconomyProfileId,
                PlacementProfileId = bundle.PlacementProfileId,
                TargetingProfileId = bundle.TargetingProfileId,
                TacticalProfileId = bundle.TacticalProfileId,
                Parameters = parameters
            };
            candidates[plan.StableKey] = plan;
            ordinal++;
        }
        return candidates.Values.OrderBy(plan => plan.StableKey, StringComparer.Ordinal).ToArray();
    }

    public static int DecisionSeed(int baseSeed, int wave, int broadeningRound, int ordinal)
    {
        unchecked
        {
            var value = (uint)baseSeed;
            value ^= (uint)wave * 0x9E3779B9u;
            value = (value << 13) | (value >> 19);
            value ^= (uint)broadeningRound * 0x85EBCA6Bu;
            value = value * 1664525u + 1013904223u + (uint)ordinal * 7919u;
            return (int)(value & 0x7fffffffu);
        }
    }

    private static IReadOnlyList<CampaignCandidateBundle> ResolveBundles(IReadOnlyList<string> requested)
    {
        if (requested.Count == 0) return DefaultBundles;
        var selected = new List<CampaignCandidateBundle>();
        foreach (var id in requested)
        {
            var bundle = DefaultBundles.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
                         throw new ArgumentException($"Unknown campaign candidate bundle '{id}'.");
            if (selected.All(existing => !existing.Id.Equals(bundle.Id, StringComparison.OrdinalIgnoreCase)))
                selected.Add(bundle);
        }
        return selected;
    }

    private static CampaignCandidateBundle Bundle(
        string id,
        string economy,
        string placement,
        string targeting,
        string tactical,
        params (string Key, double Value)[] parameters) => new(
        id,
        economy,
        placement,
        targeting,
        tactical,
        parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal));

    private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;
}

public static class CampaignStrategyOptimizer
{
    public static CampaignSearchRunResult Search(
        GameContent content,
        IReadOnlyList<CheckpointSearchState> initialFrontier,
        SimulationOptions simulationOptions,
        CampaignSearchOptions searchOptions,
        StrategyPlan? preferredPlan = null)
    {
        searchOptions.Validate();
        if (initialFrontier.Count == 0)
            throw new ArgumentException("Campaign search requires at least one checkpoint state.", nameof(initialFrontier));
        var frontier = CheckpointBeamOptimizer.RankStates(initialFrontier, searchOptions.BeamWidth);
        var startingWave = frontier[0].Checkpoint.Waves.CurrentWaveNumber;
        var referenceStrategy = frontier[0].Strategy;
        if (frontier.Any(state => state.Checkpoint.Waves.CurrentWaveNumber != startingWave))
            throw new ArgumentException("Campaign search frontier states must be at the same wave.", nameof(initialFrontier));
        foreach (var state in frontier)
        {
            state.Strategy.ValidatePrefixForCheckpoint(state.Checkpoint);
            if (state.Strategy.BaseSeed != searchOptions.BaseSeed ||
                !state.Strategy.ArtifactId.Equals(referenceStrategy.ArtifactId, StringComparison.Ordinal) ||
                !state.Strategy.MapId.Equals(referenceStrategy.MapId, StringComparison.OrdinalIgnoreCase) ||
                !state.Strategy.DifficultyId.Equals(referenceStrategy.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
                !state.Strategy.ChallengeId.Equals(referenceStrategy.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
                state.Strategy.DefaultStrategy != referenceStrategy.DefaultStrategy)
                throw new InvalidDataException("Campaign search frontier strategies do not share one execution context.");
        }
        if (preferredPlan is not null)
        {
            preferredPlan.ValidateForCheckpoint(frontier[0].Checkpoint);
            if (preferredPlan.BaseSeed != searchOptions.BaseSeed ||
                !preferredPlan.ArtifactId.Equals(referenceStrategy.ArtifactId, StringComparison.Ordinal) ||
                preferredPlan.DefaultStrategy != referenceStrategy.DefaultStrategy)
                throw new InvalidDataException("Preferred strategy does not match the campaign search execution context.");
        }

        var restored = GameSession.RestoreSaveGame(content, frontier[0].Checkpoint);
        var maximumWave = Math.Min(searchOptions.MaximumWave, restored.TotalWaves);
        var attempts = new List<CampaignSearchWaveArtifact>();
        var totalEvaluations = 0;
        var artifactRoot = string.IsNullOrWhiteSpace(searchOptions.ArtifactDirectory)
            ? null
            : Path.GetFullPath(searchOptions.ArtifactDirectory);
        var frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{startingWave:D2}/resume", frontier);
        var manifest = NewManifest(
            frontier[0].Strategy,
            simulationOptions,
            searchOptions,
            startingWave,
            maximumWave,
            frontierArtifacts);
        SaveManifest(artifactRoot, manifest);

        if (startingWave >= maximumWave)
            return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, startingWave);

        var firstAttemptedWave = true;
        while (frontier[0].Checkpoint.Waves.CurrentWaveNumber < maximumWave)
        {
            var priorFrontier = frontier;
            var targetWave = priorFrontier[0].Checkpoint.Waves.CurrentWaveNumber + 1;
            var startingRound = firstAttemptedWave ? searchOptions.StartingBroadeningRound : 0;
            firstAttemptedWave = false;
            var failures = new List<CheckpointWaveFailure>();
            var advanced = false;

            for (var offset = 0; offset <= searchOptions.BroadeningRounds; offset++)
            {
                var broadeningRound = startingRound + offset;
                var candidateCount = Math.Min(256, checked(searchOptions.CandidateCount * (offset + 1)));
                var candidates = CampaignWavePlanGenerator.Generate(
                    targetWave,
                    searchOptions,
                    broadeningRound,
                    candidateCount,
                    preferredPlan?.FindWave(targetWave));
                var waveResult = CheckpointBeamOptimizer.EvaluateWave(
                    content,
                    priorFrontier,
                    candidates,
                    simulationOptions,
                    searchOptions.BeamWidth);
                totalEvaluations += waveResult.Evaluations;
                failures.AddRange(waveResult.Failures);
                var tracePath = PersistSearchTrace(content, artifactRoot, targetWave, broadeningRound, waveResult);
                var attempt = new CampaignSearchWaveArtifact(
                    targetWave,
                    broadeningRound,
                    candidates.Count,
                    waveResult.Evaluations,
                    waveResult.SuccessfulEvaluations.Count,
                    waveResult.RetainedStates.Count,
                    waveResult.CampaignCompletions.Count,
                    waveResult.Failures.Count,
                    tracePath,
                    SelectBestFailure(waveResult.Failures));
                attempts.Add(attempt);

                if (waveResult.CampaignCompletions.Count > 0)
                {
                    var completions = OrderCompletions(waveResult.CampaignCompletions);
                    var winning = completions[0].Strategy;
                    var strategyPath = PersistStrategy(artifactRoot, "winning-strategy.json", winning);
                    manifest = manifest with
                    {
                        LastCompletedWave = targetWave,
                        NextBroadeningRound = 0,
                        TotalEvaluations = totalEvaluations,
                        Status = CampaignSearchStatus.CampaignCompleted,
                        WaveAttempts = attempts.ToArray(),
                        FrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                        BestFailure = SelectBestFailure(failures),
                        FinalStrategyPath = strategyPath
                    };
                    SaveManifest(artifactRoot, manifest);
                    return new CampaignSearchRunResult
                    {
                        Status = manifest.Status,
                        LastCompletedWave = targetWave,
                        TotalEvaluations = totalEvaluations,
                        WaveAttempts = attempts.ToArray(),
                        ResumeFrontier = Array.Empty<CheckpointSearchState>(),
                        CampaignCompletions = completions,
                        FinalStrategy = winning,
                        BestFailure = manifest.BestFailure,
                        Manifest = manifest
                    };
                }

                if (waveResult.RetainedStates.Count > 0)
                {
                    frontier = waveResult.RetainedStates;
                    frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{targetWave:D2}/frontier", frontier);
                    manifest = manifest with
                    {
                        LastCompletedWave = targetWave,
                        NextBroadeningRound = 0,
                        TotalEvaluations = totalEvaluations,
                        Status = CampaignSearchStatus.Running,
                        WaveAttempts = attempts.ToArray(),
                        FrontierArtifacts = frontierArtifacts,
                        BestFailure = SelectBestFailure(failures)
                    };
                    SaveManifest(artifactRoot, manifest);
                    advanced = true;
                    break;
                }

                manifest = manifest with
                {
                    NextBroadeningRound = broadeningRound + 1,
                    TotalEvaluations = totalEvaluations,
                    Status = CampaignSearchStatus.Running,
                    WaveAttempts = attempts.ToArray(),
                    BestFailure = SelectBestFailure(failures)
                };
                SaveManifest(artifactRoot, manifest);
            }

            if (!advanced)
            {
                frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{targetWave:D2}/retry", priorFrontier);
                var bestFailure = SelectBestFailure(failures);
                manifest = manifest with
                {
                    LastCompletedWave = targetWave - 1,
                    NextBroadeningRound = startingRound + searchOptions.BroadeningRounds + 1,
                    TotalEvaluations = totalEvaluations,
                    Status = CampaignSearchStatus.FrontierExhausted,
                    WaveAttempts = attempts.ToArray(),
                    FrontierArtifacts = frontierArtifacts,
                    BestFailure = bestFailure,
                    FinalStrategyPath = PersistStrategy(artifactRoot, "best-prefix-strategy.json", priorFrontier[0].Strategy)
                };
                SaveManifest(artifactRoot, manifest);
                return new CampaignSearchRunResult
                {
                    Status = manifest.Status,
                    LastCompletedWave = targetWave - 1,
                    TotalEvaluations = totalEvaluations,
                    WaveAttempts = attempts.ToArray(),
                    ResumeFrontier = priorFrontier,
                    CampaignCompletions = Array.Empty<CheckpointCampaignCompletion>(),
                    FinalStrategy = priorFrontier[0].Strategy,
                    BestFailure = bestFailure,
                    Manifest = manifest
                };
            }

            if (frontier[0].Checkpoint.Waves.CurrentWaveNumber >= maximumWave)
                return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, maximumWave);
        }

        return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, maximumWave);
    }

    public static CheckpointWaveFailure? SelectBestFailure(IEnumerable<CheckpointWaveFailure> failures) => failures
        .OrderBy(failure => failure.FailureMargin is null ? 1 : 0)
        .ThenBy(failure => failure.FailureMargin?.RemainingArmorAdjustedDurabilityFraction ?? float.MaxValue)
        .ThenBy(failure => failure.FailureMargin?.TotalArmorAdjustedDurability ?? float.MaxValue)
        .ThenBy(failure => failure.FailureMargin?.TotalEnemyCount ?? int.MaxValue)
        .ThenByDescending(failure => failure.FailureMargin?.FurthestProgress ?? 0)
        .ThenBy(failure => failure.WavePlan.StableKey, StringComparer.Ordinal)
        .FirstOrDefault();

    private static CampaignSearchRunResult FinishAtWaveLimit(
        GameContent content,
        string? artifactRoot,
        IReadOnlyList<CheckpointSearchState> frontier,
        IReadOnlyList<CampaignSearchWaveArtifact> attempts,
        int totalEvaluations,
        CampaignSearchManifest manifest,
        int wave)
    {
        var ranked = CheckpointBeamOptimizer.RankStates(frontier, frontier.Count);
        var finalStrategy = ranked[0].Strategy;
        var frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{wave:D2}/frontier", ranked);
        manifest = manifest with
        {
            LastCompletedWave = wave,
            NextBroadeningRound = 0,
            TotalEvaluations = totalEvaluations,
            Status = CampaignSearchStatus.WaveLimitReached,
            WaveAttempts = attempts.ToArray(),
            FrontierArtifacts = frontierArtifacts,
            FinalStrategyPath = PersistStrategy(artifactRoot, "best-strategy.json", finalStrategy)
        };
        SaveManifest(artifactRoot, manifest);
        return new CampaignSearchRunResult
        {
            Status = manifest.Status,
            LastCompletedWave = wave,
            TotalEvaluations = totalEvaluations,
            WaveAttempts = attempts.ToArray(),
            ResumeFrontier = ranked,
            CampaignCompletions = Array.Empty<CheckpointCampaignCompletion>(),
            FinalStrategy = finalStrategy,
            BestFailure = manifest.BestFailure,
            Manifest = manifest
        };
    }

    private static IReadOnlyList<CheckpointCampaignCompletion> OrderCompletions(
        IEnumerable<CheckpointCampaignCompletion> completions) => completions
        .OrderByDescending(completion => completion.Simulation.LivesRemaining)
        .ThenByDescending(completion => completion.Simulation.FinalTowers.Count(tower => tower.IsApex))
        .ThenByDescending(completion => completion.Simulation.FinalTowers.Count(tower => tower.Level >= 3))
        .ThenByDescending(completion => completion.Simulation.FinalTowers
            .Select(tower => tower.PowerNodeId).Where(id => id is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count())
        .ThenByDescending(completion => completion.Simulation.CreditsUnspent)
        .ThenBy(completion => completion.WavePlan.StableKey, StringComparer.Ordinal)
        .ToArray();

    private static CampaignSearchManifest NewManifest(
        StrategyPlan strategy,
        SimulationOptions simulationOptions,
        CampaignSearchOptions options,
        int startingWave,
        int maximumWave,
        IReadOnlyList<CampaignFrontierArtifact> frontierArtifacts) => new()
    {
        ArtifactId = strategy.ArtifactId,
        MapId = strategy.MapId,
        DifficultyId = strategy.DifficultyId,
        ChallengeId = strategy.ChallengeId,
        BaseSeed = strategy.BaseSeed,
        StartingWave = startingWave,
        LastCompletedWave = startingWave,
        MaximumWave = maximumWave,
        BeamWidth = options.BeamWidth,
        CandidateCount = options.CandidateCount,
        BroadeningRounds = options.BroadeningRounds,
        PolicyId = options.PolicyId,
        BundleIds = options.BundleIds.ToArray(),
        ParameterOverrides = options.ParameterOverrides.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal),
        SimulationSettings = CampaignSimulationSettings.From(simulationOptions),
        NextBroadeningRound = options.StartingBroadeningRound,
        TotalEvaluations = 0,
        Status = CampaignSearchStatus.Running,
        WaveAttempts = Array.Empty<CampaignSearchWaveArtifact>(),
        FrontierArtifacts = frontierArtifacts
    };

    private static IReadOnlyList<CampaignFrontierArtifact> PersistFrontier(
        GameContent content,
        string? artifactRoot,
        string relativeDirectory,
        IReadOnlyList<CheckpointSearchState> frontier)
    {
        if (artifactRoot is null) return Array.Empty<CampaignFrontierArtifact>();
        var results = new List<CampaignFrontierArtifact>();
        for (var index = 0; index < frontier.Count; index++)
        {
            var state = frontier[index];
            var stem = $"{index:D2}-{state.CheckpointFingerprint[..12]}";
            var checkpointPath = NormalizeRelative(Path.Combine(relativeDirectory, stem + ".checkpoint.json"));
            var strategyPath = NormalizeRelative(Path.Combine(relativeDirectory, stem + ".strategy.json"));
            StrategyArtifactStore.SaveCheckpoint(Path.Combine(artifactRoot, checkpointPath),
                StrategyCheckpointArtifact.Create(state.Strategy.ArtifactId, state.Checkpoint), content);
            StrategyArtifactStore.SavePlan(Path.Combine(artifactRoot, strategyPath), state.Strategy);
            results.Add(new CampaignFrontierArtifact(state.CheckpointFingerprint, checkpointPath, strategyPath));
        }
        return results;
    }

    private static string? PersistSearchTrace(
        GameContent content,
        string? artifactRoot,
        int wave,
        int broadeningRound,
        CheckpointSearchResult result)
    {
        if (artifactRoot is null) return null;
        var relativePath = NormalizeRelative(Path.Combine($"wave-{wave:D2}", $"round-{broadeningRound:D3}-search.json"));
        StrategyArtifactStore.SaveSearchResult(Path.Combine(artifactRoot, relativePath), result, content);
        return relativePath;
    }

    private static string? PersistStrategy(string? artifactRoot, string filename, StrategyPlan strategy)
    {
        if (artifactRoot is null) return null;
        var relativePath = NormalizeRelative(filename);
        StrategyArtifactStore.SavePlan(Path.Combine(artifactRoot, relativePath), strategy);
        return relativePath;
    }

    private static void SaveManifest(string? artifactRoot, CampaignSearchManifest manifest)
    {
        if (artifactRoot is null) return;
        CampaignSearchArtifactStore.SaveManifest(Path.Combine(artifactRoot, "campaign-search.json"), manifest);
    }

    private static string NormalizeRelative(string path) => path.Replace('\\', '/');
}

public static class CampaignSearchArtifactStore
{
    private const int MaximumManifestBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void SaveManifest(string path, CampaignSearchManifest manifest)
    {
        Validate(manifest);
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (payload.Length > MaximumManifestBytes)
            throw new InvalidDataException("Campaign search manifest exceeds the supported size limit.");
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, payload);
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static CampaignSearchManifest LoadManifest(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Campaign search manifest was not found.", fullPath);
        var length = new FileInfo(fullPath).Length;
        if (length <= 0 || length > MaximumManifestBytes)
            throw new InvalidDataException("Campaign search manifest size is invalid.");
        var manifest = JsonSerializer.Deserialize<CampaignSearchManifest>(File.ReadAllText(fullPath), JsonOptions) ??
                       throw new InvalidDataException("Campaign search manifest is empty or malformed.");
        Validate(manifest);
        return manifest;
    }

    public static IReadOnlyList<CheckpointSearchState> LoadFrontier(
        GameContent content,
        string manifestPath)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifest = LoadManifest(fullManifestPath);
        var root = Path.GetDirectoryName(fullManifestPath)!;
        var states = manifest.FrontierArtifacts.Select(artifact =>
        {
            var checkpointPath = ResolveRelative(root, artifact.CheckpointPath);
            var strategyPath = ResolveRelative(root, artifact.StrategyPath);
            var checkpoint = StrategyArtifactStore.LoadCheckpoint(checkpointPath, content);
            var strategy = StrategyArtifactStore.LoadPlan(strategyPath);
            var state = CheckpointSearchState.Create(content, strategy, checkpoint.Checkpoint);
            if (!state.CheckpointFingerprint.Equals(artifact.CheckpointFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("Campaign search frontier fingerprint does not match its artifacts.");
            return state;
        }).ToArray();
        return CheckpointBeamOptimizer.RankStates(states, Math.Max(1, states.Length));
    }

    private static void Validate(CampaignSearchManifest manifest)
    {
        if (manifest.SchemaVersion != CampaignSearchManifest.CurrentSchemaVersion)
            throw new InvalidDataException($"Campaign search schema {manifest.SchemaVersion} is not supported.");
        if (string.IsNullOrWhiteSpace(manifest.ArtifactId) || string.IsNullOrWhiteSpace(manifest.MapId) ||
            string.IsNullOrWhiteSpace(manifest.DifficultyId) || string.IsNullOrWhiteSpace(manifest.ChallengeId) ||
            manifest.StartingWave < 0 || manifest.LastCompletedWave < manifest.StartingWave ||
            manifest.MaximumWave < manifest.LastCompletedWave || manifest.BeamWidth <= 0 ||
            manifest.CandidateCount <= 0 || manifest.BroadeningRounds < 0 || manifest.NextBroadeningRound < 0 ||
            manifest.TotalEvaluations < 0 || string.IsNullOrWhiteSpace(manifest.PolicyId) ||
            manifest.BundleIds is null || manifest.ParameterOverrides is null || manifest.SimulationSettings is null ||
            manifest.WaveAttempts is null || manifest.FrontierArtifacts is null)
            throw new InvalidDataException("Campaign search manifest fields are invalid.");
        if (!float.IsFinite(manifest.SimulationSettings.StepSeconds) ||
            manifest.SimulationSettings.StepSeconds is < 0.01f or > 0.1f ||
            !float.IsFinite(manifest.SimulationSettings.MaximumSimulatedSeconds) ||
            manifest.SimulationSettings.MaximumSimulatedSeconds <= 0 ||
            manifest.ParameterOverrides.Any(parameter =>
                !CampaignWavePlanGenerator.SupportedParameterNames.Contains(parameter.Key) ||
                !double.IsFinite(parameter.Value)))
            throw new InvalidDataException("Campaign search execution settings are invalid.");
        if (manifest.TotalEvaluations != manifest.WaveAttempts.Sum(attempt => attempt.Evaluations))
            throw new InvalidDataException("Campaign search manifest evaluation totals are inconsistent.");
        if (manifest.FrontierArtifacts.Count > manifest.BeamWidth)
            throw new InvalidDataException("Campaign search manifest frontier exceeds its beam width.");
        foreach (var artifact in manifest.FrontierArtifacts)
        {
            if (artifact.CheckpointFingerprint.Length != 64 || !IsSafeRelativePath(artifact.CheckpointPath) ||
                !IsSafeRelativePath(artifact.StrategyPath))
                throw new InvalidDataException("Campaign search frontier artifact path or identity is invalid.");
        }
        if (manifest.FinalStrategyPath is not null && !IsSafeRelativePath(manifest.FinalStrategyPath))
            throw new InvalidDataException("Campaign search final strategy path is invalid.");
        if (manifest.Status == CampaignSearchStatus.FrontierExhausted &&
            (manifest.FrontierArtifacts.Count == 0 || manifest.BestFailure is null))
            throw new InvalidDataException("An exhausted campaign search must retain its retry frontier and best failure.");
        if (manifest.Status == CampaignSearchStatus.CampaignCompleted && manifest.FinalStrategyPath is null)
            throw new InvalidDataException("A completed campaign search must identify its final strategy.");
        if (manifest.BestFailure is { } failure && failure.FailureMargin is { } margin &&
            margin.TotalEnemyCount != failure.RemainingEnemies.Sum(enemy => enemy.Count) +
                                      failure.QueuedEnemies.Sum(enemy => enemy.Count))
            throw new InvalidDataException("Campaign search best-failure composition is inconsistent.");
    }

    private static string ResolveRelative(string root, string relativePath)
    {
        if (!IsSafeRelativePath(relativePath))
            throw new InvalidDataException("Campaign search artifact path is not a safe relative path.");
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Campaign search artifact path escapes its manifest directory.");
        return fullPath;
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        !path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == "..");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
