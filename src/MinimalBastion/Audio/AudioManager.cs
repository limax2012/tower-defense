using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MinimalBastion.Audio;

public sealed class AudioManager : IDisposable
{
    private const int SampleRate = 44100;
    private readonly Dictionary<Cue, SoundEffect> _sounds = new();
    private SoundEffect? _musicSound;
    private SoundEffectInstance? _musicInstance;
    private float _killCooldown;
    private float _sfxVolume = 0.65f;
    private float _musicVolume = 0.20f;
    private float _musicPitch;
    private bool _disposed;
    private GameSession? _attachedSession;

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(float.IsFinite(value) ? value : 0.65f, 0, 1);
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set => _musicVolume = Math.Clamp(float.IsFinite(value) ? value : 0.20f, 0, 1);
    }

    public AudioManager()
    {
        try
        {
            _sounds[Cue.Place] = CreateTone(280, 470, 0.10f, WaveShape.Triangle);
            _sounds[Cue.Upgrade] = CreateTone(480, 760, 0.14f, WaveShape.Sine);
            _sounds[Cue.Sell] = CreateTone(360, 210, 0.12f, WaveShape.Triangle);
            _sounds[Cue.Protocol] = CreateChord(390, 585, 0.20f);
            _sounds[Cue.Kill] = CreateTone(720, 560, 0.045f, WaveShape.Square);
            _sounds[Cue.Leak] = CreateTone(150, 72, 0.22f, WaveShape.Saw);
            _sounds[Cue.WaveStart] = CreateTone(260, 540, 0.24f, WaveShape.Triangle);
            _sounds[Cue.WaveClear] = CreateChord(520, 780, 0.28f);
            _sounds[Cue.Plate] = CreateNoisePulse(0.11f);
            _sounds[Cue.Forge] = CreateTone(620, 980, 0.16f, WaveShape.Sine);
            _sounds[Cue.BossPhase] = CreateTone(230, 105, 0.34f, WaveShape.Saw);
            _sounds[Cue.Victory] = CreateTriad(392, 523, 659, 0.48f);
            _sounds[Cue.Defeat] = CreateTone(190, 58, 0.48f, WaveShape.Saw);
            TryStartMusic();
        }
        catch
        {
            foreach (var sound in _sounds.Values) sound.Dispose();
            _sounds.Clear();
            throw;
        }
    }

    public static AudioManager? TryCreate()
    {
        try { return new AudioManager(); }
        catch { return null; }
    }

    public void Update(float deltaSeconds)
    {
        _killCooldown = MathF.Max(0, _killCooldown - MathF.Max(0, deltaSeconds));
        if (_musicInstance is null) return;
        try
        {
            var activity = _attachedSession?.Waves.IsActive == true ? 1f : 0.68f;
            _musicInstance.Volume = Math.Clamp(_musicVolume * activity, 0, 1);
            _musicInstance.Pitch = _musicPitch;
            if (_musicInstance.State == SoundState.Stopped) _musicInstance.Play();
        }
        catch
        {
            // Keep event cues available if a looping instance alone becomes
            // unavailable after an audio-device transition.
            DisposeMusic();
        }
    }

    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_attachedSession, session)) return;
        _attachedSession = session;
        _musicPitch = MusicPitch(session.Map.Definition.Id);
        session.TowerPlaced += _ => Play(Cue.Place, 0.72f);
        session.TowerUpgraded += (_, _) => Play(Cue.Upgrade, 0.78f);
        session.TowerSold += (_, _) => Play(Cue.Sell, 0.62f);
        session.TowerOverdriven += tower => Play(Cue.Protocol, 0.9f, ProtocolPitch(tower.Definition.Id));
        session.EnemyKilled += _ => PlayKill();
        session.EnemyEscaped += _ => Play(session.Economy.Lives <= 0 ? Cue.Defeat : Cue.Leak, 0.9f);
        session.BossPhaseChanged += _ => Play(Cue.BossPhase, 0.88f);
        session.EmergencyDefenseDeployed += (_, _) => Play(Cue.Place, 0.48f, 0.18f);
        session.EmergencyDefenseTriggered += (_, _) => Play(Cue.Plate, 0.72f);
        session.GeneratorPlaced += _ => Play(Cue.Forge, 0.72f);
        session.GeneratorUpgraded += (_, _) => Play(Cue.Upgrade, 0.72f);
        session.GeneratorSold += (_, _) => Play(Cue.Sell, 0.62f);
        session.EmergencyChargeProduced += () => Play(Cue.Forge, 0.55f);
        session.WaveStarted += _ => Play(Cue.WaveStart, 0.78f);
        session.WaveCompleted += wave => Play(wave >= session.TotalWaves && !session.IsEndlessMode ? Cue.Victory : Cue.WaveClear,
            wave >= session.TotalWaves && !session.IsEndlessMode ? 0.92f : 0.82f);
    }

    private void PlayKill()
    {
        if (_killCooldown > 0) return;
        _killCooldown = 0.055f;
        Play(Cue.Kill, 0.28f);
    }

    private void Play(Cue cue, float cueVolume, float pitch = 0)
    {
        if (_disposed || _sfxVolume <= 0 || !_sounds.TryGetValue(cue, out var sound)) return;
        try { sound.Play(Math.Clamp(_sfxVolume * cueVolume, 0, 1), Math.Clamp(pitch, -1, 1), 0); }
        catch
        {
            // Audio is presentational only. A device disappearing mid-match must
            // never interrupt the deterministic game session.
            _sfxVolume = 0;
        }
    }

    private void TryStartMusic()
    {
        try
        {
            _musicSound = CreateAmbientLoop();
            _musicInstance = _musicSound.CreateInstance();
            _musicInstance.IsLooped = true;
            _musicInstance.Volume = 0;
            _musicInstance.Play();
        }
        catch
        {
            DisposeMusic();
        }
    }

    private static float MusicPitch(string mapId) => mapId.ToLowerInvariant() switch
    {
        "crosswind_basin" => 0.035f,
        "prism_circuit" => 0.065f,
        "relay_divide" => -0.045f,
        _ => 0
    };

    private static float ProtocolPitch(string towerId) => towerId.ToLowerInvariant() switch
    {
        "siege_mortar" => -0.25f,
        "breaker_cannon" => -0.16f,
        "ember_coil" => -0.08f,
        "watchtower" => -0.03f,
        "needle_turret" => 0.04f,
        "shard_fan" => 0.10f,
        "signal_beacon" => 0.14f,
        "frost_spire" => 0.18f,
        "arc_relay" => 0.22f,
        "prism_beam" => 0.28f,
        _ => 0
    };

    private static SoundEffect CreateTone(float startFrequency, float endFrequency, float seconds, WaveShape shape)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var samples = new short[count];
        var phase = 0f;
        for (var index = 0; index < count; index++)
        {
            var t = index / (float)Math.Max(1, count - 1);
            var frequency = MathHelper.Lerp(startFrequency, endFrequency, t);
            phase += MathHelper.TwoPi * frequency / SampleRate;
            var wave = shape switch
            {
                WaveShape.Square => MathF.Sin(phase) >= 0 ? 0.62f : -0.62f,
                WaveShape.Triangle => 2f / MathF.PI * MathF.Asin(MathF.Sin(phase)),
                WaveShape.Saw => 2f * (phase / MathHelper.TwoPi - MathF.Floor(phase / MathHelper.TwoPi + 0.5f)),
                _ => MathF.Sin(phase)
            };
            samples[index] = ToSample(wave * Envelope(t) * 0.34f);
        }
        return CreateSoundEffect(samples);
    }

    private static SoundEffect CreateChord(float firstFrequency, float secondFrequency, float seconds)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var samples = new short[count];
        for (var index = 0; index < count; index++)
        {
            var t = index / (float)Math.Max(1, count - 1);
            var time = index / (float)SampleRate;
            var wave = MathF.Sin(MathHelper.TwoPi * firstFrequency * time) * 0.55f +
                       MathF.Sin(MathHelper.TwoPi * secondFrequency * time) * 0.45f;
            samples[index] = ToSample(wave * Envelope(t) * 0.28f);
        }
        return CreateSoundEffect(samples);
    }

    private static SoundEffect CreateTriad(float firstFrequency, float secondFrequency, float thirdFrequency, float seconds)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var samples = new short[count];
        for (var index = 0; index < count; index++)
        {
            var t = index / (float)Math.Max(1, count - 1);
            var time = index / (float)SampleRate;
            var wave = MathF.Sin(MathHelper.TwoPi * firstFrequency * time) * 0.38f +
                       MathF.Sin(MathHelper.TwoPi * secondFrequency * time) * 0.34f +
                       MathF.Sin(MathHelper.TwoPi * thirdFrequency * time) * 0.28f;
            samples[index] = ToSample(wave * Envelope(t) * 0.27f);
        }
        return CreateSoundEffect(samples);
    }

    private static SoundEffect CreateNoisePulse(float seconds)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var samples = new short[count];
        uint state = 0xC0FFEEu;
        for (var index = 0; index < count; index++)
        {
            state = state * 1664525u + 1013904223u;
            var noise = ((state >> 8) / (float)0xFFFFFF) * 2f - 1f;
            var time = index / (float)SampleRate;
            var body = MathF.Sin(MathHelper.TwoPi * 105f * time);
            var t = index / (float)Math.Max(1, count - 1);
            samples[index] = ToSample((noise * 0.38f + body * 0.62f) * Envelope(t) * 0.32f);
        }
        return CreateSoundEffect(samples);
    }

    private static SoundEffect CreateAmbientLoop()
    {
        const float seconds = 16f;
        var count = (int)(SampleRate * seconds);
        var samples = new short[count];
        float[] upperNotes = [220f, 247.5f, 330f, 247.5f, 196f, 247.5f, 293.333f, 247.5f];
        for (var index = 0; index < count; index++)
        {
            var time = index / (float)SampleRate;
            var step = Math.Min(upperNotes.Length - 1, (int)(time / 2f));
            var stepPhase = time % 2f;
            var pulseEnvelope = MathHelper.SmoothStep(0, 1, Math.Clamp(stepPhase / 0.18f, 0, 1)) *
                                MathHelper.SmoothStep(0, 1, Math.Clamp((1.72f - stepPhase) / 0.55f, 0, 1));
            var lowPulse = 0.5f + 0.5f * MathF.Sin(MathHelper.TwoPi * 0.25f * time - MathHelper.PiOver2);
            var drone = MathF.Sin(MathHelper.TwoPi * 55f * time) * 0.48f +
                        MathF.Sin(MathHelper.TwoPi * 82.5f * time) * 0.25f;
            var upper = MathF.Sin(MathHelper.TwoPi * upperNotes[step] * time) * pulseEnvelope * 0.22f;
            var clock = MathF.Sin(MathHelper.TwoPi * 440f * time) * MathF.Pow(lowPulse, 10) * 0.05f;
            samples[index] = ToSample((drone * (0.55f + lowPulse * 0.18f) + upper + clock) * 0.16f);
        }
        return CreateSoundEffect(samples);
    }

    private static float Envelope(float t)
    {
        var attack = MathHelper.Clamp(t / 0.08f, 0, 1);
        var release = MathHelper.Clamp((1f - t) / 0.32f, 0, 1);
        return attack * release;
    }

    private static short ToSample(float value) => (short)(MathHelper.Clamp(value, -1, 1) * short.MaxValue);

    private static MemoryStream CreateWaveStream(IReadOnlyList<short> samples)
    {
        var stream = new MemoryStream(44 + samples.Count * sizeof(short));
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + samples.Count * sizeof(short));
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(samples.Count * sizeof(short));
            foreach (var sample in samples) writer.Write(sample);
        }
        stream.Position = 0;
        return stream;
    }

    private static SoundEffect CreateSoundEffect(IReadOnlyList<short> samples)
    {
        using var stream = CreateWaveStream(samples);
        return SoundEffect.FromStream(stream);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeMusic();
        foreach (var sound in _sounds.Values) sound.Dispose();
        _sounds.Clear();
    }

    private void DisposeMusic()
    {
        try { _musicInstance?.Stop(); } catch { }
        _musicInstance?.Dispose();
        _musicSound?.Dispose();
        _musicInstance = null;
        _musicSound = null;
    }

    private enum Cue { Place, Upgrade, Sell, Protocol, Kill, Leak, WaveStart, WaveClear, Plate, Forge, BossPhase, Victory, Defeat }
    private enum WaveShape { Sine, Triangle, Square, Saw }
}
