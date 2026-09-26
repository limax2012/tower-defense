using MaximalBastion.Core;
using MaximalBastion.Analytics;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Rendering;
using MaximalBastion.Towers;
using MaximalBastion.Tactics;
using MaximalBastion.Waves;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public enum UiAction
{
    None,
    OpenSoloSetup,
    OpenCoOpSetup,
    Play,
    ReleaseNotes,
    Settings,
    PreviewSettings,
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
    RetryWave,
    ContinueEndless,
    ViewField,
    ViewResults,
    MainMenu,
    Exit
}

public sealed partial class UIManager
{
    internal static readonly Rectangle HudThreatBounds = new(820, 6, 140, 44);
    internal static readonly Rectangle HudRunSetupBounds = new(974, 6, 288, 44);
    internal static readonly Rectangle CoOpTacticalTitleBounds = new(980, 56, 178, 34);
    internal static readonly Rectangle CoOpLinkStatusBounds = new(1164, 58, 96, 14);
    internal static readonly Rectangle CoOpReadyStatusBounds = new(1164, 74, 96, 14);
    internal static readonly Rectangle CoOpPauseResumeBounds = new(986, 196, 268, 46);
    internal static readonly Rectangle CoOpPauseRestartBounds = new(986, 254, 268, 42);
    internal static readonly Rectangle CoOpPauseMenuBounds = new(986, 308, 268, 42);
    internal static readonly Rectangle MainMenuLeftDefenseBounds = new(32, 70, 235, 594);
    internal static readonly Rectangle MainMenuRightDefenseBounds = new(1013, 70, 235, 594);

    // Intel icons use a fixed header footprint rather than their battlefield
    // size. An 18px body plus the optional 6px ring leaves clear padding from
    // the card border, title, and first detail row.
    private const int TowerIntelIconRadiusCap = 18;
    private static readonly Vector2 TowerIntelIconCenter = new(1000, 503);
    private readonly SpriteFont _font;
    private readonly SpriteFont _displayFont;
    private readonly float _displayRasterScale;
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
    private Rectangle _sandboxSignalButton;
    private Rectangle _sandboxSpawnButton;
    private Rectangle _sandboxClearTowersButton;
    private Rectangle _sandboxResetButton;
    private Rectangle _sandboxProtocolButton;
    private Rectangle _sandboxToggleTowerButton;
    private Rectangle _sandboxRemoveTowerButton;
    private Rectangle _sandboxWavePreviousButton;
    private Rectangle _sandboxWaveNextButton;
    private Rectangle _sandboxWaveSignalsButton;
    private string? _hoveredTowerCardId;
    private TowerLevelDefinition? _hoveredUpgradePreview;
    private string? _hoveredUpgradePreviewLabel;
    private PowerNodeData? _hoveredPowerNode;
    private readonly List<(string Id, string Name, string PathStyle, IReadOnlyList<Vector2> Path)> _maps = new();
    private readonly List<DifficultyDefinition> _difficulties = new();
    private readonly List<ChallengeDefinition> _challenges = new();
    private int _selectedMapIndex;
    private int _selectedDifficultyIndex;
    private int _selectedChallengeIndex;
    private int _sandboxEnemyIndex;
    private int _sandboxGroupIndex;
    private int _sandboxRankIndex;
    private int _sandboxHealthIndex;
    private int _sandboxSignalIndex;
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
    private int? _retryCheckpointWave;
    private bool _retryContinuesSolo;
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
    private float _visualTimeSeconds;
    private Vector2 _hudPointer = new(-1, -1);
    private bool _hudPressed;
    private GameSession? _resourceSession;
    private int _previousCredits;
    private int _creditChange;
    private float _creditPulse;
    private MainMenuBattleScene? _mainMenuBattleScene;
    private ReleaseNotesCatalog _releaseNotes = new();

    public int RemoteCoOpSelectedTowerId => _remoteCoOpSelectedTowerId;
    internal bool IsTargetPickerOpen => _targetPickerOpen;
    internal Rectangle TargetPickerBounds => _targetPickerBounds;
    internal Rectangle TargetButtonBounds => _targetButton;
    internal Rectangle UpgradeButtonBounds => _upgradeButton;
    internal Rectangle SellButtonBounds => _sellButton;
    internal Rectangle ProtocolButtonBounds => _overdriveButton;
    internal Rectangle AutoProtocolButtonBounds => _autoProtocolButton;
    internal int CareerMedalPage => _careerMedalPage;
    internal int CareerAchievementPage => _careerAchievementPage;
    internal IReadOnlyDictionary<TargetMode, Rectangle> TargetModeButtonBounds => _targetModeButtons;
    internal Rectangle MainMenuReleaseNotesBounds => _mainMenuReleaseNotesButton;
    private UserSettings _settings = new();
    private string _settingsStatus = "";
    private bool _setupForCoOp;
    private int _settingsSelection;
    private int _activeVolumeSlider = -1;
    private int _resultMenuSelection;
    private readonly Dictionary<string, string> _towerDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Rectangle _mainMenuReleaseNotesButton = new(530, 668, 220, 25);
    private readonly Rectangle _releaseNotesBackButton = new(500, 520, 280, 46);
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
    private readonly Rectangle _saveSlotHistoryButton = new(990, 51, 190, 38);
    private readonly Rectangle _runHistoryViewButton = new(330, 520, 300, 46);
    private readonly Rectangle _runHistoryDeleteButton = new(640, 520, 310, 46);
    private readonly Rectangle _runHistoryCareerButton = new(990, 51, 190, 38);
    private readonly Rectangle _runHistoryLayoutButton = new(340, 650, 280, 42);
    private readonly Rectangle _runHistoryDetailBackButton = new(660, 650, 280, 42);
    private readonly Rectangle _runHistoryCareerBackButton = new(500, 650, 280, 42);
    private readonly Rectangle _careerMedalPreviousButton = new(1040, 590, 44, 32);
    private readonly Rectangle _careerMedalNextButton = new(1160, 590, 44, 32);
    private readonly Rectangle _careerAchievementPreviousButton = new(1040, 590, 44, 32);
    private readonly Rectangle _careerAchievementNextButton = new(1160, 590, 44, 32);
    private readonly Rectangle _hostCoOpButton = new(500, 216, 280, 46);
    private readonly Rectangle _joinHostField = new(500, 326, 280, 42);
    private readonly Rectangle _joinCodeField = new(500, 394, 280, 42);
    private readonly Rectangle _joinCoOpButton = new(500, 456, 280, 46);
    private readonly Rectangle _backButton = new(500, 518, 280, 44);
    private readonly Rectangle _coOpLobbyCodeButton = new(500, 270, 280, 64);
    private readonly Rectangle _coOpReconnectCodeButton = new(500, 370, 280, 46);
    internal static readonly Rectangle PauseResumeBounds = new(460, 221, 360, 48);
    internal static readonly Rectangle PauseSettingsBounds = new(460, 281, 360, 42);
    internal static readonly Rectangle PauseSaveBounds = new(460, 335, 360, 42);
    internal static readonly Rectangle PauseLoadBounds = new(460, 389, 360, 42);
    internal static readonly Rectangle PauseRestartBounds = new(460, 443, 360, 42);
    internal static readonly Rectangle PauseMainMenuBounds = new(460, 497, 360, 42);
    private readonly Rectangle _resultContinueButton = new(296, 580, 206, 46);
    private readonly Rectangle _resultRestartButton = new(518, 580, 206, 46);
    private readonly Rectangle _resultMenuButton = new(740, 580, 206, 46);
    private readonly Rectangle _defeatFieldButton = new(296, 580, 158, 46);
    private readonly Rectangle _defeatRetryButton = new(472, 580, 158, 46);
    private readonly Rectangle _defeatRestartButton = new(648, 580, 158, 46);
    private readonly Rectangle _defeatMenuButton = new(824, 580, 158, 46);
    private readonly Rectangle _fieldResultsButton = new(630, 9, 176, 38);
    private readonly Rectangle _coOpPausedBanner = new(350, 68, 260, 26);
    private readonly Rectangle _windowModeButton = new(350, 242, 580, 54);
    private readonly Rectangle _vsyncButton = new(350, 312, 580, 54);
    private readonly Rectangle _effectsButton = new(350, 382, 580, 54);
    private readonly Rectangle _autoStartButton = new(350, 242, 580, 54);
    private readonly Rectangle _hotkeyBadgesButton = new(350, 312, 580, 54);
    private readonly Rectangle _volumeButton = new(350, 242, 580, 54);
    private readonly Rectangle _musicVolumeButton = new(350, 312, 580, 54);
    private readonly Rectangle _settingsBackButton = new(500, 526, 280, 48);
    private static readonly int[] SandboxGroupSizes = [1, 5, 12];
    private static readonly EnemyRank[] SandboxRanks = [EnemyRank.Standard, EnemyRank.Elite, EnemyRank.Boss];
    private static readonly EnemySignalRole[] SandboxSignals = Enum.GetValues<EnemySignalRole>();

    public string JoinHostInput => _joinHostInput;
    public string JoinCodeInput => _joinCodeInput;
    public string CoOpLobbyTitle { get; private set; } = "PREPARING ONLINE CO-OP";
    public string CoOpLobbyDetail { get; private set; } = "Starting the internet connection...";
    public string CoOpLobbyCode { get; private set; } = "";
    public string CoOpLobbyCopyStatus => _coOpLobbyCopyStatus;
    public string SelectedMapId => _maps.Count == 0 ? "foundry_loop" : _maps[_selectedMapIndex].Id;
    public string SelectedMapName => _maps.Count == 0 ? "Cinderworks" : _maps[_selectedMapIndex].Name;
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
    public int SelectedSettingsIndex => _settingsSelection;
    public int SelectedSandboxWave => _sandboxWaveNumber;

    public void ConfigureSettings(UserSettings settings)
    {
        _settings = settings;
        _activeVolumeSlider = -1;
    }
    public void SetSettingsStatus(string status) => _settingsStatus = status;

    public void AdvanceVisualTime(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return;
        _visualTimeSeconds = (_visualTimeSeconds + Math.Min(elapsedSeconds, 0.25f)) % 120f;
        _creditPulse = MathF.Max(0, _creditPulse - elapsedSeconds);
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
    }

    public void PrepareResultScreen(int? retryCheckpointWave = null, bool retryContinuesSolo = false)
    {
        _resultMenuSelection = 0;
        _resultDetails = false;
        _restartArmed = false;
        _retryCheckpointWave = retryCheckpointWave;
        _retryContinuesSolo = retryCheckpointWave.HasValue && retryContinuesSolo;
    }

    public static string PauseCheckpointStatus(bool canSave) => canSave
        ? "Between waves - save slots are available."
        : "Active wave - saving unlocks after it clears.";

    public const string RestartPreservationLabel = "Restart this run? Manual saves are kept.";

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

