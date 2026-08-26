using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MinimalBastion.Audio;

public sealed class AudioManager : IDisposable
{
    private const int SampleRate = 44100;
    private const float MusicSourceGain = 1.8f;
    private readonly Dictionary<Cue, SoundEffect> _sounds = new();
    private readonly Dictionary<string, SoundEffect> _towerImpacts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _towerImpactCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private SoundEffect? _musicSound;
    private SoundEffectInstance? _musicInstance;
    private readonly SoundEffect? _menuMusicSound;
    private bool _ownsMusicSound;
    private float _killCooldown;
    private float _leakCooldown;
    private float _bossPhaseCooldown;
    private float _combatImpactCooldown;
    private float _sfxVolume = 0.65f;
    private float _musicVolume = 0.20f;
    private float _musicActivity = 0.68f;
    private string _musicThemeId = "menu";
    private bool _disposed;
    private bool _defeatCuePlayed;
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

    public AudioManager(SoundEffect? menuMusicSound = null)
    {
        _menuMusicSound = menuMusicSound;
        try
        {
            _sounds[Cue.Place] = CreateTone(280, 470, 0.10f, WaveShape.Triangle);
            _sounds[Cue.Upgrade] = CreateTone(480, 760, 0.14f, WaveShape.Sine);
            _sounds[Cue.Sell] = CreateTone(360, 210, 0.12f, WaveShape.Triangle);
            _sounds[Cue.Protocol] = CreateChord(390, 585, 0.20f);
            _sounds[Cue.Kill] = CreateTone(720, 560, 0.045f, WaveShape.Square);
            _sounds[Cue.Leak] = CreateTone(392, 392, 0.10f, WaveShape.Sine);
            _sounds[Cue.WaveStart] = CreateTone(260, 540, 0.24f, WaveShape.Triangle);
            _sounds[Cue.WaveClear] = CreateChord(520, 780, 0.28f);
            _sounds[Cue.Plate] = CreateNoisePulse(0.11f);
            _sounds[Cue.Forge] = CreateTone(620, 980, 0.16f, WaveShape.Sine);
            _sounds[Cue.BossPhase] = CreateTwoNoteCue(330, 440, 0.24f);
            _sounds[Cue.Victory] = CreateTriad(392, 523, 659, 0.48f);
            _sounds[Cue.Defeat] = CreateTwoNoteCue(392, 330, 0.30f);
            _sounds[Cue.UiConfirm] = CreateTone(410, 620, 0.075f, WaveShape.Sine);
            _sounds[Cue.UiBack] = CreateTone(430, 300, 0.075f, WaveShape.Triangle);
            _sounds[Cue.UiDelete] = CreateTone(230, 150, 0.10f, WaveShape.Saw);
            CreateTowerImpactPalette();
            TryStartMusic("menu");
        }
        catch
        {
            foreach (var sound in _sounds.Values) sound.Dispose();
            _sounds.Clear();
            foreach (var sound in _towerImpacts.Values) sound.Dispose();
            _towerImpacts.Clear();
            throw;
        }
    }

    public static AudioManager? TryCreate(SoundEffect? menuMusicSound = null)
    {
        try { return new AudioManager(menuMusicSound); }
        catch { return null; }
    }

