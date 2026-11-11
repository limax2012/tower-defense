using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Combat;

public sealed class ProjectileSystem
{
    private readonly List<ProjectileInstance> _projectiles = new();
    public IReadOnlyList<ProjectileInstance> Projectiles => _projectiles;

    public void Add(ProjectileInstance projectile) => _projectiles.Add(projectile);

    public void Clear() => _projectiles.Clear();

    public List<ProjectileRuntimeState> CaptureCoOpState() =>
        _projectiles.Where(projectile => !projectile.IsExpired).Select(projectile => projectile.CaptureCoOpState()).ToList();

    public void RestoreCoOpState(IEnumerable<ProjectileRuntimeState> projectiles, IReadOnlyDictionary<int, EnemyInstance> enemies)
    {
        _projectiles.Clear();
        foreach (var projectile in projectiles)
            _projectiles.Add(ProjectileInstance.RestoreCoOpState(projectile, enemies));
    }

    public void Update(float deltaSeconds, MaximalBastion.GameSession session)
    {
        foreach (var projectile in _projectiles)
        {
            if (projectile.IsExpired) continue;
            var appearance = projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId);
            if (projectile.Target is { IsDead: true } && projectile.Kind == ProjectileKind.Homing)
            {
                projectile.Expire();
                continue;
            }

            var incomingDirection = (projectile.Kind == ProjectileKind.Homing && projectile.Target is { } destination
                ? destination.Position : projectile.AimPoint) - projectile.Position;
            if (!projectile.Update(deltaSeconds)) continue;
            if (projectile.Kind == ProjectileKind.ImpactPoint)
            {
                var recipient = ApplySplash(session, projectile);
                AddSplashEffect(session, projectile, recipient);
                session.OnWeaponImpacted(new WeaponImpact(projectile.Payload.SourceTowerId, appearance, projectile.Position));
            }
            else if (projectile.Kind == ProjectileKind.Homing && projectile.SplashRadius > 0)
            {
                var recipient = ApplySplash(session, projectile);
                AddSplashEffect(session, projectile, recipient);
                session.OnWeaponImpacted(new WeaponImpact(projectile.Payload.SourceTowerId, appearance, projectile.Position));
            }
            else if (projectile.Target is { IsDead: false, HasEscaped: false } target)
            {
                session.DamageResolver.Apply(target, projectile.Payload);
                session.Effects.AddImpact(target.Position, projectile.Color,
                    MathF.Max(6, MathF.Min(12, target.Radius * 0.6f)), appearance, incomingDirection,
                    targetEnemy: target);
                ApplyRicochet(session, projectile, target);
                session.OnWeaponImpacted(new WeaponImpact(projectile.Payload.SourceTowerId, appearance, target.Position));
            }
            projectile.Expire();
        }
        _projectiles.RemoveAll(x => x.IsExpired);
    }

    private static void ApplyRicochet(MaximalBastion.GameSession session, ProjectileInstance projectile, EnemyInstance primary)
    {
        if (projectile.RicochetRange <= 0 || projectile.RicochetDamageMultiplier <= 0) return;

        var rangeSquared = projectile.RicochetRange * projectile.RicochetRange;
        var secondary = session.Enemies
            .Where(enemy => enemy.Id != primary.Id && !enemy.IsDead && !enemy.HasEscaped &&
                Vector2.DistanceSquared(enemy.Position, primary.Position) <= rangeSquared)
            .OrderBy(enemy => Vector2.DistanceSquared(enemy.Position, primary.Position))
            .ThenBy(enemy => enemy.Id)
            .FirstOrDefault();
        if (secondary is null) return;

        session.DamageResolver.Apply(secondary, ScaleDamage(projectile.Payload, projectile.RicochetDamageMultiplier));
        var appearance = projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId);
        session.Effects.AddRicochet(primary.Position, secondary.Position, projectile.Color,
            projectile.Speed, projectile.Radius,
            MathF.Max(5, MathF.Min(9, secondary.Radius * 0.5f)), appearance, secondary);
    }

    private static DamagePayload ScaleDamage(DamagePayload payload, float multiplier) => new()
    {
        Damage = payload.Damage * multiplier,
        PriorityDamageMultiplier = payload.PriorityDamageMultiplier,
        ArmorPierce = payload.ArmorPierce,
        IgnoreShield = payload.IgnoreShield,
        IsDamageOverTime = payload.IsDamageOverTime,
        Status = payload.Status,
        SourceTowerId = payload.SourceTowerId
    };

    private static void AddSplashEffect(MaximalBastion.GameSession session, ProjectileInstance projectile,
        EnemyInstance? targetEnemy)
    {
        session.Effects.AddSplash(
            projectile.Position,
            projectile.Color,
            MathF.Max(10, projectile.SplashRadius),
            projectile.Appearance.Resolve(session, projectile.Payload.SourceTowerId), targetEnemy: targetEnemy);
    }

    private static EnemyInstance? ApplySplash(MaximalBastion.GameSession session, ProjectileInstance projectile)
    {
        var radiusSquared = projectile.SplashRadius * projectile.SplashRadius;
        IEnumerable<EnemyInstance> targets = session.Enemies.Where(enemy =>
            !enemy.IsDead && !enemy.HasEscaped &&
            Vector2.DistanceSquared(enemy.Position, projectile.Position) <= radiusSquared);
        if (projectile.SplashTargetLimit > 0)
            targets = targets
                .OrderBy(enemy => Vector2.DistanceSquared(enemy.Position, projectile.Position))
                .ThenBy(enemy => enemy.Id)
                .Take(projectile.SplashTargetLimit);
        EnemyInstance? recipient = null;
        var nearestDistanceSquared = float.MaxValue;
        foreach (var enemy in targets)
        {
            session.DamageResolver.Apply(enemy, projectile.Payload);
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var distanceSquared = Vector2.DistanceSquared(enemy.Position, projectile.Position);
            if (distanceSquared < nearestDistanceSquared ||
                distanceSquared == nearestDistanceSquared && (recipient is null || enemy.Id < recipient.Id))
            {
                recipient = enemy;
                nearestDistanceSquared = distanceSquared;
            }
        }
        return recipient;
    }
}
