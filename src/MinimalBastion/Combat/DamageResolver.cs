using MinimalBastion.Effects;
using MinimalBastion.Enemies;

namespace MinimalBastion.Combat;

public sealed class DamageResolver
{
    private readonly MinimalBastion.GameSession _session;

    public DamageResolver(MinimalBastion.GameSession session) => _session = session;

    public event Action<DamageReport>? DamageApplied;

    public void Apply(EnemyInstance enemy, DamagePayload payload)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        var priorityMultiplier = enemy.BaseArmor >= 4 || enemy.IsElite || enemy.IsBoss
            ? MathF.Max(1, payload.PriorityDamageMultiplier)
            : 1f;
        var rawDamage = MathF.Max(0, payload.Damage * priorityMultiplier);
        var expose = enemy.StatusEffects.Active.Where(status => status.Type == StatusType.Exposed)
            .OrderByDescending(status => status.Magnitude).ThenBy(status => status.SourceId).FirstOrDefault();
        var armorBreak = enemy.StatusEffects.Active.Where(status => status.Type == StatusType.ArmorBreak)
            .OrderByDescending(status => status.Magnitude).ThenBy(status => status.SourceId).FirstOrDefault();
        var effectiveArmorBefore = enemy.EffectiveArmor;
        var armorWithoutBreakBefore = MathF.Max(0, enemy.BaseArmor - (enemy.StatusEffects.IsBurning ? 2f : 0f));
        var incomingDamage = rawDamage * enemy.StatusEffects.DamageMultiplier;
        if (incomingDamage <= 0) return;
        var damage = incomingDamage;
        var shieldDamage = 0f;
        var shieldBefore = enemy.Shield;
        var healthBefore = enemy.Health;
        if (!payload.IgnoreShield)
        {
            damage = enemy.AbsorbShield(damage);
            shieldDamage = shieldBefore - enemy.Shield;
            if (damage <= 0)
            {
                if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
                Publish(shieldDamage, 0, 0, 0, false);
                return;
            }
        }
        var armorAbsorbed = MathF.Min(damage, MathF.Max(0, enemy.EffectiveArmor - payload.ArmorPierce));
        damage -= armorAbsorbed;
        if (!payload.IsDamageOverTime) damage = MathF.Max(1, damage);
        if (damage <= 0)
        {
            if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
            Publish(shieldDamage, armorAbsorbed, 0, 0, false);
            return;
        }
        var overkill = MathF.Max(0, damage - healthBefore);
        var healthDamage = MathF.Min(healthBefore, damage);
        enemy.ApplyHealthDamage(damage);
        if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
        if (enemy.ConsumeBossPhasePulse()) _session.OnBossPhaseChanged(enemy);
        if (enemy.IsDead) _session.OnEnemyKilled(enemy);
        Publish(shieldDamage, armorAbsorbed, healthDamage, overkill, enemy.IsDead);

        void Publish(float appliedShieldDamage, float absorbedByArmor, float appliedHealthDamage, float appliedOverkill, bool killed)
        {
            var actualApplied = appliedShieldDamage + appliedHealthDamage;
            var withoutExpose = EstimateApplied(rawDamage, shieldBefore, healthBefore, effectiveArmorBefore, payload);
            var withoutExposeOrBreak = EstimateApplied(rawDamage, shieldBefore, healthBefore, armorWithoutBreakBefore, payload);
            var exposeEquivalent = expose is null ? 0 : MathF.Max(0, actualApplied - withoutExpose);
            var armorBreakEquivalent = armorBreak is null ? 0 : MathF.Max(0, withoutExpose - withoutExposeOrBreak);
            DamageApplied?.Invoke(new DamageReport(
                payload.SourceTowerId,
                incomingDamage,
                appliedShieldDamage,
                absorbedByArmor,
                appliedHealthDamage,
                appliedOverkill,
                killed,
                expose?.SourceId ?? 0,
                exposeEquivalent,
                armorBreak?.SourceId ?? 0,
                armorBreakEquivalent));
        }
    }

    private static float EstimateApplied(float rawDamage, float shield, float health, float armor, DamagePayload payload)
    {
        var damage = MathF.Max(0, rawDamage);
        var shieldDamage = 0f;
        if (!payload.IgnoreShield)
        {
            shieldDamage = MathF.Min(shield, damage);
            damage -= shieldDamage;
            if (damage <= 0) return shieldDamage;
        }
        damage -= MathF.Min(damage, MathF.Max(0, armor - payload.ArmorPierce));
        if (!payload.IsDamageOverTime) damage = MathF.Max(1, damage);
        return shieldDamage + MathF.Min(health, MathF.Max(0, damage));
    }
}
