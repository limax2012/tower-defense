using System.Globalization;
using MinimalBastion;
using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Enemies;
using MinimalBastion.Maps;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Tests;

internal static class BalanceSimulation
{
    private const float StepSeconds = 0.02f;

    public static void Run(GameContent content)
    {
        Console.WriteLine("BALANCE BENCHMARK (deterministic, current data)");
        var totalEnemies = content.Waves.Waves.SelectMany(x => x.Groups).Sum(x => x.Count);
        var incomeAnchor = content.Waves.Waves.FirstOrDefault(wave => wave.Number == GameConstants.LateIncomeAnchorWave);
        var totalKillRewards = content.Waves.Waves.Sum(wave =>
        {
            var scale = MinimalBastion.Economy.WaveIncomeCurve.CalculateScale(wave, incomeAnchor, content.Enemies);
            return wave.Groups.Sum(group => group.Count * MinimalBastion.Economy.Economy.CalculateKillReward(
                content.Enemies[group.EnemyId].Reward, wave.Number, scale));
        });
        var totalWaveRewards = content.Waves.Waves.Sum(wave =>
            MinimalBastion.Economy.Economy.CalculateWaveReward(wave.Number,
                MinimalBastion.Economy.WaveIncomeCurve.CalculateScale(wave, incomeAnchor, content.Enemies)));
        Console.WriteLine($"Wave economy: {totalEnemies} enemies, {totalKillRewards} kill credits, {totalWaveRewards} wave credits, up to {content.Waves.Waves.Count * GameConstants.EarlyStartBonus} early-start credits, {GameConstants.StartingCredits} starting credits.");
        Console.WriteLine("Tower                 Cost  Raw L1  Single L1  Dense L1  Raw L3  Upgrade DPS/currency");
        Console.WriteLine("--------------------  ----  ------  ---------  --------  ------  ----------------------");

        foreach (var tower in content.Towers.Values.OrderBy(x => x.PurchaseCost))
        {
            var levelOne = SingleTarget(content, tower, 0, 20, 0);
            var firstDoctrine = tower.Tier2Doctrines.FirstOrDefault();
            var firstSpecialization = tower.Specializations.FirstOrDefault();
            var terminalLevel = (firstSpecialization?.Level ?? tower.Levels[2]).WithDoctrine(firstDoctrine);
            var dense = DenseGroup(content, tower, 0, 12, 8);
            var levelTwo = SingleTarget(content, tower, 1, 20, 0, doctrineId: firstDoctrine?.Id);
            var firstCost = firstDoctrine?.UpgradeCost ?? tower.Levels[0].UpgradeCost ?? 0;
            var upgradeDps = firstCost > 0
                ? (levelTwo.DamagePerSecond - levelOne.DamagePerSecond) / firstCost
                : 0;

            Console.WriteLine($"{tower.DisplayName,-20}  {tower.PurchaseCost,4}  {RawDps(tower.Levels[0]),6:0.0}  {levelOne.DamagePerSecond,9:0.0}  {dense.DamagePerSecond,8:0.0}  {RawDps(terminalLevel),6:0.0}  {upgradeDps,22:0.000}");
        }

        PrintTierEconomy(content);

        Console.WriteLine();
        Console.WriteLine("ARMOR SWEEP (level 1 effective DPS; flat armor subtraction with 1 damage floor)");
        Console.WriteLine("Tower                 Armor 0  Armor 4  Armor 8  Armor prevented");
        Console.WriteLine("--------------------  --------  --------  --------  ---------------");
        foreach (var tower in content.Towers.Values.OrderBy(x => x.PurchaseCost))
        {
            var armor0 = SingleTarget(content, tower, 0, 12, 0);
            var armor4 = SingleTarget(content, tower, 0, 12, 4);
            var armor8 = SingleTarget(content, tower, 0, 12, 8);
            Console.WriteLine($"{tower.DisplayName,-20}  {armor0.DamagePerSecond,8:0.0}  {armor4.DamagePerSecond,8:0.0}  {armor8.DamagePerSecond,8:0.0}  {armor8.ArmorAbsorbed,15:0.0}");
        }

        Console.WriteLine();
        Console.WriteLine("PRACTICAL SCENARIOS (level 1)");
        Console.WriteLine("Tower                 Fast DPS  Swarm K/S/L  HP cut  Waste %  Dense aggregate DPS  Boss DPS");
        Console.WriteLine("--------------------  --------  -----------  ------  -------  -------------------  --------");
        foreach (var tower in content.Towers.Values.OrderBy(x => x.PurchaseCost))
        {
            var fast = FastEnemy(content, tower);
            var swarm = Swarm(content, tower);
            var dense = DenseGroup(content, tower, 0, 12, 8);
            var boss = SingleTarget(content, tower, 0, 30, 0, 1_000_000);
            Console.WriteLine($"{tower.DisplayName,-20}  {fast.DamagePerSecond,8:0.0}  {swarm.Kills,2}/{swarm.Survivors,2}/{swarm.Leaks,-2}  {swarm.HealthRemovedPercent,5:0.0}%  {WastePercent(swarm),7:0.0}  {dense.DamagePerSecond,19:0.0}  {boss.DamagePerSecond,8:0.0}");
        }

        PrintFinalRoleScenarios(content);

        PrintSupportEconomy(content);
        PrintCrossTowerSynergies(content);
        Console.WriteLine("Metrics include projectile travel, path movement, armor, shields, DOT persistence, overkill, kills, leaks, and damage reports.");
    }

