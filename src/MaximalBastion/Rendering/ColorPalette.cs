using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static class ColorPalette
{
    public const byte PlacementGhostPrimaryAlpha = 104;
    public const byte NeedlePlacementGhostPrimaryAlpha = 128;

    // Central tactical theme. These authored sRGB values are intentionally
    // independent from resolution, DPI, and the scene composite pipeline.
    public static readonly Color Canvas = new(6, 11, 22);
    public static readonly Color Metal = new(43, 63, 80);
    public static readonly Color TowerShadow = new(3, 7, 13);
    public static readonly Color Hostile = new(255, 76, 152);
    public static readonly Color Circuit = new(21, 55, 67);
    public static readonly Color Ink = new(8, 13, 24);
    public static readonly Color Paper = new(226, 244, 250);
    public static readonly Color Panel = new(13, 23, 38);
    public static readonly Color PanelAlt = new(21, 35, 51);
    public static readonly Color Muted = new(137, 160, 181);
    public static readonly Color Disabled = new(49, 64, 80);

    public static readonly Color Navy = new(10, 19, 33);
    public static readonly Color Cobalt = new(71, 166, 235);
    public static readonly Color Cyan = new(67, 222, 216);
    public static readonly Color Berry = new(188, 47, 138);
    public static readonly Color Violet = new(164, 137, 234);
    public static readonly Color AutoMarker = Paper;
    public static readonly Color Auto = new(158, 171, 224);
    public static readonly Color Coral = new(255, 83, 126);
    public static readonly Color Orange = new(241, 158, 71);
    public static readonly Color Gold = new(243, 194, 92);
    public static readonly Color Amber = new(223, 164, 72);
    public static readonly Color AmberText = new(243, 194, 92);
    public static readonly Color Green = new(70, 211, 153);
    public static readonly Color GreenText = new(100, 226, 177);
    public static readonly Color Lime = new(140, 219, 171);

    public static readonly Color MapBoundary = new(37, 67, 87);
    public static readonly Color BuildableOutline = Cyan;
    public static readonly Color CardOutline = new(52, 81, 103);
    public static readonly Color Divider = new(36, 57, 77);
    public static readonly Color Path = new(28, 36, 50);
    public static readonly Color PathStripe = Gold;
    public static readonly Color Range = new(70, 164, 205, 170);
    public static readonly Color PlacementValid = new(42, 194, 117, 190);
    public static readonly Color PlacementInvalid = new(236, 80, 98, 190);

    public static readonly Color HealthHigh = Green;
    public static readonly Color HealthLow = Coral;
    public static readonly Color HealthTrack = new(15, 22, 35);
    public static readonly Color Shield = new(97, 198, 245);
    public static readonly Color Slow = new(130, 229, 245);
    public static readonly Color Burn = new(255, 69, 64);
    public static readonly Color ArmorBreak = Gold;
    public static readonly Color Stun = Cyan;

    public static readonly Color NeedleShot = Cyan;
    public static readonly Color PrecisionShot = new(186, 202, 255);
    public static readonly Color ShardShot = new(255, 142, 55);
    public static readonly Color FrostShot = Slow;
    public static readonly Color EmberShot = Burn;
    public static readonly Color EmberFlame = new(255, 99, 57);
    public static readonly Color EmberCore = new(255, 220, 158);
    public static readonly Color BreakerShot = ArmorBreak;
    public static readonly Color MortarShot = new(211, 244, 70);

    public readonly record struct EnvironmentColors(Color Ground, Color Structure, Color Deck,
        Color Lane, Color Edge, Color Accent, Color Detail);

    public static readonly EnvironmentColors Cinderworks = new(
        new(25, 18, 18), new(56, 37, 32), new(43, 36, 33),
        new(66, 37, 25), new(170, 104, 53), new(255, 145, 60), new(91, 60, 43));
    public static readonly EnvironmentColors Rainline = new(
        new(9, 19, 34), new(24, 44, 64), new(38, 58, 77),
        new(49, 73, 90), new(110, 162, 184), new(117, 221, 236), new(46, 81, 107));
    public static readonly EnvironmentColors Nullspace = new(
        new(17, 10, 34), new(38, 22, 64), new(34, 26, 58),
        new(71, 43, 109), new(143, 107, 211), new(204, 157, 255), new(63, 40, 91));
    public static readonly EnvironmentColors Helix = new(
        new(10, 27, 24), new(27, 61, 49), new(25, 48, 41),
        new(25, 74, 62), new(64, 148, 111), new(151, 231, 156), new(39, 87, 66));

    public static EnvironmentColors Environment(string style) => style.ToLowerInvariant() switch
    {
        "trail" => Rainline,
        "prism" => Nullspace,
        "surge" => Helix,
        _ => Cinderworks
    };

    public static Color Surface(Color accent, float amount = 0.14f) => Color.Lerp(Panel, accent, amount);

    public static Color Text(Color color) => color == Ink || color == Navy ? Paper : color;

    public static Color Tint(Color color, float amount)
    {
        return Color.Lerp(color, Paper, MathHelper.Clamp(amount, 0, 1));
    }

    public static Color ReadableAccent(Color accent, Color background, float minimumContrast = 4.5f)
    {
        if (ContrastRatio(accent, background) >= minimumContrast) return accent;
        var darkTargetContrast = ContrastRatio(Ink, background);
        var lightTargetContrast = ContrastRatio(Paper, background);
        var target = darkTargetContrast >= lightTargetContrast ? Ink : Paper;
        if (MathF.Max(darkTargetContrast, lightTargetContrast) < minimumContrast) return target;

        var unreadableAmount = 0f;
        var readableAmount = 1f;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var amount = (unreadableAmount + readableAmount) * 0.5f;
            var candidate = Color.Lerp(accent, target, amount);
            if (ContrastRatio(candidate, background) >= minimumContrast) readableAmount = amount;
            else unreadableAmount = amount;
        }
        return Color.Lerp(accent, target, readableAmount);
    }

    public static Color BalancedAccentText(Color accent, Color background)
    {
        if (IsYellowAccent(accent)) return ReadableAccent(AmberText, background, 2.6f);
        if (IsLightGreenAccent(accent)) return ReadableAccent(accent, background, 2.6f);
        return ReadableAccent(accent, background, 3f);
    }

    public static Color BalancedAccentLine(Color accent, Color background, float minimumContrast = 1.6f)
    {
        // Pale yellow fills use amber for thin rules; saturated tactical gold
        // keeps its authored hue.
        var lineAccent = IsPaleYellowAccent(accent) ? Amber : IsYellowAccent(accent) ? Gold : accent;
        return ReadableAccent(lineAccent, background, minimumContrast);
    }

    public static Color HighContrastText(Color background) =>
        ContrastRatio(Ink, background) >= ContrastRatio(Paper, background) ? Ink : Paper;

    public static float ContrastRatio(Color first, Color second)
    {
        var lighter = MathF.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = MathF.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(Color color) =>
        0.2126f * LinearChannel(color.R) +
        0.7152f * LinearChannel(color.G) +
        0.0722f * LinearChannel(color.B);

    private static float LinearChannel(byte value)
    {
        var channel = value / 255f;
        return channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private static bool IsYellowAccent(Color color) =>
        color.R >= 180 && color.G >= 145 && color.B <= 165 && color.G >= color.R * 0.68f;

    private static bool IsPaleYellowAccent(Color color) =>
        IsYellowAccent(color) && color.R >= 235 && color.G >= 210 && color.B >= 100;

    private static bool IsLightGreenAccent(Color color) =>
        color.G >= 145 && color.G > color.R * 1.18f && color.G > color.B * 1.18f;

    public static Color WithAlpha(Color color, byte alpha) => new(color.R, color.G, color.B, alpha);

    // SpriteBatch's default AlphaBlend state expects premultiplied tint colors.
    // Use this for translucent geometry that must visibly fade its RGB as well
    // as its alpha, such as uncommitted placement ghosts.
    public static Color WithPremultipliedAlpha(Color color, byte alpha) =>
        Color.FromNonPremultiplied(color.R, color.G, color.B, alpha);

    public static Color Health(float ratio)
    {
        ratio = MathHelper.Clamp(ratio, 0, 1);
        if (ratio < 0.34f) return HealthLow;
        if (ratio < 0.67f) return Gold;
        return HealthHigh;
    }
}
