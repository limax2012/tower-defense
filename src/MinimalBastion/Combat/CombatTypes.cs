using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Multiplayer;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Combat;

public enum ProjectileKind
{
    Homing,
    ImpactPoint,
    Straight
}

public sealed class DamagePayload
{
    public float Damage { get; init; }
    public float ArmorPierce { get; init; }
    public bool IgnoreShield { get; init; }
    public bool IsDamageOverTime { get; init; }
    public StatusApplication? Status { get; init; }
    public int SourceTowerId { get; init; }
}

public readonly record struct DamageReport(
    int SourceTowerId,
    float IncomingDamage,
    float ShieldDamage,
    float ArmorAbsorbed,
    float HealthDamage,
    float Overkill,
    bool Killed,
    int ExposeSourceTowerId = 0,
    float ExposeDamageEquivalent = 0,
    int ArmorBreakSourceTowerId = 0,
    float ArmorBreakDamageEquivalent = 0);

public sealed class ProjectileInstance
{
    public Vector2 Position { get; private set; }
    public Vector2 AimPoint { get; }
    public EnemyInstance? Target { get; }
    public float Speed { get; }
    public ProjectileKind Kind { get; }
    public float SplashRadius { get; }
    public int SplashTargetLimit { get; }
    public DamagePayload Payload { get; }
    public bool IsExpired { get; private set; }
    public Color Color { get; }
    public float Radius { get; }

    public ProjectileInstance(
        Vector2 position,
        Vector2 aimPoint,
        EnemyInstance? target,
        float speed,
        ProjectileKind kind,
        float splashRadius,
        DamagePayload payload,
        Color color,
        float radius,
        int splashTargetLimit = 0)
    {
        Position = position;
        AimPoint = aimPoint;
        Target = target;
        Speed = speed;
        Kind = kind;
        SplashRadius = splashRadius;
        Payload = payload;
        Color = color;
        Radius = radius;
        SplashTargetLimit = Math.Max(0, splashTargetLimit);
    }

    public bool Update(float deltaSeconds)
    {
        if (IsExpired) return false;
        var destination = Kind == ProjectileKind.Homing && Target is { IsDead: false, HasEscaped: false } ? Target.Position : AimPoint;
        var delta = destination - Position;
        var distance = delta.Length();
        if (distance <= MathF.Max(2f, Speed * deltaSeconds))
        {
            Position = destination;
            return true;
        }
        Position += delta / distance * Speed * deltaSeconds;
        return false;
    }

    public void Expire() => IsExpired = true;

    public ProjectileRuntimeState CaptureCoOpState() => new()
    {
        X = Position.X,
        Y = Position.Y,
        AimX = AimPoint.X,
        AimY = AimPoint.Y,
        TargetEnemyId = Target?.Id ?? 0,
        Speed = Speed,
        Kind = (int)Kind,
        SplashRadius = SplashRadius,
        SplashTargetLimit = SplashTargetLimit,
        Damage = Payload.Damage,
        ArmorPierce = Payload.ArmorPierce,
        IgnoreShield = Payload.IgnoreShield,
        IsDamageOverTime = Payload.IsDamageOverTime,
        Status = Payload.Status,
        SourceTowerId = Payload.SourceTowerId,
        PackedColor = Color.PackedValue,
        Radius = Radius
    };

    public static ProjectileInstance RestoreCoOpState(ProjectileRuntimeState data, IReadOnlyDictionary<int, EnemyInstance> enemies)
    {
        if (!Enum.IsDefined(typeof(ProjectileKind), data.Kind))
            throw new InvalidDataException("Network projectile kind is invalid.");
        enemies.TryGetValue(data.TargetEnemyId, out var target);
        return new ProjectileInstance(
            new Vector2(data.X, data.Y),
            new Vector2(data.AimX, data.AimY),
            target,
            MathF.Max(0, data.Speed),
            (ProjectileKind)data.Kind,
            MathF.Max(0, data.SplashRadius),
            new DamagePayload
            {
                Damage = MathF.Max(0, data.Damage),
                ArmorPierce = MathF.Max(0, data.ArmorPierce),
                IgnoreShield = data.IgnoreShield,
                IsDamageOverTime = data.IsDamageOverTime,
                Status = data.Status,
                SourceTowerId = data.SourceTowerId
            },
            new Color(data.PackedColor),
            MathF.Max(0, data.Radius),
            Math.Max(0, data.SplashTargetLimit));
    }
}

public interface ITowerBehavior
{
    void Attack(TowerInstanceContext context);
}

public sealed class TowerInstanceContext
{
    public required MinimalBastion.Towers.TowerInstance Tower { get; init; }
    public required MinimalBastion.Enemies.EnemyInstance Target { get; init; }
    public required MinimalBastion.GameSession Session { get; init; }
}
