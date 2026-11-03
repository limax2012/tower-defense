using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyFogTransitions(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        AssertFogColorShape(content, assertions);
        foreach (var family in AreaReviewFamilies)
        foreach (var count in new[] { 1, 2, 4 })
        foreach (var reduced in new[] { false, true })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            session.SpawnEnemy(family, 1, 1);
            var enemy = session.Enemies.Single();
            var timeline = new EnemyVisualTimeline();
            timeline.Observe(session, 0, false);
            ApplyFogReviewConditions(enemy, count);
            var checksum = SessionChecksum.Compute(session, 0);
            timeline.Observe(session, .1f, false);
            var start = timeline.FogFeedback(enemy, .1f);
            var rising = timeline.FogFeedback(enemy, .15f);
            var settled = timeline.FogFeedback(enemy, .21f);
            var body = RenderEnemyFogProbe(session, enemy, .23f, reduced, false);
            var clear = RenderEnemyFogProbe(session, enemy, .23f, reduced, true, start);
            var half = RenderEnemyFogProbe(session, enemy, .23f, reduced, true, rising);
            var full = RenderEnemyFogProbe(session, enemy, .23f, reduced, true, settled);
            Require(clear.Pixels.SequenceEqual(body.Pixels) && !half.Pixels.SequenceEqual(body.Pixels) &&
                    !half.Pixels.SequenceEqual(full.Pixels),
                $"{family}, {count} colors, {(reduced ? "Reduced" : "Full")}: fog has transparent, partial and settled raster states.", assertions);
            Require(half.Sprites - body.Sprites == (reduced ? 1 : 2) &&
                    full.Sprites - body.Sprites == (reduced ? 1 : 2),
                $"{family}, {count} colors: whole-cloud transitions keep the same bounded sprite count.", assertions);
            Require(checksum == SessionChecksum.Compute(session, 0),
                $"{family}: rendering fog transitions leaves simulation unchanged.", assertions);
            enemy.StatusEffects.Update(40);
            timeline.Observe(session, .4f, false);
            var fading = RenderEnemyFogProbe(session, enemy, .23f, reduced, true, timeline.FogFeedback(enemy, .47f));
            var gone = RenderEnemyFogProbe(session, enemy, .23f, reduced, true, timeline.FogFeedback(enemy, .55f));
            Require(!fading.Pixels.SequenceEqual(body.Pixels) && gone.Pixels.SequenceEqual(body.Pixels),
                $"{family}, {count} colors: ended conditions dissipate and leave no residual overlay.", assertions);
        }

        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_FOG_TRANSITIONS") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "fog-lifecycle"));
        var review = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var family in AreaReviewFamilies) review.SpawnEnemy(family, 1, 1);
        var visual = new EnemyVisualTimeline();
        var frames = new List<object>();
        for (var frame = 0; frame < 180; frame++)
        {
            var time = frame / 30f;
            foreach (var enemy in review.Enemies)
            {
                if (frame == 12) Apply(StatusType.Burn, 4);
                if (frame == 39) Apply(StatusType.Slow, 2.4f);
                if (frame == 66) Apply(StatusType.ArmorBreak, 2);
                if (frame == 84) Apply(StatusType.Exposed, .5f);
                if (frame == 150) Apply(StatusType.Burn, .5f);
                void Apply(StatusType type, float duration) => enemy.ApplyStatus(new StatusApplication
                {
                    Type = type, Duration = duration,
                    Magnitude = type == StatusType.Burn ? 2 : type == StatusType.ArmorBreak ? 4 : .35f
                });
            }
            visual.Observe(review, time, false);
            var checksum = SessionChecksum.Compute(review, 0);
            using var scene = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(scene);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
            MechanicalReviewLabel(font, "DEBUFF FOG / APPLICATION, MIXING AND DISSIPATION", new Vector2(28, 24), .72f);
            MechanicalReviewLabel(font, $"{time:0.00} SECONDS / NATIVE SIZE ABOVE / 3X DETAIL BELOW", new Vector2(28, 62), .45f);
            for (var index = 0; index < review.Enemies.Count; index++)
            {
                var enemy = review.Enemies[index];
                var center = new Vector2(145 + index * 245, 196);
                MechanicalReviewLabel(font, enemy.Definition.DisplayName.ToUpperInvariant(), center + new Vector2(-64, -82), .47f);
                EnemyModelArt.Draw(_batch, _primitives, enemy, center, -MathHelper.PiOver2, .23f, false);
                EnemyDebuffAuraArt.Draw(_batch, _primitives, review, enemy, center,
                    EnemyModelArt.BodyRadius(enemy), time, false, visual.FogFeedback(enemy, time));
            }
            MechanicalReviewLabel(font, "ONE / TWO / THREE / FOUR / THREE / TWO / ONE / CLEAR / REAPPLICATION",
                new Vector2(28, 658), .42f);
            _batch.End();
            for (var index = 0; index < review.Enemies.Count; index++)
            {
                var enemy = review.Enemies[index];
                var center = new Vector2(145 + index * 245, 455);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null,
                    Matrix.CreateScale(3) * Matrix.CreateTranslation(center.X, center.Y, 0) * Matrix.CreateScale(GameConstants.RenderScale));
                EnemyModelArt.Draw(_batch, _primitives, enemy, Vector2.Zero, -MathHelper.PiOver2, .23f, false);
                EnemyDebuffAuraArt.Draw(_batch, _primitives, review, enemy, Vector2.Zero,
                    EnemyModelArt.BodyRadius(enemy), time, false, visual.FogFeedback(enemy, time));
                _batch.End();
            }
            GraphicsDevice.SetRenderTarget(null);
            using var native = DownsampleEnemyLaneScene(scene);
            var saved = SaveTierScene(native, $"fog-lifecycle/frame-{frame:000}.png");
            if (frame is 0 or 13 or 24 or 48 or 75 or 90 or 103 or 118 or 130 or 135 or 154) scenes.Add(saved);
            Require(checksum == SessionChecksum.Compute(review, 0),
                $"Fog lifecycle frame {frame}: rendering preserves enemy state.", assertions);
            frames.Add(new { frame, time, saved.File, saved.Sha256,
                opacity = review.Enemies.Select(enemy => visual.FogFeedback(enemy, time).Opacity.ToString()).ToArray() });
            foreach (var enemy in review.Enemies) enemy.StatusEffects.Update(1f / 30);
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "fog-lifecycle.json"), JsonSerializer.Serialize(new
        {
            fps = 30, seconds = 6, width = 1280, height = 720,
            description = "Five static enemy poses with live presentation opacity and fog motion; no movement or tower attacks.", frames
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private void AssertFogColorShape(GameContent content, ICollection<string> assertions)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        session.SpawnEnemy("t4_aegis", 1, 1);
        var enemy = session.Enemies.Single();
        foreach (var reduced in new[] { false, true })
        {
            var reference = Pixels(1, ColorPalette.Expose, reduced);
            foreach (var precision in new[] { false, true })
            for (var mask = 1; mask <= 15; mask++)
            {
                var pixels = Pixels(mask, precision ? ColorPalette.PrecisionShot : ColorPalette.Expose, reduced);
                var largestAlphaChange = pixels.Zip(reference, (a, b) => Math.Abs(a.A - b.A)).Max();
                Require(largestAlphaChange <= 1,
                    $"Fog mask {mask}, {(precision ? "precision" : "Prism")} Expose, {(reduced ? "Reduced" : "Full")}: changing colors preserves the complete rendered alpha field (max {largestAlphaChange}/255).", assertions);
            }
        }

        Color[] Pixels(int mask, Color expose, bool reduced)
        {
            using var target = new RenderTarget2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Transparent);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            EnemyDebuffAuraArt.Draw(_batch, _primitives, session, enemy, new Vector2(64),
                EnemyModelArt.BodyRadius(enemy), .43f, reduced, new EnemyFogPresentation(
                    new Vector4(mask & 1, (mask >> 1) & 1, (mask >> 2) & 1, (mask >> 3) & 1), expose));
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[256 * 256];
            target.GetData(pixels);
            return pixels;
        }
    }

}
