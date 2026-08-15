using MinimalBastion.Combat;
using MinimalBastion.Data;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Towers;

public static class TowerBehaviorRegistry
{
    public static ITowerBehavior Create(string id) => id.ToLowerInvariant() switch
    {
        "single_projectile" => new SingleProjectileBehavior(),
        "pellet_burst" => new PelletBurstBehavior(),
        "slow_projectile" => new SlowProjectileBehavior(),
        "burn_projectile" => new BurnProjectileBehavior(),
        "armor_projectile" => new ArmorProjectileBehavior(),
        "chain" => new ChainBehavior(),
        "splash_projectile" => new SplashProjectileBehavior(),
        "beam" => new BeamBehavior(),
        "aura" => new AuraBehavior(),
        _ => throw new InvalidDataException($"Unknown tower behavior: {id}")
    };
}

internal static class BehaviorHelpers
{
    public static StatusApplication? Status(TowerInstanceContext context, StatusType type, float duration, float magnitude)
    {
        return duration > 0 && magnitude > 0
            ? new StatusApplication { Type = type, Duration = duration, Magnitude = magnitude, SourceId = context.Tower.Id }
            : null;
    }

    public static StatusApplication? BurnStatus(TowerInstanceContext context, TowerLevelDefinition level)
    {
        return level.BurnDuration > 0 && level.BurnDamagePerSecond > 0
            ? new StatusApplication
            {
                Type = StatusType.Burn,
                Duration = level.BurnDuration,
                Magnitude = level.BurnDamagePerSecond,
                SourceId = context.Tower.Id,
                TickInterval = level.BurnTickInterval
            }
            : null;
    }

    public static DamagePayload Payload(TowerInstanceContext context, TowerLevelDefinition level, float damage, StatusApplication? status = null, bool ignoreShield = false, float armorPierce = 0)
    {
        return new DamagePayload
        {
            Damage = context.Session.GetEffectiveDamage(context.Tower, damage),
            PriorityDamageMultiplier = level.PriorityDamageMultiplier,
            ArmorPierce = context.Session.GetEffectiveArmorPierce(context.Tower, armorPierce),
            IgnoreShield = ignoreShield,
            Status = status,
            SourceTowerId = context.Tower.Id
        };
    }

    public static void Projectile(TowerInstanceContext context, EnemyInstance target, ProjectileKind kind, Vector2 aimPoint, float splashRadius, DamagePayload payload, float speed, Color color, float radius = 5f, int splashTargetLimit = 0)
    {
        context.Session.Projectiles.Add(new ProjectileInstance(context.Tower.Position, aimPoint, target, speed, kind, splashRadius, payload, color, radius, splashTargetLimit));
    }
}

public sealed class SingleProjectileBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        BehaviorHelpers.Projectile(context, context.Target, ProjectileKind.Homing, context.Target.Position, 0,
            BehaviorHelpers.Payload(context, level, level.Damage, null, level.IgnoreShield, level.ArmorPierce),
            level.ProjectileSpeed, context.Tower.Definition.Visual.PrimaryColor);
    }
}

public sealed class PelletBurstBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var count = Math.Max(1, level.PelletCount);
        var targets = context.Session.TargetSelector.SelectSpreadTargets(
            context.Tower.Position,
            context.Session.GetEffectiveRange(context.Tower),
            context.Target,
            count,
            context.Session.Enemies);
        for (var i = 0; i < count; i++)
        {
            var target = targets[i % targets.Count];
            BehaviorHelpers.Projectile(context, target, ProjectileKind.Homing, target.Position, 0,
                BehaviorHelpers.Payload(context, level, level.Damage, armorPierce: level.ArmorPierce),
                level.ProjectileSpeed, context.Tower.Definition.Visual.PrimaryColor, 3f);
        }
    }
}

public sealed class SlowProjectileBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var status = BehaviorHelpers.Status(context, StatusType.Slow, level.SlowDuration, level.SlowPercent);
        BehaviorHelpers.Projectile(context, context.Target, ProjectileKind.Homing, context.Target.Position, level.SplashRadius,
            BehaviorHelpers.Payload(context, level, level.Damage, status), level.ProjectileSpeed, context.Tower.Definition.Visual.PrimaryColor);
    }
}

public sealed class BurnProjectileBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var status = BehaviorHelpers.BurnStatus(context, level);
        BehaviorHelpers.Projectile(context, context.Target, ProjectileKind.Homing, context.Target.Position, level.SplashRadius,
            BehaviorHelpers.Payload(context, level, level.Damage, status), level.ProjectileSpeed, context.Tower.Definition.Visual.PrimaryColor);
    }
}

