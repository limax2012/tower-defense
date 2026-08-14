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
            var triggeringEnemy = session.Enemies
                .Where(x => !x.IsDead && !x.HasEscaped && plate.CanTrigger(x.Id))
                .OrderByDescending(x => x.DistanceAlongPath)
                .FirstOrDefault(x => Vector2.DistanceSquared(x.Position, plate.Position) <=
                                     plate.Definition.TriggerRadius * plate.Definition.TriggerRadius);
            if (triggeringEnemy is null) continue;

            var hitCount = 0;
            var damage = plate.Definition.Damage * (1f + (forge?.Level.DefenseDamageBonus ?? 0));
            var affectedEnemies = session.Enemies.Where(x => !x.IsDead && !x.HasEscaped &&
                Vector2.DistanceSquared(x.Position, plate.Position) <= plate.Definition.BlastRadius * plate.Definition.BlastRadius).ToArray();
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
                hitCount++;
            }

            plate.Trigger(triggeringEnemy.Id, affectedEnemies.Select(x => x.Id));
            session.Effects.AddFlash(plate.Position, plate.Definition.Visual.PrimaryColor, 0.28f, plate.Definition.BlastRadius);
            session.OnEmergencyDefenseTriggered(plate, hitCount);
        }

        foreach (var expired in session.EmergencyDefenses.Where(x => x.IsExpired).ToArray())
        {
            session.EmergencyDefenses.Remove(expired);
            session.OnEmergencyDefenseExpired(expired);
        }
    }
}
