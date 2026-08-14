using System.Text.Json;
using MinimalBastion.Core;

namespace MinimalBastion.Data;

public sealed class ContentLoader
{
    private readonly string _root;

    public ContentLoader(string root)
    {
        _root = root;
    }

    public GameContent Load()
    {
        var towers = Read<List<TowerDefinition>>("Towers.json");
        var enemies = Read<List<EnemyDefinition>>("Enemies.json");
        var tactics = Read<TacticsDefinition>("Tactics.json");
        var maps = LoadMaps();
        var waveSets = LoadWaveSets();
        if (!maps.TryGetValue("foundry_loop", out var map)) throw new InvalidDataException("No foundry_loop map was found.");
        if (!waveSets.TryGetValue(map.WaveSet, out var waves)) throw new InvalidDataException($"No wave set found for map {map.Id}: {map.WaveSet}");
        foreach (var candidateMap in maps.Values)
        {
            if (!waveSets.TryGetValue(candidateMap.WaveSet, out var candidateWaves))
                throw new InvalidDataException($"No wave set found for map {candidateMap.Id}: {candidateMap.WaveSet}");
            DataValidator.Validate(towers, enemies, candidateMap, candidateWaves, tactics);
        }

        return new GameContent
        {
            Towers = towers.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Enemies = enemies.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Map = map,
            Waves = waves,
            Maps = maps,
            WaveSets = waveSets,
            Tactics = tactics
        };
    }

    private Dictionary<string, MapDefinition> LoadMaps()
    {
        var result = new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(_root, "Maps");
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(path), ContentJson.Options);
            if (map is not null && !string.IsNullOrWhiteSpace(map.Id) && map.Path.Count >= 2)
                result[map.Id] = map;
        }
        return result;
    }

    private Dictionary<string, WaveSetDefinition> LoadWaveSets()
    {
        var result = new Dictionary<string, WaveSetDefinition>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(_root, "Maps");
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var waves = JsonSerializer.Deserialize<WaveSetDefinition>(File.ReadAllText(path), ContentJson.Options);
            if (waves is not null && !string.IsNullOrWhiteSpace(waves.MapId) && waves.Waves.Count > 0)
            {
                result[Path.GetFileNameWithoutExtension(path)] = waves;
                result[waves.MapId] = waves;
                if (!string.IsNullOrWhiteSpace(waves.Id)) result[waves.Id] = waves;
            }
        }
        return result;
    }

    private T Read<T>(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        if (!File.Exists(path)) throw new FileNotFoundException($"Missing content file: {path}", path);
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), ContentJson.Options);
        return value ?? throw new InvalidDataException($"Content file is empty or invalid: {path}");
    }
}

public static class DataValidator
{
    public static void Validate(
        IReadOnlyList<TowerDefinition> towers,
        IReadOnlyList<EnemyDefinition> enemies,
        MapDefinition map,
        WaveSetDefinition waves,
        TacticsDefinition tactics)
    {
        if (towers.Count != 10) throw new InvalidDataException("Version 1 requires exactly 10 towers.");
        if (enemies.Count != 5) throw new InvalidDataException("Version 1 requires exactly 5 enemy tiers.");
        RequireUnique(towers.Select(x => x.Id), "tower");
        RequireUnique(enemies.Select(x => x.Id), "enemy");
        if (map.Path.Count < 2) throw new InvalidDataException("Map path needs at least two points.");
        if (map.PowerNodes.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Radius <= 0 || x.AttackSpeedBonus < 0 || x.RangeBonus < 0 ||
            x.DamageBonus < 0 || x.ArmorPierceBonus < 0 ||
            x.AttackSpeedBonus + x.RangeBonus + x.DamageBonus + x.ArmorPierceBonus <= 0))
            throw new InvalidDataException($"Invalid power node in map: {map.Id}");
        RequireUnique(map.PowerNodes.Select(x => x.Id), "power node");
        if (waves.Waves.Count != 20) throw new InvalidDataException("Version 1 requires exactly 20 waves.");
        if (tactics.EmergencyDefense.PurchaseCost <= 0 || tactics.EmergencyDefense.Charges <= 0 ||
            tactics.EmergencyDefense.Damage <= 0 || tactics.EmergencyDefense.BlastRadius <= tactics.EmergencyDefense.TriggerRadius)
            throw new InvalidDataException("Invalid emergency-defense definition.");
        if (tactics.Generator.PurchaseCost <= 0 || tactics.Generator.Levels.Count != 3 ||
            tactics.Generator.Levels.Any(x => x.ProductionSeconds <= 0 || x.Capacity <= 0 || x.DefenseDamageBonus < 0))
            throw new InvalidDataException("Invalid emergency generator definition.");
        var enemyIds = enemies.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < towers.Count; i++)
        {
            var tower = towers[i];
            if (tower.PurchaseCost <= 0 || tower.Levels.Count != 3) throw new InvalidDataException($"Invalid tower: {tower.Id}");
            if (tower.Levels.Any(x => x.Range < 0 || (!tower.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && x.Range <= 0) || x.AttacksPerSecond < 0 || x.Damage < 0))
                throw new InvalidDataException($"Invalid tower levels: {tower.Id}");
            if (tower.Specializations.Count is not 0 and not 2 ||
                tower.Specializations.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.DisplayName) ||
                    string.IsNullOrWhiteSpace(x.ShortLabel) || x.UpgradeCost <= 0 || x.Level.Range <= 0 ||
                    x.Level.AttacksPerSecond <= 0 || x.Level.Damage < 0) ||
                tower.Specializations.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tower.Specializations.Count)
                throw new InvalidDataException($"Invalid tower specializations: {tower.Id}");
        }

        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy.MaxHealth <= 0 || enemy.Speed <= 0 || enemy.Reward < 0 || enemy.LivesLost <= 0)
                throw new InvalidDataException($"Invalid enemy: {enemy.Id}");
        }

        for (var index = 0; index < waves.Waves.Count; index++)
        {
            var wave = waves.Waves[index];
            if (wave.Number != index + 1 || wave.Groups.Count == 0) throw new InvalidDataException($"Invalid wave number/groups: {wave.Number}");
            foreach (var group in wave.Groups)
            {
                if (!enemyIds.Contains(group.EnemyId) || group.Count <= 0 || group.SpawnInterval <= 0 || group.DelayBefore < 0)
                    throw new InvalidDataException($"Invalid wave group in wave {wave.Number}: {group.EnemyId}");
                if (!Enum.TryParse<EnemyRank>(group.Rank, true, out _))
                    throw new InvalidDataException($"Invalid enemy rank in wave {wave.Number}: {group.Rank}");
            }
        }
    }

    private static void RequireUnique(IEnumerable<string> ids, string kind)
    {
        var list = ids.ToList();
        if (list.Any(string.IsNullOrWhiteSpace) || list.Count != list.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException($"Duplicate or empty {kind} IDs.");
    }
}
