using MinimalBastion.Combat;
using MinimalBastion.Core;
using MinimalBastion.Enemies;
using MinimalBastion.Effects;
using MinimalBastion.Persistence;
using MinimalBastion.Tactics;
using MinimalBastion.Towers;

namespace MinimalBastion.Analytics;

public sealed class RunStatistics
{
    private const float AttributionCompactionInterval = 2f;
    private readonly GameSession _session;
    private readonly Dictionary<int, RunTowerStatistics> _towerByInstance = new();
    private readonly Dictionary<int, string> _towerDefinitionByInstance = new();
    private readonly Dictionary<int, TowerInstance> _towerInstances = new();
    private readonly Dictionary<string, RunTowerStatistics> _towers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RunEnemyStatistics> _enemies = new(StringComparer.OrdinalIgnoreCase);
    private float _attributionCompactionRemaining = AttributionCompactionInterval;

    public float SimulatedSeconds { get; private set; }
    public float AttributionCompactionRemaining => _attributionCompactionRemaining;
    public int EmergencyDeployments { get; private set; }
    public int EmergencyDirectPurchases { get; private set; }
    public int EmergencyTriggers { get; private set; }
    public int EmergencyHits { get; private set; }
    public int EmergencyKills { get; private set; }
    public float EmergencyDamage { get; private set; }
    public int GeneratedCharges { get; private set; }
    public int GeneratorPurchases { get; private set; }
    public int GeneratorUpgrades { get; private set; }
    public int ProtocolActivations => (int)Math.Min(int.MaxValue, _towers.Values.Sum(x => (long)x.Overdrives));
    public IReadOnlyCollection<RunTowerStatistics> Towers => _towers.Values;
    public IReadOnlyCollection<RunEnemyStatistics> Enemies => _enemies.Values;
    public IReadOnlyDictionary<int, string> TowerDefinitionByInstance => _towerDefinitionByInstance;
    public int TrackedTowerObjectCount => _towerInstances.Count;
    public IEnumerable<RunTowerStatistics> TowerLeaders => _towers.Values
        .Where(x => x.ContributionDamage > 0)
        .OrderByDescending(x => x.ContributionDamage)
        .ThenBy(x => x.DisplayName);
    public RunEnemyStatistics? GreatestLeakThreat => _enemies.Values
        .Where(x => x.Escapes > 0)
        .OrderByDescending(x => x.LivesLost)
        .ThenByDescending(x => x.Escapes)
        .FirstOrDefault();

    public RunStatistics(GameSession session)
    {
        _session = session;
        session.TowerPlaced += OnTowerPlaced;
        session.TowerUpgraded += OnTowerUpgraded;
        session.TowerOverdriven += tower =>
        {
            var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
            metrics.Overdrives = MetricMath.Add(metrics.Overdrives);
        };
        session.TowerSold += OnTowerSold;
        session.EnemyKilled += OnEnemyKilled;
        session.EnemyEscaped += OnEnemyEscaped;
        session.DamageResolver.DamageApplied += OnDamage;
        session.EmergencyDefenseDeployed += (_, purchased) =>
        {
            EmergencyDeployments = MetricMath.Add(EmergencyDeployments);
            if (purchased) EmergencyDirectPurchases = MetricMath.Add(EmergencyDirectPurchases);
        };
        session.EmergencyDefenseTriggered += (_, hits) =>
        {
            EmergencyTriggers = MetricMath.Add(EmergencyTriggers);
            EmergencyHits = MetricMath.Add(EmergencyHits, hits);
        };
        session.GeneratorPlaced += _ => GeneratorPurchases = MetricMath.Add(GeneratorPurchases);
        session.GeneratorUpgraded += (_, _) => GeneratorUpgrades = MetricMath.Add(GeneratorUpgrades);
        session.EmergencyChargeProduced += () => GeneratedCharges = MetricMath.Add(GeneratedCharges);
    }

