using MinimalBastion.Data;
using MinimalBastion.Maps;
using MinimalBastion.Towers;

namespace MinimalBastion.UI;

public enum TowerStatDirection
{
    Unchanged,
    Increase,
    Decrease
}

public readonly record struct TowerStatDisplay(
    string Label,
    string Value,
    TowerStatDirection Direction,
    string? PreviousValue = null);

public static class TowerInfo
{
    public static string ProgressionLabel(TowerInstance tower)
    {
        var doctrine = tower.Doctrine?.ShortLabel.ToUpperInvariant();
        if (tower.Specialization is { } specialization)
        {
            var finalRole = specialization.DisplayName.ToUpperInvariant();
            return string.IsNullOrWhiteSpace(doctrine) ? finalRole : $"{doctrine}  >  {finalRole}";
        }
        return string.IsNullOrWhiteSpace(doctrine)
            ? $"LEVEL {tower.LevelIndex + 1}"
            : $"LEVEL {tower.LevelIndex + 1}  {doctrine}";
    }

    public static string ApexLibrarySummary(TowerDefinition definition)
    {
        if (definition.Apex is not { } apex) return "";
        var effects = new List<string>();
        if (apex.DamageMultiplier > 1f) effects.Add($"DAMAGE +{apex.DamageMultiplier - 1:P0}");
        if (apex.AttackSpeedMultiplier > 1f) effects.Add($"RATE +{apex.AttackSpeedMultiplier - 1:P0}");
        if (apex.RangeMultiplier > 1f) effects.Add($"RANGE +{apex.RangeMultiplier - 1:P0}");
        if (apex.UtilityMultiplier > 1f) effects.Add($"UTILITY +{apex.UtilityMultiplier - 1:P0}");
        return $"APEX  |  [X] PROMOTE {apex.UpgradeCost}  |  {string.Join("  ", effects)}";
    }

    public static float RawDps(TowerLevelDefinition level) => level.Damage * level.AttacksPerSecond * Math.Max(1, level.PelletCount);

    public static string LiveCombatSummary(float damage, float attacksPerSecond, float range) =>
        $"DAMAGE {damage:0.#}   RATE {attacksPerSecond:0.##}/s   RANGE {range:0}";

    public static string ComparisonStatText(TowerStatDisplay stat) =>
        stat.Direction == TowerStatDirection.Unchanged || string.IsNullOrWhiteSpace(stat.PreviousValue)
            ? $"{stat.Label} {stat.Value}"
            : $"{stat.Label} {stat.PreviousValue} -> {stat.Value}";

    public static string ComparisonStatValueText(TowerStatDisplay stat) =>
        stat.Direction == TowerStatDirection.Unchanged || string.IsNullOrWhiteSpace(stat.PreviousValue)
            ? stat.Value
            : $"{stat.PreviousValue} -> {stat.Value}";

