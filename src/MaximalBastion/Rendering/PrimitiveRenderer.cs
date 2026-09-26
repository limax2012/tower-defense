using MaximalBastion.Core;
using MaximalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed class PrimitiveRenderer : IDisposable
{
#if BLAZORGL
    private const int SharedPolygonRadius = 64;
    private readonly BrowserPrimitiveAtlas _atlas;
#endif
    private readonly int _rasterQuality;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly RasterizerState _clipRasterizer = new() { CullMode = CullMode.None, ScissorTestEnable = true };
    private readonly Dictionary<(int Radius, int Thickness), Texture2D> _rings = new();
    private readonly Dictionary<int, Texture2D> _circles = new();
    private readonly Dictionary<(int Sides, int Radius, bool Star), Texture2D> _polygons = new();
    public Texture2D Pixel { get; }
#if !BLAZORGL
    private Texture2D? _glow;
#endif

    // One radial texture serves every light size; animated lights do not allocate textures.
    public void Glow(SpriteBatch batch, Vector2 center, float radius, Color color, float strength = 0.3f)
    {
#if BLAZORGL
        _atlas.Draw(batch, _atlas.Glow, center, radius, color * strength);
#else
        if (_glow is null)
        {
            const int size = 64;
            _glow = new Texture2D(_graphicsDevice, size, size);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32)) / 32;
                var intensity = MathF.Pow(MathF.Max(0, 1 - distance), 2);
                pixels[y * size + x] = Color.White * intensity;
            }
            _glow.SetData(pixels);
        }
        batch.Draw(_glow, center, null, color * strength, 0, new Vector2(32), radius / 32, SpriteEffects.None, 0);