    public void Advance(float deltaSeconds)
    {
        deltaSeconds = MetricMath.Normalize(deltaSeconds);
        SimulatedSeconds = MetricMath.Add(SimulatedSeconds, deltaSeconds);
        if (deltaSeconds <= 0) return;

        foreach (var enemy in _session.Enemies)
        foreach (var status in enemy.StatusEffects.Active)
        {
            if (status.Type == StatusType.Burn || !_towerByInstance.TryGetValue(status.SourceId, out var metrics)) continue;
            var activeSeconds = MathF.Min(deltaSeconds, MathF.Max(0, status.RemainingSeconds));
            if (status.Type is StatusType.Slow or StatusType.Stun) metrics.ControlSeconds = MetricMath.Add(metrics.ControlSeconds, activeSeconds);
            else if (status.Type == StatusType.Exposed) metrics.ExposeSeconds = MetricMath.Add(metrics.ExposeSeconds, activeSeconds);
            else if (status.Type == StatusType.ArmorBreak) metrics.ArmorBreakSeconds = MetricMath.Add(metrics.ArmorBreakSeconds, activeSeconds);
            if (_towerInstances.TryGetValue(status.SourceId, out var sourceTower))
                sourceTower.RecordStatusUptime(status.Type, activeSeconds);
        }

        _attributionCompactionRemaining -= deltaSeconds;
        if (_attributionCompactionRemaining <= 0)
        {
            CompactAttributionSources();
            _attributionCompactionRemaining = AttributionCompactionInterval;
        }
    }

