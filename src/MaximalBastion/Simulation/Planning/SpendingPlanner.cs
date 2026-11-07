using System.Diagnostics;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

/// <summary>Persistent wave frontier with explicit spending alternatives and bounded combat branches.</summary>
public static class SpendingPlanner
{
    public static PlannerSearchState Run(GameContent content, PlanningScenario scenario, PlannerBudget budget,
        PlannerRestrictions restrictions, string directory, PlanningCheckpoint? initial = null,
        PlannerSearchState? resume = null, CancellationToken cancellation = default)
    {
        budget.Validate();
        Directory.CreateDirectory(directory);
        var clock = Stopwatch.StartNew();
        var state = resume ?? CreateState(content, scenario, budget, restrictions, initial);
        ValidateResume(content, state, scenario, restrictions);
        if (resume is not null && (state.Budget.DecisionTicks != budget.DecisionTicks ||
            state.Budget.MaximumWaveTicks != budget.MaximumWaveTicks || state.Budget.WithinWaveBranches != budget.WithinWaveBranches))
            throw new InvalidDataException("Resume requires unchanged decision ticks, maximum wave ticks and combat repair breadth; use a separate search for a different evaluation model.");
        if (state.PendingRepairs.Any(task => task.Candidate < 0 || task.ProposalOrdinal < 0 ||
            state.Nodes.All(node => node.Id != task.ParentId)))
            throw new InvalidDataException("Invalid pending combat repair task.");
        state.Budget = budget;
        var previousSeconds = state.WallSeconds;
        var priorTrials = state.Trials.Count;
        var priorTicks = state.SimulatedTicks;
        var priorInterruptedTrials = state.InterruptedTrials;
        var priorInterruptedTicks = state.InterruptedSimulatedTicks;
        var invocation = new PlannerInvocationBudget { Budget = budget };
        state.BudgetHistory.Add(invocation);
        var fixedPolicy = restrictions.Baseline != "planner";
        var batchBreadth = fixedPolicy ? 1 : budget.InitialBreadth;
        state.Breadth = Math.Min(budget.MaximumBreadth, Math.Max(state.Breadth, batchBreadth));
        var stop = false;

        void UpdateInvocation()
        {
            invocation.WallSeconds = clock.Elapsed.TotalSeconds;
            invocation.CompletedTrials = state.Trials.Count - priorTrials;
            invocation.SimulatedTicks = state.SimulatedTicks - priorTicks;
            invocation.InterruptedTrials = state.InterruptedTrials - priorInterruptedTrials;
            invocation.InterruptedSimulatedTicks = state.InterruptedSimulatedTicks - priorInterruptedTicks;
            state.WallSeconds = previousSeconds + invocation.WallSeconds;
        }

        bool Interrupted(WaveEvaluation result)
        {
            if (!cancellation.IsCancellationRequested || result.Trial.Cleared ||
                result.Session.Session.IsDefeat || result.Trial.Ticks >= budget.MaximumWaveTicks) return false;
            state.InterruptedTrials++;
            state.InterruptedSimulatedTicks += result.Trial.Ticks;
            return true;
        }

        bool PersistProgress()
        {
            UpdatePreferred(state, budget.BeamWidth);
            UpdateInvocation();
            PlannerStore.Write(Path.Combine(directory, "frontier.json"), state);
            if (GC.GetTotalMemory(false) <= budget.MemoryMegabytes * 1024L * 1024 ||
                GC.GetTotalMemory(true) <= budget.MemoryMegabytes * 1024L * 1024) return false;
            state.StopReason = "memory budget reached; frontier saved";
            return true;
        }

        // Persist the invocation's evaluation settings before consuming any frontier work.
        PlannerStore.Write(Path.Combine(directory, "frontier.json"), state);
        while (!stop && state.Trials.Count - priorTrials < budget.Simulations && clock.Elapsed.TotalSeconds < budget.WallSeconds &&
               !cancellation.IsCancellationRequested)
        {
            if (state.PendingRepairs.Count > 0)
            {
                var repair = state.PendingRepairs[0];
                var repairParent = state.Nodes.Single(node => node.Id == repair.ParentId);
                var repaired = Evaluate(content, repair.Checkpoint, budget, restrictions, repair.ProposalOrdinal,
                    cancellation, combatRepair: true);
                if (Interrupted(repaired)) break;
                repaired.Trial.CombatRepair = true;
                Record(content, state, repairParent, repair.Candidate, repaired, directory, repair.BacktrackDepth);
                state.PendingRepairs.RemoveAt(0);
                if (PersistProgress()) break;
                continue;
            }
            var candidates = Eligible(state, budget, restrictions, fixedPolicy).ToArray();
            if (candidates.Length == 0)
            {
                if (fixedPolicy || state.Breadth >= budget.MaximumBreadth)
                { state.StopReason = "restricted frontier exhausted; no impossibility claim"; break; }
                state.Breadth = Math.Min(budget.MaximumBreadth, Math.Max(state.Breadth + batchBreadth, state.Breadth * 2));
                continue;
            }
            var parent = candidates.OrderByDescending(node => node.Wave)
                .ThenBy(node => node.NextCandidate).ThenByDescending(node => node.Preferred)
                .ThenByDescending(node => node.Lives).ThenByDescending(node => node.Credits).ThenBy(node => node.Id).First();
            var depth = Math.Max(0, state.HighestCompletedWave - parent.Wave);
            if (depth > 0) state.Backtracks++;
            var end = Math.Min(state.Breadth, parent.NextCandidate + batchBreadth);
            while (parent.NextCandidate < end)
            {
                if (state.Trials.Count - priorTrials >= budget.Simulations || clock.Elapsed.TotalSeconds >= budget.WallSeconds ||
                    cancellation.IsCancellationRequested) break;
                var result = Evaluate(content, parent.Checkpoint, budget, restrictions, parent.NextCandidate, cancellation);
                if (Interrupted(result)) { stop = true; break; }
                var candidate = parent.NextCandidate;
                Record(content, state, parent, candidate, result, directory, depth);
                parent.NextCandidate++;
                if (result.Session.CampaignVictory || result.Session.HorizonSurvived)
                {
                    // Keep searching after a witness when resources permit, retaining alternative compositions.
                    if (fixedPolicy) { stop = true; break; }
                }
                if (!fixedPolicy && !result.Trial.Cleared && restrictions.CombatPurchasing && budget.WithinWaveBranches > 0)
                {
                    foreach (var checkpoint in result.Decisions.TakeLast(budget.WithinWaveBranches))
                        state.PendingRepairs.Add(new PlannerRepairTask { ParentId = parent.Id, Candidate = candidate,
                            ProposalOrdinal = candidate + 1, BacktrackDepth = depth, Checkpoint = checkpoint });
                    if (state.PendingRepairs.Count > 0) break;
                }
            }
            if (PersistProgress()) break;
        }
        UpdateInvocation();
        if (cancellation.IsCancellationRequested) state.StopReason = "cancelled; frontier saved";
        else if (state.Trials.Count - priorTrials >= budget.Simulations) state.StopReason = "simulation budget reached";
        else if (clock.Elapsed.TotalSeconds >= budget.WallSeconds) state.StopReason = "wall-time budget reached";
        else if (stop) state.StopReason = "single-attempt policy finished";
        invocation.StopReason = state.StopReason;
        PlannerStore.Write(Path.Combine(directory, "frontier.json"), state);
        PlannerReport.Write(directory, state);
        return state;
    }

