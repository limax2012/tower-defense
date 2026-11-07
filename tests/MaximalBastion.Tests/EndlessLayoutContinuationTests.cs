using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class EndlessLayoutContinuationTests
{
    public static void Run()
    {
        var tower = new TowerDefinition { Id = "continuation_beam", PurchaseCost = 100, Behavior = "beam",
            Levels = [new TowerLevelDefinition { Range = 600, Damage = 100, AttacksPerSecond = 4 }] };
        var enemy = new EnemyDefinition { Id = "continuation_runner", MaxHealth = 30, Speed = 80, LivesLost = 1,
            Visual = new EnemyVisualData { Radius = 10 } };
        var map = new MapDefinition { Id = "continuation_fixture", StartingCredits = 300, PathWidth = 30,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }],
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }] };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower }, Enemies = new() { [enemy.Id] = enemy },
            Waves = new WaveSetDefinition { Waves = Enumerable.Range(1, GameConstants.CampaignWaveCount).Select(number => new WaveDefinition
                { Number = number, Groups = [new() { EnemyId = enemy.Id, Count = 1 }] }).ToList() },
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture", SellingEnabled = true,
                TacticalSystemsEnabled = false, ProtocolsEnabled = false } }
        };
        var scenario = new PlanningScenario { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture" };
        PlanningCheckpoint Campaign(bool combatSale)
        {
            var adapter = PlannerSession.Create(content, scenario);
            var purchase = new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = tower.Id, X = 250, Y = 230 };
            Check.True(adapter.Apply(purchase).Accepted, "the continuation fixture purchases its defense with starting credits");
            for (var number = 1; number <= GameConstants.CampaignWaveCount; number++)
            {
                Check.True(adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.StartWave }).Accepted, "authored wave starts legally");
                if (number == 1 && combatSale)
                {
                    Check.True(adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.SellTower, EntityId = adapter.Session.Towers.Single().Id }).Accepted &&
                        adapter.Apply(purchase).Accepted, "unrestricted fixture records a real combat sale and repurchase");
                }
                var start = adapter.Tick;
                while (adapter.Session.Waves.IsActive && !adapter.Session.IsDefeat && adapter.Tick - start < 1000) adapter.Advance(1);
                Check.True(!adapter.Session.Waves.IsActive && !adapter.Session.IsDefeat, "authored fixture wave cleared");
            }
            return adapter.Capture();
        }

        var initial = Campaign(false);
        var policy = new TacticalPolicy { Id = "keep-defense", DirectPlateLimit = 0 };
        var plan = new EndlessLayoutPlan
        {
            Layout = new() { MapId = map.Id, Towers = [new() { Key = "main", TowerId = tower.Id, X = 250, Y = 230 }] },
            Wave = new() { FirstWave = 31, LastWave = 31, Policy = policy,
                Alternatives = [policy with { Id = "different-cadence", DecisionTicks = 60 }] }
        };
        var run = EndlessLayoutContinuation.Run(content, initial, plan, new() { ForbidCombatSales = true });
        Check.True(run.Trials.Count == 2 && run.Trials.All(row => row.VerifiedSurvival && row.Sales == 0) &&
            run.Verification is { Verified: true, VerifiedWin: false, FromNormalStart: true, HorizonSurvived: true } &&
            run.SelectedTrace is { FinalWave: 31, CampaignVictory: false, HorizonSurvived: true, OriginSaveJson: null },
            "wave 31 alternatives independently verify finite Endless survival from normal campaign resources");
        var ready = PlannerSession.Restore(content, run.Ready);
        Check.True(run.Ready.Scenario is { Endless: true, Horizon: 31, ForbidCombatSales: true } && initial.Scenario.Endless == false &&
            ready.Session.CurrentWave == 30 && ready.Session.IsEndlessMode && !ready.Session.IsVictory &&
            run.Ready.Actions.Take(initial.Actions.Count).SequenceEqual(initial.Actions) &&
            run.Ready.Actions.Count == initial.Actions.Count + 1 && run.Ready.Actions[^1] is
                { Accepted: true, DuringWave: false, Command.Type: GameCommandType.ContinueEndless },
            "the untouched campaign prefix gains precisely one real Endless transition at victory");
        ready.Apply(new() { PlayerId = 1, Type = GameCommandType.StartWave });
        Check.True(!ready.Apply(new() { PlayerId = 1, Type = GameCommandType.SellTower, EntityId = ready.Session.Towers.Single().Id }).Accepted,
            "the promoted scenario enforces the combat sale restriction in the generated wave");
        var standing = EndlessLayoutContinuation.Run(content, initial, plan with
        {
            Layout = null,
            Wave = plan.Wave with { SpendOnLayout = false, ApplyScheduledApex = false,
                Policy = policy with { KeepExistingOrders = true }, Alternatives = [] }
        });
        Check.True(standing.Verification is { Verified: true, HorizonSurvived: true, FromNormalStart: true } &&
            standing.SelectedTrace!.Actions.Skip(initial.Actions.Count).Select(action => action.Command.Type)
                .SequenceEqual(new[] { GameCommandType.ContinueEndless, GameCommandType.StartWave }),
            "a standing-board control starts the generated wave with no other commands or layout");
        var prepared = EndlessLayoutContinuation.Run(content, initial, plan with
        {
            PreparationTicks = 1080, Layout = null,
            Wave = plan.Wave with { SpendOnLayout = false, ApplyScheduledApex = false,
                Policy = policy with { KeepExistingOrders = true }, Alternatives = [] }
        });
        Check.True(prepared.Ready.Tick == initial.Tick + 1080 &&
            prepared.Ready.Actions[^1].Tick == initial.Tick &&
            prepared.Ready.Actions[^1].Command.Type == GameCommandType.ContinueEndless &&
            prepared.SelectedTrace!.Actions.Skip(initial.Actions.Count).Last().Tick == initial.Tick + 1080 &&
            prepared.Verification is { Verified: true, HorizonSurvived: true, FromNormalStart: true },
            "preparation advances real intermission ticks after the legal Endless transition and remains replayable");
        foreach (var invalidTicks in new[] { -1, 216_001 })
            Check.Throws<InvalidDataException>(() => (plan with { PreparationTicks = invalidTicks }).Validate(content, scenario),
                "Endless preparation duration is bounded");
        bool Rejects(PlanningCheckpoint checkpoint)
        {
            try { EndlessLayoutContinuation.Prepare(content, checkpoint, forbidCombatSales: true); }
            catch (InvalidDataException) { return true; }
            return false;
        }
        Check.True(Rejects(Campaign(true)) && Rejects(initial with { Actions = initial.Actions.Skip(1).ToArray() }) &&
            Rejects(initial with { OriginSaveJson = "{}" }), "forbidden sales, unverified prefixes and imported origins cannot certify a fresh continuation");
    }
}
