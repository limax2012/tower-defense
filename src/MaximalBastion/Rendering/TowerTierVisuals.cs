namespace MaximalBastion.Rendering;

/// <summary>Shared presentation geometry for tower models and their weapon effects.</summary>
public static class TowerTierVisuals
{
    public static int Stage(int level, bool apex) => apex ? 4 : Math.Clamp(level, 1, 3);

    public static float PrismBarrelLength(int level, bool apex) => Stage(level, apex) switch
    {
        1 => 1.10f,
        2 => 1.18f,
        3 => 1.28f,
        _ => 1.38f
    };

    public static float PrismApertureWidth(int level, bool apex) => Stage(level, apex) switch
    {
        1 => .20f,
        2 => .23f,
        3 => .26f,
        _ => .30f
    };
}
