using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertNodeContextPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ui = new UIManager(font, display);
        ConfigureUi(ui, content);
        var placement = new GameSession(content, "relay_divide", "normal", "sandbox_lab");
        placement.BeginPlacement("needle_turret");
        placement.HandleWorldInput(Pointer(285, 345));
        var coveredNodes = placement.Map.GetPowerNodes(placement.PlacementPreviewPosition);
        Require(placement.PlacementFailure == PlacementFailure.None && coveredNodes.Count == 1,
            "Node context review resolves legal placement away from the relay device's center.", assertions);
        scenes.Add(Capture("47-node-placement-context.png", ui, GameState.Playing, placement));
        var couplerPixels = NodePlacementOverlayPixels(placement.PlacementPreviewPosition, coveredNodes);
        var couplerBounds = new Rectangle((int)placement.PlacementPreviewPosition.X - 5,
            (int)placement.PlacementPreviewPosition.Y + 16, 10, 8);
        var changed = couplerPixels.Count(pixel => pixel != ColorPalette.Canvas);
        var outside = changed - CountPixelsDifferentFrom(couplerPixels, ColorPalette.Canvas, couplerBounds);
        Require(changed >= 32 && outside == 0,
            "Node placement feedback is confined to the receiving coupler; no line extends to the relay.", assertions);
        placement.HandleWorldInput(Pointer(285, 380));
        var uncoveredNodes = placement.Map.GetPowerNodes(placement.PlacementPreviewPosition);
        Require(placement.PlacementFailure == PlacementFailure.None && uncoveredNodes.Count == 0 &&
                NodePlacementOverlayPixels(placement.PlacementPreviewPosition, uncoveredNodes)
                    .All(pixel => pixel == ColorPalette.Canvas),
            "Leaving node coverage removes the placement coupler completely.", assertions);

        var session = new GameSession(content, "relay_divide", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("signal_beacon", new Vector2(285, 390)) &&
                session.TryPlaceTower("needle_turret", new Vector2(285, 330)),
            "Node coexistence review places a legal Beacon and Needle receiver on the Amplifier Node.", assertions);
        var beacon = session.Towers[0];
        var receiver = session.Towers[1];
        var node = session.Map.GetPowerNodes(receiver.Position).Single();
        foreach (var tower in session.Towers) tower.TickVisual(1);
        session.CancelPlacement();
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var previousReduced = _renderer.ReducedEffects;
        _renderer.ReducedEffects = true;
        try
        {
            session.ToggleSandboxTower(beacon.Id);
            session.Update(0);
            var idle = RenderPixels(ui, GameState.Playing, session);
            scenes.Add(Capture("47d-node-receiver-idle.png", ui, GameState.Playing, session));
            var towerBounds = new Rectangle(261, 306, 48, 48);
            var authoredDamage = node.DamageBonus;
            try
            {
                node.DamageBonus = 0;
                Require(!session.Map.GetPowerBuff(receiver.Position).IsPowered &&
                        CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), towerBounds) == 0,
                    "An idle tower's hardware does not acquire a persistent marker when node power is present.", assertions);
            }
            finally { node.DamageBonus = authoredDamage; }

            session.ToggleSandboxTower(beacon.Id);
            session.Update(0);
            var supported = RenderPixels(ui, GameState.Playing, session);
            scenes.Add(Capture("47e-node-receiver-supported.png", ui, GameState.Playing, session));
            var supportActive = session.GetSupportBuff(receiver).IsActive;
            var supportChangedPixels = CountChangedPixels(idle, supported, towerBounds);
            var receiverBounds = new Rectangle(283, 337, 4, 3);
            var receiverColor = Color.Lerp(ColorPalette.BeaconHighlight, ColorPalette.Paper, .06f);
            Require(supportActive &&
                    CountColorPixels(supported, receiverBounds, receiverColor) >= 12 &&
                    CountChangedPixels(idle, supported, receiverBounds) == supportChangedPixels,
                "Beacon support lights the Needle's fitted green receiver without changing the surrounding node-powered hardware.", assertions);
            receiver.ActivateOverdrive();
            var protocolAndSupport = RenderPixels(ui, GameState.Playing, session);
            scenes.Add(Capture("47f-node-receiver-protocol.png", ui, GameState.Playing, session));
            Require(CountChangedPixels(supported, protocolAndSupport, towerBounds) >= 24,
                "Needle Protocol lighting remains distinct while both Beacon support and node power are active.", assertions);
            session.TrySelectTower(receiver.Id);
            var selected = RenderPixels(ui, GameState.Playing, session);
            var headerBounds = new Rectangle(980, 530, 280, 16);
            var nodeColor = ColorPalette.BalancedAccentText(node.NodeColor, ColorPalette.PanelAlt);
            Require(CountColorPixels(selected, headerBounds, ColorPalette.BeaconHighlight) >= 4 &&
                    CountColorPixels(selected, headerBounds, nodeColor) >= 4,
                "Selected Tower Intel identifies Beacon support in green and the active node in its own color.", assertions);
            _renderer.ReducedEffects = false;
            scenes.Add(Capture("47a-node-beacon-protocol-selected.png", ui, GameState.Playing, session));
            _renderer.ReducedEffects = true;
            scenes.Add(Capture("47b-node-beacon-protocol-reduced.png", ui, GameState.Playing, session));
            ui.HandleGameplayInput(Pointer(1170, 664), session);
            scenes.Add(Capture("47g-node-upgrade-preview.png", ui, GameState.Playing, session));
            ui.HandleGameplayInput(Pointer(0, 0), session);

            session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
            Require(CountChangedPixels(protocolAndSupport, RenderPixels(ui, GameState.Playing, session), towerBounds) == 0,
                "Clearing node-tower inspection removes only the selection treatment.", assertions);
            session.ToggleSandboxTower(beacon.Id);
            session.Update(0);
            receiver.TickVisual(receiver.Protocol.DurationSeconds + .1f);
            Require(!session.GetSupportBuff(receiver).IsActive && !receiver.IsOverdriven &&
                    CountChangedPixels(idle, RenderPixels(ui, GameState.Playing, session), towerBounds) == 0,
                "Beacon expiration and Protocol completion restore the exact idle tower on the node.", assertions);
            scenes.Add(Capture("47c-node-idle-cleanup.png", ui, GameState.Playing, session));
        }
        finally { _renderer.ReducedEffects = previousReduced; }
    }

    private Color[] NodePlacementOverlayPixels(Vector2 position, IReadOnlyList<PowerNodeData> nodes)
    {
        using var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        GameRenderer.DrawPowerNodePlacementIndicator(_batch, _primitives, position, nodes);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
        target.GetData(pixels);
        return pixels;
    }

    private static int CountPixelsDifferentFrom(IReadOnlyList<Color> pixels, Color background, Rectangle region)
    {
        var count = 0;
        for (var y = region.Top * GameConstants.RenderScale; y < region.Bottom * GameConstants.RenderScale; y++)
        for (var x = region.Left * GameConstants.RenderScale; x < region.Right * GameConstants.RenderScale; x++)
            if (pixels[y * GameConstants.RenderWidth + x] != background) count++;
        return count;
    }
}
