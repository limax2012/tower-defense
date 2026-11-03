using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private sealed record EnemyStateFixture(string Id, string Title, string Description, GameSession Session,
        EnemyInstance Subject, EnemyInstance? Carrier, TowerInstance? Tower, Action<int> BeforeFrame);

    private void CaptureEnemyStateEvidence(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        _renderer.QuietEnemyHud = variant == "C";
        _renderer.ReducedEffects = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LANE_REDUCED") == "1";
        var fixtures = BuildEnemyStateFixtures(content);
        var renderers = fixtures.Select(_ => new GameRenderer
        {
            QuietEnemyHud = _renderer.QuietEnemyHud,
            ReducedEffects = _renderer.ReducedEffects
        }).ToArray();
        foreach (var renderer in renderers) ApplyEnemyEvidenceOptions(renderer);
        var interfaces = fixtures.Select(fixture =>
        {
            var ui = new UIManager(font, display);
            ConfigureUi(ui, fixture.Session.Content);
            ui.HandleGameplayInput(Pointer(0, 0), fixture.Session);
            return ui;
        }).ToArray();
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "state-truth"));
        foreach (var fixture in fixtures)
            Directory.CreateDirectory(Path.Combine(_outputDirectory, "state-truth-native", fixture.Id));
        var frames = new List<object>();
        var assertions = new List<string>();
        var originalRenderer = _renderer;
        try
        {
            for (var frame = 0; frame < EnemyLaneFrameCount; frame++)
            {
                var nativeFrames = new List<RenderTarget2D>();
                var crops = new List<Rectangle>();
                var samples = new List<object>();
                try
                {
                    for (var index = 0; index < fixtures.Count; index++)
                    {
                        var fixture = fixtures[index];
                        var session = fixture.Session;
                        fixture.BeforeFrame(frame);
                        ValidateEnemyStateMoment(fixture, frame, assertions);
                        var checksum = SessionChecksum.Compute(session, 270 + frame * 2);
                        _renderer = renderers[index];
                        using var scene = RenderScene(interfaces[index], GameState.Playing, session);
                        var enemyGlyphs = _renderer.LastEnemyGlyphCount;
                        var enemyHealthBars = _renderer.LastEnemyHealthBarCount;
                        var enemyShieldBars = _renderer.LastEnemyShieldBarCount;
                        var native = DownsampleEnemyLaneScene(scene);
                        nativeFrames.Add(native);
                        var center = fixture.Tower is { } tower ? tower.Position + new Vector2(20, 45) :
                            fixture.Carrier is { } carrier ? (carrier.Position + fixture.Subject.Position) * .5f :
                            fixture.Subject.Position;
                        var crop = new Rectangle(Math.Clamp((int)center.X - 200, 0, GameConstants.MapWidth - 400),
                            Math.Clamp((int)center.Y - 90, 60, GameConstants.LogicalHeight - 190), 400, 180);
                        crops.Add(crop);
                        if (frame is 0 or 44 or 45 or 59 or 60 or 61 or 75 or 90 or 120 or 150 or 210 or 239)
                            SaveTierScene(native, $"state-truth-native/{fixture.Id}/frame-{frame:000}.png");
                        Require(checksum == SessionChecksum.Compute(session, 270 + frame * 2),
                            $"{fixture.Id} frame {frame}: native state-truth rendering preserves the simulation checksum.", assertions);
                        samples.Add(new
                        {
                            fixture.Id, checksum, crop = new { crop.X, crop.Y, crop.Width, crop.Height },
                            enemyGlyphs, enemyHealthBars, enemyShieldBars,
                            simulatedSeconds = session.Statistics.SimulatedSeconds,
                            enemies = session.Enemies.Select(enemy => new
                            {
                                enemy.Id, family = enemy.Definition.Id, signal = enemy.SignalRole.ToString(),
                                enemy.Health, enemy.MaxHealth, enemy.Shield, enemy.DamagePauseTimer,
                                enemy.BossPhaseActive, enemy.FormationSpeedMultiplier,
                                x = enemy.Position.X, y = enemy.Position.Y,
                                conditions = enemy.StatusEffects.Active.Select(status => status.Type.ToString()).ToArray()
                            }).ToArray(),
                            towers = session.Towers.Select(tower => new
                            {
                                tower.Id, tower.DisruptionRemaining, tower.SuppressionRemaining,
                                tower.DisruptionLockoutRemaining, tower.SuppressionLockoutRemaining
                            }).ToArray(),
                            transfers = session.Effects.Effects.Where(effect => effect.Signal.IsKnown).Select(effect => new
                            {
                                role = effect.Signal.Role.ToString(), effect.Signal.SourceEnemyId,
                                effect.Signal.TargetEnemyId, effect.Signal.TargetTowerId, effect.Remaining
                            }).ToArray()
                        });
                    }
                    using var board = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color,
                        DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                    GraphicsDevice.SetRenderTarget(board);
                    GraphicsDevice.Clear(ColorPalette.Canvas);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                    StatusLabel(font, $"STATE TRUTH / NATIVE FIELD SCALE / {frame / 30f:0.00}s / {(_renderer.ReducedEffects ? "REDUCED" : "FULL")}",
                        new Vector2(20, 10), .62f);
                    for (var index = 0; index < fixtures.Count; index++)
                    {
                        var x = 20 + index % 3 * 420;
                        var y = 42 + index / 3 * 216;
                        StatusLabel(font, fixtures[index].Title, new Vector2(x, y), .52f);
                        _batch.Draw(nativeFrames[index], new Rectangle(x, y + 22, 400, 180), crops[index], Color.White);
                    }
                    StatusLabel(font, "UNSCALED GAMEPLAY CROPS", new Vector2(860, 505), .55f);
                    StatusLabel(font, "Actual signal recipients and transitions", new Vector2(860, 535), .46f);
                    StatusLabel(font, "Carrier hardware persists between events", new Vector2(860, 560), .46f);
                    StatusLabel(font, "Suppression / shutdown / cleanup", new Vector2(860, 585), .46f);
                    StatusLabel(font, "Controlled fixtures; actual simulation", new Vector2(860, 625), .46f);
                    StatusLabel(font, "Native keyframes and state log saved", new Vector2(860, 650), .46f);
                    _batch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    var saved = SaveTierScene(board, $"state-truth/frame-{frame:000}.png");
                    frames.Add(new { frame, seconds = frame / 30f, saved.File, saved.Sha256, samples });
                }
                finally { foreach (var native in nativeFrames) native.Dispose(); }
                foreach (var fixture in fixtures)
                    for (var step = 0; step < 2; step++) fixture.Session.Update(1f / 60);
                foreach (var ui in interfaces) ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer = originalRenderer; }
        ValidateEnemyStateEvidence(fixtures, assertions);
        File.WriteAllText(Path.Combine(_outputDirectory, "state-truth-manifest.json"), JsonSerializer.Serialize(new
        {
            variant, reducedEffects = _renderer.ReducedEffects, fps = 30, seconds = 8, fixedSimulationHz = 60,
            richMaterialRequested = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICH_MATERIALS") == "1",
            fixtureMovementMultiplier = .25f,
            resolution = "1280x720 board; each 400x180 field crop copied at 1:1 from normal 2560x1440→1280x720 output.",
            fixtures = fixtures.Select(fixture => new { fixture.Id, fixture.Title, fixture.Description }).ToArray(),
            assertions, frames
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Enemy state evidence: 240 frames, eight independent simulation fixtures, {assertions.Count} assertions.");
    }

    private static List<EnemyStateFixture> BuildEnemyStateFixtures(GameContent content)
    {
        var fixtures = new List<EnemyStateFixture>();
        var aegis = NewSession();
        var shielded = Spawn(aegis, "t4_aegis", 450);
        var bulwark = Spawn(aegis, "t1_crawler", 390, EnemySignalRole.Bulwark);
        Warm(aegis);
        bulwark.ArmSignalAbility(4);
        fixtures.Add(new("aegis-grant", "AEGIS / DEPLETION THEN BULWARK GRANT",
            "Standard Aegis loses exactly its initial shield at 1.5s. A nearby Bulwark activates at 4s through the unchanged signal system; the Aegis does not self-recharge.",
            aegis, shielded, bulwark, null, frame =>
            {
                if (frame == 45) aegis.DamageResolver.Apply(shielded, new DamagePayload { Damage = shielded.Shield });
            }));

        var repair = NewSession();
        var regenerator = Spawn(repair, "t5_regenerator", 400);
        Warm(repair);
        repair.DamageResolver.Apply(regenerator, new DamagePayload { Damage = 240, ArmorPierce = 1000, IgnoreShield = true });
        regenerator.TickRuntimeTimers(1.1f);
        regenerator.GrantShield(200, 200);
        regenerator.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Duration = 7.5f, Magnitude = 2, ArmorPierce = 4 });
        fixtures.Add(new("repair-pause", "REGENERATOR / REPAIR, PAUSE, RESUME",
            "Controlled damaged Regenerator with granted shield and Burn. Shield absorbs periodic Burn hits so real repair can run. Direct health hits at 2s and 5s cause the real one-second damage pause; Burn remains independently active.",
            repair, regenerator, null, null, frame =>
            {
                if (frame is 60 or 150) repair.DamageResolver.Apply(regenerator,
                    new DamagePayload { Damage = 35, ArmorPierce = 1000, IgnoreShield = true });
            }));

        var boss = NewSession();
        boss.SpawnEnemy("t3_brute", 1, .25f, "Boss");
        var commander = boss.Enemies[0];
        commander.SetSandboxPathDistance(400, boss.Map.Path);
        Warm(boss);
        commander.AbsorbShield(commander.Shield);
        boss.DamageResolver.Apply(commander, new DamagePayload { Damage = commander.MaxHealth * .49f, ArmorPierce = 1000, IgnoreShield = true });
        fixtures.Add(new("boss-overdrive", "BOSS / 51%, ONSET, SETTLED OVERDRIVE",
            "Boss Brute starts at 51% health with depleted shield. A real DamageResolver hit at 2s crosses 49%, activating its actual phase flag and reserve shield. Model panels observe that flag.",
            boss, commander, null, null, frame =>
            {
                if (frame == 60) boss.DamageResolver.Apply(commander,
                    new DamagePayload { Damage = commander.MaxHealth * .02f, ArmorPierce = 1000, IgnoreShield = true });
            }));

        var acceleration = NewSession();
        var accelerator = Spawn(acceleration, "t1_crawler", 480, EnemySignalRole.Accelerator);
        var accelerated = Spawn(acceleration, "t2_runner", 430);
        Warm(acceleration);
        accelerator.SetSandboxPathDistance(480, acceleration.Map.Path);
        accelerated.SetSandboxPathDistance(430, acceleration.Map.Path);
        acceleration.RefreshEnemySignalFormation();
        fixtures.Add(new("acceleration", "ACCELERATOR / CARRIER AND RECIPIENT",
            "The source and recipient move at quarter speed for a controlled view. The real formation system boosts only the other eligible unit; source remains 1.0 and recipient leaves coverage naturally.",
            acceleration, accelerated, accelerator, null, _ => { }));

        var restoration = NewSession();
        var restorer = Spawn(restoration, "t1_crawler", 480, EnemySignalRole.Restorer);
        var restored = Spawn(restoration, "t3_brute", 420);
        var fullNeighbor = Spawn(restoration, "t1_crawler", 540);
        Warm(restoration);
        restorer.SetSandboxPathDistance(480, restoration.Map.Path);
        restored.SetSandboxPathDistance(420, restoration.Map.Path);
        fullNeighbor.SetSandboxPathDistance(540, restoration.Map.Path);
        restoration.DamageResolver.Apply(restored, new DamagePayload { Damage = 100, ArmorPierce = 1000, IgnoreShield = true });
        restorer.ArmSignalAbility(3);
        fixtures.Add(new("restorer-transfer", "RESTORER / IDLE, TRANSFER, RECIPIENT",
            "The real Restorer activates at 3s. Its damaged Brute receives healing; a full-health neighbor receives no transfer. No permanent self-regeneration condition is assigned to the Brute.",
            restoration, restored, restorer, null, _ => { }));

        foreach (var role in new[] { EnemySignalRole.Jammer, EnemySignalRole.Disruptor })
        {
            var interference = NewSession();
            if (!interference.TryPlaceTower("needle_turret", new Vector2(220, 185)))
                throw new InvalidOperationException("Cannot place the state-evidence Needle tower.");
            var tower = interference.SelectedTower!;
            interference.CancelPlacement();
            interference.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            var source = Spawn(interference, "t1_crawler", 400, role, 100);
            Warm(interference);
            source.SetSandboxPathDistance(400, interference.Map.Path);
            source.ArmSignalAbility(2);
            fixtures.Add(new(role.ToString().ToLowerInvariant(), role == EnemySignalRole.Jammer ?
                    "JAMMER / FIRING UNDER SUPPRESSION" : "DISRUPTOR / SHUTDOWN AND RECOVERY",
                "A legal Needle emplacement targets a high-health Standard carrier. The carrier's real signal activates at 2s and preserves authored target choice, immunity windows and duration; the tower's existing artwork is unchanged.",
                interference, source, null, tower, _ => { }));
        }
        var cleanup = NewSession();
        var doomed = Spawn(cleanup, "t1_crawler", 400);
        Warm(cleanup);
        doomed.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Duration = 8, Magnitude = 2, ArmorPierce = 4 });
        doomed.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 8, Magnitude = .35f });
        fixtures.Add(new("cleanup", "LIFECYCLE / DEATH, FRESH INSTANCE, ESCAPE",
            "A burning/slowed Crawler dies from real damage at 4s and is removed normally. A new Crawler instance spawns at 5s without conditions. At 7s it is positioned at the path exit and escapes through the normal update. Enemies are allocated as new instances; no enemy pool exists.",
            cleanup, doomed, null, null, frame =>
            {
                if (frame == 120) cleanup.DamageResolver.Apply(doomed,
                    new DamagePayload { Damage = doomed.MaxHealth * 2, ArmorPierce = 1000, IgnoreShield = true });
                if (frame == 150)
                {
                    var fresh = Spawn(cleanup, "t1_crawler", doomed.DistanceAlongPath);
                    if (fresh.Id == doomed.Id || fresh.StatusEffects.Active.Count != 0)
                        throw new InvalidOperationException("A fresh enemy inherited the previous instance's identity or conditions.");
                }
                if (frame == 210 && cleanup.Enemies.Count > 0)
                    cleanup.Enemies[0].SetSandboxPathDistance(cleanup.Map.Path.TotalLength - .1f, cleanup.Map.Path);
            }));
        return fixtures;

        GameSession NewSession() => new(content, "foundry_loop", "normal", "sandbox_lab");
        static EnemyInstance Spawn(GameSession session, string family, float distance,
            EnemySignalRole role = EnemySignalRole.None, float health = 1)
        {
            session.SpawnEnemy(family, health, .25f, signalRole: role);
            var enemy = session.Enemies[^1];
            enemy.SetSandboxPathDistance(distance, session.Map.Path);
            enemy.ArmSignalAbility(100);
            return enemy;
        }
        static void Warm(GameSession session)
        {
            for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
            if (session.AnnouncementRemaining > 0)
                throw new InvalidOperationException("The state fixture's announcement pre-roll has not elapsed.");
        }
    }

    private static void ValidateEnemyStateMoment(EnemyStateFixture fixture, int frame, ICollection<string> assertions)
    {
        var subject = fixture.Subject;
        switch (fixture.Id)
        {
            case "aegis-grant" when frame is 45 or 119:
                Require(subject.Shield == 0 && subject.Health == subject.MaxHealth,
                    $"Aegis frame {frame}: depletion removes its barrier without self-recharge or health damage.", assertions);
                break;
            case "aegis-grant" when frame == 122:
                Require(subject.Shield > 0 && fixture.Session.Effects.Effects.Any(effect =>
                        effect.Signal.Role == EnemySignalRole.Bulwark && effect.Signal.TargetEnemyId == subject.Id),
                    "Aegis frame 122: its incoming shield comes from a real Bulwark transfer.", assertions);
                break;
            case "repair-pause" when frame is 0 or 91 or 181:
                Require(subject.DamagePauseTimer <= 0 && subject.Health < subject.MaxHealth && subject.StatusEffects.IsBurning,
                    $"Regenerator frame {frame}: repair is active while independent Burn remains present.", assertions);
                break;
            case "repair-pause" when frame is 60 or 150:
                Require(subject.DamagePauseTimer > 0 && subject.StatusEffects.IsBurning,
                    $"Regenerator frame {frame}: real health damage pauses repair while Burn remains present.", assertions);
                break;
            case "boss-overdrive" when frame is 59 or 60:
                Require(subject.BossPhaseActive == (frame == 60),
                    $"Boss frame {frame}: phase identity follows the authoritative flag across the actual threshold hit.", assertions);
                break;
            case "acceleration" when frame == 0:
                Require(fixture.Carrier!.FormationSpeedMultiplier == 1 && subject.FormationSpeedMultiplier > 1,
                    "The initial acceleration reaches the recipient and leaves its sole source unboosted.", assertions);
                break;
            case "restorer-transfer" when frame == 92:
                Require(fixture.Session.Effects.Effects.Any(effect => effect.Signal.Role == EnemySignalRole.Restorer &&
                        effect.Signal.TargetEnemyId == subject.Id) &&
                        fixture.Session.Effects.Effects.All(effect => !effect.Signal.IsKnown ||
                            effect.Signal.TargetEnemyId == 0 || effect.Signal.TargetEnemyId == subject.Id),
                    "The Restorer transfers only to the damaged Brute, leaving full-health neighbors without incoming repair cues.", assertions);
                break;
            case "jammer" when frame == 62:
                Require(fixture.Tower!.IsSuppressed && !fixture.Tower.IsDisrupted,
                    "Jammer frame 62: the tower is suppressed, not shut down.", assertions);
                break;
            case "disruptor" when frame == 62:
                Require(fixture.Tower!.IsDisrupted && !fixture.Tower.IsSuppressed,
                    "Disruptor frame 62: the selected target is shut down, not suppressed.", assertions);
                break;
            case "cleanup" when frame is 122 or 212:
                Require(fixture.Session.Enemies.Count == 0,
                    $"Lifecycle frame {frame}: the normal update removes the dead or escaped enemy.", assertions);
                break;
            case "cleanup" when frame == 152:
                Require(fixture.Session.Enemies.Count == 1 && fixture.Session.Enemies[0].Id != subject.Id &&
                        fixture.Session.Enemies[0].StatusEffects.Active.Count == 0,
                    "Lifecycle frame 152: a fresh allocated enemy has a new identity and no inherited condition state.", assertions);
                break;
        }
    }

    private static void ValidateEnemyStateEvidence(IReadOnlyList<EnemyStateFixture> fixtures, ICollection<string> assertions)
    {
        var aegis = fixtures.First(fixture => fixture.Id == "aegis-grant");
        Require(aegis.Subject.Shield > 0 && aegis.Subject.Health == aegis.Subject.MaxHealth,
            "The depleted Aegis receives shield through the Bulwark without receiving health damage.", assertions);
        var boss = fixtures.First(fixture => fixture.Id == "boss-overdrive");
        Require(boss.Subject.BossPhaseActive && MathF.Abs(boss.Subject.Health / boss.Subject.MaxHealth - .49f) < .001f,
            "The Boss settles in the real phase at 49% health.", assertions);
        var restoration = fixtures.First(fixture => fixture.Id == "restorer-transfer");
        Require(restoration.Subject.Health > restoration.Subject.MaxHealth - 100,
            "The Restorer heals the damaged recipient through the actual signal system.", assertions);
        var acceleration = fixtures.First(fixture => fixture.Id == "acceleration");
        Require(acceleration.Carrier!.FormationSpeedMultiplier == 1,
            "The Accelerator never boosts itself merely by being the source.", assertions);
    }
}