    public static string SoloWaveButtonLabel(MaximalBastion.GameSession session, bool autoStartWaves,
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

    public static string PulsePlateButtonLabel(MaximalBastion.GameSession session)
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

    public UIManager(SpriteFont font, SpriteFont? displayFont = null)
    {
        _font = font;
        _displayFont = displayFont ?? font;
        _displayRasterScale = displayFont is null ? 1f : UiTypography.DisplayRasterScale;
    }

    public void ConfigureReleaseNotes(ReleaseNotesCatalog releaseNotes) =>
        _releaseNotes = releaseNotes ?? throw new ArgumentNullException(nameof(releaseNotes));

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

    public void ConfigureMaps(IEnumerable<MapDefinition> maps)
    {
        _maps.Clear();
        _maps.AddRange(maps.OrderBy(x => MapSelectionOrder(x.Id))
            .ThenBy(x => x.DisplayName)
            .Select(x => (x.Id, x.DisplayName, x.PathVisual.Style,
                (IReadOnlyList<Vector2>)x.Path.Select(point => point.ToVector2()).ToArray())));
        _selectedMapIndex = Math.Clamp(_selectedMapIndex, 0, Math.Max(0, _maps.Count - 1));
    }

    public void ConfigureDifficulties(IEnumerable<DifficultyDefinition> difficulties)
    {
        _difficulties.Clear();
        _difficulties.AddRange(difficulties.OrderBy(x => DifficultyOrder(x.Id)).ThenBy(x => x.DisplayName));
        var defaultIndex = _difficulties.FindIndex(x => x.Id.Equals(DifficultyCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
        _selectedDifficultyIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    public void ConfigureChallenges(IEnumerable<ChallengeDefinition> challenges)
    {
        _challenges.Clear();
        _challenges.AddRange(challenges.OrderBy(ChallengeOrder)
            .ThenBy(challenge => challenge.DisplayName));
        var defaultIndex = _challenges.FindIndex(challenge => challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
        _selectedChallengeIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    public void ConfigureTowerNames(IEnumerable<TowerDefinition> towers)
    {
        _towerDisplayNames.Clear();
        foreach (var tower in towers) _towerDisplayNames[tower.Id] = tower.DisplayName;
    }

    public UiAction HandleMainMenu(InputSnapshot input)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_mainMenuReleaseNotesButton.Contains(point)) return UiAction.ReleaseNotes;
        foreach (var item in MainMenuActions())
            if (MainMenuActionBounds(item.Action).Contains(point)) return item.Action == UiAction.LoadGame && !_saveAvailable ? UiAction.None : item.Action;
        return UiAction.None;
    }

    public UiAction HandleReleaseNotes(InputSnapshot input)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.EscapePressed || input.PausePressed || input.RightPressed) return UiAction.MainMenu;
        return input.LeftPressed && _releaseNotesBackButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.MainMenu
            : UiAction.None;
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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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

    internal static Rectangle SetupCardRectangle(int row, int index, int count)
    {
        const int left = 48;
        const int totalWidth = 1184;
        var gap = row == 0 ? 16 : 8;
        var safeCount = Math.Max(1, count);
        var width = (totalWidth - gap * (safeCount - 1)) / safeCount;
        var y = row switch { 0 => 114, 1 => 398, _ => 502 };
        return new Rectangle(left + index * (width + gap), y, width, row == 0 ? 238 : 40);
    }

    public UiAction HandleSettingsInput(InputSnapshot input)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.EscapePressed || input.PausePressed)
        {
            _activeVolumeSlider = -1;
            return UiAction.CloseSettings;
        }

        if (_activeVolumeSlider is 3 or 4)
        {
            _settingsSelection = _activeVolumeSlider;
            if (input.LeftReleased)
            {
                SetVolumeFromSlider(_activeVolumeSlider, input.MousePosition.X);
                _activeVolumeSlider = -1;
                return UiAction.ApplySettings;
            }
            if (input.LeftDown)
            {
                SetVolumeFromSlider(_activeVolumeSlider, input.MousePosition.X);
                return UiAction.PreviewSettings;
            }

            _activeVolumeSlider = -1;
            return UiAction.ApplySettings;
        }

        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var category = 0; category < 3; category++)
            if (SettingsCategoryBounds(category).Contains(point))
            {
                _settingsCategory = category;
                _activeVolumeSlider = -1;
                return UiAction.None;
            }
        for (var index = 0; index < 8; index++)
        {
            if (!SettingVisible(index) || !SettingsOptionRectangle(index).Contains(point)) continue;
            _settingsSelection = index;
            if (index is 3 or 4)
            {
                SetVolumeFromSlider(index, input.MousePosition.X);
                if (!input.LeftDown) return UiAction.ApplySettings;
                _activeVolumeSlider = index;
                return UiAction.PreviewSettings;
            }
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

    private void SetVolumeFromSlider(int index, float pointerX)
    {
        var track = VolumeSliderTrack(SettingsOptionRectangle(index));
        var normalized = MathHelper.Clamp((pointerX - track.X) / Math.Max(1f, track.Width), 0, 1);
        var snapped = MathF.Round(normalized * 20f) / 20f;
        if (index == 3) _settings.SfxVolume = snapped;
        else if (index == 4) _settings.MusicVolume = snapped;
    }

    private static Rectangle VolumeSliderTrack(Rectangle bounds) =>
        new(bounds.X + 210, bounds.Center.Y - 3, bounds.Width - 300, 6);

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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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
            for (var tab = 0; tab < 3; tab++)
                if (CareerCategoryBounds(tab).Contains(careerPoint)) { _careerCategory = tab; return UiAction.None; }
            var medalPageCount = Math.Max(1, (career.Medals.Count + 6) / 7);
            var achievementPageCount = Math.Max(1, (career.Achievements.Count + 7) / 8);
            if (_careerCategory == 1 && _careerMedalPreviousButton.Contains(careerPoint) && _careerMedalPage > 0) _careerMedalPage--;
            else if (_careerCategory == 1 && _careerMedalNextButton.Contains(careerPoint) && _careerMedalPage + 1 < medalPageCount) _careerMedalPage++;
            else if (_careerCategory == 0 && _careerAchievementPreviousButton.Contains(careerPoint) && _careerAchievementPage > 0) _careerAchievementPage--;
            else if (_careerCategory == 0 && _careerAchievementNextButton.Contains(careerPoint) && _careerAchievementPage + 1 < achievementPageCount) _careerAchievementPage++;
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
            _careerCategory = 0;
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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.EscapePressed) return UiAction.CloseRunHistoryField;
        return input.LeftPressed && _fieldResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.CloseRunHistoryField
            : UiAction.None;
    }

    public UiAction HandleCoOpMenu(InputSnapshot input)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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

