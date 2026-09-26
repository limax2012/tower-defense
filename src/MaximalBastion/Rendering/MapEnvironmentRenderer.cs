using MaximalBastion.Core;
using MaximalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>District scenery stays below every playable deck, route, node, and combat object.</summary>
public static class MapEnvironmentRenderer
{
    public static void Ground(SpriteBatch b, PrimitiveRenderer p, MapDefinition map, float time)
    {
        var colors = ColorPalette.Environment(map.PathVisual.Style);
        p.FillRect(b, new Rectangle(0, 0, GameConstants.MapWidth, GameConstants.LogicalHeight), colors.Ground);
        switch (map.PathVisual.Style)
        {
            case "trail": RainlineDistrictArt.Ground(b, p, map, time); break;
            case "prism": Nullspace(b, p, colors, time); break;
            case "surge": Reactor(b, p, colors, time); break;
            default: Foundry(b, p, colors, time); break;
        }
    }

    public static void Deck(SpriteBatch b, PrimitiveRenderer p, Rectangle r, string style, bool hovered)
    {
        if (style == "trail")
        {
            RainlineDistrictArt.Roof(b, p, r, hovered);
            return;
        }
        var c = ColorPalette.Environment(style);
        var center = r.Center.ToVector2();
        if (style == "prism")
        {
            p.Glow(b, center, Math.Min(180, r.Width), c.Accent, .19f);
            p.DrawRect(b, new Rectangle(r.X + 7, r.Y + 9, r.Width, r.Height), c.Detail);
            for (var x = r.Left; x <= r.Right; x += Math.Max(32, r.Width / 3))
                p.Line(b, new Vector2(x, r.Bottom), new Vector2(x + 7, r.Bottom + 9), c.Edge * .5f);
        }
        else
            p.FillRect(b, new Rectangle(r.X + 5, r.Y + 7, r.Width, r.Height), ColorPalette.Ink);

        var deck = Color.Lerp(c.Deck, ColorPalette.PanelAlt, .30f);
        p.FillRect(b, r, hovered ? Color.Lerp(deck, c.Accent, .14f) : deck);
        // Shallow metal panels catch a top-left light while the build footprint stays unobstructed.
        for (var band = 0; band < 6; band++)
        {
            var y = r.Top + band * r.Height / 6;
            p.FillRect(b, new Rectangle(r.X + 2, y + 1, Math.Max(1, r.Width - 4), Math.Max(1, r.Height / 6)),
                ColorPalette.Paper * (.026f * (1 - band / 6f)));
        }
        if (style == "foundry")
        {
            for (var y = r.Top + 8; y < r.Bottom - 4; y += 10)
                p.Line(b, new Vector2(r.Left + 5, y), new Vector2(r.Right - 5, y), c.Ground * .65f);
            for (var x = r.Left + 20; x < r.Right - 8; x += 40)
            {
                p.Line(b, new Vector2(x, r.Top + 4), new Vector2(x, r.Bottom - 4), c.Detail * .45f, 2);
                p.Circle(b, new Vector2(x, r.Top + 5), 1.5f, c.Edge * .65f);
            }
        }
        else if (style == "prism")
        {
            for (var x = r.Left + 20; x < r.Right - 5; x += 30)
                p.Line(b, new Vector2(x, r.Top + 5), new Vector2(x, r.Bottom - 5), c.Detail * .5f);
            for (var y = r.Top + 20; y < r.Bottom - 5; y += 30)
                p.Line(b, new Vector2(r.Left + 5, y), new Vector2(r.Right - 5, y), c.Detail * .5f);
            p.Line(b, new Vector2(r.Left + 8, r.Bottom - 8), new Vector2(r.Right - 8, r.Top + 8), c.Detail * .32f);
            p.Line(b, new Vector2(r.Left + 12, r.Top + 8), new Vector2(r.Left + Math.Min(46, r.Width - 12), r.Top + 8), ColorPalette.Paper * .3f, 2);
        }
        else
        {
            p.DrawRect(b, new Rectangle(r.X + 4, r.Y + 4, Math.Max(1, r.Width - 8), Math.Max(1, r.Height - 8)), c.Structure, 2);
            for (var x = r.Left + 16; x < r.Right - 12; x += 42)
                p.FillRect(b, new Rectangle(x, r.Top + 6, 13, 2), c.Edge * .55f);
        }

        // Identical brackets and bolt spacing consistently identify legal build surfaces.
        p.DrawRect(b, r, c.Edge * .46f);
        p.Line(b, new Vector2(r.Left + 2, r.Top + 2), new Vector2(r.Right - 2, r.Top + 2), ColorPalette.Paper * .13f);
        p.Line(b, new Vector2(r.Right - 2, r.Top + 2), new Vector2(r.Right - 2, r.Bottom - 2), ColorPalette.Ink * .65f, 2);
        p.Brackets(b, r, hovered ? ColorPalette.PlacementValid : Color.Lerp(c.Edge, ColorPalette.Paper, .35f), 9);
        for (var x = r.Left + 17; x < r.Right - 8; x += 30)
        for (var y = r.Top + 17; y < r.Bottom - 8; y += 30)
            p.FillRect(b, new Rectangle(x, y, 2, 2), c.Edge * .15f);
    }

