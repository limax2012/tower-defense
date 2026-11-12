using MaximalBastion.Audio;

namespace MaximalBastion.Tests;

internal static class SoundEnvelopeTests
{
    public static void Run()
    {
        LaterTransientsRetainHeadroom();
        PitchedReservationsAndLoops();
        AudibleLoadAndRecovery();
        ProtocolPitchIsStable();
        RejectedAdmissionIsTransactional();
        DensePitchedVoicesStayBounded();
    }

    private static void LaterTransientsRetainHeadroom()
    {
        var sample = DelayedStrike();
        Check.True(sample.LocalPower(.04f) < .000001f, "the fixture has a genuinely silent gap");
        Check.True(sample.RemainingPeak(.04f) > .79f,
            "a quiet gap retains reservation for the stronger future contact");
        Check.True(sample.LocalPower(.075f) > .1f,
            "audible power detects the delayed contact independently of its reservation");
        Check.True(sample.RemainingPeak(.10f) is > .049f and < .051f,
            "headroom is released after the final strong transient has passed");
        Check.True(sample.LocalPower(.13f) is > .002f and < .003f,
            "a low tail reports low audible power while it remains alive");
        Check.Equal(0f, sample.RemainingPeak(.3f), "completed samples reserve no peak headroom");
        Check.Equal(0f, sample.LocalPower(.3f), "completed samples contribute no audible power");
    }

    private static void PitchedReservationsAndLoops()
    {
        var sample = DelayedStrike();
        foreach (var (pitch, insideMargin, afterMargin) in new[]
                 { (1f, .05f, .07f), (0f, .095f, .12f), (-1f, .17f, .21f) })
        {
            var voice = Voice(sample, pitch);
            voice.Age = insideMargin;
            Check.True(voice.ReservedPeak > .39f,
                "the real-time scheduling margin retains the strong strike at every pitch");
            voice.Age = afterMargin;
            Check.True(voice.ReservedPeak is > .024f and < .026f,
                "the reservation follows source playback progress once the margin has passed");
            Check.True(voice.ReservedPeak >= MaximumRemainingAmplitude(voice),
                "pitch-adjusted reservations bound every remaining PCM sample");
        }
        var loop = Voice(sample, 1, true);
        loop.Age = 10;
        loop.Envelope = .01f;
        loop.MixGain = SoundMixer.MinimumCombatMixGain;
        Check.True(loop.ReservedPeak > .39f,
            "a loop reserves its full future peak even during a quiet or releasing segment");
    }

