using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Presentation geometry for the district network and its surveillance feeds.</summary>
internal static class DefenseTerminalArt
{
    public static void Background(SpriteBatch b, PrimitiveRenderer p, float time, bool feeds = true)
    {
        CommandSurfaceArt.Surface(b, p, new Rectangle(0, 0, 1280, 720), ColorPalette.Cyan, time);
        if (feeds)
        foreach (var x in new[] { 20, 980 })
        {
            var frame = new Rectangle(x, 24, 280, 674);
            p.FillRect(b, frame, ColorPalette.Ink * .35f);
            p.DrawRect(b, frame, ColorPalette.CardOutline * .7f);
            p.Brackets(b, frame, ColorPalette.Circuit, 22);
            p.Line(b, new Vector2(x + 12, 59), new Vector2(x + 268, 59), ColorPalette.Divider);
            for (var y = 96; y < 662; y += 32)
                p.Line(b, new Vector2(x + 3, y), new Vector2(x + 8, y), ColorPalette.Muted * .35f);
            var scan = 70 + time * 19 % 584;
            p.Line(b, new Vector2(x + 12, scan), new Vector2(x + 268, scan), ColorPalette.Cyan * .09f);
        }
        p.FillRect(b, new Rectangle(0, 0, 1280, 2), ColorPalette.Circuit);
        p.FillRect(b, new Rectangle(0, 718, 1280, 2), ColorPalette.Circuit);
    }

