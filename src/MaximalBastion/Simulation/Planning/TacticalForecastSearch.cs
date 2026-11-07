using System.Diagnostics;
using System.Text.Json.Serialization;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record TacticalPolicy
{
    public string Id { get; init; } = "split-control";
    public string TargetingProfileId { get; init; } = "split";
    public int DecisionTicks { get; init; } = 15;
    public int SpendingDecisionTicks { get; init; } = 60;
    public bool SpendingBeforeTactics { get; init; } = true;
    public bool PrepareBeforeStart { get; init; }
    public bool ReactAtBoundary { get; init; } = true;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool KeepExistingOrders { get; init; }
    public int DirectPlateLimit { get; init; } = 16;
    public int ActivePlateLimit { get; init; } = 8;
    public float PlateProgressOffset { get; init; }
    public int ProtocolMinimumEnemies { get; init; } = 1;
    public float ProtocolMinimumProgress { get; init; }
    public string? PreferredProtocolBehavior { get; init; }
    public TargetMode? DamageTargetMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThreatFocusPolicy? ThreatFocus { get; init; }
    public IReadOnlyDictionary<string, TargetMode> BehaviorTargetModes { get; init; } =
        new Dictionary<string, TargetMode> { ["slow_projectile"] = TargetMode.Fastest };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 160 ||
            TargetingProfileId is not ("split" or "first" or "armored" or "support" or "strongest") ||
            DecisionTicks is < 1 or > 3600 || SpendingDecisionTicks is < 1 or > 3600 ||
            DirectPlateLimit is < 0 or > 16 || ActivePlateLimit is < 0 or > 12 ||
            !float.IsFinite(PlateProgressOffset) || PlateProgressOffset is < -.25f or > .25f ||
            ProtocolMinimumEnemies is < 1 or > 30 || !float.IsFinite(ProtocolMinimumProgress) ||
            ProtocolMinimumProgress is < 0 or > 1 || PreferredProtocolBehavior?.Length > 80 ||
            DamageTargetMode is { } mode && !Enum.IsDefined(mode) || BehaviorTargetModes is null ||
            BehaviorTargetModes.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 80 || !Enum.IsDefined(pair.Value)))
            throw new ArgumentException("Invalid tactical policy.");
        ThreatFocus?.Validate();
    }
}

public sealed record TacticalRolloutOptions
{
    public int MaximumWaveTicks { get; init; } = 18_000;
    public long? DecisionOriginTick { get; init; }
    public int CommitAfterTicks { get; init; } = 1200;
    public IReadOnlyList<float> ProgressBoundaries { get; init; } = [.42f, .58f, .72f, .85f, .93f, .98f];
    public int MinimumCommitTicks { get; init; } = 60;
    public bool VerifyWaveClear { get; init; }

    public void Validate()
    {
        if (MaximumWaveTicks is < 1 or > 216_000 || DecisionOriginTick is < 0 || CommitAfterTicks is < 1 or > 216_000 ||
            MinimumCommitTicks is < 1 or > 216_000 || ProgressBoundaries is null ||
            ProgressBoundaries.Count > 64 || ProgressBoundaries.Any(value => !float.IsFinite(value) || value is <= 0 or > 1))
            throw new ArgumentException("Invalid tactical rollout configuration.");
    }
}

public sealed record TacticalPolicySegment(long StartAfterTicks, long EndAfterTicks, TacticalPolicy Policy);

