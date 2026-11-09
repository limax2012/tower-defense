using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static void DrawReinforcedFamily(Chassis f)
    {
        DrawHull(f.Batch, f.P, f.Enemy, f.Center, f.Radius * .97f, f.Heading, f.Edge);
        DrawHull(f.Batch, f.P, f.Enemy, f.Center, f.Radius * .86f, f.Heading, ColorPalette.EnemyChassis);
        switch (f.Enemy.Definition.Id)
        {
            case "t1_crawler": DrawReinforcedAssaultArmor(f); break;
            case "t3_brute":
                DrawReinforcedRadialArmor(f, 6, .78f, .76f);
                DrawSideVents(f, .62f);
                break;
            case "t4_aegis":
                DrawReinforcedRadialArmor(f, 8, .80f, .76f, EliteAegisBodyScale);
                DrawSideVents(f, .61f, EliteAegisBodyScale);
                break;
            case "t5_regenerator": DrawReinforcedTankArmor(f); break;
        }
    }

    private static void DrawReinforcedAssaultArmor(Chassis f)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            SegmentPlate(f, new(-.43f, side * .18f), new(.20f, side * .40f), .26f, f.Shell);
            SegmentPlate(f, new(.27f, side * .57f), new(.57f, side * .32f), .31f, f.Edge);
            SegmentPlate(f, new(-.53f, side * .13f), new(-.27f, side * .33f), .23f, f.Armor);
        }
        Plate(f, new(.44f, 0), .33f, .49f, 0, f.Edge);
        f.Rail(.54f, -.14f, .54f, .14f, ColorPalette.EnemyRecess, .065f);
        Plate(f, new(-.62f, 0), .18f, .23f, 0, f.Shell);
    }

    private static void DrawReinforcedRadialArmor(Chassis f, int sides, float radius, float capOffset,
        float bodyScale = 1)
    {
        var rotation = f.Heading + (sides == 8 ? MathHelper.Pi / 8 : 0);
        f.P.ArmorPolygon(f.Batch, f.Center, f.Radius * (radius + .025f) * bodyScale, sides,
            ColorPalette.EnemyRecess, rotation);
        f.P.BeveledArmorRing(f.Batch, f.Center, f.Radius * radius * bodyScale, sides, f.Edge, rotation);
        for (var end = -1; end <= 1; end += 2)
        {
            Plate(f, new(end * capOffset * bodyScale, 0), .21f * bodyScale, .34f * bodyScale, 0, f.Edge);
            f.Rail(end * (capOffset + .012f) * bodyScale, -.09f * bodyScale,
                end * (capOffset + .012f) * bodyScale, .09f * bodyScale,
                ColorPalette.EnemyRecess, .062f * bodyScale);
        }
    }

    private static void DrawSideVents(Chassis f, float sideOffset, float bodyScale = 1)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            var lateral = side * sideOffset * bodyScale;
            Plate(f, new(0, lateral), .46f * bodyScale, .18f * bodyScale, 0, f.Edge);
            for (var slot = -1; slot <= 1; slot++)
            {
                var along = slot * .115f * bodyScale;
                f.Rail(along, lateral - .045f * bodyScale, along, lateral + .045f * bodyScale,
                    ColorPalette.EnemyChassis, .032f * bodyScale);
            }
        }
    }

    private static void DrawReinforcedTankArmor(Chassis f)
    {
        DrawTracks(f);
        var boss = f.Rank == 2;
        var ring = boss ? .64f : .62f;
        var rotation = f.Heading + MathHelper.Pi / 8;
        f.P.ArmorPolygon(f.Batch, f.Center, f.Radius * (ring + .025f), 8, ColorPalette.EnemyRecess, rotation);
        f.P.BeveledArmorRing(f.Batch, f.Center, f.Radius * ring, 8, f.Edge, rotation);
        if (boss) DrawBossCarapace(f);
        var count = boss ? 5 : 4;
        var step = boss ? .25f : .27f;
        var length = boss ? .225f : .245f;
        for (var side = -1; side <= 1; side += 2)
        for (var index = 0; index < count; index++)
        {
            var at = new Vector2((index - (count - 1) * .5f) * step, side * .585f);
            Plate(f, at, length, .20f, 0, f.Edge);
            Window(f, at + new Vector2(-length * .28f, side * .040f),
                at + new Vector2(length * .28f, side * .040f), .038f);
            if (boss && f.Enemy.BossPhaseActive)
                DrawOverdriveLamp(f, at + new Vector2(-length * .28f, side * .040f),
                    at + new Vector2(length * .28f, side * .040f), .038f);
        }
        const float cap = .83f;
        for (var end = -1; !boss && end <= 1; end += 2)
        {
            Plate(f, new(end * cap, 0), .24f, .34f, 0, f.Edge);
            f.Rail(end * (cap + .016f), -.073f, end * (cap + .016f), .073f,
                ColorPalette.EnemyRecess, .055f);
        }
        if (boss)
        {
            // Closed housings form fixed foredeck armor opposite the rear exhaust shutters.
            var front = f.Center + f.Forward * (f.Radius * .14f);
            f.P.BossExhaustShutters(f.Batch, front, f.Radius, f.Heading + MathHelper.Pi, -1);
        }
    }
}
