using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void CaptureLegMotion(GameContent content, SpriteFont font,
        ICollection<VisualVerificationScene> scenes)
    {
        if (Environment.GetEnvironmentVariable("MAXIMAL_BASTION_LEG_MOTION") != "1") return;
        var families = new[] { "t1_crawler", "t3_brute", "t4_aegis", "t5_regenerator" };
        var ranks = new[] { "Standard", "Elite", "Boss" };
        var samples = families.SelectMany(id => ranks.Select(rank =>
            EnemyRankSample(content, id, rank) with { Heading = 0 })).ToArray();
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "leg-motion"));
        using var target = new RenderTarget2D(GraphicsDevice, 1200, 960, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        for (var frame = 0; frame < 72; frame++)
        {
            var time = frame / 24f;
            var distance = time * 36;
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            StatusLabel(font, "ENEMY LOCOMOTION / STANDARD, ELITE, BOSS", new Vector2(24, 14), .75f);
            StatusLabel(font, "2x DETAIL ABOVE / GAMEPLAY SIZE BELOW", new Vector2(24, 48), .55f);
            for (var index = 0; index < samples.Length; index++)
            {
                var x = index % 3 * 400;
                var y = 80 + index / 3 * 220;
                _primitives.FillRect(_batch, new Rectangle(x + 8, y, 384, 210), ColorPalette.Panel);
                StatusLabel(font, samples[index].Label, new Vector2(x + 18, y + 7), .52f);
                _primitives.Line(_batch, new Vector2(x + 38, y + 144),
                    new Vector2(x + 362, y + 144), ColorPalette.Metal * .3f, 1);
                _primitives.Line(_batch, new Vector2(x + 112, y + 205),
                    new Vector2(x + 288, y + 205), ColorPalette.Metal * .3f, 1);
            }
            _batch.End();
            for (var index = 0; index < samples.Length; index++)
            {
                var x = index % 3 * 400;
                var y = 80 + index / 3 * 220;
                DrawAt(new Vector2(x + 92 + distance * 2, y + 88), 2);
                DrawAt(new Vector2(x + 146 + distance, y + 177), 1);
                void DrawAt(Vector2 center, float scale)
                {
                    var sample = samples[index];
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X, center.Y, 0));
                    EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero,
                        sample.Heading, time, false, travelDistance: distance);
                    EnemyStatusArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero,
                        EnemyModelArt.BodyRadius(sample.Enemy), sample.Heading, time, false);
                    _batch.End();
                }
            }
            GraphicsDevice.SetRenderTarget(null);
            scenes.Add(SaveTierScene(target, $"leg-motion/frame-{frame:000}.png"));
        }
    }
}
