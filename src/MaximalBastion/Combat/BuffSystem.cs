using MaximalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Combat;

public sealed class BuffSystem
{
    private readonly Dictionary<int, TowerBuff> _buffs = new();

    public void Update(IReadOnlyList<TowerInstance> towers)
    {
        _buffs.Clear();
        foreach (var tower in towers)
        {
            if (!tower.IsSupport || tower.IsSandboxDisabled || tower.IsDisrupted) continue;
            var auraRange = tower.EffectiveAuraRange;
            var attackSpeedBonus = tower.EffectiveAuraAttackSpeedBonus;
            var rangeBonus = tower.EffectiveAuraTowerRangeBonus;
            foreach (var recipient in towers)
            {
                if (!CanReachRecipient(tower, recipient, auraRange)) continue;
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

    // Presentation checks live coverage without changing the simulation's cached bonuses.
    public bool HasLiveSupport(TowerInstance recipient, IReadOnlyList<TowerInstance> towers)
    {
        if (!Get(recipient).IsActive) return false;
        foreach (var source in towers)
        {
            if (!source.IsSupport || source.IsSandboxDisabled || source.IsDisrupted) continue;
            if (source.EffectiveAuraAttackSpeedBonus <= 0 && source.EffectiveAuraTowerRangeBonus <= 0) continue;
            if (CanReachRecipient(source, recipient, source.EffectiveAuraRange)) return true;
        }
        return false;
    }

    private static bool CanReachRecipient(TowerInstance source, TowerInstance recipient, float auraRange) =>
        recipient.Id != source.Id && !recipient.IsSupport && !recipient.IsSandboxDisabled && !recipient.IsDisrupted &&
        Vector2.DistanceSquared(source.Position, recipient.Position) <= auraRange * auraRange;
}
