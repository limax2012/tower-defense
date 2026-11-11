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
        if (layer is not (WeaponSoundLayer.Shot or WeaponSoundLayer.Impact or WeaponSoundLayer.Chain))
            throw new ArgumentOutOfRangeException(nameof(layer));
        if (id is "shard_fan" or "frost_spire" or "ember_coil")
            return RenderedSoundBank.Load(id, layer == WeaponSoundLayer.Chain ? WeaponSoundLayer.Impact : layer, stage, variant);

        var tune = 1 + (variant - 1) * .006f;
        if (layer == WeaponSoundLayer.Shot && id == "needle_turret")
        {
            // A tiny rising dart stays readable through cadence; added coils color only the upper tiers.
            var samples = ProceduralSignalReferences.Tone(1080 * tune, 1420 * tune, .026f + stage * .002f, sourceGain: .28f);
            if (stage > 0)
            {
                var edge = ProceduralSignalReferences.Tone(2160 * tune, 2840 * tune,
                    samples.Length / (float)SampleRate + .000001f, sourceGain: .012f * stage);
                for (var index = 0; index < samples.Length; index++)
                    samples[index] = (short)(samples[index] * (1 - stage * .04f) + edge[index]);
            }
            return samples;
        }
        if (id == "signal_beacon")
            return ProceduralSignalReferences.Chord(740 * tune, (1110 + stage * 2) * tune,
                .065f + stage * .004f, sourceGain: layer == WeaponSoundLayer.Shot ? .20f : .14f);

        var sound = new Sound(WeaponDuration(id, layer) + stage * .006f, Seed(id, stage, variant, (int)layer));
        var weight = 1 - stage * .025f;
        var detail = .70f + stage * .10f;
        if (layer == WeaponSoundLayer.Chain && id == "arc_relay")
        {
            // Irregularly spaced contacts read as current finding successive targets.
            for (var jump = 0; jump < 3; jump++)
            {
                var at = jump switch { 0 => 0, 1 => .021f, _ => .057f };
                var gain = .62f / (1 + jump * .6f);
                sound.Energy(at, .038f, gain, (950 - jump * 135) * tune, 540 - jump * 70,
                    52, 3.17f + jump * .13f, 2.1f + stage * .15f);
                sound.Noise(at, .018f, .14f / (jump + 1), 1400, 4100, .0008f, 87);
            }
            return sound.Finish(.18f);
        }
        if (layer is WeaponSoundLayer.Impact or WeaponSoundLayer.Chain)
        {
            AddImpact(sound, id, stage, weight, detail, tune);
            return sound.Finish(id == "needle_turret" ? .14f : .22f);
        }

        switch (id)
        {
            case "watchtower":
                // One concentrated rail release: low-mid impulse, ion edge, then silence.
                sound.Energy(0, .085f, .90f, 310 * tune, 165 * weight, 28, 2.01f, .38f);
                sound.Energy(.002f, .078f, .32f + stage * .035f, 1130 * tune, 970, 41, 2.53f, .75f);
                sound.Noise(.001f, .050f, .12f, 1800, 4600, .0008f, 48);
                sound.Energy(.027f, .065f, .10f * detail, 470, 450, 34, 2.02f, .12f);
                break;
            case "breaker_cannon":
                // A braced bolt compresses its weight into a short, thick impulse.
                sound.Energy(0, .095f, .93f, 215 * tune, 125 * weight, 34, 2.0f, 1.35f + stage * .12f);
                sound.Energy(.001f, .040f, .32f, 830, 610, 65, 2.91f, .65f);
                sound.Noise(.002f, .038f, .15f, 500, 2400, .001f, 64);
                sound.Metal(.027f, .043f, .055f * detail, 440, 55);
                break;
            case "siege_mortar":
                // The rounded charge lifts from the well; the pressure burst belongs to arrival.
                sound.Zap(0, .135f, .73f, 265 * weight, 440 * tune, .30f, .007f);
                sound.Energy(.005f, .11f, .28f + stage * .035f, 530, 570, 20, 2.02f, .23f, .008f);
                sound.Noise(.015f, .09f, .11f, 370, 1300, .009f, 25);
                break;
            case "arc_relay":
                sound.Energy(0, .066f, .84f, 820 * tune, 285, 41, 3.17f, 2.1f + stage * .12f);
                sound.Noise(.001f, .028f, .17f, 1300, 4300, .0007f, 72);
                sound.Energy(.016f, .027f, .21f * detail, 1390, 1050, 64, 2.73f, 1.9f);
                break;
            case "prism_beam":
                // The brief focus gesture resolves into the continuous carrier.
                sound.Energy(0, .062f, .77f, 1240 * tune, 733 - stage * 23, 32, 2.0f, .12f, .003f);
                sound.Energy(.012f, .055f, .10f + stage * .02f, 1466, 1466, 26, 2.0f, .08f, .007f);
                break;
        }
        return sound.Finish(id == "prism_beam" ? .20f : .28f);
    }

    private static void AddImpact(Sound sound, string id, int stage, float weight, float detail, float tune)
    {
        switch (id)
        {
            case "needle_turret":
                sound.Energy(0, .030f, .70f, 1460 * tune, 1190, 95, 2.03f, .17f + stage * .025f);
                sound.Noise(.001f, .017f, .08f, 1600, 3500, .001f, 92);
                break;
            case "watchtower":
                // A narrow puncture rather than a second cannon discharge.
                sound.Energy(0, .060f, .68f, 560 * tune, 390 * weight, 49, 2.43f, .80f);
                sound.Noise(.001f, .044f, .18f, 1100, 3900, .001f, 57);
                sound.Metal(.008f, .057f, .10f * detail, 740 * weight, 46);
                break;
            case "breaker_cannon":
                // Armor fracture has the hardest edge of the direct-fire impacts.
                sound.Energy(0, .105f, .76f, 185 * tune, 115 * weight, 28, 2.07f, .92f);
                sound.Noise(.001f, .066f, .31f, 700, 3300, .0008f, 41);
                sound.Metal(.003f, .080f, .21f * detail, 580 * weight, 39);
                sound.Metal(.023f, .045f, .07f, 1170, 63);
                break;
            case "siege_mortar":
                // A wider pressure bloom, broken into short diffuse resonances.
                sound.Energy(0, .17f, .75f, 150 * tune, 92 * weight, 23, 1.61f, 1.25f);
                sound.Noise(.002f, .18f, .36f, 160, 2300, .003f, 19);
                sound.Energy(.026f, .13f, .21f * detail, 410, 360, 29, 2.63f, 1.8f);
                sound.Noise(.045f, .12f, .13f, 1300, 3400, .008f, 30);
                break;
            case "arc_relay":
                sound.Energy(0, .040f, .68f, 970 * tune, 610, 62, 3.31f, 1.9f + stage * .1f);
                sound.Noise(.001f, .021f, .13f, 1600, 3900, .0008f, 72);
                break;
            case "prism_beam":
                sound.Energy(0, .048f, .60f, 1040 * tune, 960, 48, 2.0f, .15f + stage * .02f);
                break;
        }
    }

    internal static short[] CreateCue(SoundCue cue, int variant = 0)
    {
        // Signals use fixed intervals so repeated actions retain a stable meaning.
        var signal = cue switch
        {
            SoundCue.Upgrade => ProceduralSignalReferences.Tone(480, 760, .14f),
            SoundCue.Sell => ProceduralSignalReferences.Tone(360, 210, .12f, triangle: true),
            SoundCue.Protocol => ProceduralSignalReferences.Chord(390, 585, .20f),
            SoundCue.Leak => ProceduralSignalReferences.TwoNote(392, 392, .22f, .26f),
            SoundCue.WaveStart => ProceduralSignalReferences.Tone(260, 540, .24f, triangle: true),
            SoundCue.WaveClear => ProceduralSignalReferences.Chord(520, 780, .28f),
            SoundCue.ChargeReady => ProceduralSignalReferences.TwoNote(880, 1175, .16f, .21f),
            SoundCue.Victory => ProceduralSignalReferences.Triad(392, 523, 659, .48f),
            SoundCue.Defeat => ProceduralSignalReferences.TwoNote(392, 330, .30f),
            SoundCue.UiNavigate => ProceduralSignalReferences.Tone(760, 820, .035f, sourceGain: .11f),
            SoundCue.UiConfirm => ProceduralSignalReferences.Tone(410, 620, .075f),
            SoundCue.UiBack => ProceduralSignalReferences.Tone(430, 300, .075f, triangle: true),
            SoundCue.UiDelete => ProceduralSignalReferences.TwoNote(420, 300, .11f, .16f),
            SoundCue.UiError => ProceduralSignalReferences.TwoNote(310, 310, .13f, .16f),
            _ => null
        };
        if (signal is not null) return signal;

        variant = (int)((uint)variant % VariantCount);
        var sound = new Sound(CueDuration(cue), Seed(cue.ToString(), 0, variant, 17));
        var tune = 1 + (variant - 1) * .014f;
        switch (cue)
        {
            case SoundCue.Place:
                sound.Metal(0, .065f, .15f, 470 * tune, 48);
                sound.Energy(.015f, .14f, .40f, 470, 470, 9, 1.5f, .12f, .008f);
                break;
            case SoundCue.ForgePlace:
                sound.Metal(0, .08f, .20f, 330 * tune, 38);
                AddActuator(sound, .015f, .15f, 190, 245, .12f);
                sound.Energy(.055f, .13f, .31f, 490, 490, 8, 2, .12f, .012f);
                break;
            case SoundCue.ForgeUpgrade:
                sound.Metal(0, .055f, .10f, 620, 55);
                AddActuator(sound, .01f, .12f, 230, 285, .10f);
                sound.Energy(.05f, .10f, .28f, 620, 620, 9, 2, .10f, .006f);
                sound.Energy(.13f, .13f, .30f, 930, 930, 8, 2, .08f, .006f);
                break;
            case SoundCue.Apex:
                // A settled field opens into higher harmonics as the promoted emitter comes online.
                sound.Energy(0, .44f, .42f, 390, 390, 2.5f, 2, .16f, .025f);
                sound.Energy(.04f, .44f, .28f, 585, 585, 2.8f, 2, .12f, .045f);
                sound.Energy(.10f, .38f, .12f, 780, 780, 4, 2, .08f, .040f);
                break;
            case SoundCue.KillSmall:
                sound.Noise(0, .038f, .28f, 1300, 4300, .001f, 62);
                sound.Metal(.003f, .065f, .16f, 1020 * tune, 57);
                sound.Energy(.009f, .065f, .15f, 670, 630, 43, 2.7f, 2.2f);
                break;
            case SoundCue.KillArmored:
                sound.Metal(0, .14f, .36f, 390 * tune, 29);
                sound.Noise(.002f, .10f, .16f, 650, 3200, .002f, 34);
                sound.Energy(.012f, .12f, .13f, 420, 390, 24, 2.4f, 2.1f);
                sound.Metal(.045f, .065f, .08f, 830, 51);
                break;
            case SoundCue.KillLarge:
                sound.Noise(0, .24f, .40f, 220, 2600, .003f, 18);
                sound.Energy(0, .20f, .26f, 138, 110, 21, 2.2f, .35f, .003f);
                sound.Metal(.008f, .19f, .24f, 340 * tune, 24);
                sound.Energy(.065f, .16f, .12f, 540, 490, 22, 3.1f, 2.5f);
                break;
            case SoundCue.ShieldHit:
                sound.Energy(0, .085f, .46f, 970 * tune, 940 * tune, 28, 2.4f, 1.6f, .001f);
                sound.Noise(0, .025f, .04f, 1700, 4000, .001f, 60);
                break;
            case SoundCue.ShieldBreak:
                sound.Energy(0, .16f, .35f, 1220 * tune, 1140, 20, 2.41f, 2.7f);
                sound.Energy(.027f, .11f, .15f, 1650, 1520, 26, 3.07f, 2.5f);
                sound.Noise(.002f, .14f, .20f, 1400, 4300, .003f, 25, 83);
                break;
            case SoundCue.PlateDeploy:
                sound.Metal(0, .055f, .18f, 680 * tune, 48);
                sound.Energy(.027f, .09f, .32f, 920, 920, 16, 2, .08f, .005f);
                break;
            case SoundCue.PlateTrigger:
                sound.Noise(0, .17f, .53f, 240, 3900, .0015f, 23);
                sound.Energy(.002f, .16f, .30f, 165, 145, 25, 2, .18f, .002f);
                sound.Noise(.028f, .14f, .11f, 1300, 3400, .008f, 23);
                break;
            case SoundCue.BossPhase:
            case SoundCue.BossArrival:
                AddBossLoad(sound, cue == SoundCue.BossArrival);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cue));
        }
        var peak = cue switch
        {
            SoundCue.KillSmall or SoundCue.ShieldHit => .20f,
            SoundCue.KillArmored or SoundCue.PlateDeploy => .24f,
            _ => .30f
        };
        return sound.Finish(peak);
    }

    private static void AddActuator(Sound sound, float at, float duration, float from, float to, float gain)
    {
        sound.Energy(at, duration, gain, from, to, 12, 2.1f, .3f, .008f);
        sound.Noise(at, duration, gain * .07f, 700, 2100, .012f, 18, 29);
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
            sound.Energy(.22f, .25f, .18f, 370, 410, 8, 2.07f, .6f, .025f);
        }
    }

    private static float WeaponDuration(string id, WeaponSoundLayer layer) => (id, layer) switch
    {
        ("needle_turret", _) => .035f,
        ("watchtower", WeaponSoundLayer.Shot) => .105f,
        ("watchtower", _) => .080f,
        ("breaker_cannon", WeaponSoundLayer.Shot) => .110f,
        ("breaker_cannon", _) => .125f,
        ("siege_mortar", WeaponSoundLayer.Shot) => .160f,
        ("siege_mortar", _) => .230f,
        ("arc_relay", WeaponSoundLayer.Chain) => .105f,
        ("arc_relay", WeaponSoundLayer.Shot) => .080f,
        ("arc_relay", _) => .055f,
        ("prism_beam", WeaponSoundLayer.Shot) => .080f,
        _ => .065f
    };

    private static float CueDuration(SoundCue cue) => cue switch
    {
        SoundCue.Place => .17f,
        SoundCue.ForgePlace => .20f,
        SoundCue.ForgeUpgrade => .28f,
        SoundCue.Apex => .50f,
        SoundCue.KillSmall => .10f,
        SoundCue.KillArmored => .18f,
        SoundCue.KillLarge => .27f,
        SoundCue.ShieldHit => .10f,
        SoundCue.ShieldBreak => .18f,
        SoundCue.PlateDeploy => .15f,
        SoundCue.PlateTrigger => .20f,
        SoundCue.BossPhase => .54f,
        SoundCue.BossArrival => .62f,
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
