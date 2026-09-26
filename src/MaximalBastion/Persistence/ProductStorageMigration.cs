namespace MaximalBastion.Persistence;

internal static class ProductStorageMigration
{
    internal static void CopyFromLegacy(string sourceRoot, string destinationRoot)
    {
        var marker = Path.Combine(destinationRoot, ".storage-migrated");
        if (File.Exists(marker) || !Directory.Exists(sourceRoot)) return;
        try
        {
            // Copy only bounded player data; existing destination files always take precedence.
            var candidates = new List<string>();
            foreach (var folder in new[] { "", "Saves", "History" })
            {
                var source = Path.Combine(sourceRoot, folder);
                if (!Directory.Exists(source) || (File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var path in Directory.EnumerateFiles(source).Take(128))
                {
                    var file = new FileInfo(path);
                    if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length > 32 * 1024 * 1024) continue;
                    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase)) candidates.Add(path);
                }
            }
            Directory.CreateDirectory(destinationRoot);
            foreach (var source in candidates)
            {
                var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, source));
                if (File.Exists(destination)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var temporary = destination + ".migration.tmp";
                File.Copy(source, temporary, overwrite: true);
                File.Move(temporary, destination, overwrite: false);
            }
            File.WriteAllText(marker, "1");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
