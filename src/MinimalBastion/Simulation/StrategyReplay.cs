using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalBastion.Core;
using MinimalBastion.Data;

namespace MinimalBastion.Simulation;

public sealed record StrategyReplayEnvelope
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required StrategyPlan Plan { get; init; }
    public required string ExpectedPlanFingerprint { get; init; }
    public required string ContentBuildFingerprint { get; init; }
    public required CampaignSimulationSettings SimulationSettings { get; init; }
    public required string ReplayFingerprint { get; init; }

    public static StrategyReplayEnvelope Create(
        GameContent content,
        StrategyPlan plan,
        CampaignSimulationSettings simulationSettings) => Create(
            plan,
            MinimalBastion.Multiplayer.BuildFingerprint.Compute(content),
            simulationSettings);

    public static StrategyReplayEnvelope Create(
        StrategyPlan plan,
        string contentBuildFingerprint,
        CampaignSimulationSettings simulationSettings) => Create(
            plan,
            StrategyReplayValidation.Fingerprint(plan),
            contentBuildFingerprint,
            simulationSettings);

    public static StrategyReplayEnvelope Create(
        StrategyPlan plan,
        string expectedPlanFingerprint,
        string contentBuildFingerprint,
        CampaignSimulationSettings simulationSettings)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(simulationSettings);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPlanFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentBuildFingerprint);
        var envelope = new StrategyReplayEnvelope
        {
            Plan = plan,
            ExpectedPlanFingerprint = expectedPlanFingerprint,
            ContentBuildFingerprint = contentBuildFingerprint,
            SimulationSettings = simulationSettings,
            ReplayFingerprint = ""
        };
        return envelope with { ReplayFingerprint = StrategyReplayValidation.ReplayFingerprint(envelope) };
    }

    public void Validate(GameContent content) => StrategyReplayValidation.ValidateEnvelope(this, content);
}

public sealed class StrategyReplayWaveDeltas
{
    public required int StartingCredits { get; init; }
    public required int CreditsEarned { get; init; }
    public required int CreditsSpent { get; init; }
    public required int SaleCreditsRecovered { get; init; }
    public required int EarlyStartCreditsEarned { get; init; }
    public required int EndingCredits { get; init; }
    public required int UnspentCreditChange { get; init; }
    public required int StartingLives { get; init; }
    public required int EndingLives { get; init; }
    public required int Kills { get; init; }
    public required int Leaks { get; init; }
    public required int TowerPurchases { get; init; }
    public required int TowerUpgrades { get; init; }
    public required int ApexUpgrades { get; init; }
    public required int TowerSales { get; init; }
    public required int PulsePlateDeployments { get; init; }
    public required int EmergencyDeployments { get; init; }
    public required int EmergencyDirectPurchases { get; init; }
    public required int EmergencyTriggers { get; init; }
    public required int EmergencyHits { get; init; }
    public required int EmergencyKills { get; init; }
    public required float EmergencyDamage { get; init; }
    public required int GeneratorPurchases { get; init; }
    public required int GeneratorUpgrades { get; init; }
    public required int GeneratedCharges { get; init; }
    public required int Overdrives { get; init; }
    public required int ProtocolActivations { get; init; }
}

public sealed class StrategyReplayWaveResult
{
    public required WavePlan WavePlan { get; init; }
    public required SimulationRunResult Simulation { get; init; }
    public required StrategyReplayWaveDeltas Deltas { get; init; }
    public required bool Succeeded { get; init; }
}

public sealed class StrategyReplayResult
{
    public required int EnvelopeSchemaVersion { get; init; }
    public required string StrategyArtifactId { get; init; }
    public required string StrategyFingerprint { get; init; }
    public required string ContentBuildFingerprint { get; init; }
    public required string ReplayFingerprint { get; init; }
    public required CampaignSimulationSettings SimulationSettings { get; init; }
    public required string MapId { get; init; }
    public required string DifficultyId { get; init; }
    public required string ChallengeId { get; init; }
    public required int BaseSeed { get; init; }
    public required string Result { get; init; }
    public required int StartingWave { get; init; }
    public required int WaveReached { get; init; }
    public required int CompletedWaveCount { get; init; }
    public required bool CampaignCleared { get; init; }
    public int? FailedWave { get; init; }
    public required IReadOnlyList<StrategyReplayWaveResult> WaveRuns { get; init; }

    [JsonIgnore]
    public SimulationRunResult? FinalSimulation => WaveRuns.LastOrDefault()?.Simulation;
}

public static class StrategyReplayValidation
{
    public const string SupportedPolicyId = "experienced-search";

