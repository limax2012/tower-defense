using System.Text.Json;
using MinimalBastion.Core;
using MinimalBastion.Data;

namespace MinimalBastion.Persistence;

public sealed record SaveSlotInfo(
    int Slot,
    bool IsOccupied,
    bool IsCoOp = false,
    string MapId = "",
    string DifficultyId = "",
    string ChallengeId = "standard",
    int CurrentWave = 0,
    bool IsEndless = false,
    int Lives = 0,
    int Credits = 0,
    DateTime SavedAtUtc = default,
    string? Error = null);

public sealed class SaveSlotRepository
{
    public const int AutosaveSlot = 0;
    public const long MaximumSaveFileBytes = 8 * 1024 * 1024;
    private const int MaximumTowers = 1024;
    private const int MaximumPulsePlates = 256;
    private const int MaximumHandledEnemiesPerPlate = 4096;
    private const int MaximumStatisticsEntries = 4096;
    private const int MaximumSpecializationsPerTower = 64;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SaveSlotRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
    public string SavesDirectory => Path.Combine(RootDirectory, "Saves");
    public bool Exists => SlotExists(AutosaveSlot) || GetExistingSlotNumbers().Count > 0;

    public string GetSlotPath(int slot)
    {
        ValidateSlot(slot);
        if (slot == AutosaveSlot) return Path.Combine(SavesDirectory, "autosave.json");
        return Path.Combine(SavesDirectory, $"slot-{slot}.json");
    }

    public string GetSlotBackupPath(int slot) => GetSlotPath(slot) + ".bak";

    public IReadOnlyList<SaveSlotInfo> GetSlots()
    {
        var occupiedSlots = GetExistingSlotNumbers();
        var slots = new List<SaveSlotInfo> { ReadSlotInfo(AutosaveSlot) };
        slots.AddRange(occupiedSlots.Select(ReadSlotInfo));
        if (FindFirstEmptySlot(occupiedSlots) is { } emptySlot)
            slots.Add(new SaveSlotInfo(emptySlot, false));
        return slots.OrderBy(slot => slot.Slot).ToArray();
    }

    public int? FindFirstEmptySlot()
    {
        return FindFirstEmptySlot(GetExistingSlotNumbers());
    }

    public void Save(GameSession session, int slot = 1)
    {
        if (!session.CanSaveCheckpoint)
            throw new InvalidOperationException("Games can only be saved between waves.");

        ValidateSlot(slot);
        WriteAtomically(GetSlotPath(slot), session.CaptureSaveGame());
    }

    public GameSession Load(GameContent content, int slot = 1)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateSlot(slot);
        var path = GetSlotPath(slot);
        var backupPath = GetSlotBackupPath(slot);
        if (!File.Exists(path) && !File.Exists(backupPath))
            throw new FileNotFoundException($"Save slot {slot} is empty.", path);