#endif
    }

    public void Brackets(SpriteBatch batch, Rectangle bounds, Color color, int length = 10)
    {
        for (var index = 0; index < 4; index++)
        {
            var corner = new Point(index % 2 == 0 ? bounds.Left : bounds.Right,
                index < 2 ? bounds.Top : bounds.Bottom);
            var x = corner.X == bounds.Left ? length : -length;
            var y = corner.Y == bounds.Top ? length : -length;
            Line(batch, corner.ToVector2(), corner.ToVector2() + new Vector2(x, 0), color, 2);
            Line(batch, corner.ToVector2(), corner.ToVector2() + new Vector2(0, y), color, 2);
        }
    }

    public PrimitiveRenderer(GraphicsDevice graphicsDevice, int rasterQuality = GameConstants.RenderScale)
    {
        _graphicsDevice = graphicsDevice;
        _rasterQuality = Math.Clamp(rasterQuality, 1, GameConstants.MaximumRenderScale);
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
#if BLAZORGL
        _atlas = new BrowserPrimitiveAtlas(graphicsDevice);
#endif
    }

    /// <summary>Clips scaled and translated logical bounds in a deferred, default-effect sprite batch.</summary>
    public void DrawClipped(SpriteBatch batch, Rectangle bounds, Matrix transform, Action draw)
    {
        batch.End();
        var rasterizer = _graphicsDevice.RasterizerState;
        var blend = _graphicsDevice.BlendState;
        var sampler = _graphicsDevice.SamplerStates[0];
        var depth = _graphicsDevice.DepthStencilState;
        var previousClip = _graphicsDevice.ScissorRectangle;
        var viewport = _graphicsDevice.Viewport.Bounds;
        var topLeft = Vector2.Transform(new Vector2(bounds.Left, bounds.Top), transform) + viewport.Location.ToVector2();
        var bottomRight = Vector2.Transform(new Vector2(bounds.Right, bounds.Bottom), transform) + viewport.Location.ToVector2();
        var clip = new Rectangle((int)MathF.Ceiling(topLeft.X), (int)MathF.Ceiling(topLeft.Y),
            Math.Max(0, (int)MathF.Floor(bottomRight.X) - (int)MathF.Ceiling(topLeft.X)),
            Math.Max(0, (int)MathF.Floor(bottomRight.Y) - (int)MathF.Ceiling(topLeft.Y)));
        clip = Rectangle.Intersect(clip, rasterizer.ScissorTestEnable ? Rectangle.Intersect(viewport, previousClip) : viewport);
        if (clip.Width > 0 && clip.Height > 0)
        {
            _graphicsDevice.ScissorRectangle = clip;
            batch.Begin(SpriteSortMode.Deferred, blend, sampler, depth, _clipRasterizer, null, transform);
            try { draw(); }
            finally
            {
                batch.End();
                _graphicsDevice.ScissorRectangle = previousClip;
                batch.Begin(SpriteSortMode.Deferred, blend, sampler, depth, rasterizer, null, transform);
            }
        }
        else
            batch.Begin(SpriteSortMode.Deferred, blend, sampler, depth, rasterizer, null, transform);
    }

    public void FillRect(SpriteBatch batch, Rectangle rectangle, Color color)
    {
#if BLAZORGL
        batch.Draw(_atlas.Texture, rectangle, _atlas.Pixel, color);
#else
        batch.Draw(Pixel, rectangle, color);
#endif
    }

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
#if BLAZORGL
        batch.Draw(_atlas.Texture, start, _atlas.Pixel, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
#else
        batch.Draw(Pixel, start, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
#endif
    }

    public void Circle(SpriteBatch batch, Vector2 center, float radius, Color color)
    {
#if BLAZORGL
        _atlas.Draw(batch, _atlas.Circle, center, radius, color);
#else
        var logicalRadius = Math.Max(1, (int)MathF.Ceiling(radius));
        var texture = GetCircle(logicalRadius);
        var scale = radius / (logicalRadius * _rasterQuality);
        batch.Draw(texture, center, null, color, 0, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0);
#endif
    }

    public void Ring(SpriteBatch batch, Vector2 center, float radius, Color color, int thickness = 2)
    {
#if BLAZORGL
        _atlas.Draw(batch, _atlas.Ring(radius, thickness), center, radius, color);
#else
        var logicalRadius = Math.Max(1, (int)MathF.Ceiling(radius));
        var logicalThickness = Math.Max(1, thickness);
        var texture = GetRing(logicalRadius, logicalThickness);
        var scale = radius / (logicalRadius * _rasterQuality);
        batch.Draw(texture, center, null, color, 0, new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0);
#endif
    }

    public void DashedRing(SpriteBatch batch, Vector2 center, float radius, Color color, int segments = 24,
        int thickness = 2, float phase = 0f)
    {
        segments = Math.Max(8, segments);
        for (var i = 0; i < segments; i += 2)
        {
            var start = phase + MathHelper.TwoPi * i / segments;
            var end = phase + MathHelper.TwoPi * (i + 1) / segments;
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

    public void DrawShapeOutline(SpriteBatch batch, Vector2 center, int radius, string shape, Color color,
        int marks = 0, float pulse = 1f, bool levelMarks = false, float thickness = 2f)
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
            Ring(batch, center, scaledRadius, color, Math.Max(1, (int)MathF.Round(thickness)));
        else
            DrawPolygonOutline(batch, center, scaledRadius, sides, normalizedShape == "star", color, rotation,
                thickness);

        for (var index = 0; index < marks; index++)
        {
            var markSlots = levelMarks ? 3 : Math.Max(1, marks);
            var angle = MathHelper.TwoPi * index / markSlots - MathHelper.PiOver2;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Line(batch, center + direction * scaledRadius * 0.20f,
                center + direction * scaledRadius * 0.76f, color, thickness);
        }
    }

    public void HealthBar(SpriteBatch batch, Vector2 center, float width, float ratio, Color fillColor, Color trackColor,
        Color outlineColor, int height = 7)
    {
        ratio = MathHelper.Clamp(ratio, 0, 1);
        height = Math.Max(5, height);
        var rect = new Rectangle((int)(center.X - width / 2), (int)center.Y, Math.Max(8, (int)width), height);
        var meter = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
        FillRect(batch, rect, outlineColor);
        FillRect(batch, meter, trackColor);
        FillRect(batch, new Rectangle(meter.X, meter.Y, (int)(meter.Width * ratio), meter.Height), fillColor);
        if (width >= 48)
            for (var notch = 1; notch < 4; notch++)
            {
                var x = meter.X + (int)MathF.Round(meter.Width * notch / 4f - .5f, MidpointRounding.AwayFromZero);
                FillRect(batch, new Rectangle(x, meter.Y, 1, meter.Height), trackColor);
            }
    }

    public void DrawPolygon(SpriteBatch batch, Vector2 center, float radius, int sides, bool star, Color color, float rotation = 0)
    {
#if BLAZORGL
        if (_atlas.TryPolygon(sides, star, out var mask))
        {
            _atlas.Draw(batch, mask, center, radius, color, rotation);
            return;
        }
        var textureRadius = SharedPolygonRadius;
#else
        var textureRadius = Math.Max(4, (int)MathF.Ceiling(radius));
#endif
        var texture = GetPolygon(sides, textureRadius, star);
        batch.Draw(texture, center, null, color, rotation, new Vector2(texture.Width / 2f, texture.Height / 2f), radius / (textureRadius * _rasterQuality), SpriteEffects.None, 0);
    }

    public void DrawPolygonOutline(SpriteBatch batch, Vector2 center, float radius, int sides, bool star, Color color, float rotation, float thickness)
    {
        var count = star ? sides * 2 : sides;
        var first = center + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * radius;
        var previous = first;
        for (var i = 1; i <= count; i++)
        {
            var angle = rotation + MathHelper.TwoPi * i / count;
            var vertexRadius = star && i % 2 == 1 ? radius * 0.45f : radius;
            var next = i == count ? first : center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * vertexRadius;
            Line(batch, previous, next, color, thickness);
            previous = next;
        }
    }

    private Texture2D GetCircle(int radius)
    {
        if (_circles.TryGetValue(radius, out var texture)) return texture;
        var rasterRadius = radius * _rasterQuality;
        var size = rasterRadius * 2 + 2;
        texture = new Texture2D(_graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = size / 2f;
        var radiusSquared = rasterRadius * rasterRadius;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var offsetX = x + 0.5f - center;
                var offsetY = y + 0.5f - center;
                data[y * size + x] = offsetX * offsetX + offsetY * offsetY <= radiusSquared
                    ? Color.White
                    : Color.Transparent;
            }
        texture.SetData(data);
        _circles[radius] = texture;
        return texture;
    }

    private Texture2D GetRing(int radius, int thickness)
    {
        var key = (radius, thickness);
        if (_rings.TryGetValue(key, out var texture)) return texture;
        var rasterRadius = radius * _rasterQuality;
        var rasterThickness = thickness * _rasterQuality;
        var size = rasterRadius * 2 + 2;
        texture = new Texture2D(_graphicsDevice, size, size);
        var data = new Color[size * size];
        var center = size / 2f;
        var outerRadiusSquared = rasterRadius * rasterRadius;
        var innerRadius = Math.Max(0, rasterRadius - rasterThickness);
        var innerRadiusSquared = innerRadius * innerRadius;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var offsetX = x + 0.5f - center;
                var offsetY = y + 0.5f - center;
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                data[y * size + x] = distanceSquared <= outerRadiusSquared && distanceSquared >= innerRadiusSquared
                    ? Color.White
                    : Color.Transparent;
            }
        texture.SetData(data);
        _rings[key] = texture;
        return texture;
    }

    private Texture2D GetPolygon(int sides, int radius, bool star)
    {
        var key = (Math.Max(3, sides), Math.Max(4, radius), star);
        if (_polygons.TryGetValue(key, out var texture)) return texture;

        var rasterRadius = key.Item2 * _rasterQuality;
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
        _clipRasterizer.Dispose();
#if BLAZORGL
        _atlas.Dispose();
#endif
        Pixel.Dispose();
#if !BLAZORGL
        _glow?.Dispose();
#endif
        foreach (var texture in _circles.Values) texture.Dispose();
        foreach (var texture in _rings.Values) texture.Dispose();
        foreach (var texture in _polygons.Values) texture.Dispose();
    }
}
