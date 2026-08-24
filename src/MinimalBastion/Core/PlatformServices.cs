namespace MinimalBastion.Core;

public sealed class PlatformPointerState
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool LeftPressed { get; set; }
    public bool LeftReleased { get; set; }
    public bool RightPressed { get; set; }
    public bool MiddlePressed { get; set; }
    public bool Active { get; set; }
}

public static class PlatformServices
{
    private static Action<string, string>? _writePersistentFile;
    private static Action<string>? _deletePersistentFile;
    private static readonly HashSet<string> KnownPersistentFiles = new(StringComparer.OrdinalIgnoreCase);

    public static Func<string?>? ClipboardReader { get; set; }
    public static Func<string, bool>? ClipboardWriter { get; set; }
    public static Action<bool>? FullscreenSetter { get; set; }
    public static Action<string>? RuntimeStageSetter { get; set; }
    public static Func<bool>? InputFocusReader { get; set; }
    public static Func<PlatformPointerState?>? PointerStateReader { get; set; }

    public static string PersistentRootDirectory => Path.GetFullPath(Path.Combine(
#if BLAZORGL
        Path.DirectorySeparatorChar.ToString(),
#else
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#endif
        "MinimalBastion"));

    public static void InitializePersistentFiles(
        IReadOnlyDictionary<string, string>? encodedFiles,
        Action<string, string> writeFile,
        Action<string> deleteFile)
    {
        ArgumentNullException.ThrowIfNull(writeFile);
        ArgumentNullException.ThrowIfNull(deleteFile);
        _writePersistentFile = writeFile;
        _deletePersistentFile = deleteFile;
        KnownPersistentFiles.Clear();
        Directory.CreateDirectory(PersistentRootDirectory);

        foreach (var pair in encodedFiles ?? new Dictionary<string, string>())
        {
            if (!TryResolvePersistentPath(pair.Key, out var path)) continue;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, Convert.FromBase64String(pair.Value));
                KnownPersistentFiles.Add(NormalizeStorageKey(pair.Key));
            }
            catch (FormatException)
            {
                deleteFile(pair.Key);
            }
        }
    }

    public static void FlushPersistentFiles()
    {
        if (_writePersistentFile is null || _deletePersistentFile is null) return;
        Directory.CreateDirectory(PersistentRootDirectory);
        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(PersistentRootDirectory, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            var key = NormalizeStorageKey(Path.GetRelativePath(PersistentRootDirectory, path));
            current.Add(key);
            _writePersistentFile(key, Convert.ToBase64String(File.ReadAllBytes(path)));
        }

        foreach (var missing in KnownPersistentFiles.Except(current).ToArray())
            _deletePersistentFile(missing);
        KnownPersistentFiles.Clear();
        KnownPersistentFiles.UnionWith(current);
    }

    private static bool TryResolvePersistentPath(string key, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key)) return false;
        var normalized = NormalizeStorageKey(key);
        if (normalized.Split('/').Any(segment => segment is "" or "." or "..")) return false;
        path = Path.GetFullPath(Path.Combine(PersistentRootDirectory,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(PersistentRootDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStorageKey(string key) => key.Replace('\\', '/');
}
