using MinimalBastion.Audio;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Debugging;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace MinimalBastion;

public sealed class Game1 : Game
{
    private static readonly string[] GameplayMusicAssets =
    [
        "Audio/Music/BassyRollIn",
        "Audio/Music/ChillStroll",
        "Audio/Music/FocusedDanceParty",
        "Audio/Music/IcyInvestigation",
        "Audio/Music/KickbackHall",
        "Audio/Music/MildlyUpbeatArpeggios",
        "Audio/Music/NightclubHalo",
        "Audio/Music/QuietInvestigation",
        "Audio/Music/SneakFocusMission",
        "Audio/Music/XylophoneBallad"
    ];
    private const int OnlineCoOpPort = 28741;
    // Commands are scheduled 200ms ahead to tolerate ordinary friend-to-friend
    // jitter. Simulation runs locally on each peer; only commands and periodic
    // checksums cross the link.
    private const int NetworkInputDelayTicks = DeterministicSessionRunner.SimulationTicksPerSecond / 5;
    private const int NetworkChecksumIntervalTicks = DeterministicSessionRunner.SimulationTicksPerSecond;
    private const int NetworkChecksumHistoryTicks = DeterministicSessionRunner.SimulationTicksPerSecond * 12;
    private const float WindowSizeSaveDelaySeconds = 0.45f;
    private readonly GraphicsDeviceManager _graphics;
    private readonly UserSettings _settings;
    private readonly ViewportTransform _viewportTransform = new();
    private InputRouter _input = null!;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _sceneTarget = null!;
#if !BLAZORGL
    private int _sceneRenderScale = GameConstants.RenderScale;
#endif
    private PrimitiveRenderer _primitives = null!;
#if BLAZORGL
    private RasterizerState _browserScissorRasterizer = null!;
#endif
    private GameRenderer _gameRenderer = null!;
    private UIManager _ui = null!;
    private DebugOverlay _debug = null!;
    private AudioManager? _audio;
    private GameContent _content = null!;
    private GameSession? _session;
    private GameSession? _historyInspectionSession;
    private GameState _state = GameState.MainMenu;
    private string? _loadError;
    private string _contentFingerprint = "";
    private LanCoOpHost? _coOpHost;
    private LanCoOpConnection? _coOpConnection;
    private CancellationTokenSource? _networkCancellation;
    private Task<LanCoOpConnection>? _connectionTask;
    private Task<CoOpEnvelope?>? _receiveTask;
    private readonly List<Task> _pendingNetworkSends = new();
    private readonly Dictionary<long, string> _networkChecksums = new();
    private readonly Dictionary<long, string> _remoteNetworkChecksums = new();
    private readonly HashSet<long> _repliedChecksumTicks = new();
    private readonly CoOpWaveReadyCoordinator _coOpWaveReady = new();
    private readonly CoOpCursorTracker _coOpCursor = new();
    private readonly CoOpHeartbeatMonitor _coOpHeartbeat = new();
    private AuthoritativeCommandHost? _authoritativeCommands;
    private DeterministicSessionRunner? _networkRunner;
    private bool _isNetworkHost;
    private bool _networkStarted;
    private bool _networkResyncing;
    private int _localPlayerId = 1;
    private long _nextClientRequestId = 1;
    private long _lastSyncTick = -1;
    private long _checksumSnapshotFenceTick = -1;
    private OnlineHostEndpoint? _joinEndpoint;
    private string _joinCode = "";
    private float _reconnectRetryRemaining;
    private int _lastAutosaveAttemptedWave = -1;
    private int? _activeSaveSlot;
    private GameState _saveSlotReturnState = GameState.MainMenu;
    private bool _saveSlotWriteMode;
    private GameState _settingsReturnState = GameState.MainMenu;
    private string _lastRecordedResultKey = "";
    private bool _windowSizeSavePending;
    private float _windowSizeSaveRemaining;
    private string? _pendingDefenseMapId;
    private string? _pendingDefenseDifficultyId;
    private string? _pendingDefenseChallengeId;
    private int? _pendingDefenseSaveSlot;
    private bool _pendingMainMenuTransition;
    private int _loadingTransitionPhase;
#if BLAZORGL
    private int _browserDisplayRevision = -1;
    private int _browserBackBufferWidth;
    private int _browserBackBufferHeight;
#endif

