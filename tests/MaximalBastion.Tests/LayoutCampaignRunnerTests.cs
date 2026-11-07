using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class LayoutCampaignRunnerTests
{
    public static void Run()
    {
        var (content, plan) = Fixture();
        var result = LayoutCampaignRunner.Run(content, plan, new() { MaximumTrials = 2, BeamWidth = 1 });
        Check.True(result.Trials.Count == 2 && !result.Trials[0].Cleared && result.Trials[1].Cleared &&
            result.Verification is { VerifiedWin: true, FromNormalStart: true } &&
            result.SelectedPolicies.Single().Policy.Id == "protect-fast-lead",
            "a fresh layout campaign discovers and independently verifies a winning tactical alternative");
        var limited = LayoutCampaignRunner.Run(content, plan, new() { MaximumTrials = 1 });
        Check.True(limited.Trials.Count == 1 && limited.HighestCompletedWave == 0 &&
            limited.Outcome == "trial budget reached" && limited.WinningTrace is null,
            "layout campaign trial exhaustion reports incomplete search without claiming impossibility");
        var callbacks = 0;
        bool Rejects(LayoutCampaignPlan invalid)
        {
            try { LayoutCampaignRunner.Run(content, invalid, completed: (_, _) => callbacks++); }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException) { return true; }
            return false;
        }
        Check.True(Rejects(plan with { Layout = plan.Layout with { MapId = "different_map" } }) &&
            Rejects(plan with { Waves = [plan.Waves[0] with { SpendOnLayout = false,
                Allocation = new() { ReserveCredits = -1 } }] }) && callbacks == 0,
            "invalid maps and unused allocation options are rejected before any rollout starts");

        var (apexContent, apexPlan) = Fixture(21);
        var adapter = PlannerSession.Create(apexContent, apexPlan.Scenario);
        LayoutBlueprintSpending.Apply(apexContent, adapter, apexPlan.Layout, 1);
        var save = adapter.Session.CaptureSaveGame();
        save.Waves.CurrentWaveNumber = 20;
        var imported = PlannerSession.FromSave(apexContent, save,
            apexPlan.Scenario with { StartingProfile = "synthetic-imported" }).Capture();
        var apexLayout = apexPlan.Layout with { Towers = [apexPlan.Layout.Towers[0] with { ApexFromWave = 21 }] };
        var disabled = new LayoutWavePlan { FirstWave = 21, LastWave = 21, SpendOnLayout = true, ApplyScheduledApex = false };
        var untouched = PlannerSession.Restore(apexContent, imported);
        LayoutCampaignRunner.CreateSpending(apexContent, imported, apexLayout, disabled, 21)(untouched);
        Check.True(!untouched.Session.Towers.Single().IsApex && untouched.Signature == imported.StateSignature,
            "disabling scheduled Apex also disables Apex inside ordered layout spending");
        var enabled = PlannerSession.Restore(apexContent, imported);
        LayoutCampaignRunner.CreateSpending(apexContent, imported, apexLayout,
            disabled with { ApplyScheduledApex = true }, 21)(enabled);
        Check.True(enabled.Session.Towers.Single().IsApex && enabled.Session.Economy.Credits == 100,
            "scheduled Apex goals use real unlocked upgrades and real costs");
        foreach (var spendOnLayout in new[] { false, true })
        {
            var reserved = PlannerSession.Restore(apexContent, imported);
            LayoutCampaignRunner.CreateSpending(apexContent, imported, apexLayout,
                disabled with { ApplyScheduledApex = true, SpendOnLayout = spendOnLayout,
                    Allocation = new() { ReserveCredits = 150 } }, 21)(reserved);
            Check.True(!reserved.Session.Towers.Single().IsApex && reserved.Signature == imported.StateSignature,
                "scheduled Apex preserves the allocation reserve with or without ordered spending");
        }
        var expanded = apexLayout with { Towers = [apexLayout.Towers[0] with { Key = "expansion", X = 410, ApexFromWave = 0 },
            apexLayout.Towers[0]] };
        var commandLimited = PlannerSession.Restore(apexContent, imported);
        LayoutCampaignRunner.CreateSpending(apexContent, imported, expanded,
            disabled with { ApplyScheduledApex = true, Allocation = new() { MaximumCommands = 1 } }, 21)(commandLimited);
        Check.True(commandLimited.Session.Towers.Count == 2 && commandLimited.Session.Towers.All(tower => !tower.IsApex) &&
            commandLimited.Actions.Count(action => action.Accepted) == imported.Actions.Count(action => action.Accepted) + 1,
            "ordered purchases and scheduled Apex share the same command budget");

        apexContent.Map.StartingCredits = 600;
        var two = PlannerSession.Create(apexContent, apexPlan.Scenario);
        var bothLayout = apexLayout with { Towers = [apexLayout.Towers[0], apexLayout.Towers[0] with { Key = "second", X = 410 }] };
        LayoutBlueprintSpending.Apply(apexContent, two, bothLayout, 1);
        var twoSave = two.Session.CaptureSaveGame();
        twoSave.Waves.CurrentWaveNumber = 20;
        var twoInitial = PlannerSession.FromSave(apexContent, twoSave,
            apexPlan.Scenario with { StartingProfile = "synthetic-imported" }).Capture();
        var apexLimited = PlannerSession.Restore(apexContent, twoInitial);
        LayoutCampaignRunner.CreateSpending(apexContent, twoInitial, bothLayout,
            disabled with { ApplyScheduledApex = true, SpendOnLayout = false,
                Allocation = new() { MaximumCommands = 1 } }, 21)(apexLimited);
        Check.True(apexLimited.Session.Towers.Count(tower => tower.IsApex) == 1 &&
            apexLimited.Actions.Count(action => action.Accepted && action.Command.Type == GameCommandType.UpgradeTower) == 1,
            "scheduled-only Apex spending respects the maximum accepted command count");
    }

    private static (GameContent Content, LayoutCampaignPlan Plan) Fixture(int waves = 1)
    {
        var tower = new TowerDefinition
        {
            Id = "campaign_fixture_tower", Behavior = "beam", PurchaseCost = 100,
            Levels = [new TowerLevelDefinition { Range = 600, Damage = 100, AttacksPerSecond = 1 }],
            Apex = new TowerApexDefinition { UpgradeCost = 150, DamageMultiplier = 2 }
        };
        var heavy = new EnemyDefinition { Id = "heavy", MaxHealth = 1000, Speed = 10, LivesLost = 1,
            Visual = new EnemyVisualData { Radius = 10 } };
        var runner = new EnemyDefinition { Id = "runner", MaxHealth = 50, Speed = 250, LivesLost = 1,
            Visual = new EnemyVisualData { Radius = 10 } };
        var map = new MapDefinition
        {
            Id = "campaign_fixture", StartingCredits = 350, PathWidth = 30,
            Path = [new PointData { X = 0, Y = 120 }, new PointData { X = 500, Y = 120 }],
            BuildableRegions = [new RectangleData { X = 100, Y = 200, Width = 700, Height = 250 }]
        };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower },
            Enemies = new() { [heavy.Id] = heavy, [runner.Id] = runner },
            Waves = new WaveSetDefinition { Waves = Enumerable.Range(1, waves).Select(number => new WaveDefinition
                { Number = number, Groups = [new() { EnemyId = heavy.Id, Count = 1 },
                    new() { EnemyId = runner.Id, Count = 1, DelayBefore = .5f }] }).ToList() },
            Difficulties = new() { ["fixture"] = new DifficultyDefinition { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new ChallengeDefinition { Id = "fixture",
                TacticalSystemsEnabled = false, ProtocolsEnabled = false, SellingEnabled = true } }
        };
        var primary = new TacticalPolicy { Id = "focus-bulk", PrepareBeforeStart = true, ReactAtBoundary = false,
            DamageTargetMode = TargetMode.Strongest, BehaviorTargetModes = new Dictionary<string, TargetMode>() };
        var plan = new LayoutCampaignPlan
        {
            Id = "tactical-alternative-fixture", Scenario = new() { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = waves },
            Layout = new() { MapId = map.Id, Towers = [new() { Key = "main", TowerId = tower.Id, X = 250, Y = 230 }] },
            Waves = [new() { FirstWave = 1, LastWave = waves, Policy = primary,
                Alternatives = [primary with { Id = "protect-fast-lead", DamageTargetMode = TargetMode.First }] }]
        };
        return (content, plan);
    }
}
