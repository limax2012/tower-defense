using MaximalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Embedded power relays and node-colored placement couplers.</summary>
public static class PowerNodeArt
{
    public static void DrawDevice(SpriteBatch b, PrimitiveRenderer p, Vector2 position,
        Color color, float scale = 1, bool glow = true)
    {
        Vector2 At(float x, float y) => position + new Vector2(x, y) * scale;
        void Rail(float x1, float y1, float x2, float y2, Color tint, float width) =>
            p.Line(b, At(x1, y1), At(x2, y2), tint, width * scale);
        if (glow) p.Glow(b, position, 24 * scale, color, .22f);
        Rail(-11, 2, 11, 2, ColorPalette.Navy, 18);
        Rail(-10, 0, 10, 0, ColorPalette.Metal, 16);
        Rail(-8, 0, 8, 0, ColorPalette.Navy, 12);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            Rail(sign * 11, -4, sign * 11, 4, ColorPalette.Muted, 2);
            Rail(-5, sign * 8, 5, sign * 8, ColorPalette.CardOutline, 2);
            for (var row = -1; row <= 1; row += 2)
                p.Circle(b, At(sign * 7, row * 5), .9f * scale, ColorPalette.Muted);
        }
        for (var i = -1; i <= 1; i++)
        {
            Rail(i * 4, -4, i * 4, 4, ColorPalette.Metal, 3);
            Rail(i * 4, -3, i * 4, 3, color, 1.5f);
        }
        Rail(-2, 0, 2, 0, ColorPalette.Paper, 1);
    }

    public static void DrawCouplers(SpriteBatch b, PrimitiveRenderer p, Vector2 position,
        IReadOnlyList<PowerNodeData> nodes)
    {
        var count = Math.Min(3, nodes.Count);
        for (var index = 0; index < count; index++)
        {
            var at = position + new Vector2((index - (count - 1) * .5f) * 9, 20);
            p.Line(b, at - new Vector2(4, 0), at + new Vector2(4, 0), ColorPalette.Navy, 6);
            p.Line(b, at - new Vector2(3, 0), at + new Vector2(3, 0), ColorPalette.Metal, 4);
            p.Line(b, at - new Vector2(2, 0), at + new Vector2(2, 0), nodes[index].NodeColor, 2);
        }
    }
}
