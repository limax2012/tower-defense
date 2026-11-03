using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Inset conductors follow the physical rails, cooling banks, and housings of each family.</summary>
internal static class TowerHardwareChannels
{
    internal static void DrawOperational(TowerStatusFrame frame, string id, bool glow, float opacity = 1,
        float firing = 0, float power = 1) => DrawConductors(frame, id, protocol: false, glow, opacity, Math.Clamp(firing, 0, 1), power);

    internal static void DrawProtocol(TowerStatusFrame frame, string id, bool active, bool glow, float opacity = 1,
        float firing = 0) => DrawConductors(frame, id, protocol: active, glow, opacity, Math.Clamp(firing, 0, 1));

    internal static void DrawInterference(TowerStatusFrame frame, string id, bool shutdown, bool glow) =>
        DrawConductors(frame, id, false, glow, 1, 0, interference: shutdown ? 2 : 1);

    private static void DrawConductors(TowerStatusFrame frame, string id, bool protocol, bool glow,
        float opacity, float firing, float power = 1, int interference = 0)
    {
        var d = new ChannelBrush(frame, NightGridArt.TowerAccent(id), true, glow, opacity,
            receiver: false, protocol, firing, power, interference);
        var stage = frame.Stage;
        switch (id)
        {
            case "needle_turret":
                var spread = stage == 1 ? .16f : stage == 2 ? .21f : .26f;
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    d.Route(.34f, sign * spread, .71f, sign * spread);
                    if (stage >= 2) d.Route(-.50f, sign * .48f, -.24f, sign * .47f);
                    if (stage == 4) d.Route(-.53f, sign * .71f, -.23f, sign * .65f);
                }
                d.Source(.38f, 0, .42f);
                break;
            case "watchtower":
                d.Route(.33f, 0, stage >= 3 ? 1.08f : .88f, 0);
                d.Route(-.34f, -.095f, -.04f, -.095f);
                d.Route(-.34f, .095f, -.04f, .095f);
                if (stage >= 3)
                    for (var sign = -1; sign <= 1; sign += 2)
                        d.Route(.46f, sign * .19f, .93f, sign * .19f);
                d.Source(-.17f, 0, .35f);
                break;
            case "shard_fan":
                var shardCore = TowerTierVisuals.ShardCoreRadius(stage);
                var shardVane = TowerTierVisuals.ShardVaneCenter(stage);
                var shardSplit = TowerTierVisuals.ShardVaneSplit(stage);
                for (var i = 0; i < 3; i++)
                    for (var petal = stage < 3 ? 0 : -1; petal <= (stage < 3 ? 0 : 1); petal += 2)
                    {
                        var angle = i * MathHelper.TwoPi / 3 + petal * shardSplit;
                        var x = MathF.Cos(angle);
                        var y = MathF.Sin(angle);
                        d.Route(x * (shardCore + .13f), y * (shardCore + .13f),
                            x * (shardVane - .05f), y * (shardVane - .05f));
                    }
                d.Source(0, 0, shardCore * 1.65f);
                break;
            case "frost_spire":
                var banks = TowerTierVisuals.FrostCoolantBanks(stage);
                var offset = TowerTierVisuals.FrostCoolantOffset(stage);
                var length = TowerTierVisuals.FrostCoolantLength(stage) * .63f;
                for (var i = 0; i < banks; i++)
                    BankRoute(d, MathHelper.PiOver2 + i * MathHelper.TwoPi / banks, offset, -length, length);
                d.Source(0, 0, TowerTierVisuals.FrostRotorRadius(stage) * 1.20f);
                break;
            case "ember_coil":
                var chamber = TowerTierVisuals.EmberChamberRadius(stage);
                d.Route(chamber + .02f, 0, TowerTierVisuals.EmberNozzleLength(stage) - .21f, 0);
                if (stage == 1)
                {
                    d.Route(-.10f, -.22f, .10f, -.22f, .075f);
                    d.Route(-.10f, .22f, .10f, .22f, .075f);
                }
                else
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        if (stage == 2) d.Route(-.43f, sign * .57f, -.26f, sign * .57f);
                        else d.Route(-.56f, sign * .57f, -.46f, sign * .57f);
                        if (stage >= 3) d.Route(.05f, sign * .72f, .37f, sign * .72f);
                    }
                d.Source(0, 0, chamber * 1.35f);
                break;
            case "breaker_cannon":
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var bank = stage == 1 ? .29f : .35f;
                    d.Route(-.40f, sign * bank, -.15f, sign * bank, stage == 1 ? .08f : .10f);
                    if (stage >= 2) d.Route(.16f, sign * .18f, stage == 2 ? .50f : .55f, sign * .18f);
                    if (stage >= 3) d.Route(-.66f, sign * .67f, -.37f, sign * .67f);
                    if (stage == 4) d.Route(-.45f, sign * .84f, -.14f, sign * .79f);
                    d.Source(-.27f, sign * bank, .29f);
                }
                break;
            case "arc_relay":
                var poles = stage == 1 ? 2 : 3;
                for (var i = 0; i < poles; i++)
                {
                    var angle = stage == 1 ? (i == 0 ? -.8f : .8f) : i * MathHelper.TwoPi / 3;
                    var end = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (stage == 4 ? .87f : .68f);
                    var start = Vector2.Lerp(new Vector2(-.20f, 0), end, .50f);
                    var stop = Vector2.Lerp(new Vector2(-.20f, 0), end, .83f);
                    d.Route(start.X, start.Y, stop.X, stop.Y);
                    d.Source(end.X, end.Y, .36f);
                }
                if (stage >= 3)
                    for (var sign = -1; sign <= 1; sign += 2)
                        d.Route(-.76f, sign * .37f, -.66f, sign * .59f);
                break;
            case "siege_mortar":
                var well = stage switch { 1 => .40f, 2 => .48f, 3 => .56f, _ => .60f };
                for (var i = 0; i < 4; i++)
                    BankRoute(d, MathHelper.PiOver4 + i * MathHelper.PiOver2, well - .07f, -.07f, .07f, .06f);
                if (stage >= 2)
                {
                    var count = stage == 2 ? 2 : 4;
                    var half = stage == 4 ? .38f : .28f;
                    for (var i = 0; i < count; i++)
                        BankRoute(d, MathHelper.PiOver2 + i * MathHelper.TwoPi / count,
                            TowerTierVisuals.MortarBankOffset(stage), -half, half);
                }
                d.Source(0, 0, well * 1.05f);
                break;
            case "prism_beam":
                var span = stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f };
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    d.Route(-.40f, sign * span, -.22f, sign * span);
                    d.Route(.22f, sign * span, .31f, sign * span);
                    if (stage >= 3) d.Route(-.64f, sign * .57f, -.34f, sign * .73f);
                    if (stage == 4) d.Route(-.36f, sign * .89f, .20f, sign * .89f);
                }
                d.Source(0, 0, .60f + stage * .035f);
                break;
            case "signal_beacon":
                d.Route(-.04f, 0, .42f, 0);
                if (stage >= 2)
                    for (var i = 0; i < 3; i++)
                    {
                        var angle = i * MathHelper.TwoPi / 3;
                        var reach = stage >= 3 ? .90f : .69f;
                        var start = stage == 4 ? .78f : stage == 3 ? .68f : .56f;
                        d.Route(MathF.Cos(angle) * start, MathF.Sin(angle) * start,
                            MathF.Cos(angle) * reach, MathF.Sin(angle) * reach);
                    }
                if (stage == 4)
                    for (var sign = -1; sign <= 1; sign += 2)
                        d.Route(-.48f, sign * .66f, .22f, sign * .66f);
                d.Source(0, 0, .50f + stage * .04f);
                break;
        }
    }

    internal static void DrawReceiver(TowerStatusFrame frame, string id, bool active, bool glow, float opacity = 1)
    {
        var d = new ChannelBrush(frame, ColorPalette.BeaconHighlight, active, glow, opacity, receiver: true);
        var stage = frame.Stage;
        switch (id)
        {
            case "needle_turret":
                var rear = stage == 1 ? -.48f : -.56f;
                var height = stage == 1 ? .09f : .14f;
                d.Route(rear, -height, rear, height);
                break;
            case "watchtower":
                var watchRear = stage == 1 ? -.50f : -.65f;
                var watchHalfWidth = stage == 1 ? .12f : .15f;
                d.Route(watchRear, -watchHalfWidth, watchRear, watchHalfWidth);
                break;
            case "shard_fan":
                var receiverRadius = TowerTierVisuals.ShardReceiverRadius(stage);
                var receiverHalfWidth = stage < 3 ? .09f : stage == 3 ? .18f : .25f;
                for (var i = 0; i < 3; i++)
                    BankRoute(d, i * MathHelper.TwoPi / 3, receiverRadius,
                        -receiverHalfWidth, receiverHalfWidth);
                break;
            case "frost_spire":
                var banks = TowerTierVisuals.FrostCoolantBanks(stage);
                var offset = TowerTierVisuals.FrostCoolantOffset(stage);
                var length = TowerTierVisuals.FrostCoolantLength(stage);
                for (var i = 0; i < banks; i++)
                    for (var sign = -1; sign <= 1; sign += 2)
                        BankTerminal(d, MathHelper.PiOver2 + i * MathHelper.TwoPi / banks,
                            offset, sign * length * .87f, stage >= 3 ? .12f : .085f);
                break;
            case "ember_coil":
                if (stage == 1)
                    d.Route(-.23f, -.10f, -.23f, .10f);
                else
                    for (var sign = -1; sign <= 1; sign += 2)
                    {
                        if (stage == 2) d.Route(-.57f, sign * .57f, -.48f, sign * .57f);
                        else d.Route(-.59f, sign * .72f, -.36f, sign * .72f);
                    }
                break;
            case "breaker_cannon":
                if (stage == 1)
                    d.Route(-.35f, 0, -.08f, 0);
                else
                    for (var sign = -1; sign <= 1; sign += 2)
                        d.Route(-.34f, sign * .555f, -.15f, sign * .52f);
                break;
            case "arc_relay":
                if (stage == 1) d.Route(-.23f, -.075f, -.23f, .075f);
                else
                    for (var sign = -1; sign <= 1; sign += 2)
                        d.Route(-.62f, sign * .25f, -.38f, sign * .212f);
                break;
            case "siege_mortar":
                if (stage == 1)
                    for (var i = 0; i < 4; i++)
                        BankRoute(d, MathHelper.PiOver4 + i * MathHelper.PiOver2, .46f, -.07f, .07f);
                else
                {
                    var count = stage == 2 ? 2 : 4;
                    var half = stage == 4 ? .45f : .36f;
                    for (var i = 0; i < count; i++)
                        for (var sign = -1; sign <= 1; sign += 2)
                            BankTerminal(d, MathHelper.PiOver2 + i * MathHelper.TwoPi / count,
                                TowerTierVisuals.MortarBankOffset(stage), sign * half, .10f);
                }
                break;
            case "prism_beam":
                var span = stage switch { 1 => .42f, 2 => .50f, 3 => .59f, _ => .69f };
                for (var sign = -1; sign <= 1; sign += 2)
                    d.Route(.39f, sign * span, .51f, sign * span);
                break;
            case "signal_beacon":
                d.Route(-.28f, 0, -.13f, 0);
                break;
        }
    }

    private static void BankRoute(ChannelBrush d, float angle, float offset, float first, float last, float width = 0)
    {
        var x = MathF.Cos(angle);
        var y = MathF.Sin(angle);
        d.Route(x * offset - y * first, y * offset + x * first,
            x * offset - y * last, y * offset + x * last, width);
    }

    private static void BankTerminal(ChannelBrush d, float angle, float offset, float at, float halfLength)
    {
        var x = MathF.Cos(angle);
        var y = MathF.Sin(angle);
        d.Route(x * (offset - halfLength) - y * at, y * (offset - halfLength) + x * at,
            x * (offset + halfLength) - y * at, y * (offset + halfLength) + x * at);
    }

    private readonly struct ChannelBrush(TowerStatusFrame frame, Color energy, bool active, bool glow,
        float opacity, bool receiver, bool protocol = false, float firing = 0, float power = 1, int interference = 0)
    {
        internal void Source(float x, float y, float size)
        {
            if (!glow || interference != 0 || power < .5f) return;
            // Local spill comes from the emitter or capacitor; armor retains its unlit material value.
            var strength = (protocol ? .20f : .13f) + firing * .16f;
            frame.Glow(x, y, size, energy, strength * opacity);
        }

        internal void Route(float x1, float y1, float x2, float y2, float width = 0)
        {
            if (width <= 0) width = receiver ? .075f : .10f;
            if (interference != 0)
            {
                var faultStart = new Vector2(x1, y1);
                var faultEnd = new Vector2(x2, y2);
                var shutdown = interference == 2;
                var fault = shutdown ? ColorPalette.Disruption : ColorPalette.Orange;
                var intensity = shutdown ? .38f + frame.Beat(8) * .62f : .70f + frame.Beat(17) * .25f;
                frame.Line(x1, y1, x2, y2, ColorPalette.Navy, width + .035f);
                // Signal faults interrupt existing conductors; no geometry crosses the weapon aperture.
                for (var segment = 0; segment < 2; segment++)
                {
                    // Jammer interference flows backward through the weapon's conductors.
                    var offset = shutdown ? segment * .78f : (1 - frame.Phase(2.3f) + segment * .5f) % 1;
                    var faultFirst = Vector2.Lerp(faultStart, faultEnd, offset * (shutdown ? 1 : .72f));
                    var faultLast = Vector2.Lerp(faultStart, faultEnd, shutdown ? offset + .22f : offset * .72f + .28f);
                    frame.Line(faultFirst.X, faultFirst.Y, faultLast.X, faultLast.Y, fault * intensity, width * .85f);
                    if (glow) frame.Glow(faultFirst.X, faultFirst.Y, .20f, fault, intensity * .20f);
                }
                return;
            }
            if (!receiver)
            {
                var breath = frame.Beat(2.1f);
                var illumination = protocol ? .94f : .78f + breath * .035f;
                var lit = Color.Lerp(ColorPalette.Navy, Color.Lerp(ColorPalette.Metal, energy, illumination), power);
                var hot = Color.Lerp(ColorPalette.Navy,
                    Color.Lerp(energy, ColorPalette.Paper, (protocol ? .25f : .09f) + firing * .28f), power);
                frame.Line(x1, y1, x2, y2, ColorPalette.Navy * opacity, width + .035f);
                frame.Line(x1, y1, x2, y2, lit * opacity, width * .82f);
                frame.Line(x1, y1, x2, y2, hot * ((.70f + firing * .25f) * opacity), width * .40f);
                return;
            }
            if (!active)
            {
                frame.Line(x1, y1, x2, y2, ColorPalette.Navy * opacity, width + .045f);
                frame.Line(x1, y1, x2, y2, ColorPalette.Circuit * opacity, width * .58f);
                return;
            }

            var beat = frame.Beat(receiver ? 4.6f : 6.8f);
            var bright = Color.Lerp(energy, ColorPalette.Paper, receiver ? .06f : .18f);
            if (glow)
                frame.Glow((x1 + x2) * .5f, (y1 + y2) * .5f, receiver ? .34f : .50f,
                    energy, (receiver ? .28f : .43f + beat * .20f) * opacity);
            frame.Line(x1, y1, x2, y2, bright * ((.82f + .18f * beat) * opacity), width);
            var start = new Vector2(x1, y1);
            var end = new Vector2(x2, y2);
            var phase = frame.Phase(receiver ? .70f : 1.7f);
            var first = Vector2.Lerp(start, end, phase * .64f);
            var last = Vector2.Lerp(start, end, phase * .64f + .36f);
            frame.Line(first.X, first.Y, last.X, last.Y,
                Color.Lerp(energy, ColorPalette.Paper, receiver ? .35f : .80f) * opacity, width * .43f);
        }
    }
}
