#if BLAZORGL
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

// All masks share one texture so painter-ordered sprites can stay in the same WebGL batch.
internal sealed class BrowserPrimitiveAtlas : IDisposable
{
    internal readonly record struct Mask(Rectangle Source, float Radius);
    private sealed record Entry(int Size, float Radius, Func<int, int, Color> Sample, Action<Mask> Assign);
    private readonly Mask[] _rings = new Mask[129];
    private readonly Dictionary<(int, bool), Mask> _polygons = new();
    public Texture2D Texture { get; }
    public Mask Circle { get; private set; }
    public Mask Glow { get; private set; }
    public Rectangle Pixel { get; private set; }

    public BrowserPrimitiveAtlas(GraphicsDevice device)
    {
        var entries = new List<Entry>();
        AddRadial(256, 256, false, mask => Circle = mask);
        for (var thickness = 1; thickness <= 128; thickness++)
        {
            var index = thickness;
            // Thick masks are used by small rings; thin, large rings retain the full raster resolution.
            var radius = thickness <= 8 ? 256 : thickness <= 32 ? 128 : 64;
            AddRadial(radius, thickness * radius / 128f, false, mask => _rings[index] = mask);
        }
        AddRadial(32, 32, true, mask => Glow = mask);
        entries.Add(new Entry(4, 1, (_, _) => Color.White, mask =>
            Pixel = new Rectangle(mask.Source.X + 1, mask.Source.Y + 1, 1, 1)));
        for (var sides = 3; sides <= 8; sides++)
        for (var starIndex = 0; starIndex < 2; starIndex++)
        {
            var key = (sides, starIndex == 1);
            var count = key.Item2 ? sides * 2 : sides;
            var vertices = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = MathHelper.TwoPi * i / count;
                var radius = key.Item2 && i % 2 == 1 ? 57.6f : 128;
                vertices[i] = new Vector2(132) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
            entries.Add(new Entry(264, 128, (x, y) => Inside(x + .5f, y + .5f, vertices)
                ? Color.White : Color.Transparent, mask => _polygons[key] = mask));
        }

        const int width = 4096;
        var ordered = entries.OrderByDescending(entry => entry.Size).ToArray();
        var placements = new List<(Entry Entry, Rectangle Source)>();
        var left = 1;
        var top = 1;
        var rowHeight = 0;
        foreach (var entry in ordered)
        {
            if (left + entry.Size + 1 > width)
            {
                left = 1;
                top += rowHeight + 2;
                rowHeight = 0;
            }
            var source = new Rectangle(left, top, entry.Size, entry.Size);
            placements.Add((entry, source));
            entry.Assign(new Mask(source, entry.Radius));
            left += entry.Size + 2;
            rowHeight = Math.Max(rowHeight, entry.Size);
        }
        var height = top + rowHeight + 1;
        var pixels = new Color[width * height];
        foreach (var (entry, source) in placements)
            for (var y = 0; y < entry.Size; y++)
                for (var x = 0; x < entry.Size; x++)
                    pixels[(source.Y + y) * width + source.X + x] = entry.Sample(x, y);
        Texture = new Texture2D(device, width, height);
        Texture.SetData(pixels);

        void AddRadial(int radius, float thickness, bool glow, Action<Mask> assign)
        {
            var size = radius * 2 + 2;
            var center = size / 2f;
            var inner = Math.Max(0, radius - thickness);
            entries.Add(new Entry(size, radius, (x, y) =>
            {
                var dx = x + .5f - center;
                var dy = y + .5f - center;
                var squared = dx * dx + dy * dy;
                if (glow)
                {
                    var intensity = MathF.Max(0, 1 - MathF.Sqrt(squared) / radius);
                    return Color.White * (intensity * intensity);
                }
                return squared <= radius * radius && squared >= inner * inner ? Color.White : Color.Transparent;
            }, assign));
        }
    }

    public Mask Ring(float radius, int thickness) => _rings[Math.Clamp(
        (int)MathF.Round(Math.Max(1, thickness) * 128 / Math.Max(1, radius)), 1, 128)];

    public bool TryPolygon(int sides, bool star, out Mask mask) => _polygons.TryGetValue((sides, star), out mask);

    public void Draw(SpriteBatch batch, Mask mask, Vector2 center, float radius, Color color, float rotation = 0) =>
        batch.Draw(Texture, center, mask.Source, color, rotation,
            new Vector2(mask.Source.Width / 2f, mask.Source.Height / 2f), radius / mask.Radius, SpriteEffects.None, 0);

    private static bool Inside(float x, float y, Vector2[] vertices)
    {
        var inside = false;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    public void Dispose() => Texture.Dispose();
}
#endif
