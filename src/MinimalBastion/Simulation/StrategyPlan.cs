using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MinimalBastion.Data;
using MinimalBastion.Persistence;

namespace MinimalBastion.Simulation;

public sealed record WavePlan
{
    public required int Wave { get; init; }
    public required int DecisionSeed { get; init; }
    public string PolicyId { get; init; } = "experienced";
    public AutoPlayerStrategy? StrategyOverride { get; init; }
    public string EconomyProfileId { get; init; } = "balanced";
    public string PlacementProfileId { get; init; } = "coverage";
    public string TargetingProfileId { get; init; } = "split";
    public string TacticalProfileId { get; init; } = "adaptive";
    public IReadOnlyDictionary<string, double> Parameters { get; init; } =
        new SortedDictionary<string, double>(StringComparer.Ordinal);

    [JsonIgnore]
    public string StableKey
    {
        get
        {
            var builder = new StringBuilder()
                .Append(Wave).Append('|')
                .Append(DecisionSeed).Append('|')
                .Append(PolicyId).Append('|')
                .Append(StrategyOverride?.ToString() ?? "-").Append('|')
                .Append(EconomyProfileId).Append('|')
                .Append(PlacementProfileId).Append('|')
                .Append(TargetingProfileId).Append('|')
                .Append(TacticalProfileId);
            foreach (var parameter in Parameters.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                builder.Append('|').Append(parameter.Key).Append('=').Append(parameter.Value.ToString("R", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public void Validate()
    {
        if (Wave <= 0) throw new InvalidDataException("A planned wave number must be positive.");
        ValidateIdentifier(PolicyId, nameof(PolicyId));
        ValidateIdentifier(EconomyProfileId, nameof(EconomyProfileId));
        ValidateIdentifier(PlacementProfileId, nameof(PlacementProfileId));
        ValidateIdentifier(TargetingProfileId, nameof(TargetingProfileId));
        ValidateIdentifier(TacticalProfileId, nameof(TacticalProfileId));
        if (Parameters is null)
            throw new InvalidDataException("A wave plan parameter collection is missing.");
        if (Parameters.Count > 64)
            throw new InvalidDataException("A wave plan cannot contain more than 64 policy parameters.");
        foreach (var parameter in Parameters)
        {
            ValidateIdentifier(parameter.Key, "parameter name");
            if (!double.IsFinite(parameter.Value))
                throw new InvalidDataException($"Wave {Wave} parameter '{parameter.Key}' is not finite.");
        }
    }

    private static void ValidateIdentifier(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new InvalidDataException($"Wave plan {field} must contain between 1 and 128 characters.");
    }
}

public sealed record StrategyPlan
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ArtifactId { get; init; }
    public required string MapId { get; init; }
    public required string DifficultyId { get; init; }
    public required string ChallengeId { get; init; }
    public required int BaseSeed { get; init; }
    public AutoPlayerStrategy DefaultStrategy { get; init; } = AutoPlayerStrategy.Experienced;
    public IReadOnlyList<WavePlan> Waves { get; init; } = Array.Empty<WavePlan>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal);

    public WavePlan? FindWave(int wave) => Waves.SingleOrDefault(plan => plan.Wave == wave);

    public StrategyPlan Append(WavePlan wavePlan)
    {
        wavePlan.Validate();
        if (Waves.Any(existing => existing.Wave == wavePlan.Wave))
            throw new InvalidOperationException($"Strategy '{ArtifactId}' already contains wave {wavePlan.Wave}.");
        if (Waves.Count > 0 && wavePlan.Wave <= Waves[^1].Wave)
            throw new InvalidOperationException("Wave plans must be appended in campaign order.");
        return this with { Waves = Waves.Append(wavePlan).ToArray() };
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Strategy schema {SchemaVersion} is not supported.");
        ValidateArtifactIdentifier(ArtifactId, nameof(ArtifactId));
        ValidateArtifactIdentifier(MapId, nameof(MapId));
        ValidateArtifactIdentifier(DifficultyId, nameof(DifficultyId));
        ValidateArtifactIdentifier(ChallengeId, nameof(ChallengeId));
        if (Waves is null || Metadata is null)
            throw new InvalidDataException("A strategy artifact is missing its waves or metadata collection.");
        if (Waves.Count > 10_000)
            throw new InvalidDataException("A strategy artifact contains too many wave plans.");
        var previousWave = 0;
        foreach (var wave in Waves)
        {
            if (wave is null) throw new InvalidDataException("A strategy artifact contains a null wave plan.");
            wave.Validate();
            if (wave.Wave <= previousWave)
                throw new InvalidDataException("Strategy wave plans must be unique and sorted in campaign order.");
            previousWave = wave.Wave;
        }
        if (Metadata.Count > 64)
            throw new InvalidDataException("A strategy artifact cannot contain more than 64 metadata entries.");
        foreach (var entry in Metadata)
        {
            ValidateArtifactIdentifier(entry.Key, "metadata name");
            if (entry.Value is null || entry.Value.Length > 1024)
                throw new InvalidDataException($"Strategy metadata '{entry.Key}' exceeds 1024 characters.");
        }
    }

    public void ValidateForCheckpoint(SaveGameData checkpoint)
    {
        Validate();
        if (!MapId.Equals(checkpoint.MapId, StringComparison.OrdinalIgnoreCase) ||
            !DifficultyId.Equals(checkpoint.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !ChallengeId.Equals(checkpoint.ChallengeId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The strategy and checkpoint describe different run configurations.");
    }

    public void ValidatePrefixForCheckpoint(SaveGameData checkpoint)
    {
        ValidateForCheckpoint(checkpoint);
        if (Waves.Any(wave => wave.Wave > checkpoint.Waves.CurrentWaveNumber))
            throw new InvalidDataException("The strategy prefix contains decisions after the supplied checkpoint.");
    }

    private static void ValidateArtifactIdentifier(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new InvalidDataException($"Strategy {field} must contain between 1 and 128 characters.");
    }
}

public sealed record StrategyCheckpointArtifact
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string StrategyArtifactId { get; init; }
    public required string CheckpointFingerprint { get; init; }
    public required SaveGameData Checkpoint { get; init; }

    public static StrategyCheckpointArtifact Create(string strategyArtifactId, SaveGameData checkpoint) => new()
    {
        StrategyArtifactId = strategyArtifactId,
        Checkpoint = checkpoint,
        CheckpointFingerprint = StrategyArtifactStore.Fingerprint(checkpoint)
    };
}

public static class StrategyArtifactStore
{
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void SavePlan(string path, StrategyPlan plan)
    {
        plan.Validate();
        WriteAtomically(path, JsonSerializer.SerializeToUtf8Bytes(plan, JsonOptions));
    }

    public static StrategyPlan LoadPlan(string path)
    {
        var plan = Read<StrategyPlan>(path);
        plan.Validate();
        return plan;
    }

    public static void SaveCheckpoint(string path, StrategyCheckpointArtifact artifact, GameContent content)
    {
        ValidateCheckpointArtifact(artifact, content);
        WriteAtomically(path, JsonSerializer.SerializeToUtf8Bytes(artifact, JsonOptions));
    }

    public static StrategyCheckpointArtifact LoadCheckpoint(string path, GameContent content)
    {
        var artifact = Read<StrategyCheckpointArtifact>(path);
        ValidateCheckpointArtifact(artifact, content);
        return artifact;
    }

    public static void SaveSearchResult(string path, CheckpointSearchResult result, GameContent content)
    {
        ValidateSearchResult(result, content);
        WriteAtomically(path, JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions));
    }

    public static CheckpointSearchResult LoadSearchResult(string path, GameContent content)
    {
        var result = Read<CheckpointSearchResult>(path);
        ValidateSearchResult(result, content);
        return result;
    }

    public static string Fingerprint(SaveGameData checkpoint)
    {
        var canonical = JsonSerializer.SerializeToNode(checkpoint, JsonOptions)?.AsObject() ??
            throw new InvalidDataException("Checkpoint could not be serialized for fingerprinting.");
        canonical.Remove("savedAtUtc");
        canonical.Remove("runId");
        var payload = JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    internal static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static T Read<T>(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Strategy artifact was not found.", fullPath);
        var length = new FileInfo(fullPath).Length;
        if (length <= 0 || length > MaximumArtifactBytes)
            throw new InvalidDataException("Strategy artifact size is invalid.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), JsonOptions) ??
            throw new InvalidDataException("Strategy artifact is empty or malformed.");
    }

    private static void ValidateCheckpointArtifact(StrategyCheckpointArtifact artifact, GameContent content)
    {
        if (artifact.SchemaVersion != StrategyCheckpointArtifact.CurrentSchemaVersion)
            throw new InvalidDataException($"Strategy checkpoint schema {artifact.SchemaVersion} is not supported.");
        if (string.IsNullOrWhiteSpace(artifact.StrategyArtifactId) || artifact.StrategyArtifactId.Length > 128)
            throw new InvalidDataException("Strategy checkpoint artifact ID is invalid.");
        if (artifact.Checkpoint is null)
            throw new InvalidDataException("Strategy checkpoint payload is missing.");
        var restored = GameSession.RestoreSaveGame(content, artifact.Checkpoint);
        if (!restored.CanSaveCheckpoint)
            throw new InvalidDataException("Strategy checkpoints must describe an inter-wave campaign state.");
        var fingerprint = Fingerprint(artifact.Checkpoint);
        if (!fingerprint.Equals(artifact.CheckpointFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Strategy checkpoint fingerprint does not match its payload.");
    }

    private static void ValidateSearchResult(CheckpointSearchResult result, GameContent content)
    {
        if (result.SchemaVersion != CheckpointSearchResult.CurrentSchemaVersion)
            throw new InvalidDataException($"Checkpoint search schema {result.SchemaVersion} is not supported.");
        if (result.TargetWave <= 0 || result.BeamWidth <= 0 || result.Evaluations < 0 ||
            result.SuccessfulEvaluations is null || result.RetainedStates is null ||
            result.CampaignCompletions is null || result.Failures is null)
            throw new InvalidDataException("Checkpoint search artifact has invalid summary fields.");
        if (result.Evaluations != result.SuccessfulEvaluations.Count + result.CampaignCompletions.Count + result.Failures.Count)
            throw new InvalidDataException("Checkpoint search artifact evaluation totals are inconsistent.");
        if (result.RetainedStates.Count > result.BeamWidth)
            throw new InvalidDataException("Checkpoint search artifact exceeds its recorded beam width.");

        foreach (var state in result.SuccessfulEvaluations.Select(success => success.State).Concat(result.RetainedStates))
        {
            if (state.Checkpoint.Waves.CurrentWaveNumber != result.TargetWave ||
                state.Strategy.FindWave(result.TargetWave) is null)
                throw new InvalidDataException("Checkpoint search state does not match its target wave.");
            state.Strategy.ValidatePrefixForCheckpoint(state.Checkpoint);
            var fingerprint = Fingerprint(state.Checkpoint);
            if (!fingerprint.Equals(state.CheckpointFingerprint, StringComparison.Ordinal) ||
                CheckpointBeamOptimizer.Rank(content, state.Checkpoint) != state.Score)
                throw new InvalidDataException("Checkpoint search state identity or score is invalid.");
        }

        foreach (var success in result.SuccessfulEvaluations)
            if (success.WavePlan.Wave != result.TargetWave || !success.Simulation.Won)
                throw new InvalidDataException("Checkpoint search success does not match its target wave.");
        foreach (var completion in result.CampaignCompletions)
        {
            completion.Strategy.Validate();
            if (completion.WavePlan.Wave != result.TargetWave || !completion.Simulation.CampaignCleared)
                throw new InvalidDataException("Checkpoint search campaign completion is invalid.");
        }
        foreach (var failure in result.Failures)
        {
            if (failure.WavePlan.Wave != result.TargetWave)
                throw new InvalidDataException("Checkpoint search failure does not match its target wave.");
            if (failure.FailureMargin is { } margin &&
                margin.TotalEnemyCount != failure.RemainingEnemies.Sum(enemy => enemy.Count) +
                                          failure.QueuedEnemies.Sum(enemy => enemy.Count))
                throw new InvalidDataException("Checkpoint search failure composition is inconsistent.");
        }
    }

    private static void WriteAtomically(string path, byte[] payload)
    {
        if (payload.LongLength > MaximumArtifactBytes)
            throw new InvalidDataException("Strategy artifact exceeds the supported size limit.");
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidDataException("Strategy artifact path has no directory.");
        Directory.CreateDirectory(directory);
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
}

internal static class StrategySimulationOptions
{
    public static SimulationOptions ForWave(
        SimulationOptions source,
        WavePlan wavePlan,
        AutoPlayerStrategy? defaultStrategy = null) => new()
    {
        Seed = wavePlan.DecisionSeed,
        Strategy = wavePlan.StrategyOverride ?? defaultStrategy ?? source.Strategy,
        MapId = source.MapId,
        DifficultyId = source.DifficultyId,
        ChallengeId = source.ChallengeId,
        StepSeconds = source.StepSeconds,
        MaximumSimulatedSeconds = source.MaximumSimulatedSeconds,
        MaximumWave = wavePlan.Wave,
        ContinueEndless = false,
        ForcedTowerId = source.ForcedTowerId,
        ForcedDoctrineId = source.ForcedDoctrineId,
        ForcedSpecializationId = source.ForcedSpecializationId,
        UseProtocols = source.UseProtocols,
        UseApexUpgrades = source.UseApexUpgrades,
        UseCounterSupport = source.UseCounterSupport,
        UseCounterAttackers = source.UseCounterAttackers,
        HoldBuild = source.HoldBuild,
        HoldFootprint = source.HoldFootprint,
        WavePlan = wavePlan
    };
}
