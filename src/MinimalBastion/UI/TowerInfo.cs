using MinimalBastion.Data;
using MinimalBastion.Maps;
using MinimalBastion.Towers;

namespace MinimalBastion.UI;

public static class TowerInfo
{
    public static float RawDps(TowerLevelDefinition level) => level.Damage * level.AttacksPerSecond * Math.Max(1, level.PelletCount);

    public static int TotalCostToLevel(TowerDefinition definition, int levelIndex)
    {
        var clampedLevel = Math.Clamp(levelIndex, 0, Math.Max(0, definition.Levels.Count - 1));
        var total = definition.PurchaseCost;
        for (var index = 0; index < clampedLevel; index++)
            total += definition.Levels[index].UpgradeCost ?? 0;
        return total;
    }

    public static int TotalCostToSpecialization(TowerDefinition definition, TowerSpecializationDefinition specialization) =>
        definition.PurchaseCost + Tier2Cost(definition) + specialization.UpgradeCost;

    public static int TotalCostToSpecialization(TowerDefinition definition, TowerDoctrineDefinition doctrine,
        TowerSpecializationDefinition specialization) =>
        definition.PurchaseCost + doctrine.UpgradeCost + specialization.UpgradeCost;

    public static int TotalCostToDoctrine(TowerDefinition definition, TowerDoctrineDefinition doctrine) =>
        definition.PurchaseCost + doctrine.UpgradeCost;

    private static int Tier2Cost(TowerDefinition definition) => definition.Tier2Doctrines.Count > 0
        ? definition.Tier2Doctrines.Min(x => x.UpgradeCost)
        : definition.Levels.FirstOrDefault()?.UpgradeCost ?? 0;

    public static string ProtocolBonuses(TowerProtocolDefinition protocol, int maximumBonuses = 3)
    {
        var bonuses = new List<string>();
        if (protocol.AttackSpeedBonus > 0) bonuses.Add($"RATE +{protocol.AttackSpeedBonus:P0}");
        if (protocol.DamageBonus > 0) bonuses.Add($"DAMAGE +{protocol.DamageBonus:P0}");
        if (protocol.RangeBonus > 0) bonuses.Add($"RANGE +{protocol.RangeBonus:P0}");
        if (protocol.ArmorPierceBonus > 0) bonuses.Add($"PIERCE +{protocol.ArmorPierceBonus:0.#}");
        if (protocol.AuraAttackSpeedBonus > 0) bonuses.Add($"AURA RATE +{protocol.AuraAttackSpeedBonus:P0}");
        if (protocol.AuraRangeBonus > 0) bonuses.Add($"AURA/TOWER RANGE +{protocol.AuraRangeBonus:P0}");
        if (protocol.BurstDamage > 0) bonuses.Add($"PULSE {protocol.BurstDamage:0.#}");
        if (!string.IsNullOrWhiteSpace(protocol.BurstStatus)) bonuses.Add(ProtocolStatusBonus(protocol));
        return string.Join("  ", bonuses.Take(Math.Max(0, maximumBonuses)));
    }

    public static string ProtocolSummary(TowerDefinition definition) =>
        $"PROTOCOL: {definition.Protocol.DisplayName.ToUpperInvariant()}  {definition.Protocol.DurationSeconds:0.#}s  |  {ProtocolBonuses(definition.Protocol)}";

    public static string ProtocolLibrarySummary(TowerDefinition definition) =>
        $"PROTOCOL: {definition.Protocol.DisplayName.ToUpperInvariant()}  {definition.Protocol.DurationSeconds:0.#}s / CD {definition.Protocol.CooldownSeconds:0.#}s  |  {ProtocolBonuses(definition.Protocol, int.MaxValue)}  |  AUTO {definition.Protocol.AutoTriggerCount}+ / ELITE/BOSS";

    private static string ProtocolStatusBonus(TowerProtocolDefinition protocol) =>
        protocol.BurstStatus.ToLowerInvariant() switch
        {
            "slow" => $"SLOW {protocol.BurstStatusMagnitude:P0}/{protocol.BurstStatusDuration:0.##}s",
            "burn" => $"BURN {protocol.BurstStatusMagnitude:0.#}/s/{protocol.BurstStatusDuration:0.##}s",
            "exposed" => $"EXPOSE +{protocol.BurstStatusMagnitude:P0}/{protocol.BurstStatusDuration:0.##}s",
            "armorbreak" => $"BREAK {protocol.BurstStatusMagnitude:0.#}/{protocol.BurstStatusDuration:0.##}s",
            "stun" => $"STUN {protocol.BurstStatusDuration:0.##}s",
            _ => protocol.BurstStatus.ToUpperInvariant()
        };

