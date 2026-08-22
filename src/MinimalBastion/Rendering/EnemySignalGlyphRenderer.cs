using MinimalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Rendering;

public static class EnemySignalGlyphRenderer
{
    public static Color Accent(EnemySignalRole role) => role switch
    {
        EnemySignalRole.Accelerator => ColorPalette.Cyan,
        EnemySignalRole.Restorer => ColorPalette.Green,
        EnemySignalRole.Bulwark => ColorPalette.Shield,
        EnemySignalRole.Jammer => ColorPalette.Orange,
        EnemySignalRole.Disruptor => ColorPalette.Violet,
        _ => ColorPalette.Muted
    };

    public static void DrawCarrierIcon(SpriteBatch batch, PrimitiveRenderer primitives, EnemySignalRole role,
        Vector2 center, float radius)
    {
        var accent = Accent(role);
        primitives.Circle(batch, center, radius, ColorPalette.Path);
        primitives.Ring(batch, center, radius, accent, 3);
        DrawEmbedded(batch, primitives, role, center, MathF.Max(4.5f, radius * 0.46f));
    }

    public static void DrawEmbedded(SpriteBatch batch, PrimitiveRenderer primitives, EnemySignalRole role,
        Vector2 center, float radius)
    {
        if (role == EnemySignalRole.None) return;
        var accent = Accent(role);
        primitives.Circle(batch, center, radius, ColorPalette.WithAlpha(ColorPalette.Navy, 235));
        primitives.Ring(batch, center, radius, accent, 1);
        DrawGlyph(batch, primitives, role, center, MathF.Max(2.5f, radius * 0.58f), accent);
    }

    private static void DrawGlyph(SpriteBatch batch, PrimitiveRenderer primitives, EnemySignalRole role,
        Vector2 center, float size, Color color)
    {
        var stroke = MathF.Max(1.5f, size * 0.42f);
        switch (role)
        {
            case EnemySignalRole.Accelerator:
                DrawChevron(batch, primitives, center - new Vector2(0, size * 0.34f), size * 0.72f, color, stroke);
                DrawChevron(batch, primitives, center + new Vector2(0, size * 0.34f), size * 0.72f, color, stroke);
                break;
            case EnemySignalRole.Restorer:
                primitives.Line(batch, center - new Vector2(size, 0), center + new Vector2(size, 0), color, stroke);
                primitives.Line(batch, center - new Vector2(0, size), center + new Vector2(0, size), color, stroke);
                break;
            case EnemySignalRole.Bulwark:
                primitives.DrawPolygon(batch, center, size * 1.12f, 4, false, color, MathHelper.PiOver4);
                primitives.DrawPolygon(batch, center, size * 0.48f, 4, false, ColorPalette.Navy, MathHelper.PiOver4);
                break;
            case EnemySignalRole.Jammer:
                primitives.Line(batch, center - new Vector2(size * 0.8f), center + new Vector2(size * 0.8f), color, stroke);
                primitives.Line(batch, center + new Vector2(size * 0.8f, -size * 0.8f),
                    center + new Vector2(-size * 0.8f, size * 0.8f), color, stroke);
                break;
            case EnemySignalRole.Disruptor:
                var half = size * 0.78f;
                primitives.Line(batch, center - new Vector2(half, 0), center - new Vector2(half * 0.28f, 0), color, stroke);
                primitives.Line(batch, center + new Vector2(half * 0.28f, 0), center + new Vector2(half, 0), color, stroke);
                primitives.Line(batch, center - new Vector2(0, half), center - new Vector2(0, half * 0.28f), color, stroke);
                primitives.Line(batch, center + new Vector2(0, half * 0.28f), center + new Vector2(0, half), color, stroke);
                primitives.DrawPolygon(batch, center, size * 0.36f, 4, false, color, MathHelper.PiOver4);
                break;
        }
    }

    private static void DrawChevron(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float size,
        Color color, float stroke)
    {
        primitives.Line(batch, center + new Vector2(-size, size * 0.38f), center + new Vector2(0, -size * 0.38f),
            color, stroke);
        primitives.Line(batch, center + new Vector2(0, -size * 0.38f), center + new Vector2(size, size * 0.38f),
            color, stroke);
    }
}
