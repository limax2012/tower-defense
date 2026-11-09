using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static void DrawBossCarapace(Chassis f)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            // The guard's inner edge clears the moving outer tread run.
            Plate(f, new(0, side * .76875f), 1.13f, .1375f, 0, f.Shell);
            for (var seam = -1; seam <= 1; seam++)
            {
                var x = seam * .32f;
                f.Rail(x, side * .716f, x, side * .797f,
                    ColorPalette.EnemyChassis, .027f);
            }
            for (var end = -1; end <= 1; end += 2)
                SegmentPlate(f, new(end * .60f, side * .71f),
                    new(end * .965f, side * .31f), .20f, f.Edge);
        }

        for (var end = -1; end <= 1; end += 2)
        {
            Plate(f, new(end * .96f, 0), .28f, .57f, 0, f.Edge);
            f.Rail(end * .97f, -.13f, end * .97f, .13f,
                ColorPalette.EnemyRecess, .065f);

            Plate(f, new(end * .735f, 0), .09f, .24f, 0, f.Shell);
            for (var side = -1; side <= 1; side += 2)
                f.Rail(end * .713f, side * .045f, end * .757f, side * .045f,
                    ColorPalette.EnemyRecess, .018f);
        }
    }
}
