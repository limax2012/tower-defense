using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Shared mechanical silhouettes and infrastructure for the field and tactical displays.</summary>
public static class NightGridArt
{
    public static Color TowerAccent(string id) => id switch
    {
        "needle_turret" => ColorPalette.NeedleShot,
        "watchtower" => ColorPalette.PrecisionShot,
        "shard_fan" => ColorPalette.ShardShot,
        "frost_spire" => ColorPalette.FrostShot,
        "ember_coil" => ColorPalette.EmberShot,
        "breaker_cannon" => ColorPalette.BreakerShot,
        "siege_mortar" => ColorPalette.MortarShot,
        "pulse_plate" => ColorPalette.Gold,
        "prism_beam" => ColorPalette.Violet,
        "signal_beacon" or "charge_forge" => ColorPalette.Green,
        _ => ColorPalette.Cyan
    };

    public static void Tower(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius, string id,
        int level = 1, float angle = -MathHelper.PiOver2, float time = 0, float recoil = 0,
        float opacity = 1, bool lights = true, bool groundEffects = true, bool apex = false)
    {
        var accent = TowerAccent(id) * opacity;
        var metal = ColorPalette.Metal * opacity;
        var ceramic = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, .56f) * opacity;
        var dark = ColorPalette.Navy * opacity;
        var white = ColorPalette.Paper * opacity;
        var r = radius;
        Vector2 At(float x, float y) => center + new Vector2(MathF.Cos(angle) * x - MathF.Sin(angle) * y,
            MathF.Sin(angle) * x + MathF.Cos(angle) * y) * r;
        void Rail(float x1, float y1, float x2, float y2, Color color, float width) =>
            p.Line(b, At(x1, y1), At(x2, y2), color, MathF.Max(1, width * r));

        const int mountSides = 6;
        const float mountRotation = MathHelper.PiOver2;
        var mountRadius = r * 1.05f;
        if (groundEffects)
        {
            var shadowCenter = center + new Vector2(Math.Clamp(r * .09f, .75f, 3), Math.Clamp(r * .15f, 1.25f, 5));
            var feather = Math.Clamp(r * .045f, .5f, 2);
            // The stationary mounting plate casts a short, feathered shadow away from the upper-left light.
            p.DrawPolygon(b, shadowCenter, mountRadius + feather * 2, mountSides, false,
                ColorPalette.TowerShadow * (.08f * opacity), mountRotation);
            p.DrawPolygon(b, shadowCenter, mountRadius + feather, mountSides, false,
                ColorPalette.TowerShadow * (.14f * opacity), mountRotation);
            p.DrawPolygon(b, shadowCenter, mountRadius, mountSides, false,
                ColorPalette.TowerShadow * (.32f * opacity), mountRotation);
            if (lights) p.Glow(b, center, r * 2.3f, accent, 0.20f);
        }
        p.DrawPolygon(b, center + new Vector2(0, Math.Clamp(r * .06f, .6f, 1.8f)), mountRadius,
            mountSides, false, dark, mountRotation);
        p.DrawPolygon(b, center, mountRadius, mountSides, false, metal, mountRotation);
        p.DrawPolygon(b, center, r * 0.88f, mountSides, false, dark, mountRotation);
        p.Ring(b, center, r * .57f, metal, Math.Max(1, (int)(r * .1f)));
        for (var side = -1; side <= 1; side += 2)
        {
            // Replaceable ceramic outriggers protect the common municipal mounting socket.
            Rail(-.7f, side * .58f, -.22f, side * .72f, ceramic, .17f);
            Rail(-.66f, side * .58f, -.48f, side * .63f, white * .65f, .05f);
            Rail(.23f, side * .7f, .52f, side * .6f, metal, .20f);
            Rail(-.55f, side * .57f, -.40f, side * .61f, dark, .06f);
        }
        p.Line(b, center + new Vector2(-r * .76f, -r * .38f),
            center + new Vector2(0, -r * .84f), ColorPalette.CardOutline * opacity, 1);
        p.Line(b, center + new Vector2(r * .76f, r * .38f),
            center + new Vector2(0, r * .84f), ColorPalette.Ink * opacity, 2);
        p.Line(b, center - new Vector2(0, r * .80f), center - new Vector2(0, r), metal, 3);
        if (apex) DrawApexSocket(b, p, center, r, accent, time, opacity, lights, groundEffects);

