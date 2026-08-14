using System.Security.Cryptography;
using System.Text;

namespace MinimalBastion.Multiplayer;

public static class BuildFingerprint
{
    public static string Compute(string contentDirectory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, typeof(BuildFingerprint).Assembly.ManifestModule.ModuleVersionId.ToString("N"));
        if (Directory.Exists(contentDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(contentDirectory, "*.json", SearchOption.AllDirectories)
                         .OrderBy(path => Path.GetRelativePath(contentDirectory, path), StringComparer.OrdinalIgnoreCase))
            {
                Append(hash, Path.GetRelativePath(contentDirectory, file).Replace('\\', '/').ToLowerInvariant());
                hash.AppendData(File.ReadAllBytes(file));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
