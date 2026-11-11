namespace MaximalBastion.Audio;

internal enum WeaponSoundLayer { Shot, Impact, Sustain, Chain }

internal enum SoundCue
{
    Place, Upgrade, Apex, Sell, Protocol, KillSmall, KillArmored, KillLarge,
    ShieldHit, ShieldBreak, Leak, WaveStart, WaveClear, PlateDeploy, PlateTrigger,
    ForgePlace, ForgeUpgrade, ChargeReady, BossPhase, BossArrival, Victory, Defeat,
    UiNavigate, UiConfirm, UiBack, UiDelete, UiError
}

/// <summary>Device-independent PCM for colorful energy weapons and futuristic interface feedback.</summary>
internal static class ProceduralSoundBank
{
    internal const int SampleRate = 44100;
    internal const int VariantCount = 3;
    private const float Tau = MathF.PI * 2;
    internal static IReadOnlyList<string> WeaponIds { get; } = Array.AsReadOnly(new[]
    {
        "needle_turret", "watchtower", "shard_fan", "frost_spire", "ember_coil",
        "breaker_cannon", "siege_mortar", "arc_relay", "prism_beam", "signal_beacon"
    });

    internal static short[] CreateWeapon(string towerId, WeaponSoundLayer layer,
        int levelIndex = 0, bool apex = false, int variant = 0)
    {
        var id = towerId.ToLowerInvariant();
        if (!WeaponIds.Contains(id)) throw new ArgumentException("Unknown weapon sound family.", nameof(towerId));
        var stage = apex ? 3 : Math.Clamp(levelIndex, 0, 2);
        variant = (int)((uint)variant % VariantCount);
        if (layer == WeaponSoundLayer.Sustain)
        {
            if (id is not ("prism_beam" or "ember_coil"))
                throw new ArgumentException("This weapon does not have a continuous emitter.", nameof(layer));
            return CreateSustain(id, stage, variant);
        }

        var duration = WeaponDuration(id, layer);
        var sound = new Sound(duration + stage * .012f, Seed(id, stage, variant, (int)layer));
        var weight = 1 - stage * .055f;
        var detail = .72f + stage * .11f;
        var tune = 1 + (variant - 1) * .018f;
        if (layer == WeaponSoundLayer.Chain && id == "arc_relay")
        {
            // Short electrical leaps retain one discharge contour across the chain.
            sound.Zap(0, .062f, .78f, 1670 * tune, 680, .95f);
            sound.Zap(.024f, .055f, .44f, 1420 * tune, 540, 1.1f);
            sound.Noise(0, .028f, .10f, 1600, 4400, .001f, 65);
            return sound.Finish(.18f);
        }

        if (layer is WeaponSoundLayer.Impact or WeaponSoundLayer.Chain)
        {
            AddImpact(sound, id, stage, weight, detail, tune);
            return sound.Finish(id == "signal_beacon" ? .14f : .22f);
        }
        if (layer != WeaponSoundLayer.Shot) throw new ArgumentOutOfRangeException(nameof(layer));

        switch (id)
        {
            case "needle_turret":
                // A tight, bright laser dart with a light upper-octave edge.
                sound.Zap(0, .074f, .90f, 1840 * tune, 880 * weight, .30f);
                sound.Zap(.002f, .045f, .21f, 3380 * tune, 1580, .18f);
                sound.Noise(.001f, .027f, .08f, 1400, 4000, .001f, 58);
                sound.Metal(.027f, .025f, .025f * detail, 1520, 90);
                break;
            case "watchtower":
                // A long ion lance sweeps into a dense electrical tail.
                sound.Zap(0, .17f, .86f, 2480 * tune, 340 * weight, .66f);
                sound.Zap(.008f, .18f, .37f, 760 * weight, 180, .38f, .004f);
                sound.Noise(.003f, .065f, .12f, 650, 4200, .002f, 35);
                sound.Metal(.001f, .045f, .06f, 440 * weight, 45);
                break;
            case "shard_fan":
                // Overlapping colored plasma chirps form one spreading burst.
                for (var petal = 0; petal < 3 + stage; petal++)
                {
                    var at = petal * .009f;
                    sound.Zap(at, .115f, .56f / (1 + petal * .20f),
                        (1480 + petal * 270) * tune, (470 + petal * 85) * weight, .78f);
                }
                sound.Noise(.008f, .075f, .12f, 900, 3500, .005f, 28);
                break;
            case "frost_spire":
                // Glassy energy rises through the crystal, with a soft coolant edge.
                sound.Zap(0, .18f, .78f, 890 * tune, 1590, .22f, .004f);
                sound.Zap(.014f, .17f, .32f * detail, 2340 * tune, 1880, .16f);
                sound.Noise(.006f, .12f, .085f, 1800, 3500, .008f, 20);
                break;
            case "ember_coil":
                // A warm, rounded plasma pulse expands from the charged coil.
                sound.Zap(0, .21f, .88f, 760 * tune, 190 * weight, 1.05f, .005f);
                sound.Zap(.009f, .135f, .30f, 1260 * tune, 430, .76f);
                sound.Noise(.015f, .14f, .13f, 240, 1800, .012f, 18, 47);
                sound.Metal(0, .025f, .035f, 760, 73);
                break;
            case "breaker_cannon":
                // A compact energy bolt has a deep, fast electrical punch.
                sound.Zap(0, .17f, .94f, 940 * tune, 135 * weight, .72f);
                sound.Zap(.002f, .09f, .33f, 1820, 480, .54f);
                sound.Noise(.002f, .055f, .13f, 420, 3200, .002f, 35);
                sound.Metal(.046f, .050f, .055f * detail, 530 * weight, 40);
                break;
            case "siege_mortar":
                // The charge lifts out of the well; its deeper collapse belongs to impact.
                sound.Zap(0, .22f, .79f, 250 * weight, 1020 * tune, .50f, .006f);
                sound.Zap(.008f, .10f, .34f, 1420, 460, .60f);
                sound.Noise(.012f, .12f, .12f, 380, 1800, .012f, 20);
                sound.Metal(.12f, .06f, .045f * detail, 470 * weight, 47);
                break;
            case "arc_relay":
                sound.Zap(0, .11f, .88f, 1920 * tune, 470, 1.25f);
                sound.Zap(.012f, .085f, .28f, 1040, 1720, .90f);
                sound.Noise(.001f, .035f, .14f, 1200, 4600, .001f, 48);
                break;
            case "prism_beam":
                // A luminous rising focus leads into the sustained optical carrier.
                sound.Zap(0, .14f, .86f, 680 * tune, 1340, .24f, .006f);
                sound.Zap(.010f, .12f, .19f, 1960, 2440, .13f);
                sound.Noise(.008f, .070f, .05f, 1600, 3400, .012f, 20);
                break;
            case "signal_beacon":
                sound.Zap(0, .15f, .70f, 710 * tune, 1060, .32f, .009f);
                sound.Zap(.025f, .10f, .16f * detail, 1530, 1290, .20f);
                sound.Noise(.012f, .075f, .045f, 1100, 2700, .012f, 22);
                break;
        }

        if (stage > 0)
        {
            // Higher capabilities add coupled energy bands without increasing source peak.
            sound.Zap(.014f, .060f + stage * .008f, .055f * stage,
                (980 + stage * 180) * tune, 420 * weight, .32f + stage * .10f);
            if (stage == 3)
                sound.Zap(.025f, .11f, .13f, 2180 * tune, 960, .40f, .004f);
        }
        return sound.Finish(id is "signal_beacon" or "prism_beam" ? .20f : .28f);
    }