    public RunStatisticsSaveData CaptureSaveData()
    {
        CompactAttributionSources();
        return new RunStatisticsSaveData
        {
            SimulatedSeconds = SimulatedSeconds,
            AttributionCompactionRemaining = _attributionCompactionRemaining,
            EmergencyDeployments = EmergencyDeployments,
            EmergencyDirectPurchases = EmergencyDirectPurchases,
            EmergencyTriggers = EmergencyTriggers,
            EmergencyHits = EmergencyHits,
            EmergencyKills = EmergencyKills,
            EmergencyDamage = EmergencyDamage,
            GeneratedCharges = GeneratedCharges,
            GeneratorPurchases = GeneratorPurchases,
            GeneratorUpgrades = GeneratorUpgrades,
            TowerDefinitionByInstance = new Dictionary<int, string>(_towerDefinitionByInstance),
            Towers = _towers.Values.Select(x => new RunTowerStatisticsSaveData
            {
                TowerId = x.TowerId,
                DisplayName = x.DisplayName,
                Purchases = x.Purchases,
                Upgrades = x.Upgrades,
                Sales = x.Sales,
                CreditsSpent = x.CreditsSpent,
                CreditsRecovered = x.CreditsRecovered,
                Hits = x.Hits,
                Kills = x.Kills,
                Overdrives = x.Overdrives,
                Damage = x.Damage,
                SupportDamageEquivalent = x.SupportDamageEquivalent,
                ExposeDamageEquivalent = x.ExposeDamageEquivalent,
                ArmorBreakDamageEquivalent = x.ArmorBreakDamageEquivalent,
                ControlSeconds = x.ControlSeconds,
                ExposeSeconds = x.ExposeSeconds,
                ArmorBreakSeconds = x.ArmorBreakSeconds,
                ArmorAbsorbed = x.ArmorAbsorbed,
                Overkill = x.Overkill,
                Specializations = new Dictionary<string, int>(x.Specializations, StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            Enemies = _enemies.Values.Select(x => new RunEnemyStatisticsSaveData
            {
                EnemyId = x.EnemyId,
                DisplayName = x.DisplayName,
                Kills = x.Kills,
                Escapes = x.Escapes,
                LivesLost = x.LivesLost
            }).ToList()
        };
    }

    public void RestoreSaveData(RunStatisticsSaveData data, IEnumerable<TowerInstance> activeTowers)
    {
        SimulatedSeconds = MetricMath.Normalize(data.SimulatedSeconds);
        EmergencyDeployments = Math.Max(0, data.EmergencyDeployments);
        EmergencyDirectPurchases = Math.Max(0, data.EmergencyDirectPurchases);
        EmergencyTriggers = Math.Max(0, data.EmergencyTriggers);
        EmergencyHits = Math.Max(0, data.EmergencyHits);
        EmergencyKills = Math.Max(0, data.EmergencyKills);
        EmergencyDamage = MetricMath.Normalize(data.EmergencyDamage);
        GeneratedCharges = Math.Max(0, data.GeneratedCharges);
        GeneratorPurchases = Math.Max(0, data.GeneratorPurchases);
        GeneratorUpgrades = Math.Max(0, data.GeneratorUpgrades);
        _towerByInstance.Clear();
        _towerDefinitionByInstance.Clear();
        _towerInstances.Clear();
        // Zero identifies checkpoints from before this private timer was
        // serialized. Current co-op snapshots require the exact positive phase.
        _attributionCompactionRemaining = data.AttributionCompactionRemaining > 0
            ? MathF.Min(data.AttributionCompactionRemaining, AttributionCompactionInterval)
            : AttributionCompactionInterval;
        _towers.Clear();
        _enemies.Clear();

        foreach (var saved in data.Towers)
        {
            var metrics = new RunTowerStatistics(saved.TowerId, saved.DisplayName)
            {
                Purchases = Math.Max(0, saved.Purchases),
                Upgrades = Math.Max(0, saved.Upgrades),
                Sales = Math.Max(0, saved.Sales),
                CreditsSpent = Math.Max(0, saved.CreditsSpent),
                CreditsRecovered = Math.Max(0, saved.CreditsRecovered),
                Hits = Math.Max(0, saved.Hits),
                Kills = Math.Max(0, saved.Kills),
                Overdrives = Math.Max(0, saved.Overdrives),
                Damage = MetricMath.Normalize(saved.Damage),
                SupportDamageEquivalent = MetricMath.Normalize(saved.SupportDamageEquivalent),
                ExposeDamageEquivalent = MetricMath.Normalize(saved.ExposeDamageEquivalent),
                ArmorBreakDamageEquivalent = MetricMath.Normalize(saved.ArmorBreakDamageEquivalent),
                ControlSeconds = MetricMath.Normalize(saved.ControlSeconds),
                ExposeSeconds = MetricMath.Normalize(saved.ExposeSeconds),
                ArmorBreakSeconds = MetricMath.Normalize(saved.ArmorBreakSeconds),
                ArmorAbsorbed = MetricMath.Normalize(saved.ArmorAbsorbed),
                Overkill = MetricMath.Normalize(saved.Overkill)
            };
            foreach (var specialization in saved.Specializations)
                metrics.Specializations[specialization.Key] = Math.Max(0, specialization.Value);
            _towers[saved.TowerId] = metrics;
        }

        foreach (var saved in data.Enemies)
            _enemies[saved.EnemyId] = new RunEnemyStatistics(saved.EnemyId, saved.DisplayName)
            {
                Kills = Math.Max(0, saved.Kills),
                Escapes = Math.Max(0, saved.Escapes),
                LivesLost = Math.Max(0, saved.LivesLost)
            };

        foreach (var source in data.TowerDefinitionByInstance)
        {
            if (source.Key <= 0 || !_towers.TryGetValue(source.Value, out var metrics)) continue;
            _towerByInstance[source.Key] = metrics;
            _towerDefinitionByInstance[source.Key] = metrics.TowerId;
        }

        foreach (var tower in activeTowers)
        {
            _towerByInstance[tower.Id] = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
            _towerDefinitionByInstance[tower.Id] = tower.Definition.Id;
            _towerInstances[tower.Id] = tower;
        }
    }

    private void OnTowerPlaced(TowerInstance tower)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Purchases = MetricMath.Add(metrics.Purchases);
        metrics.CreditsSpent = MetricMath.Add(metrics.CreditsSpent, tower.Definition.PurchaseCost);
        _towerByInstance[tower.Id] = metrics;
        _towerDefinitionByInstance[tower.Id] = tower.Definition.Id;
        _towerInstances[tower.Id] = tower;
    }

    private void OnTowerUpgraded(TowerInstance tower, int cost)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Upgrades = MetricMath.Add(metrics.Upgrades);
        metrics.CreditsSpent = MetricMath.Add(metrics.CreditsSpent, cost);
        if (tower.IsApex)
        {
            _towerByInstance[tower.Id] = metrics;
            return;
        }
        if (tower.SpecializationId is { } specializationId)
            metrics.Specializations[specializationId] = MetricMath.Add(metrics.Specializations.GetValueOrDefault(specializationId));
        else if (tower.DoctrineId is { } doctrineId)
            metrics.Specializations[$"doctrine:{doctrineId}"] = MetricMath.Add(metrics.Specializations.GetValueOrDefault($"doctrine:{doctrineId}"));
        _towerByInstance[tower.Id] = metrics;
    }

    private void OnTowerSold(TowerInstance tower, int value)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Sales = MetricMath.Add(metrics.Sales);
        metrics.CreditsRecovered = MetricMath.Add(metrics.CreditsRecovered, value);
        _towerInstances.Remove(tower.Id);
    }

