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
        var incomingDamage = MathF.Max(0, payload.Damage) * enemy.StatusEffects.DamageMultiplier;
        if (incomingDamage <= 0) return;
        var damage = incomingDamage;
        var shieldDamage = 0f;
        if (!payload.IgnoreShield)
        {
            var shieldBefore = enemy.Shield;
            damage = enemy.AbsorbShield(damage);
            shieldDamage = shieldBefore - enemy.Shield;
            if (damage <= 0)
            {
                if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
                DamageApplied?.Invoke(new DamageReport(payload.SourceTowerId, incomingDamage, shieldDamage, 0, 0, 0, false));
                return;
            }
        }
        var armorAbsorbed = MathF.Min(damage, MathF.Max(0, enemy.EffectiveArmor - payload.ArmorPierce));
        damage -= armorAbsorbed;
        if (!payload.IsDamageOverTime) damage = MathF.Max(1, damage);
        if (damage <= 0)
        {
            if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
            DamageApplied?.Invoke(new DamageReport(payload.SourceTowerId, incomingDamage, shieldDamage, armorAbsorbed, 0, 0, false));
            return;
        }
        var healthBefore = enemy.Health;
        var overkill = MathF.Max(0, damage - healthBefore);
        var healthDamage = MathF.Min(healthBefore, damage);
        enemy.ApplyHealthDamage(damage);
        if (payload.Status is not null) enemy.ApplyStatus(payload.Status);
        if (enemy.ConsumeBossPhasePulse()) _session.OnBossPhaseChanged(enemy);
        if (enemy.IsDead) _session.OnEnemyKilled(enemy);
        DamageApplied?.Invoke(new DamageReport(payload.SourceTowerId, incomingDamage, shieldDamage, armorAbsorbed, healthDamage, overkill, enemy.IsDead));
    }
}
