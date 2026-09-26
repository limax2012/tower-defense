using System.Text.Json;
using System.Text.Json.Serialization;
using MaximalBastion.Core;
using MaximalBastion.Analytics;
using MaximalBastion.Data;
using MaximalBastion.Enemies;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Towers;
using MaximalBastion.UI;
using MaximalBastion.Waves;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Mobile;

/// <summary>Local presentation adapter. All gameplay mutations use the shared session and command processor.</summary>
public sealed class MobileSessionController
{
    public const int ProtocolVersion = 1;
    public const int MaximumRequestCharacters = 16_384;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly GameContent _content;
    private readonly SaveSlotRepository _saves;
    private readonly RunHistoryRepository _history;
    private readonly UserSettingsRepository _settingsStore;
    private readonly Dictionary<long, string> _receipts = new();
    private readonly Queue<long> _receiptOrder = new();
    private long _requestFloor;
    private int _autosavedWave = -1;
    private string? _recordedResult;
    private float _accumulator;
    private AuthoritativeCommandHost _commands = new();

    public MobileSessionController(GameContent content, string persistenceRoot)
    {
        _content = content;
        _saves = new SaveSlotRepository(persistenceRoot);
        _history = new RunHistoryRepository(persistenceRoot);
        _settingsStore = new UserSettingsRepository(persistenceRoot);
        Settings = _settingsStore.Load();
    }

    public GameSession? Session { get; private set; }
    public UserSettings Settings { get; private set; }
    public bool Paused { get; private set; }
    public bool Suspended { get; private set; }
    public bool InspectingHistory { get; private set; }
    public string? Notice { get; private set; }
    public event Action<GameSession?>? SessionChanged;

