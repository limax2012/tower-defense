using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private static readonly string[] AreaReviewFamilies =
        ["t2_runner", "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator"];
    private static readonly StatusType[] AreaReviewDebuffs =
        [StatusType.Burn, StatusType.Slow, StatusType.ArmorBreak, StatusType.Exposed, StatusType.Stun];
    private static readonly PropertyInfo? AuraRegionCounter = typeof(GameRenderer).GetProperty("LastEnemyAuraRegionCount",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private void CaptureIteration05Review(GameContent content, SpriteFont font, SpriteFont display)
    {
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_AREA_PROFILE") == "1")
        {
            ProfileDenseEnemyAreas(content, font, display);
            return;
        }
        var assertions = new List<string>();
        var scenes = new List<object>();
        var originalRenderer = _renderer;
        try
        {
            var ranks = new[] { "Standard", "Elite", "Boss" };
            var samples = AreaReviewFamilies.SelectMany(id => ranks.Select(rank => EnemyRankSample(content, id, rank))).ToArray();
            foreach (var sample in samples) sample.Enemy.AbsorbShield(sample.Enemy.Shield);
            scenes.Add(CaptureEnemyScaleAtlas(content, samples, font));
            scenes.Add(CaptureEnemyFamilySilhouettes(samples, font, "area-size-silhouettes.png", fullModel: true));
            foreach (var reduced in new[] { false, true })
            foreach (var count in new[] { 0, 1, 2, 3, 4 })
            {
                var session = BuildAreaReviewCrowd(content, 20, count, FogReviewDebuffs);
                _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = reduced };
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                var checksum = SessionChecksum.Compute(session, 0);
                var file = $"area-{count}-debuffs-{(reduced ? "reduced" : "full")}.png";
                using var scene = RenderScene(ui, GameState.Playing, session);
                using var native = DownsampleEnemyLaneScene(scene);
                var saved = SaveTierScene(native, file);
                var regions = AuraRegionCounter?.GetValue(_renderer) as int?;
                if (regions is not null)
                    Require(_renderer.LastEnemyGlyphCount == 0 && regions == 20 * count,
                        $"{file}: twenty visible enemies have zero icons and exactly {20 * count} debuff aura regions, excluding natural shields and regeneration.", assertions);
                Require(SessionChecksum.Compute(session, 0) == checksum,
                    $"{file}: the area presentation preserves simulation state.", assertions);
                scenes.Add(new { kind = "area-debuffs", count, reduced, checksum, saved.File, saved.Sha256,
                    glyphs = _renderer.LastEnemyGlyphCount, regions, healthBars = _renderer.LastEnemyHealthBarCount,
                    shieldBars = _renderer.LastEnemyShieldBarCount });
            }
            foreach (var role in new[] { EnemySignalRole.Accelerator, EnemySignalRole.Restorer, EnemySignalRole.Bulwark })
            foreach (var reduced in new[] { false, true })
            {
                var session = new GameSession(content, "foundry_loop", "normal", "close_quarters");
                for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
                session.SpawnEnemy("t4_aegis", 2, 1, signalRole: role);
                session.Enemies[0].SetSandboxPathDistance(900, session.Map.Path);
                foreach (var offset in new[] { -150f, -80, -35, 35, 80, 150 })
                {
                    session.SpawnEnemy("t1_crawler", 2, 1);
                    session.Enemies[^1].SetSandboxPathDistance(900 + offset, session.Map.Path);
                    session.Enemies[^1].ApplyHealthDamage(20);
                }
                _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = reduced };
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                var checksum = SessionChecksum.Compute(session, 270);
                using var scene = RenderScene(ui, GameState.Playing, session);
                using var native = DownsampleEnemyLaneScene(scene);
                var saved = SaveTierScene(native, $"support-{role.ToString().ToLowerInvariant()}-{(reduced ? "reduced" : "full")}.png");
                Require(SessionChecksum.Compute(session, 270) == checksum,
                    $"{role}: range preview preserves the authoritative session.", assertions);
                scenes.Add(new { kind = "support-range", role = role.ToString(), reduced, checksum, saved.File, saved.Sha256,
                    radius = session.Challenge.CounterSupportRadius,
                    center = new { x = session.Enemies[0].Position.X, y = session.Enemies[0].Position.Y },
                    recipients = session.Enemies.Skip(1).Select(enemy => new { enemy.Id,
                        distance = Vector2.Distance(enemy.Position, session.Enemies[0].Position),
                        inRange = Vector2.DistanceSquared(enemy.Position, session.Enemies[0].Position) <=
                            session.Challenge.CounterSupportRadius * session.Challenge.CounterSupportRadius }).ToArray() });
            }
            var menu = new UIManager(font, display);
            ConfigureUi(menu, content);
            foreach (var frame in new[] { 0, 1 })
            {
                if (frame == 1) { menu.AdvanceMainMenuBattle(.45f); menu.AdvanceVisualTime(.45f); }
                using var scene = RenderScene(menu, GameState.MainMenu, null);
                using var native = DownsampleEnemyLaneScene(scene);
                scenes.Add(SaveTierScene(native, $"menu-{frame}.png"));
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "area-review.json"), JsonSerializer.Serialize(new
        {
            width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440,
            fixture = "Controlled real-renderer native-size review. Area panels apply zero through four sustained debuff types to twenty standard enemies with disabled source towers. Three-color panels combine Burn, Slow and Expose. Support examples show one actual Signal Gauntlet source and near/far recipients. No human recognition or preference measurements are inferred.",
            assertions, scenes
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Area review: {scenes.Count} native images, {assertions.Count} assertions.");
    }

    private static GameSession BuildAreaReviewCrowd(GameContent content, int count, int debuffCount,
        IReadOnlyList<StatusType>? debuffOrder = null)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var sources = PlaceCrowdedPilotSources(session);
        for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
        for (var index = 0; index < count; index++)
        {
            session.SpawnEnemy(AreaReviewFamilies[index % AreaReviewFamilies.Length], 160, .45f);
            var enemy = session.Enemies[^1];
            enemy.SetSandboxPathDistance(80 + index * (session.Map.Path.TotalLength - 350) / count, session.Map.Path);
            for (var statusIndex = 0; statusIndex < debuffCount; statusIndex++)
            {
                var type = (debuffOrder ?? AreaReviewDebuffs)[statusIndex];
                enemy.ApplyStatus(new StatusApplication
                {
                    Type = type, Duration = 90, SourceId = sources.GetValueOrDefault(type),
                    Magnitude = type == StatusType.Burn ? 2 : type == StatusType.ArmorBreak ? 4 : type == StatusType.Stun ? 1 : .35f,
                    ArmorPierce = type == StatusType.Burn ? 4 : 0
                });
            }
        }
        return session;
    }

    private void ProfileDenseEnemyAreas(GameContent content, SpriteFont font, SpriteFont display)
    {
        const int warmupFrames = 90;
        const int measuredFrames = 240;
        const int repeats = 3;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();
        HideAndDisableActivation();
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        var runs = new List<object>();
        var originalRenderer = _renderer;
        try
        {
            foreach (var count in new[] { 64, 128 })
            foreach (var reduced in new[] { false, true })
            for (var repeat = 0; repeat < repeats; repeat++)
            {
                var session = BuildAreaReviewCrowd(content, count, 5);
                _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = reduced };
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var initialChecksum = SessionChecksum.Compute(session, 0);
                var frames = new List<object>(measuredFrames);
                var presentTimes = new List<double>(measuredFrames);
                var renderTimes = new List<double>(measuredFrames);
                long lastPresent = 0, initialAllocated = 0;
                for (var frame = 0; frame < warmupFrames + measuredFrames; frame++)
                {
                    if (frame == warmupFrames) initialAllocated = GC.GetTotalAllocatedBytes();
                    session.Update(1f / 60);
                    ui.AdvanceVisualTime(1f / 60);
                    var metricsBefore = GraphicsDevice.Metrics;
                    var renderStart = Stopwatch.GetTimestamp();
                    RenderEnemyEvidenceScene(ui, session, target);
                    var renderMilliseconds = Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
                    var metrics = GraphicsDevice.Metrics - metricsBefore;
                    GraphicsDevice.Clear(ColorPalette.Canvas);
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                    _batch.Draw(target, new Rectangle(0, 0, 1280, 720), Color.White);
                    _batch.End();
                    GraphicsDevice.Present();
                    var present = Stopwatch.GetTimestamp();
                    if (frame >= warmupFrames)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(lastPresent, present).TotalMilliseconds;
                        presentTimes.Add(elapsed); renderTimes.Add(renderMilliseconds);
                        frames.Add(new { frame = frame - warmupFrames, presentToPresentMilliseconds = elapsed,
                            renderSubmissionMilliseconds = renderMilliseconds,
                            metrics.DrawCount, metrics.SpriteCount, metrics.PrimitiveCount,
                            enemyGlyphs = _renderer.LastEnemyGlyphCount,
                            auraRegions = AuraRegionCounter?.GetValue(_renderer) as int?,
                            enemies = session.Enemies.Count });
                    }
                    lastPresent = present;
                }
                var ordered = presentTimes.Order().ToArray();
                var renderOrdered = renderTimes.Order().ToArray();
                var run = new { count, debuffsPerEnemy = 5, fogRegionsPerEnemy = 4, reducedEffects = reduced, repeat,
                    warmupFrames, measuredFrames, initialChecksum,
                    endChecksum = SessionChecksum.Compute(session, warmupFrames + measuredFrames),
                    medianMilliseconds = ordered[ordered.Length / 2],
                    p95Milliseconds = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1],
                    medianRenderSubmissionMilliseconds = renderOrdered[renderOrdered.Length / 2],
                    allocatedBytes = GC.GetTotalAllocatedBytes() - initialAllocated,
                    framesAbove16_67Milliseconds = presentTimes.Count(value => value > 1000d / 60), frames };
                runs.Add(run);
                File.WriteAllText(Path.Combine(_outputDirectory, $"area-profile-{count}-{(reduced ? "reduced" : "full")}-{repeat}.json"),
                    JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Area profile {count} {(reduced ? "reduced" : "full")} repeat{repeat}: median {ordered[ordered.Length / 2]:F3}ms render {renderOrdered[renderOrdered.Length / 2]:F3}ms.");
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "area-profile.json"), JsonSerializer.Serialize(new
        {
            device = GraphicsDevice.Adapter.Description, platform = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(),
            width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440,
            timing = "Hidden no-vsync Present-to-Present wall time includes fixed simulation, UI, rendering, native downsample and driver/swap wait. Submission timer covers scene draw only and is not GPU time. No PNG/readback or per-frame checksums occur during timing. Counts64/128, five long-lived debuffs each, three repeated runs in each effects mode; not ordinary gameplay FPS.",
            fixture = "Synthetic scale load using the live session and status controllers. Health160x, speed45%, five legal debuffs including Stun but four fog regions; disabled legal tower sources. Natural shields/regen remain authoritative. Identical fixture code and initial/end checksums provide before/after controls.",
            warmupFrames, measuredFrames, repeats, runs
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
