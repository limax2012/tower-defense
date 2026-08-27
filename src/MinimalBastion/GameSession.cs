using MinimalBastion.Combat;
using MinimalBastion.Analytics;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Economy;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Maps;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;
using MinimalBastion.Rendering;
using MinimalBastion.Towers;
using MinimalBastion.Tactics;
using MinimalBastion.Waves;
using Microsoft.Xna.Framework;
using EconomyService = MinimalBastion.Economy.Economy;

namespace MinimalBastion;

public sealed class GameSession
{
    private const float BuildPlacementAssistRadius = 64f;
    private const float BuildPlacementAssistStep = 2f;
    private static readonly TargetMode[] StandardTargetModes = Enum.GetValues<TargetMode>()
        .Where(mode => mode != TargetMode.Support)
        .ToArray();
    private static readonly TargetMode[] SignalGauntletTargetModes = Enum.GetValues<TargetMode>();
    private readonly GameContent _content;
    private readonly EnemySystem _enemySystem = new();
    private readonly TowerSystem _towerSystem;
    private readonly BuffSystem _buffSystem = new();
    private readonly TacticalDefenseSystem _tacticalDefenseSystem = new();
    private readonly HashSet<int> _resolvedOutcomes = new();
    private int _nextEnemyId = 1;
    private int _nextTowerId = 1;
    private int _nextEmergencyDefenseId = 1;
    private WaveDefinition? _sandboxActiveWave;
    private int _sandboxGroupIndex;
    private int _sandboxSpawnedInGroup;
    private float _sandboxGroupTimer;
    private float _sandboxDelayRemaining;
    private int _sandboxQueuedEnemies;
    private bool _counterSupportSimulationEnabled = true;
    private bool _counterAttackersSimulationEnabled = true;

    public string RunId { get; private set; } = Guid.NewGuid().ToString("N");
    public GameContent Content => _content;
    public DifficultyDefinition Difficulty { get; }
    public string DifficultyId => Difficulty.Id;
    public ChallengeDefinition Challenge { get; }
    public string ChallengeId => Challenge.Id;
    public bool TacticalSystemsEnabled => Challenge.TacticalSystemsEnabled;
    public bool ProtocolsEnabled => Challenge.ProtocolsEnabled;
    public bool SellingEnabled => Challenge.SellingEnabled || IsSandbox;
    public bool CounterPressureEnabled => Challenge.CounterPressureEnabled;
    public bool SupportTargetingEnabled => ChallengeId.Equals(ChallengeCatalog.SignalGauntletId,
        StringComparison.OrdinalIgnoreCase);
    public IReadOnlyList<TargetMode> AvailableTargetModes => SupportTargetingEnabled
        ? SignalGauntletTargetModes
        : StandardTargetModes;
    internal bool CounterSupportEnabled => CounterPressureEnabled && _counterSupportSimulationEnabled;
    internal bool CounterAttackersEnabled => CounterPressureEnabled && _counterAttackersSimulationEnabled;
    public bool IsSandbox => Challenge.IsSandbox;
    public MapRuntime Map { get; }
    public EconomyService Economy { get; }
    public WaveManager Waves { get; }
    public TargetSelector TargetSelector { get; } = new();
    public DamageResolver DamageResolver { get; }
    public RunStatistics Statistics { get; }
    public ProjectileSystem Projectiles { get; } = new();
    public EffectSystem Effects { get; } = new();
    public List<EnemyInstance> Enemies { get; } = new();
    public List<TowerInstance> Towers { get; } = new();
    public List<PulsePlateInstance> EmergencyDefenses { get; } = new();
    public ChargeForgeInstance? Generator { get; private set; }
    public int EmergencyInventory { get; private set; }
    public int EmergencyDirectPurchasesThisWave { get; private set; }
    public int CurrentEmergencyDirectPurchaseCost
    {
        get
        {
            var definition = _content.Tactics.EmergencyDefense;
            var cost = (long)definition.PurchaseCost + (long)definition.DirectPurchaseCostIncrease * EmergencyDirectPurchasesThisWave;
            return (int)Math.Min(int.MaxValue, cost);
        }
    }
    public bool CanDirectPurchaseEmergencyDefense => TacticalSystemsEnabled && Waves.IsActive && Economy.CanAfford(CurrentEmergencyDirectPurchaseCost);
    public TowerInstance? SelectedTower { get; private set; }
    public TowerInstance? HoveredTower { get; private set; }
    public ChargeForgeInstance? SelectedGenerator { get; private set; }
    public ChargeForgeInstance? HoveredGenerator { get; private set; }
    public string? PlacementTowerId { get; private set; }
    public TacticalPlacementKind TacticalPlacement { get; private set; }
    public Vector2 PlacementPosition { get; private set; }
    public Vector2 PlacementPreviewPosition { get; private set; }
    public bool HasPlacementPreview { get; private set; }
    public PlacementFailure PlacementFailure { get; private set; }
    public float Speed { get; private set; } = 1f;
    public float OverdriveCooldownRemaining { get; private set; }
    public int AutoOverdriveTowerId { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsDefeat { get; private set; }
    public bool IsCoOp { get; private set; }
    public bool IsCoOpPaused { get; private set; }
    public int CoOpPausePlayerId { get; private set; }
    public int LocalPlayerId { get; private set; } = 1;
    public string? AnnouncementTitle { get; private set; }
    public string? AnnouncementSubtitle { get; private set; }
    public float AnnouncementRemaining { get; private set; }
    public bool AnnouncementPositive { get; private set; }
    public int CurrentWave => Waves.CurrentWaveNumber;
    public int TotalWaves => Waves.TotalWaves;
    public int AuthoredWaveCount => Waves.AuthoredWaveCount;
    public int NextEnemyId => _nextEnemyId;
    public int NextTowerId => _nextTowerId;
    public int NextEmergencyDefenseId => _nextEmergencyDefenseId;
    public bool IsEndlessMode => Waves.EndlessModeEnabled;
    public bool IsFinalCampaignAct => !IsEndlessMode &&
        CurrentWave + (Waves.IsActive ? 0 : 1) >= GameConstants.ApexUnlockWave &&
        CurrentWave <= TotalWaves;
    public bool CanStartWave => IsSandbox ? _sandboxActiveWave is null : Waves.CanStartNextWave;
    public float IntermissionRemaining => Waves.IntermissionRemaining;
    public int EnemiesRemaining => IsSandbox
        ? _sandboxQueuedEnemies + Enemies.Count(x => !x.IsDead && !x.HasEscaped)
        : Waves.EstimateRemainingIncludingLive(Enemies.Count(x => !x.IsDead && !x.HasEscaped));
    public bool CanSaveCheckpoint => !IsSandbox && !Waves.IsActive && Enemies.Count == 0 && !IsVictory && !IsDefeat;
    public bool SandboxWaveActive => _sandboxActiveWave is not null;
    public int SandboxQueuedEnemies => _sandboxQueuedEnemies;

    public event Action<TowerInstance>? TowerPlaced;
    public event Action<TowerInstance, int>? TowerUpgraded;
    public event Action<TowerInstance, int>? TowerSold;
    public event Action<TowerInstance>? TowerOverdriven;
    public event Action<EnemyInstance>? EnemyKilled;
    public event Action<EnemyInstance>? EnemyEscaped;
    public event Action<EnemyInstance>? EnemySpawned;
    public event Action<EnemyInstance>? BossPhaseChanged;
    public event Action<PulsePlateInstance, bool>? EmergencyDefenseDeployed;
    public event Action<PulsePlateInstance, int>? EmergencyDefenseTriggered;
    public event Action<PulsePlateInstance>? EmergencyDefenseExpired;
    public event Action<ChargeForgeInstance>? GeneratorPlaced;
    public event Action<ChargeForgeInstance, int>? GeneratorUpgraded;
    public event Action<ChargeForgeInstance, int>? GeneratorSold;
    public event Action? EmergencyChargeProduced;
    public event Action<WaveDefinition>? WaveStarted;
    public event Action<int>? WaveCompleted;

    public GameSession(GameContent content, string? mapId = null, string? difficultyId = null, string? challengeId = null)
    {
        _content = content;
        var mapDefinition = mapId is not null && content.Maps.TryGetValue(mapId, out var selectedMap) ? selectedMap : content.Map;
        var waveSet = content.WaveSets.TryGetValue(mapDefinition.WaveSet, out var selectedWaves) ? selectedWaves : content.Waves;
        Difficulty = DifficultyCatalog.Resolve(content, difficultyId);
        Challenge = ChallengeCatalog.Resolve(content, challengeId);
        Map = new MapRuntime(mapDefinition);
        Economy = new EconomyService(
            ChallengeCatalog.StartingCredits(mapDefinition, Difficulty, Challenge),
            Difficulty.StartingLives,
            unlimitedCredits: IsSandbox,
            unlimitedLives: IsSandbox);
        Waves = new WaveManager(waveSet.Waves);
        DamageResolver = new DamageResolver(this);
        Statistics = new RunStatistics(this);
        _towerSystem = new TowerSystem(TargetSelector);
        EmergencyInventory = TacticalSystemsEnabled ? Math.Max(0, content.Tactics.EmergencyDefense.StartingInventory) : 0;
        if (IsSandbox)
        {
            AnnouncementTitle = "SANDBOX LAB ONLINE";
            AnnouncementSubtitle = "Build freely, spawn fixed targets, or replay an authored wave.";
            AnnouncementRemaining = 4.2f;
            AnnouncementPositive = true;
        }
        else if (!Challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase))
        {
            AnnouncementTitle = Challenge.DisplayName.ToUpperInvariant();
            AnnouncementSubtitle = Challenge.Description;
            AnnouncementRemaining = 3.6f;
            AnnouncementPositive = true;
        }
        else if (mapDefinition.PowerNodes.Count > 0)
        {
            AnnouncementTitle = mapDefinition.DisplayName.ToUpperInvariant();
            AnnouncementSubtitle = "Hover a Surge Node; compact fields grant one focused tower bonus.";
            AnnouncementRemaining = 3.2f;
            AnnouncementPositive = true;
        }
    }

    public void Update(float unscaledDeltaSeconds)
    {
        if (IsVictory || IsDefeat) return;
        if (IsCoOpPaused)
        {
            // A shared pause freezes the defense, but the early-call window is
            // a real planning deadline rather than bankable paused time.
            UpdatePausedIntermission(unscaledDeltaSeconds);
            _buffSystem.Update(Towers);
            return;
        }
        var deltaSeconds = MathF.Min(0.1f, MathF.Max(0, unscaledDeltaSeconds)) * Speed;
        Statistics.Advance(deltaSeconds);
        AnnouncementRemaining = MathF.Max(0, AnnouncementRemaining - deltaSeconds);
        OverdriveCooldownRemaining = MathF.Max(0, OverdriveCooldownRemaining - deltaSeconds);
        if (IsSandbox) UpdateSandboxWave(deltaSeconds);
        else
        {
            Waves.UpdateIntermission(deltaSeconds);
            Waves.Update(deltaSeconds, this);
        }
        _enemySystem.Update(deltaSeconds, this);
        _tacticalDefenseSystem.Update(deltaSeconds, this);
        TryActivateAutomaticProtocol();
        _buffSystem.Update(Towers);
        _towerSystem.Update(deltaSeconds, this);
        Projectiles.Update(deltaSeconds, this);
        Effects.Update(deltaSeconds);
        Enemies.RemoveAll(x => x.IsDead || x.HasEscaped);
        if (!IsSandbox) Waves.TryComplete(Enemies.Count == 0, this);

        if (!IsSandbox && Economy.Lives <= 0) IsDefeat = true;
        else if (Waves.IsFinalWaveCleared && !Waves.EndlessModeEnabled && Enemies.Count == 0) IsVictory = true;
    }

