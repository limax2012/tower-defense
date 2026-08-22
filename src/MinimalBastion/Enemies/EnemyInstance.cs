using MinimalBastion.Data;
using MinimalBastion.Core;
using MinimalBastion.Effects;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Enemies;

public sealed class EnemyInstance
{
    public int Id { get; }
    public EnemyDefinition Definition { get; }
    public Vector2 Position { get; private set; }
    public float DistanceAlongPath { get; private set; }
    public float PathProgress { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; }
    public float Shield { get; private set; }
    public float BaseArmor { get; }
    public EnemyRank Rank { get; }
    public bool IsElite => Rank == EnemyRank.Elite;
    public bool IsBoss => Rank == EnemyRank.Boss;
    public bool BossPhaseActive { get; private set; }
    public bool BossPhasePulsePending => _bossPhasePulsePending;
    public int Reward { get; }
    public int LivesLost { get; }
    public float ControlResistance { get; }
    public string DisplayName => IsBoss ? "Bastion Core" : IsElite ? $"Elite {Definition.DisplayName}" : Definition.DisplayName;
    public float DamagePauseTimer { get; set; }
    public float KnockbackGraceRemaining { get; private set; }
    public float CounterPressureCooldownRemaining { get; private set; }
    public bool IsDead { get; private set; }
    public bool HasEscaped { get; private set; }
    public bool IsSandboxImmortal { get; }
    public float Radius => Definition.Visual.Radius + (IsBoss ? 8 : IsElite ? 3 : 0);
    public float HealthScale => _healthMultiplier;
    public float MovementSpeedScale => _speedMultiplier;
    public StatusEffectController StatusEffects { get; } = new();

    public float EffectiveArmor => MathF.Max(0, BaseArmor - StatusEffects.ArmorReduction - (StatusEffects.IsBurning ? 2f : 0f));
    public float SpeedMultiplier => StatusEffects.IsStunned ? 0 : 1f - MathHelper.Clamp(StatusEffects.SlowFactor, 0, 0.60f);
    public float CurrentSpeed => Definition.Speed * _speedMultiplier * _rankSpeedMultiplier * (BossPhaseActive ? 1.28f : 1f) * SpeedMultiplier;

    public EnemyInstance(int id, EnemyDefinition definition, PathRuntime path, float healthMultiplier, float speedMultiplier,
        string rank = "Standard", bool sandboxImmortal = false)
    {
        Id = id;
        Definition = definition;
        Rank = Enum.TryParse<EnemyRank>(rank, true, out var parsedRank) ? parsedRank : EnemyRank.Standard;
        var rankHealthMultiplier = Rank switch { EnemyRank.Elite => 1.85f, EnemyRank.Boss => 4.5f, _ => 1f };
        _rankSpeedMultiplier = Rank switch { EnemyRank.Elite => 1.07f, EnemyRank.Boss => 0.92f, _ => 1f };
        ControlResistance = Rank switch { EnemyRank.Elite => 0.30f, EnemyRank.Boss => 0.60f, _ => 0f };
        MaxHealth = definition.MaxHealth * healthMultiplier * rankHealthMultiplier;
        Health = MaxHealth;
        Shield = definition.Shield + (IsBoss ? MaxHealth * 0.12f : 0);
        BaseArmor = definition.Armor + (IsBoss ? 4 : IsElite ? 2 : 0);
        Reward = (int)MathF.Round(definition.Reward * (IsBoss ? 5f : IsElite ? 2f : 1f));
        LivesLost = IsBoss ? Math.Max(10, definition.LivesLost) : definition.LivesLost + (IsElite ? 1 : 0);
        Position = path.GetPosition(0);
        DistanceAlongPath = 0;
        PathProgress = 0;
        _speedMultiplier = speedMultiplier;
        _healthMultiplier = healthMultiplier;
        IsSandboxImmortal = sandboxImmortal;
    }

    private readonly float _healthMultiplier;
    private readonly float _speedMultiplier;
    private readonly float _rankSpeedMultiplier;
    private bool _bossPhasePulsePending;

    public void UpdateMovement(float deltaSeconds, PathRuntime path)
    {
        if (IsDead || HasEscaped) return;
        DistanceAlongPath += CurrentSpeed * deltaSeconds;
        if (DistanceAlongPath >= path.TotalLength)
        {
            DistanceAlongPath = path.TotalLength;
            Position = path.GetPosition(DistanceAlongPath);
            PathProgress = 1f;
            HasEscaped = true;
            return;
        }
        Position = path.GetPosition(DistanceAlongPath);
        PathProgress = path.GetProgress(DistanceAlongPath);
    }

    public bool TryApplyKnockback(float distance, float graceSeconds, PathRuntime path)
    {
        if (distance <= 0 || KnockbackGraceRemaining > 0 || IsDead || HasEscaped) return false;
        DistanceAlongPath = MathF.Max(0, DistanceAlongPath - distance);
        Position = path.GetPosition(DistanceAlongPath);
        PathProgress = path.GetProgress(DistanceAlongPath);
        KnockbackGraceRemaining = MathF.Max(0, graceSeconds);
        return true;
    }

    public void Regenerate(float deltaSeconds)
    {
        if (IsDead || HasEscaped || Definition.RegenerationPerSecond <= 0 || DamagePauseTimer > 0) return;
        Health = MathF.Min(MaxHealth, Health + Definition.RegenerationPerSecond * deltaSeconds);
    }

