using System.Text.Json;
using MinimalBastion.Core;
using MinimalBastion.Data;

namespace MinimalBastion.Persistence;

public sealed record RunHistoryEntry
{
    public string RunId { get; init; } = "";
    public DateTime CompletedAtUtc { get; init; }
    public bool IsCoOp { get; init; }
    public bool Victory { get; init; }
    public bool IsEndless { get; init; }
    public string MapId { get; init; } = "";
    public string MapName { get; init; } = "";
    public string DifficultyId { get; init; } = "";
    public string DifficultyName { get; init; } = "";
    public string ChallengeId { get; init; } = "standard";
    public string ChallengeName { get; init; } = "Standard";
    public int CurrentWave { get; init; }
    public int TotalWaves { get; init; }
    public int Lives { get; init; }
    public int StartingLives { get; init; }
    public int Kills { get; init; }
    public int Leaks { get; init; }
    public int CreditsRemaining { get; init; }
    public int CreditsEarned { get; init; }
    public int CreditsSpent { get; init; }
    public int SaleCreditsRecovered { get; init; }
    public int EarlyCallCredits { get; init; }
    public int ProtocolActivations { get; init; }
    public int PlateDeployments { get; init; }
    public int PlateDirectPurchases { get; init; }
    public int PlateTriggers { get; init; }
    public int PlateHits { get; init; }
    public int PlateKills { get; init; }
    public float PlateDamage { get; init; }
    public int ForgedCharges { get; init; }
    public int ForgePurchases { get; init; }
    public int ForgeUpgrades { get; init; }
    public float DefenseSeconds { get; init; }
    public string TopTowerName { get; init; } = "NONE";
    public float TopTowerContribution { get; init; }
    public string GreatestLeakThreatName { get; init; } = "NONE";
    public int GreatestLeakThreatLivesLost { get; init; }
    public List<RunHistoryTowerEntry> Towers { get; init; } = new();
    public List<RunHistoryEnemyEntry> Enemies { get; init; } = new();
    public RunHistoryLayoutSnapshot? FinalLayout { get; init; }

