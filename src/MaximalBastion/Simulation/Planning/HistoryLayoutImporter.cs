using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Maps;
using MaximalBastion.Persistence;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Simulation.Planning;

public sealed record HistoryLayoutProvenance(string RunId, DateTime CompletedAtUtc, string MapId, string DifficultyId,
    string ChallengeId, bool IsCoOp, bool IsEndless, bool HistoricalVictory, int HistoricalWave, string LayoutSource,
    int SourceWave, DateTime? SaveTimeUtc, IReadOnlyList<int> SourceTowerIds, IReadOnlyList<int> SourceApexTowerIds,
    int AutomaticProtocolTowerId, IReadOnlyDictionary<string, TargetMode> RecordedTargetModes,
    bool SourceHadForge, int SourcePlateCount, int ChosenApexFromWave)
{
    public string GoalOrder => "Surviving tower IDs in ascending order. Historical purchase and upgrade times are not recorded.";
    public string ApexTiming => "The selected wave is the earliest permitted purchase by the planner for towers observed as Apex.";
}

public sealed record HistoryLayoutImport(LayoutBlueprint Layout, LayoutCampaignPlan Plan, HistoryLayoutProvenance Provenance);

/// <summary>Converts recorded tower geometry to purchase goals for a campaign beginning at normal starting resources.</summary>
public static class HistoryLayoutImporter
{
    public static HistoryLayoutImport Convert(GameContent content, RunHistoryEntry entry, int apexFromWave = GameConstants.ApexUnlockWave,
        bool forbidCombatSales = false, SaveGameData? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RunHistoryRepository.ValidateEntries([entry]);
        if (apexFromWave is < GameConstants.ApexUnlockWave or > GameConstants.CampaignWaveCount)
            throw new ArgumentOutOfRangeException(nameof(apexFromWave), "Choose an authored campaign wave at or after Apex unlock.");
        if (!content.Maps.TryGetValue(entry.MapId, out var map))
            map = content.Map.Id == entry.MapId ? content.Map : throw new InvalidDataException($"Unknown history map '{entry.MapId}'.");
        if (!content.Difficulties.ContainsKey(entry.DifficultyId) ||
            !content.Challenges.TryGetValue(entry.ChallengeId, out var challenge) || challenge.IsSandbox)
            throw new InvalidDataException("History requires an available campaign difficulty and competitive directive.");
        if (fallback is not null)
        {
            if (fallback.SchemaVersion != SaveGameData.CurrentSchemaVersion)
                throw new InvalidDataException($"Unsupported fallback save schema {fallback.SchemaVersion}.");
            SaveSlotRepository.ValidateSaveStructure(fallback);
            if (!string.Equals(entry.RunId, fallback.RunId, StringComparison.OrdinalIgnoreCase) ||
                entry.MapId != fallback.MapId || entry.DifficultyId != fallback.DifficultyId ||
                entry.ChallengeId != fallback.ChallengeId || entry.IsCoOp != fallback.IsCoOp ||
                entry.IsEndless != fallback.Waves.EndlessModeEnabled)
                throw new InvalidDataException("Fallback save must match the run ID, map, difficulty, directive, co-op and Endless modes.");
        }
        var recorded = entry.FinalLayout;
        if (recorded is null && fallback is null)
            throw new InvalidDataException($"Run '{entry.RunId}' has no final layout. A matching raw save is required for tower positions.");
        var towers = (recorded?.Towers ?? fallback!.Towers).OrderBy(tower => tower.Id).ToArray();
        var automatic = recorded?.AutoProtocolTowerId ?? fallback!.AutoOverdriveTowerId;
        ValidateGeometry(map, challenge, towers);
        var layout = new LayoutBlueprint
        {
            Id = $"history-{entry.RunId}", MapId = entry.MapId,
            Towers = towers.Select(tower => new LayoutTowerGoal
            {
                Key = Key(tower.Id), TowerId = tower.DefinitionId, X = tower.X, Y = tower.Y,
                LevelIndex = tower.LevelIndex, DoctrineId = tower.DoctrineId, SpecializationId = tower.SpecializationId,
                ApexFromWave = tower.IsApex ? apexFromWave : 0
            }).ToArray(),
            AutomaticProtocolPreference = automatic == 0 ? [] : [Key(automatic)]
        };
        layout.Validate(content);
        var provenance = new HistoryLayoutProvenance(entry.RunId, entry.CompletedAtUtc, entry.MapId, entry.DifficultyId,
            entry.ChallengeId, entry.IsCoOp, entry.IsEndless, entry.Victory, entry.CurrentWave,
            recorded is null ? "matching-raw-save" : "history-final-layout",
            recorded is null ? fallback!.Waves.CurrentWaveNumber : entry.CurrentWave, recorded is null ? fallback!.SavedAtUtc : null,
            towers.Select(t => t.Id).ToArray(), towers.Where(t => t.IsApex).Select(t => t.Id).ToArray(), automatic,
            towers.ToDictionary(t => Key(t.Id), t => t.TargetMode),
            recorded is null ? fallback!.Generator is not null : recorded.Generator is not null,
            recorded?.PulsePlates.Count ?? fallback!.PulsePlates.Count, apexFromWave);
        var plan = new LayoutCampaignPlan
        {
            Id = layout.Id, Layout = layout,
            Scenario = new() { MapId = entry.MapId, DifficultyId = entry.DifficultyId, ChallengeId = entry.ChallengeId,
                CoOp = entry.IsCoOp, ForbidCombatSales = forbidCombatSales },
            Waves = [new() { FirstWave = 1, LastWave = GameConstants.ApexUnlockWave - 1, Policy = StarterPolicy(false) },
                new() { FirstWave = GameConstants.ApexUnlockWave, LastWave = GameConstants.CampaignWaveCount, Policy = StarterPolicy(true) }]
        };
        plan.Validate(content);
        return new(layout, plan, provenance);
    }

