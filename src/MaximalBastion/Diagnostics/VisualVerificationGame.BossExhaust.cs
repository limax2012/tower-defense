using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertBossExhaust(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var rows = new List<Texture2D[]>();
        var labels = new[] { "RIGHT", "DOWN", "LEFT", "UP" };
        try
        {
            for (var direction = 0; direction < 4; direction++)
            {
                var sample = EnemyRankSample(content, "t5_regenerator", "Boss") with
                    { Heading = direction * MathHelper.PiOver2 };
                var enemy = sample.Enemy;
                enemy.AbsorbShield(enemy.Shield);
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload
                    { Damage = enemy.MaxHealth * .49f, IgnoreShield = true, ArmorPierce = 1000 });
                var timeline = new EnemyVisualTimeline();
                timeline.Observe(sample.Session, 0);
                var before = BossExhaustPixels(sample, .9f, false, timeline.Feedback(enemy, .9f));
                var row = new List<Texture2D> { ReadabilityTexture(before) };
                rows.Add(row.ToArray());
                var speed = enemy.CurrentSpeed;
                sample.Session.DamageResolver.Apply(enemy, new DamagePayload
                    { Damage = enemy.MaxHealth * .02f, IgnoreShield = true, ArmorPierce = 1000 });
                timeline.Observe(sample.Session, 1);
                Require(enemy.BossPhaseActive && MathF.Abs(enemy.CurrentSpeed / speed - 1.28f) < .0001f &&
                        MathF.Abs(enemy.Shield / enemy.MaxHealth - .12f) < .0001f,
                    $"Boss exhaust {labels[direction]}: half-health activation retains the authored speed and shield grants.", assertions);
                var checksum = SessionChecksum.Compute(sample.Session, 915);
                foreach (var age in new[] { .05f, .2f, .4f, 1.2f })
                    row.Add(ReadabilityTexture(BossExhaustPixels(sample, 1 + age, false,
                        timeline.Feedback(enemy, 1 + age))));
                rows[^1] = row.ToArray();

                var active = BossExhaustPixels(sample, 2.2f, false, timeline.Feedback(enemy, 2.2f));
                var next = BossExhaustPixels(sample, 2.28f, false, timeline.Feedback(enemy, 2.28f));
                var rear = BossExhaustRegion(sample.Heading);
                Require(rear.All(index => before[index].A == 0),
                    $"Boss exhaust {labels[direction]}: the rear outlets emit no flow above half health.", assertions);
                Require(rear.Count(index => active[index].A > 2) >= 12 &&
                        rear.Count(index => active[index] != next[index]) >= 6,
                    $"Boss exhaust {labels[direction]}: visible flow travels within both rear outlets at native gameplay size.", assertions);
                var rest = BossExhaustPixels(sample, 0, false, new(1, 0, 0, 0), atRest: true);
                Require(rear.All(index => rest[index].A == 0),
                    $"Boss exhaust {labels[direction]}: resting Intel artwork has no trailing exhaust outside the body.", assertions);
                var reduced = BossExhaustPixels(sample, 2.2f, true, timeline.Feedback(enemy, 2.2f));
                Require(reduced.SequenceEqual(BossExhaustPixels(sample, 2.8f, true, timeline.Feedback(enemy, 2.8f))),
                    $"Boss exhaust {labels[direction]}: Reduced Effects holds a steady chassis and exhaust presentation.", assertions);
                Require(checksum == SessionChecksum.Compute(sample.Session, 915),
                    $"Boss exhaust {labels[direction]}: time sampling, heading changes, reduced effects, and resting artwork preserve synchronized state.", assertions);
            }
            scenes.Add(CaptureReadabilityTransitionAtlas(rows, labels, font, "BASTION CORE / EXHAUST SHUTTERS",
                ["51% HEALTH", "+50ms / CLOSED", "+200ms / OPENING", "+400ms / FLOW", "+1.2s / STEADY"],
                "boss-exhaust-headings.png"));
        }
        finally { foreach (var row in rows) foreach (var texture in row) texture.Dispose(); }

        var fixture = BuildBossExhaustLane(content);
        var ui = BossExhaustUi(content, font, display, fixture.Session);
        var originalRenderer = _renderer;
        try
        {
            _renderer = new GameRenderer();
            using (var scene = RenderScene(ui, GameState.Playing, fixture.Session))
            using (var native = DownsampleEnemyLaneScene(scene))
                scenes.Add(SaveTierScene(native, "boss-exhaust-lane-before.png"));
            ActivateBossExhaust(fixture.Session, fixture.Boss);
            using (var onset = RenderScene(ui, GameState.Playing, fixture.Session)) { }
            for (var tick = 0; tick < 48; tick++)
            {
                fixture.Session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 60);
            }
            var checksum = SessionChecksum.Compute(fixture.Session, 916);
            using var active = RenderScene(ui, GameState.Playing, fixture.Session);
            using var nativeActive = DownsampleEnemyLaneScene(active);
            scenes.Add(SaveTierScene(nativeActive, "boss-exhaust-lane-active.png"));
            Require(checksum == SessionChecksum.Compute(fixture.Session, 916),
                "Boss exhaust in a mixed lane leaves the native gameplay render independent of synchronized state.", assertions);
        }
        finally { _renderer = originalRenderer; }
    }

    private Color[] BossExhaustPixels(StatusReviewEnemy sample, float time, bool reduced,
        EnemyVisualFeedback feedback, bool atRest = false)
    {
        using var source = new RenderTarget2D(GraphicsDevice, 256, 256, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(source);
        GraphicsDevice.Clear(Color.Transparent);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, new Vector2(64), sample.Heading, time, reduced,
            overdriveOpening: feedback.OverdriveOpening, atRest: atRest, castShadows: false,
            overdriveElapsed: feedback.OverdriveElapsed);
        _batch.End();
        using var native = new RenderTarget2D(GraphicsDevice, 128, 128, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(native);
        GraphicsDevice.Clear(Color.Transparent);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(source, new Rectangle(0, 0, 128, 128), Color.White);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[128 * 128];
        native.GetData(pixels);
        return pixels;
    }

    private static int[] BossExhaustRegion(float heading)
    {
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var indices = new List<int>();
        for (var y = 0; y < 128; y++)
        for (var x = 0; x < 128; x++)
        {
            var offset = new Vector2(x + .5f, y + .5f) - new Vector2(64);
            var along = Vector2.Dot(offset, forward);
            var across = MathF.Abs(Vector2.Dot(offset, side));
            if (along is < -35 and > -49 && MathF.Abs(across - 12.8f) < 3.5f)
                indices.Add(y * 128 + x);
        }
        return indices.ToArray();
    }

    private static (GameSession Session, EnemyInstance Boss) BuildBossExhaustLane(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
        var families = new[] { "t5_regenerator", "t4_aegis", "t3_brute", "t1_crawler" };
        for (var index = 0; index < families.Length; index++)
        {
            session.SpawnEnemy(families[index], 1, 1);
            session.Enemies[^1].SetSandboxPathDistance(750 - index * 60, session.Map.Path);
        }
        session.SpawnEnemy("t5_regenerator", 1, 1, "Boss");
        var boss = session.Enemies[^1];
        boss.SetSandboxPathDistance(850, session.Map.Path);
        boss.AbsorbShield(boss.Shield);
        session.DamageResolver.Apply(boss, new DamagePayload
            { Damage = boss.MaxHealth * .49f, IgnoreShield = true, ArmorPierce = 1000 });
        return (session, boss);
    }

    private static void ActivateBossExhaust(GameSession session, EnemyInstance boss) =>
        session.DamageResolver.Apply(boss, new DamagePayload
            { Damage = boss.MaxHealth * .02f, IgnoreShield = true, ArmorPierce = 1000 });

    private static UIManager BossExhaustUi(GameContent content, SpriteFont font, SpriteFont display,
        GameSession session)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        ui.HandleGameplayInput(Pointer(0, 0), session);
        return ui;
    }

    private void CaptureBossExhaustMotion(GameContent content, SpriteFont font, SpriteFont display)
    {
        var fixture = BuildBossExhaustLane(content);
        var ui = BossExhaustUi(content, font, display, fixture.Session);
        var frames = new List<object>();
        var originalRenderer = _renderer;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "boss-exhaust-motion"));
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "boss-exhaust-native"));
        try
        {
            _renderer = new GameRenderer();
            for (var frame = 0; frame < 90; frame++)
            {
                if (frame == 15) ActivateBossExhaust(fixture.Session, fixture.Boss);
                var checksum = SessionChecksum.Compute(fixture.Session, frame);
                using var scene = RenderScene(ui, GameState.Playing, fixture.Session);
                using var native = DownsampleEnemyLaneScene(scene);
                var saved = SaveTierScene(native, $"boss-exhaust-native/frame-{frame:000}.png");
                var center = PresentationFrame.Create(fixture.Session, 0).EnemyPosition(fixture.Boss);
                var crop = new Rectangle(Math.Clamp((int)center.X - 160, 0, 640),
                    Math.Clamp((int)center.Y - 80, 64, 560), 320, 160);
                using var board = new RenderTarget2D(GraphicsDevice, 1040, 424, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(board);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                StatusLabel(font, "BASTION CORE / EXHAUST SHUTTERS", new Vector2(20, 12), .72f);
                StatusLabel(font, "GAMEPLAY SIZE", new Vector2(20, 52), .5f);
                StatusLabel(font, "2x DETAIL", new Vector2(380, 52), .5f);
                _batch.Draw(native, new Rectangle(20, 162, 320, 160), crop, Color.White);
                _batch.Draw(scene, new Rectangle(380, 82, 640, 320),
                    new Rectangle(crop.X * 2, crop.Y * 2, crop.Width * 2, crop.Height * 2), Color.White);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
                SaveTierScene(board, $"boss-exhaust-motion/frame-{frame:000}.png");
                if (checksum != SessionChecksum.Compute(fixture.Session, frame))
                    throw new InvalidOperationException("Boss exhaust motion capture changed synchronized state.");
                frames.Add(new { frame, phaseSeconds = (frame - 15) / 30f, saved.File, saved.Sha256,
                    checksum, fixture.Boss.Health, fixture.Boss.Shield, fixture.Boss.CurrentSpeed,
                    fixture.Boss.BossPhaseActive, crop });
                for (var tick = 0; tick < 2; tick++) fixture.Session.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer = originalRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "boss-exhaust-motion.json"), JsonSerializer.Serialize(new
        {
            fps = 30, fixedSimulationHz = 60, phaseFrame = 15,
            fixture = "Mixed sandbox formation on Cinderworks, real damage crossing the half-health phase threshold, native gameplay frames and paired 1x/2x review crops.",
            frames
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
