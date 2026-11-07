using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation;
using MaximalBastion.Simulation.Planning;

namespace MaximalBastion.Tests;

internal static class PlannerCli
{
    public static int Run(GameContent content, string[] args)
    {
        try
        {
            var output = Path.GetFullPath(Value(args, "--output") ?? ".build/planner");
            if (Has(args, "--planner-history-layout")) return HistoryLayoutCli.Run(content, args, output);
            if (Has(args, "--no-combat-sales") && (Has(args, "--planner-diagnostics") ||
                Has(args, "--planner-checkpoint-audit") || Has(args, "--planner-tests")))
                throw new ArgumentException("This command uses fixed fixtures. Use a scenario search to set the sale policy.");
            if (Has(args, "--planner-endless-continuation")) return EndlessLayoutCli.Run(content, args, output);
            if (Has(args, "--planner-layout-campaign")) return RunLayoutCampaign(content, args, output);
            if (Has(args, "--planner-tactical-forecast")) return RunTacticalForecast(content, args, output);
            if (Has(args, "--planner-diagnostics"))
            { PlanningCompetenceTests.WriteEvidence(output); Console.WriteLine($"Diagnostic evidence: {output}"); return 0; }
            if (Has(args, "--planner-checkpoint-audit"))
            { HistoricalCheckpointAudit.Run(content, Required(args, "--checkpoint"), output); Console.WriteLine($"Checkpoint allocation evidence: {output}"); return 0; }
            if (Has(args, "--planner-tests"))
            {
                PlannerAdapterTests.Run(); AutoPlayerCommandTests.Run(content); PlanningCandidateTests.Run();
                PlanningCompetenceTests.Run(); PlannerSearchTests.Run(); ApexAllocationSearchTests.Run();
                LayoutBlueprintTests.Run(); LayoutCampaignRunnerTests.Run(); TacticalForecastTests.Run();
                SelectableThreatFocusTests.Run();
                EndlessLayoutContinuationTests.Run(); HistoryLayoutImportTests.Run();
                HelixCalibrationTests.Run(); PrismCalibrationTests.Run(); RainlineCalibrationTests.Run();
                CinderworksCalibrationTests.Run();
                Console.WriteLine("Planner tests passed, including history import and campaign calibrations.");
                return 0;
            }
            if (Has(args, "--planner-revalidate"))
            {
                var checkpoint = PlannerStore.Read<PlanningCheckpoint>(Required(args, "--checkpoint"));
                if (Has(args, "--no-combat-sales"))
                    checkpoint = checkpoint with { Scenario = checkpoint.Scenario with { ForbidCombatSales = true } };
                var adapter = PlannerSession.Revalidate(content, checkpoint);
                PlannerStore.Write(Path.Combine(output, "checkpoint.json"), adapter.Capture());
                PlannerStore.Write(Path.Combine(output, "revalidation.json"), new
                {
                    PreviousBuildHash = checkpoint.BuildHash, BuildHash = PlanningFingerprint.Build(),
                    checkpoint.ContentHash, checkpoint.Tick, PreviousStateSignature = checkpoint.StateSignature,
                    StateSignature = adapter.Signature,
                    Method = adapter.RestorationMethod, Actions = adapter.Actions.Count,
                    FromNormalStart = checkpoint.OriginSaveJson is null
                });
                Console.WriteLine($"Independent prefix revalidation passed: {Path.Combine(output, "checkpoint.json")}");
                return 0;
            }
            if (Has(args, "--planner-apex-allocations"))
            {
                var initial = ReadSearchCheckpoint(content, args, new PlanningScenario());
                var options = new ApexAllocationOptions
                {
                    MaximumTrials = Integer(args, "--simulations", 256), MaximumWallSeconds = Number(args, "--seconds", 120),
                    MaximumUpgrades = Integer(args, "--apex-upgrades", 1), MaximumFundingSales = Integer(args, "--funding-sales", 0),
                    FundingSaleCandidates = Integer(args, "--sale-candidates", 6), DecisionTicks = Integer(args, "--decision-ticks", 60),
                    TargetingProfiles = (Value(args, "--targeting") ?? "split,armored,first,support,strongest").Split(',')
                };
                PlannerStore.Write(Path.Combine(output, "initial.json"), initial);
                using var stop = new CancellationTokenSource();
                ConsoleCancelEventHandler allocationCancel = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
                Console.CancelKeyPress += allocationCancel;
                ApexAllocationResult result;
                var index = 0;
                try
                {
                    result = ApexAllocationSearch.Search(content, initial, options, trial =>
                    {
                        index++;
                        if (trial.WaveCleared) PlannerStore.Write(Path.Combine(output, $"clear-{index:D6}.json"), trial.Checkpoint);
                        if (trial.WinningTrace is not { } trace) return;
                        PlannerStore.Write(Path.Combine(output, $"witness-{index:D6}.json"), trace);
                        PlannerStore.Write(Path.Combine(output, $"witness-{index:D6}-verification.json"), trial.Verification);
                        PlannerReport.WriteBuildOrder(Path.Combine(output, $"witness-{index:D6}.csv"), trace);
                    }, stop.Token);
                }
                finally { Console.CancelKeyPress -= allocationCancel; }
                var outcome = result.Trials.Any(trial => trial.Verification is { VerifiedWin: true }) ? "VERIFIED WIN" :
                    result.Trials.Any(trial => trial.WaveCleared) ? "NEXT WAVE CLEARED" : "NO SOLUTION FOUND WITHIN BUDGET";
                PlannerStore.Write(Path.Combine(output, "allocations.json"), new
                {
                    Outcome = outcome, initial.Scenario, initial.BuildHash, initial.ContentHash,
                    FromNormalStart = initial.OriginSaveJson is null, Options = options,
                    result.CandidateCount, result.StopReason, result.WallSeconds,
                    Trials = result.Trials.Select(trial => new
                    {
                        trial.Plan, trial.Accepted, trial.Wave, trial.WaveCleared, trial.CampaignVictory,
                        trial.Kills, trial.Lives, trial.Credits, trial.UpfrontCost, trial.EscapedHealth,
                        trial.RemainingEnemies, trial.QueuedEnemies, trial.RemainingHealth, trial.Ticks,
                        trial.WallSeconds, trial.CombatActions, trial.PlatePurchases, trial.Verification, trial.Rejection
                    })
                });
                Console.WriteLine($"{outcome}; {result.Trials.Count} allocation trials; {result.StopReason}\n{Path.Combine(output, "allocations.json")}");
                return result.Trials.Any(trial => trial.Verification is { Verified: false }) ? 1 : 0;
            }
            if (Has(args, "--planner-verify"))
            {
                var trace = ReadTrace(args);
                var verification = PlanningReplay.Verify(content, trace);
                Console.WriteLine(JsonSerializer.Serialize(verification, PlannerStore.Json));
                return verification.Verified ? 0 : 1;
            }
            if (Has(args, "--planner-stress"))
            {
                var trace = ReadTrace(args);
                var delays = (Value(args, "--delays") ?? "0,15,30,60").Split(',').Select(int.Parse).ToArray();
                var result = PlanningTraceExperiments.Run(content, trace, output, delays,
                    Integer(args, "--remove-actions", 8), Integer(args, "--simulations", 12), Number(args, "--seconds", 120));
                Console.WriteLine($"Trace sensitivity evidence: {output}; {result.StopReason}");
                return 0;
            }
            var resumePath = Value(args, "--resume");
            var resume = resumePath is null ? null : PlannerStore.Read<PlannerSearchState>(resumePath);
            var preset = PlannerBudget.Preset(Value(args, "--budget") ?? "smoke");
            var budget = preset with
            {
                Simulations = Integer(args, "--simulations", preset.Simulations), WallSeconds = Number(args, "--seconds", preset.WallSeconds),
                Workers = Integer(args, "--workers", preset.Workers), BeamWidth = Integer(args, "--beam", preset.BeamWidth),
                InitialBreadth = Integer(args, "--breadth", preset.InitialBreadth), MaximumBreadth = Integer(args, "--max-breadth", preset.MaximumBreadth),
                BacktrackDepth = Integer(args, "--backtrack", preset.BacktrackDepth), DecisionTicks = Integer(args, "--decision-ticks", preset.DecisionTicks),
                WithinWaveBranches = Integer(args, "--combat-branches", preset.WithinWaveBranches),
                MemoryMegabytes = Integer(args, "--memory-mb", preset.MemoryMegabytes)
            };
            budget.Validate();
            var restrictions = resume?.Restrictions ?? new PlannerRestrictions
            {
                PlacementHeuristics = !Has(args, "--no-placement"), CombatPurchasing = !Has(args, "--no-combat-purchases"),
                SupportReasoning = !Has(args, "--no-support"), Saving = !Has(args, "--no-saving"), Backtracking = !Has(args, "--no-backtracking"),
                Family = Value(args, "--family") ?? "unrestricted", Baseline = Value(args, "--baseline") ?? "planner",
                ExcludedTowerId = Value(args, "--exclude-tower"),
                ReactionTicks = Integer(args, "--reaction-ticks", 0), MinimumActionTicks = Integer(args, "--action-ticks", 0)
            };
            if (restrictions.Baseline is not ("planner" or "random" or "greedy" or "experienced"))
                throw new ArgumentException("Baseline must be planner, random, greedy or experienced.");
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                var seeds = (Value(args, "--seeds") ?? Value(args, "--seed") ?? "1337").Split(',').Select(int.Parse).ToArray();
                var discovery = PlanningScenarioRegistry.Discover(content, seeds, Has(args, "--include-coop"),
                    Has(args, "--endless") ? Integer(args, "--max-wave", 35) : null);
                if (Has(args, "--no-combat-sales"))
                    discovery = discovery with { Scenarios = discovery.Scenarios.Select(item => item with { ForbidCombatSales = true }).ToArray() };
                if (Has(args, "--planner-registry"))
                {
                    PlannerStore.Write(Path.Combine(output, "registry.json"), discovery);
                    Console.WriteLine($"{discovery.Scenarios.Count} valid scenario rows; {discovery.Exclusions.Count} explicitly excluded combinations. {output}");
                    return 0;
                }
                if (resume is not null && Has(args, "--no-combat-sales") && !resume.Scenario.ForbidCombatSales)
                    throw new ArgumentException("A frontier must resume under its original sale policy. Start a separate restricted search.");
                var scenario = resume?.Scenario ?? new PlanningScenario
                {
                    MapId = Value(args, "--map") ?? content.Map.Id, DifficultyId = Value(args, "--difficulty") ?? "normal",
                    ChallengeId = Value(args, "--challenge") ?? "standard", Seed = seeds[0],
                    Horizon = Integer(args, "--max-wave", 30), Endless = Has(args, "--endless"), CoOp = Has(args, "--coop"),
                    ForbidCombatSales = Has(args, "--no-combat-sales"),
                    InformationProfile = Value(args, "--information") ?? "oracle", ExecutionProfile = Value(args, "--execution") ?? "expert"
                };
                if (restrictions.Baseline is "random" or "experienced" && (scenario.InformationProfile != "oracle" ||
                    scenario.ExecutionProfile != "expert" || restrictions.ReactionTicks != 0 || restrictions.Family != "unrestricted" || restrictions.ExcludedTowerId is not null || !restrictions.PlacementHeuristics || !restrictions.SupportReasoning || !restrictions.Saving))
                    throw new ArgumentException("Legacy random/experienced baselines require oracle information, expert execution without reaction delay, unrestricted family, placement, support and saving enabled. Use greedy for informed or ablation runs.");
                if (Has(args, "--planner-audit"))
                {
                    PlannerStore.Write(Path.Combine(output, "registry.json"), discovery);
                    var scenarios = discovery.Scenarios.Where(item => Match(args, "--map", item.MapId) && Match(args, "--difficulty", item.DifficultyId) &&
                        Match(args, "--challenge", item.ChallengeId)).Select(item => item with
                        { InformationProfile = scenario.InformationProfile, ExecutionProfile = scenario.ExecutionProfile, ForbidCombatSales = scenario.ForbidCombatSales,
                            Horizon = item.Endless ? item.Horizon : scenario.Horizon }).ToArray();
                    var results = new ConcurrentBag<AuditRow>();
                    Parallel.ForEach(scenarios, new ParallelOptions { MaxDegreeOfParallelism = budget.Workers }, item =>
                    {
                        if (cancellation.IsCancellationRequested) return;
                        var directory = Path.Combine(output, Safe(item.Key));
                        var manifest = Path.Combine(directory, "frontier.json");
                        var previous = Has(args, "--resume-audit") && File.Exists(manifest) ? PlannerStore.Read<PlannerSearchState>(manifest) : null;
                        var result = SpendingPlanner.Run(content, item, budget, restrictions, directory, resume: previous, cancellation: cancellation.Token);
                        results.Add(new AuditRow(result.Scenario, result.Outcome, result.HighestCompletedWave, result.Trials.Count,
                            result.Backtracks, result.WallSeconds, result.BuildHash, result.ContentHash, result.ImportedStart, $"{Safe(item.Key)}/report.md"));
                        Console.WriteLine($"{item.Key}: {result.Outcome}; completed {result.HighestCompletedWave}; trials {result.Trials.Count}");
                    });
                    WriteMatrix(output, scenarios, results.ToArray());
                    return 0;
                }
                PlanningCheckpoint? initial = null;
                if (Value(args, "--checkpoint") is not null)
                {
                    initial = ReadSearchCheckpoint(content, args, scenario);
                    scenario = initial.Scenario;
                }
                var search = SpendingPlanner.Run(content, scenario, budget, restrictions, output, initial, resume, cancellation.Token);
                Console.WriteLine($"{search.Outcome}; completed wave {search.HighestCompletedWave}; {search.Trials.Count} rollouts; {search.StopReason}\n{Path.Combine(output, "report.md")}");
                return search.VerificationFailure is null ? 0 : 1;
            }
            finally { Console.CancelKeyPress -= cancel; }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or JsonException)
        { Console.Error.WriteLine(exception.Message); return 2; }
    }

    private static PlanningCheckpoint ReadCheckpoint(GameContent content, string path, PlanningScenario scenario)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 512L * 1024 * 1024) throw new InvalidDataException("Planner artifact exceeds 512 MiB.");
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.EnumerateObject().Any(property => property.Name.Equals("snapshotJson", StringComparison.OrdinalIgnoreCase)))
            return PlannerStore.Read<PlanningCheckpoint>(path);
        var save = document.RootElement.EnumerateObject().Any(property => property.Name.Equals("checkpoint", StringComparison.OrdinalIgnoreCase))
            ? StrategyArtifactStore.LoadCheckpoint(path, content).Checkpoint : PlannerStore.Read<SaveGameData>(path);
        scenario = scenario with { MapId = save.MapId, DifficultyId = save.DifficultyId, ChallengeId = save.ChallengeId,
            CoOp = save.IsCoOp, StartingProfile = "imported" };
        return PlannerSession.FromSave(content, save, scenario).Capture();
    }

    private static PlanningCheckpoint ReadSearchCheckpoint(GameContent content, string[] args, PlanningScenario scenario)
    {
        var checkpoint = ReadCheckpoint(content, Required(args, "--checkpoint"), scenario);
        return Has(args, "--no-combat-sales") ? PlannerSession.ForbidCombatSales(content, checkpoint) : checkpoint;
    }

    private static PlanningTrace ReadTrace(string[] args)
    {
        var trace = PlannerStore.Read<PlanningTrace>(Required(args, "--trace"));
        return Has(args, "--no-combat-sales") ? trace with { Scenario = trace.Scenario with { ForbidCombatSales = true } } : trace;
    }

    private static int RunTacticalForecast(GameContent content, string[] args, string output)
    {
        var initial = Has(args, "--first-endless-wave")
            ? EndlessLayoutContinuation.Prepare(content, PlannerStore.Read<PlanningCheckpoint>(Required(args, "--checkpoint")),
                Has(args, "--no-combat-sales"))
            : ReadSearchCheckpoint(content, args, new PlanningScenario());
        var layout = Value(args, "--blueprint") is { } layoutPath ? PlannerStore.Read<LayoutBlueprint>(layoutPath) : null;
        layout?.Validate(content);
        var policies = Value(args, "--policies") is { } policyPath
            ? PlannerStore.Read<List<TacticalPolicy>>(policyPath) : TacticalForecastSearch.DefaultPolicies();
        var options = new TacticalForecastOptions
        {
            MaximumTrials = Integer(args, "--simulations", 512), MaximumWallSeconds = Number(args, "--seconds", 300),
            BeamWidth = Integer(args, "--beam", 5), MaximumPhases = Integer(args, "--phases", 20),
            Rollout = new TacticalRolloutOptions { VerifyWaveClear = true, CommitAfterTicks = Integer(args, "--commit-ticks", 1200) }
        };
        PlannerStore.Write(Path.Combine(output, "initial.json"), initial);
        PlannerStore.Write(Path.Combine(output, "policies.json"), policies);
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler forecastCancel = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += forecastCancel;
        TacticalForecastResult result;
        try
        {
            result = TacticalForecastSearch.Search(content, initial, policies, options,
                layout is null ? null : adapter => LayoutBlueprintSpending.Apply(content, adapter, layout,
                    adapter.Session.CurrentWave + (adapter.Session.Waves.IsActive ? 0 : 1)), cancellation: stop.Token);
        }
        finally { Console.CancelKeyPress -= forecastCancel; }
        var success = result.SuccessfulTrial;
        var outcome = success?.Verification is { VerifiedWin: true } ? "VERIFIED WIN" :
            initial.Scenario.Endless && success?.Verification is { Verified: true, HorizonSurvived: true } ? "VERIFIED HORIZON SURVIVAL" :
            success is { WaveCleared: true } ? "NEXT WAVE CLEARED" : "NO SOLUTION FOUND WITHIN BUDGET";
        if (success?.Checkpoint is { } checkpoint) PlannerStore.Write(Path.Combine(output, "clear.json"), checkpoint);
        if (success?.Trace is { } trace)
        {
            var stem = success.CampaignVictory ? "witness" : "clear-trace";
            PlannerStore.Write(Path.Combine(output, stem + ".json"), trace);
            PlannerStore.Write(Path.Combine(output, stem + "-verification.json"), success.Verification);
            PlannerReport.WriteBuildOrder(Path.Combine(output, stem + ".csv"), trace);
        }
        PlannerStore.Write(Path.Combine(output, "forecast.json"), new
        {
            Outcome = outcome, initial.Scenario, initial.BuildHash, initial.ContentHash,
            FromNormalStart = initial.OriginSaveJson is null, Options = options, Blueprint = layout,
            result.Trials, result.Segments, result.StopReason, result.WallSeconds
        });
        Console.WriteLine($"{outcome}; {result.Trials.Count} full-wave forecasts; {result.StopReason}\n{Path.Combine(output, "forecast.json")}");
        return success?.Verification is { Verified: false } ? 1 : 0;
    }

    private static int RunLayoutCampaign(GameContent content, string[] args, string output)
    {
        var plan = PlannerStore.Read<LayoutCampaignPlan>(Required(args, "--plan"));
        if (Has(args, "--no-combat-sales"))
            plan = plan with { Scenario = plan.Scenario with { ForbidCombatSales = true } };
        plan.Validate(content);
        var options = new LayoutCampaignOptions
        {
            MaximumTrials = Integer(args, "--simulations", 4096), MaximumWallSeconds = Number(args, "--seconds", 600),
            BeamWidth = Integer(args, "--beam", 4), IncludeDefaultAlternatives = Has(args, "--search-alternatives"),
            DirectPlateLimits = Value(args, "--plate-limits") is { } limits
                ? limits.Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).Distinct().ToArray() : [],
            PlateLimitsFromWave = Integer(args, "--plate-from-wave", 1)
        };
        PlannerStore.Write(Path.Combine(output, "plan.json"), plan);
        var planHash = PlanningFingerprint.Hash(JsonSerializer.Serialize(plan, PlannerStore.Json));
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler campaignCancel = (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += campaignCancel;
        LayoutCampaignResult result;
        try
        {
            result = LayoutCampaignRunner.Run(content, plan, options, (row, trial) =>
            {
                if (!row.Cleared || trial.Checkpoint is null) return;
                PlannerStore.Write(Path.Combine(output, $"wave-{row.Wave:D2}.json"), trial.Checkpoint);
                Console.WriteLine($"Cleared {row.Wave}: {row.Credits} credits; {row.PolicyId}, paid Plate cap {row.DirectPlateLimit}");
            }, stop.Token);
        }
        finally { Console.CancelKeyPress -= campaignCancel; }
        foreach (var (choice, waveCheckpoint) in result.SelectedPolicies.Zip(result.SelectedWaveCheckpoints))
            PlannerStore.Write(Path.Combine(output, $"wave-{choice.Wave:D2}.json"), waveCheckpoint);
        if (result.Checkpoint is { } checkpoint) PlannerStore.Write(Path.Combine(output, "checkpoint.json"), checkpoint);
        if (result.WinningTrace is { } trace)
        {
            PlannerStore.Write(Path.Combine(output, "selected-plan.json"), SelectCampaignPlan(plan, result.SelectedPolicies));
            PlannerStore.Write(Path.Combine(output, "witness.json"), trace);
            PlannerStore.Write(Path.Combine(output, "witness-verification.json"), result.Verification);
            PlannerReport.WriteBuildOrder(Path.Combine(output, "witness.csv"), trace);
        }
        PlannerStore.Write(Path.Combine(output, "results.json"), new
        {
            result.Outcome, result.PlanId, PlanHash = planHash, plan.Scenario,
            BuildHash = PlanningFingerprint.Build(), ContentHash = PlanningFingerprint.Content(content),
            SolverVersion = "layout-campaign-v1", FromNormalStart = true,
            result.Options, result.HighestCompletedWave, result.WallSeconds, result.Trials,
            result.SelectedPolicies, result.Verification
        });
        var report = new StringBuilder("# Layout campaign calibration\n\n")
            .AppendLine($"**{result.Outcome}** — completed wave {result.HighestCompletedWave}.")
            .AppendLine().AppendLine($"Scenario: `{plan.Scenario.Key}`. Normal starting resources; authoritative 60 Hz stepping.")
            .AppendLine().AppendLine($"{result.Trials.Count} wave evaluations in {result.WallSeconds:F2} seconds. Trial budget {options.MaximumTrials}; wall budget {options.MaximumWallSeconds:F0} seconds, checked between bounded wave evaluations.")
            .AppendLine().AppendLine("The caller-supplied layout is a demonstration prior. The runner purchases it through legal commands as income arrives and evaluates the configured policy alternatives. This result is not an unseeded discovery or a human win-rate estimate.")
            .AppendLine().AppendLine($"Build: `{PlanningFingerprint.Build()}`. Content: `{PlanningFingerprint.Content(content)}`. Plan: `{planHash}`.");
        if (result.WinningTrace is { } winner)
            report.AppendLine().AppendLine($"Verified victory: {winner.FinalLives} lives, {winner.FinalCredits} credits, {winner.Actions.Count} actions, {winner.FinalTick} ticks.")
                .AppendLine().AppendLine("[Selected campaign plan](selected-plan.json) · [Winning replay](witness.json) · [Independent verification](witness-verification.json) · [Build order](witness.csv)");
        report.AppendLine().AppendLine("[Source plan](plan.json) · [Trials, budgets and selected policies](results.json) · [Latest legal checkpoint](checkpoint.json)");
        File.WriteAllText(Path.Combine(output, "report.md"), report.ToString());
        Console.WriteLine($"{result.Outcome}; {result.Trials.Count} wave evaluations\n{Path.Combine(output, "report.md")}");
        return result.Verification is { Verified: false } ? 1 : 0;
    }

    internal static LayoutCampaignPlan SelectCampaignPlan(LayoutCampaignPlan plan, IReadOnlyList<LayoutWaveSelection> choices)
    {
        var rules = new List<LayoutWavePlan>();
        LayoutWavePlan? lastSource = null;
        foreach (var choice in choices)
        {
            var source = plan.Waves.Single(rule => rule.FirstWave <= choice.Wave && choice.Wave <= rule.LastWave);
            if (source == lastSource && rules[^1].Policy == choice.Policy)
                rules[^1] = rules[^1] with { LastWave = choice.Wave };
            else
                rules.Add(source with { FirstWave = choice.Wave, LastWave = choice.Wave, Policy = choice.Policy, Alternatives = [] });
            lastSource = source;
        }
        return plan with { Waves = rules };
    }

    private sealed record AuditRow(PlanningScenario Scenario, string Outcome, int HighestCompletedWave, int Simulations,
        int Backtracks, double WallSeconds, string BuildHash, string ContentHash, bool ImportedStart, string Report);

    private static void WriteMatrix(string directory, IReadOnlyList<PlanningScenario> requested, IReadOnlyList<AuditRow> results)
    {
        var rows = results.OrderBy(result => result.Scenario.Key).ToArray();
        PlannerStore.Write(Path.Combine(directory, "matrix.json"), new { Requested = requested.Count, Completed = rows.Length, Rows = rows });
        var report = new StringBuilder("# Content coverage audit\n\n");
        report.AppendLine($"{rows.Length}/{requested.Count} requested scenario rows evaluated. {rows.Count(row => row.Outcome == "VERIFIED WIN")} have replay-verified campaign witnesses. These are search outcomes under declared budgets, not player win rates.\n");
        report.AppendLine("| Scenario | Result | Completed wave | Rollouts | Backtracks | Evidence |\n| --- | --- | ---: | ---: | ---: | --- |");
        foreach (var row in rows) report.AppendLine($"| {row.Scenario.Key} | {row.Outcome} | {row.HighestCompletedWave} | {row.Simulations} | {row.Backtracks} | [report]({row.Report}) |");
        report.AppendLine("\nSee [registry](registry.json) for valid scope and explicit exclusions. Future maps are discovered from loaded content. Cutoff survival is separate from campaign completion. No balance change is justified by unresolved search alone.");
        File.WriteAllText(Path.Combine(directory, "report.md"), report.ToString());
        File.WriteAllText(Path.Combine(directory, "matrix.csv"), "scenario,result,completed_wave,rollouts,backtracks,wall_seconds\n" + string.Join("\n", rows.Select(row =>
            $"{row.Scenario.Key},{row.Outcome},{row.HighestCompletedWave},{row.Simulations},{row.Backtracks},{row.WallSeconds.ToString("R", CultureInfo.InvariantCulture)}")));
    }
    private static string Safe(string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars().Concat(new[] { '/' }).Distinct().ToArray()));
    private static bool Match(string[] args, string flag, string actual) => Value(args, flag) is not { } requested || requested == "all" || requested == actual;
    private static bool Has(string[] args, string flag) => args.Contains(flag, StringComparer.OrdinalIgnoreCase);
    private static string Required(string[] args, string flag) => Value(args, flag) ?? throw new ArgumentException($"{flag} is required.");
    private static int Integer(string[] args, string flag, int fallback) => Value(args, flag) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
    private static double Number(string[] args, string flag, double fallback) => Value(args, flag) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;
    internal static string? Value(string[] args, string flag)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing value for {flag}.");
            if (args[index].StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase)) return args[index][(flag.Length + 1)..];
        }
        return null;
    }
}