    private static void PrintFinalRoleScenarios(GameContent content)
    {
        Console.WriteLine();
        Console.WriteLine("FINAL-ROLE PRACTICALS (moving 45-health rush; K/S/L = kills/survivors/leaks)");
        Console.WriteLine("Tower / final role             Cost  K/S/L     HP cut  Waste %");
        Console.WriteLine("-----------------------------  ----  --------  ------  -------");

        foreach (var tower in content.Towers.Values.OrderBy(x => x.PurchaseCost))
        {
            foreach (var row in TierRows(tower).Where(x => x.LevelIndex == 2))
            {
                var swarm = Swarm(content, tower, row.LevelIndex, row.SpecializationId, row.DoctrineId);
                Console.WriteLine($"{tower.DisplayName + " " + row.Label,-29}  {row.CumulativeCost,4}  {swarm.Kills,2}/{swarm.Survivors,2}/{swarm.Leaks,-2}  {swarm.HealthRemovedPercent,5:0.0}%  {WastePercent(swarm),7:0.0}");
            }
        }
    }

    private static void PrintTierEconomy(GameContent content)
    {
        Console.WriteLine();
        Console.WriteLine("ALL-TIER ECONOMY (20-second deterministic scenarios; cost is cumulative investment)");
        Console.WriteLine("Tower / tier                  Cost  Marginal  Single DPS  Armor-8 DPS  Dense DPS  Dense / cost");
        Console.WriteLine("----------------------------  ----  --------  ----------  -----------  ---------  ------------");

        foreach (var tower in content.Towers.Values.OrderBy(x => x.PurchaseCost))
        {
            var rows = TierRows(tower);
            foreach (var row in rows)
            {
                var single = SingleTarget(content, tower, row.LevelIndex, 20, 0,
                    specializationId: row.SpecializationId, doctrineId: row.DoctrineId);
                var armored = SingleTarget(content, tower, row.LevelIndex, 20, 8,
                    specializationId: row.SpecializationId, doctrineId: row.DoctrineId);
                var dense = DenseGroup(content, tower, row.LevelIndex, 20, 8,
                    row.SpecializationId, row.DoctrineId);
                Console.WriteLine($"{tower.DisplayName + " " + row.Label,-28}  {row.CumulativeCost,4}  {row.MarginalCost,8}  {single.DamagePerSecond,10:0.0}  {armored.DamagePerSecond,11:0.0}  {dense.DamagePerSecond,9:0.0}  {dense.DamagePerSecond / Math.Max(1, row.CumulativeCost),12:0.000}");
            }
        }
    }

