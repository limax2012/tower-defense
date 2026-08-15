using MinimalBastion.Combat;
using MinimalBastion.Enemies;
using MinimalBastion.Core;
using MinimalBastion.Towers;

namespace MinimalBastion.Simulation;

public static class HeadlessSimulation
{
    public static SimulationRunResult Run(Data.GameContent content, SimulationOptions options)
    {
        var session = new GameSession(content, options.MapId, options.DifficultyId);
        var player = new AutoPlayer(session, options.Strategy, options.Seed);
        var telemetry = new RunTelemetry(session);
        var step = Math.Clamp(options.StepSeconds, 0.01f, 0.1f);
        var elapsed = 0f;
        var reactionTimer = 0f;
        var wasWaveActive = false;

        while (!session.IsDefeat && elapsed < options.MaximumSimulatedSeconds)
        {
            if (session.IsVictory)
            {
                if (!options.ContinueEndless || options.MaximumWave <= session.TotalWaves || !session.BeginEndlessMode()) break;
            }

            if (session.CanStartWave && session.CurrentWave < options.MaximumWave)
            {
                player.PrepareForWave(session);
                if (session.StartNextWave()) telemetry.BeginWave(session, elapsed);
            }

            if (!session.Waves.IsActive && session.CurrentWave >= options.MaximumWave && session.Enemies.Count == 0)
                break;

            wasWaveActive = session.Waves.IsActive;
            session.Update(step);
            elapsed += step;
            reactionTimer += step;

            if (session.Waves.IsActive && reactionTimer >= 1f)
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

    private sealed class RunTelemetry
    {
        private readonly Dictionary<string, TowerRunMetrics> _towers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _towerIdToDefinition = new();
        private readonly Dictionary<int, int> _towerLevels = new();
        private readonly Dictionary<string, int> _enemyKills = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _enemyLeaks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<WaveRunMetrics> _waves = new();
        private WaveSnapshot? _activeWave;
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

        public RunTelemetry(GameSession session)
        {
            session.TowerPlaced += OnTowerPlaced;
            session.TowerUpgraded += OnTowerUpgraded;
            session.TowerOverdriven += tower =>
            {
                _overdrives++;
                GetTower(tower.Definition.Id).Overdrives++;
            };
            session.TowerSold += OnTowerSold;
            session.EnemyKilled += OnEnemyKilled;
            session.EnemyEscaped += OnEnemyEscaped;
            session.DamageResolver.DamageApplied += report => OnDamage(session, report);
            session.EmergencyDefenseDeployed += (_, purchased) =>
            {
                _emergencyDeployments++;
                if (purchased) _emergencyDirectPurchases++;
            };
            session.EmergencyDefenseTriggered += (_, hits) =>
            {
                _emergencyTriggers++;
                _emergencyHits += hits;
            };
            session.GeneratorPlaced += _ => _generatorPurchases++;
            session.GeneratorUpgraded += (_, _) => _generatorUpgrades++;
            session.EmergencyChargeProduced += () => _generatedCharges++;
        }

        public void BeginWave(GameSession session, float elapsed)
        {
            _activeWave = new WaveSnapshot(
                session.CurrentWave,
                elapsed,
                session.Economy.Lives,
                session.Economy.TotalKills,
                session.Economy.EscapedEnemies,
                session.Economy.TotalCreditsSpent,
                DescribeWave(session.Waves.ActiveWave, session.Content.Enemies));
        }

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
            _activeWave = null;
        }

        public SimulationRunResult Build(GameSession session, SimulationOptions options, float elapsed, string result)
        {
            return new SimulationRunResult
            {
                MapId = session.Map.Definition.Id,
                DifficultyId = session.DifficultyId,
                Strategy = options.Strategy,
                Seed = options.Seed,
                Result = result,
                WaveReached = session.CurrentWave,
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
                Waves = _waves.ToArray(),
                EmergencyDeployments = _emergencyDeployments,
                EmergencyDirectPurchases = _emergencyDirectPurchases,
                EmergencyTriggers = _emergencyTriggers,
                EmergencyHits = _emergencyHits,
                EmergencyKills = _emergencyKills,
                EmergencyDamage = _emergencyDamage,
                GeneratorPurchases = _generatorPurchases,
                GeneratorUpgrades = _generatorUpgrades,
                GeneratedCharges = _generatedCharges,
                Overdrives = _overdrives
            };
        }

        private void OnTowerPlaced(TowerInstance tower)
        {
            _towerIdToDefinition[tower.Id] = tower.Definition.Id;
            _towerLevels[tower.Id] = tower.LevelIndex + 1;
            var metrics = GetTower(tower.Definition.Id);
            metrics.Purchases++;
            metrics.CreditsSpent += tower.Definition.PurchaseCost;
        }

        private void OnTowerUpgraded(TowerInstance tower, int cost)
        {
            _towerLevels[tower.Id] = tower.LevelIndex + 1;
            var metrics = GetTower(tower.Definition.Id);
            metrics.Upgrades++;
            metrics.CreditsSpent += cost;
            if (tower.SpecializationId is { } specializationId)
                metrics.Specializations[specializationId] = metrics.Specializations.GetValueOrDefault(specializationId) + 1;
        }

        private void OnTowerSold(TowerInstance tower, int value)
        {
            var metrics = GetTower(tower.Definition.Id);
            metrics.Sales++;
            metrics.CreditsRecovered += value;
        }

        private void OnEnemyKilled(EnemyInstance enemy) => Increment(_enemyKills, EnemyKey(enemy));
        private void OnEnemyEscaped(EnemyInstance enemy) => Increment(_enemyLeaks, EnemyKey(enemy));

        private void OnDamage(GameSession session, DamageReport report)
        {
            if (report.SourceTowerId <= -100_000)
            {
                _emergencyDamage += report.HealthDamage + report.ShieldDamage;
                if (report.Killed) _emergencyKills++;
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
        }

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

        private sealed record WaveSnapshot(int Wave, float StartedAt, int Lives, int Kills, int Leaks, int CreditsSpent, string Archetype);
    }
}
