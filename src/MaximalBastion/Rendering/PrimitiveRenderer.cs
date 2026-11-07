using MaximalBastion.Core;
using MaximalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public sealed partial class PrimitiveRenderer : IDisposable
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
    private readonly Texture2D _debuffFog;
    private readonly Texture2D _armorShapes;
    private readonly Rectangle[] _armorCircleSources = new Rectangle[8];
    private static readonly Rectangle ArmorPlateSource = new(1, ArmorShapeSize * 2 + 1, ArmorPlateWidth, ArmorPlateHeight);
    private static readonly Rectangle ArmorPixelSource = new(ArmorPlateWidth + 5, ArmorShapeSize * 2 + 2, 1, 1);
    internal const int DebuffFogSize = 130;
    internal const int ArmorPlateWidth = 128;
    internal const int ArmorPlateHeight = 64;
    internal const int ArmorShapeSize = 130;
    internal const int ArmorShapeRadius = 64;
    internal const float ArmorRingInnerRatio = .65f;

    public void ArmorLine(SpriteBatch batch, Vector2 start, Vector2 end, Color color, float thickness = 1f)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= .01f) return;
        batch.Draw(_armorShapes, start, ArmorPixelSource, color, MathF.Atan2(delta.Y, delta.X),
            new Vector2(0, .5f), new Vector2(length, thickness), SpriteEffects.None, 0);
    }

    /// <summary>Draws small joints and endcaps using the same circle rasterization as the general primitives.</summary>
    public void ArmorCircle(SpriteBatch batch, Vector2 center, float radius, Color color)
    {
#if BLAZORGL
        var source = _armorCircleSources[0];
        var scale = radius / 256;
#else
        var logicalRadius = Math.Max(1, (int)MathF.Ceiling(radius));
        if (logicalRadius > _armorCircleSources.Length) throw new ArgumentOutOfRangeException(nameof(radius));
        var source = _armorCircleSources[logicalRadius - 1];
        var scale = radius / (logicalRadius * _rasterQuality);
#endif
        batch.Draw(_armorShapes, center, source, color, 0,
            new Vector2(source.Width / 2f, source.Height / 2f), scale, SpriteEffects.None, 0);
    }

    internal static Color SampleArmorCircle(int x, int y, int rasterRadius)
    {
        var center = rasterRadius + 1f;
        var offsetX = x + .5f - center;
        var offsetY = y + .5f - center;
        return offsetX * offsetX + offsetY * offsetY <= rasterRadius * rasterRadius
            ? Color.White : Color.Transparent;
    }

    /// <summary>A regular polygon with a fixed-resolution antialiased contour and its first vertex along +X.</summary>
    public void ArmorPolygon(SpriteBatch batch, Vector2 center, float radius, int sides,
        Color color, float rotation = 0)
    {
        DrawArmorShape(batch, center, radius, sides, color, rotation, 0);
    }

    /// <summary>Raised radial armor plates surrounding an opening at 65% of the outer radius.</summary>
    public void BeveledArmorRing(SpriteBatch batch, Vector2 center, float radius, int sides,
        Color color, float rotation = 0)
    {
        DrawArmorShape(batch, center, radius, sides, color, rotation, 1);
    }

    private void DrawArmorShape(SpriteBatch batch, Vector2 center, float radius, int sides,
        Color color, float rotation, int row)
    {
        if (radius <= 0) return;
        var column = sides switch
        {
            3 => 0,
            6 => 1,
            8 => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(sides))
        };
        batch.Draw(_armorShapes, center,
            new Rectangle(column * ArmorShapeSize, row * ArmorShapeSize, ArmorShapeSize, ArmorShapeSize),
            color, rotation, new Vector2(ArmorShapeSize * .5f), radius / ArmorShapeRadius,
            SpriteEffects.None, 0);
    }

    internal static Color SampleArmorPolygon(int x, int y, int sides, bool beveledRing = false)
    {
        if (x <= 0 || y <= 0 || x >= ArmorShapeSize - 1 || y >= ArmorShapeSize - 1)
            return Color.Transparent;
        var point = new Vector2(x + .5f, y + .5f) - new Vector2(ArmorShapeSize * .5f);
        var sectorAngle = MathHelper.TwoPi / sides;
        var apothem = ArmorShapeRadius * MathF.Cos(MathHelper.Pi / sides);
        var outerDistance = float.MaxValue;
        var edgeNormal = Vector2.Zero;
        for (var side = 0; side < sides; side++)
        {
            var angle = (side + .5f) * sectorAngle;
            var normal = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var distance = apothem - Vector2.Dot(point, normal);
            if (distance >= outerDistance) continue;
            outerDistance = distance;
            edgeNormal = normal;
        }

        var coverage = Math.Clamp(outerDistance + .5f, 0, 1);
        if (coverage <= 0) return Color.Transparent;
        if (!beveledRing) return Color.White * coverage;

        var innerDistance = apothem * (1 - ArmorRingInnerRatio) - outerDistance;
        coverage *= Math.Clamp(innerDistance + .5f, 0, 1);
        if (coverage <= 0) return Color.Transparent;

        var face = .86f - .07f * edgeNormal.X - .10f * edgeNormal.Y - .025f * point.Y / ArmorShapeRadius;
        var outerLight = Math.Clamp(.67f - .28f * edgeNormal.X - .34f * edgeNormal.Y, .25f, 1);
        var outerBevel = MathHelper.Lerp(.16f, outerLight, Smooth(.7f, 1.8f, outerDistance));
        var outerShade = MathHelper.Lerp(outerBevel, face, Smooth(5.2f, 6.5f, outerDistance));
        var innerLight = Math.Clamp(.62f + .23f * edgeNormal.X + .30f * edgeNormal.Y, .25f, 1);
        var innerBevel = MathHelper.Lerp(.13f, innerLight, Smooth(1.0f, 2.0f, innerDistance));
        var innerShade = MathHelper.Lerp(innerBevel, face, Smooth(4.4f, 5.5f, innerDistance));
        var shade = outerDistance < innerDistance ? outerShade : innerShade;

        var cornerAngle = MathF.Round(MathF.Atan2(point.Y, point.X) / sectorAngle) * sectorAngle;
        var cornerDirection = new Vector2(MathF.Cos(cornerAngle), MathF.Sin(cornerAngle));
        var signedSeamDistance = cornerDirection.X * point.Y - cornerDirection.Y * point.X;
        var seamDistance = MathF.Abs(signedSeamDistance);
        var seamNormal = new Vector2(cornerDirection.Y, -cornerDirection.X) * MathF.Sign(signedSeamDistance);
        var seamLight = Math.Clamp(.65f - .23f * seamNormal.X - .28f * seamNormal.Y, .25f, 1);
        var seamBevel = MathHelper.Lerp(.11f, seamLight, Smooth(.5f, 1.4f, seamDistance));
        shade = MathHelper.Lerp(seamBevel, shade, Smooth(2.2f, 3.1f, seamDistance));
        return new Color(shade * coverage, shade * coverage, shade * coverage, coverage);
    }

    /// <summary>A tinted chamfered plate with a dark rim and directional bevel lighting.</summary>
    public void ArmorPlate(SpriteBatch batch, Vector2 center, float length, float width,
        float rotation, Color tint)
    {
        if (length <= 0 || width <= 0) return;
        batch.Draw(_armorShapes, center, ArmorPlateSource, tint, rotation,
            new Vector2(ArmorPlateWidth * .5f, ArmorPlateHeight * .5f),
            new Vector2(length / ArmorPlateWidth, width / ArmorPlateHeight), SpriteEffects.None, 0);
    }

    internal static Color SampleArmorPlate(int x, int y)
    {
        var px = x + .5f;
        var py = y + .5f;
        var distance = float.MaxValue;
        var lighting = 0f;
        const float diagonal = .70710678f;
        Edge(px - 2, -1, 0);
        Edge(126 - px, 1, 0);
        Edge(py - 2, 0, -1);
        Edge(62 - py, 0, 1);
        Edge((px + py - 16) * diagonal, -diagonal, -diagonal);
        Edge((112 - px + py) * diagonal, diagonal, -diagonal);
        Edge((48 + px - py) * diagonal, -diagonal, diagonal);
        Edge((176 - px - py) * diagonal, diagonal, diagonal);
        var coverage = Math.Clamp(distance + .5f, 0, 1);
        if (coverage <= 0) return Color.Transparent;
        var face = .92f - .025f * px / ArmorPlateWidth - .035f * py / ArmorPlateHeight;
        var bevel = MathHelper.Lerp(.34f, lighting, Smooth(1.0f, 2.0f, distance));
        var shade = MathHelper.Lerp(bevel, face, Smooth(6.3f, 7.1f, distance));
        return new Color(shade * coverage, shade * coverage, shade * coverage, coverage);

        void Edge(float candidate, float normalX, float normalY)
        {
            if (candidate >= distance) return;
            distance = candidate;
            lighting = Math.Clamp(.74f - .22f * normalX - .26f * normalY, .48f, 1);
        }
    }

    public void DebuffFog(SpriteBatch batch, Vector2 center, float radius, int mask,
        bool precisionExpose, float opacity, int layer = 0, Vector2? stretch = null)
    {
        mask &= 15;
        if (mask == 0 || opacity <= 0) return;
        var cell = Math.Clamp(layer, 0, 1) * 30 + (precisionExpose ? 15 : 0) + mask - 1;
        batch.Draw(_debuffFog, center, new Rectangle(cell % 8 * DebuffFogSize,
                cell / 8 * DebuffFogSize, DebuffFogSize, DebuffFogSize),
            Color.White * opacity, 0, new Vector2(DebuffFogSize * .5f),
            (stretch ?? Vector2.One) * (radius / 64f), SpriteEffects.None, 0);
    }

    internal static Color SampleDebuffFog(int x, int y, int mask, int layer = 0, bool precisionExpose = false)
    {
        var point = (new Vector2(x + .5f, y + .5f) - new Vector2(DebuffFogSize * .5f)) / 64f;
        var distance = point.Length();
        if (distance >= 1 || (mask & 15) == 0) return Color.Transparent;
        var coarse = DensityNoise(point.X * 4.8f + 17 + layer * 23, point.Y * 4.8f + 31 + layer * 47);
        var grain = DensityNoise(point.X * 14 + 43 + layer * 37, point.Y * 14 + 11 + layer * 19);
        var warped = distance + (coarse - .5f) * .20f;
        var edge = (1 - Smooth(.65f, 1, warped)) * (1 - Smooth(.93f, 1, distance));
        var volume = (.38f + .24f * Smooth(.12f, .52f, distance)) * (.66f + .34f * coarse);
        var alpha = Math.Clamp(edge * volume * (.84f + .16f * grain), 0, 1);
        var color = DebuffFogColor(point, mask, precisionExpose);
        // Premultiplied color varies between cells; the shared alpha field never does.
        return new Color((byte)(color.X * alpha * 255), (byte)(color.Y * alpha * 255),
            (byte)(color.Z * alpha * 255), (byte)(alpha * 255));
    }

    internal static Vector3 DebuffFogColor(Vector2 point, int mask, bool precisionExpose = false)
    {
        var count = EnemyDebuffAuraArt.RegionCount(mask);
        if (count == 0) return Vector3.Zero;
        var angle = MathF.Atan2(point.Y, point.X) + MathHelper.PiOver2;
        if (angle < 0) angle += MathHelper.TwoPi;
        var sectorWidth = MathHelper.TwoPi / count;
        var distance = point.Length();
        var centerBlend = Smooth(.02f, .28f, distance);
        var sum = Vector3.Zero;
        var total = 0f;
        var index = 0;
        for (var bit = 0; bit < 4; bit++)
        {
            if ((mask & 1 << bit) == 0) continue;
            var color = bit switch
            {
                0 => ColorPalette.Burn,
                1 => ColorPalette.Slow,
                2 => ColorPalette.ArmorBreak,
                _ => precisionExpose ? ColorPalette.PrecisionShot : ColorPalette.ExposeFog
            };
            var centerAngle = (index++ + .5f) * sectorWidth;
            var delta = MathF.Abs(MathHelper.WrapAngle(angle - centerAngle));
            var weight = count == 1 ? 1 : 1 - Smooth(sectorWidth * .5f - .10f, sectorWidth * .5f + .10f, delta);
            weight = MathHelper.Lerp(1f / count, weight, centerBlend);
            sum += color.ToVector3() * weight;
            total += weight;
        }
        return sum / MathF.Max(.0001f, total);
    }
