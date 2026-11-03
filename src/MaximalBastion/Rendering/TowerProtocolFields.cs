using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public static partial class TowerProtocolArt
{
    private static void DrawFlow(ProtocolField d, string id)
    {
        var (count, speed, inward, swirl, length, width) = id switch
        {
            "needle_turret" => (18, 1.80f, true, 0f, .18f, .045f),
            "watchtower" => (12, .80f, true, 0f, .27f, .035f),
            "shard_fan" => (18, 1.35f, false, .18f, .15f, .10f),
            "frost_spire" => (20, .75f, true, .80f, .095f, .075f),
            "ember_coil" => (18, .95f, false, .12f, .10f, .11f),
            "breaker_cannon" => (12, .72f, false, 0f, .15f, .12f),
            "arc_relay" => (12, 1.60f, false, 0f, .075f, .055f),
            "siege_mortar" => (14, .68f, false, 0f, .19f, .10f),
            "prism_beam" => (16, 1.10f, true, .10f, .20f, .065f),
            _ => (12, .95f, false, 0f, .12f, .065f)
        };
        var color = FieldColor(id);
        for (var i = 0; i < count; i++)
        {
            var phase = d.Phase(speed, i * .618034f);
            var a = i * 2.399963f + swirl * (d.Time * .35f + phase);
            var distance = inward ? MathHelper.Lerp(1.97f, .85f, phase) : MathHelper.Lerp(.85f, 1.97f, phase);
            var at = Polar(a, distance);
            var direction = Polar(a + (inward ? MathHelper.Pi : 0), length);
            var intensity = MathF.Sin(phase * MathHelper.Pi) * .88f;
            var hot = Color.Lerp(color, ColorPalette.Paper, id is "watchtower" or "prism_beam" ? .40f : .17f);
            // Short, staggered discharges distribute energy across the entire field volume.
            d.Line(at - direction * .5f, at + direction * .5f, color, width, intensity);
            switch (id)
            {
                case "frost_spire":
                    var frost = Polar(a + MathHelper.PiOver2, .045f);
                    d.Line(at - frost, at + frost, ColorPalette.Paper, .025f, intensity * .70f);
                    break;
                case "prism_beam":
                    // Refraction splits a short incoming ray into two concentrated paths.
                    var refracted = Polar(a + MathHelper.Pi + (i % 2 == 0 ? .36f : -.36f), length * .55f);
                    d.Line(at, at + refracted, hot, .035f, intensity * .80f);
                    break;
                case "signal_beacon":
                    var offset = direction * 1.05f;
                    d.Line(at - offset - direction * .25f, at - offset + direction * .25f,
                        ColorPalette.BeaconHighlight, width * .70f, intensity * .65f);
                    break;
                case "breaker_cannon":
                case "siege_mortar":
                    d.Line(at - direction * .18f, at + direction * .28f, hot, width * .44f, intensity);
                    break;
                case "ember_coil":
                    d.Line(at, at + direction * .25f, ColorPalette.EmberFlame, width * .45f, intensity);
                    break;
                default:
                    d.Line(at, at + direction * .38f, hot, width * .40f, intensity * .85f);
                    break;
            }
        }
        if (id == "arc_relay") DrawElectricalSurges(d);
    }

    private static void DrawElectricalSurges(ProtocolField d)
    {
        for (var i = 0; i < 7; i++)
        {
            var phase = d.Phase(1.35f, i * .618034f);
            var angle = i * 2.399963f + .16f * MathF.Sin(d.Time * 4 + i);
            var start = Polar(angle, .88f + phase * .25f);
            var end = Polar(angle + .13f, 1.52f + phase * .22f);
            var transverse = Polar(angle + MathHelper.PiOver2, .10f * MathF.Sin(d.Time * 29 + i * 2));
            var first = Vector2.Lerp(start, end, .32f) + transverse;
            var second = Vector2.Lerp(start, end, .70f) - transverse;
            var strength = .30f + .60f * MathF.Sin(phase * MathHelper.Pi);
            d.Line(start, first, ColorPalette.ArcShot, .07f, strength);
            d.Line(first, second, ColorPalette.ArcShot, .06f, strength);
            d.Line(second, end, ColorPalette.ArcShot, .045f, strength);
            d.Line(first, first + Polar(angle - .75f, .20f), ColorPalette.ArcShot, .035f, strength * .7f);
            d.Line(first, second, ColorPalette.Paper, .025f, strength * .6f);
        }
    }
}