    public void Update(float deltaSeconds)
    {
        _killCooldown = MathF.Max(0, _killCooldown - MathF.Max(0, deltaSeconds));
        _leakCooldown = MathF.Max(0, _leakCooldown - MathF.Max(0, deltaSeconds));
        _bossPhaseCooldown = MathF.Max(0, _bossPhaseCooldown - MathF.Max(0, deltaSeconds));
        _combatImpactCooldown = MathF.Max(0, _combatImpactCooldown - MathF.Max(0, deltaSeconds));
        foreach (var towerId in _towerImpactCooldowns.Keys.ToArray())
        {
            var remaining = MathF.Max(0, _towerImpactCooldowns[towerId] - MathF.Max(0, deltaSeconds));
            if (remaining <= 0) _towerImpactCooldowns.Remove(towerId);
            else _towerImpactCooldowns[towerId] = remaining;
        }
        if (_musicInstance is null) return;
        try
        {
            var waveActive = _attachedSession?.Waves.IsActive == true;
            var liveEnemies = _attachedSession?.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped) ?? 0;
            var bossPresent = _attachedSession?.Enemies.Any(enemy => enemy.IsBoss && !enemy.IsDead && !enemy.HasEscaped) == true;
            var targetActivity = MusicActivityTarget(waveActive, liveEnemies, bossPresent);
            var blend = 1f - MathF.Exp(-MathF.Max(0, deltaSeconds) * 2.4f);
            _musicActivity = MathHelper.Lerp(_musicActivity, targetActivity, blend);
            _musicInstance.Volume = Math.Clamp(_musicVolume * _musicActivity, 0, 1);
            _musicInstance.Pitch = 0;
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
        _leakCooldown = 0;
        _bossPhaseCooldown = 0;
        _defeatCuePlayed = false;
        SwitchMusic(session.Map.Definition.Id);
        session.TowerPlaced += _ => Play(Cue.Place, 0.72f);
        session.TowerUpgraded += (_, _) => Play(Cue.Upgrade, 0.78f);
        session.TowerSold += (_, _) => Play(Cue.Sell, 0.62f);
        session.TowerOverdriven += tower => Play(Cue.Protocol, 0.9f, ProtocolPitch(tower.Definition.Id));
        session.EnemyKilled += _ => PlayKill();
        session.EnemyEscaped += _ => PlayLeak(session.Economy.Lives <= 0);
        session.BossPhaseChanged += _ => PlayBossPhase();
        session.EmergencyDefenseDeployed += (_, _) => Play(Cue.Place, 0.48f, 0.18f);
        session.EmergencyDefenseTriggered += (_, _) => Play(Cue.Plate, 0.72f);
        session.GeneratorPlaced += _ => Play(Cue.Forge, 0.72f);
        session.GeneratorUpgraded += (_, _) => Play(Cue.Upgrade, 0.72f);
        session.GeneratorSold += (_, _) => Play(Cue.Sell, 0.62f);
        session.EmergencyChargeProduced += () => Play(Cue.Forge, 0.55f);
        session.WaveStarted += _ => Play(Cue.WaveStart, 0.78f);
        session.WaveCompleted += wave => Play(wave >= session.TotalWaves && !session.IsEndlessMode ? Cue.Victory : Cue.WaveClear,
            wave >= session.TotalWaves && !session.IsEndlessMode ? 0.92f : 0.82f);
        session.DamageResolver.DamageApplied += OnDamageApplied;
    }

    public void Detach()
    {
        _attachedSession = null;
        _leakCooldown = 0;
        _bossPhaseCooldown = 0;
        _defeatCuePlayed = false;
        _towerImpactCooldowns.Clear();
        SwitchMusic("menu");
    }

    public void PlayUiConfirm() => Play(Cue.UiConfirm, 0.42f);
    public void PlayUiBack() => Play(Cue.UiBack, 0.36f);
    public void PlayUiDelete() => Play(Cue.UiDelete, 0.42f);

    public static float MusicActivityTarget(bool waveActive, int liveEnemyCount, bool bossPresent)
    {
        if (!waveActive) return 0.68f;
        var pressure = MathHelper.Clamp(Math.Max(0, liveEnemyCount) / 70f, 0, 1);
        return MathF.Min(1f, 0.78f + pressure * 0.17f + (bossPresent ? 0.08f : 0));
    }

    private void PlayKill()
    {
        if (_killCooldown > 0) return;
        _killCooldown = 0.055f;
        Play(Cue.Kill, 0.28f);
    }