    private static IEnumerable<PlannerNode> Eligible(PlannerSearchState state, PlannerBudget budget,
        PlannerRestrictions restrictions, bool fixedPolicy)
    {
        foreach (var node in state.Nodes)
        {
            if (node.Wave >= state.Scenario.Horizon || node.NextCandidate >= state.Breadth) continue;
            if ((!restrictions.Backtracking || fixedPolicy) && node.Wave < state.HighestCompletedWave) continue;
            if (state.HighestCompletedWave - node.Wave > budget.BacktrackDepth) continue;
            // Archived boards remain available once preferred boards have consumed the current tranche.
            if (!node.Preferred && state.Nodes.Any(other => other.Wave == node.Wave && other.Preferred &&
                    other.NextCandidate < state.Breadth)) continue;
            yield return node;
        }
    }

    private static PlannerSearchState CreateState(GameContent content, PlanningScenario scenario, PlannerBudget budget,
        PlannerRestrictions restrictions, PlanningCheckpoint? initial)
    {
        if (initial is not null && initial.Scenario != scenario)
        {
            if (!scenario.ForbidCombatSales || (initial.Scenario with { ForbidCombatSales = true }) != scenario)
                throw new InvalidDataException("Initial checkpoint differs from the requested search scenario.");
            initial = PlannerSession.ForbidCombatSales(content, initial);
        }
        var session = initial is null ? PlannerSession.Create(content, scenario) : PlannerSession.Restore(content, initial);
        var state = new PlannerSearchState
        {
            Scenario = scenario, Budget = budget, Restrictions = restrictions,
            BuildHash = PlanningFingerprint.Build(), ContentHash = PlanningFingerprint.Content(content),
            ImportedStart = session.Capture().OriginSaveJson is not null,
            HighestCompletedWave = session.Session.Waves.IsActive ? session.Session.CurrentWave - 1 : session.Session.CurrentWave
        };
        state.Nodes.Add(MakeNode(session, 0, null, "initial"));
        return state;
    }

