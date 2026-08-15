using MinimalBastion.Effects;

namespace MinimalBastion.Multiplayer;

public sealed class DeterministicSessionRunner
{
    public const float FixedStepSeconds = 0.05f;
    private readonly GameSession _session;
    private readonly SortedDictionary<long, List<GameCommand>> _scheduled = new();
    private readonly HashSet<long> _appliedSequences = new();
    private float _accumulator;

    public long Tick { get; private set; }
    public event Action<long>? TickCompleted;

    public DeterministicSessionRunner(GameSession session, long initialTick = 0)
    {
        _session = session;
        Tick = Math.Max(0, initialTick);
    }

    public bool Schedule(long tick, GameCommand command)
    {
        if (tick < Tick || command.Sequence <= 0 || _appliedSequences.Contains(command.Sequence)) return false;
        if (!_scheduled.TryGetValue(tick, out var commands))
            _scheduled[tick] = commands = new List<GameCommand>();
        if (commands.Any(x => x.Sequence == command.Sequence)) return false;
        commands.Add(command);
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
            Schedule(item.Tick, item.Command);
    }

    private void Step()
    {
        if (_scheduled.Remove(Tick, out var commands))
        {
            foreach (var command in commands.OrderBy(x => x.Sequence))
            {
                if (!_appliedSequences.Add(command.Sequence)) continue;
                GameCommandProcessor.Apply(_session, command);
            }
        }
        _session.Update(FixedStepSeconds);
        Tick++;
        TickCompleted?.Invoke(Tick);
    }
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
        Add(ref hash, session.CurrentWave);
        Add(ref hash, session.Speed);
        Add(ref hash, session.IsVictory ? 1 : 0);
        Add(ref hash, session.IsDefeat ? 1 : 0);
        Add(ref hash, session.Economy.Credits);
        Add(ref hash, session.Economy.Lives);
        Add(ref hash, session.Economy.TotalKills);
        Add(ref hash, session.Economy.EscapedEnemies);
        Add(ref hash, session.Economy.EarlyStartCreditsEarned);
        Add(ref hash, session.EmergencyInventory);
        Add(ref hash, session.EmergencyDirectPurchasesThisWave);
        Add(ref hash, session.OverdriveCooldownRemaining);
        Add(ref hash, session.AutoOverdriveTowerId);
        Add(ref hash, session.Waves.QueuedEnemies);
        var wave = session.Waves.CaptureCoOpState();
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
            Add(ref hash, tower.SpecializationId ?? "");
            Add(ref hash, tower.CooldownRemaining);
            Add(ref hash, tower.OverdriveRemaining);
            Add(ref hash, (int)tower.TargetMode);
            Add(ref hash, tower.LifetimeDamage);
            Add(ref hash, tower.LifetimeKills);
            Add(ref hash, tower.LifetimeSupportDamageEquivalent);
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
            Add(ref hash, enemy.DistanceAlongPath);
            Add(ref hash, enemy.Health);
            Add(ref hash, enemy.Shield);
            Add(ref hash, enemy.DamagePauseTimer);
            Add(ref hash, enemy.KnockbackGraceRemaining);
            foreach (var status in enemy.StatusEffects.Active.OrderBy(x => x.Type).ThenBy(x => x.SourceId))
            {
                Add(ref hash, (int)status.Type);
                Add(ref hash, status.SourceId);
                Add(ref hash, status.Magnitude);
                Add(ref hash, status.RemainingSeconds);
                Add(ref hash, status.TickProgress);
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
            Add(ref hash, projectile.Payload.ArmorPierce);
            Add(ref hash, projectile.Payload.IgnoreShield ? 1 : 0);
            Add(ref hash, projectile.Payload.IsDamageOverTime ? 1 : 0);
            if (projectile.Payload.Status is { } status)
            {
                Add(ref hash, (int)status.Type);
                Add(ref hash, status.Duration);
                Add(ref hash, status.Magnitude);
                Add(ref hash, status.SourceId);
                Add(ref hash, status.TickInterval);
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
            Add(ref hash, generator.ProductionRemaining);
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
