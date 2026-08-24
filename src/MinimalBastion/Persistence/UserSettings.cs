using System.Text.Json;
using MinimalBastion.Core;

namespace MinimalBastion.Persistence;

public sealed class UserSettings
{
    public const int CurrentSchemaVersion = 1;
    public static readonly int[] AutoStartDelayPresets = [0, 3, 5, 10];
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;
    public float SfxVolume { get; set; } = 0.65f;
    public float MusicVolume { get; set; } = 0.20f;
    public bool ReducedEffects { get; set; }
    public bool ShowHotkeyBadges { get; set; } = true;
    public bool AutoStartWaves { get; set; }
    public int AutoStartDelaySeconds { get; set; }

    public void Normalize()
    {
        WindowWidth = Math.Clamp(WindowWidth, 320, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 180, 4320);
        SfxVolume = float.IsFinite(SfxVolume) ? Math.Clamp(SfxVolume, 0, 1) : 0.65f;
        MusicVolume = float.IsFinite(MusicVolume) ? Math.Clamp(MusicVolume, 0, 1) : 0.20f;
        if (!AutoStartDelayPresets.Contains(AutoStartDelaySeconds))
            AutoStartDelaySeconds = AutoStartDelayPresets.MinBy(delay => Math.Abs(delay - AutoStartDelaySeconds));
    }

    public void CycleAutoStart()
    {
        if (!AutoStartWaves)
        {
            AutoStartWaves = true;
            AutoStartDelaySeconds = AutoStartDelayPresets[0];
            return;
        }

        var current = Array.IndexOf(AutoStartDelayPresets, AutoStartDelaySeconds);
        if (current < 0)
        {
            Normalize();
            current = Array.IndexOf(AutoStartDelayPresets, AutoStartDelaySeconds);
        }
        if (current >= AutoStartDelayPresets.Length - 1)
        {
            AutoStartWaves = false;
            return;
        }
        AutoStartDelaySeconds = AutoStartDelayPresets[current + 1];
    }

    public bool CaptureWindowedClientSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (WindowWidth == width && WindowHeight == height)) return false;
        WindowWidth = width;
        WindowHeight = height;
        return true;
    }

    public (int Width, int Height) ResolveBackBufferSize(int desktopWidth, int desktopHeight)
    {
        if (!Fullscreen) return (WindowWidth, WindowHeight);
        return (Math.Max(1, desktopWidth), Math.Max(1, desktopHeight));
    }
}

public sealed class UserSettingsRepository
{
    public const long MaximumSettingsFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UserSettingsRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A settings root directory is required.", nameof(rootDirectory));
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
    public string SettingsPath => Path.Combine(RootDirectory, "settings.json");
    public string BackupPath => SettingsPath + ".bak";

    public UserSettings Load()
    {
        if (TryRead(SettingsPath, out var settings)) return settings;
        if (TryRead(BackupPath, out settings)) return settings;
        return new UserSettings();
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        Directory.CreateDirectory(RootDirectory);
        var temporaryPath = SettingsPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            if (TryRead(SettingsPath, out _)) File.Copy(SettingsPath, BackupPath, true);
            File.Move(temporaryPath, SettingsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static bool TryRead(string path, out UserSettings settings)
    {
        settings = new UserSettings();
        if (!File.Exists(path)) return false;
        try
        {
            var length = new FileInfo(path).Length;
            if (length <= 0 || length > MaximumSettingsFileBytes) return false;
            var restored = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), JsonOptions);
            if (restored is null || restored.SchemaVersion != UserSettings.CurrentSchemaVersion) return false;
            restored.Normalize();
            settings = restored;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}

public static class UserSettingsStore
{
    private static UserSettingsRepository DefaultRepository => new(PlatformServices.PersistentRootDirectory);

    public static string SettingsPath => DefaultRepository.SettingsPath;
    public static UserSettings Load() => DefaultRepository.Load();
    public static void Save(UserSettings settings)
    {
        DefaultRepository.Save(settings);
        PlatformServices.FlushPersistentFiles();
    }
}
