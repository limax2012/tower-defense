using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class TacticalForecastTests
{
    public static void Run()
    {
        MechanicTargetingSurvivesCleanup();
        SpendingAndFailureMetricsUseLegalReplay();
        BranchesKeepTheWaveDecisionCadence();
    }

    private static void MechanicTargetingSurvivesCleanup()
    {
        var (content, scenario) = Fixture("slow_projectile", 1, 27);
        var initial = PlannerSession.Create(content, scenario);
        Check.True(initial.Apply(Place()).Accepted, "control fixture uses a paid legal tower");
        var save = initial.Session.CaptureSaveGame();
        save.Waves.CurrentWaveNumber = 26;
        var checkpoint = PlannerSession.FromSave(content, save, scenario with { StartingProfile = "synthetic-imported" }).Capture();
        var options = new TacticalRolloutOptions { MaximumWaveTicks = 300 };
        var policy = new TacticalPolicy();
        var baseline = TacticalForecastSearch.Evaluate(content, checkpoint,
            policy with { BehaviorTargetModes = new Dictionary<string, TargetMode>() }, options: options);
        var preserved = TacticalForecastSearch.Evaluate(content, checkpoint, policy, options: options);
        var before = PlannerSession.Restore(content, baseline.Checkpoint!).Session;
        var after = PlannerSession.Restore(content, preserved.Checkpoint!).Session;
        Check.True(after.Enemies.Any(enemy => enemy.PathProgress >= .72f) && after.Waves.QueuedEnemies == 0,
            "control fixture reaches the late cleanup condition");
        Check.True(before.Towers.Single().TargetMode == TargetMode.First && after.Towers.Single().TargetMode == TargetMode.Fastest,
            "behavior-based control targeting survives an ordinary cleanup target proposal without relying on authored tower IDs");
        var copy = JsonSerializer.Deserialize<TacticalPolicy>(JsonSerializer.Serialize(policy))!;
        Check.True(copy.BehaviorTargetModes["slow_projectile"] == TargetMode.Fastest && copy.DecisionTicks == 15,
            "tactical policy data retains its control override and cadence");
    }

    private static void SpendingAndFailureMetricsUseLegalReplay()
    {
        var (content, scenario) = Fixture("beam", 1000, 1);
        var initial = PlannerSession.Create(content, scenario);
        var signature = initial.Signature;
        var checkpoint = initial.Capture();
        var policy = new TacticalPolicy { DirectPlateLimit = 0 };
        var failed = TacticalForecastSearch.Evaluate(content, checkpoint, policy);
        Check.True(!failed.WaveCleared && failed.EscapedEnemies == 1 && failed.EscapedHealth > 0 &&
                   failed.Leaks.Count == 1 && failed.Leaks[0].LifeCost == 1,
            "failed forecasts retain measured fatal escape health and life cost");
        var winner = TacticalForecastSearch.Evaluate(content, checkpoint, policy, adapter =>
        {
            if (adapter.Session.Towers.Count == 0) adapter.Apply(Place(), "fixture spending callback");
        });
        Check.True(winner is { WaveCleared: true, CampaignVictory: true, Damage: > 0 } &&
                   winner.Verification is { VerifiedWin: true, FromNormalStart: true },
            "a composed spending callback produces a complete independently verified normal-start win");
        Check.True(winner.Checkpoint!.Actions.Count(action => action.Command.Type == GameCommandType.PlaceTower && action.Accepted) == 1 &&
                   initial.Signature == signature, "forecast spending is legal and does not mutate its shared starting checkpoint");
        var limited = TacticalForecastSearch.Search(content, checkpoint, [policy],
            new TacticalForecastOptions { MaximumTrials = 1, Rollout = new TacticalRolloutOptions { CommitAfterTicks = 60 } });
        Check.True(limited.SuccessfulTrial is null && limited.Trials.Count == 1 && limited.StopReason == "trial budget reached",
            "an exhausted tactical budget reports incomplete search without claiming impossibility");
    }

    private static void BranchesKeepTheWaveDecisionCadence()
    {
        var (content, scenario) = Fixture("beam", 1000, 1);
        var initial = PlannerSession.Create(content, scenario);
        initial.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
        initial.Advance(37);
        var spendingTicks = new List<long>();
        TacticalForecastSearch.Evaluate(content, initial.Capture(),
            new TacticalPolicy { DecisionTicks = 60, SpendingDecisionTicks = 60 },
            adapter => spendingTicks.Add(adapter.Tick), new TacticalRolloutOptions { MaximumWaveTicks = 130 });
        Check.True(spendingTicks.SequenceEqual(new long[] { 60, 120 }),
            "resuming between decision frames preserves cadence from the actual wave start");
    }

    private static GameCommand Place() => new()
    {
        PlayerId = 1, Type = GameCommandType.PlaceTower, TowerDefinitionId = "forecast_fixture_tower", X = 250, Y = 230
    };

    private static (GameContent Content, PlanningScenario Scenario) Fixture(string behavior, float damage, int waves)
    {
        var tower = new TowerDefinition
        {
            Id = "forecast_fixture_tower", DisplayName = "Forecast fixture", Behavior = behavior, PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { Range = 600, Damage = damage, AttacksPerSecond = damage > 1 ? 4 : .1f,
                ProjectileSpeed = 500, SlowPercent = .05f, SlowDuration = .1f }]
        };
        var enemy = new EnemyDefinition
        {
            Id = "forecast_fixture_enemy", DisplayName = "Forecast fixture enemy", MaxHealth = 50,
            Speed = 80, LivesLost = 1, Visual = new EnemyVisualData { Radius = 10 }
        };
        var map = new MapDefinition
        {
            Id = "forecast_fixture", DisplayName = "Synthetic forecast fixture", StartingCredits = 100,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }], PathWidth = 30,
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower }, Enemies = new() { [enemy.Id] = enemy },
            Waves = new WaveSetDefinition { Waves = Enumerable.Range(1, waves).Select(number => new WaveDefinition
                { Number = number, Groups = [new WaveGroupDefinition { EnemyId = enemy.Id, Count = 1 }] }).ToList() },
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture", TacticalSystemsEnabled = false,
                ProtocolsEnabled = false, SellingEnabled = true } }
        };
        return (content, new PlanningScenario { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = waves });
    }
}
