using MaximalBastion.Core;
using MaximalBastion.Data;

namespace MaximalBastion.Simulation.Planning;

public sealed record ObservedTower(int Id, string DefinitionId, float X, float Y, int Level,
    string? Doctrine, string? Specialization, bool Apex, TargetMode TargetMode, int Investment);
public sealed record ObservedEnemy(int Id, string DefinitionId, float X, float Y, float Progress,
    float Health, float Shield, float Armor, float Speed, EnemyRank Rank, string SignalRole);
public sealed record ObservedWaveGroup(string EnemyId, int Count, string Rank);

/// <summary>Detached observations prevent player policies from traversing session state or pending spawns.</summary>
public sealed record PlanningObservation
{
    public string InformationProfile { get; init; } = "informed";
    public int CurrentWave { get; init; }
    public bool WaveActive { get; init; }
    public int Credits { get; init; }
    public int Lives { get; init; }
    public int PlateInventory { get; init; }
    public int DirectPlatePrice { get; init; }
    public bool ApexUnlocked { get; init; }
    public IReadOnlyList<ObservedTower> Towers { get; init; } = Array.Empty<ObservedTower>();
    public IReadOnlyList<ObservedEnemy> Enemies { get; init; } = Array.Empty<ObservedEnemy>();
    public IReadOnlyList<ObservedWaveGroup>? OracleWaveComposition { get; init; }

    public static PlanningObservation Read(GameSession session, string informationProfile)
    {
        if (informationProfile is not ("oracle" or "informed"))
            throw new ArgumentException("Information profile must be oracle or informed.", nameof(informationProfile));
        WaveDefinition? wave = informationProfile == "oracle"
            ? session.Waves.IsActive ? session.Waves.ActiveWave : session.Waves.NextWave : null;
        return new()
        {
            InformationProfile = informationProfile, CurrentWave = session.CurrentWave,
            WaveActive = session.Waves.IsActive, Credits = session.Economy.Credits, Lives = session.Economy.Lives,
            PlateInventory = session.EmergencyInventory, DirectPlatePrice = session.CurrentEmergencyDirectPurchaseCost,
            ApexUnlocked = session.ApexUpgradesUnlocked,
            Towers = session.Towers.Select(tower => new ObservedTower(tower.Id, tower.Definition.Id,
                tower.Position.X, tower.Position.Y, tower.LevelIndex + 1, tower.DoctrineId, tower.SpecializationId,
                tower.IsApex, tower.TargetMode, tower.InvestedCredits)).ToArray(),
            Enemies = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped)
                .Select(enemy => new ObservedEnemy(enemy.Id, enemy.Definition.Id, enemy.Position.X, enemy.Position.Y,
                    enemy.PathProgress, enemy.Health, enemy.Shield, enemy.EffectiveArmor, enemy.CurrentSpeed,
                    enemy.Rank, enemy.SignalRole.ToString())).ToArray(),
            OracleWaveComposition = wave?.Groups.Select(group => new ObservedWaveGroup(group.EnemyId, group.Count, group.Rank)).ToArray()
        };
    }
}
