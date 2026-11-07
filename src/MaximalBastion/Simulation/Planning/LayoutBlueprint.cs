using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Towers;
using System.Text.Json.Serialization;

namespace MaximalBastion.Simulation.Planning;

public sealed record LayoutTowerGoal
{
    public required string Key { get; init; }
    public required string TowerId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int LevelIndex { get; init; }
    public string? DoctrineId { get; init; }
    public string? SpecializationId { get; init; }
    public int ApexFromWave { get; init; }
}

public sealed record LayoutGeneratorGoal
{
    public float X { get; init; }
    public float Y { get; init; }
    public int LevelIndex { get; init; }
    public int FromWave { get; init; } = 1;
}

public sealed record LayoutBlueprint
{
    public string Id { get; init; } = "layout";
    public string? MapId { get; init; }
    public IReadOnlyList<LayoutTowerGoal> Towers { get; init; } = [];
    public IReadOnlyList<string> AutomaticProtocolPreference { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LayoutGeneratorGoal? Generator { get; init; }

    public void Validate(GameContent content)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 || Towers is null || Towers.Count is < 1 or > 256 ||
            AutomaticProtocolPreference is null || Towers.Any(goal => goal is null))
            throw new InvalidDataException("Invalid layout blueprint.");
        if (Generator is { } generator && (!float.IsFinite(generator.X) || !float.IsFinite(generator.Y) ||
            generator.LevelIndex < 0 || generator.LevelIndex >= content.Tactics.Generator.Levels.Count || generator.FromWave < 1))
            throw new InvalidDataException("Invalid Charge Forge goal in layout blueprint.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var positions = new List<(float X, float Y)>();
        foreach (var goal in Towers)
        {
            if (string.IsNullOrWhiteSpace(goal.Key) || goal.Key.Length > 128 || !keys.Add(goal.Key) ||
                !float.IsFinite(goal.X) || !float.IsFinite(goal.Y) || goal.TowerId is null ||
                !content.Towers.TryGetValue(goal.TowerId, out var definition) ||
                goal.LevelIndex < 0 || goal.LevelIndex >= definition.Levels.Count || goal.ApexFromWave < 0)
                throw new InvalidDataException("Invalid tower goal in layout blueprint.");
            if (positions.Any(position => Math.Abs(position.X - goal.X) < 0.01f && Math.Abs(position.Y - goal.Y) < 0.01f))
                throw new InvalidDataException("Layout goals must have distinct positions.");
            positions.Add((goal.X, goal.Y));
            if (goal.DoctrineId is not null && !definition.Tier2Doctrines.Any(doctrine => doctrine.Id == goal.DoctrineId) ||
                goal.SpecializationId is not null && (goal.LevelIndex != definition.Levels.Count - 1 ||
                    !definition.Specializations.Any(specialization => specialization.Id == goal.SpecializationId)))
                throw new InvalidDataException($"Layout goal {goal.Key} has invalid progression choices.");
            if (goal.LevelIndex > 0 && definition.Tier2Doctrines.Count > 0 &&
                !definition.Tier2Doctrines.Any(doctrine => doctrine.Id == goal.DoctrineId))
                throw new InvalidDataException($"Layout goal {goal.Key} requires a valid doctrine.");
            if (goal.LevelIndex == definition.Levels.Count - 1 && definition.Specializations.Count > 0 &&
                !definition.Specializations.Any(specialization => specialization.Id == goal.SpecializationId))
                throw new InvalidDataException($"Layout goal {goal.Key} requires a valid specialization.");
            if (goal.ApexFromWave > 0 && (definition.Apex is null || goal.LevelIndex != definition.Levels.Count - 1))
                throw new InvalidDataException($"Layout goal {goal.Key} cannot reach Apex.");
        }
        if (AutomaticProtocolPreference.Any(key => !keys.Contains(key)))
            throw new InvalidDataException("Automatic Protocol preference references an unknown layout goal.");
    }
}

public sealed record LayoutAllocationOptions
{
    public int ReserveCredits { get; init; }
    public int MaximumCommands { get; init; } = 256;
    public float PositionTolerance { get; init; } = 0.01f;
    public bool AllowApexUpgrades { get; init; } = true;

    public void Validate()
    {
        if (ReserveCredits < 0 || MaximumCommands is < 1 or > 4096 ||
            !float.IsFinite(PositionTolerance) || PositionTolerance is <= 0 or > 4)
            throw new ArgumentException("Invalid layout allocation options.");
    }
}

public sealed record LayoutAllocationResult(int AcceptedCommands, int CompletedGoals, string? BlockedGoal,
    string? Rejection);

