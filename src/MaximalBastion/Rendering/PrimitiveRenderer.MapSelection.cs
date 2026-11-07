using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed partial class PrimitiveRenderer
{
    private readonly Texture2D _mapSelectionCheck;

    internal void MapSelectionCheck(SpriteBatch batch, Rectangle badge, Color accent) =>
        batch.Draw(_mapSelectionCheck, badge, accent);

    private static Texture2D CreateMapSelectionCheck(GraphicsDevice device, int rasterQuality)
    {
        const float radius = 1f;
        var start = new Vector2(5, 11);
        var corner = new Vector2(9, 15);
        var end = new Vector2(17, 6);
        // Keep the rounded tips inside the ends of the two-unit stroke.
        start += Vector2.Normalize(corner - start) * radius;
        end += Vector2.Normalize(corner - end) * radius;

        var size = 22 * rasterQuality;
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var point = new Vector2(x + .5f, y + .5f) / rasterQuality;
            var distance = MathF.Min(SegmentDistance(point, start, corner), SegmentDistance(point, corner, end));
            var coverage = MathHelper.Clamp(.5f + (radius - distance) * rasterQuality, 0, 1);
            pixels[y * size + x] = Color.White * coverage;
        }
        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;

        static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var axis = b - a;
            var t = MathHelper.Clamp(Vector2.Dot(point - a, axis) / axis.LengthSquared(), 0, 1);
            return Vector2.Distance(point, a + t * axis);
        }
    }
}