    public static RunHistoryEntry FromSession(GameSession session)
    {
        var leader = session.Statistics.TowerLeaders.FirstOrDefault();
        var leakThreat = session.Statistics.GreatestLeakThreat;
        return new RunHistoryEntry
        {
            RunId = session.RunId,
            CompletedAtUtc = DateTime.UtcNow,
            IsCoOp = session.IsCoOp,
            Victory = session.IsVictory,
            IsEndless = session.IsEndlessMode,
            MapId = session.Map.Definition.Id,
            MapName = session.Map.Definition.DisplayName,
            DifficultyId = session.DifficultyId,
            DifficultyName = session.Difficulty.DisplayName,
            ChallengeId = session.ChallengeId,
            ChallengeName = session.Challenge.DisplayName,
            CurrentWave = session.CurrentWave,
            TotalWaves = session.TotalWaves,
            Lives = session.Economy.Lives,
            StartingLives = session.Economy.StartingLives,
            Kills = session.Economy.TotalKills,
            Leaks = session.Economy.EscapedEnemies,
            CreditsRemaining = session.Economy.Credits,
            CreditsEarned = session.Economy.TotalCreditsEarned,
            CreditsSpent = session.Economy.TotalCreditsSpent,
            SaleCreditsRecovered = session.Economy.SaleCreditsRecovered,
            EarlyCallCredits = session.Economy.EarlyStartCreditsEarned,
            ProtocolActivations = session.Statistics.ProtocolActivations,
            PlateDeployments = session.Statistics.EmergencyDeployments,
            PlateDirectPurchases = session.Statistics.EmergencyDirectPurchases,
            PlateTriggers = session.Statistics.EmergencyTriggers,
            PlateHits = session.Statistics.EmergencyHits,
            PlateKills = session.Statistics.EmergencyKills,
            PlateDamage = session.Statistics.EmergencyDamage,
            ForgedCharges = session.Statistics.GeneratedCharges,
            ForgePurchases = session.Statistics.GeneratorPurchases,
            ForgeUpgrades = session.Statistics.GeneratorUpgrades,
            DefenseSeconds = session.Statistics.SimulatedSeconds,
            TopTowerName = leader?.DisplayName ?? "NONE",
            TopTowerContribution = leader?.ContributionDamage ?? 0,
            GreatestLeakThreatName = leakThreat?.DisplayName ?? "NONE",
            GreatestLeakThreatLivesLost = leakThreat?.LivesLost ?? 0,
            Towers = session.Statistics.Towers
                .OrderByDescending(tower => tower.ContributionDamage)
                .ThenBy(tower => tower.DisplayName)
                .Select(tower => new RunHistoryTowerEntry
                {
                    TowerId = tower.TowerId,
                    DisplayName = tower.DisplayName,
                    Purchases = tower.Purchases,
                    Upgrades = tower.Upgrades,
                    Sales = tower.Sales,
                    CreditsSpent = tower.CreditsSpent,
                    CreditsRecovered = tower.CreditsRecovered,
                    Hits = tower.Hits,
                    Kills = tower.Kills,
                    ProtocolActivations = tower.Overdrives,
                    Damage = tower.Damage,
                    SupportDamageEquivalent = tower.SupportDamageEquivalent,
                    ExposeDamageEquivalent = tower.ExposeDamageEquivalent,
                    ArmorBreakDamageEquivalent = tower.ArmorBreakDamageEquivalent,
                    ControlSeconds = tower.ControlSeconds,
                    ExposeSeconds = tower.ExposeSeconds,
                    ArmorBreakSeconds = tower.ArmorBreakSeconds,
                    ArmorAbsorbed = tower.ArmorAbsorbed,
                    Overkill = tower.Overkill
                }).ToList(),
            Enemies = session.Statistics.Enemies
                .OrderByDescending(enemy => enemy.LivesLost)
                .ThenByDescending(enemy => enemy.Escapes)
                .ThenBy(enemy => enemy.DisplayName)
                .Select(enemy => new RunHistoryEnemyEntry
                {
                    EnemyId = enemy.EnemyId,
                    DisplayName = enemy.DisplayName,
                    Kills = enemy.Kills,
                    Escapes = enemy.Escapes,
                    LivesLost = enemy.LivesLost
                }).ToList(),
            FinalLayout = RunHistoryLayoutSnapshot.FromSession(session)
        };
    }

    public GameSession CreateInspectionSession(GameContent content)
    {
        if (FinalLayout is null) throw new InvalidOperationException("This legacy run has no archived defense layout.");
        var waves = new WaveSaveData
        {
            CurrentWaveNumber = CurrentWave,
            IsFinalWaveCleared = IsEndless || Victory,
            EndlessModeEnabled = IsEndless
        };
        var save = new SaveGameData
        {
            RunId = RunId,
            IsCoOp = false,
            MapId = MapId,
            DifficultyId = DifficultyId,
            ChallengeId = ChallengeId,
            Speed = 1,
            AutoOverdriveTowerId = FinalLayout.AutoProtocolTowerId,
            EmergencyInventory = FinalLayout.StoredPlates,
            NextTowerId = FinalLayout.Towers.Select(tower => tower.Id).DefaultIfEmpty(0).Max() + 1,
            NextEmergencyDefenseId = FinalLayout.PulsePlates.Select(plate => plate.Id).DefaultIfEmpty(0).Max() + 1,
            Economy = new EconomySaveData
            {
                Credits = CreditsRemaining,
                Lives = Lives,
                TotalKills = Kills,
                EscapedEnemies = Leaks,
                TotalCreditsSpent = CreditsSpent,
                KillCreditsEarned = Math.Max(0, CreditsEarned - EarlyCallCredits),
                EarlyStartCreditsEarned = EarlyCallCredits,
                SaleCreditsRecovered = SaleCreditsRecovered
            },
            Waves = waves,
            Towers = FinalLayout.Towers.Select(RunHistoryLayoutSnapshot.CloneTower).ToList(),
            PulsePlates = FinalLayout.PulsePlates.Select(RunHistoryLayoutSnapshot.ClonePlate).ToList(),
            Generator = FinalLayout.Generator is null ? null : RunHistoryLayoutSnapshot.CloneGenerator(FinalLayout.Generator),
            Statistics = new RunStatisticsSaveData { SimulatedSeconds = DefenseSeconds }
        };
        var session = GameSession.RestoreSaveGame(content, save);
        session.ConfigureSolo();
        return session;
    }
}

