using MinimalBastion.Core;
using MinimalBastion.Analytics;
using MinimalBastion.Data;
using MinimalBastion.Enemies;
using MinimalBastion.Effects;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.Towers;
using MinimalBastion.Tactics;
using MinimalBastion.Waves;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.UI;

public enum UiAction
{
    None,
    OpenSoloSetup,
    OpenCoOpSetup,
    Play,
    TowerLibrary,
    Settings,
    ApplySettings,
    CloseSettings,
    CoOp,
    HostCoOp,
    JoinCoOp,
    Pause,
    Resume,
    SaveGame,
    LoadGame,
    ConfirmSaveSlot,
    HostSavedGame,
    DuplicateSaveSlot,
    DeleteSaveSlot,
    CloseSaveSlots,
    RunHistory,
    ViewRunHistoryField,
    CloseRunHistoryField,
    DeleteRunHistory,
    CloseRunHistory,
    Restart,
    ContinueEndless,
    ViewField,
    ViewResults,
    MainMenu,
    Exit
}

public sealed class UIManager
{
    internal static readonly Rectangle HudThreatBounds = new(820, 6, 140, 44);
    internal static readonly Rectangle HudRunSetupBounds = new(974, 6, 288, 44);
    internal static readonly Rectangle CoOpTacticalTitleBounds = new(980, 56, 178, 34);
    internal static readonly Rectangle CoOpLinkStatusBounds = new(1164, 58, 96, 14);
    internal static readonly Rectangle CoOpReadyStatusBounds = new(1164, 74, 96, 14);
    internal static readonly Rectangle CoOpPauseResumeBounds = new(986, 196, 268, 44);
    internal static readonly Rectangle CoOpPauseLibraryBounds = new(986, 248, 268, 44);
    internal static readonly Rectangle CoOpPauseRestartBounds = new(986, 316, 268, 44);
    internal static readonly Rectangle CoOpPauseMenuBounds = new(986, 368, 268, 44);
    internal static readonly Rectangle MainMenuLeftDefenseBounds = new(32, 70, 235, 594);
    internal static readonly Rectangle MainMenuRightDefenseBounds = new(1013, 70, 235, 594);

    // Intel icons use a fixed header footprint rather than their battlefield
    // size. An 18px body plus the optional 6px ring leaves clear padding from
    // the card border, title, and first detail row.
    private const int TowerIntelIconRadiusCap = 18;
    private static readonly Vector2 TowerIntelIconCenter = new(1000, 503);
    private readonly SpriteFont _font;
    private readonly Dictionary<string, Rectangle> _towerCards = new(StringComparer.OrdinalIgnoreCase);
    private Rectangle _startWaveButton;
    private Rectangle _speedButton;
    private Rectangle _pauseButton;
    private Rectangle _targetButton;
    private readonly Dictionary<TargetMode, Rectangle> _targetModeButtons = new();
    private Rectangle _targetPickerBounds;
    private bool _targetPickerOpen;
    private int _targetPickerTowerId;
    private Rectangle _upgradeButton;
    private Rectangle _sellButton;
    private Rectangle _specializationAButton;
    private Rectangle _specializationBButton;
    private Rectangle _emergencyButton;
    private Rectangle _generatorButton;
    private Rectangle _overdriveButton;
    private Rectangle _autoProtocolButton;
    private Rectangle _sandboxEnemyPreviousButton;
    private Rectangle _sandboxEnemyNextButton;
    private Rectangle _sandboxGroupButton;
    private Rectangle _sandboxRankButton;
    private Rectangle _sandboxHealthButton;
    private Rectangle _sandboxSpawnButton;
    private Rectangle _sandboxClearTowersButton;
    private Rectangle _sandboxResetButton;
    private Rectangle _sandboxProtocolButton;
    private Rectangle _sandboxToggleTowerButton;
    private Rectangle _sandboxRemoveTowerButton;
    private Rectangle _sandboxWavePreviousButton;
    private Rectangle _sandboxWaveNextButton;
    private string? _hoveredTowerCardId;
    private TowerLevelDefinition? _hoveredUpgradePreview;
    private string? _hoveredUpgradePreviewLabel;
    private PowerNodeData? _hoveredPowerNode;
    private readonly List<(string Id, string Name, int PowerNodes, int Challenge, int StartingCredits, string Description, string PathStyle,
        CampaignIntelInfo Campaign, CampaignIntelInfo MasteryCampaign, IReadOnlyList<Vector2> Path, Color PathBase, Color PathAccent)> _maps = new();
    private readonly List<DifficultyDefinition> _difficulties = new();
    private readonly List<ChallengeDefinition> _challenges = new();
    private int _selectedMapIndex;
    private int _selectedDifficultyIndex;
    private int _selectedChallengeIndex;
    private int _sandboxEnemyIndex;
    private int _sandboxGroupIndex;
    private int _sandboxRankIndex;
    private int _sandboxHealthIndex;
    private int _sandboxWaveNumber = 1;
    private TacticalPlacementKind _hoveredTacticalPlacement;
    private string _joinHostInput = "";
    private string _joinCodeInput = "";
    private bool _editingJoinCode;
    private int _coOpMenuSelection;
    private string _coOpLobbyCopyStatus = "CLICK CODE OR CTRL+C TO COPY";
    private int _coOpWaveReadyMask;
    private bool _coOpWaveStartQueued;
    private bool _coOpEarlyBonusQueued;
    private bool _coOpPeerConnected;
    private bool _coOpResyncing;
    private float _coOpLinkSilenceSeconds;
    private Vector2? _remoteCoOpCursor;
    private int _remoteCoOpCursorPlayerId;
    private int _remoteCoOpSelectedTowerId;
    private string _remoteCoOpPlacementTowerId = "";
    private TacticalPlacementKind _remoteCoOpTacticalPlacement;
    private bool _remoteCoOpHasPlacementPreview;
    private Vector2 _remoteCoOpPlacementPreviewPosition;
    private bool _saveAvailable;
    private string _persistenceStatus = "One rolling autosave; manual slots are available between waves.";
    private IReadOnlyList<SaveSlotInfo> _saveSlots = Array.Empty<SaveSlotInfo>();
    private bool _saveSlotWriteMode;
    private int _selectedSaveSlot = 1;
    private int _saveSlotPage;
    private bool _saveSlotDeleteArmed;
    private bool _restartArmed;
    private IReadOnlyList<RunHistoryEntry> _runHistory = Array.Empty<RunHistoryEntry>();
    private string? _selectedRunHistoryId;
    private int _runHistoryPage;
    private bool _runHistoryDeleteArmed;
    private bool _runHistoryDetailOpen;
    private bool _runHistoryCareerOpen;
    private int _careerMedalPage;
    private int _careerAchievementPage;
    private string _runHistoryStatus = "Completed campaigns and endless progress are recorded locally.";
    private bool _readOnlyInspection;
    private bool _archivedLayoutInspection;
    private bool _towerLibraryOpen;
    private float _visualTimeSeconds;
    private MainMenuBattleScene? _mainMenuBattleScene;

    public int RemoteCoOpSelectedTowerId => _remoteCoOpSelectedTowerId;
    internal bool IsTargetPickerOpen => _targetPickerOpen;
    internal Rectangle TargetPickerBounds => _targetPickerBounds;
    internal Rectangle TargetButtonBounds => _targetButton;
    internal Rectangle UpgradeButtonBounds => _upgradeButton;
    internal Rectangle SellButtonBounds => _sellButton;
    internal int CareerMedalPage => _careerMedalPage;
    internal int CareerAchievementPage => _careerAchievementPage;
    internal IReadOnlyDictionary<TargetMode, Rectangle> TargetModeButtonBounds => _targetModeButtons;
    private UserSettings _settings = new();
    private string _settingsStatus = "";
    private bool _setupForCoOp;
    private int _settingsSelection;
    private int _resultMenuSelection;
    private int _towerLibraryIndex;
    private int _towerLibraryDoctrineIndex;
    private int _enemyLibraryIndex;
    private bool _libraryShowsThreats;
    private bool _libraryShowsCampaign;
    private bool _libraryShowsProfiles;
    private bool _libraryShowsSystems;
    private int _campaignLibraryMapIndex;
    private Rectangle _towerLibraryDoctrineAButton;
    private Rectangle _towerLibraryDoctrineBButton;
    private IReadOnlyList<TowerDefinition> _allLibraryTowers = Array.Empty<TowerDefinition>();
    private IReadOnlyList<TowerDefinition> _libraryTowers = Array.Empty<TowerDefinition>();
    private IReadOnlyList<EnemyDefinition> _allLibraryEnemies = Array.Empty<EnemyDefinition>();
    private IReadOnlyList<ThreatLibraryEntry> _libraryThreats = Array.Empty<ThreatLibraryEntry>();
    private IReadOnlyList<(string Id, string Name, int PowerNodes, int Challenge, int StartingCredits, string Description, string PathStyle,
        CampaignIntelInfo Campaign, CampaignIntelInfo MasteryCampaign, IReadOnlyList<Vector2> Path, Color PathBase, Color PathAccent)> _libraryMaps = [];
    private IReadOnlyList<DifficultyDefinition> _libraryDifficulties = Array.Empty<DifficultyDefinition>();
    private IReadOnlyList<ChallengeDefinition> _libraryChallenges = Array.Empty<ChallengeDefinition>();
    private TacticsDefinition _libraryTactics = new();
    private readonly Dictionary<string, IReadOnlyList<CampaignWaveReference>> _libraryCampaignWaves = new(StringComparer.OrdinalIgnoreCase);
    private readonly Rectangle _playButton = new(490, PlatformCapabilities.OnlineCoOp ? 370 : PlatformCapabilities.ExitCommand ? 395 : 420, 300, 42);
    private readonly Rectangle _coOpButton = new(490, 420, 300, 42);
    private readonly Rectangle _continueButton = new(490, PlatformCapabilities.OnlineCoOp ? 470 : PlatformCapabilities.ExitCommand ? 445 : 470, 300, 42);
    private readonly Rectangle _mainMenuLibraryButton = new(490, PlatformCapabilities.OnlineCoOp ? 520 : PlatformCapabilities.ExitCommand ? 495 : 520, 300, 42);
    private readonly Rectangle _mainMenuSettingsButton = new(490, PlatformCapabilities.OnlineCoOp ? 570 : PlatformCapabilities.ExitCommand ? 545 : 570, 300, 42);
    private readonly Rectangle _quitButton = new(490, PlatformCapabilities.OnlineCoOp ? 620 : 595, 300, 42);
    private readonly Rectangle _setupConfirmButton = new(438, 586, 270, 46);
    private readonly Rectangle _setupBackButton = new(722, 586, 120, 46);
    private readonly Rectangle[] _saveSlotRows =
    {
        new(330, 130, 620, 66),
        new(330, 206, 620, 66),
        new(330, 282, 620, 66),
        new(330, 358, 620, 66),
        new(330, 434, 620, 66)
    };
    private readonly Rectangle _saveSlotConfirmButton = new(330, 520, PlatformCapabilities.OnlineCoOp ? 170 : 200, 46);
    private readonly Rectangle _saveSlotHostButton = new(510, 520, 170, 46);
    private readonly Rectangle _saveSlotDuplicateButton = PlatformCapabilities.OnlineCoOp
        ? new Rectangle(690, 520, 130, 46)
        : new Rectangle(540, 520, 200, 46);
    private readonly Rectangle _saveSlotDeleteButton = PlatformCapabilities.OnlineCoOp
        ? new Rectangle(830, 520, 120, 46)
        : new Rectangle(750, 520, 200, 46);
    private readonly Rectangle _saveSlotWriteConfirmButton = new(330, 520, 400, 46);
    private readonly Rectangle _saveSlotWriteDeleteButton = new(740, 520, 210, 46);
    private readonly Rectangle _saveSlotPreviousButton = new(330, 582, 160, 44);
    private readonly Rectangle _saveSlotBackButton = new(500, 582, 280, 44);
    private readonly Rectangle _saveSlotNextButton = new(790, 582, 160, 44);
    private readonly Rectangle _saveSlotHistoryButton = new(990, 62, 190, 38);
    private readonly Rectangle _runHistoryViewButton = new(330, 520, 300, 46);
    private readonly Rectangle _runHistoryDeleteButton = new(640, 520, 310, 46);
    private readonly Rectangle _runHistoryCareerButton = new(990, 62, 190, 38);
    private readonly Rectangle _runHistoryLayoutButton = new(340, 650, 280, 42);
    private readonly Rectangle _runHistoryDetailBackButton = new(660, 650, 280, 42);
    private readonly Rectangle _runHistoryCareerBackButton = new(500, 650, 280, 42);
    private readonly Rectangle _careerMedalPreviousButton = new(340, 406, 28, 25);
    private readonly Rectangle _careerMedalNextButton = new(388, 406, 28, 25);
    private readonly Rectangle _careerAchievementPreviousButton = new(1136, 186, 28, 25);
    private readonly Rectangle _careerAchievementNextButton = new(1196, 186, 28, 25);
    private readonly Rectangle _hostCoOpButton = new(500, 216, 280, 46);
    private readonly Rectangle _joinHostField = new(500, 326, 280, 42);
    private readonly Rectangle _joinCodeField = new(500, 394, 280, 42);
    private readonly Rectangle _joinCoOpButton = new(500, 456, 280, 46);
    private readonly Rectangle _backButton = new(500, 518, 280, 44);
    private readonly Rectangle _coOpLobbyCodeButton = new(500, 270, 280, 64);
    private readonly Rectangle _coOpReconnectCodeButton = new(500, 370, 280, 46);
    private readonly Rectangle _resumeButton = new(500, 226, 280, 42);
    private readonly Rectangle _towerLibraryButton = new(500, 276, 280, 42);
    private readonly Rectangle _pauseSettingsButton = new(500, 326, 280, 42);
    private readonly Rectangle _saveButton = new(500, 376, 280, 42);
    private readonly Rectangle _loadButton = new(500, 426, 280, 42);
    private readonly Rectangle _restartButton = new(500, 476, 280, 42);
    private readonly Rectangle _mainMenuButton = new(500, 526, 280, 42);
    // Compact content-width tabs keep library navigation distinct from both the
    // title and the explanatory row without consuming more vertical space.
    private readonly Rectangle _towerLibraryTowerTabButton = new(624, 42, 84, 30);
    private readonly Rectangle _towerLibraryThreatTabButton = new(718, 42, 88, 30);
    private readonly Rectangle _towerLibraryCampaignTabButton = new(816, 42, 110, 30);
    private readonly Rectangle _towerLibraryProfilesTabButton = new(936, 42, 88, 30);
    private readonly Rectangle _towerLibrarySystemsTabButton = new(1034, 42, 88, 30);
    private readonly Rectangle _towerLibraryCloseButton = new(1132, 42, 78, 30);
    private readonly Rectangle _resultContinueButton = new(296, 580, 206, 46);
    private readonly Rectangle _resultRestartButton = new(518, 580, 206, 46);
    private readonly Rectangle _resultMenuButton = new(740, 580, 206, 46);
    private readonly Rectangle _fieldResultsButton = new(630, 9, 176, 38);
    private readonly Rectangle _coOpPausedBanner = new(350, 68, 260, 26);
    private readonly Rectangle _windowModeButton = new(350, 202, 580, 54);
    private readonly Rectangle _vsyncButton = new(350, 266, 280, 54);
    private readonly Rectangle _effectsButton = new(650, 266, 280, 54);
    private readonly Rectangle _autoStartButton = new(350, 330, 300, 54);
    private readonly Rectangle _hotkeyBadgesButton = new(660, 330, 270, 54);
    private readonly Rectangle _volumeButton = new(350, 394, 580, 54);
    private readonly Rectangle _musicVolumeButton = new(350, 458, 580, 54);
    private readonly Rectangle _settingsBackButton = new(500, 526, 280, 48);
    private static readonly int[] SandboxGroupSizes = [1, 5, 12];
    private static readonly EnemyRank[] SandboxRanks = [EnemyRank.Standard, EnemyRank.Elite, EnemyRank.Boss];

    public string JoinHostInput => _joinHostInput;
    public string JoinCodeInput => _joinCodeInput;
    public string CoOpLobbyTitle { get; private set; } = "PREPARING ONLINE CO-OP";
    public string CoOpLobbyDetail { get; private set; } = "Starting the internet connection...";
    public string CoOpLobbyCode { get; private set; } = "";
    public string CoOpLobbyCopyStatus => _coOpLobbyCopyStatus;
    public string SelectedMapId => _maps.Count == 0 ? "foundry_loop" : _maps[_selectedMapIndex].Id;
    public string SelectedMapName => _maps.Count == 0 ? "Foundry Loop" : _maps[_selectedMapIndex].Name;
    public string SelectedDifficultyId => _difficulties.Count == 0 ? DifficultyCatalog.DefaultId : _difficulties[_selectedDifficultyIndex].Id;
    public string SelectedDifficultyName => _difficulties.Count == 0 ? "Medium" : _difficulties[_selectedDifficultyIndex].DisplayName;
    public string SelectedChallengeId => _challenges.Count == 0 ? ChallengeCatalog.DefaultId : _challenges[_selectedChallengeIndex].Id;
    public string SelectedChallengeName => _challenges.Count == 0 ? "Standard" : _challenges[_selectedChallengeIndex].DisplayName;
    public int SelectedSaveSlot => _selectedSaveSlot;
    public string? SelectedRunHistoryId => _selectedRunHistoryId;
    public RunHistoryEntry? SelectedRunHistoryEntry =>
        _runHistory.FirstOrDefault(entry => entry.RunId == _selectedRunHistoryId);
    public bool IsRunHistoryDetailOpen => _runHistoryDetailOpen;
    public bool IsRunHistoryCareerOpen => _runHistoryCareerOpen;
    public bool LibraryShowsThreats => _libraryShowsThreats;
    public bool LibraryShowsCampaign => _libraryShowsCampaign;
    public bool LibraryShowsProfiles => _libraryShowsProfiles;
    public bool LibraryShowsSystems => _libraryShowsSystems;
    public int LibraryTowerCount => _libraryTowers.Count;
    public int LibraryThreatCount => _libraryThreats.Count;
    public int LibraryCampaignCount => _libraryMaps.Count;
    public int LibraryDifficultyCount => _libraryDifficulties.Count;
    public int LibraryDirectiveCount => _libraryChallenges.Count;
    public string? SelectedLibraryTowerId => _libraryTowers.Count == 0 ? null : _libraryTowers[_towerLibraryIndex].Id;
    public string? SelectedLibraryEnemyId => _libraryThreats.Count == 0 ? null : _libraryThreats[_enemyLibraryIndex].Id;
    public string? SelectedLibraryCampaignMapId => _libraryMaps.Count == 0 ? null : _libraryMaps[_campaignLibraryMapIndex].Id;
    public int SelectedLibraryCampaignWaveCount => SelectedLibraryCampaignMapId is { } mapId && _libraryCampaignWaves.TryGetValue(mapId, out var waves)
        ? waves.Count
        : 0;
    public int SelectedSettingsIndex => _settingsSelection;
    public int SelectedSandboxWave => _sandboxWaveNumber;
    public bool IsGameplayOverlayOpen => _towerLibraryOpen;

    public void ConfigureSettings(UserSettings settings) => _settings = settings;
    public void SetSettingsStatus(string status) => _settingsStatus = status;

    public void AdvanceVisualTime(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return;
        _visualTimeSeconds = (_visualTimeSeconds + Math.Min(elapsedSeconds, 0.25f)) % 120f;
    }

    public void ConfigureMainMenuBattle(GameContent content, int? randomSeed = null) =>
        _mainMenuBattleScene = new MainMenuBattleScene(content, randomSeed);

    public void AdvanceMainMenuBattle(float elapsedSeconds) =>
        _mainMenuBattleScene?.Update(elapsedSeconds);

    internal int MainMenuBattleKills => _mainMenuBattleScene?.EnemiesKilled ?? 0;
    internal int MainMenuBattleEscapes => _mainMenuBattleScene?.EnemiesEscaped ?? 0;
    internal IReadOnlyList<int> MainMenuBattleTowerLevels => _mainMenuBattleScene?.TowerLevels ?? [];
    internal IReadOnlyList<string> MainMenuBattleTowerKinds => _mainMenuBattleScene?.TowerKinds ?? [];
    internal IReadOnlyList<int> MainMenuBattleTowerCounts => _mainMenuBattleScene?.TowerCounts ?? [];
    internal IReadOnlyList<int> MainMenuBattleEnemyCounts => _mainMenuBattleScene?.EnemyCounts ?? [];

    public void PreparePauseScreen()
    {
        _restartArmed = false;
        _towerLibraryOpen = false;
    }

    public void PrepareResultScreen()
    {
        _resultMenuSelection = 0;
        _restartArmed = false;
    }

    public void CloseGameplayOverlay() => _towerLibraryOpen = false;

    public static string PauseCheckpointStatus(bool canSave) => canSave
        ? "Between waves - save slots are available."
        : "Active wave - saving unlocks after it clears.";

    public const string RestartPreservationLabel = "FRESH RUN | MANUAL SAVES STAY PROTECTED";

    public static string CoOpWaveButtonLabel(int localPlayerId, int currentWave, int readyMask,
        bool startQueued, bool earlyBonusQueued, float intermissionRemaining)
    {
        if (startQueued) return earlyBonusQueued ? $"STARTING | +{GameConstants.EarlyStartBonus} LOCKED" : "STARTING | NO BONUS";
        var otherPlayer = localPlayerId == 1 ? 2 : 1;
        var localReady = localPlayerId is 1 or 2 && (readyMask & (1 << (localPlayerId - 1))) != 0;
        var otherReady = (readyMask & (1 << (otherPlayer - 1))) != 0;
        var action = localReady ? $"WAIT P{otherPlayer}" : otherReady ? $"JOIN P{otherPlayer}" : "READY";
        var earlyStatus = EarlyCallStatus(currentWave, intermissionRemaining);
        return string.IsNullOrEmpty(earlyStatus)
            ? action == "READY" ? "READY WAVE" : action
            : $"{action} | {earlyStatus}";
    }

    public static string SoloWaveButtonLabel(MinimalBastion.GameSession session, bool autoStartWaves,
        int autoStartDelaySeconds = 0)
    {
        if (!session.CanStartWave) return "IN WAVE";
        if (session.IntermissionRemaining <= 0)
            return autoStartWaves && session.CurrentWave > 0 ? "AUTO STARTING" : "START WAVE";
        if (!autoStartWaves || session.CurrentWave <= 0)
            return $"EARLY +{GameConstants.EarlyStartBonus}  {MathF.Ceiling(session.IntermissionRemaining):0}s";

        var boundedDelay = Math.Clamp(autoStartDelaySeconds, 0, (int)GameConstants.IntermissionSeconds);
        var elapsedIntermission = GameConstants.IntermissionSeconds - session.IntermissionRemaining;
        var automaticRemaining = MathF.Max(0, boundedDelay - elapsedIntermission);
        return automaticRemaining <= 0.001f
            ? "AUTO STARTING"
            : $"AUTO {MathF.Ceiling(automaticRemaining):0}s | +{GameConstants.EarlyStartBonus} NOW";
    }

    public static string CoOpReadyStatusLabel(int currentWave, int readyMask, bool startQueued,
        bool earlyBonusQueued, float intermissionRemaining)
    {
        var p1 = (readyMask & 0b01) != 0 ? "READY" : "WAIT";
        var p2 = (readyMask & 0b10) != 0 ? "READY" : "WAIT";
        if (currentWave <= 0 || (!startQueued && intermissionRemaining <= 0))
            return $"P1 {p1} | P2 {p2}";

        var compactP1 = p1 == "READY" ? "R" : "W";
        var compactP2 = p2 == "READY" ? "R" : "W";
        var earlyStatus = startQueued
            ? earlyBonusQueued ? $"+{GameConstants.EarlyStartBonus} LOCK" : "NO +20"
            : $"+{GameConstants.EarlyStartBonus} {MathF.Ceiling(intermissionRemaining):0}s";
        return $"P1{compactP1} | P2{compactP2} | {earlyStatus}";
    }

    public static string CoOpLinkStatusLabel(bool connected, bool resyncing, float silenceSeconds)
    {
        if (!connected) return "WAITING FOR P2";
        if (resyncing) return "P1 + P2 | RESYNC";
        var silence = float.IsFinite(silenceSeconds) ? MathF.Max(0, silenceSeconds) : 0;
        if (silence < 1.5f) return "P1 + P2 | LIVE";
        if (silence < 5f) return $"LINK DELAY | {MathF.Ceiling(silence):0}s";
        return $"LINK STALLED | {MathF.Ceiling(silence):0}s";
    }

    public static string CoOpSidebarLinkStatusLabel(bool connected, bool resyncing, float silenceSeconds)
    {
        if (!connected) return "WAITING P2";
        if (resyncing) return "RESYNC";
        var silence = float.IsFinite(silenceSeconds) ? MathF.Max(0, silenceSeconds) : 0;
        if (silence < 1.5f) return "P1+P2 LIVE";
        if (silence < 5f) return $"DELAY {MathF.Ceiling(silence):0}s";
        return $"STALLED {MathF.Ceiling(silence):0}s";
    }

    public static string PulsePlateButtonLabel(MinimalBastion.GameSession session)
    {
        var definition = session.Content.Tactics.EmergencyDefense;
        var field = $"FIELD {session.EmergencyDefenses.Count}/{definition.MaximumActive}";
        if (session.EmergencyDefenses.Count >= definition.MaximumActive)
            return $"{field} | FULL";
        if (session.EmergencyInventory > 0)
        {
            var stock = session.Generator is { } forge
                ? $"{session.EmergencyInventory}/{forge.Level.Capacity}"
                : session.EmergencyInventory.ToString();
            return $"DEPLOY {stock} | {field}";
        }
        if (session.Waves.IsActive)
            return $"BUY {session.CurrentEmergencyDirectPurchaseCost} | {field}";
        return $"PLATES 0 | {field}";
    }

    private static string EarlyCallStatus(int currentWave, float intermissionRemaining) =>
        currentWave <= 0 ? "" : intermissionRemaining > 0
            ? $"EARLY +{GameConstants.EarlyStartBonus} | {MathF.Ceiling(intermissionRemaining):0}s"
            : "BONUS EXPIRED";

    public UIManager(SpriteFont font) => _font = font;

    public void SetSaveState(bool available, string? status = null)
    {
        _saveAvailable = available;
        if (!string.IsNullOrWhiteSpace(status)) _persistenceStatus = status;
    }

    public void ConfigureSaveSlots(IReadOnlyList<SaveSlotInfo> slots, bool writeMode, int? activeSlot = null)
    {
        _saveSlots = slots.OrderBy(slot => slot.Slot).ToArray();
        _saveSlotWriteMode = writeMode;
        _saveSlotDeleteArmed = false;
        int? preferred = activeSlot is { } requested && _saveSlots.Any(slot => slot.Slot == requested)
            ? requested
            : null;
        if (preferred is null)
            preferred = writeMode
                ? _saveSlots.FirstOrDefault(slot => !slot.IsOccupied)?.Slot
                : _saveSlots.FirstOrDefault(slot => slot.IsOccupied && slot.Error is null)?.Slot;
        preferred ??= _saveSlots.FirstOrDefault()?.Slot ?? 1;
        _selectedSaveSlot = preferred.Value;
        var selectedIndex = Math.Max(0, _saveSlots.ToList().FindIndex(slot => slot.Slot == _selectedSaveSlot));
        _saveSlotPage = selectedIndex / _saveSlotRows.Length;
    }

    public void ConfigureRunHistory(IReadOnlyList<RunHistoryEntry> entries, string? preferredRunId = null)
    {
        _runHistory = entries.OrderByDescending(entry => entry.CompletedAtUtc).ToArray();
        _runHistoryDeleteArmed = false;
        _runHistoryDetailOpen = false;
        _runHistoryCareerOpen = false;
        _careerMedalPage = 0;
        _careerAchievementPage = 0;
        _selectedRunHistoryId = preferredRunId is not null && _runHistory.Any(entry => entry.RunId == preferredRunId)
            ? preferredRunId
            : _runHistory.FirstOrDefault()?.RunId;
        var selectedIndex = Math.Max(0, _runHistory.ToList().FindIndex(entry => entry.RunId == _selectedRunHistoryId));
        _runHistoryPage = selectedIndex / _saveSlotRows.Length;
    }

    public static string BestRunLabel(IEnumerable<RunHistoryEntry> entries, string mapId, string difficultyId, string challengeId)
    {
        var best = entries
            .Where(entry => entry.MapId.Equals(mapId, StringComparison.OrdinalIgnoreCase) &&
                            entry.DifficultyId.Equals(difficultyId, StringComparison.OrdinalIgnoreCase) &&
                            entry.ChallengeId.Equals(challengeId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CurrentWave)
            .ThenByDescending(entry => entry.IsEndless)
            .ThenByDescending(entry => entry.Victory)
            .ThenByDescending(entry => entry.Lives)
            .FirstOrDefault();
        if (best is null) return "";
        if (best.IsEndless) return $"BEST ENDLESS {best.CurrentWave}";
        return best.Victory ? "BEST CAMPAIGN CLEAR" : $"BEST WAVE {best.CurrentWave}";
    }

    public void SetRunHistoryStatus(string status)
    {
        if (!string.IsNullOrWhiteSpace(status)) _runHistoryStatus = status;
    }

    public void ConfigureMaps(IEnumerable<MapDefinition> maps, IReadOnlyDictionary<string, WaveSetDefinition>? waveSets = null,
        IReadOnlyDictionary<string, EnemyDefinition>? enemies = null)
    {
        var mapDefinitions = maps.ToArray();
        _maps.Clear();
        _maps.AddRange(mapDefinitions.OrderBy(x => x.Id.Equals("foundry_loop", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.ChallengeRating)
            .ThenBy(x => x.DisplayName)
            .Select(x =>
            {
                var campaign = new CampaignIntelInfo(0, 0, 0, "STANDARD", 1, 0);
                var masteryCampaign = campaign;
                if (waveSets is not null && enemies is not null && waveSets.TryGetValue(x.WaveSet, out var waveSet))
                {
                    campaign = WaveIntel.AnalyzeCampaign(waveSet, enemies);
                    masteryCampaign = WaveIntel.AnalyzeCampaign(waveSet, enemies, GameConstants.MasteryFinalWave);
                }
                return (x.Id, x.DisplayName, x.PowerNodes.Count, x.ChallengeRating, x.StartingCredits, x.Description, x.PathVisual.Style,
                    campaign, masteryCampaign, (IReadOnlyList<Vector2>)x.Path.Select(point => point.ToVector2()).ToArray(),
                    x.PathVisual.BaseColor, x.PathVisual.AccentColor);
            }));
        _selectedMapIndex = Math.Clamp(_selectedMapIndex, 0, Math.Max(0, _maps.Count - 1));
        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, Math.Max(0, _maps.Count - 1));
        _libraryCampaignWaves.Clear();
        if (waveSets is not null && enemies is not null)
        {
            foreach (var map in mapDefinitions)
            {
                if (!waveSets.TryGetValue(map.WaveSet, out var waveSet)) continue;
                _libraryCampaignWaves[map.Id] = waveSet.Waves.OrderBy(wave => wave.Number)
                    .Select(wave => CampaignWaveReference.From(wave, enemies))
                    .ToArray();
            }
        }
        ApplyLibraryCatalog();
    }

    public void ConfigureDifficulties(IEnumerable<DifficultyDefinition> difficulties)
    {
        _difficulties.Clear();
        _difficulties.AddRange(difficulties.OrderBy(x => DifficultyOrder(x.Id)).ThenBy(x => x.DisplayName));
        var defaultIndex = _difficulties.FindIndex(x => x.Id.Equals(DifficultyCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
        _selectedDifficultyIndex = defaultIndex >= 0 ? defaultIndex : 0;
        ApplyLibraryCatalog();
    }

    public void ConfigureChallenges(IEnumerable<ChallengeDefinition> challenges)
    {
        _challenges.Clear();
        _challenges.AddRange(challenges.OrderBy(ChallengeOrder)
            .ThenBy(challenge => challenge.DisplayName));
        var defaultIndex = _challenges.FindIndex(challenge => challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
        _selectedChallengeIndex = defaultIndex >= 0 ? defaultIndex : 0;
        ApplyLibraryCatalog();
    }

    public void ConfigureTowerLibrary(IEnumerable<TowerDefinition> towers, IEnumerable<EnemyDefinition>? enemies = null,
        TacticsDefinition? tactics = null)
    {
        _allLibraryTowers = towers.OrderBy(x => x.PurchaseCost).ThenBy(x => x.Id).ToArray();
        _allLibraryEnemies = enemies?.OrderBy(x => x.MaxHealth).ThenBy(x => x.Id).ToArray() ?? Array.Empty<EnemyDefinition>();
        if (tactics is not null) _libraryTactics = tactics;
        ApplyLibraryCatalog();
    }

    private void ApplyLibraryCatalog()
    {
        _libraryTowers = _allLibraryTowers;
        var threats = new List<ThreatLibraryEntry>();
        threats.AddRange(_allLibraryEnemies.Select(ThreatLibraryEntry.FromEnemy));
        foreach (var role in Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None))
            threats.Add(ThreatLibraryEntry.FromSignalRole(role));
        _libraryThreats = threats.ToArray();
        _libraryMaps = _maps.ToArray();
        _libraryDifficulties = _difficulties.ToArray();
        _libraryChallenges = _challenges.ToArray();
        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, Math.Max(0, _libraryThreats.Count - 1));
        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, Math.Max(0, _libraryMaps.Count - 1));
    }

    public UiAction HandleMainMenu(InputSnapshot input)
    {
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        var optionCount = PlatformCapabilities.OnlineCoOp ? 6 : PlatformCapabilities.ExitCommand ? 5 : 4;
        for (var index = 0; index < optionCount; index++)
        {
            if (!MainMenuOptionRectangle(index).Contains(point)) continue;
            return ActivateMainMenuSelection(index);
        }
        return UiAction.None;
    }

    private UiAction ActivateMainMenuSelection(int selection)
    {
        if (!PlatformCapabilities.OnlineCoOp)
        {
            return selection switch
            {
                0 => UiAction.OpenSoloSetup,
                1 when _saveAvailable => UiAction.LoadGame,
                2 => UiAction.TowerLibrary,
                3 => UiAction.Settings,
                4 when PlatformCapabilities.ExitCommand => UiAction.Exit,
                _ => UiAction.None
            };
        }

        return selection switch
        {
            0 => UiAction.OpenSoloSetup,
            1 => UiAction.CoOp,
            2 when _saveAvailable => UiAction.LoadGame,
            3 => UiAction.TowerLibrary,
            4 => UiAction.Settings,
            5 => UiAction.Exit,
            _ => UiAction.None
        };
    }

    public void PrepareGameSetup(bool forCoOp)
    {
        _setupForCoOp = forCoOp;
        if (forCoOp && _challenges.ElementAtOrDefault(_selectedChallengeIndex)?.IsSandbox == true)
        {
            var defaultIndex = _challenges.FindIndex(challenge => challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
            _selectedChallengeIndex = defaultIndex >= 0 ? defaultIndex : 0;
        }
    }

    public UiAction HandleGameSetup(InputSnapshot input)
    {
        if (input.EscapePressed) return _setupForCoOp ? UiAction.CoOp : UiAction.MainMenu;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var index = 0; index < _maps.Count; index++)
        {
            if (!SetupCardRectangle(0, index, _maps.Count).Contains(point)) continue;
            _selectedMapIndex = index;
            return UiAction.None;
        }
        for (var index = 0; index < _difficulties.Count; index++)
        {
            if (!SetupCardRectangle(1, index, _difficulties.Count).Contains(point)) continue;
            _selectedDifficultyIndex = index;
            return UiAction.None;
        }
        var setupChallenges = SetupChallenges();
        for (var index = 0; index < setupChallenges.Count; index++)
        {
            if (!SetupCardRectangle(2, index, setupChallenges.Count).Contains(point)) continue;
            _selectedChallengeIndex = _challenges.IndexOf(setupChallenges[index]);
            return UiAction.None;
        }
        if (_setupConfirmButton.Contains(point))
        {
            return _setupForCoOp ? UiAction.HostCoOp : UiAction.Play;
        }
        if (_setupBackButton.Contains(point))
        {
            return _setupForCoOp ? UiAction.CoOp : UiAction.MainMenu;
        }
        return UiAction.None;
    }

    private IReadOnlyList<ChallengeDefinition> SetupChallenges() => _setupForCoOp
        ? _challenges.Where(challenge => !challenge.IsSandbox).ToArray()
        : _challenges;

    private Rectangle MainMenuOptionRectangle(int index)
    {
        if (!PlatformCapabilities.OnlineCoOp)
        {
            return index switch
            {
                0 => _playButton,
                1 => _continueButton,
                2 => _mainMenuLibraryButton,
                3 => _mainMenuSettingsButton,
                4 when PlatformCapabilities.ExitCommand => _quitButton,
                _ => Rectangle.Empty
            };
        }

        return index switch
        {
            0 => _playButton,
            1 => _coOpButton,
            2 => _continueButton,
            3 => _mainMenuLibraryButton,
            4 => _mainMenuSettingsButton,
            5 => _quitButton,
            _ => Rectangle.Empty
        };
    }

    private static Rectangle SetupCardRectangle(int row, int index, int count)
    {
        const int left = 48;
        const int totalWidth = 1184;
        const int gap = 12;
        var safeCount = Math.Max(1, count);
        var width = (totalWidth - gap * (safeCount - 1)) / safeCount;
        var y = row switch { 0 => 132, 1 => 226, _ => 320 };
        return new Rectangle(left + index * (width + gap), y, width, 52);
    }

    public UiAction HandleSettingsInput(InputSnapshot input)
    {
        if (input.EscapePressed || input.PausePressed) return UiAction.CloseSettings;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var index = 0; index < 8; index++)
        {
            if (!SettingsOptionRectangle(index).Contains(point)) continue;
            _settingsSelection = index;
            return index == 7 ? UiAction.CloseSettings : ApplySelectedSetting(1);
        }
        return UiAction.None;
    }

