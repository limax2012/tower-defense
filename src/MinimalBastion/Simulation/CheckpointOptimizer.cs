using MinimalBastion.Data;
using MinimalBastion.Persistence;

namespace MinimalBastion.Simulation;

public sealed class CheckpointWaveRunResult
{
    public required WavePlan WavePlan { get; init; }
    public required SimulationRunResult Simulation { get; init; }
    public required bool Succeeded { get; init; }
    public required bool CampaignCompleted { get; init; }
    public SaveGameData? NextCheckpoint { get; init; }
    public string? NextCheckpointFingerprint { get; init; }
}

public sealed record CheckpointStateScore(
    int CompletedWave,
    int Lives,
    int PoweredNodeCount,
    int MaturePoweredTowerCount,
    int MatureTowerCount,
    int ApexTowerCount,
    int PoweredTowerCount,
    int Credits,
    double LifetimeContributionPerCredit,
    int InvestedCredits);

public sealed class CheckpointSearchState
{
    public required StrategyPlan Strategy { get; init; }
    public required SaveGameData Checkpoint { get; init; }
    public required string CheckpointFingerprint { get; init; }
    public required CheckpointStateScore Score { get; init; }

    public static CheckpointSearchState Create(GameContent content, StrategyPlan strategy, SaveGameData checkpoint)
    {
        strategy.ValidatePrefixForCheckpoint(checkpoint);
        var session = GameSession.RestoreSaveGame(content, checkpoint);
        if (!session.CanSaveCheckpoint)
            throw new InvalidDataException("Checkpoint search states must be captured between campaign waves.");
        return new CheckpointSearchState
        {
            Strategy = strategy,
            Checkpoint = checkpoint,
            CheckpointFingerprint = StrategyArtifactStore.Fingerprint(checkpoint),
            Score = CheckpointBeamOptimizer.Rank(content, checkpoint)
        };
    }
}

public sealed class CheckpointWaveSuccess
{
    public required string ParentCheckpointFingerprint { get; init; }
    public required WavePlan WavePlan { get; init; }
    public required SimulationRunResult Simulation { get; init; }
    public required CheckpointSearchState State { get; init; }
}

public sealed class CheckpointWaveFailure
{
    public required string ParentCheckpointFingerprint { get; init; }
    public required WavePlan WavePlan { get; init; }
    public required string Result { get; init; }
    public required int LivesRemaining { get; init; }
    public required int CreditsUnspent { get; init; }
    public SimulationFailureMargin? FailureMargin { get; init; }
    public required IReadOnlyList<SimulationRemainingEnemy> RemainingEnemies { get; init; }
    public required IReadOnlyList<SimulationRemainingEnemy> QueuedEnemies { get; init; }
}

public sealed class CheckpointCampaignCompletion
{
    public required string ParentCheckpointFingerprint { get; init; }
    public required WavePlan WavePlan { get; init; }
    public required StrategyPlan Strategy { get; init; }
    public required SimulationRunResult Simulation { get; init; }
}

public sealed class CheckpointSearchResult
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required int TargetWave { get; init; }
    public required int BeamWidth { get; init; }
    public required int Evaluations { get; init; }
    public required IReadOnlyList<CheckpointWaveSuccess> SuccessfulEvaluations { get; init; }
    public required IReadOnlyList<CheckpointSearchState> RetainedStates { get; init; }
    public required IReadOnlyList<CheckpointCampaignCompletion> CampaignCompletions { get; init; }
    public required IReadOnlyList<CheckpointWaveFailure> Failures { get; init; }
}

