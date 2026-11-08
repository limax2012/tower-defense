using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

public enum EnemyCondition
{
    None,
    Stun,
    Slow,
    ArmorBreak,
    Exposed,
    Burn,
    Regeneration,
    Shield,
    Acceleration
}

/// <summary>Complete active state with a stable tactical order.</summary>
public readonly record struct EnemyConditionComposition(
    EnemyCondition FirstIndicator, EnemyCondition SecondIndicator, int ActiveCount)
{
    public uint ActiveMask { get; init; }

    public int IndicatorCount => ActiveCount;

    public bool Has(EnemyCondition condition) => condition != EnemyCondition.None &&
        (ActiveMask & 1u << (int)condition) != 0;
}

/// <summary>Read-only presentation of existing conditions; ordering does not depend on application time.</summary>
public static class EnemyConditionPresentation
{
    private static readonly EnemyCondition[] Priority =
    [
        EnemyCondition.Stun, EnemyCondition.Slow, EnemyCondition.Shield, EnemyCondition.Burn,
        EnemyCondition.ArmorBreak, EnemyCondition.Exposed, EnemyCondition.Regeneration, EnemyCondition.Acceleration
    ];

    public static EnemyConditionComposition Compose(EnemyInstance enemy)
    {
        var first = EnemyCondition.None;
        var second = EnemyCondition.None;
        var count = 0;
        var mask = ActiveMask(enemy);
        foreach (var condition in Priority)
        {
            if ((mask & 1u << (int)condition) == 0) continue;
            if (count == 0) first = condition;
            else if (count == 1) second = condition;
            count++;
        }
        return new EnemyConditionComposition(first, second, count) { ActiveMask = mask };
    }

    public static int CopyActive(EnemyInstance enemy, Span<EnemyCondition> destination)
    {
        var count = 0;
        var mask = ActiveMask(enemy);
        foreach (var condition in Priority)
        {
            if ((mask & 1u << (int)condition) == 0) continue;
            if (count == destination.Length) break;
            destination[count++] = condition;
        }
        return count;
    }

    public static bool IsPresent(EnemyInstance enemy, EnemyCondition condition) =>
        condition != EnemyCondition.None && (ActiveMask(enemy) & 1u << (int)condition) != 0;

    public static Color ExposeAccent(GameSession session, EnemyInstance enemy)
    {
        var magnitude = float.NegativeInfinity;
        var sourceId = int.MaxValue;
        foreach (var status in enemy.StatusEffects.Active)
        {
            if (status.Type != StatusType.Exposed || status.Magnitude < magnitude ||
                status.Magnitude == magnitude && status.SourceId >= sourceId) continue;
            magnitude = status.Magnitude;
            sourceId = status.SourceId;
        }
        return session.Statistics.TowerDefinitionByInstance.TryGetValue(sourceId, out var definition) &&
            definition == "watchtower" ? ColorPalette.PrecisionShot : ColorPalette.Expose;
    }

    private static uint ActiveMask(EnemyInstance enemy)
    {
        if (enemy.IsDead || enemy.HasEscaped) return 0;
        var mask = 0u;
        var statuses = enemy.StatusEffects.Active;
        for (var index = 0; index < statuses.Count; index++)
        {
            var status = statuses[index];
            if (status.Magnitude <= 0) continue;
            var condition = status.Type switch
            {
                StatusType.Stun => EnemyCondition.Stun,
                StatusType.Slow => EnemyCondition.Slow,
                StatusType.ArmorBreak => EnemyCondition.ArmorBreak,
                StatusType.Exposed => EnemyCondition.Exposed,
                StatusType.Burn => EnemyCondition.Burn,
                _ => EnemyCondition.None
            };
            if (condition != EnemyCondition.None) mask |= 1u << (int)condition;
        }
        if (EnemyStatusArt.IsRegenerating(enemy)) mask |= 1u << (int)EnemyCondition.Regeneration;
        if (enemy.Shield > 0) mask |= 1u << (int)EnemyCondition.Shield;
        if (enemy.FormationSpeedMultiplier > 1.001f) mask |= 1u << (int)EnemyCondition.Acceleration;
        return mask;
    }

    public static string Name(EnemyCondition condition) => condition switch
    {
        EnemyCondition.Stun => "STUN",
        EnemyCondition.Slow => "SLOW",
        EnemyCondition.ArmorBreak => "ARMOR BREAK",
        EnemyCondition.Exposed => "EXPOSE",
        EnemyCondition.Burn => "BURN",
        EnemyCondition.Regeneration => "REPAIRING",
        EnemyCondition.Shield => "SHIELD",
        EnemyCondition.Acceleration => "ACCELERATED",
        _ => "NONE"
    };

    public static Color Accent(EnemyCondition condition) => condition switch
    {
        EnemyCondition.Stun => ColorPalette.ArcShot,
        EnemyCondition.Slow => ColorPalette.Slow,
        EnemyCondition.ArmorBreak => ColorPalette.ArmorBreak,
        EnemyCondition.Exposed => ColorPalette.Expose,
        EnemyCondition.Burn => ColorPalette.Burn,
        EnemyCondition.Regeneration => ColorPalette.Green,
        EnemyCondition.Shield => ColorPalette.Shield,
        EnemyCondition.Acceleration => ColorPalette.Acceleration,
        _ => ColorPalette.Muted
    };

    public static string Detail(EnemyInstance enemy, EnemyCondition condition) => condition switch
    {
        EnemyCondition.Stun => $"MOVEMENT PAUSED / {Remaining(enemy, StatusType.Stun):0.0}s",
        EnemyCondition.Slow => $"SPEED -{EnemyInstance.EffectiveSlowFraction(enemy.StatusEffects.SlowFactor):P0} / {Remaining(enemy, StatusType.Slow):0.0}s",
        EnemyCondition.ArmorBreak => $"ARMOR -{enemy.StatusEffects.ArmorReduction:0.#} / {Remaining(enemy, StatusType.ArmorBreak):0.0}s",
        EnemyCondition.Exposed => $"DAMAGE +{enemy.StatusEffects.DamageMultiplier - 1:P0} / {Remaining(enemy, StatusType.Exposed):0.0}s",
        EnemyCondition.Burn => $"{BurnDamage(enemy):0.#} DAMAGE/S / {Remaining(enemy, StatusType.Burn):0.0}s",
        EnemyCondition.Regeneration => $"+{enemy.Definition.RegenerationPerSecond:0.#} HEALTH/S",
        EnemyCondition.Shield => $"{enemy.Shield:0.#} SHIELD REMAINING",
        EnemyCondition.Acceleration => $"FORMATION SPEED +{enemy.FormationSpeedMultiplier - 1:P0}",
        _ => ""
    };

    private static float Remaining(EnemyInstance enemy, StatusType type)
    {
        var remaining = 0f;
        foreach (var status in enemy.StatusEffects.Active)
            if (status.Type == type) remaining = Math.Max(remaining, status.RemainingSeconds);
        return remaining;
    }

    private static float BurnDamage(EnemyInstance enemy)
    {
        var damage = 0f;
        foreach (var status in enemy.GetActiveBurns())
            damage += enemy.GetExpectedBurnDamagePerSecond(status);
        return damage;
    }
}
