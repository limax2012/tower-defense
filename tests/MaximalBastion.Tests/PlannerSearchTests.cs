using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class PlannerSearchTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MaximalBastionPlannerSearchTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            ResumeDoesNotRepeatCompletedTrials(directory);
            GreedyAndSavingAblationSpendLegally(directory);
            EarlierFrontierCanRepairALaterFailure(directory);
            CombatRepairWorkSurvivesBudgetExhaustion(directory);
            ArtifactReplacementToleratesReaders(directory);
            CombatRepairsRetainEarlierDecisionPoints(directory);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private static void ArtifactReplacementToleratesReaders(string directory)
    {
        var path = Path.Combine(directory, "concurrent-read.json");
        PlannerStore.Write(path, new[] { "old complete frontier" });
        using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            PlannerStore.Write(path, new[] { "new complete frontier" });
            using var text = new StreamReader(reader);
            Check.True(text.ReadToEnd().Contains("old complete frontier", StringComparison.Ordinal),
                "an existing reader retains its complete artifact during atomic replacement");
            Check.Equal("new complete frontier", PlannerStore.Read<string[]>(path)[0],
                "new readers observe the complete replacement");
        }
        var lockingReader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(() => { Thread.Sleep(100); lockingReader.Dispose(); });
        try { PlannerStore.Write(path, new[] { "frontier after brief external read" }); }
        finally { release.GetAwaiter().GetResult(); }
        Check.Equal("frontier after brief external read", PlannerStore.Read<string[]>(path)[0],
            "brief external reads do not abort persistent search progress");
        Check.Equal(0, Directory.GetFiles(directory, "concurrent-read.json.*.tmp").Length,
            "atomic replacement does not leave temporary artifacts");
    }

    private static void ResumeDoesNotRepeatCompletedTrials(string directory)
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy" };
        var budget = new PlannerBudget { Simulations = 1, WallSeconds = 20, InitialBreadth = 2,
            MaximumBreadth = 4, MaximumWaveTicks = 60, WithinWaveBranches = 0 };
        var restrictions = new PlannerRestrictions { CombatPurchasing = false };
        var output = Path.Combine(directory, "resume");
        var first = SpendingPlanner.Run(content, scenario, budget, restrictions, output);
        Check.Equal(1, first.Trials.Count, "one-simulation budget is respected");
        Check.True(!first.HasVerifiedWin && first.HighestCompletedWave == 0, "arbitrary cutoff cannot be a wave clear or victory");
        var firstCursor = first.Nodes[0].NextCandidate;
        var persisted = PlannerStore.Read<PlannerSearchState>(Path.Combine(output, "frontier.json"));
        var resumed = SpendingPlanner.Run(content, scenario, budget, restrictions, output, resume: persisted);
        Check.Equal(2, resumed.Trials.Count, "resume adds work to the persisted trial history");
        Check.True(resumed.Nodes[0].NextCandidate > firstCursor, "resume moves the retained candidate cursor forward");
        Check.Equal(2, resumed.Trials.Select(trial => (trial.ParentId, trial.Candidate)).Distinct().Count(),
            "completed primary trials are not repeated after resume");
        Check.True(resumed.SimulatedTicks == first.SimulatedTicks + 60, "resumed simulation accounting is cumulative");
        Check.Equal(2, resumed.BudgetHistory.Count, "resume retains both invocation budgets");
        Check.Equal(resumed.SimulatedTicks, resumed.BudgetHistory.Sum(invocation => invocation.SimulatedTicks),
            "invocation tick costs reconcile with cumulative completed work");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = SpendingPlanner.Run(content, scenario, budget, restrictions, output, resume: resumed, cancellation: cancelled.Token);
        Check.Equal(2, stopped.Trials.Count, "already-cancelled invocation does not consume a candidate");
        Check.True(File.Exists(Path.Combine(output, "frontier.json")) && stopped.StopReason.Contains("cancelled", StringComparison.Ordinal),
            "orderly cancellation persists the frontier");
        var refused = false;
        try { SpendingPlanner.Run(content, scenario, budget, restrictions with { Saving = false }, output, resume: stopped); }
        catch (InvalidDataException) { refused = true; }
        Check.True(refused, "resume rejects incompatible candidate restrictions");
        foreach (var changed in new[] { budget with { DecisionTicks = budget.DecisionTicks + 1 },
            budget with { MaximumWaveTicks = budget.MaximumWaveTicks + 1 },
            budget with { WithinWaveBranches = budget.WithinWaveBranches + 1 } })
        {
            refused = false;
            try { SpendingPlanner.Run(content, scenario, changed, restrictions, output, resume: stopped); }
            catch (InvalidDataException) { refused = true; }
            Check.True(refused, "resume rejects changed timing or combat repair semantics");
        }
    }

    private static void GreedyAndSavingAblationSpendLegally(string directory)
    {
        var content = new ContentLoader(Path.Combine(AppContext.BaseDirectory, "ContentData")).Load();
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "easy", InformationProfile = "informed" };
        var budget = new PlannerBudget { Simulations = 1, WallSeconds = 20, MaximumWaveTicks = 60, WithinWaveBranches = 0 };
        var output = Path.Combine(directory, "greedy");
        var state = SpendingPlanner.Run(content, scenario, budget,
            new PlannerRestrictions { Baseline = "greedy", Saving = false, CombatPurchasing = false }, output);
        var failure = PlannerStore.Read<FailureArtifact>(Path.Combine(output, "latest-failure.json"));
        Check.True(failure.Trace.Actions.Any(action => action.ActualCost > 0 && action.Command.Type == GameCommandType.PlaceTower),
            "coverage greedy invests instead of treating the explicit wait alternative as its purchase");
        Check.True(failure.Trace.Actions.All(action => action.Accepted), "greedy trace contains accepted player actions");
        Check.True(failure.Trace.Actions.All(action => action.Trigger != "credits reached planned price"),
            "no-saving ablation does not execute conditional saving policies");
        Check.Equal(0, state.Trials[0].MissedPurchaseThresholds, "no-saving run cannot retain an unaffordable pending purchase");
        Check.True(PlanningReplay.Verify(content, failure.Trace).Verified, "the partial greedy run independently replays");
    }

    private static void EarlierFrontierCanRepairALaterFailure(string directory)
    {
        var content = PlanningCompetenceTests.RevisionContent();
        var initial = PlanningCompetenceTests.CreateRevisionStart(content);
        var restrictions = new PlannerRestrictions { CombatPurchasing = false, Family = "concentrated" };
        var choices = SpendingCandidateGenerator.Generate(initial.Session, new CandidateGenerationOptions
        { Family = "concentrated", PositionsPerTower = 2, AllowCombatPurchases = false });
        var expansion = choices.Select((candidate, index) => (candidate, index)).First(pair =>
            pair.candidate.Commands.Count == 1 && pair.candidate.Cost == 100 &&
            pair.candidate.Commands[0].Type == GameCommandType.PlaceTower);
        var budget = new PlannerBudget { Simulations = 64, WallSeconds = 20, InitialBreadth = 1,
            MaximumBreadth = Math.Max(8, expansion.index + 4), MaximumWaveTicks = 1500,
            WithinWaveBranches = 0, BeamWidth = 2, BacktrackDepth = 2 };

        PlannerSearchState Frontier(PlannerRestrictions settings) => new()
        {
            Scenario = initial.Scenario, Restrictions = settings, Budget = budget,
            BuildHash = PlanningFingerprint.Build(), ContentHash = PlanningFingerprint.Content(content),
            Breadth = expansion.index + 1,
            Nodes = [new PlannerNode { Id = 0, Wave = 0, Checkpoint = initial.Capture(),
                Lives = initial.Session.Economy.Lives, Credits = initial.Session.Economy.Credits,
                Investment = initial.Session.Towers.Sum(tower => tower.InvestedCredits), NextCandidate = expansion.index }]
        };
        // The fixture starts its scheduling cursor at expansion so that the later dead end precedes the revision.
        var searched = SpendingPlanner.Run(content, initial.Scenario, budget, restrictions,
            Path.Combine(directory, "backtrack"), resume: Frontier(restrictions));
        Check.True(searched.Trials[0].Cleared && searched.Trials[0].Wave == 1,
            "the initial expansion is a successful earlier-wave decision");
        Check.True(searched.Trials.Any(trial => trial.Wave == 2 && !trial.Cleared), "the expanded board reaches the later failure");
        Check.True(searched.Backtracks > 0 && searched.Trials.Any(trial => trial.BacktrackDepth > 0 && trial.Wave == 1),
            "the scheduler actually returns to the earlier purchase state");
        Check.True(searched.HasVerifiedWin, "an earlier spending revision produces an independently replayed full fixture win");
        var witness = PlannerStore.Read<PlanningTrace>(Path.Combine(directory, "backtrack", searched.WitnessFiles[0]));
        var verified = PlanningReplay.Verify(content, witness);
        Check.True(verified.VerifiedWin && verified.FromNormalStart, "backtracking discovers a legal normal-origin witness without replay rewinds");

        var disabled = restrictions with { Backtracking = false };
        var forwardOnly = SpendingPlanner.Run(content, initial.Scenario, budget, disabled,
            Path.Combine(directory, "forward-only"), resume: Frontier(disabled));
        Check.True(!forwardOnly.HasVerifiedWin && forwardOnly.Backtracks == 0,
            "the same ordered fixture cannot repair the earlier spending when backtracking is disabled");
    }

    private static void CombatRepairWorkSurvivesBudgetExhaustion(string directory)
    {
        var content = PlanningCompetenceTests.RevisionContent();
        content.Towers["rifle"].PurchaseCost = 1000;
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "fixture",
            ChallengeId = "fixture", Horizon = 2 };
        var restrictions = new PlannerRestrictions { Family = "concentrated" };
        var budget = new PlannerBudget { Simulations = 1, WallSeconds = 20, InitialBreadth = 1,
            MaximumBreadth = 2, MaximumWaveTicks = 240, WithinWaveBranches = 1 };
        var output = Path.Combine(directory, "pending-repairs");
        var first = SpendingPlanner.Run(content, scenario, budget, restrictions, output);
        Check.Equal(1, first.Trials.Count, "main rollout consumes the one-simulation budget");
        Check.Equal(1, first.PendingRepairs.Count, "unrun combat repair remains in the persistent work queue");
        var cursor = first.Nodes[0].NextCandidate;
        var repairSignature = first.PendingRepairs[0].Checkpoint.StateSignature;
        var loaded = PlannerStore.Read<PlannerSearchState>(Path.Combine(output, "frontier.json"));
        Check.Equal(repairSignature, loaded.PendingRepairs[0].Checkpoint.StateSignature,
            "pending repair retains its complete active-wave checkpoint across serialization");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var paused = SpendingPlanner.Run(content, scenario, budget, restrictions, output,
            resume: loaded, cancellation: cancelled.Token);
        Check.Equal(1, paused.PendingRepairs.Count, "cancellation cannot consume an unstarted repair task");
        var resumed = SpendingPlanner.Run(content, scenario, budget, restrictions, output, resume: paused);
        Check.Equal(2, resumed.Trials.Count, "resume evaluates the retained combat repair");
        Check.True(resumed.Trials[^1].CombatRepair, "repair evaluations are distinguished from primary candidate attempts");
        Check.Equal(0, resumed.PendingRepairs.Count, "completed repair is removed exactly once");
        Check.Equal(cursor, resumed.Nodes[0].NextCandidate, "processing a repair does not consume another primary candidate");
        Check.Equal(resumed.Trials.Count, resumed.BudgetHistory.Sum(invocation => invocation.CompletedTrials),
            "per-invocation trial accounting reconciles across cancellation and resume");
    }

    private sealed record FailureArtifact(PlannerTrial Trial, PlanningTrace Trace);

    private static void CombatRepairsRetainEarlierDecisionPoints(string directory)
    {
        var content = PlanningCompetenceTests.RevisionContent();
        content.Towers["rifle"].PurchaseCost = 1000;
        foreach (var enemy in content.Enemies.Values) enemy.Speed = 1;
        var scenario = new PlanningScenario { MapId = content.Map.Id, DifficultyId = "fixture", ChallengeId = "fixture", Horizon = 2 };
        var budget = new PlannerBudget { Simulations = 1, WallSeconds = 20, MaximumWaveTicks = 1200,
            InitialBreadth = 1, MaximumBreadth = 1, WithinWaveBranches = 2 };
        var state = SpendingPlanner.Run(content, scenario, budget, new PlannerRestrictions { Family = "concentrated" },
            Path.Combine(directory, "earlier-combat-decisions"));
        Check.Equal(2, state.PendingRepairs.Count, "combat repair retains distinct complete checkpoints");
        var ticks = state.PendingRepairs.Select(task => task.Checkpoint.Tick).Order().ToArray();
        Check.True(ticks[0] is >= 300 and <= 480 && ticks[1] is >= 720 and <= 900,
            "repair decisions cover earlier thirds of the trajectory instead of only its final seconds");
        Check.True(state.PendingRepairs.All(task =>
                PlannerSession.Restore(content, task.Checkpoint).Signature == task.Checkpoint.StateSignature),
            "distributed repair checkpoints restore exactly");
    }
}
