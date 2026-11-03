using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>One cloud opacity repeated for its displayed Burn, Slow, Armor Break and Expose colors.</summary>
public readonly record struct EnemyFogPresentation(Vector4 Opacity, Color ExposeColor);

/// <summary>Bounded, soft color regions summarize timed debuffs without per-enemy layout.</summary>
public static class EnemyDebuffAuraArt
{
    public const int MaximumRegions = 4;
    private static readonly EnemyCondition[] Order =
    [EnemyCondition.Burn, EnemyCondition.Slow, EnemyCondition.ArmorBreak, EnemyCondition.Exposed];

    public static int CopyDebuffs(EnemyInstance enemy, Span<EnemyCondition> destination)
    {
        if (enemy.IsDead || enemy.HasEscaped) return 0;
        var mask = 0u;
        foreach (var status in enemy.StatusEffects.Active)
        {
            if (status.Magnitude <= 0 || status.RemainingSeconds <= 0) continue;
            var condition = status.Type switch
            {
                StatusType.Burn => EnemyCondition.Burn,
                StatusType.Slow => EnemyCondition.Slow,
                StatusType.ArmorBreak => EnemyCondition.ArmorBreak,
                StatusType.Exposed => EnemyCondition.Exposed,
                _ => EnemyCondition.None
            };
            if (condition != EnemyCondition.None) mask |= 1u << (int)condition;
        }
        var count = 0;
        foreach (var condition in Order)
            if ((mask & 1u << (int)condition) != 0 && count < destination.Length)
                destination[count++] = condition;
        return count;
    }

    public static int Mask(EnemyInstance enemy)
    {
        Span<EnemyCondition> conditions = stackalloc EnemyCondition[MaximumRegions];
        var count = CopyDebuffs(enemy, conditions);
        var mask = 0;
        for (var index = 0; index < count; index++)
            mask |= conditions[index] switch
            {
                EnemyCondition.Burn => 1,
                EnemyCondition.Slow => 2,
                EnemyCondition.ArmorBreak => 4,
                EnemyCondition.Exposed => 8,
                _ => 0
            };
        return mask;
    }

    public static int RegionCount(int mask)
    {
        var count = 0;
        for (var bit = 0; bit < MaximumRegions; bit++) count += mask >> bit & 1;
        return count;
    }

    public static int Draw(SpriteBatch batch, PrimitiveRenderer p, GameSession session, EnemyInstance enemy,
        Vector2 position, float radius, float time, bool reducedEffects, EnemyFogPresentation? presentation = null)
    {
        if (enemy.IsDead || enemy.HasEscaped) return 0;
        int mask;
        float opacity;
        Color exposeColor;
        if (presentation is { } visual)
        {
            mask = (visual.Opacity.X > .001f ? 1 : 0) | (visual.Opacity.Y > .001f ? 2 : 0) |
                (visual.Opacity.Z > .001f ? 4 : 0) | (visual.Opacity.W > .001f ? 8 : 0);
            opacity = MathF.Max(MathF.Max(visual.Opacity.X, visual.Opacity.Y),
                MathF.Max(visual.Opacity.Z, visual.Opacity.W));
            exposeColor = visual.ExposeColor;
        }
        else
        {
            mask = Mask(enemy);
            opacity = 1;
            exposeColor = EnemyConditionPresentation.ExposeAccent(session, enemy);
        }
        var count = RegionCount(mask);
        if (count == 0 || opacity <= .001f) return 0;
        var precision = exposeColor == ColorPalette.PrecisionShot;
        if (reducedEffects)
        {
            p.DebuffFog(batch, position, radius + 7, mask, precision, .62f * opacity);
            return count;
        }

        // A common density motion keeps the cloud intact when only its color assignment changes.
        var phase = time * 1.18f + enemy.Id * 1.71f;
        var curl = MathF.Sin(phase);
        var crossflow = MathF.Cos(phase * .79f + 1.4f);
        var breathing = MathF.Sin(phase * 1.37f + .8f);
        var drift = new Vector2(curl + .24f * MathF.Sin(phase * 1.91f), crossflow) *
            MathF.Min(2.6f, radius * .095f);
        var stretch = new Vector2(1 + .064f * crossflow, 1 - .055f * curl);
        var density = .045f * breathing;
        p.DebuffFog(batch, position + drift, radius + 5.5f + .5f * breathing, mask, precision,
            (.38f + density) * opacity, stretch: stretch);
        p.DebuffFog(batch, position - drift * .85f, radius + 5.5f - .5f * breathing, mask, precision,
            (.28f - density) * opacity, layer: 1, stretch: new Vector2(2 - stretch.X, 2 - stretch.Y));
        return count;
    }
}
