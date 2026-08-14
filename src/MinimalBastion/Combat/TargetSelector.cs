using MinimalBastion.Core;
using MinimalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Combat;

public sealed class TargetSelector
{
    public EnemyInstance? Select(Vector2 origin, float range, TargetMode mode, IReadOnlyList<EnemyInstance> enemies)
    {
        var rangeSquared = range * range;
        var eligible = enemies.Where(x => !x.IsDead && !x.HasEscaped && Vector2.DistanceSquared(origin, x.Position) <= rangeSquared).ToList();
        if (eligible.Count == 0) return null;

        return mode switch
        {
            TargetMode.First => eligible.OrderByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Last => eligible.OrderBy(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Strongest => eligible.OrderByDescending(x => x.Health + x.Shield).ThenByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Weakest => eligible.OrderBy(x => x.Health / x.MaxHealth).ThenByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Nearest => eligible.OrderBy(x => Vector2.DistanceSquared(origin, x.Position)).ThenByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Fastest => eligible.OrderByDescending(x => x.CurrentSpeed).ThenByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            TargetMode.Armored => eligible.OrderByDescending(x => x.EffectiveArmor).ThenByDescending(x => x.Health + x.Shield).ThenByDescending(x => x.PathProgress).ThenBy(x => x.Id).First(),
            _ => eligible[0]
        };
    }

    public IReadOnlyList<EnemyInstance> SelectSpreadTargets(Vector2 origin, float range, EnemyInstance primary, int count, IReadOnlyList<EnemyInstance> enemies)
    {
        if (count <= 0) return Array.Empty<EnemyInstance>();
        var rangeSquared = range * range;
        var eligible = enemies
            .Where(x => !x.IsDead && !x.HasEscaped && Vector2.DistanceSquared(origin, x.Position) <= rangeSquared)
            .OrderBy(x => x.Id == primary.Id ? 0 : 1)
            .ThenBy(x => Vector2.DistanceSquared(primary.Position, x.Position))
            .ThenByDescending(x => x.PathProgress)
            .ThenBy(x => x.Id)
            .Take(count)
            .ToArray();
        return eligible.Length > 0 ? eligible : new[] { primary };
    }

    public IEnumerable<EnemyInstance> SelectChainTargets(Vector2 origin, float range, int count, IReadOnlyList<EnemyInstance> enemies, ISet<int> excluded)
    {
        if (count <= 0) yield break;

        var blocked = new HashSet<int>(excluded);
        var previous = origin;
        for (var index = 0; index < count; index++)
        {
            var next = SelectNearestChainTarget(previous, range, enemies, blocked);
            if (next is null) yield break;
            blocked.Add(next.Id);
            yield return next;
            previous = next.Position;
        }
    }

    private static EnemyInstance? SelectNearestChainTarget(Vector2 origin, float range, IReadOnlyList<EnemyInstance> enemies, ISet<int> excluded)
    {
        var rangeSquared = range * range;
        return enemies
            .Where(x => !x.IsDead && !x.HasEscaped && !excluded.Contains(x.Id) && Vector2.DistanceSquared(origin, x.Position) <= rangeSquared)
            .OrderBy(x => Vector2.DistanceSquared(origin, x.Position))
            .ThenByDescending(x => x.PathProgress)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
    }
}