public sealed record TacticalRolloutTrial
{
    public required TacticalPolicy Policy { get; init; }
    public bool WaveCleared { get; init; }
    public bool CampaignVictory { get; init; }
    public int Wave { get; init; }
    public int Kills { get; init; }
    public int Lives { get; init; }
    public int Credits { get; init; }
    public float EscapedHealth { get; init; }
    public float Damage { get; init; }
    public float Overkill { get; init; }
    public float ArmorAbsorbed { get; init; }
    public IReadOnlyList<PlannerLeak> Leaks { get; init; } = [];
    public int EscapedEnemies { get; init; }
    public float RemainingHealth { get; init; }
    public int RemainingEnemies { get; init; }
    public int QueuedEnemies { get; init; }
    public int UnresolvedEnemies => EscapedEnemies + RemainingEnemies + QueuedEnemies;
    public double Score => -RemainingHealth - EscapedHealth * 4 - UnresolvedEnemies * 1000 - QueuedEnemies * 5000 + Credits * .1;
    public long StartTick { get; init; }
    public long FinalTick { get; init; }
    public int CombatActions { get; init; }
    public int PlatePurchases { get; init; }
    public double WallSeconds { get; init; }
    public string StopReason { get; init; } = "";
    public PlanningCheckpoint? Checkpoint { get; init; }
    public PlanningCheckpoint? BranchCheckpoint { get; init; }
    public PlanningTrace? Trace { get; init; }
    public ReplayVerification? Verification { get; init; }
}

public sealed record TacticalForecastOptions
{
    public int MaximumTrials { get; init; } = 512;
    public double MaximumWallSeconds { get; init; } = 300;
    public int MaximumPhases { get; init; } = 20;
    public int BeamWidth { get; init; } = 5;
    public TacticalRolloutOptions Rollout { get; init; } = new();

    public void Validate()
    {
        if (MaximumTrials is < 1 or > 100_000 || !double.IsFinite(MaximumWallSeconds) || MaximumWallSeconds <= 0 ||
            MaximumPhases is < 1 or > 1000 || BeamWidth is < 1 or > 64 || Rollout is null)
            throw new ArgumentException("Invalid tactical forecast budget.");
        Rollout.Validate();
    }
}

public sealed record TacticalForecastResult(IReadOnlyList<TacticalRolloutTrial> Trials,
    TacticalRolloutTrial? SuccessfulTrial, IReadOnlyList<TacticalPolicySegment> Segments,
    string StopReason, double WallSeconds);

/// <summary>Forecasts complete legal wave continuations before committing a shorter control interval.</summary>
public static class TacticalForecastSearch
{
    private sealed record Node(PlanningCheckpoint Checkpoint, TacticalRolloutTrial Forecast,
        IReadOnlyList<TacticalPolicySegment> Segments);

    public static IReadOnlyList<TacticalPolicy> DefaultPolicies()
    {
        var policies = new List<TacticalPolicy>();
        foreach (var profile in new[] { "split", "first", "armored", "support", "strongest", "weakest" })
        for (var variant = 0; variant < 4; variant++)
        {
            var modes = new Dictionary<string, TargetMode> { ["slow_projectile"] = TargetMode.Fastest };
            if (profile is "armored" or "strongest")
            {
                modes["armor_projectile"] = TargetMode.Armored;
                modes["beam"] = modes["burn_projectile"] = TargetMode.Strongest;
                if (profile == "strongest") modes["splash_projectile"] = TargetMode.Strongest;
            }
            policies.Add(new TacticalPolicy
            {
                Id = $"{profile}-protocol-{variant % 2}-plates-{(variant < 2 ? 8 : 2)}",
                TargetingProfileId = profile == "weakest" ? "first" : profile,
                DamageTargetMode = profile == "weakest" ? TargetMode.Weakest : profile == "first" ? TargetMode.First : null,
                BehaviorTargetModes = modes,
                PreferredProtocolBehavior = variant % 2 == 1 ? "slow_projectile" : null,
                ActivePlateLimit = variant < 2 ? 8 : 2,
                PlateProgressOffset = variant < 2 ? 0 : .15f
            });
        }
        return policies;
    }