    public static void Path(SpriteBatch b, PrimitiveRenderer p, MapDefinition map, float time)
    {
        var c = ColorPalette.Environment(map.PathVisual.Style);
        var points = map.Path;
        var width = map.PathWidth;
        var style = map.PathVisual.Style;
        var centerColor = Color.Lerp(c.Lane, ColorPalette.Ink, .62f);
        Stroke(b, p, points, ColorPalette.Ink, width + (style == "trail" ? 14 : 8));
        Stroke(b, p, points, Color.Lerp(c.Edge, ColorPalette.Metal, .3f), width);
        Stroke(b, p, points, Color.Lerp(c.Lane, ColorPalette.PanelAlt, .52f), width - 4);
        Stroke(b, p, points, centerColor, width - 12);
        if (style == "prism")
        {
            Stroke(b, p, points, c.Accent * .12f, 9);
            Stroke(b, p, points, c.Accent * .5f, 2);
            centerColor = Color.Lerp(Color.Lerp(centerColor, c.Accent, .12f), c.Accent, .5f);
        }
        if (style == "surge")
        {
            centerColor = Color.Lerp(c.Ground, ColorPalette.Panel, .35f);
            Stroke(b, p, points, centerColor, width - 16);
            Stroke(b, p, points, c.Edge * .65f, 9);
            Stroke(b, p, points, c.Accent * .4f, 2);
            centerColor = Color.Lerp(Color.Lerp(centerColor, c.Edge, .65f), c.Accent, .4f);
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i].ToVector2();
            var delta = points[i + 1].ToVector2() - start;
            var length = delta.Length();
            if (length < 1) continue;
            var forward = delta / length;
            var normal = new Vector2(-forward.Y, forward.X);
            for (var d = 16f; d < length - 8; d += style == "foundry" ? 20 : 36)
            {
                var at = start + forward * d;
                if (style == "foundry")
                {
                    p.Line(b, at - normal * (width * .31f), at + normal * (width * .31f), c.Edge * .15f, 2);
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        var edge = at + normal * sign * (width / 2f - 4);
                        p.Line(b, edge - forward * 3 - normal * sign * 3, edge + forward * 4, c.Accent * .7f, 3);
                    }
                }
                else if (style == "trail")
                {
                    p.Line(b, at - normal * (width / 2f - 5), at + normal * (width / 2f - 5), c.Edge * .15f);
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        var edge = at + normal * sign * (width / 2f - 4);
                        p.Line(b, edge, edge + forward * 10, c.Accent * .85f, 2);
                    }
                }
                else if (style == "prism")
                {
                    p.Line(b, at - normal * (width / 2f - 5), at - normal * (width / 2f - 12), c.Accent * .55f, 2);
                    p.Line(b, at + normal * (width / 2f - 5), at + normal * (width / 2f - 12), ColorPalette.Cyan * .35f, 2);
                }
                else
                {
                    p.Line(b, at - normal * (width / 2f - 3), at - normal * (width / 2f - 9), c.Accent * .65f, 3);
                    p.Line(b, at + normal * (width / 2f - 3), at + normal * (width / 2f - 9), c.Accent * .65f, 3);
                }
            }
        }
        // Opaque packet ink keeps overlapping corner joins at the same brightness.
        DrawRoutePackets(b, p, points, time, style == "foundry" ? 15 : 28,
            Color.Lerp(centerColor, c.Accent, .65f), style == "foundry" ? 4 : 2);
    }

    private static void DrawRoutePackets(SpriteBatch b, PrimitiveRenderer p, IReadOnlyList<PointData> points,
        float time, float speed, Color color, float thickness)
    {
        const float spacing = 112;
        const float packetLength = 12;
        var phase = ((time * speed) % spacing + spacing) % spacing;
        var cumulative = 0f;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i].ToVector2();
            var delta = points[i + 1].ToVector2() - start;
            var length = delta.Length();
            if (length <= .01f) continue;
            var forward = delta / length;
            var localPhase = (phase - cumulative % spacing + spacing) % spacing;
            // Every segment intersects the same moving intervals along the full route.
            for (var d = localPhase - spacing; d < length; d += spacing)
            {
                var from = MathF.Max(0, d);
                var to = MathF.Min(d + packetLength, length);
                if (to <= from) continue;
                var tail = start + forward * from;
                var head = start + forward * to;
                p.Line(b, tail, head, color, thickness);
                // Fixed-radius caps form one continuous stroke, even across subpixel corner fragments.
                p.Circle(b, tail, thickness * .5f, color);
                p.Circle(b, head, thickness * .5f, color);
            }
            cumulative += length;
        }
    }

    private static void Foundry(SpriteBatch b, PrimitiveRenderer p, ColorPalette.EnvironmentColors c, float time)
    {
        for (var y = 64; y < 720; y += 96)
        {
            p.FillRect(b, new Rectangle(0, y, 960, 3), c.Structure);
            for (var x = 12; x < 950; x += 96)
            {
                var tile = new Rectangle(x, y + 7, 86, 80);
                p.FillRect(b, tile, Color.Lerp(c.Ground, c.Structure, .20f));
                p.Line(b, new Vector2(x + 8, y + 74), new Vector2(x + 72, y + 10), c.Detail * .16f, 2);
                p.Line(b, new Vector2(tile.Left, tile.Top), new Vector2(tile.Right, tile.Top), ColorPalette.Paper * .022f);
            }
        }
        p.Glow(b, new Vector2(866, 380), 280, c.Accent, .16f);
        p.Glow(b, new Vector2(390, 344), 210, c.Accent, .13f);
        Pipe(b, p, new Vector2(12, 280), new Vector2(940, 280), c, 15);
        Pipe(b, p, new Vector2(680, 60), new Vector2(680, 680), c, 18);
        Pipe(b, p, new Vector2(355, 300), new Vector2(355, 696), c, 10);
        foreach (var r in new[] { new Rectangle(735, 354, 170, 140), new Rectangle(367, 294, 89, 144),
                     new Rectangle(52, 348, 90, 85), new Rectangle(470, 638, 170, 67) })
        {
            p.FillRect(b, new Rectangle(r.X + 6, r.Y + 8, r.Width, r.Height), ColorPalette.Ink);
            Housing(b, p, r, c, .52f);
            var inner = new Rectangle(r.X + 11, r.Y + 12, r.Width - 22, r.Height - 24);
            p.FillRect(b, inner, c.Ground);
            p.Glow(b, inner.Center.ToVector2(), Math.Max(inner.Width, inner.Height), c.Accent, .58f);
            for (var x = inner.Left + 5; x < inner.Right - 2; x += 10)
            {
                p.FillRect(b, new Rectangle(x, inner.Top + 3, 4, inner.Height - 6), c.Accent * (.36f + .06f * MathF.Sin(time * 1.2f + x)));
                p.FillRect(b, new Rectangle(x + 1, inner.Top + inner.Height / 3, 1, inner.Height / 3), ColorPalette.Gold * .65f);
            }
            for (var y = r.Top + 20; y < r.Bottom - 10; y += 25)
            {
                p.Line(b, new Vector2(r.Left + 3, y + 3), new Vector2(r.Right - 3, y + 3), ColorPalette.Ink, 7);
                p.Line(b, new Vector2(r.Left + 3, y), new Vector2(r.Right - 3, y), c.Detail, 5);
                p.Line(b, new Vector2(r.Left + 3, y - 2), new Vector2(r.Right - 3, y - 2), c.Edge * .3f);
            }
            var guard = new Rectangle(r.Left + 5, r.Top - 5, r.Width - 10, 13);
            p.FillRect(b, guard, ColorPalette.Metal);
            for (var x = guard.Left + 4; x < guard.Right - 5; x += 18)
                p.Line(b, new Vector2(x, guard.Top + 2), new Vector2(x + 7, guard.Bottom - 2), c.Accent * .65f, 4);
            p.FillRect(b, new Rectangle(r.Right - 7, r.Top + 13, 3, 15), ColorPalette.Cyan * .7f);
            p.Glow(b, new Vector2(r.Right - 5, r.Top + 20), 22, ColorPalette.Cyan, .24f);
        }
        // A service gantry links the heat exchangers; its mass sits below the playable surfaces.
        for (var x = 76; x < 920; x += 250)
        {
            var support = new Rectangle(x, 276, 18, 51);
            Housing(b, p, support, c, .18f);
            p.Line(b, new Vector2(x + 1, 282), new Vector2(x + 16, 320), ColorPalette.Ink, 3);
        }
        p.Line(b, new Vector2(80, 315), new Vector2(915, 315), ColorPalette.Ink, 10);
        p.Line(b, new Vector2(80, 312), new Vector2(915, 312), c.Detail, 6);
        p.Line(b, new Vector2(80, 310), new Vector2(915, 310), ColorPalette.Muted * .28f);
        var carriage = 430 + MathF.Sin(time * .08f) * 260;
        Housing(b, p, new Rectangle((int)carriage, 300, 31, 22), c, .4f);
        p.Line(b, new Vector2(carriage + 10, 310), new Vector2(carriage + 22, 310), c.Accent, 3);
        for (var i = 0; i < 18; i++)
        {
            var x = 30 + i * 137 % 900;
            var y = 80 + ((i * 73 - time * (5 + i % 4)) % 640 + 640) % 640;
            p.Line(b, new Vector2(x, y), new Vector2(x + 1, y + 3), c.Accent * .26f, 1);
        }
    }

    private static void Nullspace(SpriteBatch b, PrimitiveRenderer p, ColorPalette.EnvironmentColors c, float time)
    {
        var convergence = new Vector2(478, 350);
        p.Glow(b, convergence, 420, c.Accent, .1f);
        for (var i = 0; i < 15; i++)
        {
            var x = -300 + i * 115;
            p.Line(b, new Vector2(x, 720), convergence, c.Detail * .23f);
        }
        for (var y = 374; y < 720; y += 38)
            p.Line(b, new Vector2(0, y), new Vector2(960, y), c.Detail * .19f);
        foreach (var archive in new[] { (new Vector2(452, 167), 54f), (new Vector2(80, 556), 69f),
                     (new Vector2(703, 103), 51f), (new Vector2(714, 470), 50f), (new Vector2(423, 669), 36f) })
        {
            var center = archive.Item1 + new Vector2(0, MathF.Sin(time * .5f + archive.Item2) * 3);
            var r = archive.Item2;
            var ceramic = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, .32f);
            p.Glow(b, center + new Vector2(0, r * .3f), r * 2, c.Accent, .28f);
            // Suspended memory cabinets use solid armor and exposed luminous bus bars.
            p.DrawPolygon(b, center + new Vector2(7, 17), r, 4, false, ColorPalette.Ink);
            p.DrawPolygon(b, center + new Vector2(0, 10), r, 4, false, c.Structure);
            p.DrawPolygon(b, center, r, 4, false, ColorPalette.Metal);
            p.DrawPolygon(b, center, r * .89f, 4, false, ColorPalette.Panel);
            for (var slab = -2; slab <= 2; slab++)
            {
                var y = center.Y + slab * r * .20f;
                var half = r * (.73f - MathF.Abs(slab) * .14f);
                p.Line(b, new Vector2(center.X - half, y + 4), new Vector2(center.X + half, y + 4), ColorPalette.Ink, r * .17f);
                p.Line(b, new Vector2(center.X - half, y), new Vector2(center.X + half, y), Color.Lerp(c.Structure, ColorPalette.Metal, .5f), r * .14f);
                p.Line(b, new Vector2(center.X - half + 4, y - r * .055f), new Vector2(center.X + half - 4, y - r * .055f), ceramic * .28f);
                var active = .34f + MathF.Sin(time * .7f + slab + r) * .14f;
                p.Line(b, new Vector2(center.X - half + 6, y + 1), new Vector2(center.X - half + 13, y + 1), c.Accent * active, 2);
            }
            for (var side = -1; side <= 1; side += 2)
            {
                var top = center - new Vector2(0, r * .87f);
                var edge = center + new Vector2(side * r * .87f, 0);
                p.Line(b, top, edge, ceramic, 4);
                p.Line(b, edge, center + new Vector2(0, r * .87f), c.Edge * .6f, 3);
            }
            p.Line(b, center - new Vector2(0, r * .62f), center + new Vector2(0, r * .62f), ColorPalette.Ink, 8);
            p.Line(b, center - new Vector2(0, r * .62f), center + new Vector2(0, r * .62f), c.Accent * .65f, 3);
            p.DrawPolygon(b, center, r * .19f, 6, false, c.Edge);
            p.DrawPolygon(b, center, r * .10f, 6, false, ColorPalette.Paper * .8f);
            p.Glow(b, center, r * .65f, ColorPalette.Cyan, .30f);
        }
        for (var i = 0; i < 64; i++)
        {
            var x = 8 + i * 179 % 942;
            var y = 65 + i * 131 % 645;
            p.FillRect(b, new Rectangle(x, y, i % 7 == 0 ? 13 : 2, 1), c.Accent * .24f);
        }
        for (var i = 0; i < 8; i++)
        {
            var x = 65 + i * 127 % 850;
            var y = 80 + (time * 11 + i * 137) % 620;
            for (var digit = 0; digit < 5; digit++)
                p.FillRect(b, new Rectangle(x, (int)y + digit * 8, digit % 2 == 0 ? 3 : 6, 2), c.Edge * (.36f - digit * .05f));
        }
    }

    private static void Reactor(SpriteBatch b, PrimitiveRenderer p, ColorPalette.EnvironmentColors c, float time)
    {
        var hub = new Vector2(502, 376);
        for (var radius = 100; radius <= 460; radius += 60)
            p.Ring(b, hub, radius, c.Structure * .65f, radius % 120 == 40 ? 4 : 1);
        for (var spoke = 0; spoke < 24; spoke++)
        {
            var a = spoke * MathHelper.TwoPi / 24;
            var direction = new Vector2(MathF.Cos(a), MathF.Sin(a));
            p.Line(b, hub + direction * 80, hub + direction * 570, c.Detail * .22f, spoke % 3 == 0 ? 6 : 1);
        }
        foreach (var reactor in new[] { (new Vector2(824, 339), 90f), (new Vector2(283, 172), 59f),
                     (new Vector2(95, 607), 52f), (new Vector2(521, 526), 55f) })
        {
            var center = reactor.Item1;
            var radius = reactor.Item2;
            p.Glow(b, center, radius * 1.6f, c.Accent, .28f);
            p.Circle(b, center + new Vector2(4, 8), radius + 10, ColorPalette.Ink);
            p.Circle(b, center, radius + 3, ColorPalette.Metal);
            p.Ring(b, center, radius + 1, ColorPalette.Muted * .4f, 2);
            p.Circle(b, center, radius - 3, ColorPalette.Panel);
            p.Ring(b, center, radius - 5, c.Detail, 6);
            p.Circle(b, center, radius * .71f, c.Ground);
            p.DashedRing(b, center, radius * .82f, c.Edge * .6f, 32, 5, MathHelper.Pi / 32);
            p.Ring(b, center, radius * .58f, c.Edge * .65f, 3);
            p.DashedRing(b, center, radius * .46f, c.Accent * .45f, 24, 2, time * .12f);
            p.Glow(b, center, radius * .8f, c.Accent, .45f + MathF.Sin(time * 1.4f) * .06f);
            p.Circle(b, center, radius * .27f, c.Structure);
            p.Ring(b, center, radius * .27f, ColorPalette.Cyan * .45f, 2);
            p.DrawPolygon(b, center, radius * .17f, 6, false, c.Accent * .7f, time * .07f);
            p.DrawPolygon(b, center, radius * .09f, 6, false, ColorPalette.Paper * .6f, time * .07f);
            for (var i = 0; i < 8; i++)
            {
                var a = i * MathHelper.PiOver4;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                var normal = new Vector2(-dir.Y, dir.X);
                var inner = center + dir * radius * .63f;
                var outer = center + dir * radius * .98f;
                p.Line(b, inner + new Vector2(2, 3), outer + new Vector2(2, 3), ColorPalette.Ink, radius * .20f);
                p.Line(b, inner, outer, ColorPalette.Metal, radius * .18f);
                p.Line(b, inner - normal * radius * .07f, outer - normal * radius * .07f, ColorPalette.Paper * .3f, 2);
                p.Line(b, inner + dir * 3, outer - dir * 6, c.Accent * .48f, 3);
                p.Circle(b, outer - dir * 3, 2, ColorPalette.Paper * .45f);
            }
        }
        Pipe(b, p, new Vector2(24, 702), new Vector2(925, 702), c, 16);
        Pipe(b, p, new Vector2(918, 65), new Vector2(918, 697), c, 18);
    }

    private static void Housing(SpriteBatch b, PrimitiveRenderer p, Rectangle r,
        ColorPalette.EnvironmentColors c, float districtTint)
    {
        var casing = Color.Lerp(ColorPalette.Metal, c.Detail, districtTint);
        p.FillRect(b, new Rectangle(r.X + 3, r.Y + 5, r.Width, r.Height), ColorPalette.Ink);
        p.FillRect(b, r, casing);
        p.FillRect(b, new Rectangle(r.X + 3, r.Y + 3, Math.Max(1, r.Width - 6), Math.Max(1, r.Height - 6)),
            Color.Lerp(ColorPalette.Panel, c.Structure, districtTint));
        p.Line(b, new Vector2(r.Left + 2, r.Top), new Vector2(r.Right - 2, r.Top), ColorPalette.Paper * .25f, 2);
        p.Line(b, new Vector2(r.Right, r.Top + 2), new Vector2(r.Right, r.Bottom), ColorPalette.Ink * .8f, 3);
        foreach (var corner in new[] { new Vector2(r.Left + 5, r.Top + 5), new Vector2(r.Right - 5, r.Top + 5),
                     new Vector2(r.Left + 5, r.Bottom - 5), new Vector2(r.Right - 5, r.Bottom - 5) })
        {
            p.Circle(b, corner + Vector2.One, 1.5f, ColorPalette.Ink);
            p.Circle(b, corner, 1, ColorPalette.Paper * .3f);
        }
    }

    private static void Pipe(SpriteBatch b, PrimitiveRenderer p, Vector2 start, Vector2 end,
        ColorPalette.EnvironmentColors c, int width)
    {
        p.Line(b, start + new Vector2(3, 4), end + new Vector2(3, 4), ColorPalette.Ink, width + 5);
        p.Line(b, start, end, c.Detail, width);
        p.Line(b, start, end, c.Structure, width - 5);
        p.Line(b, start - new Vector2(2, 2), end - new Vector2(2, 2), c.Edge * .3f, 2);
        var direction = Vector2.Normalize(end - start);
        var normal = new Vector2(-direction.Y, direction.X);
        for (var d = 25f; d < Vector2.Distance(start, end); d += 55)
        {
            var at = start + direction * d;
            p.Line(b, at - normal * (width / 2 + 2), at + normal * (width / 2 + 2), c.Detail, 4);
            p.Line(b, at - direction * 2 - normal * (width / 2), at - direction * 2 + normal * (width / 2), ColorPalette.Muted * .22f, 1);
        }
    }

    private static void Stroke(SpriteBatch b, PrimitiveRenderer p, IReadOnlyList<PointData> points, Color color, int width)
    {
        for (var i = 0; i < points.Count - 1; i++)
            p.Line(b, points[i].ToVector2(), points[i + 1].ToVector2(), color, width);
        foreach (var point in points)
            p.FillRect(b, new Rectangle((int)(point.X - width / 2f), (int)(point.Y - width / 2f), width, width), color);
    }

    public static void Emblem(SpriteBatch b, PrimitiveRenderer p, Vector2 center, string style, float radius)
    {
        var c = ColorPalette.Environment(style);
        if (style == "surge")
        {
            p.Ring(b, center, radius, c.Accent, 2);
            p.DashedRing(b, center, radius * .6f, c.Accent, 12, 2);
            p.Circle(b, center, radius * .2f, c.Accent);
        }
        else if (style == "prism")
        {
            p.DrawPolygon(b, center, radius, 4, false, c.Accent);
            p.DrawPolygon(b, center, radius * .7f, 4, false, c.Ground);
            p.Line(b, center - new Vector2(0, radius), center + new Vector2(0, radius), c.Accent, 1);
        }
        else
        {
            for (var i = -1; i <= 1; i++)
            {
                var height = style == "trail" ? radius * (i == 0 ? 1.8f : 1.2f) : radius * 1.6f;
                p.FillRect(b, new Rectangle((int)(center.X + i * radius * .62f - 2), (int)(center.Y + radius - height),
                    style == "trail" ? 5 : 3, (int)height), c.Accent);
            }
            if (style != "trail") p.Line(b, center - new Vector2(radius, 0), center + new Vector2(radius, 0), c.Ground, 3);
        }
    }
}
