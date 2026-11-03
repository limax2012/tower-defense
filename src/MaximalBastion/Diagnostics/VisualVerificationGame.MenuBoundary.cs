using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Persistence;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void VerifyMenuFeedBoundaries(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        AssertCombatFeedClipping(content, assertions, scenes);
        var smoothMenu = new UIManager(font, display);
        ConfigureUi(smoothMenu, content);
        _ = RenderPixels(smoothMenu, GameState.MainMenu, null);
        smoothMenu.AdvanceMainMenuBattle(1f / 120f);
        _ = RenderPixels(smoothMenu, GameState.MainMenu, null);
        ui.AdvanceVisualTime(.45f);
        ui.AdvanceMainMenuBattle(.45f);
        _ = RenderPixels(ui, GameState.MainMenu, null);
        ui.ConfigureSettings(new UserSettings { ReducedEffects = true });
        _ = RenderPixels(ui, GameState.MainMenu, null);
        ui.AdvanceVisualTime(.25f);
        _ = RenderPixels(ui, GameState.MainMenu, null);
        ui.ConfigureSettings(new UserSettings());
        ui.HandleMainMenu(Pointer(0, 0));
        var baseline = CaptureFrame("menu-boundary-baseline.png");
        var frames = new List<object>();
        var failures = 0;
        for (var index = 0; index < 96; index++)
        {
            ui.AdvanceMainMenuBattle(.25f);
            if (index % 12 != 11) continue;
            var current = CaptureFrame($"menu-boundary-{index + 1:00}.png");
            var left = CountChangedPixels(baseline, current, new Rectangle(20, 668, 280, 52));
            var right = CountChangedPixels(baseline, current, new Rectangle(980, 668, 280, 52));
            if (left != 0 || right != 0) failures++;
            var changed = new List<object>();
            var min = new Point(int.MaxValue, int.MaxValue);
            var max = new Point(int.MinValue, int.MinValue);
            for (var y = 668 * GameConstants.RenderScale; y < GameConstants.RenderHeight; y++)
            for (var x = 20 * GameConstants.RenderScale; x < 1260 * GameConstants.RenderScale; x++)
            {
                if (x >= 300 * GameConstants.RenderScale && x < 980 * GameConstants.RenderScale) continue;
                var at = y * GameConstants.RenderWidth + x;
                if (baseline[at] == current[at]) continue;
                min = new Point(Math.Min(min.X, x), Math.Min(min.Y, y));
                max = new Point(Math.Max(max.X, x), Math.Max(max.Y, y));
                if (changed.Count < 24)
                    changed.Add(new { x = x / (float)GameConstants.RenderScale, y = y / (float)GameConstants.RenderScale,
                        before = baseline[at].ToString(), after = current[at].ToString() });
            }
            frames.Add(new { step = index + 1, seconds = (index + 1) * .25f, left, right,
                pixelBounds = left + right == 0 ? null : new[] { min.X, min.Y, max.X, max.Y }, changed });
        }
        File.WriteAllText(Path.Combine(_outputDirectory, "menu-boundary-pixels.json"),
            JsonSerializer.Serialize(new { failures, frames }, new JsonSerializerOptions { WriteIndented = true }));
        Require(failures == 0, "Main-menu combat stays inside both feeds throughout enemy arrivals and escapes.", assertions);

        Color[] CaptureFrame(string name)
        {
            using var scene = RenderScene(ui, GameState.MainMenu, null);
            var pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
            scene.GetData(pixels);
            using var native = DownsampleEnemyLaneScene(scene);
            scenes.Add(SaveTierScene(native, name));
            return pixels;
        }
    }
}
