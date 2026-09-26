using MaximalBastion.Core;
using MaximalBastion.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Elevated defense decks and causeways stand above a recessed utility plant.</summary>
internal static class RainlineDistrictArt
{
    private static readonly Vector2 Elevation = new(16, 58);

    public static void Ground(SpriteBatch b, PrimitiveRenderer p, MapDefinition map, float time)
    {
        var c = ColorPalette.Rainline;
        p.FillRect(b, new Rectangle(0, 56, 960, 664), c.Ground);
        // Sparse service galleries sit in the shadow well beneath the playable level.
        p.Glow(b, new Vector2(488, 578), 255, c.Detail, .13f);
        p.Glow(b, new Vector2(806, 230), 180, c.Detail, .10f);
        DrawConduit(b, p, new Vector2(-16, 572), new Vector2(974, 572));
        DrawConduit(b, p, new Vector2(414, 56), new Vector2(414, 572));
        DrawConduit(b, p, new Vector2(826, 56), new Vector2(826, 572));
        var roofs = map.BuildableRegions.Select(region => region.ToRectangle()).OrderBy(roof => roof.Bottom).ToArray();
        foreach (var roof in roofs)
        {
            var port = new Vector2(roof.Center.X, roof.Bottom) + Elevation;
            var junction = new Vector2(port.X, 572);
            DrawConduit(b, p, port, junction, false);
        }
        foreach (var x in new[] { 218, 458, 698 })
        {
            DrawConduit(b, p, new Vector2(x + 43, 572), new Vector2(x + 43, 592), false);
            DrawCoolingBank(b, p, new Rectangle(x, 592, 86, 46), time);
        }
        foreach (var roof in roofs)
        {
            var baseBounds = new Rectangle(roof.X + (int)Elevation.X, roof.Y + (int)Elevation.Y, roof.Width, roof.Height);
            var apron = baseBounds;
            apron.Inflate(5, 5);
            p.FillRect(b, apron, Color.Lerp(c.Ground, c.Structure, .28f));
            p.FillRect(b, new Rectangle(baseBounds.X + 6, baseBounds.Y + 8, baseBounds.Width + 5, baseBounds.Height + 5),
                ColorPalette.TowerShadow * .85f);
        }
        DrawViaductStructure(b, p, map);
        foreach (var roof in roofs)
        {
            DrawUtilityBlock(b, p, roof);
            DrawAccess(b, p, map, roof);
        }
    }

    private static void DrawUtilityBlock(SpriteBatch b, PrimitiveRenderer p, Rectangle roof)
    {
        var c = ColorPalette.Rainline;
        for (var y = 0; y < (int)Elevation.Y; y++)
        {
            var shift = (int)(y * Elevation.X / Elevation.Y);
            var shade = Color.Lerp(c.Structure, c.Ground, y / Elevation.Y * .48f);
            p.FillRect(b, new Rectangle(roof.Left + shift, roof.Bottom + y, roof.Width, 1), shade);
        }
        for (var x = 0; x < (int)Elevation.X; x++)
        {
            var drop = (int)(x * Elevation.Y / Elevation.X);
            p.FillRect(b, new Rectangle(roof.Right + x, roof.Top + drop, 1, roof.Height),
                Color.Lerp(c.Ground, c.Structure, .42f));
        }

        Vector2 Front(float x, float depth) => new(roof.Left + x + depth * Elevation.X / Elevation.Y, roof.Bottom + depth);
        Vector2 Side(float y, float depth) => new(roof.Right + depth * Elevation.X / Elevation.Y, roof.Top + y + depth);

        // Full-height heat-sink fins and service cassettes follow the same projection as the housing.
        for (var x = 9; x < roof.Width - 7; x += 6)
        {
            p.Line(b, Front(x, 13), Front(x, 46), c.Ground, 4);
            p.Line(b, Front(x - 1, 13), Front(x - 1, 46), c.Detail * .6f);
        }
        p.Line(b, Front(6, 11), Front(roof.Width - 6, 11), ColorPalette.Ink, 3);
        p.Line(b, Front(6, 48), Front(roof.Width - 6, 48), c.Detail * .65f, 3);
        for (var x = 3; x < roof.Width; x += 52)
        {
            p.Line(b, Front(x, 3), Front(x, 55), c.Ground, 5);
            p.Line(b, Front(x - 1, 3), Front(x - 1, 55), c.Edge * .23f, 2);
        }
        for (var y = 8; y < roof.Height - 12; y += 62)
        {
            var end = Math.Min(y + 50, roof.Height - 6);
            p.Line(b, Side(y, 9), Side(end, 9), ColorPalette.Ink, 2);
            p.Line(b, Side(y, 48), Side(end, 48), ColorPalette.Ink, 2);
            p.Line(b, Side(y, 9), Side(end, 48), c.Detail * .65f, 2);
            p.Line(b, Side(y, 48), Side(end, 9), c.Detail * .4f);
        }
        p.Line(b, Front(0, 5), Front(roof.Width, 5), c.Detail * .8f, 6);
        p.Line(b, Side(0, 5), Side(roof.Height, 5), c.Detail * .48f, 3);
        p.Line(b, new Vector2(roof.Right, roof.Bottom), new Vector2(roof.Right, roof.Bottom) + Elevation, c.Edge * .35f, 2);
        var baseLeft = new Vector2(roof.Left, roof.Bottom) + Elevation;
        var baseRight = new Vector2(roof.Right, roof.Bottom) + Elevation;
        p.Line(b, baseLeft, baseRight, ColorPalette.Ink, 4);
        p.Line(b, baseLeft + new Vector2(0, 3), baseRight + new Vector2(0, 3), c.Edge * .17f);
        var port = Front(roof.Width - 13, 7);
        p.Line(b, port - new Vector2(4, 0), port + new Vector2(4, 0), c.Accent * .6f, 2);
        p.Glow(b, port, 20, c.Accent, .13f);
    }

