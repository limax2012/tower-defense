using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

internal static class BastionBrandMark
{
    internal static void Draw(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float time)
    {
        var pulse = .5f + .5f * MathF.Sin(time * 1.1f);
        var ceramic = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, .56f);
        primitives.Glow(batch, center, 62, ColorPalette.Violet, .12f + pulse * .025f);
        Polygon(0, 8, 29, 6, ColorPalette.Ink);
        Polygon(0, 5, 29, 6, ColorPalette.Metal);
        Polygon(0, 5, 24, 6, ColorPalette.Navy);

        foreach (var side in new[] { -1, 1 })
        {
            Rail(side * 23, -7, side * 23, 16, ceramic, 3.2f);
            Rail(side * 21, 20, side * 5, 29, ColorPalette.Muted, 2.3f);
            Rail(side * 24, 0, side * 24, 9, ColorPalette.Cyan, 2);
            Rail(side * 15.5f, -15.5f, side * 15.5f, 8.5f, ceramic, 5);
            Rail(side * 15.5f, 8.5f, side * 9.5f, 14.5f, ceramic, 7);
            Rail(side * 16, -11, side * 16, 3, ColorPalette.Paper, 1.4f);
        }

        Rail(0, -31, 0, -6, ColorPalette.Metal, 8);
        Rail(0, -31, 0, -6, ColorPalette.Violet, 4);
        Polygon(0, 3, 16, 4, ColorPalette.Metal);
        primitives.Glow(batch, center + new Vector2(0, 1), 22, ColorPalette.Violet, .2f);
        Polygon(0, 1, 13, 4, ColorPalette.Violet);
        Polygon(0, 1, 6.5f, 4, ColorPalette.Paper);
        Polygon(0, 1, 2.5f, 4, ColorPalette.Violet);

        void Polygon(float x, float y, float radius, int sides, Color color) =>
            primitives.DrawPolygon(batch, center + new Vector2(x, y), radius, sides, false,
                color, -MathF.PI / 2);

        void Rail(float x1, float y1, float x2, float y2, Color color, float width) =>
            primitives.Line(batch, center + new Vector2(x1, y1), center + new Vector2(x2, y2), color, width);
    }
}
