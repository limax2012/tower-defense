using MaximalBastion.Towers;

namespace MaximalBastion.Effects;

/// <summary>
/// Transient weapon identity captured at firing time. It drives local artwork only;
/// damage, collisions, synchronized snapshots and checksums do not depend on it.
/// </summary>
public readonly record struct AttackAppearance(string? TowerId, int Level, bool IsApex)
{
    public bool IsKnown => TowerId is not null && Level > 0;

    public static AttackAppearance FromTower(TowerInstance tower) =>
        new(tower.Definition.Id, tower.LevelIndex + 1, tower.IsApex);

    public AttackAppearance Resolve(GameSession session, int sourceTowerId)
    {
        if (IsKnown) return this;
        var source = session.Towers.FirstOrDefault(tower => tower.Id == sourceTowerId);
        if (source is not null) return FromTower(source);
        return session.Statistics.TowerDefinitionByInstance.TryGetValue(sourceTowerId, out var towerId)
            ? new AttackAppearance(towerId, 1, false)
            : this;
    }
}
