using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MaximalBastion.Combat;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Effects;
using MaximalBastion.Maps;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Rendering;
using MaximalBastion.Simulation;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

/// <summary>
/// Renders deterministic review scenes with the shipped MonoGame UI while its
/// helper window remains hidden and ineligible for input focus.
/// </summary>
public sealed partial class VisualVerificationGame : Game
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
        Window.Title = "Maximal Bastion Visual Verification (Hidden)";
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
        var typography = UiTypography.Load(Content);
        var font = typography.Interface;
        var ui = new UIManager(font, typography.Display);
        ConfigureUi(ui, content);

        var assertions = new List<string>();
        var displayFactor = GameConstants.FontDrawScale * UiTypography.DisplayRasterScale;
        Require(typography.Display.MeasureString("BASTION").X * displayFactor * 2.65f < 400 &&
                typography.Display.MeasureString("MAXIMAL").X * displayFactor * .8f + 54 < 200,
            "The display wordmark fits its central title lane.", assertions);
        Require(content.Towers.Values.All(tower =>
                typography.Display.MeasureString(tower.DisplayName).X * displayFactor * .5f <= 164),
            "Every authored tower name fits the narrow Apex heading at a readable display size.", assertions);
        Require(typography.Interface.Characters.Contains('•') && typography.Display.Characters.Contains('•') &&
                typography.Interface.Characters.Contains('°') && typography.Display.Characters.Contains('°'),
            "Both bundled typefaces preserve the interface's bullet and degree glyphs.", assertions);
        AssertEveryAuthoredTowerStatSheetFits(font, content, assertions);
        AssertEveryAuthoredTowerIntelLabelFits(font, content, assertions);
        var mainMenuAtRest = RenderPixels(ui, GameState.MainMenu, null);
        var scenes = new List<VisualVerificationScene>
        {
            Capture("00-main-menu-battles.png", ui, GameState.MainMenu, null)
        };
        AssertPathMarkingsCrossCorners(assertions, scenes);
        AssertSubpixelPathCaps(assertions, scenes);
        AssertMortarShellPresentation(content, font, typography.Display, assertions, scenes);
        AssertProjectileIdentity(content, font, assertions, scenes);
        AssertPrismBeamPresentation(content, font, typography.Display, assertions, scenes);
        scenes.Add(CaptureTowerMountGallery(content, font));
        scenes.Add(CaptureTowerUpgradeGallery(content, font));
        scenes.Add(CaptureTowerRotationGallery(content, font));
        AssertTowerProtocolPresentation(content, font, assertions, scenes);
        scenes.Add(CaptureHealthBarGallery(font));
        AssertCombatFeedClipping(content, assertions, scenes);
        var smoothMenu = new UIManager(font, typography.Display);
        ConfigureUi(smoothMenu, content);
        var beforeFraction = RenderPixels(smoothMenu, GameState.MainMenu, null);
        smoothMenu.AdvanceMainMenuBattle(1f / 120f);
        var afterFraction = RenderPixels(smoothMenu, GameState.MainMenu, null);
        Require(CountChangedPixels(beforeFraction, afterFraction, UIManager.MainMenuLeftDefenseBounds) > 20 &&
                CountChangedPixels(beforeFraction, afterFraction, UIManager.MainMenuRightDefenseBounds) > 20,
            "Both menu lanes advance visually during a fraction of a fixed simulation tick.", assertions);
        ui.AdvanceVisualTime(0.45f);
        ui.AdvanceMainMenuBattle(0.45f);
        var mainMenuInMotion = RenderPixels(ui, GameState.MainMenu, null);
        Require(CountChangedPixels(mainMenuAtRest, mainMenuInMotion, UIManager.MainMenuLeftDefenseBounds) >= 300 &&
                CountChangedPixels(mainMenuAtRest, mainMenuInMotion, UIManager.MainMenuRightDefenseBounds) >= 300,
            "Both main-menu defense lanes contain visible animated combat.", assertions);
        Require(CountChangedPixels(mainMenuAtRest, mainMenuInMotion, new Rectangle(470, 365, 340, 339)) == 0,
            "Terminal atmosphere and combat motion remain outside the action labels.", assertions);
        Require(CountChangedPixels(mainMenuAtRest, mainMenuInMotion, new Rectangle(420, 45, 440, 230)) > 30,
            "The title crest and relay trace carry subtle ambient animation.", assertions);
        Require(CountChangedPixels(mainMenuAtRest, mainMenuInMotion, new Rectangle(470, 325, 340, 335)) == 0,
            "Ambient motion stays outside the main action list.", assertions);
        var expectedMenuActions = new List<UiAction> { UiAction.OpenSoloSetup };
        if (PlatformCapabilities.OnlineCoOp) expectedMenuActions.Add(UiAction.CoOp);
        expectedMenuActions.AddRange([UiAction.LoadGame, UiAction.RunHistory, UiAction.Settings]);
        if (PlatformCapabilities.ExitCommand) expectedMenuActions.Add(UiAction.Exit);
        Require(ui.MainMenuActions().Select(item => item.Action).SequenceEqual(expectedMenuActions),
            "Home presents run actions, history, settings, and the supported exit control.", assertions);
        var mainActionBounds = ui.MainMenuActions().Select(item => ui.MainMenuActionBounds(item.Action)).ToArray();
        Require(mainActionBounds.All(bounds => bounds.Center.X == GameConstants.LogicalWidth / 2) &&
                mainActionBounds.Zip(mainActionBounds.Skip(1)).All(pair => pair.First.Bottom < pair.Second.Top),
            "Main-menu controls form a centered, separated action column.", assertions);
        Require(mainActionBounds.Zip(mainActionBounds.Skip(1))
                .Select(pair => pair.Second.Top - pair.First.Bottom).Distinct().Count() == 1,
            "Every adjacent main-menu action uses the same gap.", assertions);
        var playBounds = ui.MainMenuActionBounds(UiAction.OpenSoloSetup);
        Require(mainActionBounds.Skip(1).All(bounds => bounds.Height < playBounds.Height),
            "Play has greater height than the secondary main-menu actions.", assertions);
        Require(CountColorPixels(mainMenuAtRest, playBounds, ColorPalette.Cyan) == 0,
            "Idle Play has no cyan selection treatment.", assertions);
        var playCorners = new[]
        {
            new Rectangle(playBounds.Left, playBounds.Top, 10, 2),
            new Rectangle(playBounds.Right - 10, playBounds.Top, 10, 2),
            new Rectangle(playBounds.Left, playBounds.Bottom - 2, 10, 2),
            new Rectangle(playBounds.Right - 10, playBounds.Bottom - 2, 10, 2)
        };
        Require(playCorners.All(corner =>
                Enumerable.Range(corner.Top * GameConstants.RenderScale, corner.Height * GameConstants.RenderScale)
                    .All(y => Enumerable.Range(corner.Left * GameConstants.RenderScale, corner.Width * GameConstants.RenderScale)
                        .All(x =>
                        {
                            var pixel = mainMenuAtRest[y * GameConstants.RenderWidth + x];
                            return Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) < 160;
                        }))),
            "Idle Play uses subdued corners without bright selection brackets.", assertions);
        var play = playBounds.Center;
        Require(ui.HandleMainMenu(Pointer(play.X, play.Y, leftPressed: true)) == UiAction.OpenSoloSetup,
            "Play opens unified run setup.", assertions);
        Require(ui.HandleMainMenu(Pointer(418, 250, leftPressed: true)) == UiAction.None,
            "Home artwork has no competing map-selection target.", assertions);
        var districtIds = new[] { "foundry_loop", "crosswind_basin", "prism_circuit", "relay_divide" };
        ui.PrepareGameSetup(false);
        for (var index = 0; index < districtIds.Length; index++)
        {
            var card = UIManager.SetupCardRectangle(0, index, districtIds.Length);
            Require(ui.HandleGameSetup(Pointer(card.Center.X, card.Center.Y, leftPressed: true)) == UiAction.None &&
                    ui.SelectedMapId == districtIds[index],
                $"Setup selects {districtIds[index]} without starting a run.", assertions);
            scenes.Add(Capture($"00b-setup-map-{index}.png", ui, GameState.GameSetup, null));
        }
        for (var mode = 0; mode < content.Challenges.Count; mode++)
        {
            var at = UIManager.SetupCardRectangle(2, mode, content.Challenges.Count).Center;
            ui.HandleGameSetup(Pointer(at.X, at.Y, leftPressed: true));
            Require(ui.HoveredModeDescription.Length > 0, "Every mode has a hover description.", assertions);
            ui.HandleGameSetup(Pointer(0, 0));
            Require(ui.HoveredModeDescription.Length == 0, "Selected modes do not leave a lingering description.", assertions);
        }
        var standardMode = UIManager.SetupCardRectangle(2, 0, content.Challenges.Count).Center;
        ui.HandleGameSetup(Pointer(standardMode.X, standardMode.Y, leftPressed: true));
        scenes.Add(Capture("00e-mode-hover.png", ui, GameState.GameSetup, null));
        ui.HandleGameSetup(Pointer(0, 0));
        scenes.Add(Capture("00f-mode-unhovered.png", ui, GameState.GameSetup, null));
        var difficultyHover = UIManager.SetupCardRectangle(1, 1, content.Difficulties.Count).Center;
        ui.HandleGameSetup(Pointer(difficultyHover.X, difficultyHover.Y));
        scenes.Add(Capture("00g-difficulty-hover.png", ui, GameState.GameSetup, null));
        ui.HandleGameSetup(Pointer(0, 0));
        var setupAtRest = RenderPixels(ui, GameState.GameSetup, null);
        foreach (var (row, count, label) in new[]
                 {
                     (1, content.Difficulties.Count, "difficulty"),
                     (2, content.Challenges.Count, "mode")
                 })
        {
            var selectedRails = Enumerable.Range(0, count).Select(index =>
            {
                var bounds = UIManager.SetupCardRectangle(row, index, count);
                return CountColorPixels(setupAtRest,
                    new Rectangle(bounds.Left + 6, bounds.Bottom - 3, bounds.Width - 12, 2), ColorPalette.Cyan);
            }).ToArray();
            Require(selectedRails.Count(coverage => coverage > 0) == 1 && selectedRails.Max() >= 100,
                $"Setup keeps exactly one clear cyan {label} selection rail after the pointer leaves.", assertions);
        }
        var firstMap = UIManager.SetupCardRectangle(0, 0, districtIds.Length).Center;
        ui.HandleGameSetup(Pointer(firstMap.X, firstMap.Y, leftPressed: true));
        foreach (var action in ui.MainMenuActions().Where(item => item.Action != UiAction.LoadGame))
        {
            var at = ui.MainMenuActionBounds(action.Action).Center;
            Require(ui.HandleMainMenu(Pointer(at.X, at.Y, leftPressed: true)) == action.Action,
                $"Main-menu {action.Label} activates its visible action.", assertions);
        }
        ui.HandleMainMenu(Pointer(0, 0));
        ui.ConfigureSettings(new UserSettings { ReducedEffects = true });
        var terminalStill = RenderPixels(ui, GameState.MainMenu, null);
        ui.AdvanceVisualTime(.25f);
        var terminalStillLater = RenderPixels(ui, GameState.MainMenu, null);
        Require(CountChangedPixels(terminalStill, terminalStillLater, new Rectangle(0, 0, 1280, 720)) == 0,
            "Reduced Effects freezes terminal ambient motion while the combat simulation is held fixed.", assertions);
        scenes.Add(Capture("00c-terminal-reduced-effects.png", ui, GameState.MainMenu, null));
        ui.ConfigureSettings(new UserSettings());
        Require(ui.HandleMainMenu(Pointer(ui.MainMenuActionBounds(UiAction.LoadGame).Center.X, ui.MainMenuActionBounds(UiAction.LoadGame).Center.Y, leftPressed: true)) == UiAction.None,
            "Load Saves remains unavailable when there are no saves.", assertions);
        ui.SetSaveState(true);
        Require(ui.HandleMainMenu(Pointer(ui.MainMenuActionBounds(UiAction.LoadGame).Center.X, ui.MainMenuActionBounds(UiAction.LoadGame).Center.Y, leftPressed: true)) == UiAction.LoadGame,
            "Load Saves remains available when saved games exist.", assertions);
        ui.HandleMainMenu(Pointer(play.X, play.Y));
        scenes.Add(Capture("00d-terminal-primary-hover.png", ui, GameState.MainMenu, null));
        ui.SetSaveState(false);
        ui.HandleMainMenu(Pointer(0, 0));
        Require(ui.MainMenuBattleTowerCounts.Count == 2 &&
                ui.MainMenuBattleTowerCounts.All(count => count is >= 3 and <= 5),
            "Each main-menu lane independently occupies three to five tower positions.", assertions);
        Require(ui.MainMenuBattleTowerLevels.Count == ui.MainMenuBattleTowerCounts.Sum() &&
                ui.MainMenuBattleTowerLevels.All(level => level is >= 1 and <= 3),
            "Main-menu battle towers independently use valid randomized levels.", assertions);
        Require(ui.MainMenuBattleTowerKinds.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 3,
            "Main-menu battles draw a varied random tower roster.", assertions);
        Require(ui.MainMenuBattleTowerKinds.All(towerId =>
                !towerId.Equals("signal_beacon", StringComparison.OrdinalIgnoreCase)),
            "Main-menu battles exclude the non-attacking Signal Beacon.", assertions);
        Require(ui.MainMenuBattleEnemyCounts.Count == 2 &&
                ui.MainMenuBattleEnemyCounts.All(count => count is >= 3 and <= 5),
            "Each main-menu lane fields a randomized group of three to five enemies.", assertions);
        var menuFrame = RenderPixels(ui, GameState.MainMenu, null);
        for (var index = 0; index < 96; index++)
        {
            ui.AdvanceMainMenuBattle(0.25f);
            if (index % 12 != 11) continue;
            var animatedMenu = RenderPixels(ui, GameState.MainMenu, null);
            Require(CountChangedPixels(menuFrame, animatedMenu, new Rectangle(20, 668, 280, 52)) == 0 &&
                    CountChangedPixels(menuFrame, animatedMenu, new Rectangle(980, 668, 280, 52)) == 0,
                "Main-menu combat stays inside both feeds throughout enemy arrivals and escapes.", assertions);
        }
        scenes.Add(Capture("00g-main-menu-feed-boundaries.png", ui, GameState.MainMenu, null));
        Require(ui.MainMenuBattleKills > 0,
            "Main-menu battles resolve real enemy deaths through normal combat logic.", assertions);
        Require(ui.MainMenuBattleEscapes > 0,
            "Main-menu battles allow surviving enemies to reach the end of their paths.", assertions);
        Require(ui.HandleMainMenu(Pointer(
                    ui.MainMenuReleaseNotesBounds.Center.X,
                    ui.MainMenuReleaseNotesBounds.Center.Y,
                    leftPressed: true)) == UiAction.ReleaseNotes,
            "The title-screen version opens the current release notes.", assertions);
        scenes.Add(Capture("00a-release-notes.png", ui, GameState.ReleaseNotes, null));
        Require(ui.HandleReleaseNotes(Pointer(0, 0) with { EscapePressed = true }) == UiAction.MainMenu,
            "Release notes return to the title screen with Escape.", assertions);
        Require(ui.HandleMainMenu(Pointer(ui.MainMenuActionBounds(UiAction.CoOp).Center.X, ui.MainMenuActionBounds(UiAction.CoOp).Center.Y, leftPressed: true)) == UiAction.CoOp,
            "Online Co-op opens the connection screen without visiting setup.", assertions);

        scenes.Add(Capture("01-online-coop-connect.png", ui, GameState.CoOpMenu, null));

        Require(ui.HandleCoOpMenu(Pointer(640, 239, leftPressed: true)) == UiAction.OpenCoOpSetup,
            "Only the Host command opens online defense setup.", assertions);
        ui.PrepareGameSetup(true);
        Require(ui.HandleGameSetup(Pointer(0, 0) with { EscapePressed = true }) == UiAction.CoOp,
            "Host setup returns to the connection screen.", assertions);
        Require(ui.HandleGameSetup(Pointer(560, 609, leftPressed: true)) == UiAction.HostCoOp,
            "Host setup confirmation starts hosting.", assertions);
        scenes.Add(Capture("02-online-host-setup.png", ui, GameState.GameSetup, null));
        ui.BeginLoadingTransition("LOADING DEFENSE SYSTEMS", "RENDERING DEFENSE GRID");
        scenes.Add(Capture("02a-defense-loading.png", ui, GameState.LoadingTransition, null));

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
        var nodeTerrainPixels = RenderPixels(ui, GameState.Playing, localNodePlacementSession);
        scenes.Add(Capture("03a0-power-node-glyphs.png", ui, GameState.Playing, localNodePlacementSession));
        var amplifierNodePosition = localNodePlacementSession.Map.Definition.PowerNodes
            .Single(node => node.Id == "inner_amplifier").Position.ToVector2();
        var amplifierCoreCenter = ColorPixelCenter(nodeTerrainPixels,
            new Rectangle((int)amplifierNodePosition.X - 6, (int)amplifierNodePosition.Y - 6, 12, 12),
            ColorPalette.Paper);
        Require(amplifierCoreCenter is { } coreCenter &&
                Vector2.Distance(coreCenter, amplifierNodePosition) <= 0.05f,
            "Power-node hardware shares the authored node center.", assertions);
        localNodePlacementSession.BeginPlacement("needle_turret");
        localNodePlacementSession.HandleWorldInput(Pointer(285, 330));
        Require(localNodePlacementSession.HasPlacementPreview &&
                localNodePlacementSession.Map.GetPowerNodes(localNodePlacementSession.PlacementPreviewPosition).Count == 1,
            "Local node-placement scene resolves the Amplifier Node.", assertions);
        var localNodePlacementPixels = RenderPixels(ui, GameState.Playing, localNodePlacementSession);
        scenes.Add(Capture("03a1-local-node-placement-marker.png", ui, GameState.Playing, localNodePlacementSession));
        var amplifierColor = localNodePlacementSession.Map.GetPowerNodes(localNodePlacementSession.PlacementPreviewPosition)[0].NodeColor;
        var expectedNodeMarkerCenter = new Vector2(
            MathF.Round(localNodePlacementSession.PlacementPreviewPosition.X) + 0.5f,
            MathF.Round(localNodePlacementSession.PlacementPreviewPosition.Y) + 0.5f);
        Require(CountColorPixels(localNodePlacementPixels,
                    new Rectangle((int)expectedNodeMarkerCenter.X - 6, (int)expectedNodeMarkerCenter.Y - 6, 13, 13),
                    amplifierColor) >= 20,
            "Local tower placement carries a centered node-colored marker over the ghost.", assertions);
        var renderedNodeMarkerCenter = ColorPixelCenter(localNodePlacementPixels,
            new Rectangle((int)expectedNodeMarkerCenter.X - 4, (int)expectedNodeMarkerCenter.Y - 4, 9, 9),
            ColorPalette.Paper);
        Require(renderedNodeMarkerCenter is { } center &&
                Vector2.Distance(center, expectedNodeMarkerCenter) <= 0.05f,
            "The node-overlap marker's white dot is centered inside its colored square.", assertions);

        var breachNodePlacementSession = new GameSession(content, "relay_divide", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        breachNodePlacementSession.BeginPlacement("needle_turret");
        breachNodePlacementSession.HandleWorldInput(Pointer(290, 425));
        var breachNode = breachNodePlacementSession.Map.GetPowerNodes(
            breachNodePlacementSession.PlacementPreviewPosition).Single();
        var breachTextColor = ColorPalette.BalancedAccentText(breachNode.NodeColor, ColorPalette.PanelAlt);
        Require(ColorPalette.ContrastRatio(breachTextColor, ColorPalette.PanelAlt) >= 2.99f,
            "Gold Surge Node Intel uses readable accent text on the light panel.", assertions);
        var breachNodePlacementPixels = RenderPixels(ui, GameState.Playing, breachNodePlacementSession);
        scenes.Add(Capture("03a1b-gold-node-placement-intel.png", ui, GameState.Playing,
            breachNodePlacementSession));
        Require(CountColorPixels(breachNodePlacementPixels, new Rectangle(980, 692, 280, 24),
                    breachTextColor) >= 8,
            "Gold node placement renders its adjusted Intel accent.", assertions);

        var remoteNodePlacementSession = new GameSession(content, "relay_divide", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        remoteNodePlacementSession.ConfigureCoOp(1);
        ui.SetRemoteCoOpCursor(new Vector2(250, 300), 2, placementTowerId: "needle_turret",
            hasPlacementPreview: true, placementPreviewPosition: new Vector2(285, 330));
        var remoteNodePlacementPixels = RenderPixels(ui, GameState.Playing, remoteNodePlacementSession);
        scenes.Add(Capture("03a2-remote-node-placement-marker.png", ui, GameState.Playing, remoteNodePlacementSession));
        Require(CountColorPixels(remoteNodePlacementPixels,
                    new Rectangle((int)expectedNodeMarkerCenter.X - 6, (int)expectedNodeMarkerCenter.Y - 6, 13, 13),
                    amplifierColor) >= 20,
            "Remote co-op tower placement shows the same centered node marker at its synchronized snapped position.", assertions);
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
        Require(ui.TargetModeButtonBounds.Count == comparisonSession.AvailableTargetModes.Count &&
                !ui.TargetModeButtonBounds.ContainsKey(TargetMode.Support) &&
                ui.TargetPickerBounds.Left >= GameConstants.MapWidth &&
                ui.TargetPickerBounds.Right <= GameConstants.LogicalWidth &&
                ui.TargetPickerBounds.Bottom <= ui.TargetButtonBounds.Top &&
                !ui.TargetPickerBounds.Intersects(ui.UpgradeButtonBounds) &&
                !ui.TargetPickerBounds.Intersects(ui.SellButtonBounds),
            "The standard target picker omits Support and drops upward without covering management buttons.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { TowerHotkey = 7 }, comparisonSession);
        Require(!ui.IsTargetPickerOpen && comparisonSession.SelectedTower.TargetMode == TargetMode.Armored,
            "A target-picker number hotkey applies the matching mode and closes the picker.", assertions);

        var gauntletTargetSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId,
            ChallengeCatalog.SignalGauntletId);
        Require(gauntletTargetSession.TryPlaceTower("needle_turret", new Vector2(45, 200)),
            "Signal Gauntlet target-picker scene places a selected tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, gauntletTargetSession);
        targetButtonCenter = ui.TargetButtonBounds.Center;
        _ = ui.HandleGameplayInput(Pointer(targetButtonCenter.X, targetButtonCenter.Y, true), gauntletTargetSession);
        _ = RenderPixels(ui, GameState.Playing, gauntletTargetSession);
        Require(ui.TargetModeButtonBounds.ContainsKey(TargetMode.Support) &&
                ui.TargetModeButtonBounds.Count == gauntletTargetSession.AvailableTargetModes.Count,
            "Signal Gauntlet adds Support to the target picker.", assertions);
        var supportTargetCenter = ui.TargetModeButtonBounds[TargetMode.Support].Center;
        _ = ui.HandleGameplayInput(Pointer(supportTargetCenter.X, supportTargetCenter.Y, true),
            gauntletTargetSession);
        Require(!ui.IsTargetPickerOpen && gauntletTargetSession.SelectedTower!.TargetMode == TargetMode.Support,
            "Signal Gauntlet applies Support as an explicit target choice.", assertions);

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
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { TowerHotkey = 6 }, coOpTargetSession,
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

        AssertPersistentProtocolControls(content, font, typography.Display, assertions, scenes);
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

        var gauntletSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, "close_quarters");
        Require(gauntletSession.TryPlaceTower("needle_turret", new Vector2(45, 200)),
            "Signal Gauntlet visual scene places a tower beside the opening route.", assertions);
        gauntletSession.SpawnEnemy("t5_regenerator", 1, 1, "Elite", EnemySignalRole.Disruptor);
        var gauntletThreat = gauntletSession.Enemies.Single();
        gauntletThreat.SetSandboxPathDistance(130f, gauntletSession.Map.Path);
        gauntletThreat.ArmSignalAbility(0);
        gauntletSession.TryActivateEnemySignal(gauntletThreat);
        Require(gauntletSession.Towers[0].IsDisrupted,
            "An elite Regenerator creates a visible precision tower disruption.", assertions);
        Require(gauntletSession.Effects.Effects.Any(effect => effect.Kind == EffectKind.Beam) &&
                gauntletSession.Effects.Effects.All(effect => effect.Kind != EffectKind.Splash && effect.BeamStyle == BeamStyle.Standard),
            "The Disruptor uses its standard attack beam without Prism highlights or radial pulse art.", assertions);
        var gauntletPixels = RenderPixels(ui, GameState.Playing, gauntletSession);
        Require(CountColorPixels(gauntletPixels, new Rectangle(15, 165, 60, 70), ColorPalette.Violet) >= 12,
            "Disrupted tower art carries the compact violet interruption mark.", assertions);
        scenes.Add(Capture("05c-signal-gauntlet-disruption.png", ui, GameState.Playing, gauntletSession));

        var jammerVisualSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, "close_quarters");
        foreach (var x in new[] { 190f, 230f, 270f, 310f })
            Require(jammerVisualSession.TryPlaceTower("needle_turret", new Vector2(x, 335)),
                "Signal Gauntlet visual scene places a tower inside the Jammer field.", assertions);
        jammerVisualSession.SpawnEnemy("t1_crawler", 1, 1, signalRole: EnemySignalRole.Jammer);
        var jammerVisual = jammerVisualSession.Enemies.Single();
        jammerVisual.UpdateMovement(5.7f, jammerVisualSession.Map.Path);
        jammerVisual.ArmSignalAbility(0);
        jammerVisualSession.TryActivateEnemySignal(jammerVisual);
        Require(jammerVisualSession.Effects.Effects.Any(effect => effect.Kind == EffectKind.Splash) &&
                jammerVisualSession.Effects.Effects.All(effect => effect.Kind != EffectKind.Beam),
            "The Jammer uses radial pulse art without direct attack beams.", assertions);
        scenes.Add(Capture("05c1-signal-gauntlet-jammer-pulse.png", ui, GameState.Playing, jammerVisualSession));

        var supportSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, "close_quarters");
        supportSession.SpawnEnemy("t1_crawler", 1, 1, signalRole: EnemySignalRole.Accelerator);
        supportSession.SpawnEnemy("t1_crawler", 1, 1);
        supportSession.SpawnEnemy("t4_aegis", 1, 1);
        supportSession.Enemies[0].SetSandboxPathDistance(430f, supportSession.Map.Path);
        supportSession.Enemies[1].SetSandboxPathDistance(430f, supportSession.Map.Path);
        supportSession.Enemies[2].SetSandboxPathDistance(335f, supportSession.Map.Path);
        supportSession.RefreshEnemySignalFormation();
        Require(supportSession.Enemies.Skip(1).All(enemy => enemy.FormationSpeedMultiplier > 1f),
            "An Accelerator visibly supports each nearby formation member.", assertions);
        var supportPixels = RenderPixels(ui, GameState.Playing, supportSession);
        var supportPosition = supportSession.Enemies[0].Position.ToPoint();
        Require(CountColorPixels(supportPixels,
                    new Rectangle(supportPosition.X - 9, supportPosition.Y - 9, 18, 18), ColorPalette.Cyan) >= 8,
            "A signal carrier and its embedded role glyph remain visible above an overlapping ordinary threat.", assertions);
        var shieldedThreat = supportSession.Enemies[2];
        var shieldBarPosition = shieldedThreat.Position - new Vector2(0, shieldedThreat.Radius + 18);
        Require(CountColorPixels(supportPixels,
                    new Rectangle((int)(shieldBarPosition.X - shieldedThreat.Radius * 1.25f),
                        (int)shieldBarPosition.Y, (int)(shieldedThreat.Radius * 2.5f), 6), ColorPalette.Shield) >= 4,
            "A shielded threat renders a quantitative cyan bar above its separate health bar.", assertions);
        scenes.Add(Capture("05d-signal-support-network.png", ui, GameState.Playing, supportSession));

        var crosswindSession = new GameSession(content, "crosswind_basin", DifficultyCatalog.DefaultId,
            ChallengeCatalog.DefaultId);
        Require(crosswindSession.TryPlaceTower("shard_fan", new Vector2(730, 350)) &&
                crosswindSession.TryPlaceTower("frost_spire", new Vector2(490, 330)) &&
                crosswindSession.TryPlaceTower("needle_turret", new Vector2(250, 320)),
            "Crosswind visual scene places the opening roster in its crossfire islands.", assertions);
        Require(Vector2.Distance(crosswindSession.SelectedTower!.Position, new Vector2(130, 320)) <=
                    crosswindSession.SelectedTower.Level.Range &&
                Vector2.Distance(crosswindSession.SelectedTower.Position, new Vector2(370, 320)) <=
                    crosswindSession.SelectedTower.Level.Range,
            "The selected opening Needle visibly covers both adjacent Crosswind lanes.", assertions);
        scenes.Add(Capture("05e-crosswind-crossfire-geometry.png", ui, GameState.Playing, crosswindSession));

        var crosswindWinningSample = CaptureSimulationLayout(content, ui,
            "05f-crosswind-easy-standard-win.png", "crosswind_basin", "easy", "standard",
            AutoPlayerStrategy.Control, 128041, true, assertions);
        var crosswindLosingSample = CaptureSimulationLayout(content, ui,
            "05g-crosswind-hard-standard-loss.png", "crosswind_basin", "hard", "standard",
            AutoPlayerStrategy.Experienced, 56770, false, assertions);
        scenes.Add(crosswindWinningSample.Scene);
        scenes.Add(crosswindLosingSample.Scene);

        var surgeSurvivalSample = CaptureSimulationLayout(content, ui,
            "05h-surge-easy-standard-wave26.png", "relay_divide", "easy", "standard",
            AutoPlayerStrategy.Experienced, 668436, true, assertions, targetWave: 26);
        var surgeLosingSample = CaptureSimulationLayout(content, ui,
            "05i-surge-hard-entrenched-loss.png", "relay_divide", "hard", "no_reserves",
            AutoPlayerStrategy.Experienced, 1337, false, assertions);
        scenes.Add(surgeSurvivalSample.Scene);
        scenes.Add(surgeLosingSample.Scene);
        Require(surgeSurvivalSample.OpeningNodeTowers >= 6 && surgeLosingSample.OpeningNodeTowers >= 5,
            "Representative Surge bots prioritize authored Surge Nodes during their opening builds.", assertions);
        Require(surgeSurvivalSample.OccupiedNodes >= 8,
            "The representative late-wave Surge bot expands through nearly the full node network.", assertions);

        Require(surgeSurvivalSample.OpeningNodeTowers >= 8 && surgeSurvivalSample.OccupiedNodes >= 8,
            "The Experienced model densely packs useful Surge Nodes before expanding off-node.", assertions);

        var signalLineColor = content.Towers["signal_beacon"].Visual.AccentColor;
        var breakerLineColor = content.Towers["breaker_cannon"].Visual.AccentColor;
        Require(signalLineColor != breakerLineColor &&
                ColorPalette.ContrastRatio(signalLineColor, ColorPalette.PanelAlt) >= 3f,
            "Signal Beacon uses its readable outer-ring color, distinct from Breaker Cannon.", assertions);
        Require(ColorPalette.ContrastRatio(
            ColorPalette.BalancedAccentText(ColorPalette.Gold, ColorPalette.PanelAlt),
            ColorPalette.PanelAlt) >= 2.55f,
            "Small gold accent text balances readability with palette brightness.", assertions);
        Require(autoHeaderSession.StartNextWave(), "Live co-op header scene starts an active wave.", assertions);
        scenes.Add(Capture("07-active-coop-header.png", ui, GameState.Playing, autoHeaderSession));
        Require(autoHeaderSession.SetCoOpPaused(true, 1),
            "Shared-pause visual scene enters authoritative pause.", assertions);
        var coOpPauseBounds = new[]
        {
            UIManager.CoOpPauseResumeBounds, UIManager.CoOpPauseRestartBounds, UIManager.CoOpPauseMenuBounds
        };
        Require(coOpPauseBounds.All(bounds => bounds.Left >= GameConstants.SidebarX &&
                    bounds.Right <= GameConstants.LogicalWidth) &&
                coOpPauseBounds.Select(bounds => (bounds.Left, bounds.Width)).Distinct().Count() == 1 &&
                coOpPauseBounds.Zip(coOpPauseBounds.Skip(1)).All(pair => pair.Second.Top > pair.First.Bottom) &&
                coOpPauseBounds.Zip(coOpPauseBounds.Skip(1))
                    .Select(pair => pair.Second.Top - pair.First.Bottom).Distinct().Count() == 1,
            "Co-op pause controls form one evenly spaced column within the sidebar.", assertions);
        scenes.Add(Capture("09-compact-coop-pause.png", ui, GameState.Playing, autoHeaderSession));
        var pauseCommands = new List<GameCommand>();
        var coOpResume = UIManager.CoOpPauseResumeBounds.Center;
        Require(ui.HandleGameplayInput(Pointer(coOpResume.X, coOpResume.Y, leftPressed: true),
                    autoHeaderSession, pauseCommands.Add, 2) == UiAction.None &&
                pauseCommands is [{ Type: GameCommandType.SetPaused, Paused: false, PlayerId: 2 }],
            "Co-op Resume submits the shared unpause command from its visible control.", assertions);
        var coOpRestart = UIManager.CoOpPauseRestartBounds.Center;
        Require(ui.HandleGameplayInput(Pointer(coOpRestart.X, coOpRestart.Y, leftPressed: true),
                    autoHeaderSession, pauseCommands.Add, 2) == UiAction.None &&
                ui.HandleGameplayInput(Pointer(coOpRestart.X, coOpRestart.Y, leftPressed: true),
                    autoHeaderSession, pauseCommands.Add, 2) == UiAction.Restart,
            "Co-op Restart requires and accepts explicit confirmation.", assertions);
        var coOpMenu = UIManager.CoOpPauseMenuBounds.Center;
        Require(ui.HandleGameplayInput(Pointer(coOpMenu.X, coOpMenu.Y, leftPressed: true),
                    autoHeaderSession, pauseCommands.Add, 2) == UiAction.MainMenu,
            "Co-op Main Menu activates its visible control.", assertions);

        var pauseSpecsSession = new GameSession(content, "crosswind_basin", DifficultyCatalog.DefaultId, "no_reserves");
        var pauseSpecsPixels = RenderPixels(ui, GameState.Paused, pauseSpecsSession);
        Require(CountColorPixels(pauseSpecsPixels, new Rectangle(450, 169, 380, 24), ColorPalette.Muted) >= 50,
            "Solo pause run specifications use the surrounding muted blue-gray text color.", assertions);
        Require(CountColorPixels(pauseSpecsPixels, UIManager.PauseResumeBounds, ColorPalette.Cyan) == 0,
            "Idle Resume has no cyan selection treatment.", assertions);
        scenes.Add(Capture("09a-solo-pause-run-specs.png", ui, GameState.Paused, pauseSpecsSession));
        var pauseControls = new[]
        {
            (UIManager.PauseResumeBounds, UiAction.Resume),
            (UIManager.PauseSettingsBounds, UiAction.Settings),
            (UIManager.PauseSaveBounds, UiAction.SaveGame),
            (UIManager.PauseLoadBounds, UiAction.LoadGame),
            (UIManager.PauseMainMenuBounds, UiAction.MainMenu)
        };
        ui.SetSaveState(true);
        foreach (var (bounds, action) in pauseControls)
        {
            ui.PreparePauseScreen();
            Require(ui.HandlePausedInput(Pointer(bounds.Center.X, bounds.Center.Y, leftPressed: true),
                    pauseSpecsSession) == action,
                $"Solo pause {action} activates its visible control.", assertions);
        }
        ui.SetSaveState(false);
        Require(ui.HandlePausedInput(Pointer(UIManager.PauseLoadBounds.Center.X,
                    UIManager.PauseLoadBounds.Center.Y, leftPressed: true), pauseSpecsSession) == UiAction.None,
            "Solo pause Load remains unavailable when there are no saves.", assertions);
        var pauseBounds = pauseControls.Select(control => control.Item1).Append(UIManager.PauseRestartBounds)
            .OrderBy(bounds => bounds.Top).ToArray();
        Require(pauseBounds.All(bounds => new Rectangle(420, 108, 440, 504).Contains(bounds)) &&
                pauseBounds.All(bounds => bounds.Center.X == GameConstants.LogicalWidth / 2) &&
                pauseBounds.Select(bounds => bounds.Width).Distinct().Count() == 1 &&
                pauseBounds.Zip(pauseBounds.Skip(1)).All(pair => pair.Second.Top > pair.First.Bottom) &&
                pauseBounds.Zip(pauseBounds.Skip(1))
                    .Select(pair => pair.Second.Top - pair.First.Bottom).Distinct().Count() == 1,
            "Solo pause controls form one centered, evenly spaced column within the command panel.", assertions);

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
        Require(beaconText == ColorPalette.Paper &&
                ColorPalette.ContrastRatio(beaconText, ColorPalette.Surface(beaconFill, .24f)) >= 4.5f,
            "Signal Beacon's illuminated control retains readable text while hovered.", assertions);
        var beaconUpgradePixels = RenderPixels(ui, GameState.Playing, beaconSession);
        Require(CountColorPixels(beaconUpgradePixels, new Rectangle(1074, 650, 192, 28), ColorPalette.Paper) >= 20,
            "Signal Beacon's upper Tower Intel upgrade label renders in ice white.", assertions);
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
        Require(CountColorPixels(sandboxBreakerPixels, new Rectangle(1192, 506, 60, 16), ColorPalette.Paper) >= 20,
            "Sandbox DISABLE renders light text on its dark control.", assertions);
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
        var apexSession = GameSession.RestoreSaveGame(content, apexSave);
        Require(apexSession.TryPlaceTower("needle_turret", new Vector2(45, 200)) &&
                apexSession.TryChooseTowerDoctrine(apexSession.SelectedTower!.Id, "needle_cycler") &&
                apexSession.TrySpecializeTower(apexSession.SelectedTower!.Id, "rapid_array") &&
                apexSession.CanApexUpgrade(apexSession.SelectedTower),
            "Final-act Entrenched scene exposes Apex on a completed tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, apexSession);
        _ = ui.HandleGameplayInput(Pointer(1120, 693), apexSession);
        scenes.Add(Capture("10f-apex-upgrade-preview.png", ui, GameState.Playing, apexSession));
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { ApexPressed = true }, apexSession);
        Require(apexSession.SelectedTower!.IsApex,
            "The X hotkey purchases an eligible Apex promotion.", assertions);
        _ = ui.HandleGameplayInput(Pointer(0, 0), apexSession);
        var promotedApexPixels = RenderPixels(ui, GameState.Playing, apexSession);
        Require(CountColorPixels(promotedApexPixels, new Rectangle(1204, 484, 58, 22), ColorPalette.Violet) >= 10,
            "Promoted Tower Intel identifies Apex in a reserved top-right header gutter.", assertions);
        scenes.Add(Capture("10g-apex-current-intel.png", ui, GameState.Playing, apexSession));

        var finalActSeed = new GameSession(content, "relay_divide", "hard", "no_reserves");
        var finalActSave = finalActSeed.CaptureSaveGame();
        finalActSave.Waves.CurrentWaveNumber = GameConstants.ApexUnlockWave - 1;
        var finalActSession = GameSession.RestoreSaveGame(content, finalActSave);
        Require(finalActSession.StartNextWave(true) &&
                finalActSession.AnnouncementTitle == "WAVE 21 // RESIDUAL CURRENT",
            "Final-act announcement scene starts authored wave 21.", assertions);
        scenes.Add(Capture("10h-long-final-act-announcement.png", ui, GameState.Playing, finalActSession));

        var settings = new UserSettings { AutoStartWaves = true, AutoStartDelaySeconds = 10, ShowHotkeyBadges = true };
        ui.ConfigureSettings(settings);
        settings.CaptureWindowedClientSize(1475, 830);
        settings.WindowWidth = 1280;
        settings.WindowHeight = 720;
        ui.HandleSettingsInput(Pointer(UIManager.SettingsCategoryBounds(2).Center.X, UIManager.SettingsCategoryBounds(2).Center.Y, leftPressed: true));
        scenes.Add(Capture("11-settings-auto-start.png", ui, GameState.Settings, null));
        scenes.Add(CaptureAtRenderScale("11a-settings-4k.png", ui, GameState.Settings, null,
            GameConstants.MaximumRenderScale));
        Require(ui.HandleSettingsInput(Pointer(640, 269, leftPressed: true)) == UiAction.ApplySettings &&
                !settings.AutoStartWaves,
            "The Settings screen exposes a persistent mouse-only Auto-start control.", assertions);
        Require(ui.HandleSettingsInput(Pointer(640, 339, leftPressed: true)) == UiAction.ApplySettings &&
                !settings.ShowHotkeyBadges,
            "The Settings screen can hide visual hotkey badges without changing input bindings.", assertions);
        scenes.Add(Capture("11b-settings-hotkey-badges-off.png", ui, GameState.Settings, null));

        ui.ConfigureSaveSlots(
        [
            new SaveSlotInfo(SaveSlotRepository.AutosaveSlot, true, true, "crosswind_basin", "hard", "close_quarters", 22, true, 18, 14_292, DateTime.UtcNow),
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
            MapName = "Cinderworks",
            DifficultyId = "normal",
            DifficultyName = "Medium",
            ChallengeId = "standard",
            ChallengeName = "Standard",
            CurrentWave = 65,
            TotalWaves = GameConstants.CampaignWaveCount,
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
            DefeatFieldRecorded = true,
            QueuedEnemiesRemaining = 4,
            RemainingEnemies =
            [
                new RunHistoryRemainingEnemyEntry
                {
                    EnemyId = "t4_aegis",
                    DisplayName = "Aegis",
                    Rank = "Standard",
                    SignalRole = "None",
                    Count = 6,
                    TotalHealth = 9_600,
                    TotalMaxHealth = 19_200,
                    TotalShield = 540,
                    FurthestProgress = 0.94f
                },
                new RunHistoryRemainingEnemyEntry
                {
                    EnemyId = "t5_regenerator",
                    DisplayName = "Elite Regenerator",
                    Rank = "Elite",
                    SignalRole = "Disruptor",
                    Count = 3,
                    TotalHealth = 8_400,
                    TotalMaxHealth = 12_000,
                    FurthestProgress = 0.82f
                }
            ],
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
        Require(ui.HandleRunHistory(Pointer(1080, 80, leftPressed: true)) == UiAction.None && ui.IsRunHistoryCareerOpen,
            "Run History opens the persistent medals, achievements, and records overview.", assertions);
        Require(ColorPalette.ContrastRatio(ColorPalette.AmberText, ColorPalette.PanelAlt) >= 3f,
            "Archive amber ink keeps gold-family labels readable on light panels.", assertions);
        scenes.Add(Capture("13b-career-medals-and-records.png", ui, GameState.RunHistory, null));
        ui.HandleRunHistory(Pointer(UIManager.CareerCategoryBounds(1).Center.X, 120, leftPressed: true));
        ui.HandleRunHistory(Pointer(1180, 606, leftPressed: true));
        scenes.Add(Capture("13b1-medals.png", ui, GameState.RunHistory, null));
        ui.HandleRunHistory(Pointer(UIManager.CareerCategoryBounds(2).Center.X, 120, leftPressed: true));
        scenes.Add(Capture("13b2-records.png", ui, GameState.RunHistory, null));
        ui.HandleRunHistory(Pointer(UIManager.CareerCategoryBounds(0).Center.X, 120, leftPressed: true));
        ui.HandleRunHistory(Pointer(1180, 606, leftPressed: true));
        Require(ui.CareerMedalPage == 1 && ui.CareerAchievementPage == 1,
            "Career medal and achievement catalogs expose independent later pages.", assertions);
        scenes.Add(Capture("13c-career-catalog-pages.png", ui, GameState.RunHistory, null));
        for (var page = 0; page < 5; page++)
            _ = ui.HandleRunHistory(Pointer(1180, 606, leftPressed: true));
        Require(ui.CareerAchievementPage == 6,
            "Career progression exposes its final Honors and completion goals.", assertions);
        scenes.Add(Capture("13d-career-completion-goals.png", ui, GameState.RunHistory, null));
        Require(ui.HandleRunHistory(Pointer(640, 670, leftPressed: true)) == UiAction.None && !ui.IsRunHistoryCareerOpen,
            "The career overview returns to Run History without changing its selected run.", assertions);
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
        ui.PrepareResultScreen(endlessResult.CurrentWave);
        scenes.Add(Capture("15-endless-exact-wave.png", ui, GameState.Defeat, endlessResult));
        Require(ui.HandleResultInput(Pointer(551, 603, leftPressed: true), false) == UiAction.RetryWave,
            "Defeat results expose the matching pre-wave autosave as a direct Retry Wave action.", assertions);

        var combat = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        var fieldUi = new UIManager(font, typography.Display);
        ConfigureUi(fieldUi, content);
        var candidates = new List<Vector2>();
        foreach (var region in combat.Map.BuildableRegions)
        for (var y = region.Top + 24; y < region.Bottom - 16; y += 44)
        for (var x = region.Left + 24; x < region.Right - 16; x += 44)
            candidates.Add(new Vector2(x, y));
        foreach (var tower in content.Towers.Values)
        {
            var placed = candidates.OrderByDescending(point => combat.Towers.Count == 0 ? 0 :
                    combat.Towers.Min(existing => Vector2.DistanceSquared(existing.Position, point)))
                .Any(point => combat.TryPlaceTower(tower.Id, point));
            Require(placed, $"Hardware showcase places {tower.DisplayName} on a legal deck.", assertions);
        }
        combat.CancelPlacement();
        var profiles = content.Enemies.Values.ToArray();
        for (var index = 0; index < 25; index++)
        {
            combat.SpawnEnemy(profiles[index % profiles.Length].Id, 5, 1, index == 18 ? "Boss" : index % 7 == 0 ? "Elite" : "Standard");
            combat.Enemies[^1].SetSandboxPathDistance(80 + index * (combat.Map.Path.TotalLength - 160) / 25, combat.Map.Path);
        }
        for (var frame = 0; frame < 28; frame++) combat.Update(1f / 60);
        var checksum = SessionChecksum.Compute(combat, 28);
        scenes.Add(Capture("16-night-grid-combat.png", fieldUi, GameState.Playing, combat));
        var fullEffects = RenderPixels(fieldUi, GameState.Playing, combat);
        _renderer.ReducedEffects = true;
        scenes.Add(Capture("16a-night-grid-reduced-effects.png", fieldUi, GameState.Playing, combat));
        var reducedEffects = RenderPixels(fieldUi, GameState.Playing, combat);
        _renderer.ReducedEffects = false;
        Require(SessionChecksum.Compute(combat, 28) == checksum,
            "Full and reduced rendering leave the combat checksum unchanged.", assertions);
        Require(CountChangedPixels(fullEffects, reducedEffects, new Rectangle(0, 56, 960, 664)) > 100,
            "Reduced effects suppress decorative combat lighting while retaining the same field.", assertions);
        scenes.Add(Capture("17-victory-summary.png", fieldUi, GameState.Victory, combat));

        foreach (var map in content.Maps.Values)
        {
            var district = new GameSession(content, map.Id, "normal", "sandbox_lab");
            var districtUi = new UIManager(font, typography.Display);
            ConfigureUi(districtUi, content);
            for (var frame = 0; frame < 300; frame++) district.Update(1f / 60);
            scenes.Add(Capture($"18-{map.Id}-overview.png", districtUi, GameState.Playing, district));
            _renderer.ReducedEffects = true;
            var still = RenderPixels(districtUi, GameState.Playing, district);
            _renderer.ReducedEffects = false;
            var moving = RenderPixels(districtUi, GameState.Playing, district);
            for (var frame = 0; frame < 60; frame++) district.Update(1f / 60);
            var animated = RenderPixels(districtUi, GameState.Playing, district);
            _renderer.ReducedEffects = true;
            var stillLater = RenderPixels(districtUi, GameState.Playing, district);
            _renderer.ReducedEffects = false;
            var fieldBounds = new Rectangle(0, 190, 960, 530);
            Require(CountChangedPixels(still, stillLater, fieldBounds) == 0,
                $"{map.DisplayName}: reduced effects freeze environmental motion.", assertions);
            Require(CountChangedPixels(moving, animated, fieldBounds) > 50,
                $"{map.DisplayName}: signature ambient motion is visible without combat.", assertions);
            var towerIndex = 0;
            foreach (var region in district.Map.BuildableRegions)
            {
                var towerId = content.Towers.Keys.ElementAt(towerIndex++ % content.Towers.Count);
                district.TryPlaceTower(towerId, region.Center.ToVector2());
            }
            district.CancelPlacement();
            for (var enemyIndex = 0; enemyIndex < 18; enemyIndex++)
            {
                district.SpawnEnemy(profiles[enemyIndex % profiles.Length].Id, 4, 1,
                    enemyIndex == 12 ? "Boss" : "Standard");
                district.Enemies[^1].SetSandboxPathDistance(100 + enemyIndex * (district.Map.Path.TotalLength - 200) / 18,
                    district.Map.Path);
            }
            for (var frame = 0; frame < 8; frame++) district.Update(1f / 60);
            var districtChecksum = SessionChecksum.Compute(district, 68);
            scenes.Add(Capture($"19-{map.Id}-combat.png", districtUi, GameState.Playing, district));
            Require(SessionChecksum.Compute(district, 68) == districtChecksum,
                $"{map.DisplayName}: scenery rendering leaves gameplay state intact.", assertions);
        }

        var reviewUi = new UIManager(font, typography.Display);
        ConfigureUi(reviewUi, content);
        for (var category = 0; category < 3; category++)
        {
            var tab = UIManager.SettingsCategoryBounds(category).Center;
            reviewUi.HandleSettingsInput(Pointer(tab.X, tab.Y, leftPressed: true));
            scenes.Add(Capture($"20-settings-{category}.png", reviewUi, GameState.Settings, null));
        }
        reviewUi.HandleSettingsInput(Pointer(0, 0));
        var surfaceBefore = RenderPixels(reviewUi, GameState.Settings, null);
        reviewUi.AdvanceVisualTime(1.2f);
        var surfaceAfter = RenderPixels(reviewUi, GameState.Settings, null);
        Require(CountChangedPixels(surfaceBefore, surfaceAfter, new Rectangle(24, 24, 1232, 672)) > 20,
            "Secondary menus carry subtle infrastructure motion.", assertions);
        Require(CountChangedPixels(surfaceBefore, surfaceAfter, new Rectangle(350, 164, 580, 410)) == 0,
            "Atmospheric animation leaves settings controls and labels still.", assertions);
        reviewUi.ConfigureSettings(new UserSettings { ReducedEffects = true });
        surfaceBefore = RenderPixels(reviewUi, GameState.Settings, null);
        reviewUi.AdvanceVisualTime(1.2f);
        surfaceAfter = RenderPixels(reviewUi, GameState.Settings, null);
        Require(CountChangedPixels(surfaceBefore, surfaceAfter, new Rectangle(0, 0, 1280, 720)) == 0,
            "Reduced Effects freezes secondary-menu atmosphere.", assertions);
        reviewUi.ConfigureSettings(new UserSettings());
        var reviewSession = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        reviewSession.TryPlaceTower("needle_turret", new Vector2(245, 380));
        reviewSession.CancelPlacement();
        reviewSession.HandleWorldInput(Pointer(245, 380, leftPressed: true));
        reviewUi.HandleGameplayInput(Pointer(0, 0), reviewSession);
        scenes.Add(Capture("22-tower-overview.png", reviewUi, GameState.Playing, reviewSession));
        var currentStatsPixels = RenderPixels(reviewUi, GameState.Playing, reviewSession);
        Require(CountColorPixels(currentStatsPixels, new Rectangle(980, 572, 282, 50), ColorPalette.Paper) > 30,
            "Current tower intel shows the complete stat grid by default.", assertions);
        Require(CountColorPixels(currentStatsPixels, new Rectangle(980, 628, 280, 16), ColorPalette.Muted) > 30,
            "Tower lifetime contribution appears beneath the current stats by default.", assertions);
        Require(reviewUi.HandleGameplayInput(Pointer(0, 0) with { EscapePressed = true }, reviewSession) == UiAction.Pause,
            "Escape pauses while inspecting tower intel.", assertions);

        reviewUi.HandleGameplayInput(Pointer(0, 0) with { EmergencyPressed = true }, reviewSession);
        Require(reviewSession.TacticalPlacement == TacticalPlacementKind.PulsePlate,
            "Q prepares a Pulse Plate in Sandbox.", assertions);
        reviewSession.CancelPlacement();
        var plateControl = UIManager.SandboxPlateBounds.Center;
        reviewUi.HandleGameplayInput(Pointer(plateControl.X, plateControl.Y, leftPressed: true), reviewSession);
        Require(reviewSession.TacticalPlacement == TacticalPlacementKind.PulsePlate,
            "The Sandbox plate control prepares the same placement as Q.", assertions);
        var platePoint = reviewSession.Map.Path.GetPosition(120);
        reviewSession.HandleWorldInput(Pointer(platePoint.X, platePoint.Y, leftPressed: true));
        Require(reviewSession.EmergencyDefenses.Count == 1,
            "A Sandbox plate can be placed from the visible control before a test wave.", assertions);
        reviewUi.HandleGameplayInput(Pointer(plateControl.X, plateControl.Y), reviewSession);
        scenes.Add(Capture("22c-sandbox-pulse-plates.png", reviewUi, GameState.Playing, reviewSession));
        Require(UIManager.SandboxPlateBounds.Top - 201 == 228 - UIManager.SandboxPlateBounds.Bottom,
            "Sandbox Plates has equal clearance below the divider and above the tower cards.", assertions);
        reviewUi.ConfigureSettings(new UserSettings { ShowHotkeyBadges = true });
        scenes.Add(Capture("22c-sandbox-hotkeys-on.png", reviewUi, GameState.Playing, reviewSession));
        reviewUi.ConfigureSettings(new UserSettings());
        reviewSession.CancelPlacement();
        var denseIntelSession = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        denseIntelSession.TryPlaceTower("prism_beam", new Vector2(245, 380));
        denseIntelSession.CancelPlacement();
        denseIntelSession.HandleWorldInput(Pointer(245, 380, leftPressed: true));
        reviewUi.HandleGameplayInput(Pointer(0, 0), denseIntelSession);
        scenes.Add(Capture("22a-dense-current-stats.png", reviewUi, GameState.Playing, denseIntelSession));
        denseIntelSession.BeginPlacement("prism_beam");
        denseIntelSession.HandleWorldInput(Pointer(660, 185));
        reviewUi.HandleGameplayInput(Pointer(660, 185), denseIntelSession);
        scenes.Add(Capture("22b-dense-placement-stats.png", reviewUi, GameState.Playing, denseIntelSession));
        endlessResult.Update(1f / 60);
        reviewUi.PrepareResultScreen(endlessResult.CurrentWave);
        scenes.Add(Capture("23-results-overview.png", reviewUi, GameState.Defeat, endlessResult));
        reviewUi.HandleResultInput(Pointer(UIManager.ResultDetailsBounds.Center.X, UIManager.ResultDetailsBounds.Center.Y, leftPressed: true), false);
        Require(reviewUi.ResultDetailsVisible, "Result statistics remain available on demand.", assertions);
        scenes.Add(Capture("23-results-details.png", reviewUi, GameState.Defeat, endlessResult));
        reviewUi.HandleResultInput(Pointer(0, 0) with { EscapePressed = true }, false);
        Require(!reviewUi.ResultDetailsVisible, "Escape returns from result statistics to the result overview.", assertions);

        var contributionSave = comparisonSession.CaptureSaveGame();
        contributionSave.Waves.CurrentWaveNumber = GameConstants.CampaignWaveCount;
        contributionSave.Waves.IsFinalWaveCleared = true;
        contributionSave.Waves.EndlessModeEnabled = false;
        contributionSave.Statistics = new RunStatisticsSaveData
        {
            SimulatedSeconds = 2_870,
            Towers =
            [
                new RunTowerStatisticsSaveData
                {
                    TowerId = "prism_beam", DisplayName = "Prism Beam", CreditsSpent = 5_871,
                    Damage = 638_885, ExposeDamageEquivalent = 230_023
                },
                new RunTowerStatisticsSaveData
                {
                    TowerId = "breaker_cannon", DisplayName = "Breaker Cannon", CreditsSpent = 6_440,
                    Damage = 568_732, ArmorBreakDamageEquivalent = 129_960
                },
                new RunTowerStatisticsSaveData
                {
                    TowerId = "signal_beacon", DisplayName = "Signal Beacon", CreditsSpent = 4_980,
                    SupportDamageEquivalent = 677_782
                },
                new RunTowerStatisticsSaveData
                {
                    TowerId = "siege_mortar", DisplayName = "Siege Mortar", CreditsSpent = 5_720,
                    Damage = 487_453, Kills = 500
                }
            ]
        };
        var contributionUi = new UIManager(font, typography.Display);
        ConfigureUi(contributionUi, content);
        contributionSave.Economy.Lives = 0;
        var contributionDefeat = GameSession.RestoreSaveGame(content, contributionSave);
        contributionDefeat.Update(1f / 60);
        contributionUi.PrepareResultScreen(contributionDefeat.CurrentWave);
        contributionUi.HandleResultInput(Pointer(UIManager.ResultDetailsBounds.Center.X,
            UIManager.ResultDetailsBounds.Center.Y, leftPressed: true), false);
        scenes.Add(Capture("23b-results-contributions-defeat.png", contributionUi, GameState.Defeat, contributionDefeat));
        contributionSave.Economy.Lives = 8;
        var contributionVictory = GameSession.RestoreSaveGame(content, contributionSave);
        contributionVictory.Update(1f / 60);
        contributionUi.PrepareResultScreen(contributionVictory.CurrentWave);
        contributionUi.HandleResultInput(Pointer(UIManager.ResultDetailsBounds.Center.X,
            UIManager.ResultDetailsBounds.Center.Y, leftPressed: true), true);
        scenes.Add(Capture("23c-results-contributions-victory.png", contributionUi, GameState.Victory, contributionVictory));

        reviewUi.PreparePauseScreen();
        var pauseRestart = UIManager.PauseRestartBounds.Center;
        Require(reviewUi.HandlePausedInput(Pointer(pauseRestart.X, pauseRestart.Y, leftPressed: true), reviewSession)
                == UiAction.None,
            "Solo pause Restart requires an explicit second activation.", assertions);
        scenes.Add(Capture("24-restart-confirmation.png", reviewUi, GameState.Paused, reviewSession));
        Require(reviewUi.HandlePausedInput(Pointer(pauseRestart.X, pauseRestart.Y, leftPressed: true), reviewSession)
                == UiAction.Restart,
            "Solo pause confirms an armed restart.", assertions);
        reviewUi.SetCoOpLobbyStatus("WAITING FOR PLAYER", "Share your address and join code.", "AB12CD");
        scenes.Add(Capture("25-coop-lobby.png", reviewUi, GameState.CoOpLobby, null));
        reviewUi.SetCoOpLobbyStatus("HOST COULD NOT START", "The host address is unavailable.");
        scenes.Add(Capture("25a-coop-host-error.png", reviewUi, GameState.CoOpLobby, null));
        reviewUi.SetCoOpLobbyStatus("HOSTING SAVED CO-OP", "Share your address and join code.", "AB12CD");
        scenes.Add(Capture("25b-coop-saved-lobby.png", reviewUi, GameState.CoOpLobby, null));
        reviewUi.SetCoOpLobbyStatus("RECONNECTING", "Your defense is paused.", "AB12CD");
        scenes.Add(Capture("25-coop-reconnect.png", reviewUi, GameState.CoOpReconnect, reviewSession));
        reviewUi.SetCoOpLobbyStatus("CONNECTION TO HOST LOST", "Your defense is paused.", "AB12CD");
        scenes.Add(Capture("25c-coop-long-reconnect.png", reviewUi, GameState.CoOpReconnect, reviewSession));
        reviewUi.SetCoOpLobbyStatus("STATE SYNCHRONIZED", "Waiting for the host to resume both players...");
        scenes.Add(Capture("25d-coop-reconnect-without-code.png", reviewUi, GameState.CoOpReconnect, reviewSession));
        reviewUi.SetCoOpLobbyStatus("HOST COULD NOT START",
            "The address 203.0.113.10:28741 is unavailable because another application is already using this endpoint. Choose another port and try again.");
        scenes.Add(Capture("25e-coop-long-host-error.png", reviewUi, GameState.CoOpLobby, null));
        reviewUi.SetCoOpLobbyStatus("RECONNECTING TO HOST",
            "The remote host 203.0.113.10:28741 is temporarily unavailable. Your shared defense is preserved while the connection is restored.", "AB12CD");
        scenes.Add(Capture("25f-coop-long-reconnect-detail.png", reviewUi, GameState.CoOpReconnect, reviewSession));
        reviewUi.ConfigureSaveSlots([new SaveSlotInfo(1, true, false, "foundry_loop", "normal", "standard", 12,
            false, 12, 300, new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc))], true, 1);
        scenes.Add(Capture("26-save-overwrite.png", reviewUi, GameState.SaveSlots, null));
        Require(reviewUi.HandleSaveSlots(Pointer(840, 540, leftPressed: true)) == UiAction.None,
            "Deleting a save requires an explicit second activation.", assertions);
        scenes.Add(Capture("26-save-delete-confirmation.png", reviewUi, GameState.SaveSlots, null));
        reviewUi.HandleSaveSlots(Pointer(0, 0) with { EscapePressed = true });
        reviewUi.ConfigureSaveSlots([], false);
        scenes.Add(Capture("26-save-empty.png", reviewUi, GameState.SaveSlots, null));
        reviewUi.ConfigureRunHistory([]);
        scenes.Add(Capture("27-history-empty.png", reviewUi, GameState.RunHistory, null));

        HideAndDisableActivation();
        VerifyMenuHeadingMeasurements(assertions);
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
        ui.ConfigureReleaseNotes(content.ReleaseNotes);
        ui.ConfigureMaps(content.Maps.Values);
        ui.ConfigureDifficulties(content.Difficulties.Values);
        ui.ConfigureChallenges(content.Challenges.Values);
        ui.ConfigureTowerNames(content.Towers.Values);
        ui.ConfigureMainMenuBattle(content, randomSeed: 27182);
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

    private void AssertCombatFeedClipping(GameContent content, ICollection<string> assertions,
        ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId);
        session.SpawnEnemy("t5_regenerator", 1, 1);
        session.Enemies[0].SetSandboxPathDistance(350, session.Map.Path);
        var center = session.Enemies[0].Position.ToPoint();
        var bounds = new Rectangle(center.X - 42, center.Y - 50, 84, 55);
        foreach (var scale in new[] { 2, 3 })
        {
            HideAndDisableActivation();
            using var target = new RenderTarget2D(GraphicsDevice, 720, 480,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            var transform = Matrix.CreateTranslation(-bounds.X + 24, -bounds.Y + 24, 0) * Matrix.CreateScale(scale);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, transform);
            _primitives.DrawClipped(_batch, bounds, transform,
                () => _renderer.DrawCombatShowcase(_batch, _primitives, session));
            var marker = new Rectangle(bounds.Right + 8, bounds.Bottom + 8, 6, 6);
            _primitives.FillRect(_batch, marker, ColorPalette.Paper);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            var clip = new Rectangle(24 * scale, 24 * scale, bounds.Width * scale, bounds.Height * scale);
            var markerPixels = new Rectangle((24 + bounds.Width + 8) * scale,
                (24 + bounds.Height + 8) * scale, 6 * scale, 6 * scale);
            var escapedPixels = 0;
            var visiblePixels = 0;
            for (var y = 0; y < target.Height; y++)
            for (var x = 0; x < target.Width; x++)
            {
                if (pixels[y * target.Width + x] == ColorPalette.Canvas) continue;
                if (clip.Contains(x, y)) visiblePixels++;
                else if (!markerPixels.Contains(x, y)) escapedPixels++;
            }
            Require(escapedPixels == 0 && visiblePixels > 200,
                $"A Regenerator crossing the feed edge and its regeneration ring are clipped at {scale}x with a translated viewport.", assertions);
            Require(pixels[markerPixels.Center.Y * target.Width + markerPixels.Center.X] == ColorPalette.Paper,
                $"Feed clipping restores subsequent UI drawing outside the feed at {scale}x.", assertions);
            var fileName = $"00h-menu-feed-clipping-{scale}x.png";
            var path = Path.Combine(_outputDirectory, fileName);
            using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
            scenes.Add(new VisualVerificationScene(fileName, target.Width, target.Height,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), visiblePixels));
        }
    }

    private VisualVerificationScene CaptureTowerMountGallery(GameContent content, SpriteFont font)
    {
        var towers = content.Towers.Values.ToArray();
        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        var angles = new[] { -MathHelper.PiOver2, 0, MathHelper.Pi * .75f };
        var headings = new[] { "UP / 2.4x", "RIGHT / 2.4x", "DIAGONAL / 2.4x" };
        using var target = new RenderTarget2D(GraphicsDevice, 1600, 1200,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label("TOWER MOUNTS", new Vector2(24, 16), 1.05f);
        Label("Fixed base and light direction across aim, scale, opacity, and effects", new Vector2(370, 21), .7f);
        for (var index = 0; index < towers.Length; index++)
        {
            var x = 24 + index % 2 * 788;
            var y = 68 + index / 2 * 224;
            _primitives.FillRect(_batch, new Rectangle(x, y, 764, 212), decks[index % decks.Length]);
            Label(towers[index].DisplayName, new Vector2(x + 16, y + 10), .8f);
            for (var heading = 0; heading < headings.Length; heading++)
                Label(headings[heading], new Vector2(x + 37 + heading * 174, y + 46), .48f);
            _primitives.Line(_batch, new Vector2(x + 550, y + 44), new Vector2(x + 550, y + 198), ColorPalette.Metal);
            Label("FIELD / 1x", new Vector2(x + 570, y + 49), .44f);
            Label("UI / 10px", new Vector2(x + 668, y + 49), .44f);
            Label("GHOST / 50%", new Vector2(x + 568, y + 131), .4f);
            Label("REDUCED", new Vector2(x + 669, y + 131), .44f);
        }
        _batch.End();
        for (var index = 0; index < towers.Length; index++)
        {
            var tower = towers[index];
            var x = 24 + index % 2 * 788;
            var y = 68 + index / 2 * 224;
            for (var heading = 0; heading < angles.Length; heading++)
                Sample(new Vector2(x + 92 + heading * 174, y + 134), tower.Visual.Radius, 2.4f,
                    angles[heading], 1, true);
            Sample(new Vector2(x + 601, y + 98), tower.Visual.Radius, 1, -.4f, 1, true);
            Sample(new Vector2(x + 705, y + 98), 10, 1, -.4f, 1, true);
            Sample(new Vector2(x + 601, y + 172), tower.Visual.Radius, 1, -.4f, .5f, true);
            Sample(new Vector2(x + 705, y + 172), tower.Visual.Radius, 1, -.4f, 1, false);

            void Sample(Vector2 at, float radius, float scale, float angle, float opacity, bool lights)
            {
                var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, transform);
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, radius, tower.Id,
                    angle: angle, time: lights ? .23f : 0, opacity: opacity, lights: lights);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        const string fileName = "32-tower-mounts.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return new VisualVerificationScene(fileName, target.Width, target.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixels.Count(pixel => pixel != ColorPalette.Canvas));

        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private VisualVerificationScene CaptureTowerUpgradeGallery(GameContent content, SpriteFont font)
    {
        var towers = content.Towers.Values.ToArray();
        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        var angles = new[] { -MathHelper.PiOver2, 0, MathHelper.Pi * .75f };
        var tiers = new[] { "T1", "T2", "T3", "APEX", "APEX / REDUCED" };
        using var target = new RenderTarget2D(GraphicsDevice, 1800, 1400,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label("TOWER UPGRADES", new Vector2(24, 16), 1.05f);
        Label("2.4x detail above / field scale below", new Vector2(450, 23), .7f);
        for (var index = 0; index < towers.Length; index++)
        {
            var x = 24 + index % 2 * 888;
            var y = 76 + index / 2 * 262;
            _primitives.FillRect(_batch, new Rectangle(x, y, 864, 250), decks[index % decks.Length]);
            Label(towers[index].DisplayName, new Vector2(x + 16, y + 10), .8f);
            for (var tier = 0; tier < tiers.Length; tier++)
            {
                var textScale = tier == 4 ? .42f : .54f;
                var at = new Vector2(x + Column(tier), y + 46);
                at.X -= font.MeasureString(tiers[tier]).X * textScale * GameConstants.FontDrawScale * .5f;
                Label(tiers[tier], at, textScale);
            }
            _primitives.Line(_batch, new Vector2(x + 657, y + 46), new Vector2(x + 657, y + 234), ColorPalette.Metal);
        }
        _batch.End();
        for (var index = 0; index < towers.Length; index++)
        {
            var tower = towers[index];
            var x = 24 + index % 2 * 888;
            var y = 76 + index / 2 * 262;
            for (var tier = 0; tier < tiers.Length; tier++)
            {
                var angle = angles[(index + tier) % angles.Length];
                Sample(new Vector2(x + Column(tier), y + 120), 2.4f, angle, tier);
                Sample(new Vector2(x + Column(tier), y + 210), 1, angle, tier);
            }

            void Sample(Vector2 at, float scale, float angle, int tier)
            {
                var lights = tier != 4;
                var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, transform);
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, tower.Visual.Radius, tower.Id,
                    level: Math.Min(tier + 1, 3), angle: angle, time: lights ? .23f : 0,
                    lights: lights, apex: tier >= 3);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        const string fileName = "33-tower-upgrades.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return new VisualVerificationScene(fileName, target.Width, target.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixels.Count(pixel => pixel != ColorPalette.Canvas));

        static float Column(int tier) => tier == 4 ? 758 : 82 + tier * 160;
        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private void AssertTowerProtocolPresentation(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var towers = content.Towers.Values.ToArray();
        const float firstPhase = .23f;
        const float secondPhase = .91f;
        foreach (var tower in towers)
        {
            var first = SignaturePixels(firstPhase, false);
            var second = SignaturePixels(secondPhase, false);
            var quiet = SignaturePixels(firstPhase, true);
            Require(first.Count(pixel => pixel != ColorPalette.Canvas) > 120 &&
                    quiet.Count(pixel => pixel != ColorPalette.Canvas) > 120,
                $"{tower.Id}: the active protocol remains visibly marked with full and Reduced Effects.", assertions);
            Require(first.Zip(second, (a, b) => a != b).Count(changed => changed) > 40,
                $"{tower.Id}: the active protocol signature animates between presentation phases.", assertions);
            Require(quiet.SequenceEqual(SignaturePixels(secondPhase, true)),
                $"{tower.Id}: Reduced Effects keeps the active protocol signature stable over time.", assertions);

            Color[] SignaturePixels(float time, bool reduced)
            {
                using var target = new RenderTarget2D(GraphicsDevice, 480, 480,
                    false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(ColorPalette.Canvas);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Matrix.CreateScale(4));
                TowerProtocolArt.Draw(_batch, _primitives, new Vector2(60), tower.Visual.Radius,
                    tower.Id, time, reduced);
                _batch.End();
                GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[target.Width * target.Height];
                target.GetData(pixels);
                return pixels;
            }
        }

        var decks = new[] { ColorPalette.Cinderworks.Deck, ColorPalette.Rainline.Deck,
            ColorPalette.Nullspace.Deck, ColorPalette.Helix.Deck };
        var angles = new[] { -MathHelper.PiOver2, 0, MathHelper.Pi * .75f };
        var headings = new[] { "IDLE / T3", "ACTIVE / A", "ACTIVE / B", "APEX / ACTIVE", "REDUCED / ACTIVE" };
        using var gallery = new RenderTarget2D(GraphicsDevice, 1800, 1830,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(gallery);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Label("TOWER PROTOCOLS", new Vector2(24, 16), 1.05f);
        Label("1.7x detail above / field scale below", new Vector2(450, 23), .7f);
        for (var index = 0; index < towers.Length; index++)
        {
            var x = 24 + index % 2 * 888;
            var y = 76 + index / 2 * 350;
            _primitives.FillRect(_batch, new Rectangle(x, y, 864, 338), decks[index % decks.Length]);
            Label(towers[index].DisplayName, new Vector2(x + 16, y + 10), .8f);
            for (var column = 0; column < headings.Length; column++)
            {
                var textScale = column >= 3 ? .4f : .5f;
                var at = new Vector2(x + Column(column), y + 46);
                at.X -= font.MeasureString(headings[column]).X * textScale * GameConstants.FontDrawScale * .5f;
                Label(headings[column], at, textScale);
            }
            _primitives.Line(_batch, new Vector2(x + 657, y + 46), new Vector2(x + 657, y + 322), ColorPalette.Metal);
        }
        _batch.End();
        for (var index = 0; index < towers.Length; index++)
        {
            var tower = towers[index];
            var x = 24 + index % 2 * 888;
            var y = 76 + index / 2 * 350;
            var angle = angles[index % angles.Length];
            for (var column = 0; column < headings.Length; column++)
            {
                Sample(new Vector2(x + Column(column), y + 150), 1.7f, column);
                Sample(new Vector2(x + Column(column), y + 282), 1, column);
            }

            void Sample(Vector2 at, float scale, int column)
            {
                var reduced = column == 4;
                var time = column == 2 ? secondPhase : firstPhase;
                var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(at.X, at.Y, 0);
                _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, transform);
                NightGridArt.Tower(_batch, _primitives, Vector2.Zero, tower.Visual.Radius, tower.Id,
                    level: 3, angle: angle, time: reduced ? 0 : time, lights: !reduced, apex: column >= 3);
                if (column > 0)
                    TowerProtocolArt.Draw(_batch, _primitives, Vector2.Zero, tower.Visual.Radius,
                        tower.Id, time, reduced);
                _batch.End();
            }
        }
        GraphicsDevice.SetRenderTarget(null);
        const string fileName = "34-tower-protocols.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) gallery.SaveAsPng(stream, gallery.Width, gallery.Height);
        var galleryPixels = new Color[gallery.Width * gallery.Height];
        gallery.GetData(galleryPixels);
        scenes.Add(new VisualVerificationScene(fileName, gallery.Width, gallery.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            galleryPixels.Count(pixel => pixel != ColorPalette.Canvas)));

        static float Column(int column) => column == 4 ? 758 : 82 + column * 160;
        void Label(string text, Vector2 at, float scale) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private VisualVerificationScene CaptureHealthBarGallery(SpriteFont font)
    {
        using var target = new RenderTarget2D(GraphicsDevice, 1680, 920,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Panel);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(4));
        Label("HEALTH BARS / 4x DETAIL", new Vector2(8, 8));
        var ratios = new[] { 1f, .5f, .1f, 0f };
        var widths = new[] { 32, 48, 55, 70 };
        var captions = new[] { "FULL", "HALF", "LOW", "EMPTY" };
        for (var column = 0; column < ratios.Length; column++)
            Label(captions[column], new Vector2(110 + column * 80, 34));
        for (var row = 0; row < widths.Length; row++)
        {
            var y = 65 + row * 42;
            Label($"{widths[row]} PX", new Vector2(8, y - 2));
            for (var column = 0; column < ratios.Length; column++)
                _primitives.HealthBar(_batch, new Vector2(125 + column * 80, y), widths[row], ratios[column],
                    ColorPalette.Health(ratios[column]), ColorPalette.HealthTrack, ColorPalette.Ink);
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        const string fileName = "32-health-bar-states.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return new VisualVerificationScene(fileName, target.Width, target.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixels.Count(pixel => pixel != ColorPalette.Panel));

        void Label(string text, Vector2 at) => _batch.DrawString(font, text, at, ColorPalette.Paper,
            0, Vector2.Zero, .32f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private void AssertProjectileIdentity(GameContent content, SpriteFont font,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var ids = new[] { "needle_turret", "watchtower", "shard_fan", "frost_spire", "ember_coil", "breaker_cannon", "siege_mortar" };
        var silhouettes = ids.Select(id => PixelsAt(id, 0, true)).ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            var illuminated = PixelsAt(ids[index], 0, false);
            var haloPixels = illuminated.Where((pixel, pixelIndex) => silhouettes[index][pixelIndex] == ColorPalette.Canvas &&
                pixel.R + pixel.G + pixel.B > ColorPalette.Canvas.R + ColorPalette.Canvas.G + ColorPalette.Canvas.B + 24).Count();
            Require(haloPixels > 140,
                $"{ids[index]}: full effects add a visible luminous halo beyond the core ({haloPixels} pixels).", assertions);
            Require(silhouettes[index].SequenceEqual(PixelsAt(ids[index], .37f, true)),
                $"{ids[index]}: Reduced Effects retains a stable projectile silhouette.", assertions);
            for (var other = 0; other < index; other++)
                Require(silhouettes[index].Zip(silhouettes[other], (a, b) =>
                        (a.R + a.G + a.B > 160) != (b.R + b.G + b.B > 160)).Count(changed => changed) > 40,
                    $"{ids[index]} and {ids[other]} retain different core silhouettes without relying on hue.", assertions);
        }

        using var gallery = new RenderTarget2D(GraphicsDevice, 1600, 1000,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(gallery);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(2));
        for (var index = 0; index < ids.Length; index++)
        {
            var id = ids[index];
            var x = 36 + index % 2 * 400;
            var y = 24 + index / 2 * 122;
            var definition = content.Towers[id];
            _primitives.FillRect(_batch, new Rectangle(x - 12, y, 360, 106), ColorPalette.Panel);
            _batch.DrawString(font, definition.DisplayName, new Vector2(x, y + 6), ColorPalette.Paper,
                0, Vector2.Zero, .65f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
            NightGridArt.Tower(_batch, _primitives, new Vector2(x + 35, y + 66), 27, id, angle: -.3f, time: .23f);
            foreach (var step in new[] { 0, 1, 2 })
            {
                ProjectileArt.Draw(_batch, _primitives, id, new Vector2(x + 125 + step * 82, y + 67),
                    Vector2.Normalize(new Vector2(1, -.3f)), RadiusFor(id),
                    definition.Visual.PrimaryColor, step * .17f, step == 2);
                _batch.DrawString(font, step == 2 ? "REDUCED" : step == 0 ? "FULL" : "IN FLIGHT",
                    new Vector2(x + 110 + step * 82, y + 91), ColorPalette.Muted,
                    0, Vector2.Zero, .27f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
            }
        }
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var fileName = "30-projectile-roster.png";
        var path = Path.Combine(_outputDirectory, fileName);
        using (var stream = File.Create(path)) gallery.SaveAsPng(stream, gallery.Width, gallery.Height);
        var galleryPixels = new Color[gallery.Width * gallery.Height];
        gallery.GetData(galleryPixels);
        scenes.Add(new VisualVerificationScene(fileName, gallery.Width, gallery.Height,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), galleryPixels.Count(pixel => pixel != ColorPalette.Canvas)));

        Color[] PixelsAt(string id, float time, bool quiet)
        {
            using var target = new RenderTarget2D(GraphicsDevice, 160, 160,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(4));
            ProjectileArt.Draw(_batch, _primitives, id, new Vector2(20), Vector2.UnitX, RadiusFor(id),
                ColorPalette.Cyan, time, quiet);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            return pixels;
        }

        static float RadiusFor(string id) => id switch { "siege_mortar" => 7, "shard_fan" => 3, _ => 5 };
    }

    private void AssertPrismBeamPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var accent = content.Towers["prism_beam"].Visual.PrimaryColor;
        var start = new Vector2(40, 60);
        var end = new Vector2(280, 60);
        var first = PixelsAt(.8f, false, start, end);
        var quiet = PixelsAt(.8f, true, start, end);
        foreach (var (pixels, mode) in new[] { (first, "Full"), (quiet, "Reduced") })
        {
            var uniform = true;
            for (var y = 100; y < 140; y++)
            for (var x = 140; x < 500; x++)
                uniform &= pixels[y * 640 + x] == pixels[y * 640 + 320];
            Require(uniform && pixels[120 * 640 + 320] != ColorPalette.Canvas,
                $"{mode} Prism effects retain a luminous, uniform beam between the endpoint glows.", assertions);
        }
        Require(first.Zip(quiet, (a, b) => a != b).Count(changed => changed) > 200,
            "Full Prism effects add visible bloom around the aiming core.", assertions);
        var faded = PixelsAt(0, false, start, end);
        Require(faded.All(pixel => pixel == ColorPalette.Canvas),
            "Expired Prism effects leave no luminous residue.", assertions);
        foreach (var length in new[] { 0f, .1f })
        {
            var pixels = PixelsAt(.8f, false, new Vector2(160, 60), new Vector2(160 + length, 60));
            Require(pixels.Where((pixel, index) => pixel != ColorPalette.Canvas &&
                    (Math.Abs(index % 640 - 320) > 40 || Math.Abs(index / 640 - 120) > 40)).Count() == 0 &&
                    pixels.Any(pixel => pixel != ColorPalette.Canvas),
                $"A {length:0.#}-pixel Prism beam produces only a compact local flash.", assertions);
        }

        using (var gallery = new RenderTarget2D(GraphicsDevice, 1600, 1000,
                   false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
        {
            GraphicsDevice.SetRenderTarget(gallery);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            Label("SIEGE MORTAR / CHARTREUSE", new Vector2(32, 16), .65f);
            _primitives.FillRect(_batch, new Rectangle(24, 51, 752, 92), ColorPalette.Panel);
            for (var index = 0; index < 3; index++)
                MortarShellArt.Draw(_batch, _primitives, new Vector2(190 + index * 210, 97),
                    Vector2.Normalize(new Vector2(1, -.16f)), 7, ColorPalette.MortarShot, index * .18f, false);
            Label("PRISM BEAM / VIOLET", new Vector2(32, 165), .65f);
            for (var index = 0; index < 4; index++)
            {
                var y = 209 + index * 68;
                var age = index == 3 ? .3f : index * .25f;
                _primitives.FillRect(_batch, new Rectangle(24, y, 752, 56), ColorPalette.Panel);
                Label(index == 3 ? "REDUCED" : $"{age * 150:0} ms", new Vector2(36, y + 18), .4f);
                PrismBeamArt.Draw(_batch, _primitives, new Vector2(160, y + 28), new Vector2(735, y + 28),
                    accent, 2, 1 - age, index == 3);
            }
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            const string fileName = "31-mortar-prism-signatures.png";
            var path = Path.Combine(_outputDirectory, fileName);
            using (var stream = File.Create(path)) gallery.SaveAsPng(stream, gallery.Width, gallery.Height);
            var pixels = new Color[gallery.Width * gallery.Height];
            gallery.GetData(pixels);
            scenes.Add(new VisualVerificationScene(fileName, gallery.Width, gallery.Height,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixels.Count(pixel => pixel != ColorPalette.Canvas)));
        }

        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("prism_beam", new Vector2(245, 380)),
            "The Prism showcase uses a legal emitter position.", assertions);
        session.CancelPlacement();
        var emitter = session.Towers.Single();
        emitter.TickVisual(.3f);
        emitter.OnFired();
        session.SpawnEnemy("t3_brute", 1, 1);
        var target = session.Enemies.Single();
        target.SetSandboxPathDistance(538, session.Map.Path);
        TowerBehaviorRegistry.Create("beam").Attack(new TowerInstanceContext
            { Session = session, Tower = session.Towers.Single(), Target = target });
        var beam = session.Effects.Effects.Single(effect => effect.Kind == EffectKind.Beam);
        Require(beam.BeamStyle == BeamStyle.Prism && beam.SourceTowerId == emitter.Id,
            "Prism attacks identify their optical effect and firing tower.", assertions);
        beam.Remaining = .11f;
        var checksum = SessionChecksum.Compute(session, 0);
        var beamUi = new UIManager(font, display);
        ConfigureUi(beamUi, content);
        scenes.Add(Capture("31-prism-beam-battlefield.png", beamUi, GameState.Playing, session));
        Require(SessionChecksum.Compute(session, 0) == checksum && beam.Remaining == .11f,
            "Prism rendering leaves synchronized state and effect lifetime unchanged.", assertions);
        target.SetSandboxPathDistance(350, session.Map.Path);
        beam.Remaining = .07f;
        var presentation = PresentationFrame.Create(session, 0);
        var shotDirection = Vector2.Normalize(beam.End - beam.Start);
        var shotAngle = MathF.Atan2(shotDirection.Y, shotDirection.X);
        Require(MathF.Abs(MathHelper.WrapAngle(GameRenderer.TowerAim(session, emitter, presentation) - shotAngle)) < .0001f,
            "A visible Prism beam holds its barrel on the fired ray after target movement.", assertions);
        var expectedMuzzle = emitter.Position + shotDirection * emitter.Definition.Visual.Radius *
            presentation.TowerScale(emitter) * PrismBeamArt.BarrelLength;
        Require(Vector2.Distance(GameRenderer.PrismBeamOrigin(session, beam, presentation), expectedMuzzle) < .001f &&
                beam.Start == emitter.Position,
            "The visible Prism ray begins at the scaled barrel tip without changing its stored shot.", assertions);
        scenes.Add(Capture("31a-prism-retargeting.png", beamUi, GameState.Playing, session));
        beam.Remaining = 0;
        var nextAngle = MathF.Atan2(target.Position.Y - emitter.Position.Y, target.Position.X - emitter.Position.X);
        Require(MathF.Abs(MathHelper.WrapAngle(GameRenderer.TowerAim(session, emitter, presentation) - nextAngle)) < .0001f,
            "Prism aiming resumes when the visible beam expires.", assertions);
        scenes.Add(Capture("31b-prism-retargeted-after-beam.png", beamUi, GameState.Playing, session));
        beam.Remaining = .07f;
        target.ApplyHealthDamage(target.MaxHealth);
        Require(target.IsDead && MathF.Abs(MathHelper.WrapAngle(GameRenderer.TowerAim(session, emitter, presentation) - shotAngle)) < .0001f,
            "A killed target does not detach its fading Prism beam from the barrel.", assertions);
        scenes.Add(Capture("31c-prism-after-target-death.png", beamUi, GameState.Playing, session));
        session.Effects.AddBeam(beam.End, beam.End + new Vector2(30, 40), accent, .12f, BeamStyle.Prism);
        var chained = session.Effects.Effects.Last();
        Require(chained.Start == beam.End && GameRenderer.PrismBeamOrigin(session, chained, presentation) == chained.Start &&
                GameRenderer.ActivePrismBeam(session, emitter.Id, presentation) == beam,
            "Prism chain links keep their enemy origins and do not redirect the emitter.", assertions);
        session.Effects.AddBeam(emitter.Position, emitter.Position + Vector2.UnitX * 100, accent, .15f,
            BeamStyle.Prism, emitter.Id);
        Require(GameRenderer.ActivePrismBeam(session, emitter.Id, presentation) == session.Effects.Effects.Last() &&
                MathF.Abs(GameRenderer.TowerAim(session, emitter, presentation)) < .0001f,
            "A fresh Prism shot takes visual priority over an older fading shot.", assertions);

        void Label(string text, Vector2 position, float scale) => _batch.DrawString(font, text, position,
            ColorPalette.Paper, 0, Vector2.Zero, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);

        Color[] PixelsAt(float progress, bool quiet, Vector2 from, Vector2 to)
        {
            using var target = new RenderTarget2D(GraphicsDevice, 640, 240,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(2));
            PrismBeamArt.Draw(_batch, _primitives, from, to, accent, 2, progress, quiet);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            return pixels;
        }
    }

    private void AssertPersistentProtocolControls(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        session.ConfigureCoOp(2);
        Require(session.TryPlaceTower("needle_turret", new Vector2(190, 175), 2),
            "Protocol controls have a placed tower to arm.", assertions);
        var tower = session.SelectedTower!;
        var hud = new UIManager(font, display);
        ConfigureUi(hud, content);
        RenderPixels(hud, GameState.Playing, session);
        var manualBounds = hud.ProtocolButtonBounds;
        var autoBounds = hud.AutoProtocolButtonBounds;
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var ready = RenderPixels(hud, GameState.Playing, session);
        Require(manualBounds.Width > 0 && autoBounds.Width > 0 && hud.ProtocolButtonBounds == manualBounds &&
                hud.AutoProtocolButtonBounds == autoBounds,
            "Protocol controls keep their layout when tower selection is cleared.", assertions);
        scenes.Add(Capture("05b-protocol-unselected-ready.png", hud, GameState.Playing, session));
        var commands = new List<GameCommand>();
        hud.HandleGameplayInput(Pointer(0, 0) with { OverdrivePressed = true, AutoProtocolPressed = true },
            session, commands.Add, 2);
        Require(commands.Count == 0 && session.SelectedTower is null,
            "Unselected, unarmed Protocol controls cannot activate or arm a tower.", assertions);
        Require(session.TryToggleAutoProtocol(tower.Id, 2), "The protocol tower can be armed.", assertions);
        session.BeginPlacement("frost_spire");
        var checksum = SessionChecksum.Compute(session, 0);
        hud.HandleGameplayInput(Pointer(0, 0) with { AutoProtocolPressed = true }, session, commands.Add, 2);
        Require(session.SelectedTower == tower && session.PlacementTowerId is null && commands.Count == 0 &&
                session.AutoOverdriveTowerId == tower.Id && SessionChecksum.Compute(session, 0) == checksum,
            "A selects the armed tower and cancels placement without changing shared automation.", assertions);
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        RenderPixels(hud, GameState.Playing, session);
        scenes.Add(Capture("05c-protocol-unselected-armed.png", hud, GameState.Playing, session));
        hud.HandleGameplayInput(Pointer(autoBounds.Center.X, autoBounds.Center.Y, true), session, commands.Add, 2);
        Require(session.SelectedTower == tower && commands.Count == 0 && session.AutoOverdriveTowerId == tower.Id,
            "Clicking ARMED selects its tower without disarming it.", assertions);
        hud.HandleGameplayInput(Pointer(0, 0) with { AutoProtocolPressed = true }, session, commands.Add, 2);
        Require(commands.Count == 1 && commands[0].Type == GameCommandType.ToggleAutoProtocol &&
                commands[0].EntityId == tower.Id && session.AutoOverdriveTowerId == tower.Id,
            "Disarming the selected tower follows the co-op command path.", assertions);
        Require(session.TryOverdriveTower(tower.Id, 2) && session.OverdriveCooldownRemaining > 0,
            "Manual activation starts the shared Protocol cooldown.", assertions);
        var selectedActive = RenderPixels(hud, GameState.Playing, session);
        session.HandleInspectionInput(Pointer(0, 0) with { RightPressed = true });
        var unselectedActive = RenderPixels(hud, GameState.Playing, session);
        Require(CountChangedPixels(selectedActive, unselectedActive, manualBounds) == 0,
            "An active Protocol retains its name, duration and progress strip when selection is cleared.", assertions);
        scenes.Add(Capture("05d1-protocol-unselected-active.png", hud, GameState.Playing, session));
        for (var step = 0; step < (int)MathF.Ceiling(tower.Protocol.DurationSeconds * 10) + 1; step++)
            session.Update(.1f);
        Require(!tower.IsOverdriven && session.OverdriveCooldownRemaining > 0,
            "Active duration expires before the shared cooldown finishes.", assertions);
        var cooling = RenderPixels(hud, GameState.Playing, session);
        Require(CountChangedPixels(unselectedActive, cooling, manualBounds) > 80,
            "The unselected Protocol display transitions from active duration to cooldown.", assertions);
        Require(CountChangedPixels(ready, cooling, manualBounds) > 80,
            "The manual Protocol button displays cooldown without a tower selection.", assertions);
        scenes.Add(Capture("05d-protocol-unselected-cooldown.png", hud, GameState.Playing, session));
    }

    private void AssertMortarShellPresentation(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var region = new Rectangle(600, 330, 80, 60);
        var first = PixelsAt(0, false, Vector2.UnitX);
        var later = PixelsAt(.19f, false, Vector2.UnitX);
        var reduced = PixelsAt(0, true, Vector2.UnitX);
        var reducedLater = PixelsAt(.19f, true, Vector2.UnitX);
        var turned = PixelsAt(0, true, -Vector2.UnitY);
        Require(CountChangedPixels(first, later, region) > 30,
            "Mortar charges pulse and animate their energy wake during flight.", assertions);
        Require(CountChangedPixels(reduced, reducedLater, region) == 0,
            "Reduced Effects freezes shell cosmetics while retaining the authored silhouette.", assertions);
        Require(CountChangedPixels(reduced, turned, region) > 80,
            "The mortar charge has a directional silhouette that follows its heading.", assertions);
        foreach (var (name, time, quiet) in new[] { ("flight", 0f, false), ("shimmer", .19f, false), ("reduced", 0f, true) })
        {
            var fileName = $"29-mortar-shell-{name}.png";
            var path = Path.Combine(_outputDirectory, fileName);
            using var target = RenderShell(time, quiet, Vector2.Normalize(new Vector2(1, -.35f)), true);
            var pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
            scenes.Add(new VisualVerificationScene(fileName, target.Width, target.Height,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixels.Count(pixel => pixel != ColorPalette.Canvas)));
        }

        var session = new GameSession(content, "foundry_loop", "normal", "sandbox_lab");
        Require(session.TryPlaceTower("siege_mortar", new Vector2(245, 380)),
            "The mortar flight showcase uses a legal artillery emplacement.", assertions);
        session.CancelPlacement();
        var sourceId = session.Towers.Single().Id;
        for (var index = 0; index < 3; index++)
            session.Projectiles.Add(new ProjectileInstance(new Vector2(355 + index * 130, 352 - index * 42),
                new Vector2(820, 215), null, 230, ProjectileKind.ImpactPoint, 40,
                new DamagePayload { Damage = 40, SourceTowerId = sourceId }, ColorPalette.MortarShot, 7));
        var shellUi = new UIManager(font, display);
        ConfigureUi(shellUi, content);
        var checksum = SessionChecksum.Compute(session, 0);
        scenes.Add(Capture("29-mortar-shell-battlefield.png", shellUi, GameState.Playing, session));
        Require(SessionChecksum.Compute(session, 0) == checksum,
            "Shell rendering leaves projectile motion, damage, and synchronized state unchanged.", assertions);

        Color[] PixelsAt(float time, bool quiet, Vector2 direction)
        {
            using var target = RenderShell(time, quiet, direction, false);
            var pixels = new Color[target.Width * target.Height];
            target.GetData(pixels);
            return pixels;
        }

        RenderTarget2D RenderShell(float time, bool quiet, Vector2 direction, bool closeUp)
        {
            HideAndDisableActivation();
            var width = closeUp ? 512 : GameConstants.RenderWidth;
            var height = closeUp ? 512 : GameConstants.RenderHeight;
            var target = new RenderTarget2D(GraphicsDevice, width, height,
                false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(ColorPalette.Canvas);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                null, null, null, Matrix.CreateScale(closeUp ? 8 : GameConstants.RenderScale));
            MortarShellArt.Draw(_batch, _primitives, closeUp ? new Vector2(32) : new Vector2(640, 360),
                direction, 7, ColorPalette.MortarShot, time, quiet);
            _batch.End();
            GraphicsDevice.SetRenderTarget(null);
            return target;
        }
    }

    private void AssertPathMarkingsCrossCorners(ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var cornerRegion = new Rectangle(276, 280, 48, 48);
        var incomingLeg = new Rectangle(282, 297, 16, 6);
        var outgoingLeg = new Rectangle(297, 302, 6, 16);
        foreach (var style in new[] { "foundry", "trail", "prism", "surge" })
        {
            var map = new MapDefinition
            {
                PathWidth = 48,
                PathVisual = new PathVisualData { Style = style },
                Path = [new PointData { X = 200, Y = 300 }, new PointData { X = 300, Y = 300 },
                    new PointData { X = 300, Y = 400 }]
            };
            var speed = style == "foundry" ? 15f : 28f;
            var reference = PixelsAt(42 / speed);
            var before = PixelsAt(84 / speed);
            var crossing = PixelsAt(94 / speed);
            var after = PixelsAt(104 / speed);
            var beforeCoverage = CountChangedPixels(reference, before, cornerRegion);
            var crossingCoverage = CountChangedPixels(reference, crossing, cornerRegion);
            var afterCoverage = CountChangedPixels(reference, after, cornerRegion);
            Require(CountChangedPixels(reference, crossing, incomingLeg) > 12 &&
                    CountChangedPixels(reference, crossing, outgoingLeg) > 12,
                $"{style}: a moving path marking occupies both legs while crossing a right-angle corner.", assertions);
            Require(CountChangedPixels(reference, before, incomingLeg) > 24 &&
                    CountChangedPixels(reference, before, outgoingLeg) == 0 &&
                    CountChangedPixels(reference, after, incomingLeg) == 0 &&
                    CountChangedPixels(reference, after, outgoingLeg) > 24,
                $"{style}: path markings advance continuously from the incoming to the outgoing corner leg.", assertions);
            var straightCoverage = (beforeCoverage + afterCoverage) / 2f;
            Require(straightCoverage > 50 && crossingCoverage >= straightCoverage * .75f &&
                    crossingCoverage <= straightCoverage * 1.35f,
                $"{style}: corner marking coverage remains stable ({beforeCoverage}/{crossingCoverage}/{afterCoverage} pixels before/during/after).", assertions);
            Require(CountChangedPixels(before, after, cornerRegion) > 50,
                $"{style}: center path markings visibly move over time.", assertions);
            if (style is "foundry" or "trail")
            {
                var straightBrightness = (CoreBrightness(before, new Point(290, 300)) +
                                          CoreBrightness(after, new Point(300, 310))) / 2;
                var cornerBrightness = CoreBrightness(crossing, new Point(300, 300));
                Require(MathF.Abs(cornerBrightness - straightBrightness) <= MathF.Max(5, straightBrightness * .12f),
                    $"{style}: the corner join retains the straight marking's brightness ({cornerBrightness:0.#}/{straightBrightness:0.#}).", assertions);
                scenes.Add(CapturePathCorner($"28-{style}-corner-before.png", map, 84 / speed));
                scenes.Add(CapturePathCorner($"28-{style}-corner-crossing.png", map, 94 / speed));
                scenes.Add(CapturePathCorner($"28-{style}-corner-after.png", map, 104 / speed));
            }

            Color[] PixelsAt(float time)
            {
                using var target = RenderIsolatedPath(map, time);
                var pixels = new Color[target.Width * target.Height];
                target.GetData(pixels);
                return pixels;
            }
        }

        static float CoreBrightness(IReadOnlyList<Color> pixels, Point logicalCenter)
        {
            var scale = GameConstants.RenderScale;
            var left = logicalCenter.X * scale - scale / 2;
            var top = logicalCenter.Y * scale - scale / 2;
            var brightness = 0f;
            for (var y = top; y < top + scale; y++)
            for (var x = left; x < left + scale; x++)
            {
                var color = pixels[y * GameConstants.RenderWidth + x];
                brightness += color.R * .2126f + color.G * .7152f + color.B * .0722f;
            }
            return brightness / (scale * scale);
        }
    }

    private void AssertSubpixelPathCaps(ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        var corner = new Vector2(300, 300);
        var directions = new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY };
        foreach (var style in new[] { "foundry", "trail", "prism", "surge" })
        {
            var speed = style == "foundry" ? 15f : 28f;
            var radius = style == "foundry" ? 2f : 1f;
            for (var direction = 0; direction < directions.Length; direction++)
            foreach (var turn in new[] { -1, 1 })
            {
                var incoming = directions[direction];
                var outgoing = directions[(direction + turn + directions.Length) % directions.Length];
                var start = corner - incoming * 100;
                var end = corner + outgoing * 100;
                var map = new MapDefinition
                {
                    PathWidth = 48,
                    PathVisual = new PathVisualData { Style = style },
                    Path = [new PointData { X = start.X, Y = start.Y }, new PointData { X = corner.X, Y = corner.Y },
                        new PointData { X = end.X, Y = end.Y }]
                };
                var reference = PixelsAt(42 / speed);
                var entry = PixelsAt(88.25f / speed);
                var exiting = PixelsAt(99.75f / speed);
                var departed = PixelsAt(100.25f / speed);
                var entryInk = PatchColor(entry, corner - incoming * 6);
                var exitInk = PatchColor(exiting, corner + outgoing * 6);
                var departedInk = PatchColor(departed, corner + outgoing * 6);
                var entryCap = PatchCoverage(reference, entry,
                    corner + incoming * (.35f * radius) + outgoing * (.55f * radius), entryInk);
                var exitCap = PatchCoverage(reference, exiting,
                    corner + incoming * (.35f * radius) - outgoing * (.55f * radius), exitInk);
                var departedCap = PatchCoverage(reference, departed,
                    corner + incoming * (.25f * radius) - outgoing * (.35f * radius), departedInk);
                var minimumContrast = style is "foundry" or "trail" ? 12 : 0;
                Require(entryCap.Expected >= minimumContrast && exitCap.Expected >= minimumContrast &&
                        departedCap.Expected >= minimumContrast &&
                        entryCap.Observed >= entryCap.Expected * .45f && exitCap.Observed >= exitCap.Expected * .45f &&
                        departedCap.Observed >= departedCap.Expected * .45f,
                    $"{style}: subpixel fragments retain full-width caps for direction {direction}, turn {turn} " +
                    $"(observed/expected local contrast: entry {entryCap.Observed:0.#}/{entryCap.Expected:0.#}, " +
                    $"exit {exitCap.Observed:0.#}/{exitCap.Expected:0.#}, departed {departedCap.Observed:0.#}/{departedCap.Expected:0.#}).", assertions);

                if (direction == 0 && turn == 1 && style is ("foundry" or "trail"))
                {
                    scenes.Add(CapturePathCorner($"29-{style}-corner-head-entry.png", map, 88.25f / speed));
                    scenes.Add(CapturePathCorner($"29-{style}-corner-tail-approaching.png", map, 99.75f / speed));
                    scenes.Add(CapturePathCorner($"29-{style}-corner-tail-at-turn.png", map, 100 / speed));
                    scenes.Add(CapturePathCorner($"29-{style}-corner-tail-departed.png", map, 100.25f / speed));
                }

                Color[] PixelsAt(float time)
                {
                    using var target = RenderIsolatedPath(map, time, closeUp: true);
                    var pixels = new Color[target.Width * target.Height];
                    target.GetData(pixels);
                    return pixels;
                }
            }
        }

        static Vector3 PatchColor(IReadOnlyList<Color> pixels, Vector2 logicalPoint)
        {
            var left = (int)((logicalPoint.X - 268) * 8) - 1;
            var top = (int)((logicalPoint.Y - 268) * 8) - 1;
            var color = Vector3.Zero;
            for (var y = top; y < top + 2; y++)
            for (var x = left; x < left + 2; x++)
            {
                var pixel = pixels[y * 512 + x];
                color += new Vector3(pixel.R, pixel.G, pixel.B);
            }
            return color / 4;
        }

        static (float Observed, float Expected) PatchCoverage(IReadOnlyList<Color> baseline,
            IReadOnlyList<Color> marked, Vector2 logicalPoint, Vector3 ink)
        {
            var left = (int)((logicalPoint.X - 268) * 8) - 1;
            var top = (int)((logicalPoint.Y - 268) * 8) - 1;
            var observed = 0f;
            var expected = 0f;
            for (var y = top; y < top + 2; y++)
            for (var x = left; x < left + 2; x++)
            {
                var before = baseline[y * 512 + x];
                var after = marked[y * 512 + x];
                observed += Math.Max(Math.Abs(after.R - before.R),
                    Math.Max(Math.Abs(after.G - before.G), Math.Abs(after.B - before.B)));
                expected += MathF.Max(MathF.Abs(ink.X - before.R),
                    MathF.Max(MathF.Abs(ink.Y - before.G), MathF.Abs(ink.Z - before.B)));
            }
            return (observed / 4, expected / 4);
        }
    }

    private RenderTarget2D RenderIsolatedPath(MapDefinition map, float time, bool closeUp = false)
    {
        HideAndDisableActivation();
        var width = closeUp ? 512 : GameConstants.RenderWidth;
        var height = closeUp ? 512 : GameConstants.RenderHeight;
        var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Canvas);
        var transform = closeUp
            ? Matrix.CreateTranslation(-268, -268, 0) * Matrix.CreateScale(8)
            : Matrix.CreateScale(GameConstants.RenderScale);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, transform);
        MapEnvironmentRenderer.Path(_batch, _primitives, map, time);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        return target;
    }

    private VisualVerificationScene CapturePathCorner(string fileName, MapDefinition map, float time)
    {
        var path = Path.Combine(_outputDirectory, fileName);
        using var target = RenderIsolatedPath(map, time, closeUp: true);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, target.Width, target.Height);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new VisualVerificationScene(fileName, target.Width, target.Height, hash,
            pixels.Count(pixel => pixel != ColorPalette.Paper));
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
        MeasureMenuHeading(fileName, ui, state, session, pixels, GameConstants.RenderWidth, GameConstants.RenderHeight,
            GameConstants.RenderScale);
        return new VisualVerificationScene(fileName, GameConstants.RenderWidth, GameConstants.RenderHeight, hash, nonPaperPixels);
    }

    private VisualVerificationScene CaptureAtRenderScale(string fileName, UIManager ui, GameState state,
        GameSession? session, int renderScale)
    {
        HideAndDisableActivation();
        renderScale = Math.Clamp(renderScale, GameConstants.RenderScale, GameConstants.MaximumRenderScale);
        var width = GameConstants.LogicalWidth * renderScale;
        var height = GameConstants.LogicalHeight * renderScale;
        var path = Path.Combine(_outputDirectory, fileName);
        using var primitives = new PrimitiveRenderer(GraphicsDevice, renderScale);
        using var target = new RenderTarget2D(GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(renderScale));
        if (session is not null) _renderer.Draw(_batch, primitives, session,
            foregroundTowerId: state == GameState.Playing ? ui.RemoteCoOpSelectedTowerId : 0);
        ui.Draw(_batch, primitives, state, session, Matrix.CreateScale(renderScale));
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);

        var pixels = new Color[width * height];
        target.GetData(pixels);
        using (var stream = File.Create(path)) target.SaveAsPng(stream, width, height);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var nonPaperPixels = pixels.Count(pixel => pixel != ColorPalette.Paper);
        if (nonPaperPixels < 4_500)
            throw new InvalidOperationException($"Visual scene '{fileName}' rendered unexpectedly blank.");
        MeasureMenuHeading(fileName, ui, state, session, pixels, width, height, renderScale);
        return new VisualVerificationScene(fileName, width, height, hash, nonPaperPixels);
    }

    private SimulationLayoutScene CaptureSimulationLayout(GameContent content, UIManager ui, string fileName,
        string mapId, string difficultyId, string challengeId, AutoPlayerStrategy strategy, int seed,
        bool expectTargetReached, List<string> assertions, int targetWave = GameConstants.CampaignWaveCount)
    {
        var execution = HeadlessSimulation.RunForDiagnostics(content, new SimulationOptions
        {
            MapId = mapId,
            DifficultyId = difficultyId,
            ChallengeId = challengeId,
            Strategy = strategy,
            Seed = seed,
            MaximumWave = targetWave
        });
        Require(execution.Result.Won == expectTargetReached,
            $"The deterministic {mapId} {strategy} sample " +
            $"{(expectTargetReached ? "reaches" : "does not reach")} wave {targetWave}.", assertions);
        var poweredTowers = execution.Session.Towers.Count(tower =>
            execution.Session.Map.GetPowerBuff(tower.Position).IsPowered);
        var orderedTowers = execution.Session.Towers.OrderBy(tower => tower.Id).ToArray();
        var openingNodeTowers = orderedTowers.Take(10).Count(tower =>
            execution.Session.Map.GetPowerBuff(tower.Position).IsPowered);
        var occupiedNodes = execution.Session.Map.Definition.PowerNodes.Count(node =>
            orderedTowers.Any(tower => Vector2.DistanceSquared(tower.Position, node.Position.ToVector2()) <= node.Radius * node.Radius));
        var history = RunHistoryEntry.FromSession(execution.Session);
        var inspection = history.CreateInspectionSession(content);
        var scene = Capture(fileName, ui, GameState.RunHistoryField, inspection);
        assertions.Add($"{fileName}: {execution.Result.Result} wave {execution.Result.WaveReached}, " +
                       $"{inspection.Towers.Count} final towers, {poweredTowers} on Surge Nodes, " +
                       $"{openingNodeTowers}/10 opening towers node-powered, {occupiedNodes} distinct nodes occupied.");
        return new SimulationLayoutScene(scene, poweredTowers, openingNodeTowers, occupiedNodes);
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
        ui.Draw(_batch, _primitives, state, session, Matrix.CreateScale(GameConstants.RenderScale));
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

    private static Vector2? ColorPixelCenter(IReadOnlyList<Color> pixels, Rectangle logicalRegion, Color color)
    {
        var left = Math.Max(0, logicalRegion.Left * GameConstants.RenderScale);
        var top = Math.Max(0, logicalRegion.Top * GameConstants.RenderScale);
        var right = Math.Min(GameConstants.RenderWidth, logicalRegion.Right * GameConstants.RenderScale);
        var bottom = Math.Min(GameConstants.RenderHeight, logicalRegion.Bottom * GameConstants.RenderScale);
        var count = 0;
        var sum = Vector2.Zero;
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var pixel = pixels[y * GameConstants.RenderWidth + x];
            if (pixel.R != color.R || pixel.G != color.G || pixel.B != color.B) continue;
            sum += new Vector2(x + 0.5f, y + 0.5f) / GameConstants.RenderScale;
            count++;
        }
        return count == 0 ? null : sum / count;
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
    private sealed record SimulationLayoutScene(
        VisualVerificationScene Scene,
        int PoweredTowers,
        int OpeningNodeTowers,
        int OccupiedNodes);

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