    private static void AudibleLoadAndRecovery()
    {
        var mixer = new SoundMixer(31);
        var chord = new SoundRequest(new SoundKey(null, Cue: SoundCue.Protocol), .75f,
            SoundCategory.Notification, Cooldown: 0);
        Check.True(mixer.TryPlay(chord), "the audible-load fixture starts its notification");
        mixer.Update(.04f);
        Check.Equal(1f, mixer.CombatMixGain, "notifications do not drive combat attenuation");
        mixer.Clear();
        Check.True(mixer.TryPlay(chord with { Category = SoundCategory.Combat }), "the same PCM can exercise combat load");
        Check.True(mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: SoundCue.UiConfirm), .3f,
            SoundCategory.Interface)), "interface feedback accompanies combat without joining its attenuation");
        mixer.Update(.04f);
        var ducked = mixer.CombatMixGain;
        Check.True(ducked >= SoundMixer.MinimumCombatMixGain && ducked < .999f,
            "audible combat causes only bounded mix attenuation");
        Check.Equal(1f, mixer.Voices.Single(voice => voice.Category == SoundCategory.Interface).MixGain,
            "combat attenuation leaves interface feedback at its requested mix gain");
        mixer.Update(1);
        Check.Equal(0, mixer.Voices.Count, "the load fixture reaches silence");
        Check.True(mixer.CombatMixGain > ducked && mixer.CombatMixGain <= 1,
            "silence smoothly restores the combat mix");
        mixer.Update(3);
        Check.True(mixer.CombatMixGain > .999f, "quiet playback recovers nominal gain");
        mixer.TryPlay(chord with { Category = SoundCategory.Combat });
        mixer.Update(.04f);
        Check.True(mixer.CombatMixGain < .999f, "a fresh audible load can attenuate again");
        mixer.Clear();
        Check.Equal(1f, mixer.CombatMixGain, "clearing the session resets mix attenuation immediately");
    }

    private static void ProtocolPitchIsStable()
    {
        foreach (var seed in new[] { 1, 77, 238, 7817 })
        {
            var mixer = new SoundMixer(seed);
            var request = new SoundRequest(new SoundKey(null, Cue: SoundCue.Protocol), .34f,
                SoundCategory.Combat, Pitch: -.25f, Cooldown: .65f);
            Check.True(mixer.TryPlay(request), "automatic Protocol feedback is admitted");
            Check.Equal(request.Pitch, mixer.Voices.Single().Pitch,
                "automatic Protocols preserve the family pitch without randomized combat tuning");
        }
    }

    private static void RejectedAdmissionIsTransactional()
    {
        var mixer = new SoundMixer(51);
        var detail = new SoundRequest(new SoundKey(null, Cue: SoundCue.UiDelete), .02f, SoundCategory.Detail);
        Check.True(mixer.TryPlay(detail), "a lower-priority detail occupies one voice slot");
        foreach (var cue in Enum.GetValues<SoundCue>().Take(SoundMixer.MaximumVoices - 1))
        {
            var key = new SoundKey(null, Cue: cue);
            var gain = .029f / mixer.GetSample(key).Peak;
            Check.True(mixer.TryPlay(new SoundRequest(key, gain, SoundCategory.Critical)),
                "protected transients fill the remaining voice slots and peak headroom");
        }
        var ids = mixer.Voices.Select(voice => voice.Id).ToArray();
        var retired = 0;
        mixer.Retired += (_, _) => retired++;
        var result = SoundAdmissionResult.Started;
        mixer.Evaluated += (_, evaluation) => result = evaluation;
        Check.True(!mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: SoundCue.UiError), .8f,
            SoundCategory.Notification)), "reclaiming one slot cannot make an over-budget notification audible");
        Check.Equal(SoundAdmissionResult.PeakBudget, result, "rejection occurs after potential slot reclamation");
        Check.True(ids.SequenceEqual(mixer.Voices.Select(voice => voice.Id)),
            "a rejected request cannot remove the lower-priority voice it considered reclaiming");
        Check.Equal(0, retired, "failed admission emits no retirement or device-stop events");
    }

    private static void DensePitchedVoicesStayBounded()
    {
        var mixer = new SoundMixer(871);
        var pitches = new[] { -1f, 0f, 1f };
        for (var frame = 0; frame < 180; frame++)
        {
            mixer.Update(1f / 60);
            for (var index = 0; index < ProceduralSoundBank.WeaponIds.Count; index++)
            {
                var key = new SoundKey(ProceduralSoundBank.WeaponIds[index],
                    frame % 2 == 0 ? WeaponSoundLayer.Shot : WeaponSoundLayer.Impact, Apex: frame % 3 == 0);
                mixer.TryPlay(new SoundRequest(key, .4f, Pitch: pitches[(frame + index) % pitches.Length], Cooldown: .04f));
            }
            if (frame % 25 == 0)
                mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: SoundCue.Leak), .55f, SoundCategory.Critical));
            Check.True(mixer.Voices.Count <= SoundMixer.MaximumVoices, "dense pitched combat respects voice capacity");
            Check.True(mixer.CombatReservedPeak <= SoundMixer.CombatPeakBudget + .0001f,
                "pitched source progress preserves the combat reservation ceiling");
            Check.True(mixer.ReservedPeak <= SoundMixer.TotalPeakBudget + .0001f,
                "pitched source progress preserves total reserved headroom");
            Check.True(mixer.CombatMixGain >= SoundMixer.MinimumCombatMixGain && mixer.CombatMixGain <= 1,
                "dense audible combat stays within the small attenuation range");
            if (frame % 12 == 0)
                foreach (var voice in mixer.Voices)
                    Check.True(voice.ReservedPeak + .00001f >= MaximumRemainingAmplitude(voice),
                        "every active voice reservation bounds its actual future PCM, including pitched tails");
        }
    }

    private static SoundSample DelayedStrike()
    {
        var samples = new short[ProceduralSoundBank.SampleRate / 5];
        for (var index = 0; index < samples.Length; index++)
        {
            var time = index / (float)ProceduralSoundBank.SampleRate;
            var amplitude = time < .01f ? .1f : time < .07f ? 0 : time < .08f ? .8f : .05f;
            samples[index] = (short)(amplitude * short.MaxValue * (index % 2 == 0 ? 1 : -1));
        }
        return new SoundSample(samples, .8f);
    }

    private static SoundVoice Voice(SoundSample sample, float pitch, bool loop = false) => new()
    {
        Id = 1, Key = new SoundKey("prism_beam"), Sample = sample, Category = SoundCategory.Combat,
        Gain = .5f, RequestedGain = .5f, Pitch = pitch, Pan = 0, IsLooped = loop, Envelope = 1
    };

    private static float MaximumRemainingAmplitude(SoundVoice voice)
    {
        var first = voice.IsLooped ? 0 : Math.Clamp(
            (int)(voice.Age * MathF.Pow(2, voice.Pitch) * ProceduralSoundBank.SampleRate), 0, voice.Sample.Samples.Length);
        var peak = 0;
        for (var index = first; index < voice.Sample.Samples.Length; index++)
            peak = Math.Max(peak, Math.Abs((int)voice.Sample.Samples[index]));
        return peak / (float)short.MaxValue * voice.Gain;
    }
}