public sealed record RunHistoryLayoutSnapshot
{
    public int StoredPlates { get; init; }
    public int AutoProtocolTowerId { get; init; }
    public List<TowerSaveData> Towers { get; init; } = new();
    public List<PulsePlateSaveData> PulsePlates { get; init; } = new();
    public GeneratorSaveData? Generator { get; init; }

    public static RunHistoryLayoutSnapshot FromSession(GameSession session) => new()
    {
        StoredPlates = session.EmergencyInventory,
        AutoProtocolTowerId = session.AutoOverdriveTowerId,
        Towers = session.Towers.Select(tower =>
        {
            var saved = tower.CaptureSaveData();
            // The archive is a clean tactical diagram. Preserve progression,
            // targeting, ownership, and lifetime results while dropping the
            // arbitrary fire/recoil phase from the terminal simulation tick.
            saved.CooldownRemaining = 0;
            saved.OverdriveRemaining = 0;
            saved.DisruptionRemaining = 0;
            saved.DisruptionLockoutRemaining = 0;
            saved.SuppressionRemaining = 0;
            saved.SuppressionLockoutRemaining = 0;
            return saved;
        }).ToList(),
        PulsePlates = session.EmergencyDefenses.Select(plate =>
        {
            var saved = plate.CaptureSaveData();
            saved.ArmRemaining = 0;
            saved.CooldownRemaining = 0;
            saved.HandledEnemyIds.Clear();
            return saved;
        }).ToList(),
        Generator = session.Generator?.CaptureSaveData()
    };

    internal static TowerSaveData CloneTower(TowerSaveData tower) => new()
    {
        Id = tower.Id,
        OwnerPlayerId = tower.OwnerPlayerId,
        DefinitionId = tower.DefinitionId,
        X = tower.X,
        Y = tower.Y,
        LevelIndex = tower.LevelIndex,
        DoctrineId = tower.DoctrineId,
        SpecializationId = tower.SpecializationId,
        IsApex = tower.IsApex,
        CooldownRemaining = tower.CooldownRemaining,
        TargetMode = tower.TargetMode,
        InvestedCredits = tower.InvestedCredits,
        OverdriveRemaining = tower.OverdriveRemaining,
        LifetimeDamage = tower.LifetimeDamage,
        LifetimeKills = tower.LifetimeKills,
        LifetimeSupportDamageEquivalent = tower.LifetimeSupportDamageEquivalent,
        LifetimeExposeDamageEquivalent = tower.LifetimeExposeDamageEquivalent,
        LifetimeArmorBreakDamageEquivalent = tower.LifetimeArmorBreakDamageEquivalent,
        LifetimeControlSeconds = tower.LifetimeControlSeconds,
        LifetimeExposeSeconds = tower.LifetimeExposeSeconds,
        LifetimeArmorBreakSeconds = tower.LifetimeArmorBreakSeconds
    };

    internal static PulsePlateSaveData ClonePlate(PulsePlateSaveData plate) => new()
    {
        Id = plate.Id,
        OwnerPlayerId = plate.OwnerPlayerId,
        X = plate.X,
        Y = plate.Y,
        ChargesRemaining = plate.ChargesRemaining,
        ArmRemaining = plate.ArmRemaining,
        CooldownRemaining = plate.CooldownRemaining,
        HandledEnemyIds = new List<int>(plate.HandledEnemyIds)
    };

    internal static GeneratorSaveData CloneGenerator(GeneratorSaveData generator) => new()
    {
        OwnerPlayerId = generator.OwnerPlayerId,
        X = generator.X,
        Y = generator.Y,
        LevelIndex = generator.LevelIndex,
        InvestedCredits = generator.InvestedCredits,
        ProductionRemaining = generator.ProductionRemaining
    };
}