public static class CheckpointBeamOptimizer
{
    public static CheckpointSearchResult EvaluateWave(
        GameContent content,
        IReadOnlyList<CheckpointSearchState> frontier,
        IReadOnlyList<WavePlan> candidates,
        SimulationOptions options,
        int beamWidth)
    {
        if (frontier.Count == 0) throw new ArgumentException("Checkpoint search requires at least one frontier state.", nameof(frontier));
        if (candidates.Count == 0) throw new ArgumentException("Checkpoint search requires at least one wave-plan candidate.", nameof(candidates));
        if (beamWidth <= 0) throw new ArgumentOutOfRangeException(nameof(beamWidth));

        var targetWave = candidates[0].Wave;
        foreach (var candidate in candidates)
        {
            candidate.Validate();
            if (candidate.Wave != targetWave)
                throw new ArgumentException("Every candidate in a checkpoint-search pass must target the same wave.", nameof(candidates));
        }

        var successes = new List<CheckpointWaveSuccess>();
        var completions = new List<CheckpointCampaignCompletion>();
        var failures = new List<CheckpointWaveFailure>();
        foreach (var parent in frontier.OrderBy(state => state.CheckpointFingerprint, StringComparer.Ordinal))
        {
            parent.Strategy.ValidatePrefixForCheckpoint(parent.Checkpoint);
            if (!StrategyArtifactStore.Fingerprint(parent.Checkpoint)
                    .Equals(parent.CheckpointFingerprint, StringComparison.Ordinal) ||
                Rank(content, parent.Checkpoint) != parent.Score)
                throw new InvalidDataException("Checkpoint search frontier identity or ranking score is invalid.");
            if (parent.Checkpoint.Waves.CurrentWaveNumber + 1 != targetWave)
                throw new ArgumentException($"Frontier checkpoint is not ready for wave {targetWave}.", nameof(frontier));

            foreach (var candidate in candidates.OrderBy(plan => plan.StableKey, StringComparer.Ordinal))
            {
                var run = HeadlessSimulation.RunWave(content, parent.Checkpoint, options, parent.Strategy, candidate);
                if (run.Succeeded && run.NextCheckpoint is { } checkpoint)
                {
                    var strategy = parent.Strategy.Append(candidate);
                    var state = CheckpointSearchState.Create(content, strategy, checkpoint);
                    successes.Add(new CheckpointWaveSuccess
                    {
                        ParentCheckpointFingerprint = parent.CheckpointFingerprint,
                        WavePlan = candidate,
                        Simulation = run.Simulation,
                        State = state
                    });
                }
                else if (run.CampaignCompleted)
                {
                    completions.Add(new CheckpointCampaignCompletion
                    {
                        ParentCheckpointFingerprint = parent.CheckpointFingerprint,
                        WavePlan = candidate,
                        Strategy = parent.Strategy.Append(candidate),
                        Simulation = run.Simulation
                    });
                }
                else
                {
                    failures.Add(new CheckpointWaveFailure
                    {
                        ParentCheckpointFingerprint = parent.CheckpointFingerprint,
                        WavePlan = candidate,
                        Result = run.Simulation.Result,
                        LivesRemaining = run.Simulation.LivesRemaining,
                        CreditsUnspent = run.Simulation.CreditsUnspent,
                        FailureMargin = run.Simulation.FailureMargin,
                        RemainingEnemies = run.Simulation.RemainingEnemies,
                        QueuedEnemies = run.Simulation.QueuedEnemies
                    });
                }
            }
        }

        var retained = RankStates(successes
            .GroupBy(success => $"{success.State.CheckpointFingerprint}|{success.State.Strategy.DefaultStrategy}",
                StringComparer.Ordinal)
            .Select(group => group.OrderBy(success => success.WavePlan.StableKey, StringComparer.Ordinal).First().State),
            beamWidth);

        return new CheckpointSearchResult
        {
            TargetWave = targetWave,
            BeamWidth = beamWidth,
            Evaluations = frontier.Count * candidates.Count,
            SuccessfulEvaluations = successes
                .OrderBy(success => success.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(success => success.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray(),
            RetainedStates = retained,
            CampaignCompletions = completions
                .OrderBy(completion => completion.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(completion => completion.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray(),
            Failures = failures
                .OrderBy(failure => failure.ParentCheckpointFingerprint, StringComparer.Ordinal)
                .ThenBy(failure => failure.WavePlan.StableKey, StringComparer.Ordinal)
                .ToArray()
        };
    }

    public static IReadOnlyList<CheckpointSearchState> RankStates(
        IEnumerable<CheckpointSearchState> states,
        int limit = int.MaxValue)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        return states
            .OrderByDescending(state => state.Score.CompletedWave)
            .ThenByDescending(state => state.Score.Lives)
            .ThenByDescending(state => state.Score.PoweredNodeCount)
            .ThenByDescending(state => state.Score.MaturePoweredTowerCount)
            .ThenByDescending(state => state.Score.MatureTowerCount)
            .ThenByDescending(state => state.Score.ApexTowerCount)
            .ThenByDescending(state => state.Score.PoweredTowerCount)
            .ThenByDescending(state => state.Score.LifetimeContributionPerCredit)
            .ThenByDescending(state => state.Score.Credits)
            .ThenByDescending(state => state.Score.InvestedCredits)
            .ThenBy(state => state.CheckpointFingerprint, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    public static CheckpointStateScore Rank(GameContent content, SaveGameData checkpoint)
    {
        if (!content.Maps.TryGetValue(checkpoint.MapId, out var map) &&
            !content.Map.Id.Equals(checkpoint.MapId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checkpoint map '{checkpoint.MapId}' is not available.");
        map ??= content.Map;
        var occupiedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var poweredTowers = 0;
        var maturePoweredTowers = 0;
        foreach (var tower in checkpoint.Towers)
        {
            foreach (var node in map.PowerNodes)
            {
                var deltaX = tower.X - node.Position.X;
                var deltaY = tower.Y - node.Position.Y;
                if (deltaX * deltaX + deltaY * deltaY > node.Radius * node.Radius) continue;
                poweredTowers++;
                if (tower.LevelIndex >= 2) maturePoweredTowers++;
                occupiedNodes.Add(node.Id);
                break;
            }
        }

        var investedCredits = checkpoint.Towers.Sum(tower => tower.InvestedCredits) +
                              (checkpoint.Generator?.InvestedCredits ?? 0);
        var lifetimeContribution = checkpoint.Towers.Sum(tower =>
            (double)tower.LifetimeDamage + tower.LifetimeSupportDamageEquivalent +
            tower.LifetimeExposeDamageEquivalent + tower.LifetimeArmorBreakDamageEquivalent);
        return new CheckpointStateScore(
            checkpoint.Waves.CurrentWaveNumber,
            checkpoint.Economy.Lives,
            occupiedNodes.Count,
            maturePoweredTowers,
            checkpoint.Towers.Count(tower => tower.LevelIndex >= 2),
            checkpoint.Towers.Count(tower => tower.IsApex),
            poweredTowers,
            checkpoint.Economy.Credits,
            investedCredits <= 0 ? 0 : lifetimeContribution / investedCredits,
            investedCredits);
    }
}
