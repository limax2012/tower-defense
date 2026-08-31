using MinimalBastion.Combat;
using MinimalBastion.Enemies;
using MinimalBastion.Core;
using MinimalBastion.Persistence;
using MinimalBastion.Tactics;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Simulation;

public static class HeadlessSimulation
{
    public static SimulationRunResult Run(Data.GameContent content, SimulationOptions options) =>
        Run(CreateSession(content, options), options);

    internal static (GameSession Session, SimulationRunResult Result) RunForDiagnostics(
        Data.GameContent content, SimulationOptions options)
    {
        var session = CreateSession(content, options);
        return (session, Run(session, options));
    }

    public static StrategyReplayResult ReplayStrategy(
        Data.GameContent content,
        StrategyReplayEnvelope envelope) => ReplayStrategyForDiagnostics(content, envelope).Result;

    internal static (GameSession Session, StrategyReplayResult Result) ReplayStrategyForDiagnostics(
        Data.GameContent content,
        StrategyPlan strategyPlan,
        SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(strategyPlan);
        ArgumentNullException.ThrowIfNull(options);
        strategyPlan.Validate();
        ValidateReplaySelectors(strategyPlan, options);
        var envelope = StrategyReplayEnvelope.Create(
            content,
            strategyPlan,
            CampaignSimulationSettings.From(options));
        var expectedWaveCount = content.Maps.TryGetValue(strategyPlan.MapId, out var map) &&
                                content.WaveSets.TryGetValue(map.WaveSet, out var waveSet)
            ? waveSet.Waves.Count
            : content.Waves.Waves.Count;
        strategyPlan.ValidateCompleteCampaign(expectedWaveCount);
        return ReplayValidatedStrategy(content, envelope, expectedWaveCount);
    }

    internal static (GameSession Session, StrategyReplayResult Result) ReplayStrategyForDiagnostics(
        Data.GameContent content,
        StrategyReplayEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(envelope);
        var expectedWaveCount = StrategyReplayValidation.ValidateEnvelope(envelope, content);
        return ReplayValidatedStrategy(content, envelope, expectedWaveCount);
    }

    private static (GameSession Session, StrategyReplayResult Result) ReplayValidatedStrategy(
        Data.GameContent content,
        StrategyReplayEnvelope envelope,
        int expectedWaveCount)
    {
        var strategyPlan = envelope.Plan;
        var options = CreateReplayOptions(envelope);

        var session = ConfigureSession(new GameSession(
            content,
            strategyPlan.MapId,
            strategyPlan.DifficultyId,
            strategyPlan.ChallengeId), options);
        ValidateReplaySession(strategyPlan, session);
        if (session.TotalWaves != expectedWaveCount)
            throw new InvalidOperationException("The fresh replay session resolved a different campaign wave set.");

        var waveRuns = new List<StrategyReplayWaveResult>(session.TotalWaves);
        foreach (var wavePlan in strategyPlan.Waves)
        {
            if (session.CurrentWave != wavePlan.Wave - 1 || !session.CanStartWave || session.IsVictory || session.IsDefeat)
                throw new InvalidOperationException(
                    $"The fresh replay session is not ready to execute planned wave {wavePlan.Wave}.");

            var baseline = ReplayWaveBaseline.Capture(session);
            var waveOptions = StrategySimulationOptions.ForWave(options, wavePlan, strategyPlan.DefaultStrategy);
            var simulation = Run(session, waveOptions);
            var succeeded = !session.IsDefeat && !session.Waves.IsActive && session.Enemies.Count == 0 &&
                            session.CurrentWave == wavePlan.Wave && simulation.Result is "WaveLimit" or "Victory";
            waveRuns.Add(new StrategyReplayWaveResult
            {
                WavePlan = wavePlan,
                Simulation = simulation,
                Deltas = BuildReplayWaveDeltas(session, simulation, baseline),
                Succeeded = succeeded
            });
            if (!succeeded) break;
        }

        var finalRun = waveRuns[^1];
        var campaignCleared = finalRun.Succeeded && waveRuns.Count == session.TotalWaves &&
                              session.CurrentWave == session.TotalWaves && session.IsVictory &&
                              finalRun.Simulation.CampaignCleared;
        return (session, new StrategyReplayResult
        {
            EnvelopeSchemaVersion = envelope.SchemaVersion,
            StrategyArtifactId = strategyPlan.ArtifactId,
            StrategyFingerprint = envelope.ExpectedPlanFingerprint.ToLowerInvariant(),
            ContentBuildFingerprint = envelope.ContentBuildFingerprint.ToLowerInvariant(),
            ReplayFingerprint = envelope.ReplayFingerprint.ToLowerInvariant(),
            SimulationSettings = envelope.SimulationSettings,
            MapId = session.Map.Definition.Id,
            DifficultyId = session.DifficultyId,
            ChallengeId = session.ChallengeId,
            BaseSeed = strategyPlan.BaseSeed,
            Result = finalRun.Simulation.Result,
            StartingWave = 0,
            WaveReached = session.CurrentWave,
            CompletedWaveCount = waveRuns.Count(run => run.Succeeded),
            CampaignCleared = campaignCleared,
            FailedWave = finalRun.Succeeded ? null : finalRun.WavePlan.Wave,
            WaveRuns = waveRuns
        });
    }

    public static SimulationRunResult Run(Data.GameContent content, SaveGameData save, SimulationOptions options) =>
        Run(ConfigureSession(GameSession.RestoreSaveGame(content, save), options), options);

    public static CheckpointWaveRunResult RunWave(
        Data.GameContent content,
        SaveGameData checkpoint,
        SimulationOptions options,
        WavePlan wavePlan) => RunWave(content, checkpoint, options, wavePlan, null);

