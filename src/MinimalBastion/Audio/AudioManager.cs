using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MinimalBastion.Audio;

public sealed class AudioManager : IDisposable
{
    private const int SampleRate = 44100;
    private readonly Dictionary<Cue, SoundEffect> _sounds = new();
    private float _killCooldown;
    private bool _disposed;
    private GameSession? _attachedSession;

    public float Volume { get; set; } = 0.65f;

    public AudioManager()
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
    }

    public static AudioManager? TryCreate()
    {
        try { return new AudioManager(); }
        catch { return null; }
    }

    public void Update(float deltaSeconds) => _killCooldown = MathF.Max(0, _killCooldown - MathF.Max(0, deltaSeconds));

    public void Attach(GameSession session)
    {
        if (ReferenceEquals(_attachedSession, session)) return;
        _attachedSession = session;
        session.TowerPlaced += _ => Play(Cue.Place, 0.72f);
        session.TowerUpgraded += (_, _) => Play(Cue.Upgrade, 0.78f);
        session.TowerSold += (_, _) => Play(Cue.Sell, 0.62f);
        session.TowerOverdriven += _ => Play(Cue.Protocol, 0.9f);
        session.EnemyKilled += _ => PlayKill();
        session.EnemyEscaped += _ => Play(Cue.Leak, 0.9f);
        session.EmergencyDefenseTriggered += (_, _) => Play(Cue.Plate, 0.72f);
        session.GeneratorPlaced += _ => Play(Cue.Forge, 0.72f);
        session.GeneratorUpgraded += (_, _) => Play(Cue.Upgrade, 0.72f);
        session.EmergencyChargeProduced += () => Play(Cue.Forge, 0.55f);
        session.WaveStarted += _ => Play(Cue.WaveStart, 0.78f);
        session.WaveCompleted += _ => Play(Cue.WaveClear, 0.82f);
    }

    private void PlayKill()
    {
        if (_killCooldown > 0) return;
        _killCooldown = 0.055f;
        Play(Cue.Kill, 0.28f);
    }

    private void Play(Cue cue, float cueVolume)
    {
        if (_disposed || Volume <= 0 || !_sounds.TryGetValue(cue, out var sound)) return;
        sound.Play(Math.Clamp(Volume * cueVolume, 0, 1), 0, 0);
    }

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
        return SoundEffect.FromStream(CreateWaveStream(samples));
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
        return SoundEffect.FromStream(CreateWaveStream(samples));
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
        return SoundEffect.FromStream(CreateWaveStream(samples));
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var sound in _sounds.Values) sound.Dispose();
        _sounds.Clear();
    }

    private enum Cue { Place, Upgrade, Sell, Protocol, Kill, Leak, WaveStart, WaveClear, Plate, Forge }
    private enum WaveShape { Sine, Triangle, Square, Saw }
}
