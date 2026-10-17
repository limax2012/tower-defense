using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Foundation-mounted projectors and control hardware for battlefield interaction.</summary>
public static class BattlefieldInteractionArt
{
    public static void DrawFootprint(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, Color color, float strength, bool selected, bool glow = true)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var sides = towerId switch
        {
            "watchtower" or "frost_spire" => 4,
            "arc_relay" or "signal_beacon" or "shard_fan" => 3,
            "ember_coil" or "siege_mortar" => 8,
            _ => 6
        };
        var rotation = sides == 4 ? MathHelper.PiOver4 : -MathHelper.PiOver2;
        var mount = radius * (stage switch { 1 => .89f, 2 => .99f, 3 => 1.06f, _ => 1.12f });
        var light = color * Math.Clamp(strength, 0, 1);
        if (glow) p.Glow(b, center, mount * 1.8f, color, selected ? .19f : .09f);
        // A layered skirt hugs the foundation, leaving its rotating weapon unobstructed.
        p.DrawPolygonOutline(b, center + new Vector2(0, 1), mount + 2.8f, sides, false,
            ColorPalette.Navy * strength, rotation, selected ? 3.4f : 2.6f);
        p.DrawPolygonOutline(b, center, mount + 1.7f, sides, false,
            light * (selected ? .85f : .55f), rotation, selected ? 1.3f : .8f);
        for (var i = 0; i < sides; i++)
        {
            var a = rotation + i * MathHelper.TwoPi / sides;
            var next = a + MathHelper.TwoPi / sides;
            var first = new Vector2(MathF.Cos(a), MathF.Sin(a)) * (mount + 1.7f);
            var last = new Vector2(MathF.Cos(next), MathF.Sin(next)) * (mount + 1.7f);
            var start = center + Vector2.Lerp(first, last, .34f);
            var end = center + Vector2.Lerp(first, last, .66f);
            p.Line(b, start, end, ColorPalette.Metal * strength, 3.2f);
            p.Line(b, start, end, light, selected ? 1.7f : 1f);
        }
    }

    public static void DrawOwnerContact(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, Color color)
    {
        var triangular = towerId is "arc_relay" or "signal_beacon" or "shard_fan";
        var contact = center + new Vector2(-radius * .48f, radius * (triangular ? .46f : .65f));
        p.Line(b, contact - new Vector2(2, 0), contact + new Vector2(2, 0), ColorPalette.Navy, 4);
        p.Line(b, contact - new Vector2(1.5f, 0), contact + new Vector2(1.5f, 0), color, 1.5f);
    }

    public static Vector2 AutoControllerPosition(Vector2 center, float radius, string towerId) =>
        center + new Vector2(0, radius * (towerId is "arc_relay" or "signal_beacon" or "shard_fan" ? .66f : 1.02f));

    public static void DrawAutoController(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, float time, bool reduced)
    {
        var at = AutoControllerPosition(center, radius, towerId);
        var color = ColorPalette.Auto;
        p.Line(b, at - new Vector2(0, 5), at, ColorPalette.Metal, 5);
        p.Line(b, at + new Vector2(-7, 1), at + new Vector2(7, 1), ColorPalette.Navy, 8);
        p.Line(b, at - new Vector2(7, 0), at + new Vector2(7, 0), ColorPalette.Muted, 6);
        p.Line(b, at - new Vector2(5.7f, 0), at + new Vector2(5.7f, 0), ColorPalette.Navy, 4);
        for (var i = 0; i < 3; i++)
        {
            var x = -3.6f + i * 3.6f;
            var beat = reduced ? 1 : .70f + .30f * MathF.Sin(time * 5 - i * .8f);
            p.Line(b, at + new Vector2(x - .9f, 0), at + new Vector2(x + .9f, 0), color * beat, 2.5f);
        }
        p.Line(b, at + new Vector2(-7, -1), at + new Vector2(-7, 1), color, 1);
        p.Line(b, at + new Vector2(7, -1), at + new Vector2(7, 1), color, 1);
        if (!reduced) p.Glow(b, at, 11, color, .25f);
    }
}
