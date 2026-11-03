using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Shared mechanical silhouettes and infrastructure for the field and tactical displays.</summary>
public static partial class NightGridArt
{
    public static Color TowerAccent(string id) => id switch
    {
        "needle_turret" => ColorPalette.NeedleShot,
        "arc_relay" => ColorPalette.ArcShot,
        "watchtower" => ColorPalette.PrecisionShot,
        "shard_fan" => ColorPalette.ShardShot,
        "frost_spire" => ColorPalette.FrostShot,
        "ember_coil" => ColorPalette.EmberShot,
        "breaker_cannon" => ColorPalette.BreakerShot,
        "siege_mortar" => ColorPalette.MortarShot,
        "pulse_plate" => ColorPalette.Gold,
        "prism_beam" => ColorPalette.Violet,
        "signal_beacon" => ColorPalette.BeaconEnergy,
        "charge_forge" => ColorPalette.Green,
        _ => ColorPalette.Cyan
    };

    public static void Tower(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius, string id,
        int level = 1, float angle = -MathHelper.PiOver2, float time = 0, float recoil = 0,
        float opacity = 1, bool lights = true, bool groundEffects = true, bool apex = false, float power = 1)
    {
        if (id is not "pulse_plate" and not "charge_forge")
        {
            DrawTowerModel(b, p, center, radius, id, level, angle, time, recoil, opacity, lights, groundEffects, apex, power);
            return;
        }
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
        p.Line(b, center + new Vector2(-r * .76f, -r * .38f),
            center + new Vector2(0, -r * .84f), ColorPalette.CardOutline * opacity, 1);
        p.Line(b, center + new Vector2(r * .76f, r * .38f),
            center + new Vector2(0, r * .84f), ColorPalette.Ink * opacity, 2);
        p.Line(b, center - new Vector2(0, r * .80f), center - new Vector2(0, r), metal, 3);
        if (apex) DrawApexSocket(b, p, center, r, accent, time, opacity, lights, groundEffects);
        DrawTierIndicators(b, p, center, r, level, accent, opacity, lights && groundEffects);

        // Rotating armor and weapons occlude the mounting plate's tier lights.
        for (var side = -1; side <= 1; side += 2)
        {
            Rail(-.7f, side * .58f, -.22f, side * .72f, ceramic, .17f);
            Rail(-.66f, side * .58f, -.48f, side * .63f, white * .65f, .05f);
            Rail(.23f, side * .7f, .52f, side * .6f, metal, .20f);
            Rail(-.55f, side * .57f, -.40f, side * .61f, dark, .06f);
        }

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

        }
        if (lights && id != "prism_beam")
            DrawMuzzleFlash(b, p, At(1.2f - recoil, 0), r, accent, recoil, opacity);
    }

    public static void DrawMuzzleFlash(SpriteBatch b, PrimitiveRenderer p, Vector2 muzzle,
        float radius, Color accent, float recoil, float opacity = 1)
    {
        if (recoil <= .02f) return;
        p.Glow(b, muzzle, radius * 1.8f, accent, recoil * 2);
        p.Circle(b, muzzle, 2 + recoil * 7, ColorPalette.Paper * opacity);
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

}
