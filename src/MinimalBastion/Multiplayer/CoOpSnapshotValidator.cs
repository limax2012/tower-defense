using MinimalBastion.Combat;
using MinimalBastion.Effects;

namespace MinimalBastion.Multiplayer;

internal static class CoOpSnapshotValidator
{
    private const int MaximumTowers = 1024;
    private const int MaximumEnemies = 4096;
    private const int MaximumProjectiles = 4096;
    private const int MaximumPulsePlates = 256;
    private const int MaximumStatusesPerEnemy = 8;
    private const int MaximumStatisticsEntries = 4096;
    private const int MaximumRunIdLength = 64;
    private const int MaximumAnnouncementTitleLength = 128;
    private const int MaximumAnnouncementSubtitleLength = 512;

    public static void Validate(CoOpStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != CoOpStateSnapshot.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported co-op state schema {snapshot.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(snapshot.RunId) || snapshot.RunId.Length > MaximumRunIdLength ||
            string.IsNullOrWhiteSpace(snapshot.MapId) || snapshot.MapId.Length > 128 ||
            string.IsNullOrWhiteSpace(snapshot.DifficultyId) || snapshot.DifficultyId.Length > 128 ||
            string.IsNullOrWhiteSpace(snapshot.ChallengeId) || snapshot.ChallengeId.Length > 128 ||
            snapshot.AnnouncementTitle is { Length: > MaximumAnnouncementTitleLength } ||
            snapshot.AnnouncementSubtitle is { Length: > MaximumAnnouncementSubtitleLength } ||
            snapshot.Tick < 0 || (snapshot.ReadyMask & ~0b11) != 0 ||
            snapshot.IsPaused && snapshot.PausedByPlayerId is not (1 or 2) ||
            !snapshot.IsPaused && snapshot.PausedByPlayerId != 0 ||
            !IsPositiveFinite(snapshot.Speed) || !IsNonnegativeFinite(snapshot.OverdriveCooldownRemaining) ||
            !IsNonnegativeFinite(snapshot.AnnouncementRemaining) || snapshot.EmergencyInventory < 0 ||
            snapshot.EmergencyDirectPurchasesThisWave < 0 || snapshot.NextEnemyId <= 0 ||
            snapshot.NextTowerId <= 0 || snapshot.NextEmergencyDefenseId <= 0 ||
            snapshot.IsVictory && snapshot.IsDefeat)
            throw new InvalidDataException("Co-op snapshot header is structurally invalid.");

        var economy = snapshot.Economy ?? throw new InvalidDataException("Co-op snapshot economy is missing.");
        var waves = snapshot.Waves ?? throw new InvalidDataException("Co-op snapshot wave state is missing.");
        var towers = snapshot.Towers ?? throw new InvalidDataException("Co-op snapshot tower state is missing.");
        var enemies = snapshot.Enemies ?? throw new InvalidDataException("Co-op snapshot enemy state is missing.");
        var projectiles = snapshot.Projectiles ?? throw new InvalidDataException("Co-op snapshot projectile state is missing.");
        var plates = snapshot.PulsePlates ?? throw new InvalidDataException("Co-op snapshot Pulse Plate state is missing.");
        var statistics = snapshot.Statistics ?? throw new InvalidDataException("Co-op snapshot statistics are missing.");
        var pending = snapshot.PendingCommands ?? throw new InvalidDataException("Co-op snapshot pending commands are missing.");

        if (economy.Credits < 0 || economy.Lives < 0 || economy.TotalKills < 0 || economy.EscapedEnemies < 0 ||
            economy.TotalCreditsSpent < 0 || economy.KillCreditsEarned < 0 || economy.WaveCreditsEarned < 0 ||
            economy.EarlyStartCreditsEarned < 0 || economy.SaleCreditsRecovered < 0)
            throw new InvalidDataException("Co-op snapshot economy is structurally invalid.");

        if (waves.CurrentWaveNumber < 0 || waves.ActiveWaveNumber < 0 || waves.GroupIndex < 0 ||
            waves.SpawnedInGroup < 0 || waves.QueuedEnemies < 0 || !float.IsFinite(waves.GroupTimer) ||
            !float.IsFinite(waves.DelayRemaining) || !IsNonnegativeFinite(waves.IntermissionRemaining))
            throw new InvalidDataException("Co-op snapshot wave state is structurally invalid.");

        ValidateCount(towers.Count, MaximumTowers, "tower");
        if (towers.Any(tower => tower is null) || towers.Select(tower => tower.Id).Distinct().Count() != towers.Count ||
            towers.Any(tower => tower.Id <= 0 || tower.OwnerPlayerId is < 1 or > 2 ||
                string.IsNullOrWhiteSpace(tower.DefinitionId) || tower.DefinitionId.Length > 128 ||
                !float.IsFinite(tower.X) || !float.IsFinite(tower.Y) || tower.LevelIndex < 0 ||
                !Enum.IsDefined(tower.TargetMode) || tower.InvestedCredits < 0 || !float.IsFinite(tower.CooldownRemaining) ||
                !IsNonnegativeFinite(tower.OverdriveRemaining) || !IsNonnegativeFinite(tower.LifetimeDamage) ||
                tower.LifetimeKills < 0 || !IsNonnegativeFinite(tower.LifetimeSupportDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeExposeDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeArmorBreakDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeControlSeconds) || !IsNonnegativeFinite(tower.LifetimeExposeSeconds) ||
                !IsNonnegativeFinite(tower.LifetimeArmorBreakSeconds)))
            throw new InvalidDataException("Co-op snapshot tower state is structurally invalid.");
        if (towers.Any(tower => tower.Id >= snapshot.NextTowerId) ||
            snapshot.AutoOverdriveTowerId != 0 && towers.All(tower => tower.Id != snapshot.AutoOverdriveTowerId))
            throw new InvalidDataException("Co-op snapshot tower identity state is inconsistent.");

        ValidateCount(enemies.Count, MaximumEnemies, "enemy");
        if (enemies.Any(enemy => enemy is null) || enemies.Select(enemy => enemy.Id).Distinct().Count() != enemies.Count)
            throw new InvalidDataException("Co-op snapshot enemy identities are structurally invalid.");
        foreach (var enemy in enemies)
        {
            var statuses = enemy.Statuses ?? throw new InvalidDataException("Co-op snapshot enemy statuses are missing.");
            if (enemy.Id <= 0 || string.IsNullOrWhiteSpace(enemy.DefinitionId) || enemy.DefinitionId.Length > 128 ||
                !Enum.IsDefined(enemy.Rank) || !IsPositiveFinite(enemy.HealthMultiplier) ||
                !IsPositiveFinite(enemy.SpeedMultiplier) || !IsNonnegativeFinite(enemy.DistanceAlongPath) ||
                !IsNonnegativeFinite(enemy.Health) || !IsNonnegativeFinite(enemy.Shield) ||
                !IsNonnegativeFinite(enemy.DamagePauseTimer) || !IsNonnegativeFinite(enemy.KnockbackGraceRemaining) ||
                statuses.Count > MaximumStatusesPerEnemy || statuses.Any(status => status is null ||
                    !Enum.IsDefined(status.Type) || !IsPositiveFinite(status.RemainingSeconds) ||
                    !IsPositiveFinite(status.Magnitude) || !IsPositiveFinite(status.TickInterval) ||
                    !IsNonnegativeFinite(status.TickProgress)))
                throw new InvalidDataException("Co-op snapshot enemy state is structurally invalid.");
        }
        if (enemies.Any(enemy => enemy.Id >= snapshot.NextEnemyId))
            throw new InvalidDataException("Co-op snapshot enemy identity state is inconsistent.");

        ValidateCount(projectiles.Count, MaximumProjectiles, "projectile");
        if (projectiles.Any(projectile => projectile is null ||
            !float.IsFinite(projectile.X) || !float.IsFinite(projectile.Y) ||
            !float.IsFinite(projectile.AimX) || !float.IsFinite(projectile.AimY) ||
            projectile.TargetEnemyId < 0 ||
            !Enum.IsDefined(typeof(ProjectileKind), projectile.Kind) || !IsNonnegativeFinite(projectile.Speed) ||
            !IsNonnegativeFinite(projectile.SplashRadius) || projectile.SplashTargetLimit < 0 ||
            !IsNonnegativeFinite(projectile.Damage) || !IsPositiveFinite(projectile.PriorityDamageMultiplier) ||
            projectile.PriorityDamageMultiplier > 3 || !IsNonnegativeFinite(projectile.ArmorPierce) ||
            !IsNonnegativeFinite(projectile.Radius) || !IsValidStatus(projectile.Status)))
            throw new InvalidDataException("Co-op snapshot projectile state is structurally invalid.");
        if (projectiles.Any(projectile => projectile.Kind == (int)ProjectileKind.Homing &&
            (projectile.TargetEnemyId <= 0 || enemies.All(enemy => enemy.Id != projectile.TargetEnemyId))))
            throw new InvalidDataException("Co-op snapshot homing projectile target is missing.");

        ValidateCount(plates.Count, MaximumPulsePlates, "Pulse Plate");
        if (plates.Any(plate => plate is null) || plates.Select(plate => plate.Id).Distinct().Count() != plates.Count ||
            plates.Any(plate => plate.Id <= 0 || plate.OwnerPlayerId is < 1 or > 2 ||
                !float.IsFinite(plate.X) || !float.IsFinite(plate.Y) || plate.ChargesRemaining < 0 ||
                !IsNonnegativeFinite(plate.ArmRemaining) || !IsNonnegativeFinite(plate.CooldownRemaining) ||
                plate.HandledEnemyIds is null || plate.HandledEnemyIds.Count > MaximumEnemies ||
                plate.HandledEnemyIds.Any(id => id <= 0)))
            throw new InvalidDataException("Co-op snapshot Pulse Plate state is structurally invalid.");
        if (plates.Any(plate => plate.Id >= snapshot.NextEmergencyDefenseId))
            throw new InvalidDataException("Co-op snapshot Pulse Plate identity state is inconsistent.");

        if (snapshot.Generator is { } generator && (generator.OwnerPlayerId is < 1 or > 2 ||
            !float.IsFinite(generator.X) || !float.IsFinite(generator.Y) || generator.LevelIndex < 0 ||
            generator.InvestedCredits < 0 || !IsNonnegativeFinite(generator.ProductionRemaining)))
            throw new InvalidDataException("Co-op snapshot Charge Forge state is structurally invalid.");

        ValidateStatistics(statistics);
        if (pending.Count > DeterministicSessionRunner.MaximumPendingCommands || pending.Any(item => item is null ||
                item.Command is null || item.Tick < snapshot.Tick ||
                item.Tick - snapshot.Tick > DeterministicSessionRunner.MaximumFutureTicks ||
                !IsValidCommand(item.Command)) ||
            pending.Select(item => item.Command.Sequence).Distinct().Count() != pending.Count)
            throw new InvalidDataException("Co-op snapshot pending commands are structurally invalid.");
    }

