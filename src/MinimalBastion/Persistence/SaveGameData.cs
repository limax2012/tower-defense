using MinimalBastion.Core;

namespace MinimalBastion.Persistence;

public sealed class SaveGameData
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsCoOp { get; set; }
    public string MapId { get; set; } = "";
    public string DifficultyId { get; set; } = "";
    public float Speed { get; set; } = 1f;
    public float OverdriveCooldownRemaining { get; set; }
    public int AutoOverdriveTowerId { get; set; }
    public int EmergencyInventory { get; set; }
    public int EmergencyDirectPurchasesThisWave { get; set; }
    public int NextEnemyId { get; set; } = 1;
    public int NextTowerId { get; set; } = 1;
    public int NextEmergencyDefenseId { get; set; } = 1;
    public EconomySaveData Economy { get; set; } = new();
    public WaveSaveData Waves { get; set; } = new();
    public List<TowerSaveData> Towers { get; set; } = new();
    public List<PulsePlateSaveData> PulsePlates { get; set; } = new();
    public GeneratorSaveData? Generator { get; set; }
    public RunStatisticsSaveData Statistics { get; set; } = new();
}

public sealed class EconomySaveData
{
    public int Credits { get; set; }
    public int Lives { get; set; }
    public int TotalKills { get; set; }
    public int EscapedEnemies { get; set; }
    public int TotalCreditsSpent { get; set; }
    public int KillCreditsEarned { get; set; }
    public int WaveCreditsEarned { get; set; }
    public int EarlyStartCreditsEarned { get; set; }
    public int SaleCreditsRecovered { get; set; }
}

public sealed class WaveSaveData
{
    public int CurrentWaveNumber { get; set; }
    public float IntermissionRemaining { get; set; }
    public bool IsFinalWaveCleared { get; set; }
    public bool EndlessModeEnabled { get; set; }
}

public sealed class TowerSaveData
{
    public int Id { get; set; }
    public int OwnerPlayerId { get; set; } = 1;
    public string DefinitionId { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public int LevelIndex { get; set; }
    public string? SpecializationId { get; set; }
    public float CooldownRemaining { get; set; }
    public TargetMode TargetMode { get; set; }
    public int InvestedCredits { get; set; }
    public float OverdriveRemaining { get; set; }
    public float LifetimeDamage { get; set; }
    public int LifetimeKills { get; set; }
    public float LifetimeSupportDamageEquivalent { get; set; }
    public float LifetimeControlSeconds { get; set; }
    public float LifetimeExposeSeconds { get; set; }
    public float LifetimeArmorBreakSeconds { get; set; }
}

public sealed class PulsePlateSaveData
{
    public int Id { get; set; }
    public int OwnerPlayerId { get; set; } = 1;
    public float X { get; set; }
    public float Y { get; set; }
    public int ChargesRemaining { get; set; }
    public float ArmRemaining { get; set; }
    public float CooldownRemaining { get; set; }
    public List<int> HandledEnemyIds { get; set; } = new();
}

public sealed class GeneratorSaveData
{
    public int OwnerPlayerId { get; set; } = 1;
    public float X { get; set; }
    public float Y { get; set; }
    public int LevelIndex { get; set; }
    public int InvestedCredits { get; set; }
    public float ProductionRemaining { get; set; }
}

public sealed class RunStatisticsSaveData
{
    public float SimulatedSeconds { get; set; }
    public int EmergencyDeployments { get; set; }
    public int EmergencyDirectPurchases { get; set; }
    public int EmergencyTriggers { get; set; }
    public int EmergencyHits { get; set; }
    public int EmergencyKills { get; set; }
    public float EmergencyDamage { get; set; }
    public int GeneratedCharges { get; set; }
    public int GeneratorPurchases { get; set; }
    public int GeneratorUpgrades { get; set; }
    public List<RunTowerStatisticsSaveData> Towers { get; set; } = new();
    public List<RunEnemyStatisticsSaveData> Enemies { get; set; } = new();
}

public sealed class RunTowerStatisticsSaveData
{
    public string TowerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Purchases { get; set; }
    public int Upgrades { get; set; }
    public int Sales { get; set; }
    public int CreditsSpent { get; set; }
    public int CreditsRecovered { get; set; }
    public int Hits { get; set; }
    public int Kills { get; set; }
    public int Overdrives { get; set; }
    public float Damage { get; set; }
    public float SupportDamageEquivalent { get; set; }
    public float ControlSeconds { get; set; }
    public float ExposeSeconds { get; set; }
    public float ArmorBreakSeconds { get; set; }
    public float ArmorAbsorbed { get; set; }
    public float Overkill { get; set; }
    public Dictionary<string, int> Specializations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RunEnemyStatisticsSaveData
{
    public string EnemyId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Kills { get; set; }
    public int Escapes { get; set; }
    public int LivesLost { get; set; }
}
