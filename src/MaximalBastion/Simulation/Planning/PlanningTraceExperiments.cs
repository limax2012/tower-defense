using System.Diagnostics;
using System.Globalization;
using System.Text;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Simulation.Planning;

public sealed record PlanningTraceExperiment
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public int DelayTicks { get; init; }
    public IReadOnlyList<int> RemovedActionIndices { get; init; } = Array.Empty<int>();
    public string Outcome { get; init; } = "NO WIN UNDER FIXED SCHEDULE";
    public string Reason { get; init; } = "";
    public int Actions { get; init; }
    public int CombatActions { get; init; }
    public int PlateDeployments { get; init; }
    public int PlateCredits { get; init; }
    public int FinalWave { get; init; }
    public int Lives { get; init; }
    public int Credits { get; init; }
    public long Ticks { get; init; }
    public double WallSeconds { get; init; }
    public string? WitnessFile { get; init; }
    public ReplayVerification? Verification { get; init; }
}

public sealed record PlanningTraceExperimentReport
{
    public string BuildHash { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public PlanningScenario Scenario { get; init; } = new();
    public string Scope { get; init; } = "Known-seed wave-relative schedule sensitivity and legal action deletion; no held-out policy or generalization claim.";
    public int MaximumRollouts { get; init; }
    public double MaximumWallSeconds { get; init; }
    public int VerificationRuns { get; init; }
    public int SourceCombatActions { get; init; }
    public IReadOnlyList<int> SuccessfulRemovals { get; init; } = Array.Empty<int>();
    public string StopReason { get; init; } = "completed requested variants";
    public IReadOnlyList<PlanningTraceExperiment> Experiments { get; init; } = Array.Empty<PlanningTraceExperiment>();
}

public static class PlanningTraceExperiments
{
    public static PlanningTraceExperimentReport Run(GameContent content, PlanningTrace trace, string directory,
        int[] delays, int maximumRemovals = 8, int maximumRollouts = 12, double maximumWallSeconds = 120)
    {
        if (delays is null || delays.Any(delay => delay is < 0 or > 3600) || maximumRemovals is < 0 or > 1000 ||
            maximumRollouts is < 1 or > 1000 || !double.IsFinite(maximumWallSeconds) || maximumWallSeconds <= 0)
            throw new ArgumentException("Invalid trace experiment budget or delay.");
        var sourceVerification = PlanningReplay.Verify(content, trace);
        if (!sourceVerification.Verified || !(sourceVerification.VerifiedWin || sourceVerification.HorizonSurvived))
            throw new InvalidDataException("Trace experiments require a verified campaign win or declared horizon: " + sourceVerification.Reason);
        Directory.CreateDirectory(directory);
        var stopwatch = Stopwatch.StartNew();
        var rows = new List<PlanningTraceExperiment>();
        var removed = new HashSet<int>();
        var verificationRuns = 1;
        bool CanRun() => rows.Count < maximumRollouts && stopwatch.Elapsed.TotalSeconds < maximumWallSeconds;
        foreach (var delay in delays.Distinct())
        {
            if (!CanRun()) break;
            rows.Add(Evaluate(content, trace, directory, $"delay-{delay:D4}", "combat-delay", delay,
                Array.Empty<int>(), stopwatch, maximumWallSeconds, ref verificationRuns));
        }

        var removable = Enumerable.Range(0, trace.Actions.Count)
            .Where(index => trace.Actions[index].Command.Type is not (GameCommandType.StartWave or GameCommandType.ContinueEndless))
            .OrderBy(index => RemovalPriority(trace.Actions[index].Command.Type)).ThenBy(index => index)
            .Take(maximumRemovals);
        foreach (var index in removable)
        {
            if (!CanRun()) break;
            var proposal = removed.Append(index).Order().ToArray();
            var row = Evaluate(content, trace, directory, $"remove-{index:D5}", "greedy-action-deletion", 0,
                proposal, stopwatch, maximumWallSeconds, ref verificationRuns);
            rows.Add(row);
            if (row.Verification is { Verified: true } verification &&
                (verification.VerifiedWin || verification.HorizonSurvived)) removed.Add(index);
        }
        var report = new PlanningTraceExperimentReport
        {
            BuildHash = trace.BuildHash, ContentHash = trace.ContentHash, Scenario = trace.Scenario,
            MaximumRollouts = maximumRollouts, MaximumWallSeconds = maximumWallSeconds,
            VerificationRuns = verificationRuns,
            SourceCombatActions = trace.Actions.Count(action => action.DuringWave && action.Command.Type != GameCommandType.StartWave),
            SuccessfulRemovals = removed.Order().ToArray(), Experiments = rows,
            StopReason = stopwatch.Elapsed.TotalSeconds >= maximumWallSeconds ? "wall-time budget reached" :
                rows.Count >= maximumRollouts ? "rollout budget reached" : "completed requested variants"
        };
        PlannerStore.Write(Path.Combine(directory, "trace-experiments.json"), report);
        WriteReport(Path.Combine(directory, "trace-experiments.md"), report);
        return report;
    }

