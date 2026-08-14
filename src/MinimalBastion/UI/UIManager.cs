using MinimalBastion.Core;
using MinimalBastion.Analytics;
using MinimalBastion.Data;
using MinimalBastion.Multiplayer;
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
    CoOp,
    HostCoOp,
    JoinCoOp,
    Pause,
    Resume,
    SaveGame,
    LoadGame,
    Restart,
    ViewBattlefield,
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
    private string? _hoveredTowerCardId;
    private string? _specializationHint;
    private PowerNodeData? _hoveredPowerNode;
    private readonly List<(string Id, string Name, int PowerNodes)> _maps = new();
    private int _selectedMapIndex;
    private TacticalPlacementKind _hoveredTacticalPlacement;
    private string _joinHostInput = "";
    private string _joinCodeInput = "";
    private bool _editingJoinCode;
    private int _coOpWaveReadyMask;
    private bool _coOpWaveStartQueued;
    private bool _coOpPeerConnected;
    private bool _coOpResyncing;
    private bool _saveAvailable;
    private string _persistenceStatus = "Progress checkpoints are stored between waves.";
    private readonly Rectangle _mapButton = new(500, 370, 280, 40);
    private readonly Rectangle _continueButton = new(500, 420, 280, 44);
    private readonly Rectangle _playButton = new(500, 474, 280, 44);
    private readonly Rectangle _coOpButton = new(500, 528, 280, 44);
    private readonly Rectangle _quitButton = new(500, 582, 280, 40);
    private readonly Rectangle _joinHostField = new(500, 264, 280, 46);
    private readonly Rectangle _joinCodeField = new(500, 330, 280, 46);
    private readonly Rectangle _hostCoOpButton = new(500, 396, 280, 46);
    private readonly Rectangle _joinCoOpButton = new(500, 452, 280, 46);
    private readonly Rectangle _backButton = new(500, 518, 280, 44);
    private readonly Rectangle _resumeButton = new(500, 270, 280, 46);
    private readonly Rectangle _saveButton = new(500, 326, 280, 46);
    private readonly Rectangle _loadButton = new(500, 382, 280, 46);
    private readonly Rectangle _restartButton = new(500, 438, 280, 46);
    private readonly Rectangle _mainMenuButton = new(500, 494, 280, 46);
    private readonly Rectangle _resultReviewButton = new(296, 580, 206, 46);
    private readonly Rectangle _resultRestartButton = new(518, 580, 206, 46);
    private readonly Rectangle _resultMenuButton = new(740, 580, 206, 46);
    private readonly Rectangle _reviewResultsButton = new(450, 9, 170, 38);

    public string JoinHostInput => _joinHostInput;
    public string JoinCodeInput => _joinCodeInput;
    public string CoOpLobbyTitle { get; private set; } = "PREPARING ONLINE CO-OP";
    public string CoOpLobbyDetail { get; private set; } = "Starting the internet connection...";
    public string CoOpLobbyCode { get; private set; } = "";
    public string SelectedMapId => _maps.Count == 0 ? "foundry_loop" : _maps[_selectedMapIndex].Id;
    public string SelectedMapName => _maps.Count == 0 ? "Foundry Loop" : _maps[_selectedMapIndex].Name;

    public static string PauseCheckpointStatus(bool canSave) => canSave
        ? "Between waves - checkpoint ready."
        : "Active wave - saving unlocks after it clears.";

    public UIManager(SpriteFont font) => _font = font;

    public void SetSaveState(bool available, string? status = null)
    {
        _saveAvailable = available;
        if (!string.IsNullOrWhiteSpace(status)) _persistenceStatus = status;
    }

    public void ConfigureMaps(IEnumerable<MapDefinition> maps)
    {
        _maps.Clear();
        _maps.AddRange(maps.OrderBy(x => x.Id.Equals("foundry_loop", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.DisplayName).Select(x => (x.Id, x.DisplayName, x.PowerNodes.Count)));
        _selectedMapIndex = Math.Clamp(_selectedMapIndex, 0, Math.Max(0, _maps.Count - 1));
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
        if (_continueButton.Contains(point) && _saveAvailable) return UiAction.LoadGame;
        if (_playButton.Contains(point)) return UiAction.Play;
        if (_coOpButton.Contains(point)) return UiAction.CoOp;
        if (_quitButton.Contains(point)) return UiAction.Exit;
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

    public void SetCoOpWaveReadyState(int readyMask, bool startQueued)
    {
        _coOpWaveReadyMask = readyMask & 0b11;
        _coOpWaveStartQueued = startQueued;
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
        if (session.SelectedTower is { RequiresSpecialization: true } branchPreview)
        {
            if (_specializationAButton.Contains(point) && branchPreview.Definition.Specializations.Count > 0)
                _specializationHint = TowerInfo.SpecializationSummary(branchPreview.Level, branchPreview.Definition.Specializations[0]);
            else if (_specializationBButton.Contains(point) && branchPreview.Definition.Specializations.Count > 1)
                _specializationHint = TowerInfo.SpecializationSummary(branchPreview.Level, branchPreview.Definition.Specializations[1]);
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

        foreach (var pair in _towerCards)
        {
            if (!pair.Value.Contains(point)) continue;
            session.BeginPlacement(pair.Key);
            return UiAction.None;
        }

        if (session.SelectedTower is { RequiresSpecialization: true } branchingTower &&
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

    private static void RequestOverdrive(MinimalBastion.GameSession session, Action<GameCommand>? sink, int playerId)
    {
        if (session.SelectedTower is not { IsSupport: false } tower) return;
        if (sink is null)
        {
            session.TryOverdriveTower(tower.Id, playerId);
            return;
        }
        sink(new GameCommand { PlayerId = playerId, Type = GameCommandType.OverdriveTower, EntityId = tower.Id });
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
        if (input.EscapePressed || input.PausePressed) return UiAction.Resume;
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_resumeButton.Contains(point)) return UiAction.Resume;
        if (_saveButton.Contains(point) && session.CanSaveCheckpoint) return UiAction.SaveGame;
        if (_loadButton.Contains(point) && _saveAvailable) return UiAction.LoadGame;
        if (_restartButton.Contains(point)) return UiAction.Restart;
        if (_mainMenuButton.Contains(point)) return UiAction.MainMenu;
        return UiAction.None;
    }

    public UiAction HandleResultInput(InputSnapshot input)
    {
        if (!input.LeftPressed) return UiAction.None;
        var point = input.MousePosition.ToPoint();
        if (_resultReviewButton.Contains(point)) return UiAction.ViewBattlefield;
        if (_resultRestartButton.Contains(point)) return UiAction.Restart;
        if (_resultMenuButton.Contains(point)) return UiAction.MainMenu;
        return UiAction.None;
    }

    public UiAction HandleBattlefieldReviewInput(InputSnapshot input, MinimalBastion.GameSession session)
    {
        _hoveredTowerCardId = null;
        _hoveredTacticalPlacement = TacticalPlacementKind.None;
        _hoveredPowerNode = session.Map.Definition.PowerNodes.FirstOrDefault(node =>
            Vector2.DistanceSquared(node.Position.ToVector2(), input.MousePosition) <= node.Radius * node.Radius);
        if (input.EscapePressed) return UiAction.ViewResults;
        return input.LeftPressed && _reviewResultsButton.Contains(input.MousePosition.ToPoint())
            ? UiAction.ViewResults
            : UiAction.None;
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer p, GameState state, MinimalBastion.GameSession? session)
    {
        if (state == GameState.MainMenu)
        {
            DrawMainMenu(batch, p);
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
        else if (state == GameState.BattlefieldReview) DrawBattlefieldReviewBanner(batch, p);
        else if (state == GameState.Victory) DrawResultOverlay(batch, p, session, true);
        else if (state == GameState.Defeat) DrawResultOverlay(batch, p, session, false);
    }

    private void DrawHud(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.TopBarHeight), ColorPalette.Navy);
        p.FillRect(batch, new Rectangle(0, GameConstants.TopBarHeight - 2, GameConstants.LogicalWidth, 2), ColorPalette.Cyan);
        DrawText(batch, "LIVES", new Vector2(18, 8), ColorPalette.Coral, 0.75f);
        DrawText(batch, $"{session.Economy.Lives}/{session.Economy.StartingLives}", new Vector2(18, 26), ColorPalette.Paper, 1f);
        DrawText(batch, "CREDITS", new Vector2(115, 8), ColorPalette.Gold, 0.75f);
        DrawText(batch, session.Economy.Credits.ToString(), new Vector2(115, 26), ColorPalette.Paper, 1f);
        DrawText(batch, "WAVE", new Vector2(225, 8), ColorPalette.Cyan, 0.75f);
        DrawText(batch, $"{session.CurrentWave}/{session.TotalWaves}", new Vector2(225, 26), ColorPalette.Paper, 1f);
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
            var otherPlayer = session.LocalPlayerId == 1 ? 2 : 1;
            var localReady = (_coOpWaveReadyMask & localBit) != 0;
            var otherReady = (_coOpWaveReadyMask & (1 << (otherPlayer - 1))) != 0;
            var readyLabel = session.IntermissionRemaining > 0 ? $"READY +{GameConstants.EarlyStartBonus}" : "READY WAVE";
            startWaveLabel = _coOpWaveStartQueued ? "STARTING..." :
                localReady ? $"WAITING P{otherPlayer}" :
                otherReady ? $"JOIN P{otherPlayer}" : readyLabel;
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
        _overdriveButton = new Rectangle(972, 166, 296, 28);
        var defense = session.Content.Tactics.EmergencyDefense;
        var emergencyReady = session.EmergencyInventory > 0 || session.Economy.CanAfford(defense.PurchaseCost);
        var forge = session.Generator;
        var emergencyLabel = forge is null
            ? session.EmergencyInventory > 0
                ? $"[Q] PLATES {session.EmergencyInventory}   |   PLACE STORED"
                : $"[Q] BUY PULSE PLATE   {defense.PurchaseCost}"
            : session.EmergencyInventory > 0
                ? $"[Q] PLATES {session.EmergencyInventory}/{forge.Level.Capacity}   |   PLACE STORED"
                : $"[Q] PLATES 0/{forge.Level.Capacity}   |   BUY {defense.PurchaseCost}";
        DrawButton(batch, p, _emergencyButton, emergencyLabel, emergencyReady, ColorPalette.Gold);

        var generator = session.Content.Tactics.Generator;
        var generatorReady = session.Generator is not null || session.Economy.CanAfford(generator.PurchaseCost);
        var generatorLabel = session.Generator is { } active
            ? session.EmergencyInventory >= active.Level.Capacity
                ? $"[G] FORGE L{active.LevelIndex + 1}   |   STORAGE FULL"
                : session.Waves.IsActive
                    ? $"[G] FORGE L{active.LevelIndex + 1}   |   +1 IN {active.ProductionRemaining:0}s"
                    : $"[G] FORGE L{active.LevelIndex + 1}   |   PAUSED {active.ProductionRemaining:0}s"
            : $"[G] CHARGE FORGE   {generator.PurchaseCost}   |   WAVE-POWERED";
        DrawButton(batch, p, _generatorButton, generatorLabel, generatorReady, ColorPalette.Green);

        var selected = session.SelectedTower;
        var activeOverdrive = session.Towers.FirstOrDefault(x => x.IsOverdriven);
        var overdriveReady = selected is { IsSupport: false } && session.OverdriveCooldownRemaining <= 0 && !selected.IsOverdriven;
        var overdriveLabel = activeOverdrive is not null ? $"[E] OVERDRIVE ACTIVE   |   {activeOverdrive.OverdriveRemaining:0.0}s" :
            session.OverdriveCooldownRemaining > 0 ? $"[E] OVERDRIVE COOLDOWN   |   {session.OverdriveCooldownRemaining:0.0}s" :
            selected is null ? "[E] OVERDRIVE READY   |   SELECT A TOWER" :
            selected.IsSupport ? "[E] OVERDRIVE   COMBAT TOWERS ONLY" :
            $"[E] OVERDRIVE   {selected.Definition.DisplayName.ToUpperInvariant()}";
        DrawButton(batch, p, _overdriveButton, overdriveLabel, overdriveReady, ColorPalette.Coral);
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
            var p1Ready = (_coOpWaveReadyMask & 0b01) != 0 ? "READY" : "WAIT";
            var p2Ready = (_coOpWaveReadyMask & 0b10) != 0 ? "READY" : "WAIT";
            DrawText(batch, session.CanStartWave ? $"P1 {p1Ready}  |  P2 {p2Ready}" : "SHARED WAVE IN PROGRESS", new Vector2(1210, 82), ColorPalette.Gold, 0.35f, true);
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
            var affordable = session.Economy.CanAfford(definition.PurchaseCost);
            var selected = session.PlacementTowerId == definition.Id;
            var cardFill = selected ? ColorPalette.Tint(definition.Visual.PrimaryColor, 0.42f) : ColorPalette.PanelAlt;
            var cardOutline = selected ? definition.Visual.PrimaryColor : affordable ? ColorPalette.CardOutline : ColorPalette.Coral;
            p.FillRect(batch, rect, cardFill);
            p.DrawRect(batch, rect, cardOutline, selected ? 2 : 1);
            p.DrawShape(batch, new Vector2(rect.X + 17, rect.Center.Y), 10, definition.Visual.Shape, definition.Visual.PrimaryColor, definition.Visual.AccentColor, 1, true, levelMarks: true);
            DrawText(batch, index == 9 ? "0" : (index + 1).ToString(), new Vector2(rect.Right - 10, rect.Y + 4), selected ? definition.Visual.AccentColor : ColorPalette.Muted, 0.43f, true);
            DrawText(batch, definition.DisplayName, new Vector2(rect.X + 38, rect.Y + 5), ColorPalette.Ink, 0.53f);
            DrawText(batch, $"{definition.PurchaseCost}  {TowerInfo.ShortRole(definition)}", new Vector2(rect.X + 38, rect.Y + 21), affordable ? ColorPalette.Muted : ColorPalette.Coral, 0.44f);
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

        p.FillRect(batch, new Rectangle(972, 474, 296, 156), ColorPalette.PanelAlt);
        p.DrawRect(batch, new Rectangle(972, 474, 296, 156), tower.Definition.Visual.PrimaryColor, 1);
        p.DrawShape(batch, new Vector2(1000, 512), tower.Definition.Visual.Radius, tower.Definition.Visual.Shape,
            tower.Definition.Visual.PrimaryColor, tower.Definition.Visual.AccentColor, tower.LevelIndex + 1, true, levelMarks: true);
        DrawText(batch, tower.Definition.DisplayName, new Vector2(1036, 486), ColorPalette.Ink, 0.86f);
        var ownership = session.IsCoOp ? $"   PLACED P{tower.OwnerPlayerId}" : "";
        var levelTitle = tower.Specialization is { } chosen ? chosen.DisplayName.ToUpperInvariant() : $"LEVEL {tower.LevelIndex + 1}";
        DrawText(batch, $"{levelTitle}   {TowerInfo.ShortRole(tower.Definition)}{ownership}", new Vector2(1036, 508), ColorPalette.Muted, 0.60f);
        var effectiveDamage = session.GetEffectiveDamage(tower, tower.Level.Damage);
        var effectiveRate = session.GetEffectiveAttacksPerSecond(tower);
        var effectiveDps = effectiveDamage * effectiveRate * Math.Max(1, tower.Level.PelletCount);
        DrawText(batch, tower.IsSupport
            ? $"Aura {tower.Level.AuraRange:0}   Rate +{tower.Level.AuraAttackSpeedBonus:P0}"
            : $"ACTIVE  DMG {effectiveDamage:0.#}   DPS {effectiveDps:0.#}   RNG {session.GetEffectiveRange(tower):0}", new Vector2(980, 552), ColorPalette.Ink, 0.58f);
        DrawText(batch, TowerInfo.Special(tower.Definition, tower.Level), new Vector2(980, 573), ColorPalette.Ink, 0.58f);
        var power = session.Map.GetPowerBuff(tower.Position);
        var powerNodes = session.Map.GetPowerNodes(tower.Position);
        var powerHint = powerNodes.Count > 0
            ? $"{PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}  |  {TowerInfo.PowerNodeStatChange(tower.Definition, tower.Level, power)}"
            : null;
        var overdriveHint = tower.IsOverdriven ? $"OVERDRIVE ACTIVE  {tower.OverdriveRemaining:0.0}s  RATE +{GameConstants.OverdriveAttackSpeedBonus:P0}" : null;
        var contextualHint = powerHint ?? overdriveHint ?? TowerInfo.Strength(tower.Definition);
        DrawText(batch, contextualHint, new Vector2(980, 594), powerHint is not null ? powerNodes[0].NodeColor : overdriveHint is not null ? ColorPalette.Coral : ColorPalette.Muted, powerHint is not null ? 0.45f : 0.55f);
        var upgradeLine = _specializationHint ?? (tower.RequiresSpecialization
            ? "CHOOSE A FINAL SPECIALIZATION"
            : tower.CanUpgrade ? $"NEXT {tower.UpgradeCost}: {TowerInfo.UpgradeSummary(tower.Definition, tower.LevelIndex)}" : "MAXIMUM LEVEL");
        DrawText(batch, upgradeLine, new Vector2(980, 615), _specializationHint is not null ? ColorPalette.Cobalt : tower.RequiresSpecialization || tower.CanUpgrade ? ColorPalette.Violet : ColorPalette.Muted, 0.52f);

        _targetButton = new Rectangle(980, 646, 88, 30);
        _upgradeButton = new Rectangle(1074, 646, 92, 30);
        _sellButton = new Rectangle(1172, 646, 94, 30);
        _specializationAButton = Rectangle.Empty;
        _specializationBButton = Rectangle.Empty;
        const bool canManage = true;
        if (tower.RequiresSpecialization)
        {
            _upgradeButton = Rectangle.Empty;
            _targetButton = new Rectangle(980, 646, 88, 30);
            _specializationAButton = new Rectangle(1074, 628, 118, 28);
            _specializationBButton = new Rectangle(1074, 660, 118, 28);
            _sellButton = new Rectangle(1198, 646, 68, 30);
            var first = tower.Definition.Specializations[0];
            var second = tower.Definition.Specializations[1];
            DrawButton(batch, p, _targetButton, tower.TargetMode.ToString().ToUpperInvariant(), true, ColorPalette.Cyan);
            DrawButton(batch, p, _specializationAButton, $"{first.ShortLabel.ToUpperInvariant()} {first.UpgradeCost}", canManage && session.Economy.CanAfford(first.UpgradeCost), tower.Definition.Visual.PrimaryColor);
            DrawButton(batch, p, _specializationBButton, $"{second.ShortLabel.ToUpperInvariant()} {second.UpgradeCost}", canManage && session.Economy.CanAfford(second.UpgradeCost), ColorPalette.Violet);
            DrawButton(batch, p, _sellButton, $"SELL {tower.SellValue}", canManage, ColorPalette.Orange);
            return;
        }
        if (!tower.IsSupport) DrawButton(batch, p, _targetButton, tower.TargetMode.ToString().ToUpperInvariant(), true, ColorPalette.Cyan);
        DrawButton(batch, p, _upgradeButton, tower.CanUpgrade ? $"UP {tower.UpgradeCost}" : "MAX", canManage && tower.CanUpgrade && session.Economy.CanAfford(tower.UpgradeCost), ColorPalette.Violet);
        DrawButton(batch, p, _sellButton, $"SELL {tower.SellValue}", canManage, ColorPalette.Orange);
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
        DrawText(batch, definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase)
            ? $"AURA {level.AuraRange:0}   RATE +{level.AuraAttackSpeedBonus:P0}   RNG +{level.AuraRangeBonus:P0}"
            : $"DMG {level.Damage:0.#}   DPS {TowerInfo.RawDps(level):0.#}   RATE {level.AttacksPerSecond:0.##}/s   RNG {level.Range:0}", new Vector2(980, 542), ColorPalette.Ink, 0.57f);
        DrawText(batch, TowerInfo.Special(definition, level), new Vector2(980, 565), ColorPalette.Ink, 0.57f);
        var powerNodes = placing ? session.Map.GetPowerNodes(session.PlacementPosition) : Array.Empty<PowerNodeData>();
        if (powerNodes.Count > 0)
        {
            var power = session.Map.GetPowerBuff(session.PlacementPosition);
            DrawText(batch, $"ON {PowerNodeNames(powerNodes)}  {string.Join("  ", powerNodes.Select(TowerInfo.PowerNodeBonus))}", new Vector2(980, 590), powerNodes[0].NodeColor, 0.49f);
            DrawText(batch, TowerInfo.PowerNodeStatChange(definition, level, power), new Vector2(980, 612), ColorPalette.Cobalt, 0.52f);
        }
        else
        {
            DrawText(batch, TowerInfo.Strength(definition), new Vector2(980, 590), ColorPalette.Muted, 0.54f);
            DrawText(batch, TowerInfo.Limitation(definition), new Vector2(980, 612), ColorPalette.Muted, 0.54f);
        }
        DrawText(batch, $"L2 {level.UpgradeCost}: {TowerInfo.UpgradeSummary(definition, 0)}", new Vector2(980, 638), ColorPalette.Violet, 0.51f);
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
        DrawText(batch, $"STORED {session.EmergencyInventory}   DIRECT {definition.PurchaseCost}", new Vector2(1028, 508), ColorPalette.Muted, 0.60f);
        var bonus = session.Generator?.Level.DefenseDamageBonus ?? 0;
        DrawText(batch, $"{definition.Charges} PULSES   DMG {definition.Damage * (1 + bonus):0.#}   BLAST {definition.BlastRadius:0}", new Vector2(980, 542), ColorPalette.Ink, 0.59f);
        DrawText(batch, $"Stuns {definition.StunDuration:0.#}s   Armor pierce {definition.ArmorPierce:0}", new Vector2(980, 565), ColorPalette.Ink, 0.57f);
        DrawText(batch, "Strength: catches clustered leaks instantly", new Vector2(980, 590), ColorPalette.Muted, 0.54f);
        DrawText(batch, "Limit: consumed after two pulses; weak economy", new Vector2(980, 612), ColorPalette.Muted, 0.52f);
        DrawText(batch, session.Generator is null ? "A Charge Forge replenishes stored plates." : $"Forge boost: +{bonus:P0} plate damage", new Vector2(980, 638), ColorPalette.Green, 0.53f);
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
        DrawText(batch, $"Storage {session.EmergencyInventory}/{level.Capacity}   Plate DMG +{level.DefenseDamageBonus:P0}", new Vector2(980, 571), ColorPalette.Ink, 0.57f);
        DrawText(batch, "Strength: renewable emergency reserves", new Vector2(980, 594), ColorPalette.Muted, 0.54f);

        if (active is null)
        {
            _upgradeButton = Rectangle.Empty;
            _sellButton = Rectangle.Empty;
            DrawText(batch, "Limit: high cost; produces no direct damage", new Vector2(980, 616), ColorPalette.Muted, 0.52f);
            var next = definition.Levels[1];
            DrawText(batch, $"L2 {level.UpgradeCost}: {next.ProductionSeconds:0}s   CAP {next.Capacity}   DMG +{next.DefenseDamageBonus:P0}", new Vector2(980, 638), ColorPalette.Violet, 0.52f);
            DrawText(batch, session.TacticalPlacement == TacticalPlacementKind.ChargeForge ? "CLICK A BUILD ZONE   |   ESC TO CANCEL" : "G OR CLICK ABOVE TO PREPARE", new Vector2(980, 658), ColorPalette.Cobalt, 0.49f);
            return;
        }

        DrawText(batch, active.CanUpgrade
            ? $"NEXT {active.UpgradeCost}: {definition.Levels[active.LevelIndex + 1].ProductionSeconds:0}s   CAP {definition.Levels[active.LevelIndex + 1].Capacity}   DMG +{definition.Levels[active.LevelIndex + 1].DefenseDamageBonus:P0}"
            : "MAXIMUM LEVEL", new Vector2(980, 615), active.CanUpgrade ? ColorPalette.Violet : ColorPalette.Muted, 0.49f);
        _upgradeButton = new Rectangle(1074, 646, 92, 30);
        _sellButton = new Rectangle(1172, 646, 94, 30);
        const bool canManage = true;
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
            TacticalPlacementKind.PulsePlate => $"VALID - BUY & DEPLOY {session.Content.Tactics.EmergencyDefense.PurchaseCost}",
            TacticalPlacementKind.ChargeForge => "VALID - BUILD CHARGE FORGE",
            _ => "VALID - CLICK TO DEPLOY"
        };
        var message = !pointerOnMap ? "MOVE CURSOR ONTO MAP" : valid ? validMessage : PlacementMessage(session.PlacementFailure);
        DrawText(batch, message, new Vector2(rect.Center.X, rect.Center.Y), ColorPalette.Paper, 0.58f, true);
    }

    private static bool IsPointerOnMap(Vector2 position) =>
        position.X >= 0 && position.X < GameConstants.MapWidth &&
        position.Y >= GameConstants.TopBarHeight && position.Y < GameConstants.LogicalHeight;

    private static string PlacementMessage(PlacementFailure failure) => failure switch
    {
        PlacementFailure.OutsideBuildableRegion => "MOVE INTO A BUILD ZONE",
        PlacementFailure.BlocksPath => "TOO CLOSE TO THE ROAD",
        PlacementFailure.OverlapsTower => "TOO CLOSE TO ANOTHER TOWER",
        PlacementFailure.TooCloseToEdge => "TOO CLOSE TO THE MAP EDGE",
        PlacementFailure.InsufficientCredits => "INSUFFICIENT CREDITS",
        PlacementFailure.MustBeOnPath => "PULSE PLATES DEPLOY ON THE ROAD",
        PlacementFailure.TooCloseToPathEndpoint => "MOVE AWAY FROM ENTRY OR EXIT",
        PlacementFailure.OverlapsDefense => "TOO CLOSE TO ANOTHER PLATE",
        PlacementFailure.GeneratorAlreadyBuilt => "ONLY ONE CHARGE FORGE IS ALLOWED",
        PlacementFailure.NoDefenseAvailable => "NO STORED PLATE - NEED 70 CREDITS",
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
        var map = _maps.Count == 0 ? (Id: "foundry_loop", Name: "Foundry Loop", PowerNodes: 0) : _maps[_selectedMapIndex];
        var mapSuffix = map.PowerNodes > 0 ? $"{map.PowerNodes} SURGE NODES" : "CLASSIC";
        DrawButton(batch, p, _mapButton, $"{_selectedMapIndex + 1}/{Math.Max(1, _maps.Count)}  {map.Name.ToUpperInvariant()}  •  {mapSuffix}", true, ColorPalette.Violet);
        DrawButton(batch, p, _continueButton, "CONTINUE CHECKPOINT", _saveAvailable, ColorPalette.Green);
        DrawButton(batch, p, _playButton, "NEW GAME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _coOpButton, "ONLINE CO-OP", true, ColorPalette.Green);
        DrawButton(batch, p, _quitButton, "QUIT", true, ColorPalette.Coral);
        DrawText(batch, "Click the map selector to change arenas", new Vector2(640, 646), ColorPalette.Muted, 0.54f, true);
        DrawText(batch, "Left click places/selects   \u2022   Right click cancels   \u2022   Escape pauses", new Vector2(640, 670), ColorPalette.Navy, 0.61f, true);
    }

    private void DrawCoOpMenu(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawMenuFrame(batch, p);
        DrawText(batch, "ONLINE CO-OP", new Vector2(640, 120), ColorPalette.Ink, 1.9f, true);
        DrawText(batch, "Direct internet play. The host forwards TCP 28741; the friend enters the address and code.", new Vector2(640, 164), ColorPalette.Muted, 0.60f, true);
        DrawText(batch, $"HOST MAP  {SelectedMapName.ToUpperInvariant()}", new Vector2(640, 194), ColorPalette.Violet, 0.58f, true);

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
        DrawText(batch, "Both players ready waves. Middle-click the battlefield to ping.", new Vector2(640, 613), ColorPalette.Muted, 0.54f, true);
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
        DrawText(batch, victory ? "All twenty waves neutralized." : $"Defense collapsed during wave {session.CurrentWave}.", new Vector2(640, 142), ColorPalette.Muted, 0.72f, true);

        DrawResultStatCard(batch, p, new Rectangle(296, 172, 158, 58), "WAVE", $"{session.CurrentWave}/{session.TotalWaves}", ColorPalette.Cyan);
        DrawResultStatCard(batch, p, new Rectangle(472, 172, 158, 58), "LIVES", $"{session.Economy.Lives}/{session.Economy.StartingLives}", ColorPalette.Coral);
        DrawResultStatCard(batch, p, new Rectangle(648, 172, 158, 58), "KILLS", session.Economy.TotalKills.ToString(), ColorPalette.Green);
        DrawResultStatCard(batch, p, new Rectangle(824, 172, 158, 58), "LEAKS", session.Economy.EscapedEnemies.ToString(), ColorPalette.Orange);

        DrawTowerContribution(batch, p, session.Statistics, new Rectangle(296, 250, 410, 298));
        DrawRunSummary(batch, p, session, new Rectangle(724, 250, 258, 298));

        DrawButton(batch, p, _resultReviewButton, "VIEW FINAL FIELD", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _resultRestartButton, session.IsCoOp ? "END SESSION" : "RESTART", true, victory ? ColorPalette.Green : ColorPalette.Cobalt);
        DrawButton(batch, p, _resultMenuButton, "MAIN MENU", true, ColorPalette.Violet);
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

        var maximum = MathF.Max(1, leaders[0].Damage);
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
            DrawTextRight(batch, $"{tower.Damage:0} DMG   {tower.Kills} KILLS", new Vector2(rect.Right - 14, y), ColorPalette.Muted, 0.50f);
            var bar = new Rectangle(rect.X + 14, y + 24, rect.Width - 28, 9);
            p.FillRect(batch, bar, ColorPalette.Disabled);
            p.FillRect(batch, new Rectangle(bar.X, bar.Y, Math.Max(2, (int)(bar.Width * tower.Damage / maximum)), bar.Height), color);
        }

        var strongest = leaders[0];
        DrawText(batch, $"TOP UNIT  {strongest.DisplayName}   |   {strongest.DamagePerCredit:0.0} DMG / CREDIT", new Vector2(rect.X + 14, rect.Bottom - 30), ColorPalette.Violet, 0.52f);
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

    private void DrawBattlefieldReviewBanner(SpriteBatch batch, PrimitiveRenderer p)
    {
        DrawButton(batch, p, _reviewResultsButton, "VIEW RESULTS", true, ColorPalette.Cobalt);
        var rect = new Rectangle(326, 64, 308, 28);
        p.FillRect(batch, rect, ColorPalette.WithAlpha(ColorPalette.Navy, 226));
        p.DrawRect(batch, rect, ColorPalette.Green, 2);
        DrawText(batch, "FINAL FIELD  •  SIMULATION FROZEN", new Vector2(rect.Center.X, rect.Center.Y), ColorPalette.Paper, 0.53f, true);
    }

    private void DrawPauseOverlay(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session)
    {
        p.FillRect(batch, new Rectangle(0, 0, GameConstants.LogicalWidth, GameConstants.LogicalHeight), ColorPalette.WithAlpha(ColorPalette.Navy, 220));
        p.FillRect(batch, new Rectangle(360, 130, 560, 470), ColorPalette.Paper);
        p.FillRect(batch, new Rectangle(360, 130, 560, 10), ColorPalette.Cobalt);
        p.DrawRect(batch, new Rectangle(360, 130, 560, 470), ColorPalette.Ink, 2);
        DrawText(batch, "PAUSED", new Vector2(640, 190), ColorPalette.Ink, 1.8f, true);
        DrawText(batch, PauseCheckpointStatus(session.CanSaveCheckpoint), new Vector2(640, 230), ColorPalette.Muted, 0.70f, true);
        DrawButton(batch, p, _resumeButton, "RESUME", true, ColorPalette.Cobalt);
        DrawButton(batch, p, _saveButton, session.CanSaveCheckpoint ? "SAVE CHECKPOINT" : "SAVE BETWEEN WAVES", session.CanSaveCheckpoint, ColorPalette.Green);
        DrawButton(batch, p, _loadButton, "LOAD CHECKPOINT", _saveAvailable, ColorPalette.Violet);
        DrawButton(batch, p, _restartButton, "RESTART", true, ColorPalette.Orange);
        DrawButton(batch, p, _mainMenuButton, "MAIN MENU", true, ColorPalette.Coral);
        DrawText(batch, _persistenceStatus, new Vector2(640, 566), ColorPalette.Muted, 0.55f, true);
    }

    private void DrawButton(SpriteBatch batch, PrimitiveRenderer p, Rectangle rect, string text, bool enabled, Color fillColor)
    {
        if (string.IsNullOrEmpty(text)) return;
        var background = enabled ? fillColor : ColorPalette.Disabled;
        p.FillRect(batch, rect, background);
        p.DrawRect(batch, rect, enabled ? ColorPalette.Ink : ColorPalette.Muted, enabled ? 2 : 1);
        var scale = 0.65f;
        var measured = _font.MeasureString(text).X * scale * GameConstants.FontDrawScale;
        if (measured > rect.Width - 12) scale *= (rect.Width - 12) / measured;
        DrawText(batch, text, new Vector2(rect.Center.X, rect.Center.Y), enabled ? ColorPalette.Paper : ColorPalette.Muted, MathF.Max(0.38f, scale), true);
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
            "splash_projectile" => $"Splash {level.SplashRadius:0} px",
            "beam" => $"Expose +{level.ExposePercent:P0} all incoming damage",
            _ => "Reliable direct fire"
        };
    }

    private void DrawText(SpriteBatch batch, string text, Vector2 position, Color color, float scale, bool centered = false)
    {
        var origin = centered ? _font.MeasureString(text) * 0.5f : Vector2.Zero;
        batch.DrawString(_font, text, position, color, 0, origin, scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }

    private void DrawTextRight(SpriteBatch batch, string text, Vector2 position, Color color, float scale)
    {
        var size = _font.MeasureString(text);
        batch.DrawString(_font, text, position, color, 0, new Vector2(size.X, 0), scale * GameConstants.FontDrawScale, SpriteEffects.None, 0);
    }
}
