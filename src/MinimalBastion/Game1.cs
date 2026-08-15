using MinimalBastion.Audio;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Debugging;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion;

public sealed class Game1 : Game
{
    private const int OnlineCoOpPort = 28741;
    private const int NetworkInputDelayTicks = 6;
    private readonly GraphicsDeviceManager _graphics;
    private readonly UserSettings _settings;
    private readonly ViewportTransform _viewportTransform = new();
    private InputRouter _input = null!;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _sceneTarget = null!;
    private PrimitiveRenderer _primitives = null!;
    private GameRenderer _gameRenderer = null!;
    private UIManager _ui = null!;
    private DebugOverlay _debug = null!;
    private AudioManager? _audio;
    private GameContent _content = null!;
    private GameSession? _session;
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
    private AuthoritativeCommandHost? _authoritativeCommands;
    private DeterministicSessionRunner? _networkRunner;
    private bool _isNetworkHost;
    private bool _networkStarted;
    private bool _networkResyncing;
    private int _localPlayerId = 1;
    private long _nextClientRequestId = 1;
    private long _lastSyncTick = -1;
    private OnlineHostEndpoint? _joinEndpoint;
    private string _joinCode = "";
    private float _reconnectRetryRemaining;
    private int _lastAutosavedWave = -1;
    private int? _activeSaveSlot;
    private GameState _saveSlotReturnState = GameState.MainMenu;
    private bool _saveSlotWriteMode;
    private GameState _settingsReturnState = GameState.MainMenu;

    public Game1()
    {
        _settings = UserSettingsStore.Load();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _settings.WindowWidth,
            PreferredBackBufferHeight = _settings.WindowHeight,
            SynchronizeWithVerticalRetrace = _settings.VSync,
            // The fixed 2x scene target already provides edge supersampling.
            // Backbuffer MSAA is unnecessary for the final textured composite.
            PreferMultiSampling = false,
            IsFullScreen = _settings.Fullscreen,
            HardwareModeSwitch = false
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Minimal Bastion";
    }