    /// <summary>The spending callback must derive its actions from the supplied session, without external mutable state.</summary>
    public static TacticalRolloutTrial Evaluate(GameContent content, PlanningCheckpoint initial, TacticalPolicy policy,
        Action<PlannerSession>? spending = null, TacticalRolloutOptions? options = null,
        CancellationToken cancellation = default)
    {
        policy.Validate();
        options ??= new TacticalRolloutOptions();
        options.Validate();
        if (initial.Scenario.InformationProfile != "oracle" || initial.Scenario.ExecutionProfile != "expert")
            throw new InvalidDataException("Full-wave tactical forecasting requires the oracle/expert profile.");
        var timer = Stopwatch.StartNew();
        var adapter = PlannerSession.Restore(content, initial);
        var session = adapter.Session;
        var start = adapter.Tick;
        if (session.IsDefeat || adapter.CampaignVictory) throw new InvalidDataException("Tactical forecasting requires a live campaign state.");
        var cadenceOrigin = options.DecisionOriginTick ?? (session.Waves.IsActive
            ? initial.Actions.LastOrDefault(action => action.Accepted && action.Command.Type == GameCommandType.StartWave)?.Tick ?? start
            : start);
        if (cadenceOrigin > start) throw new ArgumentException("The decision cadence origin must precede the checkpoint.");
        var startingKills = session.Economy.TotalKills;
        var startingActions = adapter.Actions.Count;
        var wave = session.Waves.IsActive ? session.CurrentWave : session.CurrentWave + 1;
        float escapedHealth = 0;
        float dealtDamage = 0, overkill = 0, armorAbsorbed = 0;
        int escapedEnemies = 0;
        var leaks = new List<PlannerLeak>();
        session.EnemyEscaped += enemy =>
        {
            escapedHealth += enemy.Health + enemy.Shield; escapedEnemies++;
            leaks.Add(new PlannerLeak(adapter.Tick, enemy.Definition.Id, enemy.Rank.ToString(), enemy.LivesLost, enemy.Health, enemy.Shield));
        };
        session.DamageResolver.DamageApplied += damage =>
        { dealtDamage += damage.HealthDamage + damage.ShieldDamage; overkill += damage.Overkill; armorAbsorbed += damage.ArmorAbsorbed; };
        var shouldStart = !session.Waves.IsActive;
        if (shouldStart) spending?.Invoke(adapter);
        var initialProgress = session.Enemies.Select(enemy => enemy.PathProgress).DefaultIfEmpty().Max();
        var nextProgress = options.ProgressBoundaries.OrderBy(value => value).FirstOrDefault(value => value > initialProgress + .001f, 1f);
        PlanningCheckpoint? branch = null;
        var bot = policy.KeepExistingOrders ? null : new AutoPlayer(session, AutoPlayerStrategy.Experienced, initial.Scenario.Seed,
            new SimulationOptions
            {
                UseApexUpgrades = false, HoldFootprint = true,
                WavePlan = new WavePlan
                {
                    Wave = wave, DecisionSeed = initial.Scenario.Seed, TargetingProfileId = policy.TargetingProfileId,
                    Parameters = new Dictionary<string, double>
                    {
                        ["apexLimit"] = 0, ["directPlateLimit"] = policy.DirectPlateLimit,
                        ["activePlateLimit"] = policy.ActivePlateLimit, ["finalRoleFill"] = 0,
                        ["plateProgressOffset"] = policy.PlateProgressOffset,
                        ["protocolMinimumEnemies"] = policy.ProtocolMinimumEnemies
                    }
                }
            }, command => ApplyTacticalCommand(adapter, policy, command));
        if (shouldStart)
        {
            if (policy.PrepareBeforeStart) bot?.PrepareForWave(session);
            adapter.Apply(new GameCommand { PlayerId = 1, Type = GameCommandType.StartWave }, "tactical forecast start");
        }
        if (policy.ReactAtBoundary && session.Waves.IsActive && !session.IsDefeat) bot?.ReactDuringWave(session);
        while (session.Waves.IsActive && !session.IsDefeat && adapter.Tick - start < options.MaximumWaveTicks && !cancellation.IsCancellationRequested)
        {
            adapter.Advance(1);
            if (!session.Waves.IsActive || session.IsDefeat) break;
            var elapsed = adapter.Tick - start;
            var spendNow = (adapter.Tick - cadenceOrigin) % policy.SpendingDecisionTicks == 0;
            if (spendNow && policy.SpendingBeforeTactics) spending?.Invoke(adapter);
            if ((adapter.Tick - cadenceOrigin) % policy.DecisionTicks == 0) bot?.ReactDuringWave(session);
            if (spendNow && !policy.SpendingBeforeTactics) spending?.Invoke(adapter);
            if (policy.ThreatFocus is { } focus) SelectableThreatFocus.Apply(adapter, focus);
            if (branch is null && (elapsed >= options.CommitAfterTicks || elapsed >= options.MinimumCommitTicks &&
                session.Enemies.Any(enemy => enemy.PathProgress >= nextProgress)))
                branch = adapter.Capture();
        }
        var cleared = !session.IsDefeat && !session.Waves.IsActive && session.Enemies.Count == 0 &&
                      session.Waves.QueuedEnemies == 0 && session.CurrentWave == wave;
        var trace = cleared ? adapter.ExportTrace() : null;
        var verification = trace is not null && (adapter.CampaignVictory || options.VerifyWaveClear)
            ? PlanningReplay.Verify(content, trace) : null;
        return new TacticalRolloutTrial
        {
            Policy = policy, Wave = wave, WaveCleared = cleared, CampaignVictory = adapter.CampaignVictory,
            Kills = session.Economy.TotalKills - startingKills, Lives = session.Economy.Lives, Credits = session.Economy.Credits,
            EscapedHealth = escapedHealth, EscapedEnemies = escapedEnemies,
            Damage = dealtDamage, Overkill = overkill, ArmorAbsorbed = armorAbsorbed, Leaks = leaks,
            RemainingHealth = session.Enemies.Sum(enemy => enemy.Health + enemy.Shield), RemainingEnemies = session.Enemies.Count,
            QueuedEnemies = session.Waves.QueuedEnemies, StartTick = start, FinalTick = adapter.Tick,
            CombatActions = adapter.Actions.Skip(startingActions).Count(action => action.Accepted && action.DuringWave),
            PlatePurchases = session.EmergencyDirectPurchasesThisWave, WallSeconds = timer.Elapsed.TotalSeconds,
            StopReason = cleared ? "wave cleared" : session.IsDefeat ? "defeat" : cancellation.IsCancellationRequested ? "cancelled" : "wave tick budget reached",
            Checkpoint = adapter.Capture(), BranchCheckpoint = branch, Trace = trace, Verification = verification
        };
    }