    public void UpdatePausedIntermission(float unscaledDeltaSeconds)
    {
        if (IsVictory || IsDefeat || IsSandbox || Waves.IsActive) return;
        var deltaSeconds = MathF.Min(0.1f, MathF.Max(0, unscaledDeltaSeconds)) * Speed;
        Waves.UpdateIntermission(deltaSeconds);
    }

    public void HandleWorldInput(InputSnapshot input, Action<GameCommand>? commandSink = null, int playerId = 1)
    {
        PlacementPosition = input.MousePosition;
        PlacementPreviewPosition = PlacementPosition;
        HasPlacementPreview = false;
        HoveredTower = null;
        HoveredGenerator = null;
        if (TacticalPlacement != TacticalPlacementKind.None)
        {
            var tacticalPosition = PlacementPosition;
            if (TacticalPlacement == TacticalPlacementKind.PulsePlate)
            {
                HasPlacementPreview = TryResolvePulsePlatePlacement(PlacementPosition, out tacticalPosition);
                if (HasPlacementPreview) PlacementPreviewPosition = tacticalPosition;
                PlacementFailure = ResolvePulsePlatePlacementFailure(PlacementPosition, tacticalPosition, HasPlacementPreview);
            }
            else
            {
                HasPlacementPreview = TryResolveChargeForgePlacement(PlacementPosition, out tacticalPosition);
                if (HasPlacementPreview) PlacementPreviewPosition = tacticalPosition;
                PlacementFailure = HasPlacementPreview
                    ? PlacementFailure.None
                    : ValidateTacticalPlacement(TacticalPlacement, PlacementPosition);
            }
            if (input.RightPressed || input.EscapePressed)
            {
                CancelPlacement();
                return;
            }
            if (input.LeftPressed && PlacementFailure == PlacementFailure.None && HasPlacementPreview)
            {
                if (commandSink is null)
                {
                    if (TacticalPlacement == TacticalPlacementKind.PulsePlate) TryDeployEmergencyDefense(tacticalPosition);
                    else if (TacticalPlacement == TacticalPlacementKind.ChargeForge) TryPlaceGenerator(tacticalPosition);
                }
                else
                {
                    commandSink(new GameCommand
                    {
                        PlayerId = playerId,
                        Type = TacticalPlacement == TacticalPlacementKind.PulsePlate ? GameCommandType.DeployEmergencyDefense : GameCommandType.PlaceGenerator,
                        X = tacticalPosition.X,
                        Y = tacticalPosition.Y
                    });
                }
                CancelPlacement();
            }
            return;
        }
        if (PlacementTowerId is not null)
        {
            HasPlacementPreview = TryResolveTowerPlacement(PlacementTowerId, PlacementPosition, out var towerPosition);
            if (HasPlacementPreview) PlacementPreviewPosition = towerPosition;
            PlacementFailure = HasPlacementPreview
                ? PlacementFailure.None
                : ValidatePlacement(PlacementTowerId, PlacementPosition);
            if (input.RightPressed || input.EscapePressed)
            {
                CancelPlacement();
                return;
            }
            if (input.LeftPressed && PlacementFailure == PlacementFailure.None && HasPlacementPreview)
            {
                if (commandSink is null) TryPlaceTower(PlacementTowerId, towerPosition);
                else commandSink(new GameCommand
                {
                    PlayerId = playerId,
                    Type = GameCommandType.PlaceTower,
                    TowerDefinitionId = PlacementTowerId,
                    X = towerPosition.X,
                    Y = towerPosition.Y
                });
                CancelPlacement();
            }
            return;
        }

        if (IsInteractiveBattlefieldPosition(input.MousePosition))
        {
            if (Generator is { } generator)
            {
                var hitRadius = generator.Definition.Visual.Radius + 5f;
                if (Vector2.DistanceSquared(generator.Position, input.MousePosition) <= hitRadius * hitRadius)
                    HoveredGenerator = generator;
            }
            HoveredTower = HoveredGenerator is null ? Towers.OrderByDescending(x => x.Id).FirstOrDefault(x =>
            {
                var hitRadius = x.Definition.Visual.Radius + 4f;
                return Vector2.DistanceSquared(x.Position, input.MousePosition) <= hitRadius * hitRadius;
            }) : null;
            if (input.LeftPressed)
            {
                SelectedGenerator = HoveredGenerator;
                SelectedTower = HoveredGenerator is null ? HoveredTower : null;
            }
        }
        if (input.EscapePressed)
        {
            SelectedTower = null;
            SelectedGenerator = null;
        }
    }

    public void HandleInspectionInput(InputSnapshot input)
    {
        CancelPlacement();
        PlacementPosition = input.MousePosition;
        HoveredTower = null;
        HoveredGenerator = null;
        if (IsInteractiveBattlefieldPosition(input.MousePosition))
        {
            if (Generator is { } generator)
            {
                var hitRadius = generator.Definition.Visual.Radius + 5f;
                if (Vector2.DistanceSquared(generator.Position, input.MousePosition) <= hitRadius * hitRadius)
                    HoveredGenerator = generator;
            }
            HoveredTower = HoveredGenerator is null ? Towers.OrderByDescending(tower => tower.Id).FirstOrDefault(tower =>
            {
                var hitRadius = tower.Definition.Visual.Radius + 4f;
                return Vector2.DistanceSquared(tower.Position, input.MousePosition) <= hitRadius * hitRadius;
            }) : null;
            if (input.LeftPressed)
            {
                SelectedGenerator = HoveredGenerator;
                SelectedTower = HoveredGenerator is null ? HoveredTower : null;
            }
        }
        if (input.RightPressed)
        {
            SelectedTower = null;
            SelectedGenerator = null;
        }
    }

    public void BeginPlacement(string towerId)
    {
        if (!_content.Towers.ContainsKey(towerId) || !IsTowerAvailable(towerId)) return;
        PlacementTowerId = towerId;
        TacticalPlacement = TacticalPlacementKind.None;
        PlacementFailure = PlacementFailure.None;
        HasPlacementPreview = false;
        SelectedTower = null;
        SelectedGenerator = null;
    }

    public void BeginEmergencyPlacement()
    {
        if (!TacticalSystemsEnabled) return;
        PlacementTowerId = null;
        TacticalPlacement = TacticalPlacementKind.PulsePlate;
        PlacementFailure = PlacementFailure.None;
        HasPlacementPreview = false;
        SelectedTower = null;
        SelectedGenerator = null;
    }

    public void BeginGeneratorPlacement()
    {
        if (!TacticalSystemsEnabled) return;
        if (Generator is not null)
        {
            SelectedGenerator = Generator;
            SelectedTower = null;
            return;
        }
        PlacementTowerId = null;
        TacticalPlacement = TacticalPlacementKind.ChargeForge;
        PlacementFailure = PlacementFailure.None;
        HasPlacementPreview = false;
        SelectedTower = null;
        SelectedGenerator = null;
    }

    public void CancelPlacement()
    {
        PlacementTowerId = null;
        TacticalPlacement = TacticalPlacementKind.None;
        PlacementFailure = PlacementFailure.None;
        HasPlacementPreview = false;
    }

