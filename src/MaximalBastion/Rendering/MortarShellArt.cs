using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A heavy plasma charge drawn at the projectile's unchanged flight position.</summary>
public static class MortarShellArt
{
    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, Vector2 position, Vector2 direction,
        float radius, Color accent, float time, bool reducedEffects)
    {
        var forward = direction.LengthSquared() > .0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        var size = Math.Clamp(radius / 7f, .7f, 1.25f);
        var phase = reducedEffects ? .4f : time;
        var pulse = MathF.Sin(phase * 6.8f);
        var breath = 1 + pulse * .06f;
        var circulation = phase * 5.2f;
        var coreLight = Color.Lerp(accent, ColorPalette.Paper, .55f);
        Vector2 At(float x, float y) => position + (forward * x + side * y) * size;

        if (!reducedEffects)
        {
            p.Glow(batch, At(-11, MathF.Sin(circulation) * .8f), 12 * size, accent, .38f);
            p.Glow(batch, At(-21, MathF.Sin(circulation - .7f) * 1.2f), 8 * size, accent, .18f);
            // Soft packets flow out of the core and fade at both ends of the wake.
            for (var packet = 0; packet < 3; packet++)
            {
                var age = (phase * 1.4f + packet / 3f) % 1;
                var envelope = MathF.Sin(age * MathHelper.Pi);
                var drift = MathF.Sin(circulation - age * 3) * age * 1.6f;
                p.Glow(batch, At(-4 - age * 23, drift), (5.5f - age * 2) * size,
                    coreLight, envelope * .48f);
            }
            p.Glow(batch, position, 26 * size * (1 + pulse * .09f), accent, .88f + pulse * .08f);
        }
        else
            p.Glow(batch, position, 13 * size, accent, .55f);

        // Overlapping light fields give the charge a soft volume and a gently shifting interior.
        p.Glow(batch, At(-2, 0), 15 * size * breath, accent, .9f);
        p.Glow(batch, position, 12 * size * breath, coreLight, .95f);
        p.Glow(batch, At(.8f, 0), 7.6f * size * breath, ColorPalette.Paper, .95f);
        p.Glow(batch, At(.8f + MathF.Cos(circulation) * 1.1f, MathF.Sin(circulation) * 1.1f),
            4.4f * size * breath, ColorPalette.Paper, .78f);
        p.Glow(batch, At(-1.6f - MathF.Cos(circulation) * 1.3f, -MathF.Sin(circulation) * 1.3f),
            4.8f * size, coreLight, .48f + MathF.Sin(phase * 6.8f + 1) * .08f);
    }
}
