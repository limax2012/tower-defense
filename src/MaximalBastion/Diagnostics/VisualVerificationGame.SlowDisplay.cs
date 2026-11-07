using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEffectiveSlowDisplay(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        foreach (var definition in content.Towers.Values)
        foreach (var (name, stats) in AuthoredTowerStatSheets(definition))
        foreach (var stat in stats.Where(stat => stat.Label == "SLOW"))
        {
            var available = 282f / UIManager.TowerStatGridColumns(stats.Count) - 6f;
            var width = font.MeasureString(TowerInfo.ComparisonStatLabelText(stat)).X *
                UIManager.TowerStatGridMinimumScale * GameConstants.FontDrawScale;
            Require(width <= available,
                $"{definition.Id}/{name}: the effective-slow cap label fits without ellipsis.", assertions);
        }

        var seed = new GameSession(content, "foundry_loop", "normal", "standard");
        var save = seed.CaptureSaveGame();
        save.Economy.Credits = 8_000;
        save.Waves.CurrentWaveNumber = GameConstants.ApexUnlockWave - 1;
        var session = GameSession.RestoreSaveGame(content, save);
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        Require(session.TryPlaceTower("frost_spire", new Vector2(45, 200)) &&
                session.TryChooseTowerDoctrine(session.SelectedTower!.Id, "frost_deep_chill"),
            "Effective-slow Intel scene creates a legal Deep Chill tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, session);
        _ = ui.HandleGameplayInput(Pointer(1170, 664), session);
        scenes.Add(Capture("slow-display-permafrost-preview.png", ui, GameState.Playing, session));
        Require(session.TrySpecializeTower(session.SelectedTower!.Id, "permafrost"),
            "Effective-slow Intel scene completes Permafrost.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), session);
        _ = RenderPixels(ui, GameState.Playing, session);
        scenes.Add(Capture("slow-display-permafrost-current.png", ui, GameState.Playing, session));
        _ = ui.HandleGameplayInput(Pointer(1120, 693), session);
        scenes.Add(Capture("slow-display-apex-preview.png", ui, GameState.Playing, session));
        var tower = session.SelectedTower!;
        var slow = TowerInfo.ComparisonStats(tower.Definition, tower.Level, tower.ApexPreviewLevel)
            .Single(stat => stat.Label == "SLOW");
        Require(slow.Direction == TowerStatDirection.Unchanged && slow.PreviousValue is null &&
                slow.Value == EnemyInstance.MaximumSlowFraction.ToString("P0"),
            "Permafrost Apex preview shows a neutral capped slow while other improvements remain visible.", assertions);
        Require(session.TryUpgradeTower(tower.Id), "Effective-slow Intel scene purchases an eligible Apex.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), session);
        scenes.Add(Capture("slow-display-apex-current.png", ui, GameState.Playing, session));
    }
}
