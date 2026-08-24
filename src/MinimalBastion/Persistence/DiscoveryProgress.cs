using System.Text.Json;
using MinimalBastion.Core;
using MinimalBastion.Enemies;
using MinimalBastion.Effects;

namespace MinimalBastion.Persistence;

public sealed class DiscoveryProgressData
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<string> Towers { get; set; } = new();
    public Dictionary<string, int> TowerLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> TowerDoctrines { get; set; } = new();
    public List<string> TowerSpecializations { get; set; } = new();
    public List<string> TowerProtocols { get; set; } = new();
    public List<string> ApexTowers { get; set; } = new();
    public List<string> Enemies { get; set; } = new();
    public List<string> EnemySignalRoles { get; set; } = new();
    public List<string> EnemyRanks { get; set; } = new();
    public List<string> Statuses { get; set; } = new();
    public List<string> Maps { get; set; } = new();
    public Dictionary<string, int> MapWaves { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Difficulties { get; set; } = new();
    public List<string> Challenges { get; set; } = new();
    public List<string> Mechanics { get; set; } = new();
}

public sealed record DiscoverySnapshot(
    IReadOnlySet<string> Towers,
    IReadOnlyDictionary<string, int> TowerLevels,
    IReadOnlySet<string> TowerDoctrines,
    IReadOnlySet<string> TowerSpecializations,
    IReadOnlySet<string> TowerProtocols,
    IReadOnlySet<string> ApexTowers,
    IReadOnlySet<string> Enemies,
    IReadOnlySet<string> EnemySignalRoles,
    IReadOnlySet<string> EnemyRanks,
    IReadOnlySet<string> Statuses,
    IReadOnlySet<string> Maps,
    IReadOnlyDictionary<string, int> MapWaves,
    IReadOnlySet<string> Difficulties,
    IReadOnlySet<string> Challenges,
    IReadOnlySet<string> Mechanics)
{
    public static DiscoverySnapshot Everything { get; } = new(
        new WildcardSet(), new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["*"] = int.MaxValue },
        new WildcardSet(), new WildcardSet(), new WildcardSet(), new WildcardSet(), new WildcardSet(),
        new WildcardSet(), new WildcardSet(), new WildcardSet(), new WildcardSet(),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["*"] = int.MaxValue },
        new WildcardSet(), new WildcardSet(), new WildcardSet());

    public bool Has(IReadOnlySet<string> values, string id) => values.Contains("*") || values.Contains(id);
    public int HighestTowerLevel(string towerId) => TowerLevels.TryGetValue(towerId, out var level)
        ? level
        : TowerLevels.GetValueOrDefault("*");
    public int HighestMapWave(string mapId) => MapWaves.TryGetValue(mapId, out var wave)
        ? wave
        : MapWaves.GetValueOrDefault("*");

    private sealed class WildcardSet : IReadOnlySet<string>
    {
        public int Count => 1;
        public bool Contains(string item) => true;
        public bool IsProperSubsetOf(IEnumerable<string> other) => false;
        public bool IsProperSupersetOf(IEnumerable<string> other) => true;
        public bool IsSubsetOf(IEnumerable<string> other) => false;
        public bool IsSupersetOf(IEnumerable<string> other) => true;
        public bool Overlaps(IEnumerable<string> other) => true;
        public bool SetEquals(IEnumerable<string> other) => false;
        public IEnumerator<string> GetEnumerator() => new[] { "*" }.AsEnumerable().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public sealed class DiscoveryProgress
{
    public const string TargetingMechanic = "targeting";
    public const string UpgradeMechanic = "upgrades";
    public const string ProtocolMechanic = "protocols";
    public const string AutoProtocolMechanic = "auto_protocol";
    public const string PlateMechanic = "plates";
    public const string ForgeMechanic = "forge";
    public const string BeaconMechanic = "beacon";
    public const string SurgeNodeMechanic = "surge_nodes";
    public const string CoOpMechanic = "coop";

    private readonly HashSet<string> _towers;
    private readonly Dictionary<string, int> _towerLevels;
    private readonly HashSet<string> _towerDoctrines;
    private readonly HashSet<string> _towerSpecializations;
    private readonly HashSet<string> _towerProtocols;
    private readonly HashSet<string> _apexTowers;
    private readonly HashSet<string> _enemies;
    private readonly HashSet<string> _enemySignalRoles;
    private readonly HashSet<string> _enemyRanks;
    private readonly HashSet<string> _statuses;
    private readonly HashSet<string> _maps;
    private readonly Dictionary<string, int> _mapWaves;
    private readonly HashSet<string> _difficulties;
    private readonly HashSet<string> _challenges;
    private readonly HashSet<string> _mechanics;

    public DiscoveryProgress(DiscoveryProgressData? data = null)
    {
        data ??= new DiscoveryProgressData();
        _towers = Set(data.Towers);
        _towerLevels = Map(data.TowerLevels);
        _towerDoctrines = Set(data.TowerDoctrines);
        _towerSpecializations = Set(data.TowerSpecializations);
        _towerProtocols = Set(data.TowerProtocols);
        _apexTowers = Set(data.ApexTowers);
        _enemies = Set(data.Enemies);
        _enemySignalRoles = Set(data.EnemySignalRoles);
        _enemyRanks = Set(data.EnemyRanks);
        _statuses = Set(data.Statuses);
        _maps = Set(data.Maps);
        _mapWaves = Map(data.MapWaves);
        _difficulties = Set(data.Difficulties);
        _challenges = Set(data.Challenges);
        _mechanics = Set(data.Mechanics);
    }

    public DiscoverySnapshot Snapshot() => new(
        Copy(_towers), new Dictionary<string, int>(_towerLevels, StringComparer.OrdinalIgnoreCase),
        Copy(_towerDoctrines), Copy(_towerSpecializations), Copy(_towerProtocols), Copy(_apexTowers),
        Copy(_enemies), Copy(_enemySignalRoles), Copy(_enemyRanks), Copy(_statuses), Copy(_maps),
        new Dictionary<string, int>(_mapWaves, StringComparer.OrdinalIgnoreCase),
        Copy(_difficulties), Copy(_challenges), Copy(_mechanics));

    public DiscoveryProgressData Capture() => new()
    {
        Towers = Sorted(_towers),
        TowerLevels = new Dictionary<string, int>(_towerLevels, StringComparer.OrdinalIgnoreCase),
        TowerDoctrines = Sorted(_towerDoctrines),
        TowerSpecializations = Sorted(_towerSpecializations),
        TowerProtocols = Sorted(_towerProtocols),
        ApexTowers = Sorted(_apexTowers),
        Enemies = Sorted(_enemies),
        EnemySignalRoles = Sorted(_enemySignalRoles),
        EnemyRanks = Sorted(_enemyRanks),
        Statuses = Sorted(_statuses),
        Maps = Sorted(_maps),
        MapWaves = new Dictionary<string, int>(_mapWaves, StringComparer.OrdinalIgnoreCase),
        Difficulties = Sorted(_difficulties),
        Challenges = Sorted(_challenges),
        Mechanics = Sorted(_mechanics)
    };

    public bool Observe(GameSession session)
    {
        var changed = DiscoverRunProfile(session.Map.Definition.Id, session.DifficultyId, session.ChallengeId,
            session.CurrentWave, session.IsCoOp);
        foreach (var enemy in session.Enemies) changed |= Discover(enemy);
        foreach (var enemy in session.Statistics.Enemies) changed |= Add(_enemies, enemy.EnemyId);
        foreach (var tower in session.Towers)
        {
            changed |= Add(_towers, tower.Definition.Id);
            changed |= Raise(_towerLevels, tower.Definition.Id, tower.LevelIndex + 1);
            changed |= AddBranch(_towerDoctrines, tower.Definition.Id, tower.DoctrineId);
            changed |= AddBranch(_towerSpecializations, tower.Definition.Id, tower.SpecializationId);
            if (tower.IsApex) changed |= Add(_apexTowers, tower.Definition.Id);
            if (session.Map.GetPowerBuff(tower.Position).IsPowered) changed |= Add(_mechanics, SurgeNodeMechanic);
            if (tower.IsSupport) changed |= Add(_mechanics, BeaconMechanic);
        }

        if (session.Towers.Any()) changed |= Add(_mechanics, TargetingMechanic);
        if (session.Towers.Any(tower => tower.LevelIndex > 0)) changed |= Add(_mechanics, UpgradeMechanic);
        if (session.Statistics.ProtocolActivations > 0)
        {
            changed |= Add(_mechanics, ProtocolMechanic);
            foreach (var tower in session.Statistics.Towers.Where(tower => tower.Overdrives > 0))
                changed |= Add(_towerProtocols, tower.TowerId);
        }
        if (session.AutoOverdriveTowerId > 0) changed |= Add(_mechanics, AutoProtocolMechanic);
        if (session.Statistics.EmergencyDeployments > 0 || session.EmergencyDefenses.Count > 0)
            changed |= Add(_mechanics, PlateMechanic);
        if (session.Statistics.GeneratorPurchases > 0 || session.Generator is not null)
            changed |= Add(_mechanics, ForgeMechanic);
        return changed;
    }

    public bool Discover(EnemyInstance enemy)
    {
        var changed = Add(_enemies, enemy.Definition.Id);
        changed |= Add(_enemyRanks, enemy.Rank.ToString());
        if (enemy.SignalRole != EnemySignalRole.None)
            changed |= Add(_enemySignalRoles, enemy.SignalRole.ToString());
        foreach (var status in enemy.StatusEffects.Active)
            changed |= Add(_statuses, status.Type.ToString());
        return changed;
    }

    public bool Import(IEnumerable<RunHistoryEntry> entries)
    {
        var changed = false;
        foreach (var entry in entries)
        {
            changed |= DiscoverRunProfile(entry.MapId, entry.DifficultyId, entry.ChallengeId, entry.CurrentWave, entry.IsCoOp);
            foreach (var enemy in entry.Enemies) changed |= Add(_enemies, enemy.EnemyId);
            foreach (var tower in entry.Towers.Where(tower => tower.Purchases > 0))
            {
                changed |= Add(_towers, tower.TowerId);
                if (tower.Upgrades > 0) changed |= Add(_mechanics, UpgradeMechanic);
                if (tower.ProtocolActivations > 0) changed |= Add(_towerProtocols, tower.TowerId);
                if (tower.ExposeSeconds > 0) changed |= Add(_statuses, StatusType.Exposed.ToString());
                if (tower.ArmorBreakSeconds > 0) changed |= Add(_statuses, StatusType.ArmorBreak.ToString());
                if (tower.TowerId.Equals("signal_beacon", StringComparison.OrdinalIgnoreCase))
                    changed |= Add(_mechanics, BeaconMechanic);
            }
            if (entry.Towers.Any(tower => tower.Purchases > 0)) changed |= Add(_mechanics, TargetingMechanic);
            if (entry.ProtocolActivations > 0) changed |= Add(_mechanics, ProtocolMechanic);
            if (entry.PlateDeployments > 0) changed |= Add(_mechanics, PlateMechanic);
            if (entry.ForgePurchases > 0) changed |= Add(_mechanics, ForgeMechanic);
            if (entry.FinalLayout is null) continue;
            foreach (var tower in entry.FinalLayout.Towers)
            {
                changed |= Add(_towers, tower.DefinitionId);
                changed |= Raise(_towerLevels, tower.DefinitionId, tower.LevelIndex + 1);
                changed |= AddBranch(_towerDoctrines, tower.DefinitionId, tower.DoctrineId);
                changed |= AddBranch(_towerSpecializations, tower.DefinitionId, tower.SpecializationId);
                if (tower.IsApex) changed |= Add(_apexTowers, tower.DefinitionId);
            }
        }
        return changed;
    }

    private bool DiscoverRunProfile(string mapId, string difficultyId, string challengeId, int wave, bool coOp)
    {
        var changed = Add(_maps, mapId) | Add(_difficulties, difficultyId) | Add(_challenges, challengeId);
        changed |= Raise(_mapWaves, mapId, Math.Max(0, wave));
        if (coOp) changed |= Add(_mechanics, CoOpMechanic);
        return changed;
    }

    private static bool AddBranch(HashSet<string> set, string towerId, string? branchId) =>
        !string.IsNullOrWhiteSpace(branchId) && Add(set, BranchKey(towerId, branchId));

    public static string BranchKey(string towerId, string branchId) => $"{towerId}:{branchId}";
    private static HashSet<string> Set(IEnumerable<string>? values) => new(
        (values ?? []).Where(IsSafeId).Take(1024), StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, int> Map(IReadOnlyDictionary<string, int>? values) =>
        (values ?? new Dictionary<string, int>()).Where(pair => IsSafeId(pair.Key) && pair.Value >= 0).Take(1024)
            .ToDictionary(pair => pair.Key, pair => Math.Min(pair.Value, 100_000), StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Copy(HashSet<string> values) => new(values, StringComparer.OrdinalIgnoreCase);
    private static List<string> Sorted(HashSet<string> values) => values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    private static bool IsSafeId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
    private static bool Add(HashSet<string> values, string? value) => IsSafeId(value) && values.Add(value!);
    private static bool Raise(Dictionary<string, int> values, string key, int candidate)
    {
        if (!IsSafeId(key) || candidate <= values.GetValueOrDefault(key)) return false;
        values[key] = Math.Min(candidate, 100_000);
        return true;
    }
}

public sealed class DiscoveryProgressRepository
{
    public const long MaximumFileBytes = 512 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public DiscoveryProgressRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A discovery root directory is required.", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
    public string ProgressPath => Path.Combine(RootDirectory, "discoveries.json");
    public string BackupPath => ProgressPath + ".bak";

    public DiscoveryProgress Load()
    {
        if (TryRead(ProgressPath, out var data) || TryRead(BackupPath, out data)) return new DiscoveryProgress(data);
        return new DiscoveryProgress();
    }

    public void Save(DiscoveryProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Directory.CreateDirectory(RootDirectory);
        var temporaryPath = ProgressPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(progress.Capture(), JsonOptions));
            if (TryRead(ProgressPath, out _)) File.Copy(ProgressPath, BackupPath, true);
            File.Move(temporaryPath, ProgressPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static bool TryRead(string path, out DiscoveryProgressData data)
    {
        data = new DiscoveryProgressData();
        if (!File.Exists(path)) return false;
        try
        {
            var length = new FileInfo(path).Length;
            if (length <= 0 || length > MaximumFileBytes) return false;
            var restored = JsonSerializer.Deserialize<DiscoveryProgressData>(File.ReadAllText(path), JsonOptions);
            if (restored is null || restored.SchemaVersion != DiscoveryProgressData.CurrentSchemaVersion) return false;
            data = restored;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}

public static class DiscoveryProgressStore
{
    private static DiscoveryProgressRepository DefaultRepository => new(PlatformServices.PersistentRootDirectory);

    public static string ProgressPath => DefaultRepository.ProgressPath;
    public static DiscoveryProgress Load() => DefaultRepository.Load();
    public static void Save(DiscoveryProgress progress)
    {
        DefaultRepository.Save(progress);
        PlatformServices.FlushPersistentFiles();
    }
}
