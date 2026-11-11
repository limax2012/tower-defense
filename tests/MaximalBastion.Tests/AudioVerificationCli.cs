using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MaximalBastion.Audio;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class AudioVerificationCli
{
    private const int Rate = ProceduralSoundBank.SampleRate;
    private const int FramesPerSecond = 60;
    private const int SamplesPerFrame = Rate / FramesPerSecond;
    private const float Seconds = 18;

    public static int ExportCores(string[] args)
    {
        try
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("Usage: --export-audio-cores <output-directory>");
            var output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(output);
            var weaponCount = 0;
            foreach (var weapon in ProceduralSoundBank.WeaponIds)
            foreach (var layer in Enum.GetValues<WeaponSoundLayer>())
            {
                if (layer == WeaponSoundLayer.Sustain && weapon is not ("prism_beam" or "ember_coil")) continue;
                for (var stage = 0; stage < 4; stage++)
                for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
                {
                    var name = $"{weapon}-{layer.ToString().ToLowerInvariant()}-{stage}-{variant}.wav";
                    var samples = ProceduralSoundBank.CreateWeaponCore(weapon, layer, Math.Min(stage, 2), stage == 3, variant);
                    WriteWave(Path.Combine(output, name), samples);
                    weaponCount++;
                }
            }
            var cueCount = 0;
            foreach (var cue in Enum.GetValues<SoundCue>())
            for (var variant = 0; variant < ProceduralSoundBank.VariantCount; variant++)
            {
                var name = $"cue-{cue.ToString().ToLowerInvariant()}-{variant}.wav";
                WriteWave(Path.Combine(output, name), ProceduralSoundBank.CreateCueCore(cue, variant));
                cueCount++;
            }
            Console.WriteLine($"Exported {weaponCount} weapon cores and {cueCount} event cores to {output}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Audio core export failed: " + error);
            return 1;
        }
    }

    public static int Run(string[] args)
    {
        try
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("Usage: --verify-audio <output-directory> [--music-root <Content/Audio>] [--ffmpeg <executable>]");
            var output = Path.GetFullPath(args[1]);
            var musicRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "MaximalBastion", "Content", "Audio");
            var ffmpeg = "ffmpeg";
            for (var index = 2; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length) throw new ArgumentException("Missing audio verification option value.");
                switch (args[index])
                {
                    case "--music-root": musicRoot = Path.GetFullPath(args[index + 1]); break;
                    case "--ffmpeg": ffmpeg = args[index + 1]; break;
                    default: throw new ArgumentException("Unknown audio verification option: " + args[index]);
                }
            }
            Directory.CreateDirectory(output);
            SoundscapeTests.Run();
            ProtocolReferenceTests.Run();
            AudioMixTests.Run();
            CombatAudioEventTests.Run();
            UiAudioFeedbackTests.Run();
            Console.WriteLine("Audio synthesis, admission, event timing, lifecycle, and interface regressions passed.");
            var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
            var palette = ExportPalette(output);
            var scenarios = new[]
            {
                new Scenario("quiet-opening", 1, 1, 0, 1, "QuietInvestigation.ogg"),
                new Scenario("mixed-towers", 1, 10, 1, 12, "MildlyUpbeatArpeggios.ogg"),
                new Scenario("dense-late-1x", 1, 24, 3, 30, "FocusedDanceParty.ogg"),
                new Scenario("dense-late-2x", 2, 24, 3, 30, "FocusedDanceParty.ogg"),
                new Scenario("boss-1x", 1, 10, 3, 30, "BassyRollIn.ogg", true),
                new Scenario("boss-2x", 2, 10, 3, 30, "IcyInvestigation.ogg", true)
            };
            var reports = new List<ScenarioReport>();
            foreach (var scenario in scenarios)
            {
                var report = RenderScenario(content, scenario, output, musicRoot, ffmpeg);
                reports.Add(report);
                Console.WriteLine($"{report.Name}: {report.Starts} voices started; maximum {report.MaximumVoices} simultaneous; " +
                    $"SFX {report.SfxRmsDb:0.0} dBFS RMS; mixed peak {report.MixedPeakDb:0.0} dBFS.");
            }
            File.WriteAllText(Path.Combine(output, "audio-report.json"), JsonSerializer.Serialize(new
            {
                sampleRate = Rate,
                secondsPerScenario = Seconds,
                sfxVolume = 0.65,
                musicVolume = 0.20,
                rendering = "Real Sandbox simulation events through AudioDirector and SoundMixer; offline PCM rendering with shipped music. Device output and listening require a runtime audition.",
                palette,
                scenarios = reports
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Audio verification artifacts: " + output);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Audio verification failed: " + error);
            return 1;
        }
    }

    private static List<PaletteEntry> ExportPalette(string output)
    {
        var folder = Path.Combine(output, "palette");
        Directory.CreateDirectory(folder);
        var mixer = new SoundMixer();
        var entries = new List<PaletteEntry>();
        var reel = new List<float>();
        foreach (var weapon in ProceduralSoundBank.WeaponIds)
        foreach (var stage in new[] { 0, 3 })
        foreach (var layer in new[] { WeaponSoundLayer.Shot, WeaponSoundLayer.Impact })
        {
            if (layer == WeaponSoundLayer.Impact && weapon is "prism_beam" or "signal_beacon") continue;
            Add(new SoundKey(weapon, layer, LevelIndex: Math.Min(2, stage), Apex: stage == 3),
                $"{weapon}-{(stage == 3 ? "apex" : "base")}-{layer.ToString().ToLowerInvariant()}");
        }
        Add(new SoundKey("arc_relay", WeaponSoundLayer.Chain), "arc_relay-chain");
        foreach (var cue in Enum.GetValues<SoundCue>().Where(cue => cue != SoundCue.UiError))
            Add(new SoundKey(null, Cue: cue), cue.ToString());
        WriteWave(Path.Combine(output, "palette-reel.wav"), reel.ToArray(), 1);
        return entries;

        void Add(SoundKey key, string name)
        {
            var sample = mixer.GetSample(key);
            var samples = sample.Samples.Select(value => value / (float)short.MaxValue).ToArray();
            WriteWave(Path.Combine(folder, name + ".wav"), samples, 1);
            entries.Add(new PaletteEntry(name, reel.Count / (float)Rate, sample.Duration, Db(sample.Peak), RmsDb(samples)));
            reel.AddRange(samples);
            reel.AddRange(new float[Rate / 3]);
        }
    }

    private static ScenarioReport RenderScenario(GameContent content, Scenario scenario, string output,
        string musicRoot, string ffmpeg)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SetSpeed(scenario.Speed);
        for (var index = 0; index < scenario.Towers; index++)
        {
            var weapon = ProceduralSoundBank.WeaponIds[index % ProceduralSoundBank.WeaponIds.Count];
            PlaceTower(session, weapon, index, scenario.Stage);
        }
        var director = new AudioDirector();
        var mixer = new SoundMixer(8101);
        var starts = new Dictionary<string, int>();
        var requests = new Dictionary<string, int>();
        var trace = new List<object>();
        var time = 0f;
        director.Requested += request =>
        {
            requests[request.Key.Family] = requests.GetValueOrDefault(request.Key.Family) + 1;
            mixer.TryPlay(request);
        };
        mixer.Started += voice =>
        {
            starts[voice.Key.Family] = starts.GetValueOrDefault(voice.Key.Family) + 1;
            trace.Add(new { seconds = time, family = voice.Key.Family, voice.Id, voice.Gain, voice.Pitch, voice.IsLooped });
        };
        director.Attach(session);
        session.StartSandboxWave(scenario.Wave);
        if (scenario.Towers > 1) SpawnTargets(session, scenario.Towers >= 24 ? 24 : 8);
        if (scenario.Boss)
        {
            session.SpawnEnemy("t5_regenerator", 24, 1, "Boss");
            session.Enemies[^1].SetSandboxPathDistance(340, session.Map.Path);
        }
        var sfx = new float[(int)(Seconds * Rate) * 2];
        var musicGain = new float[(int)(Seconds * FramesPerSecond)];
        var maxVoices = 0;
        var maxCombat = 0;
        var maxPeakSum = 0f;
        var activity = 0.68f;
        for (var frame = 0; frame < Seconds * FramesPerSecond; frame++)
        {
            time = frame / (float)FramesPerSecond;
            director.Update(1f / FramesPerSecond);
            mixer.Update(1f / FramesPerSecond);
            session.Update(1f / FramesPerSecond);
            if (scenario.Towers >= 24 && frame > 0 && frame % (FramesPerSecond * 4) == 0 && session.Enemies.Count < 80)
                SpawnTargets(session, 24);
            if (scenario.Boss && frame == FramesPerSecond * 8)
            {
                var boss = session.Enemies.FirstOrDefault(enemy => enemy.IsBoss && !enemy.BossPhaseActive);
                if (boss is not null)
                    session.DamageResolver.Apply(boss, new DamagePayload
                    {
                        Damage = boss.MaxHealth * 0.56f, ArmorPierce = 1000, IgnoreShield = true
                    });
            }
            maxVoices = Math.Max(maxVoices, mixer.Voices.Count);
            maxCombat = Math.Max(maxCombat, mixer.Voices.Count(voice => voice.Category is SoundCategory.Combat or SoundCategory.Detail));
            maxPeakSum = MathF.Max(maxPeakSum, mixer.Voices.Sum(voice => voice.Gain * voice.Sample.Peak));
            RenderFrame(mixer, sfx, frame * SamplesPerFrame * 2);
            var target = AudioManager.MusicActivityTarget(session.Waves.IsActive,
                session.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped),
                session.Enemies.Any(enemy => enemy.IsBoss && !enemy.IsDead && !enemy.HasEscaped));
            activity += (target - activity) * (1 - MathF.Exp(-2.4f / FramesPerSecond));
            musicGain[frame] = 0.20f * activity * 1.35f;
        }
        director.Detach();
        Check.True(maxVoices <= SoundMixer.MaximumVoices && maxCombat <= SoundMixer.MaximumCombatVoices,
            scenario.Name + " respects voice caps at real-time playback speed");
        Check.True(maxPeakSum <= SoundMixer.TotalPeakBudget + 0.0001f,
            scenario.Name + " has bounded worst-case simultaneous peak headroom");
        Check.True(starts.Keys.Any(key => key.EndsWith("/Shot", StringComparison.Ordinal)),
            scenario.Name + " exercises real weapon firing");
        var musicPath = Path.Combine(musicRoot, "Music", scenario.Music);
        var music = DecodeMusic(ffmpeg, musicPath, Seconds);
        var mixed = new float[sfx.Length];
        var musicOnly = new float[sfx.Length];
        for (var index = 0; index < mixed.Length; index++)
        {
            var gain = musicGain[index / (SamplesPerFrame * 2)];
            musicOnly[index] = index < music.Length ? music[index] * gain : 0;
            mixed[index] = sfx[index] + musicOnly[index];
        }
        Check.True(mixed.Max(MathF.Abs) < 1, scenario.Name + " retains output headroom with the unchanged music gain");
        WriteWave(Path.Combine(output, scenario.Name + "-sfx.wav"), sfx, 2);
        WriteWave(Path.Combine(output, scenario.Name + "-mix.wav"), mixed, 2);
        File.WriteAllText(Path.Combine(output, scenario.Name + "-events.json"),
            JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true }));
        return new ScenarioReport(scenario.Name, scenario.Speed, scenario.Music, starts.Values.Sum(), maxVoices,
            maxCombat, maxPeakSum, RmsDb(sfx), RmsDb(musicOnly), Db(mixed.Max(MathF.Abs)), requests, starts);
    }

    private static void PlaceTower(GameSession session, string weapon, int index, int stage)
    {
        var focus = session.Map.Path.GetPosition(180 + index % 12 * 68);
        var positions = new List<Vector2>();
        for (var y = 85; y < 695; y += 24)
        for (var x = 25; x < 930; x += 24)
        {
            var point = new Vector2(x, y);
            if (session.ValidatePlacement(weapon, point) == PlacementFailure.None) positions.Add(point);
        }
        var location = positions.MinBy(point => Vector2.DistanceSquared(point, focus));
        Check.True(positions.Count > 0 && session.TryPlaceTower(weapon, location), "Audio scenario places " + weapon);
        var tower = session.Towers[^1];
        if (stage >= 1)
        {
            if (tower.RequiresDoctrine) tower.TryChooseDoctrine(tower.Definition.Tier2Doctrines[0].Id);
            else tower.TryUpgrade();
        }
        if (stage >= 2)
        {
            if (tower.RequiresSpecialization) tower.TrySpecialize(tower.Definition.Specializations[0].Id);
            else tower.TryUpgrade();
        }
        if (stage >= 3) tower.TryApexUpgrade();
    }

    private static void SpawnTargets(GameSession session, int count)
    {
        foreach (var enemy in session.Content.Enemies.Values.OrderBy(enemy => enemy.MaxHealth))
            session.SpawnSandboxTargets(enemy.Id, count, 3, "Standard", false);
    }

    private static void RenderFrame(SoundMixer mixer, float[] output, int start)
    {
        foreach (var voice in mixer.Voices)
        {
            var source = voice.Sample.Samples;
            var rate = MathF.Pow(2, voice.Pitch);
            var first = voice.Age * Rate * rate;
            var gain = voice.OutputGain * 0.65f / short.MaxValue;
            var left = MathF.Sqrt(1 - MathF.Max(0, voice.Pan));
            var right = MathF.Sqrt(1 + MathF.Min(0, voice.Pan));
            for (var sample = 0; sample < SamplesPerFrame; sample++)
            {
                var offset = first + sample * rate;
                if (voice.IsLooped) offset %= source.Length;
                if (!voice.IsLooped && offset >= source.Length - 1) continue;
                var index = (int)offset;
                var next = (index + 1) % source.Length;
                var value = (source[index] + (source[next] - source[index]) * (offset - index)) * gain;
                output[start + sample * 2] += value * left;
                output[start + sample * 2 + 1] += value * right;
            }
        }
    }

    private static float[] DecodeMusic(string ffmpeg, string path, float seconds)
    {
        var info = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var value in new[] { "-hide_banner", "-loglevel", "error", "-ss", "12", "-i", path,
            "-t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "-f", "f32le", "-ar", "44100", "-ac", "2", "pipe:1" })
            info.ArgumentList.Add(value);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("FFmpeg did not start.");
        var errors = process.StandardError.ReadToEndAsync();
        using var buffer = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Music decode failed: " + errors.GetAwaiter().GetResult());
        return MemoryMarshal.Cast<byte, float>(buffer.ToArray()).ToArray();
    }

    private static void WriteWave(string path, float[] samples, int channels)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        WriteWaveHeader(writer, samples.Length, channels);
        foreach (var sample in samples) writer.Write((short)(Math.Clamp(sample, -1, 1) * short.MaxValue));
    }

    private static void WriteWave(string path, short[] samples)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        WriteWaveHeader(writer, samples.Length, 1);
        foreach (var sample in samples) writer.Write(sample);
    }

    private static void WriteWaveHeader(BinaryWriter writer, int sampleCount, int channels)
    {
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + sampleCount * 2);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(Rate);
        writer.Write(Rate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(sampleCount * 2);
    }

    private static float Db(float value) => 20 * MathF.Log10(MathF.Max(value, 0.0000001f));
    private static float RmsDb(float[] samples) => Db((float)Math.Sqrt(samples.Average(value => (double)value * value)));
    private sealed record PaletteEntry(string Name, float ReelSeconds, float Duration, float PeakDb, float RmsDb);
    private sealed record Scenario(string Name, float Speed, int Towers, int Stage, int Wave, string Music, bool Boss = false);
    private sealed record ScenarioReport(string Name, float Speed, string Music, int Starts, int MaximumVoices,
        int MaximumCombatVoices, float MaximumPeakSum, float SfxRmsDb, float MusicRmsDb, float MixedPeakDb,
        Dictionary<string, int> RequestedFamilies, Dictionary<string, int> StartedFamilies);
}
