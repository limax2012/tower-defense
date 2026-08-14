using MinimalBastion.Core;
using MinimalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Rendering;

public sealed class PrimitiveRenderer : IDisposable
{
    private const int RasterQuality = GameConstants.RenderScale;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<(int Radius, int Thickness), Texture2D> _rings = new();
    private readonly Dictionary<int, Texture2D> _circles = new();
    private readonly Dictionary<(int Sides, int Radius, bool Star), Texture2D> _polygons = new();
    public Texture2D Pixel { get; }

    public PrimitiveRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
    }

    public void FillRect(SpriteBatch batch, Rectangle rectangle, Color color) => batch.Draw(Pixel, rectangle, color);

    public void DrawRect(SpriteBatch batch, Rectangle rectangle, Color color, int thickness = 1)
    {
        FillRect(batch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        FillRect(batch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        FillRect(batch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        FillRect(batch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    public void Line(SpriteBatch batch, Vector2 start, Vector2 end, Color color, float thickness = 1f)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.01f) return;
        batch.Draw(Pixel, start, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
    }

    public void Circle(SpriteBatch batch, Vector2 center, float radius, Color color)
    {
        var logicalRadius = Math.Max(1, (int)MathF.Ceiling(radius));
        var texture = GetCircle(logicalRadius);
        var scale = radius / (logicalRadius * RasterQuality);
        batch.Draw(texture, center, null, color, 0, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0);
    }

    public void Ring(SpriteBatch batch, Vector2 center, float radius, Color color, int thickness = 2)
    {
        var logicalRadius = Math.Max(1, (int)MathF.Ceiling(radius));
        var texture = GetRing(logicalRadius, Math.Max(1, thickness));
        var scale = radius / (logicalRadius * RasterQuality);
        batch.Draw(texture, center, null, color, 0, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0);
    }

    public void DashedRing(SpriteBatch batch, Vector2 center, float radius, Color color, int segments = 24, int thickness = 2)
    {
        segments = Math.Max(8, segments);
        for (var i = 0; i < segments; i += 2)
        {
            var start = MathHelper.TwoPi * i / segments;
            var end = MathHelper.TwoPi * (i + 1) / segments;
            Line(batch, center + new Vector2(MathF.Cos(start), MathF.Sin(start)) * radius,
                center + new Vector2(MathF.Cos(end), MathF.Sin(end)) * radius, color, thickness);
        }
    }

    public void DrawShape(SpriteBatch batch, Vector2 center, int radius, string shape, Color primary, Color accent, int marks = 0, bool ring = false, float pulse = 1f, bool levelMarks = false)
    {
        radius = Math.Max(4, radius);
        var scaledRadius = radius * pulse;
        var normalizedShape = shape.ToLowerInvariant();
        var sides = normalizedShape switch
        {
            "triangle" => 3,
            "square" => 4,
            "diamond" => 4,
            "hexagon" => 6,
            "octagon" => 8,
            "star" => 5,
            _ => 0
        };
        var rotation = normalizedShape == "diamond" ? MathHelper.PiOver4 : -MathHelper.PiOver2;
        if (sides == 0)
        {
            Circle(batch, center, scaledRadius, primary);
            Ring(batch, center, scaledRadius, accent, 3);
        }
        else
        {
            DrawPolygon(batch, center, scaledRadius, sides, normalizedShape == "star", primary, rotation);
            DrawPolygonOutline(batch, center, scaledRadius, sides, normalizedShape == "star", accent, rotation, 3);
        }

        for (var i = 0; i < marks; i++)
        {
            var markSlots = levelMarks ? 3 : Math.Max(1, marks);
            var angle = MathHelper.TwoPi * i / markSlots - MathHelper.PiOver2;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Line(batch, center + direction * scaledRadius * 0.20f, center + direction * scaledRadius * 0.76f, accent, 3);
        }
        if (ring) Ring(batch, center, scaledRadius + 6, accent, 3);
    }

    public void HealthBar(SpriteBatch batch, Vector2 center, float width, float ratio, Color fillColor, Color trackColor, Color outlineColor)
    {
        ratio = MathHelper.Clamp(ratio, 0, 1);
        var rect = new Rectangle((int)(center.X - width / 2), (int)center.Y, Math.Max(8, (int)width), 7);
        FillRect(batch, rect, outlineColor);
        FillRect(batch, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), trackColor);
        FillRect(batch, new Rectangle(rect.X + 2, rect.Y + 2, (int)((rect.Width - 4) * ratio), rect.Height - 4), fillColor);
    }

    public void DrawPolygon(SpriteBatch batch, Vector2 center, float radius, int sides, bool star, Color color, float rotation = 0)
    {
        var textureRadius = Math.Max(4, (int)MathF.Ceiling(radius));
        var texture = GetPolygon(sides, textureRadius, star);
        batch.Draw(texture, center, null, color, rotation, new Vector2(texture.Width / 2f, texture.Height / 2f), radius / (textureRadius * RasterQuality), SpriteEffects.None, 0);
    }

    private void DrawPolygonOutline(SpriteBatch batch, Vector2 center, float radius, int sides, bool star, Color color, float rotation, float thickness)
    {
        var vertices = GetPolygonVertices(center, radius, sides, star, rotation);
        for (var i = 0; i < vertices.Length; i++) Line(batch, vertices[i], vertices[(i + 1) % vertices.Length], color, thickness);
    }

    private Texture2D GetCircle(int radius)
    {
        if (_circles.TryGetValue(radius, out var texture)) return texture;
        var rasterRadius = radius * RasterQuality;
        var size = rasterRadius * 2 + 2;
        texture = new Texture2D(_graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = new Vector2(size / 2f);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                data[y * size + x] = distance <= rasterRadius ? Color.White : Color.Transparent;
            }
        texture.SetData(data);
        _circles[radius] = texture;
        return texture;
    }

    private Texture2D GetRing(int radius, int thickness)
    {
        var key = (radius, thickness);
        if (_rings.TryGetValue(key, out var texture)) return texture;
        var rasterRadius = radius * RasterQuality;
        var rasterThickness = thickness * RasterQuality;
        var size = rasterRadius * 2 + 2;
        texture = new Texture2D(_graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = new Vector2(size / 2f);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                data[y * size + x] = distance <= rasterRadius && distance >= rasterRadius - rasterThickness ? Color.White : Color.Transparent;
            }
        texture.SetData(data);
        _rings[key] = texture;
        return texture;
    }

    private Texture2D GetPolygon(int sides, int radius, bool star)
    {
        var key = (Math.Max(3, sides), Math.Max(4, radius), star);
        if (_polygons.TryGetValue(key, out var texture)) return texture;

        var rasterRadius = key.Item2 * RasterQuality;
        var size = rasterRadius * 2 + 8;
        texture = new Texture2D(_graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = new Vector2(size / 2f);
        var vertices = GetPolygonVertices(center, rasterRadius, key.Item1, key.Item3, 0);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                data[y * size + x] = IsInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), vertices) ? Color.White : Color.Transparent;
        texture.SetData(data);
        _polygons[key] = texture;
        return texture;
    }

    private static Vector2[] GetPolygonVertices(Vector2 center, float radius, int sides, bool star, float rotation)
    {
        var count = star ? sides * 2 : sides;
        var vertices = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var angle = rotation + MathHelper.TwoPi * i / count;
            var vertexRadius = star && i % 2 == 1 ? radius * 0.45f : radius;
            vertices[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * vertexRadius;
        }
        return vertices;
    }

    private static bool IsInsidePolygon(Vector2 point, IReadOnlyList<Vector2> vertices)
    {
        var inside = false;
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var x = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < x) inside = !inside;
        }
        return inside;
    }

    public void Dispose()
    {
        Pixel.Dispose();
        foreach (var texture in _circles.Values) texture.Dispose();
        foreach (var texture in _rings.Values) texture.Dispose();
        foreach (var texture in _polygons.Values) texture.Dispose();
    }
}
