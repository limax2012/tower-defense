using MaximalBastion.Multiplayer;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Simulation.Planning;

public sealed record CoverageSalvageOptions
{
    public int MaximumSales { get; init; }
    public int ReserveCredits { get; init; } = 350;
    public float RangeSafetyMultiplier { get; init; } = 1.5f;
    public float PathSafetyMargin { get; init; } = 64;
    public float SampleSpacing { get; init; } = 5;

    public void Validate()
    {
        if (MaximumSales is < 0 or > 256 || ReserveCredits < 0 ||
            !float.IsFinite(RangeSafetyMultiplier) || RangeSafetyMultiplier is < 1 or > 4 ||
            !float.IsFinite(PathSafetyMargin) || PathSafetyMargin is < 0 or > 1000 ||
            !float.IsFinite(SampleSpacing) || SampleSpacing is < 1 or > 25)
            throw new ArgumentException("Invalid coverage salvage options.");
    }
}

public sealed record CoverageSalvagePlan(IReadOnlyDictionary<int, float> LastCoveredDistance,
    IReadOnlyDictionary<int, float> PreparedRadius);

/// <summary>Reclaims towers only after queued spawns end and every remaining enemy passes their conservative coverage.</summary>
public static class CoverageSalvage
{
    public static CoverageSalvagePlan Prepare(GameSession session, CoverageSalvageOptions options,
        IReadOnlySet<int>? protectedTowerIds = null)
    {
        options.Validate();
        var distances = new Dictionary<int, float>();
        var radii = new Dictionary<int, float>();
        if (options.MaximumSales == 0) return new(distances, radii);
        foreach (var tower in session.Towers.Where(tower => !tower.IsSupport &&
                     (protectedTowerIds is null || !protectedTowerIds.Contains(tower.Id))))
        {
            var radius = session.GetEffectiveRange(tower) * options.RangeSafetyMultiplier;
            var last = -1f;
            for (var distance = 0f; distance <= session.Map.Path.TotalLength; distance += options.SampleSpacing)
                if (Vector2.DistanceSquared(tower.Position, session.Map.Path.GetPosition(distance)) <= radius * radius)
                    last = distance;
            distances[tower.Id] = last;
            radii[tower.Id] = radius;
        }
        return new(distances, radii);
    }

    public static int Apply(PlannerSession adapter, CoverageSalvagePlan plan, CoverageSalvageOptions options)
    {
        options.Validate();
        var session = adapter.Session;
        if (adapter.Scenario.ForbidCombatSales || !session.SellingEnabled || !session.Waves.IsActive || session.Waves.QueuedEnemies != 0 ||
            options.MaximumSales == 0 || session.Economy.Credits >= options.ReserveCredits) return 0;
        var remaining = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
        if (remaining.Length == 0) return 0;
        var minimum = remaining.Min(enemy => enemy.DistanceAlongPath);
        var sales = adapter.Actions.Count(action => action.Accepted && action.DuringWave &&
            action.Wave == session.CurrentWave && action.Command.Type == GameCommandType.SellTower);
        var accepted = 0;
        foreach (var tower in session.Towers.Where(tower => plan.LastCoveredDistance.TryGetValue(tower.Id, out var last) &&
                     plan.PreparedRadius.TryGetValue(tower.Id, out var radius) && session.GetEffectiveRange(tower) <= radius &&
                     last + options.PathSafetyMargin < minimum).OrderByDescending(tower => tower.SellValue).ThenBy(tower => tower.Id).ToArray())
        {
            if (sales >= options.MaximumSales || session.Economy.Credits >= options.ReserveCredits) break;
            if (!adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.SellTower, EntityId = tower.Id },
                    "reclaim passed tower coverage").Accepted) continue;
            sales++; accepted++;
        }
        return accepted;
    }
}
