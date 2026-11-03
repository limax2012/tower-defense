using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyMeterPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var original = _renderer;
        try
        {
            _renderer = new GameRenderer { ReducedEffects = true };
            foreach (var family in AreaReviewFamilies)
            foreach (var rank in new[] { "Standard", "Elite", "Boss" })
            {
                var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
                session.SpawnEnemy(family, 1, 1, rank);
                var enemy = session.Enemies.Single();
                enemy.SetSandboxPathDistance(430, session.Map.Path);
                enemy.AbsorbShield(enemy.Shield);
                enemy.GrantShield(20, 100);
                enemy.ApplyHealthDamage(enemy.MaxHealth * .5f);
                var checksum = SessionChecksum.Compute(session, 0);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                var pointer = Pointer(0, 0);
                ui.HandleGameplayInput(pointer, session);
                session.HandleWorldInput(pointer);
                var plain = Pixels(ui, session);
                var position = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
                var radius = EnemyModelArt.BodyRadius(enemy);
                var (health, shield) = EnemyHudPresentation.MeterFrames(enemy, position, radius);
                AssertClosedFrame(plain, health, "health");
                AssertClosedFrame(plain, shield, "shield");
                var fillX = health.Left + 2;
                var healthRows = Enumerable.Range(health.Top, health.Height)
                    .Count(y => plain[y * 1280 + fillX] == ColorPalette.Health(.5f));
                Require(healthRows == 2,
                    $"{family} {rank}: health fill occupies exactly two native logical-pixel rows.", assertions);
                var shieldRows = Enumerable.Range(shield.Top, shield.Height)
                    .Count(y => plain[y * 1280 + shield.Left + 1] == ColorPalette.Shield);
                Require(shieldRows == 1,
                    $"{family} {rank}: shield fill occupies one native logical-pixel row within its closed frame.", assertions);
                pointer = Pointer(position.X, position.Y);
                ui.HandleGameplayInput(pointer, session);
                session.HandleWorldInput(pointer);
                var hovered = Pixels(ui, session);
                Require(SameRegion(plain, hovered, health) && SameRegion(plain, hovered, shield),
                    $"{family} {rank}: hovering leaves health and shield meter pixels, size, and position unchanged.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 0),
                    $"{family} {rank}: thin meters and hover inspection preserve authoritative combat state.", assertions);

                void AssertClosedFrame(Color[] pixels, Rectangle frame, string name)
                {
                    var closed = true;
                    for (var y = frame.Top; y < frame.Bottom; y++)
                        closed &= pixels[y * 1280 + frame.Left] == ColorPalette.Ink &&
                            pixels[y * 1280 + frame.Right - 1] == ColorPalette.Ink;
                    for (var x = frame.Left; x < frame.Right; x++)
                        closed &= pixels[frame.Top * 1280 + x] == ColorPalette.Ink &&
                            pixels[(frame.Bottom - 1) * 1280 + x] == ColorPalette.Ink;
                    Require(closed, $"{family} {rank}: {name} meter has continuous top, bottom, left, and right walls at native size.", assertions);
                }
            }

            var showcase = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            for (var index = 0; index < AreaReviewFamilies.Length; index++)
            {
                showcase.SpawnEnemy(AreaReviewFamilies[index], 1, 1);
                var enemy = showcase.Enemies[^1];
                enemy.SetSandboxPathDistance(380 + index * 120, showcase.Map.Path);
                enemy.ApplyHealthDamage(enemy.MaxHealth * (index % 3) * .35f);
                enemy.GrantShield(35, 100);
            }
            var showcaseUi = new UIManager(font, display);
            ConfigureUi(showcaseUi, content);
            showcaseUi.HandleGameplayInput(Pointer(0, 0), showcase);
            using var scene = RenderScene(showcaseUi, GameState.Playing, showcase);
            using var native = DownsampleEnemyLaneScene(scene);
            scenes.Add(SaveTierScene(native, "enemy-thin-meters.png"));

            var downward = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            foreach (var rank in new[] { "Standard", "Elite", "Boss" })
            {
                downward.SpawnEnemy("t1_crawler", 1, 1, rank);
                var crawler = downward.Enemies[^1];
                crawler.SetSandboxPathDistance(180 + (downward.Enemies.Count - 1) * 40, downward.Map.Path);
                crawler.AbsorbShield(crawler.Shield);
            }
            var downwardUi = new UIManager(font, display);
            ConfigureUi(downwardUi, content);
            downwardUi.HandleGameplayInput(Pointer(0, 0), downward);
            using var downwardScene = RenderScene(downwardUi, GameState.Playing, downward);
            using var downwardNative = DownsampleEnemyLaneScene(downwardScene);
            scenes.Add(SaveTierScene(downwardNative, "enemy-downward-crawler-meters.png"));
        }
        finally { _renderer = original; }

        Color[] Pixels(UIManager ui, GameSession session)
        {
            using var scene = RenderScene(ui, GameState.Playing, session);
            using var native = DownsampleEnemyLaneScene(scene);
            var pixels = new Color[1280 * 720];
            native.GetData(pixels);
            return pixels;
        }

        static bool SameRegion(Color[] first, Color[] second, Rectangle bounds)
        {
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            for (var x = bounds.Left; x < bounds.Right; x++)
                if (first[y * 1280 + x] != second[y * 1280 + x]) return false;
            return true;
        }
    }
}
