using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class SelectableThreatFocusTests
{
    public static void Run()
    {
        ReadyWeaponsUseReplayableModes();
        BoundsAndFiltersPreserveOrders();
        InvalidPoliciesAreRejectedBeforeCommands();
    }

    private static void ReadyWeaponsUseReplayableModes()
    {
        var (content, adapter) = Fixture();
        var policy = new ThreatFocusPolicy { Rules = [new() { Rank = EnemyRank.Elite }] };
        adapter.Advance(3);
        Check.True(adapter.Session.Enemies.Count == 2 && SelectableThreatFocus.Apply(adapter, policy) == 0,
            "cooling weapons keep their orders while both priority and ordinary enemies are present");
        for (var tick = 0; tick < 180 && adapter.Session.Towers[0].CooldownRemaining > 1f / 60; tick++) adapter.Advance(1);
        var before = adapter.Actions.Count;
        Check.True(SelectableThreatFocus.Apply(adapter, policy) == 1,
            "a ready weapon can select a priority rank through a legal target mode");
        var tower = adapter.Session.Towers[0];
        var selected = adapter.Session.TargetSelector.Select(tower.Position, adapter.Session.GetEffectiveRange(tower),
            tower.TargetMode, adapter.Session.Enemies);
        Check.True(selected?.Rank == EnemyRank.Elite && adapter.Session.Towers[1].TargetMode == TargetMode.First &&
            adapter.Actions.Skip(before).All(action => action.Accepted && action.Command.Type == GameCommandType.SetTargetMode),
            "the exact selector reaches the priority enemy while excluded Frost orders remain unchanged");
        Check.True(SelectableThreatFocus.Apply(adapter, policy) == 0,
            "an unchanged priority order does not consume another command");
        var verification = PlanningReplay.Verify(content, adapter.ExportTrace());
        Check.True(verification.Verified && verification.FromNormalStart,
            "priority targeting independently replays from the authored initial economy");
        var copy = JsonSerializer.Deserialize<ThreatFocusPolicy>(JsonSerializer.Serialize(policy))!;
        copy.Validate();
        Check.True(copy.Rules.Single().Rank == EnemyRank.Elite && copy.ExcludedBehaviors.SequenceEqual(["slow_projectile"]),
            "priority rules and excluded behavior filters round trip as policy data");
    }

    private static void BoundsAndFiltersPreserveOrders()
    {
        var (_, adapter) = Fixture();
        adapter.Advance(3);
        var before = adapter.Actions.Count;
        var rule = new ThreatFocusRule { Rank = EnemyRank.Elite };
        var policy = new ThreatFocusPolicy { ReadyWithinTicks = 3600, Rules = [rule] };
        Check.True(SelectableThreatFocus.Apply(adapter, policy with { Rules = [rule with { MinimumProgress = .9f }] }) == 0 &&
            SelectableThreatFocus.Apply(adapter, policy with { Rules = [rule with { MaximumActiveEnemies = 1 }] }) == 0 &&
            SelectableThreatFocus.Apply(adapter, policy with { Rules = [rule with { Behaviors = ["armor_projectile"] }] }) == 0 &&
            adapter.Actions.Count == before, "unmet progress, population and behavior filters leave existing orders untouched");
        var ranked = policy with { Rules = [rule, new() { Rank = EnemyRank.Standard, Priority = 200 }] };
        Check.True(SelectableThreatFocus.Apply(adapter, ranked) == 0 && adapter.Session.Towers[0].TargetMode == TargetMode.First,
            "a higher priority matching rule wins over a lower priority elite rule");
    }

    private static void InvalidPoliciesAreRejectedBeforeCommands()
    {
        var (_, adapter) = Fixture();
        var before = adapter.Actions.Count;
        var invalid = new ThreatFocusPolicy[]
        {
            new(), new() { Rules = [null!] }, new() { Rules = [new()] },
            new() { Rules = [new() { Rank = (EnemyRank)999 }] },
            new() { Rules = [new() { SignalRole = (EnemySignalRole)999 }] },
            new() { Rules = [new() { Rank = EnemyRank.Boss, MinimumProgress = .9f, MaximumProgress = .2f }] },
            new() { Rules = [new() { Rank = EnemyRank.Boss }], ReadyWithinTicks = 0 },
            new() { Rules = [new() { Rank = EnemyRank.Boss }], ExcludedBehaviors = ["beam", "beam"] }
        };
        foreach (var policy in invalid)
        {
            var rejected = false;
            try { SelectableThreatFocus.Apply(adapter, policy); }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected, "invalid threat focus rules are rejected before evaluation");
        }
        Check.True(adapter.Actions.Count == before, "invalid focus policy validation cannot emit gameplay commands");
    }

    private static (GameContent Content, PlannerSession Adapter) Fixture()
    {
        var towers = new[] { "beam", "slow_projectile" }.Select(behavior => new TowerDefinition
        {
            Id = behavior, DisplayName = behavior, Behavior = behavior, PurchaseCost = 100,
            Levels = [new() { Range = 600, Damage = 1, AttacksPerSecond = 2, ProjectileSpeed = 500,
                SlowPercent = .05f, SlowDuration = .1f }]
        }).ToDictionary(tower => tower.Id);
        var enemy = new EnemyDefinition { Id = "focus_enemy", DisplayName = "Focus fixture", MaxHealth = 200,
            Speed = 30, LivesLost = 1, Visual = new() { Radius = 10 } };
        var elite = new EnemyDefinition { Id = "focus_elite", DisplayName = "Priority fixture", MaxHealth = 200,
            Speed = 10, LivesLost = 1, Visual = new() { Radius = 10 } };
        var map = new MapDefinition { Id = "focus_map", DisplayName = "Focus fixture", StartingCredits = 200,
            Path = [new() { X = 0, Y = 120 }, new() { X = 500, Y = 120 }], PathWidth = 30,
            BuildableRegions = [new() { X = 100, Y = 200, Width = 700, Height = 250 }] };
        var content = new GameContent
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = towers,
            Enemies = new() { [enemy.Id] = enemy, [elite.Id] = elite },
            Waves = new() { Waves = [new() { Number = 1, Groups = [
                new() { EnemyId = enemy.Id, Count = 1 }, new() { EnemyId = elite.Id, Count = 1, Rank = "Elite" }] }] },
            Difficulties = new() { ["fixture"] = new() { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new() { Id = "fixture", TacticalSystemsEnabled = false, ProtocolsEnabled = false } }
        };
        var adapter = PlannerSession.Create(content, new() { MapId = map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = 1 });
        foreach (var (definition, index) in towers.Values.Select((definition, index) => (definition, index)))
            Check.True(adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = definition.Id, X = 250 + index * 80, Y = 230 }).Accepted, "focus fixture placement is legal");
        Check.True(adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.StartWave }).Accepted, "focus fixture starts legally");
        return (content, adapter);
    }
}
