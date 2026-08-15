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
    private readonly GameContent _content;
    private readonly EnemySystem _enemySystem = new();
    private readonly TowerSystem _towerSystem;
    private readonly BuffSystem _buffSystem = new();
    private readonly TacticalDefenseSystem _tacticalDefenseSystem = new();
    private readonly HashSet<int> _resolvedOutcomes = new();
    private int _nextEnemyId = 1;
    private int _nextTowerId = 1;
    private int _nextEmergencyDefenseId = 1;

    public string RunId { get; private set; } = Guid.NewGuid().ToString("N");
    public GameContent Content => _content;
    public DifficultyDefinition Difficulty { get; }
    public string DifficultyId => Difficulty.Id;
    public ChallengeDefinition Challenge { get; }
    public string ChallengeId => Challenge.Id;
    public bool TacticalSystemsEnabled => Challenge.TacticalSystemsEnabled;
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
    public bool HasTacticalPlacementPreview { get; private set; }
    public PlacementFailure PlacementFailure { get; private set; }
    public float Speed { get; private set; } = 1f;
    public float OverdriveCooldownRemaining { get; private set; }
    public int AutoOverdriveTowerId { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsDefeat { get; private set; }
    public bool IsCoOp { get; private set; }
    public int LocalPlayerId { get; private set; } = 1;
    public string? AnnouncementTitle { get; private set; }
    public string? AnnouncementSubtitle { get; private set; }
    public float AnnouncementRemaining { get; private set; }
    public bool AnnouncementPositive { get; private set; }
    public int CurrentWave => Waves.CurrentWaveNumber;
    public int TotalWaves => Waves.TotalWaves;
    public int NextEnemyId => _nextEnemyId;
    public int NextTowerId => _nextTowerId;
    public int NextEmergencyDefenseId => _nextEmergencyDefenseId;
    public bool IsEndlessMode => Waves.EndlessModeEnabled;
    public bool CanStartWave => Waves.CanStartNextWave;
    public float IntermissionRemaining => Waves.IntermissionRemaining;
    public int EnemiesRemaining => Waves.EstimateRemainingIncludingLive(Enemies.Count(x => !x.IsDead && !x.HasEscaped));
    public bool CanSaveCheckpoint => !Waves.IsActive && Enemies.Count == 0 && !IsVictory && !IsDefeat;

    public event Action<TowerInstance>? TowerPlaced;
    public event Action<TowerInstance, int>? TowerUpgraded;
    public event Action<TowerInstance, int>? TowerSold;
    public event Action<TowerInstance>? TowerOverdriven;
    public event Action<EnemyInstance>? EnemyKilled;
    public event Action<EnemyInstance>? EnemyEscaped;
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
        Economy = new EconomyService(ChallengeCatalog.StartingCredits(mapDefinition, Difficulty, Challenge), Difficulty.StartingLives);
        Waves = new WaveManager(waveSet.Waves);
        DamageResolver = new DamageResolver(this);
        Statistics = new RunStatistics(this);
        _towerSystem = new TowerSystem(TargetSelector);
        EmergencyInventory = TacticalSystemsEnabled ? Math.Max(0, content.Tactics.EmergencyDefense.StartingInventory) : 0;
        if (!Challenge.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase))
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
        var deltaSeconds = MathF.Min(0.1f, MathF.Max(0, unscaledDeltaSeconds)) * Speed;
        Statistics.Advance(deltaSeconds);
        AnnouncementRemaining = MathF.Max(0, AnnouncementRemaining - deltaSeconds);
        OverdriveCooldownRemaining = MathF.Max(0, OverdriveCooldownRemaining - deltaSeconds);
        Waves.UpdateIntermission(deltaSeconds);
        Waves.Update(deltaSeconds, this);
        _enemySystem.Update(deltaSeconds, this);
        _tacticalDefenseSystem.Update(deltaSeconds, this);
        TryActivateAutomaticProtocol();
        _buffSystem.Update(Towers);
        _towerSystem.Update(deltaSeconds, this);
        Projectiles.Update(deltaSeconds, this);
        Effects.Update(deltaSeconds);
        Enemies.RemoveAll(x => x.IsDead || x.HasEscaped);
        Waves.TryComplete(Enemies.Count == 0, this);

        if (Economy.Lives <= 0) IsDefeat = true;
        else if (Waves.IsFinalWaveCleared && !Waves.EndlessModeEnabled && Enemies.Count == 0) IsVictory = true;
    }

    public void HandleWorldInput(InputSnapshot input, Action<GameCommand>? commandSink = null, int playerId = 1)
    {
        PlacementPosition = input.MousePosition;
        PlacementPreviewPosition = PlacementPosition;
        HasTacticalPlacementPreview = TacticalPlacement != TacticalPlacementKind.None;
        HoveredTower = null;
        HoveredGenerator = null;
        if (TacticalPlacement != TacticalPlacementKind.None)
        {
            var tacticalPosition = PlacementPosition;
            if (TacticalPlacement == TacticalPlacementKind.PulsePlate)
            {
                HasTacticalPlacementPreview = TryResolvePulsePlatePlacement(PlacementPosition, out tacticalPosition);
                if (HasTacticalPlacementPreview) PlacementPreviewPosition = tacticalPosition;
                PlacementFailure = ResolvePulsePlatePlacementFailure(PlacementPosition, tacticalPosition, HasTacticalPlacementPreview);
            }
            else
            {
                PlacementFailure = ValidateTacticalPlacement(TacticalPlacement, tacticalPosition);
            }
            if (input.RightPressed || input.EscapePressed)
            {
                CancelPlacement();
                return;
            }
            if (input.LeftPressed && PlacementFailure == PlacementFailure.None && HasTacticalPlacementPreview)
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
            PlacementFailure = ValidatePlacement(PlacementTowerId, PlacementPosition);
            if (input.RightPressed || input.EscapePressed)
            {
                CancelPlacement();
                return;
            }
            if (input.LeftPressed && PlacementFailure == PlacementFailure.None)
            {
                if (commandSink is null) TryPlaceTower(PlacementTowerId, PlacementPosition);
                else commandSink(new GameCommand
                {
                    PlayerId = playerId,
                    Type = GameCommandType.PlaceTower,
                    TowerDefinitionId = PlacementTowerId,
                    X = PlacementPosition.X,
                    Y = PlacementPosition.Y
                });
                CancelPlacement();
            }
            return;
        }

        if (input.MousePosition.X < GameConstants.MapWidth && input.MousePosition.Y >= GameConstants.TopBarHeight)
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
        if (input.MousePosition.X < GameConstants.MapWidth && input.MousePosition.Y >= GameConstants.TopBarHeight)
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
        SelectedTower = null;
        SelectedGenerator = null;
    }

    public void BeginEmergencyPlacement()
    {
        if (!TacticalSystemsEnabled) return;
        PlacementTowerId = null;
        TacticalPlacement = TacticalPlacementKind.PulsePlate;
        PlacementFailure = PlacementFailure.None;
        HasTacticalPlacementPreview = false;
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
        HasTacticalPlacementPreview = false;
        SelectedTower = null;
        SelectedGenerator = null;
    }

    public void CancelPlacement()
    {
        PlacementTowerId = null;
        TacticalPlacement = TacticalPlacementKind.None;
        PlacementFailure = PlacementFailure.None;
        HasTacticalPlacementPreview = false;
    }

    public void ConfigureCoOp(int localPlayerId)
    {
        if (localPlayerId is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
        IsCoOp = true;
        LocalPlayerId = localPlayerId;
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
        if (!IsTowerAvailable(towerId)) return PlacementFailure.TowerUnavailable;
        if (!Economy.CanAfford(definition.PurchaseCost)) return PlacementFailure.InsufficientCredits;
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
            if (EmergencyDefenses.Count >= definition.MaximumActive) return PlacementFailure.DefenseCapacityReached;
            if (EmergencyInventory <= 0 && !CanDirectPurchaseEmergencyDefense) return PlacementFailure.NoDefenseAvailable;
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
        var radius = generator.Visual.Radius;
        if (position.X < radius || position.X > GameConstants.MapWidth - radius ||
            position.Y < GameConstants.TopBarHeight + radius || position.Y > GameConstants.LogicalHeight - radius)
            return PlacementFailure.TooCloseToEdge;
        if (!Map.IsBuildable(position)) return PlacementFailure.OutsideBuildableRegion;
        if (Map.Path.DistanceToPath(position) < GameConstants.PlacementPathClearance) return PlacementFailure.BlocksPath;
        if (Towers.Any(x => Vector2.DistanceSquared(x.Position, position) < 48f * 48f)) return PlacementFailure.OverlapsTower;
        return PlacementFailure.None;
    }

    /// <summary>
    /// Resolves a cursor near the road to the closest legal pulse-plate slot. This keeps
    /// the visible preview, local deployment, and network command on the same position.
    /// Endpoint clearance and nearby plates are handled as snap constraints rather than
    /// asking the player to estimate an invisible boundary.
    /// </summary>
    public bool TryResolvePulsePlatePlacement(Vector2 cursorPosition, out Vector2 placementPosition)
    {
        var definition = _content.Tactics.EmergencyDefense;
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
        var projection = Map.Path.Project(cursorPosition);
        if (projection.DistanceToPath > Map.Definition.PathWidth * 0.5f + definition.PlacementRoadTolerance)
            return PlacementFailure.MustBeOnPath;
        if (!hasPlacement) return PlacementFailure.OverlapsDefense;
        return ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, placementPosition);
    }

    public bool TryDeployEmergencyDefense(Vector2 position, int ownerPlayerId = 1)
    {
        if (ownerPlayerId is < 1 or > 2) return false;
        if (ValidateTacticalPlacement(TacticalPlacementKind.PulsePlate, position) != PlacementFailure.None) return false;
        var definition = _content.Tactics.EmergencyDefense;
        var purchased = EmergencyInventory <= 0;
        if (purchased)
        {
            if (!Waves.IsActive || !Economy.TrySpend(CurrentEmergencyDirectPurchaseCost)) return false;
            EmergencyDirectPurchasesThisWave++;
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
        if (requestingPlayerId is < 1 or > 2 || tower is null || !tower.CanUpgrade) return false;
        var cost = tower.UpgradeCost;
        if (!Economy.TrySpend(cost) || !tower.TryUpgrade()) return false;
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
        if (requestingPlayerId is < 1 or > 2 || tower is null) return false;
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
        if (requestingPlayerId is < 1 or > 2 || tower is null || tower.IsSupport) return false;
        tower.TargetMode = mode;
        return true;
    }

    public bool TryOverdriveTower(int towerId, int requestingPlayerId = 1)
    {
        var tower = Towers.FirstOrDefault(x => x.Id == towerId);
        if (requestingPlayerId is < 1 or > 2 || tower is null || tower.IsOverdriven || OverdriveCooldownRemaining > 0)
            return false;
        tower.ActivateOverdrive();
        OverdriveCooldownRemaining = tower.Protocol.CooldownSeconds;
        ApplyProtocolBurst(tower);
        Effects.AddFlash(tower.Position, tower.Definition.Visual.PrimaryColor, 0.42f,
            MathF.Max(44, tower.Protocol.BurstRadius));
        TowerOverdriven?.Invoke(tower);
        return true;
    }

    public bool TryToggleAutoProtocol(int towerId, int requestingPlayerId = 1)
    {
        if (requestingPlayerId is < 1 or > 2) return false;
        if (AutoOverdriveTowerId == towerId)
        {
            AutoOverdriveTowerId = 0;
            return true;
        }
        if (Towers.All(x => x.Id != towerId)) return false;
        AutoOverdriveTowerId = towerId;
        return true;
    }

    private void TryActivateAutomaticProtocol()
    {
        if (AutoOverdriveTowerId <= 0 || OverdriveCooldownRemaining > 0 || Enemies.Count == 0) return;
        var tower = Towers.FirstOrDefault(x => x.Id == AutoOverdriveTowerId);
        if (tower is null)
        {
            AutoOverdriveTowerId = 0;
            return;
        }

        var targets = GetProtocolTargets(tower).ToArray();
        if (targets.Length < tower.Protocol.AutoTriggerCount && !targets.Any(x => x.IsElite || x.IsBoss)) return;
        TryOverdriveTower(tower.Id);
    }

    public IReadOnlyList<EnemyInstance> GetProtocolTargets(TowerInstance tower)
    {
        if (!tower.IsSupport)
        {
            var range = GetEffectiveRange(tower);
            return Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped &&
                Vector2.DistanceSquared(tower.Position, enemy.Position) <= range * range).ToArray();
        }

        var auraRange = GetEffectiveAuraRange(tower);
        var recipients = Towers.Where(recipient => !recipient.IsSupport &&
            Vector2.DistanceSquared(tower.Position, recipient.Position) <= auraRange * auraRange).ToArray();
        return Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped && recipients.Any(recipient =>
        {
            var range = GetEffectiveRange(recipient);
            return Vector2.DistanceSquared(recipient.Position, enemy.Position) <= range * range;
        })).ToArray();
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
        SelectedTower?.CycleTargetMode();
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
        if (requestingPlayerId is < 1 or > 2 || Generator is not { } generator) return false;
        var value = generator.SellValue;
        Economy.RecoverSale(value);
        Generator = null;
        SelectedGenerator = null;
        HoveredGenerator = null;
        GeneratorSold?.Invoke(generator, value);
        return true;
    }

    public bool StartNextWave(bool? earlyStartEligible = null) => Waves.TryStartNextWave(this, earlyStartEligible);
    public void SetSpeed(float speed) => Speed = speed >= 1.5f ? 2f : 1f;

    public bool BeginEndlessMode()
    {
        if (!IsVictory || IsDefeat || !Waves.EnableEndlessMode()) return false;
        IsVictory = false;
        AnnouncementTitle = "ENDLESS DEFENSE ONLINE";
        AnnouncementSubtitle = $"Wave {CurrentWave + 1} is forming. Enemy pressure will keep scaling.";
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

    public float GetEffectiveAuraRange(TowerInstance tower) => tower.Level.AuraRange *
        (1f + (tower.IsOverdriven ? tower.Protocol.AuraRangeBonus : 0f));

    public float GetEffectiveAttacksPerSecond(TowerInstance tower)
    {
        var support = _buffSystem.Get(tower);
        var power = Map.GetPowerBuff(tower.Position);
        var overdrive = tower.IsOverdriven ? tower.Protocol.AttackSpeedBonus : 0f;
        return tower.Level.AttacksPerSecond * (1f + support.AttackSpeedBonus + power.AttackSpeedBonus + overdrive);
    }

    public float GetEffectiveDamage(TowerInstance tower, float baseDamage)
    {
        var protocol = tower.IsOverdriven ? tower.Protocol.DamageBonus : 0f;
        return baseDamage * (1f + Map.GetPowerBuff(tower.Position).DamageBonus + protocol);
    }

    public float GetEffectiveArmorPierce(TowerInstance tower, float baseArmorPierce)
    {
        var protocol = tower.IsOverdriven ? tower.Protocol.ArmorPierceBonus : 0f;
        return baseArmorPierce + Map.GetPowerBuff(tower.Position).ArmorPierceBonus + protocol;
    }

    public void SpawnEnemy(string enemyId, float healthMultiplier, float speedMultiplier, string rank = "Standard")
    {
        if (!_content.Enemies.TryGetValue(enemyId, out var definition)) return;
        var enemy = new EnemyInstance(_nextEnemyId++, definition, Map.Path,
            healthMultiplier * Difficulty.EnemyHealthMultiplier,
            speedMultiplier * Difficulty.EnemySpeedMultiplier,
            rank);
        Enemies.Add(enemy);
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
        AnnouncementSubtitle = earlyCallBonus > 0 ? $"EARLY CALL +{earlyCallBonus} // {wave.Briefing}" : wave.Briefing;
        AnnouncementPositive = false;
        AnnouncementRemaining = 2.4f;
        WaveStarted?.Invoke(wave);
    }

    public void OnWaveCompleted(int waveNumber)
    {
        AnnouncementTitle = $"WAVE {waveNumber} CLEARED";
        AnnouncementSubtitle = $"+{40 + 10 * waveNumber} completion credits";
        AnnouncementPositive = true;
        AnnouncementRemaining = 2.2f;
        WaveCompleted?.Invoke(waveNumber);
    }

    public void OnEnemyKilled(EnemyInstance enemy)
    {
        if (!_resolvedOutcomes.Add(enemy.Id)) return;
        Economy.AwardKill(enemy.Reward);
        Effects.AddFlash(enemy.Position, enemy.Definition.Visual.AccentColor, 0.16f, enemy.Radius + 8);
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
        if (data.SchemaVersion != CoOpStateSnapshot.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported co-op state schema {data.SchemaVersion}.");
        if (localPlayerId is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
        var knownMap = content.Maps.ContainsKey(data.MapId) || content.Map.Id.Equals(data.MapId, StringComparison.OrdinalIgnoreCase);
        if (!knownMap) throw new InvalidDataException($"Network map '{data.MapId}' is not available.");

        var session = new GameSession(content, data.MapId, data.DifficultyId, data.ChallengeId);
        session.RunId = NormalizeRunId(data.RunId, session.RunId);
        session.ConfigureCoOp(localPlayerId);
        session.Economy.RestoreSaveData(data.Economy);
        session.Waves.RestoreCoOpState(data.Waves);
        session.Speed = data.Speed >= 1.5f ? 2f : 1f;
        session.OverdriveCooldownRemaining = MathF.Max(0, data.OverdriveCooldownRemaining);
        session.AutoOverdriveTowerId = data.Towers.Any(tower => tower.Id == data.AutoOverdriveTowerId)
            ? data.AutoOverdriveTowerId
            : 0;
        session.EmergencyInventory = Math.Max(0, data.EmergencyInventory);
        session.EmergencyDirectPurchasesThisWave = Math.Max(0, data.EmergencyDirectPurchasesThisWave);

        foreach (var savedTower in data.Towers)
        {
            if (!content.Towers.TryGetValue(savedTower.DefinitionId, out var definition))
                throw new InvalidDataException($"Network tower '{savedTower.DefinitionId}' is not available.");
            session.Towers.Add(TowerInstance.RestoreCoOpState(savedTower, definition));
        }

        foreach (var savedEnemy in data.Enemies)
        {
            if (!content.Enemies.TryGetValue(savedEnemy.DefinitionId, out var definition))
                throw new InvalidDataException($"Network enemy '{savedEnemy.DefinitionId}' is not available.");
            session.Enemies.Add(EnemyInstance.RestoreCoOpState(savedEnemy, definition, session.Map.Path));
        }

        foreach (var savedPlate in data.PulsePlates.Where(plate => plate.ChargesRemaining > 0))
            session.EmergencyDefenses.Add(PulsePlateInstance.RestoreSaveData(savedPlate, content.Tactics.EmergencyDefense));
        if (data.Generator is not null)
            session.Generator = ChargeForgeInstance.RestoreSaveData(data.Generator, content.Tactics.Generator);

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
        session.RunId = NormalizeRunId(data.RunId, session.RunId);
        if (data.IsCoOp) session.ConfigureCoOp(1);
        session.Economy.RestoreSaveData(data.Economy);
        session.Waves.RestoreSaveData(data.Waves);
        session.Speed = data.Speed >= 1.5f ? 2f : 1f;
        session.OverdriveCooldownRemaining = MathF.Max(0, data.OverdriveCooldownRemaining);
        session.AutoOverdriveTowerId = data.Towers.Any(tower => tower.Id == data.AutoOverdriveTowerId)
            ? data.AutoOverdriveTowerId
            : 0;
        session.EmergencyInventory = Math.Max(0, data.EmergencyInventory);
        session.EmergencyDirectPurchasesThisWave = Math.Max(0, data.EmergencyDirectPurchasesThisWave);

        foreach (var savedTower in data.Towers)
        {
            if (!content.Towers.TryGetValue(savedTower.DefinitionId, out var definition))
                throw new InvalidDataException($"Saved tower '{savedTower.DefinitionId}' is not available.");
            session.Towers.Add(TowerInstance.RestoreSaveData(savedTower, definition));
        }
        foreach (var savedPlate in data.PulsePlates.Where(x => x.ChargesRemaining > 0))
            session.EmergencyDefenses.Add(PulsePlateInstance.RestoreSaveData(savedPlate, content.Tactics.EmergencyDefense));
        if (data.Generator is not null)
            session.Generator = ChargeForgeInstance.RestoreSaveData(data.Generator, content.Tactics.Generator);

        session._nextEnemyId = Math.Max(data.NextEnemyId, 1);
        session._nextTowerId = Math.Max(data.NextTowerId, session.Towers.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
        session._nextEmergencyDefenseId = Math.Max(data.NextEmergencyDefenseId, session.EmergencyDefenses.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
        session.Statistics.RestoreSaveData(data.Statistics, session.Towers);
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

    private static string NormalizeRunId(string? runId, string fallback) =>
        !string.IsNullOrWhiteSpace(runId) && runId.Length <= 64
            ? runId
            : fallback;
}