    private static void DrawViaductStructure(SpriteBatch b, PrimitiveRenderer p, MapDefinition map)
    {
        var c = ColorPalette.Rainline;
        var roadHalfWidth = map.PathWidth / 2f;
        for (var segment = 1; segment < map.Path.Count; segment++)
        {
            var start = map.Path[segment - 1].ToVector2();
            var end = map.Path[segment].ToVector2();
            var delta = end - start;
            var length = delta.Length();
            if (length < 1) continue;
            var direction = delta / length;
            var normal = new Vector2(-direction.Y, direction.X);
            p.Line(b, start + Elevation, end + Elevation, ColorPalette.TowerShadow * .75f, map.PathWidth + 7);
            for (var distance = 38f; distance < length - 16; distance += 128)
            {
                var center = start + direction * distance;
                foreach (var sign in new[] { -1, 1 })
                {
                    var top = center + normal * sign * (roadHalfWidth - 8);
                    var foot = top + Elevation;
                    p.FillRect(b, new Rectangle((int)foot.X - 8, (int)foot.Y - 5, 16, 10), c.Detail * .45f);
                    p.Line(b, foot, top, c.Ground, 10);
                    p.Line(b, foot - new Vector2(2, 0), top - new Vector2(2, 0), c.Detail * .7f, 3);
                }
                p.Line(b, center - normal * roadHalfWidth + new Vector2(3, 9),
                    center + normal * roadHalfWidth + new Vector2(3, 9), c.Structure, 8);
            }
            p.Line(b, start + new Vector2(4, 12), end + new Vector2(4, 12), c.Structure, map.PathWidth + 2);
            p.Line(b, start + new Vector2(4, 16), end + new Vector2(4, 16), c.Detail * .34f, map.PathWidth - 4);
        }
    }

    private static void DrawAccess(SpriteBatch b, PrimitiveRenderer p, MapDefinition map, Rectangle roof)
    {
        var c = ColorPalette.Rainline;
        var center = roof.Center.ToVector2();
        var distance = float.MaxValue;
        var connection = Vector2.Zero;
        var roofEdge = Vector2.Zero;
        for (var i = 1; i < map.Path.Count; i++)
        {
            var start = map.Path[i - 1].ToVector2();
            var end = map.Path[i].ToVector2();
            var segment = end - start;
            if (segment.LengthSquared() < 1) continue;
            var at = start + segment * MathHelper.Clamp(Vector2.Dot(center - start, segment) / segment.LengthSquared(), 0, 1);
            var edge = new Vector2(MathHelper.Clamp(at.X, roof.Left, roof.Right), MathHelper.Clamp(at.Y, roof.Top, roof.Bottom));
            var gap = Vector2.Distance(edge, at) - map.PathWidth / 2f;
            if (gap < 0 || gap >= distance) continue;
            distance = gap;
            roofEdge = edge;
            connection = at;
        }
        if (distance > 72 || distance < 4) return;
        var direction = Vector2.Normalize(connection - roofEdge);
        var normal = new Vector2(-direction.Y, direction.X);
        var roadEdge = connection - direction * (map.PathWidth / 2f - 2);
        p.Line(b, roofEdge + new Vector2(3, 7), roadEdge + new Vector2(3, 7), ColorPalette.Ink, 15);
        p.Line(b, roofEdge, roadEdge, c.Structure, 12);
        foreach (var side in new[] { -1, 1 })
            p.Line(b, roofEdge + normal * side * 6, roadEdge + normal * side * 6, c.Edge * .35f, 1);
        p.Line(b, roofEdge - normal * 4, roofEdge + normal * 4, c.Accent * .6f, 2);
        p.Glow(b, roofEdge, 18, c.Accent, .14f);
    }

