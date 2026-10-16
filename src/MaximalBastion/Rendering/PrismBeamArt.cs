using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A straight lavender optical core inside a violet sheath with a rounded muzzle glow.</summary>
public static class PrismBeamArt
{
    public const float BarrelLength = 1.1f;

    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 start, Vector2 end,
        Color accent, float radius, float progress, bool reducedEffects, bool emitter = true,
        int level = 1, bool apex = false)
    {
        var fade = MathF.Pow(MathHelper.Clamp(progress, 0, 1), .70f);
        if (fade <= 0) return;
        var delta = end - start;
        var length = delta.Length();
        var stage = TowerTierVisuals.Stage(level, apex);
        var width = stage switch { 1 => 2.8f, 2 => 3.6f, 3 => 4.4f, _ => 5.4f };
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
            Sheath(width + 6 + stage * 1.2f, accent * (.06f * fade));
            Sheath(width + 3 + stage * .8f, accent * (.18f * fade));
        }
        p.Line(batch, start, end, accent * fade, width);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .24f + stage * .025f) * fade,
            width * .64f);
        p.Line(batch, start, end, Color.Lerp(accent, ColorPalette.Paper, .42f + stage * .035f) * fade,
            width * (stage == 1 ? .28f : .36f));
        if (stage >= 3)
        {
            // Fine continuous edge filaments preserve a uniform optical profile at every frame rate.
            var side = new Vector2(-forward.Y, forward.X);
            var filamentColor = Color.Lerp(accent, ColorPalette.Paper, .22f) * (fade * .55f);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var offset = side * (sign * width * .42f);
                p.Line(batch, start + offset, end + offset, filamentColor, stage == 4 ? .65f : .4f);
            }
        }
        if (!reducedEffects)
        {
            p.Glow(batch, start, emitter ? 12 + stage * 1.5f : 9, accent, fade * .65f);
            p.Glow(batch, end, 9 + stage * 1.3f, hot, fade * .7f);
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