public sealed record RunHistoryTowerEntry
{
    public string TowerId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Purchases { get; init; }
    public int Upgrades { get; init; }
    public int Sales { get; init; }
    public int CreditsSpent { get; init; }
    public int CreditsRecovered { get; init; }
    public int Hits { get; init; }
    public int Kills { get; init; }
    public int ProtocolActivations { get; init; }
    public float Damage { get; init; }
    public float SupportDamageEquivalent { get; init; }
    public float ExposeDamageEquivalent { get; init; }
    public float ArmorBreakDamageEquivalent { get; init; }
    public float ControlSeconds { get; init; }
    public float ExposeSeconds { get; init; }
    public float ArmorBreakSeconds { get; init; }
    public float ArmorAbsorbed { get; init; }
    public float Overkill { get; init; }
    public float AssistDamageEquivalent => SupportDamageEquivalent + ExposeDamageEquivalent + ArmorBreakDamageEquivalent;
    public float ContributionDamage => Damage + AssistDamageEquivalent;
    public float ImpactPerCredit => CreditsSpent <= 0 ? 0 : ContributionDamage / CreditsSpent;
}

public sealed record RunHistoryEnemyEntry
{
    public string EnemyId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Kills { get; init; }
    public int Escapes { get; init; }
    public int LivesLost { get; init; }
}

public sealed class RunHistoryRepository
{
    public const long MaximumHistoryFileBytes = 16 * 1024 * 1024;
    public const int MaximumHistoryEntries = 16_384;
    private const int MaximumLabelLength = 128;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RunHistoryRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A history root directory is required.", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
    public string HistoryDirectory => Path.Combine(RootDirectory, "History");
    public string HistoryPath => Path.Combine(HistoryDirectory, "run-history.json");
    public string BackupPath => HistoryPath + ".bak";