/// <summary>Purchases an ordered goal layout through the same validated commands used during play.</summary>
public static class LayoutBlueprintSpending
{
    public static LayoutAllocationResult Apply(GameContent content, PlannerSession adapter, LayoutBlueprint blueprint,
        int wave, LayoutAllocationOptions? options = null)
    {
        options ??= new();
        blueprint.Validate(content);
        options.Validate();
        if (wave < 1) throw new ArgumentException("Layout allocation wave must be positive.");
        var session = adapter.Session;
        if (blueprint.MapId is not null && blueprint.MapId != session.Map.Definition.Id)
            throw new InvalidDataException("Layout blueprint belongs to a different map.");
        var accepted = 0;
        var completed = 0;
        string? blocked = null;
        string? rejection = null;
        if (blueprint.Generator is { } generatorGoal && wave >= generatorGoal.FromWave)
        {
            while (true)
            {
                var generator = session.Generator;
                if (generator is not null && (Math.Abs(generator.Position.X - generatorGoal.X) >= options.PositionTolerance ||
                    Math.Abs(generator.Position.Y - generatorGoal.Y) >= options.PositionTolerance))
                {
                    blocked = "generator";
                    rejection = "An existing Charge Forge occupies a different position.";
                    break;
                }
                if (generator is not null && generator.LevelIndex >= generatorGoal.LevelIndex) break;
                var cost = generator?.UpgradeCost ?? content.Tactics.Generator.PurchaseCost;
                if (accepted >= options.MaximumCommands || session.Economy.Credits < (long)cost + options.ReserveCredits)
                { blocked = "generator"; break; }
                var command = generator is null
                    ? new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceGenerator, X = generatorGoal.X, Y = generatorGoal.Y }
                    : new GameCommand { PlayerId = 1, Type = GameCommandType.UpgradeGenerator };
                var result = adapter.Apply(command, "ordered layout Charge Forge allocation");
                if (!result.Accepted) { blocked = "generator"; rejection = result.Reason; break; }
                accepted++;
            }
        }
        var assignedTowers = new HashSet<int>();
        foreach (var goal in blueprint.Towers)
        {
            if (blocked is not null) break;
            while (true)
            {
                var tower = FindTower(session, goal, options.PositionTolerance, assignedTowers);
                if (tower is not null &&
                    (goal.DoctrineId is not null && tower.DoctrineId is not null && tower.DoctrineId != goal.DoctrineId ||
                     goal.SpecializationId is not null && tower.SpecializationId is not null && tower.SpecializationId != goal.SpecializationId))
                {
                    blocked = goal.Key;
                    rejection = "An existing tower has incompatible progression choices.";
                    break;
                }
                GameCommand command;
                int cost;
                if (tower is null)
                {
                    cost = content.Towers[goal.TowerId].PurchaseCost;
                    command = new() { PlayerId = 1, Type = GameCommandType.PlaceTower,
                        TowerDefinitionId = goal.TowerId, X = goal.X, Y = goal.Y };
                }
                else if (tower.LevelIndex < goal.LevelIndex)
                {
                    if (tower.RequiresDoctrine)
                    {
                        cost = tower.Definition.Tier2Doctrines.Single(item => item.Id == goal.DoctrineId).UpgradeCost;
                        command = new() { PlayerId = 1, Type = GameCommandType.ChooseDoctrine,
                            EntityId = tower.Id, DoctrineId = goal.DoctrineId! };
                    }
                    else if (tower.RequiresSpecialization)
                    {
                        cost = tower.Definition.Specializations.Single(item => item.Id == goal.SpecializationId).UpgradeCost;
                        command = new() { PlayerId = 1, Type = GameCommandType.SpecializeTower,
                            EntityId = tower.Id, SpecializationId = goal.SpecializationId! };
                    }
                    else
                    {
                        cost = tower.UpgradeCost;
                        command = new() { PlayerId = 1, Type = GameCommandType.UpgradeTower, EntityId = tower.Id };
                    }
                }
                else if (options.AllowApexUpgrades && goal.ApexFromWave > 0 && wave >= goal.ApexFromWave && !tower.IsApex)
                {
                    cost = tower.ApexUpgradeCost;
                    command = new() { PlayerId = 1, Type = GameCommandType.UpgradeTower, EntityId = tower.Id };
                }
                else { completed++; assignedTowers.Add(tower.Id); break; }
                if (accepted >= options.MaximumCommands || session.Economy.Credits < (long)cost + options.ReserveCredits)
                { blocked = goal.Key; break; }
                var result = adapter.Apply(command, "ordered layout allocation");
                if (!result.Accepted) { blocked = goal.Key; rejection = result.Reason; break; }
                accepted++;
            }
            if (blocked is not null) break;
        }
        foreach (var key in blueprint.AutomaticProtocolPreference)
        {
            var goal = blueprint.Towers.Single(item => item.Key == key);
            var tower = FindTower(session, goal, options.PositionTolerance);
            if (tower is null || tower.IsApex) continue;
            if (session.AutoOverdriveTowerId != tower.Id && accepted < options.MaximumCommands &&
                adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.ToggleAutoProtocol, EntityId = tower.Id },
                    "layout automatic Protocol preference").Accepted) accepted++;
            break;
        }
        return new(accepted, completed, blocked, rejection);
    }

    private static TowerInstance? FindTower(GameSession session, LayoutTowerGoal goal, float tolerance,
        IReadOnlySet<int>? excluded = null) =>
        session.Towers.FirstOrDefault(tower => tower.Definition.Id == goal.TowerId &&
            (excluded is null || !excluded.Contains(tower.Id)) &&
            Math.Abs(tower.Position.X - goal.X) < tolerance && Math.Abs(tower.Position.Y - goal.Y) < tolerance);
}