    public void ConfigureCoOp(int localPlayerId)
    {
        if (localPlayerId is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
        IsCoOp = true;
        LocalPlayerId = localPlayerId;
    }

    public void ConfigureSolo()
    {
        IsCoOp = false;
        IsCoOpPaused = false;
        CoOpPausePlayerId = 0;
        LocalPlayerId = 1;
    }

    public bool TryPlaceTower(string towerId, Vector2 position, int ownerPlayerId = 1, bool selectPlaced = true)
    {
        if (ownerPlayerId is < 1 or > 2) return false;
        var failure = ValidatePlacement(towerId, position);
        if (failure != PlacementFailure.None) return false;
        var definition = _content.Towers[towerId];
        if (!Economy.TrySpend(definition.PurchaseCost)) return false;
        var tower = new TowerInstance(_nextTowerId++, definition, position, ownerPlayerId);
        Towers.Add(tower);
        if (selectPlaced) SelectedTower = tower;
        TowerPlaced?.Invoke(tower);
        return true;
    }

    public PlacementFailure ValidatePlacement(string towerId, Vector2 position)
    {
        if (!_content.Towers.TryGetValue(towerId, out var definition)) return PlacementFailure.UnknownTower;
        if (_nextTowerId >= int.MaxValue) return PlacementFailure.IdentityCapacityReached;
        if (!IsTowerAvailable(towerId)) return PlacementFailure.TowerUnavailable;
        if (!Economy.CanAfford(definition.PurchaseCost)) return PlacementFailure.InsufficientCredits;
        return ValidateTowerPlacementGeometry(position);
    }

    private PlacementFailure ValidateTowerPlacementGeometry(Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return PlacementFailure.TooCloseToEdge;
        if (position.X < GameConstants.TowerRadius || position.X > GameConstants.MapWidth - GameConstants.TowerRadius ||
            position.Y < GameConstants.TopBarHeight + GameConstants.TowerRadius || position.Y > GameConstants.LogicalHeight - GameConstants.TowerRadius)
            return PlacementFailure.TooCloseToEdge;
        if (!Map.IsBuildable(position)) return PlacementFailure.OutsideBuildableRegion;
        if (Map.Path.DistanceToPath(position) < GameConstants.PlacementPathClearance) return PlacementFailure.BlocksPath;
        if (Towers.Any(x => Vector2.DistanceSquared(x.Position, position) < GameConstants.TowerMinimumGap * GameConstants.TowerMinimumGap))
            return PlacementFailure.OverlapsTower;
        if (Generator is not null && Vector2.DistanceSquared(Generator.Position, position) < 48f * 48f)
            return PlacementFailure.OverlapsTower;
        return PlacementFailure.None;
    }

    public PlacementFailure ValidateTacticalPlacement(TacticalPlacementKind kind, Vector2 position)
    {
        if (!TacticalSystemsEnabled) return PlacementFailure.TacticalSystemsDisabled;
        if (kind == TacticalPlacementKind.PulsePlate)
        {
            var definition = _content.Tactics.EmergencyDefense;
            if (_nextEmergencyDefenseId > GameConstants.MaximumPulsePlateId) return PlacementFailure.IdentityCapacityReached;
            if (EmergencyDefenses.Count >= definition.MaximumActive) return PlacementFailure.DefenseCapacityReached;
            if (EmergencyInventory <= 0 && !CanDirectPurchaseEmergencyDefense) return PlacementFailure.NoDefenseAvailable;
            if (!IsBattlefieldPosition(position)) return PlacementFailure.MustBeOnPath;
            var projection = Map.Path.Project(position);
            if (projection.DistanceToPath > Map.Definition.PathWidth * 0.5f + definition.PlacementRoadTolerance)
                return PlacementFailure.MustBeOnPath;
            if (projection.DistanceAlongPath < definition.EndpointClearance ||
                projection.DistanceAlongPath > Map.Path.TotalLength - definition.EndpointClearance)
                return PlacementFailure.TooCloseToPathEndpoint;
            if (EmergencyDefenses.Any(x => Vector2.DistanceSquared(x.Position, projection.Position) <
                definition.MinimumSpacing * definition.MinimumSpacing))
                return PlacementFailure.OverlapsDefense;
            return PlacementFailure.None;
        }

        if (kind != TacticalPlacementKind.ChargeForge) return PlacementFailure.UnknownTower;
        if (Generator is not null) return PlacementFailure.GeneratorAlreadyBuilt;
        var generator = _content.Tactics.Generator;
        if (!Economy.CanAfford(generator.PurchaseCost)) return PlacementFailure.InsufficientCredits;
        return ValidateChargeForgePlacementGeometry(position);
    }

    private PlacementFailure ValidateChargeForgePlacementGeometry(Vector2 position)
    {
        var generator = _content.Tactics.Generator;
        var radius = generator.Visual.Radius;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return PlacementFailure.TooCloseToEdge;
        if (position.X < radius || position.X > GameConstants.MapWidth - radius ||
            position.Y < GameConstants.TopBarHeight + radius || position.Y > GameConstants.LogicalHeight - radius)
            return PlacementFailure.TooCloseToEdge;
        if (!Map.IsBuildable(position)) return PlacementFailure.OutsideBuildableRegion;
        if (Map.Path.DistanceToPath(position) < GameConstants.PlacementPathClearance) return PlacementFailure.BlocksPath;
        if (Towers.Any(x => Vector2.DistanceSquared(x.Position, position) < 48f * 48f)) return PlacementFailure.OverlapsTower;
        return PlacementFailure.None;
    }

    /// <summary>
    /// Resolves an imprecise tower cursor to the closest legal build point within a
    /// small local assist radius. Exact legal positions remain continuous and unchanged;
    /// snapping only engages around build-zone edges, roads, and occupied tower gaps.
    /// </summary>
    public bool TryResolveTowerPlacement(string towerId, Vector2 cursorPosition, out Vector2 placementPosition)
    {
        placementPosition = cursorPosition;
        var failure = ValidatePlacement(towerId, cursorPosition);
        if (failure == PlacementFailure.None) return true;
        if (!IsSpatialPlacementFailure(failure) || !IsInteractiveBattlefieldPosition(cursorPosition)) return false;
        return TryResolveNearbyBuildPosition(cursorPosition,
            candidate => ValidateTowerPlacementGeometry(candidate) == PlacementFailure.None,
            out placementPosition);
    }

    public bool TryResolveChargeForgePlacement(Vector2 cursorPosition, out Vector2 placementPosition)
    {
        placementPosition = cursorPosition;
        var failure = ValidateTacticalPlacement(TacticalPlacementKind.ChargeForge, cursorPosition);
        if (failure == PlacementFailure.None) return true;
        if (!IsSpatialPlacementFailure(failure) || !IsInteractiveBattlefieldPosition(cursorPosition)) return false;
        return TryResolveNearbyBuildPosition(cursorPosition,
            candidate => ValidateChargeForgePlacementGeometry(candidate) == PlacementFailure.None,
            out placementPosition);
    }

    private bool TryResolveNearbyBuildPosition(Vector2 cursorPosition, Func<Vector2, bool> isLegal,
        out Vector2 placementPosition)
    {
        const float distanceTieEpsilon = 0.001f;
        var assistRadiusSquared = BuildPlacementAssistRadius * BuildPlacementAssistRadius;
        var bestDistanceSquared = assistRadiusSquared + distanceTieEpsilon;
        var nearestZoneDistanceSquared = float.MaxValue;
        var bestPosition = cursorPosition;
        var found = false;

        // Seed the search with the mathematically nearest point in each authored
        // build zone. The fine local search below then handles roads and occupied gaps.
        foreach (var region in Map.BuildableRegions)
        {
            var candidate = new Vector2(
                MathHelper.Clamp(cursorPosition.X, region.Left, region.Right - 0.01f),
                MathHelper.Clamp(cursorPosition.Y, region.Top, region.Bottom - 0.01f));
            var distanceSquared = Vector2.DistanceSquared(cursorPosition, candidate);
            if (distanceSquared > assistRadiusSquared) continue;
            nearestZoneDistanceSquared = MathF.Min(nearestZoneDistanceSquared, distanceSquared);
            ConsiderAtDistance(candidate, distanceSquared);
        }
        if (found && bestDistanceSquared <= nearestZoneDistanceSquared + distanceTieEpsilon)
        {
            placementPosition = bestPosition;
            return true;
        }

        var steps = (int)MathF.Ceiling(BuildPlacementAssistRadius / BuildPlacementAssistStep);
        for (var y = -steps; y <= steps; y++)
        {
            for (var x = -steps; x <= steps; x++)
            {
                if (x == 0 && y == 0) continue;
                var offset = new Vector2(x * BuildPlacementAssistStep, y * BuildPlacementAssistStep);
                if (offset.LengthSquared() > assistRadiusSquared) continue;
                Consider(cursorPosition + offset);
            }
        }

        placementPosition = bestPosition;
        return found;

        void Consider(Vector2 candidate)
        {
            var distanceSquared = Vector2.DistanceSquared(cursorPosition, candidate);
            ConsiderAtDistance(candidate, distanceSquared);
        }

        void ConsiderAtDistance(Vector2 candidate, float distanceSquared)
        {
            if (distanceSquared > assistRadiusSquared || !IsBetterCandidate(candidate, distanceSquared) ||
                !isLegal(candidate)) return;
            bestDistanceSquared = distanceSquared;
            bestPosition = candidate;
            found = true;
        }

        bool IsBetterCandidate(Vector2 candidate, float distanceSquared)
        {
            if (!found || distanceSquared < bestDistanceSquared - distanceTieEpsilon) return true;
            if (MathF.Abs(distanceSquared - bestDistanceSquared) > distanceTieEpsilon) return false;

            // Exact distance ties should remain stable regardless of build-zone or
            // lattice iteration order. Prefer the candidate requiring less vertical
            // correction, then less horizontal correction, then screen order.
            var candidateVerticalOffset = MathF.Abs(candidate.Y - cursorPosition.Y);
            var bestVerticalOffset = MathF.Abs(bestPosition.Y - cursorPosition.Y);
            if (candidateVerticalOffset < bestVerticalOffset - distanceTieEpsilon) return true;
            if (candidateVerticalOffset > bestVerticalOffset + distanceTieEpsilon) return false;

            var candidateHorizontalOffset = MathF.Abs(candidate.X - cursorPosition.X);
            var bestHorizontalOffset = MathF.Abs(bestPosition.X - cursorPosition.X);
            if (candidateHorizontalOffset < bestHorizontalOffset - distanceTieEpsilon) return true;
            if (candidateHorizontalOffset > bestHorizontalOffset + distanceTieEpsilon) return false;
            if (candidate.Y < bestPosition.Y - distanceTieEpsilon) return true;
            return MathF.Abs(candidate.Y - bestPosition.Y) <= distanceTieEpsilon &&
                   candidate.X < bestPosition.X - distanceTieEpsilon;
        }
    }

    private static bool IsSpatialPlacementFailure(PlacementFailure failure) => failure is
        PlacementFailure.TooCloseToEdge or
        PlacementFailure.OutsideBuildableRegion or
        PlacementFailure.BlocksPath or
        PlacementFailure.OverlapsTower;

    /// <summary>
    /// Resolves a cursor near the road to the closest legal pulse-plate slot. This keeps
    /// the visible preview, local deployment, and network command on the same position.
    /// Endpoint clearance and nearby plates are handled as snap constraints rather than
    /// asking the player to estimate an invisible boundary.
    /// </summary>
    public bool TryResolvePulsePlatePlacement(Vector2 cursorPosition, out Vector2 placementPosition)
    {
        var definition = _content.Tactics.EmergencyDefense;
        placementPosition = cursorPosition;
        if (!IsBattlefieldPosition(cursorPosition)) return false;
        var projection = Map.Path.Project(cursorPosition);
        placementPosition = projection.Position;
        if (!TacticalSystemsEnabled) return false;
        var roadSnapDistance = Map.Definition.PathWidth * 0.5f + definition.PlacementRoadTolerance;
        if (projection.DistanceToPath > roadSnapDistance) return false;

        var minimumDistance = definition.EndpointClearance;
        var maximumDistance = Map.Path.TotalLength - definition.EndpointClearance;
        if (maximumDistance < minimumDistance) return false;

        var preferredDistance = MathHelper.Clamp(projection.DistanceAlongPath, minimumDistance, maximumDistance);
        var searchDistance = MathF.Max(definition.EndpointClearance, definition.MinimumSpacing * 2f);
        var bestDistanceSquared = float.MaxValue;
        var bestPosition = placementPosition;
        var found = false;

        Consider(preferredDistance);
        for (var offset = 1f; offset <= searchDistance; offset += 1f)
        {
            Consider(preferredDistance - offset);
            Consider(preferredDistance + offset);
        }

        placementPosition = bestPosition;
        return found;

        void Consider(float distanceAlongPath)
        {
            if (distanceAlongPath < minimumDistance || distanceAlongPath > maximumDistance) return;
            var candidate = Map.Path.GetPosition(distanceAlongPath);
            var minimumSpacingSquared = definition.MinimumSpacing * definition.MinimumSpacing;
            if (EmergencyDefenses.Any(defense => Vector2.DistanceSquared(defense.Position, candidate) < minimumSpacingSquared)) return;

            var distanceSquared = Vector2.DistanceSquared(cursorPosition, candidate);
            if (distanceSquared >= bestDistanceSquared) return;
            bestDistanceSquared = distanceSquared;
            bestPosition = candidate;
            found = true;
        }
    }

    private PlacementFailure ResolvePulsePlatePlacementFailure(Vector2 cursorPosition, Vector2 placementPosition, bool hasPlacement)
    {
        var definition = _content.Tactics.EmergencyDefense;
        if (EmergencyDefenses.Count >= definition.MaximumActive) return PlacementFailure.DefenseCapacityReached;
        if (EmergencyInventory <= 0 && !CanDirectPurchaseEmergencyDefense) return PlacementFailure.NoDefenseAvailable;
        if (!IsBattlefieldPosition(cursorPosition)) return PlacementFailure.MustBeOnPath;
        var projection = Map.Path.Project(cursorPosition);
        if (projection.DistanceToPath > Map.Definition.PathWidth * 0.5f + definition.PlacementRoadTolerance)
            return PlacementFailure.MustBeOnPath;
        if (!hasPlacement) return PlacementFailure.OverlapsDefense;
        return ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, placementPosition);
    }

    private static bool IsBattlefieldPosition(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        position.X >= 0 && position.X < GameConstants.MapWidth &&
        position.Y >= 0 && position.Y <= GameConstants.LogicalHeight;

    private static bool IsInteractiveBattlefieldPosition(Vector2 position) =>
        IsBattlefieldPosition(position) && position.Y >= GameConstants.TopBarHeight;

    public bool TryDeployEmergencyDefense(Vector2 position, int ownerPlayerId = 1)
    {
        if (ownerPlayerId is < 1 or > 2) return false;
        if (ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position) != PlacementFailure.None) return false;
        var definition = _content.Tactics.EmergencyDefense;
        var purchased = EmergencyInventory <= 0;
        if (purchased)
        {
            if (!Waves.IsActive || !Economy.TrySpend(CurrentEmergencyDirectPurchaseCost)) return false;
            EmergencyDirectPurchasesThisWave = MetricMath.Add(EmergencyDirectPurchasesThisWave);
        }
        else
        {
            EmergencyInventory--;
        }
        var plate = new PulsePlateInstance(_nextEmergencyDefenseId++, Map.Path.Project(position).Position, definition, ownerPlayerId);
        EmergencyDefenses.Add(plate);
        EmergencyDefenseDeployed?.Invoke(plate, purchased);
        return true;
    }

