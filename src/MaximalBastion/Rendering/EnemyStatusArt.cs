using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Local material, armor, and drive cues attached to the enemy's rendered hull.</summary>
public static class EnemyStatusArt
{
    public static bool IsRegenerating(EnemyInstance enemy) =>
        !enemy.IsDead && !enemy.HasEscaped && enemy.Definition.RegenerationPerSecond > 0 &&
        enemy.Health < enemy.MaxHealth && enemy.DamagePauseTimer <= 0;

    public static bool IsAccelerated(EnemyInstance enemy) =>
        !enemy.IsDead && !enemy.HasEscaped && enemy.FormationSpeedMultiplier > 1.001f && enemy.CurrentSpeed > 0;

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy, Vector2 position,
        float radius, float headingAngle, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        var (sides, hullRadius) = Hull(enemy, radius);
        var forward = new Vector2(MathF.Cos(headingAngle), MathF.Sin(headingAngle));
        var side = new Vector2(-forward.Y, forward.X);
        var phase = reducedEffects ? 0 : time + enemy.Id * .173f;
        Vector2 At(float x, float y) => position + (forward * x + side * y) * radius;
        Vector2 Vertex(int index, float scale = 1)
        {
            var angle = headingAngle + index * MathHelper.TwoPi / sides;
            return position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * hullRadius * scale;
        }
        void Seam(Vector2 first, Vector2 bend, Vector2 last, Color color, float width)
        {
            p.Line(batch, first, bend, ColorPalette.Ink * .85f, width + 1.4f);
            p.Line(batch, bend, last, ColorPalette.Ink * .85f, width + 1.4f);
            p.Line(batch, first, bend, color, width);
            p.Line(batch, bend, last, color, width);
        }

        if (enemy.Shield > 0)
        {
            var shimmer = reducedEffects ? .7f : .66f + MathF.Sin(phase * 3.5f) * .09f;
            p.DrawPolygon(batch, position, hullRadius, sides, false, ColorPalette.Shield * .055f, headingAngle);
            p.DrawPolygonOutline(batch, position, hullRadius + .9f, sides, false,
                ColorPalette.Shield * shimmer, headingAngle, 1.3f);
            for (var index = 0; index < sides; index++)
            {
                var first = Vertex(index);
                var next = Vertex((index + 1) % sides);
                var start = Vector2.Lerp(first, next, .18f);
                var end = Vector2.Lerp(first, next, .40f);
                p.Line(batch, start, end, ColorPalette.Paper * .55f, .8f);
            }
        }

