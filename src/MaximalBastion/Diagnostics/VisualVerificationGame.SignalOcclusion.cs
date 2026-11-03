using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertSignalBodyOcclusion(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var original = _renderer;
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer = new GameRenderer { ReducedEffects = reduced, HideEnemyHud = true };
                var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
                session.SpawnEnemy("t4_aegis", 1, 1, signalRole: EnemySignalRole.Jammer);
                session.SpawnEnemy("t4_aegis", 1, 1, signalRole: EnemySignalRole.Disruptor);
                var jammer = session.Enemies[0];
                var disruptor = session.Enemies[1];
                foreach (var enemy in session.Enemies) enemy.AbsorbShield(enemy.Shield);
                jammer.SetSandboxPathDistance(430, session.Map.Path);
                disruptor.SetSandboxPathDistance(438, session.Map.Path);
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                ui.HandleGameplayInput(Pointer(0, 0), session);
                foreach (var front in new[] { jammer, disruptor })
                {
                    var back = ReferenceEquals(front, jammer) ? disruptor : jammer;
                    session.Enemies.Clear();
                    session.Enemies.Add(front);
                    var foregroundOnly = RenderPixels(ui, GameState.Playing, session);
                    session.Enemies.Insert(0, back);
                    var checksum = SessionChecksum.Compute(session, 0);
                    var overlapped = RenderPixels(ui, GameState.Playing, session);
                    var rearCenter = PresentationFrame.Create(session, 0).EnemyPosition(back).ToPoint();
                    var rearGem = new Rectangle(rearCenter.X - 3, rearCenter.Y - 3, 6, 6);
                    Require(CountChangedPixels(foregroundOnly, overlapped, rearGem) == 0,
                        $"{front.SignalRole} foreground, {(reduced ? "Reduced" : "Full")}: the complete front body occludes the rear signal gem.", assertions);
                    Require(checksum == SessionChecksum.Compute(session, 0),
                        $"{front.SignalRole}: carrier depth is presentation-only.", assertions);
                    using var rendered = RenderScene(ui, GameState.Playing, session);
                    using var native = DownsampleEnemyLaneScene(rendered);
                    scenes.Add(SaveTierScene(native,
                        $"signal-overlap-{front.SignalRole.ToString().ToLowerInvariant()}-{(reduced ? "reduced" : "full")}.png"));
                }
                session.Enemies.Clear();
                session.Enemies.Add(jammer);
                var carrierOnly = RenderPixels(ui, GameState.Playing, session);
                var center = PresentationFrame.Create(session, 0).EnemyPosition(jammer).ToPoint();
                session.Effects.AddFlash(jammer.Position, ColorPalette.Paper, .3f, 50);
                session.Effects.AddBeam(jammer.Position - new Vector2(90, 0),
                    jammer.Position + new Vector2(90, 0), ColorPalette.Violet, .3f);
                var crossedByEffects = RenderPixels(ui, GameState.Playing, session);
                Require(CountChangedPixels(carrierOnly, crossedByEffects,
                        new Rectangle(center.X + 13, center.Y - 3, 6, 6)) == 0,
                    $"{(reduced ? "Reduced" : "Full")}: signal armor and gem share foreground priority above crossing combat effects.", assertions);
                session.SpawnEnemy("t5_regenerator", 1, 1);
                var ordinary = session.Enemies[^1];
                ordinary.SetSandboxPathDistance(430, session.Map.Path);
                var core = new Rectangle(center.X - 5, center.Y - 5, 10, 10);
                var ordinaryLast = RenderPixels(ui, GameState.Playing, session);
                session.Enemies.Remove(ordinary);
                session.Enemies.Insert(0, ordinary);
                var ordinaryFirst = RenderPixels(ui, GameState.Playing, session);
                Require(CountChangedPixels(carrierOnly, ordinaryLast, core) == 0 &&
                        CountChangedPixels(ordinaryFirst, ordinaryLast, core) == 0,
                    $"{(reduced ? "Reduced" : "Full")}: complete signal carriers have priority over ordinary bodies regardless of list position.", assertions);
            }
        }
        finally { _renderer = original; }
    }
}
