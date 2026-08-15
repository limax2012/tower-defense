using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Rendering;

public static class StatusGlyphRenderer
{
    public static void DrawArmorBreak(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius)
    {
        var offset = radius + 7;
        const float halfHeight = 5;
        const float tooth = 4;
        var left = center - new Vector2(offset, 0);
        var right = center + new Vector2(offset, 0);
        primitives.Line(batch, left + new Vector2(tooth, -halfHeight), left, ColorPalette.Gold, 2.5f);
        primitives.Line(batch, left, left + new Vector2(tooth, halfHeight), ColorPalette.Gold, 2.5f);
        primitives.Line(batch, right - new Vector2(tooth, halfHeight), right, ColorPalette.Gold, 2.5f);
        primitives.Line(batch, right, right - new Vector2(tooth, -halfHeight), ColorPalette.Gold, 2.5f);
    }

    public static void DrawStun(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius, float pulse)
    {
        var offset = radius + 7;
        var size = 3.2f + MathHelper.Clamp(pulse, 0, 1) * 0.8f;
        primitives.DrawPolygon(batch, center + new Vector2(offset * 0.72f, -offset * 0.72f), size, 4, false,
            ColorPalette.Green, MathHelper.PiOver4);
        primitives.DrawPolygon(batch, center + new Vector2(-offset * 0.72f, offset * 0.72f), size, 4, false,
            ColorPalette.Green, MathHelper.PiOver4);
    }
}
