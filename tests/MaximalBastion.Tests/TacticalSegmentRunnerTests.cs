using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class TacticalSegmentRunnerTests
{
    public static void Run()
    {
        var tower = new TowerDefinition
        {
            Id = "segment_tower", DisplayName = "Segment fixture", Behavior = "beam", PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { Range = 600, Damage = 20, AttacksPerSecond = 1, ProjectileSpeed = 500 }]
        };
        var enemy = new EnemyDefinition
        {
            Id = "segment_enemy", DisplayName = "Segment fixture", MaxHealth = 100, Speed = 30, LivesLost = 1,
            Visual = new EnemyVisualData { Radius = 10 }
        };
        var map = new MapDefinition
        {
            Id = "segment_map", DisplayName = "Segment fixture", StartingCredits = 100, PathWidth = 30,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }],
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower }, Enemies = new() { [enemy.Id] = enemy },
            Waves = new WaveSetDefinition { Waves = [new WaveDefinition { Number = 1,
                Groups = [new WaveGroupDefinition { EnemyId = enemy.Id, Count = 1 }] }] },
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture", TacticalSystemsEnabled = false, ProtocolsEnabled = false } }
        };
        var initial = PlannerSession.Create(content, new PlanningScenario
        {
            MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = 1, ForbidCombatSales = true
        }).Capture();
        var policy = new TacticalPolicy { DirectPlateLimit = 0, DecisionTicks = 60, SpendingDecisionTicks = 60,
            PrepareBeforeStart = true, ReactAtBoundary = false };
        var options = new TacticalForecastOptions { MaximumTrials = 2, MaximumPhases = 2,
            Rollout = new TacticalRolloutOptions { MaximumWaveTicks = 1800, VerifyWaveClear = true } };
        var spendingTicks = new List<long>();
        void Spend(PlannerSession adapter)
        {
            spendingTicks.Add(adapter.Tick);
            if (adapter.Session.Towers.Count == 0)
                adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
                    TowerDefinitionId = tower.Id, X = 250, Y = 230 }, "segment fixture construction");
        }
        TacticalPolicySegment[] schedule = [new(0, 37, policy), new(37, 38, policy)];
        var result = TacticalSegmentRunner.Run(content, initial, schedule, options, Spend);
        Check.True(result.SuccessfulTrial?.Verification is { VerifiedWin: true, FromNormalStart: true },
            "fixed segments preserve independently verified fresh campaign provenance");
        Check.True(spendingTicks.Count > 2 && spendingTicks[0] == 0 && spendingTicks.Skip(1).All(tick => tick % 60 == 0),
            "segment boundaries preserve the original spending cadence instead of restarting it");
        Check.True(result.Segments.Count == 2 && result.Segments[0].EndAfterTicks == 37 && result.Segments[1].EndAfterTicks > 38,
            "the final policy runs until clear rather than stopping at its historical clear time");
        var direct = TacticalForecastSearch.Evaluate(content, initial, policy, Spend, options.Rollout);
        Check.Equal(direct.Checkpoint!.StateSignature, result.SuccessfulTrial!.Checkpoint!.StateSignature,
            "splitting an unchanged policy at a non-cadence tick preserves its final state");
        var limited = TacticalSegmentRunner.Run(content, initial, schedule, options with { MaximumTrials = 1 }, Spend);
        Check.True(limited.SuccessfulTrial is null && limited.Trials.Count == 1,
            "a fixed schedule reports incomplete work when its trial budget ends");
        Check.Throws<ArgumentException>(() => TacticalSegmentRunner.Run(content, initial, [new(1, 20, policy)], options),
            "fixed schedules reject a gap before the first segment");
        Check.Throws<ArgumentException>(() => TacticalSegmentRunner.Run(content, initial, [new(0, 37, policy), new(38, 60, policy)], options),
            "fixed schedules reject gaps between segments");
    }
}
