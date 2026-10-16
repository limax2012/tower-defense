namespace MaximalBastion.Rendering;

/// <summary>Shared presentation geometry for tower models and their weapon effects.</summary>
public static class TowerTierVisuals
{
    public static int Stage(int level, bool apex) => apex ? 4 : Math.Clamp(level, 1, 3);

    public static float FrostRotorRadius(int stage) => stage switch { 1 => .32f, 2 => .40f, 3 => .46f, _ => .52f };
    public static float FrostCoolantOffset(int stage) => stage switch { 1 => .56f, 2 => .68f, 3 => .79f, _ => .91f };
    public static float FrostCoolantLength(int stage) => stage switch { 1 => .26f, 2 => .31f, 3 => .42f, _ => .50f };
    public static int FrostCoolantBanks(int stage) => stage == 1 ? 2 : 4;
    public static float MortarBankOffset(int stage) => stage switch { 1 => .46f, 2 => .64f, 3 => .78f, _ => .89f };

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
