using System.Text.Json;
using MinimalBastion.Data;

namespace MinimalBastion.Persistence;

public static class SaveGameStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MinimalBastion",
        "savegame.json");

    public static bool Exists => File.Exists(SavePath);

    public static void Save(GameSession session)
    {
        if (!session.CanSaveCheckpoint)
            throw new InvalidOperationException("Checkpoints can only be saved between waves in a solo game.");

        var data = session.CaptureSaveGame();
        var directory = Path.GetDirectoryName(SavePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SavePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryPath, SavePath, true);
    }

    public static GameSession Load(GameContent content)
    {
        if (!Exists) throw new FileNotFoundException("No Minimal Bastion checkpoint exists.", SavePath);
        var data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(SavePath), JsonOptions)
            ?? throw new InvalidDataException("The save file is empty or invalid.");
        if (data.SchemaVersion != SaveGameData.CurrentSchemaVersion)
            throw new InvalidDataException($"Save schema {data.SchemaVersion} is not supported.");
        return GameSession.RestoreSaveGame(content, data);
    }
}
