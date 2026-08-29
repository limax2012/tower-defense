using MinimalBastion.Effects;

namespace MinimalBastion.Multiplayer;

public sealed class DeterministicSessionRunner
{
    public const int SimulationTicksPerSecond = 60;
    public const float FixedStepSeconds = 1f / SimulationTicksPerSecond;
    public const int MaximumFutureTicks = SimulationTicksPerSecond * 12;
    public const int MaximumPendingCommands = 512;
    public const int AppliedSequenceHistoryLimit = 4096;
    private readonly GameSession _session;
    private readonly SortedDictionary<long, List<GameCommand>> _scheduled = new();
    private readonly HashSet<long> _pendingSequences = new();
    private readonly HashSet<long> _appliedSequences = new();
    private readonly Queue<long> _appliedSequenceOrder = new();
    private long _expiredAppliedSequenceFloor;
    private float _accumulator;

    public long Tick { get; private set; }
    public float PresentationLeadSeconds => Math.Clamp(_accumulator, 0, FixedStepSeconds);
    public int PendingCommandCount => _pendingSequences.Count;
    public int AppliedSequenceHistoryCount => _appliedSequences.Count;
    public long ExpiredAppliedSequenceFloor => _expiredAppliedSequenceFloor;
    public event Action<long>? TickCompleted;

    public DeterministicSessionRunner(GameSession session, long initialTick = 0)
    {
        _session = session;
        Tick = Math.Max(0, initialTick);
    }

    public bool Schedule(long tick, GameCommand command)
    {
        if (tick < Tick || tick > Tick + MaximumFutureTicks || command.Sequence <= 0 ||
            command.Sequence <= _expiredAppliedSequenceFloor || _appliedSequences.Contains(command.Sequence) ||
            _pendingSequences.Contains(command.Sequence) || _pendingSequences.Count >= MaximumPendingCommands) return false;
        if (!_scheduled.TryGetValue(tick, out var commands))
            _scheduled[tick] = commands = new List<GameCommand>();
        commands.Add(command);
        _pendingSequences.Add(command.Sequence);
        return true;
    }

    public int Advance(float elapsedSeconds)
    {
        _accumulator += MathF.Max(0, elapsedSeconds);
        var steps = 0;
        while (_accumulator >= FixedStepSeconds)
        {
            Step();
            _accumulator -= FixedStepSeconds;
            steps++;
        }
        return steps;
    }

    public void RunTicks(int count)
    {
        for (var index = 0; index < Math.Max(0, count); index++) Step();
    }

    public List<ScheduledCommandState> CapturePendingCommands() => _scheduled
        .SelectMany(pair => pair.Value.Select(command => new ScheduledCommandState { Tick = pair.Key, Command = command }))
        .OrderBy(item => item.Tick)
        .ThenBy(item => item.Command.Sequence)
        .ToList();

    public void RestorePendingCommands(IEnumerable<ScheduledCommandState> commands)
    {
        foreach (var item in commands.OrderBy(item => item.Tick).ThenBy(item => item.Command.Sequence))
            if (!Schedule(item.Tick, item.Command))
                throw new InvalidDataException($"Invalid pending co-op command {item.Command.Sequence} at tick {item.Tick}.");
    }

    private void Step()
    {
        if (_scheduled.Remove(Tick, out var commands))
        {
            foreach (var command in commands.OrderBy(x => x.Sequence))
            {
                _pendingSequences.Remove(command.Sequence);
                if (command.Sequence <= _expiredAppliedSequenceFloor || !_appliedSequences.Add(command.Sequence)) continue;
                _appliedSequenceOrder.Enqueue(command.Sequence);
                CompactAppliedSequenceHistory();
                GameCommandProcessor.Apply(_session, command);
            }
        }
        _session.Update(FixedStepSeconds);
        Tick++;
        TickCompleted?.Invoke(Tick);
    }

