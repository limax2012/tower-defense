using Microsoft.Xna.Framework;

namespace MinimalBastion.Effects;

public enum EffectKind
{
    Flash,
    Beam,
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
    private readonly List<EffectInstance> _effects = new();
    public IReadOnlyList<EffectInstance> Effects => _effects;

    public void AddFlash(Vector2 position, Color color, float duration, float radius)
    {
        _effects.Add(new EffectInstance { Kind = EffectKind.Flash, Start = position, End = position, Color = color, Duration = duration, Remaining = duration, Radius = radius });
    }

    public void AddBeam(Vector2 start, Vector2 end, Color color, float duration)
    {
        _effects.Add(new EffectInstance { Kind = EffectKind.Beam, Start = start, End = end, Color = color, Duration = duration, Remaining = duration, Radius = 2 });
    }

    public void AddPing(Vector2 position, Color color)
    {
        const float duration = 1.4f;
        _effects.Add(new EffectInstance { Kind = EffectKind.Ping, Start = position, End = position, Color = color, Duration = duration, Remaining = duration, Radius = 24 });
    }

    public void Update(float deltaSeconds)
    {
        foreach (var effect in _effects) effect.Remaining -= deltaSeconds;
        _effects.RemoveAll(x => x.Remaining <= 0);
    }
}
