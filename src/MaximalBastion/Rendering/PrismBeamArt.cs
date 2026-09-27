using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A straight optical core with light packets travelling through its violet sheath.</summary>
public static class PrismBeamArt
{
    public const float BarrelLength = 1.1f;

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float radius, float progress, float age, bool reducedEffects, bool emitter = true)
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
            Sheath(width + 14, accent * (.08f * fade));
            Sheath(width + 8, accent * (.18f * fade));
        }
        p.Line(batch, start, end, accent * fade, width + 2);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .18f) * fade, 2.2f);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .38f) * fade, 1.2f);
        if (emitter)
        {
            p.Ring(batch, start, width + 3.5f, accent * (.85f * fade), 1);
            p.Circle(batch, start, 1.8f, hot * fade);
        }
        if (!reducedEffects)
        {
            p.Glow(batch, start, emitter ? 18 : 11, accent, fade * .7f);
            p.Glow(batch, end, 14, hot, fade * .75f);
        }
        var packetLength = Math.Clamp(length * .10f, 6, 26);
        for (var packet = 0; packet < 3; packet++)
        {
            var fraction = (MathHelper.Clamp(age, 0, 1) * 1.15f + packet / 3f) % 1;
            var distance = fraction * length;
            var envelope = MathF.Sin(fraction * MathHelper.Pi);
            var center = start + forward * distance;
            var strength = fade * envelope;
            if (!reducedEffects) p.Glow(batch, center, 12, accent, strength * .85f);
            const int packetSegments = 5;
            for (var segment = 0; segment < packetSegments; segment++)
            {
                var from = Math.Clamp(distance + packetLength * (segment / (float)packetSegments - .5f), 0, length);
                var to = Math.Clamp(distance + packetLength * ((segment + 1) / (float)packetSegments - .5f), 0, length);
                var taper = 1 - MathF.Abs((segment + .5f) / (packetSegments * .5f) - 1);
                p.Line(batch, start + forward * from, start + forward * to,
                    hot * strength, 2.6f + taper * 2.4f);
            }
            if (reducedEffects) continue;
            // Small refracted highlights travel with the light, leaving the aiming core straight.
            var bend = 2 + MathF.Sin(age * MathHelper.TwoPi + packet) * .6f;
            for (var side = -1; side <= 1; side += 2)
                p.Line(batch, center - forward * 4 + normal * (side * bend),
                    center + forward * 3 + normal * (side * (bend + 1.2f)),
                    accent * (strength * .55f), 1);
        }

        void Sheath(float thickness, Color color)
        {
            // A semicircular rear cap meets the full-width sheath at the aperture center.
            var capRadius = thickness * .5f;
            var slices = Math.Max(12, (int)MathF.Ceiling(capRadius * 2));
            for (var i = 0; i < slices; i++)
            {
                var from = -capRadius + capRadius * i / slices;
                var to = -capRadius + capRadius * (i + 1) / slices;
                var midpoint = (from + to) * .5f;
                var capWidth = 2 * MathF.Sqrt(MathF.Max(0, capRadius * capRadius - midpoint * midpoint));
                p.Line(batch, start + forward * from, start + forward * to, color, capWidth);
            }
            p.Line(batch, start, end, color, thickness);
        }
    }
}
