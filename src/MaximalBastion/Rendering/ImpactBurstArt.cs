using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Contact flashes, fragmented pressure and weapon-specific impact materials.</summary>
public static class ImpactBurstArt
{
    public static void DrawArea(SpriteBatch batch, PrimitiveRenderer p, string towerId, Vector2 position,
        Color accent, float radius, float progress, float time, bool reducedEffects, int stage)
    {
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (fade <= 0 || radius <= 0) return;
        var age = 1 - fade;
        var phase = Seed(position);
        var expansion = 1 - fade * fade * fade;
        var spread = radius * (.18f + .82f * expansion);
        var frost = towerId == "frost_spire";
        var hot = Color.Lerp(accent, ColorPalette.Paper, .72f);

        if (towerId is "ember_coil" or "siege_mortar")
            DrawDetonation(batch, p, position, Vector2.Zero, accent, radius, fade,
                reducedEffects, stage, towerId == "ember_coil" ? 2 : 5, opacity: .84f);
        else
        {
            if (frost)
                DrawContact(batch, p, towerId, position, Vector2.Zero, accent,
                    TowerAttackArt.ContactRadius(10, stage, stage == 4), fade, time, reducedEffects, stage);
            else
                p.Glow(batch, position, 6 + stage, hot, fade * fade * .65f);
            var plumeColor = frost ? ColorPalette.FrostAura : accent;
            var density = fade * (.65f + .35f * fade) * (frost ? .55f : .40f);
            for (var index = 0; index < (reducedEffects ? 3 : 4); index++)
            {
                var variation = Variation(index + 11, phase);
                var radial = Axis(phase + index * 2.399963f);
                var at = position + radial * radius * (.06f + expansion * (.24f + variation * .24f));
                var size = radius * (.20f + variation * .12f) * (.60f + expansion * .40f);
                p.EnergyEnvelope(batch, at, MathF.Max(3, size), plumeColor,
                    phase + index * 17 + age * 2, density * (reducedEffects ? .72f : 1), 6);
            }
            var fragments = reducedEffects ? 5 : 7 + stage;
            for (var index = 0; index < fragments; index++)
            {
                var variation = Variation(index, phase);
                var radial = Axis(phase + index * 2.399963f + variation * .37f);
                var at = position + radial * spread * (.32f + variation * .64f);
                var length = MathF.Min(6, 1.2f + radius * .05f) * (.5f + fade * .5f);
                p.Line(batch, at - radial * length, at, accent * (fade * .72f), frost ? .8f : 1.2f);
                p.Line(batch, at - radial * length * .3f, at, hot * (fade * .75f), .6f);
                if (frost && !reducedEffects)
                {
                    var side = new Vector2(-radial.Y, radial.X);
                    p.Line(batch, at - side * length * .4f, at + side * length * .4f, hot * (fade * .55f), .6f);
                }
            }
        }
    }