    public UiAction HandleGameplayInput(InputSnapshot input, MaximalBastion.GameSession session, Action<GameCommand>? commandSink = null, int playerId = 1)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        var point = input.MousePosition.ToPoint();
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
        if (_targetPickerOpen && input.TowerHotkey > 0)
        {
            var modes = session.AvailableTargetModes;
            if (input.TowerHotkey <= modes.Count)
            {
                RequestTargetMode(session, modes[input.TowerHotkey - 1], commandSink, playerId);
                CloseTargetPicker();
            }
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
            _hoveredUpgradePreviewLabel = $"PREVIEW APEX  {apexPreview.ApexUpgradeCost}  |  SELF-AUTO";
        }
        _hoveredTacticalPlacement = _emergencyButton.Contains(point) ? TacticalPlacementKind.PulsePlate :
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
        if (input.EmergencyPressed) session.BeginEmergencyPlacement();
        if (!session.IsSandbox && input.GeneratorPressed) session.BeginGeneratorPlacement();
        if (session.IsSandbox)
        {
            if (input.SandboxWavePreviousPressed) ChangeSandboxWave(session, -1);
            if (input.SandboxWaveNextPressed) ChangeSandboxWave(session, 1);
            if (input.SandboxWaveSignalsPressed) session.ToggleSandboxWaveSignals();
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
            if (input.SandboxSignalPressed) CycleSandboxSignal();
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
        if (_emergencyButton.Contains(point))
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

    private UiAction HandleCoOpPausedInput(InputSnapshot input, MaximalBastion.GameSession session,
        Action<GameCommand>? commandSink, int playerId)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
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

    private bool HandleSandboxControlClick(Point point, MaximalBastion.GameSession session)
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
        if (_sandboxWaveSignalsButton.Contains(point))
        {
            session.ToggleSandboxWaveSignals();
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
        if (_sandboxSignalButton.Contains(point))
        {
            CycleSandboxSignal();
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

    private void ChangeSandboxWave(MaximalBastion.GameSession session, int direction)
    {
        if (session.AuthoredWaveCount <= 0) return;
        _sandboxWaveNumber = (_sandboxWaveNumber - 1 + direction) % session.AuthoredWaveCount;
        if (_sandboxWaveNumber < 0) _sandboxWaveNumber += session.AuthoredWaveCount;
        _sandboxWaveNumber++;
    }

    private static bool HasSandboxProtocolTestState(MaximalBastion.GameSession session) =>
        session.OverdriveCooldownRemaining > 0 || session.Towers.Any(tower => tower.IsOverdriven);

    private static void ToggleSandboxProtocolTest(MaximalBastion.GameSession session)
    {
        if (HasSandboxProtocolTestState(session))
        {
            session.ResetSandboxProtocol();
            return;
        }

        if (session.SelectedTower is { IsSandboxDisabled: false } tower)
            session.TestSandboxProtocol(tower.Id);
    }

    private void CycleSandboxEnemy(MaximalBastion.GameSession session, int direction)
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

    private void CycleSandboxSignal() =>
        _sandboxSignalIndex = (_sandboxSignalIndex + 1) % SandboxSignals.Length;

    private void SpawnSelectedSandboxTargets(MaximalBastion.GameSession session)
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
            _sandboxHealthIndex == 3,
            SandboxSignals[_sandboxSignalIndex]);
    }

    private static IReadOnlyList<EnemyDefinition> SandboxEnemies(MaximalBastion.GameSession session) =>
        session.Content.Enemies.Values.OrderBy(enemy => enemy.MaxHealth).ThenBy(enemy => enemy.Id).ToArray();

    private static void RequestStartWave(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (sink is null) session.StartNextWave();
        else sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.StartWave });
    }

    private static void RequestSpeed(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        var speed = session.Speed < 1.5f ? 2f : 1f;
        if (sink is null) session.SetSpeed(speed);
        else sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SetSpeed, Speed = speed });
    }

    private static void RequestPause(MaximalBastion.GameSession session, Action<GameCommand> sink, int playerId) =>
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SetPaused, Paused = !session.IsCoOpPaused });

    private void ToggleTargetPicker(MaximalBastion.GameSession session)
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

    private static void RequestTargetMode(MaximalBastion.GameSession session, TargetMode mode,
        Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { IsSupport: false } tower || tower.TargetMode == mode ||
            !session.IsTargetModeAvailable(mode)) return;
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

    private static void RequestUpgrade(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
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

    private static void RequestUpgradeChoice(MaximalBastion.GameSession session, int choiceIndex,
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

    private static void RequestSpecialization(MaximalBastion.GameSession session, string specializationId, Action<GameCommand>? sink, int playerId)
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

    private static void RequestDoctrine(MaximalBastion.GameSession session, string doctrineId, Action<GameCommand>? sink, int playerId)
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

    private static void RequestOverdrive(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
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

    private static void RequestAutoProtocol(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (!session.ProtocolsEnabled) return;
        if (session.SelectedTower is not { } tower)
        {
            session.TrySelectTower(session.AutoOverdriveTowerId);
            return;
        }
        if (tower.IsApex) return;
        if (sink is null)
        {
            session.TryToggleAutoProtocol(tower.Id, playerId);
            return;
        }
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.ToggleAutoProtocol, EntityId = tower.Id });
    }

    private static void RequestSell(MaximalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
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

    public UiAction HandlePausedInput(InputSnapshot input, MaximalBastion.GameSession session)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.EscapePressed || input.PausePressed)
        {
            _restartArmed = false;
            return UiAction.Resume;
        }
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        for (var selection = 0; selection < 6; selection++)
        {
            if (!PauseMenuOptionRectangle(selection).Contains(point)) continue;
            return ActivatePauseMenuOption(selection, session);
        }
        return UiAction.None;
    }

    private UiAction ActivatePauseMenuOption(int selection, MaximalBastion.GameSession session)
    {
        if (selection == 4)
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
        return selection switch
        {
            0 => UiAction.Resume,
            1 => UiAction.Settings,
            2 when session.CanSaveCheckpoint => UiAction.SaveGame,
            3 when _saveAvailable => UiAction.LoadGame,
            5 => UiAction.MainMenu,
            _ => UiAction.None
        };
    }

    private static Rectangle PauseMenuOptionRectangle(int selection) => selection switch
    {
        0 => PauseResumeBounds,
        1 => PauseSettingsBounds,
        2 => PauseSaveBounds,
        3 => PauseLoadBounds,
        4 => PauseRestartBounds,
        5 => PauseMainMenuBounds,
        _ => Rectangle.Empty
    };

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

    public UiAction HandleResultInput(InputSnapshot input, bool victory)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.TabPressed || input.NavigateLeftPressed || input.NavigateRightPressed ||
            input.NavigateUpPressed || input.NavigateDownPressed)
        {
            _restartArmed = false;
            var reverse = input.NavigateLeftPressed || input.NavigateUpPressed;
            MoveResultSelection(victory, reverse ? -1 : 1);
            return UiAction.None;
        }
        if (input.EscapePressed)
        {
            if (_resultDetails) { _resultDetails = false; return UiAction.None; }
            if (_restartArmed) { _restartArmed = false; return UiAction.None; }
            return UiAction.MainMenu;
        }
        if (input.EnterPressed) return ActivateResultSelection(victory);
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (ResultDetailsBounds.Contains(point)) { _resultDetails = !_resultDetails; return UiAction.None; }
        var optionCount = victory ? 3 : 4;
        for (var selection = 0; selection < optionCount; selection++)
        {
            if (!ResultOptionRectangle(selection, victory).Contains(point)) continue;
            if (!ResultOptionEnabled(selection, victory)) return UiAction.None;
            _resultMenuSelection = selection;
            return ActivateResultSelection(victory);
        }
        return UiAction.None;
    }

    private void MoveResultSelection(bool victory, int delta)
    {
        var optionCount = victory ? 3 : 4;
        for (var attempts = 0; attempts < optionCount; attempts++)
        {
            _resultMenuSelection = (_resultMenuSelection + delta + optionCount) % optionCount;
            if (ResultOptionEnabled(_resultMenuSelection, victory)) return;
        }
    }

    private UiAction ActivateResultSelection(bool victory)
    {
        var restartSelection = victory ? 1 : 2;
        if (_resultMenuSelection == restartSelection)
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
        if (victory)
            return _resultMenuSelection == 0 ? UiAction.ContinueEndless : UiAction.MainMenu;
        return _resultMenuSelection switch
        {
            0 => UiAction.ViewField,
            1 when _retryCheckpointWave.HasValue => UiAction.RetryWave,
            3 => UiAction.MainMenu,
            _ => UiAction.None
        };
    }

    private bool ResultOptionEnabled(int selection, bool victory) =>
        victory || selection != 1 || _retryCheckpointWave.HasValue;

    private Rectangle ResultOptionRectangle(int selection, bool victory) => victory
        ? selection switch
        {
            0 => _resultContinueButton,
            1 => _resultRestartButton,
            2 => _resultMenuButton,
            _ => Rectangle.Empty
        }
        : selection switch
    {
        0 => _defeatFieldButton,
        1 => _defeatRetryButton,
        2 => _defeatRestartButton,
        3 => _defeatMenuButton,
        _ => Rectangle.Empty
    };

    public UiAction HandleDefeatFieldInput(InputSnapshot input)
    {
        _hudPointer = input.IsMouseOverLogicalCanvas ? input.MousePosition : new Vector2(-1, -1);
        _hudPressed = input.LeftPressed;
        if (input.EscapePressed) return UiAction.ViewResults;
        return input.LeftPressed && _fieldResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.ViewResults
            : UiAction.None;
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer p, GameState state, MaximalBastion.GameSession? session,
        Matrix? transform = null)
    {
        LastMenuHeading = null;
        _readOnlyInspection = state is GameState.DefeatField or GameState.RunHistoryField;
        _archivedLayoutInspection = state == GameState.RunHistoryField;
        if (state == GameState.MainMenu)
        {
            DrawMainMenu(batch, p, transform ?? Matrix.Identity);
            return;
        }
        if (state == GameState.ReleaseNotes)
        {
            DrawReleaseNotes(batch, p);
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
        if (state == GameState.Playing && session.PlacementTowerId is null &&
            session.TacticalPlacement == TacticalPlacementKind.None && !session.IsCoOpPaused)
            DrawAnnouncement(batch, p, session);
        if (state == GameState.Playing && session.IsCoOpPaused)
        {
            DrawCoOpPausedBanner(batch, p, session.CoOpPausePlayerId);
        }
        if (state == GameState.Paused) DrawPauseOverlay(batch, p, session);
        else if (state == GameState.CoOpReconnect) DrawCoOpReconnectOverlay(batch, p);
        else if (state == GameState.Victory) DrawResultOverlay(batch, p, session, true);
        else if (state == GameState.Defeat) DrawResultOverlay(batch, p, session, false);
        else if (state == GameState.DefeatField) DrawDefeatFieldControls(batch, p);
        else if (state == GameState.RunHistoryField) DrawRunHistoryFieldControls(batch, p);
    }

    private void DrawRemoteCoOpCursor(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        if (_remoteCoOpCursor is not { } position || _remoteCoOpCursorPlayerId is < 1 or > 2) return;
        var color = _remoteCoOpCursorPlayerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral;
        if (_remoteCoOpHasPlacementPreview && !string.IsNullOrWhiteSpace(_remoteCoOpPlacementTowerId) &&
            session.Content.Towers.TryGetValue(_remoteCoOpPlacementTowerId, out var placementDefinition))
        {
            var needlePreview = placementDefinition.Id.Equals("needle_turret", StringComparison.OrdinalIgnoreCase);
            DrawRemotePlacementGhost(batch, p, position, placementDefinition.Id, placementDefinition.Visual,
                placementDefinition.Visual.Marks, color,
                needlePreview ? ColorPalette.NeedlePlacementGhostPrimaryAlpha : ColorPalette.PlacementGhostPrimaryAlpha);
            GameRenderer.DrawPowerNodePlacementIndicator(batch, p, _remoteCoOpPlacementPreviewPosition,
                session.Map.GetPowerNodes(_remoteCoOpPlacementPreviewPosition));
        }
        else if (_remoteCoOpHasPlacementPreview && _remoteCoOpTacticalPlacement == TacticalPlacementKind.PulsePlate)
        {
            var plate = session.Content.Tactics.EmergencyDefense;
            DrawRemotePlacementGhost(batch, p, position, plate.Id, plate.Visual, plate.Charges, color);
        }
        else if (_remoteCoOpHasPlacementPreview && _remoteCoOpTacticalPlacement == TacticalPlacementKind.ChargeForge)
        {
            var forge = session.Content.Tactics.Generator;
            DrawRemotePlacementGhost(batch, p, position, forge.Id, forge.Visual, 1, color);
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
                ColorPalette.HighContrastText(color), 0.34f, tag.Width - 6, preserveInk: true);

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
        string hardwareId, TowerVisualData visual, int marks, Color playerColor,
        byte primaryAlpha = ColorPalette.PlacementGhostPrimaryAlpha)
    {
        var previewPosition = _remoteCoOpPlacementPreviewPosition;
        if (Vector2.DistanceSquared(cursorPosition, previewPosition) > 36f)
            p.Line(batch, cursorPosition, previewPosition, ColorPalette.WithAlpha(playerColor, 115), 1);
        var breath = _settings.ReducedEffects ? .5f : (MathF.Sin(_visualTimeSeconds * 2.2f) + 1f) * 0.5f;
        var pulse = 0.985f + breath * 0.025f;
        NightGridArt.Tower(batch, p, previewPosition, visual.Radius * pulse, hardwareId, marks,
            opacity: primaryAlpha / 255f, lights: !_settings.ReducedEffects);
    }

    private void DrawHud(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        if (_resourceSession != session)
        {
            _resourceSession = session;
            _previousCredits = session.Economy.Credits;
            _creditPulse = 0;
        }
        if (_previousCredits != session.Economy.Credits)
        {
            _creditChange = session.Economy.Credits - _previousCredits;
            _previousCredits = session.Economy.Credits;
            _creditPulse = _settings.ReducedEffects ? 0 : .7f;
        }
        CommandSurfaceArt.Surface(batch, p, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.TopBarHeight), ColorPalette.Cyan, 0, false);
        p.FillRect(batch, new Rectangle(0, GameConstants.TopBarHeight - 2, GameConstants.LogicalWidth, 2), ColorPalette.Cyan);
        if (session.IsSandbox)
        {
            DrawSandboxHudMetric(batch, 0, "LIVES", "UNLIMITED", ColorPalette.Coral);
            DrawSandboxHudMetric(batch, 1, "CREDITS", "UNLIMITED", ColorPalette.Gold);
            DrawSandboxHudMetric(batch, 2, "MODE", "LAB", ColorPalette.Cyan);
            DrawSandboxHudMetric(batch, 3, "TARGETS", session.EnemiesRemaining.ToString(), ColorPalette.Muted);
        }
        else
        {
            DrawText(batch, "LIVES", new Vector2(18, 8), ColorPalette.Coral, 0.75f);
            DrawDisplayText(batch, $"{session.Economy.Lives}/{session.Economy.StartingLives}",
                new Vector2(18, 26), ColorPalette.Paper, 1f);
            DrawText(batch, "CREDITS", new Vector2(115, 8), ColorPalette.Gold, 0.75f);
            DrawDisplayValue(batch, session.Economy.Credits.ToString(),
                new Vector2(115, 26), _creditPulse > 0 ? _creditChange > 0 ? ColorPalette.Green : ColorPalette.Gold : ColorPalette.Paper,
                1f, 98);
            if (_creditPulse > 0 && session.Economy.Credits < 10000 && Math.Abs((long)_creditChange) < 10000)
                DrawTextRight(batch, $"{(_creditChange > 0 ? "+" : "")}{_creditChange}", new Vector2(211, 33 - (1 - _creditPulse / .7f) * 7),
                    (_creditChange > 0 ? ColorPalette.Green : ColorPalette.Gold) * (_creditPulse / .7f), .38f);
            DrawText(batch, session.IsEndlessMode ? "ENDLESS" : "WAVE",
                new Vector2(225, 8), ColorPalette.Cyan, 0.75f);
            DrawDisplayText(batch, session.IsEndlessMode ? session.CurrentWave.ToString() : $"{session.CurrentWave}/{session.TotalWaves}",
                new Vector2(225, 26), ColorPalette.Paper, 1f);
            if (new Rectangle(225, 0, 210, 56).Contains(_hudPointer.ToPoint()))
            {
                DrawText(batch, "REMAINING", new Vector2(335, 8), ColorPalette.Muted, .55f);
                DrawText(batch, session.EnemiesRemaining.ToString(), new Vector2(335, 27), ColorPalette.Paper, .8f);
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
            _sandboxWavePreviousButton = new Rectangle(820, 9, 38, 38);
            _sandboxWaveNextButton = new Rectangle(864, 9, 38, 38);
            _sandboxWaveSignalsButton = new Rectangle(908, 9, 56, 38);
            DrawSandboxButton(batch, p, _sandboxWavePreviousButton, "W-", true, ColorPalette.Cyan, "-");
            DrawSandboxButton(batch, p, _sandboxWaveNextButton, "W+", true, ColorPalette.Cyan, "+");
            DrawSandboxButton(batch, p, _sandboxWaveSignalsButton,
                session.SandboxWaveSignalsEnabled ? "SIG ON" : "SIG OFF", !session.SandboxWaveActive,
                session.SandboxWaveSignalsEnabled ? ColorPalette.Orange : ColorPalette.Cyan, "L");
        }
        else
        {
            _sandboxWavePreviousButton = Rectangle.Empty;
            _sandboxWaveNextButton = Rectangle.Empty;
            _sandboxWaveSignalsButton = Rectangle.Empty;
        }

        if (!session.IsSandbox && new Rectangle(225, 0, 210, 56).Contains(_hudPointer.ToPoint()))
        {
            var wave = session.Waves.ActiveWave ?? session.Waves.NextWave;
            if (wave is not null)
                DrawFittedText(batch, WaveIntel.Analyze(wave, session.Content.Enemies).ScalingSummary(
                    session.Difficulty.EnemyHealthMultiplier, session.Difficulty.EnemySpeedMultiplier),
                    new Vector2(824, 26), ColorPalette.Muted, .45f, 132);
        }

    }

    private void DrawSandboxHudMetric(SpriteBatch batch, int column, string label, string value, Color accent)
    {
        var x = 18 + column * 108;
        DrawText(batch, label, new Vector2(x, 8), accent, .65f);
        DrawDisplayValue(batch, value, new Vector2(x, 27), ColorPalette.Paper, .68f, 94);
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

    private void DrawAnnouncement(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        if (session.AnnouncementRemaining <= 0 || string.IsNullOrWhiteSpace(session.AnnouncementTitle)) return;
        var fade = MathHelper.Clamp(session.AnnouncementRemaining / 0.35f, 0, 1);
        var alpha = (byte)(232 * fade);
        var accent = session.AnnouncementPositive ? ColorPalette.Green : ColorPalette.Gold;
        var rect = new Rectangle(240, 64, 480, 48);
        p.FillRect(batch, rect, ColorPalette.WithPremultipliedAlpha(ColorPalette.Navy, alpha));
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 5, rect.Height), ColorPalette.WithPremultipliedAlpha(accent, alpha));
        DrawFittedCenteredText(batch, session.AnnouncementTitle, new Vector2(rect.Center.X, rect.Y + 14),
            ColorPalette.Paper * fade, 0.59f, rect.Width - 32);
        DrawFittedCenteredText(batch, session.AnnouncementSubtitle ?? "", new Vector2(rect.Center.X, rect.Y + 34),
            ColorPalette.Muted * fade, 0.44f, rect.Width - 32);
    }

    private void DrawTacticalBar(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
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
            DrawFittedCenteredText(batch, "FINAL DEFENSE REVIEW", new Vector2(archiveNotice.Center.X, 113),
                ColorPalette.Navy, 0.51f, archiveNotice.Width - 20);
            DrawFittedCenteredText(batch, "SELECT A PLACED TOWER TO INSPECT", new Vector2(archiveNotice.Center.X, 139),
                ColorPalette.Cobalt, 0.42f, archiveNotice.Width - 20);
            DrawFittedCenteredText(batch, "TOWERS SHOWN WITHOUT COMBAT", new Vector2(archiveNotice.Center.X, 165),
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
        var emergencyLabel = session.TacticalSystemsEnabled ? PulsePlateButtonLabel(session) : "PLATES | MODE OFF";
        DrawButton(batch, p, _emergencyButton, emergencyLabel, emergencyReady, ColorPalette.Gold, ColorPalette.Ink, "Q");

        var generator = session.Content.Tactics.Generator;
        var generatorReady = session.TacticalSystemsEnabled && (session.Generator is not null || session.Economy.CanAfford(generator.PurchaseCost));
        var generatorLabel = !session.TacticalSystemsEnabled ? "FORGE | MODE OFF" : session.Generator is { } active
            ? session.EmergencyInventory >= active.Level.Capacity
                ? $"FORGE L{active.LevelIndex + 1} | FULL"
                : session.Waves.IsActive
                    ? $"FORGE L{active.LevelIndex + 1} | +1 IN {active.ProductionRemaining:0}s"
                    : $"FORGE L{active.LevelIndex + 1} | PAUSED {active.ProductionRemaining:0}s"
            : $"FORGE {generator.PurchaseCost}";
        DrawButton(batch, p, _generatorButton, generatorLabel, generatorReady, ColorPalette.Green, hotkey: "G");

        var selected = session.SelectedTower;
        var activeOverdrives = session.Towers.Where(tower => tower.IsOverdriven).OrderBy(tower => tower.Id).ToArray();
        var selectedApexCooldown = selected?.ApexProtocolCooldownRemaining ?? 0;
        var overdriveReady = session.ProtocolsEnabled && selected is not null &&
            session.OverdriveCooldownRemaining <= 0 && !selected.IsOverdriven && selectedApexCooldown <= 0;
        var overdriveLabel = selected is null
            ? activeOverdrives.Length > 1 ? $"{activeOverdrives.Length} PROTOCOLS ACTIVE"
                : activeOverdrives.Length == 1 ? $"{activeOverdrives[0].Protocol.DisplayName.ToUpperInvariant()} {activeOverdrives[0].OverdriveRemaining:0.0}s"
                : !session.ProtocolsEnabled ? "PROTOCOLS | MODE OFF"
                : session.OverdriveCooldownRemaining > 0 ? $"COOLDOWN | {session.OverdriveCooldownRemaining:0.0}s"
                : "PROTOCOL | READY"
            : !session.ProtocolsEnabled
            ? selected is { IsApex: true }
                ? selected.IsOverdriven
                    ? $"{selected.Protocol.DisplayName.ToUpperInvariant()} {selected.OverdriveRemaining:0.0}s"
                    : selectedApexCooldown > 0
                        ? $"APEX AUTO | {selectedApexCooldown:0.0}s"
                        : "APEX AUTO | READY"
                : "PROTOCOLS | MODE OFF"
            : selected is { IsApex: true } && selected.IsOverdriven
                ? $"{selected.Protocol.DisplayName.ToUpperInvariant()} {selected.OverdriveRemaining:0.0}s"
            : selected is { IsApex: true } && selectedApexCooldown > 0
                    ? $"APEX AUTO | {selectedApexCooldown:0.0}s"
                    : overdriveReady && selected is not null && activeOverdrives.Length > 0
                        ? $"{selected.Protocol.DisplayName.ToUpperInvariant()} | {activeOverdrives.Length} AUTO"
                    : activeOverdrives.Length > 1
                        ? $"{activeOverdrives.Length} PROTOCOLS ACTIVE"
                        : activeOverdrives.Length == 1
                            ? $"{activeOverdrives[0].Protocol.DisplayName.ToUpperInvariant()} {activeOverdrives[0].OverdriveRemaining:0.0}s"
                            : session.OverdriveCooldownRemaining > 0
                                ? $"COOLDOWN | {session.OverdriveCooldownRemaining:0.0}s"
                                : selected is null ? "PROTOCOL | SELECT" : selected.Protocol.DisplayName.ToUpperInvariant();
        var showingActive = !overdriveReady && activeOverdrives.Length > 0 &&
            (selected is null || selected.IsOverdriven || selectedApexCooldown <= 0 && session.ProtocolsEnabled);
        DrawButton(batch, p, _overdriveButton, overdriveLabel, overdriveReady, ColorPalette.Coral,
            hotkey: "E", statusActive: showingActive);
        var timedActiveTower = selected is { IsOverdriven: true } ? selected :
            activeOverdrives.Length == 1 ? activeOverdrives[0] : null;
        if (showingActive && timedActiveTower is not null)
        {
            var remaining = Math.Clamp(timedActiveTower.OverdriveRemaining /
                MathF.Max(.01f, timedActiveTower.Protocol.DurationSeconds), 0, 1);
            p.FillRect(batch, new Rectangle(_overdriveButton.X + 4, _overdriveButton.Bottom - 3,
                (int)MathF.Round((_overdriveButton.Width - 8) * remaining), 2), ColorPalette.Coral);
        }
        var armedAutoTower = session.Towers.FirstOrDefault(tower => tower.Id == session.AutoOverdriveTowerId);
        var autoActive = session.ProtocolsEnabled && selected is not null && armedAutoTower == selected;
        var autoLabel = selected is { IsApex: true } ? "AUTO SELF" : !session.ProtocolsEnabled ? "AUTO OFF" :
            autoActive ? "AUTO ON" : armedAutoTower is not null ? selected is null ? "ARMED" : "AUTO MOVE" : "AUTO OFF";
        var autoEnabled = session.ProtocolsEnabled && selected is not { IsApex: true } &&
            (selected is not null || armedAutoTower is not null);
        DrawButton(batch, p, _autoProtocolButton, autoLabel, autoEnabled,
            ColorPalette.Auto, hotkey: autoEnabled ? "A" : null);
    }

    private void DrawSidebar(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        CommandSurfaceArt.Surface(batch, p, new Rectangle(GameConstants.SidebarX, GameConstants.TopBarHeight, 320, GameConstants.LogicalHeight - GameConstants.TopBarHeight), ColorPalette.Cyan, 0, false);
        p.Line(batch, new Vector2(GameConstants.SidebarX, GameConstants.TopBarHeight), new Vector2(GameConstants.SidebarX, GameConstants.LogicalHeight), ColorPalette.Divider, 2);
        p.FillRect(batch, new Rectangle(972, 56, 296, 34), ColorPalette.Navy);
        if (session.IsCoOp && !session.IsSandbox)
        {
            DrawDisplayText(batch, "Defenses", CoOpTacticalTitleBounds.Center.ToVector2(), ColorPalette.Paper, .82f, true);
        }
        else
        {
            DrawDisplayText(batch, session.IsSandbox ? "Sandbox Lab" : _archivedLayoutInspection ? "Defense archive" : "Defenses",
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


        if (session.IsCoOpPaused)
        {
            DrawCoOpPauseSidebar(batch, p, session);
            return;
        }

        p.FillRect(batch, new Rectangle(980, 200, 280, 1), ColorPalette.Divider);
        DrawDisplayText(batch, "Towers", new Vector2(980, 207), ColorPalette.Paper, 0.78f);
        if (session.IsSandbox)
            DrawSandboxPlateCard(batch, p, session);

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
                : selected ? ColorPalette.Surface(NightGridArt.TowerAccent(definition.Id), .22f) : ColorPalette.PanelAlt;
            var cardOutline = _archivedLayoutInspection
                ? selected ? definition.Visual.PrimaryColor : ColorPalette.CardOutline
                : !available ? ColorPalette.Muted : selected ? definition.Visual.PrimaryColor : affordable ? ColorPalette.CardOutline : ColorPalette.Coral;
            p.FillRect(batch, rect, cardFill);
            if (selected || rect.Contains(_hudPointer.ToPoint())) p.DrawRect(batch, rect, cardOutline, selected ? 2 : 1);
            NightGridArt.Tower(batch, p, new Vector2(rect.X + 17, rect.Center.Y), 10, definition.Id, time: _settings.ReducedEffects ? 0 : _visualTimeSeconds);
            var placedCount = _archivedLayoutInspection
                ? session.Towers.Count(tower => tower.Definition.Id == definition.Id)
                : 0;
            if (_archivedLayoutInspection)
            {
                DrawText(batch, placedCount.ToString(),
                    new Vector2(rect.Right - 14, rect.Y + 7), selected
                        ? definition.Visual.AccentColor
                        : ColorPalette.Muted, 0.39f, true);
            }
            else if (_settings.ShowHotkeyBadges)
                DrawHotkeyBadge(batch, p, rect, index == 9 ? "0" : (index + 1).ToString(), available);
            DrawFittedText(batch, definition.DisplayName, new Vector2(rect.X + 38, rect.Y + 5), ColorPalette.Ink, 0.53f, 80);
            var cardSubtitle = _archivedLayoutInspection
                ? placedCount > 0 ? $"{placedCount} PLACED  {TowerInfo.ShortRole(definition)}" : TowerInfo.ShortRole(definition)
                : available ? $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}" : "MODE LOCKED";
            DrawFittedText(batch, cardSubtitle, new Vector2(rect.X + 38, rect.Y + 21),
                _archivedLayoutInspection ? placedCount > 0
                    ? definition.Visual.AccentColor
                    : ColorPalette.Muted
                    : available ? affordable ? ColorPalette.Muted : ColorPalette.Coral : ColorPalette.Muted,
                0.44f, 92);
        }

        DrawTowerIntel(batch, p, session);
    }

    private void DrawSandboxPlateCard(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        var rect = _emergencyButton = SandboxPlateBounds;
        var plateCount = session.EmergencyDefenses.Count;
        var maximumPlates = session.Content.Tactics.EmergencyDefense.MaximumActive;
        var available = session.PulsePlatesEnabled && plateCount < maximumPlates;
        var selected = session.TacticalPlacement == TacticalPlacementKind.PulsePlate;
        var accent = NightGridArt.TowerAccent("pulse_plate");
        p.FillRect(batch, rect, !available ? ColorPalette.Disabled : selected
            ? ColorPalette.Surface(accent, .22f) : ColorPalette.PanelAlt);
        if (selected || rect.Contains(_hudPointer.ToPoint()))
            p.DrawRect(batch, rect, !available ? ColorPalette.Muted : selected ? accent : ColorPalette.CardOutline,
                selected ? 2 : 1);
        NightGridArt.Tower(batch, p, new Vector2(rect.X + 17, rect.Y + rect.Height * .5f), 7, "pulse_plate",
            opacity: available ? 1 : .5f, lights: available && !_settings.ReducedEffects);
        DrawPlateLabel("Plates", 38, .48f, available ? ColorPalette.Ink : ColorPalette.Muted);
        DrawPlateLabel($"{plateCount}/{maximumPlates}", 90, .40f, ColorPalette.Muted);
        if (_settings.ShowHotkeyBadges)
            DrawHotkeyBadge(batch, p, rect, "Q", available);

        void DrawPlateLabel(string text, float offset, float scale, Color color)
        {
            var ink = TextInkExtents(_font, text);
            var y = rect.Y + rect.Height * .5f - (ink.X + ink.Y) * .5f * scale * GameConstants.FontDrawScale;
            DrawText(batch, text, new Vector2(rect.X + offset, y), color, scale);
        }
    }

    private void DrawCoOpPauseSidebar(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        _towerCards.Clear();
        var owner = session.CoOpPausePlayerId is 1 or 2 ? $"P{session.CoOpPausePlayerId}" : "PEER";
        var detail = $"{owner} paused the match";
        DrawHeading(batch, "Shared pause", new Rectangle(986, CoOpTacticalTitleBounds.Bottom, 268,
            CenteredTextTop(detail, 156, .55f) - CoOpTacticalTitleBounds.Bottom), 1.02f,
            "Sidebar header", "Pause owner");
        DrawText(batch, detail, new Vector2(1120, 156), ColorPalette.Muted, .55f, true);

        DrawButton(batch, p, CoOpPauseResumeBounds, "RESUME", true, ColorPalette.Gold, primary: true);
        DrawButton(batch, p, CoOpPauseRestartBounds, _restartArmed ? "CONFIRM RESTART" : "RESTART", true,
            ColorPalette.Coral);
        DrawButton(batch, p, CoOpPauseMenuBounds, "MAIN MENU", true, ColorPalette.Violet);

        var timing = _restartArmed
            ? "Restart the shared run?"
            : session.Waves.IsActive
                ? "Combat paused"
                : session.IntermissionRemaining > 0
                    ? $"Early call: {MathF.Ceiling(session.IntermissionRemaining):0}s remaining"
                    : "Between waves";
        DrawFittedCenteredText(batch, timing, new Vector2(1120, 408),
            _restartArmed ? ColorPalette.Coral : ColorPalette.Muted, 0.48f, 268);
        DrawFittedCenteredText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(1120, 444), ColorPalette.Muted, 0.42f, 268);
    }

    private void DrawSandboxBar(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
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

        _sandboxGroupButton = new Rectangle(972, 132, 70, 28);
        _sandboxRankButton = new Rectangle(1046, 132, 70, 28);
        _sandboxHealthButton = new Rectangle(1120, 132, 70, 28);
        _sandboxSignalButton = new Rectangle(1194, 132, 74, 28);
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
        var signalRole = SandboxSignals[_sandboxSignalIndex];
        var signalLabel = signalRole switch
        {
            EnemySignalRole.None => "NO SIGNAL",
            EnemySignalRole.Accelerator => "ACCEL",
            EnemySignalRole.Restorer => "RESTORE",
            EnemySignalRole.Bulwark => "SHIELD",
            EnemySignalRole.Jammer => "JAMMER",
            _ => "DISRUPT"
        };
        DrawSandboxButton(batch, p, _sandboxGroupButton, groupLabel, true, ColorPalette.Cobalt, "G");
        DrawSandboxButton(batch, p, _sandboxRankButton, rankLabel, true,
            SandboxRanks[_sandboxRankIndex] == EnemyRank.Boss ? ColorPalette.Coral : ColorPalette.Violet, "K");
        DrawSandboxButton(batch, p, _sandboxHealthButton, healthLabel, true,
            _sandboxHealthIndex == 3 ? ColorPalette.Gold : ColorPalette.Cyan, "H");
        DrawSandboxButton(batch, p, _sandboxSignalButton, signalLabel, true,
            EnemySignalGlyphRenderer.Accent(signalRole), "J");

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

    private void DrawTowerIntel(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
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
            DrawText(batch, "Select a tower", new Vector2(980, 490), ColorPalette.Muted, .65f);
            return;
        }

        var hasBranchChoice = tower.RequiresDoctrine || tower.RequiresSpecialization;
        var intelCard = new Rectangle(972, 474, 296, hasBranchChoice ? 172 : 196);
        p.FillRect(batch, intelCard, ColorPalette.PanelAlt);

        NightGridArt.Tower(batch, p, TowerIntelIconCenter, IntelIconRadius(tower.Definition.Visual.Radius),
            tower.Definition.Id, tower.LevelIndex + 1, time: _settings.ReducedEffects ? 0 : _visualTimeSeconds,
            lights: !_settings.ReducedEffects, apex: tower.IsApex);
        var ownership = session.IsCoOp ? $"   PLACED P{tower.OwnerPlayerId}" : "";
        DrawDisplayValue(batch, tower.Definition.DisplayName, new Vector2(1036, 486), ColorPalette.Paper, 0.86f,
            tower.IsApex ? 164 : 228);
        if (tower.IsApex)
        {
            const string apexLabel = "APEX";
            const float apexScale = 0.43f;
            var apexWidth = _font.MeasureString(apexLabel).X * apexScale * GameConstants.FontDrawScale;
            DrawText(batch, apexLabel, new Vector2(intelCard.Right - 15 - apexWidth, 487.5f), ColorPalette.Violet, apexScale);
        }
        var levelTitle = TowerInfo.ProgressionLabel(tower);
        // The subtitle reserves space for progression, ownership, and the
        // Sandbox Disable control, including at the final tier.
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
        DrawFittedText(batch, TowerLifetimeSummary(tower), new Vector2(980, 628), ColorPalette.Muted, 0.43f, 280);

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
                firstFill, firstText, "U", primary: true);
            DrawButton(batch, p, _specializationBButton, $"{secondLabel.ToUpperInvariant()} {secondCost}", canManage && session.Economy.CanAfford(secondCost),
                ColorPalette.Violet, session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Violet) : null, "I", primary: true);
            if (session.IsSandbox)
                DrawSandboxButton(batch, p, _sandboxRemoveTowerButton, "REMOVE", canManage, ColorPalette.Coral, "DEL");
            else
                DrawButton(batch, p, _sellButton, session.SellingEnabled ? $"SELL {tower.SellValue}" : "FIXED",
                    canManage && session.SellingEnabled, ColorPalette.Orange, hotkey: session.SellingEnabled ? "DEL" : null);
            DrawTargetPicker(batch, p, session, tower, canManage);
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
            session.IsSandbox ? ContrastAwareButtonTextColor(ColorPalette.Violet) : null, upgradeHotkey, primary: true);
        if (session.IsSandbox)
        {
            _sandboxRemoveTowerButton = _sellButton;
            DrawSandboxButton(batch, p, _sandboxRemoveTowerButton, "REMOVE", canManage, ColorPalette.Coral, "DEL");
        }
        else
            DrawButton(batch, p, _sellButton, session.SellingEnabled ? $"SELL {tower.SellValue}" : "FIXED",
                canManage && session.SellingEnabled, ColorPalette.Orange, hotkey: session.SellingEnabled ? "DEL" : null);
        if (!tower.IsSupport) DrawTargetPicker(batch, p, session, tower, canManage);
    }

    private string TargetButtonLabel(TowerInstance tower) =>
        tower.TargetMode.ToString().ToUpperInvariant();

    private void DrawTargetPicker(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session,
        TowerInstance tower, bool enabled)
    {
        if (!_targetPickerOpen || _targetPickerTowerId != tower.Id || tower.IsSupport || _targetButton.IsEmpty) return;

        var modes = session.AvailableTargetModes;
        const int columns = 4;
        const int buttonWidth = 68;
        const int buttonHeight = 25;
        const int gap = 3;
        var rows = (modes.Count + columns - 1) / columns;
        var contentWidth = columns * buttonWidth + (columns - 1) * gap;
        var contentHeight = rows * buttonHeight + (rows - 1) * gap;
        var contentX = 980;
        var contentY = _targetButton.Top - 4 - contentHeight;
        _targetPickerBounds = new Rectangle(contentX - 4, contentY - 4, contentWidth + 8, contentHeight + 8);
        p.FillRect(batch, _targetPickerBounds, ColorPalette.Panel);
        p.DrawRect(batch, _targetPickerBounds, ColorPalette.CardOutline);

        for (var index = 0; index < modes.Count; index++)
        {
            var mode = modes[index];
            var row = index / columns;
            var column = index % columns;
            var itemsInRow = Math.Min(columns, modes.Count - row * columns);
            var rowWidth = itemsInRow * buttonWidth + (itemsInRow - 1) * gap;
            var rowOffset = (contentWidth - rowWidth) / 2;
            var bounds = new Rectangle(contentX + rowOffset + column * (buttonWidth + gap),
                contentY + row * (buttonHeight + gap), buttonWidth, buttonHeight);
            _targetModeButtons[mode] = bounds;
            var fill = mode == tower.TargetMode ? ColorPalette.Gold : ColorPalette.Cyan;
            DrawButton(batch, p, bounds, mode.ToString().ToUpperInvariant(), enabled, fill,
                ContrastAwareButtonTextColor(fill), (index + 1).ToString());
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
    public static Color ContrastAwareButtonTextColor(Color fillColor) =>
        ColorPalette.ReadableAccent(ColorPalette.Paper, ColorPalette.Surface(fillColor, .24f));

    public static Color TowerIntelPrimaryUpgradeTextColor(TowerDefinition definition) =>
        ContrastAwareButtonTextColor(definition.Visual.PrimaryColor);
    public static Color SandboxEnemyButtonTextColor(EnemyDefinition definition) =>
        ContrastAwareButtonTextColor(definition.Visual.PrimaryColor);
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

    private void DrawDefinitionIntel(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session, TowerDefinition definition, bool placing)
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
        NightGridArt.Tower(batch, p, TowerIntelIconCenter, IntelIconRadius(definition.Visual.Radius), definition.Id);
        DrawDisplayValue(batch, definition.DisplayName, new Vector2(1036, 486), ColorPalette.Paper, .86f, 228);
        DrawFittedText(batch, $"{definition.PurchaseCost}  /  {TowerInfo.ShortRole(definition)}",
            new Vector2(1036, 510), ColorPalette.Muted, .57f, 228);
        DrawTowerStatGrid(batch, comparisonStats, 548);
        if (hasPlacementModifier)
            DrawFittedText(batch, $"{PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}",
                new Vector2(980, 697), nodeTextColor, .40f, 280);
    }

    private static Color PowerNodeIntelTextColor(Color nodeColor) =>
        ColorPalette.BalancedAccentText(nodeColor, ColorPalette.PanelAlt);

    private static string PowerNodeNames(IReadOnlyList<PowerNodeData> nodes) => nodes.Count == 1
        ? nodes[0].DisplayName.ToUpperInvariant()
        : $"{string.Join(" + ", nodes.Select(node => node.DisplayName.Replace(" Node", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant()))} NODES";

    private static int IntelIconRadius(int authoredRadius) => Math.Min(authoredRadius, TowerIntelIconRadiusCap);

    private void DrawEmergencyIntel(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        _targetButton = Rectangle.Empty;
        _upgradeButton = Rectangle.Empty;
        _sellButton = Rectangle.Empty;
        var definition = session.Content.Tactics.EmergencyDefense;
        p.FillRect(batch, new Rectangle(972, 474, 296, 202), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 202), definition.Visual.PrimaryColor, 1);
        NightGridArt.Tower(batch, p, TowerIntelIconCenter, definition.Visual.Radius + 2, definition.Id, definition.Charges);
        DrawText(batch, definition.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.86f);
        var availability = session.IsSandbox ? "UNLIMITED" : $"STORED {session.EmergencyInventory}";
        DrawText(batch, $"{availability}   FIELD {session.EmergencyDefenses.Count}/{definition.MaximumActive}", new Vector2(1028, 508), ColorPalette.Muted, 0.60f);
        var bonus = session.Generator?.Level.DefenseDamageBonus ?? 0;
        DrawFittedText(batch, $"{definition.Charges} PULSES   DAMAGE {definition.Damage * (1 + bonus):0.#}   BLAST {definition.BlastRadius:0}",
            new Vector2(980, 542), ColorPalette.Ink, 0.59f, 280);
        DrawText(batch, $"PUSH {definition.KnockbackDistance:0}   SLOW {definition.SlowPercent:P0} / {definition.SlowDuration:0.#}s", new Vector2(980, 565), ColorPalette.Ink, 0.55f);
        var directIntel = session.IsSandbox ? "Free deployment. Reset Test clears plates."
            : session.Waves.IsActive
            ? $"BUY {session.CurrentEmergencyDirectPurchaseCost}  /  +{definition.DirectPurchaseCostIncrease} EACH THIS WAVE"
            : "Buy during waves. Forge replenishes charges.";
        DrawFittedText(batch, directIntel, new Vector2(980, 604), session.Waves.IsActive ? ColorPalette.Gold : ColorPalette.Green, 0.49f, 280);
        DrawFittedText(batch, session.TacticalPlacement == TacticalPlacementKind.PulsePlate ? "Click route to place. Esc cancels." : "Select Plates to place.",
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
        DrawFittedText(batch, "Place a tower inside the ring.", new Vector2(980, 606), ColorPalette.Muted, .55f, 280);
        DrawFittedText(batch, "Node bonuses do not stack.", new Vector2(980, 652),
            PowerNodeIntelTextColor(ColorPalette.Gold), 0.49f, 280);
    }

    private void DrawGeneratorIntel(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session, ChargeForgeInstance? active)
    {
        _targetButton = Rectangle.Empty;
        var definition = session.Content.Tactics.Generator;
        var level = active?.Level ?? definition.Levels[0];
        p.FillRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), definition.Visual.PrimaryColor, 1);
        NightGridArt.Tower(batch, p, TowerIntelIconCenter, IntelIconRadius(definition.Visual.Radius), definition.Id,
            (active?.LevelIndex ?? 0) + 1);
        DrawFittedText(batch, definition.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.86f, 236);
        var generatorOwner = active is not null && session.IsCoOp ? $"   PLACED P{active.OwnerPlayerId}" : "";
        DrawFittedText(batch, active is null ? $"{definition.PurchaseCost} CREDITS   GENERATOR" : $"LEVEL {active.LevelIndex + 1}   GENERATOR{generatorOwner}",
            new Vector2(1028, 508), ColorPalette.Muted, 0.60f, 236);
        var productionState = active is null
            ? $"1 PLATE / {level.ProductionSeconds:0}s"
            : session.Waves.IsActive
                ? $"NEXT PLATE  {active.ProductionRemaining:0}s"
                : $"PAUSED  /  {active.ProductionRemaining:0}s LEFT";
        DrawFittedText(batch, productionState, new Vector2(980, 548), ColorPalette.Ink, 0.56f, 280);
        DrawFittedText(batch, $"Storage {session.EmergencyInventory}/{level.Capacity}   Plate DAMAGE +{level.DefenseDamageBonus:P0}",
            new Vector2(980, 571), ColorPalette.Ink, 0.57f, 280);
        DrawFittedText(batch, "Produces during waves.",
            new Vector2(980, 594), ColorPalette.Muted, 0.48f, 280);

        if (active is null)
        {
            _upgradeButton = Rectangle.Empty;
            _sellButton = Rectangle.Empty;
            var next = definition.Levels[1];
            DrawFittedText(batch, $"L2 {level.UpgradeCost}: {next.ProductionSeconds:0}s   CAP {next.Capacity}   DAMAGE +{next.DefenseDamageBonus:P0}",
                new Vector2(980, 624), ColorPalette.Violet, 0.52f, 280);
            DrawFittedText(batch, session.TacticalPlacement == TacticalPlacementKind.ChargeForge ? "Click a build area. Esc cancels." : "Select Forge to place.",
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

    private void DrawPlacementStatus(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        var pointerOnMap = IsPointerOnMap(session.PlacementPosition);
        var valid = pointerOnMap && session.HasPlacementPreview && session.PlacementFailure == PlacementFailure.None;
        var color = valid ? ColorPalette.Green : ColorPalette.Coral;
        var rect = new Rectangle(292, 64, 376, 28);
        p.FillRect(batch, rect, ColorPalette.WithAlpha(ColorPalette.Navy, 232));
        p.DrawRect(batch, rect, color, 2);
        var validMessage = session.TacticalPlacement switch
        {
            TacticalPlacementKind.PulsePlate when session.IsSandbox => "VALID - DEPLOY PLATE",
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

    private static string PlacementMessage(MaximalBastion.GameSession session, PlacementFailure failure) => failure switch
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

    private void DrawMainMenu(SpriteBatch batch, PrimitiveRenderer p, Matrix transform)
    {
        var time = _settings.ReducedEffects ? 0 : _visualTimeSeconds;
        DefenseTerminalArt.Background(batch, p, time);
        _mainMenuBattleScene?.Draw(batch, p, transform, _settings.ReducedEffects);
        foreach (var feedX in new[] { 20, 980 })
        {
            p.FillRect(batch, new Rectangle(feedX + 1, 25, 278, 35), ColorPalette.Panel);
            p.FillRect(batch, new Rectangle(feedX + 1, 668, 278, 29), ColorPalette.Panel);
            p.Line(batch, new Vector2(feedX + 12, 60), new Vector2(feedX + 268, 60), ColorPalette.Divider);
            p.Line(batch, new Vector2(feedX + 12, 668), new Vector2(feedX + 268, 668), ColorPalette.Divider);
        }
        var console = new Rectangle(325, 24, 630, 680);
        DrawMenuPanel(batch, p, console, ColorPalette.Cyan);
        CommandSurfaceArt.Infrastructure(batch, p, console, ColorPalette.Cyan, time);
        DrawWordmark(batch, p, new Vector2(640, 167.5f), time);
        LastMenuHeading = new MenuHeadingLayout("Maximal Bastion", new Rectangle(430, 26, 420, 254),
            new Rectangle(430, 40, 420, 230), "Console frame", "Wires", 0, IsArtwork: true);
        foreach (var item in MainMenuActions())
            DrawButton(batch, p, MainMenuActionBounds(item.Action), item.Label, item.Action != UiAction.LoadGame || _saveAvailable,
                MainMenuActionAccent(item.Action),
                primary: item.Action == UiAction.OpenSoloSetup);

        DrawButton(batch, p, _mainMenuReleaseNotesButton, $"v{BuildInfo.Version}  |  WHAT'S NEW",
            true, ColorPalette.Cobalt);

    }

    private void DrawReleaseNotes(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawHeading(batch, "What's new", PageHeadingBand());

        var release = _releaseNotes.Latest;
        var panel = new Rectangle(300, 205, 680, 220);
        p.FillRect(batch, panel, ColorPalette.PanelAlt);
        p.DrawRect(batch, panel, ColorPalette.CardOutline, 1);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 5), ColorPalette.Cobalt);

        var version = release?.Version ?? BuildInfo.Version;
        DrawText(batch, $"VERSION {version}", new Vector2(panel.X + 28, panel.Y + 30),
            ColorPalette.Cobalt, 0.76f);
        p.Line(batch, new Vector2(panel.X + 28, panel.Y + 72),
            new Vector2(panel.Right - 28, panel.Y + 72), ColorPalette.Divider, 1);

        var changes = release?.Changes ?? [];
        for (var index = 0; index < changes.Count; index++)
        {
            var y = panel.Y + 100 + index * 38;
            p.FillRect(batch, new Rectangle(panel.X + 30, y + 6, 7, 7), ColorPalette.Gold);
            DrawFittedText(batch, changes[index], new Vector2(panel.X + 52, y),
                ColorPalette.Ink, 0.66f, panel.Width - 82);
        }

        DrawButton(batch, p, _releaseNotesBackButton, "BACK", true, ColorPalette.Violet);
    }

    private void DrawGameSetup(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawHeading(batch, _setupForCoOp ? "Host Game" : "New Game",
            PageHeadingBand(SetupCardRectangle(0, 0, _maps.Count).Top - 1), lowerBoundary: "Map cards");
        var time = _settings.ReducedEffects ? 0 : _visualTimeSeconds;
        for (var index = 0; index < _maps.Count; index++)
        {
            var map = _maps[index];
            var card = SetupCardRectangle(0, index, _maps.Count);
            var selected = index == _selectedMapIndex;
            var hovered = card.Contains(_hudPointer.ToPoint());
            var accent = ColorPalette.Environment(map.PathStyle).Accent;
            DefenseTerminalArt.District(batch, p, card, map.PathStyle, map.Path, time, hovered || selected);
            DrawFittedCenteredText(batch, map.Name.ToUpperInvariant(),
                new Vector2(card.Center.X, card.Bottom - 22), ColorPalette.Paper, .65f, card.Width - 36);
            if (selected)
            {
                p.DrawRect(batch, card, accent, 2);
                p.FillRect(batch, new Rectangle(card.X + 12, card.Y + 12, 22, 22), ColorPalette.Panel);
                p.Line(batch, new Vector2(card.X + 17, card.Y + 23), new Vector2(card.X + 21, card.Y + 27), accent, 2);
                p.Line(batch, new Vector2(card.X + 21, card.Y + 27), new Vector2(card.X + 29, card.Y + 18), accent, 2);
            }
        }
        DrawSetupSectionHeader(batch, "DIFFICULTY", 1, _difficulties.Count);
        for (var index = 0; index < _difficulties.Count; index++)
            DrawSetupChoice(batch, p, SetupCardRectangle(1, index, _difficulties.Count), _difficulties[index].DisplayName,
                ColorPalette.Cyan, index == _selectedDifficultyIndex);

        var setupChallenges = SetupChallenges();
        DrawSetupSectionHeader(batch, "MODE", 2, setupChallenges.Count);
        for (var index = 0; index < setupChallenges.Count; index++)
        {
            var challengeIndex = _challenges.IndexOf(setupChallenges[index]);
            DrawSetupChoice(batch, p, SetupCardRectangle(2, index, setupChallenges.Count), setupChallenges[index].MenuLabel,
                ColorPalette.Cyan, challengeIndex == _selectedChallengeIndex);
        }
        var modeDescription = HoveredModeDescription;
        if (modeDescription.Length > 0)
            DrawSetupDescription(batch, modeDescription, 2, setupChallenges.Count);
        for (var index = 0; index < _difficulties.Count; index++)
            if (SetupCardRectangle(1, index, _difficulties.Count).Contains(_hudPointer.ToPoint()))
                DrawSetupDescription(batch, _difficulties[index].Description, 1, _difficulties.Count);

        DrawButton(batch, p, _setupConfirmButton, _setupForCoOp ? "HOST" : "START", true, ColorPalette.Gold, primary: true);
        DrawButton(batch, p, _setupBackButton, "BACK", true, ColorPalette.CardOutline);
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
        DrawMenuPanel(batch, p, panel, ColorPalette.Cyan);
        DrawHeading(batch, "Loading", new Rectangle(panel.X + 24, panel.Top + 2, panel.Width - 48,
            390 - panel.Top - 2), lowerBoundary: "Activity indicators");

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
    }

    private static Rectangle SetupChoiceRowBounds(int row, int count) =>
        Rectangle.Union(SetupCardRectangle(row, 0, count), SetupCardRectangle(row, Math.Max(0, count - 1), count));

    private void DrawSetupSectionHeader(SpriteBatch batch, string label, int row, int count)
    {
        var bounds = SetupChoiceRowBounds(row, count);
        DrawText(batch, label, new Vector2(bounds.Center.X, bounds.Top - 20), ColorPalette.Muted, .57f, true);
    }

    private void DrawSetupDescription(SpriteBatch batch, string text, int row, int count)
    {
        var bounds = SetupChoiceRowBounds(row, count);
        DrawFittedCenteredText(batch, text, new Vector2(bounds.Center.X, bounds.Bottom + 16),
            ColorPalette.Muted, .46f, bounds.Width - 44);
    }

    private void DrawSetupChoice(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string label, Color accent, bool selected, string? mapStyle = null)
    {
        DrawSetupCardFrame(batch, p, rect, accent, selected);
        if (mapStyle is not null)
            MapEnvironmentRenderer.Emblem(batch, p, new Vector2(rect.X + 24, rect.Center.Y), mapStyle, 10);
        DrawFittedCenteredText(batch, label.ToUpperInvariant(), new Vector2(rect.Center.X + (mapStyle is null ? 0 : 14), rect.Center.Y),
            selected ? ColorPalette.ReadableAccent(accent, ColorPalette.Panel) : ColorPalette.Navy, 0.60f, rect.Width - (mapStyle is null ? 12 : 68));
    }

    private void DrawSetupCardFrame(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, Color accent, bool selected)
    {
        var hovered = rect.Contains(_hudPointer.ToPoint());
        p.FillRect(batch, rect, selected ? ColorPalette.Surface(accent, .22f) : ColorPalette.Panel);
        p.DrawRect(batch, rect, selected || hovered ? accent : ColorPalette.CardOutline);
        if (selected) p.FillRect(batch, new Rectangle(rect.X + 6, rect.Bottom - 3, rect.Width - 12, 2), accent);
        if (hovered) p.Brackets(batch, rect, accent, 6);
    }

    private void DrawVolumeSlider(SpriteBatch batch, PrimitiveRenderer p, Rectangle bounds, string label, float volume,
        Color accent)
    {
        p.FillRect(batch, bounds, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(bounds.X, bounds.Y, 6, bounds.Height), accent);
        p.DrawRect(batch, bounds, accent, _settingsSelection is 3 or 4 && SettingsOptionRectangle(_settingsSelection) == bounds ? 2 : 1);

        DrawFittedText(batch, label, new Vector2(bounds.X + 18, bounds.Center.Y - 9), ColorPalette.Ink, 0.52f, 174);
        DrawTextRight(batch, $"{MathF.Round(volume * 100):0}%", new Vector2(bounds.Right - 18, bounds.Center.Y - 9),
            ColorPalette.Ink, 0.52f);

        var track = VolumeSliderTrack(bounds);
        p.FillRect(batch, track, ColorPalette.CardOutline);
        var clamped = MathHelper.Clamp(volume, 0, 1);
        var filledWidth = (int)MathF.Round(track.Width * clamped);
        if (filledWidth > 0) p.FillRect(batch, new Rectangle(track.X, track.Y, filledWidth, track.Height), accent);
        var knobX = (int)MathF.Round(track.X + track.Width * clamped);
        var knob = new Rectangle(knobX - 5, track.Center.Y - 12, 10, 24);
        p.FillRect(batch, knob, accent);
        p.DrawRect(batch, knob, ColorPalette.Ink, 1);
    }

    private void DrawSaveSlots(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p, ColorPalette.Cobalt);
        DrawHeading(batch, _saveSlotWriteMode ? "Save" : "Load", PageHeadingBand());
        DrawButton(batch, p, _saveSlotHistoryButton, "HISTORY", true, ColorPalette.Cyan);

        var pageCount = Math.Max(1, (_saveSlots.Count + _saveSlotRows.Length - 1) / _saveSlotRows.Length);
        var pageSlots = _saveSlots.Skip(_saveSlotPage * _saveSlotRows.Length).Take(_saveSlotRows.Length).ToArray();
        if (pageSlots.Length == 0 && !_saveSlotWriteMode)
        {
            DrawText(batch, "No saved runs", new Vector2(640, 280), ColorPalette.Muted, .8f, true);
            DrawButton(batch, p, _saveSlotBackButton, "BACK", true, ColorPalette.CardOutline);
            return;
        }
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
            var progress = slot.IsEndless && slot.CurrentWave > GameConstants.CampaignWaveCount
                ? $"ENDLESS {slot.CurrentWave}"
                : $"WAVE {slot.CurrentWave}/{GameConstants.CampaignWaveCount}";
            var difficultyName = _difficulties.FirstOrDefault(x => x.Id.Equals(slot.DifficultyId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.DifficultyId) ? "Hard" : slot.DifficultyId);
            var challengeName = _challenges.FirstOrDefault(x => x.Id.Equals(slot.ChallengeId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.ChallengeId) ? "Standard" : slot.ChallengeId.Replace('_', ' '));
            DrawFittedText(batch, $"{mapName.ToUpperInvariant()}  /  {difficultyName.ToUpperInvariant()}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.58f, rect.Width - 164);
            var localTime = slot.SavedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(slot.SavedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : slot.SavedAtUtc.ToLocalTime();
            DrawFittedText(batch, $"{progress}  /  {localTime:g}{(slot.IsCoOp ? "  /  CO-OP" : "")}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.48f, rect.Width - 164);
        }

        var selectedSlot = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
        var canConfirm = _saveSlotWriteMode || selectedSlot is { IsOccupied: true, Error: null };
        var selectedLabel = SaveSlotLabel(_selectedSaveSlot);
        var confirmLabel = _saveSlotWriteMode
            ? selectedSlot is { IsOccupied: true } ? $"OVERWRITE SLOT {_selectedSaveSlot}" : $"SAVE TO SLOT {_selectedSaveSlot}"
            : "LOAD";
        var confirmButton = _saveSlotWriteMode ? _saveSlotWriteConfirmButton : _saveSlotConfirmButton;
        var deleteButton = _saveSlotWriteMode ? _saveSlotWriteDeleteButton : _saveSlotDeleteButton;
        DrawButton(batch, p, confirmButton, confirmLabel, canConfirm,
            _saveSlotWriteMode && selectedSlot is { IsOccupied: true } ? ColorPalette.Orange : ColorPalette.Cyan, primary: true);
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
        DrawButton(batch, p, _saveSlotPreviousButton, "<", _saveSlotPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, ">", _saveSlotPage + 1 < pageCount, ColorPalette.Cyan);
        DrawMenuStatus(batch, _persistenceStatus, _saveSlotDeleteArmed ? ColorPalette.Coral : ColorPalette.Muted);
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
        DrawMenuFrame(batch, p, ColorPalette.Gold);
        DrawHeading(batch, "History", PageHeadingBand());
        DrawButton(batch, p, _runHistoryCareerButton, "ACHIEVEMENTS", true, ColorPalette.Cyan);

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

            var progress = entry.IsEndless && entry.CurrentWave > GameConstants.CampaignWaveCount
                ? $"ENDLESS {entry.CurrentWave}"
                : $"WAVE {entry.CurrentWave}/{RecordedCampaignTotal(entry)}";
            DrawFittedText(batch, $"{entry.MapName.ToUpperInvariant()}  /  {entry.DifficultyName.ToUpperInvariant()}  /  {progress}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.56f, rect.Width - 164);
            var localTime = entry.CompletedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : entry.CompletedAtUtc.ToLocalTime();
            DrawFittedText(batch,
                $"{localTime:g}{(entry.IsCoOp ? "  /  CO-OP" : "")}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.44f, rect.Width - 164);
        }

        DrawButton(batch, p, _runHistoryViewButton, "DETAILS",
            _selectedRunHistoryId is not null, ColorPalette.Cobalt);
        DrawButton(batch, p, _runHistoryDeleteButton,
            _runHistoryDeleteArmed ? "CONFIRM DELETE" : "DELETE",
            _selectedRunHistoryId is not null, _runHistoryDeleteArmed ? ColorPalette.Coral : ColorPalette.Orange);
        DrawText(batch, $"PAGE {_runHistoryPage + 1}/{pageCount}", new Vector2(640, 574), ColorPalette.Muted, 0.48f, true);
        DrawButton(batch, p, _saveSlotPreviousButton, "<", _runHistoryPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, ">", _runHistoryPage + 1 < pageCount, ColorPalette.Cyan);
        DrawMenuStatus(batch, _runHistoryStatus, ColorPalette.Muted);
    }

    private static string CareerRunLabel(RunHistoryEntry? entry, Func<RunHistoryEntry, string> value) => entry is null
        ? "NO QUALIFYING RUN"
        : $"{value(entry)}  |  {entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}";

    private static int RecordedCampaignTotal(RunHistoryEntry entry)
    {
        if (entry.IsEndless && entry.CurrentWave > entry.TotalWaves)
            return GameConstants.CampaignWaveCount;
        return entry.TotalWaves > 0 ? entry.TotalWaves : GameConstants.CampaignWaveCount;
    }

    private void DrawRunHistoryDetail(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p, ColorPalette.Gold);
        var entry = _runHistory.FirstOrDefault(candidate => candidate.RunId == _selectedRunHistoryId);
        if (entry is null)
        {
            _runHistoryDetailOpen = false;
            return;
        }

        var localTime = entry.CompletedAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc).ToLocalTime()
            : entry.CompletedAtUtc.ToLocalTime();
        var identity = $"{entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}  |  {entry.ChallengeName.ToUpperInvariant()}  |  {(entry.IsCoOp ? "CO-OP" : "SOLO")}  |  {localTime:g}";
        DrawHeading(batch, "Run details", PageHeadingBand(CenteredTextTop(identity, 76, .46f, 1120)), 1.34f,
            lowerBoundary: "Run identity");
        DrawFittedCenteredText(batch,
            identity,
            new Vector2(640, 76), ColorPalette.Muted, 0.46f, 1120);

        const int cardY = 94;
        DrawResultStatCard(batch, p, new Rectangle(50, cardY, 224, 58), "RESULT", entry.Victory ? "SECURED" : "BREACHED",
            entry.Victory ? ColorPalette.Green : ColorPalette.Coral);
        var entryInEndless = entry.IsEndless && entry.CurrentWave > GameConstants.CampaignWaveCount;
        DrawResultStatCard(batch, p, new Rectangle(289, cardY, 224, 58), entryInEndless ? "ENDLESS" : "WAVE",
            entryInEndless ? entry.CurrentWave.ToString() : $"{entry.CurrentWave}/{RecordedCampaignTotal(entry)}", ColorPalette.Cyan);
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
            DrawFittedText(batch, CurrentTowerDisplayName(tower.TowerId, tower.DisplayName).ToUpperInvariant(),
                new Vector2(56, y), ColorPalette.Ink, 0.43f, 116);
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
                ? "UNAVAILABLE"
                : $"{finalTowerCount} {(finalTowerCount == 1 ? "TOWER" : "TOWERS")}  |  {apexTowerCount} APEX",
            new Vector2(940, 333), finalTowerCount is null ? ColorPalette.Muted : ColorPalette.Cobalt, 0.42f, 284);

        DrawText(batch, "DEFENSES", new Vector2(834, 358), ColorPalette.Navy, 0.58f);
        p.FillRect(batch, new Rectangle(834, 380, 390, 2), ColorPalette.Violet);
        DrawSummaryMetric(batch, "PROTOCOLS", entry.ProtocolActivations.ToString(), 834, 393, ColorPalette.Violet);
        DrawSummaryMetric(batch, "DIRECT PLATES", entry.PlateDirectPurchases.ToString(), 1038, 393, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATES / TRIGGERS", $"{entry.PlateDeployments} / {entry.PlateTriggers}", 834, 433, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATE HITS / KILLS", $"{entry.PlateHits} / {entry.PlateKills}", 1038, 433, ColorPalette.Coral);
        DrawSummaryMetric(batch, "PLATE DAMAGE", entry.PlateDamage.ToString("0"), 834, 473, ColorPalette.Coral);
        DrawSummaryMetric(batch, "FORGED CHARGES", entry.ForgedCharges.ToString(), 1038, 473, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "FORGES BUILT", entry.ForgePurchases.ToString(), 834, 513, ColorPalette.GreenText);
        DrawSummaryMetric(batch, "FORGE UPGRADES", entry.ForgeUpgrades.ToString(), 1038, 513, ColorPalette.GreenText);

        DrawText(batch, entry.Victory ? "FIELD AT FINISH" : "FIELD AT DEFEAT",
            new Vector2(834, 568), ColorPalette.Muted, 0.40f);
        var fieldHeadline = RecordedFieldHeadline(entry);
        var fieldColor = entry.Victory ? ColorPalette.GreenText : entry.DefeatFieldRecorded ? ColorPalette.Coral : ColorPalette.Muted;
        DrawFittedText(batch, fieldHeadline, new Vector2(834, 588), fieldColor, 0.52f, 390);
        DrawFittedText(batch, RecordedFieldDetails(entry), new Vector2(834, 612), ColorPalette.Muted, 0.36f, 390);

        DrawButton(batch, p, _runHistoryLayoutButton,
            entry.FinalLayout is null ? "LAYOUT UNAVAILABLE" : "VIEW FINAL LAYOUT",
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

    private static string RecordedFieldHeadline(RunHistoryEntry entry)
    {
        if (entry.Victory) return "CLEAR";
        if (!entry.DefeatFieldRecorded) return "NOT RECORDED";
        var count = entry.RemainingEnemies.Sum(enemy => enemy.Count);
        return entry.QueuedEnemiesRemaining > 0
            ? $"{count} ON FIELD  |  {entry.QueuedEnemiesRemaining} QUEUED"
            : $"{count} ON FIELD";
    }

    private static string RecordedFieldDetails(RunHistoryEntry entry)
    {
        if (entry.Victory) return "NO ENEMIES REMAINED";
        if (!entry.DefeatFieldRecorded) return "NEW RUNS SAVE THE FULL REMAINING FIELD";
        if (entry.RemainingEnemies.Count == 0) return entry.QueuedEnemiesRemaining > 0
            ? "NO ENEMIES ON FIELD; LATER GROUPS WERE STILL QUEUED"
            : "NO ENEMIES REMAINED ON FIELD";
        return string.Join("  |  ", entry.RemainingEnemies.Take(2).Select(RecordedRemainingEnemyLabel));
    }

    private static string RecordedRemainingEnemyLabel(RunHistoryRemainingEnemyEntry enemy)
    {
        var healthPercent = enemy.TotalMaxHealth <= 0
            ? 0
            : Math.Clamp(enemy.TotalHealth / enemy.TotalMaxHealth, 0, 1);
        var shield = enemy.TotalShield > 0 ? $" +{enemy.TotalShield:0} SHIELD" : "";
        var signal = enemy.SignalRole.Equals(nameof(EnemySignalRole.None), StringComparison.OrdinalIgnoreCase)
            ? ""
            : $" {enemy.SignalRole.ToUpperInvariant()}";
        return $"{enemy.Count} {enemy.DisplayName.ToUpperInvariant()} {healthPercent:P0} HP{shield}{signal}";
    }

    private void DrawCoOpMenu(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawHeading(batch, "Co-op", PageHeadingBand());

        DrawText(batch, "HOST A GAME", new Vector2(640, 198), ColorPalette.Cobalt, 0.52f, true);
        DrawButton(batch, p, _hostCoOpButton, "HOST", true, ColorPalette.Cyan, primary: true);

        DrawText(batch, "JOIN A FRIEND", new Vector2(640, 294), ColorPalette.GreenText, 0.52f, true);

        DrawText(batch, "HOST ADDRESS", new Vector2(500, 307), _editingJoinCode ? ColorPalette.Muted : ColorPalette.Cobalt, 0.48f);
        p.FillRect(batch, _joinHostField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinHostField, !_editingJoinCode ? ColorPalette.Cobalt : ColorPalette.CardOutline, 2);
        var hostText = string.IsNullOrWhiteSpace(_joinHostInput) ? "203.0.113.10  or  friend.example" : _joinHostInput;
        DrawText(batch, hostText, new Vector2(640, _joinHostField.Center.Y), string.IsNullOrWhiteSpace(_joinHostInput) ? ColorPalette.Muted : ColorPalette.Ink, 0.66f, true);
        DrawText(batch, "+ TCP 28741 WHEN OMITTED", new Vector2(798, _joinHostField.Y + 12), ColorPalette.Muted, 0.44f);

        DrawText(batch, "JOIN CODE", new Vector2(500, 375), _editingJoinCode ? ColorPalette.Cobalt : ColorPalette.Muted, 0.48f);
        p.FillRect(batch, _joinCodeField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinCodeField, _editingJoinCode ? ColorPalette.Cobalt : _joinCodeInput.Length == 6 ? ColorPalette.Green : ColorPalette.CardOutline, 2);
        DrawText(batch, _joinCodeInput.PadRight(6, '_'), new Vector2(640, _joinCodeField.Center.Y), ColorPalette.Ink, 0.86f, true);

        DrawButton(batch, p, _joinCoOpButton, "JOIN", CanJoinOnline, ColorPalette.Cyan, primary: CanJoinOnline);
        DrawButton(batch, p, _backButton, "BACK", true, ColorPalette.Violet);
        var focus = CoOpMenuActionRectangle(Math.Clamp(_coOpMenuSelection, 0, 2));
        focus.Inflate(3, 3);
        p.DrawRect(batch, focus, ColorPalette.Ink, 2);
        DrawText(batch, "Two players. Shared defenses.", new Vector2(640, 590), ColorPalette.Muted, 0.56f, true);
        DrawFittedCenteredText(batch, "Tab: switch fields   /   Ctrl+V: paste",
            new Vector2(640, 613), ColorPalette.Muted, 0.46f, 900);
        DrawFittedCenteredText(batch, "Internet hosting requires TCP port 28741 forwarding.",
            new Vector2(640, 672), ColorPalette.Gold, 0.46f, 900);
    }

    private void DrawCoOpLobby(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawMenuPanel(batch, p, new Rectangle(390, 150, 500, 330), ColorPalette.Cyan);
        var headingBottom = string.IsNullOrEmpty(CoOpLobbyCode)
            ? CenteredTextTop(CoOpLobbyDetail, 392, .62f, 440)
            : CenteredTextTop("JOIN CODE", 260, .62f);
        DrawHeading(batch, CoOpLobbyTitle, new Rectangle(414, 152, 452, headingBottom - 152), 1.25f,
            lowerBoundary: string.IsNullOrEmpty(CoOpLobbyCode) ? "Connection detail" : "Join code label");
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
        DrawMenuPanel(batch, p, panel, _coOpPeerConnected ? ColorPalette.Cyan : ColorPalette.Coral);
        // The compact status line's first raster row sits inside its glyph crop.
        var detailTop = CenteredTextTop(CoOpLobbyDetail, 320, .60f, panel.Width - 48) + 1;
        DrawHeading(batch, CoOpLobbyTitle, new Rectangle(panel.X + 24, panel.Top + 2, panel.Width - 48,
            detailTop - panel.Top - 2), 1.15f,
            lowerBoundary: "Connection detail");
        DrawFittedCenteredText(batch, CoOpLobbyDetail, new Vector2(640, 320), ColorPalette.Muted, .60f, panel.Width - 48);
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

    private void DrawMenuPanel(SpriteBatch batch, PrimitiveRenderer p, Rectangle panel, Color accent)
    {
        p.FillRect(batch, new Rectangle(panel.X + 4, panel.Y + 5, panel.Width, panel.Height), ColorPalette.Canvas);
        CommandSurfaceArt.Surface(batch, p, panel, accent, _settings.ReducedEffects ? 0 : _visualTimeSeconds);
        p.DrawRect(batch, panel, ColorPalette.CardOutline);
        p.Brackets(batch, panel, accent * .6f, 16);
        p.Line(batch, new Vector2(panel.X + 18, panel.Y + 1), new Vector2(panel.Right - 18, panel.Y + 1), ColorPalette.Circuit);
    }

    private void DrawMenuFrame(SpriteBatch batch, PrimitiveRenderer p, Color? mood = null)
    {
        DefenseTerminalArt.Background(batch, p, _settings.ReducedEffects ? 0 : _visualTimeSeconds, false);
        var frame = MenuFrameBounds;
        DrawMenuPanel(batch, p, frame, mood ?? ColorPalette.Cyan);
        CommandSurfaceArt.Infrastructure(batch, p, frame, mood ?? ColorPalette.Cyan, _settings.ReducedEffects ? 0 : _visualTimeSeconds);
    }

    private void DrawResultOverlay(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session, bool victory)
    {
        var accent = victory ? ColorPalette.Green : ColorPalette.Coral;
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 226));
        DrawMenuPanel(batch, p, new Rectangle(260, 64, 760, 584), accent);

        DrawHeading(batch, victory ? "Victory" : "Defeat", new Rectangle(458, 66, 364, 106), 1.55f,
            lowerBoundary: "Run statistics");
        DrawResultStatCard(batch, p, new Rectangle(296, 172, 158, 58), session.IsEndlessMode ? "ENDLESS" : "WAVE",
            session.IsEndlessMode ? session.CurrentWave.ToString() : $"{session.CurrentWave}/{session.TotalWaves}", ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(472, 172, 158, 58), "LIVES", $"{session.Economy.Lives}/{session.Economy.StartingLives}", ColorPalette.Coral);
        DrawResultStatCard(batch, p, new Rectangle(648, 172, 158, 58), "KILLS", session.Economy.TotalKills.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(824, 172, 158, 58), "LEAKS", session.Economy.EscapedEnemies.ToString(), ColorPalette.Orange);

        if (_resultDetails)
        {
            DrawTowerContribution(batch, p, session.Statistics, new Rectangle(296, 250, 410, 298));
            DrawRunSummary(batch, p, session, new Rectangle(724, 250, 258, 298));
        }
        else
        {
            DrawResultDistrict(batch, p, session.Map.Definition, new Rectangle(310, 282, 300, 195), victory);
            DrawFittedText(batch, session.Map.Definition.DisplayName, new Vector2(658, 285), ColorPalette.Paper, 1.08f, 315);
            DrawText(batch, FormatRunDuration(session.Statistics.SimulatedSeconds), new Vector2(658, 334), ColorPalette.Muted, .8f);
            if (session.IsDefeat) DrawFittedText(batch, LiveFieldHeadline(session), new Vector2(658, 377), ColorPalette.Muted, .52f, 315);
            DrawOutcomeEmblem(batch, p, new Vector2(814, 455), victory);
        }
        DrawButton(batch, p, ResultDetailsBounds, _resultDetails ? "OVERVIEW" : "STATISTICS", true, ColorPalette.CardOutline);

        if (victory)
        {
            DrawButton(batch, p, _resultContinueButton, "ENDLESS", true, ColorPalette.Cyan, primary: true);
            DrawButton(batch, p, _resultRestartButton,
                _restartArmed ? "CONFIRM RESTART" : session.IsCoOp ? "RESTART CO-OP" : "RESTART", true,
                _restartArmed ? ColorPalette.Coral : ColorPalette.Cobalt);
            DrawButton(batch, p, _resultMenuButton, "MENU", true, ColorPalette.Violet);
        }
        else
        {
            DrawButton(batch, p, _defeatFieldButton, "VIEW FIELD", true, ColorPalette.Cyan);
            DrawButton(batch, p, _defeatRetryButton, "RETRY WAVE", _retryCheckpointWave.HasValue, ColorPalette.Cyan, primary: true);
            DrawButton(batch, p, _defeatRestartButton,
                _restartArmed ? "CONFIRM RESTART" : session.IsCoOp ? "RESTART CO-OP" : "RESTART", true,
                _restartArmed ? ColorPalette.Coral : ColorPalette.Cobalt);
            DrawButton(batch, p, _defeatMenuButton, "MENU", true, ColorPalette.Violet);
        }
        if (_restartArmed)
            DrawFittedCenteredText(batch, RestartPreservationLabel, new Vector2(640, 562), ColorPalette.Coral, 0.42f, 620);
        else if (!victory)
        {
            var retryStatus = _retryCheckpointWave.HasValue
                ? _retryContinuesSolo
                    ? $"RETRY WAVE {_retryCheckpointWave}  |  CO-OP CHECKPOINT CONTINUES SOLO"
                    : $"RETRY FROM WAVE {_retryCheckpointWave}"
                : "NO PRE-WAVE AUTOSAVE FOR THIS RUN";
            DrawFittedCenteredText(batch, retryStatus, new Vector2(640, 562),
                _retryCheckpointWave.HasValue ? ColorPalette.GreenText : ColorPalette.Muted, 0.42f, 650);
        }
        var optionCount = victory ? 3 : 4;
        var focus = ResultOptionRectangle(Math.Clamp(_resultMenuSelection, 0, optionCount - 1), victory);
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
        DrawText(batch, "FINAL LAYOUT", new Vector2(label.Center.X, label.Center.Y), ColorPalette.Paper, 0.54f, true);
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
            var y = rect.Y + 54 + index * 47;
            var color = index switch
            {
                0 => ColorPalette.Cobalt,
                1 => ColorPalette.Violet,
                2 => ColorPalette.Cyan,
                _ => ColorPalette.Orange
            };
            DrawFittedText(batch, CurrentTowerDisplayName(tower.TowerId, tower.DisplayName).ToUpperInvariant(),
                new Vector2(rect.X + 14, y), ColorPalette.Paper, 0.58f, (rect.Width - 28) * .4f);
            var contribution = tower.AssistDamageEquivalent > 0
                ? $"{tower.Damage:N0} DAMAGE +{tower.AssistDamageEquivalent:N0} ASSIST"
                : $"{tower.Damage:N0} DAMAGE   {tower.Kills:N0} KILLS";
            DrawTextRight(batch, Ellipsize(contribution, .44f, (rect.Width - 28) * .58f),
                new Vector2(rect.Right - 14, y + 1), ColorPalette.Muted, .44f);
            var bar = new Rectangle(rect.X + 14, y + 23, rect.Width - 28, 7);
            p.FillRect(batch, bar, ColorPalette.Disabled);
            p.FillRect(batch, new Rectangle(bar.X, bar.Y, Math.Max(2, (int)(bar.Width * tower.ContributionDamage / maximum)), bar.Height), color);
        }

        var strongest = leaders[0];
        p.FillRect(batch, new Rectangle(rect.X + 14, rect.Bottom - 52, rect.Width - 28, 1), ColorPalette.Divider);
        DrawText(batch, "TOP UNIT", new Vector2(rect.X + 14, rect.Bottom - 43), ColorPalette.Muted, .4f);
        DrawTextRight(batch, "IMPACT / CREDIT", new Vector2(rect.Right - 14, rect.Bottom - 43), ColorPalette.Muted, .4f);
        DrawFittedText(batch, CurrentTowerDisplayName(strongest.TowerId, strongest.DisplayName),
            new Vector2(rect.X + 14, rect.Bottom - 28), ColorPalette.Paper, .62f, rect.Width - 180);
        DrawTextRight(batch, Ellipsize(strongest.DamagePerCredit.ToString("N1"), .72f, 140),
            new Vector2(rect.Right - 14, rect.Bottom - 29), ColorPalette.Cobalt, .72f);
    }

    private string CurrentTowerDisplayName(string towerId, string recordedName) =>
        _towerDisplayNames.TryGetValue(towerId, out var displayName) ? displayName : recordedName;

    private void DrawRunSummary(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session, Rectangle rect)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        DrawText(batch, "RUN ANALYSIS", new Vector2(rect.X + 14, rect.Y + 12), ColorPalette.Navy, 0.72f);
        p.FillRect(batch, new Rectangle(rect.X + 14, rect.Y + 36, rect.Width - 28, 2), ColorPalette.Gold);

        var economy = session.Economy;
        var stats = session.Statistics;
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
        DrawText(batch, session.IsDefeat ? "FIELD AT DEFEAT" : "FIELD AT FINISH",
            new Vector2(rect.X + 14, rect.Y + 210), ColorPalette.Muted, 0.44f);
        DrawFittedText(batch, LiveFieldHeadline(session), new Vector2(rect.X + 14, rect.Y + 228),
            session.IsDefeat ? ColorPalette.Coral : ColorPalette.GreenText, 0.52f, rect.Width - 28);
        DrawText(batch, $"DEFENSE TIME  {elapsed.Minutes:00}:{elapsed.Seconds:00}", new Vector2(rect.X + 14, rect.Bottom - 27), ColorPalette.Cobalt, 0.52f);
    }

    private static string LiveFieldHeadline(MaximalBastion.GameSession session)
    {
        if (!session.IsDefeat) return "CLEAR";
        var remaining = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
        if (remaining.Length == 0)
            return session.Waves.QueuedEnemies > 0 ? $"0 LIVE  |  {session.Waves.QueuedEnemies} QUEUED" : "0 LIVE";
        var largestGroup = remaining
            .GroupBy(enemy => enemy.DisplayName)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Sum(enemy => enemy.Health + enemy.Shield))
            .First();
        var healthPercent = largestGroup.Sum(enemy => enemy.MaxHealth) <= 0
            ? 0
            : Math.Clamp(largestGroup.Sum(enemy => enemy.Health) / largestGroup.Sum(enemy => enemy.MaxHealth), 0, 1);
        var queued = session.Waves.QueuedEnemies > 0 ? $"  |  {session.Waves.QueuedEnemies} QUEUED" : "";
        return $"{remaining.Length} LIVE  |  {largestGroup.Count()} {largestGroup.Key.ToUpperInvariant()} {healthPercent:P0} HP{queued}";
    }

    private void DrawSummaryMetric(SpriteBatch batch, string label, string value, int x, int y, Color valueColor)
    {
        DrawText(batch, label, new Vector2(x, y), ColorPalette.Muted, 0.38f);
        DrawText(batch, value, new Vector2(x, y + 15),
            ColorPalette.BalancedAccentText(ColorPalette.Text(valueColor), ColorPalette.PanelAlt), 0.58f);
    }

    private void DrawPauseOverlay(SpriteBatch batch, PrimitiveRenderer p, MaximalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 220));
        DrawMenuPanel(batch, p, new Rectangle(420, 108, 440, 504), ColorPalette.Muted);
        var identity = $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}";
        DrawHeading(batch, "Paused", new Rectangle(444, 110, 392, CenteredTextTop(identity, 181, .48f, 360) - 110),
            lowerBoundary: "Run identity");
        DrawFittedCenteredText(batch,
            identity,
            new Vector2(640, 181), ColorPalette.Muted, 0.48f, 360);

        DrawButton(batch, p, PauseResumeBounds, "RESUME", true, ColorPalette.Gold, primary: true);
        DrawButton(batch, p, PauseSettingsBounds, "SETTINGS", true, ColorPalette.Muted);
        DrawButton(batch, p, PauseSaveBounds, "SAVE", session.CanSaveCheckpoint, ColorPalette.Green);
        DrawButton(batch, p, PauseLoadBounds, "LOAD", _saveAvailable, ColorPalette.Cobalt);
        DrawButton(batch, p, PauseRestartBounds, _restartArmed ? "CONFIRM RESTART" : "RESTART", true,
            ColorPalette.Coral);
        DrawButton(batch, p, PauseMainMenuBounds, "MAIN MENU", true, ColorPalette.Violet);

        var status = _restartArmed
            ? RestartPreservationLabel
            : session.IsSandbox
                ? "Sandbox runs are not saved."
                : session.CanSaveCheckpoint ? "" : "Saving unlocks after this wave.";
        DrawFittedCenteredText(batch, status, new Vector2(640, 567),
            _restartArmed ? ColorPalette.Coral : ColorPalette.Muted, 0.48f, 360);
    }

    private static int MapSelectionOrder(string mapId) => mapId.ToLowerInvariant() switch
    {
        "foundry_loop" => 0,
        "crosswind_basin" => 1,
        "prism_circuit" => 2,
        "relay_divide" => 3,
        _ => int.MaxValue
    };

    private void DrawSandboxButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled,
        Color fillColor, string? hotkey = null) =>
        DrawButton(batch, p, rect, text, enabled, fillColor, ContrastAwareButtonTextColor(fillColor), hotkey);

    private void DrawButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled,
        Color fillColor, Color? textColor = null, string? hotkey = null, bool primary = false, bool statusActive = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var hovered = enabled && rect.Contains(_hudPointer.ToPoint());
        var actionColor = fillColor == ColorPalette.Cyan ? ColorPalette.Muted : fillColor;
        var accent = enabled || statusActive ? Color.Lerp(actionColor, ColorPalette.Muted, .22f) : ColorPalette.Disabled;
        var background = ColorPalette.Surface(accent, hovered ? HoverSurfaceTint : SecondarySurfaceTint);
        p.FillRect(batch, new Rectangle(rect.X + 2, rect.Y + ButtonShadowDepth, rect.Width, rect.Height), ColorPalette.Canvas);
        p.FillRect(batch, rect, background);
        p.DrawRect(batch, rect, hovered ? accent : Color.Lerp(ColorPalette.CardOutline, accent, .30f));
        p.FillRect(batch, new Rectangle(rect.X, rect.Y + 3, hovered ? 3 : 2, rect.Height - 6),
            accent * (hovered ? 1 : .65f));
        if (hovered)
        {
            p.Brackets(batch, rect, accent, 6);
            var sweep = _settings.ReducedEffects ? 0 : (int)(_visualTimeSeconds * 45) % Math.Max(1, rect.Width - 8);
            p.FillRect(batch, new Rectangle(rect.X + 4 + sweep, rect.Bottom - 3, 4, 1), ColorPalette.Paper);
            if (_hudPressed) p.FillRect(batch, rect, accent * .14f);
        }
        var scale = primary && rect.Height >= 40 ? ControlTextScale * 1.1f : ControlTextScale;
        var measured = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        var showHotkeyBadge = _settings.ShowHotkeyBadges && !string.IsNullOrWhiteSpace(hotkey);
        var badgeAllowance = showHotkeyBadge ? Math.Min(12, rect.Width / 8) : 0;
        if (measured > rect.Width - 12 - badgeAllowance) scale *= (rect.Width - 12 - badgeAllowance) / measured;
        DrawText(batch, text, new Vector2(rect.Center.X, rect.Center.Y), enabled || statusActive ? ColorPalette.Text(textColor ?? ColorPalette.Paper) : ColorPalette.Muted, MathF.Max(0.38f, scale), true);
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

    private void DrawText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, bool centered = false, bool preserveInk = false)
    {
        if (!preserveInk) color = ColorPalette.Text(color);
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

    private void DrawFittedCenteredText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maximumWidth, bool preserveInk = false)
    {
        (text, scale) = FitCenteredText(text, scale, maximumWidth);
        DrawText(batch, text, position, color, scale, true, preserveInk);
    }

    private (string Text, float Scale) FitCenteredText(string text, float scale, float maximumWidth)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        scale = MathF.Max(0.30f, scale);
        return (Ellipsize(text, scale, maximumWidth), scale);
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
        color = ColorPalette.Text(color);
        var size = _font.MeasureString(text);
        batch.DrawString(_font, text, position, color, 0, new Vector2(size.X, 0), scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }
}
