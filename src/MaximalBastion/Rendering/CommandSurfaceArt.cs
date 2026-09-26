using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Graphite command surfaces and peripheral power-distribution infrastructure.</summary>
internal static class CommandSurfaceArt
{
    internal static int UpperConduitTop(Rectangle bounds) =>
        InfrastructureTop(bounds) - 18 - 6;

    private static int InfrastructureTop(Rectangle bounds) => bounds.Top + (bounds.Width < 800 ? 280 : 118);

    public static void Surface(SpriteBatch b, PrimitiveRenderer p, Rectangle r, Color accent, float time,
        bool atmosphere = false)
    {
        p.FillRect(b, r, ColorPalette.Panel);
        for (var y = 0; y < r.Height; y += 6)
        {
            var edge = MathF.Abs(y / (float)r.Height - .5f) * 2;
            p.FillRect(b, new Rectangle(r.X, r.Y + y, r.Width, Math.Min(6, r.Height - y)),
                ColorPalette.Surface(accent, .012f + edge * edge * .045f));
        }
        p.Line(b, new Vector2(r.Left + 1, r.Top + 1), new Vector2(r.Right - 2, r.Top + 1), ColorPalette.Metal * .65f);
        p.Line(b, new Vector2(r.Left + 1, r.Bottom - 2), new Vector2(r.Right - 2, r.Bottom - 2), ColorPalette.Canvas);
        if (r.Height > 130)
        {
            foreach (var x in new[] { r.Left + 10, r.Right - 11 })
            foreach (var y in new[] { r.Top + 11, r.Bottom - 12 })
            {
                p.Circle(b, new Vector2(x, y), 2, ColorPalette.Canvas);
                p.Line(b, new Vector2(x - 1, y), new Vector2(x + 1, y), ColorPalette.Metal);
            }
        }
        if (atmosphere) Infrastructure(b, p, r, accent, time);
    }

    public static void Infrastructure(SpriteBatch b, PrimitiveRenderer p, Rectangle r, Color accent, float time)
    {
        if (r.Width < 400 || r.Height < 300) return;
        var width = Math.Min(205, (r.Width - 360) / 2);
        var top = InfrastructureTop(r);
        var bottom = r.Bottom - 68;
        if (width < 60 || bottom - top < 100) return;
        var power = new Rectangle(r.Left + 22, top, width, bottom - top);
        var server = new Rectangle(r.Right - 22 - width, top, width, bottom - top);
        var powerBus = power.Left + 14;
        var serverBus = server.Right - 14;

        // The same trunk enters both cabinets; its crossovers use the service
        // space above and below the controls rather than the central reading lane.
        Conduit(b, p, [new Vector2(powerBus, top + 14), new Vector2(powerBus, top - 18),
            new Vector2(serverBus, top - 18), new Vector2(serverBus, top + 14)], accent);
        Conduit(b, p, [new Vector2(powerBus, bottom - 14), new Vector2(powerBus, bottom + 18),
            new Vector2(serverBus, bottom + 18), new Vector2(serverBus, bottom - 14)], accent);
        Cabinet(b, p, power);
        Cabinet(b, p, server);
        Conduit(b, p, [new Vector2(powerBus, top), new Vector2(powerBus, bottom)], accent);
        Conduit(b, p, [new Vector2(serverBus, top), new Vector2(serverBus, bottom)], accent);

        for (var y = top + 16; y + 44 < bottom - 12; y += 62)
        {
            var distributor = new Rectangle(power.Left + 34, y, width - 46, 44);
            var sled = new Rectangle(server.Left + 12, y, width - 46, 44);
            ModuleConnection(b, p, new Vector2(powerBus, y + 22), new Vector2(distributor.Left, y + 22), ColorPalette.Amber);
            ModuleConnection(b, p, new Vector2(sled.Right, y + 22), new Vector2(serverBus, y + 22), accent);
            Module(b, p, distributor);
            Module(b, p, sled);

            // Recessed relay contacts distinguish power hardware from vented server sleds.
            var contacts = Math.Clamp(distributor.Width / 28, 2, 5);
            var contactGap = (distributor.Width - 18f) / contacts;
            for (var i = 0; i < contacts; i++)
            {
                var x = distributor.Left + 10 + i * contactGap;
                p.Line(b, new Vector2(x, y + 9), new Vector2(x, y + 34), ColorPalette.Metal * .72f, 4);
                p.Line(b, new Vector2(x, y + 15), new Vector2(x, y + 27), ColorPalette.Amber * .32f, 2);
                p.FillRect(b, new Rectangle((int)x - 3, y + 18, 6, 6), ColorPalette.PanelAlt);
            }
            p.Line(b, new Vector2(distributor.Right - 9, y + 9), new Vector2(distributor.Right - 9, y + 15), ColorPalette.Amber * .5f, 2);
            p.Line(b, new Vector2(sled.Left + 9, y + 9), new Vector2(sled.Left + 9, y + 15), accent * .48f, 2);
            for (var x = sled.Left + 22; x < sled.Right - 7; x += 6)
                p.Line(b, new Vector2(x, y + 10), new Vector2(x, y + 30), ColorPalette.Metal * .48f);
            p.Line(b, new Vector2(sled.Left + 9, y + 35), new Vector2(sled.Right - 9, y + 35), ColorPalette.Metal * .46f);
        }

        var phase = time * 17 % Math.Max(1, bottom - top - 36);
        p.Line(b, new Vector2(powerBus - 1.5f, top + 14 + phase),
            new Vector2(powerBus - 1.5f, top + 26 + phase), ColorPalette.Amber * .6f);
        p.Line(b, new Vector2(serverBus + 1.5f, bottom - 14 - phase),
            new Vector2(serverBus + 1.5f, bottom - 26 - phase), accent * .55f);
    }

