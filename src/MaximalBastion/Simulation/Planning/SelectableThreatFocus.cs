using MaximalBastion.Core;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record ThreatFocusRule
{
    public EnemySignalRole? SignalRole { get; init; }
    public EnemyRank? Rank { get; init; }
    public int Priority { get; init; } = 100;
    public IReadOnlyList<string> Behaviors { get; init; } = [];
    public float MinimumProgress { get; init; }
    public float MaximumProgress { get; init; } = 1;
    public int MaximumActiveEnemies { get; init; } = 10_000;

    public void Validate()
    {
        if (SignalRole is null && Rank is null ||
            SignalRole is { } role && !Enum.IsDefined(role) || Rank is { } rank && !Enum.IsDefined(rank) ||
            Priority is < 1 or > 100_000 || MaximumActiveEnemies is < 1 or > 100_000 ||
            !float.IsFinite(MinimumProgress) || !float.IsFinite(MaximumProgress) ||
            MinimumProgress < 0 || MaximumProgress > 1 || MinimumProgress > MaximumProgress ||
            !ThreatFocusPolicy.ValidBehaviors(Behaviors))
            throw new ArgumentException("Invalid threat focus rule.");
    }
}

public sealed record ThreatFocusPolicy
{
    public IReadOnlyList<ThreatFocusRule> Rules { get; init; } = [];
    public IReadOnlyList<string> ExcludedBehaviors { get; init; } = ["slow_projectile"];
    public int ReadyWithinTicks { get; init; } = 1;

    public void Validate()
    {
        if (Rules is null || Rules.Count is < 1 or > 32 || Rules.Any(rule => rule is null) ||
            !ValidBehaviors(ExcludedBehaviors) || ReadyWithinTicks is < 1 or > 3600)
            throw new ArgumentException("Invalid threat focus policy.");
        foreach (var rule in Rules) rule.Validate();
    }

    internal static bool ValidBehaviors(IReadOnlyList<string>? behaviors) => behaviors is { Count: <= 64 } &&
        behaviors.All(behavior => !string.IsNullOrWhiteSpace(behavior) && behavior.Length <= 80) &&
        behaviors.Distinct(StringComparer.Ordinal).Count() == behaviors.Count;
}

/// <summary>Chooses legal target modes that expose a priority enemy to a ready weapon.</summary>
public static class SelectableThreatFocus
{
    private static readonly TargetMode[] Modes = Enum.GetValues<TargetMode>();

    public static int Apply(PlannerSession adapter, ThreatFocusPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        var session = adapter.Session;
        var liveCount = session.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped);
        if (liveCount == 0) return 0;
        var orders = 0;
        foreach (var tower in session.Towers)
        {
            var behavior = tower.Definition.Behavior;
            if (tower.IsSupport || tower.IsDisrupted || tower.CooldownRemaining > policy.ReadyWithinTicks / 60f ||
                policy.ExcludedBehaviors.Contains(behavior, StringComparer.Ordinal)) continue;
            var rules = policy.Rules.Where(rule => liveCount <= rule.MaximumActiveEnemies &&
                (rule.Behaviors.Count == 0 || rule.Behaviors.Contains(behavior, StringComparer.Ordinal))).ToArray();
            if (rules.Length == 0) continue;
            var chosen = tower.TargetMode;
            var bestPriority = 0;
            var bestProgress = -1f;
            var range = session.GetEffectiveRange(tower);
            foreach (var mode in Modes)
            {
                var target = session.TargetSelector.Select(tower.Position, range, mode, session.Enemies);
                if (target is null) continue;
                var priority = rules.Where(rule =>
                        (rule.SignalRole is null || target.SignalRole == rule.SignalRole) &&
                        (rule.Rank is null || target.Rank == rule.Rank) &&
                        target.PathProgress >= rule.MinimumProgress && target.PathProgress <= rule.MaximumProgress)
                    .Select(rule => rule.Priority).DefaultIfEmpty(0).Max();
                if (priority == 0 || priority < bestPriority ||
                    priority == bestPriority && target.PathProgress <= bestProgress) continue;
                bestPriority = priority;
                bestProgress = target.PathProgress;
                chosen = mode;
            }
            if (chosen != tower.TargetMode && adapter.Apply(new GameCommand
                { PlayerId = tower.OwnerPlayerId, Type = GameCommandType.SetTargetMode, EntityId = tower.Id, TargetMode = chosen },
                "selectable priority threat focus").Accepted) orders++;
        }
        return orders;
    }
}
