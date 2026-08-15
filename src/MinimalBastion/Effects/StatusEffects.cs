namespace MinimalBastion.Effects;

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
}

public sealed class ActiveStatus
{
    public StatusType Type { get; init; }
    public float RemainingSeconds { get; set; }
    public float Magnitude { get; set; }
    public int SourceId { get; set; }
    public float TickInterval { get; set; } = 0.5f;
    public float TickProgress { get; set; }
}

public readonly record struct BurnTick(float Damage, int SourceId);

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
            }
            else
            {
                if (_statuses.Count(x => x.Type == StatusType.Burn) >= 2)
                {
                    var weakest = _statuses.Where(x => x.Type == StatusType.Burn).OrderBy(x => x.Magnitude).First();
                    _statuses.Remove(weakest);
                }
                _statuses.Add(new ActiveStatus
                {
                    Type = StatusType.Burn,
                    RemainingSeconds = application.Duration,
                    Magnitude = application.Magnitude,
                    SourceId = application.SourceId,
                    TickInterval = MathF.Max(0.05f, application.TickInterval)
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

    public IReadOnlyList<BurnTick> ConsumeBurnTicks(float deltaSeconds)
    {
        if (deltaSeconds <= 0) return Array.Empty<BurnTick>();

        var ticks = new List<BurnTick>();
        foreach (var status in _statuses.Where(x => x.Type == StatusType.Burn))
        {
            var activeSeconds = MathF.Min(deltaSeconds, MathF.Max(0, status.RemainingSeconds));
            status.TickProgress += activeSeconds;
            var interval = MathF.Max(0.05f, status.TickInterval);
            var tickCount = (int)MathF.Floor(status.TickProgress / interval);
            if (tickCount <= 0) continue;
            status.TickProgress -= tickCount * interval;
            for (var index = 0; index < tickCount; index++)
                ticks.Add(new BurnTick(status.Magnitude * interval, status.SourceId));
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
        TickProgress = status.TickProgress
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
                TickProgress = tickProgress
            });
        }
    }
}
