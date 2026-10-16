using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Luminous weapon signatures drawn independently of projectile simulation.</summary>
public static class ProjectileArt
{
    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, string? towerId, Vector2 position,
        Vector2 direction, float radius, Color accent, float time, bool reducedEffects, int level = 1, bool apex = false)
    {
        if (towerId == "siege_mortar")
        {
            MortarShellArt.Draw(batch, p, position, direction, radius, accent, time, reducedEffects, level, apex);
            return;
        }

        var stage = TowerTierVisuals.Stage(level, apex);
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var angle = MathF.Atan2(forward.Y, forward.X);
        var size = Math.Clamp(radius / (towerId == "shard_fan" ? 3f : 5f), .65f, 1.4f);
        var growth = 1 + (stage - 1) * .12f;
        var phase = reducedEffects ? 0 : time;
        var hot = Color.Lerp(accent, ColorPalette.Paper, .62f);
        Vector2 At(float x, float y) => position + (forward * x + side * y) * size;
        void Line(float from, float to, Color color, float width) =>
            p.Line(batch, At(from, 0), At(to, 0), color, width * size);
        void Halo(float spread, Color color, float strength)
        {
            if (reducedEffects) return;
            p.Glow(batch, position, spread * size * growth, color, strength);
            p.Glow(batch, At(-5, 0), spread * .65f * size, color, strength * .45f);
        }
        void Wake(float length, float width, Color color)
        {
            if (reducedEffects) return;
            const int segments = 6;
            for (var segment = 0; segment < segments; segment++)
            {
                var from = segment / (float)segments;
                var to = (segment + 1) / (float)segments;
                var intensity = (from + to) * .5f;
                Line(-length * (1 - from), -length * (1 - to),
                    color * (.42f * MathF.Pow(intensity, 1.5f)), width * (.12f + .88f * intensity));
            }
        }
        void Lens(float rear, float front, float width, Color color, float shoulder = .5f)
        {
            const int slices = 18;
            for (var slice = 0; slice < slices; slice++)
            {
                var t = (slice + .5f) / slices;
                var profile = t < shoulder ? t / shoulder : (1 - t) / (1 - shoulder);
                Line(MathHelper.Lerp(rear, front, slice / (float)slices),
                    MathHelper.Lerp(rear, front, (slice + 1f) / slices) + .02f,
                    color, MathF.Max(.45f, width * profile));
            }
        }

        switch (towerId)
        {
            case "needle_turret":
            {
                Halo(11, accent, .84f);
                Wake(13 + stage * 2, 1.5f + stage * .35f, accent);
                if (stage == 1)
                {
                    Line(-3.5f, 4.5f, accent, 2.3f);
                    p.Circle(batch, At(4.5f, 0), 1.15f * size, accent);
                    Line(-2.5f, 4, hot, 1);
                }
                else
                {
                    Lens(-5.2f * growth, 5.2f * growth, 3.6f * growth, accent, .40f);
                    if (stage >= 3)
                        for (var sign = -1; sign <= 1; sign += 2)
                            p.Line(batch, At(-5.7f * growth, sign * 2.1f), At(1, sign * 1.2f),
                                accent, (stage == 4 ? 1.3f : .9f) * size);
                    if (stage == 4)
                        p.Line(batch, At(-4.4f, -3.3f), At(-4.4f, 3.3f), hot, 1.1f * size);
                    Line(-3.7f * growth, 4.7f * growth, ColorPalette.Paper, 1 + stage * .15f);
                }
                break;
            }

            case "watchtower":
            {
                Halo(17, accent, .9f);
                Wake(27 + stage * 3, 1.5f + stage * .5f, accent);
                var halfLength = 10 * growth;
                Lens(-halfLength, halfLength, (stage == 1 ? 2.8f : 3.7f) * growth, accent, .35f);
                Lens(-halfLength * .85f, halfLength * .96f, 1.6f + stage * .4f, ColorPalette.Paper, .35f);
                if (stage >= 2)
                    p.Line(batch, At(-3.5f, -2.3f * growth), At(-3.5f, 2.3f * growth), hot, .8f * size);
                if (stage >= 3)
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        p.Line(batch, At(-9, sign * 3.1f), At(-3.4f, 0), accent, 1.2f * size);
                        if (stage == 4)
                            p.Line(batch, At(-12, sign * 2.2f), At(-7, sign * .9f), ColorPalette.Paper, .8f * size);
                    }
                break;
            }

            case "shard_fan":
            {
                Halo(12, accent, .9f);
                Wake(9 + stage * 1.8f, 3 * growth, accent);
                if (stage == 1)
                    p.Circle(batch, position, 2.8f * size, accent);
                else if (stage == 2)
                {
                    p.DrawDiamond(batch, position, 3.8f * size, accent, angle);
                    p.Circle(batch, position, 2.8f * size, accent);
                }
                else
                {
                    Lens(-5.5f * growth, 3.6f * growth, 6.2f * growth, accent, .68f);
                    if (stage == 4)
                        p.DrawPolygon(batch, At(.2f, 0), 4.5f * size, 4, true, hot, angle);
                }
                p.Circle(batch, At(.45f, 0), (1.35f + stage * .2f) * size, ColorPalette.Paper);
                break;
            }

            case "frost_spire":
            {
                Halo(17, accent, .8f);
                Wake(11 + stage * 2, 2.5f * growth, accent);
                if (stage == 1)
                    p.DrawDiamond(batch, position, 4.4f * size, accent, angle);
                else
                {
                    Lens(-6.1f * growth, 6.1f * growth, 7 * growth, accent);
                    if (stage >= 3)
                    {
                        if (stage == 4)
                            p.DrawPolygon(batch, position, 6 * size * growth, 6, true, accent, angle);
                        else
                            p.DrawDiamond(batch, position, 5.4f * size * growth, accent, angle);
                        for (var sign = -1; sign <= 1; sign += 2)
                            p.Line(batch, position, At(-2.5f, sign * (stage == 4 ? 5 : 4.2f) * growth), hot, 1 * size);
                    }
                    if (stage == 4)
                        p.DrawPolygon(batch, position, 4.7f * size, 6, false, hot, angle);
                }
                p.DrawDiamond(batch, position, (2.5f + stage * .25f) * size, ColorPalette.Paper, angle);
                if (stage >= 2)
                    p.Line(batch, At(-4.4f * growth, 0), At(4.4f * growth, 0), ColorPalette.Paper, .8f * size);
                break;
            }

            case "ember_coil":
            {
                var pulse = 1 + MathF.Sin(phase * 12) * .055f;
                Halo(20, ColorPalette.EmberShot, .95f);
                Wake(16 + stage * 3, 5.2f * growth, ColorPalette.EmberShot);
                Lens(-10 * growth, 3.6f * growth, 7.5f * growth, ColorPalette.EmberShot, .76f);
                if (stage >= 2)
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        var lick = MathF.Sin(phase * 10 + sign * 1.7f) * .65f;
                        p.Line(batch, At(-8.5f * growth, sign * (2.2f + lick)), At(-1.5f, sign * 1.7f),
                            ColorPalette.EmberFlame, (1.3f + stage * .15f) * size);
                    }
                p.Circle(batch, position, 3.7f * size * growth * pulse, ColorPalette.EmberShot);
                p.Circle(batch, At(.6f, 0), 2.6f * size * growth, ColorPalette.EmberFlame);
                if (stage >= 3)
                    Lens(-5.7f, 1, 3.1f * growth, ColorPalette.EmberCore, .75f);
                p.Circle(batch, At(1, 0), (1.3f + stage * .19f) * size, ColorPalette.EmberCore);
                if (stage == 4)
                {
                    p.Circle(batch, At(1.4f, 0), 1.25f * size, ColorPalette.Paper);
                    if (!reducedEffects)
                        for (var ember = 0; ember < 2; ember++)
                        {
                            var age = (phase * 2.6f + ember * .5f) % 1;
                            p.Circle(batch, At(-8 - age * 15, MathF.Sin(ember * 2.7f + age * 2) * age * 3),
                                .8f * size, ColorPalette.EmberShot * ((1 - age) * .65f));
                        }
                }
                break;
            }

            case "breaker_cannon":
            {
                Halo(18, accent, .95f);
                Wake(12 + stage * 2, 5.5f * growth, accent);
                var rear = -4.3f * growth;
                var front = 5.3f * growth;
                var shoulder = stage switch { 1 => 0f, 2 => .14f, 3 => .28f, _ => .36f };
                // The shoulder and pointed nose form one connected pressure wedge.
                for (var slice = 0; slice < 24; slice++)
                {
                    var t = (slice + .5f) / 24;
                    var profile = t < shoulder ? .72f + .28f * t / shoulder : (1 - t) / (1 - shoulder);
                    Line(MathHelper.Lerp(rear, front, slice / 24f),
                        MathHelper.Lerp(rear, front, (slice + 1) / 24f) + .02f,
                        accent, 8.2f * growth * profile);
                }
                Lens(rear * .70f, front * .80f, 3.5f * growth, ColorPalette.Paper, .12f + shoulder);
                if (stage >= 3)
                    p.Line(batch, At(rear + 1.2f, -2.9f * growth), At(rear + 1.2f, 2.9f * growth),
                        hot, (stage == 4 ? 1.25f : .85f) * size);
                break;
            }

            default:
                Halo(15, accent, .8f);
                Wake(16, 4, accent);
                p.Circle(batch, position, MathF.Max(2, radius) * growth, accent);
                p.Circle(batch, position, MathF.Max(1, radius * .45f), ColorPalette.Paper);
                break;
        }
    }
}
