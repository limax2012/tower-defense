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
        Require(!UIManager.HudThreatBounds.Intersects(UIManager.HudRunSetupBounds),
            "The active threat summary cannot enter the Run Setup region.", assertions);
        var baseline = RenderPixels(ui, GameState.Playing, session);
        var remotePosition = new Vector2(245, 380);
        ui.SetRemoteCoOpCursor(remotePosition, 2, placementTowerId: "needle_turret");
        scenes.Add(Capture("03-remote-tower-placement.png", ui, GameState.Playing, session));
        var withGhost = RenderPixels(ui, GameState.Playing, session);
        var changedGhostPixels = CountChangedPixels(baseline, withGhost,
            new Rectangle((int)remotePosition.X - 40, (int)remotePosition.Y - 40, 80, 80));
        Require(changedGhostPixels >= 500,
            $"Remote placement ghost changes a visible cursor-region footprint ({changedGhostPixels} pixels).", assertions);

        var comparisonSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        Require(comparisonSession.TryPlaceTower("needle_turret", new Vector2(45, 200)),
            "Upgrade comparison scene places a selected Needle Turret.", assertions);
        scenes.Add(Capture("04a-current-stat-grid.png", ui, GameState.Playing, comparisonSession));
        ui.HandleGameplayInput(Pointer(1170, 700), comparisonSession);
        scenes.Add(Capture("04b-upgrade-old-to-new.png", ui, GameState.Playing, comparisonSession));
        var calibratedFeed = content.Towers["needle_turret"].Tier2Doctrines
            .Single(doctrine => doctrine.Id == "needle_calibrator");
        var calibratedStats = TowerInfo.ComparisonStats(content.Towers["needle_turret"],
            content.Towers["needle_turret"].Levels[0],
            content.Towers["needle_turret"].Levels[1].WithDoctrine(calibratedFeed));
        Require(calibratedStats.Single(stat => stat.Label == "RATE").Direction == TowerStatDirection.Unchanged,
            "Calibrated Feed does not color an unchanged displayed rate as an increase.", assertions);
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
        var withoutAutoMarker = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        Require(autoHeaderSession.TryToggleAutoProtocol(autoTower.Id, 2),
            "Auto header scene arms the earlier tower beneath its later neighbor.", assertions);
        autoHeaderSession.HandleInspectionInput(Pointer(190, 175, leftPressed: true));
        var withAutoMarker = RenderPixels(ui, GameState.Playing, autoHeaderSession);
        var changedAutoMarkerPixels = CountChangedPixels(withoutAutoMarker, withAutoMarker,
            new Rectangle(150, 135, 120, 80));
        Require(changedAutoMarkerPixels >= 1_000,
            $"Arming Auto adds a prominent battlefield marker ({changedAutoMarkerPixels} changed pixels).", assertions);
        Require(autoHeaderSession.TryToggleAutoProtocol(laterTower.Id, 2) &&
                autoHeaderSession.AutoOverdriveTowerId == laterTower.Id &&
                autoHeaderSession.TryToggleAutoProtocol(autoTower.Id, 2) &&
                autoHeaderSession.AutoOverdriveTowerId == autoTower.Id,
            "Moving Auto transfers foreground priority; the previous tower immediately returns to normal order.", assertions);
        ui.HandleGameplayInput(Pointer(0, 0), autoHeaderSession, _ => { }, 2);
        scenes.Add(Capture("05-auto-coop-owner.png", ui, GameState.Playing, autoHeaderSession));
        scenes.Add(Capture("06-protocol-auto-library.png", ui, GameState.TowerLibrary, null));
        Require(autoHeaderSession.StartNextWave(), "Live co-op header scene starts an active wave.", assertions);
        scenes.Add(Capture("07-active-coop-header.png", ui, GameState.Playing, autoHeaderSession));
        Require(ui.HandleGameplayInput(Pointer(0, 0) with { TabPressed = true }, autoHeaderSession, _ => { }, 2) == UiAction.None &&
                ui.IsGameplayOverlayOpen && !autoHeaderSession.IsCoOpPaused,
            "Tab opens the Tactical Library over an unpaused co-op wave without pausing it.", assertions);
        scenes.Add(Capture("08-live-coop-library.png", ui, GameState.Playing, autoHeaderSession));
        _ = ui.HandleGameplayInput(Pointer(0, 0) with { EscapePressed = true }, autoHeaderSession, _ => { }, 2);
        Require(autoHeaderSession.SetCoOpPaused(true, 1),
            "Shared-pause visual scene enters authoritative pause.", assertions);
        Require(!UIManager.CoOpPauseResumeBounds.Intersects(UIManager.CoOpPauseLibraryBounds) &&
                !UIManager.CoOpPauseLibraryBounds.Intersects(UIManager.CoOpPauseRestartBounds) &&
                !UIManager.CoOpPauseRestartBounds.Intersects(UIManager.CoOpPauseMenuBounds) &&
                UIManager.CoOpPauseResumeBounds.X >= GameConstants.SidebarX &&
                UIManager.CoOpPauseMenuBounds.Right <= GameConstants.LogicalWidth,
            "Compact co-op pause controls are separated and contained entirely in the sidebar.", assertions);
        scenes.Add(Capture("09-compact-coop-pause.png", ui, GameState.Playing, autoHeaderSession));

        var beaconSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        beaconSession.Economy.AddCredits(1_000);
        _ = RenderPixels(ui, GameState.Playing, beaconSession);
        _ = ui.HandleGameplayInput(Pointer(1042, 379, leftPressed: true), beaconSession);
        scenes.Add(Capture("10a-signal-beacon-placement-intel.png", ui, GameState.Playing, beaconSession));
        Require(beaconSession.TryPlaceTower("signal_beacon", new Vector2(45, 200)),
            "Signal Beacon contrast scene places the support tower.", assertions);
        _ = RenderPixels(ui, GameState.Playing, beaconSession);
        _ = ui.HandleGameplayInput(Pointer(1170, 664), beaconSession);
        scenes.Add(Capture("10-signal-beacon-old-to-new.png", ui, GameState.Playing, beaconSession));
        _ = ui.HandleGameplayInput(Pointer(0, 0), beaconSession);
        Require(beaconSession.TryChooseTowerDoctrine(beaconSession.SelectedTower!.Id, "beacon_amplifier"),
            "Signal Beacon contrast scene reaches its final choices.", assertions);
        var beaconFill = content.Towers["signal_beacon"].Visual.PrimaryColor;
        var beaconText = ColorPalette.HighContrastText(beaconFill);
        Require(beaconText == ColorPalette.Ink && ColorPalette.ContrastRatio(beaconText, beaconFill) >= 7f,
            "Signal Beacon's pale upgrade control receives accessible dark text.", assertions);
        scenes.Add(Capture("10b-signal-beacon-upgrade-contrast.png", ui, GameState.Playing, beaconSession));

        var prismSession = new GameSession(content, "foundry_loop", DifficultyCatalog.DefaultId, ChallengeCatalog.DefaultId);
        prismSession.Economy.AddCredits(2_000);
        Require(prismSession.TryPlaceTower("prism_beam", new Vector2(45, 200)) &&
                prismSession.TryChooseTowerDoctrine(prismSession.SelectedTower!.Id, "prism_frequency"),
            "Dense Intel scene advances Prism Beam to its final-role previews.", assertions);
        _ = RenderPixels(ui, GameState.Playing, prismSession);
        _ = ui.HandleGameplayInput(Pointer(1170, 664), prismSession);
        scenes.Add(Capture("10c-prism-shield-old-to-new.png", ui, GameState.Playing, prismSession));

        var settings = new UserSettings { AutoStartWaves = true };
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
                new RunHistoryTowerEntry { TowerId = "frost_spire", DisplayName = "Frost Spire", Purchases = 7, Upgrades = 13, CreditsSpent = 9_800, Hits = 6_800, Kills = 420, ProtocolActivations = 20, Damage = 112_000, ControlSeconds = 1_340 }
            ],
            Enemies =
            [
                new RunHistoryEnemyEntry { EnemyId = "bastion_core:boss", DisplayName = "Bastion Core", Kills = 8, Escapes = 1, LivesLost = 12 },
                new RunHistoryEnemyEntry { EnemyId = "t4_aegis", DisplayName = "Aegis", Kills = 890, Escapes = 2, LivesLost = 6 }
            ],
            FinalLayout = RunHistoryLayoutSnapshot.FromSession(comparisonSession)
        };
        ui.ConfigureRunHistory([history]);
        Require(ui.HandleRunHistory(Pointer(400, 150, leftPressed: true)) == UiAction.None && ui.IsRunHistoryDetailOpen,
            "Selecting a history record opens its complete statistics view.", assertions);
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

            foreach (var doctrine in definition.Tier2Doctrines)
            {
                Check(definition.Id, $"PREVIEW {doctrine.DisplayName.ToUpperInvariant()}  {doctrine.UpgradeCost}", 280,
                    UIManager.TowerStatGridMinimumScale);
                Check(definition.Id, $"{doctrine.DisplayName.ToUpperInvariant()} {doctrine.UpgradeCost}", 180, 0.38f);
                var doctrineTower = new TowerInstance(1, definition, Vector2.Zero, 2);
                if (doctrineTower.TryChooseDoctrine(doctrine.Id))
                    Check(definition.Id, $"{TowerInfo.ProgressionLabel(doctrineTower)}   {TowerInfo.ShortRole(definition)}   PLACED P2",
                        228, UIManager.TowerStatGridMinimumScale);

                foreach (var specialization in definition.Specializations)
                {
                    Check(definition.Id, $"PREVIEW {specialization.DisplayName.ToUpperInvariant()}  {specialization.UpgradeCost}", 280,
                        UIManager.TowerStatGridMinimumScale);
                    Check(definition.Id, $"{specialization.DisplayName.ToUpperInvariant()} {specialization.UpgradeCost}", 180, 0.38f);
                    var finalTower = new TowerInstance(1, definition, Vector2.Zero, 2);
                    if (finalTower.TryChooseDoctrine(doctrine.Id) && finalTower.TrySpecialize(specialization.Id))
                        Check(definition.Id, $"{TowerInfo.ProgressionLabel(finalTower)}   {TowerInfo.ShortRole(definition)}   PLACED P2",
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
        if (session is not null) _renderer.Draw(_batch, _primitives, session);
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
