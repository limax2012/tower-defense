using MaximalBastion.Audio;

namespace MaximalBastion.Tests;

internal static class SoundscapeTests
{
    public static void Run()
    {
        FinishedResourceNames();
        var mixer = new SoundMixer(41);
        foreach (var id in ProceduralSoundBank.WeaponIds)
        foreach (var layer in new[] { WeaponSoundLayer.Shot, WeaponSoundLayer.Impact })
        {
            short[]? previous = null;
            for (var tier = 0; tier < 4; tier++)
            for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
            {
                var key = new SoundKey(id, layer, LevelIndex: Math.Min(2, tier), Apex: tier == 3);
                var sample = mixer.GetSample(key, variant);
                CheckSample(sample, $"{id}/{layer}/{tier}/{variant}", false);
                if (previous is not null)
                    Check.True(!previous.SequenceEqual(sample.Samples), "Weapon variants and capabilities retain distinct waveforms");
                previous = sample.Samples;
            }
        }
        for (var tier = 0; tier < 4; tier++)
        for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
        {
            var key = new SoundKey("arc_relay", WeaponSoundLayer.Chain, LevelIndex: Math.Min(2, tier), Apex: tier == 3);
            CheckSample(mixer.GetSample(key, variant), $"arc_relay/Chain/{tier}/{variant}", false);
        }
        foreach (var cue in Enum.GetValues<SoundCue>())
        for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
            CheckSample(mixer.GetSample(new SoundKey(null, Cue: cue), variant), cue.ToString(), false);
        foreach (var id in new[] { "prism_beam", "ember_coil" })
            CheckSample(mixer.GetSample(new SoundKey(id, WeaponSoundLayer.Sustain)), id + " loop", true);

        AdmissionAndRelease();
    }

    private static void FinishedResourceNames()
    {
        const string prefix = "MaximalBastion.Audio.Finished.";
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ProceduralSoundBank.WeaponIds)
        {
            var layers = id switch
            {
                "prism_beam" or "signal_beacon" => new[] { WeaponSoundLayer.Shot },
                "arc_relay" => new[] { WeaponSoundLayer.Shot, WeaponSoundLayer.Impact, WeaponSoundLayer.Chain },
                _ => new[] { WeaponSoundLayer.Shot, WeaponSoundLayer.Impact }
            };
            foreach (var layer in layers)
            for (var stage = 0; stage < 4; stage++)
            for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
                expected.Add($"{prefix}{id}-{layer.ToString().ToLowerInvariant()}-{stage}-{variant}.wav");
        }
        foreach (var cue in Enum.GetValues<SoundCue>().Where(cue => cue is not (SoundCue.Protocol or SoundCue.UiError)))
        for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
            expected.Add($"{prefix}cue-{cue.ToString().ToLowerInvariant()}-{variant}.wav");