        switch (id)
        {
            case "pulse_plate":
                p.DrawPolygon(b, center, r * .8f, 4, false, metal, MathHelper.PiOver4);
                p.DrawPolygon(b, center, r * .56f, 4, false, accent, MathHelper.PiOver4);
                p.DrawPolygon(b, center, r * .39f, 4, false, dark, MathHelper.PiOver4);
                p.Line(b, center - new Vector2(r * .3f, 0), center + new Vector2(r * .3f, 0), white, 2);
                break;
            case "charge_forge":
                for (var i = -1; i <= 1; i++)
                {
                    Rail(-.6f, i * .42f, .6f, i * .42f, metal, .28f);
                    Rail(-.64f, i * .42f, -.46f, i * .42f, ceramic, .31f);
                    Rail(.46f, i * .42f, .64f, i * .42f, ceramic, .31f);
                    Rail(-.4f, i * .42f, .4f, i * .42f, accent, .10f);
                }
                p.Ring(b, center, r * .34f, white, 2);
                p.Circle(b, center, r * .18f, accent);
                break;
            case "needle_turret":
                p.Circle(b, At(-.24f, 0), r * .36f, metal);
                p.Circle(b, At(-.24f, 0), r * .24f, dark);
                Rail(-.49f, -.30f, -.05f, -.30f, ceramic, .13f);
                Rail(-.49f, .30f, -.05f, .30f, ceramic, .13f);
                Rail(-.40f, 0, -.08f, 0, accent, .09f);
                for (var side = -1; side <= 1; side += 2)
                {
                    Rail(-.16f, side * .23f, 1.10f - recoil, side * .23f, dark, .19f);
                    Rail(-.08f, side * .23f, 1.08f - recoil, side * .23f, metal, .12f);
                    Rail(.08f, side * .23f, 1.06f - recoil, side * .23f, side < 0 ? accent : white, .05f);
                    Rail(.05f, side * .23f, .25f, side * .23f, ceramic, .18f);
                    Rail(.92f - recoil, side * .23f, 1.08f - recoil, side * .23f, ceramic, .11f);
                }
                break;
            case "watchtower":
                Rail(-.52f, 0, .12f, 0, metal, .49f);
                Rail(-.45f, -.23f, .06f, -.23f, ceramic, .10f);
                Rail(-.45f, .23f, .06f, .23f, ceramic, .10f);
                Rail(-.12f, 0, 1.28f - recoil, 0, dark, .25f);
                Rail(-.06f, 0, 1.27f - recoil, 0, metal, .16f);
                Rail(.20f, 0, 1.24f - recoil, 0, white, .06f);
                Rail(.47f - recoil, 0, .64f - recoil, 0, ceramic, .25f);
                Rail(1.09f - recoil, 0, 1.28f - recoil, 0, accent, .11f);
                Rail(-.22f, -.15f, -.22f, -.48f, metal, .14f);
                Rail(-.43f, -.49f, .26f, -.49f, metal, .29f);
                Rail(-.36f, -.49f, .17f, -.49f, ceramic, .17f);
                p.Circle(b, At(.27f, -.49f), r * .17f, dark);
                p.Circle(b, At(.29f, -.49f), r * .11f, accent);
                p.Circle(b, At(.32f, -.51f), r * .04f, white);
                break;
            case "breaker_cannon":
                Rail(-.52f, 0, .22f, 0, metal, .94f);
                Rail(-.44f, 0, .04f, 0, ceramic, .66f);
                Rail(-.34f, 0, .13f, 0, dark, .45f);
                Rail(-.04f, 0, 1.01f - recoil, 0, dark, .68f);
                Rail(.03f, 0, .94f - recoil, 0, metal, .51f);
                for (var side = -1; side <= 1; side += 2)
                {
                    Rail(-.46f, side * .51f, .47f, side * .40f, ceramic, .19f);
                    Rail(-.17f, side * .48f, .38f - recoil, side * .29f, metal, .20f);
                    Rail(.13f, side * .19f, .67f - recoil, side * .19f, ceramic, .10f);
                    Rail(-.37f, side * .51f, -.17f, side * .49f, accent, .07f);
                }
                Rail(.76f - recoil, 0, 1.06f - recoil, 0, ceramic, .73f);
                Rail(.80f - recoil, 0, 1.06f - recoil, 0, metal, .47f);
                Rail(1.04f - recoil, -.23f, 1.04f - recoil, .23f, dark, .12f);
                Rail(.84f - recoil, -.30f, 1.01f - recoil, -.30f, accent, .07f);
                Rail(.84f - recoil, .30f, 1.01f - recoil, .30f, accent, .07f);
                break;
            case "shard_fan":
                for (var i = -1; i <= 1; i++)
                {
                    Rail(-.25f, i * .18f, .85f - recoil, i * .5f, metal, .3f);
                    Rail(-.20f, i * .18f, .36f, i * .35f, ceramic, .16f);
                    Rail(.55f, i * .39f, .98f - recoil, i * .54f, accent, .12f);
                }
                break;
            case "frost_spire":
                for (var i = 0; i < 4; i++)
                {
                    var a = i * MathHelper.PiOver2 + time * .28f;
                    var v = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    p.Line(b, center + v * r * .5f, center + v * r * .9f, metal, r * .3f);
                    p.Line(b, center + v * r * .42f, center + v * r * .86f, accent, r * .19f);
                    p.Circle(b, center + v * r * .89f, r * .10f, ceramic);
                }
                p.DrawPolygon(b, center, r * .48f, 4, false, ceramic, time * .3f);
                p.DrawPolygon(b, center, r * .36f, 4, false, dark, time * .3f);
                p.DrawPolygon(b, center, r * .27f, 4, false, accent, time * .3f);
                break;
            case "ember_coil":
                p.Ring(b, center, r * .66f, metal, 4);
                p.DashedRing(b, center, r * .63f, accent, 12, 3, time * .4f);
                p.Ring(b, center, r * .42f, dark, 3);
                p.Circle(b, center, r * .32f, accent);
                for (var sign = -1; sign <= 1; sign += 2)
                    Rail(-.33f, sign * .55f, .16f, sign * .55f, ceramic, .18f);
                Rail(.3f, 0, .95f - recoil, 0, white, .20f);
                break;
            case "arc_relay":
                for (var i = -1; i <= 1; i++)
                {
                    var at = At(i == 0 ? .6f : -.35f, i * .65f);
                    p.Line(b, center, at, metal, r * .23f);
                    p.Circle(b, at, r * .26f, ceramic);
                    p.Circle(b, at, r * .23f, dark);
                    p.Circle(b, at, r * .21f, accent);
                    p.Circle(b, at, r * .09f, white);
                }
                p.Circle(b, center, r * .24f, white);
                break;
            case "siege_mortar":
                p.DrawPolygon(b, center, r * .76f, 4, false, metal, MathHelper.PiOver4);
                p.Circle(b, center, r * .53f, accent);
                p.Ring(b, center, r * .53f, ceramic, Math.Max(1, (int)(r * .11f)));
                p.Circle(b, center, r * .37f, dark);
                p.Circle(b, center, r * (.17f + recoil * .2f), white);
                for (var i = -1; i <= 1; i += 2) Rail(-.55f, i * .62f, .5f, i * .62f, accent, .10f);
                break;
            case "prism_beam":
                Rail(-.55f, -.5f, .7f, -.5f, ceramic, .23f);
                Rail(-.55f, .5f, .7f, .5f, ceramic, .23f);
                Rail(-.2f, -.5f, .35f, -.5f, dark, .08f);
                Rail(-.2f, .5f, .35f, .5f, dark, .08f);
                p.DrawPolygon(b, center, r * .58f, 4, false, accent, angle);
                p.DrawPolygon(b, center, r * .3f, 4, false, white, angle);
                Rail(.48f, 0, PrismBeamArt.BarrelLength, 0, accent, .17f);
                break;
            case "signal_beacon":
                p.Ring(b, center, r * .64f, accent, 2);
                p.DashedRing(b, center, r * .82f, accent, 12, 2, time * .5f);
                p.DrawPolygon(b, center, r * .42f, 3, false, accent, -MathHelper.PiOver2);
                Rail(-.48f, -.51f, .30f, -.51f, ceramic, .13f);
                Rail(-.48f, .51f, .30f, .51f, ceramic, .13f);
                p.Line(b, center + new Vector2(0, r * .2f), center - new Vector2(0, r * .7f), white, 2);
                break;
        }
        DrawTierIndicators(b, p, center, r, level, accent, opacity, lights && groundEffects);
        if (recoil > .02f && lights && id != "prism_beam")
        {
            var muzzle = At(1.2f - recoil, 0);
            p.Glow(b, muzzle, r * 1.8f, accent, recoil * 2);
            p.Circle(b, muzzle, 2 + recoil * 7, white);
        }
    }

    private static void DrawTierIndicators(SpriteBatch b, PrimitiveRenderer p, Vector2 center,
        float radius, int level, Color accent, float opacity, bool lights)
    {
        var socket = ColorPalette.Navy * opacity;
        var inactive = ColorPalette.Metal * opacity;
        // Tier lights stay on the mounting plate, independent of the weapon's aim.
        for (var side = -1; side <= 1; side += 2)
        {
            var upper = center + new Vector2(side * .74f, -.40f) * radius;
            var dotRadius = MathF.Max(.8f, radius * .085f);
            p.Circle(b, upper, dotRadius + MathF.Max(.65f, radius * .045f), socket);
            if (level >= 2 && lights) p.Glow(b, upper, dotRadius * 3.6f, accent, .32f);
            p.Circle(b, upper, dotRadius, level >= 2 ? accent : inactive);

            var start = center + new Vector2(side * .46f, .75f) * radius;
            var end = center + new Vector2(side * .75f, .58f) * radius;
            var width = MathF.Max(1.1f, radius * .09f);
            p.Line(b, start, end, socket, width + MathF.Max(1.3f, radius * .09f));
            if (level >= 3 && lights)
                p.Glow(b, (start + end) * .5f, radius * .38f, accent, .28f);
            p.Line(b, start, end, level >= 3 ? accent : inactive, width);
        }
    }

    private static void DrawApexSocket(SpriteBatch b, PrimitiveRenderer p, Vector2 center,
        float radius, Color accent, float time, float opacity, bool lights, bool groundEffects)
    {
        var pulse = lights ? .83f + MathF.Sin(time * 2.4f) * .17f : 1f;
        var width = MathF.Max(1.25f, radius * .075f);
        // Six separated power rails follow the hexagonal armor, leaving the core and status rings clear.
        for (var edge = 0; edge < 6; edge++)
        {
            var firstAngle = MathHelper.PiOver2 + edge * MathHelper.TwoPi / 6;
            var secondAngle = firstAngle + MathHelper.TwoPi / 6;
            var first = center + new Vector2(MathF.Cos(firstAngle), MathF.Sin(firstAngle)) * radius * 1.18f;
            var second = center + new Vector2(MathF.Cos(secondAngle), MathF.Sin(secondAngle)) * radius * 1.18f;
            var start = Vector2.Lerp(first, second, .18f);
            var end = Vector2.Lerp(first, second, .82f);
            p.Line(b, start, end, ColorPalette.Navy * opacity, width + 1.8f);
            if (lights && groundEffects) p.Glow(b, (start + end) * .5f, radius * .58f, accent, .36f * pulse);
            p.Line(b, start, end, accent * pulse, width);
        }
    }

    public static void Enemy(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float r, string id,
        float angle, float time, bool boss = false, float hit = 0, bool lights = true)
    {
        var accent = boss ? ColorPalette.Gold : ColorPalette.Hostile;
        var armor = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, Math.Clamp(hit, 0, 1));
        var shell = Color.Lerp(ColorPalette.Metal, ColorPalette.Muted, .37f + Math.Clamp(hit, 0, 1) * .4f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var side = new Vector2(-direction.Y, direction.X);
        var fast = id.Contains("runner", StringComparison.Ordinal);
        var heavy = id.Contains("brute", StringComparison.Ordinal) || id.Contains("aegis", StringComparison.Ordinal) || boss;
        if (lights) p.Glow(b, center, r * 1.9f, accent, .19f);
        p.DrawPolygon(b, center + new Vector2(2, 3), r * 1.12f, heavy ? 6 : 3, false, ColorPalette.Canvas, angle);
        if (fast)
        {
            p.Line(b, center - direction * r, center - direction * r * 1.65f, accent * .45f, 3);
            p.DrawPolygon(b, center, r, 3, false, armor, angle);
            p.DrawPolygon(b, center - direction * r * .1f, r * .7f, 3, false, ColorPalette.Navy, angle);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var nacelle = center - direction * r * .35f + side * r * .48f * sign;
                p.Line(b, nacelle - direction * r * .16f, nacelle + direction * r * .39f, shell, r * .16f);
                p.Circle(b, nacelle - direction * r * .17f, r * .09f, accent);
            }
            p.Line(b, center - side * r * .4f, center + direction * r * .7f, accent, 2);
            p.Line(b, center + side * r * .4f, center + direction * r * .7f, accent, 2);
        }
        else
        {
            for (var s = -1; s <= 1; s += 2)
            for (var i = -1; i <= 1; i++)
            {
                var root = center + side * r * .5f * s + direction * r * i * .4f;
                var foot = root + side * r * .5f * s + direction * MathF.Sin(time * 9 + i * 2) * (heavy ? 1 : 3);
                var knee = Vector2.Lerp(root, foot, .55f) - direction * r * .13f;
                p.Line(b, root, knee, armor, heavy ? 5 : 3);
                p.Line(b, knee, foot, shell, heavy ? 3 : 2);
                p.Circle(b, foot, heavy ? 2 : 1.5f, accent * .75f);
            }
            p.DrawPolygon(b, center, r * .85f, heavy ? 6 : 4, false, armor, angle);
            p.DrawPolygon(b, center, r * .63f, heavy ? 6 : 4, false, ColorPalette.Navy, angle);
            p.Line(b, center - direction * r * .48f, center + direction * r * .18f, armor, r * .33f);
            p.Line(b, center - direction * r * .48f, center + direction * r * .18f, accent * .5f, r * .075f);
            if (heavy)
            {
                for (var s = -1; s <= 1; s += 2)
                {
                    var shoulder = center + side * r * .65f * s;
                    p.Line(b, shoulder - direction * r * .44f, shoulder + direction * r * .35f, shell, r * .25f);
                    p.Line(b, shoulder - direction * r * .03f, shoulder + direction * r * .09f, ColorPalette.Navy, r * .28f);
                    p.Line(b, shoulder - direction * r * .3f, shoulder - direction * r * .08f, accent * .7f, 2);
                }
            }
            else
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var flank = center + side * r * .42f * sign;
                    p.Line(b, flank - direction * r * .20f, flank + direction * r * .16f, shell, r * .18f);
                }
            }
            p.Line(b, center + direction * r * .4f - side * r * .36f,
                center + direction * r * .4f + side * r * .36f, accent, 3);
            if (id.Contains("regenerator", StringComparison.Ordinal))
            {
                p.DashedRing(b, center, r * .72f, ColorPalette.Green, 12, 2, -time * .5f);
                for (var rib = -1; rib <= 1; rib++)
                    p.Line(b, center + direction * r * rib * .3f - side * r * .3f,
                        center + direction * r * rib * .3f + side * r * .3f, armor, 2);
            }
            if (id.Contains("aegis", StringComparison.Ordinal))
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var emitter = center + direction * r * .12f + side * r * .66f * sign;
                    p.Line(b, emitter - direction * r * .32f, emitter + direction * r * .30f, ColorPalette.Navy, r * .13f);
                    p.Line(b, emitter - direction * r * .24f, emitter + direction * r * .22f, ColorPalette.Violet, r * .065f);
                }
            }
        }
        p.Circle(b, center, r * .24f, ColorPalette.Ink);
        p.Circle(b, center, r * .17f, ColorPalette.Hostile);
        p.Line(b, center - side * r * .08f, center + side * r * .05f, ColorPalette.Paper * .6f, 1);
        if (boss)
        {
            p.Ring(b, center, r * .43f, accent, 2);
            p.DashedRing(b, center, r + 5, accent, 16, 2, time * .24f);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var rear = center - direction * r * .56f + side * r * .27f * sign;
                p.Line(b, rear, rear - direction * r * .4f, shell, r * .15f);
                p.Circle(b, rear - direction * r * .42f, r * .075f, ColorPalette.Hostile);
            }
        }
    }

}