    private UiAction ApplySelectedSetting(int direction)
    {
        switch (_settingsSelection)
        {
            case 0:
                _settings.Fullscreen = !_settings.Fullscreen;
                break;
            case 1:
                if (!PlatformCapabilities.ConfigurableVSync) return UiAction.None;
                _settings.VSync = !_settings.VSync;
                break;
            case 2:
                _settings.ReducedEffects = !_settings.ReducedEffects;
                break;
            case 3:
                _settings.SfxVolume = AdjustVolume(_settings.SfxVolume, direction);
                break;
            case 4:
                _settings.MusicVolume = AdjustVolume(_settings.MusicVolume, direction);
                break;
            case 5:
                _settings.CycleAutoStart();
                break;
            case 6:
                _settings.ShowHotkeyBadges = !_settings.ShowHotkeyBadges;
                break;
            default:
                return UiAction.None;
        }
        return UiAction.ApplySettings;
    }

    private static float AdjustVolume(float current, int direction)
    {
        const float step = 0.25f;
        if (direction < 0)
        {
            if (current <= 0.001f) return 1;
            return MathF.Floor((current - 0.001f) / step) * step;
        }
        if (current >= 0.999f) return 0;
        return MathF.Ceiling((current + 0.001f) / step) * step;
    }

    private Rectangle SettingsOptionRectangle(int index) => index switch
    {
        0 => _windowModeButton,
        1 => _vsyncButton,
        2 => _effectsButton,
        3 => _volumeButton,
        4 => _musicVolumeButton,
        5 => _autoStartButton,
        6 => _hotkeyBadgesButton,
        7 => _settingsBackButton,
        _ => Rectangle.Empty
    };

    private static int DifficultyOrder(string id) => id.ToLowerInvariant() switch
    {
        "easy" => 0,
        "normal" => 1,
        "hard" => 2,
        "bastion" => 3,
        _ => 4
    };

    private static int ChallengeOrder(ChallengeDefinition challenge) => challenge.Id.ToLowerInvariant() switch
    {
        ChallengeCatalog.DefaultId => 0,
        "core_six" => 1,
        "no_reserves" => 2,
        "close_quarters" => 3,
        "sandbox_lab" => 4,
        _ => 3
    };