    private static int RemovalPriority(GameCommandType type) => type switch
    {
        GameCommandType.SetTargetMode => 0,
        GameCommandType.OverdriveTower or GameCommandType.ToggleAutoProtocol => 1,
        GameCommandType.DeployEmergencyDefense => 2,
        GameCommandType.UpgradeTower or GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower => 3,
        _ => 4
    };

    private static PlanningTraceExperiment Evaluate(GameContent content, PlanningTrace source, string directory,
        string id, string kind, int delay, IReadOnlyList<int> removed, Stopwatch totalClock, double maximumWallSeconds,
        ref int verificationRuns)
    {
        var timer = Stopwatch.StartNew();
        var adapter = source.OriginSaveJson is null ? PlannerSession.Create(content, source.Scenario) :
            PlannerSession.FromSave(content, PlanningFingerprint.Deserialize<SaveGameData>(source.OriginSaveJson), source.Scenario);
        var originalWaveStarts = source.Actions.Where(action => action.Command.Type == GameCommandType.StartWave)
            .ToDictionary(action => action.Wave, action => action.Tick);
        var originalIntermissions = new Dictionary<int, long>();
        foreach (var action in source.Actions.Where(action => !action.DuringWave))
            originalIntermissions.TryAdd(action.Command.Type == GameCommandType.StartWave ? action.Wave - 1 : action.Wave, action.Tick);
        var actualWaveStarts = new Dictionary<int, long>();
        var actualIntermissions = new Dictionary<int, long> { [adapter.Session.CurrentWave] = 0 };
        var maximumTicks = Math.Min(100_000_000L, Math.Max(source.FinalTick * 2,
            source.FinalTick + (long)delay * source.Actions.Count + 60 * 120));
        var excluded = removed.ToHashSet();
        string? failure = null;
        var budgetExhausted = false;

        bool Step()
        {
            if (adapter.Tick >= maximumTicks || totalClock.Elapsed.TotalSeconds >= maximumWallSeconds)
            { failure = "experiment tick or wall-time budget reached"; budgetExhausted = true; return false; }
            if (adapter.Session.IsDefeat) { failure = "defeat"; return false; }
            var wasActive = adapter.Session.Waves.IsActive;
            adapter.Advance(1);
            if (wasActive && !adapter.Session.Waves.IsActive)
                actualIntermissions[adapter.Session.CurrentWave] = adapter.Tick;
            return true;
        }

        bool AdvanceTo(long tick)
        {
            while (adapter.Tick < tick) if (!Step()) return false;
            return true;
        }

        for (var index = 0; index < source.Actions.Count && failure is null; index++)
        {
            if (excluded.Contains(index)) continue;
            var action = source.Actions[index];
            var expectedWave = action.Command.Type == GameCommandType.StartWave ? action.Wave - 1 : action.Wave;
            if (adapter.Session.CurrentWave != expectedWave)
            { failure = $"action {index} expected wave {expectedWave}, actual {adapter.Session.CurrentWave}"; break; }
            if (action.DuringWave)
            {
                if (!originalWaveStarts.TryGetValue(action.Wave, out var sourceStart) ||
                    !actualWaveStarts.TryGetValue(action.Wave, out var actualStart))
                { failure = $"combat action {index} has no wave-start anchor"; break; }
                if (!AdvanceTo(actualStart + action.Tick - sourceStart + delay)) break;
                if (!adapter.Session.Waves.IsActive)
                { failure = $"combat action {index} fell outside its active-wave window"; break; }
            }
            else
            {
                while (adapter.Session.Waves.IsActive && failure is null) if (!Step()) break;
                if (failure is not null) break;
                var offset = action.Tick - originalIntermissions.GetValueOrDefault(expectedWave, action.Tick);
                if (!AdvanceTo(actualIntermissions.GetValueOrDefault(expectedWave, adapter.Tick) + offset)) break;
            }
            var requestedTick = action.DuringWave
                ? Math.Clamp(actualWaveStarts[action.Wave] + action.RequestedTick - originalWaveStarts[action.Wave], 0, adapter.Tick)
                : adapter.Tick;
            var result = adapter.Apply(action.Command,
                $"{kind}; source action {index}; combat delay {delay} ticks", requestedTick);
            if (!result.Accepted) { failure = $"action {index} rejected: {result.Reason}"; break; }
            if (action.Command.Type == GameCommandType.StartWave)
                actualWaveStarts[adapter.Session.CurrentWave] = adapter.Tick;
        }
        while (failure is null && !adapter.CampaignVictory && !adapter.HorizonSurvived && adapter.Session.Waves.IsActive)
            if (!Step()) break;
        if (failure is null && !adapter.CampaignVictory && !adapter.HorizonSurvived)
            failure = adapter.Session.IsDefeat ? "defeat" : "schedule ended before the required terminal condition";

        ReplayVerification? verification = null;
        string? witness = null;
        if (failure is null)
        {
            verificationRuns++;
            var trace = adapter.ExportTrace() with
            {
                SearchBudget = source.SearchBudget,
                SearchRestrictions = source.SearchRestrictions
            };
            verification = PlanningReplay.Verify(content, trace);
            if (verification.Verified && (verification.VerifiedWin || verification.HorizonSurvived))
            {
                witness = id + "-witness.json";
                PlannerStore.Write(Path.Combine(directory, witness), trace);
                PlannerStore.Write(Path.Combine(directory, id + "-verification.json"), verification);
            }
            else failure = "independent verification failed: " + verification.Reason;
        }
        var accepted = adapter.Actions.Where(action => action.Accepted).ToArray();
        var row = new PlanningTraceExperiment
        {
            Id = id, Kind = kind, DelayTicks = delay, RemovedActionIndices = removed.ToArray(),
            Outcome = witness is not null ? verification!.VerifiedWin ? "VERIFIED WIN" : "VERIFIED HORIZON SURVIVAL" :
                budgetExhausted ? "BUDGET EXHAUSTED" : "NO WIN UNDER FIXED SCHEDULE",
            Reason = failure ?? "Fresh legal execution and independent replay verified.",
            Actions = accepted.Length,
            CombatActions = accepted.Count(action => action.DuringWave && action.Command.Type != GameCommandType.StartWave),
            PlateDeployments = accepted.Count(action => action.Command.Type == GameCommandType.DeployEmergencyDefense),
            PlateCredits = accepted.Where(action => action.Command.Type == GameCommandType.DeployEmergencyDefense)
                .Sum(action => Math.Max(0, action.ActualCost)),
            FinalWave = adapter.Session.CurrentWave, Lives = adapter.Session.Economy.Lives,
            Credits = adapter.Session.Economy.Credits, Ticks = adapter.Tick,
            WallSeconds = timer.Elapsed.TotalSeconds, WitnessFile = witness, Verification = verification
        };
        PlannerStore.Write(Path.Combine(directory, id + "-result.json"), row);
        return row;
    }

