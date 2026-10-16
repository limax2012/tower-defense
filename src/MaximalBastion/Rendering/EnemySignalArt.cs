using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Signal carriers use role-specific machinery and short, local transfer feedback.</summary>
public static class EnemySignalArt
{
    public static void DrawHardware(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None) return;
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var accent = EnemySignalGlyphRenderer.Accent(enemy.SignalRole);
        var phase = reducedEffects ? 0 : time + enemy.Id * .173f;
        var light = reducedEffects ? .8f : .76f + MathF.Sin(phase * 3.1f) * .10f;
        Vector2 At(float x, float y) => position + (forward * x + side * y) * radius;
        void Rail(float from, float to, float lateral, Color color, float width) =>
            p.Line(batch, At(from, lateral), At(to, lateral), color, radius * width);

        switch (enemy.SignalRole)
        {
            case EnemySignalRole.Accelerator:
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Rail(-.56f, -.06f, sign * .34f, ColorPalette.Ink, .23f);
                    Rail(-.54f, -.08f, sign * .34f, ColorPalette.Muted, .14f);
                    Rail(-.54f, -.14f, sign * .34f, accent * light, .065f);
                    p.Line(batch, At(-.31f, sign * .25f), At(-.31f, sign * .43f), ColorPalette.Metal,
                        MathF.Max(1, radius * .07f));
                    p.Circle(batch, At(-.57f, sign * .34f), MathF.Max(.8f, radius * .06f), accent);
                }
                break;
            case EnemySignalRole.Restorer:
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Rail(-.30f, .22f, sign * .34f, ColorPalette.Ink, .22f);
                    Rail(-.27f, .20f, sign * .34f, ColorPalette.Metal, .16f);
                    for (var rib = 0; rib < 3; rib++)
                    {
                        var along = -.20f + rib * .17f;
                        p.Line(batch, At(along, sign * .27f), At(along, sign * .41f),
                            accent * light, MathF.Max(1, radius * .055f));
                    }
                }
                p.Line(batch, At(-.33f, -.29f), At(-.33f, .29f), ColorPalette.Muted, 1.2f);
                break;
            case EnemySignalRole.Bulwark:
                DrawShieldPlates(batch, p, enemy, position, radius, heading, light, false);
                break;
            case EnemySignalRole.Jammer:
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Rail(-.37f, -.03f, sign * .35f, ColorPalette.Ink, .21f);
                    Rail(-.35f, -.06f, sign * .35f, ColorPalette.Muted, .13f);
                    p.Line(batch, At(-.27f, sign * .35f), At(-.63f, sign * .47f), ColorPalette.Ink, 3.2f);
                    p.Line(batch, At(-.27f, sign * .35f), At(-.63f, sign * .47f), ColorPalette.Metal, 1.9f);
                    p.DrawDiamond(batch, At(-.63f, sign * .47f), MathF.Max(1.15f, radius * .075f),
                        accent * light, heading);
                    p.Line(batch, At(-.37f, sign * .29f), At(-.37f, sign * .41f), accent, 1.2f);
                }
                break;
            case EnemySignalRole.Disruptor:
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Rail(-.32f, .27f, sign * .33f, ColorPalette.Ink, .22f);
                    Rail(-.29f, .24f, sign * .33f, ColorPalette.Muted, .14f);
                    Rail(-.27f, -.065f, sign * .33f, accent * light, .065f);
                    Rail(.045f, .23f, sign * .33f, accent * light, .065f);
                    p.Line(batch, At(.22f, sign * .33f), At(.36f, sign * .19f), ColorPalette.Ink, 3.1f);
                    p.Line(batch, At(.22f, sign * .33f), At(.36f, sign * .19f), accent, 1.25f);
                    p.Circle(batch, At(-.30f, sign * .33f), MathF.Max(.7f, radius * .045f), ColorPalette.Paper * .7f);
                }
                break;
        }
    }

    public static void DrawTransfer(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        EnemySignalRole role, float progress, float time, bool reducedEffects)
    {
        var delta = end - start;
        var length = delta.Length();
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (length < 2 || fade <= 0 || role == EnemySignalRole.None) return;
        var forward = delta / length;
        var side = new Vector2(-forward.Y, forward.X);
        var accent = EnemySignalGlyphRenderer.Accent(role);
        var packetLength = Math.Clamp(length * .055f, 3, 10);
        var count = length < 30 ? 1 : 2;
        for (var packet = 0; packet < count; packet++)
        {
            var travel = reducedEffects ? .50f + packet * .23f : (1 - fade) * 1.2f - packet * .15f;
            if (travel < 0 || travel > 1) continue;
            var position = Vector2.Lerp(start, end, travel);
            var strength = fade * (reducedEffects ? .7f : MathF.Sin(travel * MathHelper.Pi) * .75f);
            p.Line(batch, position - forward * packetLength, position, accent * strength, 1.5f);
            p.Line(batch, position - forward * packetLength * .45f, position, ColorPalette.Paper * (strength * .65f), .75f);
            if (role == EnemySignalRole.Bulwark)
                p.Line(batch, position - side * 1.7f, position + side * 1.7f, accent * strength, 1);
            if (!reducedEffects) p.Glow(batch, position, 7, accent, strength * .4f);
        }
    }

    public static void DrawEmitterPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None || progress <= 0) return;
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var accent = EnemySignalGlyphRenderer.Accent(enemy.SignalRole);
        var fade = MathHelper.Clamp(progress, 0, 1);
        Vector2 At(float x, float y) => position + (forward * x + side * y) * radius;
        if (enemy.SignalRole == EnemySignalRole.Bulwark)
        {
            DrawShieldPlates(batch, p, enemy, position, radius, heading, fade, true);
            return;
        }
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var at = enemy.SignalRole switch
            {
                EnemySignalRole.Accelerator => At(-.57f, sign * .34f),
                EnemySignalRole.Jammer => At(-.63f, sign * .47f),
                EnemySignalRole.Disruptor => At(.23f, sign * .33f),
                _ => At(-.03f, sign * .34f)
            };
            if (!reducedEffects) p.Glow(batch, at, radius * .42f, accent, fade * .65f);
            p.Line(batch, at - forward * radius * .12f, at + forward * radius * .08f,
                accent * fade, 2.1f);
            p.Circle(batch, at, MathF.Max(.8f, radius * .04f), ColorPalette.Paper * (fade * .8f));
            if (enemy.SignalRole == EnemySignalRole.Restorer)
                p.Line(batch, at - side * radius * .08f, at + side * radius * .08f,
                    accent * fade, 1.3f);
        }
    }

    private static void DrawShieldPlates(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float light, bool pulse)
    {
        var runner = enemy.Definition.Id.Contains("runner", StringComparison.Ordinal);
        var heavy = enemy.IsBoss || enemy.Definition.Id.Contains("brute", StringComparison.Ordinal) ||
            enemy.Definition.Id.Contains("aegis", StringComparison.Ordinal);
        var sides = runner ? 3 : heavy ? 6 : 4;
        var hullRadius = radius * (runner ? 1 : .85f);
        var front = position + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * hullRadius;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var angle = heading + sign * MathHelper.TwoPi / sides;
            var flank = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * hullRadius;
            var start = Vector2.Lerp(front, flank, .20f);
            var end = Vector2.Lerp(front, flank, .71f);
            if (!pulse)
            {
                p.Line(batch, start, end, ColorPalette.Ink, MathF.Max(3.6f, radius * .20f));
                p.Line(batch, start, end, ColorPalette.Muted, MathF.Max(2.4f, radius * .135f));
            }
            p.Line(batch, start, end, ColorPalette.Shield * light, pulse ? 2 : 1.1f);
            if (pulse)
                p.Line(batch, Vector2.Lerp(start, end, .2f), Vector2.Lerp(start, end, .55f),
                    ColorPalette.Paper * (light * .6f), 1);
        }
    }
}
