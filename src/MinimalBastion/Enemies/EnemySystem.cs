namespace MinimalBastion.Enemies;

public sealed class EnemySystem
{
    public void Update(float deltaSeconds, MinimalBastion.GameSession session)
    {
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            enemy.TickDamagePause(deltaSeconds);
            enemy.UpdateMovement(deltaSeconds, session.Map.Path);
            if (enemy.HasEscaped)
            {
                session.OnEnemyEscaped(enemy);
                continue;
            }

            foreach (var burnTick in enemy.StatusEffects.ConsumeBurnTicks(deltaSeconds))
            {
                session.DamageResolver.Apply(enemy, new MinimalBastion.Combat.DamagePayload
                {
                    Damage = burnTick.Damage,
                    IsDamageOverTime = true,
                    IgnoreShield = false,
                    SourceTowerId = burnTick.SourceId
                });
            }
            enemy.StatusEffects.Update(deltaSeconds);
            enemy.Regenerate(deltaSeconds);
        }
    }
}
