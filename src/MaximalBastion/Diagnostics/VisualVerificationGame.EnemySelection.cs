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
    private void AssertEnemySelection(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        foreach (var id in new[] { "t2_runner", "t3_brute", "t4_aegis", "t5_regenerator" })
        {
            session.SpawnEnemy(id, 1, 1);
            session.Enemies[^1].SetSandboxPathDistance(330 + (session.Enemies.Count - 1) * 110, session.Map.Path);
        }
        var enemy = session.Enemies[1];
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Burn, Magnitude = 8, Duration = 5 });
        enemy.ApplyStatus(new StatusApplication { Type = StatusType.Slow, Magnitude = .3f, Duration = 5 });
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var position = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
        var checksum = SessionChecksum.Compute(session, 0);
        Click(Pointer(position.X, position.Y, true));
        Require(ui.SelectedEnemyIntelTarget(session) == enemy && checksum == SessionChecksum.Compute(session, 0),
            "Selecting an enemy opens persistent Intel without changing synchronized state.", assertions);
        Click(Pointer(1100, 600));
        var initial = RenderPixels(ui, GameState.Playing, session);
        var other = PresentationFrame.Create(session, 0).EnemyPosition(session.Enemies[0]);
        Click(Pointer(other.X, other.Y));
        var hoveringOther = RenderPixels(ui, GameState.Playing, session);
        Require(CountChangedPixels(initial, hoveringOther, new Rectangle(972, 474, 296, 238)) == 0,
            "Pinned enemy Intel stays identical when the pointer moves over another enemy.", assertions);
        enemy.SetSandboxPathDistance(480, session.Map.Path);
        enemy.ApplyHealthDamage(35);
        Click(Pointer(1100, 600));
        var damaged = RenderPixels(ui, GameState.Playing, session);
        Require(ui.SelectedEnemyIntelTarget(session) == enemy &&
                CountChangedPixels(initial, damaged, new Rectangle(980, 532, 280, 20)) > 10,
            "The selected enemy's health updates in Intel while it moves and the pointer remains in the sidebar.", assertions);
        using (var rendered = RenderScene(ui, GameState.Playing, session))
        using (var native = DownsampleEnemyLaneScene(rendered))
            scenes.Add(SaveTierScene(native, "enemy-intel-selected.png"));
        Click(Pointer(1100, 600) with { RightPressed = true });
        Require(ui.SelectedEnemyIntelTarget(session) is null,
            "Right click releases pinned Intel while the pointer is in the sidebar.", assertions);

        void Click(InputSnapshot input)
        {
            if (ui.HandleGameplayInput(input, session) == UiAction.None) session.HandleWorldInput(input);
        }
    }
}
