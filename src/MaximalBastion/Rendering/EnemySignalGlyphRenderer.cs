using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Shared energy colors for signal gems, transfers, and receiving effects.</summary>
public static class EnemySignalGlyphRenderer
{
    public static Color Accent(EnemySignalRole role) => role switch
    {
        EnemySignalRole.Accelerator => ColorPalette.Acceleration,
        EnemySignalRole.Restorer => ColorPalette.Green,
        EnemySignalRole.Bulwark => ColorPalette.Shield,
        EnemySignalRole.Jammer => ColorPalette.Orange,
        EnemySignalRole.Disruptor => ColorPalette.Disruption,
        _ => ColorPalette.EnemyMagenta
    };
}
