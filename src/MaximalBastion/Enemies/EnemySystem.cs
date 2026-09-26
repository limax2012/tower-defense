namespace MaximalBastion.Enemies;

public sealed class EnemySystem
{
    public void Update(float deltaSeconds, MaximalBastion.GameSession session)
    {
        session.RefreshEnemySignalFormation();
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            enemy.TickRuntimeTimers(deltaSeconds);
            enemy.UpdateMovement(deltaSeconds, session.Map.Path);
            if (enemy.HasEscaped)
            {
                session.OnEnemyEscaped(enemy);
                continue;
            }
            session.TryActivateEnemySignal(enemy);

            foreach (var burnTick in enemy.StatusEffects.ConsumeBurnTicks(deltaSeconds))
            {
                session.DamageResolver.Apply(enemy, new MaximalBastion.Combat.DamagePayload
                {
                    Damage = burnTick.Damage,
                    ArmorPierce = burnTick.ArmorPierce,
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
