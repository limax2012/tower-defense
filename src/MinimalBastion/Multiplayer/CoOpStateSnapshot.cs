using MinimalBastion.Core;
using MinimalBastion.Effects;
using MinimalBastion.Persistence;

namespace MinimalBastion.Multiplayer;

public sealed class CoOpStateSnapshot
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string RunId { get; set; } = "";
    public string MapId { get; set; } = "";
    public string DifficultyId { get; set; } = "";
    public string ChallengeId { get; set; } = "standard";
    public long Tick { get; set; }
    public int ReadyMask { get; set; }
    public bool WaveStartQueued { get; set; }
    public bool WaveEarlyBonusQueued { get; set; }
    public bool IsPaused { get; set; }
    public float Speed { get; set; } = 1f;
    public float OverdriveCooldownRemaining { get; set; }
    public int AutoOverdriveTowerId { get; set; }
    public int EmergencyInventory { get; set; }
    public int EmergencyDirectPurchasesThisWave { get; set; }
    public int NextEnemyId { get; set; } = 1;
    public int NextTowerId { get; set; } = 1;
    public int NextEmergencyDefenseId { get; set; } = 1;
    public bool IsVictory { get; set; }
    public bool IsDefeat { get; set; }
    public string? AnnouncementTitle { get; set; }
    public string? AnnouncementSubtitle { get; set; }
    public float AnnouncementRemaining { get; set; }
    public bool AnnouncementPositive { get; set; }
    public EconomySaveData Economy { get; set; } = new();
    public WaveRuntimeState Waves { get; set; } = new();
    public List<TowerSaveData> Towers { get; set; } = new();
    public List<EnemyRuntimeState> Enemies { get; set; } = new();
    public List<ProjectileRuntimeState> Projectiles { get; set; } = new();
    public List<PulsePlateSaveData> PulsePlates { get; set; } = new();
    public GeneratorSaveData? Generator { get; set; }
    public RunStatisticsSaveData Statistics { get; set; } = new();
    public List<ScheduledCommandState> PendingCommands { get; set; } = new();
}

public sealed class WaveRuntimeState
{
    public int CurrentWaveNumber { get; set; }
    public int ActiveWaveNumber { get; set; }
    public int GroupIndex { get; set; }
    public int SpawnedInGroup { get; set; }
    public float GroupTimer { get; set; }
    public float DelayRemaining { get; set; }
    public float IntermissionRemaining { get; set; }
    public bool IsFinalWaveCleared { get; set; }
    public bool EndlessModeEnabled { get; set; }
    public int QueuedEnemies { get; set; }
}

public sealed class EnemyRuntimeState
{
    public int Id { get; set; }
    public string DefinitionId { get; set; } = "";
    public EnemyRank Rank { get; set; }
    public float HealthMultiplier { get; set; } = 1f;
    public float SpeedMultiplier { get; set; } = 1f;
    public float DistanceAlongPath { get; set; }
    public float Health { get; set; }
    public float Shield { get; set; }
    public float DamagePauseTimer { get; set; }
    public float KnockbackGraceRemaining { get; set; }
    public bool IsDead { get; set; }
    public bool HasEscaped { get; set; }
    public bool BossPhaseActive { get; set; }
    public bool BossPhasePulsePending { get; set; }
    public List<ActiveStatus> Statuses { get; set; } = new();
}

public sealed class ProjectileRuntimeState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float AimX { get; set; }
    public float AimY { get; set; }
    public int TargetEnemyId { get; set; }
    public float Speed { get; set; }
    public int Kind { get; set; }
    public float SplashRadius { get; set; }
    public int SplashTargetLimit { get; set; }
    public float Damage { get; set; }
    public float ArmorPierce { get; set; }
    public bool IgnoreShield { get; set; }
    public bool IsDamageOverTime { get; set; }
    public StatusApplication? Status { get; set; }
    public int SourceTowerId { get; set; }
    public uint PackedColor { get; set; }
    public float Radius { get; set; }
}

public sealed class ScheduledCommandState
{
    public long Tick { get; set; }
    public GameCommand Command { get; set; } = new();
}