    public static void DrawDetonation(SpriteBatch batch, PrimitiveRenderer p, Vector2 position,
        Vector2 direction, Color accent, float radius, float progress, bool reducedEffects,
        int stage = 1, int texture = 5, float opacity = 1)
    {
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var age = 1 - fade;
        var extent = MathF.Max(4, radius);
        var expansion = 1 - MathF.Pow(fade, 3);
        var phase = Seed(position);
        var angle = direction.LengthSquared() > .0001f ? MathF.Atan2(direction.Y, direction.X) : phase;
        var hot = Color.Lerp(accent, ColorPalette.Paper, .78f);
        accent *= opacity;
        hot *= opacity;
        var flash = fade * fade;
        var body = fade * (.72f + .28f * fade);
        var lobes = reducedEffects ? 3 : 4 + stage / 2;
        var filledTexture = texture == 2 ? 7 : 6;

        // Overlapping density pockets give the burst an irregular, open silhouette.
        for (var index = 0; index < lobes; index++)
        {
            var radial = Axis(angle + index * 2.399963f);
            var variation = Variation(index, phase);
            var at = position + radial * extent * (.08f + expansion * (.28f + variation * .19f));
            var size = extent * (.22f + variation * .14f) * (.70f + expansion * .30f);
            p.EnergyEnvelope(batch, at, MathF.Max(3.5f, size), accent,
                phase + index * 17 + age * 3, body * (reducedEffects ? .78f : 1.10f), filledTexture);
            p.Glow(batch, at - radial * size * .13f, MathF.Max(4, size * 1.3f),
                accent, body * .50f);
        }
        p.Glow(batch, position, MathF.Max(6, extent * (.45f + expansion * .15f)), accent, flash * 1.05f);
        p.Glow(batch, position, MathF.Max(4, extent * .26f), hot, flash * 1.25f);

        var count = reducedEffects ? 4 + stage : 7 + stage * 2;
        for (var index = 0; index < count; index++)
        {
            var variation = Variation(index + 8, phase);
            var radial = Axis(angle + index * 2.399963f + variation * .37f);
            var distance = extent * (.13f + expansion * (.40f + variation * .48f));
            var length = MathF.Min(distance, (2 + extent * (.045f + variation * .07f)) * (.45f + fade * .55f));
            var at = position + radial * distance;
            var tail = at - radial * length;
            var width = MathF.Max(.7f, (extent * .025f + .45f) * (.65f + variation * .55f));
            p.Line(batch, tail, at, accent * (fade * (.65f + variation * .30f)), width);
            p.Line(batch, Vector2.Lerp(tail, at, .55f), at, hot * (fade * .9f), MathF.Max(.55f, width * .45f));
            if (!reducedEffects && index % 3 == 0)
                p.Glow(batch, at, width * 3.8f, accent, fade * .5f);
        }
        var coreSize = MathF.Max(1.5f, extent * .105f) * flash;
        var coreAxis = Axis(angle + .4f);
        p.Line(batch, position - coreAxis * coreSize, position + coreAxis * coreSize * .75f,
            hot * flash, MathF.Max(.7f, coreSize * .65f));
    }