    protected override void Initialize()
    {
        _viewportTransform.Update(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        _input = new InputRouter(_viewportTransform);
        Window.TextInput += (_, args) => _input.QueueTextInput(args.Character);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _sceneTarget = new RenderTarget2D(
            GraphicsDevice,
            GameConstants.RenderWidth,
            GameConstants.RenderHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.DiscardContents);
        _primitives = new PrimitiveRenderer(GraphicsDevice);
        _gameRenderer = new GameRenderer();
        try
        {
            var contentDirectory = Path.Combine(AppContext.BaseDirectory, "ContentData");
            _content = new ContentLoader(contentDirectory).Load();
            _contentFingerprint = BuildFingerprint.Compute(contentDirectory);
            var font = Content.Load<SpriteFont>("Fonts/Interface");
            _ui = new UIManager(font);
            _ui.ConfigureMaps(_content.Maps.Values, _content.WaveSets, _content.Enemies);
            _ui.ConfigureDifficulties(_content.Difficulties.Values);
            _ui.ConfigureTowerLibrary(_content.Towers.Values);
            _ui.ConfigureSettings(_settings);
            _ui.SetSaveState(SaveGameStore.Exists);
            _debug = new DebugOverlay(font);
            _gameRenderer.ReducedEffects = _settings.ReducedEffects;
            _audio = AudioManager.TryCreate();
            if (_audio is not null) _audio.Volume = _settings.SfxVolume;
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
        _viewportTransform.Update(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        var input = _input.Update();
        _audio?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (_loadError is not null)
        {
            if (input.EscapePressed) Exit();
            base.Update(gameTime);
            return;
        }

        switch (_state)
        {
            case GameState.MainMenu:
                HandleMenuAction(_ui.HandleMainMenu(input));
                break;
            case GameState.TowerLibrary:
                HandleMenuAction(_ui.HandleTitleTowerLibrary(input));
                break;
            case GameState.Settings:
                HandleSettingsAction(_ui.HandleSettingsInput(input));
                break;
            case GameState.SaveSlots:
                HandleSaveSlotAction(_ui.HandleSaveSlots(input));
                break;
            case GameState.CoOpMenu:
                HandleCoOpMenuAction(_ui.HandleCoOpMenu(input));
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
                if (_session is not null) HandlePauseAction(_ui.HandlePausedInput(input, _session));
                break;
            case GameState.Victory:
            case GameState.Defeat:
                var resultState = _state;
                if (_networkRunner is not null) PollNetwork();
                if (_state != resultState) break;
                HandleResultAction(_ui.HandleResultInput(input, resultState == GameState.Victory));
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
        if (_ui.HandleDefeatFieldInput(input) == UiAction.ViewResults)
        {
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
        if (_networkRunner is not null && input.PingPressed && input.MousePosition.X < GameConstants.MapWidth && input.MousePosition.Y >= GameConstants.TopBarHeight)
            SendCoOpPing(input.MousePosition);
        _debug.Update(input);
        Action<GameCommand>? commandSink = _networkRunner is null ? null : SubmitLocalNetworkCommand;
        var action = _ui.HandleGameplayInput(input, _session, commandSink, _localPlayerId);
        if (_networkRunner is null && (action == UiAction.Pause || (input.PausePressed && _session.PlacementTowerId is null)))
        {
            _state = GameState.Paused;
            return;
        }

        _session.HandleWorldInput(input, commandSink, _localPlayerId);
        if (_networkRunner is null) _session.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        else
        {
            _networkRunner.Advance((float)gameTime.ElapsedGameTime.TotalSeconds);
            if (_coOpWaveReady.StartQueued && _session.Waves.IsActive) ResetCoOpWaveReadyState(_isNetworkHost);
        }
        if (_session.IsVictory) _state = GameState.Victory;
        else if (_session.IsDefeat) _state = GameState.Defeat;
        else if ((_networkRunner is null || _isNetworkHost) && _session.CanSaveCheckpoint && _session.CurrentWave > 0 && _session.CurrentWave != _lastAutosavedWave)
            SaveCheckpoint(true);
    }

    private void HandleMenuAction(UiAction action)
    {
        if (action == UiAction.Play)
        {
            AssignSession(new GameSession(_content, _ui.SelectedMapId, _ui.SelectedDifficultyId));
            _lastAutosavedWave = -1;
            _activeSaveSlot = SaveGameStore.FindFirstEmptySlot();
            _state = GameState.Playing;
        }
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

    private void HandleCoOpMenuAction(UiAction action)
    {
        switch (action)
        {
            case UiAction.HostCoOp:
                BeginHostingCoOp();
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

    private void BeginHostingCoOp(GameSession? restoredSession = null, int? saveSlot = null)
    {
        CleanupNetwork();
        try
        {
            AssignSession(restoredSession);
            _activeSaveSlot = saveSlot ?? SaveGameStore.FindFirstEmptySlot();
            _lastAutosavedWave = restoredSession?.CurrentWave ?? -1;
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
        if (_ui.HandleCoOpLobby(input) == UiAction.MainMenu)
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
        if (input.EscapePressed)
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
            _ui.SetCoOpConnectionState(true, true);
            _receiveTask = null;
            _pendingNetworkSends.Clear();
            if (_isNetworkHost)
            {
                if (_session is null || _networkRunner is null) InitializeHostSession();
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
            session = new GameSession(_content, _ui.SelectedMapId, _ui.SelectedDifficultyId);
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
            case CoOpMessageType.CommandRequest when _isNetworkHost && _networkStarted && envelope.Command is not null:
                QueueAuthoritativeCommand(envelope.Command with { PlayerId = 2 });
                break;
            case CoOpMessageType.AuthoritativeCommand when !_isNetworkHost && envelope.Command is not null:
                if (_networkRunner is null || !_networkRunner.Schedule(envelope.Tick, envelope.Command))
                    RequestAuthoritativeResync("A command arrived after its simulation tick.");
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
        if (_session is null || _networkRunner is null) return;
        var checksum = SessionChecksum.Compute(_session, tick);
        _networkChecksums[tick] = checksum;
        foreach (var expired in _networkChecksums.Keys.Where(value => value < tick - 240).ToArray())
            _networkChecksums.Remove(expired);
        foreach (var expired in _remoteNetworkChecksums.Keys.Where(value => value < tick - 240).ToArray())
            _remoteNetworkChecksums.Remove(expired);
        _repliedChecksumTicks.RemoveWhere(value => value < tick - 240);

        if (_networkStarted && _isNetworkHost && tick % 20 == 0)
        {
            _lastSyncTick = tick;
            QueueSend(new CoOpEnvelope { Type = CoOpMessageType.TickSync, PlayerId = 1, Tick = tick, Checksum = checksum });
        }
        CompareNetworkChecksum(tick);
    }

    private void HandleTickSync(CoOpEnvelope envelope)
    {
        if (envelope.Tick < 0 || string.IsNullOrWhiteSpace(envelope.Checksum)) return;
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
        _networkChecksums[_networkRunner.Tick] = SessionChecksum.Compute(_session, _networkRunner.Tick);
        var snapshot = _session.CaptureCoOpState(_networkRunner.Tick, _coOpWaveReady.ReadyMask,
            _coOpWaveReady.StartQueued, _coOpWaveReady.EarlyBonusQueued);
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
        try { _coOpConnection?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        _coOpConnection = null;
        _receiveTask = null;
        _pendingNetworkSends.Clear();
    }

    private void CleanupNetwork()
    {
        try { _networkCancellation?.Cancel(); } catch { }
        DisposePeerConnection();
        try { _coOpHost?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        _networkCancellation?.Dispose();
        _networkCancellation = null;
        _coOpConnection = null;
        _coOpHost = null;
        _connectionTask = null;
        _receiveTask = null;
        _pendingNetworkSends.Clear();
        _networkChecksums.Clear();
        _remoteNetworkChecksums.Clear();
        _repliedChecksumTicks.Clear();
        _coOpWaveReady.Reset();
        _ui?.SetCoOpWaveReadyState(0, false);
        _ui?.SetCoOpConnectionState(false);
        _authoritativeCommands = null;
        _networkRunner = null;
        _networkStarted = false;
        _networkResyncing = false;
        _isNetworkHost = false;
        _localPlayerId = 1;
        _nextClientRequestId = 1;
        _lastSyncTick = -1;
        _joinEndpoint = null;
        _joinCode = "";
        _reconnectRetryRemaining = 0;
        AssignSession(null);
        _activeSaveSlot = null;
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
            case UiAction.MainMenu: CleanupNetwork(); _state = GameState.MainMenu; break;
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
            case UiAction.MainMenu: CleanupNetwork(); _state = GameState.MainMenu; break;
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
        AssignSession(new GameSession(_content, mapId, difficultyId));
        _lastAutosavedWave = -1;
        _activeSaveSlot = SaveGameStore.FindFirstEmptySlot();
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
        AssignSession(new GameSession(_content, mapId, difficultyId));
        _session.ConfigureCoOp(1);
        _activeSaveSlot = SaveGameStore.FindFirstEmptySlot();
        _lastAutosavedWave = -1;
        _authoritativeCommands = new AuthoritativeCommandHost();
        _networkRunner = new DeterministicSessionRunner(_session);
        AttachNetworkRunner();
        ResetCoOpWaveReadyState(false);
        _lastSyncTick = -1;
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
        var slots = SaveGameStore.GetSlots();
        _ui.ConfigureSaveSlots(slots, writeMode, _activeSaveSlot);
        _ui.SetSaveState(slots.Any(slot => slot.IsOccupied));
        _state = GameState.SaveSlots;
    }

    private void HandleSaveSlotAction(UiAction action)
    {
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
        if (action != UiAction.ConfirmSaveSlot) return;

        var slot = _ui.SelectedSaveSlot;
        if (_saveSlotWriteMode)
        {
            SaveCheckpoint(false, slot);
            _state = _saveSlotReturnState;
        }
        else
            LoadSaveSlot(slot);
    }

    private void DeleteSaveSlot(int slot)
    {
        try
        {
            if (!SaveGameStore.Delete(slot))
            {
                _ui.SetSaveState(SaveGameStore.Exists, $"Slot {slot} is already empty.");
                return;
            }

            if (_activeSaveSlot == slot) _activeSaveSlot = null;
            var slots = SaveGameStore.GetSlots();
            var preferred = slots.FirstOrDefault(candidate => candidate.Slot >= slot)?.Slot
                ?? slots.LastOrDefault()?.Slot;
            _ui.ConfigureSaveSlots(slots, _saveSlotWriteMode, preferred);
            _ui.SetSaveState(slots.Any(candidate => candidate.IsOccupied), $"Deleted save slot {slot}.");
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveGameStore.Exists, $"Delete failed: {exception.GetBaseException().Message}");
        }
    }

    private void SaveCheckpoint(bool automatic, int? requestedSlot = null)
    {
        if (_session is null || !_session.CanSaveCheckpoint) return;
        try
        {
            var slot = requestedSlot ?? _activeSaveSlot ?? SaveGameStore.FindFirstEmptySlot();
            if (slot is null)
            {
                _lastAutosavedWave = _session.CurrentWave;
                _ui.SetSaveState(true, "Save index capacity is exhausted; delete an old save before continuing.");
                return;
            }
            SaveGameStore.Save(_session, slot.Value);
            _activeSaveSlot = slot;
            _lastAutosavedWave = _session.CurrentWave;
            var label = automatic
                ? $"Autosaved wave {_session.CurrentWave} to slot {slot}."
                : $"Saved wave {_session.CurrentWave} to slot {slot}.";
            _ui.SetSaveState(true, label);
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveGameStore.Exists, $"Save failed: {exception.GetBaseException().Message}");
        }
    }

    private void LoadSaveSlot(int slot)
    {
        try
        {
            var restored = SaveGameStore.Load(_content, slot);
            if (restored.IsCoOp)
            {
                _ui.SetSaveState(true, $"Loaded co-op slot {slot}; waiting for player 2.");
                BeginHostingCoOp(restored, slot);
                return;
            }

            CleanupNetwork();
            AssignSession(restored);
            _activeSaveSlot = slot;
            _lastAutosavedWave = restored.CurrentWave;
            _ui.SetSaveState(true, $"Loaded solo slot {slot} after wave {restored.CurrentWave}.");
            _state = GameState.Playing;
        }
        catch (Exception exception)
        {
            _ui.SetSaveState(SaveGameStore.Exists, $"Load failed: {exception.GetBaseException().Message}");
            OpenSaveSlots(_saveSlotWriteMode, _saveSlotReturnState);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_sceneTarget);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            null,
            null,
            null,
            Matrix.CreateScale(GameConstants.RenderScale));

        if (_loadError is not null)
        {
            _primitives.FillRect(_spriteBatch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.Paper);
            _primitives.DrawRect(_spriteBatch, new Rectangle(120, 210, 1040, 220), ColorPalette.Coral, 5);
            _spriteBatch.DrawString(Content.Load<SpriteFont>("Fonts/Interface"), "CONTENT ERROR", new Vector2(460, 280), ColorPalette.Ink, 0, Vector2.Zero, 2 * GameConstants.FontDrawScale, SpriteEffects.None, 0);
            _spriteBatch.DrawString(Content.Load<SpriteFont>("Fonts/Interface"), _loadError, new Vector2(120, 350), ColorPalette.Muted, 0, Vector2.Zero, 0.9f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
        }
        else
        {
            if (_session is not null && _state != GameState.MainMenu)
            {
                _gameRenderer.Draw(_spriteBatch, _primitives, _session);
                if (_state == GameState.Playing || _state == GameState.Paused)
                    _debug.Draw(_spriteBatch, _primitives, _session, gameTime.ElapsedGameTime.TotalSeconds > 0 ? (float)(1 / gameTime.ElapsedGameTime.TotalSeconds) : 0);
            }
            _ui.Draw(_spriteBatch, _primitives, _state, _session);
        }

        _spriteBatch.End();

        // Rendering into a fixed 2560x1440 canvas both supersamples the
        // geometric art and clips every primitive before the canvas is
        // letterboxed. Wide roads at x=0 therefore cannot bleed into the bars.
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(ColorPalette.Paper);
        _viewportTransform.Update(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        // The standard color target and non-MSAA backbuffer preserve authored
        // theme values exactly; the 2x scene target provides edge smoothing.
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _spriteBatch.Draw(_sceneTarget, _viewportTransform.DestinationRectangle, Color.White);
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void HandleSettingsAction(UiAction action)
    {
        if (action == UiAction.ApplySettings) ApplyUserSettings();
        else if (action == UiAction.CloseSettings) _state = _settingsReturnState;
    }

    private void ApplyUserSettings()
    {
        _settings.Normalize();
        try
        {
            ApplyGraphicsSettings();
            UserSettingsStore.Save(_settings);
            _ui.SetSettingsStatus("Settings saved. Tactical canvas, geometry, and palette are unchanged.");
        }
        catch (Exception exception)
        {
            _settings.Fullscreen = false;
            _settings.WindowWidth = GameConstants.LogicalWidth;
            _settings.WindowHeight = GameConstants.LogicalHeight;
            try { ApplyGraphicsSettings(); } catch { }
            try { UserSettingsStore.Save(_settings); } catch { }
            _ui.SetSettingsStatus($"Display mode was unsupported; restored 1280 x 720 windowed. {exception.GetBaseException().Message}");
        }
    }

    private void ApplyGraphicsSettings()
    {
        _graphics.PreferredBackBufferWidth = _settings.WindowWidth;
        _graphics.PreferredBackBufferHeight = _settings.WindowHeight;
        _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = _settings.Fullscreen;
        _graphics.ApplyChanges();
        _gameRenderer.ReducedEffects = _settings.ReducedEffects;
        if (_audio is not null) _audio.Volume = _settings.SfxVolume;
    }

    private void AssignSession(GameSession? session)
    {
        _session = session;
        if (session is not null) _audio?.Attach(session);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupNetwork();
            _audio?.Dispose();
            _sceneTarget?.Dispose();
            _primitives?.Dispose();
        }
        base.Dispose(disposing);
    }
}