    private void PlayLeak(bool defeat)
    {
        if (defeat)
        {
            if (_defeatCuePlayed) return;
            _defeatCuePlayed = true;
            Play(Cue.Defeat, 0.95f);
            return;
        }
        if (_leakCooldown > 0) return;
        _leakCooldown = 0.055f;
        Play(Cue.Leak, 0.50f);
    }

    private void PlayBossPhase()
    {
        if (_bossPhaseCooldown > 0) return;
        _bossPhaseCooldown = 0.25f;
        Play(Cue.BossPhase, 0.80f);
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

    private void OnDamageApplied(Combat.DamageReport report)
    {
        if (report.ShieldDamage + report.HealthDamage <= 0 || _attachedSession is null) return;
        if (report.SourceTowerId > 0)
        {
            var source = _attachedSession.Towers.FirstOrDefault(tower => tower.Id == report.SourceTowerId);
            if (source is not null)
            {
                TryPlayTowerImpact(source.Definition.Id, false);
                var support = _attachedSession.GetSupportBuff(source);
                var beaconId = support.AttackSpeedSourceTowerId > 0
                    ? support.AttackSpeedSourceTowerId
                    : support.RangeSourceTowerId;
                if (beaconId > 0 && _attachedSession.Towers.FirstOrDefault(tower => tower.Id == beaconId) is { } beacon)
                    TryPlayTowerImpact(beacon.Definition.Id, true);
            }
        }
    }

    private void TryPlayTowerImpact(string towerId, bool supportCue)
    {
        if (_disposed || _sfxVolume <= 0 || !_towerImpacts.TryGetValue(towerId, out var sound)) return;
        if (_towerImpactCooldowns.ContainsKey(towerId) || !supportCue && _combatImpactCooldown > 0) return;
        _towerImpactCooldowns[towerId] = CombatCueCooldown(towerId);
        if (!supportCue) _combatImpactCooldown = 0.028f;
        var liveEnemies = _attachedSession?.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped) ?? 0;
        var pressureMix = liveEnemies switch { > 80 => 0.42f, > 35 => 0.58f, > 12 => 0.74f, _ => 1f };
        var cueVolume = (supportCue ? 0.07f : 0.13f) * pressureMix;
        try { sound.Play(Math.Clamp(_sfxVolume * cueVolume, 0, 1), 0, 0); }
        catch { _sfxVolume = 0; }
    }

    public static float CombatCueCooldown(string towerId) => towerId.ToLowerInvariant() switch
    {
        "needle_turret" => 0.085f,
        "shard_fan" => 0.11f,
        "arc_relay" => 0.12f,
        "prism_beam" => 0.13f,
        "frost_spire" => 0.15f,
        "ember_coil" => 0.17f,
        "watchtower" => 0.20f,
        "breaker_cannon" => 0.22f,
        "siege_mortar" => 0.28f,
        "signal_beacon" => 0.42f,
        _ => 0.14f
    };

    private void CreateTowerImpactPalette()
    {
        _towerImpacts["needle_turret"] = CreateTone(1080, 1420, 0.026f, WaveShape.Sine);
        _towerImpacts["frost_spire"] = CreateTone(610, 360, 0.055f, WaveShape.Sine);
        _towerImpacts["shard_fan"] = CreateTone(920, 540, 0.032f, WaveShape.Saw);
        _towerImpacts["watchtower"] = CreateTone(310, 165, 0.070f, WaveShape.Triangle);
        _towerImpacts["ember_coil"] = CreateTone(240, 120, 0.075f, WaveShape.Saw);
        _towerImpacts["breaker_cannon"] = CreateTone(175, 78, 0.085f, WaveShape.Square);
        _towerImpacts["signal_beacon"] = CreateChord(740, 1110, 0.065f);
        _towerImpacts["arc_relay"] = CreateTone(820, 285, 0.060f, WaveShape.Triangle);
        _towerImpacts["siege_mortar"] = CreateImpactPulse(0.095f, 82f);
        _towerImpacts["prism_beam"] = CreateTone(1240, 710, 0.050f, WaveShape.Sine);
    }

