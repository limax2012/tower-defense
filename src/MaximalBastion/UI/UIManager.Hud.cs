using MaximalBastion.Core;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    internal static Rectangle HudMetricBounds(int column) => column switch
    {
        0 => new Rectangle(18, 6, 85, 44),
        1 => new Rectangle(115, 6, 98, 44),
        2 => new Rectangle(225, 6, 98, 44),
        _ => HudThreatBounds
    };

    internal static Rectangle HudContextBounds(int column) => column switch
    {
        0 => new Rectangle(860, 6, 134, 44),
        1 => new Rectangle(1004, 6, 76, 44),
        _ => new Rectangle(1092, 6, 136, 44)
    };

    internal static string PauseGameTimeLabel(GameSession session) =>
        $"GAME TIME  {FormatRunDuration(session.Statistics.SimulatedSeconds)}";

    private void DrawHudMetrics(SpriteBatch batch, PrimitiveRenderer p, GameSession session)
    {
        const float changeScale = .38f;
        const float changeGap = 5;
        var change = !session.IsSandbox && _creditPulse > 0 && session.Economy.Credits < 10000 &&
            Math.Abs((long)_creditChange) < 10000
                ? $"{(_creditChange > 0 ? "+" : "")}{_creditChange}" : "";
        var creditBounds = HudMetricBounds(1);
        var creditWidth = creditBounds.Width - (change.Length == 0 ? 0 :
            _font.MeasureString(change).X * GameConstants.FontDrawScale * changeScale + changeGap);
        DrawMetric(0, "LIVES", $"{session.Economy.Lives}/{session.Economy.StartingLives}", ColorPalette.Coral,
            unlimited: session.IsSandbox);
        DrawMetric(1, "CREDITS", session.Economy.Credits.ToString(), ColorPalette.Gold,
            session.IsSandbox || _creditPulse <= 0 ? ColorPalette.Paper : _creditChange > 0
                ? ColorPalette.Green : ColorPalette.Gold, session.IsSandbox, creditWidth);
        DrawMetric(2, session.IsEndlessMode && !session.IsSandbox ? "ENDLESS" : "WAVE",
            session.IsSandbox ? "N/A" : session.IsEndlessMode ? session.CurrentWave.ToString()
                : $"{session.CurrentWave}/{session.TotalWaves}", ColorPalette.Cyan);
        DrawMetric(3, "ENEMIES", session.EnemiesRemaining.ToString(), ColorPalette.Muted);

        if (change.Length > 0)
        {
            var valueWidth = MathF.Min(creditWidth, _displayFont.MeasureString(session.Economy.Credits.ToString()).X *
                GameConstants.FontDrawScale * _displayRasterScale);
            DrawText(batch, change,
                new Vector2(creditBounds.X + valueWidth + changeGap, 33 - (1 - _creditPulse / .7f) * 7),
                (_creditChange > 0 ? ColorPalette.Green : ColorPalette.Gold) * (_creditPulse / .7f), changeScale);
        }

        void DrawMetric(int column, string label, string value, Color accent,
            Color? valueColor = null, bool unlimited = false, float? maximumValueWidth = null)
        {
            var bounds = HudMetricBounds(column);
            DrawText(batch, label, new Vector2(bounds.X, 8), accent, .75f);
            if (unlimited)
                DrawInfinity(batch, p, new Vector2(bounds.X + 14, 39), ColorPalette.Paper);
            else
                DrawDisplayValue(batch, value, new Vector2(bounds.X, 26), valueColor ?? ColorPalette.Paper,
                    1f, maximumValueWidth ?? bounds.Width);
        }
    }

    private void DrawHudRunContext(SpriteBatch batch, PrimitiveRenderer p, GameSession session)
    {
        DrawContext(0, "MAP", session.Map.Definition.DisplayName,
            ColorPalette.Environment(session.Map.Definition.PathVisual.Style).Accent);
        DrawContext(1, "DIFFICULTY", session.Difficulty.DisplayName, session.Difficulty.AccentColor);
        DrawContext(2, session.IsCoOp ? "MODE / CO-OP" : "MODE", session.Challenge.DisplayName,
            session.Challenge.AccentColor);

        void DrawContext(int column, string label, string value, Color accent)
        {
            var bounds = HudContextBounds(column);
            value = value.ToUpperInvariant();
            const float labelScale = .46f;
            const float rowOffset = 19;
            var valueScale = MathF.Min(.65f, bounds.Width / MathF.Max(1,
                _displayFont.MeasureString(value).X * GameConstants.FontDrawScale * _displayRasterScale));
            var labelInk = TextInkExtents(_font, label) * (labelScale * GameConstants.FontDrawScale);
            var valueInk = TextInkExtents(_displayFont, value) *
                (valueScale * GameConstants.FontDrawScale * _displayRasterScale);
            var top = MathF.Min(labelInk.X, rowOffset + valueInk.X);
            var bottom = MathF.Max(labelInk.Y, rowOffset + valueInk.Y);
            var originY = (GameConstants.TopBarHeight - 2 - top - bottom) * .5f;
            DrawText(batch, label, new Vector2(bounds.X, originY), ColorPalette.Muted, labelScale);
            DrawDisplayText(batch, value, new Vector2(bounds.X, originY + rowOffset),
                ColorPalette.BalancedAccentText(accent, ColorPalette.Navy), valueScale);
        }
    }

    private static void DrawInfinity(SpriteBatch batch, PrimitiveRenderer p, Vector2 center, Color color)
    {
        const int segments = 48;
        var previous = center + new Vector2(13, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * MathHelper.TwoPi / segments;
            var next = center + new Vector2(MathF.Cos(angle) * 13, MathF.Sin(angle * 2) * 5.5f);
            p.Line(batch, previous, next, color, 2f);
            previous = next;
        }
    }
}
