using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Localized energy density and weapon-specific flow sustained by a live Protocol.</summary>
public static partial class TowerProtocolArt
{
    public static float Strength(float remaining, float duration, float lead = 0)
    {
        if (remaining <= 0) return 0;
        var left = MathF.Max(0, remaining - lead);
        var rise = Smooth(Math.Clamp((duration - left) / .08f, 0, 1));
        var fall = Smooth(Math.Clamp(left / .05f, 0, 1));
        return (.55f + .45f * rise) * fall;
    }

    public static void DrawEnvelope(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float time, bool reducedEffects, float strength = 1)
    {
        if (strength <= 0) return;
        var stage = TowerTierVisuals.Stage(level, apex);
        var phase = reducedEffects ? .35f : time + center.X * .013f + center.Y * .007f;
        var power = reducedEffects ? 1 : .96f + .04f * MathF.Sin(phase * 4.2f);
        p.EnergyEnvelope(b, center, radius * (2.14f + stage * .025f), FieldColor(towerId),
            phase, power * Math.Clamp(strength, 0, 1), FieldStyle(towerId));
    }

    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, float time, bool reducedEffects = false, bool glow = true) =>
        Draw(b, p, center, radius, towerId, 1, false, -MathHelper.PiOver2, time, reducedEffects, glow);

    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects = false,
        bool glow = true, float strength = 1, bool external = true, float firing = 0, bool envelope = true)
    {
        strength = Math.Clamp(strength, 0, 1);
        if (strength <= 0) return;
        var frame = new TowerStatusFrame(b, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        TowerHardwareChannels.DrawProtocol(frame, towerId, active: true, glow: false, strength, firing);
        if (!external) return;
        if (envelope) DrawEnvelope(b, p, center, radius, towerId, level, apex, time, reducedEffects, strength);
        var phase = reducedEffects ? .35f : time + center.X * .013f + center.Y * .007f;
        var field = new ProtocolField(b, p, center, radius, phase, glow && !reducedEffects, strength);
        DrawFlow(field, towerId);
    }

    private static Color FieldColor(string id) =>
        id == "frost_spire" ? ColorPalette.FrostAura : NightGridArt.TowerAccent(id);
    private static int FieldStyle(string id) => id switch
    {
        "frost_spire" => 1,
        "ember_coil" => 2,
        "arc_relay" => 3,
        "prism_beam" => 4,
        "breaker_cannon" or "siege_mortar" => 5,
        _ => 0
    };
    private static Vector2 Polar(float angle, float radius) => new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    private static float Smooth(float value) => value * value * (3 - 2 * value);

    private readonly struct ProtocolField(SpriteBatch batch, PrimitiveRenderer p, Vector2 center,
        float radius, float time, bool glow, float strength)
    {
        public float Time => time;
        public float Phase(float rate, float offset) => (time * rate + offset) - MathF.Floor(time * rate + offset);
        public void Line(Vector2 first, Vector2 last, Color color, float width, float alpha)
        {
            alpha *= strength;
            if (alpha <= .005f) return;
            var start = center + first * radius;
            var end = center + last * radius;
            if (glow)
                p.Line(batch, start, end, color * (alpha * .18f), MathF.Max(1, radius * width * 2.8f));
            p.Line(batch, start, end, color * alpha, MathF.Max(.6f, radius * width));
        }
    }
}
