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
            if (!tower.IsSupport || tower.IsSandboxDisabled) continue;
            var auraRange = tower.EffectiveAuraRange;
            var attackSpeedBonus = tower.EffectiveAuraAttackSpeedBonus;
            var rangeBonus = tower.EffectiveAuraTowerRangeBonus;
            foreach (var recipient in towers)
            {
                if (recipient.Id == tower.Id || recipient.IsSupport || recipient.IsSandboxDisabled) continue;
                if (Vector2.DistanceSquared(tower.Position, recipient.Position) > auraRange * auraRange) continue;
                var current = _buffs.TryGetValue(recipient.Id, out var existing) ? existing : new TowerBuff(0, 0);
                var replacesAttackSpeed = attackSpeedBonus > current.AttackSpeedBonus ||
                    (attackSpeedBonus == current.AttackSpeedBonus && (current.AttackSpeedSourceTowerId == 0 || tower.Id < current.AttackSpeedSourceTowerId));
                var replacesRange = rangeBonus > current.RangeBonus ||
                    (rangeBonus == current.RangeBonus && (current.RangeSourceTowerId == 0 || tower.Id < current.RangeSourceTowerId));
                _buffs[recipient.Id] = new TowerBuff(
                    replacesAttackSpeed ? attackSpeedBonus : current.AttackSpeedBonus,
                    replacesRange ? rangeBonus : current.RangeBonus,
                    replacesAttackSpeed ? tower.Id : current.AttackSpeedSourceTowerId,
                    replacesRange ? tower.Id : current.RangeSourceTowerId);
            }
        }
    }

    public TowerBuff Get(TowerInstance tower) => _buffs.TryGetValue(tower.Id, out var value) ? value : new TowerBuff(0, 0);
}
