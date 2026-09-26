using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A straight optical core with light packets travelling through its violet sheath.</summary>
public static class PrismBeamArt
{
    public const float BarrelLength = 1.1f;

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float radius, float progress, float age, bool reducedEffects)
    {
        var fade = MathHelper.Clamp(progress, 0, 1);
        if (fade <= 0) return;
        var delta = end - start;
        var length = delta.Length();
        var width = MathF.Max(1, radius);
        var hot = Color.Lerp(accent, ColorPalette.Paper, .72f);
        if (length < .01f)
        {
            if (!reducedEffects) p.Glow(batch, start, width + 9, accent, fade * .7f);
            p.Circle(batch, start, width * .65f, hot * fade);
            return;
        }

        var forward = delta / length;
        var normal = new Vector2(-forward.Y, forward.X);
        if (!reducedEffects)
        {
            p.Line(batch, start, end, accent * (.08f * fade), width + 14);
            p.Line(batch, start, end, accent * (.18f * fade), width + 8);
        }
        p.Line(batch, start, end, accent * fade, width + 2);
        p.Line(batch, start, end, hot * fade, 2.2f);
        p.Line(batch, start, end, ColorPalette.Paper * fade, 1.2f);
        if (reducedEffects) return;

        p.Glow(batch, start, 11, accent, fade * .7f);
        p.Glow(batch, end, 14, hot, fade * .75f);
        var packetLength = Math.Clamp(length * .10f, 6, 26);
        for (var packet = 0; packet < 3; packet++)
        {
            var fraction = (MathHelper.Clamp(age, 0, 1) * 1.15f + packet / 3f) % 1;
            var distance = fraction * length;
            var envelope = MathF.Sin(fraction * MathHelper.Pi);
            var center = start + forward * distance;
            var strength = fade * envelope;
            p.Glow(batch, center, 12, accent, strength * .85f);
            for (var segment = 0; segment < 5; segment++)
            {
                var from = Math.Clamp(distance + packetLength * (segment / 5f - .5f), 0, length);
                var to = Math.Clamp(distance + packetLength * ((segment + 1) / 5f - .5f), 0, length);
                var taper = 1 - MathF.Abs((segment + .5f) / 2.5f - 1);
                p.Line(batch, start + forward * from, start + forward * to,
                    hot * strength, 2.6f + taper * 2.4f);
            }
            // Small refracted highlights travel with the light, leaving the aiming core straight.
            var bend = 2 + MathF.Sin(age * MathHelper.TwoPi + packet) * .6f;
            for (var side = -1; side <= 1; side += 2)
                p.Line(batch, center - forward * 4 + normal * (side * bend),
                    center + forward * 3 + normal * (side * (bend + 1.2f)),
                    accent * (strength * .55f), 1);
        }
    }
}
