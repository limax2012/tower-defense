using MinimalBastion.Core;
using MinimalBastion.Analytics;
using MinimalBastion.Data;
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
    DeleteSaveSlot,
    CloseSaveSlots,
    RunHistory,
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
    private readonly SpriteFont _font;
    private readonly Dictionary<string, Rectangle> _towerCards = new(StringComparer.OrdinalIgnoreCase);
    private Rectangle _startWaveButton;
    private Rectangle _speedButton;
    private Rectangle _pauseButton;
    private Rectangle _targetButton;
    private Rectangle _upgradeButton;
    private Rectangle _sellButton;
    private Rectangle _specializationAButton;
    private Rectangle _specializationBButton;
    private Rectangle _emergencyButton;
    private Rectangle _generatorButton;
    private Rectangle _overdriveButton;
    private Rectangle _autoProtocolButton;
    private string? _hoveredTowerCardId;
    private string? _specializationHint;
    private PowerNodeData? _hoveredPowerNode;
    private readonly List<(string Id, string Name, int PowerNodes, int Challenge, string Description, string PathStyle, CampaignIntelInfo Campaign)> _maps = new();
    private readonly List<DifficultyDefinition> _difficulties = new();
    private readonly List<ChallengeDefinition> _challenges = new();
    private int _selectedMapIndex;
    private int _selectedDifficultyIndex;
    private int _selectedChallengeIndex;
    private TacticalPlacementKind _hoveredTacticalPlacement;
    private string _joinHostInput = "";
    private string _joinCodeInput = "";
    private bool _editingJoinCode;
    private int _coOpWaveReadyMask;
    private bool _coOpWaveStartQueued;
    private bool _coOpEarlyBonusQueued;
    private bool _coOpPeerConnected;
    private bool _coOpResyncing;
    private bool _saveAvailable;
    private string _persistenceStatus = "Progress is stored in independent save slots between waves.";
    private IReadOnlyList<SaveSlotInfo> _saveSlots = Array.Empty<SaveSlotInfo>();
    private bool _saveSlotWriteMode;
    private int _selectedSaveSlot = 1;
    private int _saveSlotPage;
    private bool _saveSlotDeleteArmed;
    private IReadOnlyList<RunHistoryEntry> _runHistory = Array.Empty<RunHistoryEntry>();
    private string? _selectedRunHistoryId;
    private int _runHistoryPage;
    private bool _runHistoryDeleteArmed;
    private string _runHistoryStatus = "Completed defenses are retained locally and updated by endless continuation.";
    private bool _readOnlyInspection;
    private bool _towerLibraryOpen;
    private UserSettings _settings = new();
    private string _settingsStatus = "Changes apply immediately and persist for the next launch.";
    private int _towerLibraryIndex;
    private int _towerLibraryDoctrineIndex;
    private int _enemyLibraryIndex;
    private bool _libraryShowsThreats;
    private bool _libraryShowsCampaign;
    private int _campaignLibraryMapIndex;
    private Rectangle _towerLibraryDoctrineAButton;
    private Rectangle _towerLibraryDoctrineBButton;
    private IReadOnlyList<TowerDefinition> _libraryTowers = Array.Empty<TowerDefinition>();
    private IReadOnlyList<EnemyDefinition> _libraryEnemies = Array.Empty<EnemyDefinition>();
    private readonly Dictionary<string, IReadOnlyList<CampaignWaveReference>> _libraryCampaignWaves = new(StringComparer.OrdinalIgnoreCase);
    private readonly Rectangle _mapButton = new(440, 370, 190, 40);
    private readonly Rectangle _difficultyButton = new(640, 370, 90, 40);
    private readonly Rectangle _challengeButton = new(740, 370, 100, 40);
    private readonly Rectangle _continueButton = new(500, 420, 135, 44);
    private readonly Rectangle _mainMenuLibraryButton = new(645, 420, 135, 44);
    private readonly Rectangle _playButton = new(500, 474, 280, 44);
    private readonly Rectangle _coOpButton = new(500, 528, 176, 44);
    private readonly Rectangle _mainMenuSettingsButton = new(686, 528, 94, 44);
    private readonly Rectangle _quitButton = new(500, 582, 280, 40);
    private readonly Rectangle[] _saveSlotRows =
    {
        new(330, 130, 620, 66),
        new(330, 206, 620, 66),
        new(330, 282, 620, 66),
        new(330, 358, 620, 66),
        new(330, 434, 620, 66)
    };
    private readonly Rectangle _saveSlotConfirmButton = new(330, 520, 400, 46);
    private readonly Rectangle _saveSlotDeleteButton = new(740, 520, 210, 46);
    private readonly Rectangle _saveSlotPreviousButton = new(330, 582, 160, 44);
    private readonly Rectangle _saveSlotBackButton = new(500, 582, 280, 44);
    private readonly Rectangle _saveSlotNextButton = new(790, 582, 160, 44);
    private readonly Rectangle _saveSlotHistoryButton = new(990, 62, 190, 38);
    private readonly Rectangle _runHistoryDeleteButton = new(535, 520, 210, 46);
    private readonly Rectangle _joinHostField = new(500, 264, 280, 46);
    private readonly Rectangle _joinCodeField = new(500, 330, 280, 46);
    private readonly Rectangle _hostCoOpButton = new(500, 396, 280, 46);
    private readonly Rectangle _joinCoOpButton = new(500, 452, 280, 46);
    private readonly Rectangle _backButton = new(500, 518, 280, 44);
    private readonly Rectangle _resumeButton = new(500, 236, 280, 46);
    private readonly Rectangle _towerLibraryButton = new(500, 288, 135, 46);
    private readonly Rectangle _pauseSettingsButton = new(645, 288, 135, 46);
    private readonly Rectangle _saveButton = new(500, 340, 280, 46);
    private readonly Rectangle _loadButton = new(500, 392, 280, 46);
    private readonly Rectangle _restartButton = new(500, 444, 280, 46);
    private readonly Rectangle _mainMenuButton = new(500, 496, 280, 46);
    private readonly Rectangle _towerLibraryCloseButton = new(1080, 48, 130, 38);
    private readonly Rectangle _towerLibraryTowerTabButton = new(600, 48, 145, 38);
    private readonly Rectangle _towerLibraryThreatTabButton = new(755, 48, 145, 38);
    private readonly Rectangle _towerLibraryCampaignTabButton = new(910, 48, 145, 38);
    private readonly Rectangle _resultContinueButton = new(296, 580, 206, 46);
    private readonly Rectangle _resultRestartButton = new(518, 580, 206, 46);
    private readonly Rectangle _resultMenuButton = new(740, 580, 206, 46);
    private readonly Rectangle _fieldResultsButton = new(630, 9, 176, 38);
    private readonly Rectangle _windowModeButton = new(350, 220, 280, 54);
    private readonly Rectangle _resolutionButton = new(650, 220, 280, 54);
    private readonly Rectangle _vsyncButton = new(350, 292, 280, 54);
    private readonly Rectangle _effectsButton = new(650, 292, 280, 54);
    private readonly Rectangle _volumeButton = new(350, 364, 580, 54);
    private readonly Rectangle _settingsBackButton = new(500, 446, 280, 48);

    public string JoinHostInput => _joinHostInput;
    public string JoinCodeInput => _joinCodeInput;
    public string CoOpLobbyTitle { get; private set; } = "PREPARING ONLINE CO-OP";
    public string CoOpLobbyDetail { get; private set; } = "Starting the internet connection...";
    public string CoOpLobbyCode { get; private set; } = "";
    public string SelectedMapId => _maps.Count == 0 ? "foundry_loop" : _maps[_selectedMapIndex].Id;
    public string SelectedMapName => _maps.Count == 0 ? "Foundry Loop" : _maps[_selectedMapIndex].Name;
    public string SelectedDifficultyId => _difficulties.Count == 0 ? DifficultyCatalog.DefaultId : _difficulties[_selectedDifficultyIndex].Id;
    public string SelectedDifficultyName => _difficulties.Count == 0 ? "Normal" : _difficulties[_selectedDifficultyIndex].DisplayName;
    public string SelectedChallengeId => _challenges.Count == 0 ? ChallengeCatalog.DefaultId : _challenges[_selectedChallengeIndex].Id;
    public string SelectedChallengeName => _challenges.Count == 0 ? "Standard" : _challenges[_selectedChallengeIndex].DisplayName;
    public int SelectedSaveSlot => _selectedSaveSlot;
    public string? SelectedRunHistoryId => _selectedRunHistoryId;
    public bool LibraryShowsThreats => _libraryShowsThreats;
    public bool LibraryShowsCampaign => _libraryShowsCampaign;
    public string? SelectedLibraryEnemyId => _libraryEnemies.Count == 0 ? null : _libraryEnemies[_enemyLibraryIndex].Id;
    public string? SelectedLibraryCampaignMapId => _maps.Count == 0 ? null : _maps[_campaignLibraryMapIndex].Id;
    public int SelectedLibraryCampaignWaveCount => SelectedLibraryCampaignMapId is { } mapId && _libraryCampaignWaves.TryGetValue(mapId, out var waves)
        ? waves.Count
        : 0;

    public void ConfigureSettings(UserSettings settings) => _settings = settings;
    public void SetSettingsStatus(string status) => _settingsStatus = status;

    public static string PauseCheckpointStatus(bool canSave) => canSave
        ? "Between waves - save slots are available."
        : "Active wave - saving unlocks after it clears.";

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

    public static string CoOpReadyStatusLabel(int currentWave, int readyMask, bool startQueued,
        bool earlyBonusQueued, float intermissionRemaining)
    {
        var p1 = (readyMask & 0b01) != 0 ? "READY" : "WAIT";
        var p2 = (readyMask & 0b10) != 0 ? "READY" : "WAIT";
        var earlyStatus = startQueued
            ? earlyBonusQueued ? $"+{GameConstants.EarlyStartBonus} LOCKED" : "NO BONUS"
            : EarlyCallStatus(currentWave, intermissionRemaining);
        return string.IsNullOrEmpty(earlyStatus)
            ? $"P1 {p1} | P2 {p2}"
            : $"P1 {p1} | P2 {p2} | {earlyStatus}";
    }

    public static string PulsePlateButtonLabel(MinimalBastion.GameSession session)
    {
        var definition = session.Content.Tactics.EmergencyDefense;
        var field = $"FIELD {session.EmergencyDefenses.Count}/{definition.MaximumActive}";
        if (session.EmergencyDefenses.Count >= definition.MaximumActive)
            return $"[Q] {field} | FULL";
        if (session.EmergencyInventory > 0)
        {
            var stored = session.Generator is { } forge
                ? $"STORED {session.EmergencyInventory}/{forge.Level.Capacity}"
                : $"STORED {session.EmergencyInventory}";
            return $"[Q] {stored} | {field} | PLACE";
        }
        if (session.Waves.IsActive)
            return $"[Q] {field} | BUY {session.CurrentEmergencyDirectPurchaseCost}";
        return $"[Q] {field} | WAVE ONLY";
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
        _selectedRunHistoryId = preferredRunId is not null && _runHistory.Any(entry => entry.RunId == preferredRunId)
            ? preferredRunId
            : _runHistory.FirstOrDefault()?.RunId;
        var selectedIndex = Math.Max(0, _runHistory.ToList().FindIndex(entry => entry.RunId == _selectedRunHistoryId));
        _runHistoryPage = selectedIndex / _saveSlotRows.Length;
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
                var campaign = waveSets is not null && enemies is not null && waveSets.TryGetValue(x.WaveSet, out var waveSet)
                    ? WaveIntel.AnalyzeCampaign(waveSet, enemies)
                    : new CampaignIntelInfo(0, 0, "STANDARD", 1, 0);
                return (x.Id, x.DisplayName, x.PowerNodes.Count, x.ChallengeRating, x.Description, x.PathVisual.Style, campaign);
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
        _challenges.AddRange(challenges.OrderBy(challenge => challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(challenge => challenge.DisplayName));
        var defaultIndex = _challenges.FindIndex(challenge => challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase));
        _selectedChallengeIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    public void ConfigureTowerLibrary(IEnumerable<TowerDefinition> towers, IEnumerable<EnemyDefinition>? enemies = null)
    {
        _libraryTowers = towers.OrderBy(x => x.PurchaseCost).ThenBy(x => x.Id).ToArray();
        _libraryEnemies = enemies?.OrderBy(x => x.MaxHealth).ThenBy(x => x.Id).ToArray() ?? Array.Empty<EnemyDefinition>();
        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, Math.Max(0, _libraryEnemies.Count - 1));
    }

    public UiAction HandleMainMenu(InputSnapshot input)
    {
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_mapButton.Contains(point) && _maps.Count > 1)
        {
            _selectedMapIndex = (_selectedMapIndex + 1) % _maps.Count;
            return UiAction.None;
        }
        if (_difficultyButton.Contains(point) && _difficulties.Count > 1)
        {
            _selectedDifficultyIndex = (_selectedDifficultyIndex + 1) % _difficulties.Count;
            return UiAction.None;
        }
        if (_challengeButton.Contains(point) && _challenges.Count > 1)
        {
            _selectedChallengeIndex = (_selectedChallengeIndex + 1) % _challenges.Count;
            return UiAction.None;
        }
        if (_continueButton.Contains(point) && _saveAvailable) return UiAction.LoadGame;
        if (_mainMenuLibraryButton.Contains(point)) return UiAction.TowerLibrary;
        if (_playButton.Contains(point)) return UiAction.Play;
        if (_coOpButton.Contains(point)) return UiAction.CoOp;
        if (_mainMenuSettingsButton.Contains(point)) return UiAction.Settings;
        if (_quitButton.Contains(point)) return UiAction.Exit;
        return UiAction.None;
    }

    public UiAction HandleSettingsInput(InputSnapshot input)
    {
        if (input.EscapePressed || input.PausePressed || input.RightPressed) return UiAction.CloseSettings;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_settingsBackButton.Contains(point)) return UiAction.CloseSettings;
        if (_windowModeButton.Contains(point)) _settings.Fullscreen = !_settings.Fullscreen;
        else if (_resolutionButton.Contains(point)) _settings.CycleResolution();
        else if (_vsyncButton.Contains(point)) _settings.VSync = !_settings.VSync;
        else if (_effectsButton.Contains(point)) _settings.ReducedEffects = !_settings.ReducedEffects;
        else if (_volumeButton.Contains(point))
        {
            _settings.SfxVolume += 0.25f;
            if (_settings.SfxVolume > 1.001f) _settings.SfxVolume = 0;
        }
        else return UiAction.None;
        return UiAction.ApplySettings;
    }

    private static int DifficultyOrder(string id) => id.ToLowerInvariant() switch
    {
        "easy" => 0,
        "normal" => 1,
        "hard" => 2,
        "bastion" => 3,
        _ => 4
    };

    public UiAction HandleSaveSlots(InputSnapshot input)
    {
        if (input.EscapePressed)
        {
            if (_saveSlotDeleteArmed)
            {
                _saveSlotDeleteArmed = false;
                return UiAction.None;
            }
            return UiAction.CloseSaveSlots;
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
            _saveSlotDeleteArmed = false;
            return UiAction.None;
        }

        if (_saveSlotPreviousButton.Contains(point) && _saveSlotPage > 0)
        {
            _saveSlotPage--;
            _selectedSaveSlot = _saveSlots[_saveSlotPage * _saveSlotRows.Length].Slot;
            _saveSlotDeleteArmed = false;
            return UiAction.None;
        }
        if (_saveSlotNextButton.Contains(point) && _saveSlotPage + 1 < pageCount)
        {
            _saveSlotPage++;
            _selectedSaveSlot = _saveSlots[_saveSlotPage * _saveSlotRows.Length].Slot;
            _saveSlotDeleteArmed = false;
            return UiAction.None;
        }

        var selected = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
        var canConfirm = _saveSlotWriteMode || selected is { IsOccupied: true, Error: null };
        if (_saveSlotConfirmButton.Contains(point) && canConfirm) return UiAction.ConfirmSaveSlot;
        if (_saveSlotDeleteButton.Contains(point) && selected is { IsOccupied: true })
        {
            if (_saveSlotDeleteArmed)
            {
                _saveSlotDeleteArmed = false;
                return UiAction.DeleteSaveSlot;
            }
            _saveSlotDeleteArmed = true;
            return UiAction.None;
        }
        if (_saveSlotBackButton.Contains(point)) return UiAction.CloseSaveSlots;
        return UiAction.None;
    }

    public UiAction HandleRunHistory(InputSnapshot input)
    {
        if (input.EscapePressed)
        {
            if (_runHistoryDeleteArmed)
            {
                _runHistoryDeleteArmed = false;
                return UiAction.None;
            }
            return UiAction.CloseRunHistory;
        }
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
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

    public UiAction HandleCoOpMenu(InputSnapshot input)
    {
        if (input.LeftPressed)
        {
            var clicked = input.MousePosition.ToPoint();
            if (_joinHostField.Contains(clicked)) _editingJoinCode = false;
            else if (_joinCodeField.Contains(clicked)) _editingJoinCode = true;
        }

        if (input.CopyPressed)
            ClipboardService.TrySetText(_editingJoinCode ? _joinCodeInput : _joinHostInput);

        if (!string.IsNullOrEmpty(input.TextEntered))
        {
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
        if (input.EnterPressed && CanJoinOnline) return UiAction.JoinCoOp;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_hostCoOpButton.Contains(point)) return UiAction.HostCoOp;
        if (_joinCoOpButton.Contains(point) && CanJoinOnline) return UiAction.JoinCoOp;
        if (_backButton.Contains(point)) return UiAction.MainMenu;
        return UiAction.None;
    }

    private bool CanJoinOnline => !string.IsNullOrWhiteSpace(_joinHostInput) && _joinCodeInput.Length == 6;

    public UiAction HandleCoOpLobby(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.MainMenu;
        return input.LeftPressed && _backButton.Contains(input.MousePosition.ToPoint()) ? UiAction.MainMenu : UiAction.None;
    }

    public void SetCoOpLobbyStatus(string title, string detail, string code = "")
    {
        CoOpLobbyTitle = title;
        CoOpLobbyDetail = detail;
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
    }

    public UiAction HandleGameplayInput(InputSnapshot input, MinimalBastion.GameSession session, Action<GameCommand>? commandSink = null, int playerId = 1)
    {
        var point = input.MousePosition.ToPoint();
        _hoveredTowerCardId = _towerCards.FirstOrDefault(x => x.Value.Contains(point)).Key;
        _hoveredPowerNode = session.Map.Definition.PowerNodes.FirstOrDefault(node =>
            Vector2.DistanceSquared(node.Position.ToVector2(), input.MousePosition) <= node.Radius * node.Radius);
        _specializationHint = null;
        if (session.SelectedTower is { RequiresDoctrine: true } doctrinePreview)
        {
            if (_specializationAButton.Contains(point) && doctrinePreview.Definition.Tier2Doctrines.Count > 0)
                _specializationHint = TowerInfo.DoctrineSummary(doctrinePreview.Definition, doctrinePreview.Definition.Tier2Doctrines[0]);
            else if (_specializationBButton.Contains(point) && doctrinePreview.Definition.Tier2Doctrines.Count > 1)
                _specializationHint = TowerInfo.DoctrineSummary(doctrinePreview.Definition, doctrinePreview.Definition.Tier2Doctrines[1]);
        }
        else if (session.SelectedTower is { RequiresSpecialization: true } branchPreview)
        {
            if (_specializationAButton.Contains(point) && branchPreview.Definition.Specializations.Count > 0)
                _specializationHint = TowerInfo.SpecializationSummary(branchPreview.Level, branchPreview.Definition.Specializations[0], branchPreview.Doctrine);
            else if (_specializationBButton.Contains(point) && branchPreview.Definition.Specializations.Count > 1)
                _specializationHint = TowerInfo.SpecializationSummary(branchPreview.Level, branchPreview.Definition.Specializations[1], branchPreview.Doctrine);
        }
        _hoveredTacticalPlacement = _emergencyButton.Contains(point) ? TacticalPlacementKind.PulsePlate :
            _generatorButton.Contains(point) ? TacticalPlacementKind.ChargeForge : TacticalPlacementKind.None;
        if (input.EscapePressed && session.PlacementTowerId is null && session.TacticalPlacement == TacticalPlacementKind.None) return UiAction.Pause;

        var towersByCost = session.Content.Towers.Values.OrderBy(x => x.PurchaseCost).ToArray();
        if (input.TowerHotkey > 0 && input.TowerHotkey <= towersByCost.Length)
            session.BeginPlacement(towersByCost[input.TowerHotkey - 1].Id);
        if (input.StartWavePressed) RequestStartWave(session, commandSink, playerId);
        if (input.SpeedPressed) RequestSpeed(session, commandSink, playerId);
        if (input.EmergencyPressed) session.BeginEmergencyPlacement();
        if (input.GeneratorPressed) session.BeginGeneratorPlacement();
        if (input.OverdrivePressed) RequestOverdrive(session, commandSink, playerId);
        if (input.TargetPressed) RequestTarget(session, commandSink, playerId);
        if (input.UpgradePressed) RequestUpgrade(session, commandSink, playerId);
        if (input.SellPressed) RequestSell(session, commandSink, playerId);
        if (!input.LeftPressed) return UiAction.None;

        if (_startWaveButton.Contains(point))
        {
            RequestStartWave(session, commandSink, playerId);
            return UiAction.None;
        }
        if (_speedButton.Contains(point))
        {
            RequestSpeed(session, commandSink, playerId);
            return UiAction.None;
        }
        if (_pauseButton.Contains(point) && !session.IsCoOp) return UiAction.Pause;
        if (_emergencyButton.Contains(point))
        {
            session.BeginEmergencyPlacement();
            return UiAction.None;
        }
        if (_generatorButton.Contains(point))
        {
            session.BeginGeneratorPlacement();
            return UiAction.None;
        }
        if (_overdriveButton.Contains(point))
        {
            RequestOverdrive(session, commandSink, playerId);
            return UiAction.None;
        }
        if (_autoProtocolButton.Contains(point))
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
        else if (_targetButton.Contains(point)) RequestTarget(session, commandSink, playerId);
        else if (_upgradeButton.Contains(point)) RequestUpgrade(session, commandSink, playerId);
        else if (_sellButton.Contains(point)) RequestSell(session, commandSink, playerId);

        return UiAction.None;
    }

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

    private static void RequestTarget(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { IsSupport: false } tower) return;
        if (sink is null)
        {
            session.CycleSelectedTarget();
            return;
        }
        var modes = Enum.GetValues<TargetMode>();
        var next = modes[((int)tower.TargetMode + 1) % modes.Length];
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.SetTargetMode, EntityId = tower.Id, TargetMode = next });
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

        if (input.EscapePressed || input.PausePressed) return UiAction.Resume;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_resumeButton.Contains(point)) return UiAction.Resume;
        if (_towerLibraryButton.Contains(point))
        {
            _towerLibraryOpen = true;
            _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
            return UiAction.None;
        }
        if (_pauseSettingsButton.Contains(point)) return UiAction.Settings;
        if (_saveButton.Contains(point) && session.CanSaveCheckpoint) return UiAction.SaveGame;
        if (_loadButton.Contains(point) && _saveAvailable) return UiAction.LoadGame;
        if (_restartButton.Contains(point)) return UiAction.Restart;
        if (_mainMenuButton.Contains(point)) return UiAction.MainMenu;
        return UiAction.None;
    }

    public UiAction HandleTitleTowerLibrary(InputSnapshot input) =>
        HandleTowerLibraryInput(input) ? UiAction.MainMenu : UiAction.None;

    private bool HandleTowerLibraryInput(InputSnapshot input)
    {
        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, Math.Max(0, _libraryTowers.Count - 1));
        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, Math.Max(0, _libraryEnemies.Count - 1));
        if (input.EscapePressed || input.PausePressed || input.RightPressed) return true;
        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, Math.Max(0, _maps.Count - 1));
        var activeCount = _libraryShowsCampaign ? _maps.Count : _libraryShowsThreats ? _libraryEnemies.Count : _libraryTowers.Count;
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
            return false;
        }
        if (_towerLibraryThreatTabButton.Contains(point) && _libraryEnemies.Count > 0)
        {
            _libraryShowsThreats = true;
            _libraryShowsCampaign = false;
            return false;
        }
        if (_towerLibraryCampaignTabButton.Contains(point) && _libraryCampaignWaves.Count > 0)
        {
            _libraryShowsThreats = false;
            _libraryShowsCampaign = true;
            return false;
        }
        if (_libraryShowsCampaign)
        {
            for (var index = 0; index < _maps.Count; index++)
            {
                if (!CampaignLibraryMapRow(index).Contains(point)) continue;
                _campaignLibraryMapIndex = index;
                break;
            }
            return false;
        }
        if (_libraryShowsThreats)
        {
            for (var index = 0; index < _libraryEnemies.Count; index++)
            {
                if (!EnemyLibraryRow(index).Contains(point)) continue;
                _enemyLibraryIndex = index;
                break;
            }
            return false;
        }
        if (_towerLibraryDoctrineAButton.Contains(point))
        {
            _towerLibraryDoctrineIndex = 0;
            return false;
        }
        if (_towerLibraryDoctrineBButton.Contains(point))
        {
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

    public UiAction HandleResultInput(InputSnapshot input, bool victory)
    {
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_resultContinueButton.Contains(point)) return victory ? UiAction.ContinueEndless : UiAction.ViewField;
        if (_resultRestartButton.Contains(point)) return UiAction.Restart;
        if (_resultMenuButton.Contains(point)) return UiAction.MainMenu;
        return UiAction.None;
    }

    public UiAction HandleDefeatFieldInput(InputSnapshot input)
    {
        if (input.EscapePressed) return UiAction.ViewResults;
        return input.LeftPressed && _fieldResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.ViewResults
            : UiAction.None;
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer p, GameState state, MinimalBastion.GameSession? session)
    {
        _readOnlyInspection = state == GameState.DefeatField;
        if (state == GameState.MainMenu)
        {
            DrawMainMenu(batch, p);
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
        if (state == GameState.Playing) DrawAnnouncement(batch, p, session);

        if (state == GameState.Paused) DrawPauseOverlay(batch, p, session);
        else if (state == GameState.CoOpReconnect) DrawCoOpReconnectOverlay(batch, p);
        else if (state == GameState.Victory) DrawResultOverlay(batch, p, session, true);
        else if (state == GameState.Defeat) DrawResultOverlay(batch, p, session, false);
        else if (state == GameState.DefeatField) DrawDefeatFieldControls(batch, p);
    }

    private void DrawHud(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.TopBarHeight), ColorPalette.Navy);
        p.FillRect(batch, new Rectangle(0, GameConstants.TopBarHeight - 2, GameConstants.LogicalWidth, 2), ColorPalette.Cyan);
        DrawText(batch, "LIVES", new Vector2(18, 8), ColorPalette.Coral, 0.75f);
        DrawText(batch, $"{session.Economy.Lives}/{session.Economy.StartingLives}", new Vector2(18, 26), ColorPalette.Paper, 1f);
        DrawText(batch, "CREDITS", new Vector2(115, 8), ColorPalette.Gold, 0.75f);
        DrawText(batch, session.Economy.Credits.ToString(), new Vector2(115, 26), ColorPalette.Paper, 1f);
        DrawText(batch, session.IsEndlessMode ? "ENDLESS" : "WAVE", new Vector2(225, 8), ColorPalette.Cyan, 0.75f);
        DrawText(batch, session.IsEndlessMode ? session.CurrentWave.ToString() : $"{session.CurrentWave}/{session.TotalWaves}", new Vector2(225, 26), ColorPalette.Paper, 1f);
        DrawText(batch, "ENEMIES", new Vector2(335, 8), ColorPalette.Lime, 0.75f);
        DrawText(batch, session.EnemiesRemaining.ToString(), new Vector2(335, 26), ColorPalette.Paper, 1f);

        var previewWave = session.Waves.ActiveWave ?? session.Waves.NextWave;
        if (previewWave is not null)
        {
            var intel = WaveIntel.Analyze(previewWave, session.Content.Enemies);
            DrawText(batch, session.Waves.IsActive ? "ACTIVE THREAT" : "NEXT THREAT", new Vector2(820, 8), ColorPalette.Gold, 0.62f);
            DrawText(batch, $"{intel.ApproximateCount}  {intel.CompactThreats}", new Vector2(820, 27), ColorPalette.Paper, 0.68f);
        }

        _startWaveButton = new Rectangle(450, 9, 170, 38);
        _speedButton = new Rectangle(630, 9, 76, 38);
        _pauseButton = new Rectangle(716, 9, 90, 38);
        var startWaveLabel = session.CanStartWave
            ? session.IntermissionRemaining > 0 ? $"EARLY +{GameConstants.EarlyStartBonus}  {MathF.Ceiling(session.IntermissionRemaining):0}s" : "START WAVE"
            : "IN WAVE";
        var startWaveEnabled = session.CanStartWave;
        if (session.IsCoOp && session.CanStartWave)
        {
            var localBit = 1 << (session.LocalPlayerId - 1);
            var localReady = (_coOpWaveReadyMask & localBit) != 0;
            startWaveLabel = CoOpWaveButtonLabel(session.LocalPlayerId, session.CurrentWave,
                _coOpWaveReadyMask, _coOpWaveStartQueued, _coOpEarlyBonusQueued, session.IntermissionRemaining);
            startWaveEnabled = !_coOpWaveStartQueued && !localReady;
        }
        DrawButton(batch, p, _startWaveButton, startWaveLabel, startWaveEnabled, ColorPalette.Green);
        DrawButton(batch, p, _speedButton, session.Speed >= 1.5f ? "2x" : "1x", true, ColorPalette.Violet);
        var linkLabel = _coOpResyncing ? "SYNC..." : _coOpPeerConnected ? "P1 + P2" : "P2 OFF";
        DrawButton(batch, p, _pauseButton, session.IsCoOp ? linkLabel : "PAUSE", !session.IsCoOp || _coOpPeerConnected, session.IsCoOp ? ColorPalette.Green : ColorPalette.Coral);
    }

    private void DrawAnnouncement(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        if (session.AnnouncementRemaining <= 0 || string.IsNullOrWhiteSpace(session.AnnouncementTitle)) return;
        var fade = MathHelper.Clamp(session.AnnouncementRemaining / 0.35f, 0, 1);
        var alpha = (byte)(232 * fade);
        var accent = session.AnnouncementPositive ? ColorPalette.Green : ColorPalette.Gold;
        var rect = new Rectangle(270, 112, 420, 62);
        p.FillRect(batch, rect, ColorPalette.WithAlpha(ColorPalette.Navy, alpha));
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 5, rect.Height), ColorPalette.WithAlpha(accent, alpha));
        DrawText(batch, session.AnnouncementTitle, new Vector2(rect.Center.X, rect.Y + 19), ColorPalette.WithAlpha(ColorPalette.Paper, alpha), 0.72f, true);
        DrawText(batch, session.AnnouncementSubtitle ?? "", new Vector2(rect.Center.X, rect.Y + 43), ColorPalette.WithAlpha(ColorPalette.PanelAlt, alpha), 0.51f, true);
    }

    private void DrawTacticalBar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _emergencyButton = new Rectangle(972, 98, 296, 28);
        _generatorButton = new Rectangle(972, 132, 296, 28);
        _overdriveButton = new Rectangle(972, 166, 218, 28);
        _autoProtocolButton = new Rectangle(1196, 166, 72, 28);
        var defense = session.Content.Tactics.EmergencyDefense;
        var plateFieldFull = session.EmergencyDefenses.Count >= defense.MaximumActive;
        var emergencyReady = session.TacticalSystemsEnabled && !plateFieldFull && (session.EmergencyInventory > 0 || session.CanDirectPurchaseEmergencyDefense);
        var emergencyLabel = session.TacticalSystemsEnabled ? PulsePlateButtonLabel(session) : "[Q] PLATES | DIRECTIVE OFF";
        DrawButton(batch, p, _emergencyButton, emergencyLabel, emergencyReady, ColorPalette.Gold);

        var generator = session.Content.Tactics.Generator;
        var generatorReady = session.TacticalSystemsEnabled && (session.Generator is not null || session.Economy.CanAfford(generator.PurchaseCost));
        var generatorLabel = !session.TacticalSystemsEnabled ? "[G] FORGE | DIRECTIVE OFF" : session.Generator is { } active
            ? session.EmergencyInventory >= active.Level.Capacity
                ? $"[G] FORGE L{active.LevelIndex + 1} | FULL"
                : session.Waves.IsActive
                    ? $"[G] FORGE L{active.LevelIndex + 1} | +1 IN {active.ProductionRemaining:0}s"
                    : $"[G] FORGE L{active.LevelIndex + 1} | PAUSED {active.ProductionRemaining:0}s"
            : $"[G] FORGE {generator.PurchaseCost} | ACTIVE WAVES";
        DrawButton(batch, p, _generatorButton, generatorLabel, generatorReady, ColorPalette.Green);

        var selected = session.SelectedTower;
        var activeOverdrive = session.Towers.FirstOrDefault(x => x.IsOverdriven);
        var overdriveReady = selected is not null && session.OverdriveCooldownRemaining <= 0 && !selected.IsOverdriven;
        var overdriveLabel = activeOverdrive is not null ? $"[E] {activeOverdrive.Protocol.DisplayName.ToUpperInvariant()} {activeOverdrive.OverdriveRemaining:0.0}s" :
            session.OverdriveCooldownRemaining > 0 ? $"[E] PROTOCOL | {session.OverdriveCooldownRemaining:0.0}s" :
            selected is null ? "[E] PROTOCOL | SELECT" :
            $"[E] {selected.Protocol.DisplayName.ToUpperInvariant()}";
        DrawButton(batch, p, _overdriveButton, overdriveLabel, overdriveReady, ColorPalette.Coral);
        var autoActive = selected is not null && session.AutoOverdriveTowerId == selected.Id;
        DrawButton(batch, p, _autoProtocolButton, autoActive ? "AUTO ON" : "AUTO", selected is not null,
            autoActive ? ColorPalette.Green : ColorPalette.Cobalt);
    }

    private void DrawSidebar(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(GameConstants.SidebarX, GameConstants.TopBarHeight, 320, GameConstants.LogicalHeight - GameConstants.TopBarHeight), ColorPalette.Panel);
        p.Line(batch, new Vector2(GameConstants.SidebarX, GameConstants.TopBarHeight), new Vector2(GameConstants.SidebarX, GameConstants.LogicalHeight), ColorPalette.Divider, 2);
        p.FillRect(batch, new Rectangle(972, 56, 296, 34), ColorPalette.Navy);
        DrawText(batch, "TACTICAL SYSTEMS", new Vector2(986, 64), ColorPalette.Paper, 1.0f);
        if (session.IsCoOp)
        {
            DrawText(batch, _coOpPeerConnected ? "P1 + P2 ONLINE" : "WAITING FOR P2", new Vector2(1210, 69), _coOpPeerConnected ? ColorPalette.Green : ColorPalette.Coral, 0.43f, true);
            var readyStatus = session.CanStartWave
                ? CoOpReadyStatusLabel(session.CurrentWave, _coOpWaveReadyMask, _coOpWaveStartQueued,
                    _coOpEarlyBonusQueued, session.IntermissionRemaining)
                : "SHARED WAVE IN PROGRESS";
            DrawFittedCenteredText(batch, readyStatus, new Vector2(1120, 82), ColorPalette.Gold, 0.35f, 280);
        }
        p.FillRect(batch, new Rectangle(972, 90, 296, 3), ColorPalette.Gold);

        p.FillRect(batch, new Rectangle(972, 200, 296, 3), ColorPalette.Cyan);
        DrawText(batch, "TOWER WORKSHOP", new Vector2(980, 207), ColorPalette.Navy, 0.78f);

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
            var selected = session.PlacementTowerId == definition.Id;
            var cardFill = !available ? ColorPalette.Disabled : selected ? ColorPalette.Tint(definition.Visual.PrimaryColor, 0.42f) : ColorPalette.PanelAlt;
            var cardOutline = !available ? ColorPalette.Muted : selected ? definition.Visual.PrimaryColor : affordable ? ColorPalette.CardOutline : ColorPalette.Coral;
            p.FillRect(batch, rect, cardFill);
            p.DrawRect(batch, rect, cardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(rect.X + 17, rect.Center.Y), 10, definition.Visual.Shape, definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
            DrawText(batch, index == 9 ? "0" : (index + 1).ToString(), new Vector2(rect.Right - 14, rect.Y + 7), selected ? definition.Visual.AccentColor : ColorPalette.Muted, 0.39f, true);
            DrawFittedText(batch, definition.DisplayName, new Vector2(rect.X + 38, rect.Y + 5), ColorPalette.Ink, 0.53f, 80);
            DrawText(batch, available ? $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}" : "DIRECTIVE OFF",
                new Vector2(rect.X + 38, rect.Y + 21), available ? affordable ? ColorPalette.Muted : ColorPalette.Coral : ColorPalette.Muted, 0.44f);
        }

        p.FillRect(batch, new Rectangle(972, 452, 296, 3), ColorPalette.Violet);
        DrawText(batch, "TOWER INTEL", new Vector2(980, 458), ColorPalette.Navy, 0.72f);
        DrawTowerIntel(batch, p, session);
    }

    private void DrawTowerIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
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
            DrawText(batch, "Hover a card to compare stats.", new Vector2(980, 482), ColorPalette.Muted, 0.72f);
            DrawText(batch, "Click a placed tower to manage it.", new Vector2(980, 505), ColorPalette.Muted, 0.72f);
            return;
        }

        var intelCard = new Rectangle(972, 474, 296, 168);
        p.FillRect(batch, intelCard, ColorPalette.PanelAlt);
        p.DrawRect(batch, intelCard, tower.Definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, new Vector2(1000, 512), tower.Definition.Visual.Radius, tower.Definition.Visual.Shape,
            tower.Definition.Visual.PrimaryColor, tower.Definition.Visual.AccentColor, tower.LevelIndex + 1, true, levelMarks: true);
        DrawText(batch, tower.Definition.DisplayName, new Vector2(1036, 486), ColorPalette.Ink, 0.86f);
        var ownership = session.IsCoOp ? $"   PLACED P{tower.OwnerPlayerId}" : "";
        var autoArmed = session.AutoOverdriveTowerId == tower.Id;
        var doctrineSuffix = tower.Doctrine is { } doctrine ? $"  {doctrine.ShortLabel.ToUpperInvariant()}" : "";
        var levelTitle = tower.Specialization is { } chosen ? chosen.DisplayName.ToUpperInvariant() + doctrineSuffix : $"LEVEL {tower.LevelIndex + 1}{doctrineSuffix}";
        if (autoArmed) levelTitle += "   AUTO";
        DrawText(batch, $"{levelTitle}   {TowerInfo.ShortRole(tower.Definition)}{ownership}", new Vector2(1036, 508), ColorPalette.Muted, 0.60f);
        var effectiveDamage = session.GetEffectiveDamage(tower, tower.Level.Damage);
        var effectiveRate = session.GetEffectiveAttacksPerSecond(tower);
        var effectiveDps = effectiveDamage * effectiveRate * Math.Max(1, tower.Level.PelletCount);
        DrawFittedText(batch, tower.IsSupport
            ? $"AURA {tower.Level.AuraRange:0}   RATE +{tower.Level.AuraAttackSpeedBonus:P0}"
            : $"ACTIVE  DAMAGE {effectiveDamage:0.#}   DPS {effectiveDps:0.#}   RANGE {session.GetEffectiveRange(tower):0}",
            new Vector2(980, 540), ColorPalette.Ink, 0.56f, 280);
        DrawText(batch, TowerInfo.Special(tower.Definition, tower.Level), new Vector2(980, 559), ColorPalette.Ink, 0.56f);
        DrawFittedText(batch, TowerLifetimeSummary(tower), new Vector2(980, 578), ColorPalette.Cobalt, 0.48f, 280);
        var power = session.Map.GetPowerBuff(tower.Position);
        var powerNodes = session.Map.GetPowerNodes(tower.Position);
        var powerHint = powerNodes.Count > 0
            ? $"{PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}  |  {TowerInfo.PowerNodeStatChange(tower.Definition, tower.Level, power)}"
            : null;
        var supportBuff = session.GetSupportBuff(tower);
        var beaconHint = supportBuff.IsActive ? TowerInfo.SignalBeaconStatChange(tower.Level, supportBuff) : null;
        var overdriveHint = tower.IsOverdriven
            ? $"{tower.Protocol.DisplayName.ToUpperInvariant()}  {tower.OverdriveRemaining:0.0}s  {TowerInfo.ProtocolBonuses(tower.Protocol)}"
            : autoArmed ? $"AUTO ARMED: {tower.Protocol.DisplayName.ToUpperInvariant()}  |  THREAT {tower.Protocol.AutoTriggerCount}+" : null;
        var primaryHint = beaconHint ?? TowerInfo.Strength(tower.Definition);
        var secondaryHint = powerHint ?? overdriveHint ?? (beaconHint is not null ? TowerInfo.Strength(tower.Definition) : TowerInfo.Limitation(tower.Definition));
        DrawFittedText(batch, primaryHint, new Vector2(980, 594),
            beaconHint is not null ? ColorPalette.Gold : ColorPalette.Muted,
            beaconHint is not null ? 0.48f : 0.53f, 280);
        DrawFittedText(batch, secondaryHint, new Vector2(980, 610),
            powerHint is not null ? powerNodes[0].NodeColor : overdriveHint is not null ? ColorPalette.Coral : ColorPalette.Muted,
            powerHint is not null ? 0.44f : 0.50f, 280);
        var upgradeLine = _specializationHint ?? (tower.RequiresDoctrine
            ? "CHOOSE A TIER 2 DOCTRINE"
            : tower.RequiresSpecialization
            ? "CHOOSE A FINAL SPECIALIZATION"
            : tower.CanUpgrade
                ? $"NEXT {tower.UpgradeCost}: {TowerInfo.UpgradeSummary(tower.Definition, tower.LevelIndex, supportBuff, power)}"
                : "MAXIMUM LEVEL");
        DrawFittedText(batch, upgradeLine, new Vector2(980, 626),
            _specializationHint is not null ? ColorPalette.Cobalt : tower.RequiresDoctrine || tower.RequiresSpecialization || tower.CanUpgrade ? ColorPalette.Violet : ColorPalette.Muted,
            0.52f, 280);

        _targetButton = new Rectangle(980, 670, 88, 30);
        _upgradeButton = new Rectangle(1074, 670, 92, 30);
        _sellButton = new Rectangle(1172, 670, 94, 30);
        _specializationAButton = Rectangle.Empty;
        _specializationBButton = Rectangle.Empty;
        var canManage = !_readOnlyInspection;
        if (tower.RequiresDoctrine || tower.RequiresSpecialization)
        {
            _upgradeButton = Rectangle.Empty;
            // Keep the first branch in the normal upgrade position and place the
            // alternate directly beneath it, with a clear gutter below intel.
            _targetButton = new Rectangle(980, 650, 88, 28);
            _specializationAButton = new Rectangle(1074, 650, 118, 28);
            _specializationBButton = new Rectangle(1074, 686, 118, 28);
            _sellButton = new Rectangle(1198, 650, 68, 28);
            var firstLabel = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[0].ShortLabel : tower.Definition.Specializations[0].ShortLabel;
            var secondLabel = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[1].ShortLabel : tower.Definition.Specializations[1].ShortLabel;
            var firstCost = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[0].UpgradeCost : tower.Definition.Specializations[0].UpgradeCost;
            var secondCost = tower.RequiresDoctrine ? tower.Definition.Tier2Doctrines[1].UpgradeCost : tower.Definition.Specializations[1].UpgradeCost;
            DrawButton(batch, p, _targetButton, tower.TargetMode.ToString().ToUpperInvariant(), canManage, ColorPalette.Cyan);
            DrawButton(batch, p, _specializationAButton, $"{firstLabel.ToUpperInvariant()} {firstCost}", canManage && session.Economy.CanAfford(firstCost), tower.Definition.Visual.PrimaryColor);
            DrawButton(batch, p, _specializationBButton, $"{secondLabel.ToUpperInvariant()} {secondCost}", canManage && session.Economy.CanAfford(secondCost), ColorPalette.Violet);
            DrawButton(batch, p, _sellButton, $"SELL {tower.SellValue}", canManage, ColorPalette.Orange);
            return;
        }
        if (!tower.IsSupport) DrawButton(batch, p, _targetButton, tower.TargetMode.ToString().ToUpperInvariant(), canManage, ColorPalette.Cyan);
        DrawButton(batch, p, _upgradeButton, tower.CanUpgrade ? $"UP {tower.UpgradeCost}" : "MAX", canManage && tower.CanUpgrade && session.Economy.CanAfford(tower.UpgradeCost), ColorPalette.Violet);
        DrawButton(batch, p, _sellButton, $"SELL {tower.SellValue}", canManage, ColorPalette.Orange);
    }

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
        p.FillRect(batch, new Rectangle(972, 474, 296, 202), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 202), definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, new Vector2(1000, 512), definition.Visual.Radius, definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
        DrawText(batch, definition.DisplayName, new Vector2(1036, 486), ColorPalette.Ink, 0.86f);
        DrawText(batch, $"{definition.PurchaseCost} CREDITS   {TowerInfo.ShortRole(definition)}", new Vector2(1036, 508), ColorPalette.Muted, 0.60f);
        DrawFittedText(batch, definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
            ? $"AURA {level.AuraRange:0}   RATE +{level.AuraAttackSpeedBonus:P0}   RANGE +{level.AuraRangeBonus:P0}"
            : $"DAMAGE {level.Damage:0.#}   DPS {TowerInfo.RawDps(level):0.#}   RATE {level.AttacksPerSecond:0.##}/s   RANGE {level.Range:0}",
            new Vector2(980, 542), ColorPalette.Ink, 0.57f, 280);
        DrawText(batch, TowerInfo.Special(definition, level), new Vector2(980, 565), ColorPalette.Ink, 0.57f);
        var powerNodes = placing ? session.Map.GetPowerNodes(session.PlacementPosition) : Array.Empty<PowerNodeData>();
        if (powerNodes.Count > 0)
        {
            var power = session.Map.GetPowerBuff(session.PlacementPosition);
            DrawFittedText(batch, $"ON {PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}",
                new Vector2(980, 590), powerNodes[0].NodeColor, 0.49f, 280);
            DrawFittedText(batch, TowerInfo.PowerNodeStatChange(definition, level, power),
                new Vector2(980, 612), ColorPalette.Cobalt, 0.52f, 280);
        }
        else
        {
            DrawText(batch, TowerInfo.Strength(definition), new Vector2(980, 590), ColorPalette.Muted, 0.54f);
            DrawFittedText(batch, TowerInfo.ProtocolSummary(definition), new Vector2(980, 612), ColorPalette.Coral, 0.46f, 280);
        }
        DrawFittedText(batch, $"L2 {level.UpgradeCost}: {TowerInfo.UpgradeSummary(definition, 0)}",
            new Vector2(980, 638), ColorPalette.Violet, 0.51f, 280);
        DrawText(batch, placing ? "CLICK MAP TO DEPLOY   |   ESC TO CANCEL" : "CLICK CARD TO PREPARE PLACEMENT", new Vector2(980, 658), placing ? ColorPalette.Green : ColorPalette.Cobalt, 0.50f);
    }

    private static string PowerNodeNames(IReadOnlyList<PowerNodeData> nodes) => nodes.Count == 1
        ? nodes[0].DisplayName.ToUpperInvariant()
        : $"{string.Join(" + ", nodes.Select(node => node.DisplayName.Replace(" Node", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant()))} NODES";

    private void DrawEmergencyIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        _targetButton = Rectangle.Empty;
        _upgradeButton = Rectangle.Empty;
        _sellButton = Rectangle.Empty;
        var definition = session.Content.Tactics.EmergencyDefense;
        p.FillRect(batch, new Rectangle(972, 474, 296, 202), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 202), definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, new Vector2(1000, 512), definition.Visual.Radius + 2, definition.Visual.Shape,
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
        DrawText(batch, session.TacticalPlacement == TacticalPlacementKind.PulsePlate ? "CLICK THE ROAD TO DEPLOY   |   ESC TO CANCEL" : "Q OR CLICK ABOVE TO PREPARE", new Vector2(980, 658), ColorPalette.Cobalt, 0.49f);
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
        p.DrawPolygon(batch, new Vector2(1000, 512), 17, 4, false, zone.NodeColor, MathHelper.PiOver4);
        p.DrawPolygon(batch, new Vector2(1000, 512), 8, 4, false, ColorPalette.Paper, MathHelper.PiOver4);
        DrawText(batch, zone.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.82f);
        DrawText(batch, "SURGE NODE", new Vector2(1028, 508), zone.NodeColor, 0.60f);
        var bonus = zone.AttackSpeedBonus > 0 ? $"ATTACK RATE +{zone.AttackSpeedBonus:P0}" :
            zone.RangeBonus > 0 ? $"TOWER RANGE +{zone.RangeBonus:P0}" :
            zone.DamageBonus > 0 ? $"DIRECT DAMAGE +{zone.DamageBonus:P0}" :
            $"ARMOR PIERCE +{zone.ArmorPierceBonus:0}";
        DrawText(batch, bonus, new Vector2(980, 546), ColorPalette.Ink, 0.68f);
        DrawText(batch, $"FIELD RADIUS {zone.Radius:0}", new Vector2(980, 570), ColorPalette.Muted, 0.56f);
        DrawText(batch, "Center one or two towers in this compact field", new Vector2(980, 602), ColorPalette.Ink, 0.51f);
        DrawText(batch, "to apply its focused bonus for the entire match.", new Vector2(980, 623), ColorPalette.Ink, 0.51f);
        DrawText(batch, "Node bonuses do not stack with other nodes.", new Vector2(980, 652), ColorPalette.Gold, 0.49f);
    }

    private void DrawGeneratorIntel(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, ChargeForgeInstance? active)
    {
        _targetButton = Rectangle.Empty;
        var definition = session.Content.Tactics.Generator;
        var level = active?.Level ?? definition.Levels[0];
        p.FillRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, active is null ? 202 : 156), definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, new Vector2(1000, 512), definition.Visual.Radius, definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, (active?.LevelIndex ?? 0) + 1, true, levelMarks: true);
        DrawText(batch, definition.DisplayName, new Vector2(1028, 486), ColorPalette.Ink, 0.86f);
        var generatorOwner = active is not null && session.IsCoOp ? $"   PLACED P{active.OwnerPlayerId}" : "";
        DrawText(batch, active is null ? $"{definition.PurchaseCost} CREDITS   GENERATOR" : $"LEVEL {active.LevelIndex + 1}   GENERATOR{generatorOwner}", new Vector2(1028, 508), ColorPalette.Muted, 0.60f);
        DrawText(batch, $"Produces 1 plate every {level.ProductionSeconds:0}s", new Vector2(980, 548), ColorPalette.Ink, 0.59f);
        DrawFittedText(batch, $"Storage {session.EmergencyInventory}/{level.Capacity}   Plate DAMAGE +{level.DefenseDamageBonus:P0}",
            new Vector2(980, 571), ColorPalette.Ink, 0.57f, 280);
        DrawText(batch, "Strength: renewable emergency reserves", new Vector2(980, 594), ColorPalette.Muted, 0.54f);

        if (active is null)
        {
            _upgradeButton = Rectangle.Empty;
            _sellButton = Rectangle.Empty;
            DrawText(batch, "Limit: high cost; produces no direct damage", new Vector2(980, 616), ColorPalette.Muted, 0.52f);
            var next = definition.Levels[1];
            DrawFittedText(batch, $"L2 {level.UpgradeCost}: {next.ProductionSeconds:0}s   CAP {next.Capacity}   DAMAGE +{next.DefenseDamageBonus:P0}",
                new Vector2(980, 638), ColorPalette.Violet, 0.52f, 280);
            DrawText(batch, session.TacticalPlacement == TacticalPlacementKind.ChargeForge ? "CLICK A BUILD ZONE   |   ESC TO CANCEL" : "G OR CLICK ABOVE TO PREPARE", new Vector2(980, 658), ColorPalette.Cobalt, 0.49f);
            return;
        }

        DrawFittedText(batch, active.CanUpgrade
            ? $"NEXT {active.UpgradeCost}: {definition.Levels[active.LevelIndex + 1].ProductionSeconds:0}s   CAP {definition.Levels[active.LevelIndex + 1].Capacity}   DAMAGE +{definition.Levels[active.LevelIndex + 1].DefenseDamageBonus:P0}"
            : "MAXIMUM LEVEL", new Vector2(980, 615), active.CanUpgrade ? ColorPalette.Violet : ColorPalette.Muted, 0.49f, 280);
        _upgradeButton = new Rectangle(1074, 646, 92, 30);
        _sellButton = new Rectangle(1172, 646, 94, 30);
        var canManage = !_readOnlyInspection;
        DrawButton(batch, p, _upgradeButton, active.CanUpgrade ? $"UP {active.UpgradeCost}" : "MAX", canManage && active.CanUpgrade && session.Economy.CanAfford(active.UpgradeCost), ColorPalette.Violet);
        DrawButton(batch, p, _sellButton, $"SELL {active.SellValue}", canManage, ColorPalette.Orange);
    }

    private void DrawPlacementStatus(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        var pointerOnMap = IsPointerOnMap(session.PlacementPosition);
        var valid = pointerOnMap && session.PlacementFailure == PlacementFailure.None;
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

        // The logo sits fully above the title; it no longer covers the words in the menu.
        var logo = new Vector2(640, 150);
        p.Circle(batch, logo, 78, ColorPalette.Navy);
        p.DrawShape(batch, logo, 53, "diamond", ColorPalette.Gold, ColorPalette.Paper, 2, true);
        p.Ring(batch, logo, 80, ColorPalette.Cyan, 4);
        p.DashedRing(batch, logo, 96, ColorPalette.Coral, 32, 3);
        p.FillRect(batch, new Rectangle(442, 142, 72, 8), ColorPalette.Cyan);
        p.FillRect(batch, new Rectangle(766, 142, 72, 8), ColorPalette.Coral);

        DrawText(batch, "MINIMAL BASTION", new Vector2(640, 295), ColorPalette.Ink, 2.2f, true);
        DrawText(batch, "A colorful geometric tower-defense game", new Vector2(640, 345), ColorPalette.Muted, 0.9f, true);
        var map = _maps.Count == 0
            ? (Id: "foundry_loop", Name: "Foundry Loop", PowerNodes: 0, Challenge: 2, Description: "A balanced tactical arena.", PathStyle: "road", Campaign: new CampaignIntelInfo(0, 0, "STANDARD", 1, 0))
            : _maps[_selectedMapIndex];
        var feature = map.PowerNodes > 0 ? $"{map.PowerNodes} SURGE NODES" : map.PathStyle.ToUpperInvariant();
        var mapSuffix = $"THREAT {map.Challenge}/5 | {feature}";
        var difficulty = _difficulties.Count == 0 ? null : _difficulties[_selectedDifficultyIndex];
        var challenge = _challenges.Count == 0 ? null : _challenges[_selectedChallengeIndex];
        DrawButton(batch, p, _mapButton, $"{_selectedMapIndex + 1}/{Math.Max(1, _maps.Count)}  {map.Name.ToUpperInvariant()}", true, ColorPalette.Berry);
        DrawButton(batch, p, _difficultyButton, (difficulty?.DisplayName ?? "Normal").ToUpperInvariant(), true,
            difficulty?.AccentColor ?? ColorPalette.Cobalt);
        DrawButton(batch, p, _challengeButton, (challenge?.MenuLabel ?? "Standard").ToUpperInvariant(), true,
            challenge?.AccentColor ?? ColorPalette.Cyan);
        DrawButton(batch, p, _continueButton, "LOAD SAVES", _saveAvailable, ColorPalette.Violet);
        DrawButton(batch, p, _mainMenuLibraryButton, "TOWER LIBRARY", true, ColorPalette.Cyan);
        DrawButton(batch, p, _playButton, "NEW GAME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _coOpButton, "ONLINE CO-OP", true, ColorPalette.Green);
        DrawButton(batch, p, _mainMenuSettingsButton, "SETTINGS", true, ColorPalette.Orange, ColorPalette.Ink);
        DrawButton(batch, p, _quitButton, "QUIT", true, ColorPalette.Coral);
        var difficultySummary = difficulty?.Description ?? "A balanced defense.";
        DrawFittedCenteredText(batch, $"{mapSuffix} | {map.Description}", new Vector2(640, 632), ColorPalette.Muted, 0.50f, 920);
        DrawFittedCenteredText(batch, map.Campaign.CompactSummary, new Vector2(640, 650), ColorPalette.Cyan, 0.44f, 940);
        DrawFittedCenteredText(batch, $"{(difficulty?.DisplayName ?? "Normal").ToUpperInvariant()} | {difficultySummary}",
            new Vector2(640, 669), difficulty?.AccentColor ?? ColorPalette.Cobalt, 0.44f, 920);
        DrawFittedCenteredText(batch, $"{(challenge?.DisplayName ?? "Standard").ToUpperInvariant()} | {challenge?.Description ?? "All systems available."}",
            new Vector2(640, 691), challenge?.AccentColor ?? ColorPalette.Cyan, 0.43f, 940);
    }

    private void DrawSettings(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "SETTINGS", new Vector2(640, 112), ColorPalette.Ink, 1.9f, true);
        DrawText(batch, "Display choices change output scaling only; the tactical canvas and palette stay authored.",
            new Vector2(640, 162), ColorPalette.Muted, 0.58f, true);

        DrawButton(batch, p, _windowModeButton, _settings.Fullscreen ? "DISPLAY  FULLSCREEN" : "DISPLAY  WINDOWED",
            true, ColorPalette.Cobalt);
        DrawButton(batch, p, _resolutionButton, $"OUTPUT  {_settings.WindowWidth} x {_settings.WindowHeight}",
            true, ColorPalette.Violet);
        DrawButton(batch, p, _vsyncButton, _settings.VSync ? "VSYNC  ON" : "VSYNC  OFF",
            true, ColorPalette.Green);
        DrawButton(batch, p, _effectsButton, _settings.ReducedEffects ? "EFFECTS  REDUCED" : "EFFECTS  FULL",
            true, ColorPalette.Cyan);
        DrawButton(batch, p, _volumeButton, $"SOUND EFFECTS  {MathF.Round(_settings.SfxVolume * 100):0}%  |  CLICK TO CHANGE",
            true, ColorPalette.Gold, ColorPalette.Ink);
        DrawButton(batch, p, _settingsBackButton, "BACK", true, ColorPalette.Coral);

        DrawText(batch, "Rendering remains crisp at every output size through the fixed high-resolution scene target.",
            new Vector2(640, 540), ColorPalette.Muted, 0.54f, true);
        DrawFittedCenteredText(batch, _settingsStatus, new Vector2(640, 574), ColorPalette.Cobalt, 0.50f, 820);
    }

    private void DrawSaveSlots(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, _saveSlotWriteMode ? "SAVE GAME" : "LOAD SAVES", new Vector2(640, 62), ColorPalette.Ink, 1.75f, true);
        DrawText(batch,
            _saveSlotWriteMode
                ? "Choose a slot. Overwriting occurs only after pressing the confirmation button."
                : "Solo saves resume immediately. Co-op saves reopen as a hosted game for your friend.",
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
            DrawText(batch, $"SLOT {slot.Slot}", new Vector2(rect.X + 22, rect.Y + 13), ColorPalette.Navy, 0.68f);

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
            var progress = slot.IsEndless ? $"ENDLESS {slot.CurrentWave}" : $"WAVE {slot.CurrentWave}/20";
            var difficultyName = _difficulties.FirstOrDefault(x => x.Id.Equals(slot.DifficultyId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.DifficultyId) ? "Hard" : slot.DifficultyId);
            var challengeName = _challenges.FirstOrDefault(x => x.Id.Equals(slot.ChallengeId, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? (string.IsNullOrWhiteSpace(slot.ChallengeId) ? "Standard" : slot.ChallengeId.Replace('_', ' '));
            DrawFittedText(batch, $"{(slot.IsCoOp ? "CO-OP" : "SOLO")}  |  {mapName.ToUpperInvariant()}  |  {difficultyName.ToUpperInvariant()}  |  {challengeName.ToUpperInvariant()}  |  {progress}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.58f, rect.Width - 164);
            var localTime = slot.SavedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(slot.SavedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : slot.SavedAtUtc.ToLocalTime();
            DrawText(batch, $"{localTime:g}  |  LIVES {slot.Lives}  |  CREDITS {slot.Credits}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.48f);
        }

        var selectedSlot = _saveSlots.FirstOrDefault(slot => slot.Slot == _selectedSaveSlot);
        var canConfirm = _saveSlotWriteMode || selectedSlot is { IsOccupied: true, Error: null };
        var confirmLabel = _saveSlotWriteMode
            ? selectedSlot is { IsOccupied: true } ? $"OVERWRITE SLOT {_selectedSaveSlot}" : $"SAVE TO SLOT {_selectedSaveSlot}"
            : $"LOAD SLOT {_selectedSaveSlot}";
        DrawButton(batch, p, _saveSlotConfirmButton, confirmLabel, canConfirm,
            _saveSlotWriteMode && selectedSlot is { IsOccupied: true } ? ColorPalette.Orange : ColorPalette.Green);
        var canDelete = selectedSlot is { IsOccupied: true };
        DrawButton(batch, p, _saveSlotDeleteButton,
            _saveSlotDeleteArmed ? $"CONFIRM DELETE {_selectedSaveSlot}" : $"DELETE SLOT {_selectedSaveSlot}",
            canDelete, _saveSlotDeleteArmed ? ColorPalette.Coral : ColorPalette.Orange);
        DrawText(batch, $"PAGE {_saveSlotPage + 1}/{pageCount}", new Vector2(640, 574), ColorPalette.Muted, 0.48f, true);
        DrawButton(batch, p, _saveSlotPreviousButton, "PREVIOUS", _saveSlotPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, "NEXT", _saveSlotPage + 1 < pageCount, ColorPalette.Cyan);
        DrawText(batch, _persistenceStatus, new Vector2(640, 654), ColorPalette.Muted, 0.52f, true);
    }

    private void DrawRunHistory(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "RUN HISTORY", new Vector2(640, 62), ColorPalette.Ink, 1.75f, true);
        DrawText(batch, "Campaign conclusions and endless records share one entry per defense.",
            new Vector2(640, 102), ColorPalette.Muted, 0.58f, true);

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

            var progress = entry.IsEndless ? $"ENDLESS {entry.CurrentWave}" : $"WAVE {entry.CurrentWave}/{entry.TotalWaves}";
            DrawFittedText(batch, $"{entry.MapName.ToUpperInvariant()}  |  {entry.DifficultyName.ToUpperInvariant()}  |  {entry.ChallengeName.ToUpperInvariant()}  |  {progress}",
                new Vector2(rect.X + 150, rect.Y + 12), ColorPalette.Ink, 0.56f, rect.Width - 164);
            var localTime = entry.CompletedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(entry.CompletedAtUtc, DateTimeKind.Utc).ToLocalTime()
                : entry.CompletedAtUtc.ToLocalTime();
            DrawText(batch,
                $"{localTime:g}  |  {(entry.IsCoOp ? "CO-OP" : "SOLO")}  |  LIVES {entry.Lives}/{entry.StartingLives}  |  KILLS {entry.Kills}  |  TOP {entry.TopTowerName.ToUpperInvariant()}",
                new Vector2(rect.X + 150, rect.Y + 39), ColorPalette.Muted, 0.44f);
        }

        DrawButton(batch, p, _runHistoryDeleteButton,
            _runHistoryDeleteArmed ? "CONFIRM DELETE" : "DELETE RUN",
            _selectedRunHistoryId is not null, _runHistoryDeleteArmed ? ColorPalette.Coral : ColorPalette.Orange);
        DrawText(batch, $"PAGE {_runHistoryPage + 1}/{pageCount}", new Vector2(640, 574), ColorPalette.Muted, 0.48f, true);
        DrawButton(batch, p, _saveSlotPreviousButton, "PREVIOUS", _runHistoryPage > 0, ColorPalette.Cyan);
        DrawButton(batch, p, _saveSlotBackButton, "BACK TO SAVES", true, ColorPalette.Violet);
        DrawButton(batch, p, _saveSlotNextButton, "NEXT", _runHistoryPage + 1 < pageCount, ColorPalette.Cyan);
        DrawText(batch, _runHistoryStatus, new Vector2(640, 654), ColorPalette.Muted, 0.52f, true);
    }

    private void DrawCoOpMenu(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "ONLINE CO-OP", new Vector2(640, 120), ColorPalette.Ink, 1.9f, true);
        DrawText(batch, "Direct internet play. The host forwards TCP 28741; the friend enters the address and code.", new Vector2(640, 164), ColorPalette.Muted, 0.60f, true);
        DrawFittedCenteredText(batch, $"HOST  {SelectedMapName.ToUpperInvariant()}  |  {SelectedDifficultyName.ToUpperInvariant()}  |  {SelectedChallengeName.ToUpperInvariant()}",
            new Vector2(640, 194), ColorPalette.Violet, 0.58f, 760);

        DrawText(batch, "HOST ADDRESS  (PUBLIC IP OR DNS)", new Vector2(500, 242), _editingJoinCode ? ColorPalette.Muted : ColorPalette.Cobalt, 0.54f);
        p.FillRect(batch, _joinHostField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinHostField, !_editingJoinCode ? ColorPalette.Cobalt : ColorPalette.CardOutline, 2);
        var hostText = string.IsNullOrWhiteSpace(_joinHostInput) ? "example.com:28741" : _joinHostInput;
        DrawText(batch, hostText, new Vector2(640, _joinHostField.Center.Y), string.IsNullOrWhiteSpace(_joinHostInput) ? ColorPalette.Muted : ColorPalette.Ink, 0.66f, true);

        DrawText(batch, "SIX-CHARACTER JOIN CODE", new Vector2(500, 310), _editingJoinCode ? ColorPalette.Cobalt : ColorPalette.Muted, 0.54f);
        p.FillRect(batch, _joinCodeField, ColorPalette.PanelAlt);
        p.DrawRect(batch, _joinCodeField, _editingJoinCode ? ColorPalette.Cobalt : _joinCodeInput.Length == 6 ? ColorPalette.Green : ColorPalette.CardOutline, 2);
        DrawText(batch, _joinCodeInput.PadRight(6, '_'), new Vector2(640, _joinCodeField.Center.Y), ColorPalette.Ink, 0.86f, true);

        DrawButton(batch, p, _hostCoOpButton, "HOST ONLINE GAME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _joinCoOpButton, "JOIN ONLINE GAME", CanJoinOnline, ColorPalette.Green);
        DrawButton(batch, p, _backButton, "BACK", true, ColorPalette.Violet);
        DrawText(batch, "Shared credits, lives, and tower control; placement is still marked P1/P2.", new Vector2(640, 590), ColorPalette.Muted, 0.56f, true);
        DrawText(batch, "Ctrl+V pastes an address; hold Backspace to erase. Middle-click the battlefield to ping.", new Vector2(640, 613), ColorPalette.Muted, 0.52f, true);
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
            DrawText(batch, CoOpLobbyCode, new Vector2(640, 305), ColorPalette.Cobalt, 1.8f, true);
        }
        DrawText(batch, CoOpLobbyDetail, new Vector2(640, 385), ColorPalette.Muted, 0.66f, true);
        DrawButton(batch, p, _backButton, "CANCEL", true, ColorPalette.Coral);
    }

    private void DrawCoOpReconnectOverlay(SpriteBatch batch, PrimitiveRenderer p)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 205));
        var panel = new Rectangle(370, 214, 540, 250);
        p.FillRect(batch, panel, ColorPalette.Panel);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 7), _coOpPeerConnected ? ColorPalette.Cyan : ColorPalette.Coral);
        p.DrawRect(batch, panel, ColorPalette.Ink, 2);
        DrawText(batch, CoOpLobbyTitle, new Vector2(640, 270), ColorPalette.Ink, 1.15f, true);
        DrawText(batch, CoOpLobbyDetail, new Vector2(640, 320), ColorPalette.Muted, 0.60f, true);
        if (!string.IsNullOrEmpty(CoOpLobbyCode))
        {
            DrawText(batch, "REJOIN CODE", new Vector2(640, 360), ColorPalette.Muted, 0.50f, true);
            DrawText(batch, CoOpLobbyCode, new Vector2(640, 392), ColorPalette.Cobalt, 1.2f, true);
        }
        DrawText(batch, "The match is paused and preserved.  ESC leaves the session.", new Vector2(640, 435), ColorPalette.Coral, 0.54f, true);
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
        DrawText(batch, victory ? "Campaign secured. Continue into escalating endless defense." : $"Defense collapsed during wave {session.CurrentWave}.", new Vector2(640, 142), ColorPalette.Muted, 0.72f, true);
        DrawFittedCenteredText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(640, 160), session.Challenge.AccentColor, 0.40f, 650);

        DrawResultStatCard(batch, p, new Rectangle(296, 172, 158, 58), "WAVE", session.IsEndlessMode ? $"{session.CurrentWave}+" : $"{session.CurrentWave}/{session.TotalWaves}", ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(472, 172, 158, 58), "LIVES", $"{session.Economy.Lives}/{session.Economy.StartingLives}", ColorPalette.Coral);
        DrawResultStatCard(batch, p, new Rectangle(648, 172, 158, 58), "KILLS", session.Economy.TotalKills.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(824, 172, 158, 58), "LEAKS", session.Economy.EscapedEnemies.ToString(), ColorPalette.Orange);

        DrawTowerContribution(batch, p, session.Statistics, new Rectangle(296, 250, 410, 298));
        DrawRunSummary(batch, p, session, new Rectangle(724, 250, 258, 298));

        if (victory)
        {
            DrawButton(batch, p, _resultContinueButton, "CONTINUE ENDLESS", true, ColorPalette.Green);
            DrawButton(batch, p, _resultRestartButton, session.IsCoOp ? "RESTART CO-OP" : "RESTART", true, ColorPalette.Cobalt);
            DrawButton(batch, p, _resultMenuButton, "MAIN MENU", true, ColorPalette.Violet);
        }
        else
        {
            DrawButton(batch, p, _resultContinueButton, "VIEW FIELD", true, ColorPalette.Cyan);
            DrawButton(batch, p, _resultRestartButton, session.IsCoOp ? "RESTART CO-OP" : "RESTART", true, ColorPalette.Cobalt);
            DrawButton(batch, p, _resultMenuButton, "MAIN MENU", true, ColorPalette.Violet);
        }
    }

    private void DrawDefeatFieldControls(SpriteBatch batch, PrimitiveRenderer p)
    {
        var label = new Rectangle(450, 9, 170, 38);
        p.FillRect(batch, label, ColorPalette.Coral);
        p.DrawRect(batch, label, ColorPalette.Ink, 2);
        DrawText(batch, "DEFEATED FIELD", new Vector2(label.Center.X, label.Center.Y), ColorPalette.Paper, 0.58f, true);
        DrawButton(batch, p, _fieldResultsButton, "VIEW RESULTS", true, ColorPalette.Cobalt);
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
        DrawSummaryLine(batch, "CREDITS EARNED", economy.TotalCreditsEarned.ToString(), rect.X + 14, rect.Y + 56);
        DrawSummaryLine(batch, "CREDITS SPENT", economy.TotalCreditsSpent.ToString(), rect.X + 14, rect.Y + 79);
        DrawSummaryLine(batch, "SALE RECOVERY", economy.SaleCreditsRecovered.ToString(), rect.X + 14, rect.Y + 102);
        DrawSummaryLine(batch, "PLATES DEPLOYED", stats.EmergencyDeployments.ToString(), rect.X + 14, rect.Y + 137);
        DrawSummaryLine(batch, "PLATE DAMAGE", stats.EmergencyDamage.ToString("0"), rect.X + 14, rect.Y + 160);
        DrawSummaryLine(batch, "FORGED CHARGES", stats.GeneratedCharges.ToString(), rect.X + 14, rect.Y + 183);
        DrawText(batch, "GREATEST LEAK THREAT", new Vector2(rect.X + 14, rect.Y + 218), ColorPalette.Muted, 0.48f);
        DrawText(batch, threat is null ? "NONE" : $"{threat.DisplayName.ToUpperInvariant()}  -{threat.LivesLost} LIVES", new Vector2(rect.X + 14, rect.Y + 238), threat is null ? ColorPalette.Green : ColorPalette.Coral, 0.56f);
        DrawText(batch, $"DEFENSE TIME  {elapsed.Minutes:00}:{elapsed.Seconds:00}", new Vector2(rect.X + 14, rect.Bottom - 27), ColorPalette.Cobalt, 0.52f);
    }

    private void DrawSummaryLine(SpriteBatch batch, string label, string value, int x, int y)
    {
        DrawText(batch, label, new Vector2(x, y), ColorPalette.Muted, 0.49f);
        DrawText(batch, value, new Vector2(x + 212, y), ColorPalette.Ink, 0.55f, true);
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
        DrawText(batch, PauseCheckpointStatus(session.CanSaveCheckpoint), new Vector2(640, 184), ColorPalette.Muted, 0.66f, true);
        DrawFittedCenteredText(batch, _persistenceStatus, new Vector2(640, 211), ColorPalette.Muted, 0.50f, 510);
        DrawButton(batch, p, _resumeButton, "RESUME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _towerLibraryButton, "TOWER LIBRARY", true, ColorPalette.Cyan);
        DrawButton(batch, p, _pauseSettingsButton, "SETTINGS", true, ColorPalette.Orange, ColorPalette.Ink);
        DrawButton(batch, p, _saveButton, session.CanSaveCheckpoint ? "SAVE TO SLOT" : "SAVE BETWEEN WAVES", session.CanSaveCheckpoint, ColorPalette.Green);
        DrawButton(batch, p, _loadButton, "LOAD SAVES", _saveAvailable, ColorPalette.Violet);
        DrawButton(batch, p, _restartButton, "RESTART", true, ColorPalette.Orange);
        DrawButton(batch, p, _mainMenuButton, "MAIN MENU", true, ColorPalette.Coral);
        DrawFittedCenteredText(batch,
            $"{session.Map.Definition.DisplayName.ToUpperInvariant()}  |  {session.Difficulty.DisplayName.ToUpperInvariant()}  |  {session.Challenge.DisplayName.ToUpperInvariant()}",
            new Vector2(640, 580), session.Challenge.AccentColor, 0.50f, 500);
    }

    private void DrawTowerLibrary(SpriteBatch batch, PrimitiveRenderer p, string returnDestination)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 235));
        var panel = new Rectangle(36, 24, 1208, 672);
        p.FillRect(batch, panel, ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(panel.X, panel.Y, panel.Width, 7), ColorPalette.Cyan);
        p.DrawRect(batch, panel, ColorPalette.Ink, 2);

        DrawText(batch, "TACTICAL LIBRARY", new Vector2(62, 48), ColorPalette.Navy, 1.25f);
        DrawText(batch, _libraryShowsCampaign
            ? "Every arena's exact authored wave roster, base scaling, and threat sequence."
            : _libraryShowsThreats
                ? "Base enemy profiles, rank rules, counterplay, and battlefield status symbols."
                : "Exact tower values. Click a Tier 2 doctrine to preview either final role.",
            new Vector2(62, 82), ColorPalette.Muted, 0.56f);
        DrawButton(batch, p, _towerLibraryTowerTabButton, "TOWERS", true,
            _libraryShowsThreats || _libraryShowsCampaign ? ColorPalette.PanelAlt : ColorPalette.Cyan,
            _libraryShowsThreats || _libraryShowsCampaign ? ColorPalette.Ink : ColorPalette.Navy);
        DrawButton(batch, p, _towerLibraryThreatTabButton, "THREATS", _libraryEnemies.Count > 0,
            _libraryShowsThreats ? ColorPalette.Coral : ColorPalette.PanelAlt,
            _libraryShowsThreats ? ColorPalette.Paper : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibraryCampaignTabButton, "CAMPAIGNS", _libraryCampaignWaves.Count > 0,
            _libraryShowsCampaign ? ColorPalette.Violet : ColorPalette.PanelAlt,
            _libraryShowsCampaign ? ColorPalette.Paper : ColorPalette.Ink);
        DrawButton(batch, p, _towerLibraryCloseButton, "BACK", true, ColorPalette.Violet);

        var listPanel = new Rectangle(56, 112, 264, 540);
        var detailPanel = new Rectangle(334, 112, 890, 540);
        p.FillRect(batch, listPanel, ColorPalette.Panel);
        p.DrawRect(batch, listPanel, ColorPalette.CardOutline, 1);
        p.FillRect(batch, detailPanel, ColorPalette.Panel);
        p.DrawRect(batch, detailPanel, ColorPalette.CardOutline, 1);
        DrawText(batch, _libraryShowsCampaign ? "SELECT ARENA" : _libraryShowsThreats ? "SELECT THREAT" : "SELECT TOWER",
            new Vector2(68, 122), ColorPalette.Navy, 0.63f);
        DrawText(batch, _libraryShowsCampaign ? $"1-{_maps.Count}" : _libraryShowsThreats ? "1-5" : "1-0",
            new Vector2(302, 122), ColorPalette.Muted, 0.48f, true);

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
            DrawText(batch, "NO TOWER DEFINITIONS AVAILABLE", new Vector2(detailPanel.Center.X, detailPanel.Center.Y), ColorPalette.Coral, 0.72f, true);
            return;
        }

        _towerLibraryIndex = Math.Clamp(_towerLibraryIndex, 0, towers.Count - 1);
        for (var index = 0; index < towers.Count; index++)
        {
            var definition = towers[index];
            var row = TowerLibraryRow(index);
            var selected = index == _towerLibraryIndex;
            p.FillRect(batch, row, selected ? ColorPalette.Tint(definition.Visual.PrimaryColor, 0.78f) : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected ? definition.Visual.PrimaryColor : ColorPalette.CardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(row.X + 22, row.Center.Y), 12, definition.Visual.Shape,
                definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
            DrawFittedText(batch, definition.DisplayName, new Vector2(row.X + 44, row.Y + 7), ColorPalette.Ink, 0.56f, 142);
            DrawText(batch, $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}", new Vector2(row.X + 44, row.Y + 24), ColorPalette.Muted, 0.43f);
            var hotkeyColor = selected
                ? ColorPalette.ReadableAccent(definition.Visual.PrimaryColor, ColorPalette.Tint(definition.Visual.PrimaryColor, 0.78f))
                : ColorPalette.Muted;
            DrawTextRight(batch, index == 9 ? "0" : (index + 1).ToString(), new Vector2(row.Right - 9, row.Y + 8), hotkeyColor, 0.43f);
        }

        DrawTowerLibraryDetails(batch, p, towers[_towerLibraryIndex], detailPanel);
        DrawText(batch, $"Click a tower or press 1-0.  ESC, right-click, or BACK returns to {returnDestination}.", new Vector2(640, 674), ColorPalette.Muted, 0.49f, true);
    }

    private void DrawCampaignLibrary(SpriteBatch batch, PrimitiveRenderer p, Rectangle detailPanel, string returnDestination)
    {
        _towerLibraryDoctrineAButton = Rectangle.Empty;
        _towerLibraryDoctrineBButton = Rectangle.Empty;
        if (_maps.Count == 0 || _libraryCampaignWaves.Count == 0)
        {
            DrawText(batch, "NO CAMPAIGN DEFINITIONS AVAILABLE", new Vector2(detailPanel.Center.X, detailPanel.Center.Y), ColorPalette.Coral, 0.72f, true);
            return;
        }

        _campaignLibraryMapIndex = Math.Clamp(_campaignLibraryMapIndex, 0, _maps.Count - 1);
        for (var index = 0; index < _maps.Count; index++)
        {
            var map = _maps[index];
            var row = CampaignLibraryMapRow(index);
            var selected = index == _campaignLibraryMapIndex;
            var accent = MapLibraryAccent(map.PathStyle);
            p.FillRect(batch, row, selected ? ColorPalette.Tint(accent, 0.80f) : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected ? accent : ColorPalette.CardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(row.X + 31, row.Y + 31), 15,
                map.PathStyle.Equals("conduit", StringComparison.OrdinalIgnoreCase) ? "diamond" :
                map.PathStyle.Equals("channel", StringComparison.OrdinalIgnoreCase) ? "triangle" : "square",
                accent, ColorPalette.Ink, Math.Max(1, map.Challenge - 1), false);
            DrawFittedText(batch, map.Name, new Vector2(row.X + 58, row.Y + 14), ColorPalette.Ink, 0.62f, 160);
            DrawText(batch, $"THREAT {map.Challenge}/5  |  {map.PathStyle.ToUpperInvariant()}", new Vector2(row.X + 58, row.Y + 39), ColorPalette.Muted, 0.43f);
            DrawFittedText(batch, $"{map.Campaign.TotalContacts:N0} contacts  |  peak {map.Campaign.PeakContacts}  |  boss W{map.Campaign.BossWave}",
                new Vector2(row.X + 14, row.Y + 67), ColorPalette.ReadableAccent(accent, selected ? ColorPalette.Tint(accent, 0.80f) : ColorPalette.PanelAlt),
                0.40f, row.Width - 28);
            DrawTextRight(batch, (index + 1).ToString(), new Vector2(row.Right - 10, row.Y + 9), selected ? accent : ColorPalette.Muted, 0.43f);
        }

        var selectedMap = _maps[_campaignLibraryMapIndex];
        if (!_libraryCampaignWaves.TryGetValue(selectedMap.Id, out var waves) || waves.Count == 0)
        {
            DrawText(batch, "NO AUTHORED WAVES FOR THIS ARENA", new Vector2(detailPanel.Center.X, detailPanel.Center.Y), ColorPalette.Coral, 0.72f, true);
            return;
        }

        var mapAccent = MapLibraryAccent(selectedMap.PathStyle);
        DrawText(batch, selectedMap.Name.ToUpperInvariant(), new Vector2(detailPanel.X + 18, detailPanel.Y + 16), ColorPalette.Ink, 0.96f);
        DrawTextRight(batch, $"THREAT {selectedMap.Challenge}/5  |  {selectedMap.PowerNodes} SURGE NODES  |  {selectedMap.PathStyle.ToUpperInvariant()} PATH",
            new Vector2(detailPanel.Right - 18, detailPanel.Y + 21), ColorPalette.ReadableAccent(mapAccent, ColorPalette.Panel), 0.50f);
        DrawFittedText(batch, selectedMap.Description, new Vector2(detailPanel.X + 18, detailPanel.Y + 48), ColorPalette.Muted, 0.48f, detailPanel.Width - 36);
        DrawFittedText(batch, selectedMap.Campaign.CompactSummary, new Vector2(detailPanel.X + 18, detailPanel.Y + 72), mapAccent, 0.43f, detailPanel.Width - 36);
        p.FillRect(batch, new Rectangle(detailPanel.X + 18, detailPanel.Y + 98, detailPanel.Width - 36, 2), mapAccent);

        for (var index = 0; index < Math.Min(20, waves.Count); index++)
        {
            var column = index / 10;
            var row = index % 10;
            var rect = new Rectangle(detailPanel.X + 18 + column * 436, detailPanel.Y + 112 + row * 41, 418, 37);
            DrawCampaignWaveRow(batch, p, rect, waves[index]);
        }

        DrawText(batch, $"Select an arena or press 1-{_maps.Count}.  Base wave multipliers are shown before difficulty.  ESC, right-click, or BACK returns to {returnDestination}.",
            new Vector2(640, 674), ColorPalette.Muted, 0.45f, true);
    }

    private void DrawCampaignWaveRow(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, CampaignWaveReference wave)
    {
        var accent = CampaignWaveAccent(wave);
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, 4, rect.Height), accent);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        DrawFittedText(batch, $"W{wave.Number:00}  {wave.Archetype.ToUpperInvariant()}", new Vector2(rect.X + 10, rect.Y + 4), ColorPalette.Navy, 0.46f, 190);
        DrawTextRight(batch, $"{wave.Contacts} | HP x{wave.HealthMultiplier:0.00} | SPD x{wave.SpeedMultiplier:0.00}",
            new Vector2(rect.Right - 8, rect.Y + 4), accent, 0.38f);
        DrawStrictFittedText(batch, $"{wave.Threats}  |  {wave.Roster}", new Vector2(rect.X + 10, rect.Y + 20),
            ColorPalette.Muted, 0.35f, rect.Width - 20, 0.26f);
    }

    private static Color MapLibraryAccent(string pathStyle) => pathStyle.ToLowerInvariant() switch
    {
        "channel" => ColorPalette.Cyan,
        "conduit" => ColorPalette.Violet,
        _ => ColorPalette.Gold
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
        if (_libraryEnemies.Count == 0)
        {
            DrawText(batch, "NO THREAT DEFINITIONS AVAILABLE", new Vector2(detailPanel.Center.X, detailPanel.Center.Y), ColorPalette.Coral, 0.72f, true);
            return;
        }

        _enemyLibraryIndex = Math.Clamp(_enemyLibraryIndex, 0, _libraryEnemies.Count - 1);
        for (var index = 0; index < _libraryEnemies.Count; index++)
        {
            var definition = _libraryEnemies[index];
            var row = EnemyLibraryRow(index);
            var selected = index == _enemyLibraryIndex;
            var selectedFill = ColorPalette.Tint(definition.Visual.PrimaryColor, 0.80f);
            p.FillRect(batch, row, selected ? selectedFill : ColorPalette.PanelAlt);
            p.DrawRect(batch, row, selected ? definition.Visual.PrimaryColor : ColorPalette.CardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(row.X + 32, row.Center.Y), Math.Min(19, definition.Visual.Radius), definition.Visual.Shape,
                definition.Visual.PrimaryColor, definition.Visual.AccentColor, definition.Visual.Marks, definition.Visual.Ring);
            DrawFittedText(batch, definition.DisplayName, new Vector2(row.X + 62, row.Y + 15), ColorPalette.Ink, 0.63f, 148);
            DrawText(batch, $"HP {definition.MaxHealth:0}  |  SPD {definition.Speed:0}", new Vector2(row.X + 62, row.Y + 43), ColorPalette.Muted, 0.45f);
            DrawTextRight(batch, (index + 1).ToString(), new Vector2(row.Right - 10, row.Y + 9),
                selected ? ColorPalette.ReadableAccent(definition.Visual.PrimaryColor, selectedFill) : ColorPalette.Muted, 0.43f);
        }

        DrawEnemyLibraryDetails(batch, p, _libraryEnemies[_enemyLibraryIndex], detailPanel);
        DrawText(batch, $"Click a threat or press 1-5.  Values precede wave and difficulty scaling.  ESC, right-click, or BACK returns to {returnDestination}.",
            new Vector2(640, 674), ColorPalette.Muted, 0.46f, true);
    }

    private void DrawEnemyLibraryDetails(SpriteBatch batch, PrimitiveRenderer p, EnemyDefinition definition, Rectangle panel)
    {
        var accent = definition.Visual.PrimaryColor;
        p.DrawShape(batch, new Vector2(panel.X + 42, panel.Y + 45), Math.Min(29, definition.Visual.Radius + 5), definition.Visual.Shape,
            accent, definition.Visual.AccentColor, definition.Visual.Marks, definition.Visual.Ring);
        DrawText(batch, definition.DisplayName.ToUpperInvariant(), new Vector2(panel.X + 84, panel.Y + 17), ColorPalette.Ink, 0.98f);
        DrawText(batch, ThreatRole(definition), new Vector2(panel.X + 84, panel.Y + 49),
            ColorPalette.ReadableAccent(accent, ColorPalette.Panel), 0.57f);
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
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 18, rankY, 270, 112), "STANDARD", accent,
        [
            "HEALTH x1.00  |  SPEED x1.00",
            "ARMOR +0  |  CONTROL RESIST 0%",
            "REWARD x1  |  BASE BREACH"
        ], 0.42f);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 306, rankY, 270, 112), "ELITE", ColorPalette.Gold,
        [
            "HEALTH x1.85  |  SPEED x1.07",
            "ARMOR +2  |  CONTROL RESIST 30%",
            "REWARD x2  |  BREACH +1 LIFE"
        ], 0.42f);
        DrawEnemyInfoCard(batch, p, new Rectangle(panel.X + 594, rankY, 278, 112), "BOSS", ColorPalette.Coral,
        [
            "HEALTH x4.50  |  SPEED x0.92",
            "ARMOR +4  |  CONTROL RESIST 60%",
            "REWARD x5  |  BREACH AT LEAST 10",
            "50% PHASE: SHIELD +12%; SPEED x1.28"
        ], 0.40f, 16);

        DrawText(batch, "BATTLEFIELD STATUS LANGUAGE", new Vector2(panel.X + 18, panel.Y + 420), ColorPalette.Navy, 0.62f);
        var statusY = panel.Y + 449;
        DrawStatusLegendEntry(batch, p, new Rectangle(panel.X + 18, statusY, 162, 70), "SLOW", "DASHED CYAN", "Movement reduced", ColorPalette.Slow, "ring");
        DrawStatusLegendEntry(batch, p, new Rectangle(panel.X + 186, statusY, 162, 70), "EXPOSE", "VIOLET DIAMOND", "All damage rises", ColorPalette.Violet, "diamond");
        DrawStatusLegendEntry(batch, p, new Rectangle(panel.X + 354, statusY, 162, 70), "BREAK", "GOLD CHEVRONS", "Armor reduced", ColorPalette.Gold, "break");
        DrawStatusLegendEntry(batch, p, new Rectangle(panel.X + 522, statusY, 162, 70), "BURN", "ORANGE INNER RING", "Damage; armor -2", ColorPalette.Orange, "circle");
        DrawStatusLegendEntry(batch, p, new Rectangle(panel.X + 690, statusY, 182, 70), "STUN", "GREEN SQUARES", "Movement halted", ColorPalette.Green, "stun");
    }

    private void DrawEnemyInfoCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string title, Color accent,
        IReadOnlyList<string> lines, float lineScale = 0.47f, int lineSpacing = 19)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 5), accent);
        p.DrawRect(batch, rect, ColorPalette.CardOutline, 1);
        DrawFittedText(batch, title, new Vector2(rect.X + 12, rect.Y + 14),
            ColorPalette.ReadableAccent(accent, ColorPalette.PanelAlt), 0.61f, rect.Width - 24);
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
        DrawFittedText(batch, symbol, new Vector2(rect.X + 9, rect.Y + 34), accent, 0.36f, rect.Width - 18);
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
        p.DrawShape(batch, new Vector2(panel.X + 36, panel.Y + 42), 20, definition.Visual.Shape,
            definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
        DrawText(batch, definition.DisplayName.ToUpperInvariant(), new Vector2(panel.X + 72, panel.Y + 16), ColorPalette.Ink, 0.94f);
        DrawText(batch, $"{TowerInfo.ShortRole(definition).ToUpperInvariant()}  |  BUILD {definition.PurchaseCost}  |  DEFAULT TARGET {definition.DefaultTargetMode.ToUpperInvariant()}",
            new Vector2(panel.X + 72, panel.Y + 43), ColorPalette.Muted, 0.53f);
        DrawFittedText(batch, TowerInfo.ProtocolSummary(definition), new Vector2(panel.X + 72, panel.Y + 64), ColorPalette.Coral, 0.43f, panel.Width - 90);
        DrawFittedText(batch, $"{TowerInfo.Strength(definition)}  |  {TowerInfo.Limitation(definition)}",
            new Vector2(panel.X + 18, panel.Y + 82), ColorPalette.ReadableAccent(definition.Visual.PrimaryColor, ColorPalette.Panel), 0.44f, panel.Width - 36);
        p.FillRect(batch, new Rectangle(panel.X + 18, panel.Y + 99, panel.Width - 36, 2), definition.Visual.PrimaryColor);

        var levelOne = definition.Levels[0];
        var levelTwo = definition.Levels.Count > 1 ? definition.Levels[1] : levelOne;
        if (definition.Tier2Doctrines.Count >= 2 && definition.Specializations.Count >= 2)
        {
            _towerLibraryDoctrineIndex = Math.Clamp(_towerLibraryDoctrineIndex, 0, 1);
            var doctrine = definition.Tier2Doctrines[_towerLibraryDoctrineIndex];
            const int doctrineWidth = 270;
            var topY = panel.Y + 112;
            _towerLibraryDoctrineAButton = new Rectangle(panel.X + 302, topY, doctrineWidth, 188);
            _towerLibraryDoctrineBButton = new Rectangle(panel.X + 586, topY, doctrineWidth, 188);
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18, topY, doctrineWidth, 188), definition,
                levelOne, "LEVEL 1", $"BUILD {definition.PurchaseCost}  |  TOTAL {definition.PurchaseCost}", definition.Visual.PrimaryColor);
            var firstDoctrine = definition.Tier2Doctrines[0];
            var secondDoctrine = definition.Tier2Doctrines[1];
            DrawTowerLibraryCard(batch, p, _towerLibraryDoctrineAButton, definition,
                levelTwo.WithDoctrine(firstDoctrine), $"L2 {firstDoctrine.DisplayName.ToUpperInvariant()}",
                $"UPGRADE {firstDoctrine.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToDoctrine(definition, firstDoctrine)}",
                ColorPalette.Cyan, firstDoctrine.Summary, _towerLibraryDoctrineIndex == 0);
            DrawTowerLibraryCard(batch, p, _towerLibraryDoctrineBButton, definition,
                levelTwo.WithDoctrine(secondDoctrine), $"L2 {secondDoctrine.DisplayName.ToUpperInvariant()}",
                $"UPGRADE {secondDoctrine.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToDoctrine(definition, secondDoctrine)}",
                ColorPalette.Violet, secondDoctrine.Summary, _towerLibraryDoctrineIndex == 1);

            for (var index = 0; index < 2; index++)
            {
                var specialization = definition.Specializations[index];
                var accent = index == 0 ? definition.Visual.PrimaryColor : ColorPalette.Violet;
                DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 436, panel.Y + 310, 418, 214), definition,
                    specialization.Level.WithDoctrine(doctrine), specialization.DisplayName.ToUpperInvariant(),
                    $"AFTER {doctrine.ShortLabel.ToUpperInvariant()} {specialization.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToSpecialization(definition, doctrine, specialization)}",
                    accent, specialization.Summary);
            }
            return;
        }
        if (definition.Specializations.Count > 0)
        {
            var topWidth = 418;
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18, panel.Y + 112, topWidth, 180), definition,
                levelOne, "LEVEL 1", $"BUILD {definition.PurchaseCost}  |  TOTAL {definition.PurchaseCost}", definition.Visual.PrimaryColor);
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 454, panel.Y + 112, topWidth, 180), definition,
                levelTwo, "LEVEL 2", $"UPGRADE {levelOne.UpgradeCost ?? 0}  |  TOTAL {TowerInfo.TotalCostToLevel(definition, 1)}", ColorPalette.Cyan);

            for (var index = 0; index < Math.Min(2, definition.Specializations.Count); index++)
            {
                var specialization = definition.Specializations[index];
                var accent = index == 0 ? definition.Visual.PrimaryColor : ColorPalette.Violet;
                DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 436, panel.Y + 308, topWidth, 216), definition,
                    specialization.Level, specialization.DisplayName.ToUpperInvariant(),
                    $"FINAL {specialization.UpgradeCost}  |  TOTAL {TowerInfo.TotalCostToSpecialization(definition, specialization)}",
                    accent, specialization.Summary);
            }
            return;
        }

        const int cardWidth = 276;
        for (var index = 0; index < Math.Min(3, definition.Levels.Count); index++)
        {
            var level = definition.Levels[index];
            var incrementalCost = index == 0 ? definition.PurchaseCost : definition.Levels[index - 1].UpgradeCost ?? 0;
            var costKind = index == 0 ? "BUILD" : "UPGRADE";
            var accent = index switch { 0 => definition.Visual.PrimaryColor, 1 => ColorPalette.Cyan, _ => ColorPalette.Violet };
            DrawTowerLibraryCard(batch, p, new Rectangle(panel.X + 18 + index * 290, panel.Y + 112, cardWidth, 412), definition,
                level, $"LEVEL {index + 1}", $"{costKind} {incrementalCost}  |  TOTAL {TowerInfo.TotalCostToLevel(definition, index)}", accent);
        }
    }

    private void DrawTowerLibraryCard(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, TowerDefinition definition,
        TowerLevelDefinition level, string title, string cost, Color accent, string? summary = null, bool selected = false)
    {
        p.FillRect(batch, rect, ColorPalette.PanelAlt);
        p.FillRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 5), accent);
        p.DrawRect(batch, rect, selected ? accent : ColorPalette.CardOutline, selected ? 3 : 1);
        DrawFittedText(batch, title, new Vector2(rect.X + 12, rect.Y + 14), ColorPalette.Navy, 0.66f, rect.Width - 24);
        DrawFittedText(batch, cost, new Vector2(rect.X + 12, rect.Y + 38),
            ColorPalette.ReadableAccent(accent, ColorPalette.PanelAlt), 0.48f, rect.Width - 24);
        var dividerY = rect.Y + 62;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            DrawFittedText(batch, summary, new Vector2(rect.X + 12, rect.Y + 59), ColorPalette.Muted, 0.44f, rect.Width - 24);
            dividerY = rect.Y + 82;
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

    private static Rectangle TowerLibraryRow(int index) => new(66, 148 + index * 49, 244, 44);
    private static Rectangle EnemyLibraryRow(int index) => new(66, 148 + index * 96, 244, 82);
    private static Rectangle CampaignLibraryMapRow(int index) => new(66, 148 + index * 116, 244, 102);

    private void DrawButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled, Color fillColor, Color? textColor = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        var background = enabled ? fillColor : ColorPalette.Disabled;
        p.FillRect(batch, rect, background);
        p.DrawRect(batch, rect, enabled ? ColorPalette.Ink : ColorPalette.Muted, enabled ? 2 : 1);
        var scale = 0.65f;
        var measured = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measured > rect.Width - 12) scale *= (rect.Width - 12) / measured;
        DrawText(batch, text, new Vector2(rect.Center.X, rect.Center.Y), enabled ? textColor ?? ColorPalette.Paper : ColorPalette.Muted, MathF.Max(0.38f, scale), true);
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
        DrawText(batch, text, position, color, MathF.Max(0.36f, scale));
    }

    private void DrawStrictFittedText(SpriteBatch batch, string text, Vector2 position, Color color, float scale,
        float maximumWidth, float minimumScale)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        DrawText(batch, text, position, color, MathF.Max(minimumScale, scale));
    }

    private void DrawFittedCenteredText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, float maximumWidth)
    {
        var measuredWidth = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measuredWidth > maximumWidth)
            scale *= maximumWidth / measuredWidth;
        DrawText(batch, text, position, color, MathF.Max(0.30f, scale), true);
    }

    private void DrawTextRight(SpriteBatch batch, string text, Vector2 position, Color color, float scale)
    {
        var size = _font.MeasureString(text);
        batch.DrawString(_font, text, position, color, 0, new Vector2(size.X, 0), scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }
}