        Exception? primaryFailure = null;
        if (File.Exists(path))
        {
            try { return GameSession.RestoreSaveGame(content, ReadSaveData(path)); }
            catch (Exception exception) when (IsRecoverableLoadFailure(exception))
            {
                primaryFailure = exception;
            }
        }
        if (File.Exists(backupPath))
        {
            try { return GameSession.RestoreSaveGame(content, ReadSaveData(backupPath)); }
            catch (Exception exception) when (IsRecoverableLoadFailure(exception))
            {
                throw new InvalidDataException(
                    $"Save slot {slot} and its recovery copy cannot be restored: {exception.GetBaseException().Message}",
                    primaryFailure ?? exception);
            }
        }
        throw new InvalidDataException($"Save slot {slot} cannot be restored: {primaryFailure?.GetBaseException().Message}", primaryFailure);
    }

    public SaveGameData LoadData(int slot)
    {
        ValidateSlot(slot);
        return LoadDataCore(slot);
    }

    private SaveGameData LoadDataCore(int slot)
    {
        var path = GetSlotPath(slot);
        var backupPath = GetSlotBackupPath(slot);
        if (!File.Exists(path) && !File.Exists(backupPath))
            throw new FileNotFoundException($"Save slot {slot} is empty.", path);

        Exception? primaryFailure = null;
        if (File.Exists(path))
        {
            try { return ReadSaveData(path); }
            catch (Exception exception) when (IsRecoverableLoadFailure(exception))
            {
                primaryFailure = exception;
            }
        }
        if (File.Exists(backupPath))
        {
            try { return ReadSaveData(backupPath); }
            catch (Exception exception) when (IsRecoverableLoadFailure(exception))
            {
                throw new InvalidDataException(
                    $"Save slot {slot} and its recovery copy are unreadable: {exception.GetBaseException().Message}",
                    primaryFailure ?? exception);
            }
        }
        throw new InvalidDataException($"Save slot {slot} is unreadable: {primaryFailure?.GetBaseException().Message}", primaryFailure);
    }

    public bool Delete(int slot)
    {
        ValidateSlot(slot);
        var path = GetSlotPath(slot);
        var backupPath = GetSlotBackupPath(slot);
        var deleted = false;
        if (File.Exists(path))
        {
            File.Delete(path);
            deleted = true;
        }
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
            deleted = true;
        }
        return deleted;
    }

    private SaveSlotInfo ReadSlotInfo(int slot)
    {
        var path = GetSlotPath(slot);
        if (!File.Exists(path) && !File.Exists(GetSlotBackupPath(slot))) return new SaveSlotInfo(slot, false);
        try
        {
            // GetSlots already enumerated every occupied manual slot once.
            // Avoid repeating that directory scan for each row so dynamically
            // growing save collections remain linear.
            var data = LoadDataCore(slot);
            return new SaveSlotInfo(
                slot,
                true,
                data.IsCoOp,
                data.MapId,
                string.IsNullOrWhiteSpace(data.DifficultyId) ? DifficultyCatalog.LegacyId : data.DifficultyId,
                string.IsNullOrWhiteSpace(data.ChallengeId) ? ChallengeCatalog.DefaultId : data.ChallengeId,
                data.Waves.CurrentWaveNumber,
                data.Waves.EndlessModeEnabled,
                data.Economy.Lives,
                data.Economy.Credits,
                data.SavedAtUtc);
        }
        catch (Exception exception)
        {
            return new SaveSlotInfo(slot, true, Error: exception.GetBaseException().Message);
        }
    }

    private IReadOnlyList<int> GetExistingSlotNumbers()
    {
        if (!Directory.Exists(SavesDirectory)) return Array.Empty<int>();
        return Directory.EnumerateFiles(SavesDirectory, "slot-*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
            .Select(name => name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name)
            .Select(name => Path.GetFileNameWithoutExtension(name)!)
            .Where(name => name.StartsWith("slot-", StringComparison.OrdinalIgnoreCase))
            .Select(name => int.TryParse(name[5..], out var slot) && slot > 0 ? slot : 0)
            .Where(slot => slot > 0)
            .Distinct()
            .OrderBy(slot => slot)
            .ToArray();
    }

    private static int? FindFirstEmptySlot(IReadOnlyList<int> occupiedSlots)
    {
        var candidate = 1;
        foreach (var occupied in occupiedSlots)
        {
            if (occupied < candidate) continue;
            if (occupied > candidate) return candidate;
            if (candidate == int.MaxValue) return null;
            candidate++;
        }
        return candidate;
    }

    private bool SlotExists(int slot) => File.Exists(GetSlotPath(slot)) || File.Exists(GetSlotBackupPath(slot));

    private static void WriteAtomically(string path, SaveGameData data)
    {
        data.SavedAtUtc = DateTime.UtcNow;
        ValidateSaveStructure(data);
        var payload = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        if (payload.LongLength > MaximumSaveFileBytes)
            throw new InvalidOperationException("Checkpoint is too large to store safely.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        var backupPath = path + ".bak";
        try
        {
            File.WriteAllBytes(temporaryPath, payload);
            if (File.Exists(path) && IsReadableSave(path)) File.Copy(path, backupPath, true);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static SaveGameData ReadSaveData(string path)
    {
        var fileLength = new FileInfo(path).Length;
        if (fileLength <= 0 || fileLength > MaximumSaveFileBytes)
            throw new InvalidDataException($"Save file '{Path.GetFileName(path)}' exceeds the supported size limit.");
        var data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Save file '{Path.GetFileName(path)}' is empty or invalid.");
        if (data.SchemaVersion != SaveGameData.CurrentSchemaVersion)
            throw new InvalidDataException($"Save schema {data.SchemaVersion} is not supported.");
        ValidateSaveStructure(data);
        return data;
    }

    private static void ValidateSaveStructure(SaveGameData data)
    {
        if (string.IsNullOrWhiteSpace(data.MapId) || data.MapId.Length > 128 ||
            data.DifficultyId is null || data.DifficultyId.Length > 128 || data.ChallengeId is null || data.ChallengeId.Length > 128 ||
            data.RunId is null || data.RunId.Length > 64 ||
            data.Economy is null || data.Waves is null || data.Towers is null || data.PulsePlates is null || data.Statistics is null ||
            data.Statistics.Towers is null || data.Statistics.Enemies is null || data.Statistics.TowerDefinitionByInstance is null ||
            data.Towers.Count > MaximumTowers || data.PulsePlates.Count > MaximumPulsePlates ||
            data.Statistics.Towers.Count > MaximumStatisticsEntries || data.Statistics.Enemies.Count > MaximumStatisticsEntries ||
            data.Statistics.TowerDefinitionByInstance.Count > MaximumStatisticsEntries ||
            data.NextEnemyId <= 0 || data.NextTowerId <= 0 || data.NextEmergencyDefenseId <= 0 ||
            data.NextEmergencyDefenseId > GameConstants.ExhaustedPulsePlateNextId ||
            data.Speed is not (1f or 2f) || !IsNonnegativeFinite(data.OverdriveCooldownRemaining) ||
            data.EmergencyInventory < 0 || data.EmergencyDirectPurchasesThisWave < 0 || data.Waves.CurrentWaveNumber < 0 ||
            !IsNonnegativeFinite(data.Waves.IntermissionRemaining) || data.Economy.Credits < 0 || data.Economy.Lives < 0 ||
            data.Economy.TotalKills < 0 || data.Economy.EscapedEnemies < 0 || data.Economy.TotalCreditsSpent < 0 ||
            data.Economy.KillCreditsEarned < 0 || data.Economy.WaveCreditsEarned < 0 ||
            data.Economy.EarlyStartCreditsEarned < 0 || data.Economy.SaleCreditsRecovered < 0)
            throw new InvalidDataException("Save data is structurally invalid.");

        if (data.Towers.Any(tower => tower is null) || data.Towers.Select(tower => tower.Id).Distinct().Count() != data.Towers.Count ||
            data.Towers.Any(tower => tower.Id <= 0 || tower.OwnerPlayerId is < 1 or > 2 || string.IsNullOrWhiteSpace(tower.DefinitionId) ||
                tower.DefinitionId.Length > 128 || tower.DoctrineId is { Length: > 128 } ||
                tower.SpecializationId is { Length: > 128 } ||
                !float.IsFinite(tower.X) || !float.IsFinite(tower.Y) || tower.LevelIndex < 0 ||
                !Enum.IsDefined(tower.TargetMode) || tower.InvestedCredits < 0 || !float.IsFinite(tower.CooldownRemaining) ||
                !IsNonnegativeFinite(tower.OverdriveRemaining) || !IsNonnegativeFinite(tower.LifetimeDamage) ||
                tower.LifetimeKills < 0 || !IsNonnegativeFinite(tower.LifetimeSupportDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeExposeDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeArmorBreakDamageEquivalent) ||
                !IsNonnegativeFinite(tower.LifetimeControlSeconds) || !IsNonnegativeFinite(tower.LifetimeExposeSeconds) ||
                !IsNonnegativeFinite(tower.LifetimeArmorBreakSeconds)) ||
            data.Towers.Any(tower => tower.Id >= data.NextTowerId) ||
            data.AutoOverdriveTowerId != 0 && data.Towers.All(tower => tower.Id != data.AutoOverdriveTowerId))
            throw new InvalidDataException("Save tower data is structurally invalid.");

        if (data.PulsePlates.Any(plate => plate is null) ||
            data.PulsePlates.Select(plate => plate.Id).Distinct().Count() != data.PulsePlates.Count ||
            data.PulsePlates.Any(plate => plate.Id <= 0 || plate.Id > GameConstants.MaximumPulsePlateId ||
                plate.OwnerPlayerId is < 1 or > 2 || plate.HandledEnemyIds is null ||
                !float.IsFinite(plate.X) || !float.IsFinite(plate.Y) || plate.ChargesRemaining < 0 ||
                !IsNonnegativeFinite(plate.ArmRemaining) || !IsNonnegativeFinite(plate.CooldownRemaining) ||
                plate.HandledEnemyIds.Count > MaximumHandledEnemiesPerPlate || plate.HandledEnemyIds.Any(enemyId => enemyId <= 0)) ||
            data.PulsePlates.Any(plate => plate.Id >= data.NextEmergencyDefenseId))
            throw new InvalidDataException("Save Pulse Plate data is structurally invalid.");

        if (data.Generator is { } generator && (generator.OwnerPlayerId is < 1 or > 2 || generator.LevelIndex < 0 ||
            generator.InvestedCredits < 0 || !float.IsFinite(generator.X) || !float.IsFinite(generator.Y) ||
            !IsNonnegativeFinite(generator.ProductionRemaining)))
            throw new InvalidDataException("Save Charge Forge data is structurally invalid.");

        ValidateStatistics(data.Statistics);
    }

    private static void ValidateStatistics(RunStatisticsSaveData statistics)
    {
        if (!IsNonnegativeFinite(statistics.SimulatedSeconds) ||
            !IsNonnegativeFinite(statistics.AttributionCompactionRemaining) || statistics.AttributionCompactionRemaining > 2f ||
            statistics.EmergencyDeployments < 0 ||
            statistics.EmergencyDirectPurchases < 0 || statistics.EmergencyTriggers < 0 || statistics.EmergencyHits < 0 ||
            statistics.EmergencyKills < 0 || !IsNonnegativeFinite(statistics.EmergencyDamage) ||
            statistics.GeneratedCharges < 0 || statistics.GeneratorPurchases < 0 || statistics.GeneratorUpgrades < 0 ||
            statistics.Towers.Any(tower => tower is null || string.IsNullOrWhiteSpace(tower.TowerId) ||
                string.IsNullOrWhiteSpace(tower.DisplayName) || tower.TowerId.Length > 128 || tower.DisplayName.Length > 128 ||
                tower.Specializations is null || tower.Specializations.Count > MaximumSpecializationsPerTower ||
                tower.Purchases < 0 || tower.Upgrades < 0 || tower.Sales < 0 || tower.CreditsSpent < 0 ||
                tower.CreditsRecovered < 0 || tower.Hits < 0 || tower.Kills < 0 || tower.Overdrives < 0 ||
                !IsNonnegativeFinite(tower.Damage) || !IsNonnegativeFinite(tower.SupportDamageEquivalent) ||
                !IsNonnegativeFinite(tower.ExposeDamageEquivalent) || !IsNonnegativeFinite(tower.ArmorBreakDamageEquivalent) ||
                !IsNonnegativeFinite(tower.ControlSeconds) || !IsNonnegativeFinite(tower.ExposeSeconds) ||
                !IsNonnegativeFinite(tower.ArmorBreakSeconds) || !IsNonnegativeFinite(tower.ArmorAbsorbed) ||
                !IsNonnegativeFinite(tower.Overkill) || tower.Specializations.Any(value =>
                    string.IsNullOrWhiteSpace(value.Key) || value.Value < 0)) ||
            statistics.Towers.Select(tower => tower.TowerId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != statistics.Towers.Count ||
            statistics.Enemies.Any(enemy => enemy is null || string.IsNullOrWhiteSpace(enemy.EnemyId) ||
                string.IsNullOrWhiteSpace(enemy.DisplayName) || enemy.EnemyId.Length > 128 || enemy.DisplayName.Length > 128 ||
                enemy.Kills < 0 || enemy.Escapes < 0 || enemy.LivesLost < 0) ||
            statistics.Enemies.Select(enemy => enemy.EnemyId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != statistics.Enemies.Count ||
            statistics.TowerDefinitionByInstance.Any(source => source.Key <= 0 || string.IsNullOrWhiteSpace(source.Value) ||
                statistics.Towers.All(tower => !tower.TowerId.Equals(source.Value, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Save statistics are structurally invalid.");
    }

    private static bool IsNonnegativeFinite(float value) => float.IsFinite(value) && value >= 0;

    private static bool IsRecoverableLoadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException;

    private static bool IsReadableSave(string path)
    {
        try
        {
            _ = ReadSaveData(path);
            return true;
        }
        catch (Exception exception) when (IsRecoverableLoadFailure(exception))
        {
            return false;
        }
    }

    private static void ValidateSlot(int slot)
    {
        if (slot < AutosaveSlot)
            throw new ArgumentOutOfRangeException(nameof(slot), "Save slot cannot be negative.");
    }
}

public static class SaveGameStore
{
    private static SaveSlotRepository DefaultRepository => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion"));

    public static string SavesDirectory => DefaultRepository.SavesDirectory;
    public static string SavePath => DefaultRepository.GetSlotPath(SaveSlotRepository.AutosaveSlot);
    public static bool Exists => DefaultRepository.Exists;
    public static string GetSlotPath(int slot) => DefaultRepository.GetSlotPath(slot);
    public static IReadOnlyList<SaveSlotInfo> GetSlots() => DefaultRepository.GetSlots();
    public static int? FindFirstEmptySlot() => DefaultRepository.FindFirstEmptySlot();
    public static void Save(GameSession session, int slot = 1) => DefaultRepository.Save(session, slot);
    public static GameSession Load(GameContent content, int slot = 1) => DefaultRepository.Load(content, slot);
    public static SaveGameData LoadData(int slot) => DefaultRepository.LoadData(slot);
    public static bool Delete(int slot) => DefaultRepository.Delete(slot);
}
