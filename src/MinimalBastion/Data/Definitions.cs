using System.Text.Json;
using MinimalBastion.Core;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Data;

public sealed class GameContent
{
    public required Dictionary<string, TowerDefinition> Towers { get; init; }
    public required Dictionary<string, EnemyDefinition> Enemies { get; init; }
    public required MapDefinition Map { get; init; }
    public required WaveSetDefinition Waves { get; init; }
    public Dictionary<string, MapDefinition> Maps { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WaveSetDefinition> WaveSets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DifficultyDefinition> Difficulties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public TacticsDefinition Tactics { get; init; } = new();
}

public sealed class DifficultyDefinition
{
    public string Id { get; set; } = "hard";
    public string DisplayName { get; set; } = "Hard";
    public string Description { get; set; } = "The original uncompromised defense.";
    public float EnemyHealthMultiplier { get; set; } = 1f;
    public float EnemySpeedMultiplier { get; set; } = 1f;
    public float StartingCreditsMultiplier { get; set; } = 1f;
    public int StartingLives { get; set; } = 20;
    public string Accent { get; set; } = "#EC5062";
    public Color AccentColor => TowerVisualData.ParseColor(Accent);
}

public static class DifficultyCatalog
{
    public const string DefaultId = "normal";
    public const string LegacyId = "hard";

    private static readonly DifficultyDefinition LegacyDifficulty = new();

    public static DifficultyDefinition Resolve(GameContent content, string? difficultyId)
    {
        var requested = string.IsNullOrWhiteSpace(difficultyId) ? LegacyId : difficultyId;
        if (content.Difficulties.TryGetValue(requested, out var difficulty)) return difficulty;
        if (content.Difficulties.Count > 0) throw new ArgumentException($"Unknown difficulty profile '{requested}'.", nameof(difficultyId));
        return LegacyDifficulty;
    }

    public static int StartingCredits(MapDefinition map, DifficultyDefinition difficulty)
    {
        var baseCredits = map.StartingCredits > 0 ? map.StartingCredits : Core.GameConstants.StartingCredits;
        return Math.Max(0, (int)MathF.Round(baseCredits * difficulty.StartingCreditsMultiplier / 5f) * 5);
    }
}

public sealed class TacticsDefinition
{
    public EmergencyDefenseDefinition EmergencyDefense { get; set; } = new();
    public GeneratorDefinition Generator { get; set; } = new();
}

public sealed class EmergencyDefenseDefinition
{
    public string Id { get; set; } = "pulse_plate";
    public string DisplayName { get; set; } = "Pulse Plate";
    public int PurchaseCost { get; set; } = 60;
    public int DirectPurchaseCostIncrease { get; set; } = 15;
    public int StartingInventory { get; set; } = 1;
    public int MaximumActive { get; set; } = 16;
    public int Charges { get; set; } = 2;
    public float Damage { get; set; } = 38;
    public float BlastRadius { get; set; } = 52;
    public float TriggerRadius { get; set; } = 22;
    public float ArmTime { get; set; } = 0.2f;
    public float TriggerCooldown { get; set; } = 0.12f;
    public float StunDuration { get; set; } = 0.35f;
    public float SlowPercent { get; set; } = 0.30f;
    public float SlowDuration { get; set; } = 1.5f;
    public float KnockbackDistance { get; set; } = 28;
    public float KnockbackGraceSeconds { get; set; } = 0.75f;
    public float EliteKnockbackMultiplier { get; set; } = 0.60f;
    public float BossKnockbackMultiplier { get; set; } = 0.25f;
    public float ArmorPierce { get; set; } = 2;
    public float PlacementRoadTolerance { get; set; } = 4;
    public float MinimumSpacing { get; set; } = 28;
    public float EndpointClearance { get; set; } = 48;
    public TowerVisualData Visual { get; set; } = new() { Shape = "diamond", Primary = "#E8B637", Accent = "#EC5062", Radius = 13, Marks = 2, Ring = true };
}

public sealed class GeneratorDefinition
{
    public string Id { get; set; } = "charge_forge";
    public string DisplayName { get; set; } = "Charge Forge";
    public int PurchaseCost { get; set; } = 320;
    public TowerVisualData Visual { get; set; } = new() { Shape = "hexagon", Primary = "#2AC275", Accent = "#E8B637", Radius = 22, Marks = 3, Ring = true };
    public List<GeneratorLevelDefinition> Levels { get; set; } =
    [
        new() { ProductionSeconds = 34, Capacity = 3, DefenseDamageBonus = 0, UpgradeCost = 180 },
        new() { ProductionSeconds = 26, Capacity = 4, DefenseDamageBonus = 0.15f, UpgradeCost = 250 },
        new() { ProductionSeconds = 20, Capacity = 5, DefenseDamageBonus = 0.30f }
    ];
}

public sealed class GeneratorLevelDefinition
{
    public float ProductionSeconds { get; set; }
    public int Capacity { get; set; }
    public float DefenseDamageBonus { get; set; }
    public int? UpgradeCost { get; set; }
}

public sealed class TowerDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public string Behavior { get; set; } = "single_projectile";
    public int PurchaseCost { get; set; }
    public string DefaultTargetMode { get; set; } = "First";
    public TowerVisualData Visual { get; set; } = new();
    public TowerProtocolDefinition Protocol { get; set; } = new();
    public List<TowerLevelDefinition> Levels { get; set; } = new();
    public List<TowerSpecializationDefinition> Specializations { get; set; } = new();
}