    public static IReadOnlyList<string> LibraryStatLines(TowerDefinition definition, TowerLevelDefinition level)
    {
        var lines = new List<string>();
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"AURA RANGE  {level.AuraRange:0}");
            lines.Add($"ATTACK RATE  +{level.AuraAttackSpeedBonus:P0}");
            lines.Add($"TOWER RANGE  +{level.AuraRangeBonus:P0}");
            lines.Add("AURAS USE THE STRONGEST BEACON");
            return lines;
        }

        lines.Add($"DAMAGE  {level.Damage:0.#}    RATE  {level.AttacksPerSecond:0.##}/s");
        var outputLabel = definition.Behavior.Equals("pellet_burst", StringComparison.OrdinalIgnoreCase)
            ? "BURST DPS"
            : definition.Behavior.Equals("chain", StringComparison.OrdinalIgnoreCase)
                ? "PRIMARY DPS"
                : "DIRECT DPS";
        lines.Add($"{outputLabel}  {RawDps(level):0.#}    RANGE  {level.Range:0}");
        if (level.ProjectileSpeed > 0) lines.Add($"PROJECTILE SPEED  {level.ProjectileSpeed:0}");
        if (level.PelletCount > 1) lines.Add($"PROJECTILES  {level.PelletCount}    SPREAD  {level.PelletSpreadDegrees:0.#} deg");
        if (level.SplashRadius > 0) lines.Add($"SPLASH RADIUS  {level.SplashRadius:0.#}");
        if (level.SplashTargetLimit > 0) lines.Add($"IMPACT CAP  {level.SplashTargetLimit} TARGETS");
        if (level.SlowPercent > 0) lines.Add($"SLOW  {level.SlowPercent:P0} FOR {level.SlowDuration:0.#}s");
        if (level.BurnDamagePerSecond > 0)
        {
            lines.Add($"BURN  {level.BurnDamagePerSecond:0.#}/s FOR {level.BurnDuration:0.#}s");
            lines.Add("BURNING REDUCES ARMOR BY 2");
        }
        if (level.ChainCount > 0)
        {
            var maximumDps = (level.Damage + level.ChainCount * level.ChainDamage) * level.AttacksPerSecond;
            lines.Add($"CHAINS  {level.ChainCount}    DAMAGE  {level.ChainDamage:0.#}    REACH  {level.ChainRange:0}");
            lines.Add($"MAX CHAIN DPS  {maximumDps:0.#}    SLOWED +35%");
        }
        if (level.ArmorPierce > 0) lines.Add($"ARMOR PIERCE  {level.ArmorPierce:0.#}");
        if (level.ArmorReduction > 0) lines.Add($"ARMOR BREAK  {level.ArmorReduction:0.#} FOR {level.ArmorReductionDuration:0.#}s");
        if (level.ExposePercent > 0) lines.Add($"EXPOSE  +{level.ExposePercent:P0} FOR {level.ExposeDuration:0.#}s");
        if (level.StunDuration > 0) lines.Add($"STUN  {level.StunDuration:0.##}s");
        if (level.IgnoreShield) lines.Add("IGNORES SHIELDS");
        return lines;
    }

    public static string ShortRole(TowerDefinition definition) => definition.Behavior.ToLowerInvariant() switch
    {
        "single_projectile" when definition.Id == "watchtower" => "Long range",
        "single_projectile" => "General",
        "pellet_burst" => "Swarm",
        "slow_projectile" => "Control",
        "burn_projectile" => "Burn",
        "armor_projectile" => "Anti-armor",
        "chain" => "Chain",
        "splash_projectile" => "Splash",
        "beam" => "Beam",
        "aura" => "Support",
        _ => string.IsNullOrWhiteSpace(definition.Role) ? "Utility" : definition.Role
    };

    public static string Special(TowerDefinition definition, TowerLevelDefinition level)
    {
        return definition.Behavior.ToLowerInvariant() switch
        {
            "pellet_burst" => level.ArmorPierce > 0
                ? $"{level.PelletCount} projectiles per burst; pierce {level.ArmorPierce:0}"
                : $"{level.PelletCount} projectiles per burst",
            "slow_projectile" => $"AoE {level.SplashRadius:0}; slow {level.SlowPercent:P0} for {level.SlowDuration:0.#}s",
            "burn_projectile" => level.SplashRadius > 0
                ? $"Burn {level.BurnDamagePerSecond:0.#}/s; AoE {level.SplashRadius:0}; scorched armor -2"
                : $"Burn {level.BurnDamagePerSecond:0.#}/s; scorched armor -2",
            "armor_projectile" => level.ArmorReduction > 0 ? $"Pierce {level.ArmorPierce:0}; break {level.ArmorReduction:0}" : $"Armor pierce {level.ArmorPierce:0}",
            "chain" => $"Chain {level.ChainCount}; +35% damage to slowed",
            "splash_projectile" => level.SplashTargetLimit > 0
                ? $"Splash radius {level.SplashRadius:0}; up to {level.SplashTargetLimit} targets"
                : $"Splash radius {level.SplashRadius:0}",
            "beam" => $"Expose: +{level.ExposePercent:P0} all incoming damage for {level.ExposeDuration:0.#}s",
            "aura" => $"Aura +{level.AuraAttackSpeedBonus:P0} rate, +{level.AuraRangeBonus:P0} range",
            _ => "Reliable direct projectile"
        };
    }

    public static string Strength(TowerDefinition definition) => definition.Behavior.ToLowerInvariant() switch
    {
        "single_projectile" when definition.Id == "watchtower" => "Strength: priority targets at long range",
        "single_projectile" => "Strength: efficient general coverage",
        "pellet_burst" => "Strength: separated weak targets",
        "slow_projectile" => "Strength: slows and chips clustered enemies",
        "burn_projectile" => "Strength: persistent damage and armor setup",
        "armor_projectile" => "Strength: armored enemies",
        "chain" => "Strength: dense groups; Frost synergy",
        "splash_projectile" => "Strength: tightly packed swarms",
        "beam" => "Strength: focused pressure on durable targets",
        "aura" => "Strength: multiplies clustered towers",
        _ => "Strength: flexible defense"
    };

    public static string Limitation(TowerDefinition definition) => definition.Behavior.ToLowerInvariant() switch
    {
        "single_projectile" when definition.Id == "watchtower" => "Limit: slow fire and high cost",
        "single_projectile" => "Limit: armor reduces each small hit",
        "pellet_burst" => "Limit: short range; weak into armor",
        "slow_projectile" => "Limit: very low direct damage",
        "burn_projectile" => "Limit: needs time; armor checks each tick",
        "armor_projectile" => "Limit: ordinary against light targets",
        "chain" => "Limit: weak against isolated enemies",
        "splash_projectile" => "Limit: expensive; weak against spread targets",
        "beam" => "Limit: expensive and armor-sensitive",
        "aura" => "Limit: overlapping Beacons do not stack",
        _ => "Limit: no specialized counter"
    };

    public static string UpgradeSummary(TowerDefinition definition, int levelIndex) =>
        UpgradeSummary(definition, levelIndex, default, default);

    public static string UpgradeSummary(
        TowerDefinition definition,
        int levelIndex,
        TowerBuff supportBuff,
        MapPowerBuff powerBuff)
    {
        if (levelIndex >= definition.Levels.Count - 1) return "Maximum level reached";
        var current = definition.Levels[levelIndex];
        var next = definition.Levels[levelIndex + 1];
        var changes = new List<string>();

        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
        {
            Add("AURA", current.AuraRange, next.AuraRange, "0");
            AddPercent("RATE", current.AuraAttackSpeedBonus, next.AuraAttackSpeedBonus);
            AddPercent("RANGE", current.AuraRangeBonus, next.AuraRangeBonus);
            return string.Join("  ", changes.Take(3));
        }

        var damageMultiplier = 1f + powerBuff.DamageBonus;
        var attackSpeedMultiplier = 1f + supportBuff.AttackSpeedBonus + powerBuff.AttackSpeedBonus;
        var rangeMultiplier = 1f + supportBuff.RangeBonus + powerBuff.RangeBonus;
        Add("DAMAGE", current.Damage * damageMultiplier, next.Damage * damageMultiplier, "0.#");
        Add("RATE", current.AttacksPerSecond * attackSpeedMultiplier, next.AttacksPerSecond * attackSpeedMultiplier, "0.##");
        Add("RANGE", current.Range * rangeMultiplier, next.Range * rangeMultiplier, "0");
        if (next.PelletCount != current.PelletCount) changes.Add($"SHOT {current.PelletCount}>{next.PelletCount}");
        if (next.SlowPercent != current.SlowPercent) changes.Add($"SLOW {current.SlowPercent:P0}>{next.SlowPercent:P0}");
        if (next.ArmorPierce != current.ArmorPierce)
            changes.Add($"PIERCE {current.ArmorPierce + powerBuff.ArmorPierceBonus:0.#}>{next.ArmorPierce + powerBuff.ArmorPierceBonus:0.#}");
        if (next.ChainCount != current.ChainCount) changes.Add($"CHAIN {current.ChainCount}>{next.ChainCount}");
        if (next.SplashTargetLimit != current.SplashTargetLimit) changes.Add($"CAP {current.SplashTargetLimit}>{next.SplashTargetLimit}");
        return string.Join("  ", changes.Take(3));

        void Add(string label, float before, float after, string format)
        {
            if (MathF.Abs(after - before) > 0.001f) changes.Add($"{label} {before.ToString(format)}>{after.ToString(format)}");
        }

        void AddPercent(string label, float before, float after)
        {
            if (MathF.Abs(after - before) > 0.001f) changes.Add($"{label} +{before:P0}>+{after:P0}");
        }
    }

    public static string DoctrineSummary(TowerDefinition definition, TowerDoctrineDefinition doctrine,
        TowerBuff supportBuff = default, MapPowerBuff powerBuff = default)
    {
        var current = definition.Levels[0];
        var next = definition.Levels[Math.Min(1, definition.Levels.Count - 1)].WithDoctrine(doctrine);
        return $"{doctrine.Summary}: {CoreChanges(current, next, supportBuff, powerBuff)}";
    }

    public static string SpecializationSummary(TowerLevelDefinition current, TowerSpecializationDefinition specialization,
        TowerDoctrineDefinition? doctrine = null, TowerBuff supportBuff = default, MapPowerBuff powerBuff = default)
    {
        var next = specialization.Level.WithDoctrine(doctrine);
        var changes = new List<string>();
        if (next.AuraAttackSpeedBonus != current.AuraAttackSpeedBonus) changes.Add($"AURA RATE +{next.AuraAttackSpeedBonus:P0}");
        if (next.AuraRangeBonus != current.AuraRangeBonus) changes.Add($"AURA RANGE +{next.AuraRangeBonus:P0}");
        if (next.AuraRange != current.AuraRange && next.AuraRange > 0) changes.Add($"FIELD {next.AuraRange:0}");
        if (MathF.Abs(next.BurnDamagePerSecond - current.BurnDamagePerSecond) > 0.001f) changes.Add($"BURN {next.BurnDamagePerSecond:0.#}/s");
        if (next.SplashRadius > 0) changes.Add($"SPLASH {next.SplashRadius:0}");
        if (next.SplashTargetLimit > 0) changes.Add($"CAP {next.SplashTargetLimit}");
        if (next.SlowPercent > current.SlowPercent) changes.Add($"SLOW {next.SlowPercent:P0}");
        if (next.ArmorPierce > current.ArmorPierce)
            changes.Add($"PIERCE {current.ArmorPierce + powerBuff.ArmorPierceBonus:0.#}>{next.ArmorPierce + powerBuff.ArmorPierceBonus:0.#}");
        if (next.PelletCount != current.PelletCount) changes.Add($"SHOTS {next.PelletCount}");
        if (next.ChainCount != current.ChainCount) changes.Add($"CHAIN {next.ChainCount}");
        if (next.ExposePercent != current.ExposePercent) changes.Add($"EXPOSE +{next.ExposePercent:P0}");
        var damageMultiplier = 1f + powerBuff.DamageBonus;
        var attackSpeedMultiplier = 1f + supportBuff.AttackSpeedBonus + powerBuff.AttackSpeedBonus;
        var rangeMultiplier = 1f + supportBuff.RangeBonus + powerBuff.RangeBonus;
        if (MathF.Abs(next.Damage - current.Damage) > 0.001f)
            changes.Add($"DAMAGE {current.Damage * damageMultiplier:0.#}>{next.Damage * damageMultiplier:0.#}");
        if (MathF.Abs(next.AttacksPerSecond - current.AttacksPerSecond) > 0.001f)
            changes.Add($"RATE {current.AttacksPerSecond * attackSpeedMultiplier:0.##}>{next.AttacksPerSecond * attackSpeedMultiplier:0.##}");
        if (MathF.Abs(next.Range - current.Range) > 0.001f)
            changes.Add($"RANGE {current.Range * rangeMultiplier:0}>{next.Range * rangeMultiplier:0}");
        return $"{specialization.Summary}: {string.Join("  ", changes.Take(3))}";
    }

    private static string CoreChanges(TowerLevelDefinition current, TowerLevelDefinition next,
        TowerBuff supportBuff = default, MapPowerBuff powerBuff = default)
    {
        var changes = new List<string>();
        if (next.AuraRange > 0) changes.Add($"FIELD {next.AuraRange:0}");
        if (next.AuraAttackSpeedBonus > 0) changes.Add($"AURA +{next.AuraAttackSpeedBonus:P0}");
        var damageMultiplier = 1f + powerBuff.DamageBonus;
        var attackSpeedMultiplier = 1f + supportBuff.AttackSpeedBonus + powerBuff.AttackSpeedBonus;
        var rangeMultiplier = 1f + supportBuff.RangeBonus + powerBuff.RangeBonus;
        if (next.Damage > 0) changes.Add($"DAMAGE {current.Damage * damageMultiplier:0.#}>{next.Damage * damageMultiplier:0.#}");
        if (next.AttacksPerSecond > 0) changes.Add($"RATE {current.AttacksPerSecond * attackSpeedMultiplier:0.##}>{next.AttacksPerSecond * attackSpeedMultiplier:0.##}");
        if (next.Range > 0) changes.Add($"RANGE {current.Range * rangeMultiplier:0}>{next.Range * rangeMultiplier:0}");
        if (next.PelletCount != current.PelletCount) changes.Add($"SHOTS {next.PelletCount}");
        if (next.ChainCount != current.ChainCount) changes.Add($"CHAIN {next.ChainCount}");
        if (next.SplashTargetLimit != current.SplashTargetLimit) changes.Add($"CAP {next.SplashTargetLimit}");
        if (next.SlowPercent > current.SlowPercent) changes.Add($"SLOW {next.SlowPercent:P0}");
        if (next.BurnDamagePerSecond > current.BurnDamagePerSecond) changes.Add($"BURN {next.BurnDamagePerSecond:0.#}/s");
        if (next.ArmorPierce > current.ArmorPierce) changes.Add($"PIERCE {next.ArmorPierce:0.#}");
        return string.Join("  ", changes.Take(3));
    }

    public static string PowerNodeBonus(PowerNodeData node)
    {
        var bonuses = new List<string>();
        if (node.AttackSpeedBonus > 0) bonuses.Add($"RATE +{node.AttackSpeedBonus:P0}");
        if (node.RangeBonus > 0) bonuses.Add($"RANGE +{node.RangeBonus:P0}");
        if (node.DamageBonus > 0) bonuses.Add($"DAMAGE +{node.DamageBonus:P0}");
        if (node.ArmorPierceBonus > 0) bonuses.Add($"PIERCE +{node.ArmorPierceBonus:0.#}");
        return string.Join("  ", bonuses);
    }

    public static string PowerNodeStatChange(TowerDefinition definition, TowerLevelDefinition level, MapPowerBuff power)
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
            return "NO COMPATIBLE COMBAT STAT CHANGE";

        var changes = new List<string>();
        if (power.DamageBonus > 0 && level.Damage > 0)
            changes.Add($"DAMAGE {level.Damage:0.#}>{level.Damage * (1 + power.DamageBonus):0.#}");
        if (power.AttackSpeedBonus > 0 && level.AttacksPerSecond > 0)
            changes.Add($"RATE {level.AttacksPerSecond:0.##}>{level.AttacksPerSecond * (1 + power.AttackSpeedBonus):0.##}/s");
        if (power.RangeBonus > 0 && level.Range > 0)
            changes.Add($"RANGE {level.Range:0}>{level.Range * (1 + power.RangeBonus):0}");
        if (power.ArmorPierceBonus > 0)
            changes.Add($"PIERCE {level.ArmorPierce:0.#}>{level.ArmorPierce + power.ArmorPierceBonus:0.#}");
        return changes.Count == 0 ? "NO COMPATIBLE COMBAT STAT CHANGE" : string.Join("  ", changes);
    }

    public static string SignalBeaconStatChange(TowerLevelDefinition level, TowerBuff buff)
    {
        if (!buff.IsActive) return "NO SIGNAL BEACON";

        var changes = new List<string>();
        if (buff.AttackSpeedBonus > 0 && level.AttacksPerSecond > 0)
            changes.Add($"RATE {level.AttacksPerSecond:0.##}>{level.AttacksPerSecond * (1 + buff.AttackSpeedBonus):0.##}/s (+{buff.AttackSpeedBonus:P0})");
        if (buff.RangeBonus > 0 && level.Range > 0)
            changes.Add($"RANGE {level.Range:0}>{level.Range * (1 + buff.RangeBonus):0} (+{buff.RangeBonus:P0})");
        return changes.Count == 0 ? "NO COMPATIBLE COMBAT STAT CHANGE" : $"SIGNAL BEACON  {string.Join("  ", changes)}";
    }
}
