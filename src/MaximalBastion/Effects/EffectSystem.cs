using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Effects;

public enum EffectKind
{
    Flash,
    Beam,
    Impact,
    Splash,
    Shatter,
    Ping,
    Ricochet
}

public enum BeamStyle
{
    Standard,
    Prism
}

public enum EffectPurpose
{
    Combat,
    ProtocolActivation,
    ForgeCharge,
    EnemyEscape
}

public sealed class EffectInstance
{
    public EffectKind Kind { get; init; }
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public Color Color { get; init; }
    public float Remaining { get; set; }
    public float Duration { get; init; }
    public float Radius { get; init; }
    public float TravelDuration { get; init; }
    public float ImpactRadius { get; init; }
    public BeamStyle BeamStyle { get; init; }
    // Transient presentation ownership; effects are not part of synchronized snapshots or checksums.
    public int SourceTowerId { get; init; }
    public EnemyInstance? SourceEnemy { get; init; }
    public EnemyInstance? TargetEnemy { get; init; }
    public AttackAppearance Appearance { get; init; }
    public SignalAppearance Signal { get; init; }
    public Vector2 Direction { get; init; }
    public EffectPurpose Purpose { get; init; }
}

public sealed class EffectSystem
{
    public const int MaximumTransientEffects = 384;
    public const int MaximumPings = 8;
    public const int MaximumEffects = MaximumTransientEffects + MaximumPings;
    private readonly List<EffectInstance> _effects = new();
    public IReadOnlyList<EffectInstance> Effects => _effects;

    public void Clear() => _effects.Clear();

    public void AddFlash(Vector2 position, Color color, float duration, float radius,
        AttackAppearance appearance = default, SignalAppearance signal = default,
        EffectPurpose purpose = EffectPurpose.Combat)
    {
        if (!ReserveTransientSlot(EffectKind.Flash)) return;
        _effects.Add(new EffectInstance { Kind = EffectKind.Flash, Start = position, End = position, Color = color, Duration = duration, Remaining = duration, Radius = radius, Appearance = appearance, Signal = signal, Purpose = purpose });
    }

    public void AddBeam(Vector2 start, Vector2 end, Color color, float duration, BeamStyle style = BeamStyle.Standard,
        int sourceTowerId = 0, AttackAppearance appearance = default, SignalAppearance signal = default,
        EnemyInstance? targetEnemy = null, EnemyInstance? sourceEnemy = null)
    {
        if (!ReserveTransientSlot(EffectKind.Beam)) return;
        _effects.Add(new EffectInstance { Kind = EffectKind.Beam, Start = start, End = end, Color = color, Duration = duration, Remaining = duration, Radius = 2, BeamStyle = style, SourceTowerId = sourceTowerId, Appearance = appearance, Signal = signal, TargetEnemy = targetEnemy, SourceEnemy = sourceEnemy });
    }

    public void AddImpact(Vector2 position, Color color, float radius = 9f,
        AttackAppearance appearance = default, Vector2 direction = default, SignalAppearance signal = default,
        EnemyInstance? targetEnemy = null)
    {
        const float duration = 0.12f;
        if (!ReserveTransientSlot(EffectKind.Impact)) return;
        _effects.Add(new EffectInstance
        {
            Kind = EffectKind.Impact,
            Start = position,
            End = position,
            Color = color,
            Duration = duration,
            Remaining = duration,
            Radius = MathF.Max(4, radius),
            Appearance = appearance,
            Signal = signal,
            Direction = direction,
            TargetEnemy = targetEnemy
        });
    }

    public void AddRicochet(Vector2 start, Vector2 end, Color color, float speed, float radius,
        float impactRadius, AttackAppearance appearance = default, EnemyInstance? targetEnemy = null)
    {
        if (!ReserveTransientSlot(EffectKind.Ricochet)) return;
        var travelDuration = Vector2.Distance(start, end) / MathF.Max(1, speed);
        var duration = travelDuration + .12f;
        _effects.Add(new EffectInstance
        {
            Kind = EffectKind.Ricochet,
            Start = start,
            End = end,
            Color = color,
            Duration = duration,
            Remaining = duration,
            TravelDuration = travelDuration,
            Radius = radius,
            ImpactRadius = impactRadius,
            Appearance = appearance,
            TargetEnemy = targetEnemy,
            Direction = end - start
        });
    }

    public void AddSplash(Vector2 position, Color color, float radius,
        AttackAppearance appearance = default, SignalAppearance signal = default,
        EffectPurpose purpose = EffectPurpose.Combat, float duration = 0.24f,
        EnemyInstance? targetEnemy = null)
    {
        if (!ReserveTransientSlot(EffectKind.Splash)) return;
        _effects.Add(new EffectInstance
        {
            Kind = EffectKind.Splash,
            Start = position,
            End = position,
            Color = color,
            Duration = duration,
            Remaining = duration,
            Radius = MathF.Max(10, radius),
            Appearance = appearance,
            TargetEnemy = targetEnemy,
            Signal = signal,
            Purpose = purpose
        });
    }

    public void AddShatter(Vector2 position, Color color, float radius)
    {
        const float duration = 0.18f;
        if (!ReserveTransientSlot(EffectKind.Shatter)) return;
        _effects.Add(new EffectInstance
        {
            Kind = EffectKind.Shatter,
            Start = position,
            End = position,
            Color = color,
            Duration = duration,
            Remaining = duration,
            Radius = MathF.Max(7, radius)
        });
    }

    public void AddPing(Vector2 position, Color color)
    {
        const float duration = 1.4f;
        var oldestPing = _effects.FindIndex(effect => effect.Kind == EffectKind.Ping);
        if (_effects.Count(effect => effect.Kind == EffectKind.Ping) >= MaximumPings && oldestPing >= 0)
            _effects.RemoveAt(oldestPing);
        if (_effects.Count >= MaximumEffects) RemoveOldestTransient();
        _effects.Add(new EffectInstance { Kind = EffectKind.Ping, Start = position, End = position, Color = color, Duration = duration, Remaining = duration, Radius = 24 });
    }

    public void Update(float deltaSeconds)
    {
        foreach (var effect in _effects) effect.Remaining -= deltaSeconds;
        _effects.RemoveAll(x => x.Remaining <= 0);
    }

    private bool ReserveTransientSlot(EffectKind incomingKind)
    {
        if (_effects.Count < MaximumEffects) return true;
        if (incomingKind is EffectKind.Beam or EffectKind.Ricochet or EffectKind.Impact or EffectKind.Shatter) return false;

        var oldestMinor = _effects.FindIndex(effect => effect.Kind is EffectKind.Impact or EffectKind.Shatter);
        if (oldestMinor >= 0)
        {
            _effects.RemoveAt(oldestMinor);
            return true;
        }

        var oldestBeam = _effects.FindIndex(effect => effect.Kind is EffectKind.Beam or EffectKind.Ricochet);
        if (oldestBeam >= 0)
        {
            _effects.RemoveAt(oldestBeam);
            return true;
        }
        return RemoveOldestTransient();
    }

    private bool RemoveOldestTransient()
    {
        var oldestTransient = _effects.FindIndex(effect => effect.Kind != EffectKind.Ping);
        if (oldestTransient < 0) return false;
        _effects.RemoveAt(oldestTransient);
        return true;
    }
}
