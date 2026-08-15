using System.Text.Json;

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
    public int CreditsEarned { get; init; }
    public int CreditsSpent { get; init; }
    public float DefenseSeconds { get; init; }
    public string TopTowerName { get; init; } = "NONE";
    public float TopTowerContribution { get; init; }

    public static RunHistoryEntry FromSession(GameSession session)
    {
        var leader = session.Statistics.TowerLeaders.FirstOrDefault();
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
            CreditsEarned = session.Economy.TotalCreditsEarned,
            CreditsSpent = session.Economy.TotalCreditsSpent,
            DefenseSeconds = session.Statistics.SimulatedSeconds,
            TopTowerName = leader?.DisplayName ?? "NONE",
            TopTowerContribution = leader?.ContributionDamage ?? 0
        };
    }
}

public sealed class RunHistoryRepository
{
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
        var entries = GetEntries().Where(existing => !existing.RunId.Equals(entry.RunId, StringComparison.OrdinalIgnoreCase)).ToList();
        entries.Add(entry with { CompletedAtUtc = entry.CompletedAtUtc == default ? DateTime.UtcNow : entry.CompletedAtUtc });
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
        Directory.CreateDirectory(HistoryDirectory);
        var temporaryPath = HistoryPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, JsonOptions));
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
        var entries = JsonSerializer.Deserialize<List<RunHistoryEntry>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Run history is empty or invalid.");
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.RunId))
            .OrderByDescending(entry => entry.CompletedAtUtc)
            .ToArray();
    }

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
    private static RunHistoryRepository DefaultRepository => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion"));

    public static IReadOnlyList<RunHistoryEntry> GetEntries() => DefaultRepository.GetEntries();
    public static void Upsert(RunHistoryEntry entry) => DefaultRepository.Upsert(entry);
    public static bool Delete(string runId) => DefaultRepository.Delete(runId);
}