    public static CheckpointWaveRunResult RunWave(
        Data.GameContent content,
        SaveGameData checkpoint,
        SimulationOptions options,
        StrategyPlan strategyPlan,
        WavePlan wavePlan)
    {
        strategyPlan.ValidateForCheckpoint(checkpoint);
        if (strategyPlan.FindWave(wavePlan.Wave) is { } persistedPlan &&
            !persistedPlan.StableKey.Equals(wavePlan.StableKey, StringComparison.Ordinal))
            throw new InvalidDataException($"Strategy artifact contains a different decision for wave {wavePlan.Wave}.");
        return RunWave(content, checkpoint, options, wavePlan, strategyPlan.DefaultStrategy);
    }

    private static CheckpointWaveRunResult RunWave(
        Data.GameContent content,
        SaveGameData checkpoint,
        SimulationOptions options,
        WavePlan wavePlan,
        AutoPlayerStrategy? defaultStrategy)
    {
        wavePlan.Validate();
        var session = ConfigureSession(GameSession.RestoreSaveGame(content, checkpoint), options);
        if (!session.CanSaveCheckpoint)
            throw new InvalidDataException("Wave optimization requires an inter-wave campaign checkpoint.");
        var expectedWave = session.CurrentWave + 1;
        if (wavePlan.Wave != expectedWave)
            throw new InvalidDataException($"Checkpoint is ready for wave {expectedWave}, not planned wave {wavePlan.Wave}.");

        var waveOptions = StrategySimulationOptions.ForWave(options, wavePlan, defaultStrategy);
        var result = Run(session, waveOptions);
        var succeeded = !session.IsDefeat && !session.Waves.IsActive && session.Enemies.Count == 0 &&
                        session.CurrentWave == wavePlan.Wave && result.Result is "WaveLimit" or "Victory";
        var nextCheckpoint = succeeded && session.CanSaveCheckpoint ? session.CaptureSaveGame() : null;
        return new CheckpointWaveRunResult
        {
            WavePlan = wavePlan,
            Simulation = result,
            Succeeded = succeeded,
            CampaignCompleted = succeeded && session.IsVictory,
            NextCheckpoint = nextCheckpoint,
            NextCheckpointFingerprint = nextCheckpoint is null ? null : StrategyArtifactStore.Fingerprint(nextCheckpoint)
        };
    }

    private static GameSession CreateSession(Data.GameContent content, SimulationOptions options) =>
        ConfigureSession(new GameSession(content, options.MapId, options.DifficultyId, options.ChallengeId), options);

    private static GameSession ConfigureSession(GameSession session, SimulationOptions options)
    {
        session.ConfigureCounterPressureSimulation(options.UseCounterSupport, options.UseCounterAttackers);
        session.ConfigureApexProtocolSimulation(options.UseProtocols);
        return session;
    }

    private static SimulationOptions CreateReplayOptions(StrategyReplayEnvelope envelope)
    {
        var plan = envelope.Plan;
        var settings = envelope.SimulationSettings;
        return new SimulationOptions
        {
            Seed = plan.BaseSeed,
            Strategy = plan.DefaultStrategy,
            MapId = plan.MapId,
            DifficultyId = plan.DifficultyId,
            ChallengeId = plan.ChallengeId,
            StepSeconds = settings.StepSeconds,
            MaximumSimulatedSeconds = settings.MaximumSimulatedSeconds,
            MaximumWave = int.MaxValue,
            ContinueEndless = false,
            ForcedTowerId = settings.ForcedTowerId,
            ForcedDoctrineId = settings.ForcedDoctrineId,
            ForcedSpecializationId = settings.ForcedSpecializationId,
            UseProtocols = settings.UseProtocols,
            UseApexUpgrades = settings.UseApexUpgrades,
            UseCounterSupport = settings.UseCounterSupport,
            UseCounterAttackers = settings.UseCounterAttackers,
            HoldBuild = settings.HoldBuild,
            HoldFootprint = settings.HoldFootprint
        };
    }

    private static StrategyReplayWaveDeltas BuildReplayWaveDeltas(
        GameSession session,
        SimulationRunResult simulation,
        ReplayWaveBaseline baseline) => new()
    {
        StartingCredits = baseline.Credits,
        CreditsEarned = session.Economy.TotalCreditsEarned - baseline.CreditsEarned,
        CreditsSpent = session.Economy.TotalCreditsSpent - baseline.CreditsSpent,
        SaleCreditsRecovered = session.Economy.SaleCreditsRecovered - baseline.SaleCreditsRecovered,
        EarlyStartCreditsEarned = session.Economy.EarlyStartCreditsEarned - baseline.EarlyStartCreditsEarned,
        EndingCredits = session.Economy.Credits,
        UnspentCreditChange = session.Economy.Credits - baseline.Credits,
        StartingLives = baseline.Lives,
        EndingLives = session.Economy.Lives,
        Kills = session.Economy.TotalKills - baseline.Kills,
        Leaks = session.Economy.EscapedEnemies - baseline.Leaks,
        TowerPurchases = simulation.Towers.Values.Sum(metrics => metrics.Purchases),
        TowerUpgrades = simulation.Towers.Values.Sum(metrics => metrics.Upgrades),
        ApexUpgrades = simulation.Towers.Values.Sum(metrics => metrics.ApexUpgrades),
        TowerSales = simulation.Towers.Values.Sum(metrics => metrics.Sales),
        PulsePlateDeployments = simulation.PulsePlateDeployments.Count,
        EmergencyDeployments = simulation.EmergencyDeployments,
        EmergencyDirectPurchases = simulation.EmergencyDirectPurchases,
        EmergencyTriggers = simulation.EmergencyTriggers,
        EmergencyHits = simulation.EmergencyHits,
        EmergencyKills = simulation.EmergencyKills,
        EmergencyDamage = simulation.EmergencyDamage,
        GeneratorPurchases = simulation.GeneratorPurchases,
        GeneratorUpgrades = simulation.GeneratorUpgrades,
        GeneratedCharges = simulation.GeneratedCharges,
        Overdrives = simulation.Overdrives,
        ProtocolActivations = simulation.ProtocolActivations.Count
    };