    public static IReadOnlyList<TowerStatDisplay> ComparisonStats(
        TowerDefinition definition,
        TowerLevelDefinition current,
        TowerLevelDefinition? preview = null,
        TowerBuff supportBuff = default,
        MapPowerBuff powerBuff = default,
        TowerProtocolDefinition? activeProtocol = null,
        float signalDamageMultiplier = 1f,
        float signalRateMultiplier = 1f)
    {
        var authored = definition.Levels.Concat(definition.Specializations.Select(choice => choice.Level)).ToArray();
        var result = new List<TowerStatDisplay>();
        var shown = preview ?? current;
        var protocolDamage = activeProtocol?.DamageBonus ?? 0;
        var protocolRate = activeProtocol?.AttackSpeedBonus ?? 0;
        var protocolRange = activeProtocol?.RangeBonus ?? 0;
        var protocolPierce = activeProtocol?.ArmorPierceBonus ?? 0;
        var damageMultiplier = (1f + powerBuff.DamageBonus + protocolDamage) * signalDamageMultiplier;
        var rateMultiplier = (1f + supportBuff.AttackSpeedBonus + powerBuff.AttackSpeedBonus + protocolRate) * signalRateMultiplier;
        var rangeMultiplier = 1f + supportBuff.RangeBonus + powerBuff.RangeBonus + protocolRange;

        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
        {
            var protocolAuraRate = activeProtocol?.AuraAttackSpeedBonus ?? 0;
            var protocolAuraRange = activeProtocol?.AuraRangeBonus ?? 0;
            Add("FIELD", current.AuraRange * (1 + protocolAuraRange), shown.AuraRange * (1 + protocolAuraRange), "0");
            Add("RATE BONUS", current.AuraAttackSpeedBonus + protocolAuraRate, shown.AuraAttackSpeedBonus + protocolAuraRate, "P0");
            Add("RANGE BONUS", current.AuraRangeBonus + protocolAuraRange, shown.AuraRangeBonus + protocolAuraRange, "P0");
            return result;
        }

        Add("DAMAGE", current.Damage * damageMultiplier, shown.Damage * damageMultiplier, "0.#");
        Add("RATE", current.AttacksPerSecond * rateMultiplier, shown.AttacksPerSecond * rateMultiplier, "0.##", "/s");
        Add("RANGE", current.Range * rangeMultiplier, shown.Range * rangeMultiplier, "0");
        AddPotential("SPEED", level => level.ProjectileSpeed, current.ProjectileSpeed, shown.ProjectileSpeed, "0");
        AddPotential("SHOTS", level => level.PelletCount > 1 ? level.PelletCount : 0, current.PelletCount > 1 ? current.PelletCount : 0, shown.PelletCount > 1 ? shown.PelletCount : 0, "0");
        AddPotential("SPREAD", level => level.PelletSpreadDegrees, current.PelletSpreadDegrees, shown.PelletSpreadDegrees, "0.#", "deg");
        AddPotential("AREA", level => level.SplashRadius, current.SplashRadius, shown.SplashRadius, "0");
        AddPotential("CAP", level => level.SplashTargetLimit, current.SplashTargetLimit, shown.SplashTargetLimit, "0");
        AddPotential("RICOCHET", level => level.RicochetDamageMultiplier, current.RicochetDamageMultiplier, shown.RicochetDamageMultiplier, "P0");
        AddPotential("RICO RANGE", level => level.RicochetRange, current.RicochetRange, shown.RicochetRange, "0");
        AddPotential("SLOW", level => level.SlowPercent, current.SlowPercent, shown.SlowPercent, "P0");
        AddPotential("SLOW TIME", level => level.SlowDuration, current.SlowDuration, shown.SlowDuration, "0.#", "s");
        AddPotential("BURN", level => level.BurnDamagePerSecond, current.BurnDamagePerSecond * damageMultiplier,
            shown.BurnDamagePerSecond * damageMultiplier, "0.#", "/s");
        AddPotential("BURN TIME", level => level.BurnDuration, current.BurnDuration, shown.BurnDuration, "0.#", "s");
        AddPotential("PIERCE", level => level.ArmorPierce, current.ArmorPierce + powerBuff.ArmorPierceBonus + protocolPierce,
            shown.ArmorPierce + powerBuff.ArmorPierceBonus + protocolPierce, "0.#");
        AddPotential("BREAK", level => level.ArmorReduction, current.ArmorReduction, shown.ArmorReduction, "0.#");
        AddPotential("BREAK TIME", level => level.ArmorReductionDuration, current.ArmorReductionDuration, shown.ArmorReductionDuration, "0.#", "s");
        AddPotential("HEAVY", level => level.PriorityDamageMultiplier > 1 ? level.PriorityDamageMultiplier : 0,
            current.PriorityDamageMultiplier, shown.PriorityDamageMultiplier, "0.##", "x", prefixSuffix: true);
        AddPotential("ARCS", level => level.ChainCount, current.ChainCount, shown.ChainCount, "0");
        AddPotential("ARC DAMAGE", level => level.ChainDamage, current.ChainDamage, shown.ChainDamage, "0.#");
        AddPotential("ARC RANGE", level => level.ChainRange, current.ChainRange, shown.ChainRange, "0");
        AddPotential("EXPOSE", level => level.ExposePercent, current.ExposePercent, shown.ExposePercent, "P0");
        AddPotential("EXPOSE TIME", level => level.ExposeDuration, current.ExposeDuration, shown.ExposeDuration, "0.#", "s");
        AddPotential("STUN", level => level.StunDuration, current.StunDuration, shown.StunDuration, "0.##", "s");
        if (authored.Any(level => level.IgnoreShield))
        {
            // Keep the boolean comparison complete in the dense Intel grid.
            // With the SHIELDS label, BLOCK/PASS is both unambiguous and short
            // enough to use the same before -> after syntax as numeric stats.
            var before = current.IgnoreShield ? "PASS" : "BLOCK";
            var after = shown.IgnoreShield ? "PASS" : "BLOCK";
            var direction = preview is null || after == before
                ? TowerStatDirection.Unchanged
                : shown.IgnoreShield ? TowerStatDirection.Increase : TowerStatDirection.Decrease;
            result.Add(new TowerStatDisplay("SHIELDS", after, direction,
                direction == TowerStatDirection.Unchanged ? null : before));
        }
        return result;

        void AddPotential(string label, Func<TowerLevelDefinition, float> potential, float before, float after,
            string format, string suffix = "", bool prefixSuffix = false)
        {
            if (authored.All(level => MathF.Abs(potential(level)) < 0.001f)) return;
            Add(label, before, after, format, suffix, prefixSuffix);
        }

        void Add(string label, float before, float after, string format, string suffix = "", bool prefixSuffix = false)
        {
            var formattedBefore = before.ToString(format);
            var formattedAfter = after.ToString(format);
            var beforeText = prefixSuffix ? suffix + formattedBefore : formattedBefore + suffix;
            var afterText = prefixSuffix ? suffix + formattedAfter : formattedAfter + suffix;
            // A direction color must always correspond to a visible value
            // change. Tiny authored multiplier differences that round to the
            // same displayed value remain neutral instead of implying a gain.
            var direction = preview is null || beforeText == afterText
                ? TowerStatDirection.Unchanged
                : after > before ? TowerStatDirection.Increase : TowerStatDirection.Decrease;
            result.Add(new TowerStatDisplay(label, afterText, direction,
                direction == TowerStatDirection.Unchanged ? null : beforeText));
        }
    }

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
        return string.Join("  ", ProtocolBonusItems(protocol).Take(Math.Max(0, maximumBonuses)));
    }

    public static IReadOnlyList<string> ProtocolBonusRows(TowerProtocolDefinition protocol)
    {
        var bonuses = ProtocolBonusItems(protocol);
        if (bonuses.Count <= 3) return [string.Join("  ", bonuses)];
        var split = (bonuses.Count + 1) / 2;
        return
        [
            string.Join("  ", bonuses.Take(split)),
            string.Join("  ", bonuses.Skip(split))
        ];
    }

    private static List<string> ProtocolBonusItems(TowerProtocolDefinition protocol)
    {
        var bonuses = new List<string>();
        if (protocol.FireOnActivation) bonuses.Add("FREE VOLLEY");
        if (protocol.AttackSpeedBonus > 0) bonuses.Add($"RATE +{protocol.AttackSpeedBonus:P0}");
        if (protocol.DamageBonus > 0) bonuses.Add($"DAMAGE +{protocol.DamageBonus:P0}");
        if (protocol.RangeBonus > 0) bonuses.Add($"RANGE +{protocol.RangeBonus:P0}");
        if (protocol.ArmorPierceBonus > 0) bonuses.Add($"PIERCE +{protocol.ArmorPierceBonus:0.#}");
        if (protocol.AuraAttackSpeedBonus > 0) bonuses.Add($"AURA RATE +{protocol.AuraAttackSpeedBonus:P0}");
        if (protocol.AuraRangeBonus > 0) bonuses.Add($"AURA/TOWER RANGE +{protocol.AuraRangeBonus:P0}");
        if (protocol.BurstRadius > 0 && (protocol.BurstDamage > 0 || !string.IsNullOrWhiteSpace(protocol.BurstStatus)))
            bonuses.Add($"AREA {protocol.BurstRadius:0}");
        if (protocol.BurstDamage > 0) bonuses.Add($"PULSE {protocol.BurstDamage:0.#}");
        if (!string.IsNullOrWhiteSpace(protocol.BurstStatus)) bonuses.Add(ProtocolStatusBonus(protocol));
        return bonuses;
    }

    public static string ProtocolLiveSummary(TowerProtocolDefinition protocol)
    {
        if (protocol.BurstRadius <= 0) return ProtocolBonuses(protocol);

        var effects = new List<string>();
        effects.Add(protocol.BurstDamage > 0
            ? $"PULSE {protocol.BurstDamage:0.#} / AREA {protocol.BurstRadius:0}"
            : $"AREA {protocol.BurstRadius:0}");
        if (!string.IsNullOrWhiteSpace(protocol.BurstStatus)) effects.Add(ProtocolStatusBonus(protocol));
        if (protocol.AttackSpeedBonus > 0) effects.Add($"RATE +{protocol.AttackSpeedBonus:P0}");
        else if (protocol.AuraAttackSpeedBonus > 0) effects.Add($"AURA RATE +{protocol.AuraAttackSpeedBonus:P0}");
        if (effects.Count < 3 && protocol.DamageBonus > 0) effects.Add($"DAMAGE +{protocol.DamageBonus:P0}");
        if (effects.Count < 3 && protocol.RangeBonus > 0) effects.Add($"RANGE +{protocol.RangeBonus:P0}");
        if (effects.Count < 3 && protocol.ArmorPierceBonus > 0) effects.Add($"PIERCE +{protocol.ArmorPierceBonus:0.#}");
        return string.Join("  ", effects.Take(3));
    }

    public static string ProtocolEffectSummary(TowerProtocolDefinition protocol, bool active) =>
        $"{(active ? "ACTIVE EFFECT" : "WHEN ACTIVE")}  {ProtocolLiveSummary(protocol)}";

    public static string ProtocolSummary(TowerDefinition definition) =>
        $"PROTOCOL: {definition.Protocol.DisplayName.ToUpperInvariant()}  {definition.Protocol.DurationSeconds:0.#}s  |  {ProtocolBonuses(definition.Protocol)}";

    public static string ProtocolTimingCompact(TowerProtocolDefinition protocol) =>
        $"PROTOCOL  {protocol.DisplayName.ToUpperInvariant()}  |  ACTIVE {protocol.DurationSeconds:0.#}s  |  CD {protocol.CooldownSeconds:0.#}s";

    public static string ProtocolLibraryEffectSummary(TowerDefinition definition) =>
        $"PROTOCOL: {definition.Protocol.DisplayName.ToUpperInvariant()}  {definition.Protocol.DurationSeconds:0.#}s / CD {definition.Protocol.CooldownSeconds:0.#}s  |  {ProtocolBonuses(definition.Protocol, int.MaxValue)}";

    public static string ProtocolAutoTriggerSummary(TowerProtocolDefinition protocol) =>
        $"AUTO TRIGGER: {ProtocolAutoTriggerCondition(protocol)}, OR ANY ENGAGED ELITE / BOSS";

    public static string ProtocolAutoTriggerCompact(TowerProtocolDefinition protocol) =>
        ProtocolAutoTriggerModes.Normalize(protocol.AutoTriggerMode) switch
        {
            ProtocolAutoTriggerModes.ProtocolArea => $"{protocol.AutoTriggerCount}+ AREA / ELITE",
            ProtocolAutoTriggerModes.PriorityTargets => $"{protocol.AutoTriggerCount}+ ARMOR / ELITE",
            ProtocolAutoTriggerModes.DenseCluster => $"{protocol.AutoTriggerCount}+ CLUSTER / ELITE",
            ProtocolAutoTriggerModes.EngagedRecipients when protocol.AutoTriggerTargetCount > 0 =>
                $"{protocol.AutoTriggerCount}+ ALLIES / {protocol.AutoTriggerTargetCount}+ ON ONE / ELITE",
            ProtocolAutoTriggerModes.EngagedRecipients => $"{protocol.AutoTriggerCount}+ ALLIES / ELITE",
            _ => $"{protocol.AutoTriggerCount}+ RANGE / ELITE"
        };

    public static string ProtocolAutoTriggerBadge(TowerProtocolDefinition protocol) =>
        $"AUTO {ProtocolAutoTriggerModes.Normalize(protocol.AutoTriggerMode) switch
        {
            ProtocolAutoTriggerModes.ProtocolArea => $"{protocol.AutoTriggerCount}+ AREA",
            ProtocolAutoTriggerModes.PriorityTargets => $"{protocol.AutoTriggerCount}+ ARMOR",
            ProtocolAutoTriggerModes.DenseCluster => $"{protocol.AutoTriggerCount}+ GROUP",
            ProtocolAutoTriggerModes.EngagedRecipients => $"{protocol.AutoTriggerCount}+ ALLIES",
            _ => $"{protocol.AutoTriggerCount}+ RANGE"
        }} [A]";

    public static string ProtocolLibrarySummary(TowerDefinition definition) =>
        $"{ProtocolLibraryEffectSummary(definition)}  |  {ProtocolAutoTriggerSummary(definition.Protocol)}";

    private static string ProtocolAutoTriggerCondition(TowerProtocolDefinition protocol) =>
        ProtocolAutoTriggerModes.Normalize(protocol.AutoTriggerMode) switch
        {
            ProtocolAutoTriggerModes.ProtocolArea => $"{protocol.AutoTriggerCount}+ TARGETS IN ITS PROTOCOL AREA",
            ProtocolAutoTriggerModes.PriorityTargets => $"{protocol.AutoTriggerCount}+ ARMORED OR SHIELDED TARGETS IN RANGE",
            ProtocolAutoTriggerModes.DenseCluster => $"{protocol.AutoTriggerCount}+ TARGETS IN ONE IMPACT GROUP",
            ProtocolAutoTriggerModes.EngagedRecipients when protocol.AutoTriggerTargetCount > 0 =>
                $"{protocol.AutoTriggerCount}+ SUPPORTED TOWERS ENGAGED, OR {protocol.AutoTriggerTargetCount}+ TARGETS ON ONE RECIPIENT",
            ProtocolAutoTriggerModes.EngagedRecipients => $"{protocol.AutoTriggerCount}+ SUPPORTED TOWERS ENGAGED",
            _ => $"{protocol.AutoTriggerCount}+ TARGETS IN TOWER RANGE"
        };

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
        if (level.PriorityDamageMultiplier > 1f)
            lines.Add($"HEAVY TARGET DAMAGE  x{level.PriorityDamageMultiplier:0.##}");
        if (level.ProjectileSpeed > 0) lines.Add($"PROJECTILE SPEED  {level.ProjectileSpeed:0}");
        if (level.PelletCount > 1) lines.Add($"PROJECTILES  {level.PelletCount}    SPREAD  {level.PelletSpreadDegrees:0.#}deg");
        if (level.RicochetRange > 0)
            lines.Add($"RICOCHET  {level.RicochetDamageMultiplier:P0} DAMAGE    REACH  {level.RicochetRange:0}");
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
            lines.Add($"MAX CHAIN DPS  {maximumDps:0.#}");
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
            "single_projectile" => DirectProjectileSummary(level),
            "pellet_burst" => $"{level.PelletCount} projectiles; spread {level.PelletSpreadDegrees:0.#}deg; pierce {level.ArmorPierce:0.#}",
            "slow_projectile" => $"Impact area {level.SplashRadius:0}; slow {level.SlowPercent:P0} for {level.SlowDuration:0.#}s",
            "burn_projectile" => level.SplashRadius > 0
                ? $"Burn {level.BurnDamagePerSecond:0.#}/s for {level.BurnDuration:0.#}s; area {level.SplashRadius:0}; armor -2"
                : $"Burn {level.BurnDamagePerSecond:0.#}/s for {level.BurnDuration:0.#}s; armor -2",
            "armor_projectile" => level.PriorityDamageMultiplier > 1f
                ? level.SplashTargetLimit > 1
                    ? $"Heavy x{level.PriorityDamageMultiplier:0.##}; {level.SplashTargetLimit} targets max; pierce {level.ArmorPierce:0}"
                    : $"Heavy targets x{level.PriorityDamageMultiplier:0.##}; pierce {level.ArmorPierce:0}; break {level.ArmorReduction:0}"
                : level.ArmorReduction > 0
                    ? $"Pierce {level.ArmorPierce:0}; break {level.ArmorReduction:0} for {level.ArmorReductionDuration:0.#}s"
                    : $"Armor pierce {level.ArmorPierce:0}",
            "chain" => $"{level.ChainCount} arcs at {level.ChainDamage:0.#}; reach {level.ChainRange:0}",
            "splash_projectile" => level.SplashTargetLimit > 0
                ? $"Predictive impact; area {level.SplashRadius:0}; {level.SplashTargetLimit} targets max"
                : $"Predictive impact; area {level.SplashRadius:0}",
            "beam" => $"Expose +{level.ExposePercent:P0} incoming damage for {level.ExposeDuration:0.#}s; pierce {level.ArmorPierce:0.#}",
            "aura" => $"Aura {level.AuraRange:0}; rate +{level.AuraAttackSpeedBonus:P0}; range +{level.AuraRangeBonus:P0}",
            _ => "Direct attack"
        };
    }

    private static string DirectProjectileSummary(TowerLevelDefinition level)
    {
        var details = new List<string> { $"Projectile speed {level.ProjectileSpeed:0}" };
        if (level.ArmorPierce > 0) details.Add($"pierce {level.ArmorPierce:0.#}");
        if (level.PriorityDamageMultiplier > 1f) details.Add($"heavy x{level.PriorityDamageMultiplier:0.##}");
        if (level.RicochetRange > 0) details.Add($"ricochet {level.RicochetDamageMultiplier:P0} within {level.RicochetRange:0}");
        if (level.SplashTargetLimit > 1) details.Add($"{level.SplashTargetLimit} targets max");
        return string.Join("; ", details);
    }

    public static string Strength(TowerDefinition definition) => definition.Behavior.ToLowerInvariant() switch
    {
        "single_projectile" when definition.Id == "watchtower" => "Strength: priority targets at long range",
        "single_projectile" => "Strength: efficient general coverage",
        "pellet_burst" => "Strength: separated weak targets",
        "slow_projectile" => "Strength: slows and chips clustered enemies",
        "burn_projectile" => "Strength: persistent damage and armor setup",
        "armor_projectile" => "Strength: armored enemies",
        "chain" => "Strength: dense groups and connected targets",
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
        MapPowerBuff powerBuff,
        int maximumChanges = 6)
    {
        if (levelIndex >= definition.Levels.Count - 1) return "Maximum level reached";
        var current = definition.Levels[levelIndex];
        var next = definition.Levels[levelIndex + 1];
        return CoreChanges(definition, current, next, supportBuff, powerBuff, maximumChanges);
    }

    public static string DoctrineSummary(TowerDefinition definition, TowerDoctrineDefinition doctrine,
        TowerBuff supportBuff = default, MapPowerBuff powerBuff = default)
    {
        var current = definition.Levels[0];
        var next = definition.Levels[Math.Min(1, definition.Levels.Count - 1)].WithDoctrine(doctrine);
        return $"{doctrine.Summary}: {CoreChanges(definition, current, next, supportBuff, powerBuff, 6)}";
    }

    public static string SpecializationSummary(TowerDefinition definition, TowerLevelDefinition current, TowerSpecializationDefinition specialization,
        TowerDoctrineDefinition? doctrine = null, TowerBuff supportBuff = default, MapPowerBuff powerBuff = default)
    {
        var next = specialization.Level.WithDoctrine(doctrine);
        return $"{specialization.Summary}: {CoreChanges(definition, current, next, supportBuff, powerBuff, 6)}";
    }

    private static string CoreChanges(TowerDefinition definition, TowerLevelDefinition current, TowerLevelDefinition next,
        TowerBuff supportBuff, MapPowerBuff powerBuff, int maximumChanges)
    {
        const float epsilon = 0.001f;
        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        var damageMultiplier = 1f + powerBuff.DamageBonus;
        var attackSpeedMultiplier = 1f + supportBuff.AttackSpeedBonus + powerBuff.AttackSpeedBonus;
        var rangeMultiplier = 1f + supportBuff.RangeBonus + powerBuff.RangeBonus;

        AddFloat("DAMAGE", "DAMAGE", current.Damage * damageMultiplier, next.Damage * damageMultiplier, "0.#");
        AddFloat("RATE", "RATE", current.AttacksPerSecond * attackSpeedMultiplier,
            next.AttacksPerSecond * attackSpeedMultiplier, "0.##");
        AddFloat("RANGE", "RANGE", current.Range * rangeMultiplier, next.Range * rangeMultiplier, "0");
        AddFloat("SPEED", "SPEED", current.ProjectileSpeed, next.ProjectileSpeed, "0");
        if (next.PelletCount != current.PelletCount)
            changes["SHOTS"] = $"SHOTS {current.PelletCount}>{next.PelletCount}";
        AddFloat("SPREAD", "SPREAD", current.PelletSpreadDegrees, next.PelletSpreadDegrees, "0.#", "deg");
        var impactChanges = new List<string>();
        if (Changed(current.SplashRadius, next.SplashRadius))
            impactChanges.Add($"SPLASH {current.SplashRadius:0.#}>{next.SplashRadius:0.#}");
        if (next.SplashTargetLimit != current.SplashTargetLimit)
            impactChanges.Add($"CAP {current.SplashTargetLimit}>{next.SplashTargetLimit}");
        if (current.HomingSplash != next.HomingSplash)
            impactChanges.Add(next.HomingSplash ? "HOMING" : "FIXED IMPACT");
        if (impactChanges.Count > 0)
            changes["IMPACT"] = string.Join(" / ", impactChanges);
        if (Changed(current.RicochetDamageMultiplier, next.RicochetDamageMultiplier) ||
            Changed(current.RicochetRange, next.RicochetRange))
            changes["RICOCHET"] = $"RICOCHET {current.RicochetDamageMultiplier:P0}>{next.RicochetDamageMultiplier:P0} / REACH {current.RicochetRange:0}>{next.RicochetRange:0}";
        if (Changed(current.SlowPercent, next.SlowPercent) || Changed(current.SlowDuration, next.SlowDuration))
            changes["SLOW"] = $"SLOW {current.SlowPercent:P0} {current.SlowDuration:0.#}s>{next.SlowPercent:P0} {next.SlowDuration:0.#}s";
        if (Changed(current.BurnDamagePerSecond, next.BurnDamagePerSecond) || Changed(current.BurnDuration, next.BurnDuration))
            changes["BURN"] = $"BURN {current.BurnDamagePerSecond * damageMultiplier:0.#}/s {current.BurnDuration:0.#}s>{next.BurnDamagePerSecond * damageMultiplier:0.#}/s {next.BurnDuration:0.#}s";
        AddFloat("PIERCE", "PIERCE", current.ArmorPierce + powerBuff.ArmorPierceBonus,
            next.ArmorPierce + powerBuff.ArmorPierceBonus, "0.#");
        if (Changed(current.ArmorReduction, next.ArmorReduction) ||
            Changed(current.ArmorReductionDuration, next.ArmorReductionDuration))
            changes["BREAK"] = $"BREAK {current.ArmorReduction:0.#} {current.ArmorReductionDuration:0.#}s>{next.ArmorReduction:0.#} {next.ArmorReductionDuration:0.#}s";
        if (Changed(current.PriorityDamageMultiplier, next.PriorityDamageMultiplier))
            changes["HEAVY"] = $"HEAVY x{current.PriorityDamageMultiplier:0.##}>x{next.PriorityDamageMultiplier:0.##}";
        if ((current.ChainCount > 0 || next.ChainCount > 0) &&
            (current.ChainCount != next.ChainCount || Changed(current.ChainDamage, next.ChainDamage)))
            changes["ARCS"] = $"ARCS {current.ChainCount}x{current.ChainDamage:0.#}>{next.ChainCount}x{next.ChainDamage:0.#}";
        AddFloat("ARC_REACH", "ARC RANGE", current.ChainRange, next.ChainRange, "0");
        if (Changed(current.ExposePercent, next.ExposePercent) || Changed(current.ExposeDuration, next.ExposeDuration))
            changes["EXPOSE"] = $"EXPOSE +{current.ExposePercent:P0} {current.ExposeDuration:0.#}s>+{next.ExposePercent:P0} {next.ExposeDuration:0.#}s";
        AddFloat("STUN", "STUN", current.StunDuration, next.StunDuration, "0.##", "s");
        AddFloat("AURA_FIELD", "FIELD", current.AuraRange, next.AuraRange, "0");
        AddPercent("AURA_RATE", "AURA RATE", current.AuraAttackSpeedBonus, next.AuraAttackSpeedBonus);
        AddPercent("AURA_RANGE", "AURA RANGE", current.AuraRangeBonus, next.AuraRangeBonus);
        if (current.IgnoreShield != next.IgnoreShield)
            changes["SHIELDS"] = next.IgnoreShield ? "SHIELDS BYPASSED" : "SHIELDS BLOCK";

        var priority = definition.Behavior.ToLowerInvariant() switch
        {
            "aura" => new[] { "AURA_FIELD", "AURA_RATE", "AURA_RANGE" },
            "single_projectile" => new[] { "RICOCHET", "HEAVY", "PIERCE", "IMPACT", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "pellet_burst" => new[] { "SHOTS", "SPREAD", "PIERCE", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "slow_projectile" => new[] { "SLOW", "IMPACT", "STUN", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "burn_projectile" => new[] { "BURN", "IMPACT", "PIERCE", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "armor_projectile" => new[] { "PIERCE", "BREAK", "HEAVY", "IMPACT", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "chain" => new[] { "ARCS", "ARC_REACH", "STUN", "DAMAGE", "RATE", "RANGE" },
            "splash_projectile" => new[] { "IMPACT", "SLOW", "STUN", "DAMAGE", "RATE", "RANGE", "SPEED" },
            "beam" => new[] { "EXPOSE", "PIERCE", "SHIELDS", "ARCS", "ARC_REACH", "DAMAGE", "RATE", "RANGE" },
            _ => Array.Empty<string>()
        };
        var fallback = new[]
        {
            "DAMAGE", "RATE", "RANGE", "SPEED", "SHOTS", "SPREAD", "IMPACT", "SLOW", "BURN",
            "PIERCE", "BREAK", "HEAVY", "RICOCHET", "ARCS", "ARC_REACH", "EXPOSE", "STUN", "SHIELDS",
            "AURA_FIELD", "AURA_RATE", "AURA_RANGE"
        };
        return string.Join("  ", priority.Concat(fallback).Distinct().Where(changes.ContainsKey)
            .Take(Math.Max(1, maximumChanges)).Select(key => changes[key]));

        bool Changed(float before, float after) => MathF.Abs(after - before) > epsilon;

        void AddFloat(string key, string label, float before, float after, string format, string suffix = "")
        {
            if (Changed(before, after)) changes[key] = $"{label} {before.ToString(format)}>{after.ToString(format)}{suffix}";
        }

        void AddPercent(string key, string label, float before, float after)
        {
            if (Changed(before, after)) changes[key] = $"{label} +{before:P0}>+{after:P0}";
        }
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

    public static string ActiveBoostSources(TowerBuff supportBuff, IReadOnlyList<PowerNodeData> powerNodes,
        bool compact = false)
    {
        var hasBeacon = supportBuff.IsActive;
        if (!hasBeacon && powerNodes.Count == 0) return "";

        var nodeSource = powerNodes.Count switch
        {
            0 => "",
            1 when compact => "NODE",
            1 => $"ON {powerNodes[0].DisplayName.ToUpperInvariant()}",
            _ => $"{powerNodes.Count} NODES"
        };
        if (!hasBeacon) return nodeSource;
        if (powerNodes.Count == 0) return "BEACON";

        return compact
            ? $"BEACON + {nodeSource}"
            : $"BEACON + {nodeSource.Replace("ON ", "", StringComparison.Ordinal)}";
    }

    public static string PowerNodeStatChange(TowerDefinition definition, TowerLevelDefinition level, MapPowerBuff power)
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
            return "NO COMPATIBLE COMBAT STAT CHANGE";

        var changes = new List<string>();
        if (power.DamageBonus > 0 && level.Damage > 0)
            changes.Add($"DAMAGE {level.Damage:0.#}>{level.Damage * (1 + power.DamageBonus):0.#}");
        if (power.DamageBonus > 0 && level.BurnDamagePerSecond > 0)
            changes.Add($"BURN {level.BurnDamagePerSecond:0.#}>{level.BurnDamagePerSecond * (1 + power.DamageBonus):0.#}/s");
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

    public static string ActiveAuraSummary(TowerInstance tower)
        => $"AURA {tower.EffectiveAuraRange:0}   RATE +{tower.EffectiveAuraAttackSpeedBonus:P0}   RANGE +{tower.EffectiveAuraTowerRangeBonus:P0}";
}
