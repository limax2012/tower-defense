using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Presentation-only firing, impact, and support signatures for the four tower stages.</summary>
public static class TowerAttackArt
{
    public static void DrawArc(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float progress, float time, bool reducedEffects, int level = 1, bool apex = false,
        bool drawContact = true)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var fade = MathHelper.Clamp(progress, 0, 1);
        var delta = end - start;
        if (delta.LengthSquared() < .01f || fade <= 0) return;
        var forward = Vector2.Normalize(delta);
        var side = new Vector2(-forward.Y, forward.X);
        var count = 5 + stage * 2;
        var flicker = reducedEffects ? .7f : MathF.Floor(time * 22) * .71f;
        var amplitude = 2.3f + stage * .65f;
        var previous = start;
        for (var index = 1; index <= count; index++)
        {
            var fraction = index / (float)count;
            var displacement = index == count ? 0 : MathF.Sin(index * 5.3f + flicker) * amplitude;
            var next = Vector2.Lerp(start, end, fraction) + side * displacement;
            if (!reducedEffects)
                p.Line(batch, previous, next, accent * (fade * .22f), 5 + stage);
            p.Line(batch, previous, next, accent * fade, 1.3f + stage * .35f);
            p.Line(batch, previous, next, ColorPalette.Paper * (fade * .98f), .74f + stage * .17f);
            if (stage >= 3 && index > 1 && index < count && index % 3 == 0)
            {
                var branch = Vector2.Lerp(previous, next, .6f);
                var sign = index % 2 == 0 ? 1 : -1;
                var tip = branch - forward * 4 + side * (sign * (3 + stage));
                p.Line(batch, branch, tip, accent * (fade * .7f), 1);
                p.Line(batch, branch, Vector2.Lerp(branch, tip, .72f), ColorPalette.Paper * (fade * .85f), .45f);
                if (stage == 4)
                    p.Line(batch, tip, tip + forward * 5 - side * sign * 2, accent * (fade * .5f), .8f);
            }
            previous = next;
        }
        if (!reducedEffects)
        {
            p.Glow(batch, start, 9 + stage * 2, accent, fade * .7f);
        }
        if (drawContact)
            DrawImpact(batch, p, "arc_relay", end, delta, accent, 9, progress, time, reducedEffects, level, apex);
    }

    internal static float ContactRadius(float radius, int level, bool apex)
    {
        var scale = TowerTierVisuals.Stage(level, apex) switch { 1 => .70f, 2 => .86f, 3 => 1f, _ => 1.15f };
        return MathF.Min(14, Math.Clamp(radius, 6, 12) * scale);
    }

    public static void DrawImpact(SpriteBatch batch, PrimitiveRenderer p, string towerId, Vector2 position,
        Vector2 direction, Color accent, float radius, float progress, float time, bool reducedEffects,
        int level = 1, bool apex = false)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var extent = ContactRadius(radius, level, apex) * (towerId == "shard_fan" ? .82f : 1);
        ImpactBurstArt.DrawContact(batch, p, towerId, position, direction, accent, extent,
            progress, time, reducedEffects, stage);
    }

    public static void DrawSplash(SpriteBatch batch, PrimitiveRenderer p, string towerId, Vector2 position,
        Color accent, float radius, float progress, float time, bool reducedEffects,
        int level = 1, bool apex = false) =>
        ImpactBurstArt.DrawArea(batch, p, towerId, position, accent, radius, progress, time,
            reducedEffects, TowerTierVisuals.Stage(level, apex));

    public static void DrawBeaconBroadcast(SpriteBatch batch, PrimitiveRenderer p, Vector2 position,
        Color accent, float radius, float time, bool reducedEffects, int level = 1, bool apex = false)
    {
        var d = new TowerStatusFrame(batch, p, position, radius, "signal_beacon", level, apex,
            -MathHelper.PiOver2, time, reducedEffects);
        var energy = Color.Lerp(accent, ColorPalette.Paper, .14f + d.Beat(4.4f) * .14f);
        var hot = Color.Lerp(accent, ColorPalette.Paper, .68f);
        var phase = d.Phase(.72f);
        var reach = d.Stage == 1 ? .31f : d.Stage >= 3 ? .78f : .67f;
        for (var index = 0; index < 3; index++)
        {
            var a = index * MathHelper.TwoPi / 3;
            var axis = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var start = axis * (d.Stage == 1 ? .10f : .34f);
            var end = axis * reach;
            d.Trace(start.X, start.Y, end.X, end.Y, energy, .055f);
            var first = Vector2.Lerp(start, end, phase * .65f);
            var second = Vector2.Lerp(start, end, phase * .65f + .35f);
            d.Line(first.X, first.Y, second.X, second.Y, hot, .035f);
        }
        if (d.Stage == 4)
        {
            for (var sign = -1; sign <= 1; sign += 2)
                d.Trace(-.49f, sign * .66f, .23f, sign * .66f, energy, .055f);
        }
        if (!reducedEffects) d.Glow(0, 0, .67f, accent, .10f + d.Beat(4.4f) * .06f);
    }

    public static void DrawMuzzle(SpriteBatch batch, PrimitiveRenderer p, string towerId, Vector2 position,
        Vector2 direction, Color accent, float flash, bool reducedEffects, int level = 1, bool apex = false)
    {
        if (flash <= .02f) return;
        var stage = TowerTierVisuals.Stage(level, apex);
        if (towerId == "shard_fan")
        {
            var coreFlash = MathHelper.Clamp(flash * 2, 0, 1);
            var core = 2.2f + stage * .65f;
            if (!reducedEffects) p.Glow(batch, position, core * 4.2f, accent, coreFlash * .65f);
            p.DrawPolygon(batch, position, core * (.7f + coreFlash * .3f), 6, false,
                Color.Lerp(accent, ColorPalette.Paper, .78f) * coreFlash, -MathHelper.PiOver2);
            return;
        }
        if (stage == 1 || towerId is "signal_beacon" or "prism_beam") return;
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var strength = MathHelper.Clamp(flash * 4, 0, 1);
        var halfWidth = 2.1f + stage * .65f;
        var length = 2 + stage * 1.3f;
        if (towerId is "siege_mortar" or "ember_coil")
        {
            var heat = towerId == "ember_coil";
            var aim = MathF.Atan2(forward.Y, forward.X);
            var hot = heat ? ColorPalette.EmberCore : Color.Lerp(accent, ColorPalette.Paper, .7f);
            var vents = heat ? 3 : 3 + stage / 2;
            for (var index = 0; index < vents; index++)
            {
                var angle = heat ? aim + (index - 1) * .45f : aim + index * 2.399963f;
                var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var travel = halfWidth * (.4f + (1 - strength) * .7f);
                var start = position + ray * travel;
                var end = start + ray * (length * (.45f + index % 2 * .18f) * strength);
                p.Line(batch, start, end, accent * (strength * .8f), heat ? 1.6f : 1.1f);
                p.Line(batch, start, Vector2.Lerp(start, end, .55f), hot * strength, .6f);
            }
            if (!reducedEffects && heat)
                p.Glow(batch, position + forward * halfWidth, length * 1.4f, accent, strength * .5f);
        }
        else if (towerId == "frost_spire")
        {
            for (var index = 0; index < 4; index++)
            {
                var ray = index switch { 0 => forward, 1 => side, 2 => -forward, _ => -side };
                p.Line(batch, position + ray * halfWidth * .45f, position + ray * halfWidth * 1.15f,
                    Color.Lerp(accent, ColorPalette.Paper, .65f) * strength, .8f);
            }
        }
        else
        {
            for (var sign = -1; sign <= 1; sign += 2)
                p.Line(batch, position + side * sign * halfWidth - forward,
                    position + forward * length, accent * (strength * .7f), 1);
            if (stage >= 3)
                p.Line(batch, position - side * halfWidth, position + side * halfWidth,
                    ColorPalette.Paper * (strength * .65f), .8f);
            if (stage == 4 && !reducedEffects)
                p.Glow(batch, position + forward * length * .5f, length * 2, accent, strength * .55f);
        }
    }

    public static void DrawShardLaunch(SpriteBatch batch, PrimitiveRenderer p, Vector2 center,
        Vector2 projectilePosition, float radius, Color accent, float flash, bool reducedEffects,
        int level = 1, bool apex = false)
    {
        var ray = projectilePosition - center;
        var distance = ray.Length();
        var reach = radius * 2.6f;
        if (distance < .5f || distance >= reach || flash <= .02f) return;
        var stage = TowerTierVisuals.Stage(level, apex);
        var direction = ray / distance;
        var fade = Math.Clamp(flash * 2, 0, 1) * (1 - distance / reach);
        var end = center + direction * MathF.Min(distance, radius * 1.25f);
        // Short plasma filaments join each departing shot to the exposed aperture.
        if (!reducedEffects)
            p.Line(batch, center, end, accent * (fade * .22f), 3 + stage * .35f);
        p.Line(batch, center, end, accent * (fade * .85f), 1.1f + stage * .14f);
        p.Line(batch, center, Vector2.Lerp(center, end, .55f),
            Color.Lerp(accent, ColorPalette.Paper, .65f) * fade, .7f);
    }
}
