using System.Globalization;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    private static float AchievementProgressFraction(string progress)
    {
        var values = progress.Split('/');
        return values.Length == 2 &&
               double.TryParse(values[0], NumberStyles.Number, CultureInfo.CurrentCulture, out var current) &&
               double.TryParse(values[1], NumberStyles.Number, CultureInfo.CurrentCulture, out var target) && target > 0
            ? (float)Math.Clamp(current / target, 0, 1)
            : 0;
    }

    private static void DrawAchievementProgress(SpriteBatch b, PrimitiveRenderer p, Rectangle rail, float progress, Color accent)
    {
        p.FillRect(b, rail, ColorPalette.Metal);
        if (progress > 0)
            p.FillRect(b, new Rectangle(rail.X, rail.Y, Math.Max(2, (int)(rail.Width * progress)), rail.Height), accent * .8f);
    }

    private static void DrawCompletionInsignia(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float size, bool complete, Color accent,
        bool showPending = true)
    {
        const float rotation = -MathHelper.PiOver2;
        p.DrawPolygon(b, center + new Vector2(0, 2), size, 6, false, ColorPalette.Canvas, rotation);
        p.DrawPolygon(b, center, size, 6, false, complete ? ColorPalette.Surface(accent, .14f) : ColorPalette.PanelAlt, rotation);
        p.DrawPolygonOutline(b, center, size, 6, false, complete ? accent * .75f : ColorPalette.CardOutline, rotation, 1);
        if (complete)
        {
            p.Line(b, center + new Vector2(-.42f, 0) * size, center + new Vector2(-.08f, .3f) * size, accent, 2);
            p.Line(b, center + new Vector2(-.08f, .3f) * size, center + new Vector2(.46f, -.35f) * size, accent, 2);
        }
        else if (showPending)
            p.DrawPolygonOutline(b, center, size * .34f, 4, false, ColorPalette.Muted * .6f, 0, 1);
    }

    private static void DrawResultDistrict(SpriteBatch b, PrimitiveRenderer p, MapDefinition map, Rectangle bounds, bool victory)
    {
        var colors = ColorPalette.Environment(map.PathVisual.Style);
        var outcome = victory ? ColorPalette.Green : ColorPalette.Coral;
        var width = Math.Max(1, map.LogicalSize.Width);
        var height = Math.Max(1, map.LogicalSize.Height);
        Vector2 Project(Vector2 point) => new(bounds.Left + point.X / width * bounds.Width,
            bounds.Top + point.Y / height * bounds.Height);
        p.Glow(b, bounds.Center.ToVector2(), bounds.Width * .57f, colors.Accent, .15f);
        foreach (var region in map.BuildableRegions)
        {
            var topLeft = Project(new Vector2(region.X, region.Y));
            var bottomRight = Project(new Vector2(region.X + region.Width, region.Y + region.Height));
            var deck = new Rectangle((int)topLeft.X, (int)topLeft.Y,
                Math.Max(1, (int)(bottomRight.X - topLeft.X)), Math.Max(1, (int)(bottomRight.Y - topLeft.Y)));
            p.FillRect(b, deck, colors.Structure * .65f);
            p.Line(b, topLeft, new Vector2(bottomRight.X, topLeft.Y), colors.Edge * .42f);
        }
        for (var i = 1; i < map.Path.Count; i++)
        {
            var start = Project(map.Path[i - 1].ToVector2());
            var end = Project(map.Path[i].ToVector2());
            p.Line(b, start, end, ColorPalette.Canvas, 7);
            p.Line(b, start, end, colors.Accent * .67f, 2);
        }
        if (map.Path.Count == 0) return;
        var entry = Project(map.Path[0].ToVector2());
        var goal = Project(map.Path[^1].ToVector2());
        p.Circle(b, entry, 3, colors.Accent);
        p.Glow(b, goal, 25, outcome, .55f);
        DrawCompletionInsignia(b, p, goal, 10, victory, outcome, showPending: false);
        if (!victory)
        {
            p.Line(b, goal + new Vector2(-3, -3), goal + new Vector2(3, 3), outcome, 2);
            p.Line(b, goal + new Vector2(-3, 3), goal + new Vector2(3, -3), outcome, 2);
        }
    }

    private static void DrawOutcomeEmblem(SpriteBatch b, PrimitiveRenderer p, Vector2 center, bool victory)
    {
        var accent = victory ? ColorPalette.Green : ColorPalette.Coral;
        p.Glow(b, center, 64, accent, .24f);
        p.DrawPolygonOutline(b, center, 37, 6, false, accent * .23f, -MathHelper.PiOver2, 1);
        DrawCompletionInsignia(b, p, center, 28, victory, accent, showPending: false);
        if (!victory)
        {
            p.Line(b, center + new Vector2(-8, -8), center + new Vector2(8, 8), accent, 3);
            p.Line(b, center + new Vector2(-8, 8), center + new Vector2(8, -8), accent, 3);
        }
        p.Line(b, center + new Vector2(-72, 0), center + new Vector2(-47, 0), accent * .4f);
        p.Line(b, center + new Vector2(47, 0), center + new Vector2(72, 0), accent * .4f);
    }
}
