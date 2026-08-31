using MinimalBastion.Data;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Simulation;

internal static class StructuralTowerEvaluator
{
    public static double CurrentValue(GameSession session, TowerInstance tower, ThreatProfile threat)
    {
        if (tower.IsSupport)
        {
            var supportValue = SupportMarginalValue(session, tower, threat);
            return supportValue + ProtocolOptionValue(session, tower, threat);
        }

        var currentValue = CombatValue(session, tower, threat, session.GetSupportBuff(tower), includeCurrentProtocol: true);
        return currentValue + ProtocolOptionValue(session, tower, threat);
    }

    public static StructuralMapValue MapValue(GameSession session, ThreatProfile threat)
    {
        var combatValue = session.Towers.Where(tower => !tower.IsSupport)
            .Sum(tower => CombatValue(session, tower, threat, session.GetSupportBuff(tower), includeCurrentProtocol: true));
        var apexProtocolValue = session.Towers.Where(tower => tower.IsApex)
            .Sum(tower => ProtocolOptionValue(session, tower, threat));
        var manualProtocolValue = session.ProtocolsEnabled
            ? session.Towers.Where(tower => !tower.IsApex)
                .Select(tower => ProtocolOptionValue(session, tower, threat))
                .DefaultIfEmpty(0d)
                .Max()
            : 0d;
        var wholeMapCoverage = WholeMapCoverage(session);
        return new StructuralMapValue(
            combatValue,
            wholeMapCoverage,
            manualProtocolValue,
            apexProtocolValue,
            combatValue + manualProtocolValue + apexProtocolValue);
    }

    public static TacticalStructuralValue TacticalValue(GameSession session, ThreatProfile threat)
    {
        if (!session.TacticalSystemsEnabled)
            return new TacticalStructuralValue(0, 0, -1, 0, 0, 0);

        var definition = session.Content.Tactics.EmergencyDefense;
        var expectedTargets = EstimateEmergencyTargets(session, definition, threat);
        var triggerValue = EmergencyTriggerValue(definition, threat, expectedTargets);
        var inventoryValue = session.EmergencyInventory * definition.Charges * triggerValue * 0.85d;
        var plateCharges = session.EmergencyDefenses.Sum(plate => plate.ChargesRemaining);
        var activePlateValue = session.EmergencyDefenses.Sum(plate =>
        {
            var readinessSeconds = plate.ArmRemaining + plate.CooldownRemaining;
            var readiness = 1d / (1d + readinessSeconds / Math.Max(0.1d,
                definition.ArmTime + definition.TriggerCooldown));
            var distance = session.Map.Path.DistanceToPath(plate.Position);
            var roadFit = Math.Clamp(1d - distance / Math.Max(1d, definition.TriggerRadius), 0.25d, 1d);
            var progress = PathProgressNear(session, plate.Position);
            var placementFactor = roadFit * (0.8d + progress * 0.4d);
            return plate.ChargesRemaining * triggerValue * readiness * placementFactor;
        });

        var generatorValue = 0d;
        var generatorLevel = -1;
        var generatorProgress = 0d;
        if (session.Generator is { } generator)
        {
            generatorLevel = generator.LevelIndex;
            var level = generator.Level;
            var productionSeconds = Math.Max(0.1d, level.ProductionSeconds);
            var productionProgress = Math.Clamp(1d - generator.ProductionRemaining / productionSeconds, 0d, 1d);
            generatorProgress = productionProgress;
            var forwardSeconds = EstimateNextWaveSeconds(session);
            var forwardCharges = productionProgress + forwardSeconds / productionSeconds;
            var capacityFactor = 0.65d + level.Capacity * 0.12d;
            var forgedChargeValue = definition.Charges * triggerValue * 0.85d;
            var boostedCharges = session.EmergencyInventory * definition.Charges + plateCharges;
            var defenseBonusValue = boostedCharges * definition.Damage * level.DefenseDamageBonus * expectedTargets;
            generatorValue = forwardCharges * forgedChargeValue * capacityFactor + defenseBonusValue;
        }

        return new TacticalStructuralValue(
            session.EmergencyInventory,
            plateCharges,
            generatorLevel,
            generatorProgress,
            generatorValue,
            inventoryValue + activePlateValue + generatorValue);
    }