    public bool TryPlaceGenerator(Vector2 position, int ownerPlayerId = 1, bool selectPlaced = true)
    {
        if (ownerPlayerId is < 1 or > 2) return false;
        if (ValidateTacticalPlacement(TacticalPlacementKind.ChargeForge, position) != PlacementFailure.None) return false;
        var definition = _content.Tactics.Generator;
        if (!Economy.TrySpend(definition.PurchaseCost)) return false;
        Generator = new ChargeForgeInstance(position, definition, ownerPlayerId);
        if (selectPlaced) SelectedGenerator = Generator;
        GeneratorPlaced?.Invoke(Generator);
        return true;
    }

    public bool IsTowerAvailable(string towerId) =>
        !Challenge.ExcludedTowerIds.Contains(towerId, StringComparer.OrdinalIgnoreCase);

    public bool ApexUpgradesUnlocked => IsSandbox ||
        CurrentWave + (Waves.IsActive ? 0 : 1) >= GameConstants.ApexUnlockWave;

    public bool CanApexUpgrade(TowerInstance tower) => ApexUpgradesUnlocked && !tower.IsApex &&
        tower.Definition.Apex is not null && tower.LevelIndex == tower.Definition.Levels.Count - 1 &&
        !tower.RequiresSpecialization;

    public bool TryUpgradeSelectedTower()
    {
        return SelectedTower is not null ? TryUpgradeTower(SelectedTower.Id) : TryUpgradeGenerator();
    }

    public bool TrySellSelectedTower()
    {
        return SelectedTower is not null ? TrySellTower(SelectedTower.Id) : TrySellGenerator();
    }

    public bool TryUpgradeTower(int towerId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (requestingPlayerId is < 1 or > 2 || tower is null) return false;
        var apex = CanApexUpgrade(tower);
        if (!tower.CanUpgrade && !apex) return false;
        var cost = apex ? tower.ApexUpgradeCost : tower.UpgradeCost;
        if (!Economy.TrySpend(cost) || !(apex ? tower.TryApexUpgrade() : tower.TryUpgrade())) return false;
        TowerUpgraded?.Invoke(tower, cost);
        return true;
    }

    public bool TryChooseTowerDoctrine(int towerId, string doctrineId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (requestingPlayerId is < 1 or > 2 || tower is null || !tower.RequiresDoctrine) return false;
        var doctrine = tower.Definition.Tier2Doctrines.FirstOrDefault(x => x.Id.Equals(doctrineId, StringComparison.OrdinalIgnoreCase));
        if (doctrine is null || !Economy.TrySpend(doctrine.UpgradeCost) || !tower.TryChooseDoctrine(doctrine.Id)) return false;
        TowerUpgraded?.Invoke(tower, doctrine.UpgradeCost);
        return true;
    }

    public bool TrySpecializeTower(int towerId, string specializationId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (requestingPlayerId is < 1 or > 2 || tower is null || !tower.RequiresSpecialization) return false;
        var specialization = tower.Definition.Specializations.FirstOrDefault(x => x.Id.Equals(specializationId, StringComparison.OrdinalIgnoreCase));
        if (specialization is null || !Economy.TrySpend(specialization.UpgradeCost) || !tower.TrySpecialize(specialization.Id)) return false;
        TowerUpgraded?.Invoke(tower, specialization.UpgradeCost);
        return true;
    }

    public bool TrySellTower(int towerId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (!SellingEnabled || requestingPlayerId is < 1 or > 2 || tower is null) return false;
        var value = tower.SellValue;
        Economy.RecoverSale(value);
        Towers.Remove(tower);
        if (AutoOverdriveTowerId == tower.Id) AutoOverdriveTowerId = 0;
        if (SelectedTower == tower) SelectedTower = null;
        TowerSold?.Invoke(tower, value);
        return true;
    }

    public bool TrySetTargetMode(int towerId, TargetMode mode, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (requestingPlayerId is < 1 or > 2 || tower is null || tower.IsSupport || !IsTargetModeAvailable(mode))
            return false;
        tower.TargetMode = mode;
        return true;
    }

    public bool IsTargetModeAvailable(TargetMode mode) =>
        Enum.IsDefined(mode) && (mode != TargetMode.Support || SupportTargetingEnabled);

    public bool TryOverdriveTower(int towerId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (!ProtocolsEnabled || requestingPlayerId is < 1 or > 2 || tower is null || tower.IsSandboxDisabled || tower.IsOverdriven || OverdriveCooldownRemaining > 0)
            return false;
        tower.ActivateOverdrive();
        OverdriveCooldownRemaining = tower.Protocol.CooldownSeconds;
        ApplyProtocolBurst(tower);
        if (tower.Protocol.BurstRadius > 0 &&
            (tower.Protocol.BurstDamage > 0 || !string.IsNullOrWhiteSpace(tower.Protocol.BurstStatus)))
        {
            Effects.AddSplash(tower.Position, tower.Definition.Visual.PrimaryColor, tower.Protocol.BurstRadius);
        }
        else
        {
            Effects.AddFlash(tower.Position, tower.Definition.Visual.PrimaryColor, 0.42f, 44);
        }
        TowerOverdriven?.Invoke(tower);
        return true;
    }

    public bool TryToggleAutoProtocol(int towerId, int requestingPlayerId = 1)
    {
        if (!ProtocolsEnabled || requestingPlayerId is < 1 or > 2) return false;
        if (AutoOverdriveTowerId == towerId)
        {
            AutoOverdriveTowerId = 0;
            return true;
        }
        if (Towers.All(x => x.Id != towerId || x.IsSandboxDisabled)) return false;
        AutoOverdriveTowerId = towerId;
        return true;
    }

    private void TryActivateAutomaticProtocol()
    {
        if (!ProtocolsEnabled)
        {
            AutoOverdriveTowerId = 0;
            return;
        }
        if (AutoOverdriveTowerId <= 0 || OverdriveCooldownRemaining > 0 || Enemies.Count == 0) return;
        var tower = Towers.FirstOrDefault(x => x.Id == AutoOverdriveTowerId);
        if (tower is null || tower.IsSandboxDisabled)
        {
            AutoOverdriveTowerId = 0;
            return;
        }

        if (!ShouldActivateAutomaticProtocol(tower)) return;
        TryOverdriveTower(tower.Id);
    }

    private bool ShouldActivateAutomaticProtocol(TowerInstance tower)
    {
        var coverageTargets = GetProtocolTargets(tower).ToArray();
        // Priority ranks are always worth an automatic Protocol, but only when
        // this tower (or a Beacon recipient) can actually engage them.
        if (coverageTargets.Any(enemy => enemy.IsElite || enemy.IsBoss)) return true;

        var required = tower.Protocol.AutoTriggerCount;
        return ProtocolAutoTriggerModes.Normalize(tower.Protocol.AutoTriggerMode) switch
        {
            ProtocolAutoTriggerModes.ProtocolArea => CountLiveEnemiesInRadius(tower.Position, tower.Protocol.BurstRadius) >= required,
            ProtocolAutoTriggerModes.PriorityTargets => coverageTargets.Count(IsProtocolPriorityTarget) >= required,
            ProtocolAutoTriggerModes.DenseCluster => LargestProtocolTargetCluster(tower, coverageTargets) >= required,
            ProtocolAutoTriggerModes.EngagedRecipients => HasEngagedProtocolRecipients(tower),
            _ => coverageTargets.Length >= required
        };
    }

