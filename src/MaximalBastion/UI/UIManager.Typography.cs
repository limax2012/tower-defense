using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MaximalBastion.Core;
using MaximalBastion.Rendering;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    internal sealed record MenuHeadingLayout(string Text, Rectangle Band, Rectangle SearchBounds,
        string UpperBoundary, string LowerBoundary, float ShadowDepth, bool IsArtwork = false);

    internal MenuHeadingLayout? LastMenuHeading { get; private set; }
    private readonly Dictionary<(SpriteFont Font, string Text), Vector2> _textInkExtents = new();

    private Vector2 TextInkExtents(SpriteFont font, string text)
    {
        if (_textInkExtents.TryGetValue((font, text), out var cached)) return cached;
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character)) continue;
            var index = font.Characters.IndexOf(character);
            if (index < 0) continue;
#if BLAZORGL
            var glyph = font.Glyphs[character];
#else
            var glyph = font.Glyphs[index];
#endif
            top = MathF.Min(top, glyph.Cropping.Top);
            bottom = MathF.Max(bottom, glyph.Cropping.Top + glyph.BoundsInTexture.Height);
        }
        var extents = top <= bottom ? new Vector2(top, bottom) : new Vector2(0, font.LineSpacing);
        if (_textInkExtents.Count >= 256) _textInkExtents.Clear();
        _textInkExtents[(font, text)] = extents;
        return extents;
    }

    private int CenteredTextTop(string text, float centerY, float scale, float? maximumWidth = null)
    {
        if (maximumWidth.HasValue) (text, scale) = FitCenteredText(text, scale, maximumWidth.Value);
        return (int)MathF.Floor(centerY + (TextInkExtents(_font, text).X - _font.MeasureString(text).Y * .5f)
            * scale * GameConstants.FontDrawScale);
    }

    private void DrawDisplayText(SpriteBatch b, string text, Vector2 at, Color color, float scale, bool centered = false)
    {
        var origin = centered ? _displayFont.MeasureString(text) * .5f : Vector2.Zero;
        b.DrawString(_displayFont, text, at, ColorPalette.Text(color), 0, origin,
            scale * GameConstants.FontDrawScale * _displayRasterScale, SpriteEffects.None, 0);
    }

    private void DrawHeading(SpriteBatch b, string text, Rectangle band, float scale = MenuHeadingScale,
        string upperBoundary = "Frame", string lowerBoundary = "Wires")
    {
        const float shadowDepth = 2;
        var measured = _displayFont.MeasureString(text);
        var factor = scale * GameConstants.FontDrawScale * _displayRasterScale;
        factor = MathF.Min(factor, (band.Width - 24) / MathF.Max(1, measured.X));
        var ink = TextInkExtents(_displayFont, text);
        var origin = new Vector2(measured.X * .5f, (ink.X + ink.Y) * .5f);
        var at = new Vector2(band.Center.X, band.Top + band.Height * .5f - shadowDepth * .5f);
        b.DrawString(_displayFont, text, at + Vector2.UnitY * shadowDepth, ColorPalette.Canvas,
            0, origin, factor, SpriteEffects.None, 0);
        b.DrawString(_displayFont, text, at, ColorPalette.Paper, 0, origin, factor, SpriteEffects.None, 0);
        var halfWidth = measured.X * factor * .5f;
        var halfHeight = (ink.Y - ink.X) * factor * .5f;
        var search = new Rectangle((int)MathF.Floor(at.X - halfWidth) - 2,
            (int)MathF.Floor(at.Y - halfHeight) - 2,
            (int)MathF.Ceiling(halfWidth * 2) + 4, (int)MathF.Ceiling(halfHeight * 2) + 4);
        LastMenuHeading = new MenuHeadingLayout(text, band, search, upperBoundary, lowerBoundary, shadowDepth);
    }

    private void DrawDisplayValue(SpriteBatch b, string text, Vector2 at, Color color, float scale, float width)
    {
        var measured = _displayFont.MeasureString(text).X * GameConstants.FontDrawScale * _displayRasterScale * scale;
        DrawDisplayText(b, text, at, color, measured > width ? scale * width / measured : scale);
    }

}