    public void TickRuntimeTimers(float deltaSeconds)
    {
        DamagePauseTimer = MathF.Max(0, DamagePauseTimer - deltaSeconds);
        KnockbackGraceRemaining = MathF.Max(0, KnockbackGraceRemaining - deltaSeconds);
        CounterPressureCooldownRemaining = MathF.Max(0, CounterPressureCooldownRemaining - deltaSeconds);
    }

    public void ArmCounterPressure(float initialDelaySeconds) =>
        CounterPressureCooldownRemaining = MathF.Max(0, initialDelaySeconds);

    public bool TryEmitCounterPressure(float intervalSeconds)
    {
        if (CounterPressureCooldownRemaining > 0 || IsDead || HasEscaped) return false;
        CounterPressureCooldownRemaining = MathF.Max(0.1f, intervalSeconds);
        return true;
    }

    public void ApplyHealthDamage(float amount)
    {
        if (amount <= 0 || IsDead || HasEscaped) return;
        if (IsSandboxImmortal)
        {
            // Keep a stable full-health target while still allowing DamageResolver
            // to report every hit, status application, splash, and assist normally.
            DamagePauseTimer = 1f;
            return;
        }
        Health = MathF.Max(0, Health - amount);
        DamagePauseTimer = 1f;
        if (Health <= 0) IsDead = true;
        else if (IsBoss && !BossPhaseActive && Health <= MaxHealth * 0.5f)
        {
            BossPhaseActive = true;
            Shield = MathF.Max(Shield, MaxHealth * 0.12f);
            _bossPhasePulsePending = true;
        }
    }

    internal void SetSandboxPathDistance(float distanceAlongPath, PathRuntime path)
    {
        DistanceAlongPath = MathHelper.Clamp(distanceAlongPath, 0, path.TotalLength);
        Position = path.GetPosition(DistanceAlongPath);
        PathProgress = path.GetProgress(DistanceAlongPath);
    }

    public void ApplyStatus(StatusApplication application)
    {
        if (ControlResistance <= 0)
        {
            StatusEffects.Apply(application);
            return;
        }
        StatusEffects.Apply(new StatusApplication
        {
            Type = application.Type,
            Duration = application.Duration * (1f - ControlResistance),
            Magnitude = application.Magnitude,
            SourceId = application.SourceId,
            TickInterval = application.TickInterval
        });
    }

    public bool ConsumeBossPhasePulse()
    {
        if (!_bossPhasePulsePending) return false;
        _bossPhasePulsePending = false;
        return true;
    }

    public float AbsorbShield(float amount)
    {
        if (amount <= 0 || Shield <= 0) return amount;
        var absorbed = MathF.Min(Shield, amount);
        Shield -= absorbed;
        return amount - absorbed;
    }

    public EnemyRuntimeState CaptureCoOpState() => new()
    {
        Id = Id,
        DefinitionId = Definition.Id,
        Rank = Rank,
        HealthMultiplier = _healthMultiplier,
        SpeedMultiplier = _speedMultiplier,
        DistanceAlongPath = DistanceAlongPath,
        Health = Health,
        Shield = Shield,
        DamagePauseTimer = DamagePauseTimer,
        KnockbackGraceRemaining = KnockbackGraceRemaining,
        CounterPressureCooldownRemaining = CounterPressureCooldownRemaining,
        IsDead = IsDead,
        HasEscaped = HasEscaped,
        BossPhaseActive = BossPhaseActive,
        BossPhasePulsePending = _bossPhasePulsePending,
        Statuses = StatusEffects.CaptureState()
    };

    public static EnemyInstance RestoreCoOpState(EnemyRuntimeState data, EnemyDefinition definition, PathRuntime path)
    {
        var enemy = new EnemyInstance(
            data.Id,
            definition,
            path,
            MathF.Max(0.01f, data.HealthMultiplier),
            MathF.Max(0.01f, data.SpeedMultiplier),
            data.Rank.ToString())
        {
            DistanceAlongPath = MathHelper.Clamp(data.DistanceAlongPath, 0, path.TotalLength),
            Health = MathHelper.Clamp(data.Health, 0, definition.MaxHealth * MathF.Max(0.01f, data.HealthMultiplier) *
                (data.Rank == EnemyRank.Elite ? 1.85f : data.Rank == EnemyRank.Boss ? 4.5f : 1f)),
            Shield = MathF.Max(0, data.Shield),
            DamagePauseTimer = MathF.Max(0, data.DamagePauseTimer),
            KnockbackGraceRemaining = MathF.Max(0, data.KnockbackGraceRemaining),
            CounterPressureCooldownRemaining = MathF.Max(0, data.CounterPressureCooldownRemaining),
            IsDead = data.IsDead,
            HasEscaped = data.HasEscaped,
            BossPhaseActive = data.BossPhaseActive,
            _bossPhasePulsePending = data.BossPhasePulsePending
        };
        enemy.Position = path.GetPosition(enemy.DistanceAlongPath);
        enemy.PathProgress = path.GetProgress(enemy.DistanceAlongPath);
        enemy.StatusEffects.RestoreState(data.Statuses);
        return enemy;
    }
}
