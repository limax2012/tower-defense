using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertSandboxProtocolSelection(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var definition = content.Towers["needle_turret"];
        var apexes = new List<TowerInstance>();
        foreach (var position in new[] { new Vector2(205, 350), new Vector2(270, 350) })
        {
            Require(session.TryPlaceTower(definition.Id, position) &&
                    session.TryChooseTowerDoctrine(session.SelectedTower!.Id, definition.Tier2Doctrines[0].Id) &&
                    session.TrySpecializeTower(session.SelectedTower!.Id, definition.Specializations[0].Id) &&
                    session.TryUpgradeTower(session.SelectedTower!.Id),
                "Sandbox Protocol selection review places an Apex defense.", assertions);
            apexes.Add(session.SelectedTower!);
        }
        Require(session.TryPlaceTower(definition.Id, new Vector2(205, 415)) && session.StartSandboxWave(1),
            "Sandbox Protocol selection review starts a live wave with an ordinary tower selected.", assertions);
        var selected = session.SelectedTower!;
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var readyPixels = RenderPixels(ui, GameState.Playing, session);
        foreach (var apex in apexes) apex.ActivateOverdrive();
        var otherTimers = apexes.Select(tower => (tower.OverdriveRemaining, tower.ApexProtocolCooldownRemaining)).ToArray();
        var activePixels = RenderPixels(ui, GameState.Playing, session);
        Require(CountChangedPixels(readyPixels, activePixels, new Rectangle(1176, 166, 92, 28)) == 0,
            "Other active Apex towers do not change the selected ordinary tower's Test Protocol button.", assertions);
        scenes.Add(Capture("10d1-sandbox-selected-protocol-ready.png", ui, GameState.Playing, session));
        ui.HandleGameplayInput(Pointer(1222, 180, leftPressed: true), session);
        Require(selected.IsOverdriven && apexes.Select(tower =>
                    (tower.OverdriveRemaining, tower.ApexProtocolCooldownRemaining)).SequenceEqual(otherTimers),
            "Clicking Test Protocol activates only the selected defense during a Sandbox wave.", assertions);
        scenes.Add(Capture("10d2-sandbox-selected-protocol-active.png", ui, GameState.Playing, session));
        ui.HandleGameplayInput(Pointer(1222, 180, leftPressed: true), session);
        Require(!selected.IsOverdriven && apexes.Select(tower =>
                    (tower.OverdriveRemaining, tower.ApexProtocolCooldownRemaining)).SequenceEqual(otherTimers),
            "Clicking Reset Protocol leaves both unrelated Apex timers intact.", assertions);
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        scenes.Add(Capture("10d3-sandbox-protocol-requires-selection.png", ui, GameState.Playing, session));
        ui.HandleGameplayInput(Pointer(1222, 180, leftPressed: true), session);
        Require(session.SelectedTower is null && apexes.Select(tower =>
                    (tower.OverdriveRemaining, tower.ApexProtocolCooldownRemaining)).SequenceEqual(otherTimers),
            "The unselected Sandbox Protocol button cannot reset active Apex towers.", assertions);
    }
}
