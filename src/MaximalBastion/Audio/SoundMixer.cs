namespace MaximalBastion.Audio;

internal enum SoundCategory { Detail, Combat, Notification, Interface, Critical }

internal readonly record struct SoundKey(string? WeaponId, WeaponSoundLayer Layer = default,
    SoundCue Cue = default, int LevelIndex = 0, bool Apex = false)
{
    public string Family => WeaponId is null ? Cue.ToString() : $"{WeaponId}/{Layer}";
}

internal readonly record struct SoundRequest(SoundKey Key, float Gain,
    SoundCategory Category = SoundCategory.Combat, float Pan = 0, float Pitch = 0,
    float Cooldown = 0.08f, float HoldSeconds = 0);

internal sealed record SoundSample(short[] Samples, float Peak)
{
    public float Duration => Samples.Length / (float)ProceduralSoundBank.SampleRate;
}

internal sealed class SoundVoice
{
    public required int Id { get; init; }
    public required SoundKey Key { get; init; }
    public required SoundSample Sample { get; init; }
    public required SoundCategory Category { get; init; }
    public required float Gain { get; init; }
    public required float Pitch { get; init; }
    public required float Pan { get; init; }
    public required bool IsLooped { get; init; }
    public float Age { get; internal set; }
    public float Envelope { get; internal set; }
    public float OutputGain => Gain * Envelope;
    internal float HoldRemaining { get; set; }
}

/// <summary>Real-time voice admission and loop envelopes, independent of the audio device and simulation clock.</summary>
internal sealed class SoundMixer
{
    internal const int MaximumVoices = 20;
    internal const int MaximumCombatVoices = 12;
    internal const float CombatPeakBudget = 0.30f;
    internal const float TotalPeakBudget = 0.58f;
    private readonly List<SoundVoice> _voices = [];
    private readonly Dictionary<(SoundKey Key, int Variant), SoundSample> _samples = [];
    private readonly Dictionary<string, (float Remaining, SoundCategory Priority)> _cooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _variants = new(StringComparer.Ordinal);
    private readonly Random _variation;
    private int _nextId;

    public SoundMixer(int seed = 7817) => _variation = new Random(seed);
    public IReadOnlyList<SoundVoice> Voices => _voices;
    public event Action<SoundVoice>? Started;
    public event Action<int>? Stopped;

    public void Update(float deltaSeconds)
    {
        var elapsed = float.IsFinite(deltaSeconds) ? MathF.Max(0, deltaSeconds) : 0;
        foreach (var key in _cooldowns.Keys.ToArray())
        {
            var cooldown = _cooldowns[key];
            cooldown.Remaining -= elapsed;
            if (cooldown.Remaining <= 0) _cooldowns.Remove(key);
            else _cooldowns[key] = cooldown;
        }
        for (var index = _voices.Count - 1; index >= 0; index--)
        {
            var voice = _voices[index];
            voice.Age += elapsed;
            if (voice.IsLooped)
            {
                voice.HoldRemaining -= elapsed;
                // The lease bridges successive beam segments; release survives pause and target loss.
                voice.Envelope = Math.Clamp(voice.Age / 0.035f, 0, 1) *
                    Math.Clamp((voice.HoldRemaining + 0.085f) / 0.085f, 0, 1);
                if (voice.HoldRemaining <= -0.085f) StopAt(index);
            }
            else if (voice.Age >= voice.Sample.Duration / MathF.Pow(2, voice.Pitch)) StopAt(index);
        }
    }

