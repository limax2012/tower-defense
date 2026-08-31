using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MinimalBastion.Data;

namespace MinimalBastion.Multiplayer;

public static class BuildFingerprint
{
    private const int CanonicalContentVersion = 1;
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web)
    {
        IgnoreReadOnlyProperties = true,
        WriteIndented = false
    };

    public static string Compute(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ComputeCanonicalContent(content);
    }

    public static string Compute(string contentDirectory)
    {
        if (!Directory.Exists(contentDirectory))
            throw new DirectoryNotFoundException($"Content directory was not found: {contentDirectory}");

        var contentFiles = Directory.EnumerateFiles(contentDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(contentDirectory, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (contentFiles.Length == 0)
            throw new InvalidDataException($"Content directory contains no JSON definitions: {contentDirectory}");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "MinimalBastion.CoOpBuild.v1");
        Append(hash, typeof(BuildFingerprint).Assembly.ManifestModule.ModuleVersionId.ToString("N"));
        foreach (var file in contentFiles)
        {
            Append(hash, Path.GetRelativePath(contentDirectory, file).Replace('\\', '/').ToLowerInvariant());
            hash.AppendData(File.ReadAllBytes(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static bool IsValid(string? fingerprint) => fingerprint is { Length: 64 } &&
        fingerprint.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static string ComputeCanonicalContent(GameContent content)
    {
        var payload = new CanonicalContent(
            CanonicalContentVersion,
            typeof(BuildFingerprint).Assembly.ManifestModule.ModuleVersionId.ToString("N"),
            content.Map,
            content.Waves,
            Ordered(content.Maps),
            Ordered(content.WaveSets),
            Ordered(content.Towers),
            Ordered(content.Enemies),
            Ordered(content.Difficulties),
            Ordered(content.Challenges),
            content.Tactics);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(payload, CanonicalJsonOptions);
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    private static IReadOnlyList<CanonicalEntry<T>> Ordered<T>(IReadOnlyDictionary<string, T> values) => values
        .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => new CanonicalEntry<T>(entry.Key.ToLowerInvariant(), entry.Value))
        .ToArray();

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }

    private sealed record CanonicalEntry<T>(string Key, T Value);
    private sealed record CanonicalContent(
        int Version,
        string AssemblyMvid,
        MapDefinition DefaultMap,
        WaveSetDefinition DefaultWaveSet,
        IReadOnlyList<CanonicalEntry<MapDefinition>> Maps,
        IReadOnlyList<CanonicalEntry<WaveSetDefinition>> WaveSets,
        IReadOnlyList<CanonicalEntry<TowerDefinition>> Towers,
        IReadOnlyList<CanonicalEntry<EnemyDefinition>> Enemies,
        IReadOnlyList<CanonicalEntry<DifficultyDefinition>> Difficulties,
        IReadOnlyList<CanonicalEntry<ChallengeDefinition>> Challenges,
        TacticsDefinition Tactics);
}
