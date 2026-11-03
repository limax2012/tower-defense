using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertSignalTransmissions(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        scenes.Add(CaptureSignalTransmissionSheet(content, font, "59e-enemy-transmissions.png"));
        foreach (var role in new[] { EnemySignalRole.Jammer, EnemySignalRole.Disruptor })
        foreach (var reduced in new[] { false, true })
        {
            var first = SignalTransmissionPixels(role, .72f, reduced);
            var later = SignalTransmissionPixels(role, .42f, reduced);
            var visible = 0;
            var upper = 0;
            var lower = 0;
            for (var y = 0; y < 64; y++)
            for (var x = 32; x < 224; x++)
            {
                var pixel = first[y * 256 + x];
                if (pixel.R + pixel.G + pixel.B < 155) continue;
                visible++;
                if (y < 30) upper++;
                if (y > 34) lower++;
            }
            Require(visible >= 180 && upper >= 40 && lower >= 40,
                $"{role} {(reduced ? "Reduced" : "Full")}: native-size transmissions retain broad, visible transverse geometry ({visible} bright pixels, {upper} above / {lower} below).", assertions);
            Require(reduced ? first.SequenceEqual(later) : !first.SequenceEqual(later),
                $"{role}: packet motion is visible in Full Effects and remains steady in Reduced Effects.", assertions);
            Require(SignalTransmissionPixels(role, 0, reduced).All(pixel => pixel == ColorPalette.Canvas),
                $"{role}: an expired transmission leaves no beam or terminal residue.", assertions);
        }
        var jam = SignalTransmissionPixels(EnemySignalRole.Jammer, .72f, false);
        var disrupt = SignalTransmissionPixels(EnemySignalRole.Disruptor, .72f, false);
        Require(jam.Zip(disrupt, (a, b) => a != b).Count(changed => changed) > 400,
            "Jammer wavefronts and Disruptor split-diamond packets have different native-size silhouettes and palettes.", assertions);
        Require(ColorPalette.Disruption.R > ColorPalette.Disruption.G * 2 &&
                ColorPalette.Disruption.B > ColorPalette.Disruption.R &&
                ColorPalette.Disruption.G + 60 < ColorPalette.Violet.G &&
                EnemySignalGlyphRenderer.Accent(EnemySignalRole.Disruptor) == ColorPalette.Disruption,
            "Disruption cores, transmissions and tower fault channels share a saturated purple distinct from Prism's pale violet.", assertions);
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_SIGNAL_SHOTS_ANIMATION") != "1") return;
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "signal-transmissions-animation"));
        for (var frame = 0; frame < 120; frame++)
        {
            var age = frame % 36 / 30f;
            scenes.Add(CaptureSignalTransmissionSheet(content, font,
                $"signal-transmissions-animation/frame-{frame:000}.png", Math.Clamp(1 - age / .38f, 0, 1), frame / 30f));
        }
    }

    private Color[] SignalTransmissionPixels(EnemySignalRole role, float progress, bool reduced)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 256, 64, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        EnemySignalArt.DrawTransfer(_batch, _primitives, new Vector2(16, 32), new Vector2(240, 32),
            role, progress, .43f, reduced);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[256 * 64];
        target.GetData(pixels);
        return pixels;
    }

    private VisualVerificationScene CaptureSignalTransmissionSheet(GameContent content, SpriteFont font,
        string fileName, float? animatedProgress = null, float time = .43f)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 1280, 720, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "ENEMY CORE SIGNALS / DIRECTED INTERFERENCE", new Vector2(30, 20), .94f);
        StatusLabel(font, "NATIVE SIZE ABOVE / 2x DETAIL BELOW    |    CORE, TRANSMISSION AND TOWER SHARE ONE COLOR", new Vector2(30, 60), .54f);
        _batch.End();
        var roles = new[] { EnemySignalRole.Jammer, EnemySignalRole.Disruptor };
        for (var row = 0; row < roles.Length; row++)
        {
            var role = roles[row];
            var sample = SignalModuleSample(content, "t3_brute", role, "CLEAR");
            var y = 110 + row * 295;
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            _primitives.FillRect(_batch, new Rectangle(18, y, 1244, 280), ColorPalette.Panel);
            StatusLabel(font, role == EnemySignalRole.Jammer ? "JAMMER / RADIO WAVEFRONTS" : "DISRUPTOR / FRACTURED DIAMONDS",
                new Vector2(35, y + 10), .66f);
            _batch.End();
            for (var column = 0; column < 3; column++)
            {
                var x = 36 + column * 408;
                var progress = animatedProgress ?? new[] { .88f, .63f, .30f }[column];
                DrawTransferAt(new Vector2(x + 40, y + 86), new Vector2(x + 330, y + 86), 1);
                DrawTransferAt(new Vector2(x + 75, y + 207), new Vector2(x + 290, y + 207), 2);
                void DrawTransferAt(Vector2 first, Vector2 last, float scale)
                {
                    var localLast = (last - first) / scale;
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(first.X, first.Y, 0));
                    EnemySignalArt.DrawTransfer(_batch, _primitives, Vector2.Zero, localLast, role, progress, time, false);
                    EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero, 0, time, false, atRest: true);
                    _batch.End();
                    DrawTowerSignalSample(content.Towers["needle_turret"], 2,
                        progress > 0 ? row + 1 : 0, last, scale, MathHelper.Pi, time, false);
                }
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        return SaveTierScene(target, fileName);
    }
}