#if !BLAZORGL
    private Texture2D? _glow;
    private readonly Texture2D[] _energyEnvelopes = new Texture2D[EnergyEnvelopeStyles];
    private readonly Dictionary<string, Texture2D> _authoredShapes = new(StringComparer.Ordinal);
#endif
    internal const int AuthoredShapeRadius = 64;
    internal const int EnergyEnvelopeStyles = 8;
    internal const int EnergyEnvelopeRasterRadius = 64;
    internal const int EnergyEnvelopeRasterSize = EnergyEnvelopeRasterRadius * 2 + 2;

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

    // A broad, broken energy density keeps the field visible without drawing a range boundary.
    public void EnergyEnvelope(SpriteBatch batch, Vector2 center, float radius, Color color,
        float phase, float strength = 1f, int style = 0)
    {
        if (radius <= 0 || strength <= 0) return;
        style = Math.Clamp(style, 0, EnergyEnvelopeStyles - 1);
        var firstColor = color * (strength * .64f);
        var secondColor = color * (strength * .46f);
        var firstRotation = phase * .065f;
        var secondRotation = 1.91f - phase * .043f;
#if BLAZORGL
        var envelope = _atlas.EnergyEnvelopes[style];
        _atlas.Draw(batch, envelope, center, radius, firstColor, firstRotation);
        _atlas.Draw(batch, envelope, center, radius, secondColor, secondRotation);
#else
        var envelope = _energyEnvelopes[style];
        var origin = new Vector2(EnergyEnvelopeRasterSize / 2f);
        var scale = radius / EnergyEnvelopeRasterRadius;
        batch.Draw(envelope, center, null, firstColor, firstRotation, origin, scale, SpriteEffects.None, 0);
        batch.Draw(envelope, center, null, secondColor, secondRotation, origin, scale, SpriteEffects.None, 0);
#endif
    }

    // Mask styles: ion, frost, heat, electrical, prismatic, pressure, filled pressure, filled heat.
    internal static Color SampleEnergyEnvelope(int x, int y, int style)
    {
        var offset = (new Vector2(x + .5f, y + .5f) - new Vector2(EnergyEnvelopeRasterSize / 2f)) /
            EnergyEnvelopeRasterRadius;
        var radius = offset.Length();
        if (radius >= 1) return Color.Transparent;
        var seed = style * 13.17f;
        var coarse = DensityNoise(offset.X * 4.1f + 19 + seed, offset.Y * 4.1f + 23 - seed);
        var medium = DensityNoise(offset.X * 13 + 41 - seed, offset.Y * 13 + 17 + seed);
        var fine = DensityNoise(offset.X * 31 + 13 + seed, offset.Y * 31 + 29 + seed);
        if (style >= 6)
        {
            var solidRadius = radius + (coarse - .5f) * .22f;
            var volume = (1 - Smooth(.05f, .94f, solidRadius)) * (1 - Smooth(.90f, 1, radius));
            var grain = style == 7
                ? .72f + .28f * Smooth(.22f, .78f, coarse)
                : .63f + .25f * medium + .12f * EnvelopeFilaments(offset, 6, 5);
            return Color.White * MathHelper.Clamp(volume * grain, 0, 1);
        }
        var warpedRadius = radius + (coarse - .5f) * .12f;
        var rise = Smooth(.12f, .55f, warpedRadius);
        var fall = (1 - Smooth(.57f, .98f, warpedRadius)) * (1 - Smooth(.92f, 1, radius));
        var pattern = style switch
        {
            1 => .64f + .20f * fine + .24f * MathF.Pow(1 - MathF.Abs(medium * 2 - 1), 7) * coarse,
            2 => .63f + .37f * Smooth(.26f, .78f, coarse) + .09f * medium,
            3 => .67f + .16f * medium + .29f * EnvelopeFilaments(offset, 9, style),
            4 => .66f + .17f * coarse + .31f * EnvelopeFilaments(offset, 8, style),
            5 => .63f + .37f * DensityNoise(offset.X * 8 + 7, offset.Y * 5 + 31) +
                .10f * EnvelopeFilaments(offset, 6, style),
            _ => .72f + .17f * medium + .11f * EnvelopeFilaments(offset, 13, style)
        };
        return Color.White * MathHelper.Clamp((.16f + .80f * rise) * fall * pattern, 0, 1);
    }

    private static float EnvelopeFilaments(Vector2 position, int grid, int style)
    {
        var cell = position * grid;
        var left = (int)MathF.Floor(cell.X);
        var top = (int)MathF.Floor(cell.Y);
        var intensity = 0f;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var ix = left + dx;
            var iy = top + dy;
            var choice = DensityHash(ix + style * 43, iy + 59);
            if (choice < .30f) continue;
            var center = new Vector2(ix + .18f + .64f * DensityHash(ix + 83, iy),
                iy + .18f + .64f * DensityHash(ix, iy + 101));
            var angle = style switch
            {
                0 => MathF.Atan2(center.Y, center.X),
                4 => MathHelper.PiOver4 + MathHelper.PiOver2 * (choice > .65f ? 1 : 0),
                _ => choice * MathHelper.TwoPi
            };
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var halfLength = style == 5 ? .30f : .18f + .13f * choice;
            var width = style == 5 ? .11f : .06f;
            intensity = Math.Max(intensity, SegmentDensity(cell, center - direction * halfLength,
                center + direction * halfLength, width));
            if (style == 3)
            {
                var branch = new Vector2(MathF.Cos(angle + 1.1f), MathF.Sin(angle + 1.1f));
                intensity = Math.Max(intensity, SegmentDensity(cell, center,
                    center + branch * .23f, width * .8f));
            }
        }
        return intensity;
    }

    private static float SegmentDensity(Vector2 point, Vector2 start, Vector2 end, float width)
    {
        var delta = end - start;
        var along = MathHelper.Clamp(Vector2.Dot(point - start, delta) / delta.LengthSquared(), 0, 1);
        return 1 - Smooth(0, width, Vector2.Distance(point, start + delta * along));
    }

    private static float Smooth(float low, float high, float value)
    {
        var amount = MathHelper.Clamp((value - low) / (high - low), 0, 1);
        return amount * amount * (3 - 2 * amount);
    }

    private static float DensityNoise(float x, float y)
    {
        var left = (int)MathF.Floor(x);
        var top = (int)MathF.Floor(y);
        var sx = Smooth(0, 1, x - left);
        var sy = Smooth(0, 1, y - top);
        return MathHelper.Lerp(MathHelper.Lerp(DensityHash(left, top), DensityHash(left + 1, top), sx),
            MathHelper.Lerp(DensityHash(left, top + 1), DensityHash(left + 1, top + 1), sx), sy);
    }

    private static float DensityHash(int x, int y)
    {
        var value = unchecked((uint)(x * 374761393 + y * 668265263));
        value = unchecked((value ^ (value >> 13)) * 1274126177u);
        return ((value ^ (value >> 16)) & 65535) / 65535f;
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
        _mapSelectionCheck = CreateMapSelectionCheck(graphicsDevice, _rasterQuality);
        _supportMasks = CreateSupportMasks(graphicsDevice);
        _bossExhaustAtlas = CreateBossExhaustAtlas(graphicsDevice);
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
        const int armorCircleTop = ArmorShapeSize * 2 + ArmorPlateHeight + 4;
#if BLAZORGL
        const int armorShapeWidth = 516;
        const int maximumCircleSize = 514;
#else
        const int armorShapeWidth = ArmorShapeSize * 3;
        var maximumCircleSize = _armorCircleSources.Length * _rasterQuality * 2 + 2;
#endif
        var armorShapeHeight = armorCircleTop + maximumCircleSize + 1;
        _armorShapes = new Texture2D(graphicsDevice, armorShapeWidth, armorShapeHeight);
        var armorShapePixels = new Color[armorShapeWidth * armorShapeHeight];
        for (var row = 0; row < 2; row++)
        for (var column = 0; column < 3; column++)
        {
            var sides = column switch { 0 => 3, 1 => 6, _ => 8 };
            for (var y = 0; y < ArmorShapeSize; y++)
            for (var x = 0; x < ArmorShapeSize; x++)
                armorShapePixels[(row * ArmorShapeSize + y) * armorShapeWidth + column * ArmorShapeSize + x] =
                    SampleArmorPolygon(x, y, sides, row == 1);
        }
        for (var y = 0; y < ArmorPlateHeight; y++)
        for (var x = 0; x < ArmorPlateWidth; x++)
            armorShapePixels[(ArmorPlateSource.Y + y) * armorShapeWidth + ArmorPlateSource.X + x] =
                SampleArmorPlate(x, y);
        for (var y = ArmorPixelSource.Y - 1; y <= ArmorPixelSource.Y + 2; y++)
        for (var x = ArmorPixelSource.X - 1; x <= ArmorPixelSource.X + 2; x++)
            armorShapePixels[y * armorShapeWidth + x] = Color.White;

#if BLAZORGL
        _armorCircleSources[0] = new Rectangle(1, armorCircleTop, maximumCircleSize, maximumCircleSize);
        FillArmorCircle(_armorCircleSources[0], 256);
#else
        var circleLeft = 1;
        for (var logicalRadius = 1; logicalRadius <= _armorCircleSources.Length; logicalRadius++)
        {
            var rasterRadius = logicalRadius * _rasterQuality;
            var size = rasterRadius * 2 + 2;
            var source = new Rectangle(circleLeft, armorCircleTop, size, size);
            _armorCircleSources[logicalRadius - 1] = source;
            FillArmorCircle(source, rasterRadius);
            circleLeft += size + 2;
        }
#endif
        _armorShapes.SetData(armorShapePixels);

        void FillArmorCircle(Rectangle source, int rasterRadius)
        {
            for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
                armorShapePixels[(source.Y + y) * armorShapeWidth + source.X + x] = SampleArmorCircle(x, y, rasterRadius);
        }

        var fogWidth = DebuffFogSize * 8;
        var fogHeight = DebuffFogSize * 8;
        _debuffFog = new Texture2D(graphicsDevice, fogWidth, fogHeight);
        var fogPixels = new Color[fogWidth * fogHeight];
        for (var layer = 0; layer < 2; layer++)
        for (var precision = 0; precision < 2; precision++)
        for (var mask = 1; mask <= 15; mask++)
        {
            var cell = layer * 30 + precision * 15 + mask - 1;
            for (var y = 0; y < DebuffFogSize; y++)
            for (var x = 0; x < DebuffFogSize; x++)
                fogPixels[(cell / 8 * DebuffFogSize + y) * fogWidth + cell % 8 * DebuffFogSize + x] =
                    SampleDebuffFog(x, y, mask, layer, precision != 0);
        }
        _debuffFog.SetData(fogPixels);
#if BLAZORGL
        _atlas = new BrowserPrimitiveAtlas(graphicsDevice);
#else
        var pixels = new Color[EnergyEnvelopeRasterSize * EnergyEnvelopeRasterSize];
        for (var style = 0; style < EnergyEnvelopeStyles; style++)
        {
            var texture = new Texture2D(graphicsDevice, EnergyEnvelopeRasterSize, EnergyEnvelopeRasterSize);
            for (var y = 0; y < EnergyEnvelopeRasterSize; y++)
            for (var x = 0; x < EnergyEnvelopeRasterSize; x++)
                pixels[y * EnergyEnvelopeRasterSize + x] = SampleEnergyEnvelope(x, y, style);
            texture.SetData(pixels);
            _energyEnvelopes[style] = texture;
        }
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

    public void DrawDiamond(SpriteBatch batch, Vector2 center, float radius, Color color, float rotation = 0)
    {
        var size = new Vector2(radius * MathF.Sqrt(2));
#if BLAZORGL
        batch.Draw(_atlas.Texture, center, _atlas.Pixel, color, rotation + MathHelper.PiOver4,
            new Vector2(.5f), size, SpriteEffects.None, 0);
#else
        batch.Draw(Pixel, center, null, color, rotation + MathHelper.PiOver4,
            new Vector2(.5f), size, SpriteEffects.None, 0);
#endif
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

    public void DrawAuthoredShape(SpriteBatch batch, Vector2 center, float radius, string shapeId,
        Color color, float rotation = 0)
    {
        if (radius <= 0) return;
#if BLAZORGL
        _atlas.Draw(batch, _atlas.AuthoredShape(shapeId), center, radius, color, rotation);
#else
        var texture = GetAuthoredShape(shapeId);
        batch.Draw(texture, center, null, color, rotation,
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            radius / (AuthoredShapeRadius * _rasterQuality), SpriteEffects.None, 0);
#endif
    }

#if !BLAZORGL
    private Texture2D GetAuthoredShape(string shapeId)
    {
        if (_authoredShapes.TryGetValue(shapeId, out var texture)) return texture;
        var outline = EnemyModelContours.Shapes[shapeId];
        var rasterRadius = AuthoredShapeRadius * _rasterQuality;
        var size = rasterRadius * 2 + 8;
        var center = new Vector2(size / 2f);
        var vertices = new Vector2[outline.Length];
        for (var index = 0; index < outline.Length; index++)
            vertices[index] = center + outline[index] * rasterRadius;
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            pixels[y * size + x] = IsInsidePolygon(new Vector2(x + .5f, y + .5f), vertices)
                ? Color.White : Color.Transparent;
        texture = new Texture2D(_graphicsDevice, size, size);
        texture.SetData(pixels);
        _authoredShapes.Add(shapeId, texture);
        return texture;
    }
#endif

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
        _debuffFog.Dispose();
        _armorShapes.Dispose();
        _mapSelectionCheck.Dispose();
        _supportMasks.Dispose();
        _bossExhaustAtlas.Dispose();
#if !BLAZORGL
        _glow?.Dispose();
        foreach (var texture in _energyEnvelopes) texture.Dispose();
        foreach (var texture in _authoredShapes.Values) texture.Dispose();
#endif
        foreach (var texture in _circles.Values) texture.Dispose();
        foreach (var texture in _rings.Values) texture.Dispose();
        foreach (var texture in _polygons.Values) texture.Dispose();
    }
}