    private void SwitchMusic(string themeId)
    {
        var normalized = string.IsNullOrWhiteSpace(themeId) ? "menu" : themeId.ToLowerInvariant();
        if (_musicInstance is not null && normalized == _musicThemeId) return;
        DisposeMusic();
        TryStartMusic(normalized);
    }

    private void TryStartMusic(string themeId)
    {
        try
        {
            _musicThemeId = themeId;
            if (themeId.Equals("menu", StringComparison.OrdinalIgnoreCase) && _menuMusicSound is not null)
            {
                _musicSound = _menuMusicSound;
                _ownsMusicSound = false;
            }
            else
            {
                _musicSound = CreateTacticalLoop(themeId);
                _ownsMusicSound = true;
            }
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

    private static SoundEffect CreateTwoNoteCue(float firstFrequency, float secondFrequency, float seconds)
    {
        var count = Math.Max(2, (int)(SampleRate * seconds));
        var samples = new short[count];
        var split = count / 2;
        for (var index = 0; index < count; index++)
        {
            var firstNote = index < split;
            var noteStart = firstNote ? 0 : split;
            var noteLength = firstNote ? split : count - split;
            var noteTime = (index - noteStart) / (float)Math.Max(1, noteLength - 1);
            var frequency = firstNote ? firstFrequency : secondFrequency;
            var time = (index - noteStart) / (float)SampleRate;
            var envelope = MathF.Sin(MathF.PI * noteTime);
            var wave = MathF.Sin(MathHelper.TwoPi * frequency * time);
            samples[index] = ToSample(wave * envelope * envelope * 0.18f);
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

    private static SoundEffect CreateImpactPulse(float seconds, float bodyFrequency)
    {
        var count = Math.Max(1, (int)(SampleRate * seconds));
        var samples = new short[count];
        uint state = 0x51E6E123u;
        for (var index = 0; index < count; index++)
        {
            state = state * 1664525u + 1013904223u;
            var noise = ((state >> 8) / (float)0xFFFFFF) * 2f - 1f;
            var time = index / (float)SampleRate;
            var t = index / (float)Math.Max(1, count - 1);
            var body = MathF.Sin(MathHelper.TwoPi * bodyFrequency * (1f - t * 0.18f) * time);
            samples[index] = ToSample((body * 0.76f + noise * 0.24f) * Envelope(t) * 0.28f);
        }
        return CreateSoundEffect(samples);
    }

    private static SoundEffect CreateTacticalLoop(string themeId)
    {
        const float seconds = 24f;
        var count = (int)(SampleRate * seconds);
        var samples = new short[count];
        var theme = MusicTheme.For(themeId);
        var stepSeconds = seconds / theme.Melody.Length;
        for (var index = 0; index < count; index++)
        {
            var time = index / (float)SampleRate;
            var step = Math.Min(theme.Melody.Length - 1, (int)(time / stepSeconds));
            var bar = Math.Min(theme.Bass.Length - 1, (int)(time / (seconds / theme.Bass.Length)));
            var stepPhase = time % stepSeconds;
            var attack = MathHelper.SmoothStep(0, 1, Math.Clamp(stepPhase / 0.16f, 0, 1));
            var release = MathHelper.SmoothStep(0, 1, Math.Clamp((stepSeconds - 0.16f - stepPhase) / 0.52f, 0, 1));
            var noteEnvelope = attack * release;
            var breathing = 0.78f + MathF.Sin(MathHelper.TwoPi * time / 6f - MathHelper.PiOver2) * 0.12f;
            var bass = MathF.Sin(MathHelper.TwoPi * theme.Bass[bar] * time) * 0.48f +
                       MathF.Sin(MathHelper.TwoPi * theme.Bass[bar] * 1.5f * time) * 0.18f;
            var melodyFrequency = theme.Melody[step];
            var melody = melodyFrequency <= 0 ? 0 :
                (MathF.Sin(MathHelper.TwoPi * melodyFrequency * time) * 0.72f +
                 MathF.Sin(MathHelper.TwoPi * melodyFrequency * 2f * time) * 0.12f) * noteEnvelope;
            var counterFrequency = theme.Counter[(step / 2) % theme.Counter.Length];
            var counter = MathF.Sin(MathHelper.TwoPi * counterFrequency * time) *
                          (0.5f + 0.5f * MathF.Sin(MathHelper.TwoPi * time / 3f)) * 0.10f;
            var clockPhase = time % theme.ClockSeconds;
            var clockEnvelope = MathF.Exp(-clockPhase * 22f);
            var clock = MathF.Sin(MathHelper.TwoPi * theme.ClockFrequency * time) * clockEnvelope * 0.08f;
            samples[index] = ToSample((bass * breathing + melody * 0.34f + counter + clock) * theme.Gain * MusicSourceGain);
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
        foreach (var sound in _towerImpacts.Values) sound.Dispose();
        _towerImpacts.Clear();
    }

    private void DisposeMusic()
    {
        try { _musicInstance?.Stop(); } catch { }
        _musicInstance?.Dispose();
        if (_ownsMusicSound) _musicSound?.Dispose();
        _musicInstance = null;
        _musicSound = null;
        _ownsMusicSound = false;
    }

    private enum Cue
    {
        Place, Upgrade, Sell, Protocol, Kill, Leak, WaveStart, WaveClear, Plate, Forge, BossPhase, Victory, Defeat,
        UiConfirm, UiBack, UiDelete
    }
    private enum WaveShape { Sine, Triangle, Square, Saw }

    private sealed record MusicTheme(float[] Bass, float[] Melody, float[] Counter, float ClockSeconds,
        float ClockFrequency, float Gain)
    {
        public static MusicTheme For(string themeId) => themeId.ToLowerInvariant() switch
        {
            "crosswind_basin" => new(
                [73.416f, 65.406f, 82.407f, 55f],
                [293.665f, 0, 329.628f, 369.994f, 440f, 369.994f, 329.628f, 0, 293.665f, 329.628f, 246.942f, 293.665f, 369.994f, 329.628f, 293.665f, 0],
                [146.832f, 164.814f, 184.997f, 123.471f], 0.75f, 587.33f, 0.115f),
            "prism_circuit" => new(
                [55f, 65.406f, 73.416f, 82.407f],
                [329.628f, 391.995f, 493.883f, 0, 440f, 391.995f, 329.628f, 293.665f, 329.628f, 493.883f, 440f, 0, 391.995f, 329.628f, 293.665f, 246.942f],
                [164.814f, 195.998f, 246.942f, 146.832f], 0.60f, 659.255f, 0.105f),
            "relay_divide" => new(
                [49f, 55f, 61.735f, 46.249f],
                [246.942f, 277.183f, 293.665f, 0, 220f, 246.942f, 329.628f, 277.183f, 246.942f, 220f, 184.997f, 0, 220f, 277.183f, 246.942f, 184.997f],
                [123.471f, 138.591f, 146.832f, 110f], 0.50f, 493.883f, 0.115f),
            "foundry_loop" => new(
                [55f, 65.406f, 49f, 73.416f],
                [220f, 246.942f, 293.665f, 0, 329.628f, 293.665f, 246.942f, 220f, 196f, 246.942f, 277.183f, 0, 293.665f, 246.942f, 220f, 196f],
                [110f, 130.813f, 146.832f, 98f], 0.75f, 440f, 0.11f),
            _ => new(
                [55f, 65.406f, 49f, 55f],
                [220f, 0, 246.942f, 0, 293.665f, 0, 246.942f, 0, 196f, 0, 220f, 0, 246.942f, 0, 220f, 0],
                [110f, 130.813f, 98f, 110f], 1.5f, 440f, 0.085f)
        };
    }
}
