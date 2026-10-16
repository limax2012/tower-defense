using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Temporary receiving, power-fault, and output-lock states attached to tower hardware.</summary>
public static class TowerStatusArt
{
    public static void DrawPowerConnection(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, 0, time, reducedEffects);
        var mount = d.Stage switch { 1 => .89f, 2 => .99f, 3 => 1.06f, _ => 1.12f };
        var triangular = towerId is "arc_relay" or "signal_beacon" or "shard_fan";
        var gold = ColorPalette.Gold * (.82f + d.Beat(2.4f) * .18f);
        if (triangular)
        {
            d.Line(0, -mount * .84f, 0, -mount * .52f, ColorPalette.Ink, .27f);
            d.Line(0, -mount * .82f, 0, -mount * .54f, ColorPalette.Metal, .18f);
            d.Line(0, -mount * .78f, 0, -mount * .58f, gold, .075f);
            d.Line(-.09f, -mount * .58f, .09f, -mount * .58f, gold, .055f);
        }
        else
        {
            var y = mount * (towerId is "watchtower" or "frost_spire" ? .60f : .78f);
            d.Line(-.26f, y, .26f, y, ColorPalette.Ink, .23f);
            d.Line(-.23f, y, .23f, y, ColorPalette.Metal, .16f);
            d.Line(-.18f, y, .18f, y, gold, .06f);
            for (var sign = -1; sign <= 1; sign += 2)
                d.Trace(sign * .15f, y - .09f, sign * .15f, y + .07f, gold, .045f);
        }
    }

    public static void DrawSupport(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects, float strength = 1)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        var power = Math.Clamp(strength, .35f, 1);
        var green = ColorPalette.GreenText * ((.80f + d.Beat(4.6f) * .20f) * power);
        var (x, y) = ReceivingContacts(towerId, d.Stage);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var atY = y * sign;
            var length = d.Stage >= 3 ? .33f : .27f;
            d.Line(x - length * .5f, atY, x + length * .5f, atY, ColorPalette.Navy, .20f);
            d.Line(x - length * .5f, atY, x + length * .5f, atY, ColorPalette.Metal, .14f);
            d.Line(x - length * .37f, atY, x + length * .37f, atY, green, .065f);
            d.Trace(x + length * .5f, atY, x + length * .5f + .11f, atY * .77f, green, .045f);
            d.Trace(x - length * .5f, atY, x - length * .5f - .09f, atY * .77f, green, .045f);
            if (!reducedEffects) d.Glow(x, atY, .34f, ColorPalette.Green, .12f * power);
        }
    }

    public static void DrawDisrupted(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        var (bankX, bankY) = ReceivingContacts(towerId, d.Stage);
        var flash = reducedEffects ? .80f : .56f + .44f * MathF.Pow(d.Beat(21), 2);
        var fault = ColorPalette.Violet * flash;
        var hot = Color.Lerp(ColorPalette.Violet, ColorPalette.Paper, .58f) * flash;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var start = new Vector2(MathF.Max(-.56f, bankX), sign * MathF.Min(.42f, bankY));
            var knee = new Vector2(-.12f, sign * .27f);
            var end = new Vector2(.08f, sign * .11f);
            d.Trace(start.X, start.Y, knee.X - .07f, knee.Y, fault, .065f);
            d.Trace(knee.X + .07f, knee.Y, end.X, end.Y, fault, .065f);

            var split = Vector2.Lerp(start, knee, .47f);
            var before = Vector2.Lerp(start, knee, .27f);
            var after = Vector2.Lerp(start, knee, .70f);
            d.Line(before.X, before.Y, after.X, after.Y, ColorPalette.Ink, .13f);
            d.Line(before.X, before.Y, split.X - .035f, split.Y, fault, .065f);
            d.Line(split.X + .035f, split.Y, after.X, after.Y, fault, .065f);
            var arc = (reducedEffects ? .025f : .018f + d.Beat(27 + sign) * .035f) * sign;
            d.Line(knee.X - .075f, knee.Y, knee.X - .025f, knee.Y - arc, hot, .035f);
            d.Line(knee.X - .025f, knee.Y - arc, knee.X + .028f, knee.Y + arc, hot, .035f);
            d.Line(knee.X + .028f, knee.Y + arc, knee.X + .078f, knee.Y, hot, .035f);
        }
    }

    public static void DrawSuppressed(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects)
    {
        var d = new TowerStatusFrame(batch, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        var orange = ColorPalette.Orange * (.85f + d.Beat(3.8f) * .15f);
        if (towerId == "arc_relay")
        {
            if (d.Stage == 1)
            {
                OutputClamp(d, .68f * MathF.Cos(.8f), .68f * MathF.Sin(.8f), .14f, orange);
                OutputClamp(d, .68f * MathF.Cos(.8f), -.68f * MathF.Sin(.8f), .14f, orange);
            }
            else OutputClamp(d, d.Stage == 4 ? .87f : .68f, 0, .18f, orange);
            return;
        }
        if (towerId is "siege_mortar" or "frost_spire" or "signal_beacon")
        {
            var halfWidth = towerId == "siege_mortar" ? .26f : .20f;
            OutputClamp(d, 0, 0, halfWidth, orange);
            return;
        }
        var muzzle = NightGridArt.TowerMuzzlePosition(Vector2.Zero, 1, towerId, level, 0, apex: apex).X;
        var width = towerId switch
        {
            "breaker_cannon" => d.Stage == 1 ? .24f : .34f,
            "prism_beam" => TowerTierVisuals.PrismApertureWidth(level, apex) * .50f,
            "needle_turret" => d.Stage == 1 ? .24f : .32f,
            "shard_fan" => d.Stage == 1 ? .40f : .56f,
            _ => .16f
        };
        if (towerId == "shard_fan")
        {
            var spread = d.Stage == 1 ? .43f : d.Stage == 4 ? .69f : .57f;
            for (var i = -1; i <= 1; i++) OutputClamp(d, muzzle - .10f, i * spread, .11f, orange);
        }
        else OutputClamp(d, muzzle - .10f, 0, width, orange);
    }

    private static void OutputClamp(TowerStatusFrame d, float x, float y, float halfWidth, Color color)
    {
        for (var sign = -1; sign <= 1; sign += 2)
        {
            d.Line(x - .12f, y + sign * halfWidth, x + .10f, y + sign * halfWidth, ColorPalette.Ink, .15f);
            d.Line(x - .11f, y + sign * halfWidth, x + .08f, y + sign * halfWidth, color, .075f);
        }
        d.Trace(x + .025f, y - halfWidth, x + .025f, y + halfWidth, color, .07f);
    }

    internal static (float X, float Y) ReceivingContacts(string towerId, int stage) => towerId switch
    {
        "needle_turret" => (-.40f, stage == 1 ? .31f : .48f),
        "watchtower" => (-.48f, stage == 1 ? .25f : .49f),
        "shard_fan" => (-.45f, .34f),
        "frost_spire" => (-.06f, TowerTierVisuals.FrostCoolantOffset(stage)),
        "ember_coil" => (-.35f, stage == 1 ? .42f : .57f),
        "breaker_cannon" => (-.35f, stage == 1 ? .35f : .52f),
        "siege_mortar" => (-.20f, TowerTierVisuals.MortarBankOffset(stage)),
        "prism_beam" => (-.24f, stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f }),
        "arc_relay" => (-.54f, stage < 3 ? .26f : .47f),
        "signal_beacon" => (-.30f, .38f),
        _ => (-.35f, .44f)
    };
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