    private void CompactAppliedSequenceHistory()
    {
        while (_appliedSequenceOrder.Count > AppliedSequenceHistoryLimit)
        {
            var expired = _appliedSequenceOrder.Dequeue();
            _appliedSequences.Remove(expired);
            _expiredAppliedSequenceFloor = Math.Max(_expiredAppliedSequenceFloor, expired);
        }
    }
}

public static class CoOpChecksumWindow
{
    public static bool IsAcceptable(long localTick, long snapshotFenceTick, long remoteTick) =>
        localTick >= 0 && remoteTick > snapshotFenceTick &&
        remoteTick >= Math.Max(0, localTick - DeterministicSessionRunner.MaximumFutureTicks) &&
        remoteTick <= localTick + DeterministicSessionRunner.MaximumFutureTicks;
}

public static class SessionChecksum
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static string Compute(GameSession session, long tick)
    {
        var hash = Offset;
        Add(ref hash, tick);
        Add(ref hash, session.Map.Definition.Id);
        Add(ref hash, session.DifficultyId);
        Add(ref hash, session.ChallengeId);
        Add(ref hash, session.CurrentWave);
        Add(ref hash, session.Speed);
        Add(ref hash, session.IsCoOpPaused ? 1 : 0);
        Add(ref hash, session.CoOpPausePlayerId);
        Add(ref hash, session.IsVictory ? 1 : 0);
        Add(ref hash, session.IsDefeat ? 1 : 0);
        Add(ref hash, session.Economy.Credits);
        Add(ref hash, session.Economy.Lives);
        Add(ref hash, session.Economy.TotalKills);
        Add(ref hash, session.Economy.EscapedEnemies);
        Add(ref hash, session.Economy.TotalCreditsSpent);
        Add(ref hash, session.Economy.KillCreditsEarned);
        Add(ref hash, session.Economy.WaveCreditsEarned);
        Add(ref hash, session.Economy.EarlyStartCreditsEarned);
        Add(ref hash, session.Economy.SaleCreditsRecovered);
        Add(ref hash, session.EmergencyInventory);
        Add(ref hash, session.EmergencyDirectPurchasesThisWave);
        Add(ref hash, session.NextEnemyId);
        Add(ref hash, session.NextTowerId);
        Add(ref hash, session.NextEmergencyDefenseId);
        Add(ref hash, session.OverdriveCooldownRemaining);
        Add(ref hash, session.AutoOverdriveTowerId);
        Add(ref hash, session.Waves.QueuedEnemies);
        var wave = session.Waves.CaptureCoOpState();
        Add(ref hash, wave.CurrentWaveNumber);
        Add(ref hash, wave.ActiveWaveNumber);
        Add(ref hash, wave.GroupIndex);
        Add(ref hash, wave.SpawnedInGroup);
        Add(ref hash, wave.GroupTimer);
        Add(ref hash, wave.DelayRemaining);
        Add(ref hash, wave.IntermissionRemaining);
        Add(ref hash, wave.IsFinalWaveCleared ? 1 : 0);
        Add(ref hash, wave.EndlessModeEnabled ? 1 : 0);

        foreach (var tower in session.Towers.OrderBy(x => x.Id))
        {
            Add(ref hash, tower.Id);
            Add(ref hash, tower.OwnerPlayerId);
            Add(ref hash, tower.Definition.Id);
            Add(ref hash, tower.Position.X);
            Add(ref hash, tower.Position.Y);
            Add(ref hash, tower.LevelIndex);
            Add(ref hash, tower.DoctrineId ?? "");
            Add(ref hash, tower.SpecializationId ?? "");
            Add(ref hash, tower.IsApex ? 1 : 0);
            Add(ref hash, tower.InvestedCredits);
            Add(ref hash, tower.CooldownRemaining);
            Add(ref hash, tower.DisruptionRemaining);
            Add(ref hash, tower.DisruptionLockoutRemaining);
            Add(ref hash, tower.SuppressionRemaining);
            Add(ref hash, tower.SuppressionLockoutRemaining);
            Add(ref hash, tower.OverdriveRemaining);
            Add(ref hash, (int)tower.TargetMode);
            Add(ref hash, tower.LifetimeDamage);
            Add(ref hash, tower.LifetimeKills);
            Add(ref hash, tower.LifetimeSupportDamageEquivalent);
            Add(ref hash, tower.LifetimeExposeDamageEquivalent);
            Add(ref hash, tower.LifetimeArmorBreakDamageEquivalent);
            Add(ref hash, tower.LifetimeControlSeconds);
            Add(ref hash, tower.LifetimeExposeSeconds);
            Add(ref hash, tower.LifetimeArmorBreakSeconds);
        }

        foreach (var enemy in session.Enemies.OrderBy(x => x.Id))
        {
            Add(ref hash, enemy.Id);
            Add(ref hash, enemy.Definition.Id);
            Add(ref hash, (int)enemy.Rank);
            Add(ref hash, enemy.BossPhaseActive ? 1 : 0);
            Add(ref hash, enemy.IsDead ? 1 : 0);
            Add(ref hash, enemy.HasEscaped ? 1 : 0);
            Add(ref hash, enemy.BossPhasePulsePending ? 1 : 0);
            Add(ref hash, enemy.HealthScale);
            Add(ref hash, enemy.MovementSpeedScale);
            Add(ref hash, enemy.DistanceAlongPath);
            Add(ref hash, enemy.Health);
            Add(ref hash, enemy.Shield);
            Add(ref hash, enemy.DamagePauseTimer);
            Add(ref hash, enemy.KnockbackGraceRemaining);
            Add(ref hash, enemy.SignalAbilityCooldownRemaining);
            Add(ref hash, (int)enemy.SignalRole);
            Add(ref hash, enemy.FormationSpeedMultiplier);
            foreach (var status in enemy.StatusEffects.Active.OrderBy(x => x.Type).ThenBy(x => x.SourceId))
            {
                Add(ref hash, (int)status.Type);
                Add(ref hash, status.SourceId);
                Add(ref hash, status.Magnitude);
                Add(ref hash, status.RemainingSeconds);
                Add(ref hash, status.TickInterval);
                Add(ref hash, status.TickProgress);
                Add(ref hash, status.ArmorPierce);
            }
        }

        foreach (var projectile in session.Projectiles.Projectiles)
        {
            Add(ref hash, (int)projectile.Kind);
            Add(ref hash, projectile.Position.X);
            Add(ref hash, projectile.Position.Y);
            Add(ref hash, projectile.Target?.Id ?? 0);
            Add(ref hash, projectile.Payload.SourceTowerId);
            Add(ref hash, projectile.Payload.Damage);
            Add(ref hash, projectile.AimPoint.X);
            Add(ref hash, projectile.AimPoint.Y);
            Add(ref hash, projectile.Speed);
            Add(ref hash, projectile.SplashRadius);
            Add(ref hash, projectile.SplashTargetLimit);
            Add(ref hash, projectile.RicochetRange);
            Add(ref hash, projectile.RicochetDamageMultiplier);
            Add(ref hash, projectile.Payload.PriorityDamageMultiplier);
            Add(ref hash, projectile.Payload.ArmorPierce);
            Add(ref hash, projectile.Payload.IgnoreShield ? 1 : 0);
            Add(ref hash, projectile.Payload.IsDamageOverTime ? 1 : 0);
            Add(ref hash, projectile.Color.PackedValue);
            Add(ref hash, projectile.Radius);
            if (projectile.Payload.Status is { } status)
            {
                Add(ref hash, (int)status.Type);
                Add(ref hash, status.Duration);
                Add(ref hash, status.Magnitude);
                Add(ref hash, status.SourceId);
                Add(ref hash, status.TickInterval);
                Add(ref hash, status.ArmorPierce);
            }
        }

        foreach (var defense in session.EmergencyDefenses.OrderBy(x => x.Id))
        {
            Add(ref hash, defense.Id);
            Add(ref hash, defense.OwnerPlayerId);
            Add(ref hash, defense.Position.X);
            Add(ref hash, defense.Position.Y);
            Add(ref hash, defense.ChargesRemaining);
            Add(ref hash, defense.ArmRemaining);
            Add(ref hash, defense.CooldownRemaining);
            foreach (var enemyId in defense.HandledEnemyIds.OrderBy(x => x)) Add(ref hash, enemyId);
        }

        if (session.Generator is { } generator)
        {
            Add(ref hash, generator.OwnerPlayerId);
            Add(ref hash, generator.Position.X);
            Add(ref hash, generator.Position.Y);
            Add(ref hash, generator.LevelIndex);
            Add(ref hash, generator.InvestedCredits);
            Add(ref hash, generator.ProductionRemaining);
        }

        var statistics = session.Statistics;
        Add(ref hash, statistics.SimulatedSeconds);
        Add(ref hash, statistics.AttributionCompactionRemaining);
        Add(ref hash, statistics.EmergencyDeployments);
        Add(ref hash, statistics.EmergencyDirectPurchases);
        Add(ref hash, statistics.EmergencyTriggers);
        Add(ref hash, statistics.EmergencyHits);
        Add(ref hash, statistics.EmergencyKills);
        Add(ref hash, statistics.EmergencyDamage);
        Add(ref hash, statistics.GeneratedCharges);
        Add(ref hash, statistics.GeneratorPurchases);
        Add(ref hash, statistics.GeneratorUpgrades);
        foreach (var source in statistics.TowerDefinitionByInstance.OrderBy(value => value.Key))
        {
            Add(ref hash, source.Key);
            Add(ref hash, source.Value);
        }
        foreach (var metrics in statistics.Towers.OrderBy(value => value.TowerId, StringComparer.Ordinal))
        {
            Add(ref hash, metrics.TowerId);
            Add(ref hash, metrics.Purchases);
            Add(ref hash, metrics.Upgrades);
            Add(ref hash, metrics.Sales);
            Add(ref hash, metrics.CreditsSpent);
            Add(ref hash, metrics.CreditsRecovered);
            Add(ref hash, metrics.Hits);
            Add(ref hash, metrics.Kills);
            Add(ref hash, metrics.Overdrives);
            Add(ref hash, metrics.Damage);
            Add(ref hash, metrics.SupportDamageEquivalent);
            Add(ref hash, metrics.ExposeDamageEquivalent);
            Add(ref hash, metrics.ArmorBreakDamageEquivalent);
            Add(ref hash, metrics.ControlSeconds);
            Add(ref hash, metrics.ExposeSeconds);
            Add(ref hash, metrics.ArmorBreakSeconds);
            Add(ref hash, metrics.ArmorAbsorbed);
            Add(ref hash, metrics.Overkill);
            foreach (var specialization in metrics.Specializations.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                Add(ref hash, specialization.Key);
                Add(ref hash, specialization.Value);
            }
        }
        foreach (var metrics in statistics.Enemies.OrderBy(value => value.EnemyId, StringComparer.Ordinal))
        {
            Add(ref hash, metrics.EnemyId);
            Add(ref hash, metrics.Kills);
            Add(ref hash, metrics.Escapes);
            Add(ref hash, metrics.LivesLost);
        }
        return hash.ToString("X16");
    }

    private static void Add(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            hash ^= character;
            hash *= Prime;
        }
        hash ^= 0xFF;
        hash *= Prime;
    }

    private static void Add(ref ulong hash, int value) => Add(ref hash, (long)value);
    private static void Add(ref ulong hash, float value) => Add(ref hash, BitConverter.SingleToInt32Bits(value));
    private static void Add(ref ulong hash, long value)
    {
        unchecked
        {
            for (var index = 0; index < 8; index++)
            {
                hash ^= (byte)(value >> (index * 8));
                hash *= Prime;
            }
        }
    }
}
