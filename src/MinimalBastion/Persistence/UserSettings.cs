using System.Text.Json;

namespace MinimalBastion.Persistence;

public sealed class UserSettings
{
    public const int CurrentSchemaVersion = 1;
    public static readonly (int Width, int Height)[] ResolutionPresets =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440)
    };
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;
    public float SfxVolume { get; set; } = 0.65f;
    public bool ReducedEffects { get; set; }

    public void Normalize()
    {
        WindowWidth = Math.Clamp(WindowWidth, 960, 3840);
        WindowHeight = Math.Clamp(WindowHeight, 540, 2160);
        SfxVolume = float.IsFinite(SfxVolume) ? Math.Clamp(SfxVolume, 0, 1) : 0.65f;
    }

    public void CycleResolution()
    {
        var current = Array.FindIndex(ResolutionPresets,
            preset => preset.Width == WindowWidth && preset.Height == WindowHeight);
        var next = current < 0 ? 0 : (current + 1) % ResolutionPresets.Length;
        (WindowWidth, WindowHeight) = ResolutionPresets[next];
    }
}

public sealed class UserSettingsRepository
{
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
    private static UserSettingsRepository DefaultRepository => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion"));

    public static string SettingsPath => DefaultRepository.SettingsPath;
    public static UserSettings Load() => DefaultRepository.Load();
    public static void Save(UserSettings settings) => DefaultRepository.Save(settings);
}