    public UiAction HandleSaveSlots(InputSnapshot input)
    {
        if (input.EscapePressed)
        {
            if (_saveSlotDeleteArmed)
            {
                DisarmSaveSlotDeletion();
                return UiAction.None;
            }
            return UiAction.CloseSaveSlots;
        }
        if (input.NavigateUpPressed || input.NavigateDownPressed)
        {
            MoveSaveSlotSelection(input.NavigateUpPressed ? -1 : 1);
            return UiAction.None;
        }
        if (input.NavigateLeftPressed || input.NavigateRightPressed)
        {
            MoveSaveSlotPage(input.NavigateLeftPressed ? -1 : 1);
            return UiAction.None;
        }
        if (input.EnterPressed)
        {
            var enterSelection = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
            if (_saveSlotWriteMode || enterSelection is { IsOccupied: true, Error: null }) return UiAction.ConfirmSaveSlot;
        }
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_saveSlotHistoryButton.Contains(point)) return UiAction.RunHistory;
        var pageCount = Math.Max(1, (_saveSlots.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var pageSlots = _saveSlots.Skip(_saveSlotPage * _saveSlotRows.Length).Take(_saveSlotRows.Length).ToArray();
        for (var index = 0; index < _saveSlotRows.Length; index++)
        {
            if (!_saveSlotRows[index].Contains(point) || index >= pageSlots.Length) continue;
            _selectedSaveSlot = pageSlots[index].Slot;
            DisarmSaveSlotDeletion();
            return UiAction.None;
        }

        if (_saveSlotPreviousButton.Contains(point) && _saveSlotPage > 0)
        {
            _saveSlotPage--;
            _selectedSaveSlot = _saveSlots[_saveSlotPage * _saveSlotRows.Length].Slot;
            DisarmSaveSlotDeletion();
            return UiAction.None;
        }
        if (_saveSlotNextButton.Contains(point) && _saveSlotPage + 1 < pageCount)
        {
            _saveSlotPage++;
            _selectedSaveSlot = _saveSlots[_saveSlotPage * _saveSlotRows.Length].Slot;
            DisarmSaveSlotDeletion();
            return UiAction.None;
        }

        var selected = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
        var canConfirm = _saveSlotWriteMode || selected is { IsOccupied: true, Error: null };
        var confirmButton = _saveSlotWriteMode ? _saveSlotWriteConfirmButton : _saveSlotConfirmButton;
        var deleteButton = _saveSlotWriteMode ? _saveSlotWriteDeleteButton : _saveSlotDeleteButton;
        if (confirmButton.Contains(point) && canConfirm) return UiAction.ConfirmSaveSlot;
        if (PlatformCapabilities.OnlineCoOp && !_saveSlotWriteMode && _saveSlotHostButton.Contains(point) && selected is { IsOccupied: true, Error: null })
            return UiAction.HostSavedGame;
        if (!_saveSlotWriteMode && _saveSlotDuplicateButton.Contains(point) && selected is { IsOccupied: true, Error: null })
            return UiAction.DuplicateSaveSlot;
        if (deleteButton.Contains(point) && selected is { IsOccupied: true })
        {
            if (_saveSlotDeleteArmed)
            {
                _saveSlotDeleteArmed = false;
                return UiAction.DeleteSaveSlot;
            }
            _saveSlotDeleteArmed = true;
            _persistenceStatus = $"Delete {SaveSlotLabel(_selectedSaveSlot).ToLowerInvariant()}? Click the red CONFIRM DELETE button again. ESC cancels.";
            return UiAction.None;
        }
        if (_saveSlotBackButton.Contains(point)) return UiAction.CloseSaveSlots;
        return UiAction.None;
    }

    public UiAction HandleRunHistory(InputSnapshot input)
    {
        if (_runHistoryCareerOpen)
        {
            if (input.EscapePressed || input.LeftPressed && _runHistoryCareerBackButton.Contains(input.MousePosition.ToPoint()))
            {
                _runHistoryCareerOpen = false;
                return UiAction.None;
            }
            if (!input.LeftPressed) return UiAction.None;
            var career = CareerProgression.Analyze(_runHistory);
            var careerPoint = input.MousePosition.ToPoint();
            var medalPageCount = Math.Max(1, (career.Medals.Count + 6) / 7);
            var achievementPageCount = Math.Max(1, (career.Achievements.Count + 7) / 8);
            if (_careerMedalPreviousButton.Contains(careerPoint) && _careerMedalPage > 0) _careerMedalPage--;
            else if (_careerMedalNextButton.Contains(careerPoint) && _careerMedalPage + 1 < medalPageCount) _careerMedalPage++;
            else if (_careerAchievementPreviousButton.Contains(careerPoint) && _careerAchievementPage > 0) _careerAchievementPage--;
            else if (_careerAchievementNextButton.Contains(careerPoint) && _careerAchievementPage + 1 < achievementPageCount) _careerAchievementPage++;
            return UiAction.None;
        }
        if (_runHistoryDetailOpen)
        {
            var detailPoint = input.MousePosition.ToPoint();
            if (input.LeftPressed && _runHistoryLayoutButton.Contains(detailPoint) && SelectedRunHistoryEntry?.FinalLayout is not null)
                return UiAction.ViewRunHistoryField;
            if (input.EscapePressed || input.LeftPressed && _runHistoryDetailBackButton.Contains(input.MousePosition.ToPoint()))
                _runHistoryDetailOpen = false;
            return UiAction.None;
        }
        if (input.EscapePressed)
        {
            if (_runHistoryDeleteArmed)
            {
                _runHistoryDeleteArmed = false;
                return UiAction.None;
            }
            return UiAction.CloseRunHistory;
        }
        if (input.EnterPressed && _selectedRunHistoryId is not null)
        {
            _runHistoryDetailOpen = true;
            return UiAction.None;
        }
        if (input.NavigateUpPressed || input.NavigateDownPressed)
        {
            MoveRunHistorySelection(input.NavigateUpPressed ? -1 : 1);
            return UiAction.None;
        }
        if (input.NavigateLeftPressed || input.NavigateRightPressed)
        {
            MoveRunHistoryPage(input.NavigateLeftPressed ? -1 : 1);
            return UiAction.None;
        }
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_runHistoryCareerButton.Contains(point))
        {
            _runHistoryDeleteArmed = false;
            _careerMedalPage = 0;
            _careerAchievementPage = 0;
            _runHistoryCareerOpen = true;
            return UiAction.None;
        }
        var pageCount = Math.Max(1, (_runHistory.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var pageEntries = _runHistory.Skip(_runHistoryPage * _saveSlotRows.Length).Take(_saveSlotRows.Length).ToArray();
        for (var index = 0; index < _saveSlotRows.Length; index++)
        {
            if (!_saveSlotRows[index].Contains(point) || index >= pageEntries.Length) continue;
            _selectedRunHistoryId = pageEntries[index].RunId;
            _runHistoryDeleteArmed = false;
            return UiAction.None;
        }

        if (_saveSlotPreviousButton.Contains(point) && _runHistoryPage > 0)
        {
            _runHistoryPage--;
            _selectedRunHistoryId = _runHistory[_runHistoryPage * _saveSlotRows.Length].RunId;
            _runHistoryDeleteArmed = false;
            return UiAction.None;
        }
        if (_saveSlotNextButton.Contains(point) && _runHistoryPage + 1 < pageCount)
        {
            _runHistoryPage++;
            _selectedRunHistoryId = _runHistory[_runHistoryPage * _saveSlotRows.Length].RunId;
            _runHistoryDeleteArmed = false;
            return UiAction.None;
        }
        if (_runHistoryViewButton.Contains(point) && _selectedRunHistoryId is not null)
        {
            _runHistoryDeleteArmed = false;
            _runHistoryDetailOpen = true;
            return UiAction.None;
        }
        if (_runHistoryDeleteButton.Contains(point) && _selectedRunHistoryId is not null)
        {
            if (_runHistoryDeleteArmed)
            {
                _runHistoryDeleteArmed = false;
                return UiAction.DeleteRunHistory;
            }
            _runHistoryDeleteArmed = true;
            return UiAction.None;
        }
        if (_saveSlotBackButton.Contains(point)) return UiAction.CloseRunHistory;
        return UiAction.None;
    }

    public UiAction HandleRunHistoryFieldInput(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.CloseRunHistoryField;
        return input.LeftPressed && _fieldResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.CloseRunHistoryField
            : UiAction.None;
    }

    public UiAction HandleCoOpMenu(InputSnapshot input)
    {
        if (input.NavigateUpPressed || input.NavigateDownPressed)
        {
            MoveCoOpMenuSelection(input.NavigateUpPressed ? -1 : 1);
            return UiAction.None;
        }
        if (input.TabPressed)
        {
            _editingJoinCode = !_editingJoinCode;
            _coOpMenuSelection = 1;
        }
        if (input.LeftPressed)
        {
            var clicked = input.MousePosition.ToPoint();
            if (_joinHostField.Contains(clicked))
            {
                _editingJoinCode = false;
                _coOpMenuSelection = 1;
            }
            else if (_joinCodeField.Contains(clicked))
            {
                _editingJoinCode = true;
                _coOpMenuSelection = 1;
            }
        }

        if (input.CopyPressed)
            ClipboardService.TrySetText(_editingJoinCode ? _joinCodeInput : _joinHostInput);

        if (!string.IsNullOrEmpty(input.TextEntered))
        {
            _coOpMenuSelection = 1;
            if (_editingJoinCode && _joinCodeInput.Length < 6)
                _joinCodeInput += new string(input.TextEntered.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Take(6 - _joinCodeInput.Length).ToArray());
            else if (!_editingJoinCode && _joinHostInput.Length < 64)
                _joinHostInput += new string(input.TextEntered.Where(x => char.IsLetterOrDigit(x) || x is '.' or ':' or '-' or '[' or ']').Take(64 - _joinHostInput.Length).ToArray());
        }
        if (input.BackspacePressed)
        {
            if (_editingJoinCode && _joinCodeInput.Length > 0) _joinCodeInput = _joinCodeInput[..^1];
            else if (!_editingJoinCode && _joinHostInput.Length > 0) _joinHostInput = _joinHostInput[..^1];
        }
        if (input.EscapePressed) return UiAction.MainMenu;
        if (input.EnterPressed) return ActivateCoOpMenuSelection();
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_hostCoOpButton.Contains(point))
        {
            _coOpMenuSelection = 0;
            return UiAction.OpenCoOpSetup;
        }
        if (_joinCoOpButton.Contains(point))
        {
            _coOpMenuSelection = 1;
            return CanJoinOnline ? UiAction.JoinCoOp : UiAction.None;
        }
        if (_backButton.Contains(point))
        {
            _coOpMenuSelection = 2;
            return UiAction.MainMenu;
        }
        return UiAction.None;
    }

    private void MoveCoOpMenuSelection(int direction)
    {
        for (var attempts = 0; attempts < 3; attempts++)
        {
            _coOpMenuSelection = (_coOpMenuSelection + direction + 3) % 3;
            if (_coOpMenuSelection != 1 || CanJoinOnline) return;
        }
    }

    private UiAction ActivateCoOpMenuSelection() => _coOpMenuSelection switch
    {
        0 => UiAction.OpenCoOpSetup,
        1 when CanJoinOnline => UiAction.JoinCoOp,
        2 => UiAction.MainMenu,
        _ => UiAction.None
    };

    private Rectangle CoOpMenuActionRectangle(int selection) => selection switch
    {
        0 => _hostCoOpButton,
        1 => _joinCoOpButton,
        2 => _backButton,
        _ => Rectangle.Empty
    };

    private bool CanJoinOnline => !string.IsNullOrWhiteSpace(_joinHostInput) && _joinCodeInput.Length == 6;

    public UiAction HandleCoOpLobby(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.MainMenu;
        if (!string.IsNullOrEmpty(CoOpLobbyCode) &&
            (input.CopyPressed || input.LeftPressed && _coOpLobbyCodeButton.Contains(input.MousePosition.ToPoint())))
        {
            _coOpLobbyCopyStatus = ClipboardService.TrySetText(CoOpLobbyCode)
                ? "JOIN CODE COPIED"
                : "CLIPBOARD UNAVAILABLE";
            return UiAction.None;
        }
        return input.LeftPressed && _backButton.Contains(input.MousePosition.ToPoint()) ? UiAction.MainMenu : UiAction.None;
    }

    public UiAction HandleCoOpReconnect(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.MainMenu;
        if (!string.IsNullOrEmpty(CoOpLobbyCode) &&
            (input.CopyPressed || input.LeftPressed && _coOpReconnectCodeButton.Contains(input.MousePosition.ToPoint())))
        {
            _coOpLobbyCopyStatus = ClipboardService.TrySetText(CoOpLobbyCode)
                ? "REJOIN CODE COPIED"
                : "CLIPBOARD UNAVAILABLE";
        }
        return UiAction.None;
    }

    public void SetCoOpLobbyStatus(string title, string detail, string code = "")
    {
        CoOpLobbyTitle = title;
        CoOpLobbyDetail = detail;
        if (!string.Equals(CoOpLobbyCode, code, StringComparison.Ordinal))
            _coOpLobbyCopyStatus = "CLICK CODE OR CTRL+C TO COPY";
        CoOpLobbyCode = code;
    }

    public void SetCoOpWaveReadyState(int readyMask, bool startQueued, bool earlyBonusQueued = false)
    {
        _coOpWaveReadyMask = readyMask & 0b11;
        _coOpWaveStartQueued = startQueued;
        _coOpEarlyBonusQueued = startQueued && earlyBonusQueued;
    }

    public void SetCoOpConnectionState(bool connected, bool resyncing = false)
    {
        _coOpPeerConnected = connected;
        _coOpResyncing = resyncing;
        if (!connected) _coOpLinkSilenceSeconds = 0;
    }

    public void SetCoOpLinkSilence(float silenceSeconds) =>
        _coOpLinkSilenceSeconds = float.IsFinite(silenceSeconds) ? MathF.Max(0, silenceSeconds) : 0;

    public void SetRemoteCoOpCursor(Vector2? position, int playerId, int selectedTowerId = 0,
        string placementTowerId = "", TacticalPlacementKind tacticalPlacement = TacticalPlacementKind.None,
        bool hasPlacementPreview = false,
        Vector2 placementPreviewPosition = default)
    {
        _remoteCoOpCursor = position;
        _remoteCoOpCursorPlayerId = position is null ? 0 : playerId;
        _remoteCoOpSelectedTowerId = position is null ? 0 : Math.Max(0, selectedTowerId);
        _remoteCoOpPlacementTowerId = position is null ? "" : placementTowerId ?? "";
        _remoteCoOpTacticalPlacement = position is null ? TacticalPlacementKind.None : tacticalPlacement;
        _remoteCoOpHasPlacementPreview = position is not null && hasPlacementPreview;
        _remoteCoOpPlacementPreviewPosition = placementPreviewPosition;
    }

    public UiAction HandleGameplayInput(InputSnapshot input, MinimalBastion.GameSession session, Action<GameCommand>? commandSink = null, int playerId = 1)
    {
        if (_towerLibraryOpen)
        {
            CloseTargetPicker();
            if (session.IsCoOp && input.TabPressed)
            {
                _towerLibraryOpen = false;
                return UiAction.None;
            }
            if (!session.IsCoOp || HandleTowerLibraryInput(input))
                _towerLibraryOpen = false;
            return UiAction.None;
        }

        var point = input.MousePosition.ToPoint();
        if (session.IsCoOp && input.TabPressed)
        {
            CloseTargetPicker();
            _towerLibraryOpen = true;
            return UiAction.None;
        }
        if (session.IsCoOpPaused)
        {
            CloseTargetPicker();
            return HandleCoOpPausedInput(input, session, commandSink, playerId);
        }
        if (_targetPickerOpen && (session.SelectedTower is not { IsSupport: false } selectedTargetTower ||
                                  selectedTargetTower.Id != _targetPickerTowerId))
            CloseTargetPicker();
        if (_targetPickerOpen && (input.EscapePressed || input.PausePressed || input.RightPressed))
        {
            CloseTargetPicker();
            return UiAction.None;
        }
        _hoveredTowerCardId = _towerCards.FirstOrDefault(x => x.Value.Contains(point)).Key;
        _hoveredPowerNode = session.Map.Definition.PowerNodes.FirstOrDefault(node =>
            Vector2.DistanceSquared(node.Position.ToVector2(), input.MousePosition) <= node.Radius * node.Radius);
        _hoveredUpgradePreview = null;
        _hoveredUpgradePreviewLabel = null;
        if (session.SelectedTower is { RequiresDoctrine: true } doctrinePreview)
        {
            if (_specializationAButton.Contains(point) && doctrinePreview.Definition.Tier2Doctrines.Count > 0)
            {
                var choice = doctrinePreview.Definition.Tier2Doctrines[0];
                _hoveredUpgradePreview = doctrinePreview.Definition.Levels[1].WithDoctrine(choice);
                _hoveredUpgradePreviewLabel = $"PREVIEW {choice.DisplayName.ToUpperInvariant()}  {choice.UpgradeCost}";
            }
            else if (_specializationBButton.Contains(point) && doctrinePreview.Definition.Tier2Doctrines.Count > 1)
            {
                var choice = doctrinePreview.Definition.Tier2Doctrines[1];
                _hoveredUpgradePreview = doctrinePreview.Definition.Levels[1].WithDoctrine(choice);
                _hoveredUpgradePreviewLabel = $"PREVIEW {choice.DisplayName.ToUpperInvariant()}  {choice.UpgradeCost}";
            }
        }
        else if (session.SelectedTower is { RequiresSpecialization: true } branchPreview)
        {
            if (_specializationAButton.Contains(point) && branchPreview.Definition.Specializations.Count > 0)
            {
                var choice = branchPreview.Definition.Specializations[0];
                _hoveredUpgradePreview = choice.Level.WithDoctrine(branchPreview.Doctrine);
                _hoveredUpgradePreviewLabel = $"PREVIEW {choice.DisplayName.ToUpperInvariant()}  {choice.UpgradeCost}";
            }
            else if (_specializationBButton.Contains(point) && branchPreview.Definition.Specializations.Count > 1)
            {
                var choice = branchPreview.Definition.Specializations[1];
                _hoveredUpgradePreview = choice.Level.WithDoctrine(branchPreview.Doctrine);
                _hoveredUpgradePreviewLabel = $"PREVIEW {choice.DisplayName.ToUpperInvariant()}  {choice.UpgradeCost}";
            }
        }
        else if (session.SelectedTower is { CanUpgrade: true } upgradePreview && _upgradeButton.Contains(point))
        {
            _hoveredUpgradePreview = upgradePreview.Definition.Levels[upgradePreview.LevelIndex + 1]
                .WithDoctrine(upgradePreview.Doctrine);
            _hoveredUpgradePreviewLabel = $"PREVIEW LEVEL {upgradePreview.LevelIndex + 2}  {upgradePreview.UpgradeCost}";
        }
        else if (session.SelectedTower is { } apexPreview && session.CanApexUpgrade(apexPreview) &&
                 _upgradeButton.Contains(point))
        {
            _hoveredUpgradePreview = apexPreview.ApexPreviewLevel;
            _hoveredUpgradePreviewLabel = $"PREVIEW APEX  {apexPreview.ApexUpgradeCost}";
        }
        _hoveredTacticalPlacement = session.IsSandbox ? TacticalPlacementKind.None :
            _emergencyButton.Contains(point) ? TacticalPlacementKind.PulsePlate :
            _generatorButton.Contains(point) ? TacticalPlacementKind.ChargeForge : TacticalPlacementKind.None;
        if (_targetPickerOpen)
        {
            _hoveredTowerCardId = null;
            _hoveredPowerNode = null;
            _hoveredTacticalPlacement = TacticalPlacementKind.None;
        }
        if ((input.EscapePressed || input.PausePressed) && session.PlacementTowerId is null && session.TacticalPlacement == TacticalPlacementKind.None)
        {
            if (session.IsCoOp && commandSink is not null)
            {
                RequestPause(session, commandSink, playerId);
                return UiAction.None;
            }
            return UiAction.Pause;
        }

        var towersByCost = session.Content.Towers.Values.OrderBy(x => x.PurchaseCost).ToArray();
        if (input.TowerHotkey > 0 && input.TowerHotkey <= towersByCost.Length)
        {
            CloseTargetPicker();
            session.BeginPlacement(towersByCost[input.TowerHotkey - 1].Id);
        }
        if (input.StartWavePressed)
        {
            if (session.IsSandbox) session.StartSandboxWave(_sandboxWaveNumber);
            else RequestStartWave(session, commandSink, playerId);
        }
        if (input.SpeedPressed) RequestSpeed(session, commandSink, playerId);
        if (!session.IsSandbox && input.EmergencyPressed) session.BeginEmergencyPlacement();
        if (!session.IsSandbox && input.GeneratorPressed) session.BeginGeneratorPlacement();
        if (session.IsSandbox)
        {
            if (input.SandboxWavePreviousPressed) ChangeSandboxWave(session, -1);
            if (input.SandboxWaveNextPressed) ChangeSandboxWave(session, 1);
            if (input.SandboxSpawnPressed) SpawnSelectedSandboxTargets(session);
            if (input.SandboxResetPressed) session.ResetSandboxExperiment();
            if (input.SandboxClearTowersPressed) session.ClearSandboxTowers();
            if (input.SandboxToggleTowerPressed && session.SelectedTower is { } sandboxTower)
                session.ToggleSandboxTower(sandboxTower.Id);
            if (input.SandboxEnemyPreviousPressed) CycleSandboxEnemy(session, -1);
            if (input.SandboxEnemyNextPressed) CycleSandboxEnemy(session, 1);
            if (input.GeneratorPressed) CycleSandboxGroup();
            if (input.SandboxRankPressed) CycleSandboxRank();
            if (input.SandboxHealthPressed) CycleSandboxHealth();
        }
        if (input.OverdrivePressed)
        {
            if (session.IsSandbox) ToggleSandboxProtocolTest(session);
            else
                RequestOverdrive(session, commandSink, playerId);
        }
        if (input.AutoProtocolPressed) RequestAutoProtocol(session, commandSink, playerId);
        if (input.TargetPressed)
        {
            ToggleTargetPicker(session);
            return UiAction.None;
        }
        if (input.ApexPressed && session.SelectedTower is { } apexTower && session.CanApexUpgrade(apexTower))
            RequestUpgrade(session, commandSink, playerId);
        if (input.UpgradePressed) RequestUpgradeChoice(session, 0, commandSink, playerId);
        if (input.AlternateUpgradePressed) RequestUpgradeChoice(session, 1, commandSink, playerId);
        if (input.SellPressed)
        {
            if (session.IsSandbox && session.SelectedTower is { } sandboxTower)
                session.RemoveSandboxTower(sandboxTower.Id);
            else
                RequestSell(session, commandSink, playerId);
        }
        if (!input.LeftPressed) return UiAction.None;

        if (_targetPickerOpen)
        {
            var selectedMode = _targetModeButtons.FirstOrDefault(pair => pair.Value.Contains(point));
            if (!selectedMode.Equals(default(KeyValuePair<TargetMode, Rectangle>)))
            {
                RequestTargetMode(session, selectedMode.Key, commandSink, playerId);
                CloseTargetPicker();
                return UiAction.None;
            }
            if (_targetButton.Contains(point))
            {
                CloseTargetPicker();
                return UiAction.None;
            }
            CloseTargetPicker();
        }

        if (_startWaveButton.Contains(point))
        {
            if (session.IsSandbox) session.StartSandboxWave(_sandboxWaveNumber);
            else RequestStartWave(session, commandSink, playerId);
            return UiAction.None;
        }
        if (session.IsSandbox && HandleSandboxControlClick(point, session)) return UiAction.None;
        if (_speedButton.Contains(point))
        {
            RequestSpeed(session, commandSink, playerId);
            return UiAction.None;
        }
        if (_pauseButton.Contains(point))
        {
            if (session.IsCoOp && commandSink is not null)
            {
                RequestPause(session, commandSink, playerId);
                return UiAction.None;
            }
            return UiAction.Pause;
        }
        if (!session.IsSandbox && _emergencyButton.Contains(point))
        {
            session.BeginEmergencyPlacement();
            return UiAction.None;
        }
        if (!session.IsSandbox && _generatorButton.Contains(point))
        {
            session.BeginGeneratorPlacement();
            return UiAction.None;
        }
        if (!session.IsSandbox && _overdriveButton.Contains(point))
        {
            RequestOverdrive(session, commandSink, playerId);
            return UiAction.None;
        }
        if (!session.IsSandbox && _autoProtocolButton.Contains(point))
        {
            RequestAutoProtocol(session, commandSink, playerId);
            return UiAction.None;
        }

        foreach (var pair in _towerCards)
        {
            if (!pair.Value.Contains(point)) continue;
            session.BeginPlacement(pair.Key);
            return UiAction.None;
        }

        if (session.SelectedTower is { RequiresDoctrine: true } doctrineTower &&
            _specializationAButton.Contains(point) && doctrineTower.Definition.Tier2Doctrines.Count > 0)
            RequestDoctrine(session, doctrineTower.Definition.Tier2Doctrines[0].Id, commandSink, playerId);
        else if (session.SelectedTower is { RequiresDoctrine: true } alternateDoctrineTower &&
            _specializationBButton.Contains(point) && alternateDoctrineTower.Definition.Tier2Doctrines.Count > 1)
            RequestDoctrine(session, alternateDoctrineTower.Definition.Tier2Doctrines[1].Id, commandSink, playerId);
        else if (session.SelectedTower is { RequiresSpecialization: true } branchingTower &&
            _specializationAButton.Contains(point) && branchingTower.Definition.Specializations.Count > 0)
            RequestSpecialization(session, branchingTower.Definition.Specializations[0].Id, commandSink, playerId);
        else if (session.SelectedTower is { RequiresSpecialization: true } alternateTower &&
            _specializationBButton.Contains(point) && alternateTower.Definition.Specializations.Count > 1)
            RequestSpecialization(session, alternateTower.Definition.Specializations[1].Id, commandSink, playerId);
        else if (session.IsSandbox && session.SelectedTower is not null && _sandboxToggleTowerButton.Contains(point))
            session.ToggleSandboxTower(session.SelectedTower.Id);
        else if (session.IsSandbox && session.SelectedTower is not null && _sandboxRemoveTowerButton.Contains(point))
            session.RemoveSandboxTower(session.SelectedTower.Id);
        else if (_targetButton.Contains(point)) ToggleTargetPicker(session);
        else if (_upgradeButton.Contains(point)) RequestUpgrade(session, commandSink, playerId);
        else if (_sellButton.Contains(point)) RequestSell(session, commandSink, playerId);

        return UiAction.None;
    }

    private UiAction HandleCoOpPausedInput(InputSnapshot input, MinimalBastion.GameSession session,
        Action<GameCommand>? commandSink, int playerId)
    {
        if (input.EscapePressed || input.PausePressed)
        {
            _restartArmed = false;
            if (commandSink is not null) RequestPause(session, commandSink, playerId);
            return UiAction.None;
        }
        if (!input.LeftPressed) return UiAction.None;

        var point = input.MousePosition.ToPoint();
        if (CoOpPauseResumeBounds.Contains(point) || _pauseButton.Contains(point))
        {
            _restartArmed = false;
            if (commandSink is not null) RequestPause(session, commandSink, playerId);
            return UiAction.None;
        }
        if (CoOpPauseLibraryBounds.Contains(point))
        {
            _restartArmed = false;
            _towerLibraryOpen = true;
            return UiAction.None;
        }
        if (CoOpPauseRestartBounds.Contains(point))
        {
            if (_restartArmed)
            {
                _restartArmed = false;
                return UiAction.Restart;
            }
            _restartArmed = true;
            return UiAction.None;
        }

        _restartArmed = false;
        return CoOpPauseMenuBounds.Contains(point) ? UiAction.MainMenu : UiAction.None;
    }

    private bool HandleSandboxControlClick(Point point, MinimalBastion.GameSession session)
    {
        if (_sandboxWavePreviousButton.Contains(point))
        {
            ChangeSandboxWave(session, -1);
            return true;
        }
        if (_sandboxWaveNextButton.Contains(point))
        {
            ChangeSandboxWave(session, 1);
            return true;
        }
        if (_sandboxEnemyPreviousButton.Contains(point))
        {
            CycleSandboxEnemy(session, -1);
            return true;
        }
        if (_sandboxEnemyNextButton.Contains(point))
        {
            CycleSandboxEnemy(session, 1);
            return true;
        }
        if (_sandboxGroupButton.Contains(point))
        {
            CycleSandboxGroup();
            return true;
        }
        if (_sandboxRankButton.Contains(point))
        {
            CycleSandboxRank();
            return true;
        }
        if (_sandboxHealthButton.Contains(point))
        {
            CycleSandboxHealth();
            return true;
        }
        if (_sandboxSpawnButton.Contains(point))
        {
            SpawnSelectedSandboxTargets(session);
            return true;
        }
        if (_sandboxClearTowersButton.Contains(point))
        {
            session.ClearSandboxTowers();
            return true;
        }
        if (_sandboxResetButton.Contains(point))
        {
            session.ResetSandboxExperiment();
            return true;
        }
        if (_sandboxProtocolButton.Contains(point))
        {
            ToggleSandboxProtocolTest(session);
            return true;
        }
        return false;
    }

    private void ChangeSandboxWave(MinimalBastion.GameSession session, int direction)
    {
        if (session.AuthoredWaveCount <= 0) return;
        _sandboxWaveNumber = (_sandboxWaveNumber - 1 + direction) % session.AuthoredWaveCount;
        if (_sandboxWaveNumber < 0) _sandboxWaveNumber += session.AuthoredWaveCount;
        _sandboxWaveNumber++;
    }

    private static bool HasSandboxProtocolTestState(MinimalBastion.GameSession session) =>
        session.OverdriveCooldownRemaining > 0 || session.Towers.Any(tower => tower.IsOverdriven);

    private static void ToggleSandboxProtocolTest(MinimalBastion.GameSession session)
    {
        if (HasSandboxProtocolTestState(session))
        {
            session.ResetSandboxProtocol();
            return;
        }

        if (session.SelectedTower is { IsSandboxDisabled: false } tower)
            session.TestSandboxProtocol(tower.Id);
    }

    private void CycleSandboxEnemy(MinimalBastion.GameSession session, int direction)
    {
        var enemies = SandboxEnemies(session);
        if (enemies.Count == 0) return;
        _sandboxEnemyIndex = (_sandboxEnemyIndex + direction) % enemies.Count;
        if (_sandboxEnemyIndex < 0) _sandboxEnemyIndex += enemies.Count;
    }

    private void CycleSandboxGroup() =>
        _sandboxGroupIndex = (_sandboxGroupIndex + 1) % SandboxGroupSizes.Length;

    private void CycleSandboxRank() =>
        _sandboxRankIndex = (_sandboxRankIndex + 1) % SandboxRanks.Length;

    private void CycleSandboxHealth() =>
        _sandboxHealthIndex = (_sandboxHealthIndex + 1) % 4;

    private void SpawnSelectedSandboxTargets(MinimalBastion.GameSession session)
    {
        var enemies = SandboxEnemies(session);
        if (enemies.Count == 0) return;
        _sandboxEnemyIndex = Math.Clamp(_sandboxEnemyIndex, 0, enemies.Count - 1);
        var healthMultiplier = _sandboxHealthIndex switch
        {
            1 => session.SandboxHealthMultiplierForWave(Math.Min(10, session.TotalWaves)),
            2 => session.SandboxHealthMultiplierForWave(session.TotalWaves),
            _ => 1f
        };
        session.SpawnSandboxTargets(
            enemies[_sandboxEnemyIndex].Id,
            SandboxGroupSizes[_sandboxGroupIndex],
            healthMultiplier,
            SandboxRanks[_sandboxRankIndex].ToString(),
            _sandboxHealthIndex == 3);
    }

    private static IReadOnlyList<EnemyDefinition> SandboxEnemies(MinimalBastion.GameSession session) =>
        session.Content.Enemies.Values.OrderBy(enemy => enemy.MaxHealth).ThenBy(enemy => enemy.Id).ToArray();

    private static void RequestStartWave(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (sink is null) session.StartNextWave();
        else sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.StartWave });
    }

    private static void RequestSpeed(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        var speed = session.Speed < 1.5f ? 2f : 1f;
        if (sink is null) session.SetSpeed(speed);
        else sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SetSpeed, Speed = speed });
    }

    private static void RequestPause(MinimalBastion.GameSession session, Action<GameCommand> sink, int playerId) =>
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SetPaused, Paused = !session.IsCoOpPaused });

    private void ToggleTargetPicker(MinimalBastion.GameSession session)
    {
        if (session.SelectedTower is not { IsSupport: false } tower) return;
        if (_targetPickerOpen && _targetPickerTowerId == tower.Id)
        {
            CloseTargetPicker();
            return;
        }
        _targetPickerOpen = true;
        _targetPickerTowerId = tower.Id;
    }

    private void CloseTargetPicker()
    {
        _targetPickerOpen = false;
        _targetPickerTowerId = 0;
        _targetPickerBounds = Rectangle.Empty;
        _targetModeButtons.Clear();
    }

    private static void RequestTargetMode(MinimalBastion.GameSession session, TargetMode mode,
        Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { IsSupport: false } tower || tower.TargetMode == mode) return;
        if (sink is null)
        {
            session.TrySetTargetMode(tower.Id, mode, playerId);
            return;
        }
        sink(new GameCommand
        {
            PlayerId = playerId,
            Type = GameCommandType.SetTargetMode,
            EntityId = tower.Id,
            TargetMode = mode
        });
    }

    private static void RequestUpgrade(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (sink is null)
        {
            session.TryUpgradeSelectedTower();
            return;
        }
        if (session.SelectedTower is { } tower)
            sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.UpgradeTower, EntityId = tower.Id });
        else if (session.SelectedGenerator is not null)
            sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.UpgradeGenerator });
    }

    private static void RequestUpgradeChoice(MinimalBastion.GameSession session, int choiceIndex,
        Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is { RequiresDoctrine: true } doctrineTower &&
            choiceIndex >= 0 && choiceIndex < doctrineTower.Definition.Tier2Doctrines.Count)
        {
            RequestDoctrine(session, doctrineTower.Definition.Tier2Doctrines[choiceIndex].Id, sink, playerId);
            return;
        }

        if (session.SelectedTower is { RequiresSpecialization: true } specializationTower &&
            choiceIndex >= 0 && choiceIndex < specializationTower.Definition.Specializations.Count)
        {
            RequestSpecialization(session, specializationTower.Definition.Specializations[choiceIndex].Id, sink, playerId);
            return;
        }

        if (choiceIndex == 0 && (session.SelectedTower is null || session.SelectedTower.CanUpgrade))
            RequestUpgrade(session, sink, playerId);
    }

    private static void RequestSpecialization(MinimalBastion.GameSession session, string specializationId, Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { } tower) return;
        if (sink is null)
        {
            session.TrySpecializeTower(tower.Id, specializationId, playerId);
            return;
        }
        sink(new GameCommand
        {
            PlayerId = playerId,
            Type = GameCommandType.SpecializeTower,
            EntityId = tower.Id,
            SpecializationId = specializationId
        });
    }

    private static void RequestDoctrine(MinimalBastion.GameSession session, string doctrineId, Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { } tower) return;
        if (sink is null)
        {
            session.TryChooseTowerDoctrine(tower.Id, doctrineId, playerId);
            return;
        }
        sink(new GameCommand
        {
            PlayerId = playerId,
            Type = GameCommandType.ChooseDoctrine,
            EntityId = tower.Id,
            DoctrineId = doctrineId
        });
    }

    private static void RequestOverdrive(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (!session.ProtocolsEnabled) return;
        if (session.SelectedTower is not { } tower) return;
        if (sink is null)
        {
            session.TryOverdriveTower(tower.Id, playerId);
            return;
        }
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.OverdriveTower, EntityId = tower.Id });
    }

    private static void RequestAutoProtocol(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (!session.ProtocolsEnabled) return;
        if (session.SelectedTower is not { } tower) return;
        if (sink is null)
        {
            session.TryToggleAutoProtocol(tower.Id, playerId);
            return;
        }
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.ToggleAutoProtocol, EntityId = tower.Id });
    }

    private static void RequestSell(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (sink is null)
        {
            session.TrySellSelectedTower();
            return;
        }
        if (session.SelectedTower is { } tower)
            sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SellTower, EntityId = tower.Id });
        else if (session.SelectedGenerator is not null)
            sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SellGenerator });
    }

    public UiAction HandlePausedInput(InputSnapshot input, MinimalBastion.GameSession session)
    {
        if (_towerLibraryOpen)
        {
            if (HandleTowerLibraryInput(input)) _towerLibraryOpen = false;
            return UiAction.None;
        }

        if (input.EscapePressed || input.PausePressed)
        {
            _restartArmed = false;
            return UiAction.Resume;
        }
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var selection = 0; selection < 7; selection++)
        {
            if (!PauseMenuOptionRectangle(selection).Contains(point)) continue;
            return ActivatePauseMenuOption(selection, session);
        }
        return UiAction.None;
    }

    private UiAction ActivatePauseMenuOption(int selection, MinimalBastion.GameSession session)
    {
        if (selection == 5)
        {
            if (_restartArmed)
            {
                _restartArmed = false;
                return UiAction.Restart;
            }
            _restartArmed = true;
            return UiAction.None;
        }
        _restartArmed = false;
        if (selection == 1)
        {
            _towerLibraryOpen = true;
            _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
            return UiAction.None;
        }
        return selection switch
        {
            0 => UiAction.Resume,
            2 => UiAction.Settings,
            3 when session.CanSaveCheckpoint => UiAction.SaveGame,
            4 when _saveAvailable => UiAction.LoadGame,
            6 => UiAction.MainMenu,
            _ => UiAction.None
        };
    }

    private Rectangle PauseMenuOptionRectangle(int selection) => selection switch
    {
        0 => _resumeButton,
        1 => _towerLibraryButton,
        2 => _pauseSettingsButton,
        3 => _saveButton,
        4 => _loadButton,
        5 => _restartButton,
        6 => _mainMenuButton,
        _ => Rectangle.Empty
    };

    public UiAction HandleTitleTowerLibrary(InputSnapshot input) =>
        HandleTowerLibraryInput(input) ? UiAction.MainMenu : UiAction.None;

    private bool HandleTowerLibraryInput(InputSnapshot input)
    {
        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, Math.Max(0, _libraryThreats.Count - 1));
        if (input.EscapePressed || input.PausePressed || input.RightPressed) return true;
        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, Math.Max(0, _libraryMaps.Count - 1));
        if (input.NavigateLeftPressed) CycleTowerLibraryTab(-1);
        else if (input.NavigateRightPressed) CycleTowerLibraryTab(1);
        if (input.NavigateUpPressed || input.NavigateDownPressed)
            MoveTowerLibrarySelection(input.NavigateUpPressed ? -1 : 1);
        var activeCount = _libraryShowsSystems || _libraryShowsProfiles
            ? 0
            : _libraryShowsCampaign ? _libraryMaps.Count : _libraryShowsThreats ? _libraryThreats.Count : _libraryTowers.Count;
        if (input.TowerHotkey > 0 && input.TowerHotkey <= activeCount)
        {
            if (_libraryShowsCampaign) _campaignLibraryMapIndex = input.TowerHotkey - 1;
            else if (_libraryShowsThreats) _enemyLibraryIndex = input.TowerHotkey - 1;
            else _towerLibraryIndex = input.TowerHotkey - 1;
            _towerLibraryDoctrineIndex = 0;
        }
        if (!input.LeftPressed) return false;
        var point = input.MousePosition.ToPoint();
        if (_towerLibraryCloseButton.Contains(point)) return true;
        if (_towerLibraryTowerTabButton.Contains(point))
        {
            _libraryShowsThreats = false;
            _libraryShowsCampaign = false;
            _libraryShowsProfiles = false;
            _libraryShowsSystems = false;
            return false;
        }
        if (_towerLibraryThreatTabButton.Contains(point))
        {
            _libraryShowsThreats = true;
            _libraryShowsCampaign = false;
            _libraryShowsProfiles = false;
            _libraryShowsSystems = false;
            return false;
        }
        if (_towerLibraryCampaignTabButton.Contains(point))
        {
            _libraryShowsThreats = false;
            _libraryShowsCampaign = true;
            _libraryShowsProfiles = false;
            _libraryShowsSystems = false;
            return false;
        }
        if (_towerLibraryProfilesTabButton.Contains(point))
        {
            _libraryShowsThreats = false;
            _libraryShowsCampaign = false;
            _libraryShowsProfiles = true;
            _libraryShowsSystems = false;
            return false;
        }
        if (_towerLibrarySystemsTabButton.Contains(point))
        {
            _libraryShowsThreats = false;
            _libraryShowsCampaign = false;
            _libraryShowsProfiles = false;
            _libraryShowsSystems = true;
            return false;
        }
        if (_libraryShowsSystems || _libraryShowsProfiles) return false;
        if (_libraryShowsCampaign)
        {
            for (var index = 0; index < _libraryMaps.Count; index++)
            {
                if (!CampaignLibraryMapRow(index).Contains(point)) continue;
                _campaignLibraryMapIndex = index;
                break;
            }
            return false;
        }
        if (_libraryShowsThreats)
        {
            for (var index = 0; index < _libraryThreats.Count; index++)
            {
                if (!EnemyLibraryRow(index).Contains(point)) continue;
                _enemyLibraryIndex = index;
                break;
            }
            return false;
        }
        if (_towerLibraryDoctrineAButton.Contains(point))
        {
            if (_libraryTowers.ElementAtOrDefault(_towerLibraryIndex)?.Tier2Doctrines.Count >= 1)
                _towerLibraryDoctrineIndex = 0;
            return false;
        }
        if (_towerLibraryDoctrineBButton.Contains(point))
        {
            if (_libraryTowers.ElementAtOrDefault(_towerLibraryIndex)?.Tier2Doctrines.Count >= 2)
                _towerLibraryDoctrineIndex = 1;
            return false;
        }
        for (var index = 0; index < _libraryTowers.Count; index++)
        {
            if (!TowerLibraryRow(index).Contains(point)) continue;
            _towerLibraryIndex = index;
            _towerLibraryDoctrineIndex = 0;
            break;
        }
        return false;
    }

    private void CycleTowerLibraryTab(int direction)
    {
        const int pageCount = 5;
        var currentPage = _libraryShowsThreats ? 1
            : _libraryShowsCampaign ? 2
            : _libraryShowsProfiles ? 3
            : _libraryShowsSystems ? 4
            : 0;
        var nextPage = (currentPage + Math.Sign(direction) + pageCount) % pageCount;
        _libraryShowsThreats = nextPage == 1;
        _libraryShowsCampaign = nextPage == 2;
        _libraryShowsProfiles = nextPage == 3;
        _libraryShowsSystems = nextPage == 4;
    }

    private void MoveSaveSlotSelection(int delta)
    {
        if (_saveSlots.Count == 0) return;
        var current = _saveSlots.ToList().FindIndex(slot => slot.Slot == _selectedSaveSlot);
        var next = Math.Clamp(current < 0 ? 0 : current + delta, 0, _saveSlots.Count - 1);
        _selectedSaveSlot = _saveSlots[next].Slot;
        _saveSlotPage = next / _saveSlotRows.Length;
        DisarmSaveSlotDeletion();
    }

    private void MoveSaveSlotPage(int delta)
    {
        if (_saveSlots.Count == 0) return;
        var pageCount = Math.Max(1, (_saveSlots.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var nextPage = Math.Clamp(_saveSlotPage + delta, 0, pageCount - 1);
        if (nextPage == _saveSlotPage) return;
        _saveSlotPage = nextPage;
        _selectedSaveSlot = _saveSlots[_saveSlotPage * _saveSlotRows.Length].Slot;
        DisarmSaveSlotDeletion();
    }

    private void DisarmSaveSlotDeletion()
    {
        if (_saveSlotDeleteArmed) _persistenceStatus = "Deletion cancelled.";
        _saveSlotDeleteArmed = false;
    }

    private void MoveRunHistorySelection(int delta)
    {
        if (_runHistory.Count == 0) return;
        var current = _runHistory.ToList().FindIndex(entry => entry.RunId == _selectedRunHistoryId);
        var next = Math.Clamp(current < 0 ? 0 : current + delta, 0, _runHistory.Count - 1);
        _selectedRunHistoryId = _runHistory[next].RunId;
        _runHistoryPage = next / _saveSlotRows.Length;
        _runHistoryDeleteArmed = false;
    }

    private void MoveRunHistoryPage(int delta)
    {
        if (_runHistory.Count == 0) return;
        var pageCount = Math.Max(1, (_runHistory.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var nextPage = Math.Clamp(_runHistoryPage + delta, 0, pageCount - 1);
        if (nextPage == _runHistoryPage) return;
        _runHistoryPage = nextPage;
        _selectedRunHistoryId = _runHistory[_runHistoryPage * _saveSlotRows.Length].RunId;
        _runHistoryDeleteArmed = false;
    }

    private void MoveTowerLibrarySelection(int delta)
    {
        if (_libraryShowsSystems || _libraryShowsProfiles) return;
        if (_libraryShowsCampaign)
        {
            if (_libraryMaps.Count > 0) _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex + delta, 0, _libraryMaps.Count - 1);
        }
        else if (_libraryShowsThreats)
        {
            if (_libraryThreats.Count > 0) _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex + delta, 0, _libraryThreats.Count - 1);
        }
        else if (_libraryTowers.Count > 0)
        {
            _towerLibraryIndex = Math.Clamp(_towerLibraryIndex + delta, 0, _libraryTowers.Count - 1);
            _towerLibraryDoctrineIndex = 0;
        }
    }

    public UiAction HandleResultInput(InputSnapshot input, bool victory)
    {
        if (input.TabPressed || input.NavigateLeftPressed || input.NavigateRightPressed ||
            input.NavigateUpPressed || input.NavigateDownPressed)
        {
            _restartArmed = false;
            var reverse = input.NavigateLeftPressed || input.NavigateUpPressed;
            _resultMenuSelection = (_resultMenuSelection + (reverse ? -1 : 1) + 3) % 3;
            return UiAction.None;
        }
        if (input.EnterPressed) return ActivateResultSelection(victory);
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var selection = 0; selection < 3; selection++)
        {
            if (!ResultOptionRectangle(selection).Contains(point)) continue;
            _resultMenuSelection = selection;
            return ActivateResultSelection(victory);
        }
        return UiAction.None;
    }

    private UiAction ActivateResultSelection(bool victory)
    {
        if (_resultMenuSelection == 1)
        {
            if (_restartArmed)
            {
                _restartArmed = false;
                return UiAction.Restart;
            }
            _restartArmed = true;
            return UiAction.None;
        }
        _restartArmed = false;
        return _resultMenuSelection == 0
            ? victory ? UiAction.ContinueEndless : UiAction.ViewField
            : UiAction.MainMenu;
    }

    private Rectangle ResultOptionRectangle(int selection) => selection switch
    {
        0 => _resultContinueButton,
        1 => _resultRestartButton,
        2 => _resultMenuButton,
        _ => Rectangle.Empty
    };

    public UiAction HandleDefeatFieldInput(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.ViewResults;
        return input.LeftPressed && _fieldResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.ViewResults
            : UiAction.None;
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer p, GameState state, MinimalBastion.GameSession? session)
    {
        _readOnlyInspection = state is GameState.DefeatField or GameState.RunHistoryField;
        _archivedLayoutInspection = state == GameState.RunHistoryField;
        if (state == GameState.MainMenu)
        {
            DrawMainMenu(batch, p);
            return;
        }
        if (state == GameState.GameSetup)
        {
            DrawGameSetup(batch, p);
            return;
        }
        if (state == GameState.LoadingTransition)
        {
            DrawLoadingTransition(batch, p);
            return;
        }
        if (state == GameState.TowerLibrary)
        {
            DrawTowerLibrary(batch, p, "title screen");
            return;
        }
        if (state == GameState.Settings)
        {
            DrawSettings(batch, p);
            return;
        }
        if (state == GameState.SaveSlots)
        {
            DrawSaveSlots(batch, p);
            return;
        }
        if (state == GameState.RunHistory)
        {
            DrawRunHistory(batch, p);
            return;
        }
        if (state == GameState.CoOpMenu)
        {
            DrawCoOpMenu(batch, p);
            return;
        }
        if (state == GameState.CoOpLobby)
        {
            DrawCoOpLobby(batch, p);
            return;
        }

        if (session is null) return;
        DrawHud(batch, p, session);
        DrawSidebar(batch, p, session);
        DrawTacticalBar(batch, p, session);
        if (session.PlacementTowerId is not null || session.TacticalPlacement != TacticalPlacementKind.None) DrawPlacementStatus(batch, p, session);
        if (state == GameState.Playing && session.IsCoOp) DrawRemoteCoOpCursor(batch, p, session);
        if (state == GameState.Playing) DrawAnnouncement(batch, p, session);
        if (state == GameState.Playing && session.IsCoOpPaused)
        {
            DrawCoOpPausedBanner(batch, p, session.CoOpPausePlayerId);
        }
        if (state == GameState.Playing && session.IsCoOp && _towerLibraryOpen)
            DrawTowerLibrary(batch, p, session.IsCoOpPaused ? "co-op pause" : "live co-op");

        if (state == GameState.Paused) DrawPauseOverlay(batch, p, session);
        else if (state == GameState.CoOpReconnect) DrawCoOpReconnectOverlay(batch, p);
        else if (state == GameState.Victory) DrawResultOverlay(batch, p, session, true);
        else if (state == GameState.Defeat) DrawResultOverlay(batch, p, session, false);
        else if (state == GameState.DefeatField) DrawDefeatFieldControls(batch, p);
        else if (state == GameState.RunHistoryField) DrawRunHistoryFieldControls(batch, p);
    }

    private void DrawRemoteCoOpCursor(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (_remoteCoOpCursor is not { } position || _remoteCoOpCursorPlayerId is < 1 or > 2) return;
        var color = _remoteCoOpCursorPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral;
        if (_remoteCoOpHasPlacementPreview && !string.IsNullOrWhiteSpace(_remoteCoOpPlacementTowerId) &&
            session.Content.Towers.TryGetValue(_remoteCoOpPlacementTowerId, out var placementDefinition))
        {
            var needlePreview = placementDefinition.Id.Equals("needle_turret", StringComparison.OrdinalIgnoreCase);
            DrawRemotePlacementGhost(batch, p, position, placementDefinition.Visual,
                placementDefinition.Visual.Marks, true, color,
                needlePreview ? ColorPalette.NeedlePlacementGhostPrimaryAlpha : ColorPalette.PlacementGhostPrimaryAlpha,
                needlePreview ? ColorPalette.NeedlePlacementGhostAccentAlpha : ColorPalette.PlacementGhostAccentAlpha);
            GameRenderer.DrawPowerNodePlacementIndicator(batch, p, _remoteCoOpPlacementPreviewPosition,
                placementDefinition.Visual.Radius,
                session.Map.GetPowerNodes(_remoteCoOpPlacementPreviewPosition));
        }
        else if (_remoteCoOpHasPlacementPreview && _remoteCoOpTacticalPlacement == TacticalPlacementKind.PulsePlate)
        {
            var plate = session.Content.Tactics.EmergencyDefense;
            DrawRemotePlacementGhost(batch, p, position, plate.Visual, plate.Charges, false, color);
        }
        else if (_remoteCoOpHasPlacementPreview && _remoteCoOpTacticalPlacement == TacticalPlacementKind.ChargeForge)
        {
            var forge = session.Content.Tactics.Generator;
            DrawRemotePlacementGhost(batch, p, position, forge.Visual, 1, true, color);
        }
        if (_remoteCoOpSelectedTowerId > 0 && session.Towers.FirstOrDefault(tower => tower.Id == _remoteCoOpSelectedTowerId) is { } tower)
        {
            var radius = tower.Definition.Visual.Radius;
            const int tagWidth = 34;
            const int tagHeight = 15;
            var tagY = (int)MathF.Round(tower.Position.Y - radius - tagHeight - 6f);
            if (tagY < GameConstants.TopBarHeight + 2)
                tagY = (int)MathF.Round(tower.Position.Y + radius + 7f);
            var tag = new Rectangle((int)MathF.Round(tower.Position.X - tagWidth * 0.5f), tagY,
                tagWidth, tagHeight);
            var shadow = new Rectangle(tag.X - 2, tag.Y - 2, tag.Width + 4, tag.Height + 4);
            p.FillRect(batch, shadow, ColorPalette.WithAlpha(ColorPalette.Navy, 245));
            p.FillRect(batch, tag, color);
            p.DrawRect(batch, tag, ColorPalette.Paper, 1);
            // SpriteFont exposes line bounds rather than tight glyph ink bounds.
            // Each short label needs its own optical correction; keep these
            // independent so tuning one player never shifts the other.
            var playerLabelOffsetY = _remoteCoOpCursorPlayerId == 2 ? 1.25f : 0.25f;
            DrawFittedCenteredText(batch, $"P{_remoteCoOpCursorPlayerId}",
                tag.Center.ToVector2() + new Vector2(0, playerLabelOffsetY),
                ColorPalette.HighContrastText(color), 0.34f, tag.Width - 6);

            var tagAboveTower = tag.Center.Y < tower.Position.Y;
            var tagAnchor = new Vector2(tag.Center.X, tagAboveTower ? tag.Bottom : tag.Top);
            var towerAnchor = tower.Position + new Vector2(0, tagAboveTower ? -radius - 2f : radius + 2f);
            p.Line(batch, tagAnchor, towerAnchor, color, 2);
            var pointerCenter = tagAnchor + new Vector2(0, tagAboveTower ? 3f : -3f);
            p.DrawPolygon(batch, pointerCenter, 4.5f, 3, false, color,
                tagAboveTower ? MathHelper.PiOver2 : -MathHelper.PiOver2);
        }
        p.Ring(batch, position, 8, color, 2);
        p.Circle(batch, position, 2.5f, color);
        p.Line(batch, position + new Vector2(-15, 0), position + new Vector2(-10, 0), color, 2);
        p.Line(batch, position + new Vector2(10, 0), position + new Vector2(15, 0), color, 2);
        p.Line(batch, position + new Vector2(0, -15), position + new Vector2(0, -10), color, 2);
        p.Line(batch, position + new Vector2(0, 10), position + new Vector2(0, 15), color, 2);
        DrawText(batch, $"P{_remoteCoOpCursorPlayerId}", position + new Vector2(13, -17), color, 0.36f);
    }

    private void DrawRemotePlacementGhost(SpriteBatch batch, PrimitiveRenderer p, Vector2 cursorPosition,
        TowerVisualData visual, int marks, bool levelMarks, Color playerColor,
        byte primaryAlpha = ColorPalette.PlacementGhostPrimaryAlpha,
        byte accentAlpha = ColorPalette.PlacementGhostAccentAlpha)
    {
        var previewPosition = _remoteCoOpPlacementPreviewPosition;
        if (Vector2.DistanceSquared(cursorPosition, previewPosition) > 36f)
            p.Line(batch, cursorPosition, previewPosition, ColorPalette.WithAlpha(playerColor, 115), 1);
        var breath = (MathF.Sin(_visualTimeSeconds * 2.2f) + 1f) * 0.5f;
        var pulse = 0.985f + breath * 0.025f;
        var ghostPrimary = ColorPalette.WithPremultipliedAlpha(visual.PrimaryColor, primaryAlpha);
        var ghostAccent = ColorPalette.WithPremultipliedAlpha(visual.AccentColor, accentAlpha);

        // Local and remote placement ghosts share the same authored translucency.
        // The subtle breathing scale and remote cursor identify active manipulation
        // without making the uncommitted tower look placed.
        p.DrawShape(batch, previewPosition, visual.Radius, visual.Shape, ghostPrimary, ghostAccent,
            marks, true, pulse, levelMarks);
    }

    private void DrawHud(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.TopBarHeight), ColorPalette.Navy);
        p.FillRect(batch, new Rectangle(0, GameConstants.TopBarHeight - 2, GameConstants.LogicalWidth, 2), ColorPalette.Cyan);
        DrawText(batch, "LIVES", new Vector2(18, 8), ColorPalette.Coral, 0.75f);
        DrawText(batch, session.IsSandbox ? "UNLIMITED" : $"{session.Economy.Lives}/{session.Economy.StartingLives}",
            new Vector2(18, 26), ColorPalette.Paper, session.IsSandbox ? 0.64f : 1f);
        DrawText(batch, "CREDITS", new Vector2(115, 8), ColorPalette.Gold, 0.75f);
        DrawText(batch, session.IsSandbox ? "UNLIMITED" : session.Economy.Credits.ToString(),
            new Vector2(115, 26), ColorPalette.Paper, session.IsSandbox ? 0.64f : 1f);
        DrawText(batch, session.IsSandbox ? "MODE" : session.IsMasteryMode ? "MASTERY" : session.IsEndlessMode ? "ENDLESS" : "WAVE",
            new Vector2(225, 8), ColorPalette.Cyan, 0.75f);
        DrawText(batch, session.IsSandbox ? "LAB" : session.IsMasteryMode ? $"{session.CurrentWave}/{GameConstants.MasteryFinalWave}" : session.IsEndlessMode ? session.CurrentWave.ToString() : $"{session.CurrentWave}/{session.TotalWaves}",
            new Vector2(225, 26), ColorPalette.Paper, session.IsSandbox ? 0.84f : 1f);
        DrawText(batch, session.IsSandbox ? "TARGETS" : "ENEMIES", new Vector2(335, 8), ColorPalette.Lime, 0.75f);
        DrawText(batch, session.EnemiesRemaining.ToString(), new Vector2(335, 26), ColorPalette.Paper, 1f);

        if (_archivedLayoutInspection)
        {
            DrawFittedText(batch, "FINAL LAYOUT", new Vector2(HudThreatBounds.X, 8), ColorPalette.Gold, 0.56f,
                HudThreatBounds.Width);
            DrawFittedText(batch, "PATH CLEARED FOR REVIEW", new Vector2(HudThreatBounds.X, 27), ColorPalette.Paper, 0.58f,
                HudThreatBounds.Width);
        }
        else if (!session.IsSandbox)
        {
            var previewWave = session.Waves.ActiveWave ?? session.Waves.NextWave;
            if (previewWave is not null)
            {
                var intel = WaveIntel.Analyze(previewWave, session.Content.Enemies);
                var threatState = session.Waves.IsActive ? "ACTIVE" : "NEXT";
                DrawFittedText(batch,
                    $"{threatState} | {intel.ScalingSummary(session.Difficulty.EnemyHealthMultiplier, session.Difficulty.EnemySpeedMultiplier)}",
                    new Vector2(HudThreatBounds.X, 8), ColorPalette.Gold, 0.56f, HudThreatBounds.Width);
                var bountyMultiplier = MinimalBastion.Economy.Economy.CalculateKillRewardMultiplier(previewWave.Number);
                var bountySuffix = bountyMultiplier < 0.995f ? $"  |  BOUNTY {bountyMultiplier:P0}" : "";
                DrawFittedText(batch, $"{intel.ApproximateCount}  {intel.CompactThreats}{bountySuffix}",
                    new Vector2(HudThreatBounds.X, 27), ColorPalette.Paper, 0.68f, HudThreatBounds.Width);
            }
        }

        _startWaveButton = new Rectangle(450, 9, 170, 38);
        _speedButton = new Rectangle(630, 9, 76, 38);
        _pauseButton = new Rectangle(716, 9, 90, 38);
        var startWaveLabel = session.IsSandbox
            ? session.SandboxWaveActive ? $"DEPLOYING W{_sandboxWaveNumber}" : $"SEND TEST W{_sandboxWaveNumber}"
            : SoloWaveButtonLabel(session, _settings.AutoStartWaves, _settings.AutoStartDelaySeconds);
        var startWaveEnabled = session.CanStartWave && !session.IsCoOpPaused;
        if (session.IsCoOp && session.CanStartWave)
        {
            var localBit = 1 << (session.LocalPlayerId - 1);
            var localReady = (_coOpWaveReadyMask & localBit) != 0;
            startWaveLabel = CoOpWaveButtonLabel(session.LocalPlayerId, session.CurrentWave,
                _coOpWaveReadyMask, _coOpWaveStartQueued, _coOpEarlyBonusQueued, session.IntermissionRemaining);
            startWaveEnabled = !_coOpWaveStartQueued && !localReady;
        }
        DrawButton(batch, p, _startWaveButton, startWaveLabel, startWaveEnabled, ColorPalette.Green,
            session.IsSandbox ? ColorPalette.Paper : null, "SPC");
        DrawButton(batch, p, _speedButton, session.Speed >= 1.5f ? "2x" : "1x", !session.IsCoOpPaused, ColorPalette.Violet,
            session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Violet) : null, "S");
        var pauseLabel = session.IsCoOpPaused ? "RESUME" : "PAUSE";
        var pauseFill = session.IsCoOpPaused ? ColorPalette.Green : ColorPalette.Coral;
        DrawButton(batch, p, _pauseButton, pauseLabel, !session.IsCoOp || _coOpPeerConnected, pauseFill,
            session.IsSandbox ? ContrastAwareButtonTextColor(pauseFill) : null, "P");

        if (session.IsSandbox)
        {
            _sandboxWavePreviousButton = new Rectangle(820, 9, 62, 38);
            _sandboxWaveNextButton = new Rectangle(890, 9, 62, 38);
            DrawSandboxButton(batch, p, _sandboxWavePreviousButton, "WAVE", true, ColorPalette.Cyan, "-");
            DrawSandboxButton(batch, p, _sandboxWaveNextButton, "WAVE", true, ColorPalette.Cyan, "+");
        }
        else
        {
            _sandboxWavePreviousButton = Rectangle.Empty;
            _sandboxWaveNextButton = Rectangle.Empty;
        }

        DrawText(batch, "RUN SETUP", new Vector2(HudRunSetupBounds.X, 8), ColorPalette.Cyan, 0.55f);
        DrawFittedText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(HudRunSetupBounds.X, 28), ColorPalette.Paper, 0.52f, HudRunSetupBounds.Width);
    }

    private void DrawCoOpPausedBanner(SpriteBatch batch, PrimitiveRenderer p, int pausedByPlayerId)
    {
        var rect = _coOpPausedBanner;
        p.FillRect(batch, rect, ColorPalette.Navy);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 5, rect.Height), ColorPalette.Green);
        p.DrawRect(batch, rect, ColorPalette.Cyan, 2);
        var owner = pausedByPlayerId is 1 or 2 ? $"P{pausedByPlayerId}" : "PEER";
        DrawFittedCenteredText(batch, $"{owner} PAUSED  |  FIELD LOCKED",
            new Vector2(rect.Center.X, rect.Center.Y), ColorPalette.Paper, 0.44f, rect.Width - 20);
    }

    private void DrawAnnouncement(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (session.AnnouncementRemaining <= 0 || string.IsNullOrWhiteSpace(session.AnnouncementTitle)) return;
        var fade = MathHelper.Clamp(session.AnnouncementRemaining / 0.35f, 0, 1);
        var alpha = (byte)(232 * fade);
        var accent = session.AnnouncementPositive ? ColorPalette.Green : ColorPalette.Gold;
        var rect = new Rectangle(170, 112, 620, 62);
        p.FillRect(batch, rect, ColorPalette.WithAlpha(ColorPalette.Navy, alpha));
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 5, rect.Height), ColorPalette.WithAlpha(accent, alpha));
        DrawFittedCenteredText(batch, session.AnnouncementTitle, new Vector2(rect.Center.X, rect.Y + 19),
            ColorPalette.WithAlpha(ColorPalette.Paper, alpha), 0.72f, rect.Width - 32);
        DrawFittedCenteredText(batch, session.AnnouncementSubtitle ?? "", new Vector2(rect.Center.X, rect.Y + 43),
            ColorPalette.WithAlpha(ColorPalette.PanelAlt, alpha), 0.51f, rect.Width - 32);
    }

    private void DrawTacticalBar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (session.IsSandbox) return;
        if (_archivedLayoutInspection)
        {
            _emergencyButton = Rectangle.Empty;
            _generatorButton = Rectangle.Empty;
            _overdriveButton = Rectangle.Empty;
            _autoProtocolButton = Rectangle.Empty;
            var archiveNotice = new Rectangle(972, 98, 296, 96);
            p.FillRect(batch, archiveNotice, ColorPalette.PanelAlt);
            p.DrawRect(batch, archiveNotice, ColorPalette.Cyan, 1);
            DrawFittedCenteredText(batch, "READ-ONLY FINAL DEFENSE", new Vector2(archiveNotice.Center.X, 113),
                ColorPalette.Navy, 0.51f, archiveNotice.Width - 20);
            DrawFittedCenteredText(batch, "SELECT A PLACED TOWER TO INSPECT", new Vector2(archiveNotice.Center.X, 139),
                ColorPalette.Cobalt, 0.42f, archiveNotice.Width - 20);
            DrawFittedCenteredText(batch, "ENEMIES, SHOTS, AND EFFECTS OMITTED", new Vector2(archiveNotice.Center.X, 165),
                ColorPalette.Muted, 0.39f, archiveNotice.Width - 20);
            return;
        }
        if (session.IsCoOpPaused)
        {
            _emergencyButton = Rectangle.Empty;
            _generatorButton = Rectangle.Empty;
            _overdriveButton = Rectangle.Empty;
            _autoProtocolButton = Rectangle.Empty;
            return;
        }
        _emergencyButton = new Rectangle(972, 98, 296, 28);
        _generatorButton = new Rectangle(972, 132, 296, 28);
        _overdriveButton = new Rectangle(972, 166, 218, 28);
        _autoProtocolButton = new Rectangle(1196, 166, 72, 28);
        var defense = session.Content.Tactics.EmergencyDefense;
        var plateFieldFull = session.EmergencyDefenses.Count >= defense.MaximumActive;
        var emergencyReady = session.TacticalSystemsEnabled && !plateFieldFull && (session.EmergencyInventory > 0 || session.CanDirectPurchaseEmergencyDefense);
        var emergencyLabel = session.TacticalSystemsEnabled ? PulsePlateButtonLabel(session) : "PLATES | DIRECTIVE OFF";
        DrawButton(batch, p, _emergencyButton, emergencyLabel, emergencyReady, ColorPalette.Gold, ColorPalette.Ink, "Q");

        var generator = session.Content.Tactics.Generator;
        var generatorReady = session.TacticalSystemsEnabled && (session.Generator is not null || session.Economy.CanAfford(generator.PurchaseCost));
        var generatorLabel = !session.TacticalSystemsEnabled ? "FORGE | DIRECTIVE OFF" : session.Generator is { } active
            ? session.EmergencyInventory >= active.Level.Capacity
                ? $"FORGE L{active.LevelIndex + 1} | FULL"
                : session.Waves.IsActive
                    ? $"FORGE L{active.LevelIndex + 1} | +1 IN {active.ProductionRemaining:0}s"
                    : $"FORGE L{active.LevelIndex + 1} | PAUSED {active.ProductionRemaining:0}s"
            : $"FORGE {generator.PurchaseCost} | ACTIVE WAVES";
        DrawButton(batch, p, _generatorButton, generatorLabel, generatorReady, ColorPalette.Green, hotkey: "G");

        var selected = session.SelectedTower;
        var activeOverdrive = session.Towers.FirstOrDefault(x => x.IsOverdriven);
        var overdriveReady = session.ProtocolsEnabled && selected is not null && session.OverdriveCooldownRemaining <= 0 && !selected.IsOverdriven;
        var overdriveLabel = !session.ProtocolsEnabled ? "PROTOCOLS | DIRECTIVE OFF" :
            activeOverdrive is not null ? $"{activeOverdrive.Protocol.DisplayName.ToUpperInvariant()} {activeOverdrive.OverdriveRemaining:0.0}s" :
            session.OverdriveCooldownRemaining > 0 ? $"PROTOCOL | {session.OverdriveCooldownRemaining:0.0}s" :
            selected is null ? "PROTOCOL | SELECT" :
            selected.Protocol.DisplayName.ToUpperInvariant();
        DrawButton(batch, p, _overdriveButton, overdriveLabel, overdriveReady, ColorPalette.Coral, hotkey: "E");
        var armedAutoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);
        var autoActive = session.ProtocolsEnabled && selected is not null && armedAutoTower == selected;
        var autoLabel = !session.ProtocolsEnabled ? "OFF" : autoActive ? "ON" :
            armedAutoTower is not null && selected is not null ? "MOVE" :
            armedAutoTower is not null ? "ARMED" : "ARM";
        DrawButton(batch, p, _autoProtocolButton, autoLabel,
            session.ProtocolsEnabled && selected is not null,
            ColorPalette.Auto, hotkey: "A");
    }

    private void DrawSidebar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(GameConstants.SidebarX, GameConstants.TopBarHeight, 320, GameConstants.LogicalHeight - GameConstants.TopBarHeight), ColorPalette.Panel);
        p.Line(batch, new Vector2(GameConstants.SidebarX, GameConstants.TopBarHeight), new Vector2(GameConstants.SidebarX, GameConstants.LogicalHeight), ColorPalette.Divider, 2);
        p.FillRect(batch, new Rectangle(972, 56, 296, 34), ColorPalette.Navy);
        if (session.IsCoOp && !session.IsSandbox)
        {
            DrawFittedCenteredText(batch, "TACTICAL SYSTEMS", CoOpTacticalTitleBounds.Center.ToVector2(),
                ColorPalette.Paper, 0.82f, CoOpTacticalTitleBounds.Width);
        }
        else
        {
            DrawText(batch, session.IsSandbox ? "SANDBOX LAB" : _archivedLayoutInspection ? "DEFENSE ARCHIVE" : "TACTICAL SYSTEMS",
                new Vector2(986, 64), ColorPalette.Paper,
                session.IsSandbox ? 1.08f : _archivedLayoutInspection ? 0.88f : 1.0f);
        }
        if (session.IsSandbox)
            DrawSandboxBar(batch, p, session);
        else if (session.IsCoOp)
        {
            var linkLabel = CoOpSidebarLinkStatusLabel(_coOpPeerConnected, _coOpResyncing, _coOpLinkSilenceSeconds);
            var linkColor = !_coOpPeerConnected ? ColorPalette.Coral : _coOpResyncing ? ColorPalette.Cyan :
                _coOpLinkSilenceSeconds >= 5 ? ColorPalette.Coral : _coOpLinkSilenceSeconds >= 1.5f ? ColorPalette.Gold : ColorPalette.Green;
            p.FillRect(batch, CoOpLinkStatusBounds, ColorPalette.WithAlpha(ColorPalette.Ink, 120));
            p.DrawRect(batch, CoOpLinkStatusBounds, ColorPalette.WithAlpha(linkColor, 190), 1);
            DrawFittedCenteredText(batch, linkLabel, CoOpLinkStatusBounds.Center.ToVector2(), linkColor, 0.37f,
                CoOpLinkStatusBounds.Width - 6);
            var readyStatus = session.CanStartWave
                ? CoOpReadyStatusLabel(session.CurrentWave, _coOpWaveReadyMask, _coOpWaveStartQueued,
                    _coOpEarlyBonusQueued, session.IntermissionRemaining)
                : "SHARED WAVE ACTIVE";
            p.FillRect(batch, CoOpReadyStatusBounds, ColorPalette.WithAlpha(ColorPalette.Ink, 72));
            DrawFittedCenteredText(batch, readyStatus, CoOpReadyStatusBounds.Center.ToVector2(), ColorPalette.Gold, 0.30f,
                CoOpReadyStatusBounds.Width - 8);
        }
        p.FillRect(batch, new Rectangle(972, 90, 296, 3), ColorPalette.Gold);

        if (session.IsCoOpPaused)
        {
            DrawCoOpPauseSidebar(batch, p, session);
            return;
        }

        p.FillRect(batch, new Rectangle(972, 200, 296, 3), ColorPalette.Cyan);
        DrawText(batch, _archivedLayoutInspection ? "TOWER LEGEND" : "TOWER WORKSHOP", new Vector2(980, 207),
            ColorPalette.Navy, 0.78f);

        _towerCards.Clear();
        var towers = session.Content.Towers.Values.OrderBy(x => x.PurchaseCost).ToList();
        for (var index = 0; index < towers.Count; index++)
        {
            var definition = towers[index];
            var column = index % 2;
            var row = index / 2;
            var rect = new Rectangle(972 + column * 148, 228 + row * 44, 140, 39);
            _towerCards[definition.Id] = rect;
            var available = session.IsTowerAvailable(definition.Id);
            var affordable = available && session.Economy.CanAfford(definition.PurchaseCost);
            var selected = _archivedLayoutInspection
                ? session.SelectedTower?.Definition.Id == definition.Id
                : session.PlacementTowerId == definition.Id;
            var cardFill = !_archivedLayoutInspection && !available
                ? ColorPalette.Disabled
                : selected ? ColorPalette.Tint(definition.Visual.PrimaryColor, 0.42f) : ColorPalette.PanelAlt;
            var cardOutline = _archivedLayoutInspection
                ? selected ? definition.Visual.PrimaryColor : ColorPalette.CardOutline
                : !available ? ColorPalette.Muted : selected ? definition.Visual.PrimaryColor : affordable ? ColorPalette.CardOutline : ColorPalette.Coral;
            p.FillRect(batch, rect, cardFill);
            p.DrawRect(batch, rect, cardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(rect.X + 17, rect.Center.Y), 10, definition.Visual.Shape, definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
            var placedCount = _archivedLayoutInspection
                ? session.Towers.Count(tower => tower.Definition.Id == definition.Id)
                : 0;
            if (_archivedLayoutInspection || _settings.ShowHotkeyBadges)
            {
                DrawText(batch, _archivedLayoutInspection ? placedCount.ToString() : index == 9 ? "0" : (index + 1).ToString(),
                    new Vector2(rect.Right - 14, rect.Y + 7), selected
                        ? definition.Visual.AccentColor
                        : ColorPalette.Muted, 0.39f, true);
            }
            DrawFittedText(batch, definition.DisplayName, new Vector2(rect.X + 38, rect.Y + 5), ColorPalette.Ink, 0.53f, 80);
            var cardSubtitle = _archivedLayoutInspection
                ? placedCount > 0 ? $"{placedCount} PLACED  {TowerInfo.ShortRole(definition)}" : TowerInfo.ShortRole(definition)
                : available ? $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}" : "DIRECTIVE OFF";
            DrawFittedText(batch, cardSubtitle, new Vector2(rect.X + 38, rect.Y + 21),
                _archivedLayoutInspection ? placedCount > 0
                    ? definition.Visual.AccentColor
                    : ColorPalette.Muted
                    : available ? affordable ? ColorPalette.Muted : ColorPalette.Coral : ColorPalette.Muted,
                0.44f, 92);
        }

        p.FillRect(batch, new Rectangle(972, 450, 296, 3), ColorPalette.Violet);
        DrawText(batch, "TOWER INTEL", new Vector2(980, 456), ColorPalette.Navy, 0.70f);
        DrawTowerIntel(batch, p, session);
    }

    private void DrawCoOpPauseSidebar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _towerCards.Clear();
        var owner = session.CoOpPausePlayerId is 1 or 2 ? $"P{session.CoOpPausePlayerId}" : "PEER";
        DrawText(batch, "SHARED PAUSE", new Vector2(986, 112), ColorPalette.Navy, 1.02f);
        DrawText(batch, $"{owner} paused the match", new Vector2(986, 144), ColorPalette.Muted, 0.55f);
        DrawText(batch, "FIELD CONTROLS LOCKED", new Vector2(986, 166), ColorPalette.Coral, 0.50f);

        DrawButton(batch, p, CoOpPauseResumeBounds, "RESUME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, CoOpPauseLibraryBounds, "TACTICAL LIBRARY", true, ColorPalette.Cyan);
        DrawButton(batch, p, CoOpPauseRestartBounds, _restartArmed ? "CONFIRM RESTART" : "RESTART", true,
            _restartArmed ? ColorPalette.Coral : ColorPalette.Berry);
        DrawButton(batch, p, CoOpPauseMenuBounds, "MAIN MENU", true, ColorPalette.Coral);

        var timing = session.Waves.IsActive
            ? "COMBAT AND ABILITY TIMERS FROZEN"
            : session.IntermissionRemaining > 0
                ? $"EARLY CALL EXPIRES IN {MathF.Ceiling(session.IntermissionRemaining):0}s"
                : "BETWEEN WAVES";
        DrawFittedCenteredText(batch, timing, new Vector2(1120, 448), ColorPalette.Gold, 0.48f, 270);
        DrawFittedCenteredText(batch, "TAB opens the library without pausing.", new Vector2(1120, 474),
            ColorPalette.Muted, 0.43f, 270);
        DrawFittedCenteredText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(1120, 516), session.Challenge.AccentColor, 0.42f, 270);
        DrawFittedCenteredText(batch, "Building, upgrades, sales, plates, and Protocols resume with the match.",
            new Vector2(1120, 552), ColorPalette.Muted, 0.40f, 270);
    }

    private void DrawSandboxBar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _emergencyButton = Rectangle.Empty;
        _generatorButton = Rectangle.Empty;
        _overdriveButton = Rectangle.Empty;
        _autoProtocolButton = Rectangle.Empty;

        var enemies = SandboxEnemies(session);
        _sandboxEnemyIndex = Math.Clamp(_sandboxEnemyIndex, 0, Math.Max(0, enemies.Count - 1));
        var enemy = enemies.Count > 0 ? enemies[_sandboxEnemyIndex] : null;

        _sandboxEnemyPreviousButton = new Rectangle(972, 98, 38, 28);
        var enemyDisplay = new Rectangle(1014, 98, 208, 28);
        _sandboxEnemyNextButton = new Rectangle(1226, 98, 42, 28);
        DrawSandboxButton(batch, p, _sandboxEnemyPreviousButton, "<", enemies.Count > 1, ColorPalette.Cyan, "[");
        var enemyFill = enemy?.Visual.PrimaryColor ?? ColorPalette.Cyan;
        DrawButton(batch, p, enemyDisplay, enemy?.DisplayName.ToUpperInvariant() ?? "NO TARGETS", enemy is not null,
            enemyFill, enemy is null ? ColorPalette.Muted : SandboxEnemyButtonTextColor(enemy));
        DrawSandboxButton(batch, p, _sandboxEnemyNextButton, ">", enemies.Count > 1, ColorPalette.Cyan, "]");

        _sandboxGroupButton = new Rectangle(972, 132, 94, 28);
        _sandboxRankButton = new Rectangle(1070, 132, 94, 28);
        _sandboxHealthButton = new Rectangle(1168, 132, 100, 28);
        var groupLabel = SandboxGroupSizes[_sandboxGroupIndex] switch
        {
            1 => "1 TARGET",
            5 => "5 PACK",
            _ => "12 SWARM"
        };
        var rankLabel = SandboxRanks[_sandboxRankIndex].ToString().ToUpperInvariant();
        var healthLabel = _sandboxHealthIndex switch
        {
            1 => $"W10 {session.SandboxHealthMultiplierForWave(Math.Min(10, session.TotalWaves)):0.##}x",
            2 => $"W20 {session.SandboxHealthMultiplierForWave(session.TotalWaves):0.##}x",
            3 => "IMMORTAL",
            _ => "BASE HP"
        };
        DrawSandboxButton(batch, p, _sandboxGroupButton, groupLabel, true, ColorPalette.Cobalt, "G");
        DrawSandboxButton(batch, p, _sandboxRankButton, rankLabel, true,
            SandboxRanks[_sandboxRankIndex] == EnemyRank.Boss ? ColorPalette.Coral : ColorPalette.Violet, "K");
        DrawSandboxButton(batch, p, _sandboxHealthButton, healthLabel, true,
            _sandboxHealthIndex == 3 ? ColorPalette.Gold : ColorPalette.Cyan, "H");

        _sandboxSpawnButton = new Rectangle(972, 166, 48, 28);
        _sandboxResetButton = new Rectangle(1024, 166, 68, 28);
        _sandboxClearTowersButton = new Rectangle(1096, 166, 76, 28);
        _sandboxProtocolButton = new Rectangle(1176, 166, 92, 28);
        var protocolNeedsReset = HasSandboxProtocolTestState(session);
        var protocolCanStart = session.SelectedTower is { IsSandboxDisabled: false };
        DrawSandboxButton(batch, p, _sandboxSpawnButton, "SPAWN", enemy is not null, ColorPalette.Green, "F");
        DrawSandboxButton(batch, p, _sandboxResetButton, "RESET TEST", true, ColorPalette.Orange, "R");
        DrawSandboxButton(batch, p, _sandboxClearTowersButton, "CLEAR TOWERS", session.Towers.Count > 0, ColorPalette.Coral, "C");
        DrawSandboxButton(batch, p, _sandboxProtocolButton,
            protocolNeedsReset ? "RESET PROTOCOL" : protocolCanStart ? "TEST PROTOCOL" : "SELECT TOWER",
            protocolNeedsReset || protocolCanStart, ColorPalette.Violet, "E");
    }

    private void DrawTowerIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _targetModeButtons.Clear();
        _targetPickerBounds = Rectangle.Empty;
        var tacticalPreview = _hoveredTacticalPlacement != TacticalPlacementKind.None
            ? _hoveredTacticalPlacement
            : session.TacticalPlacement;
        if (tacticalPreview == TacticalPlacementKind.PulsePlate)
        {
            DrawEmergencyIntel(batch, p, session);
            return;
        }
        if (tacticalPreview == TacticalPlacementKind.ChargeForge)
        {
            DrawGeneratorIntel(batch, p, session, session.Generator);
            return;
        }

        var previewId = _hoveredTowerCardId ?? session.PlacementTowerId;
        TowerDefinition? preview = null;
        if (previewId is not null) session.Content.Towers.TryGetValue(previewId, out preview);
        if (preview is not null)
        {
            DrawDefinitionIntel(batch, p, session, preview, preview.Id == session.PlacementTowerId);
            return;
        }

        var tower = session.SelectedTower ?? session.HoveredTower;
        if (tower is null)
        {
            if (session.SelectedGenerator is not null || session.HoveredGenerator is not null)
            {
                DrawGeneratorIntel(batch, p, session, session.SelectedGenerator ?? session.HoveredGenerator);
                return;
            }
            if (_hoveredPowerNode is not null)
            {
                DrawSurgeZoneIntel(batch, p, _hoveredPowerNode);
                return;
            }
            if (session.IsSandbox)
            {
                DrawText(batch, "SANDBOX QUICK GUIDE", new Vector2(980, 482), ColorPalette.Navy, 0.64f);
                DrawFittedText(batch, "U / I UPGRADES  |  X APEX  |  D TOGGLES TOWER", new Vector2(980, 507), ColorPalette.Violet, 0.46f, 280);
                DrawFittedText(batch, "T TARGET  |  DELETE REMOVES  |  E TESTS PROTOCOL", new Vector2(980, 530), ColorPalette.Cobalt, 0.44f, 280);
                DrawFittedText(batch, "F SPAWN  |  R RESET TEST  |  C CLEAR TOWERS", new Vector2(980, 553), ColorPalette.Coral, 0.44f, 280);
                DrawFittedText(batch, "- / + SELECT WAVE  |  SPACE SENDS WAVE", new Vector2(980, 576), ColorPalette.Muted, 0.46f, 280);
                DrawFittedText(batch, "[ / ] ENEMY  |  G GROUP  |  K RANK  |  H HEALTH", new Vector2(980, 599), ColorPalette.Muted, 0.43f, 280);
                return;
            }
            DrawText(batch, "Hover a card to compare stats.", new Vector2(980, 482), ColorPalette.Muted, 0.72f);
            DrawText(batch, "Click a placed tower to manage it.", new Vector2(980, 505), ColorPalette.Muted, 0.72f);
            return;
        }

        var hasBranchChoice = tower.RequiresDoctrine || tower.RequiresSpecialization;
        var intelCard = new Rectangle(972, 474, 296, hasBranchChoice ? 172 : 196);
        p.FillRect(batch, intelCard, ColorPalette.PanelAlt);
        p.DrawRect(batch, intelCard, tower.Definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, TowerIntelIconCenter, IntelIconRadius(tower.Definition.Visual.Radius), tower.Definition.Visual.Shape,
            tower.Definition.Visual.PrimaryColor, tower.Definition.Visual.AccentColor, tower.LevelIndex + 1, true, levelMarks: true);
        var ownership = session.IsCoOp ? $"   PLACED P{tower.OwnerPlayerId}" : "";
        DrawFittedText(batch, tower.Definition.DisplayName, new Vector2(1036, 486), ColorPalette.Ink, 0.86f,
            tower.IsApex ? 164 : 228);
        if (tower.IsApex)
        {
            const string apexLabel = "APEX";
            const float apexScale = 0.43f;
            var apexWidth = _font.MeasureString(apexLabel).X * apexScale * GameConstants.FontDrawScale;
            DrawText(batch, apexLabel, new Vector2(intelCard.Right - 15 - apexWidth, 487.5f), ColorPalette.Violet, apexScale);
        }
        var levelTitle = TowerInfo.ProgressionLabel(tower);
        // Role belongs in the Workshop/library comparison surfaces. Live Intel
        // keeps only progression and co-op ownership, which also preserves room
        // for Sandbox's Disable control without a special final-tier layout.
        DrawFittedText(batch, $"{levelTitle}{ownership}", new Vector2(1036, 508), ColorPalette.Muted, 0.60f,
            session.IsSandbox ? 144 : 228);
        _sandboxToggleTowerButton = session.IsSandbox ? new Rectangle(1188, 502, 68, 24) : Rectangle.Empty;
        if (session.IsSandbox)
        {
            var toggleFill = tower.IsSandboxDisabled ? ColorPalette.Green : ColorPalette.Orange;
            DrawSandboxButton(batch, p, _sandboxToggleTowerButton, tower.IsSandboxDisabled ? "ENABLE" : "DISABLE",
                !_readOnlyInspection, toggleFill, "D");
        }
        var power = session.Map.GetPowerBuff(tower.Position);
        var powerNodes = session.Map.GetPowerNodes(tower.Position);
        var supportBuff = session.GetSupportBuff(tower);
        var statHeader = tower.IsSandboxDisabled
            ? "TOWER DISABLED"
            : tower.IsDisrupted
                ? $"DISRUPTED  {tower.DisruptionRemaining:0.0}s"
                : tower.IsSuppressed
                    ? $"SIGNAL WEAKENED  {tower.SuppressionRemaining:0.0}s"
                : _hoveredUpgradePreviewLabel ?? "CURRENT STATS";
        if (!tower.IsSandboxDisabled && !tower.IsDisrupted && !tower.IsSuppressed &&
            (supportBuff.IsActive || powerNodes.Count > 0))
        {
            var boostSources = TowerInfo.ActiveBoostSources(supportBuff, powerNodes,
                compact: _hoveredUpgradePreview is not null);
            statHeader += $"  |  {boostSources}";
        }
        DrawFittedText(batch, statHeader, new Vector2(980, 531),
            tower.IsSandboxDisabled ? ColorPalette.Coral : tower.IsDisrupted ? ColorPalette.Violet :
            tower.IsSuppressed ? ColorPalette.Orange : _hoveredUpgradePreview is not null ? ColorPalette.Violet : ColorPalette.Muted,
            0.45f, 280);
        var comparisonStats = TowerInfo.ComparisonStats(tower.Definition, tower.Level, _hoveredUpgradePreview,
            supportBuff, power, tower.IsOverdriven ? tower.Protocol : null,
            session.GetSignalDamageMultiplier(tower), session.GetSignalRateMultiplier(tower));
        DrawTowerStatGrid(batch, comparisonStats, 548);
        // The stat grid can use three complete label/value rows. Keep the
        // lifetime telemetry below that rhythm instead of visually attaching it
        // to the final stat cell.
        DrawFittedText(batch, TowerLifetimeSummary(tower), new Vector2(980, 628), ColorPalette.Cobalt, 0.43f, 280);

        _targetButton = new Rectangle(980, 678, 88, 30);
        _upgradeButton = new Rectangle(1074, 678, 92, 30);
        _sellButton = new Rectangle(1172, 678, 94, 30);
        _sandboxRemoveTowerButton = Rectangle.Empty;
        _specializationAButton = Rectangle.Empty;
        _specializationBButton = Rectangle.Empty;
        var canManage = !_readOnlyInspection;
        if (hasBranchChoice)
        {
            _upgradeButton = Rectangle.Empty;
            // Keep the first branch in the normal upgrade position and place the
            // alternate directly beneath it, with a clear gutter below intel.
            _targetButton = new Rectangle(980, 650, 88, 28);
            _sellButton = new Rectangle(980, 686, 88, 28);
            if (session.IsSandbox) _sandboxRemoveTowerButton = _sellButton;
            _specializationAButton = new Rectangle(1074, 650, 192, 28);
            _specializationBButton = new Rectangle(1074, 686, 192, 28);
            // The full authored names fit these wide controls and are much easier
            // to understand than terse internal labels such as CALIB or APERT.
            var firstLabel = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[0].DisplayName : tower.Definition.Specializations[0].DisplayName;
            var secondLabel = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[1].DisplayName : tower.Definition.Specializations[1].DisplayName;
            var firstCost = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[0].UpgradeCost : tower.Definition.Specializations[0].UpgradeCost;
            var secondCost = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[1].UpgradeCost : tower.Definition.Specializations[1].UpgradeCost;
            var firstFill = tower.Definition.Visual.PrimaryColor;
            var firstText = TowerIntelPrimaryUpgradeTextColor(tower.Definition);
            DrawButton(batch, p, _targetButton, TargetButtonLabel(tower), canManage, ColorPalette.Cyan,
                session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Cyan) : null, "T");
            DrawButton(batch, p, _specializationAButton, $"{firstLabel.ToUpperInvariant()} {firstCost}", canManage && session.Economy.CanAfford(firstCost),
                firstFill, firstText, "U");
            DrawButton(batch, p, _specializationBButton, $"{secondLabel.ToUpperInvariant()} {secondCost}", canManage && session.Economy.CanAfford(secondCost),
                ColorPalette.Violet, session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Violet) : null, "I");
            if (session.IsSandbox)
                DrawSandboxButton(batch, p, _sandboxRemoveTowerButton, "REMOVE", canManage, ColorPalette.Coral, "DEL");
            else
                DrawButton(batch, p, _sellButton, session.SellingEnabled ? $"SELL {tower.SellValue}" : "FIXED",
                    canManage && session.SellingEnabled, ColorPalette.Orange, hotkey: session.SellingEnabled ? "DEL" : null);
            DrawTargetPicker(batch, p, tower, canManage);
            return;
        }
        if (!tower.IsSupport)
            DrawButton(batch, p, _targetButton, TargetButtonLabel(tower), canManage, ColorPalette.Cyan,
                session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Cyan) : null, "T");
        var apexAvailable = session.CanApexUpgrade(tower);
        var upgradeCost = apexAvailable ? tower.ApexUpgradeCost : tower.UpgradeCost;
        var upgradeLabel = tower.CanUpgrade ? $"UP {tower.UpgradeCost}"
            : apexAvailable ? $"APEX {tower.ApexUpgradeCost}"
            : tower.IsApex ? "APEX"
            : session.IsEndlessMode && tower.Definition.Apex is not null ? $"APEX W{GameConstants.ApexUnlockWave}"
            : "MAX";
        var upgradeHotkey = tower.CanUpgrade ? "U" : apexAvailable ? "X" : null;
        DrawButton(batch, p, _upgradeButton, upgradeLabel,
            canManage && (tower.CanUpgrade || apexAvailable) && session.Economy.CanAfford(upgradeCost), ColorPalette.Violet,
            session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Violet) : null, upgradeHotkey);
        if (session.IsSandbox)
        {
            _sandboxRemoveTowerButton = _sellButton;
            DrawSandboxButton(batch, p, _sandboxRemoveTowerButton, "REMOVE", canManage, ColorPalette.Coral, "DEL");
        }
        else
            DrawButton(batch, p, _sellButton, session.SellingEnabled ? $"SELL {tower.SellValue}" : "FIXED",
                canManage && session.SellingEnabled, ColorPalette.Orange, hotkey: session.SellingEnabled ? "DEL" : null);
        if (!tower.IsSupport) DrawTargetPicker(batch, p, tower, canManage);
    }

    private string TargetButtonLabel(TowerInstance tower) =>
        tower.TargetMode.ToString().ToUpperInvariant();

    private void DrawTargetPicker(SpriteBatch batch, PrimitiveRenderer p, TowerInstance tower, bool enabled)
    {
        if (!_targetPickerOpen || _targetPickerTowerId != tower.Id || tower.IsSupport || _targetButton.IsEmpty) return;

        var modes = Enum.GetValues<TargetMode>();
        const int columns = 4;
        const int buttonWidth = 68;
        const int buttonHeight = 25;
        const int gap = 3;
        var rows = (modes.Length + columns - 1) / columns;
        var contentWidth = columns * buttonWidth + (columns - 1) * gap;
        var contentHeight = rows * buttonHeight + (rows - 1) * gap;
        var contentX = 980;
        var contentY = _targetButton.Top - 4 - contentHeight;
        _targetPickerBounds = new Rectangle(contentX - 4, contentY - 4, contentWidth + 8, contentHeight + 8);
        p.FillRect(batch, _targetPickerBounds, ColorPalette.Panel);
        p.DrawRect(batch, _targetPickerBounds, ColorPalette.Cyan, 2);

        for (var index = 0; index < modes.Length; index++)
        {
            var mode = modes[index];
            var row = index / columns;
            var column = index % columns;
            var itemsInRow = Math.Min(columns, modes.Length - row * columns);
            var rowWidth = itemsInRow * buttonWidth + (itemsInRow - 1) * gap;
            var rowOffset = (contentWidth - rowWidth) / 2;
            var bounds = new Rectangle(contentX + rowOffset + column * (buttonWidth + gap),
                contentY + row * (buttonHeight + gap), buttonWidth, buttonHeight);
            _targetModeButtons[mode] = bounds;
            var fill = mode == tower.TargetMode ? ColorPalette.Gold : ColorPalette.Cyan;
            DrawButton(batch, p, bounds, mode.ToString().ToUpperInvariant(), enabled, fill,
                ContrastAwareButtonTextColor(fill));
        }
    }

    private void DrawTowerStatGrid(SpriteBatch batch, IReadOnlyList<TowerStatDisplay> stats, int top)
    {
        // Current and preview stats deliberately share one label/value layout.
        // Keeping every value in the same cell prevents the panel from jumping
        // between two reading patterns while the pointer enters/leaves an
        // upgrade button. Three columns remain comfortably legible through nine
        // authored stats; only the densest tower sheets use four columns.
        var columns = TowerStatGridColumns(stats.Count);
        var columnWidth = 282 / columns;
        var rowHeight = TowerStatGridRowHeight(stats.Count);
        for (var index = 0; index < stats.Count && index < 12; index++)
        {
            var stat = stats[index];
            var color = stat.Direction switch
            {
                TowerStatDirection.Increase => ColorPalette.GreenText,
                TowerStatDirection.Decrease => ColorPalette.Coral,
                _ => ColorPalette.Ink
            };
            var column = index % columns;
            var row = index / columns;
            var position = new Vector2(980 + column * columnWidth, top + row * rowHeight);
            // Labels and values always receive separate lines. The larger base
            // scales matter when the 1280x720 canvas is displayed in a smaller
            // window; DrawFittedText only reduces individual long entries.
            DrawFittedText(batch, stat.Label, position, ColorPalette.Muted,
                TowerStatGridLabelScale(stats.Count), columnWidth - 6);
            DrawFittedText(batch, TowerInfo.ComparisonStatValueText(stat),
                position + new Vector2(0, stats.Count > 6 ? 10 : 12), color,
                TowerStatGridValueScale(stats.Count), columnWidth - 6);
        }
    }

    internal const float TowerStatGridMinimumScale = 0.36f;
    public const float ColoredButtonWhiteContrastThreshold = 3f;
    public static Color ContrastAwareButtonTextColor(Color fillColor) =>
        ColorPalette.ContrastRatio(ColorPalette.Paper, fillColor) >= ColoredButtonWhiteContrastThreshold
            ? ColorPalette.Paper
            : ColorPalette.Ink;
    public static Color TowerIntelPrimaryUpgradeTextColor(TowerDefinition definition) =>
        ContrastAwareButtonTextColor(definition.Visual.PrimaryColor);
    public static Color SandboxEnemyButtonTextColor(EnemyDefinition definition) =>
        definition.Id.Equals("t1_crawler", StringComparison.OrdinalIgnoreCase)
            ? ColorPalette.Paper
            : ContrastAwareButtonTextColor(definition.Visual.PrimaryColor);
    internal static int TowerStatGridColumns(int statCount) => statCount > 9 ? 4 : 3;
    internal static int TowerStatGridRowHeight(int statCount) => statCount > 6 ? 24 : 32;
    internal static float TowerStatGridLabelScale(int statCount) => TowerStatGridColumns(statCount) == 4 ? 0.40f : 0.48f;
    internal static float TowerStatGridValueScale(int statCount) => TowerStatGridColumns(statCount) == 4 ? 0.48f : 0.57f;

    private static string TowerLifetimeSummary(TowerInstance tower)
    {
        var utility = tower.LifetimeSupportDamageEquivalent > 0
            ? $"{tower.LifetimeSupportDamageEquivalent:0} SUPPORT"
            : tower.LifetimeExposeDamageEquivalent > 0
                ? $"+{tower.LifetimeExposeDamageEquivalent:0} EXPOSE"
                : tower.LifetimeArmorBreakDamageEquivalent > 0
                    ? $"+{tower.LifetimeArmorBreakDamageEquivalent:0} BREAK"
            : tower.LifetimeControlSeconds > 0
                ? $"{tower.LifetimeControlSeconds:0}s CONTROL"
                : tower.LifetimeExposeSeconds > 0
                    ? $"{tower.LifetimeExposeSeconds:0}s EXPOSE"
                    : tower.LifetimeArmorBreakSeconds > 0
                        ? $"{tower.LifetimeArmorBreakSeconds:0}s BREAK"
                        : null;
        var summary = $"LIFETIME  {tower.LifetimeDamage:0} DAMAGE  {tower.LifetimeKills} KILLS";
        return utility is null ? summary : $"{summary}  {utility}";
    }

    private void DrawDefinitionIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, TowerDefinition definition, bool placing)
    {
        var level = definition.Levels[0];
        var placementIntelPosition = placing && session.HasPlacementPreview
            ? session.PlacementPreviewPosition
            : session.PlacementPosition;
        var powerNodes = placing && session.HasPlacementPreview
            ? session.Map.GetPowerNodes(placementIntelPosition)
            : Array.Empty<PowerNodeData>();
        var hasPlacementModifier = powerNodes.Count > 0;
        var nodeTextColor = hasPlacementModifier
            ? PowerNodeIntelTextColor(powerNodes[0].NodeColor)
            : ColorPalette.Muted;
        var power = hasPlacementModifier ? session.Map.GetPowerBuff(placementIntelPosition) : default;
        var comparisonStats = TowerInfo.ComparisonStats(definition, level, null, default, power);
        var statColumns = TowerStatGridColumns(comparisonStats.Count);
        var statRows = (comparisonStats.Count + statColumns - 1) / statColumns;
        var lastStatValueTop = 548 + Math.Max(0, statRows - 1) * TowerStatGridRowHeight(comparisonStats.Count) +
                               (comparisonStats.Count > 6 ? 10 : 12);
        var protocolTop = lastStatValueTop + 20;
        const int detailStep = 15;
        var protocolBonusRows = session.ProtocolsEnabled
            ? TowerInfo.ProtocolBonusRows(definition.Protocol)
            : Array.Empty<string>();
        var detailCursor = protocolTop + (session.ProtocolsEnabled ? 2 + protocolBonusRows.Count : 2) * detailStep;
        var nodeTop = detailCursor;
        if (hasPlacementModifier) detailCursor += detailStep;
        var cardBottom = Math.Min(719, detailCursor + 8);
        var intelCard = new Rectangle(972, 474, 296, cardBottom - 474);
        p.FillRect(batch, intelCard, ColorPalette.PanelAlt);
        p.DrawRect(batch, intelCard, definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, TowerIntelIconCenter, IntelIconRadius(definition.Visual.Radius), definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
        DrawFittedText(batch, definition.DisplayName, new Vector2(1036, 486), ColorPalette.Ink, 0.86f, 228);
        DrawFittedText(batch, $"{definition.PurchaseCost} CREDITS   LEVEL 1   {TowerInfo.ShortRole(definition)}",
            new Vector2(1036, 508), ColorPalette.Muted, 0.60f, 228);
        DrawFittedText(batch, hasPlacementModifier ? "BASE STATS  |  NODE BOOST INCLUDED" : "BASE STATS",
            new Vector2(980, 531), nodeTextColor, 0.45f, 280);
        DrawTowerStatGrid(batch, comparisonStats, 548);
        if (session.ProtocolsEnabled)
        {
            DrawFittedText(batch,
                TowerInfo.ProtocolTimingCompact(definition.Protocol),
                new Vector2(980, protocolTop), ColorPalette.Coral, 0.40f, 280);
            DrawFittedText(batch, $"AUTO  {TowerInfo.ProtocolAutoTriggerCompact(definition.Protocol)}",
                new Vector2(992, protocolTop + detailStep), ColorPalette.Auto, 0.39f, 268);
            for (var index = 0; index < protocolBonusRows.Count; index++)
                DrawFittedText(batch, protocolBonusRows[index], new Vector2(992, protocolTop + (2 + index) * detailStep),
                    ColorPalette.Coral, 0.39f, 268);
        }
        else
        {
            DrawFittedText(batch, "PROTOCOLS OFFLINE  |  ENTRENCHED", new Vector2(980, protocolTop), ColorPalette.Muted, 0.40f, 280);
            DrawFittedText(batch, "TOWERS + UPGRADES ONLY  |  NO SALES", new Vector2(992, protocolTop + detailStep), ColorPalette.Muted, 0.39f, 268);
        }
        if (hasPlacementModifier)
        {
            DrawFittedText(batch, $"ON {PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}",
                new Vector2(980, nodeTop), nodeTextColor, 0.42f, 280);
        }
    }

    private static Color PowerNodeIntelTextColor(Color nodeColor) =>
        ColorPalette.BalancedAccentText(nodeColor, ColorPalette.PanelAlt);

    private static string PowerNodeNames(IReadOnlyList<PowerNodeData> nodes) => nodes.Count == 1
        ? nodes[0].DisplayName.ToUpperInvariant()
        : $"{string.Join(" + ", nodes.Select(node => node.DisplayName.Replace(" Node", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant()))} NODES";

    private static int IntelIconRadius(int authoredRadius) => Math.Min(authoredRadius, TowerIntelIconRadiusCap);

    private void DrawEmergencyIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _targetButton = Rectangle.Empty;
        _upgradeButton = Rectangle.Empty;
        _sellButton = Rectangle.Empty;
        var definition = session.Content.Tactics.EmergencyDefense;
        p.FillRect(batch, new Rectangle(972, 474, 296, 202), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 202), definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, TowerIntelIconCenter, definition.Visual.Radius + 2, definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, definition.Charges, true);
        DrawText(batch, definition.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.86f);
        DrawText(batch, $"STORED {session.EmergencyInventory}   FIELD {session.EmergencyDefenses.Count}/{definition.MaximumActive}", new Vector2(1028, 508), ColorPalette.Muted, 0.60f);
        var bonus = session.Generator?.Level.DefenseDamageBonus ?? 0;
        DrawFittedText(batch, $"{definition.Charges} PULSES   DAMAGE {definition.Damage * (1 + bonus):0.#}   BLAST {definition.BlastRadius:0}",
            new Vector2(980, 542), ColorPalette.Ink, 0.59f, 280);
        DrawText(batch, $"PUSH {definition.KnockbackDistance:0}   SLOW {definition.SlowPercent:P0} / {definition.SlowDuration:0.#}s", new Vector2(980, 565), ColorPalette.Ink, 0.55f);
        DrawText(batch, $"Stun {definition.StunDuration:0.##}s   Armor pierce {definition.ArmorPierce:0}", new Vector2(980, 590), ColorPalette.Muted, 0.54f);
        DrawFittedText(batch, $"Push: elite {definition.EliteKnockbackMultiplier:P0}   boss {definition.BossKnockbackMultiplier:P0}   grace {definition.KnockbackGraceSeconds:0.##}s",
            new Vector2(980, 612), ColorPalette.Muted, 0.47f, 280);
        var directIntel = session.Waves.IsActive
            ? $"Direct {session.CurrentEmergencyDirectPurchaseCost}   +{definition.DirectPurchaseCostIncrease} extra   resets next wave"
            : $"Direct buying activates in waves   Base {definition.PurchaseCost}";
        DrawFittedText(batch, directIntel, new Vector2(980, 638), session.Waves.IsActive ? ColorPalette.Gold : ColorPalette.Green, 0.49f, 280);
        DrawFittedText(batch, session.TacticalPlacement == TacticalPlacementKind.PulsePlate ? "CLICK THE ROAD TO DEPLOY   |   ESC TO CANCEL" : "Q OR CLICK ABOVE TO PREPARE",
            new Vector2(980, 658), ColorPalette.Cobalt, 0.49f, 280);
    }

    private void DrawSurgeZoneIntel(SpriteBatch batch, PrimitiveRenderer p, PowerNodeData zone)
    {
        _targetButton = Rectangle.Empty;
        _upgradeButton = Rectangle.Empty;
        _sellButton = Rectangle.Empty;
        _specializationAButton = Rectangle.Empty;
        _specializationBButton = Rectangle.Empty;
        p.FillRect(batch, new Rectangle(972, 474, 296, 202), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 202), zone.NodeColor, 1);
        p.DrawPolygon(batch, TowerIntelIconCenter, 17, 4, false, zone.NodeColor, MathHelper.PiOver4);
        p.DrawPolygon(batch, TowerIntelIconCenter, 8, 4, false, ColorPalette.Paper, MathHelper.PiOver4);
        DrawFittedText(batch, zone.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.82f, 236);
        DrawText(batch, "SURGE NODE", new Vector2(1028, 508), PowerNodeIntelTextColor(zone.NodeColor), 0.60f);
        var bonus = zone.AttackSpeedBonus > 0 ? $"ATTACK RATE +{zone.AttackSpeedBonus:P0}" :
            zone.RangeBonus > 0 ? $"TOWER RANGE +{zone.RangeBonus:P0}" :
            zone.DamageBonus > 0 ? $"DIRECT DAMAGE +{zone.DamageBonus:P0}" :
            $"ARMOR PIERCE +{zone.ArmorPierceBonus:0}";
        DrawText(batch, bonus, new Vector2(980, 546), ColorPalette.Ink, 0.68f);
        DrawText(batch, $"FIELD RADIUS {zone.Radius:0}", new Vector2(980, 570), ColorPalette.Muted, 0.56f);
        DrawFittedText(batch, "Center one or two towers in this compact field", new Vector2(980, 602), ColorPalette.Ink, 0.51f, 280);
        DrawFittedText(batch, "to apply its focused bonus for the entire match.", new Vector2(980, 623), ColorPalette.Ink, 0.51f, 280);
        DrawFittedText(batch, "Node bonuses do not stack with other nodes.", new Vector2(980, 652),
            PowerNodeIntelTextColor(ColorPalette.Gold), 0.49f, 280);
    }

    private void DrawGeneratorIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, ChargeForgeInstance? active)
    {
        _targetButton = Rectangle.Empty;
        var definition = session.Content.Tactics.Generator;
        var level = active?.Level ?? definition.Levels[0];
        p.FillRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, TowerIntelIconCenter, IntelIconRadius(definition.Visual.Radius), definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, (active?.LevelIndex ?? 0) + 1, true, levelMarks: true);
        DrawFittedText(batch, definition.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.86f, 236);
        var generatorOwner = active is not null && session.IsCoOp ? $"   PLACED P{active.OwnerPlayerId}" : "";
        DrawFittedText(batch, active is null ? $"{definition.PurchaseCost} CREDITS   GENERATOR" : $"LEVEL {active.LevelIndex + 1}   GENERATOR{generatorOwner}",
            new Vector2(1028, 508), ColorPalette.Muted, 0.60f, 236);
        var productionState = active is null
            ? $"PRODUCTION  1 PLATE / {level.ProductionSeconds:0}s OF ACTIVE WAVES"
            : session.Waves.IsActive
                ? $"PRODUCTION  +1 IN {active.ProductionRemaining:0}s"
                : $"PRODUCTION PAUSED  |  {active.ProductionRemaining:0}s REMAINING";
        DrawFittedText(batch, productionState, new Vector2(980, 548), ColorPalette.Ink, 0.56f, 280);
        DrawFittedText(batch, $"Storage {session.EmergencyInventory}/{level.Capacity}   Plate DAMAGE +{level.DefenseDamageBonus:P0}",
            new Vector2(980, 571), ColorPalette.Ink, 0.57f, 280);
        DrawFittedText(batch, "WAVE-POWERED  |  NO PROGRESS DURING INTERMISSIONS",
            new Vector2(980, 594), ColorPalette.Muted, 0.48f, 280);

        if (active is null)
        {
            _upgradeButton = Rectangle.Empty;
            _sellButton = Rectangle.Empty;
            var next = definition.Levels[1];
            DrawFittedText(batch, $"L2 {level.UpgradeCost}: {next.ProductionSeconds:0}s   CAP {next.Capacity}   DAMAGE +{next.DefenseDamageBonus:P0}",
                new Vector2(980, 624), ColorPalette.Violet, 0.52f, 280);
            DrawFittedText(batch, session.TacticalPlacement == TacticalPlacementKind.ChargeForge ? "CLICK A BUILD ZONE   |   ESC TO CANCEL" : "G OR CLICK ABOVE TO PREPARE",
                new Vector2(980, 650), ColorPalette.Cobalt, 0.49f, 280);
            return;
        }

        DrawFittedText(batch, active.CanUpgrade
            ? $"NEXT {active.UpgradeCost}: {definition.Levels[active.LevelIndex + 1].ProductionSeconds:0}s   CAP {definition.Levels[active.LevelIndex + 1].Capacity}   DAMAGE +{definition.Levels[active.LevelIndex + 1].DefenseDamageBonus:P0}"
            : "MAXIMUM LEVEL", new Vector2(980, 616), active.CanUpgrade ? ColorPalette.Violet : ColorPalette.Muted, 0.49f, 280);
        _upgradeButton = new Rectangle(1074, 646, 92, 30);
        _sellButton = new Rectangle(1172, 646, 94, 30);
        var canManage = !_readOnlyInspection;
        DrawButton(batch, p, _upgradeButton, active.CanUpgrade ? $"UP {active.UpgradeCost}" : "MAX",
            canManage && active.CanUpgrade && session.Economy.CanAfford(active.UpgradeCost), ColorPalette.Violet,
            hotkey: active.CanUpgrade ? "U" : null);
        DrawButton(batch, p, _sellButton, session.SellingEnabled ? $"SELL {active.SellValue}" : "FIXED",
            canManage && session.SellingEnabled, ColorPalette.Orange, hotkey: session.SellingEnabled ? "DEL" : null);
    }

    private void DrawPlacementStatus(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var pointerOnMap = IsPointerOnMap(session.PlacementPosition);
        var valid = pointerOnMap && session.HasPlacementPreview && session.PlacementFailure == PlacementFailure.None;
        var color = valid ? ColorPalette.Green : ColorPalette.Coral;
        var rect = new Rectangle(292, 64, 376, 28);
        p.FillRect(batch, rect, ColorPalette.WithAlpha(ColorPalette.Navy, 232));
        p.DrawRect(batch, rect, color, 2);
        var validMessage = session.TacticalPlacement switch
        {
            TacticalPlacementKind.PulsePlate when session.EmergencyInventory > 0 => "VALID - DEPLOY STORED PLATE",
            TacticalPlacementKind.PulsePlate => $"VALID - BUY & DEPLOY {session.CurrentEmergencyDirectPurchaseCost}",
            TacticalPlacementKind.ChargeForge => "VALID - BUILD CHARGE FORGE",
            _ => "VALID - CLICK TO DEPLOY"
        };
        var message = !pointerOnMap ? "MOVE CURSOR ONTO MAP" : valid ? validMessage : PlacementMessage(session, session.PlacementFailure);
        DrawText(batch, message, new Vector2(rect.Center.X, rect.Center.Y), ColorPalette.Paper, 0.58f, true);
    }

    private static bool IsPointerOnMap(Vector2 position) =>
        position.X >= 0 && position.X < GameConstants.MapWidth &&
        position.Y >= GameConstants.TopBarHeight && position.Y < GameConstants.LogicalHeight;

    private static string PlacementMessage(MinimalBastion.GameSession session, PlacementFailure failure) => failure switch
    {
        PlacementFailure.OutsideBuildableRegion => "MOVE INTO A BUILD ZONE",
        PlacementFailure.BlocksPath => "TOO CLOSE TO THE ROAD",
        PlacementFailure.OverlapsTower => "TOO CLOSE TO ANOTHER TOWER",
        PlacementFailure.TooCloseToEdge => "TOO CLOSE TO THE MAP EDGE",
        PlacementFailure.InsufficientCredits => "INSUFFICIENT CREDITS",
        PlacementFailure.MustBeOnPath => "MOVE NEAR THE ROAD TO SNAP A PLATE",
        PlacementFailure.TooCloseToPathEndpoint => "MOVE AWAY FROM ENTRY OR EXIT",
        PlacementFailure.OverlapsDefense => "NO OPEN PLATE POSITION NEARBY",
        PlacementFailure.DefenseCapacityReached => $"PLATE FIELD FULL - {session.Content.Tactics.EmergencyDefense.MaximumActive} ACTIVE MAX",
        PlacementFailure.GeneratorAlreadyBuilt => "ONLY ONE CHARGE FORGE IS ALLOWED",
        PlacementFailure.TowerUnavailable => $"{session.Challenge.DisplayName.ToUpperInvariant()} - TOWER OFFLINE",
        PlacementFailure.TacticalSystemsDisabled => $"{session.Challenge.DisplayName.ToUpperInvariant()} - RESERVES OFFLINE",
        PlacementFailure.IdentityCapacityReached => "ENDLESS ENTITY CAPACITY REACHED",
        PlacementFailure.NoDefenseAvailable => !session.Waves.IsActive && session.EmergencyInventory <= 0
            ? "NO STORED PLATE - DIRECT BUYING ACTIVATES IN WAVES"
            : $"NO STORED PLATE - NEED {session.CurrentEmergencyDirectPurchaseCost} CREDITS",
        _ => "INVALID PLACEMENT"
    };

    private void DrawMainMenu(SpriteBatch batch, PrimitiveRenderer p)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, 10), ColorPalette.Coral);
        p.FillRect(batch, new Rectangle(0, 710, GameConstants.LogicalWidth, 10), ColorPalette.Cobalt);

        _mainMenuBattleScene?.Draw(batch, p);

        // Keep the logo fully above the title and preserve the menu's vertical rhythm.
        var logo = new Vector2(640, 150);
        p.Circle(batch, logo, 78, ColorPalette.Navy);
        p.DrawShape(batch, logo, 53, "diamond", ColorPalette.Gold, ColorPalette.Paper, 2, true);
        p.Ring(batch, logo, 80, ColorPalette.Cyan, 4);
        p.DashedRing(batch, logo, 96, ColorPalette.Coral, 32, 3);
        p.FillRect(batch, new Rectangle(442, 142, 72, 8), ColorPalette.Cyan);
        p.FillRect(batch, new Rectangle(766, 142, 72, 8), ColorPalette.Coral);

        DrawText(batch, "MINIMAL BASTION", new Vector2(640, 295), ColorPalette.Ink, 2.2f, true);
        DrawText(batch, "A colorful geometric tower-defense game", new Vector2(640, 345), ColorPalette.Muted, 0.9f, true);
        DrawButton(batch, p, _playButton, "NEW GAME", true, ColorPalette.Cobalt);
        if (PlatformCapabilities.OnlineCoOp)
            DrawButton(batch, p, _coOpButton, "ONLINE CO-OP", true, ColorPalette.Green);
        DrawButton(batch, p, _continueButton, "LOAD SAVES", _saveAvailable, ColorPalette.Violet);
        DrawButton(batch, p, _mainMenuLibraryButton, "TACTICAL LIBRARY", true, ColorPalette.Cyan);
        DrawButton(batch, p, _mainMenuSettingsButton, "SETTINGS", true, ColorPalette.Orange, ColorPalette.Paper);
        if (PlatformCapabilities.ExitCommand)
            DrawButton(batch, p, _quitButton, "QUIT", true, ColorPalette.Coral);
    }

    private void DrawGameSetup(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, _setupForCoOp ? "ONLINE DEFENSE SETUP" : "NEW DEFENSE SETUP",
            new Vector2(640, 45), ColorPalette.Ink, 1.48f, true);
        if (_setupForCoOp)
            DrawText(batch, "Host settings are shared with Player 2.",
                new Vector2(640, 78), ColorPalette.Muted, 0.54f, true);

        DrawSetupRowLabel(batch, p, "ARENA", 110, ColorPalette.Berry);
        for (var index = 0; index < _maps.Count; index++)
            DrawSetupChoice(batch, p, SetupCardRectangle(0, index, _maps.Count), _maps[index].Name,
                MapLibraryAccent(_maps[index].PathStyle), index == _selectedMapIndex);

        DrawSetupRowLabel(batch, p, "DIFFICULTY", 204, ColorPalette.Cobalt);
        for (var index = 0; index < _difficulties.Count; index++)
            DrawSetupChoice(batch, p, SetupCardRectangle(1, index, _difficulties.Count), _difficulties[index].DisplayName,
                _difficulties[index].AccentColor, index == _selectedDifficultyIndex);

        DrawSetupRowLabel(batch, p, "MODE", 298, ColorPalette.Cyan);
        var setupChallenges = SetupChallenges();
        for (var index = 0; index < setupChallenges.Count; index++)
        {
            var challengeIndex = _challenges.IndexOf(setupChallenges[index]);
            DrawSetupChoice(batch, p, SetupCardRectangle(2, index, setupChallenges.Count), setupChallenges[index].DisplayName,
                setupChallenges[index].AccentColor, challengeIndex == _selectedChallengeIndex);
        }

        var map = _maps.Count > 0 ? _maps[_selectedMapIndex] : default;
        var difficulty = _difficulties.Count > 0 ? _difficulties[_selectedDifficultyIndex] : null;
        var challenge = _challenges.Count > 0 ? _challenges[_selectedChallengeIndex] : null;
        var summary = new Rectangle(48, 390, 1184, 176);
        p.FillRect(batch, summary, ColorPalette.PanelAlt);
        p.DrawRect(batch, summary, ColorPalette.CardOutline, 1);
        DrawSetupSummaryLine(batch, p, summary, 406, "ARENA", map.Name ?? "Foundry Loop",
            map.Description ?? "Balanced tactical arena.", MapLibraryAccent(map.PathStyle ?? "road"));
        DrawSetupSummaryLine(batch, p, summary, 446, "DIFFICULTY", difficulty?.DisplayName ?? "Medium",
            difficulty?.Description ?? "Balanced enemy pressure and starting resources.", difficulty?.AccentColor ?? ColorPalette.Cobalt);
        DrawSetupSummaryLine(batch, p, summary, 486, "MODE", challenge?.DisplayName ?? "Standard",
            challenge?.Description ?? "All systems available.", challenge?.AccentColor ?? ColorPalette.Cyan);

        var credits = StartingCreditsForSetup(map.StartingCredits, difficulty, challenge);
        var best = BestRunLabel(_runHistory, map.Id ?? "foundry_loop", difficulty?.Id ?? DifficultyCatalog.DefaultId,
            challenge?.Id ?? ChallengeCatalog.DefaultId);
        var setupFooter = challenge?.IsSandbox == true
            ? "UNLIMITED CREDITS + LIVES  |  FIXED TARGETS  |  30 AUTHORED WAVES"
            : $"START {credits} CREDITS  |  {difficulty?.StartingLives ?? 24} LIVES  |  {(difficulty?.CampaignWaveCount > GameConstants.CampaignWaveCount ? map.MasteryCampaign : map.Campaign)?.CompactSummary ?? "20-WAVE CAMPAIGN"}{(string.IsNullOrEmpty(best) ? "" : $"  |  {best}")}";
        DrawFittedCenteredText(batch, setupFooter, new Vector2(640, 546), ColorPalette.Navy, 0.46f, 1100);

        DrawButton(batch, p, _setupConfirmButton,
            _setupForCoOp ? "START HOSTING" : challenge?.IsSandbox == true ? "ENTER SANDBOX" : "BEGIN DEFENSE", true,
            _setupForCoOp || challenge?.IsSandbox == true ? ColorPalette.Green : ColorPalette.Cobalt);
        DrawButton(batch, p, _setupBackButton, "BACK", true, ColorPalette.Violet);
    }

    private string _loadingTransitionTitle = "LOADING DEFENSE SYSTEMS";
    private string _loadingTransitionStatus = "PREPARING SELECTED ARENA";
    public string LoadingTransitionTitle => _loadingTransitionTitle;

    public void BeginLoadingTransition(string title, string status)
    {
        _loadingTransitionTitle = title;
        _loadingTransitionStatus = status;
    }

    public void SetLoadingTransitionStatus(string status) => _loadingTransitionStatus = status;

    private void DrawLoadingTransition(SpriteBatch batch, PrimitiveRenderer p)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight),
            ColorPalette.Navy);
        var panel = new Rectangle(300, 252, 680, 218);
        p.FillRect(batch, panel, ColorPalette.Navy);
        p.DrawRect(batch, panel, ColorPalette.Cyan, 3);
        DrawFittedCenteredText(batch, _loadingTransitionTitle, new Vector2(640, 304), ColorPalette.Paper, 1.35f, 620);
        DrawText(batch, _loadingTransitionStatus, new Vector2(640, 346), ColorPalette.Disabled, 0.58f, true);

        const int indicatorSize = 14;
        const int indicatorGap = 13;
        var indicatorStartX = 640 - (indicatorSize * 3 + indicatorGap * 2) / 2;
        for (var index = 0; index < 3; index++)
        {
            var wave = (MathF.Sin(_visualTimeSeconds * 5.4f - index * 1.35f) + 1f) * 0.5f;
            var alpha = (byte)MathHelper.Lerp(70, 255, wave);
            p.FillRect(batch,
                new Rectangle(indicatorStartX + index * (indicatorSize + indicatorGap), 390, indicatorSize, indicatorSize),
                ColorPalette.WithAlpha(ColorPalette.Green, alpha));
        }
        DrawText(batch, "LOADING", new Vector2(640, 433), ColorPalette.Gold, 0.68f, true);
    }

    private void DrawSetupRowLabel(SpriteBatch batch, PrimitiveRenderer p, string label, int y, Color color)
    {
        DrawText(batch, label, new Vector2(48, y), color, 0.46f);
        p.Line(batch, new Vector2(150, y + 8), new Vector2(1232, y + 8), ColorPalette.WithAlpha(color, 96), 1);
    }

    private void DrawSetupChoice(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string label, Color accent, bool selected)
    {
        DrawSetupCardFrame(batch, p, rect, accent, selected);
        DrawFittedCenteredText(batch, label.ToUpperInvariant(), new Vector2(rect.Center.X, rect.Center.Y),
            selected ? accent : ColorPalette.Navy, 0.60f, rect.Width - 30);
    }

    private void DrawSetupSummaryLine(SpriteBatch batch, PrimitiveRenderer p, Rectangle summary, int y,
        string category, string name, string description, Color accent)
    {
        p.FillRect(batch, new Rectangle(summary.X + 16, y, 5, 30), accent);
        DrawFittedText(batch, $"{category}  {name}".ToUpperInvariant(), new Vector2(summary.X + 34, y + 4),
            accent, 0.47f, 250);
        DrawFittedText(batch, description, new Vector2(summary.X + 310, y + 4), ColorPalette.Muted, 0.43f,
            summary.Width - 334);
    }

    private static int StartingCreditsForSetup(int baseCredits, DifficultyDefinition? difficulty, ChallengeDefinition? challenge)
    {
        var difficultyCredits = Math.Max(0, (int)MathF.Round(baseCredits * (difficulty?.StartingCreditsMultiplier ?? 1f) / 5f) * 5);
        return Math.Max(0, (int)MathF.Round(difficultyCredits * (challenge?.StartingCreditsMultiplier ?? 1f) / 5f) * 5);
    }

    private static void DrawSetupCardFrame(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, Color accent, bool selected)
    {
        p.FillRect(batch, rect, selected ? ColorPalette.Panel : ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, selected ? 7 : 4, rect.Height), accent);
        p.DrawRect(batch, rect, selected ? accent : ColorPalette.CardOutline, selected ? 4 : 1);
    }

    private void DrawSettings(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "SETTINGS", new Vector2(640, 112), ColorPalette.Ink, 1.9f, true);

        DrawButton(batch, p, _windowModeButton, _settings.Fullscreen ? "DISPLAY MODE  FULLSCREEN" : "DISPLAY MODE  WINDOWED",
            true, ColorPalette.Cobalt);
        DrawButton(batch, p, _vsyncButton,
            PlatformCapabilities.ConfigurableVSync
                ? _settings.VSync ? "VSYNC  ON" : "VSYNC  OFF"
                : "VSYNC  BROWSER CONTROLLED",
            PlatformCapabilities.ConfigurableVSync, ColorPalette.Green);
        DrawButton(batch, p, _effectsButton, _settings.ReducedEffects ? "EFFECTS  REDUCED" : "EFFECTS  FULL",
            true, ColorPalette.Cyan);
        DrawButton(batch, p, _autoStartButton,
            _settings.AutoStartWaves
                ? $"AUTO-START  ON  |  {_settings.AutoStartDelaySeconds}s"
                : "AUTO-START  OFF",
            true, ColorPalette.Auto);
        DrawButton(batch, p, _hotkeyBadgesButton,
            _settings.ShowHotkeyBadges ? "HOTKEY BADGES  ON" : "HOTKEY BADGES  OFF",
            true, ColorPalette.Cyan);
        DrawButton(batch, p, _volumeButton, $"SOUND EFFECTS  {MathF.Round(_settings.SfxVolume * 100):0}%  |  CLICK TO CHANGE",
            true, ColorPalette.Gold, ColorPalette.Ink);
        DrawButton(batch, p, _musicVolumeButton, $"BACKGROUND MUSIC  {MathF.Round(_settings.MusicVolume * 100):0}%  |  CLICK TO CHANGE",
            true, ColorPalette.Violet);
        DrawButton(batch, p, _settingsBackButton, "BACK", true, ColorPalette.Coral);

        DrawText(batch, "Configured auto-starts earn +20. Wave 1 starts manually.",
            new Vector2(640, 612), ColorPalette.Muted, 0.49f, true);
        if (!string.IsNullOrWhiteSpace(_settingsStatus))
            DrawFittedCenteredText(batch, _settingsStatus, new Vector2(640, 650), ColorPalette.Cobalt, 0.50f, 900);
    }

    private void DrawSaveSlots(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, _saveSlotWriteMode ? "SAVE GAME" : "LOAD SAVES", new Vector2(640, 62), ColorPalette.Ink, 1.75f, true);
        DrawText(batch,
            _saveSlotWriteMode
                ? "Choose a slot. Overwriting occurs only after pressing the confirmation button."
                : PlatformCapabilities.OnlineCoOp
                    ? "Every readable save can continue solo, host online, or be duplicated into a protected slot."
                    : "Every readable save can continue solo or be duplicated into a protected slot.",
            new Vector2(640, 102), ColorPalette.Muted, 0.58f, true);
        DrawButton(batch, p, _saveSlotHistoryButton, "RUN HISTORY", true, ColorPalette.Cyan);

        var pageCount = Math.Max(1, (_saveSlots.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var pageSlots = _saveSlots.Skip(_saveSlotPage * _saveSlotRows.Length).Take(_saveSlotRows.Length).ToArray();
        for (var index = 0; index < pageSlots.Length; index++)
        {
            var rect = _saveSlotRows[index];
            var slot = pageSlots[index];
            var selected = slot.Slot == _selectedSaveSlot;
            p.FillRect(batch, rect, selected ? ColorPalette.Panel : ColorPalette.PanelAlt);
            p.DrawRect(batch, rect, selected ? ColorPalette.Cobalt : ColorPalette.CardOutline, selected ? 3 : 1);
            p.FillRect(batch, new Rectangle(rect.X, rect.Y, 8, rect.Height),
                !slot.IsOccupied ? ColorPalette.Disabled : slot.IsCoOp ? ColorPalette.Violet : ColorPalette.Cyan);
            DrawText(batch, SaveSlotLabel(slot.Slot), new Vector2(rect.X + 22, rect.Y + 13), ColorPalette.Navy, 0.68f);

            if (!slot.IsOccupied)
            {
                DrawText(batch, "EMPTY", new Vector2(rect.X + 150, rect.Center.Y), ColorPalette.Muted, 0.62f, true);
                continue;
            }
            if (slot.Error is not null)
            {
                DrawText(batch, "UNREADABLE SAVE", new Vector2(rect.X + 150, rect.Y + 13), ColorPalette.Coral, 0.58f);
                DrawText(batch, slot.Error, new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.42f);
                continue;
            }

            var mapName = _maps.FirstOrDefault(map => map.Id.Equals(slot.MapId, StringComparison.OrdinalIgnoreCase)).Name;
            if (string.IsNullOrWhiteSpace(mapName)) mapName = slot.MapId.Replace('_', ' ');
            var progress = slot.IsEndless && slot.CurrentWave <= GameConstants.MasteryFinalWave
                ? $"MASTERY {slot.CurrentWave}/{GameConstants.MasteryFinalWave}"
                : slot.IsEndless ? $"ENDLESS {slot.CurrentWave}" : $"WAVE {slot.CurrentWave}/{GameConstants.CampaignWaveCount}";
            var difficultyName = _difficulties.FirstOrDefault(x => x.Id.Equals(slot.DifficultyId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.DifficultyId) ? "Hard" : slot.DifficultyId);
            var challengeName = _challenges.FirstOrDefault(x => x.Id.Equals(slot.ChallengeId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.ChallengeId) ? "Standard" : slot.ChallengeId.Replace('_', ' '));
            DrawFittedText(batch, $"{(slot.IsCoOp ? "CO-OP" : "SOLO")}  |  {mapName.ToUpperInvariant()}  |  {difficultyName.ToUpperInvariant()}  |  {challengeName.ToUpperInvariant()}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.58f, rect.Width - 164);
            var localTime = slot.SavedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(slot.SavedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : slot.SavedAtUtc.ToLocalTime();
            DrawFittedText(batch, $"{progress}  |  {localTime:g}  |  LIVES {slot.Lives}  |  CREDITS {slot.Credits}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.48f, rect.Width - 164);
        }

        var selectedSlot = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
        var canConfirm = _saveSlotWriteMode || selectedSlot is { IsOccupied: true, Error: null };
        var selectedLabel = SaveSlotLabel(_selectedSaveSlot);
        var confirmLabel = _saveSlotWriteMode
            ? selectedSlot is { IsOccupied: true } ? $"OVERWRITE SLOT {_selectedSaveSlot}" : $"SAVE TO SLOT {_selectedSaveSlot}"
            : "CONTINUE SOLO";
        var confirmButton = _saveSlotWriteMode ? _saveSlotWriteConfirmButton : _saveSlotConfirmButton;
        var deleteButton = _saveSlotWriteMode ? _saveSlotWriteDeleteButton : _saveSlotDeleteButton;
        DrawButton(batch, p, confirmButton, confirmLabel, canConfirm,
            _saveSlotWriteMode && selectedSlot is { IsOccupied: true } ? ColorPalette.Orange : ColorPalette.Green);
        var canDelete = selectedSlot is { IsOccupied: true };
        if (!_saveSlotWriteMode)
        {
            if (PlatformCapabilities.OnlineCoOp)
                DrawButton(batch, p, _saveSlotHostButton, "HOST CO-OP",
                    selectedSlot is { IsOccupied: true, Error: null }, ColorPalette.Violet);
            DrawButton(batch, p, _saveSlotDuplicateButton, "DUPLICATE",
                selectedSlot is { IsOccupied: true, Error: null }, ColorPalette.Cobalt);
        }
        DrawButton(batch, p, deleteButton,
            _saveSlotDeleteArmed ? $"CONFIRM DELETE {selectedLabel}" : $"DELETE {selectedLabel}",
            canDelete, _saveSlotDeleteArmed ? ColorPalette.Coral : ColorPalette.Orange);
        DrawText(batch, $"PAGE {_saveSlotPage + 1}/{pageCount}", new Vector2(640, 574), ColorPalette.Muted, 0.48f, true);
        DrawButton(batch, p, _saveSlotPreviousButton, "PREVIOUS", _saveSlotPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, "NEXT", _saveSlotPage + 1 < pageCount, ColorPalette.Cyan);
        DrawFittedCenteredText(batch, $"ARROWS SELECT  |  ENTER CONFIRMS  |  {_persistenceStatus}",
            new Vector2(640, 654), _saveSlotDeleteArmed ? ColorPalette.Coral : ColorPalette.Muted, 0.48f, 1080);
    }

    private static string SaveSlotLabel(int slot) =>
        slot == SaveSlotRepository.AutosaveSlot ? "AUTOSAVE" : $"SLOT {slot}";

    private void DrawRunHistory(SpriteBatch batch, PrimitiveRenderer p)
    {
        if (_runHistoryCareerOpen)
        {
            DrawCareerProgress(batch, p);
            return;
        }
        if (_runHistoryDetailOpen)
        {
            DrawRunHistoryDetail(batch, p);
            return;
        }
        DrawMenuFrame(batch, p);
        DrawText(batch, "RUN HISTORY", new Vector2(640, 62), ColorPalette.Ink, 1.75f, true);
        DrawText(batch, "Select a completed defense to inspect its full run statistics.",
            new Vector2(640, 102), ColorPalette.Muted, 0.58f, true);
        DrawButton(batch, p, _runHistoryCareerButton, "MEDALS & RECORDS", true, ColorPalette.Cyan);

        var pageCount = Math.Max(1, (_runHistory.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var pageEntries = _runHistory.Skip(_runHistoryPage * _saveSlotRows.Length).Take(_saveSlotRows.Length).ToArray();
        if (pageEntries.Length == 0)
        {
            p.FillRect(batch, new Rectangle(330, 206, 620, 142), ColorPalette.PanelAlt);
            p.DrawRect(batch, new Rectangle(330, 206, 620, 142), ColorPalette.CardOutline, 1);
            DrawText(batch, "NO COMPLETED RUNS YET", new Vector2(640, 260), ColorPalette.Navy, 0.82f, true);
            DrawText(batch, "Victory and defeat summaries will appear here.", new Vector2(640, 304), ColorPalette.Muted, 0.54f, true);
        }
        for (var index = 0; index < pageEntries.Length; index++)
        {
            var rect = _saveSlotRows[index];
            var entry = pageEntries[index];
            var selected = entry.RunId == _selectedRunHistoryId;
            var accent = entry.Victory ? ColorPalette.Green : ColorPalette.Coral;
            p.FillRect(batch, rect, selected ? ColorPalette.Panel : ColorPalette.PanelAlt);
            p.DrawRect(batch, rect, selected ? ColorPalette.Cobalt : ColorPalette.CardOutline, selected ? 3 : 1);
            p.FillRect(batch, new Rectangle(rect.X, rect.Y, 8, rect.Height), accent);
            DrawText(batch, entry.Victory ? "SECURED" : "BREACHED", new Vector2(rect.X + 22, rect.Y + 13), accent, 0.62f);

            var progress = entry.IsEndless && entry.CurrentWave <= GameConstants.MasteryFinalWave
                ? $"MASTERY {entry.CurrentWave}/{GameConstants.MasteryFinalWave}"
                : entry.IsEndless ? $"ENDLESS {entry.CurrentWave}" : $"WAVE {entry.CurrentWave}/{entry.TotalWaves}";
            DrawFittedText(batch, $"{entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}  |  {entry.ChallengeName.ToUpperInvariant()}  |  {progress}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.56f, rect.Width - 164);
            var localTime = entry.CompletedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : entry.CompletedAtUtc.ToLocalTime();
            DrawFittedText(batch,
                $"{localTime:g}  |  {(entry.IsCoOp ? "CO-OP" : "SOLO")}  |  LIVES {entry.Lives}/{entry.StartingLives}  |  KILLS {entry.Kills}  |  MEDALS {CareerProgression.MedalsFor(entry).Count}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.44f, rect.Width - 164);
        }

        DrawButton(batch, p, _runHistoryViewButton, "VIEW RUN",
            _selectedRunHistoryId is not null, ColorPalette.Cobalt);
        DrawButton(batch, p, _runHistoryDeleteButton,
            _runHistoryDeleteArmed ? "CONFIRM DELETE" : "DELETE RUN",
            _selectedRunHistoryId is not null, _runHistoryDeleteArmed ? ColorPalette.Coral : ColorPalette.Orange);
        DrawText(batch, $"PAGE {_runHistoryPage + 1}/{pageCount}", new Vector2(640, 574), ColorPalette.Muted, 0.48f, true);
        DrawButton(batch, p, _saveSlotPreviousButton, "PREVIOUS", _runHistoryPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK TO SAVES", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, "NEXT", _runHistoryPage + 1 < pageCount, ColorPalette.Cyan);
        var selectedEntry = _runHistory.FirstOrDefault(entry => entry.RunId == _selectedRunHistoryId);
        if (selectedEntry is not null)
        {
            var duration = TimeSpan.FromSeconds(selectedEntry.DefenseSeconds);
            var defenseTime = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
            DrawFittedCenteredText(batch,
                $"ECONOMY  {selectedEntry.CreditsEarned} EARNED  |  {selectedEntry.CreditsSpent} SPENT  |  +{selectedEntry.EarlyCallCredits} EARLY  |  LEAKS {selectedEntry.Leaks}  |  TIME {defenseTime}",
                new Vector2(640, 646), ColorPalette.Cyan, 0.42f, 1000);
            DrawFittedCenteredText(batch,
                $"TACTICAL  {selectedEntry.ProtocolActivations} PROTOCOLS  |  {selectedEntry.PlateDeployments} PLATES  |  {selectedEntry.PlateDamage:0} PLATE DAMAGE  |  {selectedEntry.ForgedCharges} FORGED  |  TOP {selectedEntry.TopTowerContribution:0} IMPACT",
                new Vector2(640, 667), ColorPalette.Violet, 0.42f, 1000);
        }
        DrawFittedCenteredText(batch, $"ARROWS SELECT  |  {_runHistoryStatus}", new Vector2(640, 690), ColorPalette.Muted, 0.40f, 1080);
    }

    private void DrawCareerProgress(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        var career = CareerProgression.Analyze(_runHistory);
        DrawText(batch, "MEDALS & RECORDS", new Vector2(640, 45), ColorPalette.Ink, 1.35f, true);
        var nextAchievement = career.Achievements.FirstOrDefault(achievement => !achievement.IsUnlocked);
        var careerSummary = nextAchievement is null
            ? $"ALL {career.Achievements.Count} ACHIEVEMENTS COMPLETE"
            : $"{career.AchievementsUnlocked}/{career.Achievements.Count} ACHIEVEMENTS  |  NEXT: {nextAchievement.DisplayName.ToUpperInvariant()}";
        DrawFittedCenteredText(batch, careerSummary, new Vector2(640, 76), ColorPalette.Muted, 0.50f, 1120);

        const int cardY = 96;
        DrawResultStatCard(batch, p, new Rectangle(50, cardY, 275, 58), "CAMPAIGNS SECURED", career.CampaignsSecured.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(352, cardY, 275, 58), "MEDALS EARNED", career.TotalMedals.ToString(), ColorPalette.AmberText);
        DrawResultStatCard(batch, p, new Rectangle(654, cardY, 275, 58), "DEEPEST WAVE", (career.DeepestRun?.CurrentWave ?? 0).ToString(), ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(956, cardY, 275, 58), "ARENAS SECURED", $"{career.MapsSecured}/4", ColorPalette.Violet);

        var recordPanel = new Rectangle(40, 174, 392, 456);
        p.FillRect(batch, recordPanel, ColorPalette.PanelAlt);
        p.DrawRect(batch, recordPanel, ColorPalette.CardOutline, 1);
        DrawText(batch, "PERSONAL RECORDS", new Vector2(56, 190), ColorPalette.Navy, 0.68f);
        p.FillRect(batch, new Rectangle(56, 216, 360, 2), ColorPalette.Cyan);
        DrawCareerRecord(batch, "DEEPEST DEFENSE", CareerRunLabel(career.DeepestRun, run => $"WAVE {run.CurrentWave}"), 56, 230, ColorPalette.Cyan);
        DrawCareerRecord(batch, "FASTEST CAMPAIGN", CareerRunLabel(career.FastestClear, run => FormatRunDuration(run.DefenseSeconds)), 56, 274, ColorPalette.Cobalt);
        DrawCareerRecord(batch, "LEANEST CLEAR", CareerRunLabel(career.LeanestClear, run => $"{run.FinalLayout!.Towers.Count} TOWERS"), 56, 318, ColorPalette.GreenText);
        DrawCareerRecord(batch, "HIGHEST RESERVE", CareerRunLabel(career.HighestReserveClear, run => $"{run.CreditsRemaining} CREDITS"), 56, 362, ColorPalette.AmberText);

        const int medalsPerPage = 7;
        var medalPageCount = Math.Max(1, (career.Medals.Count + medalsPerPage - 1) / medalsPerPage);
        _careerMedalPage = Math.Clamp(_careerMedalPage, 0, medalPageCount - 1);
        DrawText(batch, "RUN MEDALS", new Vector2(56, 410), ColorPalette.Navy, 0.54f);
        DrawFittedText(batch, $"{career.MedalTypesUnlocked}/{career.Medals.Count} DISCOVERED", new Vector2(174, 413),
            ColorPalette.Muted, 0.31f, 156);
        DrawButton(batch, p, _careerMedalPreviousButton, "<", _careerMedalPage > 0, ColorPalette.Cyan);
        DrawFittedCenteredText(batch, $"{_careerMedalPage + 1}/{medalPageCount}", new Vector2(378, 418),
            ColorPalette.Muted, 0.28f, 26);
        DrawButton(batch, p, _careerMedalNextButton, ">", _careerMedalPage + 1 < medalPageCount, ColorPalette.Cyan);
        p.FillRect(batch, new Rectangle(56, 439, 360, 2), ColorPalette.Gold);
        var medalY = 450;
        foreach (var medal in career.Medals.Skip(_careerMedalPage * medalsPerPage).Take(medalsPerPage))
        {
            var marker = new Rectangle(56, medalY + 2, 8, 8);
            if (medal.IsUnlocked) p.FillRect(batch, marker, ColorPalette.GreenText);
            else p.DrawRect(batch, marker, ColorPalette.CardOutline, 1);
            DrawStrictFittedText(batch, medal.Definition.DisplayName.ToUpperInvariant(), new Vector2(71, medalY),
                medal.IsUnlocked ? ColorPalette.AmberText : ColorPalette.Muted, 0.35f, 210, 0.27f);
            DrawTextRight(batch, medal.IsUnlocked ? $"EARNED x{medal.EarnedCount}" : "LOCKED",
                new Vector2(416, medalY + 1), medal.IsUnlocked ? ColorPalette.GreenText : ColorPalette.Muted, 0.28f);
            DrawStrictFittedText(batch, medal.Definition.Description, new Vector2(71, medalY + 12), ColorPalette.Muted,
                0.27f, 345, 0.25f);
            medalY += 25;
        }

        var achievementPanel = new Rectangle(450, 174, 790, 456);
        p.FillRect(batch, achievementPanel, ColorPalette.PanelAlt);
        p.DrawRect(batch, achievementPanel, ColorPalette.CardOutline, 1);
        const int achievementsPerPage = 8;
        var achievementPageCount = Math.Max(1, (career.Achievements.Count + achievementsPerPage - 1) / achievementsPerPage);
        _careerAchievementPage = Math.Clamp(_careerAchievementPage, 0, achievementPageCount - 1);
        var visibleAchievements = career.Achievements
            .Skip(_careerAchievementPage * achievementsPerPage)
            .Take(achievementsPerPage)
            .ToArray();
        DrawText(batch, $"ACHIEVEMENTS {career.AchievementsUnlocked}/{career.Achievements.Count}",
            new Vector2(466, 190), ColorPalette.Navy, 0.68f);
        DrawTextRight(batch, visibleAchievements.FirstOrDefault()?.Category.ToUpperInvariant() ?? "CAREER",
            new Vector2(1118, 194), ColorPalette.Violet, 0.38f);
        DrawButton(batch, p, _careerAchievementPreviousButton, "<", _careerAchievementPage > 0, ColorPalette.Violet);
        DrawFittedCenteredText(batch, $"{_careerAchievementPage + 1}/{achievementPageCount}", new Vector2(1180, 198),
            ColorPalette.Muted, 0.28f, 26);
        DrawButton(batch, p, _careerAchievementNextButton, ">", _careerAchievementPage + 1 < achievementPageCount, ColorPalette.Violet);
        p.FillRect(batch, new Rectangle(466, 216, 758, 2), ColorPalette.Violet);
        for (var index = 0; index < visibleAchievements.Length; index++)
        {
            var achievement = visibleAchievements[index];
            var column = index % 2;
            var row = index / 2;
            var rect = new Rectangle(466 + column * 379, 230 + row * 92, 363, 78);
            var accent = achievement.IsUnlocked ? ColorPalette.Green : ColorPalette.CardOutline;
            p.FillRect(batch, rect, achievement.IsUnlocked ? ColorPalette.Panel : ColorPalette.PanelAlt);
            p.DrawRect(batch, rect, accent, achievement.IsUnlocked ? 2 : 1);
            p.FillRect(batch, new Rectangle(rect.X, rect.Y, 5, rect.Height), accent);
            DrawFittedText(batch, achievement.DisplayName.ToUpperInvariant(), new Vector2(rect.X + 14, rect.Y + 10),
                achievement.IsUnlocked ? ColorPalette.Ink : ColorPalette.Muted, 0.50f, 205);
            DrawTextRight(batch, $"{(achievement.IsUnlocked ? "EARNED" : "LOCKED")} {achievement.Progress}",
                new Vector2(rect.Right - 12, rect.Y + 11),
                achievement.IsUnlocked ? ColorPalette.GreenText : ColorPalette.Muted, 0.40f);
            DrawFittedText(batch, achievement.Description, new Vector2(rect.X + 14, rect.Y + 42), ColorPalette.Muted, 0.36f, rect.Width - 28);
        }

        DrawButton(batch, p, _runHistoryCareerBackButton, "BACK TO HISTORY", true, ColorPalette.Violet);
    }

    private void DrawCareerRecord(SpriteBatch batch, string label, string value, int x, int y, Color accent)
    {
        DrawText(batch, label, new Vector2(x, y), ColorPalette.Muted, 0.36f);
        DrawFittedText(batch, value, new Vector2(x, y + 17), accent, 0.46f, 360);
    }

    private static string CareerRunLabel(RunHistoryEntry? entry, Func<RunHistoryEntry, string> value) => entry is null
        ? "NO QUALIFYING RUN"
        : $"{value(entry)}  |  {entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}";

    private void DrawRunHistoryDetail(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        var entry = _runHistory.FirstOrDefault(candidate => candidate.RunId == _selectedRunHistoryId);
        if (entry is null)
        {
            _runHistoryDetailOpen = false;
            return;
        }

        var localTime = entry.CompletedAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc).ToLocalTime()
            : entry.CompletedAtUtc.ToLocalTime();
        DrawText(batch, "RUN DETAILS", new Vector2(640, 42), ColorPalette.Ink, 1.34f, true);
        DrawFittedCenteredText(batch,
            $"{entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}  |  {entry.ChallengeName.ToUpperInvariant()}  |  {(entry.IsCoOp ? "CO-OP" : "SOLO")}  |  {localTime:g}",
            new Vector2(640, 76), ColorPalette.Muted, 0.46f, 1120);

        const int cardY = 94;
        DrawResultStatCard(batch, p, new Rectangle(50, cardY, 224, 58), "RESULT", entry.Victory ? "SECURED" : "BREACHED",
            entry.Victory ? ColorPalette.Green : ColorPalette.Coral);
        var entryInMastery = entry.IsEndless && entry.CurrentWave <= GameConstants.MasteryFinalWave;
        DrawResultStatCard(batch, p, new Rectangle(289, cardY, 224, 58), entryInMastery ? "MASTERY" : entry.IsEndless ? "ENDLESS" : "WAVE",
            entryInMastery ? $"{entry.CurrentWave}/{GameConstants.MasteryFinalWave}" : entry.IsEndless ? entry.CurrentWave.ToString() : $"{entry.CurrentWave}/{entry.TotalWaves}", ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(528, cardY, 224, 58), "LIVES", $"{entry.Lives}/{entry.StartingLives}", ColorPalette.Coral);
        DrawResultStatCard(batch, p, new Rectangle(767, cardY, 224, 58), "KILLS", entry.Kills.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(1006, cardY, 224, 58), "LEAKS", entry.Leaks.ToString(), ColorPalette.Orange);

        var earnedMedals = CareerProgression.MedalsFor(entry);
        DrawFittedCenteredText(batch,
            earnedMedals.Count == 0
                ? "NO RUN MEDALS EARNED"
                : $"MEDALS  {string.Join("  /  ", earnedMedals.Select(medal => medal.DisplayName.ToUpperInvariant()))}",
            new Vector2(640, 160), earnedMedals.Count == 0 ? ColorPalette.Muted : ColorPalette.AmberText, 0.34f, 1160);

        var towerPanel = new Rectangle(40, 170, 758, 460);
        p.FillRect(batch, towerPanel, ColorPalette.PanelAlt);
        p.DrawRect(batch, towerPanel, ColorPalette.CardOutline, 1);
        DrawText(batch, "TOWER CONTRIBUTION", new Vector2(towerPanel.X + 14, towerPanel.Y + 12), ColorPalette.Navy, 0.68f);
        p.FillRect(batch, new Rectangle(towerPanel.X + 14, towerPanel.Y + 35, towerPanel.Width - 28, 2), ColorPalette.Cyan);
        DrawText(batch, "UNIT", new Vector2(56, 213), ColorPalette.Muted, 0.34f);
        DrawFittedText(batch, "BUILT", new Vector2(176, 213), ColorPalette.Muted, 0.30f, 38);
        DrawFittedText(batch, "UPGRADES", new Vector2(216, 213), ColorPalette.Muted, 0.30f, 54);
        DrawFittedText(batch, "SOLD", new Vector2(272, 213), ColorPalette.Muted, 0.30f, 34);
        DrawFittedText(batch, "DAMAGE", new Vector2(310, 213), ColorPalette.Muted, 0.30f, 70);
        DrawFittedText(batch, "ASSIST", new Vector2(383, 213), ColorPalette.Muted, 0.30f, 70);
        DrawFittedText(batch, "KILLS", new Vector2(456, 213), ColorPalette.Muted, 0.30f, 44);
        DrawFittedText(batch, "PROTOCOLS", new Vector2(504, 213), ColorPalette.Muted, 0.30f, 64);
        DrawFittedText(batch, "CONTROL", new Vector2(572, 213), ColorPalette.Muted, 0.30f, 74);
        DrawTextRight(batch, "IMPACT / CREDIT", new Vector2(780, 213), ColorPalette.Muted, 0.30f);

        var towers = entry.Towers.OrderByDescending(tower => tower.ContributionDamage).ThenBy(tower => tower.DisplayName).Take(10).ToArray();
        if (towers.Length == 0)
        {
            DrawText(batch, entry.TopTowerName == "NONE" ? "NO TOWER CONTRIBUTION RECORDED" : $"SUMMARY ONLY  |  TOP {entry.TopTowerName.ToUpperInvariant()}  |  {entry.TopTowerContribution:0} IMPACT",
                new Vector2(56, 250), ColorPalette.Muted, 0.52f);
        }
        for (var index = 0; index < towers.Length; index++)
        {
            var tower = towers[index];
            var y = 239 + index * 36;
            if ((index & 1) == 1) p.FillRect(batch, new Rectangle(50, y - 5, 738, 32), ColorPalette.Panel);
            DrawFittedText(batch, tower.DisplayName.ToUpperInvariant(), new Vector2(56, y), ColorPalette.Ink, 0.43f, 116);
            DrawFittedText(batch, tower.Purchases.ToString(), new Vector2(176, y), ColorPalette.Ink, 0.40f, 38);
            DrawFittedText(batch, tower.Upgrades.ToString(), new Vector2(216, y), ColorPalette.Ink, 0.40f, 54);
            DrawFittedText(batch, tower.Sales.ToString(), new Vector2(272, y), ColorPalette.Ink, 0.40f, 34);
            DrawFittedText(batch, tower.Damage.ToString("0"), new Vector2(310, y), ColorPalette.Cobalt, 0.40f, 70);
            DrawFittedText(batch, tower.AssistDamageEquivalent.ToString("0"), new Vector2(383, y), ColorPalette.Violet, 0.40f, 70);
            DrawFittedText(batch, tower.Kills.ToString(), new Vector2(456, y), ColorPalette.Ink, 0.40f, 44);
            DrawFittedText(batch, tower.ProtocolActivations.ToString(), new Vector2(504, y), ColorPalette.GreenText, 0.40f, 64);
            DrawFittedText(batch, $"{tower.ControlSeconds:0.0}s", new Vector2(572, y), ColorPalette.Cyan, 0.40f, 74);
            DrawTextRight(batch, tower.ImpactPerCredit.ToString("0.0"), new Vector2(780, y),
                ColorPalette.BalancedAccentText(ColorPalette.Gold, ColorPalette.PanelAlt), 0.40f);
        }

        var analysisPanel = new Rectangle(818, 170, 422, 460);
        p.FillRect(batch, analysisPanel, ColorPalette.PanelAlt);
        p.DrawRect(batch, analysisPanel, ColorPalette.CardOutline, 1);
        DrawText(batch, "RUN ANALYSIS", new Vector2(analysisPanel.X + 14, analysisPanel.Y + 12), ColorPalette.Navy, 0.68f);
        p.FillRect(batch, new Rectangle(analysisPanel.X + 14, analysisPanel.Y + 35, analysisPanel.Width - 28, 2), ColorPalette.Gold);
        var elapsed = FormatRunDuration(entry.DefenseSeconds);
        DrawSummaryMetric(batch, "CREDITS LEFT", entry.CreditsRemaining.ToString(), 834, 213, ColorPalette.Cyan);
        DrawSummaryMetric(batch, "EARNED", entry.CreditsEarned.ToString(), 1038, 213, ColorPalette.Gold);
        DrawSummaryMetric(batch, "SPENT", entry.CreditsSpent.ToString(), 834, 253, ColorPalette.Ink);
        DrawSummaryMetric(batch, "SALE RETURN", entry.SaleCreditsRecovered.ToString(), 1038, 253, ColorPalette.Orange);
        DrawSummaryMetric(batch, "EARLY BONUS", entry.EarlyCallCredits.ToString(), 834, 293, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "DEFENSE TIME", elapsed, 1038, 293, ColorPalette.Cobalt);

        var finalTowerCount = entry.FinalLayout?.Towers.Count;
        var apexTowerCount = entry.FinalLayout?.Towers.Count(tower => tower.IsApex);
        DrawText(batch, "FINAL DEFENSE", new Vector2(834, 333), ColorPalette.Muted, 0.38f);
        DrawFittedText(batch,
            finalTowerCount is null
                ? "NOT ARCHIVED"
                : $"{finalTowerCount} {(finalTowerCount == 1 ? "TOWER" : "TOWERS")}  |  {apexTowerCount} APEX",
            new Vector2(940, 333), finalTowerCount is null ? ColorPalette.Muted : ColorPalette.Cobalt, 0.42f, 284);

        DrawText(batch, "TACTICAL SYSTEMS", new Vector2(834, 358), ColorPalette.Navy, 0.58f);
        p.FillRect(batch, new Rectangle(834, 380, 390, 2), ColorPalette.Violet);
        DrawSummaryMetric(batch, "PROTOCOLS", entry.ProtocolActivations.ToString(), 834, 393, ColorPalette.Violet);
        DrawSummaryMetric(batch, "DIRECT PLATES", entry.PlateDirectPurchases.ToString(), 1038, 393, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATES / TRIGGERS", $"{entry.PlateDeployments} / {entry.PlateTriggers}", 834, 433, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATE HITS / KILLS", $"{entry.PlateHits} / {entry.PlateKills}", 1038, 433, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATE DAMAGE", entry.PlateDamage.ToString("0"), 834, 473, ColorPalette.Coral);
        DrawSummaryMetric(batch, "FORGED CHARGES", entry.ForgedCharges.ToString(), 1038, 473, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "FORGES BUILT", entry.ForgePurchases.ToString(), 834, 513, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "FORGE UPGRADES", entry.ForgeUpgrades.ToString(), 1038, 513, ColorPalette.GreenText);

        DrawText(batch, "GREATEST LEAK THREAT", new Vector2(834, 568), ColorPalette.Muted, 0.40f);
        var leakThreat = entry.GreatestLeakThreatLivesLost <= 0
            ? "NONE"
            : $"{entry.GreatestLeakThreatName.ToUpperInvariant()}  -{entry.GreatestLeakThreatLivesLost} LIVES";
        DrawFittedText(batch, leakThreat, new Vector2(834, 588), entry.GreatestLeakThreatLivesLost <= 0 ? ColorPalette.GreenText : ColorPalette.Coral, 0.52f, 390);
        var enemySummary = entry.Enemies.Count == 0
            ? "NO DETAILED THREAT TELEMETRY"
            : string.Join("  |  ", entry.Enemies.Where(enemy => enemy.Escapes > 0).Take(3)
                .Select(enemy => $"{enemy.DisplayName.ToUpperInvariant()} {enemy.Escapes} ESC"));
        if (string.IsNullOrWhiteSpace(enemySummary)) enemySummary = "NO ENEMIES ESCAPED";
        DrawFittedText(batch, enemySummary, new Vector2(834, 612), ColorPalette.Muted, 0.36f, 390);

        DrawButton(batch, p, _runHistoryLayoutButton,
            entry.FinalLayout is null ? "LAYOUT NOT ARCHIVED" : "VIEW FINAL LAYOUT",
            entry.FinalLayout is not null, ColorPalette.Cyan);
        DrawButton(batch, p, _runHistoryDetailBackButton, "BACK TO HISTORY", true, ColorPalette.Violet);
    }

    private static string FormatRunDuration(float seconds)
    {
        var duration = TimeSpan.FromSeconds(MathF.Max(0, seconds));
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void DrawCoOpMenu(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "ONLINE CO-OP", new Vector2(640, 120), ColorPalette.Ink, 1.9f, true);
        DrawText(batch, "Host a private defense, or join your friend's existing setup.", new Vector2(640, 164), ColorPalette.Muted, 0.60f, true);

        DrawText(batch, "HOST A GAME", new Vector2(640, 198), ColorPalette.Cobalt, 0.52f, true);
        DrawButton(batch, p, _hostCoOpButton, "CONFIGURE HOST GAME", true, ColorPalette.Cobalt);

        DrawText(batch, "JOIN A FRIEND", new Vector2(640, 294), ColorPalette.GreenText, 0.52f, true);

        DrawText(batch, "HOST ADDRESS  (PUBLIC IP OR DNS)", new Vector2(500, 307), _editingJoinCode ? ColorPalette.Muted : ColorPalette.Cobalt, 0.48f);
        p.FillRect(batch, _joinHostField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinHostField, !_editingJoinCode ? ColorPalette.Cobalt : ColorPalette.CardOutline, 2);
        var hostText = string.IsNullOrWhiteSpace(_joinHostInput) ? "203.0.113.10  or  friend.example" : _joinHostInput;
        DrawText(batch, hostText, new Vector2(640, _joinHostField.Center.Y), string.IsNullOrWhiteSpace(_joinHostInput) ? ColorPalette.Muted : ColorPalette.Ink, 0.66f, true);
        DrawText(batch, "+ TCP 28741 WHEN OMITTED", new Vector2(798, _joinHostField.Y + 12), ColorPalette.Muted, 0.44f);

        DrawText(batch, "SIX-CHARACTER JOIN CODE", new Vector2(500, 375), _editingJoinCode ? ColorPalette.Cobalt : ColorPalette.Muted, 0.48f);
        p.FillRect(batch, _joinCodeField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinCodeField, _editingJoinCode ? ColorPalette.Cobalt : _joinCodeInput.Length == 6 ? ColorPalette.Green : ColorPalette.CardOutline, 2);
        DrawText(batch, _joinCodeInput.PadRight(6, '_'), new Vector2(640, _joinCodeField.Center.Y), ColorPalette.Ink, 0.86f, true);

        DrawButton(batch, p, _joinCoOpButton, "JOIN ONLINE GAME", CanJoinOnline, ColorPalette.Green);
        DrawButton(batch, p, _backButton, "BACK", true, ColorPalette.Violet);
        var focus = CoOpMenuActionRectangle(Math.Clamp(_coOpMenuSelection, 0, 2));
        focus.Inflate(3, 3);
        p.DrawRect(batch, focus, ColorPalette.Ink, 2);
        DrawText(batch, "Shared credits, lives, and tower control. Placement markers identify P1/P2.", new Vector2(640, 590), ColorPalette.Muted, 0.56f, true);
        DrawFittedCenteredText(batch, "UP/DOWN SELECTS ACTIONS; ENTER ACTIVATES. TAB SWITCHES FIELDS; CTRL+V PASTES; HOLD BACKSPACE ERASES.",
            new Vector2(640, 613), ColorPalette.Muted, 0.46f, 900);
        DrawFittedCenteredText(batch, "FIRST HOST: WINDOWS MAY REQUEST FIREWALL ACCESS  |  INTERNET HOSTS FORWARD TCP 28741",
            new Vector2(640, 640), ColorPalette.Gold, 0.46f, 900);
    }

    private void DrawCoOpLobby(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        p.FillRect(batch, new Rectangle(390, 150, 500, 330), ColorPalette.Panel);
        p.FillRect(batch, new Rectangle(390, 150, 500, 7), ColorPalette.Green);
        p.DrawRect(batch, new Rectangle(390, 150, 500, 330), ColorPalette.Ink, 2);
        DrawText(batch, CoOpLobbyTitle, new Vector2(640, 210), ColorPalette.Ink, 1.25f, true);
        if (!string.IsNullOrEmpty(CoOpLobbyCode))
        {
            DrawText(batch, "JOIN CODE", new Vector2(640, 260), ColorPalette.Muted, 0.62f, true);
            p.FillRect(batch, _coOpLobbyCodeButton, ColorPalette.PanelAlt);
            p.DrawRect(batch, _coOpLobbyCodeButton, ColorPalette.Cobalt, 2);
            DrawText(batch, CoOpLobbyCode, new Vector2(640, 302), ColorPalette.Cobalt, 1.55f, true);
            DrawText(batch, _coOpLobbyCopyStatus, new Vector2(640, 348),
                _coOpLobbyCopyStatus == "JOIN CODE COPIED" ? ColorPalette.Green : ColorPalette.Muted, 0.48f, true);
        }
        DrawFittedCenteredText(batch, CoOpLobbyDetail, new Vector2(640, 392), ColorPalette.Muted, 0.62f, 440);
        DrawButton(batch, p, _backButton, "CANCEL", true, ColorPalette.Coral);
    }

    private void DrawCoOpReconnectOverlay(SpriteBatch batch, PrimitiveRenderer p)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 205));
        var panel = new Rectangle(370, 204, 540, 292);
        p.FillRect(batch, panel, ColorPalette.Panel);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 7), _coOpPeerConnected ? ColorPalette.Cyan : ColorPalette.Coral);
        p.DrawRect(batch, panel, ColorPalette.Ink, 2);
        DrawText(batch, CoOpLobbyTitle, new Vector2(640, 270), ColorPalette.Ink, 1.15f, true);
        DrawText(batch, CoOpLobbyDetail, new Vector2(640, 320), ColorPalette.Muted, 0.60f, true);
        if (!string.IsNullOrEmpty(CoOpLobbyCode))
        {
            DrawText(batch, "REJOIN CODE", new Vector2(640, 360), ColorPalette.Muted, 0.50f, true);
            p.FillRect(batch, _coOpReconnectCodeButton, ColorPalette.PanelAlt);
            p.DrawRect(batch, _coOpReconnectCodeButton, ColorPalette.Cobalt, 2);
            DrawText(batch, CoOpLobbyCode, new Vector2(640, 393), ColorPalette.Cobalt, 1.2f, true);
            DrawText(batch, _coOpLobbyCopyStatus, new Vector2(640, 432),
                _coOpLobbyCopyStatus == "REJOIN CODE COPIED" ? ColorPalette.Green : ColorPalette.Muted, 0.44f, true);
        }
        DrawText(batch, "The match is paused and preserved.  ESC leaves the session.", new Vector2(640, 466), ColorPalette.Coral, 0.54f, true);
    }

    private static void DrawMenuFrame(SpriteBatch batch, PrimitiveRenderer p)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, 10), ColorPalette.Coral);
        p.FillRect(batch, new Rectangle(0, 710, GameConstants.LogicalWidth, 10), ColorPalette.Cobalt);
    }

    private void DrawResultOverlay(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, bool victory)
    {
        var accent = victory ? ColorPalette.Green : ColorPalette.Coral;
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 226));
        p.FillRect(batch, new Rectangle(260, 64, 760, 584), ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(260, 64, 760, 8), accent);
        p.DrawRect(batch, new Rectangle(260, 64, 760, 584), ColorPalette.Ink, 2);

        DrawText(batch, victory ? "BASTION SECURED" : "BASTION BREACHED", new Vector2(640, 105), ColorPalette.Ink, 1.55f, true);
        DrawText(batch, victory ? $"Campaign secured. Mastery waves {GameConstants.ApexUnlockWave}-{GameConstants.MasteryFinalWave} unlock Apex upgrades." : $"Defense collapsed during wave {session.CurrentWave}.", new Vector2(640, 142), ColorPalette.Muted, 0.72f, true);
        DrawFittedCenteredText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(640, 160), session.Challenge.AccentColor, 0.40f, 650);

        DrawResultStatCard(batch, p, new Rectangle(296, 172, 158, 58), session.IsMasteryMode ? "MASTERY" : session.IsEndlessMode ? "ENDLESS" : "WAVE",
            session.IsMasteryMode ? $"{session.CurrentWave}/{GameConstants.MasteryFinalWave}" : session.IsEndlessMode ? session.CurrentWave.ToString() : $"{session.CurrentWave}/{session.TotalWaves}", ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(472, 172, 158, 58), "LIVES", $"{session.Economy.Lives}/{session.Economy.StartingLives}", ColorPalette.Coral);
        DrawResultStatCard(batch, p, new Rectangle(648, 172, 158, 58), "KILLS", session.Economy.TotalKills.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(824, 172, 158, 58), "LEAKS", session.Economy.EscapedEnemies.ToString(), ColorPalette.Orange);

        DrawTowerContribution(batch, p, session.Statistics, new Rectangle(296, 250, 410, 298));
        DrawRunSummary(batch, p, session, new Rectangle(724, 250, 258, 298));

        if (victory)
        {
            DrawButton(batch, p, _resultContinueButton, "ENTER MASTERY", true, ColorPalette.Green);
            DrawButton(batch, p, _resultRestartButton,
                _restartArmed ? "CONFIRM RESTART" : session.IsCoOp ? "RESTART CO-OP" : "RESTART", true,
                _restartArmed ? ColorPalette.Coral : ColorPalette.Cobalt);
            DrawButton(batch, p, _resultMenuButton, "MAIN MENU", true, ColorPalette.Violet);
        }
        else
        {
            DrawButton(batch, p, _resultContinueButton, "VIEW FIELD", true, ColorPalette.Cyan);
            DrawButton(batch, p, _resultRestartButton,
                _restartArmed ? "CONFIRM RESTART" : session.IsCoOp ? "RESTART CO-OP" : "RESTART", true,
                _restartArmed ? ColorPalette.Coral : ColorPalette.Cobalt);
            DrawButton(batch, p, _resultMenuButton, "MAIN MENU", true, ColorPalette.Violet);
        }
        if (_restartArmed)
            DrawFittedCenteredText(batch, RestartPreservationLabel, new Vector2(640, 562), ColorPalette.Coral, 0.42f, 620);
        var focus = ResultOptionRectangle(Math.Clamp(_resultMenuSelection, 0, 2));
        focus.Inflate(3, 3);
        p.DrawRect(batch, focus, ColorPalette.Ink, 2);
    }

    private void DrawDefeatFieldControls(SpriteBatch batch, PrimitiveRenderer p)
    {
        var label = new Rectangle(450, 9, 170, 38);
        p.FillRect(batch, label, ColorPalette.Coral);
        p.DrawRect(batch, label, ColorPalette.Ink, 2);
        DrawText(batch, "DEFEATED FIELD", new Vector2(label.Center.X, label.Center.Y), ColorPalette.Paper, 0.58f, true);
        DrawButton(batch, p, _fieldResultsButton, "VIEW RESULTS", true, ColorPalette.Cobalt);
    }

    private void DrawRunHistoryFieldControls(SpriteBatch batch, PrimitiveRenderer p)
    {
        var label = new Rectangle(432, 9, 188, 38);
        p.FillRect(batch, label, ColorPalette.Cyan);
        p.DrawRect(batch, label, ColorPalette.Ink, 2);
        DrawText(batch, "ARCHIVED LAYOUT", new Vector2(label.Center.X, label.Center.Y), ColorPalette.Paper, 0.54f, true);
        DrawButton(batch, p, _fieldResultsButton, "BACK TO HISTORY", true, ColorPalette.Violet);
    }

    private void DrawResultStatCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string label, string value, Color accent)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 4, rect.Height), accent);
        DrawText(batch, label, new Vector2(rect.X + 16, rect.Y + 9), ColorPalette.Muted, 0.52f);
        DrawText(batch, value, new Vector2(rect.X + 16, rect.Y + 28), ColorPalette.Ink, 0.88f);
    }

    private void DrawTowerContribution(SpriteBatch batch, PrimitiveRenderer p, RunStatistics stats, Rectangle rect)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        DrawText(batch, "TOWER CONTRIBUTION", new Vector2(rect.X + 14, rect.Y + 12), ColorPalette.Navy, 0.72f);
        p.FillRect(batch, new Rectangle(rect.X + 14, rect.Y + 36, rect.Width - 28, 2), ColorPalette.Cyan);

        var leaders = stats.TowerLeaders.Take(4).ToArray();
        if (leaders.Length == 0)
        {
            DrawText(batch, "No tower damage recorded.", new Vector2(rect.X + 14, rect.Y + 62), ColorPalette.Muted, 0.65f);
            return;
        }

        var maximum = MathF.Max(1, leaders[0].ContributionDamage);
        for (var index = 0; index < leaders.Length; index++)
        {
            var tower = leaders[index];
            var y = rect.Y + 54 + index * 54;
            var color = index switch
            {
                0 => ColorPalette.Cobalt,
                1 => ColorPalette.Violet,
                2 => ColorPalette.Cyan,
                _ => ColorPalette.Orange
            };
            DrawText(batch, tower.DisplayName.ToUpperInvariant(), new Vector2(rect.X + 14, y), ColorPalette.Ink, 0.58f);
            var contribution = tower.AssistDamageEquivalent > 0
                ? $"{tower.Damage:0} DAMAGE +{tower.AssistDamageEquivalent:0} ASSIST"
                : $"{tower.Damage:0} DAMAGE   {tower.Kills} KILLS";
            DrawTextRight(batch, contribution, new Vector2(rect.Right - 14, y), ColorPalette.Muted, 0.46f);
            var bar = new Rectangle(rect.X + 14, y + 24, rect.Width - 28, 9);
            p.FillRect(batch, bar, ColorPalette.Disabled);
            p.FillRect(batch, new Rectangle(bar.X, bar.Y, Math.Max(2, (int)(bar.Width * tower.ContributionDamage / maximum)), bar.Height), color);
        }

        var strongest = leaders[0];
        DrawText(batch, $"TOP UNIT  {strongest.DisplayName}   |   {strongest.DamagePerCredit:0.0} IMPACT / CREDIT", new Vector2(rect.X + 14, rect.Bottom - 30), ColorPalette.Violet, 0.52f);
    }

    private void DrawRunSummary(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, Rectangle rect)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        DrawText(batch, "RUN ANALYSIS", new Vector2(rect.X + 14, rect.Y + 12), ColorPalette.Navy, 0.72f);
        p.FillRect(batch, new Rectangle(rect.X + 14, rect.Y + 36, rect.Width - 28, 2), ColorPalette.Gold);

        var economy = session.Economy;
        var stats = session.Statistics;
        var threat = stats.GreatestLeakThreat;
        var elapsed = TimeSpan.FromSeconds(stats.SimulatedSeconds);
        var left = rect.X + 14;
        var right = rect.X + 134;
        DrawSummaryMetric(batch, "CREDITS EARNED", economy.TotalCreditsEarned.ToString(), left, rect.Y + 53, ColorPalette.Gold);
        DrawSummaryMetric(batch, "CREDITS SPENT", economy.TotalCreditsSpent.ToString(), right, rect.Y + 53, ColorPalette.Ink);
        DrawSummaryMetric(batch, "EARLY BONUS", economy.EarlyStartCreditsEarned.ToString(), left, rect.Y + 91, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "SALE RETURN", economy.SaleCreditsRecovered.ToString(), right, rect.Y + 91, ColorPalette.Orange);
        DrawSummaryMetric(batch, "PROTOCOLS", stats.ProtocolActivations.ToString(), left, rect.Y + 129, ColorPalette.Violet);
        DrawSummaryMetric(batch, "PLATES", stats.EmergencyDeployments.ToString(), right, rect.Y + 129, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATE DAMAGE", stats.EmergencyDamage.ToString("0"), left, rect.Y + 167, ColorPalette.Coral);
        DrawSummaryMetric(batch, "FORGED", stats.GeneratedCharges.ToString(), right, rect.Y + 167, ColorPalette.GreenText);
        DrawText(batch, "GREATEST LEAK THREAT", new Vector2(rect.X + 14, rect.Y + 210), ColorPalette.Muted, 0.44f);
        DrawText(batch, threat is null ? "NONE" : $"{threat.DisplayName.ToUpperInvariant()}  -{threat.LivesLost} LIVES", new Vector2(rect.X + 14, rect.Y + 228), threat is null ? ColorPalette.GreenText : ColorPalette.Coral, 0.52f);
        DrawText(batch, $"DEFENSE TIME  {elapsed.Minutes:00}:{elapsed.Seconds:00}", new Vector2(rect.X + 14, rect.Bottom - 27), ColorPalette.Cobalt, 0.52f);
    }

    private void DrawSummaryMetric(SpriteBatch batch, string label, string value, int x, int y, Color valueColor)
    {
        DrawText(batch, label, new Vector2(x, y), ColorPalette.Muted, 0.38f);
        DrawText(batch, value, new Vector2(x, y + 15),
            ColorPalette.BalancedAccentText(valueColor, ColorPalette.PanelAlt), 0.58f);
    }

    private void DrawOverlay(SpriteBatch batch, PrimitiveRenderer p, string title, string subtitle)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 220));
        p.FillRect(batch, new Rectangle(360, 150, 560, 440), ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(360, 150, 560, 10), title == "VICTORY" ? ColorPalette.Green : title == "DEFEAT" ? ColorPalette.Coral : ColorPalette.Cobalt);
        p.DrawRect(batch, new Rectangle(360, 150, 560, 440), ColorPalette.Ink, 2);
        DrawText(batch, title, new Vector2(640, 210), ColorPalette.Ink, 2f, true);
        DrawText(batch, subtitle, new Vector2(640, 255), ColorPalette.Muted, 0.85f, true);
        DrawButton(batch, p, _resumeButton, title == "PAUSED" ? "RESUME" : "RESTART", true, title == "VICTORY" ? ColorPalette.Green : ColorPalette.Cobalt);
        DrawButton(batch, p, _restartButton, title == "PAUSED" ? "RESTART" : "MAIN MENU", true, ColorPalette.Violet);
        DrawButton(batch, p, _mainMenuButton, title == "PAUSED" ? "MAIN MENU" : "", title == "PAUSED", ColorPalette.Coral);
    }

    private void DrawPauseOverlay(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (_towerLibraryOpen)
        {
            DrawTowerLibrary(batch, p, "pause");
            return;
        }

        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 220));
        var panel = new Rectangle(360, 90, 560, 540);
        p.FillRect(batch, panel, ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 10), ColorPalette.Cobalt);
        p.DrawRect(batch, panel, ColorPalette.Ink, 2);
        DrawText(batch, "PAUSED", new Vector2(640, 145), ColorPalette.Ink, 1.8f, true);
        DrawText(batch, session.IsSandbox ? "Tower laboratory - experimentation is not saved." : PauseCheckpointStatus(session.CanSaveCheckpoint),
            new Vector2(640, 184), ColorPalette.Muted, 0.66f, true);
        DrawFittedCenteredText(batch, session.IsSandbox
                ? "RESET TEST preserves placed towers; RESTART creates a clean laboratory."
                : _persistenceStatus,
            new Vector2(640, 211), ColorPalette.Muted, 0.50f, 510);
        DrawButton(batch, p, _resumeButton, "RESUME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _towerLibraryButton, "TACTICAL LIBRARY", true, ColorPalette.Cyan);
        DrawButton(batch, p, _pauseSettingsButton, "SETTINGS", true, ColorPalette.Orange, ColorPalette.Paper);
        DrawButton(batch, p, _saveButton,
            session.IsSandbox ? "SANDBOX NOT SAVED" : session.CanSaveCheckpoint ? "SAVE TO SLOT" : "SAVE BETWEEN WAVES",
            session.CanSaveCheckpoint, ColorPalette.Green);
        DrawButton(batch, p, _loadButton, "LOAD SAVES", _saveAvailable, ColorPalette.Violet);
        DrawButton(batch, p, _restartButton, _restartArmed ? "CONFIRM RESTART" : "RESTART", true,
            _restartArmed ? ColorPalette.Coral : ColorPalette.Berry);
        DrawButton(batch, p, _mainMenuButton, "MAIN MENU", true, ColorPalette.Coral);
        DrawFittedCenteredText(batch,
            _restartArmed
                ? RestartPreservationLabel
                : $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(640, 580), _restartArmed ? ColorPalette.Coral : ColorPalette.Muted, 0.50f, 500);
        DrawText(batch, "LEFT CLICK A COMMAND  |  ESC RESUMES", new Vector2(640, 612), ColorPalette.Muted, 0.44f, true);
    }

    private void DrawTowerLibrary(SpriteBatch batch, PrimitiveRenderer p, string returnDestination)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 235));
        var panel = new Rectangle(36, 24, 1208, 672);
        p.FillRect(batch, panel, ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 7), ColorPalette.Cyan);
        p.DrawRect(batch, panel, ColorPalette.Ink, 2);

        DrawText(batch, "TACTICAL LIBRARY", new Vector2(62, 42), ColorPalette.Navy, 1.25f);
        // Title, compact navigation, and description each have their own visual
        // band. Succinct copy leaves clear air above the content panels.
        DrawFittedText(batch, _libraryShowsSystems
            ? "Core rules, targeting, reserves, support, and co-op."
            : _libraryShowsProfiles
            ? "Difficulty scaling and directive restrictions."
            : _libraryShowsCampaign
            ? "Authored waves, scaling, routes, and threat order."
            : _libraryShowsThreats
                ? "Enemy stats, ranks, counters, and status symbols."
                : "Exact stats, upgrades, roles, and Protocols.",
            new Vector2(62, 82), ColorPalette.Muted, 0.50f, 1120);
        DrawButton(batch, p, _towerLibraryTowerTabButton, "TOWERS", true,
            _libraryShowsThreats || _libraryShowsCampaign || _libraryShowsProfiles || _libraryShowsSystems ? ColorPalette.PanelAlt : ColorPalette.Cyan,
            _libraryShowsThreats || _libraryShowsCampaign || _libraryShowsProfiles || _libraryShowsSystems ? ColorPalette.Ink : ColorPalette.Navy);
        DrawButton(batch, p, _towerLibraryThreatTabButton, "THREATS", true,
            _libraryShowsThreats ? ColorPalette.Coral : ColorPalette.PanelAlt,
            _libraryShowsThreats ? ColorPalette.Paper : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibraryCampaignTabButton, "CAMPAIGNS", true,
            _libraryShowsCampaign ? ColorPalette.Violet : ColorPalette.PanelAlt,
            _libraryShowsCampaign ? ColorPalette.Paper : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibraryProfilesTabButton, "PROFILES", true,
            _libraryShowsProfiles ? ColorPalette.Gold : ColorPalette.PanelAlt,
            _libraryShowsProfiles ? ColorPalette.Navy : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibrarySystemsTabButton, "SYSTEMS", true,
            _libraryShowsSystems ? ColorPalette.Green : ColorPalette.PanelAlt,
            _libraryShowsSystems ? ColorPalette.Navy : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibraryCloseButton, "BACK", true, ColorPalette.Violet);

        if (_libraryShowsSystems)
        {
            var systemsPanel = new Rectangle(56, 112, 1168, 540);
            p.FillRect(batch, systemsPanel, ColorPalette.Panel);
            p.DrawRect(batch, systemsPanel, ColorPalette.CardOutline, 1);
            DrawSystemsLibrary(batch, p, systemsPanel, returnDestination);
            return;
        }

        if (_libraryShowsProfiles)
        {
            var profilesPanel = new Rectangle(56, 112, 1168, 540);
            p.FillRect(batch, profilesPanel, ColorPalette.Panel);
            p.DrawRect(batch, profilesPanel, ColorPalette.CardOutline, 1);
            DrawProfilesLibrary(batch, p, profilesPanel, returnDestination);
            return;
        }

        var listPanel = new Rectangle(56, 112, 264, 540);
        var detailPanel = new Rectangle(334, 112, 890, 540);
        p.FillRect(batch, listPanel, ColorPalette.Panel);
        p.DrawRect(batch, listPanel, ColorPalette.CardOutline, 1);
        p.FillRect(batch, detailPanel, ColorPalette.Panel);
        p.DrawRect(batch, detailPanel, ColorPalette.CardOutline, 1);
        DrawText(batch, _libraryShowsCampaign ? "SELECT ARENA" : _libraryShowsThreats ? "SELECT THREAT" : "SELECT TOWER",
            new Vector2(68, 122), ColorPalette.Navy, 0.63f);
        var listCount = _libraryShowsCampaign ? _libraryMaps.Count : _libraryShowsThreats ? _libraryThreats.Count : _libraryTowers.Count;
        DrawTextRight(batch, listCount > 0 ? $"1-{listCount}" : "0",
            new Vector2(listPanel.Right - 10, 122), ColorPalette.Muted, 0.48f);

        if (_libraryShowsCampaign)
        {
            DrawCampaignLibrary(batch, p, detailPanel, returnDestination);
            return;
        }

        if (_libraryShowsThreats)
        {
            DrawEnemyLibrary(batch, p, detailPanel, returnDestination);
            return;
        }

        var towers = _libraryTowers;
        if (towers.Count == 0)
        {
            DrawDiscoveryEmptyState(batch, "NO TOWERS CONFIGURED", "No tower definitions are available.", detailPanel);
            return;
        }

        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, towers.Count - 1);
        for (var index = 0; index < towers.Count; index++)
        {
            var definition = towers[index];
            var row = TowerLibraryRow(index);
            var selected = index == _towerLibraryIndex;
            var selectedFill = ColorPalette.Tint(definition.Visual.PrimaryColor, 0.78f);
            var towerAccent = TowerLibraryAccent(definition);
            p.FillRect(batch, row, selected ? selectedFill : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected
                ? towerAccent
                : ColorPalette.CardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(row.X + 22, row.Center.Y), 12, definition.Visual.Shape,
                definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
            DrawFittedText(batch, definition.DisplayName, new Vector2(row.X + 44, row.Y + 7), ColorPalette.Ink, 0.56f, 142);
            DrawText(batch, $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}", new Vector2(row.X + 44, row.Y + 24), ColorPalette.Muted, 0.43f);
            if (_settings.ShowHotkeyBadges)
            {
                var hotkeyColor = selected
                    ? definition.Visual.AccentColor
                    : ColorPalette.Muted;
                DrawTextRight(batch, index == 9 ? "0" : (index + 1).ToString(), new Vector2(row.Right - 9, row.Y + 8), hotkeyColor, 0.43f);
            }
        }

        DrawTowerLibraryDetails(batch, p, towers[_towerLibraryIndex], detailPanel);
        DrawText(batch, $"Click, press 1-0, or use UP/DOWN.  TAB changes page.  ESC, right-click, or BACK returns to {returnDestination}.", new Vector2(640, 674), ColorPalette.Muted, 0.45f, true);
    }

    private void DrawCampaignLibrary(SpriteBatch batch, PrimitiveRenderer p, Rectangle detailPanel, string returnDestination)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        if (_libraryMaps.Count == 0)
        {
            DrawDiscoveryEmptyState(batch, "NO CAMPAIGNS CONFIGURED", "No arena definitions are available.", detailPanel);
            return;
        }

        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, _libraryMaps.Count - 1);
        for (var index = 0; index < _libraryMaps.Count; index++)
        {
            var map = _libraryMaps[index];
            var row = CampaignLibraryMapRow(index);
            var selected = index == _campaignLibraryMapIndex;
            var accent = MapLibraryAccent(map.PathStyle);
            p.FillRect(batch, row, selected ? ColorPalette.Tint(accent, 0.80f) : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected ? accent : ColorPalette.CardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(row.X + 31, row.Y + 31), 15,
                MapLibraryShape(map.PathStyle),
                accent, ColorPalette.Ink, Math.Max(1, map.Challenge - 1), false);
            DrawFittedText(batch, map.Name, new Vector2(row.X + 58, row.Y + 14), ColorPalette.Ink, 0.62f, 160);
            DrawFittedText(batch, $"THREAT {map.Challenge}/5  |  BASE {map.StartingCredits}",
                new Vector2(row.X + 58, row.Y + 39), ColorPalette.Muted, 0.43f, row.Width - 72);
            DrawFittedText(batch, MapPathLabel(map.PathStyle),
                new Vector2(row.X + 58, row.Y + 57), LibraryAccentText(accent,
                    selected ? ColorPalette.Tint(accent, 0.80f) : ColorPalette.PanelAlt), 0.40f, row.Width - 72);
            var authoredWaveCount = _libraryCampaignWaves.TryGetValue(map.Id, out var mapWaves)
                ? Math.Min(GameConstants.MasteryFinalWave, mapWaves.Count)
                : 0;
            DrawFittedText(batch, authoredWaveCount > 0 ? $"AUTHORED WAVES 1-{authoredWaveCount}" : "NO AUTHORED WAVE DATA",
                new Vector2(row.X + 14, row.Y + 78), LibraryAccentText(accent, selected ? ColorPalette.Tint(accent, 0.80f) : ColorPalette.PanelAlt),
                0.40f, row.Width - 28);
            if (_settings.ShowHotkeyBadges)
            {
                DrawTextRight(batch, (index + 1).ToString(), new Vector2(row.Right - 10, row.Y + 9),
                    selected ? LibraryAccentText(accent, ColorPalette.Tint(accent, 0.80f)) : ColorPalette.Muted, 0.43f);
            }
        }

        var selectedMap = _libraryMaps[_campaignLibraryMapIndex];
        if (!_libraryCampaignWaves.TryGetValue(selectedMap.Id, out var waves) || waves.Count == 0)
        {
            DrawText(batch, "NO AUTHORED WAVES FOR THIS ARENA", new Vector2(detailPanel.Center.X, detailPanel.Center.Y), ColorPalette.Coral, 0.72f, true);
            return;
        }

        var mapAccent = MapLibraryAccent(selectedMap.PathStyle);
        DrawText(batch, selectedMap.Name.ToUpperInvariant(), new Vector2(detailPanel.X + 18, detailPanel.Y + 16), ColorPalette.Ink, 0.96f);
        DrawTextRight(batch, $"THREAT {selectedMap.Challenge}/5  |  {selectedMap.PowerNodes} SURGE NODES  |  BASE {selectedMap.StartingCredits}",
            new Vector2(detailPanel.Right - 18, detailPanel.Y + 21), LibraryAccentText(mapAccent, ColorPalette.Panel), 0.50f);
        DrawFittedText(batch, selectedMap.Description, new Vector2(detailPanel.X + 18, detailPanel.Y + 48), ColorPalette.Muted, 0.48f, detailPanel.Width - 238);
        var visibleWaveCount = Math.Min(GameConstants.MasteryFinalWave, waves.Count);
        DrawFittedText(batch, $"COMPLETE WAVE REFERENCE  W1-W{visibleWaveCount}",
            new Vector2(detailPanel.X + 18, detailPanel.Y + 72),
            LibraryAccentText(mapAccent, ColorPalette.Panel), 0.43f, detailPanel.Width - 238);
        DrawRoutePreview(batch, p, new Rectangle(detailPanel.Right - 202, detailPanel.Y + 39, 184, 54),
            selectedMap.Path, selectedMap.PathBase, selectedMap.PathAccent);
        p.FillRect(batch, new Rectangle(detailPanel.X + 18, detailPanel.Y + 98, detailPanel.Width - 36, 2), mapAccent);

        var waveColumns = visibleWaveCount > GameConstants.CampaignWaveCount ? 3 : 2;
        var waveGap = 10;
        var waveColumnWidth = (detailPanel.Width - 36 - waveGap * (waveColumns - 1)) / waveColumns;
        for (var index = 0; index < visibleWaveCount; index++)
        {
            var column = index / 10;
            var row = index % 10;
            var rect = new Rectangle(detailPanel.X + 18 + column * (waveColumnWidth + waveGap),
                detailPanel.Y + 112 + row * 41, waveColumnWidth, 37);
            DrawCampaignWaveRow(batch, p, rect, waves[index]);
        }

        DrawFittedCenteredText(batch,
            $"Complete authored rosters are available for planning.  TAB changes page; ESC or BACK returns to {returnDestination}.",
            new Vector2(640, 674), ColorPalette.Muted, 0.43f, 1160);
    }

    private static void DrawRoutePreview(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect,
        IReadOnlyList<Vector2> route, Color baseColor, Color accentColor)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        if (route.Count < 2) return;

        const float padding = 6;
        var left = rect.Left + padding;
        var top = rect.Top + padding;
        var width = rect.Width - padding * 2;
        var height = rect.Height - padding * 2;
        Vector2 Transform(Vector2 point) => new(
            left + MathHelper.Clamp(point.X / GameConstants.MapWidth, 0, 1) * width,
            top + MathHelper.Clamp((point.Y - GameConstants.TopBarHeight) /
                (GameConstants.LogicalHeight - GameConstants.TopBarHeight), 0, 1) * height);

        var points = route.Select(Transform).ToArray();
        for (var index = 0; index < points.Length - 1; index++)
            p.Line(batch, points[index], points[index + 1], baseColor, 5);
        foreach (var point in points)
            p.FillRect(batch, new Rectangle((int)point.X - 2, (int)point.Y - 2, 5, 5), baseColor);
        for (var index = 0; index < points.Length - 1; index++)
            p.Line(batch, points[index], points[index + 1], accentColor, 1);

        p.DrawPolygon(batch, points[0], 4, 4, true, ColorPalette.Cyan, MathHelper.PiOver4);
        p.DrawPolygon(batch, points[^1], 4, 3, true, ColorPalette.Coral, -MathHelper.PiOver2);
    }

    private void DrawProfilesLibrary(SpriteBatch batch, PrimitiveRenderer p, Rectangle panel, string returnDestination)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        if (_libraryDifficulties.Count == 0 && _libraryChallenges.Count == 0)
        {
            DrawDiscoveryEmptyState(batch, "NO PROFILES CONFIGURED", "No difficulty or directive definitions are available.", panel);
            return;
        }

        const int cardWidth = 272;
        const int cardHeight = 236;
        const int gap = 14;
        var firstX = panel.X + 18;
        var firstY = panel.Y + 18;
        var secondY = firstY + cardHeight + gap;

        for (var index = 0; index < Math.Min(4, _libraryDifficulties.Count); index++)
        {
            var difficulty = _libraryDifficulties[index];
            DrawSystemCard(batch, p, new Rectangle(firstX + index * (cardWidth + gap), firstY, cardWidth, cardHeight),
                $"{difficulty.DisplayName.ToUpperInvariant()} DIFFICULTY", difficulty.AccentColor, "diamond",
                DifficultyReferenceLines(difficulty));
        }

        var shownChallenges = Math.Min(5, _libraryChallenges.Count);
        var challengeCardWidth = shownChallenges == 0
            ? cardWidth
            : Math.Min(cardWidth, (panel.Width - 36 - gap * (shownChallenges - 1)) / shownChallenges);
        for (var index = 0; index < shownChallenges; index++)
        {
            var challenge = _libraryChallenges[index];
            DrawSystemCard(batch, p, new Rectangle(firstX + index * (challengeCardWidth + gap), secondY, challengeCardWidth, cardHeight),
                $"{challenge.DisplayName.ToUpperInvariant()} DIRECTIVE", challenge.AccentColor, "square",
                ChallengeReferenceLinesWithSignals(challenge, _allLibraryTowers.Count));
        }

        DrawFittedCenteredText(batch,
            $"Difficulty scales every arena and endless wave; directives change available systems and opening funds.  TAB changes page; ESC or BACK returns to {returnDestination}.",
            new Vector2(640, 674), ColorPalette.Muted, 0.43f, 1160);
    }

    public static IReadOnlyList<string> DifficultyReferenceLines(DifficultyDefinition difficulty) =>
    [
        $"ENEMY HEALTH x{difficulty.EnemyHealthMultiplier:0.00}",
        $"ENEMY SPEED x{difficulty.EnemySpeedMultiplier:0.00}",
        $"START CREDITS x{difficulty.StartingCreditsMultiplier:0.000}",
        $"STARTING LIVES {difficulty.StartingLives}",
        difficulty.Id.ToLowerInvariant() switch
        {
            "easy" => "INTENT: LEARNING MARGIN + RECOVERY",
            "normal" => "INTENT: AUTHORED COMBAT BASELINE",
            "hard" => "INTENT: EXPERT 20-WAVE PRESSURE",
            "bastion" => "INTENT: COMPLETE 30-WAVE EXPERT CAMPAIGN",
            _ => $"INTENT: {difficulty.Description.ToUpperInvariant()}"
        }
    ];

    public static IReadOnlyList<string> ChallengeReferenceLines(ChallengeDefinition challenge, int totalTowerCount)
    {
        if (challenge.IsSandbox)
        {
            return
            [
                "SOLO TEST ENVIRONMENT",
                "UNLIMITED CREDITS + LIVES",
                "FIXED OR IMMORTAL TARGETS",
                "STANDARD / ELITE / BOSS RANKS",
                "REPLAY ANY AUTHORED WAVE",
                "RESET TOWER DATA + PROTOCOLS"
            ];
        }
        if (challenge.CounterPressureEnabled)
        {
            return
            [
                $"START CREDITS x{challenge.StartingCreditsMultiplier:0.00}",
                "W2 ACCELERATE / W3 REPAIR / W4 SHIELD",
                "W5 JAMMER WEAKENS ONE TOWER",
                "ELITE + BOSS GROUP DISRUPTION",
                "FULL ROSTER + ALL SYSTEMS",
                "RULE: SIGNAL CARRIERS SUPPORT FORMATIONS"
            ];
        }
        var available = Math.Max(0, totalTowerCount - challenge.ExcludedTowerIds.Count);
        var lines = new List<string>
        {
            $"START CREDITS x{challenge.StartingCreditsMultiplier:0.00}",
            $"TACTICAL RESERVES {(challenge.TacticalSystemsEnabled ? "ON" : "OFF")}",
            $"TOWER PROTOCOLS {(challenge.ProtocolsEnabled ? "ON" : "OFF")}",
            $"TOWERS AVAILABLE {available}/{Math.Max(0, totalTowerCount)}"
        };
        if (!challenge.SellingEnabled) lines.Add("TOWER SALES OFF");
        var excluded = challenge.ExcludedTowerIds
            .Select(id => id.Replace('_', ' ').ToUpperInvariant())
            .ToArray();
        if (excluded.Length == 0)
        {
            lines.Add("FULL TOWER ROSTER");
        }
        else
        {
            var split = (excluded.Length + 1) / 2;
            lines.Add($"OFFLINE: {string.Join(" / ", excluded.Take(split))}");
            if (split < excluded.Length) lines.Add(string.Join(" / ", excluded.Skip(split)));
        }
        lines.Add(challenge.Id.ToLowerInvariant() switch
        {
            "standard" => "RULE: ALL SYSTEMS AVAILABLE",
            "close_quarters" => "RULE: SIGNAL CARRIERS SUPPORT FORMATIONS",
            "core_six" => "RULE: SIX-TOWER ROSTER LOCK",
            "no_reserves" => "RULE: TOWERS + UPGRADES ONLY; NO SALES",
            _ => $"RULE: {challenge.Description.ToUpperInvariant()}"
        });
        return lines;
    }

    private void DrawSystemsLibrary(SpriteBatch batch, PrimitiveRenderer p, Rectangle panel, string returnDestination)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        const int cardWidth = 368;
        const int cardHeight = 238;
        const int gap = 14;
        var firstX = panel.X + 18;
        var firstY = panel.Y + 18;
        var cards = new List<(string Title, Color Accent, string Shape, IReadOnlyList<string> Lines)>
        {
            ("CORE CONTROLS", ColorPalette.Cobalt, "triangle",
            [
                "SPACE: START / READY WAVE    S: 1x / 2x SPEED",
                "ESC/P: PAUSE    TAB: CO-OP LIBRARY",
                "LEFT/RIGHT: LIBRARY PAGE    MIDDLE: PING",
                "1-0: SELECT    U/I: UPGRADE CHOICES",
                "X: APEX    T: TARGET    DELETE: SELL",
                "Q: PLATE    G: FORGE    E: PROTOCOL    A: AUTO",
                "SANDBOX [ ]: ENEMY    G/K/H: GROUP/RANK/HP",
                "SANDBOX F/R/C/D: SPAWN/RESET/CLEAR/TOGGLE"
            ])
        };
        cards.Add(("TARGETING MODES", ColorPalette.Cyan, "diamond",
        [
            "FIRST: FARTHEST ALONG THE ROUTE",
            "LAST: LEAST ROUTE PROGRESS",
            "STRONGEST: MOST HEALTH + SHIELD",
            "WEAKEST: LOWEST HEALTH PERCENTAGE",
            "NEAREST: SHORTEST DISTANCE TO TOWER",
            "FASTEST: HIGHEST CURRENT MOVE SPEED",
            "ARMORED: HIGHEST CURRENT ARMOR",
            "SUPPORT: SIGNAL CARRIERS, THEN STRONGEST"
        ]));
        cards.Add(("STATUS RULES", ColorPalette.Coral, "hexagon", StatusReferenceLines()));
        cards.Add(("TOWER PROTOCOLS", ColorPalette.Auto, "square",
        [
            "E: ACTIVATE THE SELECTED TOWER MANUALLY",
            "A OR AUTO: ARM ONE TOWER FOR PRESSURE-AWARE USE",
            "AUTO RULES MATCH EACH ROLE: RANGE, AREA, GROUP, OR ALLIES",
            "ANY ENGAGED ELITE / BOSS TRIGGERS AUTO IMMEDIATELY",
            "ANY ACTIVATION STARTS THE ONE SHARED COOLDOWN",
            "EACH TOWER HAS UNIQUE STATS, BURST, OR STATUS",
            "THE SIDEBAR SHOWS ACTIVE TIME AND COOLDOWN",
            "TOWER PAGES SHOW EACH EXACT EFFECT"
        ]));
        cards.Add(("BEACONS + SURGE NODES", ColorPalette.Gold, "circle", SupportReferenceLines()));
        cards.Add(("PULSE PLATES + FORGE", ColorPalette.Green, "star", ReserveReferenceLines()));

        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var column = index % 3;
            var row = index / 3;
            DrawSystemCard(batch, p,
                new Rectangle(firstX + column * (cardWidth + gap), firstY + row * (cardHeight + gap), cardWidth, cardHeight),
                card.Title, card.Accent, card.Shape, card.Lines);
        }

        DrawFittedCenteredText(batch,
            $"TAB changes page.  ESC, right-click, or BACK returns to {returnDestination}.  Co-op shares credits, lives, defenses, targeting, upgrades, Protocols, and wave readiness.",
            new Vector2(640, 674), ColorPalette.Muted, 0.43f, 1160);
    }

    private static IReadOnlyList<string> StatusReferenceLines() =>
    [
        "SLOW: STRONGEST ONLY; MOVEMENT REDUCTION CAPS AT 60%",
        "STUN: STOPS MOVEMENT WHILE ACTIVE",
        "BURN: UP TO 2 SOURCES; ALSO REDUCES ARMOR BY 2",
        "EXPOSE: STRONGEST BONUS RAISES ALL INCOMING DAMAGE",
        "ARMOR BREAK: STRONGEST REDUCTION LOWERS ARMOR",
        "ELITE / BOSS CONTROL DURATION: -30% / -60%"
    ];

    private static IReadOnlyList<string> ChallengeReferenceLinesWithSignals(ChallengeDefinition challenge, int totalTowerCount)
    {
        if (!challenge.CounterPressureEnabled) return ChallengeReferenceLines(challenge, totalTowerCount);
        var roles = Enum.GetValues<EnemySignalRole>()
            .Where(role => role != EnemySignalRole.None)
            .Select(role => role.ToString().ToUpperInvariant())
            .ToArray();
        var lines = new List<string>
        {
            $"START CREDITS x{challenge.StartingCreditsMultiplier:0.00}",
            "FULL ROSTER + ALL SYSTEMS",
            "RULE: SIGNAL CARRIERS SUPPORT FORMATIONS"
        };
        lines.Add($"SIGNALS: {string.Join(" / ", roles.Take(3))}");
        if (roles.Length > 3) lines.Add(string.Join(" / ", roles.Skip(3)));
        lines.Add("SEE THREATS FOR EXACT SIGNAL RULES");
        return lines;
    }

    private static IReadOnlyList<string> SupportReferenceLines() =>
    [
        "SIGNAL BEACON BUFFS COMBAT TOWERS INSIDE ITS AURA",
        "RATE AND RANGE EACH USE THE STRONGEST BEACON",
        "MULTIPLE BEACONS NEVER ADD THE SAME STAT",
        "GOLD PIP IDENTIFIES A BEACON-AFFECTED TOWER",
        "SURGE NODES USE THE STRONGEST LOCAL STAT",
        "DASHED GOLD RING IDENTIFIES A NODE BOOST",
        "BEACON, NODE, AND ACTIVE PROTOCOL MAY COMBINE",
        "TOWER INTEL SHOWS EACH EXACT ACTIVE CHANGE"
    ];

    private IReadOnlyList<string> ReserveReferenceLines()
    {
        var lines = new List<string>();
        var plate = _libraryTactics.EmergencyDefense;
        lines.Add($"PLATE: {plate.Charges} PULSES | {plate.Damage:0.#} DAMAGE | AREA {plate.BlastRadius:0}");
        lines.Add($"START STORED {plate.StartingInventory} | FIELD CAP {plate.MaximumActive}");
        lines.Add($"DIRECT BUY {plate.PurchaseCost} + {plate.DirectPurchaseCostIncrease} PER PURCHASE");
        var forge = _libraryTactics.Generator;
        lines.Add($"FORGE BUILD {forge.PurchaseCost}; PRODUCTION IS WAVE-POWERED");
        lines.Add($"FORGE CADENCE: {string.Join(" / ", forge.Levels.Take(3).Select(level => $"{level.ProductionSeconds:0}s"))}");
        lines.Add($"STORED CAP: {string.Join(" / ", forge.Levels.Take(3).Select(level => level.Capacity))}");
        lines.Add($"PLATE DAMAGE: {string.Join(" / ", forge.Levels.Take(3).Select(level => $"+{level.DefenseDamageBonus:P0}"))}");
        return lines;
    }

    private void DrawSystemCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string title, Color accent,
        string shape, IReadOnlyList<string> lines)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 5), accent);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        p.DrawShape(batch, new Vector2(rect.X + 24, rect.Y + 27), 10, shape, accent, ColorPalette.Navy, 1, false);
        DrawFittedText(batch, title, new Vector2(rect.X + 45, rect.Y + 15),
            LibraryAccentText(accent, ColorPalette.PanelAlt), 0.62f, rect.Width - 58);
        p.FillRect(batch, new Rectangle(rect.X + 12, rect.Y + 47, rect.Width - 24, 1), ColorPalette.CardOutline);
        var y = rect.Y + 60;
        foreach (var line in lines)
        {
            DrawStrictFittedText(batch, line, new Vector2(rect.X + 12, y), ColorPalette.Ink, 0.43f,
                rect.Width - 24, 0.33f);
            y += 23;
        }
    }

    private void DrawCampaignWaveRow(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, CampaignWaveReference wave)
    {
        var accent = CampaignWaveAccent(wave);
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 4, rect.Height), accent);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        var compact = rect.Width < 340;
        DrawFittedText(batch, $"W{wave.Number:00}  {wave.Archetype.ToUpperInvariant()}", new Vector2(rect.X + 10, rect.Y + 4),
            ColorPalette.Navy, compact ? 0.40f : 0.46f, compact ? rect.Width - 126 : 190);
        DrawTextRight(batch, $"{wave.Contacts} | HP x{wave.HealthMultiplier:0.00} | SPD x{wave.SpeedMultiplier:0.00}",
            new Vector2(rect.Right - 8, rect.Y + 4), LibraryAccentText(accent, ColorPalette.PanelAlt), compact ? 0.31f : 0.38f);
        DrawStrictFittedText(batch, $"{wave.Threats}  |  {wave.Roster}", new Vector2(rect.X + 10, rect.Y + 20),
            ColorPalette.Muted, compact ? 0.31f : 0.35f, rect.Width - 20, 0.24f);
    }

    private static Color MapLibraryAccent(string pathStyle) => pathStyle.ToLowerInvariant() switch
    {
        "trail" or "channel" => ColorPalette.Green,
        "prism" or "conduit" => ColorPalette.Violet,
        "surge" => ColorPalette.Cyan,
        _ => ColorPalette.Orange
    };

    private static string MapLibraryShape(string pathStyle) => pathStyle.ToLowerInvariant() switch
    {
        "trail" or "channel" => "circle",
        "prism" or "conduit" => "diamond",
        "surge" => "hexagon",
        _ => "square"
    };

    private static string MapPathLabel(string pathStyle) => pathStyle.ToLowerInvariant() switch
    {
        "foundry" => "MOLTEN CHANNEL",
        "trail" => "EARTH TRAIL",
        "prism" => "LIGHT RIBBON",
        "surge" => "ENERGY TRENCH",
        "channel" => "CHANNEL",
        "conduit" => "CONDUIT",
        _ => "ROAD"
    };

    private static Color CampaignWaveAccent(CampaignWaveReference wave) => wave.Threats.Contains("BOSS", StringComparison.OrdinalIgnoreCase)
        ? ColorPalette.Coral
        : wave.Threats.Contains("ELITE", StringComparison.OrdinalIgnoreCase)
            ? ColorPalette.Gold
            : wave.Threats.Contains("REGEN", StringComparison.OrdinalIgnoreCase)
                ? ColorPalette.Green
                : wave.Threats.Contains("SHIELD", StringComparison.OrdinalIgnoreCase)
                    ? ColorPalette.Violet
                    : ColorPalette.Cyan;

    private void DrawEnemyLibrary(SpriteBatch batch, PrimitiveRenderer p, Rectangle detailPanel, string returnDestination)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        if (_libraryThreats.Count == 0)
        {
            DrawDiscoveryEmptyState(batch, "NO THREATS CONFIGURED", "No enemy definitions are available.", detailPanel);
            return;
        }

        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, _libraryThreats.Count - 1);
        for (var index = 0; index < _libraryThreats.Count; index++)
        {
            var threat = _libraryThreats[index];
            var row = EnemyLibraryRow(index);
            var selected = index == _enemyLibraryIndex;
            var selectedFill = ColorPalette.Tint(threat.PrimaryColor, 0.80f);
            p.FillRect(batch, row, selected ? selectedFill : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected ? threat.PrimaryColor : ColorPalette.CardOutline, selected ? 2 : 1);
            DrawThreatIcon(batch, p, threat, new Vector2(row.X + 22, row.Center.Y), 12);
            DrawFittedText(batch, threat.DisplayName, new Vector2(row.X + 43, row.Y + 7), ColorPalette.Ink, 0.54f, 155);
            DrawFittedText(batch, threat.ListSummary, new Vector2(row.X + 43, row.Y + 25), ColorPalette.Muted, 0.39f, 178);
            if (_settings.ShowHotkeyBadges)
            {
                DrawTextRight(batch, (index + 1).ToString(), new Vector2(row.Right - 10, row.Y + 9),
                    selected ? LibraryAccentText(threat.PrimaryColor, selectedFill) : ColorPalette.Muted, 0.43f);
            }
        }

        var selectedThreat = _libraryThreats[_enemyLibraryIndex];
        if (selectedThreat.Definition is { } definition) DrawEnemyLibraryDetails(batch, p, definition, detailPanel);
        else DrawSignalRoleLibraryDetails(batch, p, selectedThreat.SignalRole, detailPanel);
        DrawText(batch, $"Complete threat and signal references; values precede scaling.  TAB changes page; ESC, right-click, or BACK returns to {returnDestination}.",
            new Vector2(640, 674), ColorPalette.Muted, 0.45f, true);
    }

    private static void DrawThreatIcon(SpriteBatch batch, PrimitiveRenderer p, ThreatLibraryEntry threat, Vector2 center, int radius)
    {
        if (threat.Definition is { } definition)
        {
            p.DrawShape(batch, center, radius, definition.Visual.Shape, definition.Visual.PrimaryColor,
                definition.Visual.AccentColor, definition.Visual.Marks, definition.Visual.Ring);
            return;
        }
        EnemySignalGlyphRenderer.DrawCarrierIcon(batch, p, threat.SignalRole, center, radius);
    }

    private void DrawEnemyLibraryDetails(SpriteBatch batch, PrimitiveRenderer p, EnemyDefinition definition, Rectangle panel)
    {
        var accent = definition.Visual.PrimaryColor;
        p.DrawShape(batch, new Vector2(panel.X + 42, panel.Y + 45), Math.Min(29, definition.Visual.Radius + 5), definition.Visual.Shape,
            accent, definition.Visual.AccentColor, definition.Visual.Marks, definition.Visual.Ring);
        DrawText(batch, definition.DisplayName.ToUpperInvariant(), new Vector2(panel.X + 84, panel.Y + 17), ColorPalette.Ink, 0.98f);
        DrawText(batch, ThreatRole(definition), new Vector2(panel.X + 84, panel.Y + 49),
            LibraryAccentText(accent, ColorPalette.Panel), 0.57f);
        DrawFittedText(batch, ThreatCounter(definition), new Vector2(panel.X + 18, panel.Y + 82), ColorPalette.Muted, 0.48f, panel.Width - 36);
        p.FillRect(batch, new Rectangle(panel.X + 18, panel.Y + 103, panel.Width - 36, 2), accent);

        var profileY = panel.Y + 116;
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 18, profileY, 270, 142), "BASE PROFILE", ColorPalette.Cyan,
        [
            $"HEALTH  {definition.MaxHealth:0}",
            $"SPEED  {definition.Speed:0} px/s",
            $"REWARD  {definition.Reward} CREDITS",
            $"BREACH  {definition.LivesLost} {(definition.LivesLost == 1 ? "LIFE" : "LIVES")}"
        ]);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 306, profileY, 270, 142), "DEFENSES", ColorPalette.Violet,
        [
            $"ARMOR  {definition.Armor:0.#}",
            $"SHIELD  {definition.Shield:0}",
            $"REGEN  {definition.RegenerationPerSecond:0.#}/s",
            $"BODY RADIUS  {definition.Visual.Radius}"
        ]);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 594, profileY, 278, 142), "BEST ANSWERS", ColorPalette.Green,
            ThreatAnswers(definition));

        DrawText(batch, "RANK MODIFIERS", new Vector2(panel.X + 18, panel.Y + 270), ColorPalette.Navy, 0.62f);
        var rankY = panel.Y + 294;
        DrawRankCard(batch, p, new Rectangle(panel.X + 18, rankY, 270, 112), EnemyRank.Standard, accent,
        [
            "HEALTH x1.00  |  SPEED x1.00",
            "ARMOR +0  |  CONTROL RESIST 0%",
            "REWARD x1  |  BASE BREACH"
        ]);
        DrawRankCard(batch, p, new Rectangle(panel.X + 306, rankY, 270, 112), EnemyRank.Elite, ColorPalette.Gold,
        [
            "HEALTH x1.85  |  SPEED x1.07",
            "ARMOR +2  |  CONTROL RESIST 30%",
            "REWARD x2  |  BREACH +1 LIFE"
        ]);
        DrawRankCard(batch, p, new Rectangle(panel.X + 594, rankY, 278, 112), EnemyRank.Boss, ColorPalette.Coral,
        [
            "HEALTH x4.50  |  SPEED x0.92",
            "ARMOR +4  |  CONTROL RESIST 60%",
            "REWARD x5  |  BREACH AT LEAST 10",
            "50% PHASE: SHIELD +12%; SPEED x1.28"
        ], 0.40f, 16);

        DrawText(batch, "BATTLEFIELD STATUS LANGUAGE", new Vector2(panel.X + 18, panel.Y + 420), ColorPalette.Navy, 0.62f);
        var statusY = panel.Y + 449;
        DrawStatusReference(batch, p, new Rectangle(panel.X + 18, statusY, 162, 70),
            "SLOW", "DASHED CYAN", "Movement reduced", ColorPalette.Slow, "ring");
        DrawStatusReference(batch, p, new Rectangle(panel.X + 186, statusY, 162, 70),
            "EXPOSE", "VIOLET DIAMOND", "All damage rises", ColorPalette.Violet, "diamond");
        DrawStatusReference(batch, p, new Rectangle(panel.X + 354, statusY, 162, 70),
            "BREAK", "GOLD CHEVRONS", "Armor reduced", ColorPalette.Gold, "break");
        DrawStatusReference(batch, p, new Rectangle(panel.X + 522, statusY, 162, 70),
            "BURN", "ORANGE INNER RING", "Damage; armor -2", ColorPalette.Orange, "circle");
        DrawStatusReference(batch, p, new Rectangle(panel.X + 690, statusY, 182, 70),
            "STUN", "GREEN SQUARES", "Movement halted", ColorPalette.Green, "stun");
    }

    private void DrawSignalRoleLibraryDetails(SpriteBatch batch, PrimitiveRenderer p, EnemySignalRole role, Rectangle panel)
    {
        var threat = ThreatLibraryEntry.FromSignalRole(role);
        var rules = _challenges.FirstOrDefault(challenge => challenge.CounterPressureEnabled) ?? new ChallengeDefinition();
        DrawThreatIcon(batch, p, threat, new Vector2(panel.X + 42, panel.Y + 45), 24);
        DrawText(batch, threat.DisplayName.ToUpperInvariant(), new Vector2(panel.X + 84, panel.Y + 17), ColorPalette.Ink, 0.98f);
        DrawText(batch, "SIGNAL GAUNTLET MODIFIER", new Vector2(panel.X + 84, panel.Y + 49),
            LibraryAccentText(threat.PrimaryColor, ColorPalette.Panel), 0.57f);
        DrawFittedText(batch, SignalRoleDescription(role), new Vector2(panel.X + 18, panel.Y + 82),
            ColorPalette.Muted, 0.48f, panel.Width - 36);
        p.FillRect(batch, new Rectangle(panel.X + 18, panel.Y + 103, panel.Width - 36, 2), threat.PrimaryColor);

        var profileY = panel.Y + 122;
        var cards = SignalRoleReferenceCards(role, rules);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 18, profileY, 270, 174), "ABILITY", threat.PrimaryColor, cards[0], 0.43f, 22);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 306, profileY, 270, 174), "TIMING + REACH", ColorPalette.Violet, cards[1], 0.43f, 22);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 594, profileY, 278, 174), "COUNTERPLAY", ColorPalette.Green, cards[2], 0.43f, 22);

        DrawText(batch, "IDENTIFICATION", new Vector2(panel.X + 18, panel.Y + 322), ColorPalette.Navy, 0.62f);
        DrawFittedText(batch, $"A carrier displays the {threat.SymbolLabel.ToLowerInvariant()} glyph inside its normal enemy body.",
            new Vector2(panel.X + 18, panel.Y + 352), ColorPalette.Ink, 0.49f, panel.Width - 36);
        DrawFittedText(batch, "This is a modifier, not a separate body type. Crawler, Runner, Brute, Aegis, and Regenerator carriers retain their normal base profile.",
            new Vector2(panel.X + 18, panel.Y + 382), ColorPalette.Muted, 0.46f, panel.Width - 36);
        DrawFittedText(batch, "Signal roles can modify any compatible base enemy in Signal Gauntlet.",
            new Vector2(panel.X + 18, panel.Y + 430), LibraryAccentText(threat.PrimaryColor, ColorPalette.Panel), 0.46f, panel.Width - 36);
    }

    private static string SignalRoleDescription(EnemySignalRole role) => role switch
    {
        EnemySignalRole.Accelerator => "A passive formation carrier that increases the movement speed of nearby threats.",
        EnemySignalRole.Restorer => "A support carrier that periodically repairs nearby damaged threats.",
        EnemySignalRole.Bulwark => "A support carrier that periodically grants temporary shielding to nearby threats.",
        EnemySignalRole.Jammer => "An attacker that periodically suppresses one nearby combat tower.",
        EnemySignalRole.Disruptor => "An elite or boss attacker that periodically pauses groups of nearby towers.",
        _ => "No additional signal behavior."
    };

    private static IReadOnlyList<string>[] SignalRoleReferenceCards(EnemySignalRole role, ChallengeDefinition rules)
    {
        var normalInterval = rules.CounterPressureInterval;
        return role switch
        {
            EnemySignalRole.Accelerator =>
            [
                [$"NEARBY THREAT SPEED +{rules.CounterHasteBonus:P0}", "PASSIVE WHILE CARRIER LIVES", "DOES NOT HASTE ITSELF"],
                [$"FORMATION RADIUS {rules.CounterSupportRadius:0}", "UPDATES CONTINUOUSLY", "MULTIPLE CARRIERS DO NOT STACK"],
                ["FOCUS THE SIGNAL CARRIER", "SLOW THE FORMATION", "COVER THE ROUTE BEFORE THE AURA"]
            ],
            EnemySignalRole.Restorer =>
            [
                [$"RESTORES {rules.CounterRepairFraction:P0} MAX HEALTH", "AFFECTS UP TO 7 NEARBY THREATS", "ONLY DAMAGED HEALTH IS RESTORED"],
                [$"PULSE EVERY {normalInterval:0.#}s", $"SUPPORT RADIUS {rules.CounterSupportRadius:0}", "FIRST PULSE IS DELAYED"],
                ["BURST DOWN THE CARRIER", "MAINTAIN CONTINUOUS DAMAGE", "SEPARATE OR CONTROL THE GROUP"]
            ],
            EnemySignalRole.Bulwark =>
            [
                [$"GRANTS {rules.CounterShieldFraction:P0} MAX-HEALTH SHIELD", $"SHIELD CAP +{rules.CounterShieldCapacityFraction:P0}", "AFFECTS UP TO 7 NEARBY THREATS"],
                [$"PULSE EVERY {normalInterval * 1.12f:0.#}s", $"SUPPORT RADIUS {rules.CounterSupportRadius:0}", "FIRST PULSE IS DELAYED"],
                ["REMOVE THE CARRIER FIRST", "USE SHIELD-BYPASSING PRESSURE", "BREAK THE FORMATION WITH CONTROL"]
            ],
            EnemySignalRole.Jammer =>
            [
                [$"ATTACK RATE -{rules.CounterSuppressionRatePenalty:P0}", $"DAMAGE -{rules.CounterSuppressionDamagePenalty:P0}", "SUPPRESSES ONE NEAREST COMBAT TOWER"],
                [$"PULSE EVERY {normalInterval:0.#}s", $"TARGET RANGE {rules.CounterSuppressionRadius:0}", $"SUPPRESSION LASTS {rules.CounterSuppressionDuration:0.#}s"],
                ["FOCUS THE CARRIER", "SPREAD CRITICAL DAMAGE SOURCES", "PLACE RANGE OUTSIDE ITS REACH"]
            ],
            EnemySignalRole.Disruptor =>
            [
                ["PAUSES EVERY TOWER IN ITS PULSE", "ELITE AND BOSS CARRIERS ONLY", "TOWERS HAVE A SHORT RE-HIT LOCKOUT"],
                [$"ELITE: {normalInterval * 0.86f:0.#}s / R{rules.CounterPressureRadius * 1.12f:0}", $"BOSS: {normalInterval * 0.72f:0.#}s / R{rules.CounterPressureRadius * 1.32f:0}", $"BASE PAUSE {rules.CounterPressureDuration:0.#}s"],
                ["DAMAGE IT BEFORE THE NEXT PULSE", "DISTRIBUTE TOWERS ACROSS THE MAP", "USE LONG RANGE OUTSIDE THE FIELD"]
            ],
            _ => [[], [], []]
        };
    }

    private void DrawRankCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, EnemyRank rank,
        Color accent, IReadOnlyList<string> lines, float lineScale = 0.42f, int lineSpacing = 19)
        => DrawEnemyInfoCard(batch, p, rect, rank.ToString().ToUpperInvariant(), accent, lines, lineScale, lineSpacing);

    private void DrawStatusReference(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect,
        string title, string symbol, string meaning, Color accent, string shape)
        => DrawStatusLegendEntry(batch, p, rect, title, symbol, meaning, accent, shape);

    private void DrawEnemyInfoCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string title, Color accent,
        IReadOnlyList<string> lines, float lineScale = 0.47f, int lineSpacing = 19)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 5), accent);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        DrawFittedText(batch, title, new Vector2(rect.X + 12, rect.Y + 14),
            LibraryAccentText(accent, ColorPalette.PanelAlt), 0.61f, rect.Width - 24);
        var y = rect.Y + 43;
        foreach (var line in lines)
        {
            if (y + 14 > rect.Bottom - 5) break;
            DrawFittedText(batch, line, new Vector2(rect.X + 12, y), ColorPalette.Ink, lineScale, rect.Width - 24);
            y += lineSpacing;
        }
    }

    private void DrawStatusLegendEntry(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string title,
        string symbol, string meaning, Color accent, string shape)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        var center = new Vector2(rect.X + 18, rect.Y + 19);
        if (shape == "ring") p.DashedRing(batch, center, 9, accent, 10, 2);
        else if (shape == "circle") p.Ring(batch, center, 8, accent, 3);
        else if (shape == "break") StatusGlyphRenderer.DrawArmorBreak(batch, p, center, 2);
        else if (shape == "stun") StatusGlyphRenderer.DrawStun(batch, p, center, 2, 0.5f);
        else p.DrawShape(batch, center, 8, shape, accent, ColorPalette.Ink, 0, false);
        DrawText(batch, title, new Vector2(rect.X + 34, rect.Y + 7), ColorPalette.Navy, 0.49f);
        DrawFittedText(batch, symbol, new Vector2(rect.X + 9, rect.Y + 34),
            LibraryAccentText(accent, ColorPalette.PanelAlt), 0.36f, rect.Width - 18);
        DrawFittedText(batch, meaning, new Vector2(rect.X + 9, rect.Y + 50), ColorPalette.Muted, 0.36f, rect.Width - 18);
    }

    private static string ThreatRole(EnemyDefinition definition) => definition.RegenerationPerSecond > 0
        ? "SUSTAINED REGENERATOR"
        : definition.Shield > 0
            ? "SHIELDED HEAVY"
            : definition.Speed >= 100
                ? "FAST BREAKTHROUGH"
                : definition.Armor >= 4
                    ? "ARMORED BRUISER"
                    : "LIGHT SWARM UNIT";

    private static string ThreatCounter(EnemyDefinition definition) => definition.RegenerationPerSecond > 0
        ? "Keep pressure continuous; focused damage and armor break prevent recovery windows."
        : definition.Shield > 0
            ? "Use sustained or shield-bypassing fire, then concentrated anti-armor damage."
            : definition.Speed >= 100
                ? "Cover early route sections with rapid fire, slowing fields, or reliable chain attacks."
                : definition.Armor >= 4
                    ? "Favor armor pierce, armor break, and high-impact shots over many light hits."
                    : "Efficient rapid fire and area coverage stop numbers from consuming targeting time.";

    private static string[] ThreatAnswers(EnemyDefinition definition) => definition.RegenerationPerSecond > 0
        ? ["PRISM / BREAKER", "SEARING EMBER", "WATCH PRIORITY FIRE", "NO LONG DAMAGE GAPS"]
        : definition.Shield > 0
            ? ["PRISM BYPASSES SHIELD", "BREAKER AFTER SHIELD", "FOCUS TARGETING", "CONTROL BUYS TIME"]
            : definition.Speed >= 100
                ? ["FROST AREA SLOW", "NEEDLE RAPID FIRE", "ARC CHAIN COVERAGE", "FIRST TARGETING"]
                : definition.Armor >= 4
                    ? ["BREAKER CANNON", "RAIL / LANCE ROLES", "PRISM EXPOSURE", "STRONGEST TARGETING"]
                    : ["SHARD FAN", "NEEDLE ARRAY", "ARC RELAY", "AREA CONTROL"];

    private sealed record CampaignWaveReference(
        int Number,
        string Archetype,
        int Contacts,
        float HealthMultiplier,
        float SpeedMultiplier,
        string Threats,
        string Roster)
    {
        public static CampaignWaveReference From(WaveDefinition wave, IReadOnlyDictionary<string, EnemyDefinition> enemies)
        {
            var intel = WaveIntel.Analyze(wave, enemies);
            var roster = string.Join(" + ", wave.Groups
                .GroupBy(group => (group.EnemyId, group.Rank))
                .Select(groups =>
            {
                var first = groups.First();
                var name = enemies.TryGetValue(first.EnemyId, out var enemy) ? enemy.DisplayName : first.EnemyId;
                var compactName = first.EnemyId.ToLowerInvariant() switch
                {
                    "t1_crawler" => "CRAWLER",
                    "t2_runner" => "RUNNER",
                    "t3_brute" => "BRUTE",
                    "t4_aegis" => "AEGIS",
                    "t5_regenerator" => "REGEN",
                    _ => name.ToUpperInvariant()
                };
                var rank = first.Rank.Equals("Elite", StringComparison.OrdinalIgnoreCase) ? "E-" :
                    first.Rank.Equals("Boss", StringComparison.OrdinalIgnoreCase) ? "B-" : "";
                return $"{groups.Sum(group => group.Count)} {rank}{compactName}";
            }));
            return new CampaignWaveReference(wave.Number, wave.Archetype, wave.Groups.Sum(group => group.Count),
                wave.HealthMultiplier, wave.SpeedMultiplier, intel.CompactThreats, roster);
        }
    }

    private void DrawTowerLibraryDetails(SpriteBatch batch, PrimitiveRenderer p, TowerDefinition definition, Rectangle panel)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        var towerAccent = TowerLibraryAccent(definition);
        p.DrawShape(batch, new Vector2(panel.X + 36, panel.Y + 42), 20, definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
        DrawText(batch, definition.DisplayName.ToUpperInvariant(), new Vector2(panel.X + 72, panel.Y + 16), ColorPalette.Ink, 0.94f);
        var operation = definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
            ? "AURA SUPPORT  |  NO TARGETING"
            : $"DEFAULT TARGET {definition.DefaultTargetMode.ToUpperInvariant()}";
        DrawText(batch, $"{TowerInfo.ShortRole(definition).ToUpperInvariant()}  |  BUILD {definition.PurchaseCost}  |  {operation}",
            new Vector2(panel.X + 72, panel.Y + 43), ColorPalette.Muted, 0.53f);
        DrawFittedText(batch, TowerInfo.ProtocolLibraryEffectSummary(definition),
            new Vector2(panel.X + 72, panel.Y + 62), ColorPalette.Coral, 0.42f, panel.Width - 90);
        DrawFittedText(batch, TowerInfo.ProtocolAutoTriggerSummary(definition.Protocol),
            new Vector2(panel.X + 72, panel.Y + 79), ColorPalette.Auto, 0.41f, panel.Width - 90);
        DrawFittedText(batch, $"{TowerInfo.Strength(definition)}  |  {TowerInfo.Limitation(definition)}",
            new Vector2(panel.X + 18, panel.Y + 96), towerAccent, 0.42f, panel.Width - 36);
        DrawFittedText(batch, TowerInfo.ApexLibrarySummary(definition), new Vector2(panel.X + 18, panel.Y + 113),
            ColorPalette.Violet, 0.40f, panel.Width - 36);
        p.FillRect(batch, new Rectangle(panel.X + 18, panel.Y + 130, panel.Width - 36, 2),
            towerAccent);

        var levelOne = definition.Levels[0];
        var levelTwo = definition.Levels.Count > 1 ? definition.Levels[1] : levelOne;
        if (definition.Tier2Doctrines.Count >= 2 && definition.Specializations.Count >= 2)
        {
            _towerLibraryDoctrineIndex = Math.Clamp(_towerLibraryDoctrineIndex, 0, 1);
            var doctrine = definition.Tier2Doctrines[_towerLibraryDoctrineIndex];
            const int doctrineWidth = 270;
            var topY = panel.Y + 138;
            _towerLibraryDoctrineAButton = new Rectangle(panel.X + 302, topY, doctrineWidth, 170);
            _towerLibraryDoctrineBButton = new Rectangle(panel.X + 586, topY, doctrineWidth, 170);
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18, topY, doctrineWidth, 170), definition,
                levelOne, "LEVEL 1", $"BUILD {definition.PurchaseCost}  |  TOTAL {definition.PurchaseCost}", towerAccent);
            var firstDoctrine = definition.Tier2Doctrines[0];
            var secondDoctrine = definition.Tier2Doctrines[1];
            DrawTowerLibraryCard(batch, p, _towerLibraryDoctrineAButton, definition,
                levelTwo.WithDoctrine(firstDoctrine), $"L2 {firstDoctrine.DisplayName.ToUpperInvariant()}",
                $"UPGRADE {firstDoctrine.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToDoctrine(definition, firstDoctrine)}",
                ColorPalette.Cyan, firstDoctrine.Summary, _towerLibraryDoctrineIndex == 0);
            DrawTowerLibraryCard(batch, p, _towerLibraryDoctrineBButton, definition,
                levelTwo.WithDoctrine(secondDoctrine), $"L2 {secondDoctrine.DisplayName.ToUpperInvariant()}",
                $"UPGRADE {secondDoctrine.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToDoctrine(definition, secondDoctrine)}",
                ColorPalette.Cyan, secondDoctrine.Summary, _towerLibraryDoctrineIndex == 1);

            for (var index = 0; index < 2; index++)
            {
                var specialization = definition.Specializations[index];
                DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 436, panel.Y + 318, 418, 206), definition,
                    specialization.Level.WithDoctrine(doctrine), specialization.DisplayName.ToUpperInvariant(),
                    $"AFTER {doctrine.DisplayName.ToUpperInvariant()} {specialization.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToSpecialization(definition, doctrine, specialization)}",
                    ColorPalette.Violet, specialization.Summary);
            }
            return;
        }
        if (definition.Specializations.Count > 0)
        {
            var topWidth = 418;
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18, panel.Y + 138, topWidth, 162), definition,
                levelOne, "LEVEL 1", $"BUILD {definition.PurchaseCost}  |  TOTAL {definition.PurchaseCost}", towerAccent);
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 454, panel.Y + 138, topWidth, 162), definition,
                levelTwo, "LEVEL 2", $"UPGRADE {levelOne.UpgradeCost ?? 0}  |  TOTAL {TowerInfo.TotalCostToLevel(definition, 1)}", ColorPalette.Cyan,
                null);

            for (var index = 0; index < Math.Min(2, definition.Specializations.Count); index++)
            {
                var specialization = definition.Specializations[index];
                DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 436, panel.Y + 318, topWidth, 206), definition,
                    specialization.Level, specialization.DisplayName.ToUpperInvariant(),
                    $"FINAL {specialization.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToSpecialization(definition, specialization)}",
                    ColorPalette.Violet, specialization.Summary);
            }
            return;
        }

        const int cardWidth = 276;
        for (var index = 0; index < Math.Min(3, definition.Levels.Count); index++)
        {
            var level = definition.Levels[index];
            var incrementalCost = index == 0 ? definition.PurchaseCost : definition.Levels[index - 1].UpgradeCost ?? 0;
            var costKind = index == 0 ? "BUILD" : "UPGRADE";
            var accent = index switch { 0 => towerAccent, 1 => ColorPalette.Cyan, _ => ColorPalette.Violet };
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 290, panel.Y + 138, cardWidth, 386), definition,
                level, $"LEVEL {index + 1}", $"{costKind} {incrementalCost}  |  TOTAL {TowerInfo.TotalCostToLevel(definition, index)}", accent,
                null);
        }
    }

    private void DrawTowerLibraryCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, TowerDefinition definition,
        TowerLevelDefinition level, string title, string cost, Color accent, string? summary = null, bool selected = false)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        var lineAccent = accent;
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 5), lineAccent);
        p.DrawRect(batch, rect, selected ? lineAccent : ColorPalette.CardOutline, selected ? 3 : 1);
        DrawFittedText(batch, title, new Vector2(rect.X + 12, rect.Y + 14), ColorPalette.Navy, 0.66f, rect.Width - 24);
        DrawFittedText(batch, cost, new Vector2(rect.X + 12, rect.Y + 38),
            accent, 0.48f, rect.Width - 24);
        var dividerY = rect.Y + 62;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            DrawWrappedText(batch, summary, new Rectangle(rect.X + 12, rect.Y + 59, rect.Width - 24, 32), ColorPalette.Muted, 0.42f, 2);
            dividerY = rect.Y + 96;
        }
        p.FillRect(batch, new Rectangle(rect.X + 12, dividerY, rect.Width - 24, 1), ColorPalette.CardOutline);
        var y = dividerY + 12;
        foreach (var line in TowerInfo.LibraryStatLines(definition, level))
        {
            if (y + 15 > rect.Bottom - 6) break;
            DrawFittedText(batch, line, new Vector2(rect.X + 12, y), ColorPalette.Ink, 0.46f, rect.Width - 24);
            y += 18;
        }
    }

    private static Color LibraryAccentText(Color accent, Color background) =>
        ColorPalette.BalancedAccentText(accent, background);

    private static Color TowerLibraryAccent(TowerDefinition definition) =>
        definition.Visual.AccentColor;

    private static Rectangle TowerLibraryRow(int index) => new(66, 148 + index * 49, 244, 44);
    private static Rectangle EnemyLibraryRow(int index) => new(66, 148 + index * 49, 244, 44);
    private static Rectangle CampaignLibraryMapRow(int index) => new(66, 148 + index * 116, 244, 102);

    private void DrawDiscoveryEmptyState(SpriteBatch batch, string title, string detail, Rectangle panel)
    {
        DrawText(batch, title, new Vector2(panel.Center.X, panel.Center.Y - 18), ColorPalette.Navy, 0.72f, true);
        DrawFittedCenteredText(batch, detail, new Vector2(panel.Center.X, panel.Center.Y + 22), ColorPalette.Muted, 0.48f, panel.Width - 80);
    }


    private sealed record ThreatLibraryEntry(
        string Id,
        string DisplayName,
        EnemyDefinition? Definition,
        EnemySignalRole SignalRole,
        Color PrimaryColor,
        string Shape,
        string ListSummary,
        string SymbolLabel)
    {
        public static ThreatLibraryEntry FromEnemy(EnemyDefinition enemy) => new(
            enemy.Id, enemy.DisplayName, enemy, EnemySignalRole.None, enemy.Visual.PrimaryColor,
            enemy.Visual.Shape, $"HP {enemy.MaxHealth:0}  |  SPD {enemy.Speed:0}", "BASE THREAT");

        public static ThreatLibraryEntry FromSignalRole(EnemySignalRole role) => role switch
        {
            EnemySignalRole.Accelerator => new("signal:accelerator", "Accelerator Signal", null, role,
                EnemySignalGlyphRenderer.Accent(role), "circle", "MODIFIER // FORMATION SPEED", "CYAN DOUBLE-CHEVRON"),
            EnemySignalRole.Restorer => new("signal:restorer", "Restorer Signal", null, role,
                EnemySignalGlyphRenderer.Accent(role), "circle", "MODIFIER // FORMATION REPAIR", "GREEN PLUS"),
            EnemySignalRole.Bulwark => new("signal:bulwark", "Bulwark Signal", null, role,
                EnemySignalGlyphRenderer.Accent(role), "circle", "MODIFIER // FORMATION SHIELD", "CYAN DIAMOND"),
            EnemySignalRole.Jammer => new("signal:jammer", "Jammer Signal", null, role,
                EnemySignalGlyphRenderer.Accent(role), "circle", "MODIFIER // TOWER SUPPRESSION", "ORANGE MINUS"),
            EnemySignalRole.Disruptor => new("signal:disruptor", "Disruptor Signal", null, role,
                EnemySignalGlyphRenderer.Accent(role), "circle", "MODIFIER // GROUP DISRUPTION", "VIOLET X"),
            _ => new("signal:none", "No Signal", null, role, ColorPalette.Muted, "circle", "NO SPECIAL ROLE", "NO SIGNAL")
        };
    }

    private void DrawSandboxButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled,
        Color fillColor, string? hotkey = null) =>
        DrawButton(batch, p, rect, text, enabled, fillColor, ContrastAwareButtonTextColor(fillColor), hotkey);

    private void DrawButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled,
        Color fillColor, Color? textColor = null, string? hotkey = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        var background = enabled ? fillColor : ColorPalette.Disabled;
        p.FillRect(batch, rect, background);
        p.DrawRect(batch, rect, enabled ? ColorPalette.Ink : ColorPalette.Muted, enabled ? 2 : 1);
        var scale = 0.65f;
        var measured = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        var showHotkeyBadge = _settings.ShowHotkeyBadges && !string.IsNullOrWhiteSpace(hotkey);
        var badgeAllowance = showHotkeyBadge ? Math.Min(12, rect.Width / 8) : 0;
        if (measured > rect.Width - 12 - badgeAllowance) scale *= (rect.Width - 12 - badgeAllowance) / measured;
        DrawText(batch, text, new Vector2(rect.Center.X, rect.Center.Y), enabled ? textColor ?? ColorPalette.Paper : ColorPalette.Muted, MathF.Max(0.38f, scale), true);
        if (showHotkeyBadge) DrawHotkeyBadge(batch, p, rect, hotkey!, enabled);
    }

    private void DrawHotkeyBadge(SpriteBatch batch, PrimitiveRenderer p, Rectangle button, string hotkey, bool enabled)
    {
        var label = hotkey.ToUpperInvariant();
        var height = Math.Clamp(button.Height / 3, 9, 12);
        var labelScale = height <= 9 ? 0.24f : 0.27f;
        var measuredWidth = _font.MeasureString(label).X * labelScale * GameConstants.FontDrawScale;
        var width = Math.Max(height, (int)MathF.Ceiling(measuredWidth) + 5);
        var badge = new Rectangle(button.Right - width - 3, button.Y + 3, width, height);
        var fill = enabled ? ColorPalette.Navy : ColorPalette.Muted;
        var foreground = enabled ? ColorPalette.Paper : ColorPalette.PanelAlt;
        p.FillRect(batch, badge, fill);
        p.DrawRect(batch, badge, foreground, 1);
        DrawFittedCenteredText(batch, label, badge.Center.ToVector2(), foreground, labelScale, badge.Width - 3);
    }

    private string DescribeSpecial(TowerInstance tower)
    {
        var level = tower.Level;
        return tower.Definition.Behavior.ToLowerInvariant() switch
        {
            "slow_projectile" => $"AoE {level.SplashRadius:0}; slow {level.SlowPercent:P0}",
            "burn_projectile" => $"Burn {level.BurnDamagePerSecond:0.#}/s",
            "armor_projectile" => $"Pierce {level.ArmorPierce:0.#}",
            "chain" => $"Chain {level.ChainCount} targets",
            "splash_projectile" => level.SplashTargetLimit > 0
                ? $"Splash {level.SplashRadius:0}; cap {level.SplashTargetLimit}"
                : $"Splash {level.SplashRadius:0} px",
            "beam" => $"Expose +{level.ExposePercent:P0} all incoming damage",
            _ when level.RicochetRange > 0 => $"Ricochet {level.RicochetDamageMultiplier:P0}; reach {level.RicochetRange:0}",
            _ => "Reliable direct fire"
        };
    }

    private void DrawText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, bool centered = false)
    {
        var origin = centered ? _font.MeasureString(text) * 0.5f : Vector2.Zero;
        batch.DrawString(_font, text, position, color, 0, origin, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private void DrawFittedText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maximumWidth)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        scale = MathF.Max(0.36f, scale);
        DrawText(batch, Ellipsize(text, scale, maximumWidth), position, color, scale);
    }

    private void DrawStrictFittedText(SpriteBatch batch, string text, Vector2 position, Color color, float scale,
        float maximumWidth, float minimumScale)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        scale = MathF.Max(minimumScale, scale);
        DrawText(batch, Ellipsize(text, scale, maximumWidth), position, color, scale);
    }

    private void DrawFittedCenteredText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maximumWidth)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        scale = MathF.Max(0.30f, scale);
        DrawText(batch, Ellipsize(text, scale, maximumWidth), position, color, scale, true);
    }

    private void DrawWrappedText(SpriteBatch batch, string text, Rectangle bounds, Color color, float scale, int maximumLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maximumLines <= 0 || bounds.Width <= 0) return;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var line = "";
        var wordIndex = 0;
        for (; wordIndex < words.Length; wordIndex++)
        {
            var candidate = line.Length == 0 ? words[wordIndex] : $"{line} {words[wordIndex]}";
            if (_font.MeasureString(candidate).X * scale * GameConstants.FontDrawScale <= bounds.Width)
            {
                line = candidate;
                continue;
            }
            if (line.Length == 0)
            {
                lines.Add(Ellipsize(candidate, scale, bounds.Width));
                line = "";
            }
            else
            {
                lines.Add(line);
                line = words[wordIndex];
            }
            if (lines.Count >= maximumLines) break;
        }
        if (lines.Count < maximumLines && line.Length > 0) lines.Add(line);
        var truncated = wordIndex < words.Length;
        if (truncated && lines.Count > 0)
            lines[^1] = Ellipsize(lines[^1] + "...", scale, bounds.Width);
        var lineHeight = MathF.Max(12, _font.LineSpacing * scale * GameConstants.FontDrawScale * 1.05f);
        for (var index = 0; index < lines.Count && index < maximumLines; index++)
        {
            if (bounds.Y + index * lineHeight >= bounds.Bottom) break;
            DrawText(batch, lines[index], new Vector2(bounds.X, bounds.Y + index * lineHeight), color, scale);
        }
    }

    private string Ellipsize(string text, float scale, float maximumWidth)
    {
        if (_font.MeasureString(text).X * scale * GameConstants.FontDrawScale <= maximumWidth) return text;
        const string suffix = "...";
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            var candidate = text[..middle].TrimEnd() + suffix;
            if (_font.MeasureString(candidate).X * scale * GameConstants.FontDrawScale <= maximumWidth) low = middle;
            else high = middle - 1;
        }
        return text[..low].TrimEnd() + suffix;
    }

    private void DrawTextRight(SpriteBatch batch, string text, Vector2 position, Color color, float scale)
    {
        var size = _font.MeasureString(text);
        batch.DrawString(_font, text, position, color, 0, new Vector2(size.X, 0), scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }
}
