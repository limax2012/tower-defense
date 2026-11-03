using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Reactor signal identity, support fields, and hostile transmissions.</summary>
public static class EnemySignalArt
{
    public static float SupportRangeRadius(GameSession session, EnemyInstance enemy) =>
        session.CounterSupportEnabled && !enemy.IsDead && !enemy.HasEscaped &&
        enemy.SignalRole is EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark
            ? session.Challenge.CounterSupportRadius : 0;

    public static void DrawSupportRange(SpriteBatch batch, PrimitiveRenderer p, GameSession session,
        EnemyInstance enemy, Vector2 position, bool reducedEffects, float time = 0)
    {
        var radius = SupportRangeRadius(session, enemy);
        if (radius <= 0) return;
        // The continuous edge marks eligibility; repair and shield pulses still select nearby recipients.
        p.Ring(batch, position, radius, EnemySignalGlyphRenderer.Accent(enemy.SignalRole) * .50f, 1);
        if (enemy.SignalRole != EnemySignalRole.Accelerator || reducedEffects) return;
        var phase = time * .46f + enemy.Id * .381966f;
        phase -= MathF.Floor(phase);
        var rippleRadius = MathHelper.Lerp(MathF.Min(19, radius * .19f), radius, phase);
        var strength = MathF.Sin(phase * MathHelper.Pi) * .36f;
        p.SupportWave(batch, position, rippleRadius, ColorPalette.Acceleration * strength, angular: false);
    }

