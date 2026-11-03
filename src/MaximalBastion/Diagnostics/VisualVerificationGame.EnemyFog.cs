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
    private static readonly StatusType[] FogReviewDebuffs =
        [StatusType.Burn, StatusType.Slow, StatusType.Exposed, StatusType.ArmorBreak];

    private void AssertEnemyFogAnimation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var samples = new List<(GameSession Session, EnemyInstance Enemy, int Count)>();
        var measurements = new List<object>();
        foreach (var count in new[] { 1, 2, 3, 4 })
        foreach (var family in AreaReviewFamilies)
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.AbsorbShield(enemy.Shield);
            ApplyFogReviewConditions(enemy, count);
            samples.Add((session, enemy, count));
            var checksum = SessionChecksum.Compute(session, 0);
            foreach (var reduced in new[] { false, true })
            {
                var body = RenderEnemyFogProbe(session, enemy, .23f, reduced, false);
                var first = RenderEnemyFogProbe(session, enemy, .23f, reduced, true);
                var later = RenderEnemyFogProbe(session, enemy, 1.67f, reduced, true);
                var changed = first.Pixels.Where((pixel, index) => pixel != later.Pixels[index]).Count();
                Require(reduced ? changed == 0 : changed >= 20,
                    $"{family}, {count} debuffs: {(reduced ? "Reduced Effects remains pixel-stable" : "Full Effects changes fog density over a frozen chassis")} ({changed} changed internal pixels).", assertions);
                Require(first.Regions == count && later.Regions == count &&
                        first.Sprites - body.Sprites == (reduced ? 1 : 2) &&
                        later.Sprites - body.Sprites == first.Sprites - body.Sprites,
                    $"{family}, {count} debuffs: {(reduced ? "one" : "two")} cached fog sprites per enemy bound rendering work independently of color count and animation time.", assertions);
                var extent = (EnemyModelArt.BodyRadius(enemy) + 12) * 2;
                Require(first.Pixels.Where((pixel, index) =>
                            Math.Abs(index % 256 - 128) > extent || Math.Abs(index / 256 - 128) > extent)
                        .SequenceEqual(body.Pixels.Where((pixel, index) =>
                            Math.Abs(index % 256 - 128) > extent || Math.Abs(index / 256 - 128) > extent)) &&
                        later.Pixels.Where((pixel, index) =>
                            Math.Abs(index % 256 - 128) > extent || Math.Abs(index / 256 - 128) > extent)
                        .SequenceEqual(body.Pixels.Where((pixel, index) =>
                            Math.Abs(index % 256 - 128) > extent || Math.Abs(index / 256 - 128) > extent)),
                    $"{family}, {count} debuffs: animated density remains local to the enemy envelope.", assertions);
                measurements.Add(new { family, count, reduced, changedPixels = changed,
                    fogSprites = first.Sprites - body.Sprites, regions = first.Regions });
            }
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{family}, {count} debuffs: temporal fog sampling leaves simulation state unchanged.", assertions);
        }
        foreach (var family in AreaReviewFamilies)
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            enemy.AbsorbShield(enemy.Shield);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .55f, Magnitude = 1 });
            var checksum = SessionChecksum.Compute(session, 0);
            foreach (var reduced in new[] { false, true })
            {
                var body = RenderEnemyFogProbe(session, enemy, .23f, reduced, false);
                var stunned = RenderEnemyFogProbe(session, enemy, .23f, reduced, true);
                Require(stunned.Regions == 0 && stunned.Sprites == body.Sprites && stunned.Pixels.SequenceEqual(body.Pixels),
                    $"{family}: Stun alone adds no fog color or sprite in {(reduced ? "Reduced" : "Full")} Effects.", assertions);
            }
            Require(checksum == SessionChecksum.Compute(session, 0) && enemy.CurrentSpeed == 0 &&
                    EnemyConditionPresentation.Compose(enemy).Has(EnemyCondition.Stun),
                $"{family}: Stun remains inspectable and stops movement without a fog region.", assertions);
            ApplyFogReviewConditions(enemy, 4);
            checksum = SessionChecksum.Compute(session, 0);
            foreach (var reduced in new[] { false, true })
            {
                var body = RenderEnemyFogProbe(session, enemy, .23f, reduced, false);
                var stacked = RenderEnemyFogProbe(session, enemy, .23f, reduced, true);
                Require(stacked.Regions == 4 && stacked.Sprites - body.Sprites == (reduced ? 1 : 2),
                    $"{family}: Stun with all four sustained debuffs still uses exactly four fog regions in {(reduced ? "Reduced" : "Full")} Effects.", assertions);
            }
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{family}: combined Stun and four-color fog rendering leaves simulation state unchanged.", assertions);
        }
        scenes.Add(CaptureEnemyFogSheet(samples, font, .23f, false, "enemy-fog-full-early.png"));
        scenes.Add(CaptureEnemyFogSheet(samples, font, 1.67f, false, "enemy-fog-full-later.png"));
        scenes.Add(CaptureEnemyFogSheet(samples, font, .23f, true, "enemy-fog-reduced.png"));
        File.WriteAllText(Path.Combine(_outputDirectory, "enemy-fog-measurements.json"),
            JsonSerializer.Serialize(new { width = 1280, height = 720, measurements },
                new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_FOG_ANIMATION") != "1") return;

        Directory.CreateDirectory(Path.Combine(_outputDirectory, "fog-isolated"));
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "fog-crowded"));
        var crowded = BuildAreaReviewCrowd(content, 20, 0);
        for (var index = 0; index < crowded.Enemies.Count; index++)
            ApplyFogReviewConditions(crowded.Enemies[index], new[] { 1, 2, 3, 4 }[index % 4]);
        crowded.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        ui.HandleGameplayInput(Pointer(0, 0), crowded);
        var previousRenderer = _renderer;
        var frames = new List<object>();
        try
        {
            _renderer = new GameRenderer { QuietEnemyHud = true, ReducedEffects = false };
            for (var frame = 0; frame < 150; frame++)
            {
                var checksum = SessionChecksum.Compute(crowded, frame * 2);
                var isolated = CaptureEnemyFogSheet(samples, font, frame / 30f, false,
                    $"fog-isolated/frame-{frame:000}.png");
                using var scene = RenderScene(ui, GameState.Playing, crowded);
                using var native = DownsampleEnemyLaneScene(scene);
                var field = SaveTierScene(native, $"fog-crowded/frame-{frame:000}.png");
                Require(checksum == SessionChecksum.Compute(crowded, frame * 2),
                    $"Fog animation frame {frame}: native crowded capture leaves combat unchanged.", assertions);
                frames.Add(new { frame, seconds = frame / 30f, checksum,
                    isolated = isolated.File, crowded = field.File,
                    regions = _renderer.LastEnemyAuraRegionCount,
                    glyphs = _renderer.LastEnemyGlyphCount });
                crowded.Update(1f / 60);
                crowded.Update(1f / 60);
                ui.AdvanceVisualTime(1f / 30);
            }
        }
        finally { _renderer = previousRenderer; }
        File.WriteAllText(Path.Combine(_outputDirectory, "enemy-fog-animation.json"),
            JsonSerializer.Serialize(new
            {
                width = 1280, height = 720, internalWidth = 2560, internalHeight = 1440,
                fps = 30, seconds = 5,
                fixture = "Isolated sheet holds body pose and simulation constant while advancing only fog time. Crowded footage uses twenty live sandbox enemies at raised health and reduced speed with one through four sustained debuffs. Three-color subjects combine Burn, Slow and Expose. No tower attacks or performance timings are inferred from these captures.",
                frames
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ApplyFogReviewConditions(EnemyInstance enemy, int count)
    {
        foreach (var type in FogReviewDebuffs.Take(count))
            enemy.ApplyStatus(new StatusApplication
            {
                Type = type, Duration = 30,
                Magnitude = type == StatusType.Burn ? 2 : type == StatusType.ArmorBreak ? 4 : .35f
            });
    }

    private (Color[] Pixels, long Sprites, int Regions) RenderEnemyFogProbe(GameSession session,
        EnemyInstance enemy, float time, bool reduced, bool aura, EnemyFogPresentation? presentation = null)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        var before = GraphicsDevice.Metrics;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        var center = new Vector2(64);
        EnemyModelArt.Draw(_batch, _primitives, enemy, center, -MathHelper.PiOver2, .23f, reduced);
        var regions = aura ? EnemyDebuffAuraArt.Draw(_batch, _primitives, session, enemy, center,
            EnemyModelArt.BodyRadius(enemy), time, reduced, presentation) : 0;
        _batch.End();
        var sprites = GraphicsDevice.Metrics.SpriteCount - before.SpriteCount;
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[256 * 256];
        target.GetData(pixels);
        return (pixels, sprites, regions);
    }

    private VisualVerificationScene CaptureEnemyFogSheet(
        IReadOnlyList<(GameSession Session, EnemyInstance Enemy, int Count)> samples,
        SpriteFont font, float time, bool reduced, string fileName)
    {
        using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(scene);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        MechanicalReviewLabel(font, "DEBUFF FOG / NATIVE GAMEPLAY SIZE", new Vector2(28, 24), .8f);
        MechanicalReviewLabel(font, reduced ? "REDUCED EFFECTS / STEADY COLOR REGIONS" : "FULL EFFECTS / FROZEN CHASSIS, MOVING PLUMES",
            new Vector2(28, 64), .48f);
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            var row = index / 5;
            var column = index % 5;
            var center = new Vector2(145 + column * 245, 183 + row * 137);
            _primitives.FillRect(_batch, new Rectangle((int)center.X - 112, (int)center.Y - 61, 224, 122), ColorPalette.Panel);
            if (row == 0)
                MechanicalReviewLabel(font, sample.Enemy.Definition.DisplayName.ToUpperInvariant(),
                    new Vector2(center.X - 64, 108), .46f);
            EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, center, -MathHelper.PiOver2, .23f, reduced);
            EnemyDebuffAuraArt.Draw(_batch, _primitives, sample.Session, sample.Enemy, center,
                EnemyModelArt.BodyRadius(sample.Enemy), time, reduced);
            var label = sample.Count switch { 1 => "BURN", 2 => "BURN + SLOW", 3 => "BURN + SLOW + EXPOSE", _ => "FOUR DEBUFF COLORS" };
            MechanicalReviewLabel(font, label,
                new Vector2(center.X - 78, center.Y + 49), .37f);
        }
        MechanicalReviewLabel(font, "Same body pose and debuff state at every time sample. Color zones remain world-facing.",
            new Vector2(28, 664), .44f);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(scene);
        return SaveTierScene(native, fileName);
    }
}
