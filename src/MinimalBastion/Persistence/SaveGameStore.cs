using System.Text.Json;
using MinimalBastion.Data;

namespace MinimalBastion.Persistence;

public sealed record SaveSlotInfo(
    int Slot,
    bool IsOccupied,
    bool IsCoOp = false,
    string MapId = "",
    int CurrentWave = 0,
    bool IsEndless = false,
    int Lives = 0,
    int Credits = 0,
    DateTime SavedAtUtc = default,
    string? Error = null);

public sealed class SaveSlotRepository
{
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
    public string LegacySavePath => Path.Combine(RootDirectory, "savegame.json");
    public bool Exists => GetSlots().Any(slot => slot.IsOccupied);

    public string GetSlotPath(int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(SavesDirectory, $"slot-{slot}.json");
    }

    public IReadOnlyList<SaveSlotInfo> GetSlots()
    {
        TryMigrateLegacySave();
        var occupiedSlots = GetExistingSlotNumbers();
        var slots = occupiedSlots.Select(ReadSlotInfo).ToList();
        if (FindFirstEmptySlot(occupiedSlots) is { } emptySlot)
            slots.Add(new SaveSlotInfo(emptySlot, false));
        return slots.OrderBy(slot => slot.Slot).ToArray();
    }

    public int? FindFirstEmptySlot()
    {
        TryMigrateLegacySave();
        return FindFirstEmptySlot(GetExistingSlotNumbers());
    }

    public void Save(GameSession session, int slot = 1)
    {
        if (!session.CanSaveCheckpoint)
            throw new InvalidOperationException("Games can only be saved between waves.");

        ValidateSlot(slot);
        WriteAtomically(GetSlotPath(slot), session.CaptureSaveGame());
    }

    public GameSession Load(GameContent content, int slot = 1) => GameSession.RestoreSaveGame(content, LoadData(slot));

    public SaveGameData LoadData(int slot)
    {
        ValidateSlot(slot);
        TryMigrateLegacySave();
        var path = GetSlotPath(slot);
        if (!File.Exists(path)) throw new FileNotFoundException($"Save slot {slot} is empty.", path);
        var data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Save slot {slot} is empty or invalid.");
        if (data.SchemaVersion != SaveGameData.CurrentSchemaVersion)
            throw new InvalidDataException($"Save schema {data.SchemaVersion} is not supported.");
        return data;
    }

    public bool Delete(int slot)
    {
        ValidateSlot(slot);
        TryMigrateLegacySave();
        var path = GetSlotPath(slot);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private SaveSlotInfo ReadSlotInfo(int slot)
    {
        var path = GetSlotPath(slot);
        if (!File.Exists(path)) return new SaveSlotInfo(slot, false);
        try
        {
            var data = LoadData(slot);
            return new SaveSlotInfo(
                slot,
                true,
                data.IsCoOp,
                data.MapId,
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

    private void TryMigrateLegacySave()
    {
        if (!File.Exists(LegacySavePath) || GetExistingSlotNumbers().Count > 0) return;
        try
        {
            var data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(LegacySavePath), JsonOptions);
            if (data is null || data.SchemaVersion != SaveGameData.CurrentSchemaVersion) return;
            WriteAtomically(GetSlotPath(1), data);
        }
        catch
        {
            // Preserve an unreadable legacy file untouched; the slot screen can still
            // be used normally and no user data is destroyed during migration.
        }
    }

    private IReadOnlyList<int> GetExistingSlotNumbers()
    {
        if (!Directory.Exists(SavesDirectory)) return Array.Empty<int>();
        return Directory.EnumerateFiles(SavesDirectory, "slot-*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path))
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

    private static void WriteAtomically(string path, SaveGameData data)
    {
        data.SavedAtUtc = DateTime.UtcNow;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryPath, path, true);
    }

    private static void ValidateSlot(int slot)
    {
        if (slot < 1)
            throw new ArgumentOutOfRangeException(nameof(slot), "Save slot must be a positive number.");
    }
}

public static class SaveGameStore
{
    private static SaveSlotRepository DefaultRepository => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion"));

    public static string SavesDirectory => DefaultRepository.SavesDirectory;
    public static string LegacySavePath => DefaultRepository.LegacySavePath;
    public static string SavePath => DefaultRepository.GetSlotPath(1);
    public static bool Exists => DefaultRepository.Exists;
    public static string GetSlotPath(int slot) => DefaultRepository.GetSlotPath(slot);
    public static IReadOnlyList<SaveSlotInfo> GetSlots() => DefaultRepository.GetSlots();
    public static int? FindFirstEmptySlot() => DefaultRepository.FindFirstEmptySlot();
    public static void Save(GameSession session, int slot = 1) => DefaultRepository.Save(session, slot);
    public static GameSession Load(GameContent content, int slot = 1) => DefaultRepository.Load(content, slot);
    public static SaveGameData LoadData(int slot) => DefaultRepository.LoadData(slot);
    public static bool Delete(int slot) => DefaultRepository.Delete(slot);
}