    private static IReadOnlyList<TierRow> TierRows(TowerDefinition tower)
    {
        var rows = new List<TierRow>
        {
            new("L1", 0, null, null, tower.PurchaseCost, tower.PurchaseCost)
        };

        if (tower.Tier2Doctrines.Count > 0)
        {
            foreach (var doctrine in tower.Tier2Doctrines)
            {
                var cumulativeDoctrine = tower.PurchaseCost + doctrine.UpgradeCost;
                rows.Add(new TierRow($"L2 {doctrine.ShortLabel}", 1, doctrine.Id, null,
                    cumulativeDoctrine, doctrine.UpgradeCost));
                rows.AddRange(tower.Specializations.Select(specialization => new TierRow(
                    $"{doctrine.ShortLabel}>{specialization.ShortLabel}",
                    2,
                    doctrine.Id,
                    specialization.Id,
                    cumulativeDoctrine + specialization.UpgradeCost,
                    specialization.UpgradeCost)));
            }
            return rows;
        }

        var levelTwoCost = tower.Levels[0].UpgradeCost ?? 0;
        var cumulativeLevelTwo = tower.PurchaseCost + levelTwoCost;
        rows.Add(new TierRow("L2", 1, null, null, cumulativeLevelTwo, levelTwoCost));

        if (tower.Specializations.Count > 0)
        {
            rows.AddRange(tower.Specializations.Select(specialization => new TierRow(
                specialization.ShortLabel,
                2,
                null,
                specialization.Id,
                cumulativeLevelTwo + specialization.UpgradeCost,
                specialization.UpgradeCost)));
        }
        else
        {
            var levelThreeCost = tower.Levels[1].UpgradeCost ?? 0;
            rows.Add(new TierRow("L3", 2, null, null, cumulativeLevelTwo + levelThreeCost, levelThreeCost));
        }

        return rows;
    }

    private static void PrintSupportEconomy(GameContent content)
    {
        var beacon = content.Towers["signal_beacon"];
        Console.WriteLine();
        Console.WriteLine("SIGNAL BEACON ECONOMY (20-second assisted DPS; compact tests throughput, spread tests coverage)");
        Console.WriteLine("Tier        Cost  Compact  Compact/cost  Spread  Spread/cost");
        Console.WriteLine("----------  ----  -------  ------------  ------  -----------");
        foreach (var row in TierRows(beacon))
        {
            var compact = SupportContribution(content, row, compact: true);
            var spread = SupportContribution(content, row, compact: false);
            Console.WriteLine($"{row.Label,-10}  {row.CumulativeCost,4}  {compact.AssistedDps,7:0.0}  {compact.AssistedDps / row.CumulativeCost,12:0.000}  {spread.AssistedDps,6:0.0}  {spread.AssistedDps / row.CumulativeCost,11:0.000}");
        }
    }

    private static void PrintCrossTowerSynergies(GameContent content)
    {
        var frost = content.Towers["frost_spire"];
        var arc = content.Towers["arc_relay"];
        var ember = content.Towers["ember_coil"];
        var needle = content.Towers["needle_turret"];
        var breaker = content.Towers["breaker_cannon"];
        var prism = content.Towers["prism_beam"];

        Console.WriteLine();
        Console.WriteLine("CROSS-TOWER SYNERGIES (20-second stationary targets; bonus isolates pair output above both towers alone)");
        Console.WriteLine("Pair / configuration               Armor  Targets  Solo sum  Pair DPS  Bonus DPS  Bonus %");
        Console.WriteLine("---------------------------------  -----  -------  --------  --------  ---------  -------");
        PrintSynergyRow(content, "Frost + Arc L1", frost, TierRows(frost)[0], arc, TierRows(arc)[0], 0, 8);
        PrintSynergyRow(content, "Permafrost + Storm", frost,
            FindTier(frost, "frost_deep_chill", "permafrost"), arc,
            FindTier(arc, "arc_fork", "storm_lattice"), 0, 8);
        PrintSynergyRow(content, "Ember + Needle L1", ember, TierRows(ember)[0], needle, TierRows(needle)[0], 8, 1);
        PrintSynergyRow(content, "Shatter + Needle L1", breaker,
            FindTier(breaker, "breaker_bored", "shatter_shell"), needle, TierRows(needle)[0], 8, 4);
        PrintSynergyRow(content, "Prism + Needle L1", prism, TierRows(prism)[0], needle, TierRows(needle)[0], 0, 1);
    }