    public IReadOnlyList<RunHistoryEntry> GetEntries()
    {
        if (!File.Exists(HistoryPath) && !File.Exists(BackupPath)) return Array.Empty<RunHistoryEntry>();
        Exception? primaryFailure = null;
        if (File.Exists(HistoryPath))
        {
            try { return Read(HistoryPath); }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                primaryFailure = exception;
            }
        }
        if (File.Exists(BackupPath))
        {
            try { return Read(BackupPath); }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Run history and its recovery copy are unreadable: {exception.GetBaseException().Message}",
                    primaryFailure ?? exception);
            }
        }
        throw new InvalidDataException($"Run history is unreadable: {primaryFailure?.GetBaseException().Message}", primaryFailure);
    }

    public void Upsert(RunHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RunId)) throw new ArgumentException("Run history entries require a run ID.", nameof(entry));
        entry = entry with { CompletedAtUtc = entry.CompletedAtUtc == default ? DateTime.UtcNow : entry.CompletedAtUtc };
        ValidateEntries([entry], nameof(entry));
        var entries = GetEntries().Where(existing => !existing.RunId.Equals(entry.RunId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count >= MaximumHistoryEntries)
            throw new InvalidOperationException($"Run history has reached its {MaximumHistoryEntries:N0}-record safety limit.");
        entries.Add(entry);
        Write(entries.OrderByDescending(existing => existing.CompletedAtUtc).ToArray());
    }

    public bool Delete(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return false;
        var entries = GetEntries();
        var remaining = entries.Where(entry => !entry.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (remaining.Length == entries.Count) return false;
        Write(remaining);
        return true;
    }

    private void Write(IReadOnlyList<RunHistoryEntry> entries)
    {
        ValidateEntries(entries);
        Directory.CreateDirectory(HistoryDirectory);
        var temporaryPath = HistoryPath + ".tmp";
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(entries, JsonOptions);
            if (payload.LongLength > MaximumHistoryFileBytes)
                throw new InvalidOperationException("Run history is too large to store safely.");
            File.WriteAllBytes(temporaryPath, payload);
            if (File.Exists(HistoryPath) && IsReadableHistory(HistoryPath)) File.Copy(HistoryPath, BackupPath, true);
            File.Move(temporaryPath, HistoryPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static IReadOnlyList<RunHistoryEntry> Read(string path)
    {
        var length = new FileInfo(path).Length;
        if (length <= 0 || length > MaximumHistoryFileBytes)
            throw new InvalidDataException("Run history has an invalid file size.");
        var entries = JsonSerializer.Deserialize<List<RunHistoryEntry>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Run history is empty or invalid.");
        ValidateEntries(entries);
        return entries
            .OrderByDescending(entry => entry.CompletedAtUtc)
            .ToArray();
    }

    private static void ValidateEntries(IReadOnlyList<RunHistoryEntry> entries, string? argumentName = null)
    {
        var valid = entries.Count <= MaximumHistoryEntries &&
            entries.Select(entry => entry.RunId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Count &&
            entries.All(IsValidEntry);
        if (valid) return;
        if (argumentName is not null)
            throw new ArgumentException("Run history entry is structurally invalid.", argumentName);
        throw new InvalidDataException("Run history contains structurally invalid records.");
    }

    private static bool IsValidEntry(RunHistoryEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.RunId) && entry.RunId.Length <= 64 &&
            entry.CompletedAtUtc != default && IsValidLabel(entry.MapId) && IsValidLabel(entry.MapName) &&
            IsValidLabel(entry.DifficultyId) && IsValidLabel(entry.DifficultyName) &&
            IsValidLabel(entry.ChallengeId) && IsValidLabel(entry.ChallengeName) &&
            IsValidLabel(entry.TopTowerName) && entry.CurrentWave >= 0 && entry.TotalWaves > 0 &&
            entry.Lives >= 0 && entry.StartingLives > 0 && entry.Kills >= 0 && entry.Leaks >= 0 &&
            entry.CreditsRemaining >= 0 && entry.CreditsEarned >= 0 && entry.CreditsSpent >= 0 &&
            entry.SaleCreditsRecovered >= 0 && entry.EarlyCallCredits >= 0 &&
            entry.ProtocolActivations >= 0 && entry.PlateDeployments >= 0 && entry.PlateDirectPurchases >= 0 &&
            entry.PlateTriggers >= 0 && entry.PlateHits >= 0 && entry.PlateKills >= 0 && entry.ForgedCharges >= 0 &&
            entry.ForgePurchases >= 0 && entry.ForgeUpgrades >= 0 &&
            float.IsFinite(entry.PlateDamage) && entry.PlateDamage >= 0 &&
            float.IsFinite(entry.DefenseSeconds) && entry.DefenseSeconds >= 0 &&
            float.IsFinite(entry.TopTowerContribution) && entry.TopTowerContribution >= 0 &&
            IsValidLabel(entry.GreatestLeakThreatName) && entry.GreatestLeakThreatLivesLost >= 0 &&
            entry.Towers is not null && entry.Towers.Count <= 128 && entry.Towers.All(IsValidTowerEntry) &&
            entry.Enemies is not null && entry.Enemies.Count <= 128 && entry.Enemies.All(IsValidEnemyEntry) &&
            (entry.FinalLayout is null || IsValidLayout(entry.FinalLayout));
    }

    private static bool IsValidTowerEntry(RunHistoryTowerEntry tower) =>
        IsValidLabel(tower.TowerId) && IsValidLabel(tower.DisplayName) &&
        tower.Purchases >= 0 && tower.Upgrades >= 0 && tower.Sales >= 0 && tower.CreditsSpent >= 0 &&
        tower.CreditsRecovered >= 0 && tower.Hits >= 0 && tower.Kills >= 0 && tower.ProtocolActivations >= 0 &&
        AreFiniteNonnegative(tower.Damage, tower.SupportDamageEquivalent, tower.ExposeDamageEquivalent,
            tower.ArmorBreakDamageEquivalent, tower.ControlSeconds, tower.ExposeSeconds,
            tower.ArmorBreakSeconds, tower.ArmorAbsorbed, tower.Overkill);

    private static bool IsValidEnemyEntry(RunHistoryEnemyEntry enemy) =>
        IsValidLabel(enemy.EnemyId) && IsValidLabel(enemy.DisplayName) &&
        enemy.Kills >= 0 && enemy.Escapes >= 0 && enemy.LivesLost >= 0;

    private static bool IsValidLayout(RunHistoryLayoutSnapshot layout) =>
        layout.StoredPlates >= 0 && layout.AutoProtocolTowerId >= 0 &&
        layout.Towers is not null && layout.Towers.Count <= 1024 &&
        layout.Towers.Select(tower => tower.Id).Distinct().Count() == layout.Towers.Count &&
        layout.Towers.All(IsValidPlacedTower) &&
        (layout.AutoProtocolTowerId == 0 || layout.Towers.Any(tower => tower.Id == layout.AutoProtocolTowerId)) &&
        layout.PulsePlates is not null && layout.PulsePlates.Count <= 256 &&
        layout.PulsePlates.Select(plate => plate.Id).Distinct().Count() == layout.PulsePlates.Count &&
        layout.PulsePlates.All(IsValidPlacedPlate) &&
        (layout.Generator is null || IsValidGenerator(layout.Generator));

    private static bool IsValidPlacedTower(TowerSaveData tower) =>
        tower.Id is > 0 and < int.MaxValue && tower.OwnerPlayerId is 1 or 2 && IsValidLabel(tower.DefinitionId) &&
        IsBattlefieldPosition(tower.X, tower.Y) && tower.LevelIndex is >= 0 and <= 2 &&
        (!tower.IsApex || tower.LevelIndex == 2 && !string.IsNullOrWhiteSpace(tower.SpecializationId)) &&
        (tower.DoctrineId is null || IsValidLabel(tower.DoctrineId)) &&
        (tower.SpecializationId is null || IsValidLabel(tower.SpecializationId)) &&
        Enum.IsDefined(tower.TargetMode) && tower.InvestedCredits >= 0 &&
        AreFiniteNonnegative(tower.CooldownRemaining, tower.OverdriveRemaining, tower.LifetimeDamage,
            tower.LifetimeSupportDamageEquivalent, tower.LifetimeExposeDamageEquivalent,
            tower.LifetimeArmorBreakDamageEquivalent, tower.LifetimeControlSeconds,
            tower.LifetimeExposeSeconds, tower.LifetimeArmorBreakSeconds) && tower.LifetimeKills >= 0;

    private static bool IsValidPlacedPlate(PulsePlateSaveData plate) =>
        plate.Id is > 0 and <= GameConstants.MaximumPulsePlateId && plate.OwnerPlayerId is 1 or 2 &&
        IsBattlefieldPosition(plate.X, plate.Y) && plate.ChargesRemaining >= 0 &&
        AreFiniteNonnegative(plate.ArmRemaining, plate.CooldownRemaining) &&
        plate.HandledEnemyIds is not null && plate.HandledEnemyIds.Count <= 4096 &&
        plate.HandledEnemyIds.All(enemyId => enemyId > 0);

    private static bool IsValidGenerator(GeneratorSaveData generator) =>
        generator.OwnerPlayerId is 1 or 2 && IsBattlefieldPosition(generator.X, generator.Y) &&
        generator.LevelIndex is >= 0 and <= 2 && generator.InvestedCredits >= 0 &&
        float.IsFinite(generator.ProductionRemaining) && generator.ProductionRemaining >= 0;

    private static bool IsBattlefieldPosition(float x, float y) =>
        float.IsFinite(x) && float.IsFinite(y) && x >= 0 && x < GameConstants.MapWidth &&
        y >= GameConstants.TopBarHeight && y <= GameConstants.LogicalHeight;

    private static bool AreFiniteNonnegative(params float[] values) =>
        values.All(value => float.IsFinite(value) && value >= 0);

    private static bool IsValidLabel(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLabelLength;

    private static bool IsReadableHistory(string path)
    {
        try
        {
            _ = Read(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return false;
        }
    }
}

public static class RunHistoryStore
{
    private static RunHistoryRepository DefaultRepository => new(PlatformServices.PersistentRootDirectory);

    public static IReadOnlyList<RunHistoryEntry> GetEntries() => DefaultRepository.GetEntries();
    public static void Upsert(RunHistoryEntry entry)
    {
        DefaultRepository.Upsert(entry);
        PlatformServices.FlushPersistentFiles();
    }

    public static bool Delete(string runId)
    {
        var deleted = DefaultRepository.Delete(runId);
        if (deleted) PlatformServices.FlushPersistentFiles();
        return deleted;
    }
}