    private static void AddImpact(Sound sound, string id, int stage, float weight, float detail, float tune)
    {
        switch (id)
        {
            case "needle_turret":
                sound.Zap(0, .065f, .72f, 2240 * tune, 1120, .46f);
                sound.Noise(0, .025f, .09f, 1400, 4200, .001f, 65);
                sound.Metal(.001f, .032f, .05f, 1250, 58);
                break;
            case "watchtower":
                sound.Zap(0, .14f, .88f, 1110 * tune, 220 * weight, .78f);
                sound.Zap(.004f, .085f, .27f, 2180, 760, .48f);
                sound.Noise(.002f, .075f, .15f, 600, 3300, .002f, 32);
                sound.Metal(.001f, .075f, .08f, 470 * weight, 38);
                break;
            case "shard_fan":
                sound.Zap(0, .12f, .70f, 1680 * tune, 600, .94f);
                sound.Zap(.012f, .09f, .30f, 2360, 980, .68f);
                sound.Noise(.003f, .070f, .12f, 1100, 3600, .002f, 32, 77);
                break;
            case "frost_spire":
                sound.Zap(0, .18f, .72f, 2430 * tune, 1680, .18f, .003f);
                sound.Zap(.015f, .15f, .26f, 1760, 2140, .22f);
                sound.Noise(.003f, .10f, .08f, 1800, 3800, .005f, 20);
                break;
            case "ember_coil":
                sound.Zap(0, .21f, .78f, 580 * tune, 170 * weight, 1.10f, .004f);
                sound.Zap(.015f, .13f, .25f, 1220, 430, .82f);
                sound.Noise(.008f, .15f, .15f, 350, 2200, .006f, 18, 57);
                break;
            case "breaker_cannon":
                sound.Zap(0, .18f, .88f, 780 * tune, 125 * weight, .96f);
                sound.Zap(.002f, .11f, .27f, 1740, 510, .62f);
                sound.Noise(.002f, .085f, .19f, 450, 3400, .002f, 30);
                sound.Metal(.006f, .09f, .12f * detail, 310 * weight, 32);
                break;
            case "siege_mortar":
                sound.Zap(0, .30f, .86f, 1020 * tune, 135 * weight, 1.06f, .003f);
                sound.Zap(.018f, .24f, .37f, 460, 180, .45f, .006f);
                sound.Noise(.005f, .21f, .24f, 250, 2900, .004f, 15);
                sound.Zap(.065f, .12f, .14f * detail, 1960, 690, .74f);
                break;
            case "arc_relay":
                sound.Zap(0, .09f, .78f, 1540 * tune, 490, 1.15f);
                sound.Noise(.001f, .028f, .12f, 1400, 4300, .001f, 55);
                break;
            case "prism_beam":
                sound.Zap(0, .09f, .72f, 1760 * tune, 940, .28f);
                sound.Noise(.002f, .055f, .055f, 1300, 3300, .006f, 29);
                break;
            case "signal_beacon":
                sound.Zap(0, .13f, .64f, 1210 * tune, 790, .24f, .005f);
                sound.Noise(.002f, .075f, .045f, 1000, 2500, .012f, 25);
                break;
        }
        if (stage >= 2 && id is not ("signal_beacon" or "prism_beam"))
            sound.Zap(.014f, .065f, .045f * stage, 2140 * tune, 940, .40f);
    }

