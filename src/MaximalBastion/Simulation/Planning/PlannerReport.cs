using System.Globalization;
using System.Text;

namespace MaximalBastion.Simulation.Planning;

public static class PlannerReport
{
    public static void Write(string directory, PlannerSearchState state)
    {
        PlannerStore.Write(Path.Combine(directory, "results.json"), new
        {
            state.SolverVersion, state.BuildHash, state.ContentHash, state.Scenario, state.ImportedStart,
            state.Outcome, state.StopReason, state.Budget, state.Restrictions, state.HighestCompletedWave,
            state.BudgetHistory, state.InterruptedTrials, state.InterruptedSimulatedTicks,
            PendingRepairCount = state.PendingRepairs.Count,
            state.Backtracks, state.WallSeconds, state.SimulatedTicks, state.CheckpointMilliseconds,
            SimulatedTicksPerWallSecond = state.SimulatedTicks / Math.Max(.001, state.WallSeconds),
            ApproximateManagedBytes = GC.GetTotalMemory(false), state.VerificationFailure,
            state.WitnessFiles, state.StrategyClusters, Trials = state.Trials, Frontier = state.Nodes.Select(node => new
            { node.Id, node.ParentId, node.Wave, node.NextCandidate, node.Preferred, node.Composition, node.Lives, node.Credits })
        });
        var csv = new StringBuilder("trial,parent,candidate,family,wave,cleared,victory,lives,credits,ticks,seconds,actions,combat_actions,invalid_actions,plates,backtrack_depth,damage,overkill,armor_absorbed,regeneration_estimate,suppressed_tower_seconds,idle_supports,missed_thresholds\n");
        foreach (var trial in state.Trials)
            csv.AppendLine(string.Join(",", new object[] { trial.Index, trial.ParentId, trial.Candidate, Quote(trial.Family), trial.Wave,
                trial.Cleared, trial.Victory, trial.Lives, trial.Credits, trial.Ticks, trial.WallSeconds.ToString("F4", CultureInfo.InvariantCulture),
                trial.AcceptedActions, trial.CombatActions, trial.InvalidActions, trial.PlatePurchases, trial.BacktrackDepth,
                F(trial.Damage), F(trial.Overkill), F(trial.ArmorAbsorbed), F(trial.EstimatedRegeneration), F(trial.SuppressedTowerSeconds),
                trial.IdleSupports, trial.MissedPurchaseThresholds }));
        File.WriteAllText(Path.Combine(directory, "trials.csv"), csv.ToString());
        var report = new StringBuilder("# Strategy search evidence\n\n");
        report.AppendLine($"**{state.Outcome}** — {state.Scenario.Key}\n");
        report.AppendLine($"Start: {(state.ImportedStart ? "imported checkpoint; normal-start reachability is not established" : "normal fresh constructor state")}. Stop: {state.StopReason}.\n");
        report.AppendLine($"Build `{state.BuildHash}`; content `{state.ContentHash}`; solver `{state.SolverVersion}`.\n");
        report.AppendLine($"Completed through wave {state.HighestCompletedWave}. {state.Trials.Count} candidate wave rollouts, {state.Trials.Count(trial => trial.Cleared)} wave clears, {state.WitnessFiles.Count} independently replayed terminal traces, {state.Backtracks} earlier-state expansions. These counts describe search yield, not a player win probability.\n");
        report.AppendLine($"Measured search wall time {state.WallSeconds:F2} s; simulation ticks {state.SimulatedTicks}; throughput {state.SimulatedTicks / Math.Max(.001, state.WallSeconds):F0} ticks/s; successor capture {state.CheckpointMilliseconds:F2} ms total. Simulation remains 60 Hz at speed one. Search scheduling and intermediate persistence are included; final report serialization and process startup are excluded. Rendering FPS is unrelated.\n");
        report.AppendLine("## Budget and interpretation\n");
        report.AppendLine($"Per invocation: {state.Budget.Simulations} simulations, {state.Budget.WallSeconds} seconds, {state.Budget.MemoryMegabytes} MiB managed-memory stop, {state.Budget.Workers} audit workers. Beam preference {state.Budget.BeamWidth}; initial/maximum breadth {state.Budget.InitialBreadth}/{state.Budget.MaximumBreadth}; periodic decisions {state.Budget.DecisionTicks} ticks; maximum wave duration {state.Budget.MaximumWaveTicks} ticks; backtrack depth {state.Budget.BacktrackDepth}; combat repair branches {state.Budget.WithinWaveBranches}.\n");
        report.AppendLine("The memory limit is a cooperative stop checked between batches, not an OS-enforced peak allocation limit. Wave duration, finite positions, policy templates, approximate beam scheduling, rolling combat checkpoints and action limits restrict search. No dominance or infeasibility certificate is claimed. Lower-priority boards remain resumable.\n");
        report.AppendLine("Oracle search may inspect future waves and retry. An informed observation policy sees live enemies, credits, towers, map and public unlocks; offline selection of such policies still uses retries and is not a fresh human attempt. Authored campaign seeds vary candidate exploration, not the deterministic enemy schedule. No robust-policy claim follows from repeating an identical replay.\n");
        report.AppendLine("Regeneration and suppressed-tower seconds are sampled diagnostic estimates; damage, overkill, escapes, costs and command outcomes come from actual events. Firing uptime, exact regeneration attribution, exhaustive timing robustness and mandatory-family proofs are not measured here.\n");
        report.AppendLine("## Verified traces\n");
        if (state.WitnessFiles.Count == 0) report.AppendLine("No independently verified terminal trace within this budget. Increase breadth/backtracking or inspect the failure record; this is not evidence of impossibility.\n");
        foreach (var witness in state.WitnessFiles)
            report.AppendLine($"- [{witness}]({witness}), [build order]({Path.ChangeExtension(witness, ".csv")}).");
        report.AppendLine($"\nWinning strategy clusters: {state.StrategyClusters.Count}. The declared clustering uses tower/path/Apex counts, occupied 80-unit regions, combat-action bands of 20 and Plate-use bands of three. This is a descriptive partition, not proof of independent strategies or mandatory families.");
        report.AppendLine("\n## Retained board alternatives\n\n| Wave | Boards | Lives range | Reserve range | Compositions |\n| --- | ---: | ---: | ---: | ---: |");
        foreach (var wave in state.Nodes.GroupBy(node => node.Wave).OrderBy(group => group.Key))
            report.AppendLine($"| {wave.Key} | {wave.Count()} | {wave.Min(node => node.Lives)}–{wave.Max(node => node.Lives)} | {wave.Min(node => node.Credits)}–{wave.Max(node => node.Credits)} | {wave.Select(node => node.Composition).Distinct().Count()} |");
        report.AppendLine("\nFull attempts, costs, failures and signatures: [JSON](results.json), [CSV](trials.csv). Resume from [frontier](frontier.json). No production balance values or saves are modified.");
        if (state.VerificationFailure is not null) report.AppendLine($"\nReplay discrepancy requires investigation: {state.VerificationFailure}");
        File.WriteAllText(Path.Combine(directory, "report.md"), report.ToString());
    }

    public static void WriteBuildOrder(string path, PlanningTrace trace)
    {
        var csv = new StringBuilder("requested_tick,tick,resolved_tick,wave,during_wave,trigger,action,entity,tower,x,y,doctrine,specialization,target,cost,credits_before,credits_after,signature\n");
        foreach (var action in trace.Actions)
        {
            var command = action.Command;
            csv.AppendLine(string.Join(",", new object[] { action.RequestedTick, action.Tick, action.ResolvedTick, action.Wave, action.DuringWave, Quote(action.Trigger), command.Type,
                command.EntityId, Quote(command.TowerDefinitionId), F(command.X), F(command.Y), Quote(command.DoctrineId),
                Quote(command.SpecializationId), command.TargetMode, action.ActualCost, action.CreditsBefore, action.CreditsAfter, action.StateSignature }));
        }
        File.WriteAllText(path, csv.ToString());
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
