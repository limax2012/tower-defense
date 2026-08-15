using System.Text.Json;
using MinimalBastion.Core;
using MinimalBastion.Effects;
using Microsoft.Xna.Framework;

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
        var difficulties = Read<List<DifficultyDefinition>>("Difficulties.json");
        var challenges = Read<List<ChallengeDefinition>>("Challenges.json");
        var maps = LoadMaps();
        var waveSets = LoadWaveSets();
        if (!maps.TryGetValue("foundry_loop", out var map)) throw new InvalidDataException("No foundry_loop map was found.");
        if (!waveSets.TryGetValue(map.WaveSet, out var waves)) throw new InvalidDataException($"No wave set found for map {map.Id}: {map.WaveSet}");
        foreach (var candidateMap in maps.Values)
        {
            if (!waveSets.TryGetValue(candidateMap.WaveSet, out var candidateWaves))
                throw new InvalidDataException($"No wave set found for map {candidateMap.Id}: {candidateMap.WaveSet}");
            if (!candidateWaves.MapId.Equals(candidateMap.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Wave set {candidateWaves.Id} belongs to {candidateWaves.MapId}, not map {candidateMap.Id}.");
            DataValidator.Validate(towers, enemies, candidateMap, candidateWaves, tactics);
        }
        ValidateIndependentCampaigns(maps, waveSets);
        DataValidator.ValidateDifficulties(difficulties);
        DataValidator.ValidateChallenges(challenges, towers);

        return new GameContent
        {
            Towers = towers.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Enemies = enemies.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Map = map,
            Waves = waves,
            Maps = maps,
            WaveSets = waveSets,
            Difficulties = difficulties.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Challenges = challenges.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase),
            Tactics = tactics
        };
    }

    private static void ValidateIndependentCampaigns(
        IReadOnlyDictionary<string, MapDefinition> maps,
        IReadOnlyDictionary<string, WaveSetDefinition> waveSets)
    {
        var assignments = maps.Values.Select(map => new
        {
            Map = map,
            Waves = waveSets[map.WaveSet],
            Signature = JsonSerializer.Serialize(waveSets[map.WaveSet].Waves, ContentJson.Options)
        }).ToArray();

        var reusedIdentity = assignments
            .GroupBy(x => x.Waves.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (reusedIdentity is not null)
            throw new InvalidDataException($"Wave set {reusedIdentity.Key} is assigned to multiple maps; each arena requires its own campaign.");

        var duplicatedRoster = assignments
            .GroupBy(x => x.Signature, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatedRoster is not null)
            throw new InvalidDataException($"Maps {string.Join(", ", duplicatedRoster.Select(x => x.Map.Id))} use identical wave rosters; each arena requires independently authored waves.");
    }

    private Dictionary<string, MapDefinition> LoadMaps()
    {
        var result = new Dictionary<string, MapDefinition>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(_root, "Maps");
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!HasTopLevelProperty(path, "path")) continue;
            var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(path), ContentJson.Options);
            if (map is null || string.IsNullOrWhiteSpace(map.Id) || map.Path.Count < 2)
                throw new InvalidDataException($"Invalid map definition: {path}");
            if (!result.TryAdd(map.Id, map))
                throw new InvalidDataException($"Duplicate map ID: {map.Id}");
        }
        return result;
    }

    private Dictionary<string, WaveSetDefinition> LoadWaveSets()
    {
        var result = new Dictionary<string, WaveSetDefinition>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(_root, "Maps");
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!HasTopLevelProperty(path, "waves")) continue;
            var waves = JsonSerializer.Deserialize<WaveSetDefinition>(File.ReadAllText(path), ContentJson.Options);
            if (waves is null || string.IsNullOrWhiteSpace(waves.Id) || string.IsNullOrWhiteSpace(waves.MapId) || waves.Waves.Count == 0)
                throw new InvalidDataException($"Invalid wave-set definition: {path}");
            AddWaveSetAlias(result, Path.GetFileNameWithoutExtension(path), waves);
            AddWaveSetAlias(result, waves.MapId, waves);
            AddWaveSetAlias(result, waves.Id, waves);
        }
        return result;
    }

    private static void AddWaveSetAlias(Dictionary<string, WaveSetDefinition> result, string alias, WaveSetDefinition waves)
    {
        if (result.TryGetValue(alias, out var existing) && !ReferenceEquals(existing, waves))
            throw new InvalidDataException($"Duplicate wave-set identity: {alias}");
        result[alias] = waves;
    }

    private static bool HasTopLevelProperty(string path, string propertyName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        return document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.EnumerateObject().Any(property => property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
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
    public static void ValidateChallenges(IReadOnlyList<ChallengeDefinition> challenges, IReadOnlyList<TowerDefinition> towers)
    {
        if (challenges.Count < 2) throw new InvalidDataException("At least two challenge directives are required.");
        RequireUnique(challenges.Select(x => x.Id), "challenge");
        if (!challenges.Any(x => x.Id.Equals(ChallengeCatalog.DefaultId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Challenge directives must include standard.");
        var towerIds = towers.Select(tower => tower.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (challenges.Any(challenge => string.IsNullOrWhiteSpace(challenge.DisplayName) ||
            string.IsNullOrWhiteSpace(challenge.MenuLabel) || challenge.StartingCreditsMultiplier <= 0 ||
            challenge.ExcludedTowerIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != challenge.ExcludedTowerIds.Count ||
            challenge.ExcludedTowerIds.Any(id => !towerIds.Contains(id)) ||
            challenge.ExcludedTowerIds.Count >= towers.Count))
            throw new InvalidDataException("Invalid challenge directive.");
    }

    public static void ValidateDifficulties(IReadOnlyList<DifficultyDefinition> difficulties)
    {
        if (difficulties.Count < 3) throw new InvalidDataException("At least three difficulty profiles are required.");
        RequireUnique(difficulties.Select(x => x.Id), "difficulty");
        if (!difficulties.Any(x => x.Id.Equals(DifficultyCatalog.DefaultId, StringComparison.OrdinalIgnoreCase)) ||
            !difficulties.Any(x => x.Id.Equals(DifficultyCatalog.LegacyId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Difficulty profiles must include normal and hard.");
        if (difficulties.Any(x => string.IsNullOrWhiteSpace(x.DisplayName) || x.EnemyHealthMultiplier <= 0 ||
            x.EnemySpeedMultiplier <= 0 || x.StartingCreditsMultiplier < 0 || x.StartingLives <= 0))
            throw new InvalidDataException("Invalid difficulty profile.");
    }

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
        if (map.SchemaVersion != 1 || waves.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported content schema for map {map.Id} or wave set {waves.Id}.");
        if (string.IsNullOrWhiteSpace(map.DisplayName) || string.IsNullOrWhiteSpace(map.Description) || string.IsNullOrWhiteSpace(map.WaveSet) ||
            map.LogicalSize.Width != GameConstants.MapWidth || map.LogicalSize.Height != GameConstants.LogicalHeight ||
            map.PathWidth <= 0 || map.StartingLives <= 0 || map.StartingCredits <= 0 || map.Path.Count < 2)
            throw new InvalidDataException($"Invalid map fundamentals: {map.Id}");
        if (map.BuildableRegions.Count == 0 || map.BuildableRegions.Any(region => !ValidMapRegion(region, map.LogicalSize)))
            throw new InvalidDataException($"Invalid buildable region in map: {map.Id}");
        if (map.BuildableRegions.Any(region => !ValidTowerCenterRegion(region, map.LogicalSize) ||
                MinimumPathDistance(region, map.Path) < GameConstants.PlacementPathClearance))
            throw new InvalidDataException($"Buildable region advertises an invalid tower center in map: {map.Id}");
        if (map.RestrictedRegions.Any(region => !ValidMapRegion(region, map.LogicalSize)))
            throw new InvalidDataException($"Invalid restricted region in map: {map.Id}");
        if (map.Path.Any(point => point.X < -map.PathWidth || point.X > map.LogicalSize.Width + map.PathWidth ||
                point.Y < 0 || point.Y > map.LogicalSize.Height) ||
            map.Path.Zip(map.Path.Skip(1)).Any(segment => Vector2.DistanceSquared(segment.First.ToVector2(), segment.Second.ToVector2()) < 1f) ||
            Vector2.DistanceSquared(map.Spawn.ToVector2(), map.Path[0].ToVector2()) > 1f ||
            Vector2.DistanceSquared(map.Goal.ToVector2(), map.Path[^1].ToVector2()) > 1f)
            throw new InvalidDataException($"Invalid route geometry in map: {map.Id}");
        if (map.ChallengeRating is < 1 or > 5 ||
            !map.PathVisual.Style.Equals("road", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("conduit", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("channel", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("foundry", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("trail", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("prism", StringComparison.OrdinalIgnoreCase) &&
            !map.PathVisual.Style.Equals("surge", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Invalid map presentation: {map.Id}");
        if (map.PowerNodes.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Radius <= 0 || x.AttackSpeedBonus < 0 || x.RangeBonus < 0 ||
            x.DamageBonus < 0 || x.ArmorPierceBonus < 0 ||
            x.Position.X < 0 || x.Position.X > map.LogicalSize.Width || x.Position.Y < 0 || x.Position.Y > map.LogicalSize.Height ||
            x.AttackSpeedBonus + x.RangeBonus + x.DamageBonus + x.ArmorPierceBonus <= 0))
            throw new InvalidDataException($"Invalid power node in map: {map.Id}");
        if (map.PowerNodes.Any(node => !MapPositionIsBuildable(node.Position, map)))
            throw new InvalidDataException($"Power node center is not a valid tower position in map: {map.Id}");
        RequireUnique(map.PowerNodes.Select(x => x.Id), "power node");
        if (waves.Waves.Count != 20) throw new InvalidDataException("Version 1 requires exactly 20 waves.");
        if (tactics.EmergencyDefense.PurchaseCost <= 0 || tactics.EmergencyDefense.DirectPurchaseCostIncrease < 0 ||
            tactics.EmergencyDefense.MaximumActive <= 0 || tactics.EmergencyDefense.Charges <= 0 ||
            tactics.EmergencyDefense.Damage <= 0 || tactics.EmergencyDefense.BlastRadius <= tactics.EmergencyDefense.TriggerRadius ||
            tactics.EmergencyDefense.SlowPercent < 0 || tactics.EmergencyDefense.SlowPercent >= 1 ||
            tactics.EmergencyDefense.SlowDuration < 0 || tactics.EmergencyDefense.KnockbackDistance < 0 ||
            tactics.EmergencyDefense.KnockbackGraceSeconds < 0 ||
            tactics.EmergencyDefense.EliteKnockbackMultiplier is < 0 or > 1 ||
            tactics.EmergencyDefense.BossKnockbackMultiplier is < 0 or > 1 ||
            tactics.EmergencyDefense.PlacementRoadTolerance < 0 || tactics.EmergencyDefense.MinimumSpacing < 0 ||
            tactics.EmergencyDefense.EndpointClearance < 0)
            throw new InvalidDataException("Invalid emergency-defense definition.");
        if (tactics.Generator.PurchaseCost <= 0 || tactics.Generator.Levels.Count != 3 ||
            tactics.Generator.Levels.Any(x => x.ProductionSeconds <= 0 || x.Capacity <= 0 || x.DefenseDamageBonus < 0))
            throw new InvalidDataException("Invalid emergency generator definition.");
        var enemyIds = enemies.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < towers.Count; i++)
        {
            var tower = towers[i];
            if (tower.PurchaseCost <= 0 || tower.Levels.Count != 3) throw new InvalidDataException($"Invalid tower: {tower.Id}");
            if (tower.Levels.Any(x => x.Range < 0 || (!tower.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase) && x.Range <= 0) || x.AttacksPerSecond < 0 || x.Damage < 0 || x.SplashTargetLimit < 0 || x.PriorityDamageMultiplier is < 1f or > 3f ||
                x.HomingSplash && (x.SplashRadius <= 0 || x.SplashTargetLimit <= 0)))
                throw new InvalidDataException($"Invalid tower levels: {tower.Id}");
            var protocol = tower.Protocol;
            if (string.IsNullOrWhiteSpace(protocol.DisplayName) || string.IsNullOrWhiteSpace(protocol.Summary) ||
                protocol.DurationSeconds <= 0 || protocol.CooldownSeconds < protocol.DurationSeconds ||
                protocol.AutoTriggerCount <= 0 || protocol.AttackSpeedBonus < 0 || protocol.DamageBonus < 0 ||
                protocol.RangeBonus < 0 || protocol.ArmorPierceBonus < 0 || protocol.AuraAttackSpeedBonus < 0 ||
                protocol.AuraRangeBonus < 0 || protocol.BurstRadius < 0 || protocol.BurstDamage < 0 ||
                protocol.BurstStatusMagnitude < 0 || protocol.BurstStatusDuration < 0 ||
                (!string.IsNullOrWhiteSpace(protocol.BurstStatus) && !Enum.TryParse<StatusType>(protocol.BurstStatus, true, out _)))
                throw new InvalidDataException($"Invalid tower protocol: {tower.Id}");
            if (tower.Tier2Doctrines.Count is not 0 and not 2 ||
                tower.Tier2Doctrines.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.DisplayName) ||
                    string.IsNullOrWhiteSpace(x.ShortLabel) || string.IsNullOrWhiteSpace(x.Summary) || x.UpgradeCost <= 0 ||
                    x.DamageMultiplier is < 0.5f or > 1.5f || x.AttackSpeedMultiplier is < 0.5f or > 1.5f ||
                    x.RangeMultiplier is < 0.5f or > 1.5f || x.UtilityMultiplier is < 0.5f or > 1.5f ||
                    x.PelletCountBonus is < 0 or > 2 || x.ChainCountBonus is < 0 or > 2 || x.SplashTargetLimitBonus is < 0 or > 2) ||
                tower.Tier2Doctrines.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tower.Tier2Doctrines.Count)
                throw new InvalidDataException($"Invalid tower doctrines: {tower.Id}");
            var specializationNeedsCombatStats = !tower.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase);
            if (tower.Specializations.Count is not 0 and not 2 ||
                tower.Specializations.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.DisplayName) ||
                    string.IsNullOrWhiteSpace(x.ShortLabel) || x.UpgradeCost <= 0 ||
                    specializationNeedsCombatStats && (x.Level.Range <= 0 || x.Level.AttacksPerSecond <= 0 || x.Level.Damage < 0 || x.Level.SplashTargetLimit < 0 || x.Level.PriorityDamageMultiplier is < 1f or > 3f ||
                        x.Level.HomingSplash && (x.Level.SplashRadius <= 0 || x.Level.SplashTargetLimit <= 0)) ||
                    !specializationNeedsCombatStats && (x.Level.AuraRange <= 0 || x.Level.AuraAttackSpeedBonus < 0 || x.Level.AuraRangeBonus < 0)) ||
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
            if (wave.Number != index + 1 || wave.Groups.Count == 0 || wave.HealthMultiplier <= 0 || wave.SpeedMultiplier <= 0 ||
                string.IsNullOrWhiteSpace(wave.Archetype) || string.IsNullOrWhiteSpace(wave.Briefing))
                throw new InvalidDataException($"Invalid wave number/scaling/groups: {wave.Number}");
            foreach (var group in wave.Groups)
            {
                if (!enemyIds.Contains(group.EnemyId) || group.Count <= 0 || group.SpawnInterval <= 0 || group.DelayBefore < 0)
                    throw new InvalidDataException($"Invalid wave group in wave {wave.Number}: {group.EnemyId}");
                if (!Enum.TryParse<EnemyRank>(group.Rank, true, out _))
                    throw new InvalidDataException($"Invalid enemy rank in wave {wave.Number}: {group.Rank}");
            }
        }
    }

    private static bool ValidMapRegion(RectangleData region, LogicalSizeData size) =>
        region.Width > 0 && region.Height > 0 && region.X >= 0 && region.Y >= 0 &&
        region.X + region.Width <= size.Width && region.Y + region.Height <= size.Height;

    private static bool ValidTowerCenterRegion(RectangleData region, LogicalSizeData size) =>
        region.X >= GameConstants.TowerRadius &&
        region.Y >= GameConstants.TopBarHeight + GameConstants.TowerRadius &&
        region.X + region.Width <= size.Width - GameConstants.TowerRadius &&
        region.Y + region.Height <= size.Height - GameConstants.TowerRadius;

    private static bool MapPositionIsBuildable(PointData position, MapDefinition map) =>
        map.BuildableRegions.Any(region => PointInRectangle(
            position.ToVector2(), region.X, region.X + region.Width, region.Y, region.Y + region.Height)) &&
        !map.RestrictedRegions.Any(region => PointInRectangle(
            position.ToVector2(), region.X, region.X + region.Width, region.Y, region.Y + region.Height));

    private static float MinimumPathDistance(RectangleData region, IReadOnlyList<PointData> path)
    {
        var minimum = float.MaxValue;
        for (var index = 0; index < path.Count - 1; index++)
            minimum = MathF.Min(minimum, SegmentRectangleDistance(path[index].ToVector2(), path[index + 1].ToVector2(), region));
        return minimum;
    }

    private static float SegmentRectangleDistance(Vector2 start, Vector2 end, RectangleData region)
    {
        var left = region.X;
        var right = region.X + region.Width;
        var top = region.Y;
        var bottom = region.Y + region.Height;
        var corners = new[]
        {
            new Vector2(left, top), new Vector2(right, top),
            new Vector2(right, bottom), new Vector2(left, bottom)
        };

        if (PointInRectangle(start, left, right, top, bottom) || PointInRectangle(end, left, right, top, bottom)) return 0;
        for (var index = 0; index < corners.Length; index++)
            if (SegmentsIntersect(start, end, corners[index], corners[(index + 1) % corners.Length])) return 0;

        var minimum = MathF.Min(PointRectangleDistance(start, left, right, top, bottom),
            PointRectangleDistance(end, left, right, top, bottom));
        foreach (var corner in corners) minimum = MathF.Min(minimum, PointSegmentDistance(corner, start, end));
        return minimum;
    }

    private static bool PointInRectangle(Vector2 point, float left, float right, float top, float bottom) =>
        point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;

    private static float PointRectangleDistance(Vector2 point, float left, float right, float top, float bottom)
    {
        var dx = MathF.Max(MathF.Max(left - point.X, 0), point.X - right);
        var dy = MathF.Max(MathF.Max(top - point.Y, 0), point.Y - bottom);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float PointSegmentDistance(Vector2 point, Vector2 start, Vector2 end)
    {
        var delta = end - start;
        var lengthSquared = delta.LengthSquared();
        var amount = lengthSquared <= 0.0001f ? 0 : MathHelper.Clamp(Vector2.Dot(point - start, delta) / lengthSquared, 0, 1);
        return Vector2.Distance(point, start + delta * amount);
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        if ((abC > 0 && abD < 0 || abC < 0 && abD > 0) &&
            (cdA > 0 && cdB < 0 || cdA < 0 && cdB > 0)) return true;
        const float epsilon = 0.001f;
        return MathF.Abs(abC) <= epsilon && OnSegment(a, b, c) ||
               MathF.Abs(abD) <= epsilon && OnSegment(a, b, d) ||
               MathF.Abs(cdA) <= epsilon && OnSegment(c, d, a) ||
               MathF.Abs(cdB) <= epsilon && OnSegment(c, d, b);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 point) =>
        point.X >= MathF.Min(a.X, b.X) && point.X <= MathF.Max(a.X, b.X) &&
        point.Y >= MathF.Min(a.Y, b.Y) && point.Y <= MathF.Max(a.Y, b.Y);

    private static void RequireUnique(IEnumerable<string> ids, string kind)
    {
        var list = ids.ToList();
        if (list.Any(string.IsNullOrWhiteSpace) || list.Count != list.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException($"Duplicate or empty {kind} IDs.");
    }
}
