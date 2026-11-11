namespace MaximalBastion.Audio;

internal enum WeaponSoundLayer { Shot, Impact, Sustain, Chain }

internal enum SoundCue
{
    Place, Upgrade, Apex, Sell, Protocol, KillSmall, KillArmored, KillLarge,
    ShieldHit, ShieldBreak, Leak, WaveStart, WaveClear, PlateDeploy, PlateTrigger,
    ForgePlace, ForgeUpgrade, ChargeReady, BossPhase, BossArrival, Victory, Defeat,
    UiNavigate, UiConfirm, UiBack, UiDelete, UiError
}

/// <summary>Device-independent PCM for mechanical contacts, pressure, and energy assemblies.</summary>
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
            // Separate contacts articulate a chain without adding a musical arpeggio.
            sound.Noise(0, .034f, .72f, 700, 5300, .001f, 65);
            sound.Energy(0, .072f, .22f, 710 * tune, 290, 26, 3.7f);
            sound.Noise(.025f, .033f, .32f, 1100, 4100, .001f, 80);
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
                // Paired rails: a tight contact, short acceleration tear, and receiver return.
                sound.Metal(0, .044f, .35f, 990 * tune, 58);
                sound.Noise(.001f, .046f, .80f, 1050, 4900, .001f, 54);
                sound.Energy(.002f, .059f, .19f, 520 * weight, 200, 52, 2.71f);
                sound.Metal(.031f, .029f, .16f * detail, 1520, 90);
                break;
            case "watchtower":
                // The long coaxial rail has a heavy contactor and an ionized pressure wake.
                sound.Metal(0, .08f, .35f, 440 * weight, 38);
                sound.Noise(.002f, .09f, .85f, 550, 5500, .001f, 40);
                sound.Energy(.004f, .14f, .42f, 310 * weight, 130, 24, 2.19f);
                sound.Noise(.024f, .15f, .52f, 480, 2400, .009f, 18);
                sound.Metal(.076f, .06f, .16f * detail, 680, 44);
                break;
            case "shard_fan":
                // Focusing petals release one granular plasma fan, not one cue per shard.
                sound.Metal(0, .055f, .22f, 870 * tune, 57);
                for (var petal = 0; petal < 3 + stage; petal++)
                {
                    var at = .003f + petal * .009f;
                    sound.Noise(at, .064f, .42f / (1 + petal * .13f), 660 + petal * 100, 4100, .002f, 36);
                    sound.Energy(at, .09f, .17f, (610 + petal * 71) * weight, 230, 34, 2.43f);
                }
                sound.Noise(.025f, .12f, .30f, 360, 1700, .008f, 22);
                break;
            case "frost_spire":
                // Inharmonic crystal modes sit above a short coolant valve exhaust.
                sound.Noise(0, .17f, .56f, 1200, 4900, .008f, 15);
                sound.Metal(.007f, .16f, .29f, 1370 * tune, 23);
                sound.Metal(.026f, .12f, .15f * detail, 1930 * tune, 31);
                sound.Energy(.002f, .11f, .16f, 490, 290, 30, 1.73f);
                break;
            case "ember_coil":
                // Igniter contact, expanding thermal chamber, and nozzle pressure.
                sound.Metal(0, .038f, .22f, 760, 73);
                sound.Noise(.006f, .20f, .92f, 170, 1850, .015f, 13, 47);
                sound.Energy(.006f, .14f, .28f, 195 * weight, 106, 26, 1.53f);
                sound.Noise(.014f, .09f, .26f * detail, 1800, 4100, .006f, 32);
                break;
            case "breaker_cannon":
                // A compact armored breech kicks harder and shorter than the mortar well.
                sound.Metal(0, .085f, .42f, 370 * weight, 41);
                sound.Noise(.002f, .13f, .95f, 220, 3600, .002f, 30);
                sound.Energy(.001f, .17f, .47f, 210 * weight, 93, 25, 1.83f);
                sound.Metal(.055f, .09f, .24f * detail, 530 * weight, 40);
                break;
            case "siege_mortar":
                // Pneumatic launch from the central well; detonation belongs to impact.
                sound.Metal(0, .075f, .28f, 350, 42);
                sound.Energy(.004f, .19f, .46f, 240 * weight, 118, 19, 1.31f);
                sound.Noise(.006f, .21f, .68f, 190, 1500, .012f, 13);
                sound.Noise(.035f, .10f, .22f, 1100, 2700, .014f, 22);
                sound.Metal(.12f, .08f, .19f * detail, 470 * weight, 47);
                break;
            case "arc_relay":
                sound.Noise(0, .055f, .85f, 950, 5600, .001f, 48);
                sound.Energy(.002f, .11f, .33f, 790 * tune, 230, 23, 3.19f);
                sound.Noise(.019f, .052f, .34f, 650, 3600, .002f, 55);
                sound.Metal(.008f, .07f, .12f * detail, 1270, 40);
                break;
            case "prism_beam":
                // Aperture opening is discrete; the optical carrier is a separate bed.
                sound.Metal(0, .045f, .25f, 1260, 55);
                sound.Noise(.002f, .11f, .40f, 1600, 4400, .012f, 17);
                sound.Energy(.004f, .13f, .21f, 800, 1120, 18, 1.41f);
                break;
            case "signal_beacon":
                sound.Metal(0, .035f, .12f, 1240, 70);
                sound.Noise(.005f, .14f, .22f, 820, 2350, .018f, 15);
                sound.Energy(.007f, .14f, .17f, 700, 920, 21, 1.71f);
                sound.Noise(.045f, .06f, .08f * detail, 2200, 4000, .007f, 24);
                break;
        }

        if (stage > 0)
        {
            // Larger assemblies add secondary contact and chamber detail, rather than gain.
            sound.Metal(.017f, .06f + stage * .008f, .035f * stage, 670 * weight, 40);
            if (stage == 3)
                sound.Noise(.018f, .11f, .13f, 280, 1600, .012f, 23);
        }
        return sound.Finish(id is "signal_beacon" or "prism_beam" ? .20f : .28f);
    }

    private static void AddImpact(Sound sound, string id, int stage, float weight, float detail, float tune)
    {
        switch (id)
        {
            case "needle_turret":
                sound.Noise(0, .035f, .52f, 1400, 5000, .001f, 75);
                sound.Metal(.001f, .061f, .29f, 1250 * tune, 58);
                break;
            case "watchtower":
                sound.Noise(0, .085f, .85f, 410, 4700, .001f, 36);
                sound.Metal(.002f, .13f, .43f, 470 * weight, 29);
                sound.Energy(.002f, .11f, .23f, 230, 140, 31, 2.17f);
                break;
            case "shard_fan":
                sound.Noise(0, .085f, .65f, 800, 4200, .002f, 38, 77);
                sound.Energy(.002f, .11f, .26f, 740 * tune, 330, 30, 2.73f);
                sound.Metal(.019f, .07f, .16f, 1430, 42);
                break;
            case "frost_spire":
                sound.Metal(0, .14f, .37f, 1730 * tune, 31);
                sound.Metal(.014f, .12f, .18f, 2210, 33);
                sound.Noise(.003f, .18f, .42f, 1500, 4800, .005f, 18);
                break;
            case "ember_coil":
                sound.Noise(0, .19f, .94f, 260, 2450, .006f, 18, 57);
                sound.Energy(.002f, .11f, .23f, 190, 120, 29, 1.61f);
                sound.Noise(.028f, .09f, .27f, 1900, 3900, .005f, 28);
                break;
            case "breaker_cannon":
                sound.Metal(0, .15f, .58f, 250 * weight, 28);
                sound.Noise(.001f, .15f, .91f, 290, 4100, .001f, 26);
                sound.Energy(.002f, .16f, .41f, 150 * weight, 82, 22, 1.93f);
                sound.Metal(.041f, .10f, .19f * detail, 720, 35);
                break;
            case "siege_mortar":
                sound.Noise(0, .29f, .98f, 150, 3200, .003f, 13);
                sound.Energy(.001f, .27f, .49f, 160 * weight, 78, 16, 1.79f);
                sound.Metal(.015f, .20f, .30f, 410 * weight, 23);
                for (var fragment = 0; fragment < 3; fragment++)
                    sound.Noise(.049f + fragment * .031f, .065f, .20f * detail, 1200, 3900, .002f, 44);
                break;
            case "arc_relay":
                sound.Noise(0, .046f, .58f, 900, 4800, .001f, 55);
                sound.Energy(.001f, .083f, .27f, 650, 250, 35, 3.11f);
                break;
            case "prism_beam":
                sound.Noise(0, .075f, .36f, 1300, 3800, .006f, 29);
                sound.Energy(.001f, .08f, .16f, 1090, 730, 28, 1.59f);
                break;
            case "signal_beacon":
                sound.Noise(0, .11f, .18f, 900, 2500, .012f, 25);
                sound.Energy(.001f, .11f, .13f, 830, 640, 26, 1.73f);
                break;
        }
        if (stage >= 2 && id is not ("signal_beacon" or "prism_beam"))
            sound.Noise(.014f, .055f, .06f * stage, 600, 2100, .004f, 40);
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
                sound.Metal(0, .11f, .43f, cue == SoundCue.Place ? 470 : 330, 35);
                sound.Noise(.003f, .15f, .62f, 210, 1900, .006f, 22);
                sound.Metal(.065f, .075f, .21f, 860, 44);
                sound.Energy(.025f, .15f, .19f, 150, 280, 19, 1.81f);
                break;
            case SoundCue.Upgrade:
            case SoundCue.ForgeUpgrade:
                AddActuator(sound, .035f, .21f, 195, 390, .35f);
                sound.Metal(0, .075f, .32f, 620, 47);
                sound.Metal(.145f, .11f, .32f, cue == SoundCue.Upgrade ? 1030 : 770, 35);
                sound.Noise(.16f, .12f, .29f, 1200, 3100, .006f, 23);
                break;
            case SoundCue.Apex:
                sound.Metal(0, .14f, .42f, 320, 24);
                AddActuator(sound, .015f, .36f, 140, 350, .48f);
                sound.Metal(.095f, .13f, .20f, 620, 29);
                sound.Metal(.205f, .15f, .27f, 1080, 24);
                sound.Energy(.25f, .27f, .34f, 650, 910, 15, 1.41f);
                sound.Noise(.255f, .25f, .38f, 900, 3100, .025f, 13);
                break;
            case SoundCue.Sell:
                sound.Metal(0, .064f, .29f, 900, 49);
                AddActuator(sound, .012f, .17f, 410, 190, .32f);
                sound.Metal(.12f, .07f, .26f, 430, 45);
                break;
            case SoundCue.Protocol:
                sound.Metal(0, .09f, .29f, 460, 38);
                AddActuator(sound, .005f, .25f, 180, 460, .45f);
                sound.Noise(.045f, .24f, .55f, 650, 3300, .02f, 14, 39);
                sound.Energy(.078f, .23f, .25f, 720, 430, 17, 2.13f);
                break;
            case SoundCue.KillSmall:
                sound.Noise(0, .065f, .74f, 1100, 5100, .001f, 46);
                sound.Metal(.002f, .08f, .29f, 1020 * tune, 49);
                sound.Metal(.028f, .044f, .12f, 1670, 64);
                break;
            case SoundCue.KillArmored:
                sound.Metal(0, .14f, .49f, 390 * tune, 29);
                sound.Noise(.002f, .14f, .73f, 460, 4100, .002f, 25);
                sound.Metal(.043f, .08f, .22f, 830, 42);
                break;
            case SoundCue.KillLarge:
                sound.Noise(0, .30f, .95f, 170, 2900, .003f, 13);
                sound.Energy(.001f, .24f, .42f, 160, 85, 18, 2.07f);
                sound.Metal(.006f, .22f, .37f, 340 * tune, 20);
                sound.Metal(.068f, .14f, .23f, 690, 27);
                sound.Noise(.13f, .14f, .22f, 1200, 3900, .004f, 22);
                break;
            case SoundCue.ShieldHit:
                sound.Noise(0, .075f, .35f, 1300, 4500, .003f, 32);
                sound.Energy(.001f, .075f, .28f, 1270 * tune, 790, 37, 2.57f);
                break;
            case SoundCue.ShieldBreak:
                sound.Energy(0, .17f, .38f, 1210, 310, 21, 2.37f);
                sound.Noise(.002f, .18f, .73f, 950, 4300, .004f, 21, 83);
                sound.Noise(.047f, .09f, .24f, 410, 2100, .004f, 24);
                break;
            case SoundCue.Leak:
                sound.Metal(0, .12f, .38f, 310, 28);
                sound.Noise(.001f, .11f, .38f, 480, 2200, .003f, 29);
                sound.Metal(.11f, .13f, .33f, 275, 28);
                sound.Energy(.105f, .13f, .19f, 460, 290, 26, 1.83f);
                break;
            case SoundCue.WaveStart:
                sound.Metal(0, .09f, .31f, 480, 42);
                AddActuator(sound, .012f, .23f, 210, 470, .40f);
                sound.Metal(.16f, .13f, .25f, 1070, 31);
                sound.Noise(.165f, .13f, .31f, 1100, 3300, .008f, 23);
                break;
            case SoundCue.WaveClear:
                sound.Metal(0, .07f, .26f, 740, 43);
                sound.Noise(.025f, .26f, .53f, 580, 2100, .025f, 12);
                sound.Energy(.03f, .26f, .25f, 370, 620, 13, 1.41f);
                sound.Metal(.145f, .13f, .20f, 1230, 31);
                break;
            case SoundCue.PlateDeploy:
                sound.Metal(0, .09f, .42f, 680, 39);
                sound.Noise(.002f, .085f, .38f, 550, 2700, .004f, 35);
                sound.Metal(.046f, .065f, .19f, 1130, 52);
                break;
            case SoundCue.PlateTrigger:
                sound.Noise(0, .20f, .86f, 280, 3900, .002f, 21);
                sound.Energy(.001f, .19f, .38f, 520, 160, 22, 2.31f);
                sound.Metal(.006f, .14f, .26f, 460, 31);
                break;
            case SoundCue.ChargeReady:
                sound.Metal(0, .06f, .24f, 880, 49);
                sound.Energy(.004f, .18f, .24f, 450, 820, 18, 1.51f);
                sound.Noise(.005f, .17f, .25f, 1000, 2800, .012f, 19);
                sound.Metal(.10f, .10f, .25f, 1400, 32);
                break;
            case SoundCue.BossPhase:
            case SoundCue.BossArrival:
                AddBossLoad(sound, cue == SoundCue.BossArrival);
                break;
            case SoundCue.Victory:
                sound.Metal(0, .11f, .20f, 500, 30);
                AddActuator(sound, .015f, .30f, 175, 320, .22f);
                // Sparse, damped resonances leave the soundtrack in charge of the melody.
                sound.Energy(.07f, .45f, .23f, 392, 392, 9, 1.003f, .18f);
                sound.Energy(.15f, .46f, .18f, 523.25f, 523.25f, 9, 1.002f, .14f);
                sound.Energy(.23f, .46f, .16f, 659.25f, 659.25f, 9, 1.003f, .12f);
                sound.Noise(.09f, .43f, .24f, 1100, 3300, .035f, 11);
                break;
            case SoundCue.Defeat:
                sound.Metal(0, .19f, .32f, 310, 20);
                AddActuator(sound, .012f, .40f, 270, 82, .43f);
                sound.Noise(.028f, .37f, .37f, 230, 1500, .013f, 10);
                sound.Metal(.27f, .13f, .18f, 270, 28);
                break;
            case SoundCue.UiNavigate:
                sound.Metal(0, .031f, .34f, 1470 * tune, 105);
                sound.Noise(0, .026f, .37f, 1700, 4200, .001f, 97);
                break;
            case SoundCue.UiConfirm:
                sound.Metal(0, .047f, .32f, 1020, 62);
                sound.Noise(.001f, .05f, .33f, 960, 2800, .002f, 48);
                sound.Energy(.012f, .065f, .15f, 630, 890, 34, 1.67f);
                break;
            case SoundCue.UiBack:
                sound.Metal(0, .043f, .27f, 820, 65);
                sound.Noise(.003f, .063f, .36f, 650, 2300, .004f, 38);
                sound.Energy(.006f, .07f, .16f, 580, 310, 38, 1.67f);
                break;
            case SoundCue.UiDelete:
                sound.Metal(0, .043f, .30f, 1240, 70);
                AddActuator(sound, .007f, .10f, 490, 230, .26f);
                sound.Metal(.073f, .046f, .24f, 550, 65);
                break;
            case SoundCue.UiError:
                sound.Metal(0, .06f, .29f, 370, 45);
                sound.Noise(.001f, .07f, .31f, 430, 1600, .003f, 39);
                sound.Metal(.065f, .062f, .23f, 340, 45);
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
        sound.Energy(at, duration, gain, from, to, 7, 2.07f, .65f, .018f);
        sound.Noise(at, duration, gain * .70f, 360, 1900, .018f, 10, 29);
    }

    private static void AddBossLoad(Sound sound, bool arrival)
    {
        // Contactor strike and accelerating, inharmonic rotor preserve the boss load signature.
        sound.Metal(0, .15f, .29f, arrival ? 260 : 340, 28);
        sound.Energy(0, .12f, .40f, 160, 92, 29, 1.53f);
        sound.Noise(0, .075f, .38f, 600, 3600, .003f, 48);
        sound.Energy(.012f, arrival ? .56f : .49f, .52f, 82, 165, 2, 2.07f, .85f, .055f);
        sound.Noise(.012f, arrival ? .55f : .49f, .44f, 110, 1700, .055f, 3, 27);
        if (arrival)
        {
            sound.Metal(.17f, .19f, .18f, 510, 21);
            sound.Noise(.22f, .25f, .22f, 450, 2400, .025f, 11);
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
            var flutter = .81f + .11f * MathF.Sin(Tau * (prism ? 17 : 31) * time) +
                          .08f * MathF.Sin(Tau * (prism ? 29 : 47) * time);
            var carrier = prism
                ? MathF.Sin(Tau * (1103 - stage * 27) * time + .8f * MathF.Sin(Tau * 397 * time)) * .11f +
                  MathF.Sin(Tau * 1727 * time + MathF.Sin(Tau * 109 * time)) * .035f * stage
                : MathF.Sin(Tau * 139 * time + .8f * MathF.Sin(Tau * 71 * time)) * .075f;
            var signal = ((low - floor) * (prism ? 1.35f : 1.8f) + carrier) * flutter;
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
