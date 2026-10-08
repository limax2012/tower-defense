using System.Text.Json;
using MaximalBastion.Data;

namespace MaximalBastion.Gameplay;

/// <summary>String-based JSON boundaries using the shared content serialization settings.</summary>
public static class GameplayJson
{
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, ContentJson.Options);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, ContentJson.Options);
}
