using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;

namespace MaximalBastion.Tests;

internal static class SlowStatusTests
{
    public static void Run()
    {
        SharedTimerKeepsTheStrongestIntensity();
        EqualStrengthRefreshUpdatesAttribution();

        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        MovementAndInspectorUseTheSharedEffect(content);
        ResistanceShortensIncomingDurations(content);
    }

    private static void SharedTimerKeepsTheStrongestIntensity()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Slow(10, .5f, 1));
        statuses.Update(.25f);
        statuses.Apply(Slow(-1, .3f, 1.5f));
        Check.Equal(1, statuses.Active.Count, "different slow sources share one status");
        Check.Nearly(.5f, statuses.SlowFactor, "weaker slows preserve the strongest intensity rather than adding to it");
        Check.Nearly(1.5f, SharedSlow(statuses).RemainingSeconds,
            "a weaker hit extends the stronger slow to the incoming duration without summing durations");
        Check.Equal(10, SharedSlow(statuses).SourceId, "a weaker refresh retains the stronger source's attribution");

        statuses.Apply(Slow(10, .2f, .5f));
        Check.Nearly(1.5f, SharedSlow(statuses).RemainingSeconds,
            "a shorter same-source refresh does not shorten or add to the shared timer");
        statuses.Apply(Slow(20, .6f, .25f));
        Check.Nearly(.6f, statuses.SlowFactor, "a stronger hit replaces the shared intensity");
        Check.Nearly(1.5f, SharedSlow(statuses).RemainingSeconds,
            "a stronger shorter hit preserves the longer remaining duration");
        Check.Equal(20, SharedSlow(statuses).SourceId, "the stronger hit takes utility attribution");
        statuses.Apply(Slow(20, .1f, 2));
        Check.Equal(1, statuses.Active.Count, "same-source changes also retain one shared status");
        Check.Nearly(.6f, statuses.SlowFactor, "a weaker same-source hit preserves the strongest intensity");
        Check.Nearly(2, SharedSlow(statuses).RemainingSeconds, "a longer same-source hit refreshes the shared timer");
        statuses.Update(1.5f);
        Check.Nearly(.6f, statuses.SlowFactor, "the strongest intensity lasts for the refreshed shared duration");
        Check.Nearly(.5f, SharedSlow(statuses).RemainingSeconds, "the shared timer counts down normally");
        statuses.Update(.5f);
        Check.Equal(0, statuses.Active.Count, "expiry removes the shared slow without a weaker fallback");
        Check.Nearly(0, statuses.SlowFactor, "expiry clears the movement reduction");
    }

    private static void EqualStrengthRefreshUpdatesAttribution()
    {
        var statuses = new StatusEffectController();
        statuses.Apply(Slow(1, .5f, 2));
        statuses.Apply(Slow(2, .5f, 1));
        Check.Equal(1, SharedSlow(statuses).SourceId, "an equal-strength shorter hit preserves attribution");
        statuses.Apply(Slow(3, .5f, 2));
        Check.Equal(3, SharedSlow(statuses).SourceId, "an equal-strength full refresh takes attribution");
        Check.Nearly(2, SharedSlow(statuses).RemainingSeconds, "equal-strength refreshes do not accumulate duration");
    }

    private static void MovementAndInspectorUseTheSharedEffect(GameContent content)
    {
        var session = new GameSession(content, "foundry_loop", "normal", "standard");
        var enemy = new EnemyInstance(1, content.Enemies["t3_brute"], session.Map.Path, 1, 1, "Standard");
        enemy.ApplyStatus(Slow(1, .8f, 1));
        enemy.ApplyStatus(Slow(2, .3f, 4));
        Check.Nearly(1 - EnemyInstance.MaximumSlowFraction, enemy.SpeedMultiplier,
            "the shared raw slow respects the movement cap");
        Check.Equal($"SPEED -{EnemyInstance.MaximumSlowFraction:P0} / {4f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Slow),
            "the inspector shows effective slow strength and the refreshed shared duration");
        enemy.StatusEffects.Update(1);
        Check.Nearly(1 - EnemyInstance.MaximumSlowFraction, enemy.SpeedMultiplier,
            "a weaker refresh sustains the capped movement reduction beyond the original duration");
        Check.Equal($"SPEED -{EnemyInstance.MaximumSlowFraction:P0} / {3f:0.0}s",
            EnemyConditionPresentation.Detail(enemy, EnemyCondition.Slow), "the inspector follows the shared countdown");
        enemy.StatusEffects.Update(3);
        Check.Nearly(1, enemy.SpeedMultiplier, "movement returns to normal when the shared slow expires");
    }

    private static void ResistanceShortensIncomingDurations(GameContent content)
    {
        var path = new GameSession(content, "foundry_loop", "normal", "standard").Map.Path;
        foreach (var (rank, durationScale) in new[] { ("Standard", 1f), ("Elite", .7f), ("Boss", .4f) })
        {
            var enemy = new EnemyInstance(1, content.Enemies["t3_brute"], path, 1, 1, rank);
            enemy.ApplyStatus(Slow(1, .5f, 1));
            enemy.ApplyStatus(Slow(2, .3f, 4));
            Check.Nearly(4 * durationScale, SharedSlow(enemy.StatusEffects).RemainingSeconds,
                $"{rank} resistance scales the incoming refresh before merging durations");
            Check.Nearly(.5f, enemy.StatusEffects.SlowFactor, $"{rank} resistance preserves slow intensity");
            enemy.StatusEffects.Update(4 * durationScale);
            Check.Nearly(0, enemy.StatusEffects.SlowFactor, $"{rank} shared slow expires at its adjusted duration");
        }
    }

    private static ActiveStatus SharedSlow(StatusEffectController statuses) =>
        statuses.Active.Single(status => status.Type == StatusType.Slow);

    private static StatusApplication Slow(int source, float magnitude, float duration) => new()
    {
        Type = StatusType.Slow, SourceId = source, Magnitude = magnitude, Duration = duration
    };
}