public sealed class TowerProtocolDefinition
{
    public string DisplayName { get; set; } = "Overdrive";
    public string Summary { get; set; } = "Temporarily increases attack speed.";
    public float DurationSeconds { get; set; } = GameConstants.OverdriveDurationSeconds;
    public float CooldownSeconds { get; set; } = GameConstants.OverdriveCooldownSeconds;
    public float AttackSpeedBonus { get; set; } = GameConstants.OverdriveAttackSpeedBonus;
    public float DamageBonus { get; set; }
    public float RangeBonus { get; set; }
    public float ArmorPierceBonus { get; set; }
    public float AuraAttackSpeedBonus { get; set; }
    public float AuraRangeBonus { get; set; }
    public int AutoTriggerCount { get; set; } = 4;
    public float BurstRadius { get; set; }
    public float BurstDamage { get; set; }
    public string BurstStatus { get; set; } = "";
    public float BurstStatusMagnitude { get; set; }
    public float BurstStatusDuration { get; set; }
}

public sealed class TowerSpecializationDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ShortLabel { get; set; } = "";
    public string Summary { get; set; } = "";
    public int UpgradeCost { get; set; }
    public TowerLevelDefinition Level { get; set; } = new();
}

public sealed class TowerLevelDefinition
{
    public float Range { get; set; }
    public float Damage { get; set; }
    public float AttacksPerSecond { get; set; }
    public float ProjectileSpeed { get; set; }
    public int? UpgradeCost { get; set; }
    public int PelletCount { get; set; } = 1;
    public float PelletSpreadDegrees { get; set; }
    public float SplashRadius { get; set; }
    public int SplashTargetLimit { get; set; }
    public int ChainCount { get; set; }
    public float ChainDamage { get; set; }
    public float ChainRange { get; set; }
    public float SlowPercent { get; set; }
    public float SlowDuration { get; set; }
    public float BurnDamagePerSecond { get; set; }
    public float BurnDuration { get; set; }
    public float BurnTickInterval { get; set; } = 0.5f;
    public float ArmorPierce { get; set; }
    public float ArmorReduction { get; set; }
    public float ArmorReductionDuration { get; set; }
    public float ExposePercent { get; set; }
    public float ExposeDuration { get; set; }
    public float StunDuration { get; set; }
    public float AuraRange { get; set; }
    public float AuraAttackSpeedBonus { get; set; }
    public float AuraRangeBonus { get; set; }
    public bool IgnoreShield { get; set; }
}

public sealed class TowerVisualData
{
    public string Shape { get; set; } = "circle";
    public string Primary { get; set; } = "#FFFFFF";
    public string Accent { get; set; } = "#FFFFFF";
    public int Radius { get; set; } = 18;
    public int Marks { get; set; }
    public bool Ring { get; set; }