    public bool TryPlay(SoundRequest request)
    {
        if (!float.IsFinite(request.Gain) || request.Gain <= 0) return false;
        var family = request.Key.Family;
        var loop = request.HoldSeconds > 0;
        if (loop)
        {
            var activeLoop = _voices.FirstOrDefault(voice => voice.IsLooped && voice.Key.Family == family);
            if (activeLoop is not null)
            {
                // A shared emitter keeps its timbre for the engagement; mixed-tier batteries
                // must not restart its carrier on alternating tower heartbeats.
                activeLoop.HoldRemaining = Math.Clamp(request.HoldSeconds, 0.04f, 0.35f);
                return true;
            }
        }
        if (_cooldowns.TryGetValue(family, out var activeCooldown) && activeCooldown.Priority >= request.Category)
            return false;
        var combat = IsCombat(request.Category);
        var familyLimit = loop ? 1 : combat ? 2 : 3;
        while (_voices.Count(voice => voice.Key.Family == family) >= familyLimit)
        {
            var victim = _voices.FindIndex(voice => voice.Key.Family == family && voice.Category < request.Category);
            if (victim < 0) return false;
            StopAt(victim);
        }

        // Notifications may reclaim routine voices; combat never steals the interface or threat channel.
        var categoryLimit = request.Category == SoundCategory.Detail ? 4 :
            request.Category == SoundCategory.Interface ? 3 : MaximumVoices;
        if (_voices.Count(voice => voice.Category == request.Category) >= categoryLimit) return false;
        while (_voices.Count >= MaximumVoices || combat && _voices.Count(voice => IsCombat(voice.Category)) >= MaximumCombatVoices)
        {
            var victim = _voices.FindIndex(voice => voice.Category < request.Category && !voice.IsLooped);
            if (victim < 0) return false;
            StopAt(victim);
        }

        var variant = _variants.GetValueOrDefault(family, _variation.Next(ProceduralSoundBank.VariantCount));
        var sample = GetSample(request.Key, variant);
        var gain = AudioManager.MasteredOneShotGain(sample.Peak, request.Gain);
        var usedPeak = _voices.Sum(voice => voice.Gain * voice.Sample.Peak);
        var available = TotalPeakBudget - usedPeak;
        if (combat)
            available = MathF.Min(available, CombatPeakBudget - _voices.Where(voice => IsCombat(voice.Category))
                .Sum(voice => voice.Gain * voice.Sample.Peak));
        else if (available < sample.Peak * gain)
        {
            // Reserve audible space for warnings even at the busiest instant.
            for (var index = _voices.Count - 1; index >= 0 && available < sample.Peak * gain; index--)
            {
                if (_voices[index].Category >= request.Category) continue;
                available += _voices[index].Gain * _voices[index].Sample.Peak;
                StopAt(index);
            }
        }
        gain = MathF.Min(gain, MathF.Max(0, available) / MathF.Max(sample.Peak, 0.001f));
        if (gain < request.Gain * 0.28f) return false;
        _variants[family] = (variant + 1 + _variation.Next(ProceduralSoundBank.VariantCount - 1)) % ProceduralSoundBank.VariantCount;
        _cooldowns[family] = (MathF.Max(0, float.IsFinite(request.Cooldown) ? request.Cooldown : 0), request.Category);
        var pitch = float.IsFinite(request.Pitch) ? request.Pitch : 0;
        if (combat && !loop) pitch += (_variation.NextSingle() - 0.5f) * 0.045f;
        var voice = new SoundVoice
        {
            Id = ++_nextId, Key = request.Key, Sample = sample, Category = request.Category,
            Gain = gain, Pitch = Math.Clamp(pitch, -1, 1),
            Pan = Math.Clamp(float.IsFinite(request.Pan) ? request.Pan : 0, -0.45f, 0.45f),
            IsLooped = loop, Envelope = loop ? 0 : 1,
            HoldRemaining = Math.Clamp(request.HoldSeconds, 0, 0.35f)
        };
        _voices.Add(voice);
        Started?.Invoke(voice);
        return true;
    }

    public SoundSample GetSample(SoundKey key, int variant = 0)
    {
        key = key with { LevelIndex = Math.Clamp(key.LevelIndex, 0, 2) };
        variant = Math.Clamp(variant, 0, ProceduralSoundBank.VariantCount - 1);
        if (_samples.TryGetValue((key, variant), out var sample)) return sample;
        var samples = key.WeaponId is not null
            ? ProceduralSoundBank.CreateWeapon(key.WeaponId, key.Layer, key.LevelIndex, key.Apex, variant)
            : ProceduralSoundBank.CreateCue(key.Cue, variant);
        var peak = samples.Max(value => Math.Abs((int)value)) / (float)short.MaxValue;
        sample = new SoundSample(samples, peak);
        _samples[(key, variant)] = sample;
        return sample;
    }

    public void StopCombat()
    {
        for (var index = _voices.Count - 1; index >= 0; index--)
        {
            if (!IsCombat(_voices[index].Category)) continue;
            if (_voices[index].IsLooped) _voices[index].HoldRemaining = MathF.Min(0, _voices[index].HoldRemaining);
            else StopAt(index);
        }
    }

    public void Clear(bool preserveInterface = false)
    {
        for (var index = _voices.Count - 1; index >= 0; index--)
            if (!preserveInterface || _voices[index].Category != SoundCategory.Interface) StopAt(index);
        foreach (var family in _cooldowns.Keys.ToArray())
            if (!preserveInterface || _cooldowns[family].Priority != SoundCategory.Interface) _cooldowns.Remove(family);
    }

    private static bool IsCombat(SoundCategory category) => category is SoundCategory.Combat or SoundCategory.Detail;
    private void StopAt(int index)
    {
        var id = _voices[index].Id;
        _voices.RemoveAt(index);
        Stopped?.Invoke(id);
    }
}