    private static void Cabinet(SpriteBatch b, PrimitiveRenderer p, Rectangle bounds)
    {
        p.FillRect(b, new Rectangle(bounds.X + 3, bounds.Y + 5, bounds.Width, bounds.Height), ColorPalette.Canvas * .8f);
        p.FillRect(b, bounds, ColorPalette.PanelAlt * .8f);
        p.DrawRect(b, bounds, ColorPalette.Metal * .52f);
        p.FillRect(b, new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width - 10, bounds.Height - 10), ColorPalette.Navy * .48f);
        p.Line(b, new Vector2(bounds.Left + 3, bounds.Top + 2), new Vector2(bounds.Right - 3, bounds.Top + 2), ColorPalette.Metal * .65f, 2);
        foreach (var x in new[] { bounds.Left + 6, bounds.Right - 7 })
        foreach (var y in new[] { bounds.Top + 7, bounds.Bottom - 8 })
        {
            p.Circle(b, new Vector2(x, y), 2, ColorPalette.Canvas);
            p.Line(b, new Vector2(x - 1, y), new Vector2(x + 1, y), ColorPalette.Metal * .7f);
        }
    }

    private static void Module(SpriteBatch b, PrimitiveRenderer p, Rectangle bounds)
    {
        p.FillRect(b, bounds, ColorPalette.Canvas * .7f);
        p.Line(b, new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), ColorPalette.Metal * .5f);
        p.Line(b, new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom), ColorPalette.Metal * .35f);
    }

    private static void ModuleConnection(SpriteBatch b, PrimitiveRenderer p, Vector2 start, Vector2 end, Color accent)
    {
        p.Line(b, start, end, ColorPalette.Canvas, 7);
        p.Line(b, start, end, ColorPalette.Metal * .7f, 4);
        p.Line(b, start, end, ColorPalette.Navy, 2);
        p.Line(b, start, end, accent * .25f);
    }

    private static void Conduit(SpriteBatch b, PrimitiveRenderer p, Vector2[] route, Color accent)
    {
        foreach (var (color, thickness) in new[] { (ColorPalette.Canvas, 12),
                     (ColorPalette.Metal * .58f, 8), (ColorPalette.Navy, 5) })
        {
            for (var i = 1; i < route.Length; i++) p.Line(b, route[i - 1], route[i], color, thickness);
            foreach (var point in route)
                p.FillRect(b, new Rectangle((int)point.X - thickness / 2, (int)point.Y - thickness / 2, thickness, thickness), color);
        }
        for (var conductor = -1; conductor <= 1; conductor += 2)
        {
            var offset = conductor * 1.5f;
            Vector2 OffsetAt(int i)
            {
                var before = Vector2.Normalize(route[Math.Max(1, i)] - route[Math.Max(0, i - 1)]);
                var after = Vector2.Normalize(route[Math.Min(route.Length - 1, i + 1)] - route[Math.Min(route.Length - 2, i)]);
                var incoming = new Vector2(-before.Y, before.X);
                var outgoing = new Vector2(-after.Y, after.X);
                var miter = incoming + outgoing;
                return route[i] + miter * (offset / Vector2.Dot(miter, incoming));
            }
            var color = conductor < 0 ? ColorPalette.Amber * .24f : accent * .24f;
            for (var i = 1; i < route.Length; i++) p.Line(b, OffsetAt(i - 1), OffsetAt(i), color);
        }
    }
}
