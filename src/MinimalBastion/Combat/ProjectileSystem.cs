using MinimalBastion.Enemies;
using Microsoft.Xna.Framework;
using MinimalBastion.Multiplayer;

namespace MinimalBastion.Combat;

public sealed class ProjectileSystem
{
    private readonly List<ProjectileInstance> _projectiles = new();
    public IReadOnlyList<ProjectileInstance> Projectiles => _projectiles;

    public void Add(ProjectileInstance projectile) => _projectiles.Add(projectile);

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
                foreach (var enemy in session.Enemies)
                {
                    if (!enemy.IsDead && !enemy.HasEscaped && Vector2.DistanceSquared(enemy.Position, projectile.Position) <= projectile.SplashRadius * projectile.SplashRadius)
                        session.DamageResolver.Apply(enemy, projectile.Payload);
                }
            }
            else if (projectile.Kind == ProjectileKind.Homing && projectile.SplashRadius > 0)
            {
                foreach (var enemy in session.Enemies)
                {
                    if (!enemy.IsDead && !enemy.HasEscaped && Vector2.DistanceSquared(enemy.Position, projectile.Position) <= projectile.SplashRadius * projectile.SplashRadius)
                        session.DamageResolver.Apply(enemy, projectile.Payload);
                }
            }
            else if (projectile.Target is { IsDead: false, HasEscaped: false } target)
            {
                session.DamageResolver.Apply(target, projectile.Payload);
            }
            projectile.Expire();
        }
        _projectiles.RemoveAll(x => x.IsExpired);
    }
}
