using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Receiving power, interrupted conductors, and interference within tower hardware.</summary>
public static class TowerStatusArt
{
    public static void DrawSupport(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects, float strength = 1)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        var power = Math.Clamp(strength, .35f, 1);
        TowerHardwareChannels.DrawReceiver(d, towerId, active: true, glow: !reducedEffects, power);
    }

    public static void DrawDisrupted(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        TowerHardwareChannels.DrawInterference(d, towerId, shutdown: true, glow: !reducedEffects);
    }

    public static void DrawSuppressed(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        TowerHardwareChannels.DrawInterference(d, towerId, shutdown: false, glow: !reducedEffects);
    }

    public static Vector2 SignalContact(Vector2 center, float radius, string towerId, int level, bool apex, float aim)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var local = towerId switch
        {
            "needle_turret" => new Vector2(stage == 1 ? -.48f : -.56f, 0),
            "watchtower" => new Vector2(stage == 1 ? -.50f : -.65f, 0),
            "prism_beam" => new Vector2(.45f, stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f }),
            "frost_spire" => new Vector2(0, TowerTierVisuals.FrostCoolantOffset(stage)),
            "siege_mortar" => new Vector2(0, TowerTierVisuals.MortarBankOffset(stage)),
            "shard_fan" => new Vector2(-TowerTierVisuals.ShardReceiverRadius(stage) * .5f,
                TowerTierVisuals.ShardReceiverRadius(stage) * .866f),
            _ => new Vector2(-.25f, 0)
        };
        return center + new Vector2(MathF.Cos(aim) * local.X - MathF.Sin(aim) * local.Y,
            MathF.Sin(aim) * local.X + MathF.Cos(aim) * local.Y) * radius;
    }

}

internal readonly struct TowerStatusFrame
{
    private readonly SpriteBatch _batch;
    private readonly PrimitiveRenderer _primitives;
    private readonly Vector2 _center;
    private readonly float _radius;
    private readonly float _cos;
    private readonly float _sin;
    private readonly float _time;
    private readonly bool _reducedEffects;
    public readonly int Stage;

    internal TowerStatusFrame(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        _batch = batch;
        _primitives = primitives;
        _center = center;
        _radius = radius;
        _cos = MathF.Cos(aim);
        _sin = MathF.Sin(aim);
        _time = reducedEffects ? 0 : time;
        _reducedEffects = reducedEffects;
        Stage = TowerTierVisuals.Stage(level, apex);
    }

    internal float Beat(float rate) => _reducedEffects ? 1 : .5f + .5f * MathF.Sin(_time * rate);
    internal float Phase(float rate) => _reducedEffects ? .5f : _time * rate - MathF.Floor(_time * rate);
    internal Vector2 At(float x, float y) => _center + new Vector2(_cos * x - _sin * y, _sin * x + _cos * y) * _radius;
    internal void Line(float x1, float y1, float x2, float y2, Color color, float width) =>
        _primitives.Line(_batch, At(x1, y1), At(x2, y2), color, MathF.Max(.65f, width * _radius));
    internal void Trace(float x1, float y1, float x2, float y2, Color color, float width)
    {
        Line(x1, y1, x2, y2, ColorPalette.Ink, width + .075f);
        Line(x1, y1, x2, y2, color, width);
    }
    internal void Glow(float x, float y, float size, Color color, float intensity) =>
        _primitives.Glow(_batch, At(x, y), _radius * size, color, intensity);
}
