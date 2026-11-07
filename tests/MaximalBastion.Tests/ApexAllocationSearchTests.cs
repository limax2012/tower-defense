using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class ApexAllocationSearchTests
{
    public static void Run()
    {
        var rifle = new TowerDefinition
        {
            Id = "allocation_rifle", DisplayName = "Allocation rifle", Behavior = "beam", PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { Range = 600, Damage = 10, AttacksPerSecond = 4 }],
            Apex = new TowerApexDefinition { UpgradeCost = 150, DamageMultiplier = 6 },
            Protocol = new TowerProtocolDefinition { AutoTriggerCount = 100 }
        };
        var enemy = new EnemyDefinition
        {
            Id = "allocation_armor", DisplayName = "Allocation armor", MaxHealth = 300, Speed = 80,
            Armor = 20, LivesLost = 1, Visual = new EnemyVisualData { Radius = 10 }
        };
        var map = new MapDefinition
        {
            Id = "allocation_fixture", DisplayName = "Synthetic allocation fixture", StartingCredits = 350,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }], PathWidth = 30,
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map },
            Towers = new() { [rifle.Id] = rifle }, Enemies = new() { [enemy.Id] = enemy },
            Waves = new WaveSetDefinition { Waves = Enumerable.Range(1, 21).Select(number => new WaveDefinition
                { Number = number, Groups = [new WaveGroupDefinition { EnemyId = enemy.Id, Count = 1 }] }).ToList() },
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture", SellingEnabled = true,
                ProtocolsEnabled = false, TacticalSystemsEnabled = false } }
        };
        var scenario = new PlanningScenario { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = 21 };
        var setup = PlannerSession.Create(content, scenario);
        for (var index = 0; index < 3; index++)
            Check.True(setup.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = rifle.Id, X = 250 + 80 * index, Y = 230 }).Accepted, "allocation fixture tower purchase");
        var options = new ApexAllocationOptions { MaximumFundingSales = 2, MaximumTrials = 32, MaximumWallSeconds = 20 };
        Check.True(ApexAllocationSearch.Generate(setup.Session, options).All(plan => plan.ApexTowerIds.Count == 0),
            "allocation proposals obey the real Apex wave unlock");
        var save = setup.Session.CaptureSaveGame();
        save.Waves.CurrentWaveNumber = 20;
        var initial = PlannerSession.FromSave(content, save, scenario with { StartingProfile = "synthetic-imported" });
        var before = initial.Signature;
        var plans = ApexAllocationSearch.Generate(initial.Session, options);
        Check.True(plans.Any(plan => plan.ApexTowerIds.Count == 1 &&
            plan.InitialCommands.Count(command => command.Type == GameCommandType.SellTower) == 2),
            "allocation proposals include a legal two-sale funding bundle");
        Check.True(plans.Where(plan => plan.ApexTowerIds.Count > 0).All(plan =>
            plan.InitialCommands.Count(command => command.Type == GameCommandType.SellTower) == 2),
            "unfunded upgrades are not substituted for legal spending");
        var result = ApexAllocationSearch.Search(content, initial.Capture(), options);
        Check.True(result.Trials.Any(trial => trial.Accepted && !trial.WaveCleared), "retaining the weak board loses the finite allocation fixture");
        var winner = result.Trials.FirstOrDefault(trial => trial.Verification is { VerifiedWin: true });
        Check.True(winner is not null && winner.WinningTrace is not null && winner.Verification is { FromNormalStart: false },
            "joint allocation search discovers and independently verifies the funded Apex continuation");
        Check.True(initial.Signature == before, "allocation branches never mutate their common checkpoint");
    }
}