    private static void ValidateStatistics(Persistence.RunStatisticsSaveData statistics)
    {
        var towers = statistics.Towers ?? throw new InvalidDataException("Co-op snapshot tower statistics are missing.");
        var enemies = statistics.Enemies ?? throw new InvalidDataException("Co-op snapshot enemy statistics are missing.");
        var sources = statistics.TowerDefinitionByInstance ?? throw new InvalidDataException("Co-op snapshot source statistics are missing.");
        if (!IsNonnegativeFinite(statistics.SimulatedSeconds) || statistics.EmergencyDeployments < 0 ||
            statistics.EmergencyDirectPurchases < 0 || statistics.EmergencyTriggers < 0 ||
            statistics.EmergencyHits < 0 || statistics.EmergencyKills < 0 ||
            !IsNonnegativeFinite(statistics.EmergencyDamage) || statistics.GeneratedCharges < 0 ||
            statistics.GeneratorPurchases < 0 || statistics.GeneratorUpgrades < 0 ||
            towers.Count > MaximumStatisticsEntries || enemies.Count > MaximumStatisticsEntries ||
            sources.Count > MaximumStatisticsEntries || towers.Any(tower => tower is null ||
                string.IsNullOrWhiteSpace(tower.TowerId) || string.IsNullOrWhiteSpace(tower.DisplayName) ||
                tower.Specializations is null || tower.Specializations.Count > 64 ||
                tower.Purchases < 0 || tower.Upgrades < 0 || tower.Sales < 0 || tower.CreditsSpent < 0 ||
                tower.CreditsRecovered < 0 || tower.Hits < 0 || tower.Kills < 0 || tower.Overdrives < 0 ||
                !IsNonnegativeFinite(tower.Damage) || !IsNonnegativeFinite(tower.SupportDamageEquivalent) ||
                !IsNonnegativeFinite(tower.ExposeDamageEquivalent) || !IsNonnegativeFinite(tower.ArmorBreakDamageEquivalent) ||
                !IsNonnegativeFinite(tower.ControlSeconds) || !IsNonnegativeFinite(tower.ExposeSeconds) ||
                !IsNonnegativeFinite(tower.ArmorBreakSeconds) || !IsNonnegativeFinite(tower.ArmorAbsorbed) ||
                !IsNonnegativeFinite(tower.Overkill) || tower.Specializations.Any(value =>
                    string.IsNullOrWhiteSpace(value.Key) || value.Value < 0)) ||
            towers.Select(tower => tower.TowerId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != towers.Count ||
            enemies.Any(enemy => enemy is null || string.IsNullOrWhiteSpace(enemy.EnemyId) ||
                string.IsNullOrWhiteSpace(enemy.DisplayName) || enemy.Kills < 0 || enemy.Escapes < 0 || enemy.LivesLost < 0) ||
            enemies.Select(enemy => enemy.EnemyId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != enemies.Count ||
            sources.Any(source => source.Key <= 0 || string.IsNullOrWhiteSpace(source.Value) ||
                towers.All(tower => !tower.TowerId.Equals(source.Value, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Co-op snapshot statistics are structurally invalid.");
    }

    private static bool IsValidCommand(GameCommand command) =>
        command.Sequence > 0 && GameCommandValidator.IsStructurallyValid(command);

    private static bool IsValidStatus(StatusApplication? status) => status is null ||
        Enum.IsDefined(status.Type) && IsPositiveFinite(status.Duration) && IsPositiveFinite(status.Magnitude) &&
        IsPositiveFinite(status.TickInterval);

    private static bool IsPositiveFinite(float value) => float.IsFinite(value) && value > 0;
    private static bool IsNonnegativeFinite(float value) => float.IsFinite(value) && value >= 0;

    private static void ValidateCount(int count, int maximum, string label)
    {
        if (count > maximum) throw new InvalidDataException($"Co-op snapshot contains too many {label} entries.");
    }
}
