using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public static partial class NightGridArt
{
    private static void DrawTowerModel(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center,
        float radius, string id, int level, float angle, float time, float recoil, float opacity,
        bool lights, bool groundEffects, bool apex)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var accent = TowerAccent(id) * opacity;
        var brush = new TowerBrush(batch, primitives, center, radius, angle, opacity, accent);
        DrawTowerFoundation(batch, primitives, center, radius, id, stage, accent, opacity, lights, groundEffects);
        DrawModelTierIndicators(batch, primitives, center, radius, id, stage, accent, opacity, lights && groundEffects);

        // Weapon assemblies render above every stationary foundation indicator.
        switch (id)
        {
            case "needle_turret": DrawNeedle(brush, stage, recoil); break;
            case "watchtower": DrawWatchtower(brush, stage, recoil); break;
            case "breaker_cannon": DrawBreaker(brush, stage, recoil); break;
            case "shard_fan": DrawShard(brush, stage, recoil); break;
            case "frost_spire": DrawFrost(brush, stage, time); break;
            case "ember_coil": DrawEmber(brush, stage, recoil, time); break;
            case "arc_relay": DrawArc(brush, stage, time); break;
            case "siege_mortar": DrawMortar(brush, stage, recoil); break;
            case "prism_beam": DrawPrism(brush, stage, level, apex); break;
            case "signal_beacon": DrawBeacon(brush, stage, time); break;
        }

        if (!lights || id == "prism_beam") return;
        var muzzle = TowerMuzzlePosition(center, radius, id, level, angle, recoil, apex);
        DrawMuzzleFlash(batch, primitives, muzzle, radius, accent, recoil, opacity);
        TowerAttackArt.DrawMuzzle(batch, primitives, id, muzzle,
            new Vector2(MathF.Cos(angle), MathF.Sin(angle)), accent,
            Math.Clamp(recoil / .2f, 0, 1), false, level, apex);
    }

    public static Vector2 TowerMuzzlePosition(Vector2 center, float radius, string id, int level,
        float angle, float recoil = 0, bool apex = false)
    {
        var stage = TowerTierVisuals.Stage(level, apex);
        var reach = id switch
        {
            "needle_turret" => NeedleLength(stage) - recoil,
            "watchtower" => WatchLength(stage) - recoil,
            "breaker_cannon" => BreakerLength(stage) - recoil,
            "shard_fan" => ShardLength(stage) - recoil,
            "ember_coil" => TowerTierVisuals.EmberNozzleLength(stage) - recoil,
            "prism_beam" => TowerTierVisuals.PrismBarrelLength(level, apex),
            "frost_spire" => .65f,
            "arc_relay" => .70f,
            _ => 0
        };
        return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * reach;
    }

    private static float NeedleLength(int stage) => stage switch { 1 => 1.00f, 2 => 1.12f, 3 => 1.25f, _ => 1.34f };
    private static float WatchLength(int stage) => stage switch { 1 => 1.22f, 2 => 1.36f, 3 => 1.48f, _ => 1.58f };
    private static float BreakerLength(int stage) => stage switch { 1 => .96f, 2 => 1.03f, 3 => 1.12f, _ => 1.18f };
    private static float ShardLength(int stage) => stage switch { 1 => .92f, 2 => 1.00f, 3 => 1.12f, _ => 1.20f };

    private static void DrawModelTierIndicators(SpriteBatch b, PrimitiveRenderer p, Vector2 center,
        float radius, string id, int stage, Color accent, float opacity, bool lights)
    {
        var triangular = id is "arc_relay" or "signal_beacon" or "shard_fan";
        var socket = ColorPalette.Navy * opacity;
        var inactive = ColorPalette.Metal * opacity;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var at = center + (triangular ? new Vector2(sign * .62f, .23f) : new Vector2(sign * .55f, -.36f)) * radius;
            var size = MathF.Max(.8f, radius * .085f);
            p.Circle(b, at, size + MathF.Max(.65f, radius * .045f), socket);
            if (stage >= 2 && lights) p.Glow(b, at, size * 3.6f, accent, .32f);
            p.Circle(b, at, size, stage >= 2 ? accent : inactive);
            var start = center + (triangular ? new Vector2(sign * .18f, .43f) : new Vector2(sign * .32f, .62f)) * radius;
            var end = center + (triangular ? new Vector2(sign * .50f, .43f) : new Vector2(sign * .59f, .46f)) * radius;
            var width = MathF.Max(1.1f, radius * .09f);
            p.Line(b, start, end, socket, width + MathF.Max(1.3f, radius * .09f));
            if (stage >= 3 && lights) p.Glow(b, (start + end) * .5f, radius * .38f, accent, .28f);
            p.Line(b, start, end, stage >= 3 ? accent : inactive, width);
        }
    }

    private static void DrawTowerFoundation(SpriteBatch b, PrimitiveRenderer p, Vector2 center,
        float radius, string id, int stage, Color accent, float opacity, bool lights, bool groundEffects)
    {
        var sides = id switch
        {
            "watchtower" or "frost_spire" => 4,
            "arc_relay" or "signal_beacon" or "shard_fan" => 3,
            "ember_coil" or "siege_mortar" => 8,
            _ => 6
        };
        var rotation = sides == 4 ? MathHelper.PiOver4 : -MathHelper.PiOver2;
        var mountSize = stage switch { 1 => .89f, 2 => .99f, 3 => 1.06f, _ => 1.12f };
        var triangular = sides == 3;
        var supports = stage < 2 ? 0 : triangular ? 3 : stage >= 3 ? 4 : 2;
        var supportRotation = triangular ? -MathHelper.PiOver2 : supports == 2 ? MathHelper.PiOver2 : MathHelper.PiOver4;

        void Footprint(Vector2 at, float expansion, Color color)
        {
            p.DrawPolygon(b, at, radius * mountSize + expansion, sides, false, color, rotation);
            for (var i = 0; i < supports; i++)
            {
                var a = supportRotation + i * MathHelper.TwoPi / supports;
                var axis = new Vector2(MathF.Cos(a), MathF.Sin(a));
                p.Line(b, at + axis * radius * .69f,
                    at + axis * (radius * (stage == 4 ? 1.18f : 1.08f) + expansion), color,
                    radius * (stage == 4 ? .27f : .23f) + expansion * 2);
            }
        }

        if (groundEffects)
        {
            var shadow = center + new Vector2(Math.Clamp(radius * .09f, .75f, 3), Math.Clamp(radius * .15f, 1.25f, 5));
            var feather = Math.Clamp(radius * .045f, .5f, 2);
            Footprint(shadow, feather * 2, ColorPalette.TowerShadow * (.08f * opacity));
            Footprint(shadow, feather, ColorPalette.TowerShadow * (.14f * opacity));
            Footprint(shadow, 0, ColorPalette.TowerShadow * (.32f * opacity));
            if (lights) p.Glow(b, center, radius * (stage == 4 ? 2.15f : 1.9f), accent, stage == 4 ? .23f : .15f);
        }

        Footprint(center + new Vector2(0, Math.Clamp(radius * .06f, .6f, 1.8f)), 0, ColorPalette.Navy * opacity);
        Footprint(center, 0, ColorPalette.Metal * opacity);
        p.DrawPolygon(b, center, radius * (mountSize - .15f), sides, false, ColorPalette.Navy * opacity, rotation);
        p.Ring(b, center, radius * .46f, ColorPalette.CardOutline * opacity, Math.Max(1, (int)(radius * .08f)));
        if (stage < 3) return;
        for (var side = -1; side <= 1; side += 2)
        {
            var start = center + new Vector2(side * .47f, .75f) * radius;
            var end = center + new Vector2(side * .72f, .60f) * radius;
            p.Line(b, start, end, ColorPalette.Muted * opacity, MathF.Max(1, radius * .09f));
        }
    }

    private static void DrawNeedle(TowerBrush d, int stage, float recoil)
    {
        var end = NeedleLength(stage) - recoil;
        var spread = stage == 1 ? .16f : stage == 2 ? .21f : .26f;
        d.Disc(-.26f, 0, stage == 1 ? .29f : .38f, d.Metal);
        d.Disc(-.26f, 0, .20f, d.Dark);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            d.Rail(-.18f, sign * spread, end, sign * spread, d.Dark, .19f);
            d.Rail(-.12f, sign * spread, end, sign * spread, d.Metal, .12f);
            d.Rail(.10f, sign * spread, end - .03f, sign * spread, sign < 0 ? d.Accent : d.White, .055f);
            if (stage >= 2)
            {
                d.Rail(-.62f, sign * .48f, -.03f, sign * .46f, d.Ceramic, .23f);
                d.Rail(-.52f, sign * .48f, -.23f, sign * .47f, d.Accent, .075f);
                d.Rail(.08f, sign * spread, .30f, sign * spread, d.Ceramic, .22f);
            }
            if (stage >= 3)
            {
                d.Rail(-.52f, sign * .53f, .54f, sign * .46f, d.Metal, .19f);
                d.Rail(.18f, sign * .46f, .59f, sign * .42f, d.Ceramic, .14f);
                d.Rail(end - .27f, sign * spread, end - .07f, sign * spread, d.Ceramic, .20f);
            }
            if (stage == 4)
            {
                d.Rail(-.67f, sign * .72f, -.12f, sign * .62f, d.Ceramic, .22f);
                d.Rail(-.56f, sign * .72f, -.20f, sign * .65f, d.Accent, .09f);
                d.Rail(.45f, sign * .42f, 1.02f - recoil, sign * .35f, d.Accent, .085f);
            }
        }
        d.Rail(-.48f, 0, -.02f, 0, d.Accent, stage >= 3 ? .14f : .09f);
        if (stage == 4) d.Diamond(-.26f, 0, .19f, d.White);
    }

    private static void DrawWatchtower(TowerBrush d, int stage, float recoil)
    {
        var end = WatchLength(stage) - recoil;
        d.Rail(-.49f, 0, .15f, 0, d.Metal, stage >= 3 ? .62f : .44f);
        d.Rail(-.39f, 0, .08f, 0, d.Ceramic, stage >= 3 ? .39f : .28f);
        d.Rail(-.10f, 0, end, 0, d.Dark, stage == 4 ? .31f : .25f);
        d.Rail(-.03f, 0, end, 0, d.Metal, stage == 4 ? .22f : .16f);
        d.Rail(.19f, 0, end - .02f, 0, d.White, .06f);
        d.Rail(end - .22f, 0, end - .02f, 0, d.Accent, .10f);
        if (stage == 1)
        {
            d.Rail(-.26f, -.25f, .15f, -.25f, d.Dark, .15f);
            d.Disc(.17f, -.25f, .10f, d.Accent);
            return;
        }
        d.Rail(-.40f, -.44f, .27f, -.44f, d.Metal, .29f);
        d.Rail(-.35f, -.44f, .13f, -.44f, d.Ceramic, .17f);
        d.Disc(.28f, -.44f, .18f, d.Dark);
        d.Disc(.30f, -.44f, .115f, d.Accent);
        d.Disc(.33f, -.47f, .035f, d.White);
        d.Rail(-.37f, .40f, .27f, .35f, d.Ceramic, .19f);
        if (stage >= 3)
        {
            d.Rail(-.72f, -.56f, -.19f, -.56f, d.Ceramic, .22f);
            d.Rail(-.72f, .52f, -.19f, .49f, d.Metal, .23f);
            d.Rail(.38f, -.19f, end - .29f, -.19f, d.Ceramic, .11f);
            d.Rail(.38f, .19f, end - .29f, .19f, d.Ceramic, .11f);
        }
        if (stage == 4)
        {
            d.Rail(-.87f, -.32f, -.49f, -.32f, d.Accent, .16f);
            d.Rail(-.87f, .32f, -.49f, .32f, d.Accent, .16f);
            d.Rail(.28f, -.42f, .97f, -.30f, d.Metal, .20f);
            d.Rail(.28f, .42f, .97f, .30f, d.Metal, .20f);
            d.Rail(.42f, -.42f, .75f, -.36f, d.Accent, .07f);
            d.Rail(.42f, .42f, .75f, .36f, d.Accent, .07f);
        }
    }

    private static void DrawBreaker(TowerBrush d, int stage, float recoil)
    {
        var end = BreakerLength(stage) - recoil;
        d.Rail(-.52f, 0, .20f, 0, d.Metal, stage == 1 ? .70f : .96f);
        d.Rail(-.43f, 0, .02f, 0, d.Ceramic, stage == 1 ? .48f : .66f);
        d.Rail(-.35f, 0, .10f, 0, d.Dark, .36f);
        d.Rail(-.02f, 0, end, 0, d.Dark, stage == 1 ? .48f : .65f);
        d.Rail(.02f, 0, end - .05f, 0, d.Metal, stage == 1 ? .33f : .48f);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            d.Rail(-.41f, sign * .35f, -.13f, sign * .35f, d.Accent, .075f);
            if (stage >= 2)
            {
                d.Rail(-.57f, sign * .55f, .43f, sign * .42f, d.Ceramic, .22f);
                d.Rail(-.37f, sign * .56f, -.13f, sign * .52f, d.Accent, .08f);
                d.Rail(.13f, sign * .18f, end - .30f, sign * .18f, d.Ceramic, .10f);
            }
            if (stage >= 3)
            {
                d.Rail(-.76f, sign * .67f, -.30f, sign * .67f, d.Metal, .30f);
                d.Rail(-.67f, sign * .67f, -.36f, sign * .67f, d.Accent, .10f);
                d.Rail(-.10f, sign * .53f, .57f - recoil, sign * .40f, d.Metal, .21f);
            }
            if (stage == 4)
            {
                d.Rail(-.62f, sign * .84f, .21f, sign * .72f, d.Ceramic, .25f);
                d.Rail(-.47f, sign * .84f, -.12f, sign * .79f, d.Accent, .10f);
            }
        }
        d.Rail(end - .29f, 0, end, 0, d.Ceramic, stage == 1 ? .56f : stage == 4 ? .90f : .74f);
        d.Rail(end - .24f, 0, end, 0, d.Metal, stage == 1 ? .34f : .48f);
        d.Rail(end - .03f, -.19f, end - .03f, .19f, d.Dark, .11f);
        if (stage == 4) d.Rail(-.48f, 0, -.03f, 0, d.Accent, .21f);
    }

    private static void DrawShard(TowerBrush d, int stage, float recoil)
    {
        var end = ShardLength(stage) - recoil;
        if (stage >= 2)
        {
            d.Poly(-.20f, 0, stage == 4 ? .69f : .54f, 3, d.Metal);
            d.Poly(-.20f, 0, stage == 4 ? .47f : .35f, 3, d.Dark);
        }
        for (var i = -1; i <= 1; i++)
        {
            var spread = stage == 1 ? .43f : stage == 4 ? .69f : .57f;
            d.Rail(-.25f, i * .12f, end, i * spread, d.Metal, stage >= 3 ? .34f : .25f);
            d.Rail(-.17f, i * .12f, end - .27f, i * spread * .80f, d.Ceramic, stage >= 3 ? .19f : .13f);
            d.Rail(end - .28f, i * spread * .84f, end, i * spread, d.Accent, .12f);
            if (stage >= 3)
            {
                d.Rail(end - .45f, i * spread * .70f, end - .23f, i * spread * .86f, d.Dark, .29f);
                d.Rail(end - .42f, i * spread * .72f, end - .26f, i * spread * .84f, d.Ceramic, .23f);
            }
        }
        if (stage >= 2)
        {
            d.Rail(-.69f, -.42f, -.19f, -.28f, d.Ceramic, .25f);
            d.Rail(-.69f, .42f, -.19f, .28f, d.Ceramic, .25f);
            d.Disc(-.30f, 0, .17f, d.Accent);
        }
        if (stage == 4)
        {
            d.Rail(-.60f, -.68f, .26f, -.78f, d.Metal, .25f);
            d.Rail(-.60f, .68f, .26f, .78f, d.Metal, .25f);
            d.Rail(-.46f, -.68f, .14f, -.76f, d.Accent, .10f);
            d.Rail(-.46f, .68f, .14f, .76f, d.Accent, .10f);
            d.Diamond(-.30f, 0, .12f, d.White);
        }
    }

    private static void DrawFrost(TowerBrush d, int stage, float time)
    {
        var rotor = TowerTierVisuals.FrostRotorRadius(stage);
        var offset = TowerTierVisuals.FrostCoolantOffset(stage);
        var length = TowerTierVisuals.FrostCoolantLength(stage);
        var banks = TowerTierVisuals.FrostCoolantBanks(stage);
        var spin = time * .22f;
        // Fixed coolant banks attach to the bearing outside the crystal's full swept radius.
        for (var i = 0; i < banks; i++)
        {
            var a = MathHelper.PiOver2 + i * MathHelper.TwoPi / banks;
            var x = MathF.Cos(a);
            var y = MathF.Sin(a);
            d.Rail(x * (rotor + .10f), y * (rotor + .10f), x * offset, y * offset, d.Metal, .22f);
            d.Rail(x * offset + y * length, y * offset - x * length,
                x * offset - y * length, y * offset + x * length, d.Dark, .30f + stage * .02f);
            d.Rail(x * offset + y * length, y * offset - x * length,
                x * offset - y * length, y * offset + x * length, d.Ceramic, .20f + stage * .02f);
            d.Rail(x * offset + y * length * .69f, y * offset - x * length * .69f,
                x * offset - y * length * .69f, y * offset + x * length * .69f, d.Dark, .11f);
            d.Rail(x * offset + y * length * .63f, y * offset - x * length * .63f,
                x * offset - y * length * .63f, y * offset + x * length * .63f, d.Accent, .055f);
            if (stage >= 3)
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var end = sign * length * .88f;
                    d.Rail(x * (offset - .17f) - y * end, y * (offset - .17f) + x * end,
                        x * (offset + .17f) - y * end, y * (offset + .17f) + x * end, d.Metal, .11f);
                }
            }
        }
        if (stage == 4)
        {
            for (var i = 0; i < 4; i++)
            {
                var a = MathHelper.PiOver4 + i * MathHelper.PiOver2;
                var x = MathF.Cos(a);
                var y = MathF.Sin(a);
                d.Rail(x * .64f, y * .64f, x * 1.10f, y * 1.10f, d.Metal, .22f);
                d.Rail(x * .81f, y * .81f, x * 1.10f, y * 1.10f, d.Ceramic, .13f);
                d.Diamond(x * 1.06f, y * 1.06f, .12f, d.Accent, a);
            }
        }
        d.Poly(0, 0, rotor + .14f, 8, d.Metal, MathHelper.PiOver4);
        d.Disc(0, 0, rotor + .075f, d.Dark);
        d.Diamond(0, 0, rotor, d.Ceramic, spin);
        d.Diamond(0, 0, rotor - .07f, d.Dark, spin);
        d.Diamond(0, 0, rotor - .12f, d.Accent, spin);
        if (stage == 4) d.Diamond(0, 0, .13f, d.White, spin);
    }

    private static void DrawEmber(TowerBrush d, int stage, float recoil, float time)
    {
        var size = TowerTierVisuals.EmberChamberRadius(stage);
        var nozzle = TowerTierVisuals.EmberNozzleLength(stage) - recoil;
        if (stage == 4)
        {
            d.Poly(0, 0, .87f, 6, d.Ceramic);
            d.Poly(0, 0, .74f, 6, d.Dark);
        }
        if (stage >= 2)
        {
            for (var sign = -1; sign <= 1; sign += 2)
            {
                d.Rail(-.61f, sign * .57f, .18f, sign * .57f, d.Metal, .30f);
                d.Rail(-.54f, sign * .57f, -.12f, sign * .57f, d.Ceramic, .19f);
                d.Rail(-.43f, sign * .57f, -.19f, sign * .57f, d.Accent, .09f);
            }
        }
        if (stage >= 3)
        {
            for (var sign = -1; sign <= 1; sign += 2)
            {
                d.Rail(-.67f, sign * .72f, .43f, sign * .72f, d.Metal, .20f);
                d.Rail(.02f, sign * .72f, .40f, sign * .72f, d.Ceramic, .14f);
            }
        }
        if (stage == 4)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = -.56f + i * .32f;
                d.Rail(x, -.77f, x, -.53f, d.Accent, .10f);
                d.Rail(x, .77f, x, .53f, d.Accent, .10f);
            }
        }
        // The short nozzle seats beneath the concentric chamber on the aiming swivel.
        d.Rail(size - .15f, 0, nozzle, 0, d.Metal, stage >= 3 ? .35f : .25f);
        d.Rail(size - .08f, 0, nozzle, 0, d.Ceramic, stage >= 3 ? .22f : .16f);
        d.Rail(size, 0, nozzle, 0, d.Accent, .10f);
        d.Ring(0, 0, size, d.Metal, .16f);
        d.DashedRing(0, 0, size - .05f, d.Accent, stage == 1 ? 6 : 8, .10f, time * .35f);
        d.Disc(0, 0, size * .65f, d.Dark);
        d.Disc(0, 0, size * .42f, d.Accent);
        if (stage == 4) d.Disc(0, 0, .13f, d.White);
    }

    private static void DrawArc(TowerBrush d, int stage, float time)
    {
        var poles = stage == 1 ? 2 : 3;
        if (stage >= 3)
        {
            d.Poly(-.08f, 0, stage == 4 ? .82f : .68f, 3, d.Ceramic);
            d.Poly(-.08f, 0, stage == 4 ? .65f : .51f, 3, d.Dark);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                d.Rail(-.80f, sign * .30f, -.60f, sign * .72f, d.Metal, .35f);
                d.Rail(-.78f, sign * .35f, -.64f, sign * .64f, d.Ceramic, .23f);
                d.Rail(-.76f, sign * .37f, -.66f, sign * .59f, d.Accent, .10f);
            }
        }
        for (var i = 0; i < poles; i++)
        {
            var a = stage == 1 ? (i == 0 ? -.8f : .8f) : i * MathHelper.TwoPi / 3;
            var x = MathF.Cos(a) * (stage == 4 ? .87f : .68f);
            var y = MathF.Sin(a) * (stage == 4 ? .87f : .68f);
            d.Rail(-.20f, 0, x, y, d.Metal, stage >= 3 ? .30f : .21f);
            d.Disc(x, y, stage == 1 ? .21f : stage == 4 ? .30f : .26f, d.Ceramic);
            d.Disc(x, y, stage == 1 ? .15f : stage == 4 ? .23f : .19f, d.Dark);
            d.Disc(x, y, stage == 1 ? .10f : stage == 4 ? .17f : .14f, d.Accent);
            if (stage >= 3) d.Disc(x + .025f, y - .025f, .065f, d.White);
        }
        if (stage >= 2)
        {
            d.Rail(-.67f, -.26f, -.30f, -.20f, d.Ceramic, .18f);
            d.Rail(-.67f, .26f, -.30f, .20f, d.Ceramic, .18f);
        }
        d.Poly(-.18f, 0, stage == 1 ? .22f : .30f, 3, d.Metal);
        d.Poly(-.18f, 0, stage == 1 ? .13f : .20f, 3, d.Accent);
        if (stage == 4)
        {
            d.Rail(-.87f, -.42f, -.67f, -.72f, d.Ceramic, .25f);
            d.Rail(-.87f, .42f, -.67f, .72f, d.Ceramic, .25f);
            d.Rail(-.83f, -.45f, -.71f, -.64f, d.Accent, .10f);
            d.Rail(-.83f, .45f, -.71f, .64f, d.Accent, .10f);
            d.Disc(-.18f, 0, .095f, d.White);
        }
    }

    private static void DrawMortar(TowerBrush d, int stage, float recoil)
    {
        var well = stage == 1 ? .40f : stage == 2 ? .48f : stage == 3 ? .56f : .60f;
        d.Poly(0, 0, well + .19f, stage == 1 ? 4 : 8, d.Metal, MathHelper.PiOver4);
        if (stage >= 2)
        {
            var banks = stage == 2 ? 2 : 4;
            var offset = TowerTierVisuals.MortarBankOffset(stage);
            var length = stage == 2 ? .42f : stage == 3 ? .43f : .52f;
            for (var i = 0; i < banks; i++)
            {
                var a = MathHelper.PiOver2 + i * MathHelper.TwoPi / banks;
                var x = MathF.Cos(a);
                var y = MathF.Sin(a);
                d.Rail(x * (well + .05f), y * (well + .05f), x * offset, y * offset, d.Metal, .28f);
                d.Rail(x * offset + y * length, y * offset - x * length,
                    x * offset - y * length, y * offset + x * length, d.Metal, .30f);
                d.Rail(x * offset + y * (length - .07f), y * offset - x * (length - .07f),
                    x * offset - y * (length - .07f), y * offset + x * (length - .07f), d.Ceramic, .18f);
                d.Rail(x * offset + y * (length - .14f), y * offset - x * (length - .14f),
                    x * offset - y * (length - .14f), y * offset + x * (length - .14f), d.Accent, .07f);
            }
        }
        if (stage == 4)
        {
            for (var i = 0; i < 4; i++)
            {
                var a = i * MathHelper.PiOver2 + MathHelper.PiOver4;
                var x = MathF.Cos(a);
                var y = MathF.Sin(a);
                d.Rail(x * .66f, y * .66f, x * 1.03f, y * 1.03f, d.Metal, .38f);
                d.Rail(x * .75f, y * .75f, x * 1.03f, y * 1.03f, d.Ceramic, .28f);
                d.Rail(x * .83f, y * .83f, x * 1.04f, y * 1.04f, d.Accent, .11f);
            }
        }
        d.Disc(0, 0, well, d.Accent);
        d.Ring(0, 0, well, d.Ceramic, stage >= 3 ? .14f : .10f);
        d.Disc(0, 0, well - .16f, d.Dark);
        d.Disc(0, 0, .12f + recoil * .2f + (stage - 1) * .018f, d.White);
        if (stage >= 3)
        {
            for (var i = 0; i < 4; i++)
            {
                var a = i * MathHelper.PiOver2 + MathHelper.PiOver4;
                d.Rail(MathF.Cos(a) * (well - .04f), MathF.Sin(a) * (well - .04f),
                    MathF.Cos(a) * (well + .18f), MathF.Sin(a) * (well + .18f), d.Metal, .13f);
            }
        }
    }

    private static void DrawPrism(TowerBrush d, int stage, int level, bool apex)
    {
        var core = stage switch { 1 => .46f, 2 => .53f, 3 => .58f, _ => .64f };
        var span = stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f };
        for (var sign = -1; sign <= 1; sign += 2)
        {
            d.Rail(-.47f, sign * span, .58f, sign * span, d.Metal, stage >= 3 ? .34f : .25f);
            d.Rail(-.43f, sign * span, .53f, sign * span, d.Ceramic, stage >= 3 ? .23f : .17f);
            if (stage >= 2)
            {
                d.Rail(-.25f, sign * span, .23f, sign * span, d.Dark, .075f);
                d.Rail(-.66f, sign * .40f, -.41f, sign * .59f, d.Ceramic, .22f);
            }
            if (stage >= 3)
            {
                d.Rail(-.72f, sign * .55f, -.24f, sign * .80f, d.Metal, .22f);
                d.Rail(-.66f, sign * .56f, -.32f, sign * .74f, d.Accent, .075f);
                d.Rail(.48f, sign * .44f, 1.00f, sign * .30f, d.Metal, .24f);
                d.Rail(.57f, sign * .41f, .98f, sign * .30f, d.Ceramic, .15f);
                d.Rail(.69f, sign * .38f, .93f, sign * .31f, d.Accent, .065f);
            }
            if (stage == 4)
            {
                d.Rail(-.51f, sign * .89f, .42f, sign * .89f, d.Ceramic, .24f);
                d.Rail(-.38f, sign * .89f, .22f, sign * .89f, d.Accent, .10f);
                d.Rail(.50f, sign * .69f, .85f, sign * .46f, d.Metal, .23f);
            }
        }
        var housing = TowerTierVisuals.PrismApertureWidth(level, apex);
        var opening = housing * .5f;
        var length = TowerTierVisuals.PrismBarrelLength(level, apex);
        d.Rail(.30f, 0, length, 0, d.Metal, housing);
        d.Rail(.35f, 0, length, 0, d.Accent, opening);

        // The complete concentric core covers the barrel root, including its forward corner.
        var border = (housing - opening) * .5f;
        d.Diamond(0, 0, core + border * MathF.Sqrt(2), d.Metal);
        d.Diamond(0, 0, core, d.Accent);
        d.Diamond(0, 0, core * .515f, d.White);
        d.Diamond(0, 0, core * .205f, d.Accent);
    }

    private static void DrawBeacon(TowerBrush d, int stage, float time)
    {
        var size = stage switch { 1 => .35f, 2 => .44f, 3 => .52f, _ => .61f };
        if (stage >= 2)
        {
            for (var i = 0; i < 3; i++)
            {
                var a = i * MathHelper.TwoPi / 3;
                var x = MathF.Cos(a);
                var y = MathF.Sin(a);
                d.Rail(x * .35f, y * .35f, x * (stage >= 3 ? .91f : .73f), y * (stage >= 3 ? .91f : .73f), d.Metal, stage == 4 ? .30f : .21f);
                d.Rail(x * .51f, y * .51f, x * (stage >= 3 ? .86f : .70f), y * (stage >= 3 ? .86f : .70f), d.Accent, .085f);
                if (stage >= 3)
                {
                    d.Poly(x * .78f, y * .78f, .22f, 3, d.Ceramic, a);
                    d.Poly(x * .78f, y * .78f, .13f, 3, d.Accent, a);
                }
            }
        }
        if (stage == 4)
        {
            d.Poly(0, 0, .83f, 6, d.Metal);
            d.Poly(0, 0, .69f, 6, d.Dark);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                d.Rail(-.67f, sign * .66f, .43f, sign * .66f, d.Ceramic, .18f);
                d.Rail(-.51f, sign * .66f, .25f, sign * .66f, d.Accent, .07f);
            }
        }
        d.Poly(0, 0, size + .12f, 3, d.Ceramic);
        d.Poly(0, 0, size, 3, d.Dark);
        d.Poly(0, 0, size * .74f, 3, d.Accent);
        d.Rail(-.33f, 0, .52f, 0, d.Metal, stage == 1 ? .13f : .18f);
        d.Rail(-.27f, 0, .44f, 0, d.White, .065f);
        if (stage == 4) d.Poly(0, 0, .16f, 3, d.White);
    }

    private readonly struct TowerBrush
    {
        private readonly SpriteBatch _batch;
        private readonly PrimitiveRenderer _primitives;
        private readonly Vector2 _center;
        private readonly float _radius;
        private readonly float _angle;
        private readonly float _cos;
        private readonly float _sin;
        public readonly Color Metal;
        public readonly Color Ceramic;
        public readonly Color Dark;
        public readonly Color White;
        public readonly Color Accent;

        public TowerBrush(SpriteBatch batch, PrimitiveRenderer primitives, Vector2 center, float radius,
            float angle, float opacity, Color accent)
        {
            _batch = batch;
            _primitives = primitives;
            _center = center;
            _radius = radius;
            _angle = angle;
            _cos = MathF.Cos(angle);
            _sin = MathF.Sin(angle);
            Metal = ColorPalette.Metal * opacity;
            Ceramic = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, .56f) * opacity;
            Dark = ColorPalette.Navy * opacity;
            White = ColorPalette.Paper * opacity;
            Accent = accent;
        }

        private Vector2 At(float x, float y) => _center + new Vector2(_cos * x - _sin * y, _sin * x + _cos * y) * _radius;
        public void Rail(float x1, float y1, float x2, float y2, Color color, float width) =>
            _primitives.Line(_batch, At(x1, y1), At(x2, y2), color, MathF.Max(.7f, width * _radius));
        public void Disc(float x, float y, float size, Color color) => _primitives.Circle(_batch, At(x, y), _radius * size, color);
        public void Diamond(float x, float y, float size, Color color, float rotation = 0) =>
            _primitives.DrawDiamond(_batch, At(x, y), _radius * size, color, _angle + rotation);
        public void Poly(float x, float y, float size, int sides, Color color, float rotation = 0) =>
            _primitives.DrawPolygon(_batch, At(x, y), _radius * size, sides, false, color, _angle + rotation);
        public void Ring(float x, float y, float size, Color color, float width) =>
            _primitives.Ring(_batch, At(x, y), _radius * size, color, Math.Max(1, (int)(_radius * width)));
        public void DashedRing(float x, float y, float size, Color color, int segments, float width, float rotation) =>
            _primitives.DashedRing(_batch, At(x, y), _radius * size, color, segments,
                Math.Max(1, (int)(_radius * width)), rotation);
    }
}