    private static TierRow FindTier(TowerDefinition tower, string doctrineId, string specializationId) =>
        TierRows(tower).Single(row =>
            string.Equals(row.DoctrineId, doctrineId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(row.SpecializationId, specializationId, StringComparison.OrdinalIgnoreCase));

    private static void PrintSynergyRow(
        GameContent content,
        string label,
        TowerDefinition first,
        TierRow firstTier,
        TowerDefinition second,
        TierRow secondTier,
        float armor,
        int targetCount)
    {
        var firstDps = RunSynergyTeam(content, new[] { new TowerBuild(first, firstTier) }, armor, targetCount).DamagePerSecond;
        var secondDps = RunSynergyTeam(content, new[] { new TowerBuild(second, secondTier) }, armor, targetCount).DamagePerSecond;
        var pairDps = RunSynergyTeam(content,
            new[] { new TowerBuild(first, firstTier), new TowerBuild(second, secondTier) }, armor, targetCount).DamagePerSecond;
        var soloSum = firstDps + secondDps;
        var bonus = pairDps - soloSum;
        var bonusPercent = soloSum <= 0 ? 0 : bonus / soloSum * 100f;
        Console.WriteLine($"{label,-33}  {armor,5:0.#}  {targetCount,7}  {soloSum,8:0.0}  {pairDps,8:0.0}  {bonus,9:0.0}  {bonusPercent,6:0.0}%");
    }

    private static SimulationResult RunSynergyTeam(
        GameContent content,
        IReadOnlyList<TowerBuild> builds,
        float armor,
        int targetCount)
    {
        const float seconds = 20f;
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_synergy", 100_000, 0.01f, armor, 0, 0);
        var session = CreateSession(content, builds.Select(x => x.Definition).ToArray(), new[] { enemy }, StationaryMap());
        var positions = builds.Count == 1
            ? new[] { new Vector2(400, 300) }
            : new[] { new Vector2(340, 300), new Vector2(460, 300) };
        for (var index = 0; index < builds.Count; index++)
        {
            var build = builds[index];
            AddTower(session, build.Definition, positions[index], build.Tier.LevelIndex,
                build.Tier.SpecializationId, build.Tier.DoctrineId);
        }

        var targets = Enumerable.Range(0, targetCount).Select(_ => AddEnemy(session, enemy)).ToArray();
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, seconds);
        return metrics.ToResult(seconds, targets);
    }

    private static float RawDps(TowerLevelDefinition level) => level.Damage * level.AttacksPerSecond;

    private static float WastePercent(SimulationResult result) => result.Damage + result.Overkill <= 0
        ? 0
        : result.Overkill / (result.Damage + result.Overkill) * 100;