    public Game1()
    {
        _settings = UserSettingsStore.Load();
#if BLAZORGL
        // Browsers require a fresh user gesture for each fullscreen session.
        _settings.Fullscreen = false;
#endif
        var desktopMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var outputSize = _settings.ResolveBackBufferSize(desktopMode.Width, desktopMode.Height);
        if (!_settings.Fullscreen)
            outputSize = WindowLayout.FitClientInsideDesktop(outputSize.Width, outputSize.Height, desktopMode.Width, desktopMode.Height);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = outputSize.Width,
            PreferredBackBufferHeight = outputSize.Height,
            SynchronizeWithVerticalRetrace = _settings.VSync,
            // The fixed 2x scene target already provides edge supersampling.
            // Backbuffer MSAA is unnecessary for the final textured composite.
            PreferMultiSampling = false,
            IsFullScreen = _settings.Fullscreen,
            HardwareModeSwitch = false
        };
#if BLAZORGL
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Minimal Bastion";
        // Variable presentation permits 60/120/144 Hz rendering. Co-op gameplay
        // remains on its own deterministic fixed-rate runner.
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        _viewportTransform.Update(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _input = new InputRouter(_viewportTransform);
        Window.TextInput += (_, args) =>
        {
            if (PlatformServices.InputFocusReader?.Invoke() ?? IsActive) _input.QueueTextInput(args.Character);
        };
        Deactivated += (_, _) =>
        {
            _input.LoseWindowFocus();
            PersistPendingWindowSize();
        };
        Window.ClientSizeChanged += (_, _) => CaptureWindowedClientSize(scheduleSave: true);
        base.Initialize();
        CaptureWindowedClientSize(scheduleSave: true);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
#if BLAZORGL
        _browserScissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };
#endif
        EnsureSceneRenderResources();
        _gameRenderer = new GameRenderer();
        try
        {
            var contentDirectory = Path.Combine(AppContext.BaseDirectory, "ContentData");
            _content = new ContentLoader(contentDirectory).Load();
#if BLAZORGL
            _contentFingerprint = "browser-solo";
#else
            _contentFingerprint = BuildFingerprint.Compute(contentDirectory);
#endif
            var font = Content.Load<SpriteFont>("Fonts/Interface");
            _ui = new UIManager(font);
            _ui.ConfigureMaps(_content.Maps.Values, _content.WaveSets, _content.Enemies);
            _ui.ConfigureDifficulties(_content.Difficulties.Values);
            _ui.ConfigureChallenges(_content.Challenges.Values);
            _ui.ConfigureTowerLibrary(_content.Towers.Values, _content.Enemies.Values, _content.Tactics);
            _ui.ConfigureMainMenuBattle(_content);
            var desktopMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _ui.ConfigureSettings(_settings);
            _ui.SetSaveState(SaveSlotsExistSafely());
            RefreshRunHistoryCache();
            _debug = new DebugOverlay(font);
            _gameRenderer.ReducedEffects = _settings.ReducedEffects;
            SoundEffect? menuMusic = null;
            try { menuMusic = Content.Load<SoundEffect>("Audio/MainMenuLoop"); }
            catch { }
            var gameplayMusic = new List<Song>(GameplayMusicAssets.Length);
            foreach (var asset in GameplayMusicAssets)
            {
                try { gameplayMusic.Add(Content.Load<Song>(asset)); }
                catch { }
            }
            _audio = AudioManager.TryCreate(menuMusic, gameplayMusic);
            if (_audio is not null)
            {
                _audio.SfxVolume = _settings.SfxVolume;
                _audio.MusicVolume = _settings.MusicVolume;
            }
        }
        catch (Exception exception)
        {
            _loadError = exception.Message;
            // Keep a minimal UI alive so a missing data/font error is visible instead of failing silently.
            var font = Content.Load<SpriteFont>("Fonts/Interface");
            _ui = new UIManager(font);
            _debug = new DebugOverlay(font);
        }
    }

    protected override void Update(GameTime gameTime)
    {
#if BLAZORGL
        SynchronizeBrowserDisplayState();
#endif
        UpdateViewportTransform();
        var input = _input.Update(PlatformServices.InputFocusReader?.Invoke() ?? IsActive);
        if (input.FullscreenPressed) ToggleFullscreenFromHotkey();
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        AdvanceWindowSizePersistence(elapsedSeconds);
        _ui.AdvanceVisualTime(elapsedSeconds);
        if (_state == GameState.MainMenu) _ui.AdvanceMainMenuBattle(elapsedSeconds);
        _audio?.Update(elapsedSeconds);
        _coOpCursor.Advance(elapsedSeconds);
        SyncRemoteCoOpCursor();

        if (_loadError is not null)
        {
            if (input.EscapePressed) Exit();
            base.Update(gameTime);
            return;
        }

        switch (_state)
        {
            case GameState.MainMenu:
                HandleMenuAction(WithUiAudio(_ui.HandleMainMenu(input)));
                break;
            case GameState.GameSetup:
                HandleMenuAction(WithUiAudio(_ui.HandleGameSetup(input)));
                break;
            case GameState.LoadingTransition:
                AdvanceLoadingTransition();
                break;
            case GameState.TowerLibrary:
                HandleMenuAction(WithUiAudio(_ui.HandleTitleTowerLibrary(input)));
                break;
            case GameState.Settings:
                HandleSettingsAction(WithSettingsUiAudio(_ui.HandleSettingsInput(input), input));
                break;
            case GameState.SaveSlots:
                HandleSaveSlotAction(WithUiAudio(_ui.HandleSaveSlots(input)));
                break;
            case GameState.RunHistory:
                HandleRunHistoryAction(WithUiAudio(_ui.HandleRunHistory(input)));
                break;
            case GameState.RunHistoryField:
                UpdateRunHistoryField(input);
                break;
            case GameState.CoOpMenu:
                HandleCoOpMenuAction(WithUiAudio(_ui.HandleCoOpMenu(input)));
                break;
            case GameState.CoOpLobby:
                UpdateCoOpLobby(input);
                break;
            case GameState.CoOpReconnect:
                UpdateCoOpReconnect(input, gameTime);
                break;
            case GameState.Playing:
                UpdatePlaying(input, gameTime);
                break;
            case GameState.Paused:
                if (_session is not null)
                {
                    HandlePauseAction(WithUiAudio(_ui.HandlePausedInput(input, _session)));
                    if (_state == GameState.Paused) _session.UpdatePausedIntermission(elapsedSeconds);
                }
                break;
            case GameState.Victory:
            case GameState.Defeat:
                var resultState = _state;
                if (_networkRunner is not null) PollNetwork();
                if (_state != resultState) break;
                HandleResultAction(WithUiAudio(_ui.HandleResultInput(input, resultState == GameState.Victory)));
                if (_networkRunner is not null && _session is not null && _state == resultState)
                {
                    _networkRunner.Advance((float)gameTime.ElapsedGameTime.TotalSeconds);
                    if (!_session.IsVictory && !_session.IsDefeat) _state = GameState.Playing;
                }
                break;
            case GameState.DefeatField:
                UpdateDefeatField(input, gameTime);
                break;
        }


        var connectionTimedOut = _coOpConnection is not null && _coOpHeartbeat.Advance(elapsedSeconds);
        _ui?.SetCoOpLinkSilence(_coOpHeartbeat.SilenceSeconds);
        if (connectionTimedOut)
            HandleConnectionLoss("CONNECTION TIMED OUT", "No co-op traffic was received for 15 seconds. The match is preserved for reconnection.");

        RecordTerminalRun();

        base.Update(gameTime);
    }

    private void UpdateDefeatField(InputSnapshot input, GameTime gameTime)
    {
        if (_session is null) return;
        if (_networkRunner is not null)
        {
            PollNetwork();
            if (_state != GameState.DefeatField || _session is null) return;
            _networkRunner.Advance((float)gameTime.ElapsedGameTime.TotalSeconds);
        }
        if (WithUiAudio(_ui.HandleDefeatFieldInput(input)) == UiAction.ViewResults)
        {
            _ui.PrepareResultScreen();
            _state = GameState.Defeat;
            return;
        }
        _session.HandleInspectionInput(input);
    }

    private void UpdatePlaying(InputSnapshot input, GameTime gameTime)
    {
        if (_session is null) return;
        if (_networkRunner is not null) PollNetwork();
        if (_session is null || _state != GameState.Playing) return;
        if (_networkRunner is not null && input.PingPressed && input.IsMouseOverLogicalCanvas &&
            input.MousePosition.X >= 0 && input.MousePosition.X < GameConstants.MapWidth &&
            input.MousePosition.Y >= GameConstants.TopBarHeight && input.MousePosition.Y <= GameConstants.LogicalHeight)
            SendCoOpPing(input.MousePosition);
        _debug.Update(input);
        Action<GameCommand>? commandSink = _networkRunner is null ? null : SubmitLocalNetworkCommand;
        var gameplayOverlayWasOpen = _ui.IsGameplayOverlayOpen;
        var action = WithUiAudio(_ui.HandleGameplayInput(input, _session, commandSink, _localPlayerId));
        if (_networkRunner is null && action == UiAction.Pause)
        {
            _ui.PreparePauseScreen();
            _state = GameState.Paused;
            return;
        }
        if (_networkRunner is not null && action == UiAction.Restart)
        {
            Restart();
            return;
        }
        if (_networkRunner is not null && action == UiAction.MainMenu)
        {
            BeginMainMenuTransition();
            return;
        }

        // A local library overlay in co-op must consume its opening, browsing,
        // and closing input without emitting a semantic action every frame.
        // Track both sides of the transition so the same keystroke never leaks
        // through to battlefield controls.
        if (!gameplayOverlayWasOpen && !_ui.IsGameplayOverlayOpen && action != UiAction.TowerLibrary &&
            !(_session.IsCoOp && _session.IsCoOpPaused))
            _session.HandleWorldInput(input, commandSink, _localPlayerId);
        SendLocalCoOpCursor(input);
        if (_networkRunner is null)
        {
            _session.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            _session.TryAutoStartNextWave(_settings.AutoStartWaves, _settings.AutoStartDelaySeconds);
        }
        else
        {
            _networkRunner.Advance((float)gameTime.ElapsedGameTime.TotalSeconds);
            if (_coOpWaveReady.StartQueued && _session.Waves.IsActive) ResetCoOpWaveReadyState(_isNetworkHost);
        }
        if (_session.IsVictory)
        {
            _ui.PrepareResultScreen();
            _state = GameState.Victory;
        }
        else if (_session.IsDefeat)
        {
            _ui.PrepareResultScreen();
            _state = GameState.Defeat;
        }
        else if ((_networkRunner is null || _isNetworkHost) && _session.CanSaveCheckpoint && _session.CurrentWave > 0 && _session.CurrentWave != _lastAutosaveAttemptedWave)
            SaveCheckpoint(true);
    }

    private void HandleMenuAction(UiAction action)
    {
        if (action == UiAction.OpenSoloSetup)
        {
            _ui.PrepareGameSetup(false);
            _state = GameState.GameSetup;
        }
        else if (action == UiAction.OpenCoOpSetup)
        {
            _ui.PrepareGameSetup(true);
            _state = GameState.GameSetup;
        }
        else if (action == UiAction.Play)
        {
            BeginDefenseLoading(_ui.SelectedMapId, _ui.SelectedDifficultyId, _ui.SelectedChallengeId);
        }
        else if (action == UiAction.HostCoOp) BeginHostingCoOp();
        else if (action == UiAction.TowerLibrary) _state = GameState.TowerLibrary;
        else if (action == UiAction.Settings)
        {
            _settingsReturnState = GameState.MainMenu;
            _state = GameState.Settings;
        }
        else if (action == UiAction.LoadGame) OpenSaveSlots(false, GameState.MainMenu);
        else if (action == UiAction.MainMenu) _state = GameState.MainMenu;
        else if (action == UiAction.CoOp) _state = GameState.CoOpMenu;
        else if (action == UiAction.Exit) Exit();
    }

    private void BeginDefenseLoading(string mapId, string difficultyId, string challengeId)
    {
        PlatformServices.RuntimeStageSetter?.Invoke("defense loading");
        if (!PlatformCapabilities.StagedRuntimeTransitions)
        {
            InitializeDefenseSession(mapId, difficultyId, challengeId);
            _state = GameState.Playing;
            PlatformServices.RuntimeStageSetter?.Invoke("gameplay");
            return;
        }

        _pendingDefenseMapId = mapId;
        _pendingDefenseDifficultyId = difficultyId;
        _pendingDefenseChallengeId = challengeId;
        _pendingDefenseSaveSlot = null;
        _pendingMainMenuTransition = false;
        _loadingTransitionPhase = 0;
        BeginLoadingPresentation("LOADING DEFENSE SYSTEMS", "PREPARING SELECTED ARENA");
        _state = GameState.LoadingTransition;
    }

    private void AdvanceLoadingTransition()
    {
        if (_pendingMainMenuTransition)
        {
            AdvanceMainMenuTransition();
            return;
        }
        if (_pendingDefenseSaveSlot is int saveSlot)
        {
            AdvanceSaveSlotLoading(saveSlot);
            return;
        }

        switch (_loadingTransitionPhase)
        {
            case 0:
                SetLoadingPresentationStatus("INITIALIZING WAVE SYSTEMS");
                _loadingTransitionPhase = 1;
                return;
            case 1:
                InitializeDefenseSession(
                    _pendingDefenseMapId ?? throw new InvalidOperationException("Defense loading has no pending map."),
                    _pendingDefenseDifficultyId ?? throw new InvalidOperationException("Defense loading has no pending difficulty."),
                    _pendingDefenseChallengeId ?? throw new InvalidOperationException("Defense loading has no pending directive."));
                SetLoadingPresentationStatus("RENDERING DEFENSE GRID");
                _loadingTransitionPhase = 2;
                return;
            case 2:
                // Keep the transition visible for one complete frame after the
                // session is initialized before exposing interactive play.
                SetLoadingPresentationStatus("FINALIZING TOWER WORKSHOP");
                _loadingTransitionPhase = 3;
                return;
            default:
                _pendingDefenseMapId = null;
                _pendingDefenseDifficultyId = null;
                _pendingDefenseChallengeId = null;
                _state = GameState.Playing;
                PlatformServices.LoadingTransitionCompleter?.Invoke();
                PlatformServices.RuntimeStageSetter?.Invoke("gameplay");
                return;
        }
    }

    private void BeginSaveSlotLoading(int slot)
    {
        PlatformServices.RuntimeStageSetter?.Invoke("save loading");
        _pendingDefenseMapId = null;
        _pendingDefenseDifficultyId = null;
        _pendingDefenseChallengeId = null;
        _pendingDefenseSaveSlot = slot;
        _pendingMainMenuTransition = false;
        _loadingTransitionPhase = 0;
        BeginLoadingPresentation("LOADING SAVED DEFENSE", "PREPARING SAVE SLOT");
        _state = GameState.LoadingTransition;
    }

    private void AdvanceSaveSlotLoading(int slot)
    {
        switch (_loadingTransitionPhase)
        {
            case 0:
                SetLoadingPresentationStatus("READING SAVE SLOT");
                _loadingTransitionPhase = 1;
                return;
            case 1:
                if (!LoadSaveSlot(slot, hostCoOp: false, deferPlaying: true))
                {
                    _pendingDefenseSaveSlot = null;
                    PlatformServices.LoadingTransitionCompleter?.Invoke();
                    return;
                }
                SetLoadingPresentationStatus("RENDERING SAVED DEFENSE");
                _loadingTransitionPhase = 2;
                return;
            case 2:
                SetLoadingPresentationStatus("FINALIZING TOWER WORKSHOP");
                _loadingTransitionPhase = 3;
                return;
            default:
                _pendingDefenseSaveSlot = null;
                _state = GameState.Playing;
                PlatformServices.LoadingTransitionCompleter?.Invoke();
                PlatformServices.RuntimeStageSetter?.Invoke("gameplay");
                return;
        }
    }

    private void BeginMainMenuTransition()
    {
        if (!PlatformCapabilities.StagedRuntimeTransitions)
        {
            CleanupNetwork();
            _state = GameState.MainMenu;
            PlatformServices.RuntimeStageSetter?.Invoke("main menu");
            return;
        }

        PlatformServices.RuntimeStageSetter?.Invoke("menu loading");
        _pendingDefenseMapId = null;
        _pendingDefenseDifficultyId = null;
        _pendingDefenseChallengeId = null;
        _pendingDefenseSaveSlot = null;
        _pendingMainMenuTransition = true;
        _loadingTransitionPhase = 0;
        BeginLoadingPresentation("RETURNING TO MAIN MENU", "CLOSING ACTIVE DEFENSE");
        _state = GameState.LoadingTransition;
    }

    private void AdvanceMainMenuTransition()
    {
        switch (_loadingTransitionPhase)
        {
            case 0:
                SetLoadingPresentationStatus("PREPARING COMMAND CENTER");
                _loadingTransitionPhase = 1;
                return;
            case 1:
                CleanupNetwork();
                SetLoadingPresentationStatus("FINALIZING MAIN MENU");
                _loadingTransitionPhase = 2;
                return;
            default:
                _pendingMainMenuTransition = false;
                _state = GameState.MainMenu;
                PlatformServices.LoadingTransitionCompleter?.Invoke();
                PlatformServices.RuntimeStageSetter?.Invoke("main menu");
                return;
        }
    }

    private void BeginLoadingPresentation(string title, string status)
    {
        _ui.BeginLoadingTransition(title, status);
        PlatformServices.LoadingTransitionSetter?.Invoke(title, status);
    }

    private void SetLoadingPresentationStatus(string status)
    {
        _ui.SetLoadingTransitionStatus(status);
        PlatformServices.LoadingTransitionSetter?.Invoke(_ui.LoadingTransitionTitle, status);
    }

    private void InitializeDefenseSession(string mapId, string difficultyId, string challengeId)
    {
        AssignSession(new GameSession(_content, mapId, difficultyId, challengeId));
        _lastAutosaveAttemptedWave = -1;
        _activeSaveSlot = null;
    }

    private UiAction WithUiAudio(UiAction action)
    {
        if (action == UiAction.None || _audio is null) return action;
        if (action is UiAction.DeleteSaveSlot or UiAction.DeleteRunHistory)
            _audio.PlayUiDelete();
        else if (action is UiAction.CloseSettings or UiAction.CloseSaveSlots or UiAction.CloseRunHistory or
                 UiAction.MainMenu or UiAction.Exit)
            _audio.PlayUiBack();
        else
            _audio.PlayUiConfirm();
        return action;
    }

    private UiAction WithSettingsUiAudio(UiAction action, InputSnapshot input)
    {
        if (_ui.SelectedSettingsIndex is 3 or 4 &&
            action is (UiAction.PreviewSettings or UiAction.ApplySettings))
        {
            if (input.LeftPressed) _audio?.PlayUiConfirm();
            return action;
        }
        return WithUiAudio(action);
    }

    private void HandleCoOpMenuAction(UiAction action)
    {
        switch (action)
        {
            case UiAction.OpenCoOpSetup:
                _ui.PrepareGameSetup(true);
                _state = GameState.GameSetup;
                break;
            case UiAction.JoinCoOp:
                BeginJoiningCoOp(_ui.JoinHostInput, _ui.JoinCodeInput);
                break;
            case UiAction.MainMenu:
                CleanupNetwork();
                _state = GameState.MainMenu;
                break;
        }
    }

    private void BeginHostingCoOp(GameSession? restoredSession = null, int? saveSlot = null,
        bool preserveMusic = false)
    {
        CleanupNetwork(preserveMusic);
        try
        {
            AssignSession(restoredSession, preserveMusic);
            _activeSaveSlot = saveSlot is > SaveSlotRepository.AutosaveSlot ? saveSlot : null;
            _lastAutosaveAttemptedWave = restoredSession?.CurrentWave ?? -1;
            _networkCancellation = new CancellationTokenSource();
            _coOpHost = new LanCoOpHost(OnlineCoOpPort, buildFingerprint: _contentFingerprint);
            _coOpHost.Start();
            _isNetworkHost = true;
            _localPlayerId = 1;
            _authoritativeCommands = new AuthoritativeCommandHost();
            _ui.SetCoOpConnectionState(false);
            _connectionTask = _coOpHost.AcceptPlayerAsync(_networkCancellation.Token);
            _ui.SetCoOpLobbyStatus(restoredSession is null ? "HOSTING ONLINE CO-OP" : "HOSTING SAVED CO-OP",
                restoredSession is null
                    ? $"Share this code and your public IP. Forward TCP {OnlineCoOpPort} to this PC."
                    : $"Saved wave {restoredSession.CurrentWave} is ready. Share this code; the restored match begins when your friend joins.",
                _coOpHost.JoinCode);
            _state = GameState.CoOpLobby;
        }
        catch (Exception exception)
        {
            CleanupNetwork();
            _ui.SetCoOpLobbyStatus("HOST COULD NOT START", exception.Message);
            _state = GameState.CoOpLobby;
        }
    }

    private void BeginJoiningCoOp(string hostInput, string joinCode)
    {
        CleanupNetwork();
        try
        {
            var endpoint = OnlineHostEndpoint.Parse(hostInput, OnlineCoOpPort);
            _networkCancellation = new CancellationTokenSource();
            _isNetworkHost = false;
            _localPlayerId = 2;
            _joinEndpoint = endpoint;
            _joinCode = joinCode.Trim().ToUpperInvariant();
            _connectionTask = LanCoOpClient.ConnectAsync(endpoint.Host, endpoint.Port, joinCode, _networkCancellation.Token, _contentFingerprint);
            _ui.SetCoOpConnectionState(false);
            _ui.SetCoOpLobbyStatus("JOINING ONLINE CO-OP", $"Connecting to {endpoint.Host}:{endpoint.Port}...", joinCode);
            _state = GameState.CoOpLobby;
        }
        catch (Exception exception)
        {
            CleanupNetwork();
            _ui.SetCoOpLobbyStatus("ADDRESS NOT VALID", exception.Message, joinCode);
            _state = GameState.CoOpLobby;
        }
    }

    private void UpdateCoOpLobby(InputSnapshot input)
    {
        if (WithUiAudio(_ui.HandleCoOpLobby(input)) == UiAction.MainMenu)
        {
            CleanupNetwork();
            _state = GameState.CoOpMenu;
            return;
        }

        CompletePendingConnection(false);
        PollNetwork();
    }

    private void UpdateCoOpReconnect(InputSnapshot input, GameTime gameTime)
    {
        if (WithUiAudio(_ui.HandleCoOpReconnect(input)) == UiAction.MainMenu)
        {
            CleanupNetwork();
            _state = GameState.CoOpMenu;
            return;
        }

        if (!_isNetworkHost && _coOpConnection is null && _connectionTask is null && _joinEndpoint is { } endpoint)
        {
            _reconnectRetryRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_reconnectRetryRemaining <= 0)
            {
                _connectionTask = LanCoOpClient.ConnectAsync(endpoint.Host, endpoint.Port, _joinCode, _networkCancellation?.Token ?? CancellationToken.None, _contentFingerprint);
                _reconnectRetryRemaining = 2f;
                _ui.SetCoOpLobbyStatus("RECONNECTING TO HOST", $"Trying {endpoint.Host}:{endpoint.Port}. The match is preserved.", _joinCode);
            }
        }

        CompletePendingConnection(true);
        PollNetwork();
    }

    private void CompletePendingConnection(bool reconnecting)
    {
        if (_connectionTask is not { IsCompleted: true } connectionTask) return;
        _connectionTask = null;
        try
        {
            _coOpConnection = connectionTask.GetAwaiter().GetResult();
            _coOpHeartbeat.MarkInboundActivity();
            _ui.SetCoOpConnectionState(true, true);
            _receiveTask = null;
            _pendingNetworkSends.Clear();
            if (_isNetworkHost)
            {
                if (_session is null || _networkRunner is null) InitializeHostSession();
                if (reconnecting) _authoritativeCommands?.BeginRequestSession(2);
                _networkResyncing = false;
                _ui.SetCoOpLobbyStatus(reconnecting ? "PLAYER 2 RECONNECTED" : "PLAYER 2 CONNECTED",
                    "Sending the host's authoritative match state...", _coOpHost?.JoinCode ?? "");
                SendAuthoritativeSnapshot(reconnecting ? "Reconnect state" : "Initial state");
            }
            else
            {
                _ui.SetCoOpLobbyStatus(reconnecting ? "LINK RESTORED" : "CONNECTED",
                    "Receiving the host's authoritative match state...", _joinCode);
            }
        }
        catch (Exception exception)
        {
            if (_isNetworkHost && _coOpHost is not null && _networkCancellation is not null)
            {
                _connectionTask = _coOpHost.AcceptPlayerAsync(_networkCancellation.Token);
                _ui.SetCoOpLobbyStatus("WAITING FOR PLAYER 2", "A connection attempt was rejected; the host is still available.", _coOpHost.JoinCode);
                return;
            }
            if (reconnecting)
            {
                _reconnectRetryRemaining = 2f;
                _ui.SetCoOpLobbyStatus("RECONNECTING TO HOST", exception.GetBaseException().Message, _joinCode);
                return;
            }
            SetNetworkFailure("CONNECTION FAILED", exception.GetBaseException().Message);
        }
    }

    private void InitializeHostSession()
    {
        var session = _session;
        if (session is null)
        {
            session = new GameSession(_content, _ui.SelectedMapId, _ui.SelectedDifficultyId, _ui.SelectedChallengeId);
            AssignSession(session);
            session.ConfigureCoOp(1);
        }
        else if (!session.IsCoOp)
            session.ConfigureCoOp(1);
        ResetCoOpWaveReadyState(false);
        _networkRunner = new DeterministicSessionRunner(session);
        AttachNetworkRunner();
    }

    private void ApplyAuthoritativeSnapshot(CoOpStateSnapshot snapshot)
    {
        _networkChecksums.Clear();
        _remoteNetworkChecksums.Clear();
        _repliedChecksumTicks.Clear();
        var restored = GameSession.RestoreCoOpState(_content, snapshot, 2);
        AssignSession(restored);
        _networkRunner = new DeterministicSessionRunner(restored, snapshot.Tick);
        _networkRunner.RestorePendingCommands(snapshot.PendingCommands);
        AttachNetworkRunner();
        _coOpWaveReady.ApplyState(snapshot.ReadyMask, snapshot.WaveStartQueued, snapshot.WaveEarlyBonusQueued);
        _ui.SetCoOpWaveReadyState(_coOpWaveReady.ReadyMask, _coOpWaveReady.StartQueued, _coOpWaveReady.EarlyBonusQueued);
        _lastSyncTick = snapshot.Tick - 1;
        _checksumSnapshotFenceTick = snapshot.Tick;
        _networkResyncing = false;
        _networkStarted = false;
        _ui.SetCoOpLobbyStatus("STATE SYNCHRONIZED", "Waiting for the host to resume both players...", _joinCode);
        QueueSend(new CoOpEnvelope { Type = CoOpMessageType.Ready, PlayerId = 2, Ready = true });
    }

    private void PollNetwork()
    {
        for (var index = _pendingNetworkSends.Count - 1; index >= 0; index--)
        {
            var send = _pendingNetworkSends[index];
            if (!send.IsCompleted) continue;
            _pendingNetworkSends.RemoveAt(index);
            if (send.IsFaulted)
            {
                HandleConnectionLoss("CONNECTION LOST", send.Exception?.GetBaseException().Message ?? "A network send failed.");
                return;
            }
        }

        if (_coOpConnection is null) return;
        _receiveTask ??= _coOpConnection.ReceiveAsync(_networkCancellation?.Token ?? CancellationToken.None);
        if (!_receiveTask.IsCompleted) return;
        var receive = _receiveTask;
        _receiveTask = null;
        try
        {
            var envelope = receive.GetAwaiter().GetResult();
            if (envelope is null)
            {
                HandleConnectionLoss("PLAYER DISCONNECTED", "The online co-op connection was closed.");
                return;
            }
            HandleNetworkEnvelope(envelope);
            _coOpHeartbeat.MarkInboundActivity();
        }
        catch (OperationCanceledException) when (_networkCancellation?.IsCancellationRequested == true)
        {
        }
        catch (Exception exception)
        {
            HandleConnectionLoss("CONNECTION LOST", exception.GetBaseException().Message);
        }
    }

    private void HandleNetworkEnvelope(CoOpEnvelope envelope)
    {
        if (!CoOpEnvelopeValidator.IsExpectedInbound(envelope, _isNetworkHost))
            throw new InvalidDataException("The co-op peer sent a message reserved for the opposite connection direction.");

        switch (envelope.Type)
        {
            case CoOpMessageType.StateSnapshot when !_isNetworkHost && envelope.State is not null:
                ApplyAuthoritativeSnapshot(envelope.State);
                break;
            case CoOpMessageType.Ready when _isNetworkHost && envelope.PlayerId == 2 && envelope.Ready:
                _networkStarted = true;
                _networkResyncing = false;
                _ui.SetCoOpConnectionState(true);
                QueueSend(new CoOpEnvelope { Type = CoOpMessageType.Ready, PlayerId = 1, Ready = true });
                _ui.SetCoOpLobbyStatus("DEFENSE LINKED", "Player 1 and Player 2 share credits and lives.", _coOpHost?.JoinCode ?? "");
                ResumeNetworkSessionState();
                break;
            case CoOpMessageType.Ready when !_isNetworkHost && envelope.PlayerId == 1 && envelope.Ready:
                _networkStarted = true;
                _networkResyncing = false;
                _ui.SetCoOpConnectionState(true);
                _ui.SetCoOpLobbyStatus("DEFENSE LINKED", "Player 1 and Player 2 share credits and lives.", _ui.CoOpLobbyCode);
                ResumeNetworkSessionState();
                break;
            case CoOpMessageType.WaveReady when _isNetworkHost && envelope.PlayerId == 2:
                RegisterCoOpWaveReady(2);
                break;
            case CoOpMessageType.WaveReady when !_isNetworkHost && envelope.PlayerId == 1:
                _coOpWaveReady.ApplyState(envelope.ReadyMask, envelope.Ready, envelope.EarlyBonus);
                _ui.SetCoOpWaveReadyState(_coOpWaveReady.ReadyMask, _coOpWaveReady.StartQueued, _coOpWaveReady.EarlyBonusQueued);
                break;
            case CoOpMessageType.Ping when envelope.PlayerId != _localPlayerId:
                ShowCoOpPing(new Vector2(envelope.X, envelope.Y), envelope.PlayerId);
                break;
            case CoOpMessageType.Cursor when envelope.PlayerId != _localPlayerId:
                if (_coOpCursor.Receive(new Vector2(envelope.X, envelope.Y), envelope.PlayerId, envelope.EntityId,
                        envelope.TowerDefinitionId, envelope.TacticalPlacement, envelope.HasPlacementPreview,
                        new Vector2(envelope.PlacementX, envelope.PlacementY)))
                    SyncRemoteCoOpCursor();
                break;
            case CoOpMessageType.CommandRequest when _isNetworkHost && _networkStarted && envelope.Command is not null:
                QueueAuthoritativeCommand(envelope.Command with { PlayerId = 2 });
                break;
            case CoOpMessageType.AuthoritativeCommand when !_isNetworkHost && envelope.Command is not null:
                if (!GameCommandValidator.IsStructurallyValid(envelope.Command) ||
                    _networkRunner is null || !_networkRunner.Schedule(envelope.Tick, envelope.Command))
                    RequestAuthoritativeResync("An authoritative command was malformed or arrived after its simulation tick.");
                break;
            case CoOpMessageType.CommandReceipt:
                break;
            case CoOpMessageType.TickSync:
                HandleTickSync(envelope);
                break;
            case CoOpMessageType.ResyncRequest when _isNetworkHost:
                SendAuthoritativeSnapshot(string.IsNullOrWhiteSpace(envelope.Message) ? "Client resynchronization" : envelope.Message);
                break;
            case CoOpMessageType.RestartRequest when _isNetworkHost && envelope.PlayerId == 2 && _networkStarted:
                RestartCoOpAsHost();
                break;
            case CoOpMessageType.Rejected:
                SetNetworkFailure("CONNECTION REJECTED", envelope.Message);
                break;
            case CoOpMessageType.Disconnect:
                HandleConnectionLoss("PLAYER DISCONNECTED", envelope.Message);
                break;
        }
    }

    private void SubmitLocalNetworkCommand(GameCommand request)
    {
        if (!_networkStarted || _networkRunner is null) return;
        if (request.Type == GameCommandType.StartWave)
        {
            RegisterCoOpWaveReady(_localPlayerId);
            return;
        }
        request = request with { PlayerId = _localPlayerId, ClientRequestId = _nextClientRequestId++ };
        if (_isNetworkHost) QueueAuthoritativeCommand(request);
        else QueueSend(new CoOpEnvelope { Type = CoOpMessageType.CommandRequest, PlayerId = 2, Command = request });
    }

    private void RegisterCoOpWaveReady(int playerId)
    {
        if (_session is null || !_coOpWaveReady.RegisterReady(playerId, _session.CanStartWave, IsEarlyCallAvailable(_session))) return;
        _ui.SetCoOpWaveReadyState(_coOpWaveReady.ReadyMask, _coOpWaveReady.StartQueued, _coOpWaveReady.EarlyBonusQueued);

        if (!_isNetworkHost)
        {
            QueueSend(new CoOpEnvelope { Type = CoOpMessageType.WaveReady, PlayerId = 2, Ready = true });
            return;
        }

        if (_coOpWaveReady.StartQueued)
        {
            QueueAuthoritativeCommand(new GameCommand
            {
                PlayerId = 1,
                ClientRequestId = _nextClientRequestId++,
                Type = GameCommandType.StartWave,
                EarlyStartEligible = _coOpWaveReady.EarlyBonusQueued
            });
        }
        BroadcastCoOpWaveReadyState();
    }

    private void BroadcastCoOpWaveReadyState()
    {
        if (!_isNetworkHost) return;
        QueueSend(new CoOpEnvelope
        {
            Type = CoOpMessageType.WaveReady,
            PlayerId = 1,
            ReadyMask = _coOpWaveReady.ReadyMask,
            Ready = _coOpWaveReady.StartQueued,
            EarlyBonus = _coOpWaveReady.EarlyBonusQueued
        });
    }

    private static bool IsEarlyCallAvailable(GameSession session) =>
        session.CurrentWave > 0 && session.IntermissionRemaining > 0;

    private void ResetCoOpWaveReadyState(bool broadcast)
    {
        _coOpWaveReady.Reset();
        _ui.SetCoOpWaveReadyState(0, false, false);
        if (broadcast) BroadcastCoOpWaveReadyState();
    }

    private void SendCoOpPing(Vector2 position)
    {
        ShowCoOpPing(position, _localPlayerId);
        QueueSend(new CoOpEnvelope
        {
            Type = CoOpMessageType.Ping,
            PlayerId = _localPlayerId,
            X = position.X,
            Y = position.Y
        });
    }

    private void ShowCoOpPing(Vector2 position, int playerId)
    {
        if (_session is null || playerId is < 1 or > 2 || position.X < 0 || position.X >= GameConstants.MapWidth ||
            position.Y < GameConstants.TopBarHeight || position.Y > GameConstants.LogicalHeight) return;
        _session.Effects.AddPing(position, playerId == 1 ? ColorPalette.Cyan : ColorPalette.Coral);
    }

    private void SyncRemoteCoOpCursor() =>
        _ui?.SetRemoteCoOpCursor(_coOpCursor.RemotePosition, _coOpCursor.RemotePlayerId, _coOpCursor.RemoteEntityId,
            _coOpCursor.RemotePlacementTowerId, _coOpCursor.RemoteTacticalPlacement,
            _coOpCursor.RemoteHasPlacementPreview,
            _coOpCursor.RemotePlacementPreviewPosition);

    private void QueueAuthoritativeCommand(GameCommand request)
    {
        if (!_networkStarted || _authoritativeCommands is null || _networkRunner is null) return;
        var receipt = _authoritativeCommands.Sequence(request);
        QueueSend(new CoOpEnvelope { Type = CoOpMessageType.CommandReceipt, PlayerId = request.PlayerId, Receipt = receipt });
        if (!receipt.Accepted || receipt.Duplicate) return;
        var scheduledTick = _networkRunner.Tick + NetworkInputDelayTicks;
        if (!_networkRunner.Schedule(scheduledTick, receipt.Command))
        {
            SetNetworkFailure("SYNCHRONIZATION LOST", "The host could not schedule an authoritative command.");
            return;
        }
        QueueSend(new CoOpEnvelope
        {
            Type = CoOpMessageType.AuthoritativeCommand,
            PlayerId = request.PlayerId,
            Command = receipt.Command,
            Tick = scheduledTick
        });
    }

    private void QueueSend(CoOpEnvelope envelope)
    {
        if (_coOpConnection is null) return;
        if (_pendingNetworkSends.Count >= LanCoOpConnection.MaximumQueuedSends)
        {
            HandleConnectionLoss("CONNECTION STALLED", "Outbound co-op traffic stopped draining; the match was paused for reconnection.");
            return;
        }
        _pendingNetworkSends.Add(_coOpConnection.SendAsync(envelope, _networkCancellation?.Token ?? CancellationToken.None));
    }

    private void AttachNetworkRunner()
    {
        if (_networkRunner is null) return;
        _networkRunner.TickCompleted += OnNetworkTickCompleted;
        _networkChecksums[_networkRunner.Tick] = SessionChecksum.Compute(_session!, _networkRunner.Tick);
    }

    private void OnNetworkTickCompleted(long tick)
    {
        // Checksums are synchronization samples, not part of simulation. Hashing
        // every entity at all 60 local ticks would waste substantial endgame CPU;
        // both peers instead hash the same once-per-second deterministic boundary.
        if (_session is null || _networkRunner is null || !_networkStarted ||
            tick % NetworkChecksumIntervalTicks != 0) return;
        var checksum = SessionChecksum.Compute(_session, tick);
        _networkChecksums[tick] = checksum;
        foreach (var expired in _networkChecksums.Keys.Where(value => value < tick - NetworkChecksumHistoryTicks).ToArray())
            _networkChecksums.Remove(expired);
        foreach (var expired in _remoteNetworkChecksums.Keys.Where(value => value < tick - NetworkChecksumHistoryTicks).ToArray())
            _remoteNetworkChecksums.Remove(expired);
        _repliedChecksumTicks.RemoveWhere(value => value < tick - NetworkChecksumHistoryTicks);

        if (_isNetworkHost)
        {
            _lastSyncTick = tick;
            QueueSend(new CoOpEnvelope { Type = CoOpMessageType.TickSync, PlayerId = 1, Tick = tick, Checksum = checksum });
        }
        CompareNetworkChecksum(tick);
    }

    private void HandleTickSync(CoOpEnvelope envelope)
    {
        if (_networkRunner is null || string.IsNullOrWhiteSpace(envelope.Checksum) ||
            !CoOpChecksumWindow.IsAcceptable(_networkRunner.Tick, _checksumSnapshotFenceTick, envelope.Tick)) return;
        _remoteNetworkChecksums[envelope.Tick] = envelope.Checksum;
        CompareNetworkChecksum(envelope.Tick);
    }

    private void CompareNetworkChecksum(long tick)
    {
        if (!_networkStarted || !_networkChecksums.TryGetValue(tick, out var localChecksum) ||
            !_remoteNetworkChecksums.TryGetValue(tick, out var remoteChecksum)) return;
        if (!string.Equals(localChecksum, remoteChecksum, StringComparison.Ordinal))
        {
            if (_isNetworkHost) SendAuthoritativeSnapshot($"Automatic repair after divergence at tick {tick}");
            else RequestAuthoritativeResync($"State differed at fixed tick {tick}.");
            return;
        }
        if (!_isNetworkHost && _repliedChecksumTicks.Add(tick))
            QueueSend(new CoOpEnvelope { Type = CoOpMessageType.TickSync, PlayerId = 2, Tick = tick, Checksum = localChecksum });
    }

    private void SendAuthoritativeSnapshot(string reason)
    {
        if (!_isNetworkHost || _networkResyncing || _coOpConnection is null || _session is null || _networkRunner is null) return;
        _networkStarted = false;
        _networkResyncing = true;
        _ui.SetCoOpConnectionState(true, true);
        _networkChecksums.Clear();
        _remoteNetworkChecksums.Clear();
        _repliedChecksumTicks.Clear();
        var snapshot = _session.CaptureCoOpState(_networkRunner.Tick, _coOpWaveReady.ReadyMask,
            _coOpWaveReady.StartQueued, _coOpWaveReady.EarlyBonusQueued);
        _checksumSnapshotFenceTick = snapshot.Tick;
        // Snapshot capture compacts expired telemetry source IDs. Hash the
        // resulting authoritative state, not the pre-compaction state.
        _networkChecksums[_networkRunner.Tick] = SessionChecksum.Compute(_session, _networkRunner.Tick);
        snapshot.PendingCommands = _networkRunner.CapturePendingCommands();
        QueueSend(new CoOpEnvelope
        {
            Type = CoOpMessageType.StateSnapshot,
            PlayerId = 1,
            Tick = snapshot.Tick,
            Message = reason,
            State = snapshot
        });
        if (_state is GameState.Playing or GameState.Paused)
        {
            _ui.SetCoOpLobbyStatus("RESYNCHRONIZING", "The host is sending a clean authoritative state to Player 2.", _coOpHost?.JoinCode ?? "");
            _state = GameState.CoOpReconnect;
        }
    }

    private void RequestAuthoritativeResync(string reason)
    {
        if (_isNetworkHost || _networkResyncing || _coOpConnection is null) return;
        _networkStarted = false;
        _networkResyncing = true;
        _ui.SetCoOpConnectionState(true, true);
        _ui.SetCoOpLobbyStatus("RESYNCHRONIZING", "State drift was detected. Requesting a clean copy from the host...", _joinCode);
        _state = GameState.CoOpReconnect;
        QueueSend(new CoOpEnvelope { Type = CoOpMessageType.ResyncRequest, PlayerId = 2, Message = reason });
    }

    private void ResumeNetworkSessionState()
    {
        if (_session is null) return;
        if (_session.IsVictory || _session.IsDefeat) _ui.PrepareResultScreen();
        _state = _session.IsVictory ? GameState.Victory : _session.IsDefeat ? GameState.Defeat : GameState.Playing;
    }

    private void HandleConnectionLoss(string title, string detail)
    {
        if (_session is null || _networkRunner is null)
        {
            SetNetworkFailure(title, detail);
            return;
        }

        DisposePeerConnection();
        _networkStarted = false;
        _networkResyncing = true;
        _ui.SetCoOpConnectionState(false, true);
        _networkChecksums.Clear();
        _remoteNetworkChecksums.Clear();
        _repliedChecksumTicks.Clear();
        _state = GameState.CoOpReconnect;
        if (_isNetworkHost && _coOpHost is not null && _networkCancellation is not null)
        {
            _connectionTask = _coOpHost.AcceptPlayerAsync(_networkCancellation.Token);
            _ui.SetCoOpLobbyStatus("PLAYER 2 DISCONNECTED", "Match paused. Rejoin with the same address and code.", _coOpHost.JoinCode);
        }
        else
        {
            _reconnectRetryRemaining = 0;
            _ui.SetCoOpLobbyStatus("CONNECTION TO HOST LOST", "Match paused. Reconnecting automatically...", _joinCode);
        }
    }

    private void SetNetworkFailure(string title, string detail)
    {
        CleanupNetwork();
        _ui.SetCoOpLobbyStatus(title, detail);
        _state = GameState.CoOpLobby;
    }

    private void DisposePeerConnection()
    {
        var receive = _receiveTask;
        _receiveTask = null;
        var sends = _pendingNetworkSends.ToArray();
        _pendingNetworkSends.Clear();
        var connection = _coOpConnection;
        _coOpConnection = null;
        try { connection?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        if (receive is not null) _ = ObserveNetworkTaskAsync(receive);
        foreach (var send in sends) _ = ObserveNetworkTaskAsync(send);
        _coOpCursor.Reset();
        _coOpHeartbeat.Reset();
        SyncRemoteCoOpCursor();
    }

    private void CleanupNetwork(bool preserveMusic = false)
    {
        var pendingConnection = _connectionTask;
        _connectionTask = null;
        try { _networkCancellation?.Cancel(); } catch { }
        DisposePeerConnection();
        try { _coOpHost?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        if (pendingConnection is not null) _ = DisposePendingConnectionAsync(pendingConnection);
        _networkCancellation?.Dispose();
        _networkCancellation = null;
        _coOpConnection = null;
        _coOpHost = null;
        _receiveTask = null;
        _pendingNetworkSends.Clear();
        _networkChecksums.Clear();
        _remoteNetworkChecksums.Clear();
        _repliedChecksumTicks.Clear();
        _coOpWaveReady.Reset();
        _ui?.SetCoOpWaveReadyState(0, false);
        _ui?.SetCoOpConnectionState(false);
        _ui?.CloseGameplayOverlay();
        _authoritativeCommands = null;
        _networkRunner = null;
        _networkStarted = false;
        _networkResyncing = false;
        _isNetworkHost = false;
        _localPlayerId = 1;
        _nextClientRequestId = 1;
        _lastSyncTick = -1;
        _checksumSnapshotFenceTick = -1;
        _joinEndpoint = null;
        _joinCode = "";
        _reconnectRetryRemaining = 0;
        AssignSession(null, preserveMusic);
        _activeSaveSlot = null;
    }

    private static async Task DisposePendingConnectionAsync(Task<LanCoOpConnection> pendingConnection)
    {
        try
        {
            var connection = await pendingConnection.ConfigureAwait(false);
            await connection.DisposeAsync();
        }
        catch
        {
            // Cancellation and failed handshakes are expected during menu exits.
        }
    }

    private static async Task ObserveNetworkTaskAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { }
    }

    private void HandlePauseAction(UiAction action)
    {
        switch (action)
        {
            case UiAction.Resume: _state = GameState.Playing; break;
            case UiAction.SaveGame: OpenSaveSlots(true, GameState.Paused); break;
            case UiAction.LoadGame: OpenSaveSlots(false, GameState.Paused); break;
            case UiAction.Settings:
                _settingsReturnState = GameState.Paused;
                _state = GameState.Settings;
                break;
            case UiAction.Restart: Restart(); break;
            case UiAction.MainMenu: BeginMainMenuTransition(); break;
        }
    }

    private void HandleResultAction(UiAction action)
    {
        switch (action)
        {
            case UiAction.ContinueEndless:
                if (_session is null) break;
                if (_networkRunner is null)
                {
                    if (_session.BeginEndlessMode()) _state = GameState.Playing;
                }
                else
                    SubmitLocalNetworkCommand(new GameCommand { PlayerId = _localPlayerId, Type = GameCommandType.ContinueEndless });
                break;
            case UiAction.ViewField:
                if (_session?.IsDefeat == true) _state = GameState.DefeatField;
                break;
            case UiAction.Restart: Restart(); break;
            case UiAction.MainMenu: BeginMainMenuTransition(); break;
        }
    }

    private void Restart()
    {
        if (_networkRunner is not null)
        {
            RequestCoOpRestart();
            return;
        }
        var mapId = _session?.Map.Definition.Id ?? _ui.SelectedMapId;
        var difficultyId = _session?.DifficultyId ?? _ui.SelectedDifficultyId;
        var challengeId = _session?.ChallengeId ?? _ui.SelectedChallengeId;
        AssignSession(new GameSession(_content, mapId, difficultyId, challengeId));
        _lastAutosaveAttemptedWave = -1;
        _activeSaveSlot = null;
        _state = GameState.Playing;
    }

    private void RequestCoOpRestart()
    {
        if (_session is null || _networkRunner is null || !_networkStarted) return;
        if (_isNetworkHost)
        {
            RestartCoOpAsHost();
            return;
        }

        _networkStarted = false;
        _networkResyncing = true;
        _ui.SetCoOpConnectionState(true, true);
        _ui.SetCoOpLobbyStatus("RESTART REQUESTED", "Waiting for the host to initialize a fresh shared defense...", _joinCode);
        _state = GameState.CoOpReconnect;
        QueueSend(new CoOpEnvelope { Type = CoOpMessageType.RestartRequest, PlayerId = 2 });
    }

    private void RestartCoOpAsHost()
    {
        if (!_isNetworkHost || _session is null || _coOpConnection is null) return;
        var mapId = _session.Map.Definition.Id;
        var difficultyId = _session.DifficultyId;
        var challengeId = _session.ChallengeId;
        AssignSession(new GameSession(_content, mapId, difficultyId, challengeId));
        _session.ConfigureCoOp(1);
        _activeSaveSlot = null;
        _lastAutosaveAttemptedWave = -1;
        _authoritativeCommands = new AuthoritativeCommandHost();
        _networkRunner = new DeterministicSessionRunner(_session);
        AttachNetworkRunner();
        ResetCoOpWaveReadyState(false);
        _lastSyncTick = -1;
        _checksumSnapshotFenceTick = -1;
        _networkStarted = true;
        _networkResyncing = false;
        _ui.SetCoOpLobbyStatus("RESTARTING CO-OP", "Sending both players a fresh defense on the same map...", _coOpHost?.JoinCode ?? "");
        _state = GameState.CoOpReconnect;
        SendAuthoritativeSnapshot("Host-authoritative co-op restart");
    }

    private void OpenSaveSlots(bool writeMode, GameState returnState)
    {
        _saveSlotWriteMode = writeMode;
        _saveSlotReturnState = returnState;
        try
        {
            var slots = SaveGameStore.GetSlots();
            if (writeMode) slots = slots.Where(slot => slot.Slot != SaveSlotRepository.AutosaveSlot).ToArray();
            _ui.ConfigureSaveSlots(slots, writeMode, _activeSaveSlot);
            _ui.SetSaveState(SaveSlotsExistSafely());
        }
        catch (Exception exception)
        {
            _ui.ConfigureSaveSlots(Array.Empty<SaveSlotInfo>(), writeMode, _activeSaveSlot);
            _ui.SetSaveState(false, $"Save storage unavailable: {exception.GetBaseException().Message}");
        }
        _state = GameState.SaveSlots;
    }

    private void HandleSaveSlotAction(UiAction action)
    {
        if (action == UiAction.RunHistory)
        {
            OpenRunHistory();
            return;
        }
        if (action == UiAction.CloseSaveSlots)
        {
            _state = _saveSlotReturnState;
            return;
        }
        if (action == UiAction.DeleteSaveSlot)
        {
            DeleteSaveSlot(_ui.SelectedSaveSlot);
            return;
        }
        if (action == UiAction.DuplicateSaveSlot)
        {
            DuplicateSaveSlot(_ui.SelectedSaveSlot);
            return;
        }
        if (action == UiAction.HostSavedGame)
        {
            LoadSaveSlot(_ui.SelectedSaveSlot, hostCoOp: true);
            return;
        }
        if (action != UiAction.ConfirmSaveSlot) return;

        var slot = _ui.SelectedSaveSlot;
        if (_saveSlotWriteMode)
        {
            SaveCheckpoint(false, slot);
            _state = _saveSlotReturnState;
        }
        else if (PlatformCapabilities.StagedRuntimeTransitions)
            BeginSaveSlotLoading(slot);
        else
            LoadSaveSlot(slot, hostCoOp: false);
    }

    private void OpenRunHistory(string? preferredRunId = null)
    {
        _historyInspectionSession = null;
        try
        {
            _ui.ConfigureRunHistory(RunHistoryStore.GetEntries(), preferredRunId);
            _ui.SetRunHistoryStatus("Completed campaigns and endless progress are recorded locally.");
        }
        catch (Exception exception)
        {
            _ui.ConfigureRunHistory(Array.Empty<RunHistoryEntry>());
            _ui.SetRunHistoryStatus($"History unavailable: {exception.GetBaseException().Message}");
        }
        _state = GameState.RunHistory;
    }

    private void HandleRunHistoryAction(UiAction action)
    {
        if (action == UiAction.ViewRunHistoryField && _ui.SelectedRunHistoryEntry is { } entry)
        {
            try
            {
                _historyInspectionSession = entry.CreateInspectionSession(_content);
                _state = GameState.RunHistoryField;
            }
            catch (Exception exception)
            {
                _ui.SetRunHistoryStatus($"Layout unavailable: {exception.GetBaseException().Message}");
            }
            return;
        }
        if (action == UiAction.CloseRunHistory)
        {
            OpenSaveSlots(_saveSlotWriteMode, _saveSlotReturnState);
            return;
        }
        if (action != UiAction.DeleteRunHistory || _ui.SelectedRunHistoryId is not { } runId) return;
        try
        {
            if (RunHistoryStore.Delete(runId))
            {
                var entries = RunHistoryStore.GetEntries();
                _ui.ConfigureRunHistory(entries);
                _ui.SetRunHistoryStatus("Run record deleted.");
            }
        }
        catch (Exception exception)
        {
            _ui.SetRunHistoryStatus($"History delete failed: {exception.GetBaseException().Message}");
        }
    }

    private void UpdateRunHistoryField(InputSnapshot input)
    {
        if (_historyInspectionSession is null)
        {
            _state = GameState.RunHistory;
            return;
        }
        if (WithUiAudio(_ui.HandleRunHistoryFieldInput(input)) == UiAction.CloseRunHistoryField)
        {
            _historyInspectionSession = null;
            _state = GameState.RunHistory;
            return;
        }
        _historyInspectionSession.HandleInspectionInput(input);
    }

    private void RecordTerminalRun()
    {
        if (_session is null || (!_session.IsVictory && !_session.IsDefeat)) return;
        var resultKey = $"{_session.RunId}:{_session.IsVictory}:{_session.IsDefeat}:{_session.CurrentWave}:{_session.Economy.TotalKills}:{_session.Economy.Lives}";
        if (resultKey == _lastRecordedResultKey) return;
        // Mark the terminal state before touching disk. If local history storage is
        // unavailable, a result screen must not retry the same failed write every frame.
        _lastRecordedResultKey = resultKey;
        try
        {
            RunHistoryStore.Upsert(RunHistoryEntry.FromSession(_session));
            RefreshRunHistoryCache();
        }
        catch
        {
            // A history write must never interrupt or obscure the result screen.
        }
    }

    private void RefreshRunHistoryCache()
    {
        try
        {
            var entries = RunHistoryStore.GetEntries();
            _ui.ConfigureRunHistory(entries);
        }
        catch
        {
            _ui.ConfigureRunHistory(Array.Empty<RunHistoryEntry>());
        }
    }

    private void DeleteSaveSlot(int slot)
    {
        try
        {
            if (!SaveGameStore.Delete(slot))
            {
                _ui.SetSaveState(SaveSlotsExistSafely(), $"{SaveSlotLabel(slot)} is already empty.");
                return;
            }

            if (_activeSaveSlot == slot) _activeSaveSlot = null;
            var slots = SaveGameStore.GetSlots();
            if (_saveSlotWriteMode) slots = slots.Where(candidate => candidate.Slot != SaveSlotRepository.AutosaveSlot).ToArray();
            var preferred = slots.FirstOrDefault(candidate => candidate.Slot >= slot)?.Slot
                ?? slots.LastOrDefault()?.Slot;
            _ui.ConfigureSaveSlots(slots, _saveSlotWriteMode, preferred);
            _ui.SetSaveState(SaveSlotsExistSafely(), $"Deleted {SaveSlotLabel(slot).ToLowerInvariant()}.");
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveSlotsExistSafely(), $"Delete failed: {exception.GetBaseException().Message}");
        }
    }

    private void DuplicateSaveSlot(int sourceSlot)
    {
        try
        {
            var destinationSlot = SaveGameStore.Duplicate(sourceSlot);
            var slots = SaveGameStore.GetSlots();
            _ui.ConfigureSaveSlots(slots, false, destinationSlot);
            _ui.SetSaveState(true,
                $"Duplicated {SaveSlotLabel(sourceSlot).ToLowerInvariant()} to slot {destinationSlot}.");
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveSlotsExistSafely(), $"Duplicate failed: {exception.GetBaseException().Message}");
        }
    }

    private void SaveCheckpoint(bool automatic, int? requestedSlot = null)
    {
        if (_session is null || !_session.CanSaveCheckpoint) return;
        // One automatic attempt per completed wave prevents a persistent I/O
        // failure from retrying every render frame. Manual Save remains an
        // immediate retry and a later wave gets a fresh automatic attempt.
        if (automatic) _lastAutosaveAttemptedWave = _session.CurrentWave;
        try
        {
            var slot = automatic
                ? SaveSlotRepository.AutosaveSlot
                : requestedSlot ?? _activeSaveSlot ?? SaveGameStore.FindFirstEmptySlot();
            if (slot is null)
            {
                _ui.SetSaveState(true, "Save index capacity is exhausted; delete an old save before continuing.");
                return;
            }
            SaveGameStore.Save(_session, slot.Value);
            if (!automatic) _activeSaveSlot = slot;
            _lastAutosaveAttemptedWave = _session.CurrentWave;
            var label = automatic
                ? $"Autosaved wave {_session.CurrentWave}."
                : $"Saved wave {_session.CurrentWave} to slot {slot}.";
            _ui.SetSaveState(true, label);
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveSlotsExistSafely(), $"Save failed: {exception.GetBaseException().Message}");
        }
    }

    private bool LoadSaveSlot(int slot, bool hostCoOp, bool deferPlaying = false)
    {
        try
        {
            var restored = SaveGameStore.Load(_content, slot);
            var preserveMusic = _session is not null;
            if (hostCoOp)
            {
                _ui.SetSaveState(true, $"Hosting {SaveSlotLabel(slot).ToLowerInvariant()}; waiting for player 2.");
                BeginHostingCoOp(restored, slot, preserveMusic);
                return true;
            }

            CleanupNetwork(preserveMusic);
            var resumedFromCoOp = restored.IsCoOp;
            restored.ConfigureSolo();
            AssignSession(restored, preserveMusic);
            _activeSaveSlot = slot > SaveSlotRepository.AutosaveSlot ? slot : null;
            _lastAutosaveAttemptedWave = restored.CurrentWave;
            _ui.SetSaveState(true, resumedFromCoOp
                ? $"Continued co-op {SaveSlotLabel(slot).ToLowerInvariant()} solo after wave {restored.CurrentWave}."
                : $"Loaded solo {SaveSlotLabel(slot).ToLowerInvariant()} after wave {restored.CurrentWave}.");
            if (!deferPlaying)
            {
                _state = GameState.Playing;
                PlatformServices.RuntimeStageSetter?.Invoke("gameplay");
            }
            return true;
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveSlotsExistSafely(), $"Load failed: {exception.GetBaseException().Message}");
            OpenSaveSlots(_saveSlotWriteMode, _saveSlotReturnState);
            return false;
        }
    }

    private static bool SaveSlotsExistSafely()
    {
        try { return SaveGameStore.Exists; }
        catch { return false; }
    }

    private static string SaveSlotLabel(int slot) =>
        slot == SaveSlotRepository.AutosaveSlot ? "AUTOSAVE" : $"SLOT {slot}";

    protected override void Draw(GameTime gameTime)
    {
        EnsureSceneRenderResources();
#if BLAZORGL
        GraphicsDevice.SetRenderTarget(null);
        var (backBufferWidth, backBufferHeight) = BrowserBackBufferSize();
        GraphicsDevice.Clear(ColorPalette.Paper);
        _viewportTransform.Update(backBufferWidth, backBufferHeight);
        var backBufferBounds = new Rectangle(
            0,
            0,
            backBufferWidth,
            backBufferHeight);
        GraphicsDevice.ScissorRectangle = Rectangle.Intersect(
            backBufferBounds,
            _viewportTransform.DestinationRectangle);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            null,
            _browserScissorRasterizer,
            null,
            _viewportTransform.DrawMatrix);
        DrawLogicalScene(gameTime);
        _spriteBatch.End();
