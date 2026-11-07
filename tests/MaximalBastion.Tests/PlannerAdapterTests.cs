using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Tests;

internal static class PlannerAdapterTests
{
    public static void Run()
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        foreach (var map in content.Maps.Values)
        foreach (var challenge in new[] { "standard", "close_quarters" })
            SnapshotContinuation(content, new PlanningScenario
            {
                MapId = map.Id, DifficultyId = "easy", ChallengeId = challenge, Seed = 4711
            });
        ReplayValidation(content);
        CommandBoundaryAgreement(content);
        ImportedOriginAndPause(content);
        DetachedTargetSnapshot(content);
        LegalPrefixRecovery(content);
        ExecutionProfileVerification(content);
        CrossBuildPrefixRevalidation(content);
        IntermissionOnlySelling(content);
        ReplacementAfterRestore(content);
    }

    private static void ReplacementAfterRestore(GameContent content)
    {
        var direct = PlannerSession.Create(content, new PlanningScenario
            { MapId = "foundry_loop", DifficultyId = "easy", ForbidCombatSales = true });
        foreach (var position in new[] { new Vector2(50, 200), new Vector2(170, 199), new Vector2(309.99f, 199) })
            Check.True(direct.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y }).Accepted,
                "replacement fixture buys three legal towers");
        Check.True(direct.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SellTower, EntityId = 2 }).Accepted,
            "replacement fixture sells the middle attribution entry between waves");
        var restored = PlannerSession.Restore(content, direct.Capture());
        var replacement = new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "needle_turret", X = 170, Y = 199 };
        Check.True(direct.Apply(replacement).Accepted && restored.Apply(replacement).Accepted,
            "both branches legally replace the sold tower");
        Check.Equal(direct.Signature, restored.Signature,
            "attribution dictionary storage order does not change a replacement state signature");
        foreach (var branch in new[] { direct, restored })
        {
            Check.True(PlanningReplay.Verify(content, branch.ExportTrace()) is
                { Verified: true, CombatSales: 0, IntermissionSales: 1 },
                "replacement branches verify independently from their full normal-start prefix");
            Check.Equal(branch.Signature, PlannerSession.Revalidate(content, branch.Capture()).Signature,
                "replacement checkpoints authenticate through sequential prefix reconstruction");
            branch.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
            branch.Advance(240);
        }
        Check.Equal(direct.Signature, restored.Signature, "replacement branches have identical combat futures");
    }

    private static void IntermissionOnlySelling(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = "foundry_loop", DifficultyId = "easy" };
        var restricted = scenario with { ForbidCombatSales = true };
        Check.True(scenario.Key != restricted.Key &&
            !JsonSerializer.Serialize(scenario, PlannerStore.Json).Contains("forbidCombatSales", StringComparison.Ordinal),
            "only restricted scenarios add a sale policy to the persisted identity");

        foreach (var forge in new[] { false, true })
        {
            var original = PlannerSession.Create(content, scenario);
            var position = forge ? new Vector2(50, 200) : BestNeedlePosition(original.Session);
            var purchase = new GameCommand { PlayerId = 1, Type = forge ? GameCommandType.PlaceGenerator : GameCommandType.PlaceTower,
                TowerDefinitionId = forge ? "" : "needle_turret", X = position.X, Y = position.Y };
            Check.True(original.Apply(purchase).Accepted, "buy the asset through normal placement rules");
            var sale = new GameCommand { PlayerId = 1, Type = forge ? GameCommandType.SellGenerator : GameCommandType.SellTower,
                EntityId = forge ? 0 : original.Session.Towers.Single().Id };
            original.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
            original.Advance(60);
            var source = original.Capture();
            var converted = PlannerSession.ForbidCombatSales(content,
                source with { BuildHash = new string('0', 64), SnapshotJson = "untrusted cache" });
            var branch = PlannerSession.Restore(content, converted);
            Check.True(branch.Scenario == restricted && !source.Scenario.ForbidCombatSales &&
                converted.BuildHash == PlanningFingerprint.Build() && branch.Signature == source.StateSignature,
                "a legal prefix acquires the restriction without changing the source or simulation state");
            Check.True(!branch.Apply(sale).Accepted && branch.Signature == source.StateSignature,
                "selling an asset in combat leaves the restricted branch unchanged");

            Check.True(original.Apply(sale).Accepted, "ordinary selling during combat remains legal");
            var originalTrace = original.ExportTrace();
            Check.True(PlanningReplay.Verify(content, originalTrace) is { Verified: true, CombatSales: 1, IntermissionSales: 0 },
                "unrestricted replay counts the actual active-wave sale");
            var mislabeled = originalTrace with { Scenario = restricted, Actions = originalTrace.Actions.Select(action =>
                action.Command.Type == sale.Type ? action with { DuringWave = false } : action).ToArray() };
            var verification = PlanningReplay.Verify(content, mislabeled);
            Check.True(!verification.Verified && verification.Reason.Contains("between waves", StringComparison.Ordinal),
                "replay uses the simulation phase even when the sale record claims an intermission");
            Check.Throws<InvalidDataException>(() => PlannerSession.ForbidCombatSales(content, original.Capture()),
                "a restricted continuation cannot inherit an earlier combat sale");
            Check.Throws<InvalidDataException>(() => PlannerSession.Restore(content, original.Capture() with { Scenario = restricted }),
                "a cached board cannot hide a recorded forbidden sale");

            while (branch.Session.Waves.IsActive && !branch.Session.IsDefeat && branch.Tick < 18_000)
                branch.Advance(1);
            Check.True(!branch.Session.Waves.IsActive && !branch.Session.IsDefeat && branch.Apply(sale).Accepted,
                "the same asset can be sold in the intermission after the wave");
            Check.True(PlanningReplay.Verify(content, branch.ExportTrace()) is { Verified: true, CombatSales: 0, IntermissionSales: 1 },
                "restricted replay certifies the legal between-wave sale");
        }
    }

    private static void CrossBuildPrefixRevalidation(GameContent content)
    {
        var adapter = PlannerSession.Create(content, new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" });
        var position = BestNeedlePosition(adapter.Session);
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y });
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
        adapter.Advance(180);
        var checkpoint = adapter.Capture() with { BuildHash = new string('0', 64), SnapshotJson = "untrusted cache" };
        var refused = false;
        try { PlannerSession.Restore(content, checkpoint); }
        catch (InvalidDataException) { refused = true; }
        Check.True(refused, "ordinary restore refuses a different executable");
        var revalidated = PlannerSession.Revalidate(content, checkpoint);
        Check.Equal(adapter.Signature, revalidated.Signature, "cross-build revalidation reconstructs the exact legal prefix");
        Check.Equal(PlanningFingerprint.Build(), revalidated.Capture().BuildHash, "revalidated artifact identifies the current build");
        Check.Equal(new string('0', 64), checkpoint.BuildHash, "source artifact identity remains unchanged");
        foreach (var changed in new[]
        {
            checkpoint with { ContentHash = new string('0', 64) },
            checkpoint with { Actions = checkpoint.Actions.Select((action, index) => index == 0
                ? action with { ActualCost = action.ActualCost + 1 } : action).ToArray() },
            checkpoint with { StateSignature = new string('0', 64) }
        })
        {
            refused = false;
            try { PlannerSession.Revalidate(content, changed); }
            catch (InvalidDataException) { refused = true; }
            Check.True(refused, "revalidation rejects changed content, action costs or final state");
        }
    }

    private static void SnapshotContinuation(GameContent content, PlanningScenario scenario)
    {
        var adapter = PlannerSession.Create(content, scenario);
        var position = BestNeedlePosition(adapter.Session);
        Check.True(adapter.Apply(new GameCommand
        {
            PlayerId = 1, Type = GameCommandType.PlaceTower, TowerDefinitionId = "needle_turret",
            X = position.X, Y = position.Y
        }).Accepted, "adapter places through player rules");
        Check.True(adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave }).Accepted,
            "adapter starts the authored wave");
        adapter.Advance(173);
        var checkpoint = adapter.Capture();
        var serialized = JsonSerializer.Serialize(checkpoint, PlanningFingerprint.JsonOptions);
        var transferred = JsonSerializer.Deserialize<PlanningCheckpoint>(serialized, PlanningFingerprint.JsonOptions)!;
        var branch = PlannerSession.Restore(content, transferred);
        Check.Equal(adapter.Signature, branch.Signature, "snapshot restores complete active state");
        Check.True(!ReferenceEquals(adapter.Session.Towers[0], branch.Session.Towers[0]), "fork towers are independent");
        Check.True(adapter.Session.Enemies.Count == 0 ||
            !ReferenceEquals(adapter.Session.Enemies[0], branch.Session.Enemies[0]), "fork enemies are independent");
        adapter.Advance(421);
        branch.Advance(421);
        Check.Equal(adapter.Signature, branch.Signature, "restored active state has identical future");
        var unchanged = adapter.Signature;
        branch.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetTargetMode,
            EntityId = branch.Session.Towers[0].Id, TargetMode = TargetMode.Last });
        branch.Advance(1);
        Check.Equal(unchanged, adapter.Signature, "branch edits and stepping cannot mutate the source");
        Check.Equal(checkpoint.StateSignature, PlannerSession.Restore(content, checkpoint).Signature,
            "a serialized checkpoint can be restored repeatedly");
        var verification = PlanningReplay.Verify(content, adapter.ExportTrace());
        Check.True(verification.Verified && !verification.VerifiedWin,
            "independent replay verifies active partial traces without claiming victory: " + verification.Reason);
    }

    private static void ReplayValidation(GameContent source)
    {
        var map = source.Maps.Values.First();
        var waves = new WaveSetDefinition { Waves = new List<WaveDefinition>
        {
            new() { Number = 1, HealthMultiplier = .1f, SpeedMultiplier = 1,
                Groups = new List<WaveGroupDefinition>
                { new() { EnemyId = source.Enemies.Keys.First(), Count = 1, SpawnInterval = .5f } } }
        } };
        // The finite fixture has its own content hash and cannot certify an authored campaign.
        var content = new GameContent
        {
            Towers = source.Towers, Enemies = source.Enemies, Map = map, Waves = waves,
            Maps = new Dictionary<string, MapDefinition> { [map.Id] = map },
            WaveSets = new Dictionary<string, WaveSetDefinition> { [map.WaveSet] = waves },
            Difficulties = source.Difficulties, Challenges = source.Challenges, Tactics = source.Tactics
        };
        var scenario = new PlanningScenario { MapId = map.Id, DifficultyId = "easy", Horizon = 1 };
        var adapter = PlannerSession.Create(content, scenario);
        Check.True(adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave }).Accepted,
            "start finite replay fixture");
        for (var ticks = 0; ticks < 36000 && !adapter.Session.IsVictory && !adapter.Session.IsDefeat; ticks++)
            adapter.Advance(1);
        Check.True(adapter.CampaignVictory, "actual finite-fixture terminal condition reached");
        var trace = adapter.ExportTrace();
        var verified = PlanningReplay.Verify(content, trace);
        Check.True(verified.Verified && verified.VerifiedWin && verified.FromNormalStart,
            "terminal legal replay independently verified: " + verified.Reason);
        Check.True(!PlanningReplay.Verify(source, trace).Verified, "fixture cannot verify against production content");
        var corrupted = trace with { Actions = trace.Actions.Select((action, index) =>
            index == 0 ? action with { ActualCost = action.ActualCost + 1 } : action).ToArray() };
        Check.True(!PlanningReplay.Verify(content, corrupted).Verified, "replay verifies actual costs");
        Check.True(!PlanningReplay.Verify(content, trace with { FinalSignature = "incorrect" }).Verified,
            "replay verifies terminal state signature");
        Check.True(!PlanningReplay.Verify(content, trace with { BuildHash = "incorrect" }).Verified,
            "replay rejects incompatible executable");
        var experimentDirectory = Path.Combine(Path.GetTempPath(), "MaximalBastionTraceTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var combatSource = PlannerSession.Create(content, scenario);
            var position = BestNeedlePosition(combatSource.Session);
            combatSource.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y });
            combatSource.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
            combatSource.Advance(10);
            combatSource.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetTargetMode,
                EntityId = combatSource.Session.Towers[0].Id, TargetMode = TargetMode.Last });
            combatSource.Advance(10);
            combatSource.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetTargetMode,
                EntityId = combatSource.Session.Towers[0].Id, TargetMode = TargetMode.First });
            while (!combatSource.Session.IsVictory && !combatSource.Session.IsDefeat && combatSource.Tick < 36000)
                combatSource.Advance(1);
            var experiments = PlanningTraceExperiments.Run(content, combatSource.ExportTrace(), experimentDirectory,
                new[] { 0, 1 }, maximumRemovals: 2, maximumRollouts: 4, maximumWallSeconds: 10);
            Check.Equal(4, experiments.Experiments.Count, "fixed-trace delay experiment budget is explicit");
            Check.True(experiments.Experiments.All(experiment => experiment.Verification?.VerifiedWin == true),
                "each finite-fixture execution is independently replay-verified");
            Check.Equal(2, experiments.SourceCombatActions, "trace experiments count actual combat interventions");
            Check.Equal(2, experiments.SuccessfulRemovals.Count, "unnecessary target changes are independently removable");
            Check.True(File.Exists(Path.Combine(experimentDirectory, "trace-experiments.json")),
                "trace experiments emit structured results");
        }
        finally { if (Directory.Exists(experimentDirectory)) Directory.Delete(experimentDirectory, true); }

        var invalid = PlannerSession.Create(content, scenario);
        var origin = invalid.Signature;
        Check.True(!invalid.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "unknown", X = 0, Y = 0 }).Accepted, "illegal placement rejected");
        Check.Equal(origin, invalid.Signature, "rejected placement changes no state");
        Check.True(!invalid.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave,
            EarlyStartEligible = true }).Accepted, "search cannot invent early-start eligibility");
        Check.Equal(2, invalid.Capture().Actions.Count, "rejected attempts remain diagnostic records");
        Check.Equal(0, invalid.ExportTrace().Actions.Count, "witness contains only executed legal actions");
        Check.True(PlanningReplay.Verify(content, invalid.ExportTrace()).Verified,
            "removing no-op rejects still replays exact state");
    }

    private static void CommandBoundaryAgreement(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" };
        var adapter = PlannerSession.Create(content, scenario);
        var normal = new GameSession(content, scenario.MapId, scenario.DifficultyId, scenario.ChallengeId);
        var runner = new DeterministicSessionRunner(normal);
        var position = BestNeedlePosition(adapter.Session);
        var commands = new[]
        {
            new GameCommand { Sequence = 1, PlayerId = 1, Type = GameCommandType.PlaceTower,
                TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y },
            new GameCommand { Sequence = 2, PlayerId = 1, Type = GameCommandType.StartWave }
        };
        foreach (var command in commands)
        {
            Check.True(adapter.Apply(command).Accepted, "adapter accepts scheduled comparison command");
            Check.True(runner.Schedule(0, command), "normal deterministic entry queues comparison command");
        }
        adapter.Advance(900);
        runner.RunTicks(900);
        Check.Equal(PlanningFingerprint.State(normal, 900), adapter.Signature,
            "normal command scheduling and adapter agree after 900 fixed ticks");
    }

    private static void ImportedOriginAndPause(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" };
        var original = PlannerSession.Create(content, scenario);
        var imported = PlannerSession.FromSave(content, original.Session.CaptureSaveGame(),
            scenario with { StartingProfile = "imported" });
        var verification = PlanningReplay.Verify(content, imported.ExportTrace());
        Check.True(verification.Verified && !verification.FromNormalStart && !verification.VerifiedWin,
            "imported replay keeps its reachability claim separate: " + verification.Reason);
        var coop = PlannerSession.Create(content, scenario with { CoOp = true });
        Check.True(coop.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetPaused,
            Paused = true }).Accepted, "co-op pause remains a legal command");
        coop.Advance(120);
        var paused = PlannerSession.Restore(content, coop.Capture());
        Check.True(paused.Session.IsCoOpPaused && !paused.CampaignVictory && !paused.HorizonSurvived,
            "paused state restores without a survival or victory claim");
        Check.True(PlanningReplay.Verify(content, paused.ExportTrace()).Verified,
            "normal replay preserves legal pause behavior");
    }

    private static void DetachedTargetSnapshot(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" };
        var adapter = PlannerSession.Create(content, scenario);
        var session = adapter.Session;
        // This runtime fixture isolates the end-of-update lifetime of projectile targets.
        session.SpawnEnemy(content.Enemies.Keys.First(), 1, 1);
        var target = session.Enemies.Single();
        session.Projectiles.Add(new ProjectileInstance(target.Position + new Vector2(300, 0), target.Position,
            target, 1, ProjectileKind.Homing, 0, new DamagePayload { Damage = 1 }, Color.White, 2));
        session.Projectiles.Add(new ProjectileInstance(target.Position, target.Position,
            target, 1000, ProjectileKind.Homing, 0,
            new DamagePayload { Damage = 100000, IgnoreShield = true, ArmorPierce = 100000 }, Color.White, 2));
        adapter.Advance(1);
        Check.True(session.Enemies.Count == 0 && session.Projectiles.Projectiles.Count == 1 &&
            session.Projectiles.Projectiles[0].Target?.IsDead == true,
            "a later projectile can kill a target still referenced by an earlier projectile");
        var checkpoint = adapter.Capture();
        Check.Throws<InvalidDataException>(() => GameSession.RestoreCoOpState(content,
            JsonSerializer.Deserialize<CoOpStateSnapshot>(checkpoint.SnapshotJson, PlanningFingerprint.JsonOptions)!, 1),
            "native network snapshot alone cannot restore an omitted projectile target");
        var restored = PlannerSession.Restore(content, checkpoint);
        Check.Equal("snapshot-with-detached-projectile-targets", restored.RestorationMethod,
            "solver checkpoint restores the detached target without changing the game snapshot format");
        Check.Equal(adapter.Signature, restored.Signature, "detached target state participates in exact signatures");
        Check.Equal(0, restored.Session.Enemies.Count, "restoring a projectile reference does not revive its target");
        adapter.Advance(2);
        restored.Advance(2);
        Check.Equal(adapter.Signature, restored.Signature, "detached target restoration has identical continuation");
        Check.True(!PlanningReplay.Verify(content, adapter.ExportTrace()).Verified,
            "an injected runtime fixture cannot masquerade as a legal fresh-start replay");
    }

    private static void LegalPrefixRecovery(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" };
        var adapter = PlannerSession.Create(content, scenario);
        var position = BestNeedlePosition(adapter.Session);
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y });
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
        adapter.Advance(731);
        var checkpoint = adapter.Capture();
        var restored = PlannerSession.Restore(content, checkpoint with { SnapshotJson = "invalid acceleration cache" });
        Check.Equal("legal-prefix-reconstruction", restored.RestorationMethod,
            "replay prefix recovers a checkpoint when the acceleration snapshot is unavailable");
        Check.Equal(adapter.Signature, restored.Signature, "prefix reconstruction reproduces the exact checkpoint");
        adapter.Advance(123);
        restored.Advance(123);
        Check.Equal(adapter.Signature, restored.Signature, "reconstructed checkpoint continuation is deterministic");
    }

    private static void ExecutionProfileVerification(GameContent content)
    {
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy", ExecutionProfile = "informed" };
        var adapter = PlannerSession.Create(content, scenario);
        var position = BestNeedlePosition(adapter.Session);
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.PlaceTower,
            TowerDefinitionId = "needle_turret", X = position.X, Y = position.Y });
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave });
        adapter.Advance(30);
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetTargetMode,
            EntityId = adapter.Session.Towers[0].Id, TargetMode = TargetMode.Last }, requestedTick: 0);
        adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.SetTargetMode,
            EntityId = adapter.Session.Towers[0].Id, TargetMode = TargetMode.First }, requestedTick: 0);
        var rejected = PlanningReplay.Verify(content, adapter.ExportTrace());
        Check.True(!rejected.Verified && rejected.Reason.Contains("action rate", StringComparison.Ordinal),
            "legal commands cannot certify a schedule exceeding the declared execution profile");
        Check.True(!PlanningReplay.Verify(content, adapter.ExportTrace() with
            { Scenario = scenario with { ExecutionProfile = "unknown" } }).Verified,
            "unknown execution profiles cannot be certified");
        var inadequateReaction = adapter.ExportTrace() with
        {
            Actions = adapter.ExportTrace().Actions.Take(3).Select(action =>
                action.DuringWave ? action with { RequestedTick = action.Tick } : action).ToArray()
        };
        var rejectedReaction = PlanningReplay.Verify(content, inadequateReaction);
        Check.True(!rejectedReaction.Verified && rejectedReaction.Reason.Contains("reaction delay", StringComparison.Ordinal),
            "an informed schedule must record enough time between observation and action");
    }

    private static Vector2 BestNeedlePosition(GameSession session)
    {
        var candidates = new List<(Vector2 Position, int Exposure)>();
        for (var x = 24; x < session.Map.Definition.LogicalSize.Width; x += 24)
        for (var y = 24; y < session.Map.Definition.LogicalSize.Height; y += 24)
        {
            var position = new Vector2(x, y);
            if (session.ValidatePlacement("needle_turret", position) != PlacementFailure.None) continue;
            var range = session.Content.Towers["needle_turret"].Levels[0].Range;
            var exposure = Enumerable.Range(0, 100).Count(index =>
                Vector2.Distance(position, session.Map.Path.GetPosition(session.Map.Path.TotalLength * index / 100)) < range);
            candidates.Add((position, exposure));
        }
        return candidates.OrderByDescending(candidate => candidate.Exposure).First().Position;
    }
}