    private static string Key(int id) => $"goal-{id}";

    private static TacticalPolicy StarterPolicy(bool late) => new()
    {
        Id = late ? "split-control" : "first", TargetingProfileId = late ? "split" : "first",
        DecisionTicks = late ? 15 : 60, SpendingDecisionTicks = late ? 15 : 60,
        DirectPlateLimit = 0, PrepareBeforeStart = true, ReactAtBoundary = false,
        BehaviorTargetModes = late ? new Dictionary<string, TargetMode> { ["slow_projectile"] = TargetMode.Fastest }
            : new Dictionary<string, TargetMode>()
    };

    private static void ValidateGeometry(MapDefinition map, ChallengeDefinition challenge, IReadOnlyList<TowerSaveData> towers)
    {
        var runtime = new MapRuntime(map);
        var positions = new List<Vector2>();
        foreach (var tower in towers)
        {
            var position = new Vector2(tower.X, tower.Y);
            if (tower.LevelIndex == 0 && tower.DoctrineId is not null)
                throw new InvalidDataException($"Tower {tower.Id} has a doctrine before its tier-two upgrade.");
            if (challenge.ExcludedTowerIds.Contains(tower.DefinitionId, StringComparer.OrdinalIgnoreCase) ||
                position.X < GameConstants.TowerRadius || position.X > GameConstants.MapWidth - GameConstants.TowerRadius ||
                position.Y < GameConstants.TopBarHeight + GameConstants.TowerRadius || position.Y > GameConstants.LogicalHeight - GameConstants.TowerRadius ||
                !runtime.IsBuildable(position) || runtime.Path.DistanceToPath(position) < GameConstants.PlacementPathClearance ||
                positions.Any(other => Vector2.DistanceSquared(position, other) < GameConstants.TowerMinimumGap * GameConstants.TowerMinimumGap))
                throw new InvalidDataException($"Tower {tower.Id} ({tower.DefinitionId}) is not a legal build goal on the current map and directive.");
            positions.Add(position);
        }
    }
}
