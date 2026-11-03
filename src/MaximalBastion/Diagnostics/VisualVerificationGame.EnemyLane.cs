using System.Diagnostics;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.Simulation;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private const int EnemyLaneFramesPerSecond = 30;
    private const int EnemyLaneFrameCount = 240;

    private void CaptureEnemyLanePilot(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        ApplyEnemyEvidenceOptions(_renderer);
        _renderer.QuietEnemyHud = variant == "C";
        _renderer.ReducedEffects = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LANE_REDUCED") == "1";
        var captures = new List<object>();
        foreach (var mapId in new[] { "foundry_loop", "crosswind_basin" })
        {
            var session = new GameSession(content, mapId, "normal", "sandbox_lab");
            const int preRollTicks = 270;
            for (var tick = 0; tick < preRollTicks; tick++) session.Update(1f / 60);
            if (session.AnnouncementRemaining > 0)
                throw new InvalidOperationException("The pilot's announcement pre-roll has not elapsed.");
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            ui.HandleGameplayInput(Pointer(0, 0), session);
            var subjects = new List<(EnemyInstance Enemy, string Condition)>();
            var conditions = new[] { "neutral", "burn", "slow", "burn-slow", "burn-armor-break", "untreated-neighbor" };
            foreach (var family in new[] { "t3_brute", "t2_runner" })
            foreach (var condition in conditions)
            {
                session.SpawnEnemy(family, 1, 1);
                var enemy = session.Enemies[^1];
                enemy.SetSandboxPathDistance(80 + subjects.Count * 68, session.Map.Path);
                subjects.Add((enemy, condition));
            }
            var directory = Path.Combine(_outputDirectory, mapId);
            Directory.CreateDirectory(directory);
            var frames = new List<object>();
            for (var frame = 0; frame < EnemyLaneFrameCount; frame++)
            {
                if (frame == EnemyLaneFramesPerSecond)
                    foreach (var subject in subjects) ApplyEnemyLaneConditions(subject.Enemy, subject.Condition);
                var tick = preRollTicks + frame * 2;
                var checksum = SessionChecksum.Compute(session, tick);
                var before = GraphicsDevice.Metrics;
                var timer = Stopwatch.StartNew();
                using var scene = RenderScene(ui, GameState.Playing, session);
                timer.Stop();
                var metrics = GraphicsDevice.Metrics - before;
                var enemyGlyphs = _renderer.LastEnemyGlyphCount;
                var enemyHealthBars = _renderer.LastEnemyHealthBarCount;
                var enemyShieldBars = _renderer.LastEnemyShieldBarCount;
                using var native = DownsampleEnemyLaneScene(scene);
                var fileName = $"{mapId}/frame-{frame:000}.png";
                var saved = SaveTierScene(native, fileName);
                var state = frame switch
                {
                    0 => "neutral",
                    30 => "first-application",
                    75 => "sustained-between-ticks",
                    120 => "turning-and-overlap",
                    179 => "last-active-frame",
                    182 => "expired",
                    _ => null
                };
                if (state is not null)
                    File.Copy(Path.Combine(_outputDirectory, fileName), Path.Combine(directory, state + ".png"), true);
                if (checksum != SessionChecksum.Compute(session, tick))
                    throw new InvalidOperationException($"{mapId} {variant} frame {frame}: capture changed gameplay state.");
                frames.Add(new
                {
                    frame, tick, seconds = frame / (float)EnemyLaneFramesPerSecond,
                    checksum, fileName, saved.Sha256,
                    renderSubmissionMilliseconds = timer.Elapsed.TotalMilliseconds,
                    metrics.DrawCount, metrics.SpriteCount, metrics.PrimitiveCount,
                    enemyGlyphs, enemyHealthBars, enemyShieldBars,
                    transientEffects = session.Effects.Effects.Count,
                    projectiles = session.Projectiles.Projectiles.Count,
                    enemies = subjects.Select(subject => new
                    {
                        subject.Enemy.Id, family = subject.Enemy.Definition.Id, intendedCondition = subject.Condition,
                        x = subject.Enemy.Position.X, y = subject.Enemy.Position.Y,
                        subject.Enemy.DistanceAlongPath, subject.Enemy.Health, subject.Enemy.Shield,
                        active = subject.Enemy.StatusEffects.Active.Select(status => new
                        {
                            type = status.Type.ToString(), status.Magnitude, status.RemainingSeconds
                        }).ToArray()
                    }).ToArray()
                });
                for (var step = 0; step < 2; step++) session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / EnemyLaneFramesPerSecond);
            }
            captures.Add(new { mapId, preRollTicks, initialPathDistance = 80, spacing = 68, frames });
            Console.WriteLine($"Enemy lane {variant}: {mapId}, {EnemyLaneFrameCount} native frames captured.");
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "lane-manifest.json"), JsonSerializer.Serialize(new
        {
            variant,
            fixture = "Controlled standard-rank Brute/Runner lane; synthetic direct status applications through the real controller, with Burn 2 DPS, Slow 35%, Armor Break 4. No firing tower, initial projectile impact or random state is fabricated.",
            seed = "No random source is used by this controlled fixture.",
            resolution = new { width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440 },
            fps = EnemyLaneFramesPerSecond, seconds = 8, fixedSimulationHz = 60, wave = 0,
            speed = 1, difficulty = "normal", challenge = "sandbox_lab",
            reducedEffects = _renderer.ReducedEffects, quietEnemyHud = _renderer.QuietEnemyHud,
            applicationsAtSeconds = 1, statusDurationSeconds = 5,
            measurement = "CPU render submission excludes GPU completion, image readback, downsampling, PNG encoding and disk writes. Draw counts cover the 2560x1440 game/UI pass. No particle pool exists; transient effect instances are reported separately.",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LANE_EVIDENCE") == "1")
            CaptureEnemyLaneEvidence(content, font, display, variant);
    }

    private void CaptureEnemyLaneEvidence(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        ApplyEnemyEvidenceOptions(_renderer);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_VIEWPORT_EVIDENCE") == "1")
        {
            CaptureEnemyViewportEvidence(content, font, display, variant);
            return;
        }
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_WAVE_EVIDENCE") == "1")
        {
            CaptureCompleteEnemyWaves(content, font, display, variant);
            return;
        }
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROFILE_EVIDENCE") == "1")
        {
            ProfileEnemyEvidence(content, font, display, variant);
            return;
        }
        var captures = new List<object>();
        const int ordinarySeed = 1421;
        var options = new SimulationOptions
        {
            MapId = "foundry_loop", DifficultyId = "normal", ChallengeId = "standard",
            Strategy = AutoPlayerStrategy.Experienced, Seed = ordinarySeed, MaximumWave = 16,
            StepSeconds = 1f / 60
        };
        var execution = HeadlessSimulation.RunForDiagnostics(content, options);
        if (execution.Session.IsDefeat || execution.Session.CurrentWave != 16)
            throw new InvalidOperationException("The ordinary-wave capture could not reach its authored wave-16 checkpoint.");
        var ordinary = GameSession.RestoreSaveGame(content, execution.Session.CaptureSaveGame());
        var player = new AutoPlayer(ordinary, options.Strategy, ordinarySeed + 10_003, options);
        player.PrepareForWave(ordinary);
        if (!ordinary.StartNextWave(true)) throw new InvalidOperationException("The ordinary-wave capture could not start wave 17.");
        for (var tick = 0; tick < 720; tick++) AdvanceOrdinary(tick);
        ordinary.CancelPlacement();
        ordinary.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        _renderer.ReducedEffects = false;
        captures.Add(CaptureEnemyEvidenceSequence(ordinary, "ordinary-foundry-full", font, display,
            EnemyLaneFrameCount, 720, "Authored ordinary campaign wave 17, Standard directive, Experienced autoplay; no direct enemy or status edits.",
            tick => AdvanceOrdinary(tick)));

        foreach (var map in content.Maps.Values)
        {
            foreach (var reduced in new[] { false, true })
            {
                var dense = BuildEnemyEvidenceStress(content, map.Id);
                _renderer.ReducedEffects = reduced;
                var mode = reduced ? "reduced" : "full";
                captures.Add(CaptureEnemyEvidenceSequence(dense, $"stress-{map.Id}-{mode}", font, display,
                    map.Id == "foundry_loop" ? EnemyLaneFrameCount : 1, 234,
                    "Synthetic stress formation: 36 enemies, all five families, three ranks, five signals, 18 legally placed authored towers; raised health and reduced movement speed retain overlap. Status pairs use the real controller and source tower IDs.",
                    _ => dense.Update(1f / 60)));
            }
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "evidence-manifest.json"), JsonSerializer.Serialize(new
        {
            variant, ordinarySeed, ordinaryWave = 17,
            ordinaryCheckpointBuildStepSeconds = 1f / 60,
            fps = EnemyLaneFramesPerSecond, fixedSimulationHz = 60,
            resolution = new { width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440 },
            measurement = "CPU render submission excludes GPU completion and all PNG/readback work. Draw counts include game and UI, excluding final downsample. Transient effect count is not a particle count.",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));

        void AdvanceOrdinary(int tick)
        {
            ordinary.Update(1f / 60);
            if (tick % 60 == 59) player.ReactDuringWave(ordinary);
        }
    }

    private object CaptureEnemyEvidenceSequence(GameSession session, string directoryName, SpriteFont font,
        SpriteFont display, int frameCount, int startTick, string fixture, Action<int> advance)
    {
        Directory.CreateDirectory(Path.Combine(_outputDirectory, directoryName));
        var ui = new UIManager(font, display);
        ConfigureUi(ui, session.Content);
        ui.HandleGameplayInput(Pointer(0, 0), session);
        var frames = new List<object>();
        var initialSimulatedSeconds = session.Statistics.SimulatedSeconds;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var tick = startTick + frame * 2;
            var checksum = SessionChecksum.Compute(session, tick);
            var before = GraphicsDevice.Metrics;
            var timer = Stopwatch.StartNew();
            using var scene = RenderScene(ui, GameState.Playing, session);
            timer.Stop();
            var metrics = GraphicsDevice.Metrics - before;
            var enemyGlyphs = _renderer.LastEnemyGlyphCount;
            var enemyHealthBars = _renderer.LastEnemyHealthBarCount;
            var enemyShieldBars = _renderer.LastEnemyShieldBarCount;
            using var native = DownsampleEnemyLaneScene(scene);
            var saved = SaveTierScene(native, $"{directoryName}/frame-{frame:000}.png");
            if (checksum != SessionChecksum.Compute(session, tick))
                throw new InvalidOperationException($"{directoryName} frame {frame}: capture changed gameplay state.");
            frames.Add(new
            {
                frame, tick, checksum, session.CurrentWave, simulatedSeconds = session.Statistics.SimulatedSeconds,
                saved.File, saved.Sha256, renderSubmissionMilliseconds = timer.Elapsed.TotalMilliseconds,
                metrics.DrawCount, metrics.SpriteCount, metrics.PrimitiveCount,
                enemyGlyphs, enemyHealthBars, enemyShieldBars,
                transientEffects = session.Effects.Effects.Count,
                projectiles = session.Projectiles.Projectiles.Count,
                towers = session.Towers.Count,
                enemies = session.Enemies.Select(enemy => new
                {
                    enemy.Id, family = enemy.Definition.Id, rank = enemy.Rank.ToString(), signal = enemy.SignalRole.ToString(),
                    x = enemy.Position.X, y = enemy.Position.Y, enemy.DistanceAlongPath,
                    enemy.Health, enemy.Shield, enemy.DamagePauseTimer, enemy.BossPhaseActive,
                    enemy.FormationSpeedMultiplier,
                    active = enemy.StatusEffects.Active.Select(status => status.Type.ToString()).ToArray()
                }).ToArray()
            });
            for (var step = 0; step < 2; step++) advance(tick + step);
            ui.AdvanceVisualTime(1f / EnemyLaneFramesPerSecond);
        }
        Console.WriteLine($"Enemy evidence: {directoryName}, {frameCount} native frames captured.");
        return new { directoryName, mapId = session.Map.Definition.Id, fixture, initialSimulatedSeconds,
            reducedEffects = _renderer.ReducedEffects, frames };
    }

    private static GameSession BuildEnemyEvidenceStress(GameContent content, string mapId)
    {
        var session = new GameSession(content, mapId, "normal", "sandbox_lab");
        var candidates = new List<Vector2>();
        foreach (var region in session.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 24)
        for (var x = region.Left + 24; x < region.Right - 16; x += 24)
            candidates.Add(new Vector2(x, y));
        candidates = candidates.OrderBy(point => Vector2.DistanceSquared(point, new Vector2(480, 360))).ToList();
        var towerDefinitions = content.Towers.Values.ToArray();
        for (var index = 0; index < 18; index++)
        {
            var definition = towerDefinitions[index % towerDefinitions.Length];
            if (!candidates.Any(point => session.TryPlaceTower(definition.Id, point)))
                throw new InvalidOperationException($"{mapId}: no legal position for stress tower {definition.Id}.");
            var tower = session.SelectedTower!;
            var stage = definition.Id is "breaker_cannon" or "arc_relay" ? 3 : index % 3 + 1;
            if (stage >= 2 && !session.TryChooseTowerDoctrine(tower.Id, definition.Tier2Doctrines[0].Id))
                throw new InvalidOperationException("Could not apply an authored stress-tower doctrine.");
            if (stage >= 3 && !session.TrySpecializeTower(tower.Id, definition.Specializations[0].Id))
                throw new InvalidOperationException("Could not apply an authored stress-tower specialization.");
        }
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var families = content.Enemies.Values.ToArray();
        var ranks = new[] { "Standard", "Elite", "Boss" };
        for (var index = 0; index < 36; index++)
        {
            session.SpawnEnemy(families[index % families.Length].Id, 160, .45f,
                ranks[index / families.Length % ranks.Length], (EnemySignalRole)(index % 6));
            session.Enemies[^1].SetSandboxPathDistance(90 + index * (session.Map.Path.TotalLength - 800) / 36,
                session.Map.Path);
        }
        for (var tick = 0; tick < 222; tick++) session.Update(1f / 60);
        var sources = new Dictionary<StatusType, int>
        {
            [StatusType.Burn] = session.Towers.First(tower => tower.Definition.Id == "ember_coil").Id,
            [StatusType.Slow] = session.Towers.First(tower => tower.Definition.Id == "frost_spire").Id,
            [StatusType.ArmorBreak] = session.Towers.First(tower => tower.Definition.Id == "breaker_cannon").Id,
            [StatusType.Exposed] = session.Towers.First(tower => tower.Definition.Id == "prism_beam").Id,
            [StatusType.Stun] = session.Towers.First(tower => tower.Definition.Id == "arc_relay").Id
        };
        for (var index = 0; index < session.Enemies.Count; index++)
        {
            var enemy = session.Enemies[index];
            enemy.SetSandboxPathDistance(90 + index * (session.Map.Path.TotalLength - 220) / 36, session.Map.Path);
            enemy.ApplyHealthDamage(enemy.MaxHealth * .22f);
            enemy.TickRuntimeTimers(1.1f);
            switch (index % 7)
            {
                case 0: Apply(StatusType.Burn); Apply(StatusType.Slow); break;
                case 1: Apply(StatusType.Burn); Apply(StatusType.ArmorBreak); break;
                case 2: Apply(StatusType.Slow); Apply(StatusType.Exposed); break;
                case 3:
                    enemy.GrantShield(enemy.MaxHealth * .10f, enemy.Definition.Shield + enemy.MaxHealth * .20f);
                    Apply(StatusType.Burn);
                    break;
                case 4: Apply(StatusType.Stun); Apply(StatusType.Slow); break;
                case 5: Apply(StatusType.Burn); break;
            }
            enemy.ArmSignalAbility(0);

            void Apply(StatusType type) => enemy.ApplyStatus(new StatusApplication
            {
                Type = type, Duration = 8, SourceId = sources[type],
                Magnitude = type == StatusType.Burn ? 10 : type == StatusType.ArmorBreak ? 4 : type == StatusType.Stun ? 1 : .35f,
                ArmorPierce = type == StatusType.Burn ? 4 : 0
            });
        }
        for (var tick = 0; tick < 12; tick++) session.Update(1f / 60);
        return session;
    }

    private RenderTarget2D DownsampleEnemyLaneScene(RenderTarget2D scene)
    {
        var native = new RenderTarget2D(GraphicsDevice, GameConstants.LogicalWidth, GameConstants.LogicalHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(native);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(scene, new Rectangle(0, 0, native.Width, native.Height), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return native;
    }

    private static void ApplyEnemyLaneConditions(EnemyInstance enemy, string condition)
    {
        if (condition.Contains("burn", StringComparison.Ordinal)) Apply(StatusType.Burn, 2, 4);
        if (condition.Contains("slow", StringComparison.Ordinal)) Apply(StatusType.Slow, .35f);
        if (condition.Contains("armor-break", StringComparison.Ordinal)) Apply(StatusType.ArmorBreak, 4);

        void Apply(StatusType type, float magnitude, float armorPierce = 0) => enemy.ApplyStatus(new StatusApplication
        {
            Type = type, Duration = 5, Magnitude = magnitude, ArmorPierce = armorPierce
        });
    }
}
