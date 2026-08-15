using Microsoft.Xna.Framework;

namespace MinimalBastion.Effects;

public enum EffectKind
{
    Flash,
    Beam,
    Impact,
    Splash,
    Shatter,
    Ping
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
}

public sealed class EffectSystem
{
    public const int MaximumTransientEffects = 384;
    public const int MaximumPings = 8;
    public const int MaximumEffects = MaximumTransientEffects + MaximumPings;
    private readonly List<EffectInstance> _effects = new();
    public IReadOnlyList<EffectInstance> Effects => _effects;

    public void AddFlash(Vector2 position, Color color, float duration, float radius)
    {
        if (!ReserveTransientSlot(EffectKind.Flash)) return;
        _effects.Add(new EffectInstance { Kind = EffectKind.Flash, Start = position, End = position, Color = color, Duration = duration, Remaining = duration, Radius = radius });
    }

    public void AddBeam(Vector2 start, Vector2 end, Color color, float duration)
    {
        if (!ReserveTransientSlot(EffectKind.Beam)) return;
        _effects.Add(new EffectInstance { Kind = EffectKind.Beam, Start = start, End = end, Color = color, Duration = duration, Remaining = duration, Radius = 2 });
    }

    public void AddImpact(Vector2 position, Color color, float radius = 9f)
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
            Radius = MathF.Max(4, radius)
        });
    }

    public void AddSplash(Vector2 position, Color color, float radius)
    {
        const float duration = 0.24f;
        if (!ReserveTransientSlot(EffectKind.Splash)) return;
        _effects.Add(new EffectInstance
        {
            Kind = EffectKind.Splash,
            Start = position,
            End = position,
            Color = color,
            Duration = duration,
            Remaining = duration,
            Radius = MathF.Max(10, radius)
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
        if (incomingKind is EffectKind.Beam or EffectKind.Impact or EffectKind.Shatter) return false;

        var oldestMinor = _effects.FindIndex(effect => effect.Kind is EffectKind.Impact or EffectKind.Shatter);
        if (oldestMinor >= 0)
        {
            _effects.RemoveAt(oldestMinor);
            return true;
        }

        var oldestBeam = _effects.FindIndex(effect => effect.Kind == EffectKind.Beam);
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