    internal static short[] CreateCue(SoundCue cue, int variant = 0)
    {
        variant = (int)((uint)variant % VariantCount);
        var sound = new Sound(CueDuration(cue), Seed(cue.ToString(), 0, variant, 17));
        var tune = 1 + (variant - 1) * .014f;
        switch (cue)
        {
            case SoundCue.Place:
            case SoundCue.ForgePlace:
                sound.Metal(0, .075f, .18f, cue == SoundCue.Place ? 470 : 330, 42);
                sound.Zap(.018f, .16f, .48f, 290 * tune, cue == SoundCue.Place ? 710 : 540, .38f, .006f);
                sound.Noise(.003f, .10f, .12f, 500, 2500, .006f, 28);
                break;
            case SoundCue.Upgrade:
            case SoundCue.ForgeUpgrade:
                sound.Zap(.005f, .21f, .48f, 360 * tune, cue == SoundCue.Upgrade ? 1080 : 810, .42f, .006f);
                sound.Zap(.13f, .14f, .24f, 930 * tune, 1490, .28f, .004f);
                sound.Metal(0, .045f, .08f, 620, 55);
                sound.Noise(.15f, .08f, .06f, 1300, 3300, .006f, 28);
                break;
            case SoundCue.Apex:
                sound.Metal(0, .09f, .12f, 320, 32);
                sound.Zap(.012f, .35f, .48f, 230 * tune, 1150, .62f, .012f);
                sound.Zap(.17f, .25f, .26f, 620 * tune, 1740, .38f, .008f);
                sound.Zap(.29f, .22f, .19f, 1530, 1070, .24f, .012f);
                sound.Noise(.25f, .17f, .08f, 1500, 3800, .015f, 16);
                break;
            case SoundCue.Sell:
                sound.Metal(0, .045f, .11f, 900, 58);
                sound.Zap(.009f, .17f, .44f, 820 * tune, 270, .35f, .004f);
                sound.Metal(.13f, .05f, .08f, 430, 55);
                break;
            case SoundCue.Protocol:
                sound.Zap(.002f, .25f, .55f, 320 * tune, 1380, .72f, .008f);
                sound.Zap(.14f, .15f, .25f, 1930, 860, .58f, .003f);
                sound.Noise(.04f, .19f, .11f, 900, 3400, .012f, 18, 39);
                sound.Metal(0, .045f, .07f, 460, 52);
                break;
            case SoundCue.KillSmall:
                sound.Zap(0, .075f, .48f, 1170 * tune, 340, .70f);
                sound.Noise(0, .055f, .15f, 1300, 4800, .001f, 46);
                sound.Metal(.015f, .05f, .10f, 1020, 58);
                break;
            case SoundCue.KillArmored:
                sound.Metal(0, .12f, .25f, 390 * tune, 33);
                sound.Zap(.002f, .14f, .46f, 840 * tune, 190, .68f);
                sound.Noise(.002f, .11f, .17f, 700, 3600, .002f, 29);
                sound.Metal(.043f, .06f, .09f, 830, 48);
                break;
            case SoundCue.KillLarge:
                sound.Zap(.001f, .28f, .58f, 610 * tune, 95, .78f, .004f);
                sound.Noise(0, .24f, .24f, 250, 2700, .003f, 17);
                sound.Metal(.006f, .18f, .20f, 340, 25);
                sound.Zap(.075f, .17f, .16f, 1160, 360, .58f, .004f);
                break;
            case SoundCue.ShieldHit:
                sound.Zap(.001f, .075f, .48f, 1510 * tune, 960, .68f);
                sound.Noise(0, .055f, .08f, 1700, 4200, .003f, 38);
                break;
            case SoundCue.ShieldBreak:
                sound.Zap(0, .18f, .50f, 1940 * tune, 270, .83f);
                sound.Zap(.035f, .12f, .20f, 2710, 640, .56f);
                sound.Noise(.002f, .14f, .17f, 1300, 4300, .004f, 25, 83);
                break;
            case SoundCue.Leak:
                sound.Zap(0, .115f, .43f, 590 * tune, 390, .36f);
                sound.Zap(.115f, .12f, .40f, 510 * tune, 320, .36f);
                sound.Metal(0, .05f, .08f, 310, 45);
                break;
            case SoundCue.WaveStart:
                sound.Zap(.005f, .22f, .44f, 390 * tune, 950, .42f, .005f);
                sound.Zap(.16f, .13f, .25f, 1180, 1600, .25f, .004f);
                sound.Metal(0, .05f, .08f, 480, 52);
                break;
            case SoundCue.WaveClear:
                sound.Zap(.005f, .26f, .39f, 470 * tune, 820, .26f, .008f);
                sound.Zap(.13f, .16f, .20f, 1220, 1460, .19f, .008f);
                sound.Noise(.025f, .18f, .07f, 1300, 3400, .025f, 17);
                break;
            case SoundCue.PlateDeploy:
                sound.Metal(0, .07f, .19f, 680, 46);
                sound.Zap(.02f, .105f, .43f, 510 * tune, 1190, .42f, .004f);
                sound.Noise(.002f, .055f, .07f, 1000, 2800, .004f, 42);
                break;
            case SoundCue.PlateTrigger:
                sound.Zap(.001f, .21f, .64f, 1360 * tune, 220, .80f, .003f);
                sound.Noise(0, .15f, .22f, 850, 3900, .002f, 26);
                sound.Zap(.035f, .14f, .20f, 2070, 590, .48f, .003f);
                break;
            case SoundCue.ChargeReady:
                sound.Zap(.004f, .16f, .41f, 570 * tune, 1150, .30f, .005f);
                sound.Zap(.10f, .11f, .23f, 1300, 1770, .22f, .004f);
                sound.Noise(.005f, .11f, .05f, 1500, 3300, .012f, 26);
                break;
            case SoundCue.BossPhase:
            case SoundCue.BossArrival:
                AddBossLoad(sound, cue == SoundCue.BossArrival);
                break;
            case SoundCue.Victory:
                sound.Metal(0, .07f, .07f, 500, 40);
                AddActuator(sound, .015f, .30f, 175, 320, .22f);
                // Sparse, damped resonances leave the soundtrack in charge of the melody.
                sound.Energy(.07f, .45f, .23f, 392, 392, 9, 1.003f, .18f);
                sound.Energy(.15f, .46f, .18f, 523.25f, 523.25f, 9, 1.002f, .14f);
                sound.Energy(.23f, .46f, .16f, 659.25f, 659.25f, 9, 1.003f, .12f);
                sound.Noise(.09f, .34f, .07f, 1500, 3600, .035f, 14);
                break;
            case SoundCue.Defeat:
                sound.Zap(.005f, .39f, .46f, 490 * tune, 100, .48f, .012f);
                sound.Metal(0, .13f, .14f, 310, 27);
                sound.Noise(.028f, .29f, .12f, 300, 1900, .013f, 15);
                sound.Zap(.25f, .14f, .19f, 340, 170, .24f, .007f);
                break;
            case SoundCue.UiNavigate:
                sound.Zap(0, .032f, .44f, 1040 * tune, 1510, .24f, .001f);
                sound.Noise(0, .021f, .04f, 2100, 4300, .001f, 97);
                break;
            case SoundCue.UiConfirm:
                sound.Zap(.001f, .072f, .43f, 580 * tune, 1060, .31f);
                sound.Zap(.033f, .042f, .13f, 1110, 1360, .17f);
                sound.Noise(.001f, .035f, .03f, 1500, 3300, .002f, 55);
                break;
            case SoundCue.UiBack:
                sound.Zap(.001f, .073f, .44f, 940 * tune, 400, .30f);
                sound.Noise(.002f, .039f, .03f, 1200, 2800, .003f, 48);
                break;
            case SoundCue.UiDelete:
                sound.Zap(0, .055f, .39f, 1130 * tune, 660, .38f);
                sound.Zap(.063f, .057f, .32f, 590, 260, .29f);
                sound.Metal(.07f, .03f, .05f, 550, 73);
                break;
            case SoundCue.UiError:
                sound.Zap(0, .057f, .36f, 470 * tune, 330, .26f);
                sound.Zap(.067f, .058f, .32f, 420 * tune, 290, .26f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cue));
        }
        var peak = cue switch
        {
            SoundCue.UiNavigate => .11f,
            SoundCue.UiConfirm or SoundCue.UiBack or SoundCue.UiDelete or SoundCue.UiError => .16f,
            SoundCue.KillSmall or SoundCue.ShieldHit => .20f,
            SoundCue.KillArmored or SoundCue.ChargeReady => .24f,
            _ => .30f
        };
        return sound.Finish(peak);
    }

    private static void AddActuator(Sound sound, float at, float duration, float from, float to, float gain)
    {
        sound.Zap(at, duration, gain, from, to, .42f, .012f);
        sound.Noise(at, duration, gain * .12f, 900, 2700, .018f, 16, 29);
    }

    private static void AddBossLoad(Sound sound, bool arrival)
    {
        // A contactor strike and accelerating, inharmonic rotor mark a boss reactor under load.
        sound.Metal(0, .15f, .29f, arrival ? 260 : 340, 28);
        sound.Energy(0, .12f, .40f, 160, 92, 29, 1.53f);
        sound.Noise(0, .075f, .15f, 600, 3600, .003f, 48);
        sound.Energy(.012f, arrival ? .56f : .49f, .52f, 82, 165, 2, 2.07f, .85f, .055f);
        sound.Noise(.012f, arrival ? .55f : .49f, .14f, 110, 1700, .055f, 3, 27);
        if (arrival)
        {
            sound.Metal(.17f, .19f, .18f, 510, 21);
            sound.Zap(.22f, .25f, .18f, 370, 840, .45f, .025f);
        }
    }

    private static float WeaponDuration(string id, WeaponSoundLayer layer) => (id, layer) switch
    {
        ("needle_turret", _) => .08f,
        ("watchtower", WeaponSoundLayer.Shot) => .20f,
        ("watchtower", _) => .16f,
        ("shard_fan", WeaponSoundLayer.Shot) => .18f,
        ("shard_fan", _) => .14f,
        ("frost_spire", _) => .22f,
        ("ember_coil", _) => .24f,
        ("breaker_cannon", _) => .20f,
        ("siege_mortar", WeaponSoundLayer.Shot) => .25f,
        ("siege_mortar", _) => .34f,
        ("arc_relay", WeaponSoundLayer.Chain) => .09f,
        ("arc_relay", _) => .14f,
        ("prism_beam", WeaponSoundLayer.Shot) => .16f,
        ("prism_beam", _) => .10f,
        _ => .18f
    };

    private static float CueDuration(SoundCue cue) => cue switch
    {
        SoundCue.Place or SoundCue.ForgePlace => .21f,
        SoundCue.Upgrade or SoundCue.ForgeUpgrade => .31f,
        SoundCue.Apex => .56f,
        SoundCue.Sell => .22f,
        SoundCue.Protocol => .35f,
        SoundCue.KillSmall => .10f,
        SoundCue.KillArmored => .18f,
        SoundCue.KillLarge => .35f,
        SoundCue.ShieldHit => .10f,
        SoundCue.ShieldBreak => .22f,
        SoundCue.Leak => .27f,
        SoundCue.WaveStart => .34f,
        SoundCue.WaveClear => .35f,
        SoundCue.PlateDeploy => .15f,
        SoundCue.PlateTrigger => .25f,
        SoundCue.ChargeReady => .25f,
        SoundCue.BossPhase => .54f,
        SoundCue.BossArrival => .62f,
        SoundCue.Victory => .74f,
        SoundCue.Defeat => .46f,
        SoundCue.UiNavigate => .04f,
        SoundCue.UiConfirm or SoundCue.UiBack => .09f,
        SoundCue.UiDelete => .14f,
        SoundCue.UiError => .15f,
        _ => throw new ArgumentOutOfRangeException(nameof(cue))
    };

    private static short[] CreateSustain(string id, int stage, int variant)
    {
        const int length = SampleRate;
        const int overlap = SampleRate / 8;
        var raw = new float[length + overlap];
        var random = new NoiseSource(Seed(id, stage, variant, 9));
        var low = 0f;
        var floor = 0f;
        var softened = 0f;
        var airCut = Coefficient(3000);
        var highCut = Coefficient(id == "prism_beam" ? 3200 : 1650);
        var lowCut = Coefficient(id == "prism_beam" ? 950 : 170);
        var prism = id == "prism_beam";
        for (var index = -overlap; index < raw.Length; index++)
        {
            var input = random.Next();
            low += highCut * (input - low);
            floor += lowCut * (low - floor);
            var time = index / (float)SampleRate;
            var flutter = .91f + .06f * MathF.Sin(Tau * (prism ? 5 : 11) * time) +
                          .03f * MathF.Sin(Tau * (prism ? 9 : 19) * time);
            var focus = 733 - stage * 23;
            var carrier = prism
                ? MathF.Sin(Tau * focus * time + .30f * MathF.Sin(Tau * focus * 2 * time)) * .52f +
                  MathF.Sin(Tau * (focus + 3 + variant) * time) * .18f +
                  MathF.Sin(Tau * (focus * 2 + 1) * time + .15f * MathF.Sin(Tau * 7 * time)) * (.07f + .015f * stage)
                : MathF.Sin(Tau * 227 * time + .70f * MathF.Sin(Tau * 454 * time)) * .48f +
                  MathF.Sin(Tau * 457 * time) * .14f;
            var signal = ((low - floor) * (prism ? .12f : .28f) + carrier) * flutter;
            softened += airCut * (signal - softened);
            if (index >= 0) raw[index] = softened;
        }
        var loop = new float[length];
        for (var index = 0; index < length; index++)
        {
            var blend = Smooth(index / (float)overlap);
            loop[index] = index < overlap ? raw[index + length] * (1 - blend) + raw[index] * blend : raw[index];
        }
        return Quantize(loop, .14f);
    }

    private static uint Seed(string key, int stage, int variant, int layer)
    {
        uint seed = 2166136261;
        foreach (var character in key) seed = unchecked((seed ^ character) * 16777619);
        return unchecked(seed ^ (uint)(stage * 997 + variant * 7919 + layer * 104729));
    }

    private static float Coefficient(float frequency) => 1 - MathF.Exp(-Tau * frequency / SampleRate);
    private static float Smooth(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    private static short[] Quantize(float[] signal, float targetPeak)
    {
        var peak = 0f;
        foreach (var value in signal) peak = MathF.Max(peak, MathF.Abs(value));
        var gain = peak > 0 ? targetPeak / peak : 0;
        var samples = new short[signal.Length];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = (short)Math.Clamp((int)MathF.Round(signal[index] * gain * short.MaxValue), short.MinValue, short.MaxValue);
        return samples;
    }

    private struct NoiseSource(uint seed)
    {
        private uint _state = seed;
        internal float Next()
        {
            _state = unchecked(_state * 1664525 + 1013904223);
            return ((_state >> 8) / 16777215f) * 2 - 1;
        }
    }

    private sealed class Sound(float seconds, uint seed)
    {
        private readonly float[] _signal = new float[Math.Max(2, (int)(seconds * SampleRate))];
        private NoiseSource _noise = new(seed);

        internal void Noise(float at, float duration, float gain, float lowHz, float highHz,
            float attack, float decay, float flutterHz = 0)
        {
            var start = (int)(at * SampleRate);
            var count = Math.Min((int)(duration * SampleRate), _signal.Length - start);
            var ceiling = 0f;
            var floor = 0f;
            var high = Coefficient(highHz);
            var low = Coefficient(lowHz);
            for (var index = 0; index < count; index++)
            {
                var time = index / (float)SampleRate;
                ceiling += high * (_noise.Next() - ceiling);
                floor += low * (ceiling - floor);
                var envelope = Smooth(time / attack) * MathF.Exp(-time * decay) *
                               Smooth((duration - time) / MathF.Min(.025f, duration * .3f));
                var flutter = flutterHz <= 0 ? 1 : .76f + .24f * MathF.Sin(Tau * flutterHz * time);
                _signal[start + index] += (ceiling - floor) * gain * envelope * flutter * 2.2f;
            }
        }

        internal void Zap(float at, float duration, float gain, float fromHz, float toHz,
            float color = .45f, float attack = .002f)
        {
            var start = (int)(at * SampleRate);
            var count = Math.Min((int)(duration * SampleRate), _signal.Length - start);
            var phase = 0f;
            var tune = 1 + _noise.Next() * .004f;
            var phaseOffset = _noise.Next() * .12f;
            for (var index = 0; index < count; index++)
            {
                var time = index / (float)SampleRate;
                var progress = time / duration;
                var sweep = (1 - MathF.Exp(-3 * progress)) / (1 - MathF.Exp(-3));
                phase += Tau * (fromHz + (toHz - fromHz) * sweep) * tune / SampleRate;
                var envelope = Smooth(time / attack) * MathF.Pow(MathF.Max(0, 1 - progress), 1.45f) *
                               Smooth((duration - time) / MathF.Min(.018f, duration * .25f));
                // Near-harmonic modulation colors the sweep; upper bands
                // settle as the pulse decays so repeated fire leaves room for the music.
                var modulation = color * (.22f + .78f * (1 - progress));
                var wave = MathF.Sin(phase + modulation * MathF.Sin(phase * 2 + phaseOffset)) * .72f +
                           MathF.Sin(phase * 2 + phaseOffset) * (.12f + color * .08f) * (1 - progress) +
                           MathF.Sin(phase * 3) * .055f * (1 - progress);
                _signal[start + index] += wave * envelope * gain;
            }
        }

        internal void Metal(float at, float duration, float gain, float frequency, float decay)
        {
            // Damped, inharmonic modes make small contacts read as housings and ceramic hardware.
            Energy(at, duration, gain, frequency, frequency * .94f, decay, 1.483f, .42f, .0008f);
            Energy(at, duration * .68f, gain * .27f, frequency * 2.173f, frequency * 2.12f, decay * 1.5f,
                1.757f, .18f, .0006f);
        }

        internal void Energy(float at, float duration, float gain, float fromHz, float toHz,
            float decay, float ratio, float modulation = 1.25f, float attack = .0015f)
        {
            var start = (int)(at * SampleRate);
            var count = Math.Min((int)(duration * SampleRate), _signal.Length - start);
            var phase = 0f;
            for (var index = 0; index < count; index++)
            {
                var time = index / (float)SampleRate;
                var sweep = 1 - MathF.Exp(-time * 4 / duration);
                phase += Tau * (fromHz + (toHz - fromHz) * sweep) / SampleRate;
                var envelope = Smooth(time / attack) * MathF.Exp(-time * decay) *
                               Smooth((duration - time) / MathF.Min(.045f, duration * .4f));
                var mod = modulation * MathF.Exp(-time * 9);
                var wave = MathF.Sin(phase + mod * MathF.Sin(phase * ratio)) * .81f +
                           MathF.Sin(phase * 2.071f + .15f) * .19f;
                _signal[start + index] += wave * envelope * gain;
            }
        }

        internal short[] Finish(float peak)
        {
            var highPass = 0f;
            var lowPass = 0f;
            var softened = 0f;
            var lowCut = Coefficient(65);
            var highCut = Coefficient(6200);
            var airCut = Coefficient(5200);
            for (var index = 0; index < _signal.Length; index++)
            {
                highPass += lowCut * (_signal[index] - highPass);
                lowPass += highCut * (_signal[index] - highPass - lowPass);
                softened += airCut * (lowPass - softened);
                var edge = Smooth(index / (SampleRate * .0007f)) *
                           Smooth((_signal.Length - 1 - index) / (SampleRate * .008f));
                _signal[index] = softened * edge;
            }
            return Quantize(_signal, peak);
        }
    }
}
