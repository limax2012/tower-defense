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
    private void AssertUnifiedHud(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var campaign = new GameSession(content, "crosswind_basin", "bastion", "close_quarters");
        var sandbox = new GameSession(content, "crosswind_basin", "bastion", "sandbox_lab");
        campaign.Statistics.Advance(3672);
        foreach (var session in new[] { campaign, sandbox })
        {
            session.SpawnEnemy("t3_brute", 1, 1);
            session.Enemies[^1].SetSandboxPathDistance(220, session.Map.Path);
        }
        ui.HandleGameplayInput(Pointer(100, 600), campaign);
        var checksum = SessionChecksum.Compute(campaign, 0);
        var normal = RenderPixels(ui, GameState.Playing, campaign);
        ui.HandleGameplayInput(Pointer(346, 22), campaign);
        var hovering = RenderPixels(ui, GameState.Playing, campaign);
        Require(CountChangedPixels(normal, hovering, new Rectangle(0, 0, 1280, 56)) == 0,
            "Enemy count and run context remain identical when the wave readout is hovered.", assertions);
        Require(CountColorPixels(normal, new Rectangle(335, 8, 101, 18), ColorPalette.Muted) > 40 &&
                CountColorPixels(normal, new Rectangle(335, 26, 101, 24), ColorPalette.Paper) > 10,
            "The ENEMIES label and count are visible without hovering.", assertions);
        Require(checksum == SessionChecksum.Compute(campaign, 0),
            "HUD inspection does not mutate synchronized gameplay state.", assertions);
        var contextColors = new[] { ColorPalette.Environment(campaign.Map.Definition.PathVisual.Style).Accent,
            campaign.Difficulty.AccentColor, campaign.Challenge.AccentColor };
        for (var column = 0; column < 3; column++)
        {
            var bounds = UIManager.HudContextBounds(column);
            var ink = ColorBounds(normal, bounds, ColorPalette.Muted,
                ColorPalette.Text(ColorPalette.BalancedAccentText(contextColors[column], ColorPalette.Navy)));
            Require(ink.Height > 15 && MathF.Abs((ink.Top + ink.Bottom) * .5f - 27) <= 1.5f,
                $"Run-context field {column + 1} centers its visible label/value ink vertically in the toolbar.", assertions);
            if (column > 0)
                Require(CountColorPixels(normal, new Rectangle(bounds.Left - 8, 12, 3, 31), ColorPalette.Divider) == 0,
                    $"Run-context field {column + 1} uses open spacing without a vertical separator.", assertions);
        }
        Require(Math.Abs((UIManager.HudRunSetupBounds.Left - 806) -
                (GameConstants.LogicalWidth - UIManager.HudRunSetupBounds.Right)) <= 2,
            "Run context has balanced empty space between Pause and the screen edge.", assertions);
        CaptureNative("hud-campaign.png", campaign);

        ui.HandleGameplayInput(Pointer(100, 600), sandbox);
        var lab = RenderPixels(ui, GameState.Playing, sandbox);
        foreach (var column in Enumerable.Range(0, 4))
        {
            var bounds = UIManager.HudMetricBounds(column);
            Require(CountChangedPixels(normal, lab, new Rectangle(bounds.X, 8, bounds.Width, 16)) == 0,
                $"Campaign and Sandbox metric {column + 1} use identical label size and placement.", assertions);
        }
        Require(CountColorPixels(lab, new Rectangle(18, 31, 29, 16), ColorPalette.Paper) >= 40 &&
                CountColorPixels(lab, new Rectangle(115, 31, 29, 16), ColorPalette.Paper) >= 40,
            "Sandbox lives and credits show compact infinity symbols at the common value baseline.", assertions);

        var controls = new[] { UIManager.SandboxWavePreviousBounds, UIManager.SandboxWaveNextBounds,
            UIManager.SandboxWaveSignalsBounds };
        Require(controls.All(bounds => bounds.Top >= GameConstants.TopBarHeight && bounds.Bottom <= 90) &&
                controls.Zip(controls.Skip(1)).All(pair => pair.First.Right < pair.Second.Left),
            "Sandbox wave controls occupy separate sidebar header targets below the shared toolbar.", assertions);
        var previousWave = ui.SelectedSandboxWave;
        Click(UIManager.SandboxWaveNextBounds, sandbox);
        Require(ui.SelectedSandboxWave == previousWave + 1,
            "The visible Sandbox W+ control selects the next authored wave.", assertions);
        Click(UIManager.SandboxWavePreviousBounds, sandbox);
        Require(ui.SelectedSandboxWave == previousWave,
            "The visible Sandbox W- control selects the preceding authored wave.", assertions);
        var signals = sandbox.SandboxWaveSignalsEnabled;
        Click(UIManager.SandboxWaveSignalsBounds, sandbox);
        Require(sandbox.SandboxWaveSignalsEnabled != signals,
            "The visible Sandbox signal control toggles authored-wave signals.", assertions);
        ui.HandleGameplayInput(Pointer(100, 600), sandbox);
        CaptureNative("hud-sandbox.png", sandbox);

        var displayFactor = GameConstants.FontDrawScale * UiTypography.DisplayRasterScale;
        AssertContextFits(content.Maps.Values.Select(map => map.DisplayName), 0, "Map");
        AssertContextFits(content.Difficulties.Values.Select(difficulty => difficulty.DisplayName), 1, "Difficulty");
        AssertContextFits(content.Challenges.Values.Select(challenge => challenge.DisplayName), 2, "Mode");
        var mapNameColors = new HashSet<Color>();
        foreach (var map in content.Maps.Values)
        {
            var mapSession = new GameSession(content, map.Id, "normal", "standard");
            ui.HandleGameplayInput(Pointer(0, 0), mapSession);
            var pixels = RenderPixels(ui, GameState.Playing, mapSession);
            var accent = ColorPalette.Environment(map.PathVisual.Style).Accent;
            var textColor = ColorPalette.Text(ColorPalette.BalancedAccentText(accent, ColorPalette.Navy));
            mapNameColors.Add(textColor);
            Require(CountColorPixels(pixels, UIManager.HudContextBounds(0), textColor) > 40 &&
                    ColorPalette.ContrastRatio(textColor, ColorPalette.Navy) >= 3,
                $"{map.DisplayName}: the permanent map name uses its district accent with readable contrast.", assertions);
            CaptureNative($"hud-map-{map.Id}.png", mapSession);
        }
        Require(mapNameColors.Count == content.Maps.Count,
            "Every authored map has a distinct district-colored toolbar name.", assertions);
        Require(Enumerable.Range(0, 3).All(column =>
                UIManager.HudRunSetupBounds.Contains(UIManager.HudContextBounds(column))) &&
                UIManager.HudRunSetupBounds.Left > 806,
            "All run parameters fit to the right of Pause without entering its input bounds.", assertions);

        campaign.Enemies.Clear();
        var restored = GameSession.RestoreSaveGame(content, campaign.CaptureSaveGame());
        Require(UIManager.PauseGameTimeLabel(restored) == "GAME TIME  1:01:12",
            "Pause game time uses the persisted simulation clock across saved-run restoration.", assertions);
        CaptureNative("hud-pause-game-time.png", restored, GameState.Paused);
        foreach (var challenge in new[] { "standard", "core_six", "no_reserves" })
            CaptureNative($"hud-{challenge}.png", new GameSession(content, "prism_circuit", "hard", challenge));
        foreach (var (credits, change) in new[] { (90, 5), (390, 25), (999, -99), (9990, 9), (9000, -8999) })
        {
            var creditSession = new GameSession(content, "foundry_loop", "normal", "standard");
            creditSession.Economy.TrySpend(creditSession.Economy.Credits);
            creditSession.Economy.AddCredits(credits);
            var baseline = RenderPixels(ui, GameState.Playing, creditSession);
            if (change > 0) creditSession.Economy.AddCredits(change);
            else creditSession.Economy.TrySpend(-change);
            var changed = RenderPixels(ui, GameState.Playing, creditSession);
            var valueWidth = display.MeasureString(creditSession.Economy.Credits.ToString()).X * displayFactor;
            var valueEnd = (int)MathF.Ceiling(115 + valueWidth);
            var deltaInk = ColorBounds(changed, new Rectangle(valueEnd, 26, 213 - valueEnd, 24),
                change > 0 ? ColorPalette.Green : ColorPalette.Gold);
            Require(deltaInk.Width > 3 && deltaInk.Left - valueEnd is >= 3 and <= 7 && deltaInk.Right <= 213,
                $"Credit change {change:+0;-0} stays beside {creditSession.Economy.Credits} with a small gap inside the credit field.", assertions);
            Require(CountChangedPixels(baseline, changed, UIManager.HudMetricBounds(2)) == 0,
                $"Credit change {change:+0;-0} does not alter or overlap the Wave field.", assertions);
            if ((credits, change) is (390, 25) or (999, -99))
                CaptureNative(change > 0 ? "hud-credit-gain.png" : "hud-credit-spend.png", creditSession);
        }
        var coOp = new GameSession(content, "relay_divide", "normal", "standard");
        coOp.ConfigureCoOp(1);
        ui.SetCoOpConnectionState(true);
        CaptureNative("hud-co-op.png", coOp);

        void Click(Rectangle bounds, GameSession session) =>
            ui.HandleGameplayInput(Pointer(bounds.Center.X, bounds.Center.Y, leftPressed: true), session);

        void AssertContextFits(IEnumerable<string> values, int column, string label)
        {
            var width = UIManager.HudContextBounds(column).Width;
            foreach (var value in values)
                Require(display.MeasureString(value.ToUpperInvariant()).X * displayFactor * .52f <= width,
                    $"{label} {value} fits its permanent HUD field at a readable display scale.", assertions);
        }

        void CaptureNative(string file, GameSession session, GameState state = GameState.Playing)
        {
            using var rendered = RenderScene(ui, state, session);
            using var native = DownsampleEnemyLaneScene(rendered);
            scenes.Add(SaveTierScene(native, file));
        }

        static Rectangle ColorBounds(Color[] pixels, Rectangle region, params Color[] colors)
        {
            var minimum = new Point(int.MaxValue, int.MaxValue);
            var maximum = new Point(int.MinValue, int.MinValue);
            for (var y = region.Top * GameConstants.RenderScale; y < region.Bottom * GameConstants.RenderScale; y++)
            for (var x = region.Left * GameConstants.RenderScale; x < region.Right * GameConstants.RenderScale; x++)
            {
                if (!colors.Contains(pixels[y * GameConstants.RenderWidth + x])) continue;
                minimum.X = Math.Min(minimum.X, x);
                minimum.Y = Math.Min(minimum.Y, y);
                maximum.X = Math.Max(maximum.X, x);
                maximum.Y = Math.Max(maximum.Y, y);
            }
            if (minimum.X > maximum.X) return Rectangle.Empty;
            return new Rectangle(minimum.X / GameConstants.RenderScale, minimum.Y / GameConstants.RenderScale,
                (maximum.X - minimum.X + 1 + GameConstants.RenderScale - 1) / GameConstants.RenderScale,
                (maximum.Y - minimum.Y + 1 + GameConstants.RenderScale - 1) / GameConstants.RenderScale);
        }
    }
}
