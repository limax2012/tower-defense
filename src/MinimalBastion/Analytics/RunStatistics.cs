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
    private readonly GameSession _session;
    private readonly Dictionary<int, RunTowerStatistics> _towerByInstance = new();
    private readonly Dictionary<int, TowerInstance> _towerInstances = new();
    private readonly Dictionary<string, RunTowerStatistics> _towers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RunEnemyStatistics> _enemies = new(StringComparer.OrdinalIgnoreCase);

    public float SimulatedSeconds { get; private set; }
    public int EmergencyDeployments { get; private set; }
    public int EmergencyDirectPurchases { get; private set; }
    public int EmergencyTriggers { get; private set; }
    public int EmergencyHits { get; private set; }
    public int EmergencyKills { get; private set; }
    public float EmergencyDamage { get; private set; }
    public int GeneratedCharges { get; private set; }
    public int GeneratorPurchases { get; private set; }
    public int GeneratorUpgrades { get; private set; }
    public IReadOnlyCollection<RunTowerStatistics> Towers => _towers.Values;
    public IReadOnlyCollection<RunEnemyStatistics> Enemies => _enemies.Values;
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
        session.TowerOverdriven += tower => GetTower(tower.Definition.Id, tower.Definition.DisplayName).Overdrives++;
        session.TowerSold += OnTowerSold;
        session.EnemyKilled += OnEnemyKilled;
        session.EnemyEscaped += OnEnemyEscaped;
        session.DamageResolver.DamageApplied += OnDamage;
        session.EmergencyDefenseDeployed += (_, purchased) =>
        {
            EmergencyDeployments++;
            if (purchased) EmergencyDirectPurchases++;
        };
        session.EmergencyDefenseTriggered += (_, hits) =>
        {
            EmergencyTriggers++;
            EmergencyHits += hits;
        };
        session.GeneratorPlaced += _ => GeneratorPurchases++;
        session.GeneratorUpgraded += (_, _) => GeneratorUpgrades++;
        session.EmergencyChargeProduced += () => GeneratedCharges++;
    }

    public void Advance(float deltaSeconds)
    {
        deltaSeconds = MathF.Max(0, deltaSeconds);
        SimulatedSeconds += deltaSeconds;
        if (deltaSeconds <= 0) return;

        foreach (var enemy in _session.Enemies)
        foreach (var status in enemy.StatusEffects.Active)
        {
            if (status.Type == StatusType.Burn || !_towerByInstance.TryGetValue(status.SourceId, out var metrics)) continue;
            var activeSeconds = MathF.Min(deltaSeconds, MathF.Max(0, status.RemainingSeconds));
            if (status.Type is StatusType.Slow or StatusType.Stun) metrics.ControlSeconds += activeSeconds;
            else if (status.Type == StatusType.Exposed) metrics.ExposeSeconds += activeSeconds;
            else if (status.Type == StatusType.ArmorBreak) metrics.ArmorBreakSeconds += activeSeconds;
            if (_towerInstances.TryGetValue(status.SourceId, out var sourceTower))
                sourceTower.RecordStatusUptime(status.Type, activeSeconds);
        }
    }

    public RunStatisticsSaveData CaptureSaveData() => new()
    {
        SimulatedSeconds = SimulatedSeconds,
        EmergencyDeployments = EmergencyDeployments,
        EmergencyDirectPurchases = EmergencyDirectPurchases,
        EmergencyTriggers = EmergencyTriggers,
        EmergencyHits = EmergencyHits,
        EmergencyKills = EmergencyKills,
        EmergencyDamage = EmergencyDamage,
        GeneratedCharges = GeneratedCharges,
        GeneratorPurchases = GeneratorPurchases,
        GeneratorUpgrades = GeneratorUpgrades,
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

    public void RestoreSaveData(RunStatisticsSaveData data, IEnumerable<TowerInstance> activeTowers)
    {
        SimulatedSeconds = MathF.Max(0, data.SimulatedSeconds);
        EmergencyDeployments = Math.Max(0, data.EmergencyDeployments);
        EmergencyDirectPurchases = Math.Max(0, data.EmergencyDirectPurchases);
        EmergencyTriggers = Math.Max(0, data.EmergencyTriggers);
        EmergencyHits = Math.Max(0, data.EmergencyHits);
        EmergencyKills = Math.Max(0, data.EmergencyKills);
        EmergencyDamage = MathF.Max(0, data.EmergencyDamage);
        GeneratedCharges = Math.Max(0, data.GeneratedCharges);
        GeneratorPurchases = Math.Max(0, data.GeneratorPurchases);
        GeneratorUpgrades = Math.Max(0, data.GeneratorUpgrades);
        _towerByInstance.Clear();
        _towerInstances.Clear();
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
                Damage = MathF.Max(0, saved.Damage),
                SupportDamageEquivalent = MathF.Max(0, saved.SupportDamageEquivalent),
                ControlSeconds = MathF.Max(0, saved.ControlSeconds),
                ExposeSeconds = MathF.Max(0, saved.ExposeSeconds),
                ArmorBreakSeconds = MathF.Max(0, saved.ArmorBreakSeconds),
                ArmorAbsorbed = MathF.Max(0, saved.ArmorAbsorbed),
                Overkill = MathF.Max(0, saved.Overkill)
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

        foreach (var tower in activeTowers)
        {
            _towerByInstance[tower.Id] = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
            _towerInstances[tower.Id] = tower;
        }
    }

    private void OnTowerPlaced(TowerInstance tower)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Purchases++;
        metrics.CreditsSpent += tower.Definition.PurchaseCost;
        _towerByInstance[tower.Id] = metrics;
        _towerInstances[tower.Id] = tower;
    }

    private void OnTowerUpgraded(TowerInstance tower, int cost)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Upgrades++;
        metrics.CreditsSpent += cost;
        if (tower.SpecializationId is { } specializationId)
            metrics.Specializations[specializationId] = metrics.Specializations.GetValueOrDefault(specializationId) + 1;
        _towerByInstance[tower.Id] = metrics;
    }

    private void OnTowerSold(TowerInstance tower, int value)
    {
        var metrics = GetTower(tower.Definition.Id, tower.Definition.DisplayName);
        metrics.Sales++;
        metrics.CreditsRecovered += value;
    }

    private void OnEnemyKilled(EnemyInstance enemy)
    {
        GetEnemy(enemy).Kills++;
    }

    private void OnEnemyEscaped(EnemyInstance enemy)
    {
        var metrics = GetEnemy(enemy);
        metrics.Escapes++;
        metrics.LivesLost += enemy.LivesLost;
    }

    private void OnDamage(DamageReport report)
    {
        var appliedDamage = report.HealthDamage + report.ShieldDamage;
        if (report.SourceTowerId <= -100_000)
        {
            EmergencyDamage += appliedDamage;
            if (report.Killed) EmergencyKills++;
            return;
        }

        if (!_towerByInstance.TryGetValue(report.SourceTowerId, out var metrics)) return;
        metrics.Hits++;
        metrics.Damage += appliedDamage;
        metrics.ArmorAbsorbed += report.ArmorAbsorbed;
        metrics.Overkill += report.Overkill;
        if (report.Killed) metrics.Kills++;
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
                supportMetrics.SupportDamageEquivalent += supportContribution;
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
    public float ControlSeconds { get; internal set; }
    public float ExposeSeconds { get; internal set; }
    public float ArmorBreakSeconds { get; internal set; }
    public float ArmorAbsorbed { get; internal set; }
    public float Overkill { get; internal set; }
    public Dictionary<string, int> Specializations { get; } = new(StringComparer.OrdinalIgnoreCase);
    public float ContributionDamage => Damage + SupportDamageEquivalent;
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
