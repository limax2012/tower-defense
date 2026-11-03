using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;

namespace MaximalBastion.Rendering;

/// <summary>Local shell vibration that leaves path progress and ground contacts untouched.</summary>
internal static class EnemyStunMotion
{
    internal const float MaximumDisplacement = .6f;

    internal static float RemainingSeconds(EnemyInstance enemy)
    {
        if (enemy.IsDead || enemy.HasEscaped) return 0;
        var remaining = 0f;
        var statuses = enemy.StatusEffects.Active;
        for (var index = 0; index < statuses.Count; index++)
        {
            var status = statuses[index];
            if (status.Type == StatusType.Stun && status.Magnitude > 0)
                remaining = MathF.Max(remaining, status.RemainingSeconds);
        }
        return remaining;
    }

    /// <summary>Displacement in logical pixels: X follows the nose, Y crosses the shell.</summary>
    internal static Vector2 BodyOffset(EnemyInstance enemy, float time, bool reducedEffects)
    {
        if (reducedEffects) return Vector2.Zero;
        var remaining = RemainingSeconds(enemy);
        if (remaining <= 0) return Vector2.Zero;
        var settling = MathHelper.SmoothStep(0, 1, Math.Clamp(remaining / .06f, 0, 1));
        var seed = enemy.Id * 1.731f;
        var longitudinal = .24f * (MathF.Sin(time * 115 + seed) + .35f * MathF.Sin(time * 193 + seed * 2));
        var lateral = .39f * (MathF.Sin(time * 143 + seed * 1.37f) + .25f * MathF.Sin(time * 251 + seed));
        return new Vector2(longitudinal, lateral) * settling;
    }

    internal static Vector2 WorldOffset(EnemyInstance enemy, float heading, float time, bool reducedEffects)
    {
        var local = BodyOffset(enemy, time, reducedEffects);
        if (local == Vector2.Zero) return Vector2.Zero;
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        return forward * local.X + new Vector2(-forward.Y, forward.X) * local.Y;
    }
}