    public static void DrawContact(SpriteBatch batch, PrimitiveRenderer p, string towerId,
        Vector2 position, Vector2 direction, Color accent, float radius, float progress,
        float time, bool reducedEffects, int stage)
    {
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var age = 1 - fade;
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var angle = MathF.Atan2(forward.Y, forward.X);
        var extent = Math.Clamp(radius, 3.5f, 14);
        var travel = extent * (.18f + (1 - fade * fade) * .82f);
        var hot = Color.Lerp(accent, ColorPalette.Paper, .72f);
        var phase = Seed(position);
        var count = reducedEffects ? 2 + stage / 2 : 4 + stage;
        var glowColor = towerId == "frost_spire" ? ColorPalette.FrostAura : accent;
        if (!reducedEffects) p.Glow(batch, position, MathF.Min(22, extent * 1.25f + 4), glowColor, fade * .42f);
        p.Circle(batch, position, 1.05f + stage * .15f, hot * (fade * fade * .9f));

        switch (towerId)
        {
            case "needle_turret":
            case "watchtower":
            {
                var precision = towerId == "watchtower";
                var penetration = precision ? 1.35f : .9f;
                p.Line(batch, position - forward * travel * .28f, position + forward * travel * penetration,
                    hot * (fade * fade), precision ? 1.35f + stage * .12f : .95f + stage * .1f);
                for (var index = 0; index < count; index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + (variation - .5f) * (precision ? 1.6f : 2.6f));
                    var at = position + ray * travel * (.35f + variation * .6f);
                    p.Line(batch, at - ray * (1.2f + stage * .35f) * fade, at,
                        (index % 2 == 0 ? accent : hot) * fade, .65f + variation * .4f);
                }
                break;
            }
            case "shard_fan":
                for (var index = 0; index < count; index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + index * 2.399963f + variation * .3f);
                    var at = position + ray * travel * (.35f + variation * .65f);
                    p.Line(batch, at - ray * (1.8f + stage * .6f) * fade, at,
                        accent * fade, 1 + variation * .7f);
                    if (!reducedEffects && index % 2 == 0) p.Glow(batch, at, 4 + stage, accent, fade * .5f);
                }
                p.Line(batch, position - forward * 1.6f * fade, position + forward * 2 * fade,
                    hot * fade, 1.2f + stage * .2f);
                break;
            case "frost_spire":
                if (!reducedEffects) p.EnergyEnvelope(batch, position, travel * .8f + 4,
                    ColorPalette.FrostAura, phase, fade * .85f, 6);
                for (var index = 0; index < count; index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + index * 2.399963f);
                    var cross = new Vector2(-ray.Y, ray.X);
                    var at = position + ray * travel * (.25f + variation * .65f);
                    var length = (1.4f + stage * .55f) * (.5f + variation * .5f) * fade;
                    p.Line(batch, at - ray * length, at + ray * length * .65f, hot * fade, .9f + variation * .6f);
                    p.Line(batch, at - cross * length * .55f, at + cross * length * .55f,
                        accent * (fade * .75f), .65f);
                }
                break;
            case "ember_coil":
                if (!reducedEffects)
                    p.EnergyEnvelope(batch, position, extent * (1 + age * .15f), ColorPalette.EmberFlame,
                        phase + age * 2, fade * .65f, 7);
                for (var index = 0; index < count; index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + index * 2.399963f);
                    var at = position + ray * travel * (.35f + variation * .55f);
                    p.Line(batch, at - ray * (1.2f + stage * .3f) * fade, at,
                        ColorPalette.EmberFlame * fade, .85f + variation * .35f);
                }
                p.Glow(batch, position, 3 + stage * .6f, ColorPalette.EmberCore, fade * fade * .8f);
                break;
            case "breaker_cannon":
                for (var index = 0; index < count + 1; index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + (variation - .5f) * 3.6f);
                    var at = position + ray * travel * (.32f + variation * .72f);
                    var length = (2 + stage * .6f) * fade;
                    p.Line(batch, at - ray * length, at, accent * fade, 1.4f + stage * .17f);
                    p.Line(batch, at - ray * length * .35f, at, hot * fade, .6f);
                }
                p.Line(batch, position - side * travel * .18f, position + forward * travel * .42f,
                    hot * (fade * fade), 2 + stage * .3f);
                break;
            case "arc_relay":
                for (var index = 0; index < Math.Min(count, 8); index++)
                {
                    var variation = Variation(index, phase);
                    var ray = Axis(angle + index * 2.399963f);
                    var cross = new Vector2(-ray.Y, ray.X);
                    var reach = travel * (.40f + variation * .55f);
                    var jitter = reducedEffects ? .7f : MathF.Sin(time * 38 + index * 4.7f);
                    var bend = position + ray * reach * .5f + cross * (1.2f + stage * .35f) * jitter;
                    var end = position + ray * reach;
                    p.Line(batch, position + ray, bend, accent * fade, 1.1f);
                    p.Line(batch, bend, end, hot * (fade * .9f), .7f);
                }
                break;
            case "prism_beam":
                // A bright contact cut with off-axis refracted chips remains subordinate to the beam.
                p.Line(batch, position - forward * 1.2f, position + forward * 2.2f,
                    hot * fade, 1.7f + stage * .25f);
                for (var index = 0; index < Math.Min(count, 6); index++)
                {
                    var variation = Variation(index, phase);
                    var sign = index % 2 == 0 ? 1 : -1;
                    var ray = Vector2.Normalize(forward * (.2f + variation * .5f) + side * sign);
                    var at = position + ray * travel * (.4f + variation * .4f);
                    p.Line(batch, at - ray * (1.1f + stage * .25f) * fade, at,
                        (index % 3 == 0 ? hot : accent) * fade, .75f + variation * .45f);
                }
                break;
            default:
                DrawDetonation(batch, p, position, direction, accent, extent, fade, reducedEffects, stage);
                break;
        }
    }

    private static Vector2 Axis(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    private static float Seed(Vector2 position) => position.X * .037f + position.Y * .053f;
    private static float Variation(int index, float phase) => .5f + .5f * MathF.Sin(index * 5.713f + phase);
}
