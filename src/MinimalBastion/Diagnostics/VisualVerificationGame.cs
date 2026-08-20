using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.Towers;
using MinimalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Diagnostics;

/// <summary>
/// Renders deterministic review scenes with the shipped MonoGame UI while its
/// helper window remains hidden and ineligible for input focus.
/// </summary>
public sealed class VisualVerificationGame : Game
{
    private const int DefaultCoOpPort = 28741;
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _outputDirectory;
    private SpriteBatch _batch = null!;
    private PrimitiveRenderer _primitives = null!;
    private GameRenderer _renderer = null!;
    private bool _complete;

    public VisualVerificationGame(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640,
            PreferredBackBufferHeight = 360,
            SynchronizeWithVerticalRetrace = false,
            PreferMultiSampling = false,
            HardwareModeSwitch = false,
            IsFullScreen = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = false;
        Window.Title = "Minimal Bastion Visual Verification (Hidden)";
        HideAndDisableActivation();
    }

    protected override void Initialize()
    {
        HideAndDisableActivation();
        base.Initialize();
        HideAndDisableActivation();
    }

    protected override void LoadContent()
    {
        HideAndDisableActivation();
        if (IsVerifierForeground())
            throw new InvalidOperationException("The hidden visual verifier unexpectedly became the foreground window.");

        Directory.CreateDirectory(_outputDirectory);
        _batch = new SpriteBatch(GraphicsDevice);
        _primitives = new PrimitiveRenderer(GraphicsDevice);
        _renderer = new GameRenderer { ReducedEffects = false };

        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var font = Content.Load<SpriteFont>("Fonts/Interface");
        var ui = new UIManager(font);
        ConfigureUi(ui, content);

        var assertions = new List<string>();
        AssertEveryAuthoredTowerStatSheetFits(font, content, assertions);
        AssertEveryAuthoredTowerIntelLabelFits(font, content, assertions);
        Require(ui.HandleMainMenu(Pointer(640, 440, leftPressed: true)) == UiAction.CoOp,
            "Online Co-op opens the connection screen without visiting setup.", assertions);

        var scenes = new List<VisualVerificationScene>
        {
            Capture("01-online-coop-connect.png", ui, GameState.CoOpMenu, null)
        };

        Require(ui.HandleCoOpMenu(Pointer(640, 239, leftPressed: true)) == UiAction.OpenCoOpSetup,
            "Only the Host command opens online defense setup.", assertions);
        ui.PrepareGameSetup(true);
        Require(ui.HandleGameSetup(Pointer(0, 0) with { EscapePressed = true }) == UiAction.CoOp,
            "Host setup returns to the connection screen.", assertions);
        Require(ui.HandleGameSetup(Pointer(560, 609, leftPressed: true)) == UiAction.HostCoOp,
            "Host setup confirmation starts hosting.", assertions);
        scenes.Add(Capture("02-online-host-setup.png", ui, GameState.GameSetup, null));

        var defaultEndpoint = OnlineHostEndpoint.Parse("203.0.113.10", DefaultCoOpPort);
        var customEndpoint = OnlineHostEndpoint.Parse("friend.example:30123", DefaultCoOpPort);
        Require(defaultEndpoint.Port == DefaultCoOpPort,
            "A bare IP or DNS address automatically receives TCP port 28741.", assertions);
        Require(customEndpoint.Port == 30123,
            "An explicitly supplied custom port is preserved.", assertions);

        var session = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        session.ConfigureCoOp(1);
        ui.SetCoOpConnectionState(true);
        Require(!UIManager.CoOpTacticalTitleBounds.Intersects(UIManager.CoOpLinkStatusBounds) &&
                !UIManager.CoOpTacticalTitleBounds.Intersects(UIManager.CoOpReadyStatusBounds) &&
                !UIManager.CoOpLinkStatusBounds.Intersects(UIManager.CoOpReadyStatusBounds),
            "Co-op player status has dedicated space outside the Tactical Systems title.", assertions);
        Require(UIManager.CoOpLinkStatusBounds.Left > UIManager.CoOpTacticalTitleBounds.Right &&
                UIManager.CoOpReadyStatusBounds.Left > UIManager.CoOpTacticalTitleBounds.Right &&
                UIManager.CoOpLinkStatusBounds.X == UIManager.CoOpReadyStatusBounds.X &&
                UIManager.CoOpLinkStatusBounds.Width == UIManager.CoOpReadyStatusBounds.Width &&
                UIManager.CoOpLinkStatusBounds.Bottom <= UIManager.CoOpReadyStatusBounds.Top,
            "Co-op connection and ready states form a dedicated two-row column to the title's right.", assertions);
        var compactReadyStatus = UIManager.CoOpReadyStatusLabel(1, 0b01, false, false, 7.1f);
        Require(font.MeasureString(compactReadyStatus).X * 0.30f * GameConstants.FontDrawScale <=
                UIManager.CoOpReadyStatusBounds.Width - 8,
            "The co-op ready and early-bonus status fits its sidebar row without ellipsis.", assertions);
        Require(!UIManager.HudThreatBounds.Intersects(UIManager.HudRunSetupBounds),
            "The active threat summary cannot enter the Run Setup region.", assertions);
        var baseline = RenderPixels(ui, GameState.Playing, session);
        var remotePosition = new Vector2(245, 380);
        var remotePreviewPosition = new Vector2(280, 410);
        ui.SetRemoteCoOpCursor(remotePosition, 2, placementTowerId: "needle_turret",
            hasPlacementPreview: true, placementPreviewPosition: remotePreviewPosition);
        scenes.Add(Capture("03-remote-tower-placement.png", ui, GameState.Playing, session));
        var withGhost = RenderPixels(ui, GameState.Playing, session);
        var changedGhostPixels = CountChangedPixels(baseline, withGhost,
            new Rectangle((int)remotePreviewPosition.X - 40, (int)remotePreviewPosition.Y - 40, 80, 80));
        Require(changedGhostPixels >= 150,
            $"Remote filled placement ghost has a clear visible footprint ({changedGhostPixels} pixels).", assertions);
        Require(CountChangedPixels(baseline, withGhost,
                    new Rectangle((int)remotePreviewPosition.X - 5, (int)remotePreviewPosition.Y - 5, 11, 11)) >= 80,
            "Remote placement retains a recognizable filled tower interior.", assertions);
        ui.AdvanceVisualTime(0.25f);
        ui.AdvanceVisualTime(0.25f);
        ui.AdvanceVisualTime(0.25f);
        var breathedGhost = RenderPixels(ui, GameState.Playing, session);
        Require(CountChangedPixels(withGhost, breathedGhost,
                    new Rectangle((int)remotePreviewPosition.X - 32, (int)remotePreviewPosition.Y - 32, 64, 64)) >= 8,
            "Remote placement silhouette has a subtle breathing pulse without a placed-owner ring.", assertions);
        Require(Vector2.Distance(remotePosition, remotePreviewPosition) > 20,
            "Remote raw cursor and snapped build ghost retain separate coordinates.", assertions);

        var remotePlatePosition = new Vector2(360, 250);
        ui.SetRemoteCoOpCursor(remotePosition, 2, tacticalPlacement: TacticalPlacementKind.PulsePlate,
            hasPlacementPreview: true, placementPreviewPosition: remotePlatePosition);
        var withPlateGhost = RenderPixels(ui, GameState.Playing, session);
        Require(CountChangedPixels(baseline, withPlateGhost,
                    new Rectangle((int)remotePlatePosition.X - 24, (int)remotePlatePosition.Y - 24, 48, 48)) >= 120,
            "Remote pulse-plate placement renders a recognizable snapped tactical ghost.", assertions);
        scenes.Add(Capture("03a-remote-pulse-plate-placement.png", ui, GameState.Playing, session));
        ui.SetRemoteCoOpCursor(null, 0);

        var placementSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        var placementBaselinePixels = RenderPixels(ui, GameState.Playing, placementSession);
        placementSession.BeginPlacement("needle_turret");
        placementSession.HandleWorldInput(Pointer(45, 200));
        Require(placementSession.PlacementFailure == PlacementFailure.None,
            "Placement-validity scene resolves an authored buildable position.", assertions);
        var validPlacementPixels = RenderPixels(ui, GameState.Playing, placementSession);
        scenes.Add(Capture("03a-valid-placement-ghost.png", ui, GameState.Playing, placementSession));
        Require(CountChangedPixels(placementBaselinePixels, validPlacementPixels,
                    new Rectangle(130, 75, 48, 250)) >= 35,
            "Tower placement retains the full dashed attack/aura range preview.", assertions);
        Require(CountColorPixels(validPlacementPixels, new Rectangle(37, 192, 16, 16), ColorPalette.PlacementValid) < 10,
            "Tower placement no longer overlays a green check icon at its center.", assertions);

        var localNodePlacementSession = new GameSession(content, "relay_divide", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        localNodePlacementSession.BeginPlacement("needle_turret");
        localNodePlacementSession.HandleWorldInput(Pointer(285, 330));
        Require(localNodePlacementSession.HasPlacementPreview &&
                localNodePlacementSession.Map.GetPowerNodes(localNodePlacementSession.PlacementPreviewPosition).Count == 1,
            "Local node-placement scene resolves the Amplifier Node.", assertions);
        var localNodePlacementPixels = RenderPixels(ui, GameState.Playing, localNodePlacementSession);
        scenes.Add(Capture("03a1-local-node-placement-marker.png", ui, GameState.Playing, localNodePlacementSession));
        var amplifierColor = localNodePlacementSession.Map.GetPowerNodes(localNodePlacementSession.PlacementPreviewPosition)[0].NodeColor;
        Require(CountColorPixels(localNodePlacementPixels, new Rectangle(300, 345, 18, 18), amplifierColor) >= 20,
            "Local tower placement carries a node-colored marker beside the ghost.", assertions);

        var remoteNodePlacementSession = new GameSession(content, "relay_divide", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        remoteNodePlacementSession.ConfigureCoOp(1);
        ui.SetRemoteCoOpCursor(new Vector2(250, 300), 2, placementTowerId: "needle_turret",
            hasPlacementPreview: true, placementPreviewPosition: new Vector2(285, 330));
        var remoteNodePlacementPixels = RenderPixels(ui, GameState.Playing, remoteNodePlacementSession);
        scenes.Add(Capture("03a2-remote-node-placement-marker.png", ui, GameState.Playing, remoteNodePlacementSession));
        Require(CountColorPixels(remoteNodePlacementPixels, new Rectangle(300, 345, 18, 18), amplifierColor) >= 20,
            "Remote co-op tower placement shows the same node marker at its synchronized snapped position.", assertions);
        ui.SetRemoteCoOpCursor(null, 0);

        placementSession.HandleWorldInput(Pointer(100, 200));
        Require(placementSession.PlacementFailure == PlacementFailure.None && placementSession.HasPlacementPreview &&
                Vector2.Distance(placementSession.PlacementPosition, placementSession.PlacementPreviewPosition) > 20,
            "An imprecise cursor beside a build zone snaps to a nearby legal tower position.", assertions);
        scenes.Add(Capture("03b-assisted-placement-snap.png", ui, GameState.Playing, placementSession));

        var cornerSnapSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        cornerSnapSession.BeginPlacement("needle_turret");
        cornerSnapSession.HandleWorldInput(Pointer(321, 451));
        Require(cornerSnapSession.HasPlacementPreview && cornerSnapSession.PlacementFailure == PlacementFailure.None &&
                cornerSnapSession.PlacementPreviewPosition.X < 320 && cornerSnapSession.PlacementPreviewPosition.Y < 450 &&
                Vector2.Distance(cornerSnapSession.PlacementPosition, cornerSnapSession.PlacementPreviewPosition) < 4,
            "A Foundry corner cursor snaps to the truly nearest upper zone instead of a later-authored lower zone.", assertions);
        scenes.Add(Capture("03b-nearest-zone-corner-snap.png", ui, GameState.Playing, cornerSnapSession));

        placementSession.HandleWorldInput(Pointer(100, 100));
        Require(placementSession.PlacementFailure != PlacementFailure.None && !placementSession.HasPlacementPreview,
            "A cursor too far from every legal build point does not show an invalid ghost.", assertions);
        var invalidPlacementPixels = RenderPixels(ui, GameState.Playing, placementSession);
        scenes.Add(Capture("03c-no-invalid-placement-ghost.png", ui, GameState.Playing, placementSession));
        Require(CountColorPixels(invalidPlacementPixels, new Rectangle(92, 92, 16, 16), ColorPalette.PlacementInvalid) < 10,
            "Tower placement no longer overlays a red X icon at an illegal cursor.", assertions);

        var preparePlacementSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        _ = RenderPixels(ui, GameState.Playing, preparePlacementSession);
        _ = ui.HandleGameplayInput(Pointer(1042, 247), preparePlacementSession);
        var preparePlacementPixels = RenderPixels(ui, GameState.Playing, preparePlacementSession);
        scenes.Add(Capture("03d-prepare-placement-guidance.png", ui, GameState.Playing, preparePlacementSession));
        var comparisonSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        Require(comparisonSession.TryPlaceTower("needle_turret", new Vector2(45, 200)),
            "Upgrade comparison scene places a selected Needle Turret.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), comparisonSession);
        scenes.Add(Capture("04a-current-stat-grid.png", ui, GameState.Playing, comparisonSession));
        var nodeIntelSession = new GameSession(content, "relay_divide", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        Require(nodeIntelSession.TryPlaceTower("needle_turret", new Vector2(285, 330)),
            "Power-node Intel scene places a selected tower on the Amplifier Node.", assertions);
        scenes.Add(Capture("04a1-power-node-current-stat-grid.png", ui, GameState.Playing, nodeIntelSession));
        var initialTargetMode = comparisonSession.SelectedTower!.TargetMode;
        var targetButtonCenter = ui.TargetButtonBounds.Center;
        _ = ui.HandleGameplayInput(Pointer(targetButtonCenter.X, targetButtonCenter.Y, true), comparisonSession);
        Require(ui.IsTargetPickerOpen && comparisonSession.SelectedTower.TargetMode == initialTargetMode,
            "Opening the target picker does not cycle or temporarily alter targeting.", assertions);
        scenes.Add(Capture("04a2-target-picker-drop-up.png", ui, GameState.Playing, comparisonSession));
        Require(ui.TargetModeButtonBounds.Count == Enum.GetValues<TargetMode>().Length &&
                ui.TargetPickerBounds.Left >= GameConstants.MapWidth &&
                ui.TargetPickerBounds.Right <= GameConstants.LogicalWidth &&
                ui.TargetPickerBounds.Bottom <= ui.TargetButtonBounds.Top &&
                !ui.TargetPickerBounds.Intersects(ui.UpgradeButtonBounds) &&
                !ui.TargetPickerBounds.Intersects(ui.SellButtonBounds),
            "The complete target picker drops upward inside the sidebar without covering management buttons.", assertions);
        var armoredTargetCenter = ui.TargetModeButtonBounds[TargetMode.Armored].Center;
        _ = ui.HandleGameplayInput(Pointer(armoredTargetCenter.X, armoredTargetCenter.Y, true), comparisonSession);
        Require(!ui.IsTargetPickerOpen && comparisonSession.SelectedTower.TargetMode == TargetMode.Armored,
            "Choosing an explicit target mode applies it once and closes the picker.", assertions);

        var coOpTargetSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        coOpTargetSession.ConfigureCoOp(2);
        Require(coOpTargetSession.TryPlaceTower("needle_turret", new Vector2(45, 200), 1),
            "Co-op target-picker scene places a shared tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, coOpTargetSession);
        targetButtonCenter = ui.TargetButtonBounds.Center;
        var targetCommands = new List<GameCommand>();
        _ = ui.HandleGameplayInput(Pointer(targetButtonCenter.X, targetButtonCenter.Y, true), coOpTargetSession,
            targetCommands.Add, 2);
        _ = RenderPixels(ui, GameState.Playing, coOpTargetSession);
        var fastestTargetCenter = ui.TargetModeButtonBounds[TargetMode.Fastest].Center;
        _ = ui.HandleGameplayInput(Pointer(fastestTargetCenter.X, fastestTargetCenter.Y, true), coOpTargetSession,
            targetCommands.Add, 2);
        Require(targetCommands.Count == 1 && targetCommands[0].Type == GameCommandType.SetTargetMode &&
                targetCommands[0].TargetMode == TargetMode.Fastest &&
                coOpTargetSession.SelectedTower!.TargetMode != TargetMode.Fastest,
            "Co-op target picking emits one exact authoritative command without speculative target cycling.", assertions);

        ui.HandleGameplayInput(Pointer(1170, 700), comparisonSession);
        scenes.Add(Capture("04b-upgrade-old-to-new.png", ui, GameState.Playing, comparisonSession));
        var calibratedFeed = content.Towers["needle_turret"].Tier2Doctrines
            .Single(doctrine => doctrine.Id == "needle_calibrator");
        var calibratedStats = TowerInfo.ComparisonStats(content.Towers["needle_turret"],
            content.Towers["needle_turret"].Levels[0],
            content.Towers["needle_turret"].Levels[1].WithDoctrine(calibratedFeed));
        Require(calibratedStats.Single(stat => stat.Label == "RATE").Direction == TowerStatDirection.Unchanged,
            "Precision Feed does not color an unchanged displayed rate as an increase.", assertions);
        Require(TowerInfo.ComparisonStatText(calibratedStats.Single(stat => stat.Label == "DAMAGE")) == "DAMAGE 8 -> 11",
            "Changed preview stats show old and new values.", assertions);
        Require(TowerInfo.ComparisonStatValueText(calibratedStats.Single(stat => stat.Label == "DAMAGE")) == "8 -> 11",
            "Two-line preview cells preserve the complete old-to-new value pair.", assertions);

        var autoHeaderSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        autoHeaderSession.ConfigureCoOp(2);
        Require(autoHeaderSession.TryPlaceTower("needle_turret", new Vector2(190, 175), 2),
            "Auto header scene places a player-two tower.", assertions);
        var autoTower = autoHeaderSession.SelectedTower!;
        Require(autoHeaderSession.TryPlaceTower("frost_spire", new Vector2(230, 175), 2),
            "Auto foreground scene places a later neighboring tower whose art overlaps the marker area.", assertions);
        var laterTower = autoHeaderSession.SelectedTower!;
        autoHeaderSession.HandleInspectionInput(Pointer(190, 175, leftPressed: true));
        var withoutAutoMarker = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        Require(autoHeaderSession.TryToggleAutoProtocol(autoTower.Id, 2),
            "Auto header scene arms the earlier tower beneath its later neighbor.", assertions);
        var withAutoMarker = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        var changedAutoMarkerPixels = CountChangedPixels(withoutAutoMarker, withAutoMarker,
            new Rectangle(160, 176, 30, 30));
        Require(changedAutoMarkerPixels >= 90,
            $"Arming Auto adds a compact, legible badge ({changedAutoMarkerPixels} changed pixels).", assertions);
        var changedOutsideBadge = CountChangedPixels(withoutAutoMarker, withAutoMarker,
            new Rectangle(155, 140, 75, 75)) - changedAutoMarkerPixels;
        Require(changedOutsideBadge >= 40,
            $"Auto restores compact L-shaped corner brackets around its tower ({changedOutsideBadge} changed pixels outside badge).", assertions);
        Require(autoHeaderSession.TryToggleAutoProtocol(laterTower.Id, 2) &&
                autoHeaderSession.AutoOverdriveTowerId == laterTower.Id &&
                autoHeaderSession.TryToggleAutoProtocol(autoTower.Id, 2) &&
                autoHeaderSession.AutoOverdriveTowerId == autoTower.Id,
            "Moving Auto transfers foreground priority; the previous tower immediately returns to normal order.", assertions);
        ui.HandleGameplayInput(Pointer(0, 0), autoHeaderSession, _ => { }, 2);
        scenes.Add(Capture("05-auto-coop-owner.png", ui, GameState.Playing, autoHeaderSession));
        ui.SetRemoteCoOpCursor(new Vector2(330, 220), 1);
        var withoutRemoteSelection = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        ui.SetRemoteCoOpCursor(new Vector2(330, 220), 1, selectedTowerId: autoTower.Id);
        var withRemoteSelection = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        Require(CountChangedPixels(withoutRemoteSelection, withRemoteSelection, new Rectangle(165, 130, 50, 35)) >= 160,
            "Remote inspection uses an opaque high-contrast player flag instead of Auto-like square corners.", assertions);
        scenes.Add(Capture("05a-remote-selected-player-flag.png", ui, GameState.Playing, autoHeaderSession));
        ui.SetRemoteCoOpCursor(new Vector2(330, 220), 2, selectedTowerId: autoTower.Id);
        var playerTwoSelection = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        Require(CountColorPixels(playerTwoSelection, new Rectangle(165, 130, 50, 35), ColorPalette.Coral) >= 250,
            "P2 uses the same centered selection-flag geometry as P1 with its own opaque color.", assertions);
        scenes.Add(Capture("05a2-remote-selected-player-two-flag.png", ui, GameState.Playing, autoHeaderSession));
        ui.SetRemoteCoOpCursor(null, 0);

        var crowdedMarkerSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        crowdedMarkerSession.ConfigureCoOp(2);
        crowdedMarkerSession.Economy.AddCredits(5_000);
        var crowdedTowerIds = new[]
        {
            "needle_turret", "frost_spire", "shard_fan",
            "watchtower", "ember_coil", "breaker_cannon",
            "signal_beacon", "arc_relay", "prism_beam"
        };
        var crowdedIndex = 0;
        foreach (var y in new[] { 330f, 370f, 410f })
        foreach (var x in new[] { 190f, 230f, 270f })
        {
            Require(crowdedMarkerSession.TryPlaceTower(crowdedTowerIds[crowdedIndex++], new Vector2(x, y),
                    crowdedIndex % 2 + 1, selectPlaced: false),
                "Crowded co-op marker scene places a legal tower cluster.", assertions);
        }
        var crowdedAutoTower = crowdedMarkerSession.Towers[4];
        var crowdedRemoteTower = crowdedMarkerSession.Towers[5];
        Require(crowdedMarkerSession.TryToggleAutoProtocol(crowdedAutoTower.Id, 2),
            "Crowded co-op marker scene arms its center Auto tower.", assertions);
        ui.SetRemoteCoOpCursor(new Vector2(500, 600), 1);
        var crowdedWithoutSelection = RenderPixels(ui, GameState.Playing, crowdedMarkerSession);
        ui.SetRemoteCoOpCursor(new Vector2(500, 600), 1, selectedTowerId: crowdedRemoteTower.Id);
        var crowdedWithSelection = RenderPixels(ui, GameState.Playing, crowdedMarkerSession);
        var crowdedTagRegion = new Rectangle((int)crowdedRemoteTower.Position.X - 22,
            (int)crowdedRemoteTower.Position.Y - crowdedRemoteTower.Definition.Visual.Radius - 25, 44, 24);
        Require(CountChangedPixels(crowdedWithoutSelection, crowdedWithSelection, crowdedTagRegion) >= 160,
            "The P1 remote-selection flag remains fully visible above a dense tower cluster.", assertions);
        scenes.Add(Capture("05b-crowded-auto-and-remote-selection.png", ui, GameState.Playing, crowdedMarkerSession));
        ui.SetRemoteCoOpCursor(null, 0);

        scenes.Add(Capture("06-protocol-auto-library.png", ui, GameState.TowerLibrary, null));
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 7 });
        Require(ui.SelectedLibraryTowerId == "signal_beacon",
            "Tactical Library contrast scene selects Signal Beacon.", assertions);
        var signalLibraryPixels = RenderPixels(ui, GameState.TowerLibrary, null);
        scenes.Add(Capture("06a-signal-beacon-library-contrast.png", ui, GameState.TowerLibrary, null));
        var signalLineColor = content.Towers["signal_beacon"].Visual.AccentColor;
        var breakerLineColor = content.Towers["breaker_cannon"].Visual.AccentColor;
        Require(signalLineColor != breakerLineColor &&
                ColorPalette.ContrastRatio(signalLineColor, ColorPalette.PanelAlt) >= 3f,
            "Signal Beacon uses its readable outer-ring color, distinct from Breaker Cannon.", assertions);
        Require(CountColorPixels(signalLibraryPixels, new Rectangle(330, 110, 890, 540),
                    signalLineColor) >= 1_000,
            "Signal Beacon text and structural rules share its outer-ring color.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(760, 57, leftPressed: true));
        Require(ui.LibraryShowsThreats, "Tactical Library contrast scene opens threat status language.", assertions);
        scenes.Add(Capture("06b-threat-library-contrast.png", ui, GameState.TowerLibrary, null));
        _ = ui.HandleTitleTowerLibrary(Pointer(850, 57, leftPressed: true));
        Require(ui.LibraryShowsCampaign, "Tactical Library contrast scene opens campaign wave scaling.", assertions);
        scenes.Add(Capture("06c-campaign-library-contrast.png", ui, GameState.TowerLibrary, null));
        Require(ColorPalette.ContrastRatio(
            ColorPalette.BalancedAccentText(ColorPalette.Gold, ColorPalette.PanelAlt),
            ColorPalette.PanelAlt) >= 2.55f,
            "Small gold Tactical Library text balances readability with palette brightness.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(666, 57, leftPressed: true));
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 8 });
        Require(ui.SelectedLibraryTowerId == "arc_relay",
            "Tactical Library color scene selects Arc Relay.", assertions);
        var arcLibraryPixels = RenderPixels(ui, GameState.TowerLibrary, null);
        scenes.Add(Capture("06d-arc-relay-library-color.png", ui, GameState.TowerLibrary, null));
        Require(CountColorPixels(arcLibraryPixels, new Rectangle(330, 110, 890, 540),
                    content.Towers["arc_relay"].Visual.AccentColor) >= 1_000,
            "Arc Relay library text and rules share its outer-ring green.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 6 });
        Require(ui.SelectedLibraryTowerId == "breaker_cannon",
            "Tactical Library color scene selects Breaker Cannon.", assertions);
        var breakerLibraryPixels = RenderPixels(ui, GameState.TowerLibrary, null);
        scenes.Add(Capture("06e-breaker-library-color.png", ui, GameState.TowerLibrary, null));
        Require(CountColorPixels(breakerLibraryPixels, new Rectangle(330, 110, 890, 540),
                    breakerLineColor) >= 1_000,
            "Breaker Cannon library text and rules share its outer-ring gold.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 3 });
        Require(ui.SelectedLibraryTowerId == "shard_fan",
            "Tactical Library color scene selects Shard Fan.", assertions);
        var shardLibraryPixels = RenderPixels(ui, GameState.TowerLibrary, null);
        scenes.Add(Capture("06f-shard-library-color.png", ui, GameState.TowerLibrary, null));
        Require(CountColorPixels(shardLibraryPixels, new Rectangle(330, 110, 890, 540),
                    content.Towers["shard_fan"].Visual.AccentColor) >= 1_000,
            "Shard Fan library text and rules share its outer-ring orange.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 9 });
        Require(ui.SelectedLibraryTowerId == "siege_mortar",
            "Tactical Library color scene selects Siege Mortar.", assertions);
        var mortarLibraryPixels = RenderPixels(ui, GameState.TowerLibrary, null);
        scenes.Add(Capture("06g-mortar-library-color.png", ui, GameState.TowerLibrary, null));
        Require(CountColorPixels(mortarLibraryPixels, new Rectangle(330, 110, 890, 540),
                    content.Towers["siege_mortar"].Visual.AccentColor) >= 1_000,
            "Siege Mortar library text and rules share its bright outer-ring coral.", assertions);
        _ = ui.HandleTitleTowerLibrary(Pointer(0, 0) with { TowerHotkey = 1 });
        Require(autoHeaderSession.StartNextWave(), "Live co-op header scene starts an active wave.", assertions);
        scenes.Add(Capture("07-active-coop-header.png", ui, GameState.Playing, autoHeaderSession));
        Require(ui.HandleGameplayInput(Pointer(0, 0) with { TabPressed = true }, autoHeaderSession, _ => { }, 2) == UiAction.None &&
                ui.IsGameplayOverlayOpen && !autoHeaderSession.IsCoOpPaused,
            "Tab opens the Tactical Library over an unpaused co-op wave without pausing it.", assertions);
        scenes.Add(Capture("08-live-coop-library.png", ui, GameState.Playing, autoHeaderSession));
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { NavigateRightPressed = true }, autoHeaderSession, _ => { }, 2);
        Require(ui.LibraryShowsThreats,
            "Right Arrow changes Tactical Library pages while the co-op simulation remains live.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { NavigateLeftPressed = true }, autoHeaderSession, _ => { }, 2);
        Require(!ui.LibraryShowsThreats && !ui.LibraryShowsCampaign && !ui.LibraryShowsProfiles && !ui.LibraryShowsSystems,
            "Left Arrow changes Tactical Library pages in the reverse direction.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { TabPressed = true }, autoHeaderSession, _ => { }, 2);
        Require(!ui.IsGameplayOverlayOpen,
            "Tab toggles the live co-op Tactical Library closed.", assertions);
        Require(autoHeaderSession.SetCoOpPaused(true, 1),
            "Shared-pause visual scene enters authoritative pause.", assertions);
        Require(!UIManager.CoOpPauseResumeBounds.Intersects(UIManager.CoOpPauseLibraryBounds) &&
                !UIManager.CoOpPauseLibraryBounds.Intersects(UIManager.CoOpPauseRestartBounds) &&
                !UIManager.CoOpPauseRestartBounds.Intersects(UIManager.CoOpPauseMenuBounds) &&
                UIManager.CoOpPauseResumeBounds.X >= GameConstants.SidebarX &&
                UIManager.CoOpPauseMenuBounds.Right <= GameConstants.LogicalWidth,
            "Compact co-op pause controls are separated and contained entirely in the sidebar.", assertions);
        scenes.Add(Capture("09-compact-coop-pause.png", ui, GameState.Playing, autoHeaderSession));

        var pauseSpecsSession = new GameSession(content, "crosswind_basin", DifficultyCatalog.DefaultId, "no_reserves");
        var pauseSpecsPixels = RenderPixels(ui, GameState.Paused, pauseSpecsSession);
        Require(CountColorPixels(pauseSpecsPixels, new Rectangle(390, 570, 500, 24), ColorPalette.Muted) >= 50,
            "Solo pause run specifications use the surrounding muted blue-gray text color.", assertions);
        scenes.Add(Capture("09a-solo-pause-run-specs.png", ui, GameState.Paused, pauseSpecsSession));

        var beaconSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        beaconSession.Economy.AddCredits(1_000);
        _ = RenderPixels(ui, GameState.Playing, beaconSession);
        _ = ui.HandleGameplayInput(Pointer(1042, 379, leftPressed: true), beaconSession);
        scenes.Add(Capture("10a-signal-beacon-placement-intel.png", ui, GameState.Playing, beaconSession));
        beaconSession.CancelPlacement();
        Require(beaconSession.TryPlaceTower("signal_beacon", new Vector2(45, 200)),
            "Signal Beacon contrast scene places the support tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, beaconSession);
        _ = ui.HandleGameplayInput(Pointer(1170, 664), beaconSession);
        scenes.Add(Capture("10-signal-beacon-old-to-new.png", ui, GameState.Playing, beaconSession));
        _ = ui.HandleGameplayInput(Pointer(0, 0), beaconSession);
        Require(beaconSession.TryChooseTowerDoctrine(beaconSession.SelectedTower!.Id, "beacon_amplifier"),
            "Signal Beacon contrast scene reaches its final choices.", assertions);
        var beaconFill = content.Towers["signal_beacon"].Visual.PrimaryColor;
        var beaconText = UIManager.TowerIntelPrimaryUpgradeTextColor(content.Towers["signal_beacon"]);
        Require(ColorPalette.ContrastRatio(ColorPalette.Paper, beaconFill) < UIManager.ColoredButtonWhiteContrastThreshold &&
                beaconText == ColorPalette.Ink && ColorPalette.ContrastRatio(beaconText, beaconFill) >= 7f,
            "Signal Beacon's pale upgrade control falls below the white threshold and receives dark text.", assertions);
        var beaconUpgradePixels = RenderPixels(ui, GameState.Playing, beaconSession);
        Require(CountColorPixels(beaconUpgradePixels, new Rectangle(1074, 650, 192, 28), ColorPalette.Ink) >= 20,
            "Signal Beacon's upper Tower Intel upgrade label renders in black.", assertions);
        scenes.Add(Capture("10b-signal-beacon-upgrade-contrast.png", ui, GameState.Playing, beaconSession));

        var prismSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        prismSession.Economy.AddCredits(2_000);
        Require(prismSession.TryPlaceTower("prism_beam", new Vector2(45, 200)) &&
                prismSession.TryChooseTowerDoctrine(prismSession.SelectedTower!.Id, "prism_frequency"),
            "Dense Intel scene advances Prism Beam to its final-role previews.", assertions);
        _ = RenderPixels(ui, GameState.Playing, prismSession);
        _ = ui.HandleGameplayInput(Pointer(1170, 664), prismSession);
        Require(UIManager.TowerIntelPrimaryUpgradeTextColor(content.Towers["prism_beam"]) == ColorPalette.Paper,
            "Prism Beam keeps white upgrade text above the authored contrast threshold.", assertions);
        var prismUpgradePixels = RenderPixels(ui, GameState.Playing, prismSession);
        Require(CountColorPixels(prismUpgradePixels, new Rectangle(1074, 650, 192, 28), ColorPalette.Paper) >= 20,
            "Prism Beam's upper Tower Intel upgrade label renders in white.", assertions);
        scenes.Add(Capture("10c-prism-shield-old-to-new.png", ui, GameState.Playing, prismSession));

        var sandboxBreakerSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, "sandbox_lab");
        Require(sandboxBreakerSession.TryPlaceTower("breaker_cannon", new Vector2(45, 200)) &&
                sandboxBreakerSession.TryChooseTowerDoctrine(sandboxBreakerSession.SelectedTower!.Id, "breaker_repeater") &&
                sandboxBreakerSession.TrySpecializeTower(sandboxBreakerSession.SelectedTower!.Id, "breach_round"),
            "Finalized Sandbox Intel scene advances Breaker Cannon through Piercing Round.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), sandboxBreakerSession);
        _ = RenderPixels(ui, GameState.Playing, sandboxBreakerSession);
        _ = ui.HandleGameplayInput(Pointer(1247, 112, leftPressed: true), sandboxBreakerSession);
        var sandboxBreakerPixels = RenderPixels(ui, GameState.Playing, sandboxBreakerSession);
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(455, 14, 160, 28), ColorPalette.Paper) >= 20,
            "Sandbox SEND TEST remains a deliberate white-text exception.", assertions);
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(1018, 102, 200, 20), ColorPalette.Paper) >= 20,
            "Sandbox Crawler selector keeps its requested white label.", assertions);
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(1192, 506, 60, 16), ColorPalette.Ink) >= 20,
            "Sandbox DISABLE renders dark text on its orange button.", assertions);
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(1257, 503, 9, 22), ColorPalette.PanelAlt) >= 150,
            "Sandbox DISABLE leaves a clear inset before the Tower Intel outline.", assertions);
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(1180, 170, 84, 20), ColorPalette.Paper) >= 20,
            "Sandbox Protocol retains white text on its violet button.", assertions);
        scenes.Add(Capture("10d-sandbox-final-breaker-intel.png", ui, GameState.Playing, sandboxBreakerSession));

        var finalBreakerSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        finalBreakerSession.Economy.AddCredits(2_000);
        Require(finalBreakerSession.TryPlaceTower("breaker_cannon", new Vector2(45, 200)) &&
                finalBreakerSession.TryChooseTowerDoctrine(finalBreakerSession.SelectedTower!.Id, "breaker_repeater") &&
                finalBreakerSession.TrySpecializeTower(finalBreakerSession.SelectedTower!.Id, "breach_round") &&
                finalBreakerSession.TryToggleAutoProtocol(finalBreakerSession.SelectedTower!.Id),
            "Finalized standard Intel scene advances and arms Breaker Cannon without duplicating protocol text.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), finalBreakerSession);
        scenes.Add(Capture("10e-standard-final-breaker-intel.png", ui, GameState.Playing, finalBreakerSession));

        var apexSeed = new GameSession(content, "foundry_loop", "bastion", "no_reserves");
        var apexSave = apexSeed.CaptureSaveGame();
        apexSave.Economy.Credits = 8_000;
        apexSave.Waves.CurrentWaveNumber = GameConstants.ApexUnlockWave - 1;
        apexSave.Waves.IsFinalWaveCleared = true;
        apexSave.Waves.EndlessModeEnabled = true;
        var apexSession = GameSession.RestoreSaveGame(content, apexSave);
        Require(apexSession.TryPlaceTower("needle_turret", new Vector2(45, 200)) &&
                apexSession.TryChooseTowerDoctrine(apexSession.SelectedTower!.Id, "needle_cycler") &&
                apexSession.TrySpecializeTower(apexSession.SelectedTower!.Id, "rapid_array") &&
                apexSession.CanApexUpgrade(apexSession.SelectedTower),
            "Wave-31 Fundamentals scene exposes Apex on a completed tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, apexSession);
        _ = ui.HandleGameplayInput(Pointer(1120, 693), apexSession);
        scenes.Add(Capture("10f-apex-upgrade-preview.png", ui, GameState.Playing, apexSession));
        Require(apexSession.TryUpgradeSelectedTower(),
            "Wave-31 Fundamentals scene purchases the Apex promotion.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), apexSession);
        var promotedApexPixels = RenderPixels(ui, GameState.Playing, apexSession);
        Require(CountColorPixels(promotedApexPixels, new Rectangle(1204, 484, 58, 22), ColorPalette.Violet) >= 10,
            "Promoted Tower Intel identifies Apex in a reserved top-right header gutter.", assertions);
        scenes.Add(Capture("10g-apex-current-intel.png", ui, GameState.Playing, apexSession));

        var settings = new UserSettings { AutoStartWaves = true, AutoStartDelaySeconds = 10 };
        ui.ConfigureSettings(settings);
        scenes.Add(Capture("11-settings-auto-start.png", ui, GameState.Settings, null));
        Require(ui.HandleSettingsInput(Pointer(640, 357, leftPressed: true)) == UiAction.ApplySettings &&
                !settings.AutoStartWaves,
            "The Settings screen exposes a persistent mouse-only Auto-start control.", assertions);

        ui.ConfigureSaveSlots(
        [
            new SaveSlotInfo(SaveSlotRepository.AutosaveSlot, true, true, "foundry_loop", "normal", "standard", 28, true, 12, 8_400, DateTime.UtcNow),
            new SaveSlotInfo(1, false)
        ], false, SaveSlotRepository.AutosaveSlot);
        scenes.Add(Capture("12-save-solo-or-host.png", ui, GameState.SaveSlots, null));
        Require(ui.HandleSaveSlots(Pointer(595, 543, leftPressed: true)) == UiAction.HostSavedGame,
            "A saved co-op defense exposes an explicit online-host continuation.", assertions);
        Require(ui.HandleSaveSlots(Pointer(415, 543, leftPressed: true)) == UiAction.ConfirmSaveSlot,
            "The same saved co-op defense exposes an explicit solo continuation.", assertions);

        var history = new RunHistoryEntry
        {
            RunId = "visual-history",
            CompletedAtUtc = DateTime.UtcNow,
            IsCoOp = true,
            IsEndless = true,
            MapId = "foundry_loop",
            MapName = "Foundry Loop",
            DifficultyId = "normal",
            DifficultyName = "Medium",
            ChallengeId = "standard",
            ChallengeName = "Standard",
            CurrentWave = 65,
            TotalWaves = 20,
            Lives = 0,
            StartingLives = 24,
            Kills = 7_420,
            Leaks = 9,
            CreditsRemaining = 11_280,
            CreditsEarned = 188_400,
            CreditsSpent = 177_120,
            SaleCreditsRecovered = 9_840,
            EarlyCallCredits = 220,
            ProtocolActivations = 146,
            PlateDeployments = 118,
            PlateDirectPurchases = 71,
            PlateTriggers = 226,
            PlateHits = 1_804,
            PlateKills = 93,
            PlateDamage = 74_180,
            ForgedCharges = 47,
            ForgePurchases = 1,
            ForgeUpgrades = 2,
            DefenseSeconds = 4_287,
            TopTowerName = "Siege Mortar",
            TopTowerContribution = 302_400,
            GreatestLeakThreatName = "Bastion Core",
            GreatestLeakThreatLivesLost = 12,
            Towers =
            [
                new RunHistoryTowerEntry { TowerId = "siege_mortar", DisplayName = "Siege Mortar", Purchases = 5, Upgrades = 10, CreditsSpent = 8_500, Hits = 3_208, Kills = 1_442, ProtocolActivations = 18, Damage = 285_000, SupportDamageEquivalent = 17_400, Overkill = 21_300 },
                new RunHistoryTowerEntry { TowerId = "prism_beam", DisplayName = "Prism Beam", Purchases = 6, Upgrades = 12, CreditsSpent = 11_200, Hits = 4_010, Kills = 822, ProtocolActivations = 24, Damage = 198_400, ExposeDamageEquivalent = 54_300, ExposeSeconds = 880 },
                new RunHistoryTowerEntry { TowerId = "breaker_cannon", DisplayName = "Breaker Cannon", Purchases = 8, Upgrades = 14, Sales = 1, CreditsSpent = 12_600, CreditsRecovered = 1_200, Hits = 5_400, Kills = 990, ProtocolActivations = 30, Damage = 216_700, ArmorBreakDamageEquivalent = 34_100, ArmorBreakSeconds = 640, ArmorAbsorbed = 22_000 },
                new RunHistoryTowerEntry { TowerId = "frost_spire", DisplayName = "Frost Spire", Purchases = 7, Upgrades = 13, CreditsSpent = 9_800, Hits = 6_800, Kills = 420, ProtocolActivations = 20, Damage = 112_000, ControlSeconds = 1_340 },
                new RunHistoryTowerEntry { TowerId = "arc_relay", DisplayName = "Arc Relay", Purchases = 14, Upgrades = 26, Sales = 1, CreditsSpent = 11_535, Hits = 156_845, Kills = 2_187, ProtocolActivations = 83, Damage = 5_261_791, ControlSeconds = 7_365.7f },
                new RunHistoryTowerEntry { TowerId = "ember_coil", DisplayName = "Ember Coil", Purchases = 7, Upgrades = 12, Sales = 1, CreditsSpent = 3_460, Hits = 395_281, Kills = 805, ProtocolActivations = 3, Damage = 3_408_446 },
                new RunHistoryTowerEntry { TowerId = "watchtower", DisplayName = "Watchtower", Purchases = 35, Upgrades = 64, Sales = 5, CreditsSpent = 16_685, Hits = 26_955, Kills = 137, Damage = 3_329_775 },
                new RunHistoryTowerEntry { TowerId = "shard_fan", DisplayName = "Shard Fan", Purchases = 1, Upgrades = 2, CreditsSpent = 375, Hits = 17_473, Kills = 103, Damage = 259_702 },
                new RunHistoryTowerEntry { TowerId = "needle_turret", DisplayName = "Needle Turret", Purchases = 5, Upgrades = 10, Sales = 5, CreditsSpent = 1_115, Hits = 12_205, Kills = 503, ProtocolActivations = 5, Damage = 208_163 },
                new RunHistoryTowerEntry { TowerId = "signal_beacon", DisplayName = "Signal Beacon", Purchases = 9, Upgrades = 16, Sales = 2, CreditsSpent = 6_110, ProtocolActivations = 144, SupportDamageEquivalent = 10_846_800 }
            ],
            Enemies =
            [
                new RunHistoryEnemyEntry { EnemyId = "bastion_core:boss", DisplayName = "Bastion Core", Kills = 8, Escapes = 1, LivesLost = 12 },
                new RunHistoryEnemyEntry { EnemyId = "t4_aegis", DisplayName = "Aegis", Kills = 890, Escapes = 2, LivesLost = 6 }
            ],
            FinalLayout = RunHistoryLayoutSnapshot.FromSession(comparisonSession)
        };
        ui.ConfigureRunHistory([history]);
        Require(ui.HandleRunHistory(Pointer(400, 150, leftPressed: true)) == UiAction.None && !ui.IsRunHistoryDetailOpen,
            "Selecting a history record keeps the list open until an action is chosen.", assertions);
        scenes.Add(Capture("13a-run-history-selection.png", ui, GameState.RunHistory, null));
        Require(ui.HandleRunHistory(Pointer(480, 543, leftPressed: true)) == UiAction.None && ui.IsRunHistoryDetailOpen,
            "The explicit View Run action opens the complete statistics view.", assertions);
        var runDetailPixels = RenderPixels(ui, GameState.RunHistory, null);
        Require(CountColorPixels(runDetailPixels, new Rectangle(50, 232, 738, 370), ColorPalette.Muted) < 10,
            "Tower Contribution body contains no unheaded gray diagnostic lines.", assertions);
        Require(CountColorPixels(runDetailPixels, new Rectangle(818, 285, 422, 245), ColorPalette.GreenText) >= 20 &&
                CountColorPixels(runDetailPixels, new Rectangle(818, 285, 422, 245), ColorPalette.Green) < 10,
            "Run Analysis success values use the darker readable green text color.", assertions);
        scenes.Add(Capture("13-run-history-details.png", ui, GameState.RunHistory, null));
        Require(ui.HandleRunHistory(Pointer(480, 671, leftPressed: true)) == UiAction.ViewRunHistoryField,
            "Run details expose their archived defense layout.", assertions);
        var archivedLayout = history.CreateInspectionSession(content);
        archivedLayout.HandleInspectionInput(Pointer(45, 200, leftPressed: true));
        Require(archivedLayout.SelectedTower is not null && archivedLayout.Enemies.Count == 0 && archivedLayout.Projectiles.Projectiles.Count == 0,
            "The archived field is path-empty while its towers remain clickable for exact inspection.", assertions);
        scenes.Add(Capture("14-archived-final-layout.png", ui, GameState.RunHistoryField, archivedLayout));
        Require(ui.HandleRunHistoryFieldInput(Pointer(700, 28, leftPressed: true)) == UiAction.CloseRunHistoryField,
            "The archived field has an explicit return to run history.", assertions);

        var endlessSave = comparisonSession.CaptureSaveGame();
        endlessSave.Waves.CurrentWaveNumber = 65;
        endlessSave.Waves.IsFinalWaveCleared = true;
        endlessSave.Waves.EndlessModeEnabled = true;
        endlessSave.Economy.Lives = 0;
        var endlessResult = GameSession.RestoreSaveGame(content, endlessSave);
        scenes.Add(Capture("15-endless-exact-wave.png", ui, GameState.Defeat, endlessResult));

        HideAndDisableActivation();
        Require(!IsVerifierForeground(), "The visual verifier never owns foreground input focus.", assertions);
        var manifest = new
        {
            generatedAtUtc = DateTime.UtcNow,
            renderWidth = GameConstants.RenderWidth,
            renderHeight = GameConstants.RenderHeight,
            foregroundInputCaptured = false,
            changedGhostPixels,
            changedAutoMarkerPixels,
            assertions,
            scenes
        };
        File.WriteAllText(Path.Combine(_outputDirectory, "verification-report.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"UI verification passed: {scenes.Count} scenes, {assertions.Count} assertions.");
        Console.WriteLine(_outputDirectory);
        _complete = true;
        Exit();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_complete) Exit();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Review scenes are rendered explicitly in LoadContent, so the hidden
        // helper never needs to present a normal backbuffer frame.
        base.Draw(gameTime);
    }

    private static void ConfigureUi(UIManager ui, GameContent content)
    {
        ui.ConfigureMaps(content.Maps.Values, content.WaveSets, content.Enemies);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        ui.ConfigureChallenges(content.Challenges.Values);
        ui.ConfigureTowerLibrary(content.Towers.Values, content.Enemies.Values, content.Tactics);
        ui.SetSaveState(false);
    }

    private static void AssertEveryAuthoredTowerStatSheetFits(SpriteFont font, GameContent content,
        ICollection<string> assertions)
    {
        var overflows = new List<string>();
        var sheetsChecked = 0;
        foreach (var definition in content.Towers.Values)
        {
            var sheets = AuthoredTowerStatSheets(definition).ToArray();
            foreach (var (sheetName, stats) in sheets)
            {
                sheetsChecked++;
                var columns = UIManager.TowerStatGridColumns(stats.Count);
                var cellWidth = 282f / columns - 6f;
                foreach (var stat in stats)
                {
                    CheckText(stat.Label, "label");
                    CheckText(TowerInfo.ComparisonStatValueText(stat), "value");
                }

                var rows = (stats.Count + columns - 1) / columns;
                var valueOffset = stats.Count > 6 ? 10 : 12;
                var lastValueTop = 548 + Math.Max(0, rows - 1) * UIManager.TowerStatGridRowHeight(stats.Count) + valueOffset;
                var valueHeight = font.LineSpacing * UIManager.TowerStatGridValueScale(stats.Count) * GameConstants.FontDrawScale;
                if (lastValueTop + valueHeight > 622)
                    overflows.Add($"{definition.Id}/{sheetName}: vertical stat grid ends at {lastValueTop + valueHeight:0.#}");

                void CheckText(string text, string kind)
                {
                    // DrawFittedText bottoms out at this scale. Anything wider
                    // would be ellipsized, which is forbidden for stat labels
                    // and especially for complete old-to-new comparisons.
                    var minimumWidth = font.MeasureString(text).X * UIManager.TowerStatGridMinimumScale * GameConstants.FontDrawScale;
                    if (minimumWidth > cellWidth + 0.01f)
                        overflows.Add($"{definition.Id}/{sheetName} {kind} '{text}' needs {minimumWidth:0.#}/{cellWidth:0.#} px");
                }
            }
        }

        Require(overflows.Count == 0,
            $"All {sheetsChecked} authored current/upgrade stat sheets fit without clipping or ellipsis" +
            (overflows.Count == 0 ? "." : $": {string.Join("; ", overflows.Take(8))}"), assertions);
    }

    private static IEnumerable<(string Name, IReadOnlyList<TowerStatDisplay> Stats)> AuthoredTowerStatSheets(
        TowerDefinition definition)
    {
        var baseLevel = definition.Levels[0];
        yield return ("base-current", TowerInfo.ComparisonStats(definition, baseLevel));

        if (definition.Tier2Doctrines.Count > 0)
        {
            var authoredTierTwo = definition.Levels[Math.Min(1, definition.Levels.Count - 1)];
            foreach (var doctrine in definition.Tier2Doctrines)
            {
                var tierTwo = authoredTierTwo.WithDoctrine(doctrine);
                yield return ($"{doctrine.Id}-preview", TowerInfo.ComparisonStats(definition, baseLevel, tierTwo));
                yield return ($"{doctrine.Id}-current", TowerInfo.ComparisonStats(definition, tierTwo));
                foreach (var specialization in definition.Specializations)
                {
                    var final = specialization.Level.WithDoctrine(doctrine);
                    yield return ($"{doctrine.Id}-{specialization.Id}-preview", TowerInfo.ComparisonStats(definition, tierTwo, final));
                    yield return ($"{doctrine.Id}-{specialization.Id}-current", TowerInfo.ComparisonStats(definition, final));
                    yield return ($"{doctrine.Id}-{specialization.Id}-fully-boosted",
                        TowerInfo.ComparisonStats(definition, final, null,
                            new TowerBuff(0.35f, 0.22f), new MapPowerBuff(0.18f, 0.22f, 0.18f, 2f), definition.Protocol));
                }
            }
            yield break;
        }

        for (var index = 0; index < definition.Levels.Count; index++)
        {
            var current = definition.Levels[index];
            yield return ($"level-{index + 1}-current", TowerInfo.ComparisonStats(definition, current));
            if (index + 1 < definition.Levels.Count)
                yield return ($"level-{index + 2}-preview", TowerInfo.ComparisonStats(definition, current, definition.Levels[index + 1]));
        }
    }

    private static void AssertEveryAuthoredTowerIntelLabelFits(SpriteFont font, GameContent content,
        ICollection<string> assertions)
    {
        var overflows = new List<string>();
        var labelsChecked = 0;
        foreach (var definition in content.Towers.Values)
        {
            Check(definition.Id, definition.DisplayName, 80, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}", 92,
                UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, definition.DisplayName, 228, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, definition.DisplayName, 150, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, $"{definition.PurchaseCost} CREDITS   LEVEL 1   {TowerInfo.ShortRole(definition)}",
                228, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, TowerInfo.ProtocolTimingCompact(definition.Protocol), 280,
                UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, $"AUTO  {TowerInfo.ProtocolAutoTriggerCompact(definition.Protocol)}", 268,
                UIManager.TowerStatGridMinimumScale);
            foreach (var bonusRow in TowerInfo.ProtocolBonusRows(definition.Protocol))
                Check(definition.Id, bonusRow, 268, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, $"PROTOCOL AUTO  {definition.Protocol.DisplayName.ToUpperInvariant()}  |  READY",
                280, UIManager.TowerStatGridMinimumScale);
            Check(definition.Id, TowerInfo.ProtocolEffectSummary(definition.Protocol, false), 268,
                UIManager.TowerStatGridMinimumScale);

            foreach (var node in content.Maps.Values.SelectMany(map => map.PowerNodes))
            {
                Check(definition.Id, $"CURRENT STATS  |  {TowerInfo.ActiveBoostSources(default, new[] { node })}",
                    280, UIManager.TowerStatGridMinimumScale);
                Check(definition.Id,
                    $"CURRENT STATS  |  {TowerInfo.ActiveBoostSources(new TowerBuff(0.15f, 0.10f), new[] { node })}",
                    280, UIManager.TowerStatGridMinimumScale);
            }

            foreach (var doctrine in definition.Tier2Doctrines)
            {
                Check(definition.Id, $"PREVIEW {doctrine.DisplayName.ToUpperInvariant()}  {doctrine.UpgradeCost}", 280,
                    UIManager.TowerStatGridMinimumScale);
                Check(definition.Id, $"{doctrine.DisplayName.ToUpperInvariant()} {doctrine.UpgradeCost}", 180, 0.38f);
                var doctrineTower = new TowerInstance(1, definition, Vector2.Zero, 2);
                if (doctrineTower.TryChooseDoctrine(doctrine.Id))
                    Check(definition.Id, $"{TowerInfo.ProgressionLabel(doctrineTower)}   PLACED P2",
                        228, UIManager.TowerStatGridMinimumScale);

                foreach (var specialization in definition.Specializations)
                {
                    Check(definition.Id, $"PREVIEW {specialization.DisplayName.ToUpperInvariant()}  {specialization.UpgradeCost}", 280,
                        UIManager.TowerStatGridMinimumScale);
                    Check(definition.Id, $"{specialization.DisplayName.ToUpperInvariant()} {specialization.UpgradeCost}", 180, 0.38f);
                    var finalTower = new TowerInstance(1, definition, Vector2.Zero, 2);
                    if (finalTower.TryChooseDoctrine(doctrine.Id) && finalTower.TrySpecialize(specialization.Id))
                        Check(definition.Id, $"{TowerInfo.ProgressionLabel(finalTower)}   PLACED P2",
                            228, UIManager.TowerStatGridMinimumScale);
                }
            }

            var baseStats = TowerInfo.ComparisonStats(definition, definition.Levels[0]);
            var statColumns = UIManager.TowerStatGridColumns(baseStats.Count);
            var statRows = (baseStats.Count + statColumns - 1) / statColumns;
            var lastValueTop = 548 + Math.Max(0, statRows - 1) * UIManager.TowerStatGridRowHeight(baseStats.Count) +
                               (baseStats.Count > 6 ? 10 : 12);
            var protocolTop = lastValueTop + 20;
            var nodeInstructionTop = protocolTop + (2 + TowerInfo.ProtocolBonusRows(definition.Protocol).Count) * 15 + 18;
            var instructionBottom = nodeInstructionTop + font.LineSpacing * 0.42f * GameConstants.FontDrawScale;
            if (instructionBottom > 719)
                overflows.Add($"{definition.Id} node-placement Intel ends at {instructionBottom:0.#}/719 px");
        }


        var plate = content.Tactics.EmergencyDefense;
        Check(plate.Id, plate.DisplayName, 236, UIManager.TowerStatGridMinimumScale);
        Check(plate.Id, $"{plate.Charges} PULSES   DAMAGE {plate.Damage:0.#}   BLAST {plate.BlastRadius:0}", 280,
            UIManager.TowerStatGridMinimumScale);
        Check(plate.Id, $"PUSH {plate.KnockbackDistance:0}   SLOW {plate.SlowPercent:P0} / {plate.SlowDuration:0.#}s", 280, 0.55f);
        Check(plate.Id, $"Stun {plate.StunDuration:0.##}s   Armor pierce {plate.ArmorPierce:0}", 280, 0.54f);
        Check(plate.Id, $"Push: elite {plate.EliteKnockbackMultiplier:P0}   boss {plate.BossKnockbackMultiplier:P0}   grace {plate.KnockbackGraceSeconds:0.##}s",
            280, UIManager.TowerStatGridMinimumScale);
        Check(plate.Id, $"Direct {plate.PurchaseCost}   +{plate.DirectPurchaseCostIncrease} extra   resets next wave", 280,
            UIManager.TowerStatGridMinimumScale);

        var forge = content.Tactics.Generator;
        Check(forge.Id, forge.DisplayName, 236, UIManager.TowerStatGridMinimumScale);
        foreach (var level in forge.Levels)
        {
            Check(forge.Id, $"PRODUCTION  1 PLATE / {level.ProductionSeconds:0}s OF ACTIVE WAVES", 280,
                UIManager.TowerStatGridMinimumScale);
            Check(forge.Id, $"Storage {level.Capacity}/{level.Capacity}   Plate DAMAGE +{level.DefenseDamageBonus:P0}", 280,
                UIManager.TowerStatGridMinimumScale);
        }

        foreach (var node in content.Maps.Values.SelectMany(map => map.PowerNodes))
        {
            Check(node.Id, node.DisplayName, 236, UIManager.TowerStatGridMinimumScale);
            var bonus = node.AttackSpeedBonus > 0 ? $"ATTACK RATE +{node.AttackSpeedBonus:P0}" :
                node.RangeBonus > 0 ? $"TOWER RANGE +{node.RangeBonus:P0}" :
                node.DamageBonus > 0 ? $"DIRECT DAMAGE +{node.DamageBonus:P0}" :
                $"ARMOR PIERCE +{node.ArmorPierceBonus:0}";
            Check(node.Id, bonus, 280, 0.68f);
        }

        Require(overflows.Count == 0,
            $"All {labelsChecked} authored Tower Intel headers, protocol rows, and upgrade controls fit without ellipsis" +
            (overflows.Count == 0 ? "." : $": {string.Join("; ", overflows.Take(8))}"), assertions);
        return;

        void Check(string owner, string value, float maximumWidth, float minimumScale)
        {
            labelsChecked++;
            var width = font.MeasureString(value).X * minimumScale * GameConstants.FontDrawScale;
            if (width > maximumWidth + 0.01f)
                overflows.Add($"{owner} '{value}' needs {width:0.#}/{maximumWidth:0.#} px");
        }
    }

    private VisualVerificationScene Capture(string fileName, UIManager ui, GameState state, GameSession? session)
    {
        var path = Path.Combine(_outputDirectory, fileName);
        Color[] pixels;
        using (var target = RenderScene(ui, state, session))
        {
            pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
            target.GetData(pixels);
            using var stream = File.Create(path);
            target.SaveAsPng(stream, GameConstants.RenderWidth, GameConstants.RenderHeight);
        }
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var nonPaperPixels = pixels.Count(pixel => pixel != ColorPalette.Paper);
        if (nonPaperPixels < 2_000)
            throw new InvalidOperationException($"Visual scene '{fileName}' rendered unexpectedly blank.");
        return new VisualVerificationScene(fileName, GameConstants.RenderWidth, GameConstants.RenderHeight, hash, nonPaperPixels);
    }

    private Color[] RenderPixels(UIManager ui, GameState state, GameSession? session)
    {
        using var target = RenderScene(ui, state, session);
        var pixels = new Color[GameConstants.RenderWidth * GameConstants.RenderHeight];
        target.GetData(pixels);
        return pixels;
    }

    private RenderTarget2D RenderScene(UIManager ui, GameState state, GameSession? session)
    {
        HideAndDisableActivation();
        var target = new RenderTarget2D(GraphicsDevice, GameConstants.RenderWidth, GameConstants.RenderHeight,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(GameConstants.RenderScale));
        if (session is not null) _renderer.Draw(_batch, _primitives, session,
            foregroundTowerId: state == GameState.Playing ? ui.RemoteCoOpSelectedTowerId : 0);
        ui.Draw(_batch, _primitives, state, session);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return target;
    }

    private static int CountChangedPixels(IReadOnlyList<Color> baseline, IReadOnlyList<Color> changed, Rectangle logicalRegion)
    {
        var count = 0;
        var left = Math.Max(0, logicalRegion.Left * GameConstants.RenderScale);
        var top = Math.Max(0, logicalRegion.Top * GameConstants.RenderScale);
        var right = Math.Min(GameConstants.RenderWidth, logicalRegion.Right * GameConstants.RenderScale);
        var bottom = Math.Min(GameConstants.RenderHeight, logicalRegion.Bottom * GameConstants.RenderScale);
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var index = y * GameConstants.RenderWidth + x;
            if (baseline[index] != changed[index]) count++;
        }
        return count;
    }

    private static int CountColorPixels(IReadOnlyList<Color> pixels, Rectangle logicalRegion, Color color)
    {
        var count = 0;
        var left = Math.Max(0, logicalRegion.Left * GameConstants.RenderScale);
        var top = Math.Max(0, logicalRegion.Top * GameConstants.RenderScale);
        var right = Math.Min(GameConstants.RenderWidth, logicalRegion.Right * GameConstants.RenderScale);
        var bottom = Math.Min(GameConstants.RenderHeight, logicalRegion.Bottom * GameConstants.RenderScale);
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var pixel = pixels[y * GameConstants.RenderWidth + x];
            if (pixel.R == color.R && pixel.G == color.G && pixel.B == color.B) count++;
        }
        return count;
    }

    private static InputSnapshot Pointer(float x, float y, bool leftPressed = false) =>
        default(InputSnapshot) with
        {
            MousePosition = new Vector2(x, y),
            LeftPressed = leftPressed,
            IsMouseOverLogicalCanvas = true,
            TextEntered = ""
        };

    private static void Require(bool condition, string description, ICollection<string> assertions)
    {
        if (!condition) throw new InvalidOperationException(description);
        assertions.Add(description);
    }

    private void HideAndDisableActivation()
    {
        if (!OperatingSystem.IsWindows() || Window.Handle == IntPtr.Zero) return;
        var style = GetWindowLongPtr(Window.Handle, GwlExStyle);
        SetWindowLongPtr(Window.Handle, GwlExStyle,
            new IntPtr(style.ToInt64() | WsExNoActivate | WsExToolWindow));
        SetWindowPos(Window.Handle, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpHideWindow);
    }

    private bool IsVerifierForeground() =>
        OperatingSystem.IsWindows() && Window.Handle != IntPtr.Zero && GetForegroundWindow() == Window.Handle;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _primitives?.Dispose();
            _batch?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed record VisualVerificationScene(string File, int Width, int Height, string Sha256, int NonPaperPixels);

    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpHideWindow = 0x0080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
