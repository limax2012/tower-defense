using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Protocols energize the recessed power channels in each weapon assembly.</summary>
public static class TowerProtocolArt
{
    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, float time, bool reducedEffects = false, bool glow = true) =>
        Draw(b, p, center, radius, towerId, 1, false, -MathHelper.PiOver2, time, reducedEffects, glow);

    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects = false, bool glow = true)
    {
        var frame = new TowerStatusFrame(b, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        TowerHardwareChannels.DrawProtocol(frame, towerId, active: true, glow: glow && !reducedEffects);
    }

    public static void DrawStandby(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float opacity)
    {
        var frame = new TowerStatusFrame(b, p, center, radius, towerId, level, apex, aim, 0, true);
        TowerHardwareChannels.DrawProtocol(frame, towerId, active: false, glow: false, opacity);
        TowerHardwareChannels.DrawReceiver(frame, towerId, active: false, glow: false, opacity);
    }
}
