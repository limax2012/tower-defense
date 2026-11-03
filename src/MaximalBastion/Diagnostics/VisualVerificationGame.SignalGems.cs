using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertSignalGemFaces(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var roles = new[] { EnemySignalRole.None, EnemySignalRole.Accelerator, EnemySignalRole.Restorer,
            EnemySignalRole.Bulwark, EnemySignalRole.Jammer, EnemySignalRole.Disruptor };
        foreach (var family in AreaReviewFamilies)
        {
            foreach (var role in roles.Skip(1))
            {
                var sample = SignalModuleSample(content, family, role, "CLEAR");
                var pixels = SignalModulePixels(sample, 0, true, 3, identityOnly: true);
                var extent = EnemyModelArt.CoreRadius(sample.Enemy) * .19f * 3;
                var dark = 0;
                for (var y = (int)MathF.Ceiling(192 - extent); y < 192 + extent; y++)
                for (var x = (int)MathF.Ceiling(192 - extent); x < 192 + extent; x++)
                {
                    var pixel = pixels[y * 384 + x];
                    if (pixel.R + pixel.G + pixel.B < 110) dark++;
                }
                Require(dark == 0,
                    $"{family} {role}: the colored gem has an uninterrupted luminous center without a symbol.", assertions);
            }
        }
        Require(EnemySignalGlyphRenderer.Accent(EnemySignalRole.Accelerator) == ColorPalette.Acceleration &&
                ColorPalette.Acceleration.R >= 230 && ColorPalette.Acceleration.G >= 230 && ColorPalette.Acceleration.B >= 230,
            "Acceleration uses a near-white gem and support range distinct from the blue shield role.", assertions);

        using var target = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        StatusLabel(font, "SIGNAL GEMS / NATIVE SIZE + 2.5x DETAIL", new Vector2(24, 20), .85f);
        var labels = new[] { "ORDINARY", "SPEED", "REPAIR", "SHIELD", "JAM", "DISRUPT" };
        for (var column = 0; column < roles.Length; column++)
            StatusLabel(font, labels[column], new Vector2(138 + column * 181, 80), .48f);
        for (var row = 0; row < AreaReviewFamilies.Length; row++)
            StatusLabel(font, content.Enemies[AreaReviewFamilies[row]].DisplayName.ToUpperInvariant(),
                new Vector2(14, 156 + row * 108), .42f);
        _batch.End();
        for (var row = 0; row < AreaReviewFamilies.Length; row++)
        for (var column = 0; column < roles.Length; column++)
        {
            var sample = SignalModuleSample(content, AreaReviewFamilies[row], roles[column], "CLEAR");
            foreach (var scale in new[] { 1f, 2.5f })
            {
                var at = new Vector2(168 + column * 181 + (scale == 1 ? -32 : 48), 160 + row * 108);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0));
                EnemyModelArt.Draw(_batch, _primitives, sample.Enemy, Vector2.Zero,
                    -MathHelper.PiOver2, 0, true, atRest: true);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        scenes.Add(SaveTierScene(target, "signal-gem-roster.png"));
    }
}