        if (enemy.StatusEffects.DamageMultiplier > 1)
        {
            var brightness = reducedEffects ? .85f : .76f + MathF.Sin(phase * 6) * .12f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var first = At(-.29f, sign * .31f);
                var bend = At(.11f, sign * .31f);
                var last = At(.28f, sign * .18f);
                Seam(first, bend, last, ColorPalette.Violet * brightness, 1.15f);
                p.DrawDiamond(batch, first, MathF.Max(.8f, radius * .045f), ColorPalette.Violet, headingAngle);
                p.Circle(batch, last, .85f, ColorPalette.Paper * .7f);
            }
        }

        if (enemy.StatusEffects.ArmorReduction > 0)
        {
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var first = At(-.29f, sign * .47f);
                var bend = At(-.09f, sign * .35f);
                var last = At(-.16f, sign * .19f);
                Seam(first, bend, last, ColorPalette.ArmorBreak * .95f, 1.3f);
                p.Line(batch, bend, At(.05f, sign * .43f), ColorPalette.Ink, 2.4f);
                p.Line(batch, bend, At(.05f, sign * .43f), ColorPalette.ArmorBreak * .8f, 1);
            }
        }

        if (enemy.StatusEffects.SlowFactor > 0)
        {
            // Ice accumulates on separated plate edges, leaving the original hull contour readable.
            for (var deposit = 0; deposit < 3; deposit++)
            {
                var edge = deposit * sides / 3;
                var first = Vertex(edge, .94f);
                var next = Vertex((edge + 1) % sides, .94f);
                var start = Vector2.Lerp(first, next, .29f);
                var end = Vector2.Lerp(first, next, .60f);
                var middle = Vector2.Lerp(start, end, .55f);
                p.Line(batch, start, end, ColorPalette.Slow * .9f, 1.7f);
                p.Line(batch, middle, Vector2.Lerp(middle, position, .22f), ColorPalette.Paper * .7f, .9f);
                p.DrawDiamond(batch, start, MathF.Max(.8f, radius * .055f), ColorPalette.Slow, headingAngle);
            }
        }

        if (enemy.StatusEffects.IsBurning)
        {
            for (var ember = 0; ember < 2; ember++)
            {
                var at = ember == 0 ? At(-.14f, .28f) : At(.23f, -.21f);
                var flicker = reducedEffects ? .8f : .72f + MathF.Sin(phase * 13 + ember * 2.3f) * .18f;
                if (!reducedEffects)
                    p.Glow(batch, at, radius * .48f, ColorPalette.Burn, flicker * .6f);
                p.Circle(batch, at, MathF.Max(1.15f, radius * .075f), ColorPalette.Burn * flicker);
                p.Line(batch, at - forward * radius * .10f, at + side * radius * .07f,
                    ColorPalette.EmberFlame * flicker, 1.1f);
                p.Circle(batch, at + side * .5f, .7f, ColorPalette.EmberCore * flicker);
                if (!reducedEffects)
                {
                    var drift = (phase * 1.7f + ember * .5f) % 1;
                    var mote = at - forward * radius * drift * .24f + side * radius * drift * .10f;
                    p.Circle(batch, mote, .65f, ColorPalette.Burn * ((1 - drift) * .7f));
                }
            }
        }

        if (IsRegenerating(enemy))
        {
            for (var rib = -1; rib <= 1; rib++)
            {
                var active = reducedEffects ? .72f : .40f + .48f * MathF.Pow(MathF.Sin(phase * 3.2f - rib), 2);
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var first = At(rib * .26f, sign * .23f);
                    var last = At(rib * .26f, sign * .37f);
                    p.Line(batch, first, last, ColorPalette.Green * active, 1.6f);
                    if (!reducedEffects) p.Glow(batch, last, radius * .18f, ColorPalette.Green, active * .33f);
                }
            }
        }

        if (IsAccelerated(enemy))
        {
            var energy = reducedEffects ? .75f : .7f + MathF.Sin(phase * 11) * .14f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var front = At(-.19f, sign * .34f);
                var rear = At(-.48f, sign * .34f);
                var exhaust = At(-.73f, sign * .27f);
                p.Line(batch, front, rear, ColorPalette.Ink, 3.1f);
                p.Line(batch, front, rear, ColorPalette.Cyan * energy, 1.4f);
                p.Line(batch, rear, exhaust, ColorPalette.Cyan * (energy * .55f), 1.1f);
                if (!reducedEffects) p.Glow(batch, rear, radius * .25f, ColorPalette.Cyan, energy * .4f);
            }
        }

        if (enemy.StatusEffects.IsStunned)
        {
            // Short discharges bridge the internal plates without obscuring the hostile core.
            var step = reducedEffects ? 0 : MathF.Floor(phase * 18f);
            var energy = reducedEffects ? .90f : .83f + MathF.Sin(phase * 27f) * .13f;
            var charge = ColorPalette.Cyan * energy;
            var hot = Color.Lerp(ColorPalette.Cyan, ColorPalette.Paper, .78f) * energy;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var shift = reducedEffects ? 0 : MathF.Sin(step * 2.7f + sign) * .045f;
                var first = At(-.31f, sign * .18f);
                var bend = At(-.12f, sign * (.30f + shift));
                var knee = At(.06f, sign * (.17f - shift));
                var last = At(.29f, sign * .21f);
                Seam(first, bend, knee, charge, 1.6f);
                p.Line(batch, knee, last, ColorPalette.Ink * .85f, 3);
                p.Line(batch, knee, last, charge, 1.6f);
                p.Line(batch, first, bend, hot, .7f);
                p.Line(batch, bend, knee, hot, .7f);
                p.Line(batch, knee, last, hot, .7f);
                if (!reducedEffects)
                    p.Glow(batch, bend, radius * .32f, ColorPalette.Cyan, energy * .30f);
            }
        }

        if (enemy.IsElite || enemy.IsBoss)
        {
            var rankColor = enemy.BossPhaseActive ? ColorPalette.Coral : ColorPalette.Gold;
            var trim = enemy.IsBoss ? 2.1f : 1.6f;
            for (var corner = 0; corner < sides; corner++)
            {
                if (!enemy.IsBoss && corner != 0 && corner != sides - 1) continue;
                var vertex = Vertex(corner, .88f);
                var before = Vertex((corner + sides - 1) % sides, .88f);
                var after = Vertex((corner + 1) % sides, .88f);
                p.Line(batch, Vector2.Lerp(vertex, before, .18f), vertex, rankColor * .85f, trim);
                p.Line(batch, vertex, Vector2.Lerp(vertex, after, .18f), rankColor * .85f, trim);
            }
            if (enemy.IsBoss)
                p.Line(batch, At(.30f, -.27f), At(.30f, .27f), ColorPalette.Gold, 1.5f);
        }
    }

    public static void DrawRepairPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float headingAngle, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || progress <= 0) return;
        var fade = MathHelper.Clamp(progress, 0, 1);
        var forward = new Vector2(MathF.Cos(headingAngle), MathF.Sin(headingAngle));
        var side = new Vector2(-forward.Y, forward.X);
        var phase = reducedEffects ? 0 : time + enemy.Id * .173f;
        var (sides, hullRadius) = Hull(enemy, radius);
        p.DrawPolygon(batch, position, hullRadius * .92f, sides, false,
            ColorPalette.Green * (.09f * fade), headingAngle);
        for (var rib = -1; rib <= 1; rib++)
        {
            var active = reducedEffects ? .85f : .68f + MathF.Sin(phase * 5 - rib * .8f) * .20f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var root = position + forward * radius * rib * .24f + side * radius * sign * .22f;
                var tip = root + side * radius * sign * .18f;
                p.Line(batch, root, tip, ColorPalette.Green * (active * fade), 2);
                p.Circle(batch, tip, .8f, ColorPalette.Paper * (fade * .65f));
                if (!reducedEffects) p.Glow(batch, tip, radius * .23f, ColorPalette.Green, fade * .3f);
            }
        }
    }

    public static void DrawShieldPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float headingAngle, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.Shield <= 0 || progress <= 0) return;
        var fade = MathHelper.Clamp(progress, 0, 1);
        var (sides, hullRadius) = Hull(enemy, radius);
        var phase = reducedEffects ? .35f : (1 - fade) * .65f;
        p.DrawPolygon(batch, position, hullRadius, sides, false, ColorPalette.Shield * (.09f * fade), headingAngle);
        for (var edge = 0; edge < sides; edge++)
        {
            var firstAngle = headingAngle + edge * MathHelper.TwoPi / sides;
            var nextAngle = headingAngle + (edge + 1) * MathHelper.TwoPi / sides;
            var first = position + new Vector2(MathF.Cos(firstAngle), MathF.Sin(firstAngle)) * (hullRadius + .9f);
            var next = position + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * (hullRadius + .9f);
            p.Line(batch, first, next, ColorPalette.Shield * (fade * .8f), 1.6f);
            p.Line(batch, Vector2.Lerp(first, next, phase), Vector2.Lerp(first, next, phase + .25f),
                ColorPalette.Paper * (fade * .65f), 1);
        }
    }

    private static (int Sides, float Radius) Hull(EnemyInstance enemy, float radius)
    {
        if (enemy.Definition.Id.Contains("runner", StringComparison.Ordinal)) return (3, radius);
        var heavy = enemy.IsBoss || enemy.Definition.Id.Contains("brute", StringComparison.Ordinal) ||
            enemy.Definition.Id.Contains("aegis", StringComparison.Ordinal);
        return (heavy ? 6 : 4, radius * .85f);
    }
}
