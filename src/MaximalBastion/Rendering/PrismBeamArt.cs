using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A straight lavender optical core inside a violet sheath with a rounded muzzle glow.</summary>
public static class PrismBeamArt
{
    public const float BarrelLength = 1.1f;

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float radius, float progress, bool reducedEffects, bool emitter = true)
    {
        var fade = MathF.Pow(MathHelper.Clamp(progress, 0, 1), .70f);
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
        if (!reducedEffects)
        {
            Sheath(width + 14, accent * (.08f * fade));
            Sheath(width + 8, accent * (.22f * fade));
        }
        p.Line(batch, start, end, accent * fade, 5.5f);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .26f) * fade, 3.4f);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .50f) * fade, 2.0f);
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
