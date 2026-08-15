using MinimalBastion.Data;
using MinimalBastion.Persistence;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Tactics;

public sealed class PulsePlateInstance
{
    private readonly HashSet<int> _handledEnemyIds = new();
    public int Id { get; }
    public int OwnerPlayerId { get; }
    public Vector2 Position { get; }
    public EmergencyDefenseDefinition Definition { get; }
    public int ChargesRemaining { get; private set; }
    public float ArmRemaining { get; private set; }
    public float CooldownRemaining { get; private set; }
    public IReadOnlyCollection<int> HandledEnemyIds => _handledEnemyIds;
    public int DamageSourceId => -100_000 - Id;
    public bool IsExpired => ChargesRemaining <= 0;

    public PulsePlateInstance(int id, Vector2 position, EmergencyDefenseDefinition definition, int ownerPlayerId = 1)
    {
        Id = id;
        OwnerPlayerId = ownerPlayerId;
        Position = position;
        Definition = definition;
        ChargesRemaining = definition.Charges;
        ArmRemaining = definition.ArmTime;
    }

    public void Tick(float deltaSeconds)
    {
        ArmRemaining = MathF.Max(0, ArmRemaining - deltaSeconds);
        CooldownRemaining = MathF.Max(0, CooldownRemaining - deltaSeconds);
    }

    public bool CanTrigger(int enemyId) =>
        !IsExpired && ArmRemaining <= 0 && CooldownRemaining <= 0 && !_handledEnemyIds.Contains(enemyId);

    public void RetainHandledEnemies(IEnumerable<int> enemyIdsStillOnPlate)
    {
        _handledEnemyIds.IntersectWith(enemyIdsStillOnPlate);
    }

    public void Trigger(int triggeringEnemyId)
    {
        if (!CanTrigger(triggeringEnemyId)) return;
        ChargesRemaining--;
        CooldownRemaining = Definition.TriggerCooldown;
        _handledEnemyIds.Add(triggeringEnemyId);
    }

    public PulsePlateSaveData CaptureSaveData() => new()
    {
        Id = Id,
        OwnerPlayerId = OwnerPlayerId,
        X = Position.X,
        Y = Position.Y,
        ChargesRemaining = ChargesRemaining,
        ArmRemaining = ArmRemaining,
        CooldownRemaining = CooldownRemaining,
        HandledEnemyIds = _handledEnemyIds.ToList()
    };

    public static PulsePlateInstance RestoreSaveData(PulsePlateSaveData data, EmergencyDefenseDefinition definition)
    {
        var plate = new PulsePlateInstance(data.Id, new Vector2(data.X, data.Y), definition, data.OwnerPlayerId)
        {
            ChargesRemaining = Math.Clamp(data.ChargesRemaining, 0, definition.Charges),
            ArmRemaining = MathF.Max(0, data.ArmRemaining),
            CooldownRemaining = MathF.Max(0, data.CooldownRemaining)
        };
        foreach (var enemyId in data.HandledEnemyIds.Where(x => x > 0)) plate._handledEnemyIds.Add(enemyId);
        return plate;
    }
}

public sealed class ChargeForgeInstance
{
    public int OwnerPlayerId { get; }
    public Vector2 Position { get; }
    public GeneratorDefinition Definition { get; }
    public int LevelIndex { get; private set; }
    public int InvestedCredits { get; private set; }
    public float ProductionRemaining { get; private set; }
    public GeneratorLevelDefinition Level => Definition.Levels[LevelIndex];
    public bool CanUpgrade => LevelIndex < Definition.Levels.Count - 1 && Level.UpgradeCost.HasValue;
    public int UpgradeCost => Level.UpgradeCost ?? 0;
    public int SellValue => (int)MathF.Floor(InvestedCredits * Core.GameConstants.SellRatio);
    public float ProductionProgress => 1f - MathHelper.Clamp(ProductionRemaining / MathF.Max(0.01f, Level.ProductionSeconds), 0, 1);

    public ChargeForgeInstance(Vector2 position, GeneratorDefinition definition, int ownerPlayerId = 1)
    {
        OwnerPlayerId = ownerPlayerId;
        Position = position;
        Definition = definition;
        InvestedCredits = definition.PurchaseCost;
        ProductionRemaining = definition.Levels[0].ProductionSeconds;
    }

    public bool Update(float deltaSeconds, bool inventoryFull)
    {
        if (inventoryFull) return false;
        ProductionRemaining -= deltaSeconds;
        if (ProductionRemaining > 0) return false;
        ProductionRemaining = Level.ProductionSeconds;
        return true;
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade) return false;
        InvestedCredits += UpgradeCost;
        LevelIndex++;
        ProductionRemaining = MathF.Min(ProductionRemaining, Level.ProductionSeconds);
        return true;
    }

    public GeneratorSaveData CaptureSaveData() => new()
    {
        OwnerPlayerId = OwnerPlayerId,
        X = Position.X,
        Y = Position.Y,
        LevelIndex = LevelIndex,
        InvestedCredits = InvestedCredits,
        ProductionRemaining = ProductionRemaining
    };

    public static ChargeForgeInstance RestoreSaveData(GeneratorSaveData data, GeneratorDefinition definition)
    {
        if (data.LevelIndex < 0 || data.LevelIndex >= definition.Levels.Count)
            throw new InvalidDataException("Saved Charge Forge level is invalid.");
        var forge = new ChargeForgeInstance(new Vector2(data.X, data.Y), definition, data.OwnerPlayerId)
        {
            LevelIndex = data.LevelIndex,
            InvestedCredits = Math.Max(definition.PurchaseCost, data.InvestedCredits),
            ProductionRemaining = MathHelper.Clamp(data.ProductionRemaining, 0, definition.Levels[data.LevelIndex].ProductionSeconds)
        };
        return forge;
    }
}
