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
    private static readonly float[] StunReviewDurations = [.25f, .55f, .85f, 1.06624f];
    private sealed record StunReviewSample(GameSession Session, EnemyInstance Enemy, float Duration);

    private void AssertEnemyStunPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var measurements = new List<object>();
        Span<EnemyCondition> fog = stackalloc EnemyCondition[EnemyDebuffAuraArt.MaximumRegions];
        foreach (var family in AreaReviewFamilies)
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.SetSandboxPathDistance(430, session.Map.Path);
            foreach (var reduced in new[] { false, true })
            {
                var inactive = RenderEnemyStunProbe(enemy, .23f, reduced);
                Require(inactive.Sprites == 0 && inactive.Pixels.All(pixel => pixel == ColorPalette.Canvas),
                    $"{family}: unstunned enemies have no electrical overlay in {(reduced ? "Reduced" : "Full")} Effects.", assertions);
            }
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            var checksum = SessionChecksum.Compute(session, 0);
            foreach (var reduced in new[] { false, true })
            {
                var first = RenderEnemyStunProbe(enemy, .23f, reduced);
                var later = RenderEnemyStunProbe(enemy, .32f, reduced);
                var changed = first.Pixels.Where((pixel, index) => pixel != later.Pixels[index]).Count();
                var blue = first.Pixels.Count(pixel => pixel.B > pixel.R * 1.2f && pixel.B > pixel.G * 1.05f && pixel.B > 40);
                Require(blue >= 4 && first.Sprites is > 0 && first.Sprites <= EnemyStunArt.MaximumSprites &&
                        later.Sprites <= EnemyStunArt.MaximumSprites,
                    $"{family}: visible Arc-blue short circuits use bounded drawing work ({first.Sprites}/{later.Sprites} sprites).", assertions);
                Require(reduced ? changed == 0 : changed > 8,
                    $"{family}: {(reduced ? "Reduced Effects holds a steady electrical read" : "fine discharge paths change rapidly through the stunned body")} ({changed} changed pixels).", assertions);
                var extent = (EnemyModelArt.BodyRadius(enemy) + 10) * 2;
                Require(first.Pixels.Where((pixel, index) => Math.Abs(index % 256 - 128) > extent ||
                        Math.Abs(index / 256 - 128) > extent).All(pixel => pixel == ColorPalette.Canvas),
                    $"{family}: electrical discharges stay near the stunned chassis.", assertions);
                measurements.Add(new { family, reduced, changedPixels = changed, bluePixels = blue,
                    firstSprites = first.Sprites, laterSprites = later.Sprites });
            }
            Require(EnemyDebuffAuraArt.CopyDebuffs(enemy, fog) == 0 && enemy.CurrentSpeed == 0 &&
                    checksum == SessionChecksum.Compute(session, 0),
                $"{family}: short-circuit rendering preserves the stopped movement, simulation state, and absence of Stun fog.", assertions);
            enemy.StatusEffects.Update(.56f);
            foreach (var reduced in new[] { false, true })
                Require(RenderEnemyStunProbe(enemy, .79f, reduced).Sprites == 0,
                    $"{family}: electrical state disappears on actual Stun expiry in {(reduced ? "Reduced" : "Full")} Effects.", assertions);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            enemy.ApplyHealthDamage(enemy.MaxHealth);
            Require(RenderEnemyStunProbe(enemy, .23f, false).Sprites == 0,
                $"{family}: death suppresses all short-circuit drawing immediately.", assertions);
        }
        var samples = BuildStunReviewSamples(content);
        foreach (var sample in samples) ApplyStunReviewPulse(sample.Enemy, sample.Duration);
        scenes.Add(CaptureEnemyStunSheet(samples, font, .23f, false, "enemy-stun-full.png"));
        scenes.Add(CaptureEnemyStunSheet(samples, font, .23f, true, "enemy-stun-reduced.png"));
        scenes.Add(CaptureEnemyStunDetail(samples.Take(5).ToArray(), font, .23f, "enemy-stun-detail.png"));
        File.WriteAllText(Path.Combine(_outputDirectory, "enemy-stun-measurements.json"),
            JsonSerializer.Serialize(new { width = 1280, height = 720, measurements },
                new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_STUN_ANIMATION") == "1")
            CaptureEnemyStunAnimation(content, font, display, assertions);
    }

    private (Color[] Pixels, long Sprites) RenderEnemyStunProbe(EnemyInstance enemy, float time, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        var before = GraphicsDevice.Metrics;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        EnemyStunArt.Draw(_batch, _primitives, enemy, new Vector2(64), EnemyModelArt.BodyRadius(enemy),
            -MathHelper.PiOver2, time, reduced);
        _batch.End();
        var sprites = GraphicsDevice.Metrics.SpriteCount - before.SpriteCount;
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[256 * 256];
        target.GetData(pixels);
        return (pixels, sprites);
    }

    private static List<StunReviewSample> BuildStunReviewSamples(GameContent content)
    {
        var samples = new List<StunReviewSample>();
        foreach (var duration in StunReviewDurations)
        foreach (var family in AreaReviewFamilies)
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.AbsorbShield(enemy.Shield);
            samples.Add(new(session, enemy, duration));
        }
        return samples;
    }

    private static void ApplyStunReviewPulse(EnemyInstance enemy, float duration) =>
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = duration, Magnitude = 1 });

    private VisualVerificationScene CaptureEnemyStunSheet(IReadOnlyList<StunReviewSample> samples,
        SpriteFont font, float time, bool reduced, string fileName)
    {
        using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        MechanicalReviewLabel(font, "SHORT CIRCUIT / NATIVE GAMEPLAY SIZE", new Vector2(28, 24), .8f);
        MechanicalReviewLabel(font, reduced ? "REDUCED EFFECTS / STILL CHASSIS, STEADY CONTACTS" :
            "FULL EFFECTS / FINE DISCHARGES AND SHELL SHUDDER", new Vector2(28, 64), .48f);
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var row = index / 5;
            var center = new Vector2(145 + index % 5 * 245, 182 + row * 137);
            _primitives.FillRect(_batch, new Rectangle((int)center.X - 112, (int)center.Y - 60, 224, 122), ColorPalette.Panel);
            if (row == 0) MechanicalReviewLabel(font, sample.Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(center.X - 64, 108), .46f);
            DrawStunReviewBody(sample.Enemy, center, time, reduced);
            MechanicalReviewLabel(font, $"{sample.Duration:0.00}s PULSE / {(sample.Enemy.StatusEffects.IsStunned ? "STUNNED" : "CLEAR")}",
                new Vector2(center.X - 82, center.Y + 50), .37f);
        }
        MechanicalReviewLabel(font, "Pulse durations use Standard-rank gameplay values. Poses stay in place for direct electrical review.",
            new Vector2(28, 676), .43f);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(scene);
        return SaveTierScene(native, fileName);
    }

    private void DrawStunReviewBody(EnemyInstance enemy, Vector2 center, float time, bool reduced)
    {
        EnemyModelArt.Draw(_batch, _primitives, enemy, center, -MathHelper.PiOver2, time, reduced);
        EnemyStunArt.Draw(_batch, _primitives, enemy, center, EnemyModelArt.BodyRadius(enemy),
            -MathHelper.PiOver2, time, reduced);
    }

    private VisualVerificationScene CaptureEnemyStunDetail(IReadOnlyList<StunReviewSample> samples,
        SpriteFont font, float time, string fileName)
    {
        using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        MechanicalReviewLabel(font, "SHORT CIRCUIT / DISCHARGE DETAIL", new Vector2(28, 24), .8f);
        MechanicalReviewLabel(font, "3x CONSTRUCTION DETAIL ABOVE / NATIVE GAMEPLAY SIZE BELOW", new Vector2(28, 64), .48f);
        for (var index = 0; index < samples.Count; index++)
            MechanicalReviewLabel(font, samples[index].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(70 + index * 245, 112), .46f);
        _batch.End();
        foreach (var zoom in new[] { 3f, 1f })
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(GameConstants.RenderScale * zoom));
            for (var index = 0; index < samples.Count; index++)
                DrawStunReviewBody(samples[index].Enemy, new Vector2(145 + index * 245, zoom > 1 ? 300 : 550) / zoom,
                    time, false);
            _batch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(scene);
        return SaveTierScene(native, fileName);
    }

    private void CaptureEnemyStunAnimation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions)
    {
        var modes = Environment.GetEnvironmentVariable("MAXIMAL_BASTION_STUN_REDUCED_ANIMATION") == "1"
            ? new[] { false, true } : new[] { false };
        var previousRenderer = _renderer;
        var frames = new List<object>();
        try
        {
            foreach (var reduced in modes)
            {
                var mode = reduced ? "reduced" : "full";
                Directory.CreateDirectory(Path.Combine(_outputDirectory, $"stun-sheet-{mode}"));
                Directory.CreateDirectory(Path.Combine(_outputDirectory, $"stun-crowd-{mode}"));
                var samples = BuildStunReviewSamples(content);
                var crowd = BuildAreaReviewCrowd(content, 20, 0);
                for (var index = 0; index < crowd.Enemies.Count; index++)
                    if (index % 2 == 0) ApplyFogReviewConditions(crowd.Enemies[index], 3);
                crowd.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), crowd);
                _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = reduced };
                for (var frame = 0; frame < 180; frame++)
                {
                    if (frame % 60 == 12)
                        foreach (var sample in samples) ApplyStunReviewPulse(sample.Enemy, sample.Duration);
                    for (var index = 0; index < crowd.Enemies.Count; index++)
                        if (frame % 60 == 12 + index % 5 * 2)
                            ApplyStunReviewPulse(crowd.Enemies[index], StunReviewDurations[index % 4]);
                    var checksum = SessionChecksum.Compute(crowd, frame * 2);
                    var sheet = CaptureEnemyStunSheet(samples, font, frame / 30f, reduced,
                        $"stun-sheet-{mode}/frame-{frame:000}.png");
                    using var scene = RenderScene(ui, GameState.Playing, crowd);
                    using var native = DownsampleEnemyLaneScene(scene);
                    var field = SaveTierScene(native, $"stun-crowd-{mode}/frame-{frame:000}.png");
                    Require(checksum == SessionChecksum.Compute(crowd, frame * 2),
                        $"Stun {mode} animation frame {frame}: drawing preserves the live simulation checksum.", assertions);
                    frames.Add(new { frame, reduced, seconds = frame / 30f, checksum,
                        sheet = sheet.File, crowded = field.File,
                        stunned = crowd.Enemies.Count(enemy => enemy.StatusEffects.IsStunned),
                        fogRegions = _renderer.LastEnemyAuraRegionCount });
                    foreach (var sample in samples)
                    {
                        sample.Enemy.StatusEffects.Update(1f / 60);
                        sample.Enemy.StatusEffects.Update(1f / 60);
                    }
                    crowd.Update(1f / 60);
                    crowd.Update(1f / 60);
                    ui.AdvanceVisualTime(1f / 30);
                }
            }
        }
        finally { _renderer = previousRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "enemy-stun-animation.json"),
            JsonSerializer.Serialize(new
            {
                width = 1280, height = 720, fps = 30, seconds = 6, durations = StunReviewDurations,
                fixture = "Three repeated groups of actual-duration Stun applications. The sheet holds poses in place; the lane advances live movement and status timers on twenty high-health targets. Every other lane target also carries Burn, Slow and Expose. Stuns are applied directly for a controlled effect review, not through tower targeting; no performance timing or player recognition score is inferred.",
                frames
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
