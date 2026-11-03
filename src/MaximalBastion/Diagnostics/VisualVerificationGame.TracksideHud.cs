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
    private void AssertTracksideStatusPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var original = _renderer;
        try
        {
            foreach (var map in content.Maps.Values)
            foreach (var reduced in new[] { false, true })
            {
                var session = new GameSession(content, map.Id, "normal", "sandbox_lab");
                for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
                var families = new[] { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" };
                for (var index = 0; index < 12; index++)
                {
                    session.SpawnEnemy(families[index % families.Length], 1, 1);
                    var enemy = session.Enemies[^1];
                    enemy.SetSandboxPathDistance(150 + index * 20, session.Map.Path);
                    enemy.ApplyHealthDamage(enemy.MaxHealth * .2f);
                    enemy.TickRuntimeTimers(1.1f);
                    enemy.GrantShield(100, 100);
                    enemy.SetFormationSpeedMultiplier(1.2f);
                    foreach (var type in Enum.GetValues<StatusType>())
                        enemy.ApplyStatus(new StatusApplication { Type = type, Duration = 5, Magnitude = type == StatusType.Burn ? 2 : .3f });
                }
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                var checksum = SessionChecksum.Compute(session, 270);
                _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
                using var bodyScene = RenderScene(ui, GameState.Playing, session);
                using var bodyNative = DownsampleEnemyLaneScene(bodyScene);
                var bodyPixels = new Color[1280 * 720];
                bodyNative.GetData(bodyPixels);
                _renderer.HideEnemyHud = false;
                using var scene = RenderScene(ui, GameState.Playing, session);
                using var native = DownsampleEnemyLaneScene(scene);
                var pixels = new Color[1280 * 720];
                native.GetData(pixels);
                Require(_renderer.LastEnemyGlyphCount == 0 && _renderer.LastEnemyStrips.Count == 0,
                    $"{map.Id}: active gameplay has no enemy status glyphs or leader layout.", assertions);
                Require(_renderer.LastEnemyAuraRegionCount == 48,
                    $"{map.Id} {(reduced ? "Reduced" : "Full")}: twelve enemies retain all four sustained debuff color regions.", assertions);
                Require(_renderer.LastEnemyHealthBarCount == 12 && _renderer.LastEnemyShieldBarCount == 12,
                    $"{map.Id}: every in-view enemy retains quantitative health and shield meters.", assertions);
                Require(checksum == SessionChecksum.Compute(session, 270),
                    $"{map.Id}: complete status presentation leaves simulation state unchanged.", assertions);
                scenes.Add(SaveTierScene(native, $"66-debuff-aura-stacks-{map.Id}-{(reduced ? "reduced" : "full")}.png"));
            }
            AssertEnemyEntryMeters(content, font, display, assertions, scenes);
            AssertEnemyMeterPresentation(content, font, display, assertions, scenes);
        }
        finally { _renderer = original; }
    }

    private void AssertEnemyEntryMeters(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var family in new[] { "t1_crawler", "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" })
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
            for (var tick = 0; tick < 270; tick++) session.Update(1f / 60);
            session.SpawnEnemy(family, 1, 1, rank);
            var enemy = session.Enemies.Single();
            enemy.AbsorbShield(enemy.Shield);
            enemy.GrantShield(1, 1);
            enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = 1, Duration = 5 });
            var spawnBounds = EnemyHudPresentation.VisibleBounds(enemy, enemy.Position, 0);
            enemy.SetSandboxPathDistance(.5f - spawnBounds.Z, session.Map.Path);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            ui.HandleGameplayInput(Pointer(0, 0), session);
            _renderer = new GameRenderer();
            using var scene = RenderScene(ui, GameState.Playing, session);
            using var native = DownsampleEnemyLaneScene(scene);
            var pixels = new Color[1280 * 720];
            native.GetData(pixels);
            scenes.Add(SaveTierScene(native, $"67-entry-vitals-{family}-{rank.ToLowerInvariant()}.png"));
            Require(_renderer.LastEnemyHealthBarCount == 1 && _renderer.LastEnemyShieldBarCount == 1 &&
                _renderer.LastEnemyGlyphCount == 0 && _renderer.LastEnemyAuraRegionCount == 1,
                $"{family} {rank}: first visible shield edge has complete health, shield and condition information.", assertions);
            var radius = EnemyModelArt.BodyRadius(enemy);
            var frames = EnemyHudPresentation.MeterFrames(enemy,
                PresentationFrame.Create(session, 0).EnemyPosition(enemy), radius);
            Require(frames.Shield.Bottom < frames.Health.Top,
                $"{family} {rank}: health and shield frames retain a clear gap when clamped at entry.", assertions);
            foreach (var shield in new[] { false, true })
            {
                var frame = shield ? frames.Shield : frames.Health;
                var name = shield ? "shield" : "health";
                Require(frame.Left >= 0 && frame.Right <= GameConstants.SidebarX &&
                    frame.Top >= GameConstants.TopBarHeight && frame.Bottom <= GameConstants.LogicalHeight,
                    $"{family} {rank}: the complete {name} meter frame stays inside the gameplay viewport at entry.", assertions);
                foreach (var edge in new[] { -1, 1 })
                {
                    var x = edge < 0 ? frame.Left : frame.Right - 1;
                    var wallPixels = Enumerable.Range(frame.Top, frame.Height)
                        .Count(y => pixels[y * 1280 + x] == ColorPalette.Ink);
                    Require(wallPixels == frame.Height,
                        $"{family} {rank}: {name} meter endpoint {edge} retains its entire {frame.Height}-pixel wall at native entry size.", assertions);
                }
                var horizontalWalls = Enumerable.Range(frame.Left, frame.Width).All(x =>
                    pixels[frame.Top * 1280 + x] == ColorPalette.Ink &&
                    pixels[(frame.Bottom - 1) * 1280 + x] == ColorPalette.Ink);
                Require(horizontalWalls,
                    $"{family} {rank}: {name} meter top and bottom walls close both entry endpoints.", assertions);
                var fillColor = shield ? ColorPalette.Shield : ColorPalette.Health(1);
                Require(Enumerable.Range(frame.Top + 1, frame.Height - 2).All(y =>
                    pixels[y * 1280 + frame.Left + 1] == fillColor),
                    $"{family} {rank}: the positive {name} fill remains visible inside its closed frame at entry.", assertions);
            }
        }
    }
}
