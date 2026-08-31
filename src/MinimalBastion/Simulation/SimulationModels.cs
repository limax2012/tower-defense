using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Towers;
using System.Text.Json.Serialization;

namespace MinimalBastion.Simulation;

public enum AutoPlayerStrategy
{
    Conservative,
    Economy,
    Aggressive,
    UpgradeFocused,
    Spam,
    AntiSwarm,
    AntiArmor,
    LongRange,
    Control,
    Synergy,
    Tactical,
    Adaptive,
    Experienced,
    Randomized
}

public sealed class SimulationOptions
{
    public int Seed { get; init; } = 1337;
    public AutoPlayerStrategy Strategy { get; init; } = AutoPlayerStrategy.Adaptive;
    public string? MapId { get; init; }
    public string DifficultyId { get; init; } = DifficultyCatalog.LegacyId;
    public string ChallengeId { get; init; } = ChallengeCatalog.DefaultId;
    public float StepSeconds { get; init; } = 0.05f;
    public float MaximumSimulatedSeconds { get; init; } = 3_600f;
    public int MaximumWave { get; init; } = int.MaxValue;
    public bool ContinueEndless { get; init; }
    public string? ForcedTowerId { get; init; }
    public string? ForcedDoctrineId { get; init; }
    public string? ForcedSpecializationId { get; init; }
    public bool UseProtocols { get; init; } = true;
    public bool UseApexUpgrades { get; init; } = true;
    public bool UseCounterSupport { get; init; } = true;
    public bool UseCounterAttackers { get; init; } = true;
    public bool HoldBuild { get; init; }
    public bool HoldFootprint { get; init; }
    public WavePlan? WavePlan { get; init; }
}

public sealed class SimulationRunResult
{
    public required string MapId { get; init; }
    public required string DifficultyId { get; init; }
    public required string ChallengeId { get; init; }
    public required AutoPlayerStrategy Strategy { get; init; }
    public required int Seed { get; init; }
    public string? ForcedTowerId { get; init; }
    public string? ForcedDoctrineId { get; init; }
    public string? ForcedSpecializationId { get; init; }
    public string? ForcedBuildPath => ForcedTowerId is null || ForcedDoctrineId is null || ForcedSpecializationId is null
        ? null
        : $"{ForcedTowerId}:{ForcedDoctrineId}>{ForcedSpecializationId}";
    public required string Result { get; init; }
    public required int WaveReached { get; init; }
    public int CampaignWaveCount { get; init; } = GameConstants.CampaignWaveCount;
    public bool CampaignCleared { get; init; }
    public required int LivesRemaining { get; init; }
    public required int Kills { get; init; }
    public required int EscapedEnemies { get; init; }
    public required int CreditsEarned { get; init; }
    public required int CreditsSpent { get; init; }
    public required int CreditsUnspent { get; init; }
    public required int SaleCreditsRecovered { get; init; }
    public int EarlyStartCreditsEarned { get; init; }
    public required float SimulatedSeconds { get; init; }
    public required IReadOnlyDictionary<string, TowerRunMetrics> Towers { get; init; }
    public required IReadOnlyDictionary<string, int> EnemyKills { get; init; }
    public required IReadOnlyDictionary<string, int> EnemyLeaks { get; init; }
    public IReadOnlyList<SimulationRemainingEnemy> RemainingEnemies { get; init; } = Array.Empty<SimulationRemainingEnemy>();
    public IReadOnlyList<SimulationRemainingEnemy> QueuedEnemies { get; init; } = Array.Empty<SimulationRemainingEnemy>();
    public int QueuedEnemiesRemaining { get; init; }
    public float RemainingHealth => RemainingEnemies.Sum(enemy => enemy.CurrentHealth);
    public float RemainingShield => RemainingEnemies.Sum(enemy => enemy.Shield);
    public float RemainingArmorAdjustedDurability => RemainingEnemies.Sum(enemy => enemy.ArmorAdjustedDurability);
    public float QueuedHealth => QueuedEnemies.Sum(enemy => enemy.CurrentHealth);
    public float QueuedShield => QueuedEnemies.Sum(enemy => enemy.Shield);
    public float QueuedArmorAdjustedDurability => QueuedEnemies.Sum(enemy => enemy.ArmorAdjustedDurability);
    public int RemainingEnemyCount => RemainingEnemies.Sum(enemy => enemy.Count);
    public SimulationFailureMargin? FailureMargin { get; init; }
    public IReadOnlyList<SimulationEscapedEnemy> FatalEscapedEnemies { get; init; } = Array.Empty<SimulationEscapedEnemy>();
    [JsonIgnore]
    public SimulationEscapedEnemy? FatalEscapedEnemy => FatalEscapedEnemies.FirstOrDefault() ?? FailureMargin?.FatalEscapedEnemy;
    public int FatalEscapedEnemyCount => FatalEscapedEnemies.Count > 0
        ? FatalEscapedEnemies.Count
        : FatalEscapedEnemy is null ? 0 : 1;
    public float FatalEscapedArmorAdjustedDurability => FatalEscapedEnemies.Count > 0
        ? FatalEscapedEnemies.Sum(enemy => enemy.ArmorAdjustedDurability)
        : FatalEscapedEnemy?.ArmorAdjustedDurability ?? 0;
    public int UnresolvedEnemyCount => RemainingEnemyCount + QueuedEnemiesRemaining + FatalEscapedEnemyCount;
    public float UnresolvedArmorAdjustedDurability => RemainingArmorAdjustedDurability +
        QueuedArmorAdjustedDurability + FatalEscapedArmorAdjustedDurability;
    public required IReadOnlyList<WaveRunMetrics> Waves { get; init; }
    public IReadOnlyList<SimulationTowerPlacement> FinalTowers { get; init; } = Array.Empty<SimulationTowerPlacement>();
    public IReadOnlyList<SimulationPulsePlateDeployment> PulsePlateDeployments { get; init; } =
        Array.Empty<SimulationPulsePlateDeployment>();
    public IReadOnlyList<SimulationProtocolActivation> ProtocolActivations { get; init; } =
        Array.Empty<SimulationProtocolActivation>();
    public int EmergencyDeployments { get; init; }
    public int EmergencyDirectPurchases { get; init; }
    public int EmergencyTriggers { get; init; }
    public int EmergencyHits { get; init; }
    public int EmergencyKills { get; init; }
    public float EmergencyDamage { get; init; }
    public int GeneratorPurchases { get; init; }
    public int GeneratorUpgrades { get; init; }
    public int GeneratedCharges { get; init; }
    public int Overdrives { get; init; }
    public bool ProtocolsEnabled { get; init; } = true;
    public bool ApexUpgradesEnabled { get; init; } = true;
    public int EndlessDepth => CampaignCleared ? Math.Max(0, WaveReached - CampaignWaveCount) : 0;
    public bool Won => Result is "Victory" or "WaveLimit";
}