#else
        GraphicsDevice.SetRenderTarget(_sceneTarget);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            null,
            null,
            null,
            Matrix.CreateScale(_sceneRenderScale));
        DrawLogicalScene(gameTime);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _viewportTransform.Update(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        // The standard color target and non-MSAA backbuffer preserve authored
        // theme values exactly; the 2x scene target provides edge smoothing.
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _spriteBatch.Draw(_sceneTarget, _viewportTransform.DestinationRectangle, Color.White);
        _spriteBatch.End();
#endif
        base.Draw(gameTime);
    }

    private void DrawLogicalScene(GameTime gameTime)
    {
        if (_loadError is not null)
        {
            _primitives.FillRect(_spriteBatch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.Paper);
            _primitives.DrawRect(_spriteBatch, new Rectangle(120, 210, 1040, 220), ColorPalette.Coral, 5);
            _spriteBatch.DrawString(Content.Load<SpriteFont>("Fonts/Interface"), "CONTENT ERROR", new Vector2(460, 280), ColorPalette.Ink, 0, Vector2.Zero, 2 * GameConstants.FontDrawScale, SpriteEffects.None, 0);
            _spriteBatch.DrawString(Content.Load<SpriteFont>("Fonts/Interface"), _loadError, new Vector2(120, 350), ColorPalette.Muted, 0, Vector2.Zero, 0.9f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
            return;
        }

        var presentedSession = _state == GameState.RunHistoryField ? _historyInspectionSession : _session;
        if (presentedSession is not null && _state is not (GameState.MainMenu or GameState.LoadingTransition))
        {
            _gameRenderer.Draw(_spriteBatch, _primitives, presentedSession,
                showTransientCombat: _state is not (GameState.DefeatField or GameState.RunHistoryField),
                presentationLeadSeconds: _state == GameState.Playing
                    ? _networkRunner?.PresentationLeadSeconds ?? 0
                    : 0,
                foregroundTowerId: _state == GameState.Playing ? _ui.RemoteCoOpSelectedTowerId : 0);
            if (_state == GameState.Playing || _state == GameState.Paused)
                _debug.Draw(_spriteBatch, _primitives, presentedSession, gameTime.ElapsedGameTime.TotalSeconds > 0 ? (float)(1 / gameTime.ElapsedGameTime.TotalSeconds) : 0);
        }
        _ui.Draw(_spriteBatch, _primitives, _state, presentedSession);
    }

    private void EnsureSceneRenderResources()
    {
#if BLAZORGL
        if (_primitives is null)
            _primitives = new PrimitiveRenderer(GraphicsDevice, GameConstants.RenderScale);
        return;
#else
        var presentation = GraphicsDevice.PresentationParameters;
        var desiredScale = GameConstants.RenderScaleForOutput(
            presentation.BackBufferWidth,
            presentation.BackBufferHeight);
        if (_sceneTarget is not null && !_sceneTarget.IsDisposed &&
            _sceneRenderScale == desiredScale) return;

        var replacementTarget = new RenderTarget2D(
            GraphicsDevice,
            GameConstants.LogicalWidth * desiredScale,
            GameConstants.LogicalHeight * desiredScale,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.DiscardContents);
        var replacementPrimitives = new PrimitiveRenderer(GraphicsDevice, desiredScale);
        _sceneTarget?.Dispose();
        _primitives?.Dispose();
        _sceneTarget = replacementTarget;
        _primitives = replacementPrimitives;
        _sceneRenderScale = desiredScale;
#endif
    }

    private void HandleSettingsAction(UiAction action)
    {
        if (action == UiAction.ApplySettings) ApplyUserSettings();
        else if (action == UiAction.PreviewSettings) ApplyPresentationSettings();
        else if (action == UiAction.CloseSettings) _state = _settingsReturnState;
    }

    private void ApplyUserSettings()
    {
        _settings.Normalize();
        if (_ui.SelectedSettingsIndex <= 1)
        {
            try
            {
                ApplyGraphicsSettings();
            }
            catch (Exception exception)
            {
                _settings.Fullscreen = false;
                _settings.WindowWidth = GameConstants.LogicalWidth;
                _settings.WindowHeight = GameConstants.LogicalHeight;
                try { ApplyGraphicsSettings(); } catch { }
                try { UserSettingsStore.Save(_settings); } catch { }
                _ui.SetSettingsStatus($"Display mode was unsupported; restored 1280 x 720 windowed. {exception.GetBaseException().Message}");
                return;
            }
        }
        else ApplyPresentationSettings();

        try
        {
            UserSettingsStore.Save(_settings);
            _windowSizeSavePending = false;
            _ui.SetSettingsStatus("");
        }
        catch (Exception exception)
        {
            // A storage failure must not undo a display mode that the graphics
            // device already accepted. Keep it live for this process and give
            // the player an actionable persistence warning.
            _ui.SetSettingsStatus($"Settings applied for this session but could not be saved: {exception.GetBaseException().Message}");
        }
    }

    private void SendLocalCoOpCursor(InputSnapshot input)
    {
        if (_session is null || _networkRunner is null || !_networkStarted) return;
        var placementTowerId = _session.PlacementTowerId ?? "";
        var tacticalPlacement = _session.TacticalPlacement;
        var hasPlacementPreview = (placementTowerId.Length > 0 || tacticalPlacement != TacticalPlacementKind.None) &&
                                  _session.HasPlacementPreview &&
                                  !_ui.IsGameplayOverlayOpen && !_session.IsCoOpPaused;
        if (!_coOpCursor.TryCaptureLocal(input.MousePosition, input.IsMouseOverLogicalCanvas,
                _session.SelectedTower?.Id ?? 0, placementTowerId, tacticalPlacement, hasPlacementPreview,
                _session.PlacementPreviewPosition, out var cursor)) return;
        QueueSend(new CoOpEnvelope
        {
            Type = CoOpMessageType.Cursor,
            PlayerId = _localPlayerId,
            X = cursor.Position.X,
            Y = cursor.Position.Y,
            EntityId = _session.SelectedTower?.Id ?? 0,
            TowerDefinitionId = placementTowerId,
            TacticalPlacement = cursor.TacticalPlacement,
            HasPlacementPreview = cursor.HasPlacementPreview,
            PlacementX = cursor.PlacementPreviewPosition.X,
            PlacementY = cursor.PlacementPreviewPosition.Y
        });
    }

    private void ToggleFullscreenFromHotkey()
    {
#if BLAZORGL
        var browserDisplay = PlatformServices.BrowserDisplayStateReader?.Invoke();
        if (browserDisplay?.Pending == true) return;
#endif
        var previousFullscreen = _settings.Fullscreen;
        _settings.Fullscreen = !previousFullscreen;
        try
        {
            ApplyGraphicsSettings();
            UserSettingsStore.Save(_settings);
            _windowSizeSavePending = false;
            _ui.SetSettingsStatus("");
        }
        catch (Exception exception)
        {
            _settings.Fullscreen = previousFullscreen;
            try { ApplyGraphicsSettings(); } catch { }
            try { UserSettingsStore.Save(_settings); } catch { }
            _ui.SetSettingsStatus($"F11 display change failed; restored the previous mode. {exception.GetBaseException().Message}");
        }
    }

    private void ApplyGraphicsSettings()
    {
#if BLAZORGL
        // Fullscreen requests require a user gesture. Browser-reported canvas
        // dimensions are applied separately when the transition settles.
        PlatformServices.FullscreenSetter?.Invoke(_settings.Fullscreen);
        UpdateViewportTransform();
        ApplyPresentationSettings();
#else
        // Borderless fullscreen must use the desktop-sized backbuffer. Using a
        // smaller remembered window size here leaves SDL's borderless window at desktop
        // size while shrinking only the render surface, which also separates
        // MouseState coordinates from the logical input transform.
        var desktopMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var previousClientBounds = Window.ClientBounds;
        var previousWindowBounds = new Rectangle(
            Window.Position.X,
            Window.Position.Y,
            previousClientBounds.Width,
            previousClientBounds.Height);
        var outputSize = _settings.ResolveBackBufferSize(desktopMode.Width, desktopMode.Height);
        if (!_settings.Fullscreen)
            outputSize = WindowLayout.FitClientInsideDesktop(outputSize.Width, outputSize.Height, desktopMode.Width, desktopMode.Height);
        _graphics.PreferredBackBufferWidth = outputSize.Width;
        _graphics.PreferredBackBufferHeight = outputSize.Height;
        _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = _settings.Fullscreen;
        _graphics.ApplyChanges();
        if (!_settings.Fullscreen)
        {
            var resizedClientBounds = Window.ClientBounds;
            Window.Position = WindowLayout.Recenter(
                previousWindowBounds,
                resizedClientBounds.Width,
                resizedClientBounds.Height,
                desktopMode.Width,
                desktopMode.Height);
            CaptureWindowedClientSize(scheduleSave: false);
        }
        _viewportTransform.Update(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
        ApplyPresentationSettings();
#endif
    }

#if BLAZORGL
    private void SynchronizeBrowserDisplayState()
    {
        var browserDisplay = PlatformServices.BrowserDisplayStateReader?.Invoke();
        if (browserDisplay is null || browserDisplay.Revision == _browserDisplayRevision) return;
        _browserDisplayRevision = browserDisplay.Revision;
        if (browserDisplay.BackBufferWidth > 0 && browserDisplay.BackBufferHeight > 0)
        {
            _browserBackBufferWidth = browserDisplay.BackBufferWidth;
            _browserBackBufferHeight = browserDisplay.BackBufferHeight;
            if (_graphics.PreferredBackBufferWidth != _browserBackBufferWidth ||
                _graphics.PreferredBackBufferHeight != _browserBackBufferHeight)
            {
                _graphics.PreferredBackBufferWidth = _browserBackBufferWidth;
                _graphics.PreferredBackBufferHeight = _browserBackBufferHeight;
                _graphics.ApplyChanges();
            }
        }
        if (browserDisplay.Pending || _settings.Fullscreen == browserDisplay.Active) return;

        _settings.Fullscreen = browserDisplay.Active;
        try
        {
            UserSettingsStore.Save(_settings);
            _windowSizeSavePending = false;
        }
        catch
        {
            // Browser presentation remains authoritative even if persistence is unavailable.
        }
    }

    private (int Width, int Height) BrowserBackBufferSize()
    {
        return (
            Math.Max(1, _browserBackBufferWidth > 0
                ? _browserBackBufferWidth
                : GraphicsDevice.PresentationParameters.BackBufferWidth),
            Math.Max(1, _browserBackBufferHeight > 0
                ? _browserBackBufferHeight
                : GraphicsDevice.PresentationParameters.BackBufferHeight));
    }
#endif

    private void UpdateViewportTransform()
    {
#if BLAZORGL
        var (width, height) = BrowserBackBufferSize();
        _viewportTransform.Update(width, height);
#else
        _viewportTransform.Update(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
#endif
    }

    private void CaptureWindowedClientSize(bool scheduleSave)
    {
        if (_settings.Fullscreen || _graphics.IsFullScreen) return;
        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        _viewportTransform.Update(bounds.Width, bounds.Height);
        if (!_settings.CaptureWindowedClientSize(bounds.Width, bounds.Height)) return;
        if (!scheduleSave) return;
        _windowSizeSavePending = true;
        _windowSizeSaveRemaining = WindowSizeSaveDelaySeconds;
    }

    private void AdvanceWindowSizePersistence(float elapsedSeconds)
    {
        if (!_windowSizeSavePending) return;
        _windowSizeSaveRemaining -= Math.Max(0, elapsedSeconds);
        if (_windowSizeSaveRemaining > 0) return;
        PersistPendingWindowSize();
    }

    private void PersistPendingWindowSize()
    {
        if (!_windowSizeSavePending) return;
        _windowSizeSavePending = false;
        try
        {
            UserSettingsStore.Save(_settings);
            if (_state == GameState.Settings) _ui.SetSettingsStatus("");
        }
        catch (Exception exception)
        {
            if (_state == GameState.Settings)
                _ui.SetSettingsStatus($"Window size changed but could not be saved: {exception.GetBaseException().Message}");
        }
    }

    private void ApplyPresentationSettings()
    {
        _gameRenderer.ReducedEffects = _settings.ReducedEffects;
        if (_audio is not null)
        {
            _audio.SfxVolume = _settings.SfxVolume;
            _audio.MusicVolume = _settings.MusicVolume;
        }
    }

#if !BLAZORGL
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        PersistPendingWindowSize();
        base.OnExiting(sender, args);
    }
#endif

    private void AssignSession(GameSession? session, bool preserveMusic = false)
    {
        _session = session;
        _lastRecordedResultKey = "";
        if (session is not null)
        {
            _audio?.Attach(session, preserveMusic);
        }
        else _audio?.Detach(preserveMusic);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupNetwork();
            _audio?.Dispose();
            _sceneTarget?.Dispose();
            _primitives?.Dispose();
#if BLAZORGL
            _browserScissorRasterizer?.Dispose();
#endif
        }
        base.Dispose(disposing);
    }
}
