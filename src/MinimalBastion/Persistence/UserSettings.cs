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
        SfxVolume = Math.Clamp(SfxVolume, 0, 1);
    }

    public void CycleResolution()
    {
        var current = Array.FindIndex(ResolutionPresets,
            preset => preset.Width == WindowWidth && preset.Height == WindowHeight);
        var next = current < 0 ? 0 : (current + 1) % ResolutionPresets.Length;
        (WindowWidth, WindowHeight) = ResolutionPresets[next];
    }
}

public static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion",
        "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new UserSettings();
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new UserSettings();
            if (settings.SchemaVersion != UserSettings.CurrentSchemaVersion) return new UserSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