public sealed record SimulationTowerPlacement(
    int Id,
    string TowerId,
    float X,
    float Y,
    int Level,
    string? DoctrineId,
    string? SpecializationId,
    bool IsApex,
    TargetMode TargetMode,
    int InvestedCredits,
    float LifetimeDamage,
    int LifetimeKills,
    float LifetimeSupportDamageEquivalent,
    float LifetimeExposeDamageEquivalent,
    float LifetimeArmorBreakDamageEquivalent,
    float LifetimeControlSeconds,
    float LifetimeExposeSeconds,
    float LifetimeArmorBreakSeconds,
    string? PowerNodeId)
{
    public float LifetimeContributionDamage => LifetimeDamage + LifetimeSupportDamageEquivalent +
        LifetimeExposeDamageEquivalent + LifetimeArmorBreakDamageEquivalent;
    public float LifetimeContributionPerCredit => InvestedCredits <= 0 ? 0 : LifetimeContributionDamage / InvestedCredits;
}

public sealed record SimulationRemainingEnemy(
    string EnemyId,
    string DisplayName,
    string Rank,
    string SignalRole,
    int Count,
    float CurrentHealth,
    float MaxHealth,
    float Shield,
    float ArmorAdjustedDurability,
    float FurthestProgress);

public sealed record SimulationEscapedEnemy(
    string EnemyId,
    string DisplayName,
    string Rank,
    string SignalRole,
    float CurrentHealth,
    float MaxHealth,
    float Shield,
    float ArmorAdjustedDurability,
    float Progress);

public sealed record SimulationPulsePlateDeployment(
    int Wave,
    int PlateId,
    float ElapsedSeconds,
    float WaveElapsedSeconds,
    bool DirectPurchase,
    int Cost,
    float PathProgress,
    float X,
    float Y,
    float LeadProgress,
    int LiveEnemyCount,
    int QueuedEnemyCount,
    int TriggerCount,
    int HitCount,
    int KillCount,
    float Damage);