    public static void District(SpriteBatch b, PrimitiveRenderer p, Rectangle r, string style,
        IReadOnlyList<Vector2> path, float time, bool hovered)
    {
        var c = ColorPalette.Environment(style);
        p.FillRect(b, r, c.Ground);
        var scene = new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 58);
        var center = scene.Center.ToVector2();
        var scale = MathF.Min(scene.Width / 120f, scene.Height / 72f);
        Vector2 At(float x, float y) => new(scene.X + x * scene.Width / 120f, scene.Y + y * scene.Height / 72f);
        Rectangle Rect(float x, float y, float w, float h) => new((int)At(x, y).X, (int)At(x, y).Y,
            Math.Max(1, (int)(w * scene.Width / 120f)), Math.Max(1, (int)(h * scene.Height / 72f)));
        var ceramic = Color.Lerp(ColorPalette.Metal, ColorPalette.Paper, .34f);
        void Housing(Rectangle bounds)
        {
            p.FillRect(b, new Rectangle(bounds.X + 2, bounds.Y + 4, bounds.Width, bounds.Height), ColorPalette.Ink);
            p.FillRect(b, bounds, Color.Lerp(ColorPalette.Metal, c.Structure, .36f));
            p.FillRect(b, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6), ColorPalette.Panel);
            p.Line(b, new Vector2(bounds.Left + 2, bounds.Top), new Vector2(bounds.Right - 2, bounds.Top), ceramic * .65f, 1);
        }
        if (style == "foundry")
        {
            p.Line(b, At(4, 62), At(116, 62), ColorPalette.Ink, 7);
            p.Line(b, At(4, 61), At(116, 61), c.Detail, 4);
            p.Line(b, At(4, 60), At(116, 60), c.Edge * .45f, 1);
            for (var i = 0; i < 4; i++)
            {
                var x = 3 + i * 29;
                var y = 12 + i % 2 * 9;
                var furnace = Rect(x, y, 20, 44);
                Housing(furnace);
                for (var bar = 0; bar < 3; bar++)
                {
                    p.FillRect(b, Rect(x + 4 + bar * 5, y + 5, 2, 32), c.Accent * .46f);
                    p.FillRect(b, Rect(x + 4.5f + bar * 5, y + 11, .5f, 20), ColorPalette.Gold * .55f);
                }
                p.Glow(b, furnace.Center.ToVector2(), 9 * scale, c.Accent, .5f);
                for (var slat = 0; slat < 3; slat++)
                    p.FillRect(b, Rect(x + 1, y + 11 + slat * 10, 18, 2), c.Detail);
                p.FillRect(b, Rect(x + 1, y + 1, 18, 3), ColorPalette.Metal);
                for (var stripe = 0; stripe < 3; stripe++)
                    p.Line(b, At(x + 3 + stripe * 5, y + 1), At(x + 5 + stripe * 5, y + 4), c.Accent * .8f, 2);
                p.FillRect(b, Rect(x + 18, y + 6, 1, 5), ColorPalette.Cyan * .8f);
            }
        }
        else if (style == "trail")
        {
            var gallery = Color.Lerp(c.Ground, ColorPalette.Ink, .65f);
            var pipe = Color.Lerp(c.Ground, c.Edge, .28f);
            foreach (var x in new[] { 17, 76 })
            {
                p.FillRect(b, Rect(x - 4, 0, 8, 72), gallery);
                p.Line(b, At(x - 1.5f, 0), At(x - 1.5f, 72), pipe, 2);
                p.Line(b, At(x + 1.5f, 0), At(x + 1.5f, 72), pipe, 2);
                for (var clamp = 4; clamp < 72; clamp += 12)
                    p.Line(b, At(x - 3, clamp), At(x + 3, clamp), c.Detail * .6f, 2);
            }
            p.FillRect(b, Rect(0, 60, 120, 8), gallery);
            foreach (var y in new[] { 62.5f, 65.5f })
                p.Line(b, At(0, y), At(120, y), pipe, 2);
            for (var clamp = 5; clamp < 120; clamp += 12)
            {
                if (clamp is > 12 and < 22 or > 71 and < 81) continue;
                p.Line(b, At(clamp, 61), At(clamp, 67), c.Detail * .6f, 2);
            }
            void UtilityPlatform(float x, float y, float width, float height)
            {
                var roof = Rect(x, y, width, height);
                var projection = At(2, 6) - At(0, 0);
                var depth = Math.Max(1, (int)projection.Y);
                var offset = Math.Max(1, (int)projection.X);
                var wall = Color.Lerp(c.Ground, c.Structure, .70f);
                var side = Color.Lerp(c.Ground, c.Structure, .40f);
                p.FillRect(b, Rect(x + 3, y + 8, width, height), ColorPalette.Ink * .65f);
                for (var row = 0; row < depth; row++)
                    p.FillRect(b, new Rectangle(roof.Left + row * offset / depth,
                        roof.Bottom + row, roof.Width, 1), wall);
                for (var column = 0; column < offset; column++)
                    p.FillRect(b, new Rectangle(roof.Right + column,
                        roof.Top + column * depth / offset, 1, roof.Height), side);
                for (var rib = 1f; rib < width; rib += 6)
                    p.Line(b, At(x + rib, y + height), At(x + rib + 2, y + height + 6), c.Detail * .72f, 2);
                p.FillRect(b, Rect(x + 2, y + height + 1, width - 4, 3), gallery);
                for (var louver = 0; louver < 3; louver++)
                {
                    var drop = 1.5f + louver;
                    p.Line(b, At(x + 2 + drop / 3, y + height + drop),
                        At(x + width - 2 + drop / 3, y + height + drop), c.Detail * .65f, 1);
                }
                p.Line(b, At(x + 1, y + height), At(x + width / 2 + 2, y + height + 6), c.Edge * .34f, 1);
                p.Line(b, At(x + width - 1, y + height), At(x + width / 2 + 2, y + height + 6), c.Edge * .34f, 1);
                p.Line(b, At(x + 2, y + height + 6), At(x + width + 2, y + height + 6), c.Detail, 2);
                p.FillRect(b, Rect(x + width - 1, y + height + 1, .7f, 2), c.Accent * .7f);
                p.FillRect(b, roof, Color.Lerp(c.Structure, c.Detail, .28f));
                p.DrawRect(b, roof, c.Edge * .65f);
                p.DrawRect(b, Rect(x + 1, y + 1, width - 2, height - 2), c.Detail);
                for (var seam = 6f; seam < height - 2; seam += 6)
                    p.Line(b, At(x + 2, y + seam), At(x + width - 2, y + seam), c.Edge * .12f);
                p.FillRect(b, Rect(x + 2, y + 2, width - 4, 2), ColorPalette.Panel);
                for (var vent = 3f; vent < width - 2; vent += 2)
                    p.Line(b, At(x + vent, y + 2), At(x + vent, y + 4), c.Edge * .28f);
                p.Line(b, At(x, y), At(x + width, y), c.Accent * .65f, 1);
                p.Line(b, At(x + width, y + height), At(x + width + 2, y + height + 6), c.Edge * .25f, 1);
            }
            UtilityPlatform(26, 23, 15, 25);
            UtilityPlatform(55, 25, 15, 20);
            UtilityPlatform(84, 27, 15, 26);
            for (var rain = 0; rain < 12; rain++)
            {
                var x = 4 + rain * 29 % 111;
                var y = 3 + (rain * 19 + time * 13) % 62;
                p.Line(b, At(x, y), At(x - 1, y + 3), c.Edge * .25f, 1);
            }
        }
        else if (style == "prism")
        {
            for (var i = 0; i < 6; i++)
                p.Line(b, At(i * 23, 72), At(60, 16), c.Detail * .5f);
            foreach (var offset in new[] { new Vector2(-33, -10), new Vector2(31, 10) })
            {
                var cabinet = center + offset * scale + new Vector2(0, MathF.Sin(time * .5f + offset.X) * scale);
                p.DrawPolygon(b, cabinet + new Vector2(2, 5) * scale, 21 * scale, 4, false, ColorPalette.Ink);
                p.DrawPolygon(b, cabinet, 21 * scale, 4, false, ColorPalette.Metal);
                p.DrawPolygon(b, cabinet, 18 * scale, 4, false, ColorPalette.Panel);
                for (var module = -2; module <= 2; module++)
                {
                    var half = (15 - Math.Abs(module) * 3) * scale;
                    var y = cabinet.Y + module * 4 * scale;
                    p.Line(b, new Vector2(cabinet.X - half, y), new Vector2(cabinet.X + half, y), ColorPalette.Metal, 2 * scale);
                    p.Line(b, new Vector2(cabinet.X - half + 2, y - scale), new Vector2(cabinet.X + half - 2, y - scale), ceramic * .4f, 1);
                }
                for (var sign = -1; sign <= 1; sign += 2)
                    p.Line(b, cabinet - new Vector2(0, 19) * scale, cabinet + new Vector2(sign * 19, 0) * scale, ceramic, 2);
                p.Line(b, cabinet - new Vector2(0, 14) * scale, cabinet + new Vector2(0, 14) * scale, c.Accent * .65f, 2);
                p.DrawPolygon(b, cabinet, 4 * scale, 6, false, c.Accent);
                p.DrawPolygon(b, cabinet, 2 * scale, 6, false, ColorPalette.Paper * .8f);
                p.Glow(b, cabinet, 11 * scale, ColorPalette.Cyan, .24f);
            }
        }
        else
        {
            foreach (var offset in new[] { new Vector2(-26, -9), new Vector2(27, 12) })
            {
                var reactor = center + offset * scale;
                p.Circle(b, reactor + new Vector2(2, 4) * scale, 25 * scale, ColorPalette.Ink);
                p.Circle(b, reactor, 25 * scale, ColorPalette.Metal);
                p.Ring(b, reactor, 24 * scale, ceramic * .45f, 1);
                p.Circle(b, reactor, 22 * scale, c.Ground);
                p.DashedRing(b, reactor, 18 * scale, c.Edge * .8f, 24, 2, time * .12f);
                p.Ring(b, reactor, 13 * scale, c.Edge * .6f, 2);
                for (var rib = 0; rib < 8; rib++)
                {
                    var angle = rib * MathHelper.PiOver4;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    p.Line(b, reactor + direction * 18 * scale, reactor + direction * 24 * scale, ColorPalette.Metal, 4 * scale);
                    p.Line(b, reactor + direction * 19 * scale, reactor + direction * 23 * scale, ceramic * .6f, 1);
                }
                p.Glow(b, reactor, 19 * scale, c.Accent, .3f);
                p.DrawPolygon(b, reactor, 5 * scale, 6, false, c.Accent * .7f, time * .07f);
                p.DrawPolygon(b, reactor, 2.5f * scale, 6, false, ColorPalette.Paper * .6f, time * .07f);
            }
        }
        // Authored routes connect these terminal views to the actual arenas.
        Vector2 Project(Vector2 point) => new(scene.X + 6 + MathHelper.Clamp(point.X / 960, 0, 1) * (scene.Width - 12),
            scene.Y + 5 + MathHelper.Clamp(point.Y / 720, 0, 1) * (scene.Height - 10));
        for (var i = 1; i < path.Count; i++)
        {
            p.Line(b, Project(path[i - 1]), Project(path[i]), ColorPalette.Canvas, Math.Max(5, (int)(4 * scale)));
            p.Line(b, Project(path[i - 1]), Project(path[i]), c.Accent * .8f, Math.Max(2, (int)(1.5f * scale)));
        }
        p.DrawRect(b, r, hovered ? c.Accent : c.Edge * .6f);
        p.FillRect(b, new Rectangle(r.X + 1, r.Bottom - 42, r.Width - 2, 41), ColorPalette.Panel);
        p.Line(b, new Vector2(r.X + 9, r.Bottom - 42), new Vector2(r.Right - 9, r.Bottom - 42), c.Edge * .5f);
        if (hovered)
        {
            p.Brackets(b, r, c.Accent, 10);
            p.FillRect(b, scene, c.Accent * .07f);
        }
        var packet = new Vector2(scene.X + 4 + time * 15 % (scene.Width - 8), scene.Y + 2);
        p.Line(b, packet, packet + new Vector2(4, 0), c.Accent, 1);
    }
}
