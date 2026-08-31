using System.Security.Cryptography;
using System.Text;
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

public sealed record CampaignRecoveryArchiveLayerState(
    int CompletedWave,
    IReadOnlyList<CheckpointSearchState> Alternatives,
    IReadOnlyList<string> ExcludedStrategicFingerprints);

public sealed record CampaignSearchResumeState(
    CampaignSearchManifest Manifest,
    IReadOnlyList<CheckpointSearchState> Frontier,
    IReadOnlyList<CheckpointSearchState> PendingFrontier,
    IReadOnlyList<CampaignRecoveryArchiveLayerState> RecoveryArchive,
    IReadOnlyList<string> EvaluatedConfigurationFingerprints);

public sealed class CampaignSearchOptions
{
    public int BaseSeed { get; init; } = 1337;
    public int BeamWidth { get; init; } = 3;
    public int CandidateCount { get; init; } = 6;
    public int MaximumWave { get; init; } = GameConstants.CampaignWaveCount;
    public int BroadeningRounds { get; init; } = 1;
    public int StartingBroadeningRound { get; init; }
    public int InProgressWave { get; init; }
    public int BacktrackDepth { get; init; } = 2;
    public int MaximumRecoveryAttempts { get; init; } = 8;
    public int RecoveryAttemptOffset { get; init; }
    public string PolicyId { get; init; } = "experienced-search";
    public IReadOnlyList<string> BundleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, double> ParameterOverrides { get; init; } =
        new SortedDictionary<string, double>(StringComparer.Ordinal);
    public IReadOnlyList<string> PreviouslyEvaluatedConfigurationFingerprints { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CheckpointSearchState> PendingFrontier { get; init; } = Array.Empty<CheckpointSearchState>();
    public IReadOnlyList<CampaignRecoveryArchiveLayerState> RecoveryArchive { get; init; } =
        Array.Empty<CampaignRecoveryArchiveLayerState>();
    public CampaignSearchManifest? ResumeManifest { get; init; }
    public string? ArtifactDirectory { get; init; }

    public void Validate()
    {
        if (BeamWidth is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(BeamWidth));
        if (CandidateCount is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(CandidateCount));
        if (MaximumWave <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumWave));
        if (BroadeningRounds is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(BroadeningRounds));
        if (StartingBroadeningRound is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(StartingBroadeningRound));
        if (InProgressWave < 0) throw new ArgumentOutOfRangeException(nameof(InProgressWave));
        if (BacktrackDepth is < 0 or > GameConstants.CampaignWaveCount)
            throw new ArgumentOutOfRangeException(nameof(BacktrackDepth));
        if (MaximumRecoveryAttempts is < 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(MaximumRecoveryAttempts));
        if (RecoveryAttemptOffset is < 0 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(RecoveryAttemptOffset));
        if (string.IsNullOrWhiteSpace(PolicyId) || PolicyId.Length > 128)
            throw new InvalidDataException("Campaign search policy ID is invalid.");
        if (BundleIds is null || ParameterOverrides is null || PreviouslyEvaluatedConfigurationFingerprints is null ||
            PendingFrontier is null || RecoveryArchive is null)
            throw new InvalidDataException("Campaign search candidate configuration is missing.");
        if (ParameterOverrides.Count > 64 || ParameterOverrides.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 128 || !double.IsFinite(entry.Value) ||
                !CampaignWavePlanGenerator.SupportedParameterNames.Contains(entry.Key)))
            throw new InvalidDataException("Campaign search parameter overrides are invalid.");
        if (PreviouslyEvaluatedConfigurationFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            PreviouslyEvaluatedConfigurationFingerprints.Count ||
            PreviouslyEvaluatedConfigurationFingerprints.Any(fingerprint => !IsSha256(fingerprint)))
            throw new InvalidDataException("Campaign search evaluated-configuration fingerprints are invalid.");
        if ((PendingFrontier.Count > 0 || InProgressWave > 0) && StartingBroadeningRound > BroadeningRounds)
            throw new ArgumentOutOfRangeException(nameof(StartingBroadeningRound),
                "An in-progress campaign wave cannot resume beyond its final broadening round.");
        if (PendingFrontier.Count > 0 && InProgressWave == 0)
            throw new InvalidDataException("A pending campaign frontier requires an in-progress wave marker.");
    }

    internal static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
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
    CheckpointWaveFailure? BestFailure)
{
    public int RecoveryAttempt { get; init; }
}

public sealed record CampaignFrontierArtifact(
    string CheckpointFingerprint,
    string CheckpointPath,
    string StrategyPath)
{
    public string StrategicFingerprint { get; init; } = "";
    public string StrategyFingerprint { get; init; } = "";
    public string StateFingerprint { get; init; } = "";
    public string CheckpointContentHash { get; init; } = "";
    public string StrategyContentHash { get; init; } = "";
}

public sealed record CampaignRecoveryStateArtifact(
    string StateFingerprint,
    string CheckpointFingerprint,
    string DecisionFingerprint,
    string CheckpointPath,
    string StrategyPath)
{
    public string StrategicFingerprint { get; init; } = "";
    public string StrategyFingerprint { get; init; } = "";
    public string CheckpointContentHash { get; init; } = "";
    public string StrategyContentHash { get; init; } = "";
}

public sealed record CampaignEvaluationIdentityArtifact(
    string Path,
    string ContentHash,
    int Count);

public sealed record CampaignRecoveryArchiveArtifact(
    int CompletedWave,
    int RemainingStateCount,
    int DistinctCheckpointCount,
    int DistinctDecisionCount,
    int ExcludedStateCount,
    IReadOnlyList<string> ExcludedCheckpointFingerprints,
    IReadOnlyList<CampaignRecoveryStateArtifact> States)
{
    public int DistinctStrategicCount { get; init; }
    public IReadOnlyList<string> ExcludedStrategicFingerprints { get; init; } = Array.Empty<string>();
}