    private static SimulationResult SingleTarget(
        GameContent content,
        TowerDefinition tower,
        int levelIndex,
        float seconds,
        float armor,
        float health = 100_000,
        string? specializationId = null,
        string? doctrineId = null)
    {
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_target", health, 0.01f, armor, 0, 0);
        var session = CreateSession(content, new[] { tower }, new[] { enemy }, StationaryMap());
        AddTower(session, tower, new Vector2(400, 300), levelIndex, specializationId, doctrineId);
        var target = AddEnemy(session, enemy);
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, seconds);
        return metrics.ToResult(seconds, target);
    }

    private static SimulationResult DenseGroup(GameContent content, TowerDefinition tower, int levelIndex, float seconds,
        int count, string? specializationId = null, string? doctrineId = null)
    {
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_dense", 100_000, 0.01f, 0, 0, 0);
        var session = CreateSession(content, new[] { tower }, new[] { enemy }, StationaryMap());
        AddTower(session, tower, new Vector2(400, 300), levelIndex, specializationId, doctrineId);
        var targets = Enumerable.Range(0, count).Select(_ => AddEnemy(session, enemy)).ToArray();
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, seconds);
        return metrics.ToResult(seconds, targets);
    }

    private static SimulationResult FastEnemy(GameContent content, TowerDefinition tower)
    {
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_fast", 10_000, 500, 0, 0, 0);
        var session = CreateSession(content, new[] { tower }, new[] { enemy }, MovingMap());
        AddTower(session, tower, new Vector2(400, 300), 0);
        var target = AddEnemy(session, enemy);
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, 4);
        return metrics.ToResult(4, target);
    }

    private static SimulationResult Swarm(
        GameContent content,
        TowerDefinition tower,
        int levelIndex = 0,
        string? specializationId = null,
        string? doctrineId = null)
    {
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_swarm", 45, 30, 0, 0, 0);
        var session = CreateSession(content, new[] { tower }, new[] { enemy }, SwarmMap());
        AddTower(session, tower, new Vector2(400, 300), levelIndex, specializationId, doctrineId);
        var targets = Enumerable.Range(0, 24).Select(_ => AddEnemy(session, enemy)).ToArray();
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, 22);
        return metrics.ToResult(22, targets);
    }

    private static SupportResult SupportContribution(GameContent content, TierRow beaconTier, bool compact)
    {
        var enemy = EnemyLike(content.Enemies.Values.First(), "benchmark_support", 100_000, 0.01f, 0, 0, 0);
        var beacon = content.Towers["signal_beacon"];
        var offense = content.Towers[compact ? "needle_turret" : "watchtower"];
        var positions = compact
            ? new[] { new Vector2(330, 280), new Vector2(400, 280), new Vector2(470, 280) }
            : new[] { new Vector2(300, 200), new Vector2(400, 260), new Vector2(500, 200) };
        var beaconPosition = new Vector2(400, 390);
        var without = RunSupportTeam(content, offense, beacon, enemy, positions, beaconPosition, null);
        var with = RunSupportTeam(content, offense, beacon, enemy, positions, beaconPosition, beaconTier);
        return new SupportResult(with.Damage - without.Damage, (with.Damage - without.Damage) / 20f);
    }

    private static SimulationResult RunSupportTeam(
        GameContent content,
        TowerDefinition offense,
        TowerDefinition beacon,
        EnemyDefinition enemy,
        IReadOnlyList<Vector2> offensePositions,
        Vector2 beaconPosition,
        TierRow? beaconTier)
    {
        var towers = beaconTier is null ? new[] { offense } : new[] { offense, beacon };
        var session = CreateSession(content, towers, new[] { enemy }, StationaryMap());
        foreach (var position in offensePositions)
            AddTower(session, offense, position, 0);
        if (beaconTier is { } tier)
            AddTower(session, beacon, beaconPosition, tier.LevelIndex, tier.SpecializationId, tier.DoctrineId);
        var target = AddEnemy(session, enemy);
        var metrics = new Metrics();
        session.DamageResolver.DamageApplied += metrics.Record;
        Simulate(session, 20);
        return metrics.ToResult(20, target);
    }

    private static GameSession CreateSession(GameContent content, IReadOnlyList<TowerDefinition> towers, IReadOnlyList<EnemyDefinition> enemies, MapDefinition map)
    {
        return new GameSession(new GameContent
        {
            Towers = towers.Distinct().ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Enemies = enemies.Distinct().ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Map = map,
            Waves = new WaveSetDefinition { Waves = new List<WaveDefinition>() }
        });
    }

    private static TowerInstance AddTower(
        GameSession session,
        TowerDefinition definition,
        Vector2 position,
        int levelIndex,
        string? specializationId = null,
        string? doctrineId = null)
    {
        var tower = new TowerInstance(session.Towers.Count + 1, definition, position);
        if (doctrineId is not null)
        {
            if (!tower.TryChooseDoctrine(doctrineId))
                throw new InvalidOperationException($"Could not create {definition.Id} doctrine {doctrineId}.");
            if (specializationId is not null && !tower.TrySpecialize(specializationId))
                throw new InvalidOperationException($"Could not create {definition.Id} doctrine {doctrineId} specialization {specializationId}.");
        }
        else if (specializationId is not null)
        {
            if (!tower.TryUpgrade() || !tower.TrySpecialize(specializationId))
                throw new InvalidOperationException($"Could not create {definition.Id} specialization {specializationId}.");
        }
        else
        {
            for (var level = 0; level < levelIndex; level++)
                if (!tower.TryUpgrade())
                    throw new InvalidOperationException($"Could not create {definition.Id} level {levelIndex + 1}.");
        }
        session.Towers.Add(tower);
        return tower;
    }

    internal static int ValidateTierConfigurations(GameContent content)
    {
        var count = 0;
        foreach (var definition in content.Towers.Values)
        {
            foreach (var row in TierRows(definition))
            {
                var session = CreateSession(content, new[] { definition }, content.Enemies.Values.Take(1).ToArray(), StationaryMap());
                var tower = AddTower(session, definition, new Vector2(400, 300), row.LevelIndex,
                    row.SpecializationId, row.DoctrineId);
                if (tower.LevelIndex != row.LevelIndex)
                    throw new InvalidOperationException($"Benchmark tier mismatch for {definition.Id} {row.Label}.");
                count++;
            }
        }
        return count;
    }

    private static EnemyInstance AddEnemy(GameSession session, EnemyDefinition definition)
    {
        var enemy = new EnemyInstance(session.Enemies.Count + 1, definition, session.Map.Path, 1, 1);
        session.Enemies.Add(enemy);
        return enemy;
    }

    private static void Simulate(GameSession session, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += StepSeconds)
            session.Update(MathF.Min(StepSeconds, seconds - elapsed));
    }

    private static MapDefinition StationaryMap() => new()
    {
        Id = "benchmark_stationary",
        Path = new List<PointData> { Point(400, 200), Point(401, 200) },
        BuildableRegions = new List<RectangleData> { new() { X = 300, Y = 250, Width = 200, Height = 120 } },
        Background = new BackgroundData()
    };

    private static MapDefinition MovingMap() => new()
    {
        Id = "benchmark_moving",
        Path = new List<PointData> { Point(40, 200), Point(920, 200) },
        BuildableRegions = new List<RectangleData> { new() { X = 20, Y = 250, Width = 900, Height = 120 } },
        Background = new BackgroundData()
    };

    private static MapDefinition SwarmMap() => new()
    {
        Id = "benchmark_swarm",
        Path = new List<PointData> { Point(300, 200), Point(920, 200) },
        BuildableRegions = new List<RectangleData> { new() { X = 20, Y = 250, Width = 900, Height = 120 } },
        Background = new BackgroundData()
    };

    private static EnemyDefinition EnemyLike(EnemyDefinition source, string id, float health, float speed, float armor, float shield, float regeneration) => new()
    {
        Id = id,
        DisplayName = id,
        MaxHealth = health,
        Speed = speed,
        Reward = source.Reward,
        LivesLost = 1,
        Armor = armor,
        Shield = shield,
        RegenerationPerSecond = regeneration,
        Visual = source.Visual
    };

    private static PointData Point(float x, float y) => new() { X = x, Y = y };

    private sealed record SupportResult(float AssistedDamage, float AssistedDps);
    private sealed record TowerBuild(TowerDefinition Definition, TierRow Tier);
    private sealed record TierRow(string Label, int LevelIndex, string? DoctrineId, string? SpecializationId,
        int CumulativeCost, int MarginalCost);

    private sealed class Metrics
    {
        public float Damage { get; private set; }
        public float ArmorAbsorbed { get; private set; }
        public float Overkill { get; private set; }
        public float Incoming { get; private set; }
        public int Hits { get; private set; }

        public int Kills { get; private set; }
        public int Leaks { get; private set; }

        public void Record(MinimalBastion.Combat.DamageReport report)
        {
            Damage += report.HealthDamage + report.ShieldDamage;
            Incoming += report.IncomingDamage;
            ArmorAbsorbed += report.ArmorAbsorbed;
            Overkill += report.Overkill;
            Hits++;
            if (report.Killed) Kills++;
        }

        public SimulationResult ToResult(float seconds, params EnemyInstance[] targets)
        {
            Leaks = targets.Count(x => x.HasEscaped);
            var survivors = targets.Count(x => !x.IsDead && !x.HasEscaped);
            var startingHealth = targets.Sum(x => x.MaxHealth);
            var remainingHealth = targets.Sum(x => x.IsDead ? 0 : x.Health);
            var healthRemovedPercent = startingHealth <= 0
                ? 0
                : MathHelper.Clamp((startingHealth - remainingHealth) / startingHealth * 100f, 0, 100);
            return new SimulationResult(Damage, Damage / MathF.Max(0.001f, seconds), Kills, survivors, Leaks,
                healthRemovedPercent, ArmorAbsorbed, Overkill, Incoming, Hits);
        }
    }

    private sealed record SimulationResult(float Damage, float DamagePerSecond, int Kills, int Survivors, int Leaks,
        float HealthRemovedPercent, float ArmorAbsorbed, float Overkill, float Incoming, int Hits);
}
