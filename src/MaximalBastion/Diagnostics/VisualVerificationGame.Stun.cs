using System.Security.Cryptography;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertStunPresentation(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        using (var gallery = new RenderTarget2D(GraphicsDevice, 1280, 1200,
                   false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
        {
            GraphicsDevice.SetRenderTarget(gallery);
            GraphicsDevice.Clear(ColorPalette.Panel);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            var samples = new[]
            {
                (Id: "t2_runner", Boss: false, Heading: MathHelper.PiOver4, Name: "RUNNER"),
                (Id: "t1_crawler", Boss: false, Heading: MathHelper.PiOver2, Name: "CRAWLER"),
                (Id: "t3_brute", Boss: false, Heading: 0f, Name: "BRUTE"),
                (Id: "t4_aegis", Boss: true, Heading: MathHelper.Pi, Name: "BOSS")
            };
            string[] columns = ["STUN", "SLOW", "STACKED STATUSES", "REDUCED EFFECTS"];
            for (var column = 0; column < columns.Length; column++)
                Label(columns[column], new Vector2(25 + column * 155, 12));
            for (var row = 0; row < samples.Length; row++)
            {
                var sample = samples[row];
                var radius = content.Enemies[sample.Id].Visual.Radius + (sample.Boss ? 8 : 0);
                for (var column = 0; column < columns.Length; column++)
                {
                    var center = new Vector2(80 + column * 155, 80 + row * 140);
                    NightGridArt.Enemy(_batch, _primitives, center, radius, sample.Id,
                        sample.Heading, 0, sample.Boss, 0, false);
                    _primitives.HealthBar(_batch, center - new Vector2(0, radius + 11),
                        radius * (sample.Boss ? 3.5f : 2.5f), .7f,
                        ColorPalette.HealthHigh, ColorPalette.HealthTrack, ColorPalette.Ink);
                    if (column == 1)
                        _primitives.DashedRing(_batch, center, radius + 9, ColorPalette.Slow, 16, 2);
                    if (column == 2)
                    {
                        _primitives.Ring(_batch, center, radius - 2, ColorPalette.Burn, 2);
                        _primitives.DashedRing(_batch, center, radius + 9, ColorPalette.Slow, 16, 2);
                        StatusGlyphRenderer.DrawArmorBreak(_batch, _primitives, center, radius);
                        _primitives.Ring(_batch, center, radius + 13, ColorPalette.Violet, 2);
                    }
                    if (column != 1)
                        StatusGlyphRenderer.DrawStun(_batch, _primitives, center,
                            radius, column * .11f, column == 3, sample.Heading);
                    Label(sample.Name, center + new Vector2(-25, 72));
                }
            }
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            SaveTarget(gallery, "35-stun-status.png");
        }

        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        string[] enemies = ["t2_runner", "t3_brute", "t4_aegis"];
        for (var index = 0; index < enemies.Length; index++)
        {
            session.SpawnEnemy(enemies[index], 1, 1, index == 2 ? "Boss" : "Standard");
            var enemy = session.Enemies[^1];
            enemy.SetSandboxPathDistance(353 + index * 75, session.Map.Path);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = 6, Magnitude = 1 });
            if (index == 0) continue;
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Duration = 6, Magnitude = 1 });
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Duration = 6, Magnitude = .4f });
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.ArmorBreak, Duration = 6, Magnitude = 3 });
        }
        var fieldUi = new UIManager(font);
        ConfigureUi(fieldUi, content);
        var checksum = SessionChecksum.Compute(session, 0);
        scenes.Add(Capture("35a-stun-battlefield.png", fieldUi, GameState.Playing, session));
        var originalReducedEffects = _renderer.ReducedEffects;
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("35b-stun-battlefield-reduced.png", fieldUi, GameState.Playing, session));
        _renderer.ReducedEffects = originalReducedEffects;
        Require(SessionChecksum.Compute(session, 0) == checksum,
            "Standard and reduced stun rendering leave synchronized session state unchanged.", assertions);

        using (var crop = BattlefieldCrop(session))
            SaveTarget(crop, "35c-stun-detail.png");
        Directory.CreateDirectory(Path.Combine(_outputDirectory, "stun-motion"));
        for (var frame = 0; frame < 18; frame++)
        {
            using var crop = BattlefieldCrop(session);
            SaveTarget(crop, $"stun-motion/frame-{frame:00}.png");
            session.Statistics.Advance(1f / 15f);
        }
        var first = StunPixels(0, false);
        var second = StunPixels(.11f, false);
        var quiet = StunPixels(0, true);
        Require(first.Zip(second, (a, b) => a != b).Count(changed => changed) > 100,
            "Stun internal currents flicker between short presentation intervals.", assertions);
        Require(quiet.SequenceEqual(StunPixels(.11f, true)) && quiet.Any(pixel => pixel != ColorPalette.Panel),
            "Reduced Effects keeps a visible, stationary stun marker.", assertions);

        var expirySession = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        expirySession.SpawnEnemy("t2_runner", 1, 1);
        var expiring = expirySession.Enemies.Single();
        expiring.SetSandboxPathDistance(380, expirySession.Map.Path);
        using var unstunned = BattlefieldCrop(expirySession);
        expiring.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Duration = .2f, Magnitude = 1 });
        using var stunned = BattlefieldCrop(expirySession);
        expiring.StatusEffects.Update(.2f);
        using var expired = BattlefieldCrop(expirySession);
        var baseline = Pixels(unstunned);
        Require(!baseline.SequenceEqual(Pixels(stunned)) && baseline.SequenceEqual(Pixels(expired)),
            "The internal stun current appears only while the enemy's stun status is active.", assertions);

        void Label(string text, Vector2 at) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, .38f * GameConstants.FontDrawScale, SpriteEffects.None, 0);

        void SaveTarget(RenderTarget2D target, string fileName)
        {
            var path = Path.Combine(_outputDirectory, fileName);
            using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
            scenes.Add(new VisualVerificationScene(fileName, target.Width, target.Height,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                Pixels(target).Count(pixel => pixel != ColorPalette.Panel)));
        }

        RenderTarget2D BattlefieldCrop(GameSession battlefield)
        {
            var target = new RenderTarget2D(GraphicsDevice, 660, 380,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateTranslation(-80, -135, 0) * Matrix.CreateScale(2));
            _renderer.Draw(_batch, _primitives, battlefield);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            return target;
        }

        Color[] StunPixels(float time, bool reduced)
        {
            using var target = new RenderTarget2D(GraphicsDevice, 200, 200);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Panel);
            _batch.Begin(transformMatrix: Matrix.CreateScale(2));
            StatusGlyphRenderer.DrawStun(_batch, _primitives, new Vector2(50), 14, time, reduced);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            return Pixels(target);
        }

        static Color[] Pixels(RenderTarget2D target)
        {
            var result = new Color[target.Width * target.Height];
            target.GetData(result);
            return result;
        }
    }
}
