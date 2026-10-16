using System.Security.Cryptography;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private VisualVerificationScene CaptureTowerRotationGallery(GameContent content, SpriteFont font)
    {
        var towers = content.Towers.Values.ToArray();
        var rows = towers.Length * 2;
        using var target = new RenderTarget2D(GraphicsDevice, 1800, 108 + rows * 128,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label("TIER LIGHT OCCLUSION / FULL ROTATION", new Vector2(24, 18), 1);
        Label("Tier 3 and Apex / 30-degree steps / rotating armor above fixed lights", new Vector2(24, 54), .7f);
        for (var row = 0; row < rows; row++)
        {
            var y = 100 + row * 128;
            _primitives.FillRect(_batch, new Rectangle(16, y, 1768, 120),
                row % 2 == 0 ? ColorPalette.Cinderworks.Deck : ColorPalette.Rainline.Deck);
            Label(towers[row / 2].DisplayName, new Vector2(28, y + 31), .63f);
            Label(row % 2 == 0 ? "T3" : "APEX", new Vector2(28, y + 65), .52f);
        }
        _batch.End();
        for (var row = 0; row < rows; row++)
        for (var column = 0; column < 12; column++)
        {
            var at = new Vector2(244 + column * 132, 160 + row * 128);
            var transform = Matrix.CreateScale(2) * Matrix.CreateTranslation(at.X, at.Y, 0);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, transform);
            NightGridArt.Tower(_batch, _primitives, Vector2.Zero, towers[row / 2].Visual.Radius,
                towers[row / 2].Id, level: 3, angle: -MathHelper.PiOver2 + column * MathHelper.Pi / 6,
                time: .23f, apex: row % 2 == 1);
            _batch.End();
        }
        GraphicsDevice.SetRenderTarget(null);
        const string fileName = "33a-tower-tier-rotation.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return new VisualVerificationScene(fileName, target.Width, target.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            pixels.Count(pixel => pixel != ColorPalette.Canvas));

        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at,
            ColorPalette.Paper, 0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }
}
