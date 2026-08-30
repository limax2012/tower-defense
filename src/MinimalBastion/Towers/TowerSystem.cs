using MinimalBastion.Combat;

namespace MinimalBastion.Towers;

public sealed class TowerSystem
{
    private readonly Dictionary<string, ITowerBehavior> _behaviors = new(StringComparer.OrdinalIgnoreCase);
    private readonly TargetSelector _targetSelector;

    public TowerSystem(TargetSelector targetSelector) => _targetSelector = targetSelector;

    public void Update(float deltaSeconds, MinimalBastion.GameSession session)
    {
        foreach (var tower in session.Towers)
        {
            tower.TickVisual(deltaSeconds);
            if (tower.IsSandboxDisabled) continue;
            if (tower.IsSupport) continue;
            tower.CooldownRemaining -= deltaSeconds;
            if (tower.IsDisrupted) continue;
            if (tower.CooldownRemaining > 0) continue;
            if (!TryFire(tower, session)) continue;
            var attacksPerSecond = session.GetEffectiveAttacksPerSecond(tower);
            tower.CooldownRemaining = attacksPerSecond <= 0 ? 0.5f : 1f / attacksPerSecond;
        }
    }

    public bool TryFireImmediate(TowerInstance tower, MinimalBastion.GameSession session) =>
        !tower.IsSandboxDisabled && !tower.IsSupport && TryFire(tower, session);

    private bool TryFire(TowerInstance tower, MinimalBastion.GameSession session)
    {
        var target = _targetSelector.Select(tower.Position, session.GetEffectiveRange(tower), tower.TargetMode, session.Enemies);
        if (target is null) return false;
        if (!_behaviors.TryGetValue(tower.Definition.Behavior, out var behavior))
            _behaviors[tower.Definition.Behavior] = behavior = TowerBehaviorRegistry.Create(tower.Definition.Behavior);
        behavior.Attack(new TowerInstanceContext { Tower = tower, Target = target, Session = session });
        tower.OnFired();
        return true;
    }
}