    private static void ValidateResume(GameContent content, PlannerSearchState state, PlanningScenario scenario, PlannerRestrictions restrictions)
    {
        if (state.SolverVersion != PlannerSearchState.Version || state.BuildHash != PlanningFingerprint.Build() ||
            state.ContentHash != PlanningFingerprint.Content(content) || state.Scenario != scenario || state.Restrictions != restrictions)
            throw new InvalidDataException("Search version, build, content, scenario or restrictions changed; start a separate search.");
        if (state.Nodes.Count == 0 || state.Nodes.Any(node => node.NextCandidate < 0 || node.Checkpoint.Scenario != scenario) ||
            state.PendingRepairs.Any(repair => repair.Checkpoint.Scenario != scenario) ||
            state.Nodes.Select(node => node.Id).Distinct().Count() != state.Nodes.Count)
            throw new InvalidDataException("Invalid persistent search frontier.");
    }

    private static PlannerNode MakeNode(PlannerSession adapter, int id, int? parentId, string family) => new()
    {
        Id = id, ParentId = parentId, Wave = adapter.Session.Waves.IsActive ? adapter.Session.CurrentWave - 1 : adapter.Session.CurrentWave,
        Checkpoint = adapter.Capture(), Family = family, Lives = adapter.Session.Economy.Lives,
        Credits = adapter.Session.Economy.Credits, Investment = adapter.Session.Towers.Sum(tower => tower.InvestedCredits),
        Composition = Composition(adapter.Session)
    };

    private static string Composition(GameSession session) => string.Join(";", session.Towers.GroupBy(tower =>
        $"{tower.Definition.Id}:{tower.DoctrineId}:{tower.SpecializationId}:{tower.IsApex}").OrderBy(group => group.Key)
        .Select(group => $"{group.Key}={group.Count()}"));

    private static void UpdatePreferred(PlannerSearchState state, int beamWidth)
    {
        foreach (var wave in state.Nodes.GroupBy(node => node.Wave))
        {
            foreach (var node in wave) node.Preferred = false;
            // Preserve money, survival and composition; this is approximate scheduling, not dominance pruning.
            var selected = wave.OrderByDescending(node => node.Credits).Take(1)
                .Concat(wave.OrderByDescending(node => node.Lives).ThenByDescending(node => node.Investment).Take(1))
                .Concat(wave.GroupBy(node => node.Composition).Select(group => group.OrderByDescending(node => node.Lives)
                    .ThenByDescending(node => node.Credits).First()).OrderByDescending(node => node.Lives))
                .DistinctBy(node => node.Id).Take(beamWidth);
            foreach (var node in selected) node.Preferred = true;
        }
    }