    public static bool ApplyTacticalCommand(PlannerSession adapter, TacticalPolicy policy, GameCommand command)
    {
        var session = adapter.Session;
        if (command.Type is not (GameCommandType.SetTargetMode or GameCommandType.OverdriveTower or GameCommandType.DeployEmergencyDefense)) return false;
        if (command.Type == GameCommandType.SetTargetMode)
        {
            var tower = session.Towers.FirstOrDefault(tower => tower.Id == command.EntityId);
            if (tower is null) return false;
            if (policy.BehaviorTargetModes.TryGetValue(tower.Definition.Behavior, out var mode)) command = command with { TargetMode = mode };
            else if (!tower.IsSupport && policy.DamageTargetMode is { } damageMode) command = command with { TargetMode = damageMode };
            if (tower.TargetMode == command.TargetMode) return true;
        }
        if (command.Type == GameCommandType.DeployEmergencyDefense && session.EmergencyInventory <= 0 &&
            session.EmergencyDirectPurchasesThisWave >= policy.DirectPlateLimit) return false;
        if (command.Type == GameCommandType.OverdriveTower)
        {
            if (!session.Enemies.Any(enemy => !enemy.IsDead && !enemy.HasEscaped && enemy.PathProgress >= policy.ProtocolMinimumProgress)) return false;
            if (!string.IsNullOrEmpty(policy.PreferredProtocolBehavior))
            {
                var candidate = session.Towers.Where(tower => !tower.IsApex && !tower.IsOverdriven &&
                    tower.Definition.Behavior == policy.PreferredProtocolBehavior)
                    .Select(tower => new { Tower = tower, Targets = session.GetProtocolTargets(tower) })
                    .Where(choice => choice.Targets.Count > 0)
                    .OrderByDescending(choice => choice.Targets.Sum(enemy => 8 + enemy.PathProgress * 22))
                    .ThenBy(choice => choice.Tower.Id).FirstOrDefault();
                if (candidate is null) return false;
                command = command with { EntityId = candidate.Tower.Id };
            }
        }
        return adapter.Apply(command, $"tactical policy {policy.Id}").Accepted;
    }

