using System.Diagnostics;
using MaximalBastion.Data;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

/// <summary>Executes a fixed wave-policy schedule with the forecast allocator and shared decision clock.</summary>
internal static class TacticalSegmentRunner
{
    public static TacticalForecastResult Run(GameContent content, PlanningCheckpoint initial,
        IReadOnlyList<TacticalPolicySegment> segments, TacticalForecastOptions options,
        Action<PlannerSession>? spending = null, CancellationToken cancellation = default)
    {
        options.Validate();
        if (segments is null || segments.Count is < 1 or > 1000)
            throw new ArgumentException("A bounded nonempty segment schedule is required.");
        long previousEnd = 0;
        foreach (var segment in segments)
        {
            if (segment is null || segment.Policy is null || segment.StartAfterTicks != previousEnd ||
                segment.EndAfterTicks <= segment.StartAfterTicks || segment.EndAfterTicks > 216_000)
                throw new ArgumentException("Policy segments must be positive contiguous intervals starting at zero.");
            segment.Policy.Validate();
            previousEnd = segment.EndAfterTicks;
        }
        var clock = Stopwatch.StartNew();
        var trials = new List<TacticalRolloutTrial>();
        var executed = new List<TacticalPolicySegment>();
        var checkpoint = initial;
        for (var index = 0; index < segments.Count; index++)
        {
            if (cancellation.IsCancellationRequested)
                return new(trials, null, executed, "cancelled", clock.Elapsed.TotalSeconds);
            if (trials.Count >= options.MaximumTrials || index >= options.MaximumPhases || clock.Elapsed.TotalSeconds >= options.MaximumWallSeconds)
                return new(trials, null, executed, "fixed schedule budget reached", clock.Elapsed.TotalSeconds);
            var segment = segments[index];
            var final = index == segments.Count - 1;
            var requestedTicks = final ? options.Rollout.MaximumWaveTicks : checked((int)(segment.EndAfterTicks - segment.StartAfterTicks));
            var trial = TacticalForecastSearch.Evaluate(content, checkpoint, segment.Policy, spending,
                options.Rollout with
                {
                    DecisionOriginTick = options.Rollout.DecisionOriginTick ?? initial.Tick,
                    MaximumWaveTicks = Math.Min(requestedTicks, options.Rollout.MaximumWaveTicks)
                }, cancellation);
            trials.Add(trial with { Checkpoint = null, BranchCheckpoint = null, Trace = null });
            executed.Add(new(checkpoint.Tick - initial.Tick, trial.FinalTick - initial.Tick, segment.Policy));
            if (trial.WaveCleared)
                return new(trials, trial, executed, "fixed schedule cleared wave", clock.Elapsed.TotalSeconds);
            if (trial.Checkpoint is null || trial.Lives <= 0 || final || trial.FinalTick - initial.Tick != segment.EndAfterTicks)
                return new(trials, null, executed, trial.StopReason, clock.Elapsed.TotalSeconds);
            checkpoint = trial.Checkpoint;
        }
        return new(trials, null, executed, "fixed schedule exhausted", clock.Elapsed.TotalSeconds);
    }
}
