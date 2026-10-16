using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A tiered ion charge with a compact, non-flaming containment wake.</summary>
public static class MortarShellArt
{
    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 position, Vector2 direction,
        float radius, Color accent, float time, bool reducedEffects, int level = 1, bool apex = false)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var angle = MathF.Atan2(forward.Y, forward.X);
        var size = Math.Clamp(radius / 7f, .7f, 1.25f);
        var growth = 1 + (stage - 1) * .12f;
        var phase = reducedEffects ? .4f : time;
        var pulse = 1 + MathF.Sin(phase * 6.8f) * .04f;
        var light = Color.Lerp(accent, ColorPalette.Paper, .55f);
        Vector2 At(float x, float y) => position + (forward * x + side * y) * size;

        if (!reducedEffects)
        {
            p.Glow(batch, position, 23 * size * growth * pulse, accent, .88f);
            p.Glow(batch, At(-10, 0), 11 * size, accent, .35f);
            for (var packet = 0; packet < stage; packet++)
            {
                var age = (phase * 1.4f + packet / (float)stage) % 1;
                var envelope = MathF.Sin(age * MathHelper.Pi);
                var drift = stage >= 3 ? MathF.Sin(phase * 5.2f - age * 3) * age * 1.4f : 0;
                p.Glow(batch, At(-4 - age * (15 + stage * 2), drift), (4.2f - age * 1.7f) * size,
                    light, envelope * .4f);
            }
        }

        if (stage == 1)
            p.Circle(batch, position, 4.4f * size, accent);
        else if (stage == 2)
        {
            p.DrawPolygon(batch, position, 6 * size, 6, false, accent, angle);
            p.Circle(batch, position, 4.2f * size, light);
        }
        else
        {
            if (stage == 4)
                p.DrawPolygon(batch, position, 6.1f * size * growth, 6, false, accent, angle);
            else
                p.DrawDiamond(batch, position, 6.4f * size * growth, accent, angle);
            p.Circle(batch, position, 4.8f * size * growth, accent);
            // Connected lateral fields compress a single charge.
            for (var sign = -1; sign <= 1; sign += 2)
            {
                p.Line(batch, At(-4.3f, sign * 2.4f), At(.5f, sign * 4.4f), light, 1.1f * size);
                if (stage == 4)
                    p.Line(batch, At(.5f, sign * 4.4f), At(4.3f, sign * 1.6f), light, 1.1f * size);
            }
            if (stage == 4)
                p.DrawPolygon(batch, position, 4.5f * size, 6, false, light, angle);
        }
        p.Circle(batch, At(.65f, 0), (2.6f + stage * .25f) * size * pulse, light);
        p.Circle(batch, At(1, 0), (1.6f + stage * .18f) * size, ColorPalette.Paper);
        if (!reducedEffects)
        {
            p.Glow(batch, position, 12 * size * growth, light, .6f);
            p.Glow(batch, At(.8f, 0), 7 * size, ColorPalette.Paper, .78f);
        }
    }
}