    public static TacticalForecastResult Search(GameContent content, PlanningCheckpoint initial,
        IReadOnlyList<TacticalPolicy> policies, TacticalForecastOptions options, Action<PlannerSession>? spending = null,
        Action<TacticalRolloutTrial>? completed = null, CancellationToken cancellation = default)
    {
        options.Validate();
        if (policies is null || policies.Count == 0 || policies.Count > 4096) throw new ArgumentException("A bounded nonempty tactical policy set is required.");
        foreach (var policy in policies) policy.Validate();
        var timer = Stopwatch.StartNew();
        var rows = new List<TacticalRolloutTrial>();
        var frontier = new List<Node> { new(initial, new TacticalRolloutTrial { Policy = policies[0] }, []) };
        var stop = "frontier exhausted";
        for (var phase = 0; phase < options.MaximumPhases && frontier.Count > 0; phase++)
        {
            var next = new List<Node>();
            foreach (var parent in frontier)
            foreach (var policy in policies)
            {
                if (cancellation.IsCancellationRequested || rows.Count >= options.MaximumTrials || timer.Elapsed.TotalSeconds >= options.MaximumWallSeconds)
                {
                    stop = cancellation.IsCancellationRequested ? "cancelled" : rows.Count >= options.MaximumTrials ? "trial budget reached" : "wall-time budget reached";
                    return new(rows, null, [], stop, timer.Elapsed.TotalSeconds);
                }
                var row = Evaluate(content, parent.Checkpoint, policy, spending,
                    options.Rollout with { DecisionOriginTick = options.Rollout.DecisionOriginTick ?? initial.Tick }, cancellation);
                completed?.Invoke(row);
                rows.Add(row with { Checkpoint = null, BranchCheckpoint = null, Trace = null });
                if (row.WaveCleared)
                {
                    var segments = parent.Segments.Append(new TacticalPolicySegment(parent.Checkpoint.Tick - initial.Tick,
                        row.FinalTick - initial.Tick, policy)).ToArray();
                    return new(rows, row, segments, row.Verification is { VerifiedWin: true } ? "verified campaign win" : "wave cleared", timer.Elapsed.TotalSeconds);
                }
                if (row.BranchCheckpoint is { } checkpoint)
                    next.Add(new(checkpoint, row, parent.Segments.Append(new TacticalPolicySegment(
                        parent.Checkpoint.Tick - initial.Tick, checkpoint.Tick - initial.Tick, policy)).ToArray()));
            }
            frontier = next.OrderByDescending(node => node.Forecast.Score).Take(1)
                .Concat(next.OrderBy(node => node.Forecast.EscapedHealth).ThenByDescending(node => node.Forecast.Score).Take(1))
                .Concat(next.OrderBy(node => node.Forecast.UnresolvedEnemies).ThenByDescending(node => node.Forecast.Score).Take(1))
                .Concat(next.OrderByDescending(node => node.Forecast.Score).GroupBy(node => node.Forecast.Policy.TargetingProfileId).Select(group => group.First()))
                .Concat(next.OrderByDescending(node => node.Forecast.Score))
                .DistinctBy(node => node.Checkpoint.StateSignature).Take(options.BeamWidth).ToList();
            stop = frontier.Count == 0 ? "frontier exhausted" : "phase budget reached";
        }
        return new(rows, null, [], stop, timer.Elapsed.TotalSeconds);
    }
}
