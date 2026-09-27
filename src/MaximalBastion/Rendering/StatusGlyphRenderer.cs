using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public static class StatusGlyphRenderer
{
    public static void DrawDisrupted(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius)
    {
        var halfHeight = radius * .42f;
        for (var side = -1; side <= 1; side += 2)
        {
            var offset = new Vector2(side * 5.5f, 0);
            DrawOutlinedBar(batch, primitives, center + offset - new Vector2(0, halfHeight),
                center + offset + new Vector2(0, halfHeight), ColorPalette.Violet);
        }
    }

    public static void DrawSuppressed(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius)
    {
        var halfWidth = radius * .34f;
        for (var side = -1; side <= 1; side += 2)
            DrawOutlinedBar(batch, primitives, center + new Vector2(side * halfWidth, 5),
                center + new Vector2(side * 2, 1), ColorPalette.Orange);
    }

    private static void DrawOutlinedBar(SpriteBatch batch, PrimitiveRenderer primitives,
        Vector2 start, Vector2 end, Color color)
    {
        var direction = Vector2.Normalize(end - start);
        primitives.Line(batch, start - direction, end + direction, ColorPalette.Ink, 5);
        primitives.Line(batch, start, end, color, 3);
    }

    public static void DrawArmorBreak(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius)
    {
        var offset = radius + 7;
        const float halfHeight = 5;
        const float tooth = 4;
        var left = center - new Vector2(offset, 0);
        var right = center + new Vector2(offset, 0);
        primitives.Line(batch, left + new Vector2(tooth, -halfHeight), left, ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, left, left + new Vector2(tooth, halfHeight), ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, right - new Vector2(tooth, halfHeight), right, ColorPalette.ArmorBreak, 2.5f);
        primitives.Line(batch, right, right - new Vector2(tooth, -halfHeight), ColorPalette.ArmorBreak, 2.5f);
    }
}