public sealed record CampaignRecoveryArtifact(
    int Attempt,
    int BlockingWave,
    int RecoveredWave,
    int Depth,
    IReadOnlyList<string> CheckpointFingerprints,
    IReadOnlyList<CampaignFrontierArtifact> FrontierArtifacts);

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
    public const int CurrentSchemaVersion = 5;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ArtifactId { get; init; }
    public required string MapId { get; init; }
    public required string DifficultyId { get; init; }
    public required string ChallengeId { get; init; }
    public required int BaseSeed { get; init; }
    public string? BuildFingerprint { get; init; }
    public AutoPlayerStrategy? DefaultStrategy { get; init; }
    public required int StartingWave { get; init; }
    public required int LastCompletedWave { get; init; }
    public required int MaximumWave { get; init; }
    public required int BeamWidth { get; init; }
    public required int CandidateCount { get; init; }
    public required int BroadeningRounds { get; init; }
    public int BacktrackDepth { get; init; } = 2;
    public int MaximumRecoveryAttempts { get; init; } = 8;
    public int RecoveryAttemptOffset { get; init; }
    public required string PolicyId { get; init; }
    public required IReadOnlyList<string> BundleIds { get; init; }
    public required IReadOnlyDictionary<string, double> ParameterOverrides { get; init; }
    public required CampaignSimulationSettings SimulationSettings { get; init; }
    public required int NextBroadeningRound { get; init; }
    public required int TotalEvaluations { get; init; }
    public required CampaignSearchStatus Status { get; init; }
    public required IReadOnlyList<CampaignSearchWaveArtifact> WaveAttempts { get; init; }
    public required IReadOnlyList<CampaignFrontierArtifact> FrontierArtifacts { get; init; }
    public int InProgressWave { get; init; }
    public int PendingWave { get; init; }
    public IReadOnlyList<CampaignFrontierArtifact> PendingFrontierArtifacts { get; init; } =
        Array.Empty<CampaignFrontierArtifact>();
    public IReadOnlyList<CampaignRecoveryArchiveArtifact> RecoveryArchive { get; init; } =
        Array.Empty<CampaignRecoveryArchiveArtifact>();
    public IReadOnlyList<CampaignRecoveryArtifact> RecoveryAttempts { get; init; } =
        Array.Empty<CampaignRecoveryArtifact>();
    public IReadOnlyList<string> EvaluatedConfigurationFingerprints { get; init; } = Array.Empty<string>();
    public CampaignEvaluationIdentityArtifact? EvaluationIdentityArtifact { get; init; }
    public CheckpointWaveFailure? BestFailure { get; init; }
    public string? FinalStrategyPath { get; init; }
    public string? FinalStrategyFingerprint { get; init; }
    public string? FinalStrategyContentHash { get; init; }
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
    private sealed record ParameterMutationDomain(string Name, IReadOnlyList<double> Values, int Stride);

    public static readonly IReadOnlySet<string> SupportedParameterNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "purchaseBias", "upgradeBias", "reserveMultiplier", "reserveCredits", "coverageWeight", "nodeWeight",
        "clusterWeight", "plateProgressOffset", "plateClusterWeight", "plateLeadWeight", "activePlateLimit",
        "directPlateLimit", "protocolMinimumEnemies",
        "protocolSupportBias", "signalSupportExitProgress", "signalSupportTier", "cleanupArmoredCount",
        "cleanupArmoredOffset", "cleanupSupportTier",
        "openingSupportExitProgress", "openingSignalSupportTier", "escapeFrostFirstCount", "frostEscapeProgress",
        "plateEscapeProgress", "plateSaleProgress", "plateSaleMaxLevel", "plateSaleMinimumDirectPurchases",
        "finalPlateReserve", "finalRoleFill", "apexLimit",
        "apexWave", "apexCandidate", "saleLimit"
    };

    public static readonly IReadOnlyList<CampaignCandidateBundle> DefaultBundles =
    [
        Bundle("balanced-coverage", "balanced", "coverage", "split", "adaptive",
            ("purchaseBias", 1.0), ("upgradeBias", 1.0), ("reserveMultiplier", 1.0),
            ("coverageWeight", 1.2), ("nodeWeight", 1.15), ("clusterWeight", 1.0),
            ("plateProgressOffset", 0.0), ("plateClusterWeight", 1.0), ("plateLeadWeight", 1.0),
            ("activePlateLimit", 6), ("directPlateLimit", 4),
            ("protocolMinimumEnemies", 5), ("protocolSupportBias", 1.0), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 0), ("signalSupportExitProgress", 0.72), ("signalSupportTier", 2),
            ("cleanupArmoredCount", 1), ("cleanupArmoredOffset", 0), ("cleanupSupportTier", 2),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 1), ("frostEscapeProgress", 0.86), ("plateEscapeProgress", 0.98),
            ("plateSaleProgress", 0.78), ("plateSaleMaxLevel", 1), ("plateSaleMinimumDirectPurchases", 0),
            ("finalRoleFill", 1), ("openingSupportExitProgress", 0),
            ("openingSignalSupportTier", 2),
            ("saleLimit", 1)),
        Bundle("mature-nodes", "mature", "nodes", "armored", "plates",
            ("purchaseBias", 0.82), ("upgradeBias", 1.28), ("reserveMultiplier", 0.85),
            ("coverageWeight", 1.05), ("nodeWeight", 1.5), ("clusterWeight", 0.9),
            ("plateProgressOffset", -0.06), ("plateClusterWeight", 1.6), ("plateLeadWeight", 1.8),
            ("activePlateLimit", 7), ("directPlateLimit", 5),
            ("protocolMinimumEnemies", 5), ("protocolSupportBias", 1.0), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 1), ("signalSupportExitProgress", 0.78), ("signalSupportTier", 5),
            ("cleanupArmoredCount", 4), ("cleanupArmoredOffset", 1), ("cleanupSupportTier", 1),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 2), ("frostEscapeProgress", 0.82), ("plateEscapeProgress", 0.94),
            ("plateSaleProgress", 0.75), ("plateSaleMaxLevel", 1), ("plateSaleMinimumDirectPurchases", 1),
            ("finalRoleFill", 1), ("openingSupportExitProgress", 0.15),
            ("openingSignalSupportTier", 5),
            ("saleLimit", 1)),
        Bundle("invest-clusters", "invest", "clusters", "support", "protocols",
            ("purchaseBias", 0.92), ("upgradeBias", 1.15), ("reserveMultiplier", 0.72),
            ("coverageWeight", 1.25), ("nodeWeight", 1.05), ("clusterWeight", 1.45),
            ("plateProgressOffset", 0.03), ("plateClusterWeight", 3.0), ("plateLeadWeight", 0.75),
            ("activePlateLimit", 5), ("directPlateLimit", 3),
            ("protocolMinimumEnemies", 3), ("protocolSupportBias", 1.4), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 2), ("signalSupportExitProgress", 0.68), ("signalSupportTier", 6),
            ("cleanupArmoredCount", 2), ("cleanupArmoredOffset", 2), ("cleanupSupportTier", 6),
            ("finalPlateReserve", 0),
            ("escapeFrostFirstCount", 1), ("frostEscapeProgress", 0.9), ("plateEscapeProgress", 0.9),
            ("plateSaleProgress", 0.7), ("plateSaleMaxLevel", 1), ("plateSaleMinimumDirectPurchases", 3),
            ("finalRoleFill", 0), ("openingSupportExitProgress", 0.3),
            ("openingSignalSupportTier", 6),
            ("saleLimit", 1)),
        Bundle("apex-precise", "apex", "precise", "strongest", "conserve",
            ("purchaseBias", 0.7), ("upgradeBias", 1.35), ("reserveMultiplier", 1.3),
            ("reserveCredits", 180), ("coverageWeight", 1.3), ("nodeWeight", 1.25),
            ("clusterWeight", 1.1), ("plateProgressOffset", 0.08), ("plateClusterWeight", 0.5),
            ("plateLeadWeight", 4.5), ("activePlateLimit", 4), ("directPlateLimit", 2),
            ("protocolMinimumEnemies", 7), ("protocolSupportBias", 0.7),
            ("apexLimit", 1), ("apexWave", 21), ("apexCandidate", 3),
            ("signalSupportExitProgress", 0.82), ("signalSupportTier", 1), ("cleanupArmoredCount", 0),
            ("cleanupArmoredOffset", 0), ("cleanupSupportTier", 5),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 3), ("frostEscapeProgress", 0.78), ("plateEscapeProgress", 0.94),
            ("plateSaleProgress", 0.84), ("plateSaleMaxLevel", 0), ("plateSaleMinimumDirectPurchases", 4),
            ("finalRoleFill", 0), ("openingSupportExitProgress", 0.45),
            ("openingSignalSupportTier", 4),
            ("saleLimit", 2)),
        Bundle("reserve-explore", "reserve", "explore", "first", "conserve",
            ("purchaseBias", 0.9), ("upgradeBias", 1.05), ("reserveMultiplier", 1.55),
            ("reserveCredits", 240), ("coverageWeight", 1.0), ("nodeWeight", 1.0),
            ("clusterWeight", 0.85), ("plateProgressOffset", 0.1), ("plateClusterWeight", 0.0),
            ("plateLeadWeight", 5.5), ("activePlateLimit", 3), ("directPlateLimit", 1),
            ("protocolMinimumEnemies", 8), ("protocolSupportBias", 0.7),
            ("apexLimit", 1), ("apexWave", 30), ("apexCandidate", 4),
            ("signalSupportExitProgress", 0.98), ("signalSupportTier", 0), ("cleanupArmoredCount", 0),
            ("cleanupArmoredOffset", 0), ("cleanupSupportTier", 0),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 0), ("frostEscapeProgress", 0.98), ("plateEscapeProgress", 0.98),
            ("plateSaleProgress", 0.9), ("plateSaleMaxLevel", 0), ("plateSaleMinimumDirectPurchases", 7),
            ("finalRoleFill", 0), ("openingSupportExitProgress", 0.6),
            ("openingSignalSupportTier", 0),
            ("saleLimit", 2)),
        Bundle("plate-coverage", "balanced", "coverage", "first", "plates",
            ("purchaseBias", 0.95), ("upgradeBias", 1.08), ("reserveMultiplier", 0.78),
            ("coverageWeight", 1.35), ("nodeWeight", 1.2), ("clusterWeight", 1.05),
            ("plateProgressOffset", -0.1), ("plateClusterWeight", 4.0), ("plateLeadWeight", 2.5),
            ("activePlateLimit", 9), ("directPlateLimit", 7),
            ("protocolMinimumEnemies", 5), ("protocolSupportBias", 1.0), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 5), ("signalSupportExitProgress", 0.75), ("signalSupportTier", 3),
            ("cleanupArmoredCount", 3), ("cleanupArmoredOffset", 2), ("cleanupSupportTier", 3),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 4), ("frostEscapeProgress", 0.72), ("plateEscapeProgress", 0.82),
            ("plateSaleProgress", 0.62), ("plateSaleMaxLevel", 2), ("plateSaleMinimumDirectPurchases", 2),
            ("finalRoleFill", 1), ("openingSupportExitProgress", 0.3),
            ("openingSignalSupportTier", 3),
            ("saleLimit", 1)),
        Bundle("protocol-support", "invest", "clusters", "support", "protocols",
            ("purchaseBias", 1.0), ("upgradeBias", 1.1), ("reserveMultiplier", 0.7),
            ("coverageWeight", 1.2), ("nodeWeight", 1.1), ("clusterWeight", 1.35),
            ("plateProgressOffset", 0.04), ("plateClusterWeight", 2.4), ("plateLeadWeight", 1.25),
            ("activePlateLimit", 5), ("directPlateLimit", 2),
            ("protocolMinimumEnemies", 2), ("protocolSupportBias", 2.4), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 6), ("signalSupportExitProgress", 0.62), ("signalSupportTier", 4),
            ("cleanupArmoredCount", 1), ("cleanupArmoredOffset", 3), ("cleanupSupportTier", 6),
            ("finalPlateReserve", 0),
            ("escapeFrostFirstCount", 2), ("frostEscapeProgress", 0.86), ("plateEscapeProgress", 0.9),
            ("plateSaleProgress", 0.78), ("plateSaleMaxLevel", 1), ("plateSaleMinimumDirectPurchases", 6),
            ("finalRoleFill", 0), ("openingSupportExitProgress", 0.45),
            ("openingSignalSupportTier", 6),
            ("saleLimit", 1)),
        Bundle("armor-nodes", "mature", "nodes", "armored", "adaptive",
            ("purchaseBias", 0.86), ("upgradeBias", 1.22), ("reserveMultiplier", 0.9),
            ("coverageWeight", 1.1), ("nodeWeight", 1.55), ("clusterWeight", 0.95),
            ("plateProgressOffset", -0.02), ("plateClusterWeight", 0.8), ("plateLeadWeight", 3.5),
            ("activePlateLimit", 6), ("directPlateLimit", 4),
            ("protocolMinimumEnemies", 4), ("protocolSupportBias", 1.0), ("apexLimit", 1), ("apexWave", 30),
            ("apexCandidate", 7), ("signalSupportExitProgress", 0.5), ("signalSupportTier", 2),
            ("cleanupArmoredCount", 5), ("cleanupArmoredOffset", 4), ("cleanupSupportTier", 4),
            ("finalPlateReserve", 1),
            ("escapeFrostFirstCount", 5), ("frostEscapeProgress", 0.65), ("plateEscapeProgress", 0.75),
            ("plateSaleProgress", 0.5), ("plateSaleMaxLevel", 1), ("plateSaleMinimumDirectPurchases", 5),
            ("finalRoleFill", 1), ("openingSupportExitProgress", 0.15),
            ("openingSignalSupportTier", 5),
            ("saleLimit", 1))
    ];

    private static readonly IReadOnlyList<ParameterMutationDomain> MutationDomains =
    [
        new("apexCandidate", Enumerable.Range(0, 16).Select(value => (double)value).ToArray(), 5),
        new("signalSupportTier", Enumerable.Range(0, 7).Select(value => (double)value).ToArray(), 3),
        new("cleanupSupportTier", Enumerable.Range(0, 7).Select(value => (double)value).ToArray(), 5),
        new("cleanupArmoredCount", Enumerable.Range(0, 6).Select(value => (double)value).ToArray(), 5),
        new("cleanupArmoredOffset", Enumerable.Range(0, 5).Select(value => (double)value).ToArray(), 2),
        new("signalSupportExitProgress", [0.5, 0.58, 0.62, 0.68, 0.72, 0.75, 0.78, 0.82, 0.9, 0.98], 3),
        new("finalPlateReserve", [0, 1], 1),
        new("plateClusterWeight", [0, 0.5, 0.8, 1, 1.6, 2, 2.4, 3, 4], 4),
        new("plateLeadWeight", [0.5, 0.75, 1, 1.25, 1.8, 2.5, 3, 3.5, 4.5, 5.5], 7),
        new("plateProgressOffset", [-0.1, -0.06, -0.02, 0, 0.03, 0.04, 0.08, 0.1], 5),
        new("activePlateLimit", Enumerable.Range(3, 8).Select(value => (double)value).ToArray(), 3),
        new("directPlateLimit", Enumerable.Range(1, 10).Select(value => (double)value).ToArray(), 7),
        new("protocolMinimumEnemies", Enumerable.Range(2, 9).Select(value => (double)value).ToArray(), 5),
        new("protocolSupportBias", [0.7, 1, 1.4, 1.8, 2.4, 3], 5),
        new("apexWave", [21, 25, 30], 2),
        new("apexLimit", [1, 2], 1),
        new("saleLimit", [0, 1, 2, 3], 3),
        new("plateSaleMaxLevel", [0, 1, 2], 2),
        new("plateSaleMinimumDirectPurchases", Enumerable.Range(0, 8).Select(value => (double)value).ToArray(), 3),
        new("escapeFrostFirstCount", Enumerable.Range(0, 8).Select(value => (double)value).ToArray(), 3),
        new("frostEscapeProgress", [0.65, 0.72, 0.78, 0.82, 0.86, 0.9, 0.94, 0.98], 5),
        new("plateEscapeProgress", [0.5, 0.65, 0.75, 0.82, 0.9, 0.94, 0.98], 3),
        new("plateSaleProgress", [0.5, 0.62, 0.7, 0.78, 0.84, 0.9, 0.94, 0.98], 7),
        new("finalRoleFill", [0, 1], 1),
        new("openingSupportExitProgress", [0, 0.15, 0.3, 0.45, 0.6], 2),
        new("openingSignalSupportTier", Enumerable.Range(0, 7).Select(value => (double)value).ToArray(), 5)
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
            ApplyParameterMutations(parameters, options.BaseSeed, wave, broadeningRound, ordinal);
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

    private static void ApplyParameterMutations(
        IDictionary<string, double> parameters,
        int baseSeed,
        int wave,
        int broadeningRound,
        int ordinal)
    {
        var sampleIndex = checked(broadeningRound * 263 + ordinal);
        for (var dimension = 0; dimension < MutationDomains.Count; dimension++)
        {
            var domain = MutationDomains[dimension];
            var count = domain.Values.Count;
            var cycle = sampleIndex / count;
            var position = sampleIndex % count;
            var phase = MutationPhase(baseSeed, wave, dimension, cycle, count);
            var valueIndex = PositiveModulo(position * domain.Stride + phase, count);
            parameters[domain.Name] = domain.Values[valueIndex];
        }
    }

    private static int MutationPhase(int baseSeed, int wave, int dimension, int cycle, int count)
    {
        unchecked
        {
            var value = (uint)baseSeed;
            value ^= (uint)wave * 0x9E3779B9u;
            value ^= (uint)(dimension + 1) * 0x85EBCA6Bu;
            value ^= (uint)(cycle + 1) * 0xC2B2AE35u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            return (int)(value % (uint)count);
        }
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

internal delegate CheckpointSearchResult CampaignWaveEvaluator(
    GameContent content,
    IReadOnlyList<CheckpointSearchState> frontier,
    IReadOnlyList<WavePlan> candidates,
    SimulationOptions options,
    int beamWidth);

public static class CampaignStrategyOptimizer
{
    private const int MaximumPersistedFailureTraces = 8;
    private const int MaximumPersistedCompletionTraces = 1;

    private sealed record PersistedStrategyArtifact(
        string Path,
        string StrategyFingerprint,
        string ContentHash);

    private sealed class RecoveryLayer(
        int completedWave,
        IReadOnlyList<CheckpointSearchState> alternatives,
        IEnumerable<string>? excludedStrategicFingerprints = null)
    {
        public int CompletedWave { get; } = completedWave;
        public List<CheckpointSearchState> Alternatives { get; } = alternatives.ToList();
        public HashSet<string> ExcludedStrategicFingerprints { get; } = new(
            excludedStrategicFingerprints ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static CampaignSearchRunResult Search(
        GameContent content,
        IReadOnlyList<CheckpointSearchState> initialFrontier,
        SimulationOptions simulationOptions,
        CampaignSearchOptions searchOptions,
        StrategyPlan? preferredPlan = null) => Search(
        content,
        initialFrontier,
        simulationOptions,
        searchOptions,
        preferredPlan,
        CheckpointBeamOptimizer.EvaluateWave);

    internal static CampaignSearchRunResult Search(
        GameContent content,
        IReadOnlyList<CheckpointSearchState> initialFrontier,
        SimulationOptions simulationOptions,
        CampaignSearchOptions searchOptions,
        StrategyPlan? preferredPlan,
        CampaignWaveEvaluator evaluator)
    {
        searchOptions.Validate();
        if (initialFrontier.Count == 0)
            throw new ArgumentException("Campaign search requires at least one checkpoint state.", nameof(initialFrontier));
        ArgumentNullException.ThrowIfNull(evaluator);

        var startingWave = initialFrontier[0].Checkpoint.Waves.CurrentWaveNumber;
        var referenceStrategy = initialFrontier[0].Strategy;
        var resumeManifest = searchOptions.ResumeManifest;
        if (initialFrontier.Any(state => state.Checkpoint.Waves.CurrentWaveNumber != startingWave))
            throw new ArgumentException("Campaign search frontier states must be at the same wave.", nameof(initialFrontier));
        foreach (var state in initialFrontier)
        {
            ValidateResumableState(content, state, referenceStrategy, searchOptions.BaseSeed, startingWave,
                "frontier");
        }
        var rankedInitial = RankDistinctStates(content, initialFrontier);
        if (resumeManifest is not null)
        {
            if (resumeManifest.SchemaVersion >= 4 &&
                !MinimalBastion.Multiplayer.BuildFingerprint.Compute(content).Equals(
                    resumeManifest.BuildFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Campaign resume manifest was created by a different gameplay build.");
            ValidateResumeCompatibility(resumeManifest, referenceStrategy, simulationOptions, searchOptions,
                startingWave);
        }
        if (searchOptions.InProgressWave != 0 && searchOptions.InProgressWave != startingWave + 1)
            throw new InvalidDataException("The in-progress campaign wave does not follow the resume frontier.");
        IReadOnlyList<CheckpointSearchState> frontier = rankedInitial.Take(searchOptions.BeamWidth).ToArray();
        foreach (var state in searchOptions.PendingFrontier)
            ValidateResumableState(content, state, referenceStrategy, searchOptions.BaseSeed, startingWave + 1,
                "pending frontier");
        var pendingFrontier = RankDistinctStates(content, searchOptions.PendingFrontier);
        if (pendingFrontier.Count >= searchOptions.BeamWidth)
            throw new InvalidDataException("A pending campaign frontier must be smaller than the configured beam.");
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
        if (pendingFrontier.Count > 0 && startingWave >= maximumWave)
            throw new InvalidDataException("A pending campaign frontier exceeds the requested campaign boundary.");
        var attempts = resumeManifest?.WaveAttempts
            .Select(attempt => attempt with { TracePath = null })
            .ToList() ?? new List<CampaignSearchWaveArtifact>();
        var recoveryAttempts = resumeManifest?.RecoveryAttempts
            .Select(attempt => attempt with { FrontierArtifacts = Array.Empty<CampaignFrontierArtifact>() })
            .ToList() ?? new List<CampaignRecoveryArtifact>();
        var bestFailureSoFar = resumeManifest?.BestFailure;
        var archiveCapacity = checked(searchOptions.BeamWidth * searchOptions.MaximumRecoveryAttempts);
        var recoveryLayers = RestoreRecoveryLayers(
            content,
            searchOptions.RecoveryArchive,
            referenceStrategy,
            searchOptions.BaseSeed,
            archiveCapacity);
        ArchiveRecoveryLayer(content, recoveryLayers, startingWave, rankedInitial, frontier, archiveCapacity);
        var evaluatedConfigurations = new HashSet<string>(
            searchOptions.PreviouslyEvaluatedConfigurationFingerprints.Select(value => value.ToUpperInvariant()),
            StringComparer.Ordinal);
        var totalEvaluations = resumeManifest?.TotalEvaluations ?? 0;
        if (resumeManifest is not null && evaluatedConfigurations.Count != totalEvaluations)
            throw new InvalidDataException("Campaign resume evaluation identities do not match its evaluation total.");
        var artifactRoot = string.IsNullOrWhiteSpace(searchOptions.ArtifactDirectory)
            ? null
            : Path.GetFullPath(searchOptions.ArtifactDirectory);
        var frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{startingWave:D2}/resume", frontier);
        var pendingFrontierArtifacts = PersistFrontier(
            content,
            artifactRoot,
            $"wave-{startingWave + 1:D2}/pending-resume",
            pendingFrontier);
        var recoveryArchiveArtifacts = PersistRecoveryArchive(content, artifactRoot, recoveryLayers);
        var evaluationIdentityArtifact = PersistEvaluationIdentities(artifactRoot, evaluatedConfigurations);
        var manifest = NewManifest(
            content,
            frontier[0].Strategy,
            simulationOptions,
            searchOptions,
            startingWave,
            maximumWave,
            frontierArtifacts,
            searchOptions.InProgressWave,
            pendingFrontier.Count == 0 ? 0 : startingWave + 1,
            pendingFrontierArtifacts,
            recoveryArchiveArtifacts,
            evaluationIdentityArtifact,
            resumeManifest,
            attempts,
            recoveryAttempts,
            totalEvaluations,
            bestFailureSoFar);
        SaveManifest(artifactRoot, manifest);

        if (startingWave >= maximumWave)
            return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, startingWave);

        var firstAttemptedWave = true;
        while (frontier[0].Checkpoint.Waves.CurrentWaveNumber < maximumWave)
        {
            var priorFrontier = frontier;
            var targetWave = priorFrontier[0].Checkpoint.Waves.CurrentWaveNumber + 1;
            var isPendingResume = firstAttemptedWave && searchOptions.InProgressWave == targetWave;
            var startingRound = firstAttemptedWave ? searchOptions.StartingBroadeningRound : 0;
            var seededPendingFrontier = firstAttemptedWave ? pendingFrontier : Array.Empty<CheckpointSearchState>();
            var finalRound = isPendingResume
                ? searchOptions.BroadeningRounds
                : checked(startingRound + searchOptions.BroadeningRounds);
            firstAttemptedWave = false;
            var failures = new List<CheckpointWaveFailure>();
            var successfulStates = seededPendingFrontier.ToList();
            var advanced = false;

            for (var broadeningRound = startingRound;
                 broadeningRound <= finalRound;
                 broadeningRound++)
            {
                var candidateMultiplier = isPendingResume
                    ? broadeningRound + 1
                    : broadeningRound - startingRound + 1;
                var candidateCount = Math.Min(256, checked(searchOptions.CandidateCount * candidateMultiplier));
                var candidates = CampaignWavePlanGenerator.Generate(
                    targetWave,
                    searchOptions,
                    broadeningRound,
                    candidateCount,
                    preferredPlan?.FindWave(targetWave));
                var waveResult = EvaluateUnseenWave(
                    content,
                    priorFrontier,
                    candidates,
                    simulationOptions,
                    searchOptions.BeamWidth,
                    evaluatedConfigurations,
                    evaluator);
                totalEvaluations += waveResult.Evaluations;
                failures.AddRange(waveResult.Failures);
                successfulStates.AddRange(waveResult.SuccessfulEvaluations.Select(success => success.State));
                var distinctSuccessfulStates = RankDistinctStates(content, successfulStates);
                var retainedStates = CheckpointBeamOptimizer.RankStates(
                    distinctSuccessfulStates.Where(state =>
                        !IsStrategicallyExcluded(recoveryLayers, targetWave, state)),
                    searchOptions.BeamWidth);
                var tracePath = PersistSearchTrace(
                    content,
                    artifactRoot,
                    targetWave,
                    attempts.Count,
                    broadeningRound,
                    waveResult);
                var attempt = new CampaignSearchWaveArtifact(
                    targetWave,
                    broadeningRound,
                    candidates.Count,
                    waveResult.Evaluations,
                    waveResult.SuccessfulEvaluations.Count,
                    retainedStates.Count,
                    waveResult.CampaignCompletions.Count,
                    waveResult.Failures.Count,
                    tracePath,
                    SelectBestFailure(waveResult.Failures))
                {
                    RecoveryAttempt = searchOptions.RecoveryAttemptOffset + recoveryAttempts.Count
                };
                attempts.Add(attempt);
                bestFailureSoFar = SelectBestFailure(
                    waveResult.Failures.Concat(bestFailureSoFar is null ? [] : [bestFailureSoFar]));
                evaluationIdentityArtifact = PersistEvaluationIdentities(artifactRoot, evaluatedConfigurations);
                var inlineEvaluationIdentities = evaluationIdentityArtifact is null
                    ? OrderedFingerprints(evaluatedConfigurations)
                    : Array.Empty<string>();

                if (waveResult.CampaignCompletions.Count > 0)
                {
                    var completions = OrderCompletions(waveResult.CampaignCompletions);
                    var winning = completions[0].Strategy;
                    ValidateCompletedStrategy(winning, targetWave);
                    var strategyArtifact = PersistStrategy(artifactRoot, "winning-strategy.json", winning);
                    recoveryArchiveArtifacts = PersistRecoveryArchive(content, artifactRoot, recoveryLayers);
                    manifest = manifest with
                    {
                        LastCompletedWave = targetWave,
                        NextBroadeningRound = 0,
                        TotalEvaluations = totalEvaluations,
                        Status = CampaignSearchStatus.CampaignCompleted,
                        WaveAttempts = attempts.ToArray(),
                        FrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                        InProgressWave = 0,
                        PendingWave = 0,
                        PendingFrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                        RecoveryArchive = recoveryArchiveArtifacts,
                        RecoveryAttempts = recoveryAttempts.ToArray(),
                        EvaluatedConfigurationFingerprints = inlineEvaluationIdentities,
                        EvaluationIdentityArtifact = evaluationIdentityArtifact,
                        BestFailure = bestFailureSoFar,
                        FinalStrategyPath = strategyArtifact?.Path,
                        FinalStrategyFingerprint = strategyArtifact?.StrategyFingerprint,
                        FinalStrategyContentHash = strategyArtifact?.ContentHash
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
                        BestFailure = bestFailureSoFar,
                        Manifest = manifest
                    };
                }

                var exhaustedBroadening = broadeningRound == finalRound;
                if (retainedStates.Count > 0 &&
                    (retainedStates.Count >= searchOptions.BeamWidth || exhaustedBroadening))
                {
                    frontier = retainedStates;
                    ArchiveRecoveryLayer(
                        content,
                        recoveryLayers,
                        targetWave,
                        successfulStates,
                        frontier,
                        archiveCapacity);
                    frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{targetWave:D2}/frontier", frontier);
                    recoveryArchiveArtifacts = PersistRecoveryArchive(content, artifactRoot, recoveryLayers);
                    manifest = manifest with
                    {
                        LastCompletedWave = targetWave,
                        NextBroadeningRound = 0,
                        TotalEvaluations = totalEvaluations,
                        Status = CampaignSearchStatus.Running,
                        WaveAttempts = attempts.ToArray(),
                        FrontierArtifacts = frontierArtifacts,
                        InProgressWave = 0,
                        PendingWave = 0,
                        PendingFrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                        RecoveryArchive = recoveryArchiveArtifacts,
                        RecoveryAttempts = recoveryAttempts.ToArray(),
                        EvaluatedConfigurationFingerprints = inlineEvaluationIdentities,
                        EvaluationIdentityArtifact = evaluationIdentityArtifact,
                        BestFailure = bestFailureSoFar
                    };
                    SaveManifest(artifactRoot, manifest);
                    advanced = true;
                    break;
                }

                pendingFrontierArtifacts = PersistFrontier(
                    content,
                    artifactRoot,
                    $"wave-{targetWave:D2}/pending-round-{broadeningRound:D3}",
                    retainedStates);
                manifest = manifest with
                {
                    NextBroadeningRound = broadeningRound + 1,
                    TotalEvaluations = totalEvaluations,
                    Status = CampaignSearchStatus.Running,
                    WaveAttempts = attempts.ToArray(),
                    InProgressWave = targetWave,
                    PendingWave = retainedStates.Count == 0 ? 0 : targetWave,
                    PendingFrontierArtifacts = pendingFrontierArtifacts,
                    RecoveryAttempts = recoveryAttempts.ToArray(),
                    EvaluatedConfigurationFingerprints = inlineEvaluationIdentities,
                    EvaluationIdentityArtifact = evaluationIdentityArtifact,
                    BestFailure = bestFailureSoFar
                };
                SaveManifest(artifactRoot, manifest);
            }

            if (!advanced)
            {
                if (searchOptions.RecoveryAttemptOffset + recoveryAttempts.Count <
                    searchOptions.MaximumRecoveryAttempts &&
                    TryTakeRecoveryFrontier(
                        content,
                        recoveryLayers,
                        targetWave - 1,
                        searchOptions.BacktrackDepth,
                        searchOptions.BeamWidth,
                        out var recoveredFrontier,
                        out var recoveredWave,
                        out var recoveryDepth))
                {
                    frontier = recoveredFrontier;
                    var recoveryNumber = searchOptions.RecoveryAttemptOffset + recoveryAttempts.Count + 1;
                    frontierArtifacts = PersistFrontier(
                        content,
                        artifactRoot,
                        $"recovery-{recoveryNumber:D3}/wave-{recoveredWave:D2}/frontier",
                        frontier);
                    recoveryAttempts.Add(new CampaignRecoveryArtifact(
                        recoveryNumber,
                        targetWave,
                        recoveredWave,
                        recoveryDepth,
                        frontier.Select(state => state.CheckpointFingerprint).ToArray(),
                        frontierArtifacts));
                    recoveryArchiveArtifacts = PersistRecoveryArchive(content, artifactRoot, recoveryLayers);
                    manifest = manifest with
                    {
                        LastCompletedWave = recoveredWave,
                        NextBroadeningRound = 0,
                        TotalEvaluations = totalEvaluations,
                        Status = CampaignSearchStatus.Running,
                        WaveAttempts = attempts.ToArray(),
                        FrontierArtifacts = frontierArtifacts,
                        InProgressWave = 0,
                        PendingWave = 0,
                        PendingFrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                        RecoveryArchive = recoveryArchiveArtifacts,
                        RecoveryAttempts = recoveryAttempts.ToArray(),
                        EvaluatedConfigurationFingerprints = evaluationIdentityArtifact is null
                            ? OrderedFingerprints(evaluatedConfigurations)
                            : Array.Empty<string>(),
                        EvaluationIdentityArtifact = evaluationIdentityArtifact,
                        BestFailure = bestFailureSoFar
                    };
                    SaveManifest(artifactRoot, manifest);
                    continue;
                }

                frontierArtifacts = PersistFrontier(content, artifactRoot, $"wave-{targetWave:D2}/retry", priorFrontier);
                recoveryArchiveArtifacts = PersistRecoveryArchive(content, artifactRoot, recoveryLayers);
                var prefixStrategyArtifact = PersistStrategy(
                    artifactRoot, "best-prefix-strategy.json", priorFrontier[0].Strategy);
                manifest = manifest with
                {
                    LastCompletedWave = targetWave - 1,
                    NextBroadeningRound = finalRound + 1,
                    TotalEvaluations = totalEvaluations,
                    Status = CampaignSearchStatus.FrontierExhausted,
                    WaveAttempts = attempts.ToArray(),
                    FrontierArtifacts = frontierArtifacts,
                    InProgressWave = 0,
                    PendingWave = 0,
                    PendingFrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
                    RecoveryArchive = recoveryArchiveArtifacts,
                    RecoveryAttempts = recoveryAttempts.ToArray(),
                    EvaluatedConfigurationFingerprints = evaluationIdentityArtifact is null
                        ? OrderedFingerprints(evaluatedConfigurations)
                        : Array.Empty<string>(),
                    EvaluationIdentityArtifact = evaluationIdentityArtifact,
                    BestFailure = bestFailureSoFar,
                    FinalStrategyPath = prefixStrategyArtifact?.Path,
                    FinalStrategyFingerprint = prefixStrategyArtifact?.StrategyFingerprint,
                    FinalStrategyContentHash = prefixStrategyArtifact?.ContentHash
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
                    BestFailure = bestFailureSoFar,
                    Manifest = manifest
                };
            }

            if (frontier[0].Checkpoint.Waves.CurrentWaveNumber >= maximumWave)
                return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, maximumWave);
        }

        return FinishAtWaveLimit(content, artifactRoot, frontier, attempts, totalEvaluations, manifest, maximumWave);
    }

    private static CheckpointSearchResult EvaluateUnseenWave(
        GameContent content,
        IReadOnlyList<CheckpointSearchState> frontier,
        IReadOnlyList<WavePlan> candidates,
        SimulationOptions options,
        int beamWidth,
        ISet<string> evaluatedConfigurations,
        CampaignWaveEvaluator evaluator)
    {
        var targetWave = candidates[0].Wave;
        var successes = new List<CheckpointWaveSuccess>();
        var completions = new List<CheckpointCampaignCompletion>();
        var failures = new List<CheckpointWaveFailure>();
        var evaluations = 0;
        foreach (var parent in frontier.OrderBy(state => state.CheckpointFingerprint, StringComparer.Ordinal))
        {
            var unseen = candidates
                .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .Where(candidate => evaluatedConfigurations.Add(EvaluationFingerprint(parent, candidate)))
                .ToArray();
            if (unseen.Length == 0) continue;
            var result = evaluator(content, [parent], unseen, options, beamWidth);
            if (result.TargetWave != targetWave || result.Evaluations != unseen.Length)
                throw new InvalidDataException("Campaign wave evaluator returned an inconsistent evaluation trace.");
            evaluations += result.Evaluations;
            successes.AddRange(result.SuccessfulEvaluations);
            completions.AddRange(result.CampaignCompletions);
            failures.AddRange(result.Failures);
        }

        var retained = RankDistinctStates(content, successes.Select(success => success.State), beamWidth);
        return new CheckpointSearchResult
        {
            TargetWave = targetWave,
            BeamWidth = beamWidth,
            Evaluations = evaluations,
            SuccessfulEvaluations = successes
                .OrderBy(success => success.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(success => success.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray(),
            RetainedStates = retained,
            CampaignCompletions = completions
                .OrderBy(completion => completion.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(completion => completion.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray(),
            Failures = failures
                .OrderBy(failure => failure.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(failure => failure.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static string EvaluationFingerprint(CheckpointSearchState parent, WavePlan candidate)
    {
        var payload = Encoding.UTF8.GetBytes(parent.CheckpointFingerprint + "\n" + candidate.StableKey);
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    internal static IReadOnlyList<CheckpointSearchState> RankDistinctStates(
        GameContent content,
        IEnumerable<CheckpointSearchState> states,
        int limit = int.MaxValue)
    {
        var materialized = states.ToArray();
        foreach (var state in materialized)
        {
            var computed = CheckpointStrategicFingerprint.Compute(content, state.Checkpoint);
            if (!computed.Equals(state.StrategicFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("Campaign checkpoint strategic fingerprint is invalid.");
        }
        return CheckpointBeamOptimizer.RankStates(
        materialized
            .GroupBy(CheckpointBeamOptimizer.StrategicIdentity, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(state => state.Strategy.Waves.LastOrDefault()?.StableKey ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(state => StrategyFingerprint(state.Strategy), StringComparer.Ordinal)
                .ThenBy(state => state.CheckpointFingerprint, StringComparer.Ordinal)
                .First()),
        limit);
    }

    private static bool IsStrategicallyExcluded(
        IReadOnlyDictionary<int, RecoveryLayer> layers,
        int completedWave,
        CheckpointSearchState state) =>
        layers.TryGetValue(completedWave, out var layer) &&
        layer.ExcludedStrategicFingerprints.Contains(state.StrategicFingerprint);

    private static void ValidateResumableState(
        GameContent content,
        CheckpointSearchState state,
        StrategyPlan referenceStrategy,
        int baseSeed,
        int completedWave,
        string field)
    {
        state.Strategy.ValidatePrefixForCheckpoint(state.Checkpoint);
        if (state.Checkpoint.Waves.CurrentWaveNumber != completedWave ||
            !StrategyArtifactStore.Fingerprint(state.Checkpoint)
                .Equals(state.CheckpointFingerprint, StringComparison.Ordinal) ||
            !CheckpointStrategicFingerprint.Compute(content, state.Checkpoint)
                .Equals(state.StrategicFingerprint, StringComparison.Ordinal) ||
            CheckpointBeamOptimizer.Rank(content, state.Checkpoint) != state.Score)
            throw new InvalidDataException($"Campaign search {field} identity, wave, or ranking score is invalid.");
        if (state.Strategy.BaseSeed != baseSeed ||
            !state.Strategy.ArtifactId.Equals(referenceStrategy.ArtifactId, StringComparison.Ordinal) ||
            !state.Strategy.MapId.Equals(referenceStrategy.MapId, StringComparison.OrdinalIgnoreCase) ||
            !state.Strategy.DifficultyId.Equals(referenceStrategy.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !state.Strategy.ChallengeId.Equals(referenceStrategy.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
            state.Strategy.DefaultStrategy != referenceStrategy.DefaultStrategy)
            throw new InvalidDataException($"Campaign search {field} does not share the execution context.");
    }

    private static void ValidateResumeCompatibility(
        CampaignSearchManifest manifest,
        StrategyPlan strategy,
        SimulationOptions simulationOptions,
        CampaignSearchOptions options,
        int startingWave)
    {
        if (manifest.Status == CampaignSearchStatus.CampaignCompleted || manifest.LastCompletedWave != startingWave ||
            !manifest.ArtifactId.Equals(strategy.ArtifactId, StringComparison.Ordinal) ||
            !manifest.MapId.Equals(strategy.MapId, StringComparison.OrdinalIgnoreCase) ||
            !manifest.DifficultyId.Equals(strategy.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !manifest.ChallengeId.Equals(strategy.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
            manifest.BaseSeed != strategy.BaseSeed ||
            manifest.DefaultStrategy is { } defaultStrategy && defaultStrategy != strategy.DefaultStrategy)
            throw new InvalidDataException("Campaign resume manifest does not match its frontier execution context.");
        if (CampaignSimulationSettings.From(simulationOptions) != manifest.SimulationSettings)
            throw new InvalidDataException("Campaign resume cannot change persisted simulation settings.");
        var expectedInProgressWave = manifest.InProgressWave != 0
            ? manifest.InProgressWave
            : manifest.SchemaVersion < 4 ? manifest.PendingWave : 0;
        if (options.InProgressWave != expectedInProgressWave ||
            options.RecoveryAttemptOffset != manifest.RecoveryAttemptOffset)
            throw new InvalidDataException("Campaign resume cursor or recovery offset does not match its manifest.");
        var consumedRecoveries = checked(manifest.RecoveryAttemptOffset + manifest.RecoveryAttempts.Count);
        if (options.MaximumRecoveryAttempts < consumedRecoveries)
            throw new InvalidDataException("Campaign resume recovery limit is below the attempts already consumed.");
        if (expectedInProgressWave == 0) return;
        if (options.BeamWidth != manifest.BeamWidth || options.CandidateCount != manifest.CandidateCount ||
            options.BroadeningRounds != manifest.BroadeningRounds ||
            options.StartingBroadeningRound != manifest.NextBroadeningRound ||
            options.MaximumWave != manifest.MaximumWave ||
            options.BacktrackDepth != manifest.BacktrackDepth ||
            options.MaximumRecoveryAttempts != manifest.MaximumRecoveryAttempts ||
            !options.PolicyId.Equals(manifest.PolicyId, StringComparison.Ordinal) ||
            !options.BundleIds.SequenceEqual(manifest.BundleIds, StringComparer.Ordinal) ||
            options.ParameterOverrides.Count != manifest.ParameterOverrides.Count ||
            options.ParameterOverrides.Any(parameter =>
                !manifest.ParameterOverrides.TryGetValue(parameter.Key, out var value) || value != parameter.Value))
            throw new InvalidDataException("An in-progress campaign wave must retain its search configuration.");
    }

    private static void ValidateCompletedStrategy(StrategyPlan strategy, int completedWave)
    {
        strategy.Validate();
        if (strategy.Waves.Count != completedWave ||
            !strategy.Waves.Select(wave => wave.Wave).SequenceEqual(Enumerable.Range(1, completedWave)))
            throw new InvalidDataException("A completed campaign strategy must contain every campaign wave in order.");
    }

    private static Dictionary<int, RecoveryLayer> RestoreRecoveryLayers(
        GameContent content,
        IReadOnlyList<CampaignRecoveryArchiveLayerState> archivedLayers,
        StrategyPlan referenceStrategy,
        int baseSeed,
        int capacity)
    {
        var layers = new Dictionary<int, RecoveryLayer>();
        foreach (var archived in archivedLayers.OrderBy(layer => layer.CompletedWave))
        {
            if (archived.CompletedWave < 0 || archived.Alternatives is null ||
                archived.ExcludedStrategicFingerprints is null || layers.ContainsKey(archived.CompletedWave) ||
                archived.ExcludedStrategicFingerprints.Any(fingerprint =>
                    !CampaignSearchOptions.IsSha256(fingerprint)) ||
                archived.ExcludedStrategicFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                archived.ExcludedStrategicFingerprints.Count)
                throw new InvalidDataException("Campaign recovery archive layer is invalid.");
            foreach (var state in archived.Alternatives)
                ValidateResumableState(content, state, referenceStrategy, baseSeed, archived.CompletedWave,
                    "recovery archive");
            var ranked = RankDistinctStates(content, archived.Alternatives);
            var excluded = archived.ExcludedStrategicFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ranked.Any(state => excluded.Contains(state.StrategicFingerprint)))
                throw new InvalidDataException("Campaign recovery archive contains an excluded state.");
            var retained = capacity == 0 ? Array.Empty<CheckpointSearchState>() : ranked.Take(capacity).ToArray();
            layers.Add(archived.CompletedWave, new RecoveryLayer(archived.CompletedWave, retained, excluded));
        }
        return layers;
    }

    private static void ArchiveRecoveryLayer(
        GameContent content,
        IDictionary<int, RecoveryLayer> layers,
        int completedWave,
        IEnumerable<CheckpointSearchState> successfulStates,
        IReadOnlyList<CheckpointSearchState> retainedStates,
        int capacity)
    {
        if (!layers.TryGetValue(completedWave, out var layer))
        {
            layer = new RecoveryLayer(completedWave, Array.Empty<CheckpointSearchState>());
            layers.Add(completedWave, layer);
        }
        foreach (var retained in retainedStates)
            layer.ExcludedStrategicFingerprints.Add(retained.StrategicFingerprint);
        var alternatives = RankDistinctStates(content, layer.Alternatives.Concat(successfulStates))
            .Where(state => !layer.ExcludedStrategicFingerprints.Contains(state.StrategicFingerprint));
        var ranked = capacity == 0 ? Array.Empty<CheckpointSearchState>() : alternatives.Take(capacity).ToArray();
        layer.Alternatives.Clear();
        layer.Alternatives.AddRange(ranked);
    }

    private static bool TryTakeRecoveryFrontier(
        GameContent content,
        IDictionary<int, RecoveryLayer> layers,
        int currentCompletedWave,
        int maximumDepth,
        int beamWidth,
        out IReadOnlyList<CheckpointSearchState> frontier,
        out int recoveredWave,
        out int depth)
    {
        for (var candidateDepth = 1; candidateDepth <= maximumDepth; candidateDepth++)
        {
            var candidateWave = currentCompletedWave - candidateDepth + 1;
            if (!layers.TryGetValue(candidateWave, out var layer) || layer.Alternatives.Count == 0) continue;
            var selected = RankDistinctStates(content, layer.Alternatives, beamWidth);
            var selectedIdentities = selected.Select(CheckpointBeamOptimizer.StrategicIdentity)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var selectedState in selected)
                layer.ExcludedStrategicFingerprints.Add(selectedState.StrategicFingerprint);
            layer.Alternatives.RemoveAll(state =>
                selectedIdentities.Contains(CheckpointBeamOptimizer.StrategicIdentity(state)));
            frontier = selected;
            recoveredWave = layer.CompletedWave;
            depth = candidateDepth;
            return true;
        }

        frontier = Array.Empty<CheckpointSearchState>();
        recoveredWave = 0;
        depth = 0;
        return false;
    }

    private static IReadOnlyList<string> OrderedFingerprints(IEnumerable<string> fingerprints) =>
        fingerprints.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public static CheckpointWaveFailure? SelectBestFailure(IEnumerable<CheckpointWaveFailure> failures) =>
        OrderFailures(failures).FirstOrDefault();

    private static IOrderedEnumerable<CheckpointWaveFailure> OrderFailures(
        IEnumerable<CheckpointWaveFailure> failures) => failures
        .OrderByDescending(failure => failure.WavePlan.Wave)
        .ThenBy(failure => failure.FailureMargin is null ? 1 : 0)
        .ThenBy(failure => failure.FailureMargin?.UnresolvedArmorAdjustedDurabilityFraction ?? float.MaxValue)
        .ThenBy(failure => failure.FailureMargin?.UnresolvedArmorAdjustedDurability ?? float.MaxValue)
        .ThenBy(failure => failure.FailureMargin?.UnresolvedEnemyCount ?? int.MaxValue)
        .ThenBy(failure => failure.FailureMargin?.UnresolvedFurthestProgress ?? 0)
        .ThenBy(failure => failure.WavePlan.StableKey, StringComparer.Ordinal)
        .ThenBy(failure => failure.ParentCheckpointFingerprint, StringComparer.Ordinal);

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
        var strategyArtifact = PersistStrategy(artifactRoot, "best-strategy.json", finalStrategy);
        manifest = manifest with
        {
            LastCompletedWave = wave,
            NextBroadeningRound = 0,
            TotalEvaluations = totalEvaluations,
            Status = CampaignSearchStatus.WaveLimitReached,
            WaveAttempts = attempts.ToArray(),
            FrontierArtifacts = frontierArtifacts,
            InProgressWave = 0,
            PendingWave = 0,
            PendingFrontierArtifacts = Array.Empty<CampaignFrontierArtifact>(),
            FinalStrategyPath = strategyArtifact?.Path,
            FinalStrategyFingerprint = strategyArtifact?.StrategyFingerprint,
            FinalStrategyContentHash = strategyArtifact?.ContentHash
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
        .ThenBy(completion => completion.ParentCheckpointFingerprint, StringComparer.Ordinal)
        .ToArray();

    private static CampaignSearchManifest NewManifest(
        GameContent content,
        StrategyPlan strategy,
        SimulationOptions simulationOptions,
        CampaignSearchOptions options,
        int startingWave,
        int maximumWave,
        IReadOnlyList<CampaignFrontierArtifact> frontierArtifacts,
        int inProgressWave,
        int pendingWave,
        IReadOnlyList<CampaignFrontierArtifact> pendingFrontierArtifacts,
        IReadOnlyList<CampaignRecoveryArchiveArtifact> recoveryArchive,
        CampaignEvaluationIdentityArtifact? evaluationIdentityArtifact,
        CampaignSearchManifest? resumeManifest,
        IReadOnlyList<CampaignSearchWaveArtifact> attempts,
        IReadOnlyList<CampaignRecoveryArtifact> recoveryAttempts,
        int totalEvaluations,
        CheckpointWaveFailure? bestFailure) => new()
    {
        ArtifactId = strategy.ArtifactId,
        MapId = strategy.MapId,
        DifficultyId = strategy.DifficultyId,
        ChallengeId = strategy.ChallengeId,
        BaseSeed = strategy.BaseSeed,
        BuildFingerprint = MinimalBastion.Multiplayer.BuildFingerprint.Compute(content),
        DefaultStrategy = strategy.DefaultStrategy,
        StartingWave = resumeManifest?.StartingWave ?? startingWave,
        LastCompletedWave = startingWave,
        MaximumWave = maximumWave,
        BeamWidth = options.BeamWidth,
        CandidateCount = options.CandidateCount,
        BroadeningRounds = options.BroadeningRounds,
        BacktrackDepth = options.BacktrackDepth,
        MaximumRecoveryAttempts = options.MaximumRecoveryAttempts,
        RecoveryAttemptOffset = resumeManifest?.RecoveryAttemptOffset ?? options.RecoveryAttemptOffset,
        PolicyId = options.PolicyId,
        BundleIds = options.BundleIds.ToArray(),
        ParameterOverrides = options.ParameterOverrides.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal),
        SimulationSettings = CampaignSimulationSettings.From(simulationOptions),
        NextBroadeningRound = options.StartingBroadeningRound,
        TotalEvaluations = totalEvaluations,
        Status = CampaignSearchStatus.Running,
        WaveAttempts = attempts.ToArray(),
        FrontierArtifacts = frontierArtifacts,
        InProgressWave = inProgressWave,
        PendingWave = pendingWave,
        PendingFrontierArtifacts = pendingFrontierArtifacts,
        RecoveryArchive = recoveryArchive,
        RecoveryAttempts = recoveryAttempts.ToArray(),
        EvaluatedConfigurationFingerprints = evaluationIdentityArtifact is null
            ? options.PreviouslyEvaluatedConfigurationFingerprints
                .Select(value => value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>(),
        EvaluationIdentityArtifact = evaluationIdentityArtifact,
        BestFailure = bestFailure
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
                StrategyCheckpointArtifact.Create(state.Strategy.ArtifactId, state.Checkpoint, content), content);
            StrategyArtifactStore.SavePlan(Path.Combine(artifactRoot, strategyPath), state.Strategy);
            results.Add(new CampaignFrontierArtifact(state.CheckpointFingerprint, checkpointPath, strategyPath)
            {
                StrategicFingerprint = state.StrategicFingerprint,
                StrategyFingerprint = StrategyFingerprint(state.Strategy),
                StateFingerprint = RecoveryStateFingerprint(state),
                CheckpointContentHash = ContentHash(Path.Combine(artifactRoot, checkpointPath)),
                StrategyContentHash = ContentHash(Path.Combine(artifactRoot, strategyPath))
            });
        }
        return results;
    }

    private static IReadOnlyList<CampaignRecoveryArchiveArtifact> PersistRecoveryArchive(
        GameContent content,
        string? artifactRoot,
        IReadOnlyDictionary<int, RecoveryLayer> layers)
    {
        if (artifactRoot is null) return Array.Empty<CampaignRecoveryArchiveArtifact>();
        var archivedLayers = new List<CampaignRecoveryArchiveArtifact>();
        foreach (var layer in layers.Values.OrderBy(layer => layer.CompletedWave))
        {
            var states = new List<CampaignRecoveryStateArtifact>();
            foreach (var state in RankDistinctStates(content, layer.Alternatives))
            {
                if (layer.ExcludedStrategicFingerprints.Contains(state.StrategicFingerprint))
                    throw new InvalidDataException("Campaign recovery archive contains an excluded state.");
                var stateFingerprint = RecoveryStateFingerprint(state);
                var decisionFingerprint = RecoveryDecisionFingerprint(state, layer.CompletedWave);
                var relativeDirectory = $"recovery-archive/wave-{layer.CompletedWave:D2}";
                var checkpointPath = NormalizeRelative(Path.Combine(
                    relativeDirectory, stateFingerprint + ".checkpoint.json"));
                var strategyPath = NormalizeRelative(Path.Combine(
                    relativeDirectory, stateFingerprint + ".strategy.json"));
                var fullCheckpointPath = Path.Combine(artifactRoot, checkpointPath);
                var fullStrategyPath = Path.Combine(artifactRoot, strategyPath);
                StrategyArtifactStore.SaveCheckpoint(fullCheckpointPath,
                    StrategyCheckpointArtifact.Create(state.Strategy.ArtifactId, state.Checkpoint, content), content);
                StrategyArtifactStore.SavePlan(fullStrategyPath, state.Strategy);
                states.Add(new CampaignRecoveryStateArtifact(
                    stateFingerprint,
                    state.CheckpointFingerprint,
                    decisionFingerprint,
                    checkpointPath,
                    strategyPath)
                {
                    StrategicFingerprint = state.StrategicFingerprint,
                    StrategyFingerprint = StrategyFingerprint(state.Strategy),
                    CheckpointContentHash = ContentHash(fullCheckpointPath),
                    StrategyContentHash = ContentHash(fullStrategyPath)
                });
            }
            var excluded = layer.ExcludedStrategicFingerprints
                .OrderBy(fingerprint => fingerprint, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            archivedLayers.Add(new CampaignRecoveryArchiveArtifact(
                layer.CompletedWave,
                states.Count,
                states.Select(state => state.CheckpointFingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                states.Select(state => state.DecisionFingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                excluded.Length,
                Array.Empty<string>(),
                states)
            {
                DistinctStrategicCount = states.Select(state => state.StrategicFingerprint)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ExcludedStrategicFingerprints = excluded
            });
        }
        return archivedLayers;
    }

    internal static string RecoveryStateFingerprint(CheckpointSearchState state)
    {
        var payload = new StringBuilder(state.CheckpointFingerprint)
            .Append('\n').Append(state.Strategy.DefaultStrategy);
        foreach (var wave in state.Strategy.Waves)
            payload.Append('\n').Append(wave.StableKey);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    internal static string StrategyFingerprint(StrategyPlan strategy)
    {
        strategy.Validate();
        var canonical = strategy with
        {
            Waves = strategy.Waves.Select(wave => wave with
            {
                Parameters = wave.Parameters.OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                    .ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal)
            }).ToArray(),
            Metadata = strategy.Metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(canonical, StrategyArtifactStore.CreateJsonOptions());
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    internal static string ContentHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static string RecoveryDecisionFingerprint(CheckpointSearchState state, int completedWave)
    {
        var decision = state.Strategy.FindWave(completedWave)?.StableKey ?? $"opening:{completedWave}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(decision)));
    }

    private static string? PersistSearchTrace(
        GameContent content,
        string? artifactRoot,
        int wave,
        int attemptIndex,
        int broadeningRound,
        CheckpointSearchResult result)
    {
        if (artifactRoot is null) return null;
        var relativePath = NormalizeRelative(Path.Combine(
            $"wave-{wave:D2}",
            $"attempt-{attemptIndex:D4}-round-{broadeningRound:D3}-search.json"));
        StrategyArtifactStore.SaveSearchResult(
            Path.Combine(artifactRoot, relativePath),
            CompactSearchTrace(result),
            content);
        return relativePath;
    }

    private static CheckpointSearchResult CompactSearchTrace(CheckpointSearchResult result)
    {
        var retainedSuccesses = result.RetainedStates.Select(state =>
                result.SuccessfulEvaluations
                    .Where(success => SameTraceState(success.State, state))
                    .OrderBy(success => success.ParentCheckpointFingerprint, StringComparer.Ordinal)
                    .ThenBy(success => success.WavePlan.StableKey, StringComparer.Ordinal)
                    .FirstOrDefault() ??
                throw new InvalidDataException("A retained campaign state has no matching successful evaluation."))
            .ToArray();
        var completions = OrderCompletions(result.CampaignCompletions)
            .Take(MaximumPersistedCompletionTraces)
            .ToArray();
        var failures = OrderFailures(result.Failures)
            .Take(MaximumPersistedFailureTraces)
            .ToArray();
        return new CheckpointSearchResult
        {
            TargetWave = result.TargetWave,
            BeamWidth = result.BeamWidth,
            Evaluations = result.Evaluations,
            SuccessfulEvaluations = retainedSuccesses,
            RetainedStates = result.RetainedStates,
            CampaignCompletions = completions,
            Failures = failures,
            OmittedSuccessfulEvaluations = checked(result.OmittedSuccessfulEvaluations +
                                                    result.SuccessfulEvaluations.Count -
                                                    retainedSuccesses.Length),
            OmittedCampaignCompletions = checked(result.OmittedCampaignCompletions +
                                                 result.CampaignCompletions.Count -
                                                 completions.Length),
            OmittedFailures = checked(result.OmittedFailures + result.Failures.Count - failures.Length)
        };
    }

    private static bool SameTraceState(CheckpointSearchState left, CheckpointSearchState right) =>
        left.CheckpointFingerprint.Equals(right.CheckpointFingerprint, StringComparison.Ordinal) &&
        left.Strategy.DefaultStrategy == right.Strategy.DefaultStrategy &&
        left.Strategy.Waves.Select(wave => wave.StableKey)
            .SequenceEqual(right.Strategy.Waves.Select(wave => wave.StableKey), StringComparer.Ordinal);

    private static CampaignEvaluationIdentityArtifact? PersistEvaluationIdentities(
        string? artifactRoot,
        IEnumerable<string> fingerprints)
    {
        if (artifactRoot is null) return null;
        var ordered = OrderedFingerprints(fingerprints);
        var payload = Encoding.UTF8.GetBytes(ordered.Count == 0 ? "" : string.Join('\n', ordered) + "\n");
        var contentHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var relativePath = NormalizeRelative(Path.Combine(
            "evaluation-identities", contentHash + ".txt"));
        WriteAtomically(Path.Combine(artifactRoot, relativePath), payload);
        return new CampaignEvaluationIdentityArtifact(
            relativePath,
            contentHash,
            ordered.Count);
    }

    private static PersistedStrategyArtifact? PersistStrategy(
        string? artifactRoot,
        string filename,
        StrategyPlan strategy)
    {
        if (artifactRoot is null) return null;
        var relativePath = NormalizeRelative(filename);
        var fullPath = Path.Combine(artifactRoot, relativePath);
        StrategyArtifactStore.SavePlan(fullPath, strategy);
        return new PersistedStrategyArtifact(
            relativePath,
            StrategyFingerprint(strategy),
            ContentHash(fullPath));
    }

    private static void WriteAtomically(string path, byte[] payload)
    {
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
        ValidateBuildFingerprint(content, manifest);
        var root = Path.GetDirectoryName(fullManifestPath)!;
        return LoadFrontierArtifacts(
            content,
            root,
            manifest,
            manifest.FrontierArtifacts,
            manifest.LastCompletedWave,
            "frontier");
    }

    public static CampaignSearchResumeState LoadResumeState(GameContent content, string manifestPath)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifest = LoadManifest(fullManifestPath);
        ValidateBuildFingerprint(content, manifest);
        if (manifest.SchemaVersion < 5 && manifest.RecoveryArchive.Count > 0)
            throw new InvalidDataException(
                "Legacy campaign recovery archives use exact checkpoint tombstones and cannot be resumed strategically.");
        var root = Path.GetDirectoryName(fullManifestPath)!;
        var frontier = LoadFrontierArtifacts(
            content,
            root,
            manifest,
            manifest.FrontierArtifacts,
            manifest.LastCompletedWave,
            "frontier");
        var pending = LoadFrontierArtifacts(
            content,
            root,
            manifest,
            manifest.PendingFrontierArtifacts,
            manifest.PendingWave,
            "pending frontier");
        IReadOnlyList<CampaignRecoveryArchiveLayerState> archive = manifest.SchemaVersion < 5
            ? Array.Empty<CampaignRecoveryArchiveLayerState>()
            : manifest.RecoveryArchive.OrderBy(layer => layer.CompletedWave).Select(layer =>
            {
                var states = layer.States.Select(artifact =>
                {
                    var checkpointPath = ResolveRelative(root, artifact.CheckpointPath);
                    var strategyPath = ResolveRelative(root, artifact.StrategyPath);
                    VerifyContentHash(manifest, checkpointPath, artifact.CheckpointContentHash,
                        "recovery checkpoint");
                    VerifyContentHash(manifest, strategyPath, artifact.StrategyContentHash,
                        "recovery strategy");
                    var checkpoint = StrategyArtifactStore.LoadCheckpoint(checkpointPath, content);
                    var strategy = StrategyArtifactStore.LoadPlan(strategyPath);
                    var state = CheckpointSearchState.Create(content, strategy, checkpoint.Checkpoint);
                    ValidateArtifactContext(manifest, checkpoint, strategy, state, layer.CompletedWave,
                        "recovery state");
                    if (state.Checkpoint.Waves.CurrentWaveNumber != layer.CompletedWave ||
                        !state.CheckpointFingerprint.Equals(artifact.CheckpointFingerprint,
                            StringComparison.OrdinalIgnoreCase) ||
                        !CampaignStrategyOptimizer.RecoveryStateFingerprint(state).Equals(
                            artifact.StateFingerprint, StringComparison.OrdinalIgnoreCase) ||
                        !CampaignStrategyOptimizer.RecoveryDecisionFingerprint(state, layer.CompletedWave).Equals(
                            artifact.DecisionFingerprint, StringComparison.OrdinalIgnoreCase) ||
                        !state.StrategicFingerprint.Equals(artifact.StrategicFingerprint,
                            StringComparison.OrdinalIgnoreCase) ||
                        !CampaignStrategyOptimizer.StrategyFingerprint(strategy).Equals(
                            artifact.StrategyFingerprint, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Campaign recovery state does not match its archive identity.");
                    return state;
                }).ToArray();
                var ranked = CampaignStrategyOptimizer.RankDistinctStates(
                    content, states, Math.Max(1, states.Length));
                return new CampaignRecoveryArchiveLayerState(
                    layer.CompletedWave,
                    ranked,
                    layer.ExcludedStrategicFingerprints.ToArray());
            }).ToArray();
        var evaluatedConfigurations = LoadEvaluationIdentities(root, manifest);
        return new CampaignSearchResumeState(manifest, frontier, pending, archive, evaluatedConfigurations);
    }

    private static IReadOnlyList<CheckpointSearchState> LoadFrontierArtifacts(
        GameContent content,
        string root,
        CampaignSearchManifest manifest,
        IReadOnlyList<CampaignFrontierArtifact> artifacts,
        int expectedWave,
        string field)
    {
        var states = artifacts.Select(artifact =>
        {
            var checkpointPath = ResolveRelative(root, artifact.CheckpointPath);
            var strategyPath = ResolveRelative(root, artifact.StrategyPath);
            VerifyContentHash(manifest, checkpointPath, artifact.CheckpointContentHash, $"{field} checkpoint");
            VerifyContentHash(manifest, strategyPath, artifact.StrategyContentHash, $"{field} strategy");
            var checkpoint = StrategyArtifactStore.LoadCheckpoint(checkpointPath, content);
            var strategy = StrategyArtifactStore.LoadPlan(strategyPath);
            var state = CheckpointSearchState.Create(content, strategy, checkpoint.Checkpoint);
            ValidateArtifactContext(manifest, checkpoint, strategy, state, expectedWave, field);
            if (state.Checkpoint.Waves.CurrentWaveNumber != expectedWave ||
                !state.CheckpointFingerprint.Equals(artifact.CheckpointFingerprint, StringComparison.OrdinalIgnoreCase) ||
                manifest.SchemaVersion >= 4 &&
                (!state.StrategicFingerprint.Equals(artifact.StrategicFingerprint,
                     StringComparison.OrdinalIgnoreCase) ||
                 !CampaignStrategyOptimizer.StrategyFingerprint(strategy).Equals(
                     artifact.StrategyFingerprint, StringComparison.OrdinalIgnoreCase) ||
                 !CampaignStrategyOptimizer.RecoveryStateFingerprint(state).Equals(
                     artifact.StateFingerprint, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Campaign search {field} does not match its artifacts.");
            return state;
        }).ToArray();
        return CampaignStrategyOptimizer.RankDistinctStates(content, states, Math.Max(1, states.Length));
    }

    public static StrategyPlan LoadFinalStrategy(GameContent content, string manifestPath)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifest = LoadManifest(fullManifestPath);
        ValidateBuildFingerprint(content, manifest);
        if (manifest.FinalStrategyPath is null)
            throw new InvalidDataException("Campaign search manifest has no final strategy artifact.");
        var strategyPath = ResolveRelative(Path.GetDirectoryName(fullManifestPath)!, manifest.FinalStrategyPath);
        VerifyContentHash(manifest, strategyPath, manifest.FinalStrategyContentHash, "final strategy");
        var strategy = StrategyArtifactStore.LoadPlan(strategyPath);
        ValidateStrategyContext(manifest, strategy, "final strategy");
        if (manifest.SchemaVersion >= 4 &&
            !CampaignStrategyOptimizer.StrategyFingerprint(strategy).Equals(
                manifest.FinalStrategyFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Campaign final strategy fingerprint does not match its artifact.");
        if (manifest.Status == CampaignSearchStatus.CampaignCompleted)
        {
            if (manifest.LastCompletedWave != manifest.MaximumWave ||
                strategy.Waves.Count != manifest.LastCompletedWave ||
                !strategy.Waves.Select(wave => wave.Wave)
                    .SequenceEqual(Enumerable.Range(1, manifest.LastCompletedWave)))
                throw new InvalidDataException("Completed campaign strategy is not a full contiguous campaign plan.");
        }
        return strategy;
    }

    private static void ValidateBuildFingerprint(GameContent content, CampaignSearchManifest manifest)
    {
        if (manifest.SchemaVersion < 4) return;
        var current = MinimalBastion.Multiplayer.BuildFingerprint.Compute(content);
        if (!current.Equals(manifest.BuildFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Campaign search manifest was created by a different gameplay build.");
    }

    private static void ValidateArtifactContext(
        CampaignSearchManifest manifest,
        StrategyCheckpointArtifact checkpoint,
        StrategyPlan strategy,
        CheckpointSearchState state,
        int expectedWave,
        string field)
    {
        ValidateStrategyContext(manifest, strategy, field);
        if (!checkpoint.StrategyArtifactId.Equals(manifest.ArtifactId, StringComparison.Ordinal) ||
            !checkpoint.StrategyArtifactId.Equals(strategy.ArtifactId, StringComparison.Ordinal) ||
            !checkpoint.Checkpoint.MapId.Equals(manifest.MapId, StringComparison.OrdinalIgnoreCase) ||
            !checkpoint.Checkpoint.DifficultyId.Equals(manifest.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !checkpoint.Checkpoint.ChallengeId.Equals(manifest.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
            checkpoint.Checkpoint.Waves.CurrentWaveNumber != expectedWave ||
            !checkpoint.CheckpointFingerprint.Equals(state.CheckpointFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Campaign search {field} does not match its manifest context.");
    }

    private static void ValidateStrategyContext(
        CampaignSearchManifest manifest,
        StrategyPlan strategy,
        string field)
    {
        if (!strategy.ArtifactId.Equals(manifest.ArtifactId, StringComparison.Ordinal) ||
            !strategy.MapId.Equals(manifest.MapId, StringComparison.OrdinalIgnoreCase) ||
            !strategy.DifficultyId.Equals(manifest.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !strategy.ChallengeId.Equals(manifest.ChallengeId, StringComparison.OrdinalIgnoreCase) ||
            strategy.BaseSeed != manifest.BaseSeed ||
            manifest.DefaultStrategy is { } defaultStrategy && strategy.DefaultStrategy != defaultStrategy)
            throw new InvalidDataException($"Campaign search {field} strategy does not match its manifest context.");
    }

    private static void VerifyContentHash(
        CampaignSearchManifest manifest,
        string path,
        string? expectedHash,
        string field)
    {
        if (manifest.SchemaVersion < 4) return;
        if (!CampaignSearchOptions.IsSha256(expectedHash) ||
            !CampaignStrategyOptimizer.ContentHash(path).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Campaign search {field} content hash does not match its artifact.");
    }

    private static IReadOnlyList<string> LoadEvaluationIdentities(string root, CampaignSearchManifest manifest)
    {
        if (manifest.EvaluationIdentityArtifact is not { } artifact)
            return manifest.EvaluatedConfigurationFingerprints
                .Select(value => value.ToUpperInvariant()).ToArray();
        var path = ResolveRelative(root, artifact.Path);
        if (!CampaignStrategyOptimizer.ContentHash(path).Equals(
                artifact.ContentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Campaign evaluation identity content hash does not match its artifact.");
        var values = File.ReadAllLines(path)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .ToArray();
        if (values.Length != artifact.Count ||
            values.Any(value => !CampaignSearchOptions.IsSha256(value)) ||
            values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length ||
            !values.SequenceEqual(values.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Campaign evaluation identity artifact is invalid.");
        return values;
    }

    private static void Validate(CampaignSearchManifest manifest)
    {
        if (manifest.SchemaVersion is < 1 or > CampaignSearchManifest.CurrentSchemaVersion)
            throw new InvalidDataException($"Campaign search schema {manifest.SchemaVersion} is not supported.");
        if (string.IsNullOrWhiteSpace(manifest.ArtifactId) || string.IsNullOrWhiteSpace(manifest.MapId) ||
            string.IsNullOrWhiteSpace(manifest.DifficultyId) || string.IsNullOrWhiteSpace(manifest.ChallengeId) ||
            manifest.StartingWave < 0 || manifest.LastCompletedWave < manifest.StartingWave ||
            manifest.MaximumWave < manifest.LastCompletedWave || manifest.BeamWidth <= 0 ||
            manifest.CandidateCount <= 0 || manifest.BroadeningRounds < 0 || manifest.NextBroadeningRound < 0 ||
            manifest.InProgressWave < 0 ||
            manifest.BacktrackDepth is < 0 or > GameConstants.CampaignWaveCount ||
            manifest.MaximumRecoveryAttempts is < 0 or > 1000 || manifest.RecoveryAttemptOffset < 0 ||
            manifest.TotalEvaluations < 0 || string.IsNullOrWhiteSpace(manifest.PolicyId) ||
            manifest.BundleIds is null || manifest.ParameterOverrides is null || manifest.SimulationSettings is null ||
            manifest.WaveAttempts is null || manifest.FrontierArtifacts is null || manifest.RecoveryAttempts is null ||
            manifest.PendingFrontierArtifacts is null || manifest.RecoveryArchive is null ||
            manifest.EvaluatedConfigurationFingerprints is null)
            throw new InvalidDataException("Campaign search manifest fields are invalid.");
        if (manifest.SchemaVersion < 3 &&
            (manifest.PendingWave != 0 || manifest.PendingFrontierArtifacts.Count != 0 ||
             manifest.RecoveryArchive.Count != 0))
            throw new InvalidDataException("Legacy campaign search manifests cannot contain resume archives.");
        if (manifest.SchemaVersion < 4 &&
            (manifest.InProgressWave != 0 || manifest.BuildFingerprint is not null ||
             manifest.DefaultStrategy is not null ||
             manifest.EvaluationIdentityArtifact is not null || manifest.FinalStrategyFingerprint is not null ||
             manifest.FinalStrategyContentHash is not null))
            throw new InvalidDataException("Legacy campaign search manifests cannot contain schema-v4 fields.");
        if (manifest.SchemaVersion >= 4 &&
            (manifest.DefaultStrategy is null || !CampaignSearchOptions.IsSha256(manifest.BuildFingerprint)))
            throw new InvalidDataException("Campaign search manifest build or default strategy is missing.");
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
        if (manifest.EvaluatedConfigurationFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            manifest.EvaluatedConfigurationFingerprints.Count ||
            manifest.EvaluatedConfigurationFingerprints.Any(fingerprint =>
                !CampaignSearchOptions.IsSha256(fingerprint)))
            throw new InvalidDataException("Campaign search evaluated-configuration fingerprints are invalid.");
        if (manifest.EvaluationIdentityArtifact is { } evaluationArtifact)
        {
            if (manifest.EvaluatedConfigurationFingerprints.Count != 0 || evaluationArtifact.Count < 0 ||
                evaluationArtifact.Count != manifest.TotalEvaluations ||
                !IsSafeRelativePath(evaluationArtifact.Path) ||
                !CampaignSearchOptions.IsSha256(evaluationArtifact.ContentHash))
                throw new InvalidDataException("Campaign search evaluation identity artifact is invalid.");
        }
        else if (manifest.SchemaVersion >= 4 &&
                 manifest.EvaluatedConfigurationFingerprints.Count != manifest.TotalEvaluations)
            throw new InvalidDataException("Campaign search inline evaluation identities are incomplete.");
        if (manifest.FrontierArtifacts.Count > manifest.BeamWidth)
            throw new InvalidDataException("Campaign search manifest frontier exceeds its beam width.");
        foreach (var artifact in manifest.FrontierArtifacts)
        {
            if (!CampaignSearchOptions.IsSha256(artifact.CheckpointFingerprint) ||
                !IsSafeRelativePath(artifact.CheckpointPath) || !IsSafeRelativePath(artifact.StrategyPath) ||
                manifest.SchemaVersion >= 4 &&
                (!CampaignSearchOptions.IsSha256(artifact.StrategicFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.StrategyFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.StateFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.CheckpointContentHash) ||
                 !CampaignSearchOptions.IsSha256(artifact.StrategyContentHash)))
                throw new InvalidDataException("Campaign search frontier artifact path or identity is invalid.");
        }
        if (manifest.SchemaVersion >= 5 &&
            manifest.FrontierArtifacts.Select(artifact => artifact.StrategicFingerprint)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.FrontierArtifacts.Count)
            throw new InvalidDataException("Campaign search frontier contains duplicate strategic identities.");
        var expectedInProgressWave = manifest.SchemaVersion >= 4
            ? manifest.InProgressWave
            : manifest.PendingWave;
        if ((manifest.PendingWave == 0) != (manifest.PendingFrontierArtifacts.Count == 0) ||
            manifest.PendingFrontierArtifacts.Count >= manifest.BeamWidth ||
            manifest.PendingWave != 0 && manifest.PendingWave != expectedInProgressWave ||
            expectedInProgressWave != 0 &&
            (manifest.Status != CampaignSearchStatus.Running ||
             expectedInProgressWave != manifest.LastCompletedWave + 1 ||
             expectedInProgressWave > manifest.MaximumWave || manifest.NextBroadeningRound <= 0) ||
            expectedInProgressWave == 0 &&
            (manifest.PendingWave != 0 || manifest.PendingFrontierArtifacts.Count != 0))
            throw new InvalidDataException("Campaign search pending frontier summary is invalid.");
        foreach (var artifact in manifest.PendingFrontierArtifacts)
        {
            if (!CampaignSearchOptions.IsSha256(artifact.CheckpointFingerprint) ||
                !IsSafeRelativePath(artifact.CheckpointPath) || !IsSafeRelativePath(artifact.StrategyPath) ||
                manifest.SchemaVersion >= 4 &&
                (!CampaignSearchOptions.IsSha256(artifact.StrategicFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.StrategyFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.StateFingerprint) ||
                 !CampaignSearchOptions.IsSha256(artifact.CheckpointContentHash) ||
                 !CampaignSearchOptions.IsSha256(artifact.StrategyContentHash)))
                throw new InvalidDataException("Campaign search pending frontier artifact is invalid.");
        }
        if (manifest.SchemaVersion >= 5 &&
            manifest.PendingFrontierArtifacts.Select(artifact => artifact.StrategicFingerprint)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.PendingFrontierArtifacts.Count)
            throw new InvalidDataException("Campaign search pending frontier contains duplicate strategic identities.");
        if (manifest.RecoveryArchive.Select(layer => layer.CompletedWave).Distinct().Count() !=
            manifest.RecoveryArchive.Count)
            throw new InvalidDataException("Campaign recovery archive contains duplicate wave layers.");
        var maximumArchivedStates = (long)manifest.BeamWidth * manifest.MaximumRecoveryAttempts;
        foreach (var layer in manifest.RecoveryArchive)
        {
            if (layer.CompletedWave < 0 || layer.CompletedWave > manifest.MaximumWave ||
                layer.States is null || layer.ExcludedCheckpointFingerprints is null ||
                layer.ExcludedStrategicFingerprints is null ||
                layer.RemainingStateCount != layer.States.Count ||
                layer.DistinctCheckpointCount != layer.States.Select(state => state.CheckpointFingerprint)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
                layer.DistinctDecisionCount != layer.States.Select(state => state.DecisionFingerprint)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
                layer.RemainingStateCount < 0 || layer.RemainingStateCount > maximumArchivedStates ||
                layer.ExcludedStateCount < 0 ||
                layer.States.Select(state => state.StateFingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                layer.States.Count ||
                layer.States.Any(state =>
                    !CampaignSearchOptions.IsSha256(state.StateFingerprint) ||
                    !CampaignSearchOptions.IsSha256(state.CheckpointFingerprint) ||
                    !CampaignSearchOptions.IsSha256(state.DecisionFingerprint) ||
                    !IsSafeRelativePath(state.CheckpointPath) || !IsSafeRelativePath(state.StrategyPath) ||
                    manifest.SchemaVersion >= 4 &&
                    (!CampaignSearchOptions.IsSha256(state.StrategicFingerprint) ||
                     !CampaignSearchOptions.IsSha256(state.StrategyFingerprint) ||
                     !CampaignSearchOptions.IsSha256(state.CheckpointContentHash) ||
                     !CampaignSearchOptions.IsSha256(state.StrategyContentHash))))
                throw new InvalidDataException("Campaign recovery archive layer is invalid.");
            if (manifest.SchemaVersion >= 5)
            {
                if (layer.ExcludedCheckpointFingerprints.Count != 0 ||
                    layer.DistinctStrategicCount != layer.States.Select(state => state.StrategicFingerprint)
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
                    layer.DistinctStrategicCount != layer.States.Count ||
                    layer.ExcludedStateCount != layer.ExcludedStrategicFingerprints.Count ||
                    layer.ExcludedStrategicFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                    layer.ExcludedStrategicFingerprints.Count ||
                    layer.ExcludedStrategicFingerprints.Any(fingerprint =>
                        !CampaignSearchOptions.IsSha256(fingerprint)) ||
                    layer.States.Any(state => layer.ExcludedStrategicFingerprints.Contains(
                        state.StrategicFingerprint, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Campaign strategic recovery archive layer is invalid.");
            }
            else if (layer.DistinctStrategicCount != 0 || layer.ExcludedStrategicFingerprints.Count != 0 ||
                     layer.ExcludedStateCount != layer.ExcludedCheckpointFingerprints.Count ||
                     layer.ExcludedCheckpointFingerprints.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                     layer.ExcludedCheckpointFingerprints.Count ||
                     layer.ExcludedCheckpointFingerprints.Any(fingerprint =>
                         !CampaignSearchOptions.IsSha256(fingerprint)) ||
                     layer.States.Any(state => layer.ExcludedCheckpointFingerprints.Contains(
                         state.CheckpointFingerprint, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Legacy campaign recovery archive layer is invalid.");
        }
        var expectedRecoveryAttempt = manifest.RecoveryAttemptOffset + 1;
        foreach (var recovery in manifest.RecoveryAttempts)
        {
            if (recovery.Attempt != expectedRecoveryAttempt++ || recovery.BlockingWave <= recovery.RecoveredWave ||
                recovery.Depth != recovery.BlockingWave - recovery.RecoveredWave ||
                recovery.Depth is < 1 || recovery.Depth > manifest.BacktrackDepth ||
                recovery.CheckpointFingerprints is null || recovery.FrontierArtifacts is null ||
                recovery.CheckpointFingerprints.Count == 0 ||
                recovery.FrontierArtifacts.Count != 0 &&
                recovery.CheckpointFingerprints.Count != recovery.FrontierArtifacts.Count ||
                recovery.CheckpointFingerprints.Count > manifest.BeamWidth ||
                recovery.CheckpointFingerprints.Any(fingerprint => !CampaignSearchOptions.IsSha256(fingerprint)) ||
                recovery.FrontierArtifacts.Count > 0 &&
                !recovery.CheckpointFingerprints.SequenceEqual(
                    recovery.FrontierArtifacts.Select(artifact => artifact.CheckpointFingerprint),
                    StringComparer.Ordinal))
                throw new InvalidDataException("Campaign search recovery history is invalid.");
            foreach (var artifact in recovery.FrontierArtifacts)
            {
                if (!CampaignSearchOptions.IsSha256(artifact.CheckpointFingerprint) ||
                    !IsSafeRelativePath(artifact.CheckpointPath) || !IsSafeRelativePath(artifact.StrategyPath) ||
                    manifest.SchemaVersion >= 4 &&
                    (!CampaignSearchOptions.IsSha256(artifact.StrategicFingerprint) ||
                     !CampaignSearchOptions.IsSha256(artifact.StrategyFingerprint) ||
                     !CampaignSearchOptions.IsSha256(artifact.StateFingerprint) ||
                     !CampaignSearchOptions.IsSha256(artifact.CheckpointContentHash) ||
                     !CampaignSearchOptions.IsSha256(artifact.StrategyContentHash)))
                    throw new InvalidDataException("Campaign search recovery artifact path is invalid.");
            }
        }
        if ((long)manifest.RecoveryAttemptOffset + manifest.RecoveryAttempts.Count >
            manifest.MaximumRecoveryAttempts ||
            manifest.WaveAttempts.Any(attempt =>
                attempt.RecoveryAttempt < manifest.RecoveryAttemptOffset ||
                attempt.RecoveryAttempt > manifest.RecoveryAttemptOffset + manifest.RecoveryAttempts.Count))
            throw new InvalidDataException("Campaign search recovery bounds are inconsistent.");
        if (manifest.FinalStrategyPath is not null && !IsSafeRelativePath(manifest.FinalStrategyPath))
            throw new InvalidDataException("Campaign search final strategy path is invalid.");
        if (manifest.SchemaVersion >= 4 &&
            ((manifest.FinalStrategyPath is null) != (manifest.FinalStrategyFingerprint is null) ||
             (manifest.FinalStrategyPath is null) != (manifest.FinalStrategyContentHash is null) ||
             manifest.FinalStrategyFingerprint is not null &&
             !CampaignSearchOptions.IsSha256(manifest.FinalStrategyFingerprint) ||
             manifest.FinalStrategyContentHash is not null &&
             !CampaignSearchOptions.IsSha256(manifest.FinalStrategyContentHash)))
            throw new InvalidDataException("Campaign search final strategy identity is invalid.");
        if (manifest.Status == CampaignSearchStatus.FrontierExhausted &&
            (manifest.FrontierArtifacts.Count == 0 || manifest.BestFailure is null))
            throw new InvalidDataException("An exhausted campaign search must retain its retry frontier and best failure.");
        if (manifest.Status == CampaignSearchStatus.CampaignCompleted &&
            (manifest.FinalStrategyPath is null || manifest.SchemaVersion >= 4 &&
             (manifest.FinalStrategyFingerprint is null || manifest.FinalStrategyContentHash is null)))
            throw new InvalidDataException("A completed campaign search must identify its final strategy.");
        if (manifest.BestFailure is { } failure && failure.FailureMargin is { } margin)
        {
            if (margin.TotalEnemyCount != failure.RemainingEnemies.Sum(enemy => enemy.Count) +
                                          failure.QueuedEnemies.Sum(enemy => enemy.Count) ||
                margin.FatalFrameEscapedEnemyCount != failure.FatalEscapedEnemies.Count)
                throw new InvalidDataException("Campaign search best-failure composition is inconsistent.");
        }
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
