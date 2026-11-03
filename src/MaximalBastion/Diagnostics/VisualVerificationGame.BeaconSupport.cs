using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertBeaconSupportPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var previousReduced = _renderer.ReducedEffects;
        try
        {
            foreach (var reduced in new[] { false, true })
            {
                _renderer.ReducedEffects = reduced;
                var mode = reduced ? "reduced" : "full";
                var session = new GameSession(content, "relay_divide", "normal", "sandbox_lab");
                Require(session.TryPlaceTower("signal_beacon", new Vector2(285, 390), selectPlaced: false) &&
                        session.TryPlaceTower("signal_beacon", new Vector2(285, 450), selectPlaced: false) &&
                        session.TryPlaceTower("needle_turret", new Vector2(285, 330), selectPlaced: false),
                    $"{mode}: Beacon support review places two legal sources and a fitted Needle receiver.", assertions);
                var primary = session.Towers[0];
                var backup = session.Towers[1];
                var receiver = session.Towers[2];
                Require(primary.TryChooseDoctrine("beacon_amplifier"),
                    $"{mode}: the primary Beacon supplies a stronger bonus than its backup.", assertions);
                foreach (var tower in session.Towers) tower.TickVisual(1);
                session.CancelPlacement();
                session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
                var ui = new UIManager(font, display);
                ConfigureUi(ui, content);
                var receiverBounds = new Rectangle(280, 334, 10, 10);

                session.ToggleSandboxTower(primary.Id);
                session.ToggleSandboxTower(backup.Id);
                session.Update(0);
                var idle = RenderPixels(ui, GameState.Playing, session);
                session.ToggleSandboxTower(primary.Id);
                session.Update(0);
                var supported = RenderPixels(ui, GameState.Playing, session);
                Require(session.GetSupportBuff(receiver).IsActive && session.HasLiveSupport(receiver) &&
                        CountChangedPixels(idle, supported, receiverBounds) >= 12,
                    $"{mode}: active Beacon support visibly illuminates the Needle's receiver at gameplay scale.", assertions);
                if (reduced)
                    Require(CountColorPixels(supported, new Rectangle(283, 337, 4, 3),
                            Color.Lerp(ColorPalette.BeaconHighlight, ColorPalette.Paper, .06f)) >= 12,
                        "Reduced Effects retains the emerald receiver's authored highlight.", assertions);
                scenes.Add(Capture($"61-beacon-supported-{mode}.png", ui, GameState.Playing, session));

                Require(primary.ApplyDisruption(.3f, 0),
                    $"{mode}: the supporting Beacon accepts a temporary shutdown.", assertions);
                var disrupted = RenderPixels(ui, GameState.Playing, session);
                Require(session.GetSupportBuff(receiver).IsActive && !session.HasLiveSupport(receiver) &&
                        CountChangedPixels(idle, disrupted, receiverBounds) == 0,
                    $"{mode}: source disruption extinguishes the receiver immediately, even before the support cache refreshes.", assertions);
                scenes.Add(Capture($"61-beacon-disrupted-{mode}.png", ui, GameState.Playing, session));

                session.Update(0);
                Require(!session.GetSupportBuff(receiver).IsActive && !session.HasLiveSupport(receiver),
                    $"{mode}: the next simulation refresh agrees with the extinguished receiver.", assertions);
                primary.TickVisual(.31f);
                Require(!session.HasLiveSupport(receiver),
                    $"{mode}: source recovery does not advertise support before the gameplay cache activates it.", assertions);
                session.Update(0);
                Require(session.HasLiveSupport(receiver) &&
                        CountChangedPixels(supported, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
                    $"{mode}: refreshed support restores the receiver's exact active appearance.", assertions);

                session.ToggleSandboxTower(primary.Id);
                Require(session.GetSupportBuff(receiver).IsActive && !session.HasLiveSupport(receiver) &&
                        CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
                    $"{mode}: disabling a source extinguishes its receiver without advancing the simulation.", assertions);
                session.ToggleSandboxTower(primary.Id);
                session.ToggleSandboxTower(backup.Id);
                session.Update(0);
                Require(session.GetSupportBuff(receiver).AttackSpeedSourceTowerId == primary.Id &&
                        primary.ApplyDisruption(.3f, 0),
                    $"{mode}: the stronger cached source is interrupted while an overlapping backup remains active.", assertions);
                var fallback = RenderPixels(ui, GameState.Playing, session);
                Require(session.HasLiveSupport(receiver) &&
                        CountChangedPixels(supported, fallback, receiverBounds) == 0,
                    $"{mode}: a weaker live Beacon keeps the receiver continuously illuminated when the cached source shuts down.", assertions);
                scenes.Add(Capture($"61-beacon-fallback-{mode}.png", ui, GameState.Playing, session));

                Require(backup.ApplyDisruption(.3f, 0) && session.GetSupportBuff(receiver).IsActive &&
                        !session.HasLiveSupport(receiver) &&
                        CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
                    $"{mode}: interrupting the final live Beacon removes all residual receiver illumination immediately.", assertions);
                session.Update(0);
                primary.TickVisual(.31f);
                session.Update(0);
                Require(session.HasLiveSupport(receiver) &&
                        CountChangedPixels(supported, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
                    $"{mode}: one recovered Beacon restores support while the other source remains disrupted.", assertions);
                scenes.Add(Capture($"61-beacon-recovered-{mode}.png", ui, GameState.Playing, session));

                Require(session.RemoveSandboxTower(primary.Id) && session.GetSupportBuff(receiver).IsActive &&
                        !session.HasLiveSupport(receiver) &&
                        CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), receiverBounds) == 0,
                    $"{mode}: removing the last operational source clears receiver lights before the next simulation update.", assertions);
            }
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }
}
