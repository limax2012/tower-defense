using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class HistoryLayoutCli
{
    private sealed record Input<T>(string Path, string Sha256, T Value);

    public static int Run(GameContent content, string[] args, string output)
    {
        var runId = Option(args, "--run-id") ?? throw new ArgumentException("--run-id is required for history layout import.");
        var history = ReadHistory(Option(args, "--history"));
        var entry = history.Value.SingleOrDefault(item => string.Equals(item.RunId, runId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"History has no run with ID '{runId}'.");
        var fallback = Option(args, "--fallback-save") is { } savePath
            ? Read<SaveGameData>(savePath, SaveSlotRepository.MaximumSaveFileBytes) : null;
        var firstApex = GameConstants.ApexUnlockWave;
        if (Option(args, "--apex-from-wave") is { } wave && !int.TryParse(wave, out firstApex))
            throw new ArgumentException("--apex-from-wave requires an integer campaign wave.");
        var imported = HistoryLayoutImporter.Convert(content, entry, firstApex,
            args.Contains("--no-combat-sales", StringComparer.OrdinalIgnoreCase), fallback?.Value);
        var outputs = new[] { "layout.json", "plan.json", "provenance.json" }
            .Select(name => Path.GetFullPath(Path.Combine(output, name))).ToArray();
        if (outputs.Any(path => SamePath(path, history.Path) || fallback is not null && SamePath(path, fallback.Path)))
            throw new ArgumentException("The output layout, plan, or provenance would replace an input file.");
        PlannerStore.Write(outputs[0], imported.Layout);
        PlannerStore.Write(outputs[1], imported.Plan);
        PlannerStore.Write(outputs[2], new
        {
            HistoryPath = history.Path, HistorySha256 = history.Sha256,
            FallbackPath = fallback?.Path, FallbackSha256 = fallback?.Sha256,
            LayoutSha256 = PlanningFingerprint.Hash(JsonSerializer.Serialize(imported.Layout, PlannerStore.Json)),
            PlanSha256 = PlanningFingerprint.Hash(JsonSerializer.Serialize(imported.Plan, PlannerStore.Json)),
            Derived = imported.Provenance
        });
        Console.WriteLine($"Imported {imported.Layout.Towers.Count} tower goals from {imported.Provenance.LayoutSource} for {entry.RunId}.");
        Console.WriteLine($"Fresh campaign plan: {outputs[1]}");
        return 0;
    }

    private static Input<List<RunHistoryEntry>> ReadHistory(string? path)
    {
        if (path is not null) return ReadHistoryFile(path);
        var repository = new RunHistoryRepository(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaximalBastion"));
        try { return ReadHistoryFile(repository.HistoryPath); }
        catch (Exception e) when ((e is IOException or InvalidDataException or JsonException) && File.Exists(repository.BackupPath))
        {
            return ReadHistoryFile(repository.BackupPath);
        }
    }

    private static Input<List<RunHistoryEntry>> ReadHistoryFile(string path)
    {
        var input = Read<List<RunHistoryEntry>>(path, RunHistoryRepository.MaximumHistoryFileBytes);
        RunHistoryRepository.ValidateEntries(input.Value);
        return input;
    }

    private static Input<T> Read<T>(string path, long maximumBytes) where T : class
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var length = stream.Length;
        if (length <= 0 || length > maximumBytes) throw new InvalidDataException($"Invalid input size for '{fullPath}'.");
        var bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var value = JsonSerializer.Deserialize<T>(reader.ReadToEnd(), PlannerStore.Json)
            ?? throw new InvalidDataException($"Missing JSON data in '{fullPath}'.");
        return new(fullPath, Convert.ToHexString(SHA256.HashData(bytes)), value);
    }

    private static string? Option(string[] args, string name)
    {
        var value = PlannerCli.Value(args, name);
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException($"{name} requires a value.");
        return value;
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(first, second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
