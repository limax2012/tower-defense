using MinimalBastion.Combat;
using MinimalBastion.Effects;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Tactics;

public sealed class TacticalDefenseSystem
{
    public void Update(float deltaSeconds, GameSession session)
    {
        var forge = session.Generator;
        if (session.Waves.IsActive && forge is not null && forge.Update(deltaSeconds, session.EmergencyInventory >= forge.Level.Capacity))
            session.OnEmergencyChargeProduced();

        foreach (var plate in session.EmergencyDefenses)
        {
            plate.Tick(deltaSeconds);
            var enemiesOnPlate = session.Enemies
                .Where(x => !x.IsDead && !x.HasEscaped && Vector2.DistanceSquared(x.Position, plate.Position) <=
                    plate.Definition.TriggerRadius * plate.Definition.TriggerRadius)
                .ToArray();
            plate.RetainHandledEnemies(enemiesOnPlate.Select(x => x.Id));
            var triggeringEnemy = enemiesOnPlate
                .Where(x => plate.CanTrigger(x.Id))
                .OrderByDescending(x => x.DistanceAlongPath)
                .FirstOrDefault();
            if (triggeringEnemy is null) continue;

            var hitCount = 0;
            var damage = plate.Definition.Damage * (1f + (forge?.Level.DefenseDamageBonus ?? 0));
            var affectedEnemies = session.Enemies.Where(x => !x.IsDead && !x.HasEscaped &&
                Vector2.DistanceSquared(x.Position, plate.Position) <= plate.Definition.BlastRadius * plate.Definition.BlastRadius).ToArray();
            plate.Trigger(triggeringEnemy.Id);
            foreach (var enemy in affectedEnemies)
            {
                session.DamageResolver.Apply(enemy, new DamagePayload
                {
                    Damage = damage,
                    ArmorPierce = plate.Definition.ArmorPierce,
                    SourceTowerId = plate.DamageSourceId,
                    Status = new StatusApplication
                    {
                        Type = StatusType.Stun,
                        Duration = plate.Definition.StunDuration,
                        Magnitude = 1,
                        SourceId = plate.DamageSourceId
                    }
                });
                if (plate.Definition.SlowPercent > 0 && plate.Definition.SlowDuration > 0)
                {
                    enemy.ApplyStatus(new StatusApplication
                    {
                        Type = StatusType.Slow,
                        Duration = plate.Definition.SlowDuration,
                        Magnitude = plate.Definition.SlowPercent,
                        SourceId = plate.DamageSourceId
                    });
                }
                hitCount++;
            }

            var knockbackMultiplier = triggeringEnemy.IsBoss
                ? plate.Definition.BossKnockbackMultiplier
                : triggeringEnemy.IsElite
                    ? plate.Definition.EliteKnockbackMultiplier
                    : 1f;
            triggeringEnemy.TryApplyKnockback(
                plate.Definition.KnockbackDistance * knockbackMultiplier,
                plate.Definition.KnockbackGraceSeconds,
                session.Map.Path);
            session.Effects.AddSplash(plate.Position, plate.Definition.Visual.PrimaryColor, plate.Definition.BlastRadius);
            session.OnEmergencyDefenseTriggered(plate, hitCount);
        }

        foreach (var expired in session.EmergencyDefenses.Where(x => x.IsExpired).ToArray())
        {
            session.EmergencyDefenses.Remove(expired);
            session.OnEmergencyDefenseExpired(expired);
        }
    }
}
