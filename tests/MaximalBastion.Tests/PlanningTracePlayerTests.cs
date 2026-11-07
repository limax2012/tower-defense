using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class PlanningTracePlayerTests
{
    public static void Run()
    {
        FrameSamplingPreservesCommandBoundaries();
        AuthenticationPrecedesPlayback();
        FinalStateDetectsPresentationMutation();
        ZeroTickTraceAppliesFinalCommands();
    }

    private static void FrameSamplingPreservesCommandBoundaries()
    {
        var content = Content();
        var trace = PartialTrace(content);
        var mutableActions = trace.Actions.ToList();
        var sampled = PlanningTracePlayer.Create(content, trace with { Actions = mutableActions });
        mutableActions.Clear();
        var singleStep = PlanningTracePlayer.Create(content, trace);
        Check.True(sampled.Tick == 0 && sampled.AppliedActions == 2 && sampled.Session.Towers.Count == 1 &&
            sampled.Session.Waves.IsActive && !sampled.IsComplete && sampled.Progress == 0,
            "the first playback boundary includes all tick-zero commands");
        Check.Throws<InvalidOperationException>(() => sampled.VerifyFinalState(),
            "a partial playback cannot claim final-state verification");

        foreach (var boundary in new long[] { 17, 17, 40, 53, 60 })
        {
            sampled.AdvanceTo(boundary);
            while (singleStep.Tick < boundary) singleStep.Advance(1);
            Check.Equal(PlanningFingerprint.State(singleStep.Session, singleStep.Tick),
                PlanningFingerprint.State(sampled.Session, sampled.Tick),
                "frame sampling preserves the exact simulation and all same-tick commands");
        }
        sampled.AdvanceTo(long.MaxValue);
        sampled.Advance(100);
        var verification = sampled.VerifyFinalState();
        Check.True(sampled.IsComplete && sampled.Tick == trace.FinalTick && sampled.Progress == 1 &&
            sampled.AppliedActions == trace.Actions.Count && sampled.Session.Towers.Single(tower => tower.Id == 1).TargetMode == TargetMode.First &&
            verification is { Verified: true, VerifiedWin: false, FromNormalStart: true } &&
            verification.FinalSignature == trace.FinalSignature && verification.ActionsChecked == trace.Actions.Count,
            "playback includes the final-tick target change and clamps frames beyond the end");
        Check.Throws<ArgumentOutOfRangeException>(() => sampled.Advance(-1), "negative playback advance is rejected");
        Check.Throws<ArgumentOutOfRangeException>(() => sampled.AdvanceTo(59), "backward sampling is rejected");
    }

    private static void AuthenticationPrecedesPlayback()
    {
        var content = Content();
        var trace = PartialTrace(content);
        var otherBuild = trace with { BuildHash = new string('0', 64) };
        Check.Throws<InvalidDataException>(() => PlanningTracePlayer.Create(content, otherBuild),
            "playback requires the current build unless cross-build verification is explicit");
        var authenticated = PlanningTracePlayer.Create(content, otherBuild, requireCurrentBuild: false);
        authenticated.AdvanceTo(otherBuild.FinalTick);
        Check.True(authenticated.Verification.Verified && authenticated.VerifyFinalState().Verified,
            "cross-build playback remains conditional on complete independent replay");
        foreach (var corrupt in new[]
        {
            otherBuild with { ContentHash = new string('0', 64) },
            otherBuild with { FinalSignature = new string('0', 64) },
            otherBuild with { Actions = otherBuild.Actions.Select((action, index) => index == 0
                ? action with { ActualCost = action.ActualCost + 1 } : action).ToArray() },
            otherBuild with { Actions = otherBuild.Actions.Select((action, index) => index == otherBuild.Actions.Count - 1
                ? action with { Tick = otherBuild.FinalTick + 1 } : action).ToArray() }
        })
            Check.Throws<InvalidDataException>(() => PlanningTracePlayer.Create(content, corrupt, requireCurrentBuild: false),
                "changed content, costs, final state and out-of-bounds commands cannot become a visual replay");
    }

    private static void FinalStateDetectsPresentationMutation()
    {
        var content = Content();
        var player = PlanningTracePlayer.Create(content, PartialTrace(content));
        player.AdvanceTo(player.FinalTick);
        Check.True(GameCommandProcessor.Apply(player.Session, new GameCommand
            { PlayerId = 1, Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.Last }).Accepted,
            "the mutation control changes a real simulation order after playback");
        Check.Throws<InvalidDataException>(() => player.VerifyFinalState(),
            "final playback verification catches simulation mutations from a presentation caller");
    }

    private static void ZeroTickTraceAppliesFinalCommands()
    {
        var content = Content();
        var adapter = Create(content);
        Apply(adapter, new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = "playback_beam", X = 250, Y = 230 });
        Apply(adapter, new() { Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.Strongest });
        var player = PlanningTracePlayer.Create(content, adapter.ExportTrace());
        Check.True(player.IsComplete && player.Tick == 0 && player.AppliedActions == 2 && player.Progress == 1 &&
            player.Session.Towers.Single().TargetMode == TargetMode.Strongest && player.VerifyFinalState().Verified,
            "a zero-duration replay applies and verifies every final-boundary command");
    }

    private static PlanningTrace PartialTrace(GameContent content)
    {
        var adapter = Create(content);
        Apply(adapter, new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = "playback_beam", X = 250, Y = 230 });
        Apply(adapter, new() { Type = GameCommandType.StartWave });
        adapter.Advance(17);
        Apply(adapter, new() { Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.Strongest });
        Apply(adapter, new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = "playback_beam", X = 330, Y = 230 });
        adapter.Advance(23);
        Apply(adapter, new() { Type = GameCommandType.SellTower, EntityId = 2 });
        adapter.Advance(13);
        Apply(adapter, new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = "playback_beam", X = 330, Y = 230 });
        adapter.Advance(7);
        Apply(adapter, new() { Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = TargetMode.First });
        return adapter.ExportTrace();
    }

    private static PlannerSession Create(GameContent content) => PlannerSession.Create(content,
        new() { MapId = content.Map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = 1 });

    private static void Apply(PlannerSession adapter, GameCommand command) =>
        Check.True(adapter.Apply(command with { PlayerId = 1 }).Accepted, "playback fixture command is legal");

    private static GameContent Content()
    {
        var tower = new TowerDefinition { Id = "playback_beam", DisplayName = "Playback beam", Behavior = "beam", PurchaseCost = 100,
            Levels = [new() { Range = 600, Damage = 2, AttacksPerSecond = 1 }] };
        var enemy = new EnemyDefinition { Id = "playback_enemy", DisplayName = "Playback enemy", MaxHealth = 300,
            Speed = 20, LivesLost = 1, Visual = new() { Radius = 10 } };
        var map = new MapDefinition { Id = "playback_map", StartingCredits = 400, PathWidth = 30,
            Path = [new() { X = 0, Y = 120 }, new() { X = 500, Y = 120 }],
            BuildableRegions = [new() { X = 100, Y = 200, Width = 700, Height = 250 }] };
        return new()
        {
            Map = map, Maps = new() { [map.Id] = map }, Towers = new() { [tower.Id] = tower }, Enemies = new() { [enemy.Id] = enemy },
            Waves = new() { Waves = [new() { Number = 1, Groups = [new() { EnemyId = enemy.Id, Count = 1 }] }] },
            Difficulties = new() { ["fixture"] = new() { Id = "fixture", StartingLives = 1 } },
            Challenges = new() { ["fixture"] = new() { Id = "fixture", SellingEnabled = true,
                TacticalSystemsEnabled = false, ProtocolsEnabled = false } }
        };
    }
}