    public static float LevelValue(
        TowerDefinition definition,
        TowerLevelDefinition level,
        ThreatProfile threat)
    {
        if (definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase))
            return 16f + level.AuraAttackSpeedBonus * 90f + level.AuraRangeBonus * 55f + level.AuraRange * 0.03f;

        var directDps = level.Damage * level.AttacksPerSecond;
        var value = directDps;
        value += level.BurnDamagePerSecond * MathF.Min(1f, level.BurnDuration * level.AttacksPerSecond);
        value *= 1f + MathF.Max(0, level.PelletCount - 1) * (0.25f + threat.Swarm * 0.65f);
        if (level.RicochetRange > 0)
            value += directDps * level.RicochetDamageMultiplier * (0.25f + threat.Swarm * 0.75f);
        if (level.ChainCount > 0)
            value += level.ChainDamage * level.ChainCount * level.AttacksPerSecond * (0.35f + threat.Swarm * 0.65f);
        if (level.SplashRadius > 0) value *= 1.12f + threat.Swarm * MathF.Min(2.4f, level.SplashRadius / 24f);
        value += level.ArmorPierce * level.AttacksPerSecond * threat.Armored * 1.8f;
        value += level.ArmorReduction * threat.Armored * 2.2f;
        value += level.SlowPercent * level.SlowDuration * (8f + threat.Fast * 20f);
        value += level.ExposePercent * (12f + threat.Durable * 30f);
        value += level.StunDuration * (4f + threat.Fast * 12f);
        var rankedThreat = threat.HasBoss ? 1f : threat.HasElite ? 0.35f : 0f;
        value += rankedThreat * (level.Damage * 0.35f + level.ArmorPierce * 2.5f + level.ExposePercent * 45f);
        if (threat.HasBoss && (level.PelletCount > 1 || level.SplashRadius > 0 || level.RicochetRange > 0)) value *= 0.94f;
        value *= 1f + MathHelper.Clamp((level.Range - 115f) / 650f, 0, 0.32f);
        if (threat.Armored > 0.3f && level.Damage < 12 && level.ArmorPierce <= 0) value *= 0.72f;
        if (threat.Shielded > 0.2f && level.IgnoreShield) value *= 1.25f;
        return MathF.Max(0.1f, value);
    }

    private static double CombatValue(
        GameSession session,
        TowerInstance tower,
        ThreatProfile threat,
        TowerBuff support,
        bool includeCurrentProtocol)
    {
        var level = tower.Level;
        var power = session.Map.GetPowerBuff(tower.Position);
        var protocolActive = includeCurrentProtocol && tower.IsOverdriven;
        var protocol = tower.Protocol;
        var protocolDamage = protocolActive ? protocol.DamageBonus : 0f;
        var protocolRate = protocolActive ? protocol.AttackSpeedBonus : 0f;
        var protocolRange = protocolActive ? protocol.RangeBonus : 0f;
        var protocolArmorPierce = protocolActive ? protocol.ArmorPierceBonus : 0f;
        var baseThroughput = Math.Max(0.01d, level.Damage * level.AttacksPerSecond);
        var effectiveDamage = level.Damage * (1f + power.DamageBonus + protocolDamage) *
                              session.GetSignalDamageMultiplier(tower);
        var effectiveRate = level.AttacksPerSecond *
                            (1f + support.AttackSpeedBonus + power.AttackSpeedBonus + protocolRate) *
                            session.GetSignalRateMultiplier(tower);
        var effectiveThroughput = Math.Max(0.01d, effectiveDamage * effectiveRate);
        var throughputFactor = Math.Clamp(effectiveThroughput / baseThroughput, 0.25d, 4d);
        var range = level.Range * (1f + support.RangeBonus + power.RangeBonus + protocolRange);
        var coverageFactor = 0.5d + PathCoverage(session, tower.Position, range) * 1.5d;
        var effectiveArmorPierce = level.ArmorPierce + power.ArmorPierceBonus + protocolArmorPierce;
        var armorPierceGain = Math.Max(0, effectiveArmorPierce - level.ArmorPierce) * effectiveRate *
                              threat.Armored * 1.8d;
        return LevelValue(tower.Definition, level, threat) * throughputFactor * coverageFactor + armorPierceGain;
    }

    private static double ProtocolOptionValue(
        GameSession session,
        TowerInstance tower,
        ThreatProfile threat)
    {
        if (!session.ProtocolsEnabled) return 0;
        var protocol = tower.Protocol;
        var cycleSeconds = Math.Max(0.1d, Math.Max(protocol.DurationSeconds, protocol.CooldownSeconds));
        var uptime = Math.Clamp(protocol.DurationSeconds / cycleSeconds, 0d, 1d);
        var cooldownRemaining = tower.IsApex
            ? tower.ApexProtocolCooldownRemaining
            : session.OverdriveCooldownRemaining;
        var readiness = 1d / (1d + cooldownRemaining / cycleSeconds);
        var availability = tower.IsApex ? 1d : 0.55d;

        var sustainedValue = 0d;
        var armorValue = 0d;
        var auraValue = 0d;
        if (tower.IsSupport)
        {
            foreach (var recipient in session.Towers.Where(candidate => !candidate.IsSupport))
            {
                var baseline = StrongestSupportBuff(session, recipient, suppressProtocolTowerId: tower.Id);
                var activated = StrongestSupportBuff(session, recipient, forceProtocolTowerId: tower.Id);
                var baselineValue = CombatValue(session, recipient, threat, baseline, includeCurrentProtocol: true);
                var activatedValue = CombatValue(session, recipient, threat, activated, includeCurrentProtocol: true);
                auraValue += Math.Max(0d, activatedValue - baselineValue) * uptime;
            }
        }
        else
        {
            var steadyValue = CombatValue(session, tower, threat, session.GetSupportBuff(tower), includeCurrentProtocol: false);
            var throughputGain = Math.Max(0d,
                (1d + protocol.AttackSpeedBonus) * (1d + protocol.DamageBonus) - 1d);
            var rangeGain = Math.Max(0d, protocol.RangeBonus) * 0.35d;
            sustainedValue = steadyValue * uptime * (throughputGain + rangeGain);
            var steadyRate = SteadyAttacksPerSecond(session, tower, session.GetSupportBuff(tower));
            armorValue = Math.Max(0d, protocol.ArmorPierceBonus) * steadyRate * threat.Armored * 1.8d * uptime;
        }

        var expectedTargets = Math.Clamp(
            protocol.AutoTriggerTargetCount > 0 ? protocol.AutoTriggerTargetCount : protocol.AutoTriggerCount,
            1,
            6);
        var burstValue = Math.Max(0d, protocol.BurstDamage) * expectedTargets / cycleSeconds;
        if (protocol.FireOnActivation)
            burstValue += SteadyDamage(session, tower, tower.Level.Damage) *
                           Math.Max(1, tower.Level.PelletCount) / cycleSeconds;
        if (!string.IsNullOrWhiteSpace(protocol.BurstStatus))
            burstValue += Math.Max(0d, protocol.BurstStatusMagnitude) *
                          Math.Max(0d, protocol.BurstStatusDuration) * expectedTargets * 8d / cycleSeconds;

        return (sustainedValue + armorValue + auraValue + burstValue) * readiness * availability;
    }

    private static double SupportMarginalValue(GameSession session, TowerInstance support, ThreatProfile threat)
    {
        var value = 0d;
        foreach (var recipient in session.Towers.Where(candidate => !candidate.IsSupport))
        {
            var current = StrongestSupportBuff(session, recipient);
            var withoutSupport = StrongestSupportBuff(session, recipient, excludedSupportId: support.Id);
            var currentValue = CombatValue(session, recipient, threat, current, includeCurrentProtocol: true);
            var withoutValue = CombatValue(session, recipient, threat, withoutSupport, includeCurrentProtocol: true);
            value += Math.Max(0d, currentValue - withoutValue);
        }
        return value;
    }

    private static TowerBuff StrongestSupportBuff(
        GameSession session,
        TowerInstance recipient,
        int excludedSupportId = 0,
        int suppressProtocolTowerId = 0,
        int forceProtocolTowerId = 0)
    {
        if (recipient.IsSandboxDisabled || recipient.IsDisrupted) return new TowerBuff(0, 0);
        var attackSpeed = 0f;
        var range = 0f;
        var attackSource = 0;
        var rangeSource = 0;
        foreach (var support in session.Towers.Where(candidate => candidate.IsSupport &&
                     candidate.Id != excludedSupportId && !candidate.IsSandboxDisabled && !candidate.IsDisrupted))
        {
            var protocolActive = support.Id == forceProtocolTowerId ||
                                 support.IsOverdriven && support.Id != suppressProtocolTowerId;
            var auraRange = support.Level.AuraRange *
                            (1f + (protocolActive ? support.Protocol.AuraRangeBonus : 0f));
            if (Vector2.DistanceSquared(support.Position, recipient.Position) > auraRange * auraRange) continue;
            var candidateAttack = support.Level.AuraAttackSpeedBonus +
                                  (protocolActive ? support.Protocol.AuraAttackSpeedBonus : 0f);
            var candidateRange = support.Level.AuraRangeBonus +
                                 (protocolActive ? support.Protocol.AuraRangeBonus : 0f);
            if (candidateAttack > attackSpeed || candidateAttack == attackSpeed &&
                (attackSource == 0 || support.Id < attackSource))
            {
                attackSpeed = candidateAttack;
                attackSource = support.Id;
            }
            if (candidateRange > range || candidateRange == range &&
                (rangeSource == 0 || support.Id < rangeSource))
            {
                range = candidateRange;
                rangeSource = support.Id;
            }
        }
        return new TowerBuff(attackSpeed, range, attackSource, rangeSource);
    }

    private static double PathCoverage(GameSession session, Vector2 position, float range)
    {
        var rangeSquared = range * range;
        var samples = PathSamples(session).ToArray();
        if (samples.Length == 0) return 0;
        return samples.Count(distance => Vector2.DistanceSquared(position,
            session.Map.Path.GetPosition(distance)) <= rangeSquared) / (double)samples.Length;
    }

    private static double WholeMapCoverage(GameSession session)
    {
        var towers = session.Towers.Where(tower => !tower.IsSupport).ToArray();
        if (towers.Length == 0) return 0;
        var samples = PathSamples(session).ToArray();
        if (samples.Length == 0) return 0;
        var covered = 0;
        foreach (var distance in samples)
        {
            var point = session.Map.Path.GetPosition(distance);
            if (towers.Any(tower => Vector2.DistanceSquared(tower.Position, point) <=
                                    session.GetEffectiveRange(tower) * session.GetEffectiveRange(tower)))
                covered++;
        }
        return covered / (double)samples.Length;
    }

    private static IEnumerable<float> PathSamples(GameSession session)
    {
        const float sampleSpacing = 32f;
        var total = session.Map.Path.TotalLength;
        for (var distance = 0f; distance < total; distance += sampleSpacing)
            yield return distance;
        yield return total;
    }

    private static float SteadyDamage(GameSession session, TowerInstance tower, float baseDamage) =>
        baseDamage * (1f + session.Map.GetPowerBuff(tower.Position).DamageBonus) *
        session.GetSignalDamageMultiplier(tower);

    private static float SteadyAttacksPerSecond(GameSession session, TowerInstance tower, TowerBuff support) =>
        tower.Level.AttacksPerSecond *
        (1f + support.AttackSpeedBonus + session.Map.GetPowerBuff(tower.Position).AttackSpeedBonus) *
        session.GetSignalRateMultiplier(tower);

    private static double EmergencyTriggerValue(
        EmergencyDefenseDefinition definition,
        ThreatProfile threat,
        double expectedTargets)
    {
        var areaValue = definition.Damage + definition.ArmorPierce * threat.Armored * 1.8d +
                        definition.StunDuration * (4d + threat.Fast * 12d) +
                        definition.SlowPercent * definition.SlowDuration * (8d + threat.Fast * 20d);
        return areaValue * expectedTargets + definition.KnockbackDistance * 0.12d;
    }

    private static double EstimateEmergencyTargets(
        GameSession session,
        EmergencyDefenseDefinition definition,
        ThreatProfile threat)
    {
        var wave = session.Waves.NextWave;
        if (wave is null || wave.Groups.Count == 0) return 1d;
        var weightedTargets = 0d;
        var totalWeight = 0d;
        foreach (var group in wave.Groups)
        {
            if (group.Count <= 0 || !session.Content.Enemies.TryGetValue(group.EnemyId, out var enemy)) continue;
            var speed = Math.Max(0.01d,
                enemy.Speed * wave.SpeedMultiplier * session.Difficulty.EnemySpeedMultiplier);
            var spacing = speed * Math.Max(0.05d, group.SpawnInterval);
            var targets = Math.Clamp(1d + definition.BlastRadius * 2d / spacing, 1d, group.Count);
            weightedTargets += targets * group.Count;
            totalWeight += group.Count;
        }
        if (totalWeight <= 0) return 1d;
        var averageTargets = weightedTargets / totalWeight;
        var clusteredTargets = 1d + (averageTargets - 1d) * (1d + threat.Swarm * 0.35d);
        return Math.Clamp(clusteredTargets, 1d, Math.Min(8d, totalWeight));
    }

    private static double EstimateNextWaveSeconds(GameSession session)
    {
        var wave = session.Waves.NextWave;
        if (wave is null) return 0;
        var spawnSeconds = wave.Groups.Sum(group =>
            Math.Max(0, group.DelayBefore) + Math.Max(0, group.Count - 1) * Math.Max(0, group.SpawnInterval));
        var traversalSeconds = wave.Groups.Select(group =>
        {
            if (!session.Content.Enemies.TryGetValue(group.EnemyId, out var enemy)) return 0d;
            var speed = enemy.Speed * wave.SpeedMultiplier * session.Difficulty.EnemySpeedMultiplier;
            return speed <= 0 ? 0 : session.Map.Path.TotalLength / speed;
        }).DefaultIfEmpty(0).Max();
        return Math.Max(0, spawnSeconds + traversalSeconds);
    }

    private static double PathProgressNear(GameSession session, Vector2 position)
    {
        var bestDistance = float.MaxValue;
        var progress = 0f;
        for (var distance = 0f; distance <= session.Map.Path.TotalLength; distance += 18f)
        {
            var pathPoint = session.Map.Path.GetPosition(distance);
            var candidateDistance = Vector2.DistanceSquared(position, pathPoint);
            if (candidateDistance >= bestDistance) continue;
            bestDistance = candidateDistance;
            progress = session.Map.Path.GetProgress(distance);
        }
        return progress;
    }
}

internal readonly record struct StructuralMapValue(
    double CurrentCombatValue,
    double WholeMapCoverage,
    double ManualProtocolValue,
    double ApexProtocolValue,
    double ForwardValue);

internal readonly record struct TacticalStructuralValue(
    int EmergencyInventory,
    int PulsePlateCharges,
    int GeneratorLevel,
    double GeneratorProgress,
    double GeneratorForwardValue,
    double ForwardValue);