    public static void Roof(SpriteBatch b, PrimitiveRenderer p, Rectangle roof, bool hovered)
    {
        var c = ColorPalette.Rainline;
        var deck = Color.Lerp(c.Deck, ColorPalette.PanelAlt, .30f);
        p.FillRect(b, roof, hovered ? Color.Lerp(deck, c.Accent, .14f) : deck);
        p.DrawRect(b, new Rectangle(roof.X + 3, roof.Y + 3, roof.Width - 6, roof.Height - 6), c.Structure, 3);
        for (var y = roof.Top + 36; y < roof.Bottom - 10; y += 56)
            p.Line(b, new Vector2(roof.Left + 8, y), new Vector2(roof.Right - 8, y), c.Edge * .10f);
        var runoff = new Rectangle(roof.Right - 12, roof.Top + 9, 3, roof.Height - 18);
        p.FillRect(b, runoff, c.Ground * .6f);
        p.Line(b, new Vector2(roof.Left + 8, roof.Bottom - 10), new Vector2(roof.Right - 9, roof.Bottom - 10), c.Ground * .55f, 2);
        if (roof.Width > 70 && roof.Height > 45)
        {
            var vent = new Rectangle(roof.X + 11, roof.Y + 11, 32, roof.Height > 130 ? 16 : 10);
            p.FillRect(b, vent, c.Structure);
            if (roof.Height > 130)
            {
                foreach (var x in new[] { vent.Left + 8, vent.Left + 24 })
                {
                    var fan = new Vector2(x, vent.Center.Y);
                    p.Circle(b, fan, 6, c.Ground);
                    p.Ring(b, fan, 5, c.Edge * .36f, 1);
                    for (var blade = 0; blade < 3; blade++)
                    {
                        var angle = blade * MathHelper.TwoPi / 3 + .4f;
                        p.Line(b, fan, fan + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4, c.Detail, 2);
                    }
                }
                p.Line(b, new Vector2(vent.Right, vent.Center.Y), new Vector2(roof.Right - 11, vent.Center.Y), c.Edge * .15f, 2);
                p.FillRect(b, new Rectangle(vent.Left + 2, vent.Bottom + 3, 8, 2), c.Accent * .55f);
                p.Glow(b, new Vector2(vent.Left + 6, vent.Bottom + 4), 16, c.Accent, .13f);
            }
            else
                for (var x = vent.Left + 3; x < vent.Right - 2; x += 4)
                    p.Line(b, new Vector2(x, vent.Top + 2), new Vector2(x, vent.Bottom - 2), c.Ground, 2);
            p.Line(b, new Vector2(vent.Left, vent.Top), new Vector2(vent.Right, vent.Top), c.Edge * .28f);
        }
        if (roof.Width > 70 && roof.Height > 130)
        {
            var hatch = new Rectangle(roof.Right - 39, roof.Bottom - 31, 22, 16);
            p.FillRect(b, hatch, Color.Lerp(deck, c.Ground, .3f));
            p.DrawRect(b, hatch, c.Detail * .65f);
            p.Line(b, new Vector2(hatch.Left + 5, hatch.Top + 4), new Vector2(hatch.Right - 5, hatch.Top + 4), c.Edge * .25f);
            p.Line(b, new Vector2(hatch.Left + 4, hatch.Bottom - 4), new Vector2(hatch.Right - 4, hatch.Bottom - 4), c.Ground * .7f);
            for (var y = roof.Top + 48; y < roof.Bottom - 28; y += 84)
            {
                var center = new Vector2(roof.Left + roof.Width * .45f, y);
                p.Line(b, center - new Vector2(18, 0), center + new Vector2(18, 0), c.Edge * .055f, 3);
                p.Line(b, center - new Vector2(10, 3), center + new Vector2(23, 3), c.Edge * .035f);
            }
        }
        // Broad, faint reflections stay within the roof and do not resemble additional equipment.
        for (var stripe = 0; stripe < 5; stripe++)
        {
            var y = roof.Top + 12 + stripe * 2;
            if (y >= roof.Bottom - 8) break;
            p.Line(b, new Vector2(roof.Left + roof.Width * .42f, y),
                new Vector2(roof.Right - 14, y), c.Edge * (.035f + stripe * .005f));
        }
        p.Line(b, new Vector2(roof.Left + 2, roof.Top + 2), new Vector2(roof.Right - 2, roof.Top + 2), c.Edge * .6f, 2);
        p.Line(b, new Vector2(roof.Left + 2, roof.Top + 2), new Vector2(roof.Left + 2, roof.Bottom - 2), c.Edge * .32f);
        p.Line(b, new Vector2(roof.Right - 2, roof.Top + 2), new Vector2(roof.Right - 2, roof.Bottom - 2), ColorPalette.Ink * .65f, 2);
        p.DrawRect(b, roof, c.Edge * .46f);
        p.Brackets(b, roof, hovered ? ColorPalette.PlacementValid : Color.Lerp(c.Edge, ColorPalette.Paper, .35f), 9);
    }

