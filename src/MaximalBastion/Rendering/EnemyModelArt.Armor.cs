using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

public static partial class EnemyModelArt
{
    private static void DrawArmoredFamily(Chassis f)
    {
        DrawHull(f.Batch, f.P, f.Enemy, f.Center, f.Radius * .94f, f.Heading, f.Shell);
        DrawHull(f.Batch, f.P, f.Enemy, f.Center, f.Radius * .83f, f.Heading, ColorPalette.EnemyChassis);
        var vertices = Contour(f.Enemy);
        for (var index = 0; index < vertices.Length; index++)
        {
            var a = f.At(vertices[index].X * .94f, vertices[index].Y * .94f);
            var next = vertices[(index + 1) % vertices.Length];
            var b = f.At(next.X * .94f, next.Y * .94f);
            var edge = b - a;
            var light = Vector2.Dot(Vector2.Normalize(new Vector2(edge.Y, -edge.X)), new Vector2(-.6f, -.8f));
            if (light > .1f) f.P.ArmorLine(f.Batch, a, b, ColorPalette.EnemyArmorEdge * (.35f + light * .35f), .65f);
        }
        switch (f.Enemy.Definition.Id)
        {
            case "t2_runner": DrawInterceptorArmor(f); break;
            case "t3_brute": DrawRadialArmor(f, 6, .71f); break;
            case "t4_aegis": DrawRadialArmor(f, 8, .66f); break;
            case "t5_regenerator": DrawTankArmor(f); break;
            default: DrawAssaultArmor(f); break;
        }
    }