        var resources = typeof(ProceduralSoundBank).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Check.Equal(303, expected.Count, "the live soundscape defines 228 weapon and 75 event samples");
        Check.Equal(expected.Count, resources.Length, "the desktop assembly embeds every finished sample exactly once");
        Check.True(expected.SetEquals(resources), "finished resource names exactly match every live family, layer, stage, variant, and event");
    }

    private static void CheckSample(SoundSample sample, string name, bool loop)
    {
        Check.True(sample.Duration is > 0.025f and <= 1.2f, name + " has a bounded useful duration");
        Check.True(sample.Peak is > 0.01f and <= 0.31f, name + " retains headroom");
        var dc = sample.Samples.Average(value => (double)value / short.MaxValue);
        Check.True(Math.Abs(dc) < 0.006, name + " has negligible DC offset");
        if (loop)
        {
            Check.True(Math.Abs(sample.Samples[0] - sample.Samples[^1]) < 600,
                name + " wraps without a discontinuity");
        }
        else
        {
            Check.Equal((short)0, sample.Samples[0], name + " starts at zero");
            Check.Equal((short)0, sample.Samples[^1], name + " ends at zero");
        }
    }

    private static void AdmissionAndRelease()
    {
        var mixer = new SoundMixer(42);
        var launch = new SoundRequest(new SoundKey("needle_turret"), 0.45f, Cooldown: 0.09f);
        Check.True(mixer.TryPlay(launch), "First firing sound is admitted");
        Check.True(!mixer.TryPlay(launch), "Same-frame burst does not stack voices");
        mixer.Update(0.1f);
        Check.True(mixer.TryPlay(launch), "Real elapsed time releases family cooldown");
        Check.True(!mixer.TryPlay(launch with { Gain = float.NaN }), "Invalid gains cannot reach playback");
        mixer.Clear();

        var beam = new SoundRequest(new SoundKey("prism_beam", WeaponSoundLayer.Sustain), 0.3f,
            HoldSeconds: 0.15f);
        Check.True(mixer.TryPlay(beam), "Beam starts a sustained voice");
        var id = mixer.Voices.Single().Id;
        for (var tick = 0; tick < 120; tick++)
        {
            mixer.Update(1f / 60);
            mixer.TryPlay(beam);
            Check.Equal(1, mixer.Voices.Count, "Beam refreshes cannot layer duplicate loops");
            Check.Equal(id, mixer.Voices.Single().Id, "Beam refresh retains phase continuity");
        }
        mixer.StopCombat();
        mixer.Update(0.04f);
        Check.True(mixer.Voices.Count == 1 && mixer.Voices[0].Envelope < 1,
            "Pausing releases a sustained voice gradually");
        mixer.Update(0.1f);
        Check.Equal(0, mixer.Voices.Count, "Paused sustained audio reaches silence");

        for (var frame = 0; frame < 90; frame++)
        {
            mixer.Update(1f / 60);
            foreach (var family in ProceduralSoundBank.WeaponIds)
            {
                mixer.TryPlay(new SoundRequest(new SoundKey(family, WeaponSoundLayer.Shot, Apex: true), 0.8f, Cooldown: 0));
                mixer.TryPlay(new SoundRequest(new SoundKey(family, WeaponSoundLayer.Impact, Apex: true), 0.8f, Cooldown: 0));
            }
            Check.True(mixer.Voices.Count <= SoundMixer.MaximumVoices, "Dense combat respects the total voice budget");
            Check.True(mixer.Voices.Count(voice => voice.Category is SoundCategory.Combat or SoundCategory.Detail) <=
                SoundMixer.MaximumCombatVoices, "Dense combat respects its reserved voice budget");
            Check.True(mixer.ReservedPeak <= SoundMixer.TotalPeakBudget + 0.0001f,
                "Worst-case aligned transients remain below the mix peak budget");
        }
        Check.True(mixer.TryPlay(new SoundRequest(new SoundKey(null, Cue: SoundCue.Leak), 0.75f, SoundCategory.Critical)),
            "A breach warning remains audible during saturated combat");
        mixer.Clear();
        Check.Equal(0, mixer.Voices.Count, "Session detach can release every voice");
        var confirmation = new SoundRequest(new SoundKey(null, Cue: SoundCue.UiConfirm), 0.3f,
            SoundCategory.Interface, Cooldown: 0.2f);
        Check.True(mixer.TryPlay(confirmation) && mixer.TryPlay(launch), "Transition fixture contains interface and combat voices");
        mixer.Clear(preserveInterface: true);
        Check.Equal(1, mixer.Voices.Count, "Changing sessions preserves the initiating interface sound");
        Check.Equal(SoundCategory.Interface, mixer.Voices.Single().Category, "Session transition releases old combat voices");
        Check.True(!mixer.TryPlay(confirmation), "Preserved interface voices retain their cooldown");
        Check.True(mixer.TryPlay(launch), "New-session combat does not inherit old cooldowns");
        mixer.Clear();
        Check.Equal(0, mixer.Voices.Count, "Mute and disposal release preserved interface voices too");
    }
}