    private int CountLiveEnemiesInRadius(Vector2 center, float radius)
    {
        if (radius <= 0) return 0;
        var radiusSquared = radius * radius;
        return Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped &&
            Vector2.DistanceSquared(center, enemy.Position) <= radiusSquared);
    }

    private static bool IsProtocolPriorityTarget(EnemyInstance enemy) =>
        enemy.BaseArmor > 0 || enemy.Shield > 0 || enemy.IsElite || enemy.IsBoss;

    private int LargestProtocolTargetCluster(TowerInstance tower, IReadOnlyList<EnemyInstance> coverageTargets)
    {
        var clusterRadius = tower.Level.SplashRadius > 0
            ? tower.Level.SplashRadius
            : tower.Level.ChainRange;
        if (clusterRadius <= 0 || coverageTargets.Count == 0) return 0;

        var radiusSquared = clusterRadius * clusterRadius;
        var liveEnemies = Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
        return coverageTargets.Max(center => liveEnemies.Count(enemy =>
            Vector2.DistanceSquared(center.Position, enemy.Position) <= radiusSquared));
    }

    private bool HasEngagedProtocolRecipients(TowerInstance supportTower)
    {
        var engagedRecipients = 0;
        foreach (var recipient in GetProtocolRecipients(supportTower))
        {
            var range = GetEffectiveRange(recipient);
            var rangeSquared = range * range;
            var targetCount = Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                Vector2.DistanceSquared(recipient.Position, enemy.Position) <= rangeSquared);
            if (targetCount > 0) engagedRecipients++;
            if (supportTower.Protocol.AutoTriggerTargetCount > 0 &&
                targetCount >= supportTower.Protocol.AutoTriggerTargetCount)
                return true;
        }

        return engagedRecipients >= supportTower.Protocol.AutoTriggerCount;
    }

    public IReadOnlyList<EnemyInstance> GetProtocolTargets(TowerInstance tower)
    {
        if (tower.IsSandboxDisabled) return Array.Empty<EnemyInstance>();
        if (!tower.IsSupport)
        {
            var range = GetEffectiveRange(tower);
            return Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                Vector2.DistanceSquared(tower.Position, enemy.Position) <= range * range).ToArray();
        }

        var recipients = GetProtocolRecipients(tower);
        return Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped && recipients.Any(recipient =>
        {
            var range = GetEffectiveRange(recipient);
            return Vector2.DistanceSquared(recipient.Position, enemy.Position) <= range * range;
        })).ToArray();
    }

    private TowerInstance[] GetProtocolRecipients(TowerInstance supportTower)
    {
        var auraRange = GetEffectiveAuraRange(supportTower);
        var auraRangeSquared = auraRange * auraRange;
        return Towers.Where(recipient => recipient.Id != supportTower.Id && !recipient.IsSupport && !recipient.IsSandboxDisabled &&
            Vector2.DistanceSquared(supportTower.Position, recipient.Position) <= auraRangeSquared).ToArray();
    }

    private void ApplyProtocolBurst(TowerInstance tower)
    {
        var protocol = tower.Protocol;
        if (protocol.BurstRadius <= 0 || (protocol.BurstDamage <= 0 && string.IsNullOrWhiteSpace(protocol.BurstStatus))) return;
        StatusType? statusType = Enum.TryParse<StatusType>(protocol.BurstStatus, true, out var parsedStatus) ? parsedStatus : null;
        foreach (var enemy in Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                     Vector2.DistanceSquared(tower.Position, enemy.Position) <= protocol.BurstRadius * protocol.BurstRadius).ToArray())
        {
            StatusApplication? status = statusType is null || protocol.BurstStatusMagnitude <= 0 || protocol.BurstStatusDuration <= 0
                ? null
                : new StatusApplication
                {
                    Type = statusType.Value,
                    Duration = protocol.BurstStatusDuration,
                    Magnitude = protocol.BurstStatusMagnitude,
                    SourceId = tower.Id
                };
            if (protocol.BurstDamage > 0)
            {
                DamageResolver.Apply(enemy, new DamagePayload
                {
                    Damage = GetEffectiveDamage(tower, protocol.BurstDamage),
                    ArmorPierce = GetEffectiveArmorPierce(tower, tower.Level.ArmorPierce),
                    Status = status,
                    SourceTowerId = tower.Id
                });
            }
            else if (status is not null)
            {
                enemy.ApplyStatus(status);
            }
        }
    }

    public void CycleSelectedTarget()
    {
        if (SelectedTower is not { IsSupport: false } tower) return;
        var currentIndex = Array.IndexOf(SupportTargetingEnabled ? SignalGauntletTargetModes : StandardTargetModes,
            tower.TargetMode);
        var modes = AvailableTargetModes;
        tower.TargetMode = modes[(currentIndex + 1 + modes.Count) % modes.Count];
    }

    public bool TryUpgradeGenerator(int requestingPlayerId = 1)
    {
        if (requestingPlayerId is < 1 or > 2 || Generator is not { CanUpgrade: true } generator) return false;
        var cost = generator.UpgradeCost;
        if (!Economy.TrySpend(cost) || !generator.TryUpgrade()) return false;
        GeneratorUpgraded?.Invoke(generator, cost);
        return true;
    }

    public bool TrySellGenerator(int requestingPlayerId = 1)
    {
        if (!SellingEnabled || requestingPlayerId is < 1 or > 2 || Generator is not { } generator) return false;
        var value = generator.SellValue;
        Economy.RecoverSale(value);
        Generator = null;
        SelectedGenerator = null;
        HoveredGenerator = null;
        GeneratorSold?.Invoke(generator, value);
        return true;
    }

    public bool StartNextWave(bool? earlyStartEligible = null) =>
        !IsSandbox && Waves.TryStartNextWave(this, earlyStartEligible);

    public bool TryAutoStartNextWave(bool enabled, int delaySeconds = 0)
    {
        // Co-op readiness is a shared player decision and must never be
        // bypassed by one peer's local preference. Wave one also remains
        // manual so enabling this option cannot erase opening build time.
        if (!enabled || IsSandbox || IsCoOp || CurrentWave <= 0 || !CanStartWave)
            return false;
        var boundedDelay = Math.Clamp(delaySeconds, 0, (int)GameConstants.IntermissionSeconds);
        var elapsedIntermission = GameConstants.IntermissionSeconds - IntermissionRemaining;
        if (elapsedIntermission + 0.001f < boundedDelay) return false;

        // Selecting an automatic cadence is an advance commitment, so every
        // configured delay earns the early-call reward. Manual calls continue
        // to derive eligibility from the live intermission timer.
        return StartNextWave(true);
    }

    public void SetSpeed(float speed) => Speed = speed >= 1.5f ? 2f : 1f;

    public float SandboxHealthMultiplierForWave(int waveNumber)
    {
        var wave = Waves.GetAuthoredWave(waveNumber);
        return wave is null ? 1f : wave.HealthMultiplier * Difficulty.EnemyHealthMultiplier;
    }

    public bool SpawnSandboxTargets(string enemyId, int count, float healthMultiplier, string rank, bool immortal)
    {
        if (!IsSandbox || count is < 1 or > 24 || !float.IsFinite(healthMultiplier) || healthMultiplier <= 0 ||
            !_content.Enemies.TryGetValue(enemyId, out var definition)) return false;
        if (!Enum.TryParse<EnemyRank>(rank, true, out _)) return false;

        var spacing = MathF.Max(18f, definition.Visual.Radius * 1.35f);
        for (var index = 0; index < count; index++)
        {
            if (_nextEnemyId >= int.MaxValue) break;
            var enemy = new EnemyInstance(_nextEnemyId++, definition, Map.Path, healthMultiplier, 1f, rank, immortal);
            enemy.SetSandboxPathDistance(18f + index * spacing, Map.Path);
            Enemies.Add(enemy);
        }

        AnnouncementTitle = immortal ? "IMMORTAL TARGETS DEPLOYED" : $"{count} {definition.DisplayName.ToUpperInvariant()} DEPLOYED";
        AnnouncementSubtitle = immortal
            ? "Damage and status effects register, but target health cannot fall."
            : $"Fixed health scale {healthMultiplier:0.##}x // {rank.ToUpperInvariant()} rank.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 2.5f;
        return true;
    }

    public bool StartSandboxWave(int waveNumber)
    {
        if (!IsSandbox || _sandboxActiveWave is not null || Waves.GetAuthoredWave(waveNumber) is not { } wave) return false;
        _sandboxActiveWave = wave;
        _sandboxGroupIndex = 0;
        _sandboxSpawnedInGroup = 0;
        _sandboxGroupTimer = 0;
        _sandboxDelayRemaining = wave.Groups.Count > 0 ? wave.Groups[0].DelayBefore : 0;
        _sandboxQueuedEnemies = wave.Groups.Sum(group => group.Count);
        AnnouncementTitle = $"TEST WAVE {wave.Number} // {wave.Archetype.ToUpperInvariant()}";
        AnnouncementSubtitle = $"{Map.Definition.DisplayName} composition with {Difficulty.DisplayName} scaling.";
        AnnouncementPositive = false;
        AnnouncementRemaining = 2.8f;
        return wave.Groups.Count > 0;
    }

    public void ClearSandboxTargets()
    {
        if (!IsSandbox) return;
        CancelSandboxWave();
        Enemies.Clear();
        Projectiles.Clear();
        Effects.Clear();
        AnnouncementTitle = "TEST TARGETS CLEARED";
        AnnouncementSubtitle = "Tower placement and lifetime test data were preserved.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 1.8f;
    }

    public void ResetSandboxExperiment()
    {
        if (!IsSandbox) return;
        ClearSandboxTargets();
        OverdriveCooldownRemaining = 0;
        AutoOverdriveTowerId = 0;
        foreach (var tower in Towers) tower.ResetSandboxTelemetry();
        AnnouncementTitle = "TEST RESET";
        AnnouncementSubtitle = "Targets, shots, tower metrics, and Protocol timers cleared; towers preserved.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 2.2f;
    }

    public bool RemoveSandboxTower(int towerId)
    {
        if (!IsSandbox) return false;
        var tower = Towers.FirstOrDefault(candidate => candidate.Id == towerId);
        if (tower is null || !TrySellTower(towerId)) return false;
        AnnouncementTitle = "TOWER REMOVED";
        AnnouncementSubtitle = $"{tower.Definition.DisplayName} was removed; targets and other towers were preserved.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 1.8f;
        return true;
    }

    public bool ToggleSandboxTower(int towerId)
    {
        if (!IsSandbox) return false;
        var tower = Towers.FirstOrDefault(candidate => candidate.Id == towerId);
        if (tower is null) return false;
        tower.ToggleSandboxDisabled();
        if (AutoOverdriveTowerId == tower.Id) AutoOverdriveTowerId = 0;
        AnnouncementTitle = tower.IsSandboxDisabled ? "TOWER DISABLED" : "TOWER ENABLED";
        AnnouncementSubtitle = $"{tower.Definition.DisplayName} {(tower.IsSandboxDisabled ? "will not attack or provide support." : "has returned to the experiment.")}";
        AnnouncementPositive = !tower.IsSandboxDisabled;
        AnnouncementRemaining = 1.6f;
        return true;
    }

    public void ClearSandboxTowers()
    {
        if (!IsSandbox) return;
        Towers.Clear();
        Projectiles.Clear();
        Effects.Clear();
        SelectedTower = null;
        HoveredTower = null;
        AutoOverdriveTowerId = 0;
        OverdriveCooldownRemaining = 0;
        AnnouncementTitle = "ALL TOWERS CLEARED";
        AnnouncementSubtitle = "Targets remain in place for a fresh defense layout.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 1.8f;
    }

    public bool ResetSandboxProtocol()
    {
        if (!IsSandbox) return false;
        OverdriveCooldownRemaining = 0;
        AutoOverdriveTowerId = 0;
        foreach (var tower in Towers) tower.ClearOverdrive();
        AnnouncementTitle = "PROTOCOL TEST READY";
        AnnouncementSubtitle = "Active effects and the shared cooldown were reset; targets and metrics remain.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 1.8f;
        return true;
    }

    public bool TestSandboxProtocol(int towerId)
    {
        if (!IsSandbox) return false;
        var tower = Towers.FirstOrDefault(candidate => candidate.Id == towerId);
        if (tower is null || tower.IsSandboxDisabled) return false;
        OverdriveCooldownRemaining = 0;
        AutoOverdriveTowerId = 0;
        foreach (var candidate in Towers) candidate.ClearOverdrive();
        if (!TryOverdriveTower(tower.Id)) return false;
        AnnouncementTitle = $"TESTING {tower.Protocol.DisplayName.ToUpperInvariant()}";
        AnnouncementSubtitle = "The selected tower's Protocol restarted from a clean timer.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 1.8f;
        return true;
    }

    private void UpdateSandboxWave(float deltaSeconds)
    {
        if (_sandboxActiveWave is not { } wave || wave.Groups.Count == 0) return;
        if (_sandboxGroupIndex >= wave.Groups.Count)
        {
            CancelSandboxWave();
            return;
        }
        if (_sandboxDelayRemaining > 0)
        {
            _sandboxDelayRemaining -= deltaSeconds;
            if (_sandboxDelayRemaining > 0) return;
        }

        var group = wave.Groups[_sandboxGroupIndex];
        _sandboxGroupTimer -= deltaSeconds;
        if (_sandboxSpawnedInGroup < group.Count && _sandboxGroupTimer <= 0)
        {
            SpawnEnemy(group.EnemyId, wave.HealthMultiplier, wave.SpeedMultiplier, group.Rank);
            _sandboxSpawnedInGroup++;
            _sandboxQueuedEnemies = Math.Max(0, _sandboxQueuedEnemies - 1);
            _sandboxGroupTimer += group.SpawnInterval;
        }

        if (_sandboxSpawnedInGroup < group.Count) return;
        _sandboxGroupIndex++;
        if (_sandboxGroupIndex >= wave.Groups.Count)
        {
            _sandboxActiveWave = null;
            _sandboxQueuedEnemies = 0;
            return;
        }
        _sandboxSpawnedInGroup = 0;
        _sandboxGroupTimer = 0;
        _sandboxDelayRemaining = wave.Groups[_sandboxGroupIndex].DelayBefore;
    }

    private void CancelSandboxWave()
    {
        _sandboxActiveWave = null;
        _sandboxGroupIndex = 0;
        _sandboxSpawnedInGroup = 0;
        _sandboxGroupTimer = 0;
        _sandboxDelayRemaining = 0;
        _sandboxQueuedEnemies = 0;
    }

    public bool SetCoOpPaused(bool paused, int playerId = 1)
    {
        if (!IsCoOp || playerId is < 1 or > 2) return false;
        IsCoOpPaused = paused;
        CoOpPausePlayerId = paused ? playerId : 0;
        if (paused) CancelPlacement();
        return true;
    }

    public bool BeginEndlessMode()
    {
        if (!IsVictory || IsDefeat || !Waves.EnableEndlessMode()) return false;
        IsVictory = false;
        AnnouncementTitle = "ENDLESS // APEX ONLINE";
        AnnouncementSubtitle = $"Generated Endless begins at wave {GameConstants.GeneratedEndlessStartWave}. Promote final-tier towers or expand the defense.";
        AnnouncementPositive = true;
        AnnouncementRemaining = 3.4f;
        return true;
    }

    public TowerBuff GetSupportBuff(TowerInstance tower) => _buffSystem.Get(tower);

    public float GetEffectiveRange(TowerInstance tower)
    {
        var support = _buffSystem.Get(tower);
        var power = Map.GetPowerBuff(tower.Position);
        var protocol = tower.IsOverdriven ? tower.Protocol.RangeBonus : 0f;
        return tower.Level.Range * (1f + support.RangeBonus + power.RangeBonus + protocol);
    }

    public float GetEffectiveAuraRange(TowerInstance tower) => tower.EffectiveAuraRange;

    public float GetEffectiveAttacksPerSecond(TowerInstance tower)
    {
        var support = _buffSystem.Get(tower);
        var power = Map.GetPowerBuff(tower.Position);
        var overdrive = tower.IsOverdriven ? tower.Protocol.AttackSpeedBonus : 0f;
        return tower.Level.AttacksPerSecond * (1f + support.AttackSpeedBonus + power.AttackSpeedBonus + overdrive) *
            GetSignalRateMultiplier(tower);
    }

    public float GetEffectiveDamage(TowerInstance tower, float baseDamage)
    {
        var protocol = tower.IsOverdriven ? tower.Protocol.DamageBonus : 0f;
        return baseDamage * (1f + Map.GetPowerBuff(tower.Position).DamageBonus + protocol) *
            GetSignalDamageMultiplier(tower);
    }

    public float GetSignalRateMultiplier(TowerInstance tower) =>
        tower.IsSuppressed ? 1f - Challenge.CounterSuppressionRatePenalty : 1f;

    public float GetSignalDamageMultiplier(TowerInstance tower) =>
        tower.IsSuppressed ? 1f - Challenge.CounterSuppressionDamagePenalty : 1f;

    public float GetEffectiveArmorPierce(TowerInstance tower, float baseArmorPierce)
    {
        var protocol = tower.IsOverdriven ? tower.Protocol.ArmorPierceBonus : 0f;
        return baseArmorPierce + Map.GetPowerBuff(tower.Position).ArmorPierceBonus + protocol;
    }

    public EnemySignalRole ResolveEnemySignalRole(WaveDefinition wave, int groupIndex, int spawnedInGroup,
        WaveGroupDefinition group)
    {
        if (!CounterPressureEnabled || wave.Number < 2 || group.Count <= 0) return EnemySignalRole.None;
        if (Enum.TryParse<EnemyRank>(group.Rank, true, out var rank) && rank is EnemyRank.Elite or EnemyRank.Boss)
            return CounterAttackersEnabled ? EnemySignalRole.Disruptor : EnemySignalRole.None;

        var carrierIndex = (group.Count - 1) / 2;
        if (spawnedInGroup != carrierIndex) return EnemySignalRole.None;
        if (wave.Number >= 6 && (wave.Number + groupIndex) % 2 != 0) return EnemySignalRole.None;

        EnemySignalRole[] roles =
        [
            EnemySignalRole.Accelerator,
            EnemySignalRole.Restorer,
            EnemySignalRole.Bulwark,
            EnemySignalRole.Jammer
        ];
        EnemySignalRole role;
        if (wave.Number <= 5)
            role = groupIndex < wave.Number - 1 ? roles[groupIndex] : EnemySignalRole.None;
        else if (group.EnemyId.Contains("aegis", StringComparison.OrdinalIgnoreCase)) role = EnemySignalRole.Bulwark;
        else if (group.EnemyId.Contains("regenerator", StringComparison.OrdinalIgnoreCase)) role = EnemySignalRole.Restorer;
        else role = roles[(wave.Number + groupIndex) % roles.Length];
        return IsCounterRoleEnabled(role) ? role : EnemySignalRole.None;
    }

    internal void ConfigureCounterPressureSimulation(bool supportEnabled, bool attackersEnabled)
    {
        _counterSupportSimulationEnabled = supportEnabled;
        _counterAttackersSimulationEnabled = attackersEnabled;
    }

    private bool IsCounterRoleEnabled(EnemySignalRole role) => role switch
    {
        EnemySignalRole.Accelerator or EnemySignalRole.Restorer or EnemySignalRole.Bulwark => CounterSupportEnabled,
        EnemySignalRole.Jammer or EnemySignalRole.Disruptor => CounterAttackersEnabled,
        _ => true
    };

    public void SpawnEnemy(string enemyId, float healthMultiplier, float speedMultiplier, string rank = "Standard",
        EnemySignalRole signalRole = EnemySignalRole.None)
    {
        if (_nextEnemyId >= int.MaxValue || !_content.Enemies.TryGetValue(enemyId, out var definition)) return;
        var enemy = new EnemyInstance(_nextEnemyId++, definition, Map.Path,
            healthMultiplier * Difficulty.EnemyHealthMultiplier,
            speedMultiplier * Difficulty.EnemySpeedMultiplier,
            rank, signalRole: signalRole);
        if (IsCounterRoleEnabled(signalRole) && signalRole is EnemySignalRole.Restorer or EnemySignalRole.Bulwark or
            EnemySignalRole.Jammer or EnemySignalRole.Disruptor)
        {
            var initialDelay = signalRole switch
            {
                EnemySignalRole.Jammer => 2.6f,
                EnemySignalRole.Restorer => 3.0f,
                EnemySignalRole.Bulwark => 3.4f,
                _ => enemy.IsBoss ? 1.6f : enemy.IsElite ? 2.2f : 2.8f
            };
            enemy.ArmSignalAbility(initialDelay + enemy.Id % 3 * 0.35f);
        }
        Enemies.Add(enemy);
        EnemySpawned?.Invoke(enemy);
        if (enemy.IsBoss)
        {
            AnnouncementTitle = "BOSS SIGNAL // BASTION CORE";
            AnnouncementSubtitle = "Break its shield; expect an overdrive phase at half integrity.";
            AnnouncementPositive = false;
            AnnouncementRemaining = 3.4f;
            Effects.AddFlash(enemy.Position, ColorPalette.Coral, 0.8f, enemy.Radius + 24);
        }
    }

    public void OnBossPhaseChanged(EnemyInstance enemy)
    {
        AnnouncementTitle = "BASTION CORE // OVERDRIVE";
        AnnouncementSubtitle = "Shield restored. Movement accelerated. Control resistance remains high.";
        AnnouncementPositive = false;
        AnnouncementRemaining = 3.2f;
        Effects.AddFlash(enemy.Position, ColorPalette.Gold, 0.9f, enemy.Radius + 34);
        BossPhaseChanged?.Invoke(enemy);
    }

    public void OnWaveStarted(WaveDefinition wave, int earlyCallBonus = 0)
    {
        EmergencyDirectPurchasesThisWave = 0;
        AnnouncementTitle = $"WAVE {wave.Number} // {wave.Archetype.ToUpperInvariant()}";
        var briefing = CounterPressureEnabled ? wave.Number switch
        {
            2 => "SIGNAL: ACCELERATOR // Nearby threats move faster.",
            3 => "SIGNAL: RESTORER // Nearby threats recover health.",
            4 => "SIGNAL: BULWARK // Nearby threats gain shields.",
            5 => "SIGNAL: JAMMER // One nearby tower is weakened.",
            _ => wave.Briefing
        } : wave.Briefing;
        AnnouncementSubtitle = earlyCallBonus > 0 ? $"EARLY CALL +{earlyCallBonus} // {briefing}" : briefing;
        AnnouncementPositive = false;
        AnnouncementRemaining = 2.4f;
        WaveStarted?.Invoke(wave);
    }

    public void OnWaveCompleted(int waveNumber)
    {
        foreach (var tower in Towers) tower.ClearSignalInterference();
        var campaignCleared = !IsEndlessMode && waveNumber == TotalWaves;
        var finalEscalationUnlocked = !IsEndlessMode && waveNumber == GameConstants.ApexUnlockWave - 1 &&
            TotalWaves >= GameConstants.CampaignWaveCount;
        AnnouncementTitle = campaignCleared
            ? "CAMPAIGN SECURED"
            : finalEscalationUnlocked ? "FINAL ESCALATION" : $"WAVE {waveNumber} CLEARED";
        AnnouncementSubtitle = campaignCleared
            ? $"Generated Endless begins at wave {GameConstants.GeneratedEndlessStartWave}."
            : finalEscalationUnlocked
                ? "APEX PROMOTIONS UNLOCKED // 10 WAVES REMAIN"
                : $"+{EconomyService.CalculateWaveReward(waveNumber)} completion credits";
        AnnouncementPositive = true;
        AnnouncementRemaining = campaignCleared || finalEscalationUnlocked ? 3.2f : 2.2f;
        WaveCompleted?.Invoke(waveNumber);
    }

    public void RefreshEnemySignalFormation()
    {
        foreach (var enemy in Enemies) enemy.SetFormationSpeedMultiplier(1f);
        if (!CounterSupportEnabled) return;

        var radiusSquared = Challenge.CounterSupportRadius * Challenge.CounterSupportRadius;
        var accelerators = Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                enemy.SignalRole == EnemySignalRole.Accelerator)
            .ToArray();
        if (accelerators.Length == 0) return;

        foreach (var enemy in Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped))
        {
            if (accelerators.Any(source => source.Id != enemy.Id &&
                    Vector2.DistanceSquared(source.Position, enemy.Position) <= radiusSquared))
                enemy.SetFormationSpeedMultiplier(1f + Challenge.CounterHasteBonus);
        }
    }

    public void TryActivateEnemySignal(EnemyInstance enemy)
    {
        if (!CounterPressureEnabled || !IsCounterRoleEnabled(enemy.SignalRole) ||
            enemy.SignalRole is EnemySignalRole.None or EnemySignalRole.Accelerator) return;
        if (enemy.SignalRole == EnemySignalRole.Disruptor)
        {
            TryEmitDisruption(enemy);
            return;
        }

        var interval = Challenge.CounterPressureInterval * (enemy.SignalRole == EnemySignalRole.Bulwark ? 1.12f : 1f);
        if (!enemy.TryActivateSignalAbility(interval)) return;

        var radiusSquared = Challenge.CounterSupportRadius * Challenge.CounterSupportRadius;
        var nearbyEnemies = Enemies.Where(target => !target.IsDead && !target.HasEscaped &&
                Vector2.DistanceSquared(target.Position, enemy.Position) <= radiusSquared)
            .OrderBy(target => Vector2.DistanceSquared(target.Position, enemy.Position))
            .Take(7)
            .ToArray();
        if (enemy.SignalRole == EnemySignalRole.Restorer)
        {
            var restored = 0f;
            foreach (var target in nearbyEnemies)
            {
                var amount = target.RestoreHealth(target.MaxHealth * Challenge.CounterRepairFraction);
                if (amount <= 0) continue;
                restored += amount;
                Effects.AddBeam(enemy.Position, target.Position, ColorPalette.Green, 0.46f);
                Effects.AddFlash(target.Position, ColorPalette.Green, 0.32f, target.Radius + 4);
            }
            if (restored <= 0) return;
            Effects.AddSplash(enemy.Position, ColorPalette.Green, Challenge.CounterSupportRadius);
            return;
        }
        if (enemy.SignalRole == EnemySignalRole.Bulwark)
        {
            var granted = 0f;
            foreach (var target in nearbyEnemies)
            {
                var amount = target.GrantShield(target.MaxHealth * Challenge.CounterShieldFraction,
                    target.Definition.Shield + target.MaxHealth * Challenge.CounterShieldCapacityFraction);
                if (amount <= 0) continue;
                granted += amount;
                Effects.AddBeam(enemy.Position, target.Position, ColorPalette.Shield, 0.46f);
                Effects.AddFlash(target.Position, ColorPalette.Shield, 0.32f, target.Radius + 5);
            }
            if (granted <= 0) return;
            Effects.AddSplash(enemy.Position, ColorPalette.Shield, Challenge.CounterSupportRadius);
            return;
        }

        var suppressionRadiusSquared = Challenge.CounterSuppressionRadius * Challenge.CounterSuppressionRadius;
        var targetTower = Towers.Where(tower => !tower.IsSupport && !tower.IsSandboxDisabled &&
                Vector2.DistanceSquared(tower.Position, enemy.Position) <= suppressionRadiusSquared)
            .OrderBy(tower => Vector2.DistanceSquared(tower.Position, enemy.Position))
            .ThenBy(tower => tower.Id)
            .FirstOrDefault(tower => tower.ApplySuppression(Challenge.CounterSuppressionDuration, 2.4f));
        if (targetTower is null) return;
        Effects.AddFlash(enemy.Position, ColorPalette.Orange, 0.32f, enemy.Radius + 8);
        Effects.AddBeam(enemy.Position, targetTower.Position, ColorPalette.Orange, 0.40f);
        Effects.AddFlash(targetTower.Position, ColorPalette.Orange, 0.34f,
            targetTower.Definition.Visual.Radius + 7);
    }

    private void TryEmitDisruption(EnemyInstance enemy)
    {
        var interval = Challenge.CounterPressureInterval * (enemy.IsBoss ? 0.72f : enemy.IsElite ? 0.86f : 1f);
        if (!enemy.TryActivateSignalAbility(interval)) return;

        var radius = Challenge.CounterPressureRadius * (enemy.IsBoss ? 1.32f : enemy.IsElite ? 1.12f : 1f);
        var duration = Challenge.CounterPressureDuration * (enemy.IsBoss ? 1.55f : enemy.IsElite ? 1.22f : 1f);
        var radiusSquared = radius * radius;
        var affected = Towers.Where(tower => !tower.IsSandboxDisabled &&
                Vector2.DistanceSquared(tower.Position, enemy.Position) <= radiusSquared)
            .Where(tower => tower.ApplyDisruption(duration, 2.4f))
            .ToArray();
        if (affected.Length == 0) return;

        Effects.AddSplash(enemy.Position, ColorPalette.Violet, radius);
        foreach (var tower in affected.Take(5))
        {
            Effects.AddBeam(enemy.Position, tower.Position, ColorPalette.Violet, 0.38f);
            Effects.AddFlash(tower.Position, ColorPalette.Violet, 0.22f, tower.Definition.Visual.Radius + 7);
        }
    }

    public void OnEnemyKilled(EnemyInstance enemy)
    {
        if (!_resolvedOutcomes.Add(enemy.Id)) return;
        Economy.AwardKill(enemy.Reward, CurrentWave);
        Effects.AddShatter(enemy.Position, enemy.Definition.Visual.PrimaryColor, enemy.Radius + 8);
        EnemyKilled?.Invoke(enemy);
    }

    public void OnEnemyEscaped(EnemyInstance enemy)
    {
        if (!_resolvedOutcomes.Add(enemy.Id)) return;
        Economy.LoseLives(enemy.LivesLost);
        Effects.AddFlash(enemy.Position, ColorPalette.Coral, 0.35f, enemy.Radius + 12);
        EnemyEscaped?.Invoke(enemy);
    }

    public void OnEmergencyDefenseTriggered(PulsePlateInstance defense, int hitCount) => EmergencyDefenseTriggered?.Invoke(defense, hitCount);
    public void OnEmergencyDefenseExpired(PulsePlateInstance defense) => EmergencyDefenseExpired?.Invoke(defense);

    public void OnEmergencyChargeProduced()
    {
        if (Generator is null || EmergencyInventory >= Generator.Level.Capacity) return;
        EmergencyInventory++;
        Effects.AddFlash(Generator.Position, Generator.Definition.Visual.PrimaryColor, 0.22f, 28);
        EmergencyChargeProduced?.Invoke();
    }

    public SaveGameData CaptureSaveGame()
    {
        if (!CanSaveCheckpoint)
            throw new InvalidOperationException("Games can only be saved between waves.");
        return new SaveGameData
        {
            RunId = RunId,
            IsCoOp = IsCoOp,
            MapId = Map.Definition.Id,
            DifficultyId = DifficultyId,
            ChallengeId = ChallengeId,
            Speed = Speed,
            OverdriveCooldownRemaining = OverdriveCooldownRemaining,
            AutoOverdriveTowerId = AutoOverdriveTowerId,
            EmergencyInventory = EmergencyInventory,
            EmergencyDirectPurchasesThisWave = EmergencyDirectPurchasesThisWave,
            NextEnemyId = _nextEnemyId,
            NextTowerId = _nextTowerId,
            NextEmergencyDefenseId = _nextEmergencyDefenseId,
            Economy = Economy.CaptureSaveData(),
            Waves = Waves.CaptureSaveData(),
            Towers = Towers.Select(x => x.CaptureSaveData()).ToList(),
            PulsePlates = EmergencyDefenses.Select(x => x.CaptureSaveData()).ToList(),
            Generator = Generator?.CaptureSaveData(),
            Statistics = Statistics.CaptureSaveData()
        };
    }

    public CoOpStateSnapshot CaptureCoOpState(long tick, int readyMask, bool waveStartQueued, bool waveEarlyBonusQueued = false) => new()
    {
        RunId = RunId,
        MapId = Map.Definition.Id,
        DifficultyId = DifficultyId,
        ChallengeId = ChallengeId,
        Tick = Math.Max(0, tick),
        ReadyMask = readyMask,
        WaveStartQueued = waveStartQueued,
        WaveEarlyBonusQueued = waveEarlyBonusQueued,
        IsPaused = IsCoOpPaused,
        PausedByPlayerId = CoOpPausePlayerId,
        Speed = Speed,
        OverdriveCooldownRemaining = OverdriveCooldownRemaining,
        AutoOverdriveTowerId = AutoOverdriveTowerId,
        EmergencyInventory = EmergencyInventory,
        EmergencyDirectPurchasesThisWave = EmergencyDirectPurchasesThisWave,
        NextEnemyId = _nextEnemyId,
        NextTowerId = _nextTowerId,
        NextEmergencyDefenseId = _nextEmergencyDefenseId,
        IsVictory = IsVictory,
        IsDefeat = IsDefeat,
        AnnouncementTitle = AnnouncementTitle,
        AnnouncementSubtitle = AnnouncementSubtitle,
        AnnouncementRemaining = AnnouncementRemaining,
        AnnouncementPositive = AnnouncementPositive,
        Economy = Economy.CaptureSaveData(),
        Waves = Waves.CaptureCoOpState(),
        Towers = Towers.Select(tower => tower.CaptureSaveData()).ToList(),
        Enemies = Enemies.Select(enemy => enemy.CaptureCoOpState()).ToList(),
        Projectiles = Projectiles.CaptureCoOpState(),
        PulsePlates = EmergencyDefenses.Select(defense => defense.CaptureSaveData()).ToList(),
        Generator = Generator?.CaptureSaveData(),
        Statistics = Statistics.CaptureSaveData()
    };

    public static GameSession RestoreCoOpState(GameContent content, CoOpStateSnapshot data, int localPlayerId)
    {
        CoOpSnapshotValidator.Validate(data);
        if (localPlayerId is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
        var knownMap = content.Maps.ContainsKey(data.MapId) || content.Map.Id.Equals(data.MapId, StringComparison.OrdinalIgnoreCase);
        if (!knownMap) throw new InvalidDataException($"Network map '{data.MapId}' is not available.");

        var session = new GameSession(content, data.MapId, data.DifficultyId, data.ChallengeId);
        ValidateRestoredHeaderState(session, data.Speed, data.Economy, false);
        session.RunId = NormalizeRunId(data.RunId, session.RunId);
        session.ConfigureCoOp(localPlayerId);
        session.IsCoOpPaused = data.IsPaused;
        session.CoOpPausePlayerId = data.IsPaused ? data.PausedByPlayerId : 0;
        session.Economy.RestoreSaveData(data.Economy);
        session.Waves.RestoreCoOpState(data.Waves);
        session.Speed = data.Speed >= 1.5f ? 2f : 1f;
        session.OverdriveCooldownRemaining = session.ProtocolsEnabled ? MathF.Max(0, data.OverdriveCooldownRemaining) : 0;
        session.AutoOverdriveTowerId = session.ProtocolsEnabled && data.Towers.Any(tower => tower.Id == data.AutoOverdriveTowerId)
            ? data.AutoOverdriveTowerId
            : 0;
        session.EmergencyInventory = Math.Max(0, data.EmergencyInventory);
        session.EmergencyDirectPurchasesThisWave = Math.Max(0, data.EmergencyDirectPurchasesThisWave);

        foreach (var savedTower in data.Towers)
        {
            if (!content.Towers.TryGetValue(savedTower.DefinitionId, out var definition))
                throw new InvalidDataException($"Network tower '{savedTower.DefinitionId}' is not available.");
            if (savedTower.InvestedCredits < definition.PurchaseCost)
                throw new InvalidDataException($"Network tower '{savedTower.DefinitionId}' has impossible investment state.");
            var tower = TowerInstance.RestoreCoOpState(savedTower, definition);
            session.NormalizeTargetMode(tower);
            if (!session.ProtocolsEnabled) tower.ClearOverdrive();
            session.Towers.Add(tower);
        }

        foreach (var savedEnemy in data.Enemies)
        {
            if (!content.Enemies.TryGetValue(savedEnemy.DefinitionId, out var definition))
                throw new InvalidDataException($"Network enemy '{savedEnemy.DefinitionId}' is not available.");
            if (savedEnemy.DistanceAlongPath > session.Map.Path.TotalLength + 0.01f)
                throw new InvalidDataException("Network enemy progress is outside the selected map path.");
            var rankHealthMultiplier = savedEnemy.Rank switch { EnemyRank.Elite => 1.85f, EnemyRank.Boss => 4.5f, _ => 1f };
            var maximumHealth = definition.MaxHealth * savedEnemy.HealthMultiplier * rankHealthMultiplier;
            var maximumShield = definition.Shield + (savedEnemy.Rank == EnemyRank.Boss ? maximumHealth * 0.12f : 0);
            if (!float.IsFinite(maximumHealth) || savedEnemy.Health > maximumHealth + 0.01f ||
                savedEnemy.Shield > maximumShield + 0.01f)
                throw new InvalidDataException("Network enemy health or shield exceeds its authored maximum.");
            session.Enemies.Add(EnemyInstance.RestoreCoOpState(savedEnemy, definition, session.Map.Path));
        }

        ValidateRestoredTacticalState(session, data.PulsePlates, data.Generator);
        foreach (var savedPlate in data.PulsePlates.Where(plate => plate.ChargesRemaining > 0))
            session.EmergencyDefenses.Add(PulsePlateInstance.RestoreSaveData(savedPlate, content.Tactics.EmergencyDefense));
        if (data.Generator is not null)
            session.Generator = ChargeForgeInstance.RestoreSaveData(data.Generator, content.Tactics.Generator);
        ValidateRestoredDefenseLayout(session);

        session._nextEnemyId = Math.Max(data.NextEnemyId, session.Enemies.Select(enemy => enemy.Id).DefaultIfEmpty(0).Max() + 1);
        session._nextTowerId = Math.Max(data.NextTowerId, session.Towers.Select(tower => tower.Id).DefaultIfEmpty(0).Max() + 1);
        session._nextEmergencyDefenseId = Math.Max(data.NextEmergencyDefenseId, session.EmergencyDefenses.Select(defense => defense.Id).DefaultIfEmpty(0).Max() + 1);
        session.Statistics.RestoreSaveData(data.Statistics, session.Towers);
        session.Projectiles.RestoreCoOpState(data.Projectiles, session.Enemies.ToDictionary(enemy => enemy.Id));
        session._buffSystem.Update(session.Towers);
        session.SelectedTower = null;
        session.SelectedGenerator = null;
        session.HoveredTower = null;
        session.HoveredGenerator = null;
        session.CancelPlacement();
        session.IsVictory = data.IsVictory;
        session.IsDefeat = data.IsDefeat;
        session.AnnouncementTitle = data.AnnouncementTitle;
        session.AnnouncementSubtitle = data.AnnouncementSubtitle;
        session.AnnouncementRemaining = MathF.Max(0, data.AnnouncementRemaining);
        session.AnnouncementPositive = data.AnnouncementPositive;
        return session;
    }

    public static GameSession RestoreSaveGame(GameContent content, SaveGameData data)
    {
        var knownMap = content.Maps.ContainsKey(data.MapId) || content.Map.Id.Equals(data.MapId, StringComparison.OrdinalIgnoreCase);
        if (!knownMap) throw new InvalidDataException($"Saved map '{data.MapId}' is not available.");

        var session = new GameSession(content, data.MapId, data.DifficultyId, data.ChallengeId);
        ValidateRestoredHeaderState(session, data.Speed, data.Economy, string.IsNullOrWhiteSpace(data.DifficultyId));
        session.RunId = NormalizeRunId(data.RunId, session.RunId);
        if (data.IsCoOp) session.ConfigureCoOp(1);
        session.Economy.RestoreSaveData(data.Economy);
        session.Waves.RestoreSaveData(NormalizeCampaignWaveState(data.Waves, session.TotalWaves));
        session.Speed = data.Speed >= 1.5f ? 2f : 1f;
        session.OverdriveCooldownRemaining = session.ProtocolsEnabled ? MathF.Max(0, data.OverdriveCooldownRemaining) : 0;
        session.AutoOverdriveTowerId = session.ProtocolsEnabled && data.Towers.Any(tower => tower.Id == data.AutoOverdriveTowerId)
            ? data.AutoOverdriveTowerId
            : 0;
        session.EmergencyInventory = Math.Max(0, data.EmergencyInventory);
        session.EmergencyDirectPurchasesThisWave = Math.Max(0, data.EmergencyDirectPurchasesThisWave);

        foreach (var savedTower in data.Towers)
        {
            if (!content.Towers.TryGetValue(savedTower.DefinitionId, out var definition))
                throw new InvalidDataException($"Saved tower '{savedTower.DefinitionId}' is not available.");
            if (savedTower.InvestedCredits < definition.PurchaseCost)
                throw new InvalidDataException($"Saved tower '{savedTower.DefinitionId}' has impossible investment state.");
            var tower = TowerInstance.RestoreSaveData(savedTower, definition);
            session.NormalizeTargetMode(tower);
            if (!session.ProtocolsEnabled) tower.ClearOverdrive();
            session.Towers.Add(tower);
        }
        ValidateRestoredTacticalState(session, data.PulsePlates, data.Generator);
        foreach (var savedPlate in data.PulsePlates.Where(x => x.ChargesRemaining > 0))
            session.EmergencyDefenses.Add(PulsePlateInstance.RestoreSaveData(savedPlate, content.Tactics.EmergencyDefense));
        if (data.Generator is not null)
            session.Generator = ChargeForgeInstance.RestoreSaveData(data.Generator, content.Tactics.Generator);
        ValidateRestoredDefenseLayout(session);

        session._nextEnemyId = Math.Max(data.NextEnemyId, 1);
        session._nextTowerId = Math.Max(data.NextTowerId, session.Towers.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
        session._nextEmergencyDefenseId = Math.Max(data.NextEmergencyDefenseId, session.EmergencyDefenses.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
        session.Statistics.RestoreSaveData(data.Statistics, session.Towers);
        session._buffSystem.Update(session.Towers);
        session.SelectedTower = null;
        session.SelectedGenerator = null;
        session.HoveredTower = null;
        session.HoveredGenerator = null;
        session.CancelPlacement();
        session.IsVictory = false;
        session.IsDefeat = false;
        session.AnnouncementTitle = "SAVE RESTORED";
        session.AnnouncementSubtitle = $"Surge Divide state resumed after wave {session.CurrentWave}.";
        if (!session.Map.Definition.Id.Equals("relay_divide", StringComparison.OrdinalIgnoreCase))
            session.AnnouncementSubtitle = $"{session.Map.Definition.DisplayName} resumed after wave {session.CurrentWave}.";
        session.AnnouncementRemaining = 2.8f;
        session.AnnouncementPositive = true;
        return session;
    }

    private void NormalizeTargetMode(TowerInstance tower)
    {
        if (IsTargetModeAvailable(tower.TargetMode)) return;
        tower.TargetMode = Enum.TryParse<TargetMode>(tower.Definition.DefaultTargetMode, true, out var authoredMode) &&
                           IsTargetModeAvailable(authoredMode)
            ? authoredMode
            : TargetMode.First;
    }

    private static WaveSaveData NormalizeCampaignWaveState(WaveSaveData data, int authoredWaveCount)
    {
        if (data.CurrentWaveNumber >= authoredWaveCount) return data;

        return new WaveSaveData
        {
            CurrentWaveNumber = data.CurrentWaveNumber,
            IntermissionRemaining = data.IntermissionRemaining,
            IsFinalWaveCleared = false,
            EndlessModeEnabled = false
        };
    }

    private static void ValidateRestoredHeaderState(
        GameSession session,
        float speed,
        Persistence.EconomySaveData economy,
        bool allowLegacyLivesMigration)
    {
        if (speed is not (1f or 2f))
            throw new InvalidDataException("Restored simulation speed is invalid.");
        if (!allowLegacyLivesMigration && economy.Lives > session.Economy.StartingLives)
            throw new InvalidDataException("Restored lives exceed the selected difficulty's starting lives.");
    }

    private static void ValidateRestoredTacticalState(
        GameSession session,
        IReadOnlyList<Persistence.PulsePlateSaveData> plates,
        Persistence.GeneratorSaveData? generator)
    {
        var plateDefinition = session._content.Tactics.EmergencyDefense;
        if (plates.Any(plate => plate.ChargesRemaining <= 0 || plate.ChargesRemaining > plateDefinition.Charges ||
            plate.ArmRemaining > plateDefinition.ArmTime + 0.01f ||
            plate.CooldownRemaining > plateDefinition.TriggerCooldown + 0.01f))
            throw new InvalidDataException("Restored Pulse Plate charge or timer state exceeds its authored limits.");

        if (generator is null) return;
        var definition = session._content.Tactics.Generator;
        if (generator.LevelIndex < 0 || generator.LevelIndex >= definition.Levels.Count ||
            generator.InvestedCredits < definition.PurchaseCost ||
            !float.IsFinite(generator.ProductionRemaining) || generator.ProductionRemaining < 0 ||
            generator.ProductionRemaining > definition.Levels[generator.LevelIndex].ProductionSeconds + 0.01f)
            throw new InvalidDataException("Restored Charge Forge progression or timer state exceeds its authored limits.");
    }

    private static void ValidateRestoredDefenseLayout(GameSession session)
    {
        if (!session.TacticalSystemsEnabled &&
            (session.EmergencyInventory != 0 || session.EmergencyDirectPurchasesThisWave != 0 ||
             session.EmergencyDefenses.Count != 0 || session.Generator is not null))
            throw new InvalidDataException("Restored tactical defenses conflict with the selected directive.");

        foreach (var tower in session.Towers)
        {
            var position = tower.Position;
            if (!session.IsTowerAvailable(tower.Definition.Id) ||
                position.X < GameConstants.TowerRadius || position.X > GameConstants.MapWidth - GameConstants.TowerRadius ||
                position.Y < GameConstants.TopBarHeight + GameConstants.TowerRadius ||
                position.Y > GameConstants.LogicalHeight - GameConstants.TowerRadius ||
                !session.Map.IsBuildable(position) ||
                session.Map.Path.DistanceToPath(position) < GameConstants.PlacementPathClearance)
                throw new InvalidDataException("Restored tower placement is outside the selected map's legal defense area.");
        }
        for (var i = 0; i < session.Towers.Count; i++)
        for (var j = i + 1; j < session.Towers.Count; j++)
            if (Vector2.DistanceSquared(session.Towers[i].Position, session.Towers[j].Position) <
                GameConstants.TowerMinimumGap * GameConstants.TowerMinimumGap)
                throw new InvalidDataException("Restored tower placements overlap.");

        var plateDefinition = session._content.Tactics.EmergencyDefense;
        if (session.EmergencyDefenses.Count > plateDefinition.MaximumActive)
            throw new InvalidDataException("Restored Pulse Plate field exceeds its active capacity.");
        foreach (var plate in session.EmergencyDefenses)
        {
            var projection = session.Map.Path.Project(plate.Position);
            if (projection.DistanceToPath > 0.25f ||
                projection.DistanceAlongPath < plateDefinition.EndpointClearance ||
                projection.DistanceAlongPath > session.Map.Path.TotalLength - plateDefinition.EndpointClearance)
                throw new InvalidDataException("Restored Pulse Plate placement is outside the selected map's legal road area.");
        }
        for (var i = 0; i < session.EmergencyDefenses.Count; i++)
        for (var j = i + 1; j < session.EmergencyDefenses.Count; j++)
            if (Vector2.DistanceSquared(session.EmergencyDefenses[i].Position, session.EmergencyDefenses[j].Position) <
                plateDefinition.MinimumSpacing * plateDefinition.MinimumSpacing)
                throw new InvalidDataException("Restored Pulse Plate placements overlap.");

        if (session.Generator is not { } generator) return;
        var radius = generator.Definition.Visual.Radius;
        var generatorPosition = generator.Position;
        if (generatorPosition.X < radius || generatorPosition.X > GameConstants.MapWidth - radius ||
            generatorPosition.Y < GameConstants.TopBarHeight + radius ||
            generatorPosition.Y > GameConstants.LogicalHeight - radius ||
            !session.Map.IsBuildable(generatorPosition) ||
            session.Map.Path.DistanceToPath(generatorPosition) < GameConstants.PlacementPathClearance ||
            session.Towers.Any(tower => Vector2.DistanceSquared(tower.Position, generatorPosition) < 48f * 48f))
            throw new InvalidDataException("Restored Charge Forge placement is outside the selected map's legal defense area.");
    }

    private static string NormalizeRunId(string? runId, string fallback) =>
        !string.IsNullOrWhiteSpace(runId) && runId.Length <= 64
            ? runId
            : fallback;
}