    private static void Record(GameContent content, PlannerSearchState state, PlannerNode parent, int candidate,
        WaveEvaluation result, string directory, int depth)
    {
        result.Trial.Index = state.Trials.Count;
        result.Trial.ParentId = parent.Id;
        result.Trial.Candidate = candidate;
        result.Trial.BacktrackDepth = depth;
        state.Trials.Add(result.Trial);
        state.SimulatedTicks += result.Trial.Ticks;
        if (result.Trial.Cleared && !result.Session.CampaignVictory && !result.Session.HorizonSurvived)
        {
            var timer = Stopwatch.StartNew();
            var node = MakeNode(result.Session, state.Nodes.Count, parent.Id, result.Trial.Family);
            state.CheckpointMilliseconds += timer.Elapsed.TotalMilliseconds;
            // Only identical complete snapshots are merged. Similar DPS or reserves are not an equivalence.
            if (!state.Nodes.Any(existing => existing.Checkpoint.StateSignature == node.Checkpoint.StateSignature)) state.Nodes.Add(node);
            state.HighestCompletedWave = Math.Max(state.HighestCompletedWave, node.Wave);
        }
        if (result.Session.CampaignVictory || result.Session.HorizonSurvived)
        {
            var trace = result.Session.ExportTrace() with { SearchBudget = state.Budget, SearchRestrictions = state.Restrictions };
            var verification = PlanningReplay.Verify(content, trace);
            if (!verification.Verified)
            {
                state.VerificationFailure = verification.Reason;
                PlannerStore.Write(Path.Combine(directory, $"replay-failure-{state.Trials.Count}.json"), trace);
                return;
            }
            var name = $"witness-{state.Trials.Count:D6}.json";
            PlannerStore.Write(Path.Combine(directory, name), trace);
            PlannerStore.Write(Path.Combine(directory, $"witness-{state.Trials.Count:D6}-verification.json"), verification);
            state.WitnessFiles.Add(name);
            var cluster = Composition(result.Session.Session) + "|regions:" + string.Join(";", result.Session.Session.Towers
                .GroupBy(tower => $"{(int)(tower.Position.X / 80)},{(int)(tower.Position.Y / 80)}").OrderBy(group => group.Key)
                .Select(group => $"{group.Key}:{group.Count()}")) +
                $"|combat:{trace.Actions.Count(action => action.DuringWave) / 20}|plates:{trace.Actions.Count(action => action.Command.Type == GameCommandType.DeployEmergencyDefense) / 3}";
            state.StrategyClusters[cluster] = state.StrategyClusters.GetValueOrDefault(cluster) + 1;
            state.HighestCompletedWave = Math.Max(state.HighestCompletedWave, result.Session.Session.CurrentWave);
            state.Outcome = verification.VerifiedWin ? "VERIFIED WIN" : "VERIFIED HORIZON SURVIVAL";
            PlannerReport.WriteBuildOrder(Path.Combine(directory, $"witness-{state.Trials.Count:D6}.csv"), trace);
        }
        if (!result.Trial.Cleared && (state.Trials.Count == 1 || result.Trial.Wave >= state.HighestCompletedWave))
            PlannerStore.Write(Path.Combine(directory, "latest-failure.json"), new { result.Trial,
                Trace = result.Session.ExportTrace() with { SearchBudget = state.Budget, SearchRestrictions = state.Restrictions } });
    }

    private sealed record WaveEvaluation(PlannerSession Session, PlannerTrial Trial, List<PlanningCheckpoint> Decisions);

