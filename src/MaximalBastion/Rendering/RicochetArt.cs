using MaximalBastion.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>A traveling copy of the firing projectile followed by its landing flash.</summary>
public static class RicochetArt
{
    public static void Draw(SpriteBatch batch, PrimitiveRenderer p, EffectInstance effect,
        float remaining, float time, bool reducedEffects, Vector2? destination = null)
    {
        if (remaining <= 0) return;
        var age = MathF.Max(0, effect.Duration - remaining);
        var appearance = effect.Appearance;
        var end = destination ?? effect.End;
        var direction = end - effect.Start;
        if (age < effect.TravelDuration)
        {
            var position = Vector2.Lerp(effect.Start, end, age / effect.TravelDuration);
            ProjectileArt.Draw(batch, p, appearance.TowerId, position, direction,
                effect.Radius, effect.Color, time, reducedEffects, appearance.Level, appearance.IsApex);
            return;
        }

        var impactDuration = MathF.Max(.001f, effect.Duration - effect.TravelDuration);
        TowerAttackArt.DrawImpact(batch, p, appearance.TowerId!, end, direction,
            effect.Color, effect.ImpactRadius, Math.Clamp(remaining / impactDuration, 0, 1),
            time, reducedEffects, appearance.Level, appearance.IsApex);
    }
}
