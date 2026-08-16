using MinimalBastion.Enemies;
using Microsoft.Xna.Framework;
using MinimalBastion.Multiplayer;

namespace MinimalBastion.Combat;

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

    public void Update(float deltaSeconds, MinimalBastion.GameSession session)
    {
        foreach (var projectile in _projectiles)
        {
            if (projectile.IsExpired) continue;
            if (projectile.Target is { IsDead: true } && projectile.Kind == ProjectileKind.Homing)
            {
                projectile.Expire();
                continue;
            }

            if (!projectile.Update(deltaSeconds)) continue;
            if (projectile.Kind == ProjectileKind.ImpactPoint)
            {
                ApplySplash(session, projectile);
                AddSplashEffect(session, projectile);
            }
            else if (projectile.Kind == ProjectileKind.Homing && projectile.SplashRadius > 0)
            {
                ApplySplash(session, projectile);
                AddSplashEffect(session, projectile);
            }
            else if (projectile.Target is { IsDead: false, HasEscaped: false } target)
            {
                session.DamageResolver.Apply(target, projectile.Payload);
                session.Effects.AddImpact(target.Position, projectile.Color,
                    MathF.Max(6, MathF.Min(12, target.Radius * 0.6f)));
                ApplyRicochet(session, projectile, target);
            }
            projectile.Expire();
        }
        _projectiles.RemoveAll(x => x.IsExpired);
    }

    private static void ApplyRicochet(MinimalBastion.GameSession session, ProjectileInstance projectile, EnemyInstance primary)
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
        session.Effects.AddBeam(primary.Position, secondary.Position, projectile.Color, 0.11f);
        session.Effects.AddImpact(secondary.Position, projectile.Color,
            MathF.Max(5, MathF.Min(9, secondary.Radius * 0.5f)));
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

    private static void AddSplashEffect(MinimalBastion.GameSession session, ProjectileInstance projectile)
    {
        session.Effects.AddSplash(
            projectile.Position,
            projectile.Color,
            MathF.Max(10, projectile.SplashRadius));
    }

    private static void ApplySplash(MinimalBastion.GameSession session, ProjectileInstance projectile)
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
        foreach (var enemy in targets) session.DamageResolver.Apply(enemy, projectile.Payload);
    }
}
