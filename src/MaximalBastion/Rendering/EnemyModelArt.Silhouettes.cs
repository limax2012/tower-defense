using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static void DrawEliteSilhouetteArmor(Chassis f)
    {
        if (f.Rank != 1) return;
        switch (f.Enemy.Definition.Id)
        {
            case "t1_crawler": DrawEliteShoulderArmor(f); break;
            case "t3_brute": DrawEliteRadialCarapace(f, false); break;
            case "t4_aegis": DrawEliteRadialCarapace(f, true); break;
            case "t5_regenerator": DrawEliteTrackShoulders(f); break;
        }
    }

    private static void InsetArmorPlate(Chassis f, float x, float y, float length, float width,
        float angle)
    {
        var center = f.At(x, y);
        var rotation = f.Heading + angle;
        f.P.ArmorPlate(f.Batch, center, (length + .048f) * f.Radius,
            (width + .048f) * f.Radius, rotation, ColorPalette.EnemyRecess);
        f.P.ArmorPlate(f.Batch, center, length * f.Radius,
            width * f.Radius, rotation, f.Edge);
    }

    private static void DrawInsetArmorVent(Chassis f, float y, float length)
    {
        const float width = .18f;
        InsetArmorPlate(f, 0, y, length, width, 0);
        for (var slot = 0; slot < 2; slot++)
        {
            var x = (slot - .5f) * length * .25f;
            f.Rail(x, y - width * .23f, x, y + width * .23f,
                ColorPalette.EnemyRecess, .045f);
        }
    }

    private static void DrawEliteShoulderArmor(Chassis f)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            InsetArmorPlate(f, .40f, side * .545f, .43f, .31f, -side * .62f);
            InsetArmorPlate(f, -.36f, side * .375f, .34f, .27f, side * .48f);
        }
    }

    private static void DrawEliteRadialCarapace(Chassis f, bool aegis)
    {
        for (var end = -1; end <= 1; end += 2)
        {
            for (var side = -1; side <= 1; side += 2)
                InsetArmorPlate(f, end * .43f, side * .44f, .40f, .24f,
                    -end * side * MathHelper.PiOver4);
            var capOffset = aegis ? .72f : .78f;
            InsetArmorPlate(f, end * capOffset, 0, .24f, .39f, 0);
            f.Rail(end * (capOffset + .01f), -.09f, end * (capOffset + .01f), .09f,
                ColorPalette.EnemyRecess, .06f);
            DrawInsetArmorVent(f, end * (aegis ? .60f : .65f), aegis ? .31f : .43f);
        }
    }

    private static void DrawEliteTrackShoulders(Chassis f)
    {
        for (var end = -1; end <= 1; end += 2)
        {
            for (var side = -1; side <= 1; side += 2)
                InsetArmorPlate(f, end * .65f, side * .35f, .36f, .23f,
                    -end * side * MathHelper.PiOver4);
            InsetArmorPlate(f, end * .80f, 0, .30f, .47f, 0);
            f.Rail(end * .825f, -.11f, end * .825f, .11f,
                ColorPalette.EnemyRecess, .065f);
        }
    }
}
