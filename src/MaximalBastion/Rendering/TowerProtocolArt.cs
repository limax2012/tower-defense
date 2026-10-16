using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Active protocols energize the weapon's physical power and cooling routes.</summary>
public static class TowerProtocolArt
{
    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, float time, bool reducedEffects = false, bool glow = true) =>
        Draw(b, p, center, radius, towerId, 1, false, -MathHelper.PiOver2, time, reducedEffects, glow);

    public static void Draw(SpriteBatch b, PrimitiveRenderer p, Vector2 center, float radius,
        string towerId, int level, bool apex, float aim, float time, bool reducedEffects = false, bool glow = true)
    {
        var d = new TowerStatusFrame(b, p, center, radius, towerId, level, apex, aim, time, reducedEffects);
        var energy = NightGridArt.TowerAccent(towerId);
        var color = Color.Lerp(energy, ColorPalette.Paper, .12f + d.Beat(5.5f) * .12f);
        var hot = Color.Lerp(energy, ColorPalette.Paper, .72f);
        var phase = d.Phase(.85f);
        void Route(float x1, float y1, float x2, float y2) => ChargeRoute(d,
            new Vector2(x1, y1), new Vector2(x2, y2), color, hot, phase, reducedEffects);

        switch (towerId)
        {
            case "needle_turret":
                var rail = d.Stage == 1 ? .16f : d.Stage == 2 ? .21f : .26f;
                var needle = NightGridArt.TowerMuzzlePosition(Vector2.Zero, 1, towerId, level, 0, apex: apex).X;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Route(-.12f, sign * rail, needle - .15f, sign * rail);
                    if (d.Stage >= 2) Route(-.51f, sign * .48f, -.22f, sign * .47f);
                }
                break;
            case "watchtower":
                var watch = NightGridArt.TowerMuzzlePosition(Vector2.Zero, 1, towerId, level, 0, apex: apex).X;
                Route(.16f, -.10f, watch - .25f, -.10f);
                Route(.16f, .10f, watch - .25f, .10f);
                if (d.Stage >= 2) Route(-.34f, -.44f, .10f, -.44f);
                break;
            case "shard_fan":
                var fan = NightGridArt.TowerMuzzlePosition(Vector2.Zero, 1, towerId, level, 0, apex: apex).X;
                var spread = d.Stage == 1 ? .43f : d.Stage == 4 ? .69f : .57f;
                for (var index = -1; index <= 1; index++)
                    Route(-.15f, index * .12f, fan - .23f, index * spread * .82f);
                break;
            case "frost_spire":
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var side = d.Stage == 1 ? .38f : .64f;
                    Route(-.43f, sign * side, .43f, sign * side);
                    if (d.Stage >= 3) Route(-.55f, sign * .49f, -.13f, sign * .69f);
                }
                break;
            case "ember_coil":
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var side = d.Stage == 1 ? .35f : .57f;
                    Route(-.48f, sign * side, .10f, sign * side);
                    Route(.38f, sign * .08f, .81f, sign * .08f);
                }
                break;
            case "breaker_cannon":
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Route(-.43f, sign * (d.Stage == 1 ? .30f : .49f), -.13f, sign * (d.Stage == 1 ? .30f : .46f));
                    Route(.14f, sign * .18f, .66f, sign * .18f);
                }
                break;
            case "arc_relay":
                var electrodes = d.Stage == 1 ? 2 : 3;
                for (var index = 0; index < electrodes; index++)
                {
                    var a = d.Stage == 1 ? (index == 0 ? -.8f : .8f) : index * MathHelper.TwoPi / 3;
                    var reach = d.Stage == 4 ? .87f : .68f;
                    Route(-.12f, 0, MathF.Cos(a) * reach * .78f, MathF.Sin(a) * reach * .78f);
                }
                break;
            case "siege_mortar":
                var well = d.Stage == 1 ? .40f : d.Stage == 2 ? .48f : d.Stage == 3 ? .56f : .60f;
                for (var index = 0; index < 4; index++)
                {
                    var a = index * MathHelper.PiOver2 + MathHelper.PiOver4;
                    var x = MathF.Cos(a);
                    var y = MathF.Sin(a);
                    Route(x * (well - .19f), y * (well - .19f), x * (well + .06f), y * (well + .06f));
                }
                break;
            case "prism_beam":
                var span = d.Stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f };
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    Route(-.40f, sign * span, .36f, sign * span);
                    if (d.Stage >= 3) Route(.60f, sign * .40f, .93f, sign * .31f);
                }
                break;
            case "signal_beacon":
                var antenna = d.Stage switch { 1 => .35f, 2 => .44f, 3 => .52f, _ => .61f };
                for (var index = 0; index < 3; index++)
                {
                    var a = index * MathHelper.TwoPi / 3;
                    Route(MathF.Cos(a) * .12f, MathF.Sin(a) * .12f,
                        MathF.Cos(a) * antenna, MathF.Sin(a) * antenna);
                }
                break;
        }
        if (!reducedEffects && glow)
            d.Glow(-.10f, 0, .85f, energy, .13f + d.Beat(5.5f) * .07f);
    }

    private static void ChargeRoute(TowerStatusFrame d, Vector2 start, Vector2 end,
        Color color, Color hot, float phase, bool reducedEffects)
    {
        d.Trace(start.X, start.Y, end.X, end.Y, color, .065f);
        var first = Vector2.Lerp(start, end, reducedEffects ? .35f : phase * .70f);
        var second = Vector2.Lerp(start, end, reducedEffects ? .65f : phase * .70f + .30f);
        d.Line(first.X, first.Y, second.X, second.Y, hot, .035f);
    }
}
