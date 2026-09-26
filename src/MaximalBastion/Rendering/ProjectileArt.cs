using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Luminous weapon signatures drawn independently of projectile simulation.</summary>
public static class ProjectileArt
{
    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, string? towerId, Vector2 position,
        Vector2 direction, float radius, Color accent, float time, bool reducedEffects)
    {
        if (towerId == "siege_mortar")
        {
            MortarShellArt.Draw(batch, p, position, direction, radius, accent, time, reducedEffects);
            return;
        }

        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var angle = MathF.Atan2(forward.Y, forward.X);
        var size = Math.Clamp(radius / (towerId == "shard_fan" ? 3f : 5f), .65f, 1.4f);
        var phase = reducedEffects ? 0 : time;
        Vector2 At(float x, float y) => position + (forward * x + side * y) * size;
        void Line(float from, float to, Color color, float width) =>
            p.Line(batch, At(from, 0), At(to, 0), color, width * size);
        void Halo(float spread, Color color, float strength)
        {
            if (reducedEffects) return;
            p.Glow(batch, position, spread * size, color, strength);
            p.Glow(batch, At(-5, 0), spread * .7f * size, color, strength * .55f);
        }
        void Wake(float length, float width, Color color)
        {
            if (reducedEffects) return;
            const int segments = 7;
            for (var segment = 0; segment < segments; segment++)
            {
                var from = segment / (float)segments;
                var to = (segment + 1) / (float)segments;
                var intensity = (from + to) * .5f;
                Line(-length * (1 - from), -length * (1 - to),
                    color * (.48f * MathF.Pow(intensity, 1.5f)), width * (.12f + .88f * intensity));
            }
        }

        switch (towerId)
        {
            case "needle_turret":
                Halo(12, accent, .95f);
                Wake(16, 2, accent);
                Line(-4.5f, 5.5f, accent, 2.2f);
                Line(-3.5f, 5, ColorPalette.Paper, 1.1f);
                break;

            case "watchtower":
                Halo(19, accent, .95f);
                Wake(32, 3, accent);
                // A long tapered flash gives precision fire a broad, bright middle and a sharp tip.
                for (var segment = 0; segment < 14; segment++)
                {
                    var from = -14 + segment * 2;
                    var width = (1 - MathF.Abs((segment + .5f) / 7 - 1)) * 6;
                    Line(from, from + 2.05f, accent, MathF.Max(.7f, width));
                    Line(from, from + 2.05f, ColorPalette.Paper, MathF.Max(.6f, width * .6f));
                }
                break;

            case "shard_fan":
                Halo(13, accent, .95f);
                Wake(11, 4, accent);
                p.Circle(batch, position, 3.3f * size, accent);
                p.Circle(batch, At(.4f, 0), 1.8f * size, ColorPalette.Paper);
                break;

            case "frost_spire":
                Halo(19, accent, .85f);
                Wake(13, 3.5f, accent);
                p.DrawPolygon(batch, position, 5.2f * size, 4, false, accent, angle);
                p.DrawPolygon(batch, position, (3.4f + MathF.Sin(phase * 5) * .18f) * size,
                    4, false, ColorPalette.Paper, angle);
                break;

            case "ember_coil":
            {
                var pulse = 1 + MathF.Sin(phase * 12) * .07f;
                Halo(24, ColorPalette.EmberShot, 1);
                Wake(23, 7, ColorPalette.EmberShot);
                p.DrawPolygon(batch, At(-5, MathF.Sin(phase * 9) * .8f), 8 * size * pulse, 3, false,
                    ColorPalette.EmberShot * .9f, angle + MathHelper.Pi);
                p.DrawPolygon(batch, At(-2.5f, MathF.Sin(phase * 11) * .6f), 5.2f * size, 3, false,
                    ColorPalette.EmberFlame, angle + MathHelper.Pi);
                p.Circle(batch, position, 4.7f * size * pulse, ColorPalette.EmberShot);
                p.Circle(batch, At(.6f, 0), 3.2f * size, ColorPalette.EmberFlame);
                p.Circle(batch, At(1.3f, 0), 1.8f * size, ColorPalette.EmberCore);
                if (!reducedEffects)
                    for (var ember = 0; ember < 2; ember++)
                    {
                        var age = (phase * 2.6f + ember * .5f) % 1;
                        var drift = MathF.Sin(ember * 2.7f + age * 2) * age * 3;
                        p.Circle(batch, At(-7 - age * 13, drift), .85f * size,
                            ColorPalette.EmberShot * ((1 - age) * .8f));
                    }
                break;
            }

            case "breaker_cannon":
                Halo(22, accent, 1);
                Wake(15, 8, accent);
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    p.Line(batch, At(-4.5f, sign * 5), At(4, 0), accent, 4.5f * size);
                    p.Line(batch, At(-3.5f, sign * 4), At(4, 0), ColorPalette.Paper, 2 * size);
                }
                break;

            default:
                Halo(17, accent, .85f);
                Wake(16, 4, accent);
                p.Circle(batch, position, MathF.Max(2, radius), accent);
                p.Circle(batch, position, MathF.Max(1, radius * .45f), ColorPalette.Paper);
                break;
        }
    }
}
