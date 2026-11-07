using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static Color RepairBezelColor(Chassis f, float time)
    {
        if (f.AtRest || f.Enemy.Definition.Id != "t5_regenerator" ||
            !EnemyStatusArt.IsRegenerating(f.Enemy)) return ColorPalette.EnemyArmorEdge;

        var phase = time * (MathHelper.TwoPi / 1.2f) + f.Enemy.Id * .73f;
        var pulse = f.Reduced ? .55f : .5f - .5f * MathF.Cos(phase);
        return Color.Lerp(ColorPalette.EnemyArmorEdge, ColorPalette.BeaconEnergy, .45f + .40f * pulse);
    }
}
