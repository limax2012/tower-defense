using MaximalBastion.Core;
using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

internal sealed record EnemyHudStrip(EnemyInstance Enemy, EnemyCondition[] Conditions, Vector2[] Centers,
    Vector2 OwnerAnchor, Vector2 LeaderEnd, Vector2 Anchor, Vector2 Outward, int Rail, int Side,
    bool IsLegible, int LeaderCrossings)
{
    internal Vector2 TrackEdge => OwnerAnchor;
    internal Vector2[] OwnerHull { get; init; } = [];
}

/// <summary>Allocation-free bounds and viewport clamping for enemy durability meters.</summary>
internal static class EnemyHudPresentation
{
    internal const float GlyphRadius = 3.4f;

    internal static Vector4 VisibleBounds(EnemyInstance enemy, Vector2 position, float heading)
    {
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var radius = EnemyModelArt.BodyRadius(enemy) + (enemy.Shield > 0 ? 3.5f : 0);
        var padding = enemy.Shield > 0 ? 1.45f : 0;
        var minimum = new Vector2(float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity);
        var vertices = EnemyModelContours.Shapes[EnemyModelContours.ShapeId(enemy)];
        foreach (var vertex in vertices)
        {
            var point = position + (forward * vertex.X + side * vertex.Y) * radius;
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }
        return new Vector4(minimum.X - padding, minimum.Y - padding, maximum.X + padding, maximum.Y + padding);
    }

    internal static Vector2 ClampMeterOrigin(Vector2 origin, float width, float above, float below,
        Rectangle? viewport = null)
    {
        var bounds = viewport ?? new Rectangle(0, GameConstants.TopBarHeight,
            GameConstants.SidebarX, GameConstants.LogicalHeight - GameConstants.TopBarHeight);
        return new Vector2(
            Math.Clamp(origin.X, bounds.Left + width * .5f + 3, bounds.Right - width * .5f - 3),
            Math.Clamp(origin.Y, bounds.Top + above + 2, bounds.Bottom - below - 2));
    }

    internal static (Rectangle Health, Rectangle Shield) MeterFrames(EnemyInstance enemy,
        Vector2 position, float radius, Rectangle? viewport = null)
    {
        var width = Math.Max(8, (int)MathF.Round(radius * (enemy.IsBoss ? 2f : 1.65f)));
        // One clamped anchor preserves the gap between both frames at viewport edges.
        var healthCenter = ClampMeterOrigin(position - new Vector2(0, radius + 4), width,
            enemy.Shield > 0 ? 6.5f : 2, 2, viewport);
        var left = (int)MathF.Round(healthCenter.X - width * .5f);
        var health = new Rectangle(left, (int)MathF.Round(healthCenter.Y - 2), width, 4);
        var shield = new Rectangle(left, (int)MathF.Round(healthCenter.Y - 6.5f), width, 3);
        return (health, shield);
    }
}
