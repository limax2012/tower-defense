namespace MaximalBastion.Audio;

internal enum SoundCategory { Detail, Combat, Notification, Interface, Critical }
internal enum SoundAdmissionResult { Started, Renewed, Cooldown, FamilyLimit, CategoryLimit, VoiceLimit, PeakBudget, Invalid }

internal readonly record struct SoundKey(string? WeaponId, WeaponSoundLayer Layer = default,
    SoundCue Cue = default, int LevelIndex = 0, bool Apex = false)
{
    public string Family => WeaponId is null ? Cue.ToString() : $"{WeaponId}/{Layer}";
}

internal readonly record struct SoundRequest(SoundKey Key, float Gain,
    SoundCategory Category = SoundCategory.Combat, float Pan = 0, float Pitch = 0,
    float Cooldown = 0.08f, float HoldSeconds = 0);

internal sealed class SoundSample
{
    private const int EnvelopeFrames = ProceduralSoundBank.SampleRate / 100;
    private readonly float[] _remainingPeaks;
    private readonly float[] _powers;
    public short[] Samples { get; }
    public float Peak { get; }
    public float Duration => Samples.Length / (float)ProceduralSoundBank.SampleRate;

    public SoundSample(short[] samples, float peak)
    {
        Samples = samples;
        Peak = peak;
        var bins = (samples.Length + EnvelopeFrames - 1) / EnvelopeFrames;
        _remainingPeaks = new float[bins + 1];
        _powers = new float[bins];
        for (var bin = 0; bin < bins; bin++)
        {
            var end = Math.Min(samples.Length, (bin + 1) * EnvelopeFrames);
            double power = 0;
            for (var frame = bin * EnvelopeFrames; frame < end; frame++)
            {
                var value = samples[frame] / (float)short.MaxValue;
                _remainingPeaks[bin] = MathF.Max(_remainingPeaks[bin], MathF.Abs(value));
                power += value * value;
            }
            _powers[bin] = (float)(power / (end - bin * EnvelopeFrames));
        }
        // A quiet gap cannot release headroom needed by a later transient.
        for (var bin = bins - 2; bin >= 0; bin--)
            _remainingPeaks[bin] = MathF.Max(_remainingPeaks[bin], _remainingPeaks[bin + 1]);
    }

    public float RemainingPeak(float sourceSeconds)
    {
        var bin = (int)(MathF.Max(0, sourceSeconds) * ProceduralSoundBank.SampleRate / EnvelopeFrames);
        return bin < _powers.Length ? _remainingPeaks[bin] : 0;
    }

    public float LocalPower(float sourceSeconds)
    {
        var bin = (int)(MathF.Max(0, sourceSeconds) * ProceduralSoundBank.SampleRate / EnvelopeFrames);
        if (bin >= _powers.Length) return 0;
        var first = Math.Max(0, bin - 1);
        var end = Math.Min(_powers.Length, bin + 2);
        float power = 0;
        for (var index = first; index < end; index++) power += _powers[index];
        return power / (end - first);
    }
}

internal sealed class SoundVoice
{
    public required int Id { get; init; }
    public required SoundKey Key { get; init; }
    public required SoundSample Sample { get; init; }
    public required SoundCategory Category { get; init; }
    public required float Gain { get; init; }
    public required float RequestedGain { get; init; }
    public required float Pitch { get; init; }
    public required float Pan { get; init; }
    public required bool IsLooped { get; init; }
    public float Age { get; internal set; }
    public float Envelope { get; internal set; }
    public float OutputGain => Gain * Envelope * MixGain;
    // Allow 25 ms of playback scheduling slack before releasing headroom.
    public float ReservedPeak => Gain * (IsLooped ? Sample.Peak :
        Sample.RemainingPeak(MathF.Max(0, Age - .025f) * MathF.Pow(2, Pitch)));
    internal float MixGain { get; set; } = 1;
    internal float SourcePower => Sample.LocalPower(IsLooped
        ? Age * MathF.Pow(2, Pitch) % Sample.Duration : Age * MathF.Pow(2, Pitch));
    internal float HoldRemaining { get; set; }
}