    public Color PrimaryColor => ParseColor(Primary);
    public Color AccentColor => ParseColor(Accent);

    public static Color ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Color.White;
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return Color.White;
    }
}

public sealed class EnemyDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float MaxHealth { get; set; }
    public float Speed { get; set; }
    public int Reward { get; set; }
    public int LivesLost { get; set; } = 1;
    public float Armor { get; set; }
    public float Shield { get; set; }
    public float RegenerationPerSecond { get; set; }
    public EnemyVisualData Visual { get; set; } = new();
}

public sealed class EnemyVisualData
{
    public string Shape { get; set; } = "circle";
    public string Primary { get; set; } = "#FFFFFF";
    public string Accent { get; set; } = "#FFFFFF";
    public int Radius { get; set; } = 14;
    public int Marks { get; set; }
    public bool Ring { get; set; }

    public Color PrimaryColor => TowerVisualData.ParseColor(Primary);
    public Color AccentColor => TowerVisualData.ParseColor(Accent);
}

public sealed class MapDefinition
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "A balanced tactical arena.";
    public int ChallengeRating { get; set; } = 2;
    public LogicalSizeData LogicalSize { get; set; } = new();
    public BackgroundData Background { get; set; } = new();
    public PathVisualData PathVisual { get; set; } = new();
    public PointData Spawn { get; set; } = new();
    public PointData Goal { get; set; } = new();
    public int PathWidth { get; set; } = 56;
    public List<PointData> Path { get; set; } = new();
    public List<RectangleData> BuildableRegions { get; set; } = new();
    public List<RectangleData> RestrictedRegions { get; set; } = new();
    public List<PowerNodeData> PowerNodes { get; set; } = new();
    public string WaveSet { get; set; } = "";
    public int StartingLives { get; set; } = 20;
    public int StartingCredits { get; set; } = 300;
}

public sealed class PathVisualData
{
    public string Style { get; set; } = "road";
    public string Base { get; set; } = "#384E65";
    public string Accent { get; set; } = "#E8B637";
    public string Secondary { get; set; } = "#2192AA";
    public Color BaseColor => TowerVisualData.ParseColor(Base);
    public Color AccentColor => TowerVisualData.ParseColor(Accent);
    public Color SecondaryColor => TowerVisualData.ParseColor(Secondary);
}

public sealed class PowerNodeData
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "Surge Node";
    public PointData Position { get; set; } = new();
    public float Radius { get; set; } = 80;
    public float AttackSpeedBonus { get; set; }
    public float RangeBonus { get; set; }
    public float DamageBonus { get; set; }
    public float ArmorPierceBonus { get; set; }
    public string Color { get; set; } = "#2192AA";
    public Color NodeColor => TowerVisualData.ParseColor(Color);
}

public sealed class LogicalSizeData
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
}

public sealed class BackgroundData
{
    public string Base { get; set; } = "#152D36";
    public string Accent { get; set; } = "#254A57";
    public Color BaseColor => TowerVisualData.ParseColor(Base);
    public Color AccentColor => TowerVisualData.ParseColor(Accent);
}

public sealed class PointData
{
    public float X { get; set; }
    public float Y { get; set; }
    public Vector2 ToVector2() => new(X, Y);
}

public sealed class RectangleData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Rectangle ToRectangle() => new(X, Y, Width, Height);
}

public sealed class WaveSetDefinition
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string MapId { get; set; } = "";
    public List<WaveDefinition> Waves { get; set; } = new();
}

public sealed class WaveDefinition
{
    public int Number { get; set; }
    public string Archetype { get; set; } = "Standard";
    public string Briefing { get; set; } = "";
    public float HealthMultiplier { get; set; } = 1f;
    public float SpeedMultiplier { get; set; } = 1f;
    public List<WaveGroupDefinition> Groups { get; set; } = new();
}

public sealed class WaveGroupDefinition
{
    public string EnemyId { get; set; } = "";
    public string Rank { get; set; } = "Standard";
    public int Count { get; set; }
    public float SpawnInterval { get; set; } = 1f;
    public float DelayBefore { get; set; }
}

public static class ContentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
