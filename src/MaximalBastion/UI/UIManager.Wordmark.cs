using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MaximalBastion.Core;
using MaximalBastion.Rendering;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    private void DrawWordmark(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float time)
    {
        BastionBrandMark.Draw(b, p, center + new Vector2(0, -84), time);
        DrawBrandWord(b, "MAXIMAL", center + new Vector2(0, -25), .8f, 9, 0, ColorPalette.Cyan);
        DrawBrandWord(b, "BASTION", center + new Vector2(0, 21.25f), 2.65f, 0, 4, ColorPalette.Paper);
        var baseline = center.Y + 85;
        var pulse = .5f + .5f * MathF.Sin(time * 1.1f);
        p.Line(b, new Vector2(center.X - 172, baseline), new Vector2(center.X + 172, baseline), ColorPalette.Metal);
        p.Line(b, new Vector2(center.X - 36, baseline), new Vector2(center.X + 36, baseline), ColorPalette.Cyan * (.6f + pulse * .2f), 2);
        var travel = time * 18 % 132;
        foreach (var side in new[] { -1, 1 })
        {
            var end = center.X + side * 172;
            p.Line(b, new Vector2(end, baseline - 4), new Vector2(end, baseline), ColorPalette.Muted, 1);
            p.Line(b, new Vector2(end, baseline), new Vector2(end - side * 10, baseline), ColorPalette.Muted, 1);
            var packet = center.X + side * (39 + travel);
            p.Line(b, new Vector2(packet, baseline), new Vector2(packet - side * 4, baseline), ColorPalette.Cyan * .7f, 1);
        }
    }

    private void DrawBrandWord(SpriteBatch b, string text, Vector2 center, float scale, float tracking,
        int depth, Color color)
    {
        var factor = scale * GameConstants.FontDrawScale * _displayRasterScale;
        var ink = TextInkExtents(_displayFont, text);
        var left = float.MaxValue;
        var right = float.MinValue;
        var advance = 0f;
        for (var i = 0; i < text.Length; i++)
        {
#if BLAZORGL
            var glyph = _displayFont.Glyphs[text[i]];
#else
            var glyph = _displayFont.Glyphs[_displayFont.Characters.IndexOf(text[i])];
#endif
            advance += i == 0 ? MathF.Max(glyph.LeftSideBearing, 0)
                : _displayFont.Spacing + glyph.LeftSideBearing + tracking / factor;
            left = MathF.Min(left, advance + glyph.Cropping.Left);
            right = MathF.Max(right, advance + glyph.Cropping.Left + glyph.BoundsInTexture.Width);
            advance += glyph.Width + glyph.RightSideBearing;
        }
        var origin = new Vector2((left + right) * .5f, (ink.X + ink.Y) * .5f);
        var at = center - new Vector2(depth * .325f, depth * .5f);
        for (var layer = depth; layer >= 0; layer--)
        {
            advance = 0;
            for (var i = 0; i < text.Length; i++)
            {
#if BLAZORGL
                var glyph = _displayFont.Glyphs[text[i]];
#else
                var glyph = _displayFont.Glyphs[_displayFont.Characters.IndexOf(text[i])];
#endif
                advance += i == 0 ? MathF.Max(glyph.LeftSideBearing, 0)
                    : _displayFont.Spacing + glyph.LeftSideBearing + tracking / factor;
                var position = at + (new Vector2(advance + glyph.Cropping.X, glyph.Cropping.Y) - origin) * factor;
                b.Draw(_displayFont.Texture, position + new Vector2(layer * .65f, layer), glyph.BoundsInTexture,
                    layer == 0 ? color : ColorPalette.Metal, 0, Vector2.Zero, factor, SpriteEffects.None, 0);
                advance += glyph.Width + glyph.RightSideBearing;
            }
        }
    }
}