    private void CompactAttributionSources()
    {
        var activeSources = _session.Towers.Select(tower => tower.Id).ToHashSet();
        foreach (var enemy in _session.Enemies)
        foreach (var status in enemy.StatusEffects.Active)
            if (status.SourceId > 0) activeSources.Add(status.SourceId);
        foreach (var projectile in _session.Projectiles.Projectiles)
            if (!projectile.IsExpired && projectile.Payload.SourceTowerId > 0)
                activeSources.Add(projectile.Payload.SourceTowerId);

        foreach (var expired in _towerDefinitionByInstance.Keys.Where(id => !activeSources.Contains(id)).ToArray())
        {
            _towerDefinitionByInstance.Remove(expired);
            _towerByInstance.Remove(expired);
            _towerInstances.Remove(expired);
        }
    }

    private void OnEnemyKilled(EnemyInstance enemy)
    {
        var metrics = GetEnemy(enemy);
        metrics.Kills = MetricMath.Add(metrics.Kills);
    }

    private void OnEnemyEscaped(EnemyInstance enemy)
    {
        var metrics = GetEnemy(enemy);
        metrics.Escapes = MetricMath.Add(metrics.Escapes);
        metrics.LivesLost = MetricMath.Add(metrics.LivesLost, enemy.LivesLost);
    }

    private void OnDamage(DamageReport report)
    {
        var appliedDamage = report.HealthDamage + report.ShieldDamage;
        if (report.SourceTowerId <= -100_000)
        {
            EmergencyDamage = MetricMath.Add(EmergencyDamage, appliedDamage);
            if (report.Killed) EmergencyKills = MetricMath.Add(EmergencyKills);
            return;
        }

        if (!_towerByInstance.TryGetValue(report.SourceTowerId, out var metrics)) return;
        metrics.Hits = MetricMath.Add(metrics.Hits);
        metrics.Damage = MetricMath.Add(metrics.Damage, appliedDamage);
        metrics.ArmorAbsorbed = MetricMath.Add(metrics.ArmorAbsorbed, report.ArmorAbsorbed);
        metrics.Overkill = MetricMath.Add(metrics.Overkill, report.Overkill);
        if (report.Killed) metrics.Kills = MetricMath.Add(metrics.Kills);
        if (report.ExposeDamageEquivalent > 0 && _towerByInstance.TryGetValue(report.ExposeSourceTowerId, out var exposeMetrics))
        {
            exposeMetrics.ExposeDamageEquivalent = MetricMath.Add(exposeMetrics.ExposeDamageEquivalent, report.ExposeDamageEquivalent);
            if (_towerInstances.TryGetValue(report.ExposeSourceTowerId, out var exposeTower))
                exposeTower.RecordExposeAssist(report.ExposeDamageEquivalent);
        }
        if (report.ArmorBreakDamageEquivalent > 0 && _towerByInstance.TryGetValue(report.ArmorBreakSourceTowerId, out var breakMetrics))
        {
            breakMetrics.ArmorBreakDamageEquivalent = MetricMath.Add(breakMetrics.ArmorBreakDamageEquivalent, report.ArmorBreakDamageEquivalent);
            if (_towerInstances.TryGetValue(report.ArmorBreakSourceTowerId, out var breakTower))
                breakTower.RecordArmorBreakAssist(report.ArmorBreakDamageEquivalent);
        }
        if (_towerInstances.TryGetValue(report.SourceTowerId, out var tower))
        {
            tower.RecordCombat(appliedDamage, report.Killed);
            var support = _session.GetSupportBuff(tower);
            if (support.AttackSpeedBonus > 0 && _towerByInstance.TryGetValue(support.AttackSpeedSourceTowerId, out var supportMetrics))
            {
                var power = _session.Map.GetPowerBuff(tower.Position);
                var protocol = tower.IsOverdriven ? tower.Protocol.AttackSpeedBonus : 0f;
                var totalRateMultiplier = 1f + support.AttackSpeedBonus + power.AttackSpeedBonus + protocol;
                var supportContribution = appliedDamage * support.AttackSpeedBonus / MathF.Max(1f, totalRateMultiplier);
                supportMetrics.SupportDamageEquivalent = MetricMath.Add(supportMetrics.SupportDamageEquivalent, supportContribution);
                if (_towerInstances.TryGetValue(support.AttackSpeedSourceTowerId, out var supportTower))
                    supportTower.RecordSupport(supportContribution);
            }
        }
    }

