using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using MaximalBastion.Core;

namespace MaximalBastion.Audio;

public sealed class AudioManager : IDisposable
{
    private const int SampleRate = 44100;
    private const float MusicSourceGain = 1.8f;
    private const float GameplayMusicGain = 1.35f;
    internal const float OneShotCompressionThreshold = 0.18f;
    internal const float OneShotCompressionRatio = 2.5f;
    internal const float OneShotPeakLimit = 0.22f;
    private readonly AudioDirector _director = new();
    private readonly SoundMixer _mixer = new();
    private readonly Dictionary<SoundSample, SoundEffect> _effects = [];
    private readonly Dictionary<int, SoundEffectInstance> _voices = [];
    private bool _effectsUnavailable;
    private SoundEffect? _musicSound;
    private SoundEffectInstance? _musicInstance;
    private readonly SoundEffect? _menuMusicSound;
    private readonly IReadOnlyList<Song> _gameplaySongs;
    private readonly ShuffleBag? _gameplayShuffle;
    private bool _ownsMusicSound;
    private bool _gameplayPlaylistActive;
    private float _playlistStartGrace;
    private float _sfxVolume = 0.65f;
    private float _musicVolume = 0.20f;
    private float _musicActivity = 0.68f;
    private string _musicThemeId = "menu";
    private bool _disposed;
    private GameSession? _attachedSession;

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Math.Clamp(float.IsFinite(value) ? value : 0.65f, 0, 1);
            if (_sfxVolume <= 0) _mixer.Clear();
            SynchronizeVoices();
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set => _musicVolume = Math.Clamp(float.IsFinite(value) ? value : 0.20f, 0, 1);
    }

    public AudioManager(SoundEffect? menuMusicSound = null, IReadOnlyList<Song>? gameplaySongs = null)
    {
        _menuMusicSound = menuMusicSound;
        _gameplaySongs = gameplaySongs?.Where(song => song is not null).ToArray() ?? [];
        _gameplayShuffle = _gameplaySongs.Count > 0 ? new ShuffleBag(_gameplaySongs.Count) : null;
        _director.Requested += Play;
        _mixer.Started += StartVoice;
        _mixer.Stopped += StopVoice;
        TryStartMusic("menu");
    }

    public static AudioManager? TryCreate(SoundEffect? menuMusicSound = null, IReadOnlyList<Song>? gameplaySongs = null)
    {
        try { return new AudioManager(menuMusicSound, gameplaySongs); }
        catch { return null; }
    }

    public void Update(float deltaSeconds, bool combatEnabled = true)
    {
        if (_disposed) return;
        _director.Update(deltaSeconds, combatEnabled);
        if (!combatEnabled || _attachedSession?.IsCoOpPaused == true) _mixer.StopCombat();
        _mixer.Update(deltaSeconds);
        SynchronizeVoices();
        if (_musicInstance is null && !_gameplayPlaylistActive) return;
        try
        {
            var waveActive = _attachedSession?.Waves.IsActive == true;
            var liveEnemies = _attachedSession?.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped) ?? 0;
            var bossPresent = _attachedSession?.Enemies.Any(enemy => enemy.IsBoss && !enemy.IsDead && !enemy.HasEscaped) == true;
            var targetActivity = MusicActivityTarget(waveActive, liveEnemies, bossPresent);
            var blend = 1f - MathF.Exp(-MathF.Max(0, deltaSeconds) * 2.4f);
            _musicActivity = MathHelper.Lerp(_musicActivity, targetActivity, blend);
            var playbackVolume = Math.Clamp(_musicVolume * _musicActivity *
                (_gameplayPlaylistActive ? GameplayMusicGain : 1f), 0, 1);
            if (_gameplayPlaylistActive)
            {
                MediaPlayer.Volume = playbackVolume;
                _playlistStartGrace = MathF.Max(0, _playlistStartGrace - MathF.Max(0, deltaSeconds));
                if (_playlistStartGrace <= 0 && MediaPlayer.State == MediaState.Stopped)
                    PlayNextGameplaySong();
            }
            else if (_musicInstance is not null)
            {
                _musicInstance.Volume = playbackVolume;
                _musicInstance.Pitch = 0;
                if (_musicInstance.State == SoundState.Stopped) _musicInstance.Play();
            }
        }
        catch
        {
            if (_gameplayPlaylistActive)
            {
                StopGameplayPlaylist();
                TryStartSoundEffectMusic(_musicThemeId);
            }
            else
            {
                // Keep event cues available if a looping instance alone becomes
                // unavailable after an audio-device transition.
                DisposeMusic();
            }
        }
    }

    public void Attach(GameSession session, bool preserveMusic = false)
    {
        if (_disposed || ReferenceEquals(_attachedSession, session)) return;
        _director.Attach(session);
        _mixer.Clear(preserveInterface: true);
        _attachedSession = session;
        var themeId = session.Map.Definition.Id;
        if (preserveMusic && HasActiveGameplayMusic()) _musicThemeId = themeId.ToLowerInvariant();
        else SwitchMusic(themeId);
    }

    public void Detach(bool preserveMusic = false)
    {
        _director.Detach();
        _mixer.Clear(preserveInterface: true);
        _attachedSession = null;
        if (!_disposed && (!preserveMusic || !HasActiveGameplayMusic())) SwitchMusic("menu");
    }

    public void PlayUiNavigate() => PlayUi(SoundCue.UiNavigate, 0.30f);
    public void PlayUiConfirm() => PlayUi(SoundCue.UiConfirm, 0.40f);
    public void PlayUiBack() => PlayUi(SoundCue.UiBack, 0.34f);
    public void PlayUiDelete() => PlayUi(SoundCue.UiDelete, 0.36f);
    public void PlayUiError() => PlayUi(SoundCue.UiError, 0.34f);

    private void PlayUi(SoundCue cue, float gain) =>
        Play(new SoundRequest(new SoundKey(null, Cue: cue), gain, SoundCategory.Interface, Cooldown: 0.065f));

    private void Play(SoundRequest request)
    {
        if (_disposed || _effectsUnavailable || _sfxVolume <= 0) return;
        try { _mixer.TryPlay(request); }
        catch
        {
            _effectsUnavailable = true;
            _mixer.Clear();
        }
    }

    private void StartVoice(SoundVoice voice)
    {
        SoundEffectInstance? instance = null;
        try
        {
            if (!_effects.TryGetValue(voice.Sample, out var effect))
            {
                effect = CreateSoundEffect(voice.Sample.Samples);
                _effects.Add(voice.Sample, effect);
            }
            instance = effect.CreateInstance();
            PlatformServices.PlayWithImmediateOneShotAudioParameters(() =>
            {
                instance.IsLooped = voice.IsLooped;
                instance.Volume = Math.Clamp(_sfxVolume * voice.OutputGain, 0, 1);
                instance.Pitch = voice.Pitch;
                instance.Pan = voice.Pan;
                instance.Play();
            });
            _voices.Add(voice.Id, instance);
        }
        catch
        {
            try { instance?.Dispose(); } catch { }
            // Device failures cannot interrupt gameplay or overwrite the user's volume preference.
            _effectsUnavailable = true;
            _mixer.Clear();
        }
    }

    private void SynchronizeVoices()
    {
        try
        {
            foreach (var voice in _mixer.Voices)
                if (_voices.TryGetValue(voice.Id, out var instance))
                    instance.Volume = Math.Clamp(_sfxVolume * voice.OutputGain, 0, 1);
        }
        catch
        {
            _effectsUnavailable = true;
            _mixer.Clear();
        }
    }

    private void StopVoice(int id)
    {
        if (!_voices.Remove(id, out var instance)) return;
        try { instance.Stop(); } catch { }
        try { instance.Dispose(); } catch { }
    }

    public static float MusicActivityTarget(bool waveActive, int liveEnemyCount, bool bossPresent)
    {
        if (!waveActive) return 0.68f;
        var pressure = MathHelper.Clamp(Math.Max(0, liveEnemyCount) / 70f, 0, 1);
        return MathF.Min(1f, 0.78f + pressure * 0.17f + (bossPresent ? 0.08f : 0));
    }

    internal static float MasteredOneShotGain(float sourcePeak, float requestedGain)
    {
        if (!float.IsFinite(sourcePeak) || sourcePeak <= 0 ||
            !float.IsFinite(requestedGain) || requestedGain <= 0)
            return 0;

        var gain = Math.Clamp(requestedGain, 0, 1);
        var outputPeak = sourcePeak * gain;
        if (outputPeak > OneShotCompressionThreshold)
        {
            outputPeak = OneShotCompressionThreshold +
                (outputPeak - OneShotCompressionThreshold) / OneShotCompressionRatio;
        }
        outputPeak = MathF.Min(outputPeak, OneShotPeakLimit);
        return outputPeak / sourcePeak;
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

    private void SwitchMusic(string themeId)
    {
        var normalized = string.IsNullOrWhiteSpace(themeId) ? "menu" : themeId.ToLowerInvariant();
        if (normalized == _musicThemeId && (_musicInstance is not null || _gameplayPlaylistActive)) return;
        StopMusicPlayback();
        TryStartMusic(normalized);
    }

    private bool HasActiveGameplayMusic() =>
        _gameplayPlaylistActive ||
        (_musicInstance is not null && !_musicThemeId.Equals("menu", StringComparison.OrdinalIgnoreCase));

    private void TryStartMusic(string themeId)
    {
        try
        {
            _musicThemeId = themeId;
            if (!themeId.Equals("menu", StringComparison.OrdinalIgnoreCase) && TryStartGameplayPlaylist()) return;
            TryStartSoundEffectMusic(themeId);
        }
        catch
        {
            StopMusicPlayback();
        }
    }

    private bool TryStartGameplayPlaylist()
    {
        if (_gameplayShuffle is null || _gameplaySongs.Count == 0) return false;
        try
        {
            _gameplayPlaylistActive = true;
            MediaPlayer.IsRepeating = false;
            MediaPlayer.IsShuffled = false;
            PlayNextGameplaySong();
            return true;
        }
        catch
        {
            StopGameplayPlaylist();
            return false;
        }
    }

    private void PlayNextGameplaySong()
    {
        if (_gameplayShuffle is null || _gameplaySongs.Count == 0) return;
        MediaPlayer.Play(_gameplaySongs[_gameplayShuffle.Next()]);
        MediaPlayer.Volume = Math.Clamp(_musicVolume * _musicActivity * GameplayMusicGain, 0, 1);
        _playlistStartGrace = 0.5f;
    }

    private void TryStartSoundEffectMusic(string themeId)
    {
        try
        {
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
        _director.Detach();
        _director.Requested -= Play;
        _mixer.Clear();
        StopMusicPlayback();
        foreach (var sound in _effects.Values) sound.Dispose();
        _effects.Clear();
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

    private void StopGameplayPlaylist()
    {
        if (!_gameplayPlaylistActive) return;
        try { MediaPlayer.Stop(); } catch { }
        _gameplayPlaylistActive = false;
        _playlistStartGrace = 0;
    }

    private void StopMusicPlayback()
    {
        StopGameplayPlaylist();
        DisposeMusic();
    }

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