    private static readonly IReadOnlySet<string> EconomyProfiles =
        new HashSet<string>(["balanced", "mature", "invest", "apex", "reserve"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> PlacementProfiles =
        new HashSet<string>(["coverage", "nodes", "clusters", "precise", "explore"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> TargetingProfiles =
        new HashSet<string>(["split", "armored", "support", "strongest", "first"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> TacticalProfiles =
        new HashSet<string>(["adaptive", "plates", "protocols", "conserve"], StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ParameterRule> ParameterRules =
        new Dictionary<string, ParameterRule>(StringComparer.Ordinal)
        {
            ["purchaseBias"] = new(0.2, 3),
            ["upgradeBias"] = new(0.2, 3),
            ["reserveMultiplier"] = new(0, 4),
            ["reserveCredits"] = new(0, 2_000, true),
            ["coverageWeight"] = new(0, 4),
            ["nodeWeight"] = new(0, 4),
            ["clusterWeight"] = new(0, 4),
            ["plateProgressOffset"] = new(-0.25, 0.25),
            ["plateClusterWeight"] = new(0, 4),
            ["plateLeadWeight"] = new(0, 6),
            ["activePlateLimit"] = new(0, 12, true),
            ["directPlateLimit"] = new(0, 16, true),
            ["protocolMinimumEnemies"] = new(1, 30, true),
            ["protocolSupportBias"] = new(0.25, 6),
            ["signalSupportExitProgress"] = new(0.5, 0.98),
            ["signalSupportTier"] = new(0, 6, true),
            ["cleanupArmoredCount"] = new(0, 5, true),
            ["cleanupArmoredOffset"] = new(0, 4, true),
            ["cleanupSupportTier"] = new(0, 6, true),
            ["openingSupportExitProgress"] = new(0, 0.6),
            ["openingSignalSupportTier"] = new(0, 6, true),
            ["escapeFrostFirstCount"] = new(0, 7, true),
            ["frostEscapeProgress"] = new(0.65, 0.98),
            ["plateEscapeProgress"] = new(0.5, 0.98),
            ["plateSaleProgress"] = new(0.5, 0.98),
            ["plateSaleMaxLevel"] = new(0, 2, true),
            ["plateSaleMinimumDirectPurchases"] = new(0, 16, true),
            ["finalPlateReserve"] = new(0, 1, true),
            ["finalRoleFill"] = new(0, 1, true),
            ["apexLimit"] = new(0, 4, true),
            ["apexWave"] = new(GameConstants.ApexUnlockWave, GameConstants.CampaignWaveCount, true),
            ["apexCandidate"] = new(0, 63, true),
            ["saleLimit"] = new(0, 4, true)
        };

    public static void ValidateCompleteCampaign(this StrategyPlan plan, int totalWaves)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        if (totalWaves <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalWaves), "A campaign must contain at least one wave.");
        if (!Enum.IsDefined(plan.DefaultStrategy))
            throw new InvalidDataException($"Strategy '{plan.ArtifactId}' has an invalid default player policy.");
        if (plan.Waves.Count != totalWaves)
            throw new InvalidDataException(
                $"Strategy '{plan.ArtifactId}' must contain exactly {totalWaves} campaign wave decisions.");

        for (var index = 0; index < totalWaves; index++)
        {
            var wavePlan = plan.Waves[index];
            var expectedWave = index + 1;
            if (wavePlan.Wave != expectedWave)
                throw new InvalidDataException(
                    $"Strategy '{plan.ArtifactId}' must contain one contiguous decision for campaign wave {expectedWave}.");
            if (!wavePlan.PolicyId.Equals(SupportedPolicyId, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Strategy wave {expectedWave} uses unsupported policy '{wavePlan.PolicyId}'.");
            if (wavePlan.StrategyOverride is { } strategyOverride && !Enum.IsDefined(strategyOverride))
                throw new InvalidDataException($"Strategy wave {expectedWave} has an invalid player policy override.");
            ValidateProfile(EconomyProfiles, wavePlan.EconomyProfileId, "economy", expectedWave);
            ValidateProfile(PlacementProfiles, wavePlan.PlacementProfileId, "placement", expectedWave);
            ValidateProfile(TargetingProfiles, wavePlan.TargetingProfileId, "targeting", expectedWave);
            ValidateProfile(TacticalProfiles, wavePlan.TacticalProfileId, "tactical", expectedWave);
            foreach (var parameter in wavePlan.Parameters)
            {
                if (!ParameterRules.TryGetValue(parameter.Key, out var rule))
                    throw new InvalidDataException(
                        $"Strategy wave {expectedWave} uses unsupported parameter '{parameter.Key}'.");
                if (parameter.Value < rule.Minimum || parameter.Value > rule.Maximum ||
                    rule.IntegerOnly && parameter.Value != Math.Truncate(parameter.Value))
                    throw new InvalidDataException(
                        $"Strategy wave {expectedWave} parameter '{parameter.Key}' is outside its operational range.");
            }
        }
    }

    public static string Fingerprint(StrategyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        var canonical = plan with
        {
            Waves = plan.Waves.Select(wave => wave with
            {
                Parameters = new SortedDictionary<string, double>(
                    wave.Parameters.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
            }).ToArray(),
            Metadata = new SortedDictionary<string, string>(
                plan.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                StringComparer.Ordinal)
        };
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(canonical, StrategyArtifactStore.CreateJsonOptions()));
    }

    public static string ReplayFingerprint(StrategyReplayEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var identity = new ReplayIdentity(
            envelope.SchemaVersion,
            envelope.ExpectedPlanFingerprint.ToLowerInvariant(),
            envelope.ContentBuildFingerprint.ToLowerInvariant(),
            envelope.SimulationSettings);
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(identity, StrategyArtifactStore.CreateJsonOptions()));
    }

    internal static int ValidateEnvelope(StrategyReplayEnvelope envelope, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(content);
        if (envelope.SchemaVersion != StrategyReplayEnvelope.CurrentSchemaVersion)
            throw new InvalidDataException($"Strategy replay envelope schema {envelope.SchemaVersion} is not supported.");
        if (envelope.Plan is null || envelope.SimulationSettings is null)
            throw new InvalidDataException("Strategy replay envelope is missing its plan or simulation settings.");

        var planFingerprint = Fingerprint(envelope.Plan);
        if (!MinimalBastion.Multiplayer.BuildFingerprint.IsValid(envelope.ExpectedPlanFingerprint) ||
            !envelope.ExpectedPlanFingerprint.Equals(planFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Strategy replay plan fingerprint does not match its payload.");
        var buildFingerprint = MinimalBastion.Multiplayer.BuildFingerprint.Compute(content);
        if (!MinimalBastion.Multiplayer.BuildFingerprint.IsValid(envelope.ContentBuildFingerprint) ||
            !envelope.ContentBuildFingerprint.Equals(buildFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Strategy replay content was produced by a different balance/build.");
        ValidateSettings(envelope.SimulationSettings, content);
        if (!MinimalBastion.Multiplayer.BuildFingerprint.IsValid(envelope.ReplayFingerprint) ||
            !envelope.ReplayFingerprint.Equals(ReplayFingerprint(envelope), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Strategy replay fingerprint does not match its plan, build, and settings.");

        var plan = envelope.Plan;
        var waveCount = ResolveCampaignWaveCount(content, plan);
        plan.ValidateCompleteCampaign(waveCount);
        return waveCount;
    }

    private static void ValidateSettings(CampaignSimulationSettings settings, GameContent content)
    {
        if (!float.IsFinite(settings.StepSeconds) || settings.StepSeconds is < 0.01f or > 0.1f)
            throw new InvalidDataException("Strategy replay step seconds must be within 0.01 through 0.1.");
        if (!float.IsFinite(settings.MaximumSimulatedSeconds) || settings.MaximumSimulatedSeconds <= 0)
            throw new InvalidDataException("Strategy replay timeout must be finite and positive.");
        if (settings.ForcedTowerId is null)
        {
            if (settings.ForcedDoctrineId is not null || settings.ForcedSpecializationId is not null)
                throw new InvalidDataException("A forced replay build path requires a tower ID.");
            return;
        }
        if (!content.Towers.TryGetValue(settings.ForcedTowerId, out var tower))
            throw new InvalidDataException($"Strategy replay references unknown tower '{settings.ForcedTowerId}'.");
        if (settings.ForcedDoctrineId is { } doctrineId &&
            tower.Tier2Doctrines.All(doctrine => !doctrine.Id.Equals(doctrineId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Strategy replay references unknown doctrine '{doctrineId}'.");
        if (settings.ForcedSpecializationId is { } specializationId &&
            tower.Specializations.All(specialization =>
                !specialization.Id.Equals(specializationId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Strategy replay references unknown specialization '{specializationId}'.");
    }

    private static void ValidateProfile(
        IReadOnlySet<string> supported,
        string profileId,
        string kind,
        int wave)
    {
        if (!supported.Contains(profileId))
            throw new InvalidDataException($"Strategy wave {wave} uses unsupported {kind} profile '{profileId}'.");
    }

    private static int ResolveCampaignWaveCount(GameContent content, StrategyPlan plan)
    {
        MapDefinition map;
        if (!content.Maps.TryGetValue(plan.MapId, out map!))
        {
            if (content.Maps.Count != 0 ||
                !content.Map.Id.Equals(plan.MapId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Strategy replay references an unavailable map.");
            map = content.Map;
        }

        WaveSetDefinition waveSet;
        if (!content.WaveSets.TryGetValue(map.WaveSet, out waveSet!))
        {
            if (content.WaveSets.Count != 0 ||
                !string.IsNullOrWhiteSpace(map.WaveSet) &&
                !content.Waves.Id.Equals(map.WaveSet, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Strategy replay references an unavailable campaign wave set.");
            waveSet = content.Waves;
        }

        if (!content.Difficulties.ContainsKey(plan.DifficultyId) &&
            (content.Difficulties.Count != 0 ||
             !plan.DifficultyId.Equals(DifficultyCatalog.LegacyId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Strategy replay references an unavailable difficulty.");
        if (!content.Challenges.ContainsKey(plan.ChallengeId) &&
            (content.Challenges.Count != 0 ||
             !plan.ChallengeId.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Strategy replay references an unavailable challenge.");
        return waveSet.Waves.Count;
    }

    private static string Sha256(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private sealed record ParameterRule(double Minimum, double Maximum, bool IntegerOnly = false);
    private sealed record ReplayIdentity(
        int SchemaVersion,
        string PlanFingerprint,
        string ContentBuildFingerprint,
        CampaignSimulationSettings SimulationSettings);
}
