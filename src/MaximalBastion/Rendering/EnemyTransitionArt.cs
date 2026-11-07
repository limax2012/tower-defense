using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Short contour-bound shield responses and a single outward command-core release.</summary>
internal static class EnemyTransitionArt
{
    internal static void Draw(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 position, float radius, float heading, EnemyVisualFeedback feedback, bool reducedEffects)
    {
        if (enemy.IsDead || enemy.HasEscaped) return;
        DrawDetachedMaterials(batch, p, feedback, reducedEffects);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        Vector2 At(Vector2 local, float extent) => position + (forward * local.X + side * local.Y) * extent;
        var envelope = EnemyStatusArt.ShieldEnvelope(enemy);
        var sides = envelope.Count;
        var shield = enemy.Shield > 0 ? feedback.ShieldImpact : feedback.ShieldBreak;
        if (shield > 0)
        {
            var expansion = enemy.Shield > 0 ? .8f : (1 - shield) * (reducedEffects ? 2 : 5);
            var stride = reducedEffects ? 3 : 2;
            for (var edge = 0; edge < sides; edge += stride)
            {
                var first = At(envelope[edge], radius + 3.5f + expansion);
                var last = At(envelope[(edge + 1) % sides], radius + 3.5f + expansion);
                p.Line(batch, Vector2.Lerp(first, last, .15f), Vector2.Lerp(first, last, .80f),
                    ColorPalette.Shield * (shield * .8f), reducedEffects ? 1 : 1.7f);
                if (!reducedEffects && enemy.Shield > 0)
                    p.Line(batch, Vector2.Lerp(first, last, .35f), Vector2.Lerp(first, last, .55f),
                        ColorPalette.Paper * (shield * .7f), .8f);
            }
        }
        if (feedback.OverdriveTransition <= 0 || !enemy.IsBoss) return;
        if (enemy.Definition.Id == "t5_regenerator")
        {
            if (enemy.Shield <= 0) return;
            var elapsed = (1 - feedback.OverdriveTransition) * .65f;
            var progress = Math.Clamp(elapsed / .62f, 0, 1);
            var flash = MathF.Pow(MathF.Max(0, MathF.Sin(progress * MathHelper.Pi)), 1.5f);
            var extent = radius * (1.015f + .112f * MathHelper.SmoothStep(0, 1, progress));
            for (var edge = 0; edge < sides; edge++)
            {
                var first = At(envelope[edge], extent);
                var last = At(envelope[(edge + 1) % sides], extent);
                if (!reducedEffects) p.Line(batch, first, last, ColorPalette.Shield * (.17f * flash), 2.8f);
                p.Line(batch, first, last, Color.Lerp(ColorPalette.Shield, ColorPalette.Paper, .75f) *
                    ((reducedEffects ? .42f : .88f) * flash), .95f);
            }
            return;
        }
        var fade = feedback.OverdriveTransition;
        var release = (1 - fade) * (reducedEffects ? 2 : 7);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var root = position + side * sign * (radius * .64f + release);
            var tip = root + forward * radius * .22f;
            p.Line(batch, root - forward * radius * .12f, tip, ColorPalette.Hostile * (fade * .85f), 1.5f);
            p.Line(batch, tip, tip - forward * radius * .12f + side * sign * 2,
                ColorPalette.Paper * (fade * .65f), 1);
            if (!reducedEffects) p.Glow(batch, root, radius * .45f, ColorPalette.Hostile, fade * .22f);
        }
    }

    private static void DrawDetachedMaterials(SpriteBatch batch, PrimitiveRenderer p,
        EnemyVisualFeedback feedback, bool reducedEffects)
    {
        if (feedback.Particles is null) return;
        foreach (var particle in feedback.Particles)
        {
            var progress = particle.Progress(feedback.MaterialTime);
            if (progress >= 1) continue;
            var position = particle.Position(feedback.MaterialTime);
            if (particle.Kind == EnemyMaterialParticleKind.IceFlake)
            {
                p.Line(batch, position - new Vector2(.6f, .2f), position + new Vector2(.5f, .4f),
                    ColorPalette.FrostHighlight * ((1 - progress) * .58f), .65f);
                continue;
            }
            if (reducedEffects) continue;
            if (particle.Kind == EnemyMaterialParticleKind.Ember)
            {
                var fade = (1 - progress) * .75f;
                p.Circle(batch, position, .48f, ColorPalette.EmberFlame * fade);
                p.Line(batch, position, position - particle.Velocity * .035f,
                    ColorPalette.Burn * (fade * .45f), .55f);
            }
            else
            {
                var fade = MathF.Sin(progress * MathHelper.Pi);
                p.Glow(batch, position, 2 + progress * 2.5f,
                    ColorPalette.FrostHighlight, fade * .043f);
                p.Line(batch, position - Vector2.UnitX, position + Vector2.UnitX * 1.3f,
                    ColorPalette.FrostHighlight * (fade * .10f), .85f);
            }
        }
    }
}
