using MaximalBastion.Core;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    private const float SetupTooltipDelay = .6f;
    private const float SetupTooltipFadeDuration = .1f;
    private const float SetupTooltipScale = .64f;
    private const int SetupTooltipPadding = 9;
    private readonly record struct SetupTip(string Text, float Opacity);
    private Rectangle _setupHoverOption;
    private string _setupHoverDescription = "";
    private Point _setupTooltipCursor;
    private float _setupHoverSeconds;
    private SetupTip? _outgoingSetupTip;
    private float _setupFadeRemaining;

    internal string SetupTooltipText => VisibleSetupTip?.Text ?? "";
    internal Rectangle? SetupTooltipBounds => VisibleSetupTip is { } tip ? PlaceSetupTooltip(tip.Text) : null;

    private static float SetupTipOpacity(float progress) => progress * (2f - progress);

    private SetupTip? HoveredSetupTip
    {
        get
        {
            if (_setupHoverSeconds < SetupTooltipDelay || _setupHoverDescription.Length == 0) return null;
            var progress = MathHelper.Clamp((_setupHoverSeconds - SetupTooltipDelay) / SetupTooltipFadeDuration, 0f, 1f);
            return new SetupTip(_setupHoverDescription, SetupTipOpacity(progress));
        }
    }

    private SetupTip? VisibleSetupTip => _outgoingSetupTip is { } tip
        ? tip with { Opacity = tip.Opacity * SetupTipOpacity(_setupFadeRemaining / SetupTooltipFadeDuration) }
        : HoveredSetupTip;

    private void AdvanceSetupTooltip(float elapsedSeconds)
    {
        if (_setupHoverOption != Rectangle.Empty)
            _setupHoverSeconds = MathF.Min(SetupTooltipDelay + SetupTooltipFadeDuration, _setupHoverSeconds + elapsedSeconds);
        if (_outgoingSetupTip.HasValue)
        {
            _setupFadeRemaining = MathF.Max(0, _setupFadeRemaining - elapsedSeconds);
            if (_setupFadeRemaining == 0) _outgoingSetupTip = null;
        }
    }

    private void SetSetupHover(Rectangle option, string description)
    {
        if (option != _setupHoverOption)
        {
            if (HoveredSetupTip is { Opacity: > 0 } previous)
            {
                _outgoingSetupTip = previous;
                _setupFadeRemaining = SetupTooltipFadeDuration;
            }
            _setupHoverSeconds = 0;
        }
        _setupHoverOption = option;
        _setupHoverDescription = description;
        if (_hudPointer.X >= 0 && _hudPointer.Y >= 0) _setupTooltipCursor = _hudPointer.ToPoint();
    }

    private void ResetSetupTooltip()
    {
        _setupHoverOption = Rectangle.Empty;
        _setupHoverDescription = "";
        _setupHoverSeconds = 0;
        _outgoingSetupTip = null;
        _setupFadeRemaining = 0;
    }

    private Rectangle PlaceSetupTooltip(string text)
    {
        var area = MenuFrameBounds;
        area.Inflate(-6, -6);
        var textWidth = _font.MeasureString(text).X * SetupTooltipScale * GameConstants.FontDrawScale;
        var width = Math.Min(area.Width, (int)MathF.Ceiling(textWidth) + 2 * SetupTooltipPadding);
        const int height = 24;
        var cursor = _setupTooltipCursor;
        var x = cursor.X + 12;
        var y = cursor.Y + 16;
        if (x + width > area.Right) x = cursor.X - 12 - width;
        if (y + height > area.Bottom) y = cursor.Y - 10 - height;
        return new Rectangle(Math.Clamp(x, area.Left, area.Right - width),
            Math.Clamp(y, area.Top, area.Bottom - height), width, height);
    }

    private void DrawSetupTooltip(SpriteBatch batch, PrimitiveRenderer p)
    {
        if (VisibleSetupTip is not { } tip) return;
        var rect = PlaceSetupTooltip(tip.Text);
        var opacity = tip.Opacity;
        p.FillRect(batch, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height), ColorPalette.Canvas * (.65f * opacity));
        p.FillRect(batch, rect, ColorPalette.Navy * opacity);
        p.DrawRect(batch, rect, ColorPalette.CardOutline * opacity);
        DrawFittedCenteredText(batch, tip.Text, rect.Center.ToVector2(), ColorPalette.Paper * opacity,
            SetupTooltipScale, rect.Width - 2 * SetupTooltipPadding);
    }
}
