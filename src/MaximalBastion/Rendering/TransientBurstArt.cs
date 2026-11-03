using MaximalBastion.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Short machinery discharges and debris driven by bounded transient effect lifetimes.</summary>
public static class TransientBurstArt
{
    public static void DrawProtocol(SpriteBatch b, PrimitiveRenderer p, EffectInstance effect,
        AttackAppearance appearance, float progress, bool reduced)
    {
        var fade = Math.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var age = 1 - fade;
        var stage = TowerTierVisuals.Stage(appearance.Level, appearance.IsApex);
        var id = appearance.TowerId;
        var color = id == "arc_relay" ? ColorPalette.Cobalt : NightGridArt.TowerAccent(id ?? "");
        var glowColor = id == "frost_spire" ? ColorPalette.FrostAura : color;
        var hot = Color.Lerp(color, ColorPalette.Paper, .65f);
        var area = effect.Kind == EffectKind.Splash;
        var inward = !area && id is "needle_turret" or "signal_beacon";
        var extent = effect.Radius;
        var seed = effect.Start.X * .071f + effect.Start.Y * .043f;
        var travel = MathF.Pow(age, .62f);
        var count = reduced ? 12 : 20 + stage * 2;
        var core = MathF.Pow(fade, 2);
        p.Glow(b, effect.Start, MathF.Min(38, extent * .8f), glowColor, core * .95f);
        p.Glow(b, effect.Start, 8 + stage, hot, core * .9f);

        // Unequal propagation speeds and short lived pockets give the release volume.
        if (!reduced)
            for (var i = 0; i < 7; i++)
            {
                var a = seed + i * 2.399963f;
                var distance = extent * travel * (.14f + .48f * Noise(i, seed));
                var at = effect.Start + Polar(a) * distance;
                var size = MathF.Min(27, extent * (.15f + .08f * Noise(i + 8, seed)));
                p.Glow(b, at, size * (.65f + age), glowColor, fade * .34f);
            }

        for (var i = 0; i < count; i++)
        {
            var n = Noise(i, seed);
            var a = seed + i * 2.399963f;
            var axis = Polar(a);
            var velocity = .60f + .40f * n;
            var distance = inward
                ? extent * (.16f + (1 - travel) * .78f) * velocity
                : extent * (.06f + travel * .94f) * velocity;
            var tip = effect.Start + axis * distance;
            var length = MathF.Min(area ? 23 : 9, extent * (.09f + .05f * n)) * (.4f + fade * .6f);
            var tail = tip + axis * (inward ? length : -length);
            var alpha = fade * (.50f + .4f * n);
            var thickness = id is "breaker_cannon" or "siege_mortar" ? 1.7f : 1.1f;
            if (!reduced) p.Line(b, tail, tip, glowColor * (alpha * .16f), thickness + 3);
            if (id == "arc_relay")
            {
                var side = Polar(a + MathHelper.PiOver2);
                var elbow = Vector2.Lerp(tail, tip, .45f) + side * length * .23f;
                p.Line(b, tail, elbow, color * alpha, 1.5f);
                p.Line(b, elbow, tip, hot * alpha, .8f);
                p.Line(b, elbow, elbow - axis * length * .25f - side * length * .30f,
                    color * (alpha * .65f), .7f);
            }
            else
            {
                p.Line(b, tail, tip, color * alpha, thickness);
                p.Line(b, Vector2.Lerp(tail, tip, .65f), tip, hot * alpha, .7f);
                if (id == "frost_spire")
                    p.Line(b, tip - Polar(a + .7f) * 2.5f, tip, ColorPalette.Paper * (alpha * .7f), 1.1f);
                else if (id == "prism_beam")
                    p.Line(b, tip, tip - Polar(a + .32f) * length * .5f, color * (alpha * .6f), .8f);
                else if (id == "ember_coil")
                    p.Glow(b, tip, reduced ? 3 : 6, ColorPalette.EmberFlame, alpha * .7f);
                else if (id == "signal_beacon")
                    p.Line(b, tail + axis * 3, tail + axis * 5, ColorPalette.BeaconHighlight * alpha, 1.6f);
            }
        }
    }

    public static void DrawGeneric(SpriteBatch b, PrimitiveRenderer p, EffectInstance effect,
        float progress, bool reduced)
    {
        var fade = Math.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var age = 1 - fade;
        var color = effect.Color;
        var seed = effect.Start.X * .071f + effect.Start.Y * .043f;
        if (effect.Purpose == EffectPurpose.ForgeCharge)
        {
            p.Glow(b, effect.Start, effect.Radius * .75f, color, fade * .6f);
            for (var i = 0; i < (reduced ? 4 : 7); i++)
            {
                var axis = Polar(seed + i * 2.399963f);
                var at = effect.Start + axis * (5 + effect.Radius * fade * (.4f + Noise(i, seed) * .4f));
                p.Line(b, at, at - axis * (3 + fade * 4), color * fade, 1.5f);
            }
            return;
        }
        if (effect.Kind == EffectKind.Splash)
        {
            ImpactBurstArt.DrawDetonation(b, p, effect.Start, Vector2.Zero, color,
                effect.Radius, fade, reduced, stage: 2);
            return;
        }
        if (effect.Kind == EffectKind.Impact)
        {
            TowerAttackArt.DrawImpact(b, p, "needle_turret", effect.Start, effect.Direction,
                color, effect.Radius, fade, 0, reduced);
            return;
        }

        var escaping = effect.Purpose == EffectPurpose.EnemyEscape;
        var reach = effect.Radius * (escaping ? .8f : 1.2f);
        p.Glow(b, effect.Start, effect.Radius * 1.3f, color, fade * fade * .7f);
        var count = reduced ? 5 : 9;
        for (var i = 0; i < count; i++)
        {
            var axis = Polar(seed + i * 2.399963f);
            var distance = reach * (escaping ? 1 - age * .85f : .15f + age * .85f) * (.45f + .55f * Noise(i, seed));
            var at = effect.Start + axis * distance;
            var length = (escaping ? 5 : 3) * (.6f + Noise(i + 4, seed)) * fade;
            if (!escaping)
                p.Line(b, at - axis * length, at + axis * length * .5f,
                    Color.Lerp(ColorPalette.Metal, ColorPalette.Muted, .5f) * fade, 2.2f);
            p.Line(b, at, at + axis * (escaping ? -length : length * .7f), color * fade, 1.2f);
            if (!reduced) p.Glow(b, at, 4, color, fade * .4f);
        }
    }

    private static Vector2 Polar(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    private static float Noise(int index, float seed)
    {
        var value = MathF.Sin(index * 127.1f + seed * 17.7f) * 43758.5453f;
        return value - MathF.Floor(value);
    }
}
