using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    internal static void DrawGroundShadow(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 center, float heading, float time, bool reducedEffects, ReadOnlySpan<EnemyGroundContact> contacts)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        DrawGroundShadow(new Chassis(batch, p, enemy, center, heading, time, reducedEffects, 0, 0, 1), contacts);
    }

    private static void DrawGroundShadow(Chassis f, ReadOnlySpan<EnemyGroundContact> contacts)
    {
        f.P.DrawAuthoredShape(f.Batch, f.GroundCenter + new Vector2(1, 2), f.Radius,
            EnemyModelContours.ArmorOutlineId(f.Enemy), ColorPalette.Canvas, f.Heading);
        foreach (var contact in contacts)
        {
            var pad = contact.Pose.ContactRadius;
            var radius = pad * (contact.Planted ? 1.15f : .8f);
            f.P.ArmorLine(f.Batch, contact.GroundPoint - f.Forward * radius,
                contact.GroundPoint + f.Forward * radius, ColorPalette.TowerShadow,
                pad * (contact.Planted ? 1.5f : .9f));
        }
    }
}
