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
    private void AssertEnemyRestPortraits(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var samples = AreaReviewFamilies.SelectMany(id => EnemyRoster.RanksFor(id)
            .Select(rank => EnemyRankSample(content, id, rank.ToString()))).ToArray();
        var families = samples.GroupBy(sample => sample.Enemy.Definition.Id).Select(group => group.ToArray()).ToArray();
        foreach (var sample in samples)
        {
            var enemy = sample.Enemy;
            var resting = PortraitPixels(enemy);
            AssertPortraitBounds(enemy, resting);
            foreach (var distance in new[] { 87f, 167f, 218f, 435f })
            {
                enemy.SetSandboxPathDistance(distance, sample.Session.Map.Path);
                var checksum = SessionChecksum.Compute(sample.Session, 0);
                Require(resting.SequenceEqual(PortraitPixels(enemy)) &&
                        checksum == SessionChecksum.Compute(sample.Session, 0),
                    $"{enemy.Definition.Id} {enemy.Rank}: the resting Intel portrait is independent of travel at {distance}px and leaves simulation unchanged.", assertions);
            }
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Stun, Magnitude = 1, Duration = .5f });
            Require(resting.SequenceEqual(PortraitPixels(enemy)),
                $"{enemy.Definition.Id} {enemy.Rank}: the Intel portrait holds its neutral contacts during battlefield Stun.", assertions);
        }
        using var target = NewEnemyArmorTarget();
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        StatusLabel(font, "ENEMY INTEL / RESTING PORTRAITS", new Vector2(26, 24), .8f);
        StatusLabel(font, "INTEL PORTRAIT SIZE / SYMMETRIC CONTACTS", new Vector2(26, 61), .5f);
        for (var family = 0; family < 5; family++)
        {
            var x = 20 + family * 250;
            StatusLabel(font, families[family][0].Enemy.Definition.DisplayName.ToUpperInvariant(),
                new Vector2(x + 14, 104), .54f);
            for (var rank = 0; rank < families[family].Length; rank++)
            {
                var y = 144 + rank * 180;
                var enemy = families[family][rank].Enemy;
                _primitives.FillRect(_batch, new Rectangle(x, y, 240, 166), ColorPalette.Panel);
                StatusLabel(font, enemy.Rank.ToString().ToUpperInvariant(), new Vector2(x + 14, y + 14), .46f);
                UIManager.DrawEnemyIntelModel(_batch, _primitives, enemy, new Vector2(x + 120, y + 100), Matrix.CreateScale(2));
            }
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        using var native = DownsampleEnemyLaneScene(target);
        scenes.Add(SaveTierScene(native, "enemy-intel-resting-portraits.png"));
        CaptureHeaders();

        Color[] PortraitPixels(EnemyInstance enemy)
        {
            using var portrait = new RenderTarget2D(GraphicsDevice, 256, 256, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(portrait);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            UIManager.DrawEnemyIntelModel(_batch, _primitives, enemy, new Vector2(64), Matrix.CreateScale(2));
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[256 * 256];
            portrait.GetData(pixels);
            return pixels;
        }

        void AssertPortraitBounds(EnemyInstance enemy, Color[] pixels)
        {
            var left = 256;
            var top = 256;
            var right = 0;
            var bottom = 0;
            for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
            {
                if (pixels[y * 256 + x] == ColorPalette.Canvas) continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
            Require(left >= 78 && top >= 78 && right <= 178 && bottom <= 178 && right > left && bottom > top,
                $"{enemy.Definition.Id} {enemy.Rank}{(enemy.BossPhaseActive ? " overdrive" : "")}: the complete Intel portrait fits its 50px pocket with clearance from the rule, title, and health row.", assertions);
        }

        void CaptureHeaders()
        {
            const int width = 1568;
            const int height = 480;
            using var sheet = new RenderTarget2D(GraphicsDevice, width * 2, height * 2, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(sheet);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            StatusLabel(font, "ENEMY INTEL / HEADER CLEARANCE", new Vector2(20, 20), .8f);
            StatusLabel(font, "NATIVE UI SIZE / STANDARD, ELITE, BOSS OVERDRIVE", new Vector2(20, 55), .5f);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            for (var family = 0; family < 5; family++)
            foreach (var rank in EnemyRoster.RanksFor(AreaReviewFamilies[family]))
            {
                var id = AreaReviewFamilies[family];
                var role = id == "t5_regenerator" && rank == EnemyRank.Elite ? EnemySignalRole.Disruptor : EnemySignalRole.None;
                var sample = EnemyRankSample(content, id, rank.ToString(), role);
                var enemy = sample.Enemy;
                if (enemy.IsBoss)
                {
                    enemy.ApplyHealthDamage(enemy.MaxHealth * .55f);
                    AssertPortraitBounds(enemy, PortraitPixels(enemy));
                }
                else if (role == EnemySignalRole.Disruptor)
                {
                    enemy.ApplyHealthDamage(enemy.MaxHealth * .05f);
                    enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .6f, Duration = 2.8f });
                    enemy.ApplyStatus(new StatusApplication { Type = StatusType.Exposed, Magnitude = .15f, Duration = 1.4f });
                }
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                var position = PresentationFrame.Create(sample.Session, 0).EnemyPosition(enemy);
                ui.HandleGameplayInput(Pointer(position.X, position.Y, true), sample.Session);
                ui.HandleGameplayInput(Pointer(1100, 600), sample.Session);
                using var scene = RenderScene(ui, GameState.Playing, sample.Session);
                if (id == "t5_regenerator" && rank is EnemyRank.Elite or EnemyRank.Boss)
                {
                    using var full = DownsampleEnemyLaneScene(scene);
                    scenes.Add(SaveTierScene(full, rank == EnemyRank.Elite ? "enemy-intel-regenerator-disruptor.png" :
                        "enemy-intel-regenerator-overdrive.png"));
                }
                GraphicsDevice.SetRenderTarget(sheet);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(2));
                _batch.Draw(scene, new Rectangle(20 + family * 308, 88 + (int)rank * 126, 296, 114),
                    new Rectangle(972 * 2, 474 * 2, 296 * 2, 114 * 2), Color.White);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
            }
            using var output = new RenderTarget2D(GraphicsDevice, width, height, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(output);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            _batch.Draw(sheet, new Rectangle(0, 0, width, height), Color.White);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            scenes.Add(SaveTierScene(output, "enemy-intel-header-clearance.png"));
        }
    }
}
