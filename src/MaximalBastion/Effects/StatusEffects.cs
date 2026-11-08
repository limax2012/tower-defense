namespace MaximalBastion.Effects;

public enum StatusType
{
    Slow,
    Burn,
    ArmorBreak,
    Exposed,
    Stun
}

public sealed class StatusApplication
{
    public StatusType Type { get; init; }
    public float Duration { get; init; }
    public float Magnitude { get; init; }
    public int SourceId { get; init; }
    public float TickInterval { get; init; } = 0.5f;
    public float ArmorPierce { get; init; }
}

public sealed class ActiveStatus
{
    public StatusType Type { get; init; }
    public float RemainingSeconds { get; set; }
    public float Magnitude { get; set; }
    public int SourceId { get; set; }
    public float TickInterval { get; set; } = 0.5f;
    public float TickProgress { get; set; }
    public float ArmorPierce { get; set; }
}

public readonly record struct BurnTick(float Damage, int SourceId, float ArmorPierce);

public sealed class StatusEffectController
{
    private readonly List<ActiveStatus> _statuses = new();
    public IReadOnlyList<ActiveStatus> Active => _statuses;

    public float SlowFactor => _statuses.Where(x => x.Type == StatusType.Slow).Select(x => x.Magnitude).DefaultIfEmpty(0).Max();
    public float ArmorReduction => _statuses.Where(x => x.Type == StatusType.ArmorBreak).Select(x => x.Magnitude).DefaultIfEmpty(0).Max();
    public float DamageMultiplier => 1f + _statuses.Where(x => x.Type == StatusType.Exposed).Select(x => x.Magnitude).DefaultIfEmpty(0).Max();
    public bool IsStunned => _statuses.Any(x => x.Type == StatusType.Stun);
    public bool IsBurning => _statuses.Any(x => x.Type == StatusType.Burn);

    public void Apply(StatusApplication application)
    {
        if (application.Duration <= 0 || application.Magnitude <= 0) return;
        if (application.Type == StatusType.Burn)
        {
            var sameSource = _statuses.FirstOrDefault(x => x.Type == StatusType.Burn && x.SourceId == application.SourceId);
            if (sameSource is not null)
            {
                sameSource.RemainingSeconds = MathF.Max(sameSource.RemainingSeconds, application.Duration);
                sameSource.Magnitude = application.Magnitude;
                sameSource.TickInterval = MathF.Max(0.05f, application.TickInterval);
                sameSource.ArmorPierce = MathF.Max(0, application.ArmorPierce);
            }
            else
            {
                _statuses.Add(new ActiveStatus
                {
                    Type = StatusType.Burn,
                    RemainingSeconds = application.Duration,
                    Magnitude = application.Magnitude,
                    SourceId = application.SourceId,
                    TickInterval = MathF.Max(0.05f, application.TickInterval),
                    ArmorPierce = MathF.Max(0, application.ArmorPierce)
                });
            }
            return;
        }

        var existing = _statuses.FirstOrDefault(x => x.Type == application.Type);
        if (existing is null)
        {
            _statuses.Add(new ActiveStatus { Type = application.Type, RemainingSeconds = application.Duration, Magnitude = application.Magnitude, SourceId = application.SourceId });
            return;
        }

        if (application.Magnitude > existing.Magnitude ||
            application.Magnitude == existing.Magnitude && application.Duration >= existing.RemainingSeconds)
        {
            existing.Magnitude = application.Magnitude;
            existing.SourceId = application.SourceId;
        }
        existing.RemainingSeconds = MathF.Max(existing.RemainingSeconds, application.Duration);
    }

    public float ConsumeBurnDamage(float deltaSeconds) => ConsumeBurnTicks(deltaSeconds).Sum(x => x.Damage);

    public IReadOnlyList<ActiveStatus> GetActiveBurns(float effectiveArmor = 0)
    {
        var (first, second) = SelectBurns(effectiveArmor);
        if (first is null) return Array.Empty<ActiveStatus>();
        return second is null ? new[] { first } : new[] { first, second };
    }

    public static float ArmorAdjustedBurnDamagePerSecond(ActiveStatus burn, float effectiveArmor = 0)
    {
        var interval = MathF.Max(0.05f, burn.TickInterval);
        return MathF.Max(0, burn.Magnitude - MathF.Max(0, effectiveArmor - burn.ArmorPierce) / interval);
    }

    public float GetExpectedBurnDamagePerSecond(ActiveStatus burn, float effectiveArmor = 0, float shield = 0) =>
        ExpectedBurnDamagePerSecond(burn, effectiveArmor, shield, DamageMultiplier);

