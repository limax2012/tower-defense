using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Presentation-only firing, impact, and support signatures for the four tower stages.</summary>
public static class TowerAttackArt
{
    public static void DrawArc(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float progress, float time, bool reducedEffects, int level = 1, bool apex = false)
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
                p.Line(batch, previous, next, accent * (fade * .16f), 5 + stage);
            p.Line(batch, previous, next, accent * fade, 1.3f + stage * .35f);
            if (stage >= 2)
                p.Line(batch, previous, next, ColorPalette.Paper * (fade * .82f), .55f + stage * .14f);
            if (stage >= 3 && index > 1 && index < count && index % 3 == 0)
            {
                var branch = Vector2.Lerp(previous, next, .6f);
                var sign = index % 2 == 0 ? 1 : -1;
                var tip = branch - forward * 4 + side * (sign * (3 + stage));
                p.Line(batch, branch, tip, accent * (fade * .7f), 1);
                if (stage == 4)
                    p.Line(batch, tip, tip + forward * 5 - side * sign * 2, accent * (fade * .5f), .8f);
            }
            previous = next;
        }
        if (!reducedEffects)
        {
            p.Glow(batch, start, 9 + stage * 2, accent, fade * .7f);
            p.Glow(batch, end, 9 + stage * 2, accent, fade * .7f);
        }
        p.Circle(batch, end, 1 + stage * .4f, ColorPalette.Paper * fade);
    }

    public static void DrawImpact(SpriteBatch batch, PrimitiveRenderer p, string towerId, Vector2 position,
        Vector2 direction, Color accent, float radius, float progress, float time, bool reducedEffects,
        int level = 1, bool apex = false)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var age = 1 - fade;
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var angle = MathF.Atan2(forward.Y, forward.X);
        var reach = Math.Clamp(radius, 4, 90) * (.32f + age * .68f);
        var color = accent * fade;
        var hot = Color.Lerp(accent, ColorPalette.Paper, .65f) * fade;
        if (!reducedEffects)
            p.Glow(batch, position, MathF.Min(70, reach * 2 + 9), accent, fade * (.55f + stage * .05f));

        void Rays(int count, float inner, float outer, float rotation, float thickness)
        {
            for (var index = 0; index < count; index++)
            {
                var a = rotation + index * MathHelper.TwoPi / count;
                var radial = new Vector2(MathF.Cos(a), MathF.Sin(a));
                p.Line(batch, position + radial * inner, position + radial * outer, color, thickness);
            }
        }

        switch (towerId)
        {
            case "needle_turret":
                p.Line(batch, position - forward * reach * .55f, position + forward * reach,
                    hot, 1.1f + stage * .25f);
                if (stage >= 2) Rays(2 + stage, reach * .35f, reach * .7f, angle + .5f, 1);
                if (stage == 4) p.DrawDiamond(batch, position, 2.3f * fade, hot, angle);
                break;
            case "watchtower":
                p.Line(batch, position - forward * reach, position + forward * reach * 1.5f,
                    hot, 1.4f + stage * .2f);
                p.Line(batch, position - side * reach * .45f, position + side * reach * .45f, color, 1.2f);
                if (stage >= 3)
                    Rays(stage == 4 ? 8 : 4, reach * .35f, reach * .75f, angle + MathHelper.PiOver4, 1);
                break;
            case "shard_fan":
                for (var index = 0; index < 2 + stage; index++)
                {
                    var a = angle + index * MathHelper.TwoPi / (2 + stage);
                    var radial = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    var at = position + radial * reach * .65f;
                    p.Line(batch, at - radial * (1.5f + stage * .35f), at, color, 1.3f + stage * .2f);
                }
                p.Circle(batch, position, (1.2f + stage * .4f) * fade, hot);
                break;
            case "frost_spire":
            {
                var count = stage == 4 ? 6 : 4;
                Rays(count, reach * .20f, reach, angle, stage >= 3 ? 1.7f : 1.2f);
                if (stage >= 2)
                    p.DrawPolygonOutline(batch, position, MathF.Max(2, reach * .58f), count, false,
                        color * .7f, angle, 1);
                if (stage >= 3)
                    for (var index = 0; index < count; index++)
                    {
                        var a = angle + index * MathHelper.TwoPi / count;
                        var radial = new Vector2(MathF.Cos(a), MathF.Sin(a));
                        p.DrawDiamond(batch, position + radial * reach * .7f, 1.5f * fade, hot, a);
                    }
                break;
            }
            case "ember_coil":
                for (var index = 0; index < 2 + stage; index++)
                {
                    var a = angle + index * MathHelper.TwoPi / (2 + stage) + age * .35f;
                    var at = position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * reach * .56f;
                    if (!reducedEffects) p.Glow(batch, at, reach * .75f + 4, ColorPalette.EmberShot, fade * .65f);
                    p.Circle(batch, at, MathF.Max(1.3f, reach * .13f) * fade, ColorPalette.EmberFlame * fade);
                }
                p.Circle(batch, position, (2 + stage * .6f) * fade, ColorPalette.EmberCore * fade);
                if (stage == 4) Rays(5, reach * .72f, reach, angle, 1.3f);
                break;
            case "breaker_cannon":
                p.DrawPolygonOutline(batch, position, MathF.Max(2, reach * .7f), stage < 3 ? 4 : 6,
                    false, color, angle, stage == 4 ? 2.1f : 1.4f);
                Rays(2 + stage, reach * .72f, reach * 1.05f, angle, 1.5f + stage * .15f);
                if (stage >= 2) p.Line(batch, position - forward * reach * .4f,
                    position + forward * reach * .5f, hot, 1.7f);
                break;
            case "siege_mortar":
                p.Ring(batch, position, MathF.Max(3, reach), color, stage >= 3 ? 3 : 2);
                if (stage >= 2)
                    p.Ring(batch, position, MathF.Max(2, reach * .55f), hot * .65f, 1);
                if (stage >= 3)
                    Rays(stage == 4 ? 8 : 6, reach * .75f, reach * 1.05f, angle, 1.5f);
                if (stage == 4)
                    p.DrawPolygonOutline(batch, position, MathF.Max(2, reach * .30f), 6, false,
                        hot * .75f, angle, 1.5f);
                break;
            case "arc_relay":
                Rays(2 + stage, reach * .1f, reach * .8f, angle + (reducedEffects ? 0 : time * .4f), 1.2f);
                p.Circle(batch, position, 1.4f + stage * .35f, hot);
                break;
            case "prism_beam":
                p.DrawDiamond(batch, position, (2 + stage * .5f) * fade, hot, angle);
                if (stage >= 2) Rays(4, reach * .4f, reach * .8f, angle, 1);
                if (stage == 4)
                    p.DrawPolygonOutline(batch, position, MathF.Max(2, reach * .65f), 4, false,
                        color, angle, 1.3f);
                break;
            default:
                p.Ring(batch, position, MathF.Max(2, reach), color, 2);
                break;
        }
    }

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
        if (stage == 1 || towerId is "signal_beacon" or "prism_beam") return;
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var strength = MathHelper.Clamp(flash * 4, 0, 1);
        var halfWidth = 2.1f + stage * .65f;
        var length = 2 + stage * 1.3f;
        if (towerId is "siege_mortar" or "ember_coil")
        {
            p.Ring(batch, position, halfWidth + (1 - strength) * 3, accent * (strength * .6f), 1);
            if (stage == 4)
                p.Line(batch, position - side * halfWidth, position + side * halfWidth,
                    ColorPalette.Paper * strength, 1);
        }
        else if (towerId == "shard_fan")
        {
            for (var index = -1; index <= 1; index++)
            {
                var spread = Vector2.Normalize(forward + side * index * .55f);
                p.Line(batch, position + spread * 2, position + spread * length,
                    accent * strength, 1 + stage * .2f);
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
}