    public static void DrawSupportWave(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float rangeRadius, float progress, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || rangeRadius <= 0 || progress <= 0 ||
            enemy.SignalRole is not (EnemySignalRole.Restorer or EnemySignalRole.Bulwark)) return;
        var remaining = MathHelper.Clamp(progress, 0, 1);
        var accent = EnemySignalGlyphRenderer.Accent(enemy.SignalRole);
        if (reducedEffects)
        {
            p.Ring(batch, position, rangeRadius, accent * (remaining * .32f), 1);
            return;
        }
        var age = 1 - remaining;
        var radius = MathHelper.Lerp(MathF.Min(16, rangeRadius * .16f), rangeRadius,
            1 - MathF.Pow(remaining, 1.35f));
        p.SupportWave(batch, position, radius, accent * (MathF.Pow(remaining, .65f) * .82f),
            angular: enemy.SignalRole == EnemySignalRole.Bulwark);
        var boundaryCatch = MathF.Max(0, 1 - MathF.Abs(age - .83f) / .17f);
        if (boundaryCatch > 0)
            p.Ring(batch, position, rangeRadius, accent * (boundaryCatch * .55f), 1);
    }

    public static Vector2 MountedPosition(EnemyInstance enemy, Vector2 position, float radius, float heading) => position;

    public static Rectangle IdentityBounds(EnemyInstance enemy, Vector2 position, float radius, float heading)
    {
        var center = MountedPosition(enemy, position, radius, heading);
        var extent = MathF.Ceiling(EnemyModelArt.CoreRadius(enemy) * 1.12f + 2);
        return new Rectangle((int)MathF.Floor(center.X - extent), (int)MathF.Floor(center.Y - extent),
            (int)extent * 2 + 1, (int)extent * 2 + 1);
    }

    public static void DrawIdentity(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float time, bool reducedEffects, float heading = 0)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None) return;
        EnemyModelArt.DrawSignalCore(batch, p, enemy, position, heading, time, reducedEffects);
    }

    public static void DrawTransfer(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        EnemySignalRole role, float progress, float time, bool reducedEffects)
    {
        var delta = end - start;
        var length = delta.Length();
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (length < 2 || fade <= 0 || role == EnemySignalRole.None) return;
        if (role is EnemySignalRole.Jammer or EnemySignalRole.Disruptor)
            DrawHostileTransfer(batch, p, start, end, role, fade, reducedEffects);
    }

    private static void DrawHostileTransfer(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        EnemySignalRole role, float remaining, bool reducedEffects)
    {
        var delta = end - start;
        var length = delta.Length();
        var forward = delta / length;
        var side = new Vector2(-forward.Y, forward.X);
        var accent = EnemySignalGlyphRenderer.Accent(role);
        var hot = Color.Lerp(accent, ColorPalette.Paper, .58f);
        var age = 1 - remaining;
        var envelope = MathF.Min(1, remaining * 3.5f);
        var spread = role == EnemySignalRole.Jammer ? 3.6f : 4.0f;
        var railAlpha = envelope * (reducedEffects ? .32f : .42f);
        // Paired rails and transverse gates read as an outbound transmission, not weapon tracers.
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var previous = start;
            const int segments = 10;
            for (var segment = 1; segment <= segments; segment++)
            {
                var t = segment / (float)segments;
                var width = MathF.Sin(t * MathHelper.Pi) * spread;
                if (role == EnemySignalRole.Disruptor) width *= segment % 2 == 0 ? .65f : 1.35f;
                var at = Vector2.Lerp(start, end, t) + side * (sign * width);
                p.Line(batch, previous, at, ColorPalette.Ink * (envelope * .72f), 3.1f);
                p.Line(batch, previous, at, accent * railAlpha, 1.35f);
                previous = at;
            }
        }
        var count = reducedEffects ? 2 : 3;
        for (var packet = 0; packet < count; packet++)
        {
            var t = reducedEffects ? .32f + packet * .36f : age * 1.85f - packet * .24f;
            if (t < 0 || t > 1) continue;
            var at = Vector2.Lerp(start, end, t);
            var fade = envelope * MathF.Min(1, MathF.Min(t, 1 - t) * 12 + .25f);
            if (!reducedEffects) p.Glow(batch, at, 11, accent, fade * .25f);
            if (role == EnemySignalRole.Jammer)
            {
                var half = 4 + MathF.Sin(t * MathHelper.Pi) * 3;
                for (var echo = 0; echo < 2; echo++)
                {
                    var tip = at - forward * echo * 4.5f;
                    var wing = half - echo;
                    p.Line(batch, tip - forward * 3 - side * wing, tip, ColorPalette.Ink * fade, 3.2f);
                    p.Line(batch, tip, tip - forward * 3 + side * wing, ColorPalette.Ink * fade, 3.2f);
                    p.Line(batch, tip - forward * 3 - side * wing, tip, accent * fade, 1.8f);
                    p.Line(batch, tip, tip - forward * 3 + side * wing, accent * fade, 1.8f);
                }
                p.Line(batch, at - side * 2.5f, at + side * 2.5f, hot * fade, 1.1f);
            }
            else
            {
                var nose = at + forward * 6.5f;
                var tail = at - forward * 6.5f;
                var left = at - side * 5.2f;
                var right = at + side * 5.2f;
                Edge(nose, left);
                Edge(left, tail);
                Edge(tail, right);
                Edge(right, nose);
                void Edge(Vector2 first, Vector2 last)
                {
                    p.Line(batch, first, last, ColorPalette.Ink * fade, 3.5f);
                    p.Line(batch, first, last, accent * fade, 1.7f);
                }
                p.Line(batch, at - side * 2, at + side * 2, hot * fade, 1.2f);
            }
        }
        var terminal = envelope * .8f;
        p.Line(batch, end - side * 4, end + side * 4, accent * terminal, 1.5f);
        if (!reducedEffects) p.Glow(batch, start, 10, accent, envelope * .2f);
    }

    public static void DrawEmitterPulse(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, float progress, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None || progress <= 0) return;
        var center = MountedPosition(enemy, position, radius, heading);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var accent = EnemySignalGlyphRenderer.Accent(enemy.SignalRole);
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (enemy.SignalRole is EnemySignalRole.Restorer or EnemySignalRole.Bulwark)
        {
            if (!reducedEffects)
                p.Glow(batch, center, EnemyModelArt.CoreRadius(enemy) * 1.6f, accent, fade * fade * .42f);
            fade *= fade;
        }
        // Successful transfers energize opposing edges of the reactor socket.
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var at = center + side * (sign * EnemyModelArt.CoreRadius(enemy) * .78f);
            if (!reducedEffects) p.Glow(batch, at, 6, accent, fade * .42f);
            p.Line(batch, at - forward * 1.5f, at + forward * 1.5f,
                accent * fade, 1.4f);
        }
    }
}