public sealed record SimulationProtocolActivation(
    int Wave,
    float ElapsedSeconds,
    float WaveElapsedSeconds,
    int TowerId,
    string TowerType,
    bool IsApex,
    bool IsAutonomous,
    TargetMode TargetMode,
    int LiveEnemyCount,
    int QueuedEnemyCount,
    float LeadProgress,
    int EliteEnemyCount,
    int BossEnemyCount,
    int SignalEnemyCount,
    float LiveArmorAdjustedDurability,
    float RankedArmorAdjustedDurability,
    IReadOnlyList<SimulationRemainingEnemy> LiveComposition)
{
    public int RankedEnemyCount => EliteEnemyCount + BossEnemyCount;
}

public sealed record SimulationFailureMargin(
    int Wave,
    int LiveEnemyCount,
    int QueuedEnemyCount,
    float LiveHealth,
    float LiveShield,
    float LiveArmorAdjustedDurability,
    float QueuedHealth,
    float QueuedShield,
    float QueuedArmorAdjustedDurability,
    float FurthestProgress,
    int WaveEnemyCount,
    float WaveArmorAdjustedDurability)
{
    public SimulationEscapedEnemy? FatalEscapedEnemy { get; init; }
    public int FatalFrameEscapedEnemyCount { get; init; }
    public float FatalFrameEscapedHealth { get; init; }
    public float FatalFrameEscapedShield { get; init; }
    public float FatalFrameEscapedArmorAdjustedDurability { get; init; }
    public float FatalFrameFurthestProgress { get; init; }
    public int FatalEscapedEnemyCount => FatalFrameEscapedEnemyCount > 0
        ? FatalFrameEscapedEnemyCount
        : FatalEscapedEnemy is null ? 0 : 1;
    public float FatalEscapedHealth => FatalFrameEscapedEnemyCount > 0
        ? FatalFrameEscapedHealth
        : FatalEscapedEnemy?.CurrentHealth ?? 0;
    public float FatalEscapedShield => FatalFrameEscapedEnemyCount > 0
        ? FatalFrameEscapedShield
        : FatalEscapedEnemy?.Shield ?? 0;
    public float FatalEscapedArmorAdjustedDurability => FatalFrameEscapedEnemyCount > 0
        ? FatalFrameEscapedArmorAdjustedDurability
        : FatalEscapedEnemy?.ArmorAdjustedDurability ?? 0;
    public float FatalEscapedFurthestProgress => FatalFrameEscapedEnemyCount > 0
        ? FatalFrameFurthestProgress
        : FatalEscapedEnemy?.Progress ?? 0;
    public int TotalEnemyCount => LiveEnemyCount + QueuedEnemyCount;
    public float LiveDurability => LiveHealth + LiveShield;
    public float QueuedDurability => QueuedHealth + QueuedShield;
    public float TotalDurability => LiveDurability + QueuedDurability;
    public float TotalArmorAdjustedDurability => LiveArmorAdjustedDurability + QueuedArmorAdjustedDurability;
    public int UnresolvedEnemyCount => TotalEnemyCount + FatalEscapedEnemyCount;
    public float FatalEscapedDurability => FatalEscapedHealth + FatalEscapedShield;
    public float UnresolvedDurability => TotalDurability + FatalEscapedDurability;
    public float UnresolvedArmorAdjustedDurability => TotalArmorAdjustedDurability +
        FatalEscapedArmorAdjustedDurability;
    public float UnresolvedFurthestProgress => MathF.Max(FurthestProgress, FatalEscapedFurthestProgress);
    public float RemainingEnemyFraction => WaveEnemyCount <= 0 ? 0 : TotalEnemyCount / (float)WaveEnemyCount;
    public float RemainingArmorAdjustedDurabilityFraction => WaveArmorAdjustedDurability <= 0
        ? 0
        : TotalArmorAdjustedDurability / WaveArmorAdjustedDurability;
    public float UnresolvedEnemyFraction => WaveEnemyCount <= 0 ? 0 : UnresolvedEnemyCount / (float)WaveEnemyCount;
    public float UnresolvedArmorAdjustedDurabilityFraction => WaveArmorAdjustedDurability <= 0
        ? 0
        : UnresolvedArmorAdjustedDurability / WaveArmorAdjustedDurability;
}