    private static WaveEvaluation Evaluate(GameContent content, PlanningCheckpoint checkpoint, PlannerBudget budget,
        PlannerRestrictions restrictions, int ordinal, CancellationToken cancellation, bool combatRepair = false)
    {
        var timer = Stopwatch.StartNew();
        var adapter = PlannerSession.Restore(content, checkpoint);
        var session = adapter.Session;
        var startTick = adapter.Tick;
        var startActions = adapter.Actions.Count;
        var profile = PlanningExecutionProfile.Resolve(adapter.Scenario.ExecutionProfile);
        var interval = Math.Max(budget.DecisionTicks, Math.Max(profile.MinimumActionIntervalTicks, restrictions.MinimumActionTicks));
        var reaction = Math.Max(profile.ReactionDelayTicks, restrictions.ReactionTicks);
        var allowedCombat = restrictions.CombatPurchasing && profile.CombatPurchases;
        var wave = session.Waves.IsActive ? session.CurrentWave : session.CurrentWave + 1;
        var fixedPolicy = restrictions.Baseline != "planner";
        var usePrior = !combatRepair && reaction == 0 && adapter.Scenario.InformationProfile == "oracle" &&
            restrictions.Family == "unrestricted" && restrictions.ExcludedTowerId is null && restrictions.PlacementHeuristics && restrictions.SupportReasoning && restrictions.Saving &&
            restrictions.Baseline != "greedy" && (fixedPolicy || ordinal % 3 != 2);
        var trial = new PlannerTrial { Wave = wave, RestorationMethod = adapter.RestorationMethod };
        var decisions = new List<PlanningCheckpoint>();
        var checkpointInterval = Math.Max(interval, 120);
        var nextCheckpoint = startTick + 121;
        session.EnemyEscaped += enemy => trial.Leaks.Add(new(adapter.Tick, enemy.Definition.Id, enemy.Rank.ToString(),
            enemy.LivesLost, enemy.Health, enemy.Shield));
        session.DamageResolver.DamageApplied += damage =>
        { trial.Damage += damage.HealthDamage + damage.ShieldDamage; trial.Overkill += damage.Overkill; trial.ArmorAbsorbed += damage.ArmorAbsorbed; };
        var existingCombat = adapter.Actions.Where(action => action.Accepted && action.DuringWave && action.Wave == wave).ToArray();
        var combatActions = existingCombat.Length;
        long lastAction = existingCombat.Length == 0 ? long.MinValue / 2 : existingCombat.Max(action => action.Tick);
        bool Apply(GameCommand command, string trigger, long? requestedTick = null)
        {
            if (command.Type == GameCommandType.SetTargetMode && session.Towers.FirstOrDefault(tower => tower.Id == command.EntityId)?.TargetMode == command.TargetMode)
                return true;
            if (session.Waves.IsActive && command.Type != GameCommandType.StartWave)
            {
                if (combatActions >= profile.MaximumCombatActionsPerWave || adapter.Tick - lastAction < Math.Max(profile.MinimumActionIntervalTicks, restrictions.MinimumActionTicks)) return false;
                if (!allowedCombat && command.Type is GameCommandType.PlaceTower or GameCommandType.UpgradeTower or GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower or GameCommandType.PlaceGenerator or GameCommandType.UpgradeGenerator) return false;
                if (!profile.TacticalActions && command.Type is GameCommandType.DeployEmergencyDefense or GameCommandType.OverdriveTower) return false;
            }
            var record = adapter.Apply(command, trigger, requestedTick);
            if (record.Accepted && session.Waves.IsActive && command.Type != GameCommandType.StartWave)
            { combatActions++; lastAction = adapter.Tick; }
            return record.Accepted;
        }

        AutoPlayer? prior = null;
        if (usePrior)
        {
            var strategy = restrictions.Baseline == "random" ? AutoPlayerStrategy.Randomized : AutoPlayerStrategy.Experienced;
            WavePlan? plan = null;
            if (ordinal > 0 && restrictions.Baseline == "planner")
                plan = CampaignWavePlanGenerator.Generate(wave, new CampaignSearchOptions { BaseSeed = adapter.Scenario.Seed },
                    ordinal / 128, 128)[ordinal % 128];
            trial.Family = plan is null ? strategy.ToString() : $"prior:{plan.EconomyProfileId}/{plan.PlacementProfileId}/{plan.TacticalProfileId}";
            prior = new AutoPlayer(session, strategy, plan?.DecisionSeed ?? adapter.Scenario.Seed,
                new SimulationOptions { Seed = adapter.Scenario.Seed, WavePlan = plan, StepSeconds = DeterministicSessionRunner.FixedStepSeconds },
                command => Apply(command, session.Waves.IsActive ? "periodic/observed income" : "pre-wave bundle"));
        }
        else trial.Family = $"spending:{restrictions.Family}:{ordinal}";

        SpendingCandidate? pending = null;
        var pendingIndex = 0;
        long? pendingReadyTick = null;
        long pendingProposalTick = adapter.Tick;
        bool ExecutePending()
        {
            if (pending is null) return true;
            if (pendingIndex == 0 && session.Economy.Credits < pending.WaitForCredits) return false;
            if (session.Waves.IsActive && reaction > 0)
            {
                if (pendingReadyTick is null)
                { pendingProposalTick = adapter.Tick; pendingReadyTick = adapter.Tick + reaction; }
                if (adapter.Tick < pendingReadyTick) return false;
            }
            while (pendingIndex < pending.Commands.Count)
            {
                if (!Apply(pending.Commands[pendingIndex], pending.WaitForCredits is not null ?
                        "credits reached planned price" : "ordered purchase bundle", pendingProposalTick)) return false;
                pendingIndex++;
            }
            pending = null;
            pendingIndex = 0;
            pendingReadyTick = null;
            return true;
        }
        void Purchase(bool combat, bool first)
        {
            if (prior is not null)
            {
                if (combat) prior.ReactDuringWave(session); else prior.PrepareForWave(session);
                return;
            }
            if (combat && !allowedCombat && !combatRepair) return;
            if (pending is not null)
            {
                ExecutePending();
                return;
            }
            for (var purchase = 0; purchase < (combat ? 1 : 32); purchase++)
            {
                var choices = SpendingCandidateGenerator.Generate(session, new CandidateGenerationOptions
                {
                    MaximumCandidates = 50, PositionsPerTower = ordinal < 10 ? 2 : 4,
                    SpatialStep = ordinal < 10 ? 40 : 24, RefinementStep = ordinal < 50 ? 12 : 6,
                    AllowCombatPurchases = allowedCombat, AllowSaving = restrictions.Saving,
                    AllowSupport = restrictions.SupportReasoning, UsePlacementHeuristics = restrictions.PlacementHeuristics,
                    Family = restrictions.Family, InformationProfile = adapter.Scenario.InformationProfile,
                    IncludeTactical = profile.TacticalActions,
                    IncludeSales = !fixedPolicy && (!session.Waves.IsActive || !adapter.Scenario.ForbidCombatSales),
                    ExplorationOffset = ordinal,
                    ExcludedTowerId = restrictions.ExcludedTowerId
                });
                if (choices.Count == 0) return;
                var choice = first && purchase == 0 && !fixedPolicy ? choices[ordinal % choices.Count] :
                    choices.Where(candidate => candidate.Commands.Count > 0 && candidate.Cost >= 0 && candidate.Score > 0 &&
                        (!fixedPolicy || candidate.WaitForCredits is null || candidate.WaitForCredits <= session.Economy.Credits) &&
                        (candidate.Cost > 0 || combat && candidate.Commands.Any(command => command.Type is GameCommandType.DeployEmergencyDefense or GameCommandType.OverdriveTower)))
                        .OrderByDescending(candidate => candidate.Score).FirstOrDefault();
                if (choice is null) return;
                if (choice.WaitForCredits > session.Economy.Credits)
                { pending = choice; pendingIndex = 0; pendingReadyTick = null; pendingProposalTick = adapter.Tick; return; }
                if (choice.Commands.Count == 0) return;
                pending = choice;
                pendingIndex = 0;
                pendingReadyTick = null;
                pendingProposalTick = adapter.Tick;
                if (!ExecutePending()) return;
            }
        }

        if (!session.Waves.IsActive)
        {
            if (session.IsVictory && adapter.Scenario.Endless)
                Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.ContinueEndless }, "campaign cleared; evaluate Endless horizon");
            Purchase(false, true);
            Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave }, "preparation complete");
        }
        else if (combatRepair) Purchase(true, true);
        var lastCredits = session.Economy.Credits;
        var nextPeriodic = adapter.Tick + interval + reaction;
        long? eventDue = null;
        var lastDecision = adapter.Tick;
        while (session.Waves.IsActive && !session.IsDefeat && adapter.Tick - startTick < budget.MaximumWaveTicks &&
               !cancellation.IsCancellationRequested)
        {
            if (adapter.Tick % 60 == 0)
            {
                trial.SuppressedTowerSeconds += session.Towers.Count(tower => tower.IsSuppressed || tower.IsDisrupted);
                trial.EstimatedRegeneration += session.Enemies.Where(enemy => enemy.DamagePauseTimer <= 0 && enemy.Health < enemy.MaxHealth)
                    .Sum(enemy => MathF.Min(enemy.Definition.RegenerationPerSecond, enemy.MaxHealth - enemy.Health));
            }
            adapter.Advance(1);
            if (pending is not null && pendingReadyTick is { } ready && adapter.Tick >= ready &&
                session.Waves.IsActive && !session.IsDefeat) ExecutePending();
            if (session.Economy.Credits > lastCredits && eventDue is null) eventDue = adapter.Tick + reaction;
            lastCredits = session.Economy.Credits;
            if (adapter.Tick < nextPeriodic && (eventDue is null || adapter.Tick < eventDue) || adapter.Tick - lastDecision < interval) continue;
            if (!session.Waves.IsActive || session.IsDefeat) break;
            if (!combatRepair && !fixedPolicy && budget.WithinWaveBranches > 0 && adapter.Tick >= nextCheckpoint)
            {
                // Retain the whole trajectory at a bounded, progressively coarser cadence.
                decisions.Add(adapter.Capture());
                if (decisions.Count >= Math.Max(32, budget.WithinWaveBranches * 2))
                {
                    var retained = decisions.Where((_, index) => index % 2 == 0).ToArray();
                    decisions.Clear();
                    decisions.AddRange(retained);
                    checkpointInterval *= 2;
                }
                nextCheckpoint = adapter.Tick + checkpointInterval;
            }
            Purchase(true, false);
            lastDecision = adapter.Tick;
            nextPeriodic = adapter.Tick + interval;
            eventDue = null;
        }
        trial.Cleared = !session.IsDefeat && !session.Waves.IsActive && session.Enemies.Count == 0 && session.CurrentWave == wave;
        trial.Victory = adapter.CampaignVictory;
        trial.Lives = session.Economy.Lives;
        trial.Credits = session.Economy.Credits;
        trial.Ticks = adapter.Tick - startTick;
        trial.WallSeconds = timer.Elapsed.TotalSeconds;
        trial.AcceptedActions = adapter.Actions.Skip(startActions).Count(action => action.Accepted);
        trial.InvalidActions = adapter.Actions.Skip(startActions).Count(action => !action.Accepted);
        trial.CombatActions = adapter.Actions.Skip(startActions).Count(action => action.Accepted && action.DuringWave);
        trial.PlatePurchases = session.EmergencyDirectPurchasesThisWave;
        trial.IdleSupports = session.Towers.Count(tower => tower.IsSupport && !session.Towers.Any(other => !other.IsSupport &&
            Microsoft.Xna.Framework.Vector2.Distance(tower.Position, other.Position) <= session.GetEffectiveAuraRange(tower)));
        trial.MissedPurchaseThresholds = pending is null ? 0 : 1;
        trial.Signature = adapter.Signature;
        trial.RemainingEnemies = session.Enemies.GroupBy(enemy => enemy.Definition.Id).Select(group =>
            new PlannerEnemyRemainder(group.Key, group.Count(), group.Sum(enemy => enemy.Health), group.Sum(enemy => enemy.Shield), group.Max(enemy => enemy.PathProgress))).ToList();
        var repairs = decisions.Count == 0 ? [] : Enumerable.Range(1, budget.WithinWaveBranches)
            .Select(index => startTick + trial.Ticks * index / (budget.WithinWaveBranches + 1))
            .Select(target => decisions.MinBy(checkpoint => Math.Abs(checkpoint.Tick - target))!)
            .DistinctBy(checkpoint => checkpoint.Tick).OrderBy(checkpoint => checkpoint.Tick).ToList();
        return new(adapter, trial, repairs);
    }
}