/// <summary>Real-time voice admission and loop envelopes, independent of the audio device and simulation clock.</summary>
internal sealed class SoundMixer
{
    internal const int MaximumVoices = 20;
    internal const int MaximumCombatVoices = 12;
    internal const float CombatPeakBudget = 0.36f;
    internal const float TotalPeakBudget = 0.58f;
    internal const float MinimumCombatMixGain = 0.7943282f;
    internal const float MinimumCombatAdmissionRatio = 0.75f;
    private const float CombatRmsStart = .035f;
    private const float CombatRmsFull = .075f;
    private const float MixAttackSeconds = .12f;
    private const float MixReleaseSeconds = .60f;
    private readonly List<SoundVoice> _voices = [];
    private readonly Dictionary<(SoundKey Key, int Variant), SoundSample> _samples = [];
    private readonly Dictionary<string, (float Remaining, SoundCategory Priority)> _cooldowns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _variants = new(StringComparer.Ordinal);
    private readonly Random _variation;
    private int _nextId;

    public SoundMixer(int seed = 7817) => _variation = new Random(seed);
    public IReadOnlyList<SoundVoice> Voices => _voices;
    public float CombatMixGain { get; private set; } = 1;
    public float ReservedPeak => _voices.Sum(voice => voice.ReservedPeak);
    public float CombatReservedPeak => _voices.Where(voice => IsCombat(voice.Category)).Sum(voice => voice.ReservedPeak);
    public event Action<SoundVoice>? Started;
    public event Action<int>? Stopped;
    public event Action<SoundRequest, SoundAdmissionResult>? Evaluated;
    public event Action<SoundVoice, bool>? Retired;

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
        var power = _voices.Where(voice => IsCombat(voice.Category))
            .Sum(voice => voice.SourcePower * voice.Gain * voice.Gain * voice.Envelope * voice.Envelope);
        var load = Math.Clamp((MathF.Sqrt(power) - CombatRmsStart) / (CombatRmsFull - CombatRmsStart), 0, 1);
        var target = 1 - (1 - MinimumCombatMixGain) * load;
        var seconds = target < CombatMixGain ? MixAttackSeconds : MixReleaseSeconds;
        CombatMixGain += (target - CombatMixGain) * (1 - MathF.Exp(-elapsed / seconds));
        foreach (var voice in _voices)
            voice.MixGain = IsCombat(voice.Category) ? CombatMixGain : 1;
    }

    public bool TryPlay(SoundRequest request)
    {
        var result = Admit(request);
        Evaluated?.Invoke(request, result);
        return result is SoundAdmissionResult.Started or SoundAdmissionResult.Renewed;
    }

    private SoundAdmissionResult Admit(SoundRequest request)
    {
        if (!float.IsFinite(request.Gain) || request.Gain <= 0) return SoundAdmissionResult.Invalid;
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
                return SoundAdmissionResult.Renewed;
            }
        }
        if (_cooldowns.TryGetValue(family, out var activeCooldown) && activeCooldown.Priority >= request.Category)
            return SoundAdmissionResult.Cooldown;
        var combat = IsCombat(request.Category);
        var familyLimit = loop ? 1 : combat ? 2 : 3;
        var candidates = new List<SoundVoice>(_voices);
        var victims = new List<SoundVoice>();
        while (candidates.Count(voice => voice.Key.Family == family) >= familyLimit)
        {
            if (!Reclaim(family)) return SoundAdmissionResult.FamilyLimit;
        }

        // Notifications may reclaim routine voices; combat never steals the interface or threat channel.
        var categoryLimit = request.Category == SoundCategory.Detail ? 4 :
            request.Category == SoundCategory.Interface ? 3 : MaximumVoices;
        if (candidates.Count(voice => voice.Category == request.Category) >= categoryLimit)
            return SoundAdmissionResult.CategoryLimit;
        while (candidates.Count >= MaximumVoices || combat && candidates.Count(voice => IsCombat(voice.Category)) >= MaximumCombatVoices)
        {
            if (!Reclaim(excludeLoops: true)) return SoundAdmissionResult.VoiceLimit;
        }

        var variant = _variants.GetValueOrDefault(family, _variation.Next(ProceduralSoundBank.VariantCount));
        var sample = GetSample(request.Key, variant);
        var gain = AudioManager.MasteredOneShotGain(sample.Peak, request.Gain);
        var minimumGain = combat ? gain * MinimumCombatAdmissionRatio : request.Gain * .28f;
        var usedPeak = candidates.Sum(voice => voice.ReservedPeak);
        var available = TotalPeakBudget - usedPeak;
        if (combat)
            available = MathF.Min(available, CombatPeakBudget - candidates.Where(voice => IsCombat(voice.Category))
                .Sum(voice => voice.ReservedPeak));
        else if (available < sample.Peak * gain)
        {
            // Reserve audible space for warnings even at the busiest instant.
            while (available < sample.Peak * gain)
            {
                if (!Reclaim()) break;
                available = TotalPeakBudget - candidates.Sum(voice => voice.ReservedPeak);
            }
        }
        gain = MathF.Min(gain, MathF.Max(0, available) / MathF.Max(sample.Peak, 0.001f));
        if (gain < minimumGain)
            return SoundAdmissionResult.PeakBudget;
        // Commit replacement only after the new voice has passed every admission check.
        foreach (var victim in victims) StopAt(_voices.IndexOf(victim), preempted: true);
        _variants[family] = (variant + 1 + _variation.Next(ProceduralSoundBank.VariantCount - 1)) % ProceduralSoundBank.VariantCount;
        _cooldowns[family] = (MathF.Max(0, float.IsFinite(request.Cooldown) ? request.Cooldown : 0), request.Category);
        var pitch = float.IsFinite(request.Pitch) ? request.Pitch : 0;
        if (combat && !loop && (request.Key.WeaponId is not null || request.Key.Cue != SoundCue.Protocol))
            pitch += (_variation.NextSingle() - 0.5f) * 0.045f;
        var voice = new SoundVoice
        {
            Id = ++_nextId, Key = request.Key, Sample = sample, Category = request.Category,
            Gain = gain, RequestedGain = request.Gain, Pitch = Math.Clamp(pitch, -1, 1),
            Pan = Math.Clamp(float.IsFinite(request.Pan) ? request.Pan : 0, -0.45f, 0.45f),
            IsLooped = loop, Envelope = loop ? 0 : 1,
            MixGain = combat ? CombatMixGain : 1,
            HoldRemaining = Math.Clamp(request.HoldSeconds, 0, 0.35f)
        };
        _voices.Add(voice);
        Started?.Invoke(voice);
        return SoundAdmissionResult.Started;

        bool Reclaim(string? onlyFamily = null, bool excludeLoops = false)
        {
            var victim = candidates.Where(voice => voice.Category < request.Category &&
                    (onlyFamily is null || voice.Key.Family == onlyFamily) && (!excludeLoops || !voice.IsLooped))
                .OrderBy(voice => voice.Category).ThenBy(voice => voice.ReservedPeak)
                .ThenByDescending(voice => voice.Age).FirstOrDefault();
            if (victim is null) return false;
            candidates.Remove(victim);
            victims.Add(victim);
            return true;
        }
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
        CombatMixGain = 1;
    }

    private static bool IsCombat(SoundCategory category) => category is SoundCategory.Combat or SoundCategory.Detail;
    private void StopAt(int index, bool preempted = false)
    {
        var voice = _voices[index];
        _voices.RemoveAt(index);
        Stopped?.Invoke(voice.Id);
        Retired?.Invoke(voice, preempted);
    }
}
