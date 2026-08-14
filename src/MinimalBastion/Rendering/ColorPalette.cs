using Microsoft.Xna.Framework;

namespace MinimalBastion.Rendering;

public static class ColorPalette
{
    // Central tactical theme. These authored sRGB values are intentionally
    // independent from resolution, DPI, and the scene composite pipeline.
    public static readonly Color Ink = new(18, 27, 43);
    public static readonly Color Paper = new(244, 245, 248);
    public static readonly Color Panel = new(248, 249, 251);
    public static readonly Color PanelAlt = new(231, 237, 245);
    public static readonly Color Muted = new(91, 107, 128);
    public static readonly Color Disabled = new(188, 199, 213);

    public static readonly Color Navy = new(21, 43, 70);
    public static readonly Color Cobalt = new(44, 122, 231);
    public static readonly Color Cyan = new(33, 146, 170);
    public static readonly Color Violet = new(124, 83, 218);
    public static readonly Color Coral = new(236, 80, 98);
    public static readonly Color Orange = new(229, 138, 50);
    public static readonly Color Gold = new(232, 182, 55);
    public static readonly Color Green = new(42, 194, 117);
    public static readonly Color Lime = new(129, 201, 77);

    public static readonly Color MapBoundary = new(27, 53, 68);
    public static readonly Color BuildableOutline = Cyan;
    public static readonly Color CardOutline = new(180, 194, 210);
    public static readonly Color Divider = new(202, 213, 226);
    public static readonly Color Path = new(56, 78, 101);
    public static readonly Color PathStripe = Gold;
    public static readonly Color Range = new(70, 164, 205, 170);
    public static readonly Color PlacementValid = new(42, 194, 117, 190);
    public static readonly Color PlacementInvalid = new(236, 80, 98, 190);

    public static readonly Color HealthHigh = Green;
    public static readonly Color HealthLow = Coral;
    public static readonly Color HealthTrack = new(30, 45, 61);
    public static readonly Color Shield = new(48, 164, 198);
    public static readonly Color Slow = new(92, 184, 220);

    public static Color Tint(Color color, float amount)
    {
        return Color.Lerp(color, Paper, MathHelper.Clamp(amount, 0, 1));
    }

    public static Color WithAlpha(Color color, byte alpha) => new(color.R, color.G, color.B, alpha);

    public static Color Health(float ratio)
    {
        ratio = MathHelper.Clamp(ratio, 0, 1);
        if (ratio < 0.34f) return HealthLow;
        if (ratio < 0.67f) return Gold;
        return HealthHigh;
    }
}
