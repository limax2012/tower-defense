using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Combat;

public sealed class BuffSystem
{
    private readonly Dictionary<int, TowerBuff> _buffs = new();

    public void Update(IReadOnlyList<TowerInstance> towers)
    {
        _buffs.Clear();
        foreach (var tower in towers)
        {
            if (!tower.IsSupport) continue;
            var level = tower.Level;
            var protocol = tower.IsOverdriven ? tower.Protocol : null;
            var auraRange = level.AuraRange * (1f + (protocol?.AuraRangeBonus ?? 0));
            var attackSpeedBonus = level.AuraAttackSpeedBonus + (protocol?.AuraAttackSpeedBonus ?? 0);
            var rangeBonus = level.AuraRangeBonus + (protocol?.AuraRangeBonus ?? 0);
            foreach (var recipient in towers)
            {
                if (recipient.Id == tower.Id || recipient.IsSupport) continue;
                if (Vector2.DistanceSquared(tower.Position, recipient.Position) > auraRange * auraRange) continue;
                var current = _buffs.TryGetValue(recipient.Id, out var existing) ? existing : new TowerBuff(0, 0);
                _buffs[recipient.Id] = new TowerBuff(
                    MathF.Max(current.AttackSpeedBonus, attackSpeedBonus),
                    MathF.Max(current.RangeBonus, rangeBonus));
            }
        }
    }

    public TowerBuff Get(TowerInstance tower) => _buffs.TryGetValue(tower.Id, out var value) ? value : new TowerBuff(0, 0);
}
