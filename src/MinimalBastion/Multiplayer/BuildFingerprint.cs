using System.Security.Cryptography;
using System.Text;

namespace MinimalBastion.Multiplayer;

public static class BuildFingerprint
{
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

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