    private RunTowerStatistics GetTower(string id, string displayName)
    {
        if (!_towers.TryGetValue(id, out var metrics))
            _towers[id] = metrics = new RunTowerStatistics(id, displayName);
        return metrics;
    }

    private RunEnemyStatistics GetEnemy(EnemyInstance enemy)
    {
        var id = enemy.Rank == EnemyRank.Standard ? enemy.Definition.Id : $"{enemy.Definition.Id}:{enemy.Rank.ToString().ToLowerInvariant()}";
        if (!_enemies.TryGetValue(id, out var metrics))
            _enemies[id] = metrics = new RunEnemyStatistics(id, enemy.DisplayName);
        return metrics;
    }
}

public sealed class RunTowerStatistics
{
    public string TowerId { get; }
    public string DisplayName { get; }
    public int Purchases { get; internal set; }
    public int Upgrades { get; internal set; }
    public int Sales { get; internal set; }
    public int CreditsSpent { get; internal set; }
    public int CreditsRecovered { get; internal set; }
    public int Hits { get; internal set; }
    public int Kills { get; internal set; }
    public int Overdrives { get; internal set; }
    public float Damage { get; internal set; }
    public float SupportDamageEquivalent { get; internal set; }
    public float ExposeDamageEquivalent { get; internal set; }
    public float ArmorBreakDamageEquivalent { get; internal set; }
    public float ControlSeconds { get; internal set; }
    public float ExposeSeconds { get; internal set; }
    public float ArmorBreakSeconds { get; internal set; }
    public float ArmorAbsorbed { get; internal set; }
    public float Overkill { get; internal set; }
    public Dictionary<string, int> Specializations { get; } = new(StringComparer.OrdinalIgnoreCase);
    public float AssistDamageEquivalent => MetricMath.Add(MetricMath.Add(SupportDamageEquivalent, ExposeDamageEquivalent), ArmorBreakDamageEquivalent);
    public float ContributionDamage => MetricMath.Add(Damage, AssistDamageEquivalent);
    public float DamagePerCredit => CreditsSpent <= 0 ? 0 : ContributionDamage / CreditsSpent;

    public RunTowerStatistics(string towerId, string displayName)
    {
        TowerId = towerId;
        DisplayName = displayName;
    }
}

public sealed class RunEnemyStatistics
{
    public string EnemyId { get; }
    public string DisplayName { get; }
    public int Kills { get; internal set; }
    public int Escapes { get; internal set; }
    public int LivesLost { get; internal set; }

    public RunEnemyStatistics(string enemyId, string displayName)
    {
        EnemyId = enemyId;
        DisplayName = displayName;
    }
}