    public void Update(float elapsedSeconds)
    {
        if (Session is not { } session || Suspended || InspectingHistory) return;
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0) return;
        if (Paused)
        {
            session.UpdatePausedIntermission(Math.Min(elapsedSeconds, 0.1f));
            return;
        }
        const float step = 1f / DeterministicSessionRunner.SimulationTicksPerSecond;
        _accumulator = Math.Min(_accumulator + elapsedSeconds, 6 * step);
        while (_accumulator >= step)
        {
            session.TryAutoStartNextWave(Settings.AutoStartWaves, Settings.AutoStartDelaySeconds);
            session.Update(step);
            _accumulator -= step;
        }
        PersistProgress();
    }

    public void SetSuspended(bool suspended)
    {
        Suspended = suspended;
        _accumulator = 0;
        if (suspended)
        {
            Paused = Session is not null;
            PersistProgress(refreshCheckpoint: true);
            Flush();
        }
    }

    public string GetStateJson() => JsonSerializer.Serialize(new
    {
        type = "state", protocol = ProtocolVersion, state = State()
    }, JsonOptions);

    public string HandleRequest(string json)
    {
        long requestId = 0;
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumRequestCharacters)
                throw new ArgumentException("Request exceeds the local bridge limit.");
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var request = document.RootElement;
            requestId = request.GetProperty("id").GetInt64();
            if (requestId <= 0) throw new ArgumentException("A positive request id is required.");
            if (_receipts.TryGetValue(requestId, out var receipt)) return receipt;
            if (requestId <= _requestFloor) throw new ArgumentException("This request has expired.");
            if (request.GetProperty("protocol").GetInt32() != ProtocolVersion)
                throw new ArgumentException("The mobile interface and bundled engine have different bridge versions.");
            var action = Text(request, "action");
            var data = request.TryGetProperty("data", out var payload) ? payload : default;
            if (action is not ("catalog" or "newRun" or "load" or "saves" or "history" or "settings" or "inspect"))
            {
                if (Session is not null && Text(request, "runId") != Session.RunId)
                    throw new InvalidOperationException("The run changed. Please repeat the action.");
            }
            object? result = action switch
            {
                "catalog" => Catalog(),
                "newRun" => NewRun(data),
                "load" => Load(Integer(data, "slot")),
                "saves" => _saves.GetSlots(),
                "save" => Save(Integer(data, "slot", -1)),
                "duplicateSave" => Duplicate(Integer(data, "slot")),
                "deleteSave" => DeleteSave(Integer(data, "slot")),
                "history" => _history.GetEntries(),
                "career" => CareerProgression.Analyze(_history.GetEntries()),
                "inspect" => Inspect(Text(data, "runId")),
                "menu" => MainMenu(),
                "pause" => Pause(Boolean(data, "paused")),
                "suspend" => Suspend(Boolean(data, "suspended")),
                "beginPlacement" => BeginPlacement(data),
                "pointer" => Pointer(data),
                "cancel" => Cancel(),
                "command" => Command(data, requestId),
                "settings" => ConfigureSettings(data),
                "sandbox" => Sandbox(data),
                _ => throw new ArgumentException("Unknown mobile action.")
            };
            var response = JsonSerializer.Serialize(new
            {
                type = "reply", protocol = ProtocolVersion, id = requestId, ok = true, result, state = State()
            }, JsonOptions);
            Remember(requestId, response);
            return response;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or
            KeyNotFoundException or IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            var response = JsonSerializer.Serialize(new
            {
                type = "reply", protocol = ProtocolVersion, id = requestId, ok = false,
                error = exception.Message, state = State()
            }, JsonOptions);
            if (requestId > 0 && requestId > _requestFloor) Remember(requestId, response);
            return response;
        }
    }

    private void Remember(long id, string response)
    {
        _receipts[id] = response;
        _receiptOrder.Enqueue(id);
        while (_receiptOrder.Count > 128)
        {
            var expired = _receiptOrder.Dequeue();
            _receipts.Remove(expired);
            _requestFloor = Math.Max(_requestFloor, expired);
        }
    }

    private object Catalog() => new
    {
        towers = _content.Towers.Values.OrderBy(tower => tower.PurchaseCost).Select(tower => new
        {
            tower.Id, tower.DisplayName, tower.Role, tower.PurchaseCost,
            visual = new { tower.Visual.Shape, tower.Visual.Primary, tower.Visual.Accent },
            stats = TowerInfo.ComparisonStats(tower, tower.Levels[0]),
            protocol = new { tower.Protocol.DisplayName, tower.Protocol.Summary,
                tower.Protocol.DurationSeconds, tower.Protocol.CooldownSeconds,
                auto = TowerInfo.ProtocolAutoTriggerSummary(tower.Protocol) },
            doctrines = tower.Tier2Doctrines.Select(choice => new
            {
                choice.Id, choice.DisplayName, choice.Summary, cost = choice.UpgradeCost,
                stats = TowerInfo.ComparisonStats(tower, tower.Levels[0], tower.Levels[1].WithDoctrine(choice))
            }),
            specializations = tower.Specializations.Select(choice => new
            {
                choice.Id, choice.DisplayName, choice.Summary, cost = choice.UpgradeCost,
                stats = TowerInfo.ComparisonStats(tower, choice.Level)
            }),
            apex = tower.Apex
        }),
        maps = _content.Maps.Values.Select(map => new
        {
            map.Id, map.DisplayName, map.Description, map.Path, map.StartingCredits,
            waves = _content.WaveSets[map.WaveSet].Waves
        }),
        difficulties = _content.Difficulties.Values.Select(difficulty => new
        {
            difficulty.Id, difficulty.DisplayName, difficulty.Description, difficulty.ModifierSummary
        }),
        challenges = _content.Challenges.Values.Select(challenge => new
        {
            challenge.Id, challenge.DisplayName, challenge.Description, challenge.IsSandbox,
            rules = ChallengeRules(challenge, _content.Towers.Count)
        }),
        enemies = _content.Enemies.Values,
        signals = Enum.GetValues<EnemySignalRole>().Where(role => role != EnemySignalRole.None).Select(role => new
        {
            id = role.ToString(), displayName = role + " Signal"
        }),
        tactics = _content.Tactics,
        settings = Settings,
        capabilities = new { onlineCoOp = false, checkpointSaves = true, offline = true }
    };

    private static IReadOnlyList<string> ChallengeRules(ChallengeDefinition challenge, int totalTowerCount)
    {
        if (challenge.IsSandbox)
        {
            return
            [
                "SOLO TEST ENVIRONMENT",
                "UNLIMITED CREDITS + LIVES",
                "FIXED OR IMMORTAL TARGETS",
                "ALL RANKS + SELECTABLE SIGNAL ROLES",
                "WAVE SIGNALS OPTIONAL; OFF BY DEFAULT",
                "RESET TOWER DATA + PROTOCOLS"
            ];
        }
        if (challenge.CounterPressureEnabled)
        {
            return
            [
                $"START CREDITS x{challenge.StartingCreditsMultiplier:0.00}",
                "W2 ACCELERATE / W3 REPAIR / W4 SHIELD",
                "W5 JAMMER WEAKENS EVERY TOWER IN RANGE",
                "ELITE + BOSS PRECISION DISRUPTION",
                "FULL ROSTER + ALL SYSTEMS",
                "RULE: SIGNAL ENEMIES SUPPORT FORMATIONS"
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
            "close_quarters" => "RULE: SIGNAL ENEMIES SUPPORT FORMATIONS",
            "core_six" => "RULE: SIX-TOWER ROSTER LOCK",
            "no_reserves" => "RULE: TOWERS + UPGRADES ONLY; NO SALES",
            _ => $"RULE: {challenge.Description.ToUpperInvariant()}"
        });
        return lines;
    }

    private object State()
    {
        var session = Session;
        if (session is null) return new { screen = "menu", notice = Notice, settings = Settings };
        var wave = session.Waves.ActiveWave ?? session.Waves.NextWave;
        return new
        {
            screen = InspectingHistory ? "inspect" : session.IsDefeat ? "defeat" : session.IsVictory ? "victory" : "playing",
            session.RunId, session.Map.Definition.DisplayName, mapId = session.Map.Definition.Id,
            session.DifficultyId, session.ChallengeId, session.IsSandbox, session.IsEndlessMode,
            session.CurrentWave, session.TotalWaves, credits = session.Economy.Credits, lives = session.Economy.Lives,
            session.Economy.StartingLives, session.EnemiesRemaining, session.CanStartWave,
            session.CanSaveCheckpoint, session.IntermissionRemaining, session.Speed,
            waveButtonLabel = UIManager.SoloWaveButtonLabel(session, Settings.AutoStartWaves, Settings.AutoStartDelaySeconds),
            waveActive = session.Waves.IsActive, paused = Paused, suspended = Suspended,
            mutable = CanMutate, notice = Notice, settings = Settings,
            selected = SelectedTower(session),
            generator = Generator(session),
            placement = new
            {
                towerId = session.PlacementTowerId, kind = session.TacticalPlacement.ToString(),
                active = session.PlacementTowerId is not null || session.TacticalPlacement != TacticalPlacementKind.None,
                session.HasPlacementPreview, x = session.PlacementPreviewPosition.X, y = session.PlacementPreviewPosition.Y,
                failure = session.PlacementFailure.ToString(),
                nodes = session.Map.GetPowerNodes(session.PlacementPreviewPosition).Select(node => new { node.DisplayName })
            },
            availableTowers = _content.Towers.Keys.Where(session.IsTowerAvailable),
            tactical = new
            {
                session.TacticalSystemsEnabled, session.PulsePlatesEnabled, session.ProtocolsEnabled, session.SellingEnabled,
                session.EmergencyInventory, session.CurrentEmergencyDirectPurchaseCost, session.CanDirectPurchaseEmergencyDefense,
                plateLabel = UIManager.PulsePlateButtonLabel(session),
                plateCount = session.EmergencyDefenses.Count, plateCapacity = _content.Tactics.EmergencyDefense.MaximumActive,
                session.OverdriveCooldownRemaining, session.AutoOverdriveTowerId, session.ApexUpgradesUnlocked
            },
            wave = wave is null ? null : new
            {
                intel = WaveIntel.Analyze(wave, _content.Enemies), wave.Groups,
                healthScale = wave.HealthMultiplier * session.Difficulty.EnemyHealthMultiplier,
                speedScale = wave.SpeedMultiplier * session.Difficulty.EnemySpeedMultiplier
            },
            session.AnnouncementTitle, session.AnnouncementSubtitle, session.AnnouncementRemaining,
            sandbox = new { session.SandboxWaveActive, session.SandboxWaveSignalsEnabled },
            result = session.IsDefeat || session.IsVictory ? RunHistoryEntry.FromSession(session) : null
        };
    }

    private object? SelectedTower(GameSession session)
    {
        if (session.SelectedTower is not { } tower) return null;
        var upgrades = new List<object>();
        if (tower.RequiresDoctrine)
            foreach (var choice in tower.Definition.Tier2Doctrines)
                AddUpgrade("ChooseDoctrine", choice.Id, choice.DisplayName, choice.Summary, choice.UpgradeCost,
                    tower.Definition.Levels[1].WithDoctrine(choice));
        else if (tower.RequiresSpecialization)
            foreach (var choice in tower.Definition.Specializations)
                AddUpgrade("SpecializeTower", choice.Id, choice.DisplayName, choice.Summary, choice.UpgradeCost,
                    choice.Level.WithDoctrine(tower.Doctrine));
        else if (tower.CanUpgrade)
            AddUpgrade("UpgradeTower", "", "Upgrade", "Improve this tower's current role.", tower.UpgradeCost,
                tower.Definition.Levels[tower.LevelIndex + 1].WithDoctrine(tower.Doctrine));
        else if (!tower.IsApex && tower.LevelIndex >= tower.Definition.Levels.Count - 1)
            AddUpgrade("UpgradeTower", "apex", "Apex", "Permanent promotion with independent automatic Protocol activation.",
                tower.ApexUpgradeCost, tower.ApexPreviewLevel, session.ApexUpgradesUnlocked);
        return new
        {
            tower.Id, definitionId = tower.Definition.Id, tower.Definition.DisplayName, tower.Definition.Role,
            progression = TowerInfo.ProgressionLabel(tower), tower.IsApex, tower.IsSupport, tower.IsSandboxDisabled,
            tower.IsDisrupted, tower.DisruptionRemaining, tower.IsSuppressed, tower.SuppressionRemaining,
            targetMode = tower.TargetMode.ToString(), targetModes = session.AvailableTargetModes.Select(mode => mode.ToString()),
            stats = Stats(session, tower), upgrades, tower.SellValue, tower.InvestedCredits,
            tower.LifetimeDamage, tower.LifetimeKills, tower.LifetimeControlSeconds, tower.LifetimeSupportDamageEquivalent,
            nodes = session.Map.GetPowerNodes(tower.Position).Select(node => new { node.DisplayName }),
            buffs = TowerInfo.ActiveBoostSources(session.GetSupportBuff(tower), session.Map.GetPowerNodes(tower.Position), false),
            protocol = new
            {
                tower.Protocol.DisplayName, tower.Protocol.Summary, tower.OverdriveRemaining,
                cooldown = Math.Max(session.OverdriveCooldownRemaining, tower.ApexProtocolCooldownRemaining),
                ready = session.ProtocolsEnabled && !tower.IsOverdriven && session.OverdriveCooldownRemaining <= 0 &&
                    tower.ApexProtocolCooldownRemaining <= 0,
                armed = session.AutoOverdriveTowerId == tower.Id,
                self = tower.IsApex,
                auto = TowerInfo.ProtocolAutoTriggerSummary(tower.Protocol)
            }
        };

        void AddUpgrade(string command, string id, string name, string summary, int cost, TowerLevelDefinition preview,
            bool unlocked = true) => upgrades.Add(new
        {
            command, id, name, summary, cost, unlocked,
            affordable = session.Economy.CanAfford(cost), stats = Stats(session, tower, preview)
        });
    }

    private static IReadOnlyList<TowerStatDisplay> Stats(GameSession session, TowerInstance tower, TowerLevelDefinition? preview = null) =>
        TowerInfo.ComparisonStats(tower.Definition, tower.Level, preview, session.GetSupportBuff(tower),
            session.Map.GetPowerBuff(tower.Position), tower.IsOverdriven ? tower.Protocol : null,
            session.GetSignalDamageMultiplier(tower), session.GetSignalRateMultiplier(tower));

    private object? Generator(GameSession session) => session.Generator is not { } generator ? null : new
    {
        selected = session.SelectedGenerator is not null, level = generator.LevelIndex + 1,
        generator.ProductionRemaining, generator.Level.Capacity, generator.Level.ProductionSeconds,
        generator.CanUpgrade, generator.UpgradeCost, generator.SellValue
    };

    private bool CanMutate => Session is { IsVictory: false, IsDefeat: false } && !Paused && !Suspended && !InspectingHistory;

    private GameSession MutableSession()
    {
        if (!CanMutate) throw new InvalidOperationException("Resume the match before changing the battlefield.");
        return Session!;
    }

    private bool NewRun(JsonElement data)
    {
        var map = Text(data, "mapId");
        var difficulty = Text(data, "difficultyId");
        var challenge = Text(data, "challengeId");
        if (!_content.Maps.ContainsKey(map) || !_content.Difficulties.ContainsKey(difficulty) || !_content.Challenges.ContainsKey(challenge))
            throw new ArgumentException("Select an authored map, difficulty, and mode.");
        Assign(new GameSession(_content, map, difficulty, challenge));
        return true;
    }

    private bool Load(int slot)
    {
        var session = _saves.Load(_content, slot);
        session.ConfigureSolo();
        Assign(session);
        Paused = true;
        return true;
    }

    private void Assign(GameSession session)
    {
        Session = session;
        Paused = false;
        InspectingHistory = false;
        _accumulator = 0;
        _autosavedWave = -1;
        _recordedResult = null;
        _commands = new AuthoritativeCommandHost();
        Notice = null;
        SessionChanged?.Invoke(session);
    }

    private bool Inspect(string runId)
    {
        var entry = _history.GetEntries().FirstOrDefault(entry => entry.RunId == runId)
            ?? throw new ArgumentException("The history entry is unavailable.");
        Assign(entry.CreateInspectionSession(_content));
        InspectingHistory = true;
        Paused = true;
        return true;
    }

    private bool MainMenu()
    {
        PersistProgress(refreshCheckpoint: true);
        Session = null;
        InspectingHistory = false;
        Paused = false;
        _accumulator = 0;
        SessionChanged?.Invoke(null);
        return true;
    }

    private bool Pause(bool paused)
    {
        Paused = paused;
        _accumulator = 0;
        return true;
    }

    private bool Suspend(bool suspended) { SetSuspended(suspended); return true; }

    private bool BeginPlacement(JsonElement data)
    {
        var session = MutableSession();
        switch (Text(data, "kind"))
        {
            case "tower":
                if (!_content.Towers.ContainsKey(Text(data, "towerId"))) throw new ArgumentException("Unknown tower.");
                session.BeginPlacement(Text(data, "towerId"));
                break;
            case "plate": session.BeginEmergencyPlacement(); break;
            case "forge": session.BeginGeneratorPlacement(); break;
            default: throw new ArgumentException("Unknown placement type.");
        }
        return true;
    }

    private object Pointer(JsonElement data)
    {
        var session = Session ?? throw new InvalidOperationException("No active run.");
        var point = new Vector2(Number(data, "x"), Number(data, "y"));
        if (point.X < 0 || point.X > GameConstants.MapWidth || point.Y < 0 || point.Y > GameConstants.LogicalHeight)
            throw new ArgumentException("The pointer is outside the battlefield.");
        var commit = Boolean(data, "commit");
        var placing = session.PlacementTowerId is not null || session.TacticalPlacement != TacticalPlacementKind.None;
        if (placing && commit) MutableSession();
        if (!placing && commit)
        {
            var radius = Math.Clamp(Number(data, "radius", 24), 22, 100);
            var nearby = session.Towers.Where(tower => Vector2.DistanceSquared(tower.Position, point) <= radius * radius)
                .OrderBy(tower => Vector2.DistanceSquared(tower.Position, point)).ThenBy(tower => tower.Id).ToArray();
            var selectedId = Integer(data, "towerId");
            if (nearby.Length > 1 && selectedId == 0)
                return new { candidates = nearby.Select(tower => new { tower.Id, tower.Definition.DisplayName,
                    x = tower.Position.X, y = tower.Position.Y, progression = TowerInfo.ProgressionLabel(tower) }) };
            if (nearby.FirstOrDefault(tower => selectedId == 0 || tower.Id == selectedId) is { } selected)
                point = selected.Position;
            else if (session.Generator is { } generator && Vector2.DistanceSquared(generator.Position, point) <= radius * radius)
                point = generator.Position;
        }
        var input = default(InputSnapshot) with
        {
            MousePosition = point, LeftPressed = commit, IsMouseOverLogicalCanvas = true, TextEntered = ""
        };
        if (InspectingHistory || Paused || session.IsVictory || session.IsDefeat)
            session.HandleInspectionInput(input);
        else
            session.HandleWorldInput(input);
        if (placing && commit && session.PlacementFailure != PlacementFailure.None)
            throw new InvalidOperationException(session.PlacementFailure.ToString());
        return true;
    }

    private bool Cancel()
    {
        Session?.CancelPlacement();
        Session?.HandleWorldInput(default(InputSnapshot) with { EscapePressed = true, TextEntered = "" });
        return true;
    }

    private bool Command(JsonElement data, long requestId)
    {
        var session = Session ?? throw new InvalidOperationException("No active run.");
        var command = data.Deserialize<GameCommand>(JsonOptions) ?? throw new ArgumentException("Missing command.");
        if (command.Type == GameCommandType.ContinueEndless)
        {
            if (!session.IsVictory || InspectingHistory) throw new InvalidOperationException("Complete the campaign first.");
            Paused = false;
        }
        else MutableSession();
        command = command with { PlayerId = 1, ClientRequestId = requestId, Sequence = 0 };
        var result = _commands.Submit(session, command);
        if (!result.Accepted) throw new InvalidOperationException(result.Reason);
        return true;
    }

    private int Save(int slot)
    {
        if (Session is null || InspectingHistory) throw new InvalidOperationException("No active run to save.");
        if (slot < 0) slot = _saves.FindFirstEmptySlot() ?? throw new InvalidOperationException("No save slots available.");
        _saves.Save(Session, slot);
        Flush();
        return slot;
    }

    private int Duplicate(int slot) { var result = _saves.Duplicate(slot); Flush(); return result; }
    private bool DeleteSave(int slot) { var result = _saves.Delete(slot); Flush(); return result; }

    private UserSettings ConfigureSettings(JsonElement data)
    {
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return Settings;
        Settings = data.Deserialize<UserSettings>(JsonOptions) ?? throw new ArgumentException("Missing settings.");
        Settings.Normalize();
        _settingsStore.Save(Settings);
        Flush();
        return Settings;
    }

    private bool Sandbox(JsonElement data)
    {
        var session = MutableSession();
        if (!session.IsSandbox) throw new InvalidOperationException("Open Sandbox Lab to use these controls.");
        var accepted = Text(data, "operation") switch
        {
            "spawn" => session.SpawnSandboxTargets(Text(data, "enemyId"), Integer(data, "count", 1),
                Number(data, "health", 1), Text(data, "rank", "Standard"), Boolean(data, "immortal"),
                Enum.Parse<EnemySignalRole>(Text(data, "signalRole", "None"), true)),
            "wave" => session.StartSandboxWave(Integer(data, "wave", 1)),
            "signals" => session.ToggleSandboxWaveSignals(),
            "reset" => Reset(),
            "clear" => Clear(),
            "toggle" => session.ToggleSandboxTower(Integer(data, "towerId")),
            "protocol" => session.TestSandboxProtocol(Integer(data, "towerId")),
            _ => throw new ArgumentException("Unknown Sandbox operation.")
        };
        if (!accepted) throw new InvalidOperationException("The Sandbox action is unavailable for these values.");
        return true;
        bool Reset() { session.ResetSandboxExperiment(); return true; }
        bool Clear() { session.ClearSandboxTowers(); return true; }
    }

    private void PersistProgress(bool refreshCheckpoint = false)
    {
        if (Session is not { } session || InspectingHistory) return;
        try
        {
            if (session.CanSaveCheckpoint && (refreshCheckpoint || _autosavedWave != session.CurrentWave))
            {
                _saves.Save(session, SaveSlotRepository.AutosaveSlot);
                _autosavedWave = session.CurrentWave;
                Flush();
            }
            if (!session.IsSandbox && (session.IsVictory || session.IsDefeat))
            {
                var key = $"{session.RunId}:{session.CurrentWave}:{session.IsVictory}";
                if (_recordedResult == key) return;
                _history.Upsert(RunHistoryEntry.FromSession(session));
                _recordedResult = key;
                Flush();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notice = $"Progress could not be saved: {exception.Message}";
        }
    }

    private void Flush()
    {
        try { PlatformServices.FlushPersistentFiles(); }
        catch (Exception exception) { Notice = $"Device storage is unavailable: {exception.Message}"; }
    }

    private static string Text(JsonElement value, string name, string fallback = "") =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property)
            ? property.GetString() ?? fallback : fallback;
    private static int Integer(JsonElement value, string name, int fallback = 0) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) ? property.GetInt32() : fallback;
    private static bool Boolean(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.GetBoolean();
    private static float Number(JsonElement value, string name, float fallback = 0)
    {
        var number = value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property)
            ? property.GetSingle() : fallback;
        if (!float.IsFinite(number)) throw new ArgumentException("Coordinates and values must be finite.");
        return number;
    }
}
