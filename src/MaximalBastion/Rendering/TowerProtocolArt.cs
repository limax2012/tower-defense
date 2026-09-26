using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A single perimeter of weapon-colored energy marks identifies an active protocol.</summary>
public static class TowerProtocolArt
{
    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, float time, bool reducedEffects = false, bool glow = true)
    {
        if (reducedEffects) time = 0;
        var beat = reducedEffects ? 1f : .5f + MathF.Sin(time * 4.5f) * .5f;
        var energy = NightGridArt.TowerAccent(towerId);
        var color = Color.Lerp(energy, ColorPalette.Navy, .16f * (1 - beat));
        var hot = Color.Lerp(color, ColorPalette.Paper, .32f);
        var count = towerId switch
        {
            "needle_turret" => 6,
            "ember_coil" => 5,
            "shard_fan" or "breaker_cannon" or "arc_relay" or "siege_mortar" or "prism_beam" => 3,
            _ => 4
        };
        var speed = towerId switch
        {
            "watchtower" or "siege_mortar" => 0f,
            "frost_spire" or "ember_coil" => .16f,
            "signal_beacon" => -.22f,
            _ => .38f
        };
        var phase = -MathHelper.PiOver2 + time * speed;
        var orbit = radius + 17;
        for (var index = 0; index < count; index++)
        {
            var angle = phase + MathHelper.TwoPi * index / count;
            var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-radial.Y, radial.X);
            var anchor = center + radial * orbit;
            if (!reducedEffects && glow)
                p.Glow(b, anchor, 10, energy, .22f + beat * .16f);

            Vector2 At(float outward, float sideways) => anchor + radial * outward + tangent * sideways;
            void Stroke(float x1, float y1, float x2, float y2, float width = 1.8f) =>
                p.Line(b, At(x1, y1), At(x2, y2), color, width);

            // Each motif occupies the same narrow band, leaving the machine and tactical rings clear.
            switch (towerId)
            {
                case "needle_turret":
                    Stroke(-3, 0, 3, 0, 1.6f);
                    break;
                case "watchtower":
                    Stroke(-3, 0, 3, 0);
                    Stroke(3, -3, 3, 3, 1.4f);
                    break;
                case "frost_spire":
                    Stroke(-3.4f, 0, 0, -2.5f, 1.5f);
                    Stroke(0, -2.5f, 3.4f, 0, 1.5f);
                    Stroke(3.4f, 0, 0, 2.5f, 1.5f);
                    Stroke(0, 2.5f, -3.4f, 0, 1.5f);
                    break;
                case "shard_fan":
                    Stroke(-2, -2.3f, 2, -4, 1.6f);
                    Stroke(-2, 0, 3.5f, 0, 1.6f);
                    Stroke(-2, 2.3f, 2, 4, 1.6f);
                    break;
                case "ember_coil":
                    var flare = 2.5f + beat * 1.5f;
                    Stroke(-2.5f, -2.3f, flare, 0, 2.2f);
                    Stroke(flare, 0, -1.7f, 2.3f, 2.2f);
                    p.Line(b, At(-1.5f, 0), At(flare - 1, 0), ColorPalette.EmberFlame, 1.2f);
                    break;
                case "breaker_cannon":
                    Stroke(2.5f, -4, -2.5f, 0, 2.6f);
                    Stroke(-2.5f, 0, 2.5f, 4, 2.6f);
                    break;
                case "arc_relay":
                    Stroke(-1.5f, -4.5f, 2, -1.5f, 1.7f);
                    Stroke(2, -1.5f, -2, 1.5f, 1.7f);
                    Stroke(-2, 1.5f, 1.5f, 4.5f, 1.7f);
                    break;
                case "siege_mortar":
                    Stroke(0, -4.5f, 0, 4.5f, 2.5f);
                    Stroke(0, -4.5f, -2.8f, -4.5f, 1.5f);
                    Stroke(0, 4.5f, -2.8f, 4.5f, 1.5f);
                    p.Line(b, At(0, -1.6f), At(0, 1.6f), hot, 1.2f);
                    break;
                case "prism_beam":
                    Stroke(-2.5f, -3.5f, 3.5f, 0, 1.6f);
                    Stroke(3.5f, 0, -2.5f, 3.5f, 1.6f);
                    Stroke(-2.5f, 3.5f, -2.5f, -3.5f, 1.6f);
                    break;
                case "signal_beacon":
                    Stroke(-2.2f, -2.8f, 2.2f, -2.8f, 1.5f);
                    Stroke(2.2f, -2.8f, 2.2f, 2.8f, 1.5f);
                    Stroke(2.2f, 2.8f, -2.2f, 2.8f, 1.5f);
                    Stroke(-2.2f, 2.8f, -2.2f, -2.8f, 1.5f);
                    break;
                default:
                    Stroke(0, -3, 0, 3);
                    break;
            }
        }
    }
}