    private static void WriteReport(string path, PlanningTraceExperimentReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# Fixed-trace execution experiments");
        text.AppendLine();
        text.AppendLine(report.Scope);
        text.AppendLine();
        text.AppendLine($"Scenario: `{report.Scenario.Key}`. Build: `{report.BuildHash}`. Content: `{report.ContentHash}`.");
        text.AppendLine();
        text.AppendLine("Combat timestamps are relative to each actual wave start. Preparation waits for the preceding wave to clear, preserves the source preparation offsets, and uses the original entity IDs. A delayed command outside its combat window or a rejected command fails that schedule. Each deletion trial starts from the original origin and retains previously verified deletions.");
        text.AppendLine();
        text.AppendLine($"Limits: {report.MaximumRollouts} variant rollouts and {report.MaximumWallSeconds.ToString("0.##", CultureInfo.InvariantCulture)} seconds, plus independent replay checks. Stop: {report.StopReason}. Verification runs: {report.VerificationRuns}. Source combat actions: {report.SourceCombatActions}. Verified removed action indices: {string.Join(", ", report.SuccessfulRemovals)}.");
        text.AppendLine();
        text.AppendLine("| Variant | Delay (ticks) | Removed | Result | Wave | Lives | Credits | Combat actions | Plate credits | Evidence |");
        text.AppendLine("|---|---:|---:|---|---:|---:|---:|---:|---:|---|");
        foreach (var row in report.Experiments)
            text.AppendLine($"| {row.Id} | {row.DelayTicks} | {row.RemovedActionIndices.Count} | {row.Outcome} | {row.FinalWave} | {row.Lives} | {row.Credits} | {row.CombatActions} | {row.PlateCredits} | {(row.WitnessFile is null ? row.Reason.Replace('|', '/') : $"[trace]({row.WitnessFile})")} |");
        File.WriteAllText(path, text.ToString());
    }
}
