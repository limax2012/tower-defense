using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static float OverdriveClock(Chassis f) => f.Reduced || f.AtRest ? 1.1f :
        float.IsFinite(f.OverdriveElapsed) ? MathF.Max(0, f.OverdriveElapsed) : f.Time;

    private static float OverdriveStrength(Chassis f) => f.Reduced || f.AtRest ? 1 :
        float.IsFinite(f.OverdriveElapsed)
            ? MathHelper.SmoothStep(0, 1, Math.Clamp(f.OverdriveElapsed / .12f, 0, 1))
            : f.Opening;

    private static void DrawOverdriveLamp(Chassis f, Vector2 first, Vector2 last, float width)
    {
        var strength = OverdriveStrength(f);
        if (strength <= 0) return;
        if (f.Reduced || f.AtRest)
        {
            f.Rail(first.X, first.Y, last.X, last.Y, ColorPalette.EnemyOverdriveWhite * .18f, width);
            return;
        }

        var time = OverdriveClock(f);
        var phase = float.IsFinite(f.OverdriveElapsed) && time < .58f
            ? MathHelper.SmoothStep(0, 1, Math.Clamp(time / .58f, 0, 1))
            : ((time - .58f) % 1.65f + 1.65f) % 1.65f / 1.65f;
        var front = MathHelper.Lerp(-.625f, .625f, phase);
        const float halfWidth = .0625f;
        if (front + halfWidth <= first.X || front - halfWidth >= last.X) return;

        // Each light segment remains inside its own lamp's original footprint.
        const int segments = 8;
        for (var segment = 0; segment < segments; segment++)
        {
            var start = Vector2.Lerp(first, last, segment / (float)segments);
            var end = Vector2.Lerp(first, last, (segment + 1f) / segments);
            var distance = MathF.Abs((start.X + end.X) * .5f - front) / halfWidth;
            var intensity = MathHelper.SmoothStep(0, 1, Math.Clamp((1 - distance) / .75f, 0, 1));
            if (intensity <= .001f) continue;
            f.Rail(start.X, start.Y, end.X, end.Y,
                ColorPalette.EnemyOverdriveWhite * (strength * intensity), width);
        }
    }

    private static void DrawOverdriveReactor(Chassis f)
    {
        var strength = OverdriveStrength(f);
        if (strength <= 0) return;
        var clock = OverdriveClock(f);
        var breathe = .5f + .5f * MathF.Sin(clock * 3.2f);
        var radius = CoreRadius(f.Enemy);
        var rotation = f.Heading + MathHelper.Pi / 8;
        var signal = f.Enemy.SignalRole != EnemySignalRole.None;
        var energy = signal ? EnemySignalGlyphRenderer.Accent(f.Enemy.SignalRole) : ColorPalette.EnemyMagenta;
        var hot = signal ? Color.Lerp(energy, ColorPalette.Paper, .60f) : ColorPalette.EnemyHot;
        var flash = !f.Reduced && !f.AtRest && float.IsFinite(f.OverdriveElapsed)
            ? MathF.Pow(MathF.Max(0, MathF.Sin(Math.Clamp(f.OverdriveElapsed / .20f, 0, 1) * MathHelper.Pi)), 1.4f)
            : 0;

        f.P.ArmorPolygon(f.Batch, f.Center, radius * .68f, 8,
            hot * (strength * (.14f + .04f * breathe)), rotation);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .55f, 8,
            hot * (strength * (.20f + .06f * breathe)), rotation);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * (.40f + .055f * breathe), 8,
            ColorPalette.EnemyExhaustHighlight * (strength * (.46f + .13f * breathe)), rotation);

        for (var edge = 0; edge < 8; edge++)
        {
            var angle = rotation + edge * MathHelper.PiOver4;
            var first = f.Center + new Vector2(MathF.Cos(angle + .06f), MathF.Sin(angle + .06f)) * radius * .94f;
            var last = f.Center + new Vector2(MathF.Cos(angle + MathHelper.PiOver4 - .06f),
                MathF.Sin(angle + MathHelper.PiOver4 - .06f)) * radius * .94f;
            f.P.ArmorLine(f.Batch, first, last, energy * (strength * (.42f + .23f * flash)), .8f);
        }
        if (flash <= .001f) return;
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .96f, 8, energy * (flash * .12f), rotation);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .68f, 8, hot * (flash * .40f), rotation);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .40f, 8,
            ColorPalette.EnemyExhaustHighlight * (flash * .85f), rotation);
    }
}
