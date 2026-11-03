using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed partial class PrimitiveRenderer
{
    private const int SupportMaskRadius = 256;
    private const int SupportMaskSize = SupportMaskRadius * 2 + 2;
    private readonly Texture2D _supportMasks;

    /// <summary>A soft circular or segmented angular wave whose outer edge stays within its radius.</summary>
    public void SupportWave(SpriteBatch batch, Vector2 center, float radius, Color color, bool angular)
    {
        DrawSupportMask(batch, center, radius, color, angular ? 1 : 0);
    }

    private void DrawSupportMask(SpriteBatch batch, Vector2 center, float radius, Color color, int mask)
    {
        if (radius <= 0 || color.A == 0) return;
        batch.Draw(_supportMasks, center, new Rectangle(mask * SupportMaskSize, 0, SupportMaskSize, SupportMaskSize),
            color, 0, new Vector2(SupportMaskSize * .5f), radius / SupportMaskRadius, SpriteEffects.None, 0);
    }

    private static Texture2D CreateSupportMasks(GraphicsDevice device)
    {
        var width = SupportMaskSize * 2;
        var pixels = new Color[width * SupportMaskSize];
        for (var mask = 0; mask < 2; mask++)
        for (var y = 0; y < SupportMaskSize; y++)
        for (var x = 0; x < SupportMaskSize; x++)
            pixels[y * width + mask * SupportMaskSize + x] = SampleSupportMask(x, y, mask);
        var texture = new Texture2D(device, width, SupportMaskSize);
        texture.SetData(pixels);
        return texture;
    }

    internal static Color SampleSupportMask(int x, int y, int mask)
    {
        var point = (new Vector2(x + .5f, y + .5f) - new Vector2(SupportMaskSize * .5f)) / SupportMaskRadius;
        var radius = point.Length();
        if (radius > 1 || radius < .76f) return Color.Transparent;
        var distance = 1 - radius;
        float intensity;
        if (mask == 0)
        {
            var edge = Bell(distance - .007f, .006f);
            var wake = .10f * Bell(distance - .022f, .022f);
            intensity = (edge + wake) * MathHelper.Clamp(distance * 512, 0, 1);
        }
        else
        {
            const float halfSegment = MathHelper.Pi / 12;
            var angle = MathF.Atan2(point.Y, point.X);
            var local = angle - MathF.Round(angle / (halfSegment * 2)) * halfSegment * 2;
            distance = MathF.Cos(halfSegment) - radius * MathF.Cos(local);
            if (distance < 0) return Color.Transparent;
            var gap = MathHelper.Clamp((.92f - MathF.Abs(local) / halfSegment) / .10f, 0, 1);
            var edge = Bell(distance - .007f, .0055f);
            var echo = .24f * Bell(distance - .035f, .004f);
            var wake = .08f * Bell(distance - .015f, .016f);
            intensity = (edge + echo + wake) * gap * MathHelper.Clamp(distance * 512, 0, 1);
        }
        return Color.White * MathHelper.Clamp(intensity, 0, 1);
    }

    private static float Bell(float distance, float width)
    {
        var normalized = distance / width;
        return MathF.Exp(-normalized * normalized);
    }
}
