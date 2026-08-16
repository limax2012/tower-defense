using MinimalBastion.Combat;
using MinimalBastion.Effects;
using MinimalBastion.Enemies;
using MinimalBastion.Multiplayer;
using MinimalBastion.Towers;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Rendering;

/// <summary>
/// Produces a smooth, local-only view of the next fraction of a deterministic
/// co-op tick. Presented values never mutate simulation state, enter snapshots,
/// or contribute to checksums.
/// </summary>
public readonly struct PresentationFrame
{
    private readonly GameSession _session;

    public float LeadSeconds { get; }
    public float TimeSeconds => _session.Statistics.SimulatedSeconds + LeadSeconds;

    private PresentationFrame(GameSession session, float leadSeconds)
    {
        _session = session;
        LeadSeconds = leadSeconds;
    }

    public static PresentationFrame Create(GameSession session, float unscaledLeadSeconds)
    {
        var lead = session.IsCoOpPaused || session.IsVictory || session.IsDefeat
            ? 0
            : Math.Clamp(unscaledLeadSeconds, 0, DeterministicSessionRunner.FixedStepSeconds) * session.Speed;
        return new PresentationFrame(session, lead);
    }

    public Vector2 EnemyPosition(EnemyInstance enemy)
    {
        if (LeadSeconds <= 0 || enemy.IsDead || enemy.HasEscaped) return enemy.Position;
        var distance = Math.Clamp(enemy.DistanceAlongPath + enemy.CurrentSpeed * LeadSeconds,
            0, _session.Map.Path.TotalLength);
        return _session.Map.Path.GetPosition(distance);
    }

    public Vector2 ProjectilePosition(ProjectileInstance projectile)
    {
        if (LeadSeconds <= 0 || projectile.IsExpired || projectile.Speed <= 0) return projectile.Position;
        var destination = projectile.Kind == ProjectileKind.Homing &&
                          projectile.Target is { IsDead: false, HasEscaped: false } target
            ? EnemyPosition(target)
            : projectile.AimPoint;
        var delta = destination - projectile.Position;
        var distance = delta.Length();
        var travel = projectile.Speed * LeadSeconds;
        return distance <= MathF.Max(2, travel) || distance <= 0.001f
            ? destination
            : projectile.Position + delta / distance * travel;
    }

    public float EffectRemaining(EffectInstance effect) => MathF.Max(0, effect.Remaining - LeadSeconds);

    public float TowerScale(TowerInstance tower) => tower.VisualScaleAt(LeadSeconds);
}
