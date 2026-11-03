using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CapturePrismDisruptionReview(GameContent content, SpriteFont font,
        ICollection<VisualVerificationScene> scenes)
    {
        scenes.Add(CapturePrismDisruptionComparison(content, font, "prism-disruption-comparison.png"));
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_PRISM_DISRUPTION_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "prism-disruption-animation"));
        const int frames = 150;
        const int fps = 30;
        for (var frame = 0; frame < frames; frame++)
        {
            var saved = CapturePrismDisruptionComparison(content, font,
                $"prism-disruption-animation/frame-{frame:000}.png", frame / (float)fps);
            if (frame is 0 or 33 or 48 or 69 or 120) scenes.Add(saved);
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "prism-disruption-animation.json"), JsonSerializer.Serialize(new
        {
            width = 1280, height = 720, fps, frames,
            description = "Controlled renderer review: live tower and enemy artwork with fixed poses, reference panels, and authored signal timing.",
            activationSeconds = 1,
            transferSeconds = .38f,
            pauseSeconds = EnemySignalTuning.DisruptorPause(content.Challenges["close_quarters"], EnemyRank.Standard)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private VisualVerificationScene CapturePrismDisruptionComparison(GameContent content, SpriteFont font,
        string fileName, float? animationTime = null)
    {
        const int stage = 3;
        const float activation = 1;
        const float transferDuration = .38f;
        var definition = content.Towers["prism_beam"];
        var sample = SignalModuleSample(content, "t3_brute", EnemySignalRole.Disruptor, "CLEAR");
        var pause = EnemySignalTuning.DisruptorPause(content.Challenges["close_quarters"], sample.Enemy.Rank);
        var time = animationTime ?? activation + .15f;
        var elapsed = time - activation;
        var middleState = animationTime is null || elapsed is >= 0 && elapsed < pause ? 1 : elapsed < 0 ? 0 : 2;
        using var target = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "PRISM BEAM / PURPLE DISRUPTION", new Vector2(26, 20), .94f);
        StatusLabel(font, "CONTROLLED RENDER REVIEW / T3 PRISM / NATIVE HARDWARE ABOVE / 3x HARDWARE BELOW",
            new Vector2(26, 60), .54f);
        var titles = new[] { "NORMAL / FIRING", middleState == 1 ? "DISRUPTED / DISABLED" :
            middleState == 0 ? "BEFORE DISRUPTION" : "RECOVERED / FIRING", "RECOVERED / FIRING" };
        for (var column = 0; column < 3; column++)
        {
            var x = 20 + column * 420;
            _primitives.FillRect(_batch, new Rectangle(x, 108, 400, 562), ColorPalette.Panel);
            StatusLabel(font, titles[column], new Vector2(x + 16, 124), .67f);
            StatusLabel(font, column == 1 && animationTime.HasValue ? $"SEQUENCE / {time:0.00}s" : "REFERENCE STATE",
                new Vector2(x + 16, 153), .45f);
            StatusLabel(font, "1x", new Vector2(x + 16, 187), .5f);
            StatusLabel(font, "3x", new Vector2(x + 16, 342), .5f);
            StatusLabel(font, "DISRUPTOR", new Vector2(x + 30, 284), .46f);
            StatusLabel(font, "PRISM BEAM", new Vector2(x + 261, 284), .46f);
            var state = column == 1 ? middleState : column == 0 ? 0 : 2;
            StatusLabel(font, state == 1 ? "PURPLE FAULT CONTACTS / OPTICS DIMMED" : "LAVENDER OPTICS / CONTINUOUS BEAM SHAPE",
                new Vector2(x + 16, 586), .43f);
            StatusLabel(font, state == 1 ? $"FIRING PAUSED / {MathF.Max(0, pause - elapsed):0.00}s LEFT" : "TOWER CAN FIRE",
                new Vector2(x + 16, 615), .46f);
        }
        _primitives.FillRect(_batch, new Rectangle(30, 689, 18, 13), ColorPalette.Disruption);
        StatusLabel(font, "DISRUPTION / SATURATED PURPLE", new Vector2(58, 686), .46f);
        _primitives.FillRect(_batch, new Rectangle(500, 689, 18, 13), ColorPalette.Violet);
        StatusLabel(font, "PRISM / LAVENDER VIOLET", new Vector2(528, 686), .46f);
        StatusLabel(font, $"SIGNAL {transferDuration:0.00}s / PAUSE {pause:0.00}s", new Vector2(925, 686), .46f);
        _batch.End();

        for (var column = 0; column < 3; column++)
        {
            var x = 20 + column * 420;
            var state = column == 1 ? middleState : column == 0 ? 0 : 2;
            var transfer = state == 1 ? Math.Clamp(1 - elapsed / transferDuration, 0, 1) : 0;
            DrawPair(new Vector2(x + 72, 237), new Vector2(x + 300, 237), 1, state, transfer);
            DrawPair(new Vector2(x + 77, 470), new Vector2(x + 300, 470), 3, state, transfer);
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);

        void DrawPair(Vector2 enemyAt, Vector2 towerAt, float scale, int state, float transfer)
        {
            var towerLocal = (towerAt - enemyAt) / scale;
            DrawTowerSignalSample(definition, stage, state == 1 ? 2 : 0, towerAt,
                scale, MathHelper.Pi, time, false);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(enemyAt.X, enemyAt.Y, 0));
            if (state == 1)
                EnemySignalArt.DrawTransfer(_batch, _primitives, Vector2.Zero, towerLocal,
                    EnemySignalRole.Disruptor, transfer, time, false);
            else
            {
                var shotAge = time % (1f / definition.Levels[stage - 1].AttacksPerSecond);
                var progress = animationTime.HasValue ? Math.Clamp(1 - shotAge / .15f, 0, 1) : .82f;
                var muzzle = towerLocal - Vector2.UnitX * definition.Visual.Radius * TowerTierVisuals.PrismBarrelLength(stage, false);
                PrismBeamArt.Draw(_batch, _primitives, muzzle, Vector2.Zero,
                    NightGridArt.TowerAccent(definition.Id), 2, progress, false, true, stage);
            }
            EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero, 0, time, false, atRest: true);
            if (state == 1 && elapsed < .24f)
                EnemySignalArt.DrawEmitterPulse(_batch, _primitives, sample.Enemy, Vector2.Zero,
                    EnemyModelArt.BodyRadius(sample.Enemy), 0, Math.Clamp(1 - elapsed / .24f, 0, 1), time, false);
            _batch.End();
        }
    }

    private VisualVerificationScene CapturePrismDisruptionNativeState(UIManager ui, GameSession session, string state)
    {
        using var scene = RenderScene(ui, GameState.Playing, session);
        using var native = DownsampleEnemyLaneScene(scene);
        return SaveTierScene(native, $"prism-disruption-live-{state}.png");
    }
}