public sealed class ArmorProjectileBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var status = BehaviorHelpers.Status(context, StatusType.ArmorBreak, level.ArmorReductionDuration, level.ArmorReduction);
        BehaviorHelpers.Projectile(context, context.Target, level.SplashRadius > 0 ? ProjectileKind.ImpactPoint : ProjectileKind.Homing,
            context.Target.Position, level.SplashRadius,
            BehaviorHelpers.Payload(context, level, level.Damage, status, false, level.ArmorPierce), level.ProjectileSpeed,
            context.Tower.Definition.Visual.PrimaryColor, splashTargetLimit: level.SplashTargetLimit);
    }
}

public sealed class ChainBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var primaryStatus = BehaviorHelpers.Status(context, StatusType.Stun, level.StunDuration, 1f);
        context.Session.DamageResolver.Apply(context.Target, BehaviorHelpers.Payload(context, level,
            level.Damage * ConductiveMultiplier(context.Target), primaryStatus));
        context.Session.Effects.AddBeam(context.Tower.Position, context.Target.Position, context.Tower.Definition.Visual.PrimaryColor, 0.14f);

        var excluded = new HashSet<int> { context.Target.Id };
        var previous = context.Target.Position;
        foreach (var enemy in context.Session.TargetSelector.SelectChainTargets(previous, level.ChainRange, level.ChainCount, context.Session.Enemies, excluded))
        {
            excluded.Add(enemy.Id);
            context.Session.DamageResolver.Apply(enemy, BehaviorHelpers.Payload(context, level,
                level.ChainDamage * ConductiveMultiplier(enemy)));
            context.Session.Effects.AddBeam(previous, enemy.Position, context.Tower.Definition.Visual.AccentColor, 0.12f);
            previous = enemy.Position;
        }
        context.Session.Effects.AddFlash(previous, context.Tower.Definition.Visual.PrimaryColor, 0.18f, 28);
    }

    private static float ConductiveMultiplier(EnemyInstance enemy) => enemy.StatusEffects.SlowFactor > 0 ? 1.35f : 1f;
}

public sealed class SplashProjectileBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var status = BehaviorHelpers.Status(context, StatusType.Slow, level.SlowDuration, level.SlowPercent);
        var aimPoint = PredictImpactPoint(context, level.ProjectileSpeed);
        BehaviorHelpers.Projectile(context, context.Target, ProjectileKind.ImpactPoint, aimPoint, level.SplashRadius,
            BehaviorHelpers.Payload(context, level, level.Damage, status), level.ProjectileSpeed, context.Tower.Definition.Visual.PrimaryColor, 7f,
            level.SplashTargetLimit);
    }

    private static Vector2 PredictImpactPoint(TowerInstanceContext context, float projectileSpeed)
    {
        if (projectileSpeed <= 0) return context.Target.Position;

        var path = context.Session.Map.Path;
        var travelSeconds = Vector2.Distance(context.Tower.Position, context.Target.Position) / projectileSpeed;
        var aim = context.Target.Position;
        // Re-estimate flight time against the path-aware future position. This
        // naturally follows right-angle turns instead of extrapolating off-road.
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var futureDistance = MathF.Min(path.TotalLength, context.Target.DistanceAlongPath + context.Target.CurrentSpeed * travelSeconds);
            aim = path.GetPosition(futureDistance);
            travelSeconds = Vector2.Distance(context.Tower.Position, aim) / projectileSpeed;
        }
        return aim;
    }
}

public sealed class BeamBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context)
    {
        var level = context.Tower.Level;
        var status = BehaviorHelpers.Status(context, StatusType.Exposed, level.ExposeDuration, level.ExposePercent);
        context.Session.DamageResolver.Apply(context.Target, BehaviorHelpers.Payload(context, level, level.Damage, status, level.IgnoreShield, level.ArmorPierce));
        context.Session.Effects.AddBeam(context.Tower.Position, context.Target.Position, context.Tower.Definition.Visual.PrimaryColor, 0.15f);
        if (level.ChainCount <= 0 || level.ChainDamage <= 0 || level.ChainRange <= 0) return;

        var excluded = new HashSet<int> { context.Target.Id };
        var previous = context.Target.Position;
        foreach (var enemy in context.Session.TargetSelector.SelectChainTargets(previous, level.ChainRange, level.ChainCount, context.Session.Enemies, excluded))
        {
            excluded.Add(enemy.Id);
            context.Session.DamageResolver.Apply(enemy,
                BehaviorHelpers.Payload(context, level, level.ChainDamage, status, level.IgnoreShield, level.ArmorPierce));
            context.Session.Effects.AddBeam(previous, enemy.Position, context.Tower.Definition.Visual.AccentColor, 0.12f);
            previous = enemy.Position;
        }
    }
}

public sealed class AuraBehavior : ITowerBehavior
{
    public void Attack(TowerInstanceContext context) { }
}