public sealed class TowerRunMetrics
{
    public string TowerId { get; init; } = "";
    public int Purchases { get; set; }
    public int Upgrades { get; set; }
    public int ApexUpgrades { get; set; }
    public int ApexCreditsSpent { get; set; }
    public int Sales { get; set; }
    public int CreditsSpent { get; set; }
    public int CreditsRecovered { get; set; }
    public int Hits { get; set; }
    public int Kills { get; set; }
    public int Overdrives { get; set; }
    public float Damage { get; set; }
    public float ShieldDamage { get; set; }
    public float ArmorAbsorbed { get; set; }
    public float Overkill { get; set; }
    public float SupportDamageEquivalent { get; set; }
    public float ExposeDamageEquivalent { get; set; }
    public float ArmorBreakDamageEquivalent { get; set; }
    public float SupportedAttackSeconds { get; set; }
    public float SupportedRangeSeconds { get; set; }
    public Dictionary<string, float> StatusEnemySeconds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> StatusMagnitudeSeconds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, float> DamageByLevel { get; init; } = new();
    public Dictionary<string, int> Doctrines { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Specializations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> BuildPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public float ContributionDamage => Damage + SupportDamageEquivalent + ExposeDamageEquivalent + ArmorBreakDamageEquivalent;
    public float DamagePerCredit => CreditsSpent <= 0 ? 0 : ContributionDamage / CreditsSpent;

    public void RecordBranchUpgrade(TowerInstance tower)
    {
        if (tower.SpecializationId is { } specializationId)
        {
            Specializations[specializationId] = Specializations.GetValueOrDefault(specializationId) + 1;
            if (tower.DoctrineId is { } doctrineId)
            {
                var path = $"{doctrineId}>{specializationId}";
                BuildPaths[path] = BuildPaths.GetValueOrDefault(path) + 1;
            }
        }
        else if (tower.DoctrineId is { } doctrineId)
            Doctrines[doctrineId] = Doctrines.GetValueOrDefault(doctrineId) + 1;
    }
}

public sealed class WaveRunMetrics
{
    public int Wave { get; init; }
    public string Archetype { get; init; } = "";
    public float DurationSeconds { get; set; }
    public int StartingLives { get; init; }
    public int EndingLives { get; set; }
    public int Kills { get; set; }
    public int Leaks { get; set; }
    public int CreditsSpent { get; set; }
    public int EndingCredits { get; set; }
    public int LivesLost => StartingLives - EndingLives;
}

public sealed class SimulationBatchResult
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required IReadOnlyList<SimulationRunResult> Runs { get; init; }
    public int Wins => Runs.Count(x => x.Won);
    public float WinRate => Runs.Count == 0 ? 0 : Wins / (float)Runs.Count;
    public int CampaignClears => Runs.Count(x => x.CampaignCleared);
    public int DeepestWave => Runs.Count == 0 ? 0 : Runs.Max(x => x.WaveReached);
    public float AverageWaveReached => Runs.Count == 0 ? 0 : (float)Runs.Average(x => x.WaveReached);
    public float AverageLivesRemaining => Runs.Count == 0 ? 0 : (float)Runs.Average(x => x.LivesRemaining);
}

internal readonly record struct ThreatProfile(
    int Total,
    float Swarm,
    float Fast,
    float Armored,
    float Shielded,
    float Durable,
    bool HasElite,
    bool HasBoss)
{
    public static ThreatProfile From(Data.WaveDefinition? wave, IReadOnlyDictionary<string, Data.EnemyDefinition> enemies)
    {
        if (wave is null) return new ThreatProfile(0, 0, 0, 0, 0, 0, false, false);
        var total = wave.Groups.Sum(x => x.Count);
        if (total <= 0) return new ThreatProfile(0, 0, 0, 0, 0, 0, false, false);

        float CountWhere(Func<Data.EnemyDefinition, bool> predicate) => wave.Groups.Sum(group =>
            enemies.TryGetValue(group.EnemyId, out var enemy) && predicate(enemy) ? group.Count : 0);

        return new ThreatProfile(
            total,
            CountWhere(x => x.MaxHealth <= 100) / total,
            CountWhere(x => x.Speed >= 100) / total,
            CountWhere(x => x.Armor > 0) / total,
            CountWhere(x => x.Shield > 0) / total,
            CountWhere(x => x.MaxHealth >= 450 || x.RegenerationPerSecond > 0) / total,
            wave.Groups.Any(x => x.Rank.Equals("Elite", StringComparison.OrdinalIgnoreCase)),
            wave.Groups.Any(x => x.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase)));
    }
}

internal readonly record struct PurchaseOption(Data.TowerDefinition Definition, Microsoft.Xna.Framework.Vector2 Position, float Score);
internal readonly record struct UpgradeOption(Towers.TowerInstance Tower, string? DoctrineId, string? SpecializationId, float Score);
