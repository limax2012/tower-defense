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
            foreach (var recipient in towers)
            {
                if (recipient.Id == tower.Id || recipient.IsSupport) continue;
                if (Vector2.DistanceSquared(tower.Position, recipient.Position) > level.AuraRange * level.AuraRange) continue;
                var current = _buffs.TryGetValue(recipient.Id, out var existing) ? existing : new TowerBuff(0, 0);
                _buffs[recipient.Id] = new TowerBuff(
                    MathF.Max(current.AttackSpeedBonus, level.AuraAttackSpeedBonus),
                    MathF.Max(current.RangeBonus, level.AuraRangeBonus));
            }
        }
    }

    public TowerBuff Get(TowerInstance tower) => _buffs.TryGetValue(tower.Id, out var value) ? value : new TowerBuff(0, 0);
}