    private static void DrawConduit(SpriteBatch b, PrimitiveRenderer p, Vector2 start, Vector2 end, bool trunk = true)
    {
        var c = ColorPalette.Rainline;
        var length = Vector2.Distance(start, end);
        if (length < 1) return;
        var along = (end - start) / length;
        var normal = new Vector2(-along.Y, along.X);
        var halfWidth = trunk ? 7 : 4;
        p.Line(b, start + new Vector2(3, 4), end + new Vector2(3, 4), ColorPalette.TowerShadow * .8f, halfWidth * 2 + 6);
        p.Line(b, start, end, Color.Lerp(c.Ground, c.Structure, .35f), halfWidth * 2 + 4);
        p.Line(b, start, end, ColorPalette.Canvas, halfWidth * 2);
        foreach (var side in new[] { -1, 1 })
        {
            var offset = normal * side * halfWidth * .5f;
            p.Line(b, start + offset, end + offset, c.Detail * .6f, trunk ? 3 : 2);
            p.Line(b, start + offset - normal, end + offset - normal, c.Edge * .12f);
        }
        for (var distance = 12f; distance < length - 4; distance += trunk ? 28 : 24)
        {
            var at = start + along * distance;
            p.Line(b, at - normal * (halfWidth + 1), at + normal * (halfWidth + 1), c.Structure, 4);
            p.Line(b, at - normal * halfWidth - along, at + normal * halfWidth - along, c.Edge * .15f);
        }
    }

    private static void DrawCoolingBank(SpriteBatch b, PrimitiveRenderer p, Rectangle bounds, float time)
    {
        var c = ColorPalette.Rainline;
        p.FillRect(b, new Rectangle(bounds.X + 5, bounds.Y + 8, bounds.Width + 3, bounds.Height + 4), ColorPalette.TowerShadow * .85f);
        p.FillRect(b, new Rectangle(bounds.X + 2, bounds.Bottom, bounds.Width, 6), Color.Lerp(c.Ground, c.Structure, .4f));
        p.FillRect(b, bounds, Color.Lerp(c.Ground, c.Structure, .6f));
        var grille = new Rectangle(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12);
        p.FillRect(b, grille, c.Ground);
        foreach (var x in new[] { bounds.Left + 25, bounds.Right - 25 })
        {
            var fan = new Vector2(x, bounds.Center.Y);
            p.Circle(b, fan + new Vector2(1, 2), 13, ColorPalette.Canvas);
            p.Ring(b, fan, 12, c.Detail * .65f, 2);
            for (var blade = 0; blade < 4; blade++)
            {
                var angle = blade * MathHelper.PiOver2 + time * .7f + bounds.X;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                p.Line(b, fan + direction * 3, fan + direction * 9, c.Detail * .85f, 3);
            }
            p.Circle(b, fan, 3, c.Structure);
        }
        for (var y = grille.Top + 3; y < grille.Bottom; y += 5)
            p.Line(b, new Vector2(grille.Left, y), new Vector2(grille.Right, y), c.Structure * .48f);
        p.Line(b, new Vector2(bounds.Left + 4, bounds.Top + 1), new Vector2(bounds.Right - 4, bounds.Top + 1), c.Edge * .19f);
        var light = new Vector2(bounds.Left + 10, bounds.Bottom - 3);
        p.Line(b, light, light + new Vector2(10, 0), c.Accent * .45f, 2);
        p.FillRect(b, new Rectangle(bounds.Right - 9, bounds.Bottom - 5, 2, 2), ColorPalette.Amber * .45f);
        p.Glow(b, light + new Vector2(5, 0), 36, c.Accent, .18f);
        for (var band = 0; band < 7; band++)
        {
            var at = light + new Vector2(5 + band % 2, 12 + band * 3);
            var halfWidth = 8 - band * .7f;
            p.Line(b, at - new Vector2(halfWidth, 0), at + new Vector2(halfWidth, 0), c.Accent * (.085f - band * .009f));
        }
    }

    public static void Weather(SpriteBatch b, PrimitiveRenderer p, float time)
    {
        var c = ColorPalette.Rainline;
        for (var i = 0; i < 54; i++)
        {
            var x = (i * 149 + time * 13) % 960;
            var y = 58 + (i * 83 + time * 92) % 654;
            p.Line(b, new Vector2(x, y), new Vector2(x - 2, y + 8), c.Edge * .11f);
        }
    }
}