    private static float ExpectedBurnDamagePerSecond(ActiveStatus burn, float effectiveArmor, float shield,
        float damageMultiplier)
    {
        // Estimate one source's full tick for inspection, independently of source selection.
        var interval = MathF.Max(0.05f, burn.TickInterval);
        var damage = burn.Magnitude * interval * damageMultiplier;
        var shieldDamage = MathF.Min(damage, MathF.Max(0, shield));
        var healthDamage = MathF.Max(0, damage - shieldDamage - MathF.Max(0, effectiveArmor - burn.ArmorPierce));
        return (shieldDamage + healthDamage) / interval;
    }

    private (ActiveStatus? First, ActiveStatus? Second) SelectBurns(float effectiveArmor)
    {
        ActiveStatus? first = null, second = null;
        var firstScore = float.NegativeInfinity;
        var secondScore = float.NegativeInfinity;
        foreach (var burn in _statuses)
        {
            if (burn.Type != StatusType.Burn || burn.RemainingSeconds <= 0) continue;
            var score = ArmorAdjustedBurnDamagePerSecond(burn, effectiveArmor);
            if (RanksBefore(burn, score, first, firstScore))
            {
                second = first;
                secondScore = firstScore;
                first = burn;
                firstScore = score;
            }
            else if (RanksBefore(burn, score, second, secondScore))
            {
                second = burn;
                secondScore = score;
            }
        }
        return (first, second);
    }

    private static bool RanksBefore(ActiveStatus burn, float score, ActiveStatus? other, float otherScore) =>
        other is null || score > otherScore || score == otherScore &&
        (burn.Magnitude > other.Magnitude || burn.Magnitude == other.Magnitude && burn.SourceId < other.SourceId);

    public IReadOnlyList<BurnTick> ConsumeBurnTicks(float deltaSeconds, float effectiveArmor = 0)
    {
        if (deltaSeconds <= 0) return Array.Empty<BurnTick>();

        var (first, second) = SelectBurns(effectiveArmor);
        if (first is null) return Array.Empty<BurnTick>();
        var ticks = new List<BurnTick>();
        foreach (var status in _statuses)
        {
            // Inactive sources retain earned tick progress, but never accrue deferred damage.
            if (!ReferenceEquals(status, first) && !ReferenceEquals(status, second)) continue;
            var activeSeconds = MathF.Min(deltaSeconds, MathF.Max(0, status.RemainingSeconds));
            status.TickProgress += activeSeconds;
            var interval = MathF.Max(0.05f, status.TickInterval);
            var tickCount = (int)MathF.Floor(status.TickProgress / interval);
            if (tickCount <= 0) continue;
            status.TickProgress -= tickCount * interval;
            for (var index = 0; index < tickCount; index++)
                ticks.Add(new BurnTick(status.Magnitude * interval, status.SourceId, status.ArmorPierce));
        }
        return ticks;
    }

    public void Update(float deltaSeconds)
    {
        for (var i = _statuses.Count - 1; i >= 0; i--)
        {
            _statuses[i].RemainingSeconds -= deltaSeconds;
            if (_statuses[i].RemainingSeconds <= 0) _statuses.RemoveAt(i);
        }
    }

    public List<ActiveStatus> CaptureState() => _statuses.Select(status => new ActiveStatus
    {
        Type = status.Type,
        RemainingSeconds = status.RemainingSeconds,
        Magnitude = status.Magnitude,
        SourceId = status.SourceId,
        TickInterval = status.TickInterval,
        TickProgress = status.TickProgress,
        ArmorPierce = status.ArmorPierce
    }).ToList();

    public void RestoreState(IEnumerable<ActiveStatus> statuses)
    {
        _statuses.Clear();
        foreach (var status in statuses.Where(status => status.RemainingSeconds > 0 && status.Magnitude > 0))
        {
            var tickInterval = float.IsFinite(status.TickInterval) ? MathF.Max(0.05f, status.TickInterval) : 0.5f;
            var tickProgress = float.IsFinite(status.TickProgress)
                ? Math.Clamp(status.TickProgress, 0, tickInterval)
                : 0;
            _statuses.Add(new ActiveStatus
            {
                Type = status.Type,
                RemainingSeconds = status.RemainingSeconds,
                Magnitude = status.Magnitude,
                SourceId = status.SourceId,
                TickInterval = tickInterval,
                TickProgress = tickProgress,
                ArmorPierce = MathF.Max(0, status.ArmorPierce)
            });
        }
    }
}