    private static void ValidateReplaySelectors(StrategyPlan strategyPlan, SimulationOptions options)
    {
        if (options.MapId is { } mapId &&
            !mapId.Equals(strategyPlan.MapId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The replay map selector does not match the strategy artifact.");
        if (!string.Equals(options.DifficultyId, strategyPlan.DifficultyId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The replay difficulty selector does not match the strategy artifact.");
        if (!string.Equals(options.ChallengeId, strategyPlan.ChallengeId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The replay challenge selector does not match the strategy artifact.");
        if (options.Seed != strategyPlan.BaseSeed)
            throw new InvalidDataException("The replay seed selector does not match the strategy artifact.");
        if (options.WavePlan is not null)
            throw new InvalidDataException("A complete strategy replay cannot also specify a single-wave plan.");
        if (options.ContinueEndless)
            throw new InvalidDataException("A complete campaign strategy replay cannot continue into endless mode.");
        if (options.MaximumWave < strategyPlan.Waves.Count)
            throw new InvalidDataException("A complete campaign strategy replay cannot use a partial wave limit.");
        if (!float.IsFinite(options.StepSeconds) || options.StepSeconds is < 0.01f or > 0.1f)
            throw new InvalidDataException("The replay simulation step must be within 0.01 through 0.1 seconds.");
        if (!float.IsFinite(options.MaximumSimulatedSeconds) || options.MaximumSimulatedSeconds <= 0)
            throw new InvalidDataException("The replay time limit must be finite and positive.");
    }

    private static void ValidateReplaySession(StrategyPlan strategyPlan, GameSession session)
    {
        if (!session.Map.Definition.Id.Equals(strategyPlan.MapId, StringComparison.OrdinalIgnoreCase) ||
            !session.DifficultyId.Equals(strategyPlan.DifficultyId, StringComparison.OrdinalIgnoreCase) ||
            !session.ChallengeId.Equals(strategyPlan.ChallengeId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The strategy artifact references an unavailable run configuration.");
        if (session.CurrentWave != 0 || session.Waves.IsActive || session.Enemies.Count != 0 ||
            session.IsVictory || session.IsDefeat)
            throw new InvalidOperationException("A strategy replay must begin from a fresh campaign session.");
    }

    private readonly record struct ReplayWaveBaseline(
        int Credits,
        int CreditsEarned,
        int CreditsSpent,
        int SaleCreditsRecovered,
        int EarlyStartCreditsEarned,
        int Lives,
        int Kills,
        int Leaks)
    {
        public static ReplayWaveBaseline Capture(GameSession session) => new(
            session.Economy.Credits,
            session.Economy.TotalCreditsEarned,
            session.Economy.TotalCreditsSpent,
            session.Economy.SaleCreditsRecovered,
            session.Economy.EarlyStartCreditsEarned,
            session.Economy.Lives,
            session.Economy.TotalKills,
            session.Economy.EscapedEnemies);
    }

    private static SimulationRunResult Run(GameSession session, SimulationOptions options)
    {
        if (!float.IsFinite(options.StepSeconds) || options.StepSeconds is < 0.01f or > 0.1f)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Simulation step seconds must be within 0.01 through 0.1.");
        if (!float.IsFinite(options.MaximumSimulatedSeconds) || options.MaximumSimulatedSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Maximum simulated seconds must be finite and positive.");
        var player = new AutoPlayer(session, options.Strategy, options.Seed, options);
        using var telemetry = new RunTelemetry(session);
        var step = options.StepSeconds;
        var elapsed = 0f;
        var reactionTimer = 0f;
        var wasWaveActive = false;

        while (!session.IsDefeat && elapsed < options.MaximumSimulatedSeconds)
        {
            telemetry.SetElapsed(elapsed);
            if (session.IsVictory)
            {
                if (!options.ContinueEndless || options.MaximumWave <= session.TotalWaves || !session.BeginEndlessMode()) break;
            }

            if (session.CanStartWave && session.CurrentWave < options.MaximumWave)
            {
                telemetry.BeginWave(session, elapsed);
                player.PrepareForWave(session);
                if (!session.StartNextWave()) telemetry.CancelWave();
            }

            if (!session.Waves.IsActive && session.CurrentWave >= options.MaximumWave && session.Enemies.Count == 0)
                break;

            wasWaveActive = session.Waves.IsActive;
            telemetry.SampleUtility(session, step * session.Speed);
            telemetry.BeginUpdate(elapsed + step);
            session.Update(step);
            elapsed += step;
            telemetry.SetElapsed(elapsed);
            telemetry.CompleteUpdate(session);
            reactionTimer += step;

            if (!session.IsDefeat && session.Waves.IsActive && reactionTimer >= 1f)
            {
                player.ReactDuringWave(session);
                reactionTimer = 0f;
            }

            if (wasWaveActive && !session.Waves.IsActive) telemetry.EndWave(session, elapsed);
        }

        if (session.Waves.IsActive) telemetry.EndWave(session, elapsed);
        var result = session.IsVictory ? "Victory" : session.IsDefeat ? "Defeat" : session.CurrentWave >= options.MaximumWave ? "WaveLimit" : "Timeout";
        return telemetry.Build(session, options, elapsed, result);
    }

    private sealed class RunTelemetry : IDisposable
    {
        private readonly GameSession _session;
        private readonly Dictionary<string, TowerRunMetrics> _towers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _towerIdToDefinition = new();
        private readonly Dictionary<int, TowerInstance> _towerInstances = new();
        private readonly Dictionary<int, int> _towerLevels = new();
        private readonly Dictionary<string, int> _enemyKills = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _enemyLeaks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<WaveRunMetrics> _waves = new();
        private readonly List<SimulationEscapedEnemy> _escapedThisUpdate = new();
        private IReadOnlyList<SimulationEscapedEnemy> _fatalFrameEscapedEnemies = Array.Empty<SimulationEscapedEnemy>();
        private readonly List<PlateDeploymentAccumulator> _pulsePlateDeployments = new();
        private readonly Dictionary<int, PlateDeploymentAccumulator> _pulsePlateById = new();
        private readonly List<SimulationProtocolActivation> _protocolActivations = new();
        private WaveSnapshot? _activeWave;
        private WaveSnapshot? _lastWave;
        private float _elapsed;
        private bool _insideSessionUpdate;
        private int _emergencyDeployments;
        private int _emergencyDirectPurchases;
        private int _emergencyTriggers;
        private int _emergencyHits;
        private int _emergencyKills;
        private float _emergencyDamage;
        private int _generatorPurchases;
        private int _generatorUpgrades;
        private int _generatedCharges;
        private int _overdrives;
        private bool _disposed;

        public RunTelemetry(GameSession session)
        {
            _session = session;
            session.TowerPlaced += OnTowerPlaced;
            session.TowerUpgraded += OnTowerUpgraded;
            session.TowerOverdriven += OnTowerOverdriven;
            session.TowerSold += OnTowerSold;
            session.EnemyKilled += OnEnemyKilled;
            session.EnemyEscaped += OnEnemyEscaped;
            session.DamageResolver.DamageApplied += OnDamage;
            session.EmergencyDefenseDeployed += OnEmergencyDefenseDeployed;
            session.EmergencyDefenseTriggered += OnEmergencyDefenseTriggered;
            session.GeneratorPlaced += OnGeneratorPlaced;
            session.GeneratorUpgraded += OnGeneratorUpgraded;
            session.EmergencyChargeProduced += OnEmergencyChargeProduced;
            foreach (var tower in session.Towers) TrackExistingTower(tower);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session.TowerPlaced -= OnTowerPlaced;
            _session.TowerUpgraded -= OnTowerUpgraded;
            _session.TowerOverdriven -= OnTowerOverdriven;
            _session.TowerSold -= OnTowerSold;
            _session.EnemyKilled -= OnEnemyKilled;
            _session.EnemyEscaped -= OnEnemyEscaped;
            _session.DamageResolver.DamageApplied -= OnDamage;
            _session.EmergencyDefenseDeployed -= OnEmergencyDefenseDeployed;
            _session.EmergencyDefenseTriggered -= OnEmergencyDefenseTriggered;
            _session.GeneratorPlaced -= OnGeneratorPlaced;
            _session.GeneratorUpgraded -= OnGeneratorUpgraded;
            _session.EmergencyChargeProduced -= OnEmergencyChargeProduced;
        }

        public void SetElapsed(float elapsed) => _elapsed = MathF.Max(0, elapsed);

        public void BeginUpdate(float elapsed)
        {
            _elapsed = MathF.Max(0, elapsed);
            _insideSessionUpdate = true;
            _escapedThisUpdate.Clear();
        }

        public void CompleteUpdate(GameSession session)
        {
            if (session.IsDefeat && _escapedThisUpdate.Count > 0)
                _fatalFrameEscapedEnemies = _escapedThisUpdate.ToArray();
            _insideSessionUpdate = false;
        }

        public void BeginWave(GameSession session, float elapsed)
        {
            var wave = session.Waves.NextWave ?? session.Waves.ActiveWave;
            var pressure = wave is not null
                ? WavePressureAnalysis.Analyze(
                    wave,
                    session.Content.Enemies,
                    session.Difficulty.EnemyHealthMultiplier,
                    session.Difficulty.EnemySpeedMultiplier)
                : null;
            _activeWave = new WaveSnapshot(
                wave?.Number ?? session.CurrentWave + 1,
                elapsed,
                session.Economy.Lives,
                session.Economy.TotalKills,
                session.Economy.EscapedEnemies,
                session.Economy.TotalCreditsSpent,
                DescribeWave(wave, session.Content.Enemies),
                pressure?.EnemyCount ?? 0,
                pressure?.ArmorAdjustedDemand ?? 0);
        }

        public void CancelWave() => _activeWave = null;

        public void EndWave(GameSession session, float elapsed)
        {
            if (_activeWave is not { } start) return;
            _waves.Add(new WaveRunMetrics
            {
                Wave = start.Wave,
                Archetype = start.Archetype,
                DurationSeconds = elapsed - start.StartedAt,
                StartingLives = start.Lives,
                EndingLives = session.Economy.Lives,
                Kills = session.Economy.TotalKills - start.Kills,
                Leaks = session.Economy.EscapedEnemies - start.Leaks,
                CreditsSpent = session.Economy.TotalCreditsSpent - start.CreditsSpent,
                EndingCredits = session.Economy.Credits
            });
            _lastWave = start;
            _activeWave = null;
        }

        public SimulationRunResult Build(GameSession session, SimulationOptions options, float elapsed, string result)
        {
            var remainingEnemies = CaptureRemainingEnemies(session);
            var queuedEnemies = CaptureQueuedEnemies(session);
            return new SimulationRunResult
            {
                MapId = session.Map.Definition.Id,
                DifficultyId = session.DifficultyId,
                ChallengeId = session.ChallengeId,
                Strategy = options.Strategy,
                Seed = options.Seed,
                ForcedTowerId = options.ForcedTowerId,
                ForcedDoctrineId = options.ForcedDoctrineId,
                ForcedSpecializationId = options.ForcedSpecializationId,
                Result = result,
                WaveReached = session.CurrentWave,
                CampaignWaveCount = session.TotalWaves,
                CampaignCleared = session.Waves.IsFinalWaveCleared,
                LivesRemaining = session.Economy.Lives,
                Kills = session.Economy.TotalKills,
                EscapedEnemies = session.Economy.EscapedEnemies,
                CreditsEarned = session.Economy.TotalCreditsEarned,
                CreditsSpent = session.Economy.TotalCreditsSpent,
                CreditsUnspent = session.Economy.Credits,
                SaleCreditsRecovered = session.Economy.SaleCreditsRecovered,
                EarlyStartCreditsEarned = session.Economy.EarlyStartCreditsEarned,
                SimulatedSeconds = elapsed,
                Towers = _towers.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                EnemyKills = _enemyKills.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                EnemyLeaks = _enemyLeaks.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                RemainingEnemies = remainingEnemies,
                QueuedEnemies = queuedEnemies,
                QueuedEnemiesRemaining = queuedEnemies.Sum(enemy => enemy.Count),
                FatalEscapedEnemies = _fatalFrameEscapedEnemies,
                FailureMargin = result == "Defeat"
                    ? CaptureFailureMargin(session, remainingEnemies, queuedEnemies)
                    : null,
                Waves = _waves.ToArray(),
                PulsePlateDeployments = _pulsePlateDeployments
                    .OrderBy(deployment => deployment.Wave)
                    .ThenBy(deployment => deployment.ElapsedSeconds)
                    .ThenBy(deployment => deployment.PlateId)
                    .Select(deployment => deployment.Build())
                    .ToArray(),
                ProtocolActivations = _protocolActivations.ToArray(),
                FinalTowers = session.Towers.OrderBy(tower => tower.Id).Select(tower => new SimulationTowerPlacement(
                    tower.Id,
                    tower.Definition.Id,
                    tower.Position.X,
                    tower.Position.Y,
                    tower.LevelIndex + 1,
                    tower.DoctrineId,
                    tower.SpecializationId,
                    tower.IsApex,
                    tower.TargetMode,
                    tower.InvestedCredits,
                    tower.LifetimeDamage,
                    tower.LifetimeKills,
                    tower.LifetimeSupportDamageEquivalent,
                    tower.LifetimeExposeDamageEquivalent,
                    tower.LifetimeArmorBreakDamageEquivalent,
                    tower.LifetimeControlSeconds,
                    tower.LifetimeExposeSeconds,
                    tower.LifetimeArmorBreakSeconds,
                    session.Map.Definition.PowerNodes.FirstOrDefault(node =>
                        Vector2.DistanceSquared(tower.Position, node.Position.ToVector2()) <= node.Radius * node.Radius)?.Id)).ToArray(),
                EmergencyDeployments = _emergencyDeployments,
                EmergencyDirectPurchases = _emergencyDirectPurchases,
                EmergencyTriggers = _emergencyTriggers,
                EmergencyHits = _emergencyHits,
                EmergencyKills = _emergencyKills,
                EmergencyDamage = _emergencyDamage,
                GeneratorPurchases = _generatorPurchases,
                GeneratorUpgrades = _generatorUpgrades,
                GeneratedCharges = _generatedCharges,
                Overdrives = _overdrives,
                ProtocolsEnabled = options.UseProtocols && (session.ProtocolsEnabled || options.UseApexUpgrades),
                ApexUpgradesEnabled = options.UseApexUpgrades
            };
        }

        private static IReadOnlyList<SimulationRemainingEnemy> CaptureRemainingEnemies(GameSession session) =>
            session.Enemies
                .Where(enemy => !enemy.IsDead && !enemy.HasEscaped)
                .GroupBy(enemy => new
                {
                    EnemyId = enemy.Definition.Id,
                    enemy.DisplayName,
                    Rank = enemy.Rank.ToString(),
                    SignalRole = enemy.SignalRole.ToString()
                })
                .Select(group => new SimulationRemainingEnemy(
                    group.Key.EnemyId,
                    group.Key.DisplayName,
                    group.Key.Rank,
                    group.Key.SignalRole,
                    group.Count(),
                    FiniteSum(group.Select(enemy => enemy.Health)),
                    FiniteSum(group.Select(enemy => enemy.MaxHealth)),
                    FiniteSum(group.Select(enemy => enemy.Shield)),
                    FiniteSum(group.Select(enemy => ArmorAdjustedDurability(
                        enemy.Health, enemy.Shield, enemy.BaseArmor))),
                    group.Max(enemy => enemy.PathProgress)))
                .OrderByDescending(group => group.CurrentHealth + group.Shield)
                .ThenByDescending(group => group.Count)
                .ThenBy(group => group.DisplayName)
                .ToArray();

        private static IReadOnlyList<SimulationRemainingEnemy> CaptureQueuedEnemies(GameSession session)
        {
            if (session.Waves.ActiveWave is not { } activeWave)
                return Array.Empty<SimulationRemainingEnemy>();

            var healthMultiplier = activeWave.HealthMultiplier * session.Difficulty.EnemyHealthMultiplier;
            var speedMultiplier = activeWave.SpeedMultiplier * session.Difficulty.EnemySpeedMultiplier;
            return session.Waves.CaptureQueuedEnemies(session)
                .Select(group =>
                {
                    var definition = session.Content.Enemies[group.EnemyId];
                    var enemy = new EnemyInstance(
                        0,
                        definition,
                        session.Map.Path,
                        healthMultiplier,
                        speedMultiplier,
                        group.Rank,
                        signalRole: group.SignalRole);
                    return new SimulationRemainingEnemy(
                        group.EnemyId,
                        enemy.DisplayName,
                        enemy.Rank.ToString(),
                        enemy.SignalRole.ToString(),
                        group.Count,
                        FiniteProduct(enemy.MaxHealth, group.Count),
                        FiniteProduct(enemy.MaxHealth, group.Count),
                        FiniteProduct(enemy.Shield, group.Count),
                        FiniteProduct(ArmorAdjustedDurability(
                            enemy.MaxHealth, enemy.Shield, enemy.BaseArmor), group.Count),
                        0);
                })
                .ToArray();
        }

        private SimulationFailureMargin CaptureFailureMargin(
            GameSession session,
            IReadOnlyList<SimulationRemainingEnemy> remainingEnemies,
            IReadOnlyList<SimulationRemainingEnemy> queuedEnemies)
        {
            return new SimulationFailureMargin(
                session.CurrentWave,
                remainingEnemies.Sum(enemy => enemy.Count),
                queuedEnemies.Sum(enemy => enemy.Count),
                FiniteSum(remainingEnemies.Select(enemy => enemy.CurrentHealth)),
                FiniteSum(remainingEnemies.Select(enemy => enemy.Shield)),
                FiniteSum(remainingEnemies.Select(enemy => enemy.ArmorAdjustedDurability)),
                FiniteSum(queuedEnemies.Select(enemy => enemy.CurrentHealth)),
                FiniteSum(queuedEnemies.Select(enemy => enemy.Shield)),
                FiniteSum(queuedEnemies.Select(enemy => enemy.ArmorAdjustedDurability)),
                remainingEnemies.Count == 0 ? 0 : remainingEnemies.Max(enemy => enemy.FurthestProgress),
                _lastWave?.Wave == session.CurrentWave ? _lastWave.EnemyCount : 0,
                _lastWave?.Wave == session.CurrentWave ? _lastWave.ArmorAdjustedDurability : 0)
            {
                FatalEscapedEnemy = _fatalFrameEscapedEnemies.FirstOrDefault(),
                FatalFrameEscapedEnemyCount = _fatalFrameEscapedEnemies.Count,
                FatalFrameEscapedHealth = FiniteSum(_fatalFrameEscapedEnemies.Select(enemy => enemy.CurrentHealth)),
                FatalFrameEscapedShield = FiniteSum(_fatalFrameEscapedEnemies.Select(enemy => enemy.Shield)),
                FatalFrameEscapedArmorAdjustedDurability = FiniteSum(
                    _fatalFrameEscapedEnemies.Select(enemy => enemy.ArmorAdjustedDurability)),
                FatalFrameFurthestProgress = _fatalFrameEscapedEnemies.Count == 0
                    ? 0
                    : _fatalFrameEscapedEnemies.Max(enemy => enemy.Progress)
            };
        }

        private static float ArmorAdjustedDurability(float health, float shield, float armor) =>
            MathF.Max(0, shield) + MathF.Max(0, health) * WavePressureAnalysis.ReferenceHitDamage /
            MathF.Max(1f, WavePressureAnalysis.ReferenceHitDamage - MathF.Max(0, armor));

        private static float FiniteProduct(float value, int count) =>
            !float.IsFinite(value) || value <= 0 || count <= 0
                ? 0
                : (float)Math.Min(float.MaxValue, (double)value * count);

        private static float FiniteSum(IEnumerable<float> values)
        {
            double total = 0;
            foreach (var value in values)
                if (float.IsFinite(value) && value > 0) total += value;
            return (float)Math.Min(float.MaxValue, total);
        }

        public void SampleUtility(GameSession session, float deltaSeconds)
        {
            if (deltaSeconds <= 0) return;

            foreach (var enemy in session.Enemies)
            foreach (var status in enemy.StatusEffects.Active)
            {
                if (status.Type == MinimalBastion.Effects.StatusType.Burn || !_towerIdToDefinition.TryGetValue(status.SourceId, out var towerId)) continue;
                var metrics = GetTower(towerId);
                var statusId = status.Type.ToString();
                metrics.StatusEnemySeconds[statusId] = metrics.StatusEnemySeconds.GetValueOrDefault(statusId) + deltaSeconds;
                metrics.StatusMagnitudeSeconds[statusId] = metrics.StatusMagnitudeSeconds.GetValueOrDefault(statusId) + status.Magnitude * deltaSeconds;
            }

            foreach (var recipient in session.Towers.Where(tower => !tower.IsSupport))
            {
                var buff = session.GetSupportBuff(recipient);
                if (buff.AttackSpeedBonus > 0 && _towerIdToDefinition.TryGetValue(buff.AttackSpeedSourceTowerId, out var attackSource))
                    GetTower(attackSource).SupportedAttackSeconds += deltaSeconds;
                if (buff.RangeBonus > 0 && _towerIdToDefinition.TryGetValue(buff.RangeSourceTowerId, out var rangeSource))
                    GetTower(rangeSource).SupportedRangeSeconds += deltaSeconds;
            }
        }

        private void OnTowerPlaced(TowerInstance tower)
        {
            _towerIdToDefinition[tower.Id] = tower.Definition.Id;
            _towerInstances[tower.Id] = tower;
            _towerLevels[tower.Id] = tower.LevelIndex + 1;
            var metrics = GetTower(tower.Definition.Id);
            metrics.Purchases++;
            metrics.CreditsSpent += tower.Definition.PurchaseCost;
        }

        private void TrackExistingTower(TowerInstance tower)
        {
            _towerIdToDefinition[tower.Id] = tower.Definition.Id;
            _towerInstances[tower.Id] = tower;
            _towerLevels[tower.Id] = tower.LevelIndex + 1;
        }

        private void OnTowerUpgraded(TowerInstance tower, int cost)
        {
            _towerLevels[tower.Id] = tower.LevelIndex + 1;
            var metrics = GetTower(tower.Definition.Id);
            metrics.Upgrades++;
            metrics.CreditsSpent += cost;
            if (tower.IsApex)
            {
                metrics.ApexUpgrades++;
                metrics.ApexCreditsSpent += cost;
            }
            else
                metrics.RecordBranchUpgrade(tower);
        }

        private void OnTowerSold(TowerInstance tower, int value)
        {
            var metrics = GetTower(tower.Definition.Id);
            metrics.Sales++;
            metrics.CreditsRecovered += value;
        }

        private void OnTowerOverdriven(TowerInstance tower)
        {
            _overdrives++;
            GetTower(tower.Definition.Id).Overdrives++;
            var liveInstances = _session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
            var composition = CaptureRemainingEnemies(_session);
            var rankedComposition = composition.Where(enemy =>
                !enemy.Rank.Equals(EnemyRank.Standard.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray();
            _protocolActivations.Add(new SimulationProtocolActivation(
                _session.CurrentWave,
                _elapsed,
                _activeWave is null ? 0 : MathF.Max(0, _elapsed - _activeWave.StartedAt),
                tower.Id,
                tower.Definition.Id,
                tower.IsApex,
                _insideSessionUpdate,
                tower.TargetMode,
                liveInstances.Length,
                _session.Waves.ActiveWave is null
                    ? 0
                    : _session.Waves.CaptureQueuedEnemies(_session).Sum(group => group.Count),
                liveInstances.Length == 0 ? 0 : liveInstances.Max(enemy => enemy.PathProgress),
                liveInstances.Count(enemy => enemy.Rank == EnemyRank.Elite),
                liveInstances.Count(enemy => enemy.Rank == EnemyRank.Boss),
                liveInstances.Count(enemy => enemy.SignalRole != EnemySignalRole.None),
                FiniteSum(composition.Select(enemy => enemy.ArmorAdjustedDurability)),
                FiniteSum(rankedComposition.Select(enemy => enemy.ArmorAdjustedDurability)),
                composition));
        }

        private void OnEnemyKilled(EnemyInstance enemy) => Increment(_enemyKills, EnemyKey(enemy));
        private void OnEnemyEscaped(EnemyInstance enemy)
        {
            Increment(_enemyLeaks, EnemyKey(enemy));
            _escapedThisUpdate.Add(new SimulationEscapedEnemy(
                enemy.Definition.Id,
                enemy.DisplayName,
                enemy.Rank.ToString(),
                enemy.SignalRole.ToString(),
                FiniteProduct(enemy.Health, 1),
                FiniteProduct(enemy.MaxHealth, 1),
                FiniteProduct(enemy.Shield, 1),
                ArmorAdjustedDurability(enemy.Health, enemy.Shield, enemy.BaseArmor),
                float.IsFinite(enemy.PathProgress) ? Math.Clamp(enemy.PathProgress, 0, 1) : 0));
        }

        private void OnEmergencyDefenseDeployed(PulsePlateInstance plate, bool purchased)
        {
            _emergencyDeployments++;
            if (purchased) _emergencyDirectPurchases++;
            var actualCost = purchased
                ? SaturatingPlateCost(plate.Definition.PurchaseCost, plate.Definition.DirectPurchaseCostIncrease,
                    Math.Max(0, _session.EmergencyDirectPurchasesThisWave - 1))
                : 0;
            var projection = _session.Map.Path.Project(plate.Position);
            var liveEnemies = _session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).ToArray();
            var deployment = new PlateDeploymentAccumulator
            {
                Wave = _session.Waves.IsActive ? _session.CurrentWave : _session.CurrentWave + 1,
                PlateId = plate.Id,
                ElapsedSeconds = _elapsed,
                WaveElapsedSeconds = _activeWave is null ? 0 : MathF.Max(0, _elapsed - _activeWave.StartedAt),
                DirectPurchase = purchased,
                Cost = actualCost,
                PathProgress = _session.Map.Path.GetProgress(projection.DistanceAlongPath),
                X = plate.Position.X,
                Y = plate.Position.Y,
                LeadProgress = liveEnemies.Length == 0 ? 0 : liveEnemies.Max(enemy => enemy.PathProgress),
                LiveEnemyCount = liveEnemies.Length,
                QueuedEnemyCount = _session.Waves.ActiveWave is null
                    ? 0
                    : _session.Waves.CaptureQueuedEnemies(_session).Sum(group => group.Count)
            };
            _pulsePlateDeployments.Add(deployment);
            _pulsePlateById[plate.Id] = deployment;
        }

        private void OnEmergencyDefenseTriggered(PulsePlateInstance plate, int hits)
        {
            _emergencyTriggers++;
            _emergencyHits += hits;
            if (!_pulsePlateById.TryGetValue(plate.Id, out var deployment)) return;
            deployment.TriggerCount++;
            deployment.HitCount += Math.Max(0, hits);
        }

        private static int SaturatingPlateCost(int baseCost, int increase, int purchaseIndex)
        {
            var cost = (long)Math.Max(0, baseCost) + (long)Math.Max(0, increase) * Math.Max(0, purchaseIndex);
            return (int)Math.Min(int.MaxValue, cost);
        }

        private void OnDamage(DamageReport report)
        {
            if (report.SourceTowerId <= -100_000)
            {
                var damage = report.HealthDamage + report.ShieldDamage;
                _emergencyDamage += damage;
                if (report.Killed) _emergencyKills++;
                var plateId = checked(-GameConstants.PulsePlateDamageSourceOffset - report.SourceTowerId);
                if (_pulsePlateById.TryGetValue(plateId, out var deployment))
                {
                    deployment.Damage += damage;
                    if (report.Killed) deployment.KillCount++;
                }
                return;
            }
            if (!_towerIdToDefinition.TryGetValue(report.SourceTowerId, out var towerId)) return;
            var metrics = GetTower(towerId);
            metrics.Hits++;
            if (report.Killed) metrics.Kills++;
            metrics.Damage += report.HealthDamage + report.ShieldDamage;
            metrics.ShieldDamage += report.ShieldDamage;
            metrics.ArmorAbsorbed += report.ArmorAbsorbed;
            metrics.Overkill += report.Overkill;
            var level = _towerLevels.GetValueOrDefault(report.SourceTowerId, 1);
            metrics.DamageByLevel[level] = metrics.DamageByLevel.GetValueOrDefault(level) + report.HealthDamage + report.ShieldDamage;

            if (_towerIdToDefinition.TryGetValue(report.ExposeSourceTowerId, out var exposeTowerId))
                GetTower(exposeTowerId).ExposeDamageEquivalent += report.ExposeDamageEquivalent;
            if (_towerIdToDefinition.TryGetValue(report.ArmorBreakSourceTowerId, out var breakTowerId))
                GetTower(breakTowerId).ArmorBreakDamageEquivalent += report.ArmorBreakDamageEquivalent;

            if (!_towerInstances.TryGetValue(report.SourceTowerId, out var sourceTower)) return;
            var support = _session.GetSupportBuff(sourceTower);
            if (support.AttackSpeedBonus <= 0 || !_towerIdToDefinition.TryGetValue(support.AttackSpeedSourceTowerId, out var supportTowerId)) return;
            var power = _session.Map.GetPowerBuff(sourceTower.Position);
            var protocol = sourceTower.IsOverdriven ? sourceTower.Protocol.AttackSpeedBonus : 0f;
            var totalRateMultiplier = 1f + support.AttackSpeedBonus + power.AttackSpeedBonus + protocol;
            GetTower(supportTowerId).SupportDamageEquivalent +=
                (report.HealthDamage + report.ShieldDamage) * support.AttackSpeedBonus / MathF.Max(1f, totalRateMultiplier);
        }

        private void OnGeneratorPlaced(ChargeForgeInstance _) => _generatorPurchases++;
        private void OnGeneratorUpgraded(ChargeForgeInstance _, int __) => _generatorUpgrades++;
        private void OnEmergencyChargeProduced() => _generatedCharges++;

        private TowerRunMetrics GetTower(string id)
        {
            if (!_towers.TryGetValue(id, out var metrics))
                _towers[id] = metrics = new TowerRunMetrics { TowerId = id };
            return metrics;
        }

        private static void Increment(Dictionary<string, int> values, string id) => values[id] = values.GetValueOrDefault(id) + 1;

        private static string EnemyKey(EnemyInstance enemy) => enemy.Rank == EnemyRank.Standard
            ? enemy.Definition.Id
            : $"{enemy.Definition.Id}:{enemy.Rank.ToString().ToLowerInvariant()}";

        private static string DescribeWave(Data.WaveDefinition? wave, IReadOnlyDictionary<string, Data.EnemyDefinition> enemies)
        {
            var threat = ThreatProfile.From(wave, enemies);
            var tags = new List<string>();
            if (threat.HasBoss) tags.Add("Boss");
            else if (threat.HasElite) tags.Add("Elite");
            if (threat.Swarm >= 0.55f) tags.Add("Swarm");
            if (threat.Fast >= 0.25f) tags.Add("Rush");
            if (threat.Armored >= 0.25f) tags.Add("Armored");
            if (threat.Shielded > 0) tags.Add("Shielded");
            if (threat.Durable > 0) tags.Add("Durable");
            return tags.Count == 0 ? "Standard" : string.Join(" + ", tags);
        }

        private sealed record WaveSnapshot(
            int Wave,
            float StartedAt,
            int Lives,
            int Kills,
            int Leaks,
            int CreditsSpent,
            string Archetype,
            int EnemyCount,
            float ArmorAdjustedDurability);

        private sealed class PlateDeploymentAccumulator
        {
            public int Wave { get; init; }
            public int PlateId { get; init; }
            public float ElapsedSeconds { get; init; }
            public float WaveElapsedSeconds { get; init; }
            public bool DirectPurchase { get; init; }
            public int Cost { get; init; }
            public float PathProgress { get; init; }
            public float X { get; init; }
            public float Y { get; init; }
            public float LeadProgress { get; init; }
            public int LiveEnemyCount { get; init; }
            public int QueuedEnemyCount { get; init; }
            public int TriggerCount { get; set; }
            public int HitCount { get; set; }
            public int KillCount { get; set; }
            public float Damage { get; set; }

            public SimulationPulsePlateDeployment Build() => new(
                Wave,
                PlateId,
                ElapsedSeconds,
                WaveElapsedSeconds,
                DirectPurchase,
                Cost,
                PathProgress,
                X,
                Y,
                LeadProgress,
                LiveEnemyCount,
                QueuedEnemyCount,
                TriggerCount,
                HitCount,
                KillCount,
                Damage);
        }
    }
}
