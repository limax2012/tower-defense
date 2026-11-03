using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
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
    private sealed record OrdinaryWaveFixture(string Id, string MapId, int Wave, int Seed, bool Holdout);

    private static readonly OrdinaryWaveFixture[] OrdinaryWaveFixtures =
    [
        new("ordinary-foundry-wave17-seed1421", "foundry_loop", 17, 1421, false),
        new("holdout-rainline-wave9-seed7331", "crosswind_basin", 9, 7331, true)
    ];

    private static (GameSession Session, AutoPlayer Player) BuildOrdinaryWave(GameContent content,
        OrdinaryWaveFixture fixture)
    {
        var options = new SimulationOptions
        {
            MapId = fixture.MapId, DifficultyId = "normal", ChallengeId = "standard",
            Strategy = AutoPlayerStrategy.Experienced, Seed = fixture.Seed,
            MaximumWave = fixture.Wave - 1, StepSeconds = 1f / 60
        };
        var execution = HeadlessSimulation.RunForDiagnostics(content, options);
        if (execution.Session.IsDefeat || execution.Session.CurrentWave != fixture.Wave - 1)
            throw new InvalidOperationException($"{fixture.Id}: the authored pre-wave checkpoint was not reached.");
        var session = GameSession.RestoreSaveGame(content, execution.Session.CaptureSaveGame());
        var player = new AutoPlayer(session, options.Strategy, fixture.Seed + 10_003, options);
        player.PrepareForWave(session);
        if (!session.StartNextWave(true))
            throw new InvalidOperationException($"{fixture.Id}: the requested authored wave did not start.");
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        return (session, player);
    }

    private static void AdvanceOrdinaryWave(GameSession session, AutoPlayer player, int tick)
    {
        session.Update(1f / 60);
        if (tick % 60 == 59) player.ReactDuringWave(session);
    }

    private void CaptureCompleteEnemyWaves(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        var selection = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_WAVE_SELECTION") ?? "both";
        if (selection is not ("ordinary" or "holdout" or "both"))
            throw new InvalidOperationException("Wave selection must be ordinary, holdout or both.");
        var preview = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_WAVE_PREVIEW") == "1";
        var reduced = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LANE_REDUCED") == "1";
        var originalRenderer = _renderer;
        var captures = new List<object>();
        try
        {
            foreach (var fixture in OrdinaryWaveFixtures.Where(fixture => selection == "both" || fixture.Holdout == (selection == "holdout")))
            {
                _renderer = new GameRenderer { QuietEnemyHud = variant == "C", ReducedEffects = reduced };
                ApplyEnemyEvidenceOptions(_renderer);
                var (session, player) = BuildOrdinaryWave(content, fixture);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                Directory.CreateDirectory(Path.Combine(_outputDirectory, fixture.Id));
                var frames = new List<object>();
                var settledFrames = 0;
                var initialSimulatedSeconds = session.Statistics.SimulatedSeconds;
                using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
                    false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                using var native = new RenderTarget2D(GraphicsDevice, 1280, 720,
                    false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                for (var frame = 0; frame < 5400; frame++)
                {
                    var tick = frame * 2;
                    var checksum = SessionChecksum.Compute(session, tick);
                    RenderEnemyEvidenceScene(ui, session, scene);
                    string? file = null;
                    string? hash = null;
                    if (!preview || frame % 60 == 0 || settledFrames == 59)
                    {
                        GraphicsDevice.SetRenderTarget(native);
                        GraphicsDevice.Clear(ColorPalette.Canvas);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                        _batch.Draw(scene, new Rectangle(0, 0, 1280, 720), Color.White);
                        _batch.End();
                        GraphicsDevice.SetRenderTarget(null);
                        file = $"{fixture.Id}/frame-{frame:0000}.png";
                        hash = SaveTierScene(native, file).Sha256;
                    }
                    else GraphicsDevice.Present();
                    if (checksum != SessionChecksum.Compute(session, tick))
                        throw new InvalidOperationException($"{fixture.Id} frame {frame}: rendering changed simulation state.");
                    frames.Add(new
                    {
                        frame, tick, seconds = frame / 30f, file, hash, checksum,
                        simulatedSeconds = session.Statistics.SimulatedSeconds,
                        session.CurrentWave, waveActive = session.Waves.IsActive, session.IsDefeat,
                        session.Economy.Lives, session.Economy.Credits,
                        enemyGlyphs = _renderer.LastEnemyGlyphCount,
                        enemyHealthBars = _renderer.LastEnemyHealthBarCount,
                        enemyShieldBars = _renderer.LastEnemyShieldBarCount,
                        statusStrips = CaptureEnemyRailGeometry(session),
                        transientEffectInstances = session.Effects.Effects.Count,
                        projectiles = session.Projectiles.Projectiles.Count,
                        towers = session.Towers.Select(tower => new
                        {
                            tower.Id, definition = tower.Definition.Id,
                            x = tower.Position.X, y = tower.Position.Y
                        }).ToArray(),
                        enemies = session.Enemies.Select(enemy => new
                        {
                            enemy.Id, family = enemy.Definition.Id, rank = enemy.Rank.ToString(),
                            signal = enemy.SignalRole.ToString(), x = enemy.Position.X, y = enemy.Position.Y,
                            enemy.DistanceAlongPath, enemy.Health, enemy.MaxHealth, enemy.Shield,
                            enemy.DamagePauseTimer, enemy.BossPhaseActive, enemy.FormationSpeedMultiplier,
                            enemy.IsDead, enemy.HasEscaped, bodyRadius = EnemyModelArt.BodyRadius(enemy),
                            headingRadians = CrowdedPilotHeading(session, enemy),
                            visibleBounds = CaptureEnemyVisibleBounds(session, enemy),
                            expectedConditions = ExpectedEnemyConditions(enemy),
                            active = enemy.StatusEffects.Active.Select(status => new
                            {
                                type = status.Type.ToString(), status.SourceId, status.Magnitude, status.RemainingSeconds
                            }).ToArray()
                        }).ToArray()
                    });
                    if (!session.Waves.IsActive || session.IsDefeat) settledFrames++;
                    if (settledFrames >= 60) break;
                    for (var step = 0; step < 2; step++) AdvanceOrdinaryWave(session, player, tick + step);
                    ui.AdvanceVisualTime(1f / 30);
                }
                if (settledFrames < 60)
                    throw new InvalidOperationException($"{fixture.Id}: the 180-second safety bound was reached before the complete wave settled.");
                var capture = new
                {
                    fixture.Id, fixture.MapId, fixture.Wave, fixture.Seed, fixture.Holdout,
                    initialSimulatedSeconds, reducedEffects = reduced, fullWave = true,
                    frameCount = frames.Count, durationSeconds = frames.Count / 30f,
                    outcome = session.IsDefeat ? "defeat" : "wave-cleared", frames
                };
                captures.Add(capture);
                File.WriteAllText(Path.Combine(_outputDirectory, fixture.Id, "truth.json"),
                    JsonSerializer.Serialize(capture, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Complete wave: {fixture.Id}, {frames.Count} native frames, {frames.Count / 30f:F2}s, {(session.IsDefeat ? "defeat" : "cleared")}.");
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "complete-wave-manifest.json"), JsonSerializer.Serialize(new
        {
            variant, selection, preview, fps = 30, fixedSimulationHz = 60,
            richMaterialRequested = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICH_MATERIALS") == "1",
            fixture = "Complete authored waves following a deterministic Experienced autoplay checkpoint. No direct enemy/status/health/speed edits. Two seconds of settled outcome are retained; no subsequent wave is started.",
            resolution = new { width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440 },
            camera = "Unmodified 1280x720 logical board; pointer outside battlefield; normal playback speed.",
            holdout = "Rainline Heights wave9 seed7331 is reserved for finalist validation, not candidate tuning.",
            geometry = "Direct renderer rail cells and leader endpoints. Body radii are bounding-circle proxies, not pixel masks. Nullable fields are absent in older renderers. Human attribution, speed and enjoyment are unmeasured.",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void RenderEnemyEvidenceScene(UIManager ui, GameSession session, RenderTarget2D target)
    {
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        _renderer.Draw(_batch, _primitives, session, foregroundTowerId: ui.RemoteCoOpSelectedTowerId);
        ui.Draw(_batch, _primitives, GameState.Playing, session, Matrix.CreateScale(GameConstants.RenderScale));
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
    }

    private object[] CaptureEnemyRailGeometry(GameSession session) => _renderer.LastEnemyStrips.Select(strip =>
    {
        var ownerAnchor = OptionalStripValue<Vector2>(strip, "OwnerAnchor") ?? strip.TrackEdge;
        var leaderEnd = OptionalStripValue<Vector2>(strip, "LeaderEnd") ?? strip.Anchor - strip.Outward * 5;
        var ownerHull = OptionalStripProperties["OwnerHull"]?.GetValue(strip) as Vector2[];
        return (object)new
        {
            enemyId = strip.Enemy.Id, strip.Rail, outwardX = strip.Outward.X, outwardY = strip.Outward.Y,
            ownerX = strip.Enemy.Position.X, ownerY = strip.Enemy.Position.Y,
            leaderStartX = ownerAnchor.X, leaderStartY = ownerAnchor.Y,
            leaderEndX = leaderEnd.X, leaderEndY = leaderEnd.Y,
            anchorX = strip.Anchor.X, anchorY = strip.Anchor.Y,
            legible = OptionalStripValue<bool>(strip, "IsLegible"),
            leaderCrossings = OptionalStripValue<int>(strip, "LeaderCrossings"),
            side = OptionalStripValue<int>(strip, "Side"),
            ownerHull = ownerHull?.Select(point => new { x = point.X, y = point.Y }).ToArray(),
            expectedConditions = ExpectedEnemyConditions(strip.Enemy),
            glyphs = strip.Conditions.Select((condition, index) => new
            {
                condition = condition.ToString(), x = strip.Centers[index].X, y = strip.Centers[index].Y,
                trackClearance = session.Map.Path.DistanceToPath(strip.Centers[index]) - session.Map.Definition.PathWidth * .5f
            }).ToArray()
        };
    }).ToArray();

    private static readonly Dictionary<string, PropertyInfo?> OptionalStripProperties =
        new[] { "OwnerAnchor", "LeaderEnd", "IsLegible", "LeaderCrossings", "Side", "OwnerHull" }
            .ToDictionary(name => name, name => typeof(EnemyHudStrip).GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

    private static readonly MethodInfo? EnemyVisibleBoundsMethod = typeof(EnemyHudPresentation).GetMethod(
        "VisibleBounds", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
        [typeof(EnemyInstance), typeof(Vector2), typeof(float)], null);

    private static object? CaptureEnemyVisibleBounds(GameSession session, EnemyInstance enemy)
    {
        if (EnemyVisibleBoundsMethod?.Invoke(null, [enemy, enemy.Position, CrowdedPilotHeading(session, enemy)]) is not Vector4 bounds)
            return null;
        return new { left = bounds.X, top = bounds.Y, right = bounds.Z, bottom = bounds.W };
    }

    private static T? OptionalStripValue<T>(EnemyHudStrip strip, string name) where T : struct =>
        OptionalStripProperties[name]?.GetValue(strip) is T value ? value : null;

    private static string[] ExpectedEnemyConditions(EnemyInstance enemy)
    {
        Span<EnemyCondition> conditions = stackalloc EnemyCondition[8];
        var count = EnemyConditionPresentation.CopyActive(enemy, conditions);
        var names = new string[count];
        for (var index = 0; index < count; index++) names[index] = conditions[index].ToString();
        return names;
    }

    private static void ApplyEnemyEvidenceOptions(GameRenderer renderer)
    {
        var property = typeof(GameRenderer).GetProperty("RichEnemyMaterials",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property?.SetValue(renderer, Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICH_MATERIALS") == "1");
    }

    private void ProfileEnemyEvidence(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        const int warmupFrames = 180;
        var longRun = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PROFILE_LONG") == "1";
        var measuredFrames = longRun ? 3600 : 480;
        const int repeats = 3;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();
        HideAndDisableActivation();
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        using var process = Process.GetCurrentProcess();
        var particleCounter = typeof(GameRenderer).GetProperty("EnemyMaterialParticleCount",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var originalRenderer = _renderer;
        var runs = new List<object>();
        try
        {
            foreach (var scenario in longRun ? new[] { "ordinary" } : new[] { "ordinary", "stress" })
            foreach (var reduced in new[] { false, true })
            for (var repeat = 0; repeat < repeats; repeat++)
            {
                GameSession session;
                Action<int> advance;
                if (scenario == "ordinary")
                {
                    var ordinary = BuildOrdinaryWave(content, OrdinaryWaveFixtures[0]);
                    session = ordinary.Session;
                    var preRollTicks = longRun ? 0 : 720;
                    for (var tick = 0; tick < preRollTicks; tick++) AdvanceOrdinaryWave(session, ordinary.Player, tick);
                    advance = tick => AdvanceOrdinaryWave(session, ordinary.Player, tick + preRollTicks);
                }
                else
                {
                    session = BuildEnemyEvidenceStress(content, "foundry_loop");
                    advance = _ => session.Update(1f / 60);
                }
                _renderer = new GameRenderer { QuietEnemyHud = variant == "C", ReducedEffects = reduced };
                ApplyEnemyEvidenceOptions(_renderer);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var samples = new List<object>(measuredFrames);
                var milliseconds = new List<double>(measuredFrames);
                var busyMilliseconds = new List<double>(measuredFrames);
                var memorySamples = new List<object>();
                int? settledSimulationFrame = null;
                long lastPresent = 0;
                long memoryAtStart = 0;
                long managedAtStart = 0;
                long allocationsAtStart = 0;
                for (var frame = 0; frame < warmupFrames + measuredFrames; frame++)
                {
                    if (longRun && frame % 60 == 0)
                    {
                        process.Refresh();
                        memorySamples.Add(new
                        {
                            simulationFrame = frame, secondsSinceWaveStart = frame / 60f,
                            warmup = frame < warmupFrames, waveActive = session.Waves.IsActive,
                            privateMemoryBytes = process.PrivateMemorySize64,
                            managedMemoryBytes = GC.GetTotalMemory(false),
                            allocatedBytes = GC.GetTotalAllocatedBytes(),
                            enemyMaterialParticles = particleCounter?.GetValue(_renderer) as int? ?? 0,
                            transientEffectInstances = session.Effects.Effects.Count,
                            projectiles = session.Projectiles.Projectiles.Count
                        });
                    }
                    if (frame == warmupFrames - 1)
                    {
                        process.Refresh();
                        memoryAtStart = process.PrivateMemorySize64;
                        managedAtStart = GC.GetTotalMemory(false);
                        allocationsAtStart = GC.GetTotalAllocatedBytes();
                    }
                    advance(frame);
                    if (longRun && settledSimulationFrame is null && (!session.Waves.IsActive || session.IsDefeat))
                        settledSimulationFrame = frame;
                    ui.AdvanceVisualTime(1f / 60);
                    RenderEnemyEvidenceScene(ui, session, target);
                    GraphicsDevice.Clear(ColorPalette.Canvas);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                    _batch.Draw(target, new Rectangle(0, 0, 1280, 720), Color.White);
                    _batch.End();
                    GraphicsDevice.Present();
                    var present = Stopwatch.GetTimestamp();
                    if (frame >= warmupFrames)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(lastPresent, present).TotalMilliseconds;
                        milliseconds.Add(elapsed);
                        if (session.Waves.IsActive && !session.IsDefeat) busyMilliseconds.Add(elapsed);
                        samples.Add(new
                        {
                            frame = frame - warmupFrames, presentToPresentMilliseconds = elapsed,
                            enemies = session.Enemies.Count, transientEffectInstances = session.Effects.Effects.Count,
                            enemyMaterialParticles = particleCounter?.GetValue(_renderer) as int? ?? 0,
                            waveActive = session.Waves.IsActive,
                            periodicMemoryProbeIncluded = longRun && frame % 60 == 0,
                            projectiles = session.Projectiles.Projectiles.Count
                        });
                    }
                    lastPresent = present;
                }
                process.Refresh();
                var ordered = milliseconds.Order().ToArray();
                var sustained = Enumerable.Range(0, milliseconds.Count - 29)
                    .Select(start => milliseconds.Skip(start).Take(30).Average()).Max();
                var worst = milliseconds.IndexOf(milliseconds.Max());
                var run = new
                {
                    scenario, reducedEffects = reduced, repeat, warmupFrames, measuredFrames, longRun,
                    waveEntryCaptured = longRun,
                    settledSimulationFrame,
                    settledSecondsSinceWaveStart = settledSimulationFrame / 60f,
                    completeWaveObserved = longRun && settledSimulationFrame is not null,
                    measuredWaveActiveFrames = longRun ? busyMilliseconds.Count : (int?)null,
                    measuredSettledTailFrames = longRun ? milliseconds.Count - busyMilliseconds.Count : (int?)null,
                    medianWaveActiveMilliseconds = !longRun || busyMilliseconds.Count == 0 ? (double?)null : busyMilliseconds.Order().ElementAt(busyMilliseconds.Count / 2),
                    medianMilliseconds = ordered[ordered.Length / 2],
                    p95Milliseconds = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1],
                    maximumMilliseconds = ordered[^1], worstFrame = worst,
                    worst30FrameMeanMilliseconds = sustained,
                    framesAbove16_67Milliseconds = milliseconds.Count(value => value > 1000d / 60),
                    framesAbove33_33Milliseconds = milliseconds.Count(value => value > 1000d / 30),
                    privateMemoryStartBytes = memoryAtStart, privateMemoryEndBytes = process.PrivateMemorySize64,
                    managedMemoryStartBytes = managedAtStart, managedMemoryEndBytes = GC.GetTotalMemory(false),
                    allocatedBytes = GC.GetTotalAllocatedBytes() - allocationsAtStart,
                    endChecksum = SessionChecksum.Compute(session, warmupFrames + measuredFrames), samples, memorySamples
                };
                runs.Add(run);
                File.WriteAllText(Path.Combine(_outputDirectory, $"profile-{scenario}-{(reduced ? "reduced" : "full")}-{repeat}.json"),
                    JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Present profile {scenario} {(reduced ? "reduced" : "full")} repeat{repeat}: median{ordered[ordered.Length / 2]:F2}ms p95{ordered[(int)Math.Ceiling(ordered.Length * .95) - 1]:F2}ms.");
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "present-profile.json"), JsonSerializer.Serialize(new
        {
            variant, warmupFrames, measuredFrames, repeats, fixedSimulationHz = 60, longRun,
            scope = longRun ? "Complete repeated authored wave17 from entry. The first three simulation seconds warm up the renderer; measurement includes the remaining wave and its settled empty tail. Periodic memory probes run every60 simulation frames and are marked in samples; their overhead is included. Do not compare this whole-wave median directly with the separate busy-window profile." : "Identical busy windows after a twelve-second ordinary-wave pre-roll, or the controlled stress fixture. Three seconds warm up each run before eight seconds of measurement.",
            richMaterialRequested = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_RICH_MATERIALS") == "1",
            backbuffer = new { width = 1280, height = 720 },
            scene = new { width = 2560, height = 1440 },
            timing = "Wall-clock Present-to-Present intervals, including simulation, real renderer/UI, native downsample, driver submission and any swap wait. VSync and frame limiting disabled; hidden window. Not GPU timestamps or interactive gameplay FPS. No screenshots, readback, PNG export or per-frame checksums during measurement. Reusable scene target.",
            counters = "Transient effect instances and projectiles are exact controller counts, not a count of decorative particles. Memory is process-private and managed heap; samples/renderer caches contribute to allocation. Three repeats estimate local noise.",
            enemyMaterialParticleCounterAvailable = particleCounter is not null,
            particleAccounting = "Enemy material particles use the renderer-owned bounded particle collection when present. Zero with an unavailable counter is not evidence that an older procedural renderer drew no particles.",
            device = GraphicsDevice.Adapter.Description,
            platform = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(), runs
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void CaptureEnemyViewportEvidence(GameContent content, SpriteFont font, SpriteFont display, string variant)
    {
        var sizes = new[] { (Width: 1280, Height: 720), (Width: 1920, Height: 1080),
            (Width: 1024, Height: 768), (Width: 320, Height: 180) };
        var originalRenderer = _renderer;
        var captures = new List<object>();
        using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        try
        {
            foreach (var map in content.Maps.Values)
            {
                var session = BuildEnemyEvidenceStress(content, map.Id);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                const int tick = 234;
                var checksum = SessionChecksum.Compute(session, tick);
                foreach (var reduced in new[] { false, true })
                {
                    _renderer = new GameRenderer { QuietEnemyHud = variant == "C", ReducedEffects = reduced };
                    ApplyEnemyEvidenceOptions(_renderer);
                    RenderEnemyEvidenceScene(ui, session, scene);
                    foreach (var size in sizes)
                    {
                        var viewport = new ViewportTransform();
                        viewport.Update(size.Width, size.Height);
                        using var output = new RenderTarget2D(GraphicsDevice, size.Width, size.Height,
                            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                        GraphicsDevice.SetRenderTarget(output);
                        GraphicsDevice.Clear(ColorPalette.Paper);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                        _batch.Draw(scene, viewport.DestinationRectangle, Color.White);
                        _batch.End();
                        GraphicsDevice.SetRenderTarget(null);
                        var file = $"viewport-{map.Id}-{(reduced ? "reduced" : "full")}-{size.Width}x{size.Height}.png";
                        var saved = SaveTierScene(output, file);
                        if (checksum != SessionChecksum.Compute(session, tick))
                            throw new InvalidOperationException($"{file}: viewport compositing changed the gameplay state.");
                        var destination = viewport.DestinationRectangle;
                        captures.Add(new
                        {
                            mapId = map.Id, reducedEffects = reduced, tick, checksum, file, saved.Sha256,
                            size.Width, size.Height, viewport.Scale,
                            destination = new { destination.X, destination.Y, destination.Width, destination.Height },
                            projectedGlyphDiameterPixels = EnemyHudPresentation.GlyphRadius * 2 * viewport.Scale,
                            enemyGlyphs = _renderer.LastEnemyGlyphCount,
                            enemyHealthBars = _renderer.LastEnemyHealthBarCount,
                            enemyShieldBars = _renderer.LastEnemyShieldBarCount,
                            limit = size.Width == 320 ? "Minimum accepted client size: condition glyphs project to1.7px. This verifies viewport composition only; complete human recognition at this scale is not claimed." : null
                        });
                    }
                }
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "viewport-evidence.json"), JsonSerializer.Serialize(new
        {
            variant,
            fixture = "Unchanged synthetic stress fixture: four districts, Full/Reduced, all five families and three ranks. Each size uses the same already-rendered2560x1440 scene and unchanged tick/state.",
            compositor = "The DesktopGL final composition path: ViewportTransform.DestinationRectangle, opaque blend, LinearClamp sampler, Paper letterbox background. No user-facing scale setting is changed.",
            interpretation = "1280x720 is the native decision view.1920x1080 and1024x768 test larger and letterboxed output.320x180 is a known legibility limit, not evidence that every status is human-readable at0.25 logical scale.",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Viewport evidence: {captures.Count} output images; identical state across all four sizes and both effect modes.");
    }
}
