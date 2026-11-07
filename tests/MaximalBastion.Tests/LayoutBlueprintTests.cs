using MaximalBastion.Data;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class LayoutBlueprintTests
{
    public static void Run()
    {
        var level = new TowerLevelDefinition { Range = 100, Damage = 10, AttacksPerSecond = 1 };
        var tower = new TowerDefinition
        {
            Id = "layout_tower", PurchaseCost = 100, Behavior = "beam",
            Levels = [level, level, level],
            Tier2Doctrines = [new TowerDoctrineDefinition { Id = "a", UpgradeCost = 50 },
                new TowerDoctrineDefinition { Id = "b", UpgradeCost = 50 }],
            Specializations = [new TowerSpecializationDefinition { Id = "x", UpgradeCost = 70, Level = level },
                new TowerSpecializationDefinition { Id = "y", UpgradeCost = 70, Level = level }]
        };
        var map = new MapDefinition
        {
            Id = "layout_fixture", StartingCredits = 350,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }], PathWidth = 30,
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower },
            Enemies = new(), Waves = new WaveSetDefinition(),
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture" } }
        };
        var session = PlannerSession.Create(content, new() { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture" });
        var first = new LayoutTowerGoal { Key = "first", TowerId = tower.Id, X = 250, Y = 230,
            LevelIndex = 2, DoctrineId = "a", SpecializationId = "x" };
        var blueprint = new LayoutBlueprint { MapId = map.Id, Towers = [first, first with { Key = "second", X = 410 }] };
        var result = LayoutBlueprintSpending.Apply(content, session, blueprint, 1);
        Check.True(result.CompletedGoals == 1 && result.BlockedGoal == "second" && result.AcceptedCommands == 4 &&
            session.Session.Economy.Credits == 30 && session.Session.Towers.Count == 2,
            "ordered layout spending obeys progression costs and retains unaffordable work");
        var signature = session.Signature;
        var doctrineMismatch = LayoutBlueprintSpending.Apply(content, session,
            blueprint with { Towers = [first with { DoctrineId = "b" }] }, 1);
        var specializationMismatch = LayoutBlueprintSpending.Apply(content, session,
            blueprint with { Towers = [first with { SpecializationId = "y" }] }, 1);
        Check.True(doctrineMismatch.Rejection is not null && doctrineMismatch.CompletedGoals == 0 &&
            specializationMismatch.Rejection is not null && specializationMismatch.CompletedGoals == 0 &&
            session.Signature == signature, "existing incompatible branches never count as completed layout goals");
        var rejectedDuplicate = false;
        try { (blueprint with { Towers = [first, first with { Key = "duplicate" }] }).Validate(content); }
        catch (InvalidDataException) { rejectedDuplicate = true; }
        Check.True(rejectedDuplicate, "distinct layout keys cannot count the same tower position twice");
        var reserved = PlannerSession.Create(content, session.Scenario);
        var reserveResult = LayoutBlueprintSpending.Apply(content, reserved, blueprint, 1, new() { ReserveCredits = 50 });
        Check.True(reserveResult.CompletedGoals == 1 && reserved.Session.Economy.Credits == 130 && reserved.Session.Towers.Count == 1,
            "layout spending preserves the requested tactical reserve");

        content.Tactics.Generator.PurchaseCost = 100;
        content.Tactics.Generator.Levels[0].UpgradeCost = 50;
        content.Tactics.Generator.Levels[1].UpgradeCost = 70;
        var forgeLayout = blueprint with
        {
            Towers = [first with { LevelIndex = 0, DoctrineId = null, SpecializationId = null }],
            Generator = new() { X = 600, Y = 300, LevelIndex = 2, FromWave = 12 }
        };
        var scheduled = PlannerSession.Create(content, session.Scenario);
        LayoutBlueprintSpending.Apply(content, scheduled, forgeLayout, 11);
        Check.True(scheduled.Session.Generator is null && scheduled.Session.Towers.Count == 1,
            "a future Forge goal does not block earlier tower purchases");
        var forgeResult = LayoutBlueprintSpending.Apply(content, scheduled, forgeLayout, 12);
        Check.True(forgeResult.AcceptedCommands == 3 && scheduled.Session.Generator?.LevelIndex == 2 &&
            scheduled.Session.Economy.Credits == 30,
            "scheduled Forge purchase and upgrades use validated commands at actual costs");
        var forgeReserved = PlannerSession.Create(content, session.Scenario);
        var blockedForge = LayoutBlueprintSpending.Apply(content, forgeReserved, forgeLayout, 12,
            new() { ReserveCredits = 150 });
        Check.True(blockedForge.BlockedGoal == "generator" && blockedForge.AcceptedCommands == 2 &&
            forgeReserved.Session.Generator?.LevelIndex == 1 && forgeReserved.Session.Economy.Credits == 200 &&
            forgeReserved.Session.Towers.Count == 0,
            "Forge progression preserves reserves and blocks later investment until affordable");
        var limitedForge = LayoutBlueprintSpending.Apply(content, forgeReserved, forgeLayout, 12,
            new() { MaximumCommands = 1 });
        Check.True(limitedForge.AcceptedCommands == 1 && forgeReserved.Session.Generator?.LevelIndex == 2 &&
            forgeReserved.Session.Towers.Count == 0,
            "Forge and tower goals share the allocation command limit");
        var forgeSignature = forgeReserved.Signature;
        var wrongPosition = LayoutBlueprintSpending.Apply(content, forgeReserved,
            forgeLayout with { Generator = forgeLayout.Generator! with { X = 650 } }, 12);
        Check.True(wrongPosition.Rejection is not null && forgeReserved.Signature == forgeSignature,
            "a Forge at a different position never silently satisfies the goal");
    }
}