    private static void DrawInterceptorArmor(Chassis f)
    {
        Plate(f, new(.50f, 0), .52f, .18f, 0, f.Edge);
        Window(f, new(.76f, 0), new(.91f, 0), .055f);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var start = new Vector2(-.47f, sign * .54f);
            var end = new Vector2(.06f, sign * .24f);
            SegmentPlate(f, start, end, .21f, f.Edge);
            Window(f, Vector2.Lerp(start, end, .10f), Vector2.Lerp(start, end, .42f), .06f);
            f.P.ArmorLine(f.Batch, f.At(start.X, start.Y), f.At(start.X - .08f, start.Y + sign * .08f),
                ColorPalette.EnemyRecess, 1.1f);
        }
        Plate(f, new(-.43f, 0), .25f, .12f, 0, f.Shell);
        Window(f, new(-.54f, 0), new(-.65f, 0), .05f);
        if (!f.Reduced && f.TravelDistance > 0 && f.Enemy.CurrentSpeed > 0)
        {
            var travel = f.TravelDistance * .025f % 1;
            f.Rail(-.56f - travel * .10f, 0, -1.08f - travel * .10f, 0, f.Energy * .28f, .07f);
            for (var sign = -1; sign <= 1; sign += 2)
                f.Rail(-.57f, sign * .31f, -.83f - travel * .12f, sign * .34f, f.Energy * .19f, .035f);
        }
    }

    private static void DrawAssaultArmor(Chassis f)
    {
        Plate(f, new(.43f, 0), .25f, .43f, 0, f.Edge, fasteners: true);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            SegmentPlate(f, new(-.35f, sign * .13f), new(.23f, sign * .35f), .16f, f.Shell);
            Plate(f, new(.06f, sign * .40f), .21f, .12f, 0, f.Edge);
        }
        Plate(f, new(-.47f, 0), .20f, .22f, 0, f.Armor);
    }

    private static void DrawRadialArmor(Chassis f, int sides, float radius)
    {
        var rotation = f.Heading + (sides == 8 ? MathHelper.Pi / 8 : 0);
        f.P.ArmorPolygon(f.Batch, f.Center + new Vector2(.3f, 1), f.Radius * (radius + .035f),
            sides, ColorPalette.EnemyRecess, rotation);
        f.P.BeveledArmorRing(f.Batch, f.Center, f.Radius * radius, sides, f.Edge, rotation);
        for (var end = -1; end <= 1; end += 2)
        {
            Plate(f, new(end * .66f, 0), .18f, .28f, 0, f.Edge, fasteners: true);
            f.Rail(end * .67f, -.07f, end * .67f, .07f, ColorPalette.EnemyRecess, .05f);
        }
    }

    private static void DrawTankArmor(Chassis f)
    {
        DrawTracks(f);
        f.P.ArmorPolygon(f.Batch, f.Center, f.Radius * .59f, 8, ColorPalette.EnemyRecess, f.Heading + MathHelper.Pi / 8);
        f.P.BeveledArmorRing(f.Batch, f.Center, f.Radius * .57f, 8, f.Edge, f.Heading + MathHelper.Pi / 8);
        for (var sign = -1; sign <= 1; sign += 2)
        for (var index = -1; index <= 1; index++)
        {
            var at = new Vector2(index * .30f, sign * .515f);
            Plate(f, at, .275f, .20f, 0, f.Edge, fasteners: index != 0);
            Window(f, at + new Vector2(-.075f, sign * .057f), at + new Vector2(.075f, sign * .057f), .035f);
        }
        for (var end = -1; end <= 1; end += 2)
        {
            Plate(f, new(end * .74f, 0), .24f, .32f, 0, f.Edge, fasteners: true);
            f.Rail(end * .77f, -.07f, end * .77f, .07f, ColorPalette.EnemyRecess, .05f);
        }
    }

    public static float CoreRadius(EnemyInstance enemy) => BodyRadius(enemy) *
        (enemy.Definition.Id is "t2_runner" or "t1_crawler" ? .43f : .38f);

    public static void DrawSignalCore(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 center, float heading, float time, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped || enemy.SignalRole == EnemySignalRole.None) return;
        DrawFacetedCore(new Chassis(batch, p, enemy, center, heading, time, reducedEffects, 0, 0, 1, atRest: true), glow: false);
    }

    private static void DrawFacetedCore(Chassis f, bool glow = true)
    {
        var triangular = f.Enemy.Definition.Id is "t2_runner" or "t1_crawler";
        var sides = triangular ? 3 : f.Enemy.Definition.Id == "t3_brute" ? 6 : 8;
        var angle = f.Heading + (triangular ? MathHelper.Pi : MathHelper.Pi / sides);
        var radius = CoreRadius(f.Enemy);
        var signal = f.Enemy.SignalRole != EnemySignalRole.None;
        var accelerator = f.Enemy.SignalRole == EnemySignalRole.Accelerator;
        var energy = accelerator ? ColorPalette.AccelerationGem :
            signal ? EnemySignalGlyphRenderer.Accent(f.Enemy.SignalRole) : f.Energy;
        var hot = accelerator ? ColorPalette.Acceleration :
            signal ? Color.Lerp(energy, ColorPalette.Paper, .60f) : ColorPalette.EnemyHot;
        var rim = signal ? Color.Lerp(ColorPalette.EnemyRecess, energy, .30f) : ColorPalette.EnemyCoreRim;
        f.P.ArmorPolygon(f.Batch, f.Center + new Vector2(0, .8f), radius * 1.12f, sides, ColorPalette.EnemyRecess, angle);
        f.P.ArmorPolygon(f.Batch, f.Center, radius, sides, ColorPalette.EnemyArmorEdge, angle);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .89f, sides, rim, angle);
        if (glow && !f.Reduced) f.P.Glow(f.Batch, f.Center, radius * 1.5f, energy, .24f * f.Reactor);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .68f, sides, energy * f.Reactor, angle);
        f.P.ArmorPolygon(f.Batch, f.Center, radius * .40f, sides,
            Color.Lerp(energy, hot, .68f) * f.Reactor, angle);
        if (!triangular)
        {
            f.P.ArmorPolygon(f.Batch, f.Center + new Vector2(-.25f, -.45f), radius * .28f, 6,
                hot, f.Heading);
            for (var edge = 0; edge < 3; edge++)
            {
                var a = f.Heading + edge * MathHelper.TwoPi / 3;
                f.P.ArmorLine(f.Batch, f.Center, f.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * .28f,
                    energy * .58f, .45f);
            }
        }
    }

    private static void DrawRankArmor(Chassis f)
    {
        if (f.Rank == 0) return;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var x = f.Enemy.Definition.Id == "t2_runner" ? -.30f : -.51f;
            Plate(f, new(x, sign * .34f), .19f, .11f, 0, f.Edge);
            if (f.Rank == 2)
                Window(f, new(x - .065f, sign * .34f), new(x + .065f, sign * .34f), .035f);
        }
    }

    private static void SegmentPlate(Chassis f, Vector2 first, Vector2 last, float width, Color color)
    {
        var delta = last - first;
        Plate(f, (first + last) * .5f, delta.Length(), width, MathF.Atan2(delta.Y, delta.X), color);
    }

    private static void Plate(Chassis f, Vector2 local, float length, float width, float angle, Color color, bool fasteners = false)
    {
        var center = f.At(local.X, local.Y);
        var rotation = f.Heading + angle;
        f.P.ArmorPlate(f.Batch, center + new Vector2(.35f, .9f), length * f.Radius + .8f,
            width * f.Radius + .8f, rotation, ColorPalette.EnemyRecess);
        f.P.ArmorPlate(f.Batch, center, length * f.Radius, width * f.Radius, rotation, color);
        if (!fasteners || length * f.Radius < 6) return;
        var direction = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        for (var sign = -1; sign <= 1; sign += 2)
            f.P.ArmorCircle(f.Batch, center + direction * (sign * length * f.Radius * .29f), .5f, ColorPalette.EnemyRecess);
    }

    private static void Window(Chassis f, Vector2 first, Vector2 last, float width)
    {
        var start = f.At(first.X, first.Y);
        var end = f.At(last.X, last.Y);
        f.P.ArmorLine(f.Batch, start, end, ColorPalette.EnemyRecess, width * f.Radius + 1.0f);
        f.P.ArmorLine(f.Batch, start, end, f.Energy * f.Reactor, width * f.Radius);
        f.P.ArmorLine(f.Batch, start, Vector2.Lerp(start, end, .60f), ColorPalette.EnemyHot * .8f, .45f);
    }
}
