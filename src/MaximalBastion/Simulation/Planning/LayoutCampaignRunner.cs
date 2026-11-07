using System.Diagnostics;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record LayoutWavePlan
{
    public int FirstWave { get; init; } = 1;
    public int LastWave { get; init; } = 1;
    public bool SpendOnLayout { get; init; } = true;
    public bool ApplyScheduledApex { get; init; } = true;
    public LayoutAllocationOptions Allocation { get; init; } = new();
    public CoverageSalvageOptions Salvage { get; init; } = new();
    public TacticalPolicy Policy { get; init; } = new();
    public IReadOnlyList<TacticalPolicy> Alternatives { get; init; } = [];
}

public sealed record LayoutCampaignPlan
{
    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "layout-campaign";
    public PlanningScenario Scenario { get; init; } = new();
    public LayoutBlueprint Layout { get; init; } = new();
    public IReadOnlyList<LayoutWavePlan> Waves { get; init; } = [];

    public void Validate(GameContent content)
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(Id) || Id.Length > 160 || Scenario is null ||
            Layout is null || Waves is null || Waves.Count == 0 || Scenario.StartingProfile != "fresh" || Scenario.Endless)
            throw new InvalidDataException("A layout campaign requires a finite normal-start scenario and wave policies.");
        if (Scenario.Horizon is < 1 or > 1000 || Scenario.Horizon > content.Waves.Waves.Select(wave => wave.Number).DefaultIfEmpty().Max() ||
            Scenario.InformationProfile != "oracle" || Scenario.ExecutionProfile != "expert")
            throw new InvalidDataException("Layout campaign forecasting requires a bounded oracle/expert scenario.");
        Layout.Validate(content);
        if (Layout.MapId is not null && Layout.MapId != Scenario.MapId)
            throw new InvalidDataException("Layout blueprint belongs to a different campaign map.");
        foreach (var rule in Waves)
        {
            if (rule is null || rule.FirstWave < 1 || rule.LastWave < rule.FirstWave || rule.LastWave > Scenario.Horizon ||
                rule.Policy is null || rule.Alternatives is null || rule.Allocation is null || rule.Salvage is null ||
                rule.Alternatives.Count > 256 || rule.Alternatives.Any(policy => policy is null))
                throw new InvalidDataException("Invalid layout campaign wave policy.");
            rule.Policy.Validate(); rule.Salvage.Validate(); rule.Allocation.Validate();
            foreach (var alternative in rule.Alternatives) alternative.Validate();
        }
        for (var wave = 1; wave <= Scenario.Horizon; wave++)
            if (Waves.Count(rule => wave >= rule.FirstWave && wave <= rule.LastWave) != 1)
                throw new InvalidDataException("Each campaign wave requires exactly one policy range.");
    }
}

public sealed record LayoutCampaignOptions
{
    public int MaximumTrials { get; init; } = 4096;
    public double MaximumWallSeconds { get; init; } = 600;
    public int MaximumWaveTicks { get; init; } = 18_000;
    public int BeamWidth { get; init; } = 4;
    public bool IncludeDefaultAlternatives { get; init; }
    public IReadOnlyList<int> DirectPlateLimits { get; init; } = [];
    public int PlateLimitsFromWave { get; init; } = 1;
}

public sealed record LayoutCampaignTrial(int Number, int Wave, int Parent, string PolicyId, bool Cleared,
    int Kills, int Lives, int Credits, int TowerValue, float EscapedHealth, float RemainingHealth, long Ticks,
    string StopReason, int DirectPlateLimit);
public sealed record LayoutWaveSelection(int Wave, TacticalPolicy Policy);
public sealed record LayoutCampaignResult(string PlanId, LayoutCampaignOptions Options, string Outcome,
    int HighestCompletedWave, double WallSeconds, IReadOnlyList<LayoutCampaignTrial> Trials,
    IReadOnlyList<LayoutWaveSelection> SelectedPolicies, PlanningCheckpoint? Checkpoint,
    PlanningTrace? WinningTrace, ReplayVerification? Verification)
{
    public IReadOnlyList<PlanningCheckpoint> SelectedWaveCheckpoints { get; init; } = [];
}

/// <summary>Builds a caller-supplied layout from normal starting resources and searches bounded wave-policy alternatives.</summary>
public static class LayoutCampaignRunner
{
    private sealed record Node(PlanningCheckpoint Checkpoint, Node? Parent, LayoutWaveSelection? Selection, double Value);

    public static LayoutCampaignResult Run(GameContent content, LayoutCampaignPlan plan, LayoutCampaignOptions? options = null,
        Action<LayoutCampaignTrial, TacticalRolloutTrial>? completed = null, CancellationToken cancellation = default)
    {
        options ??= new(); plan.Validate(content);
        if (options.MaximumTrials is < 1 or > 100_000 || !double.IsFinite(options.MaximumWallSeconds) ||
            options.MaximumWallSeconds <= 0 || options.MaximumWaveTicks is < 1 or > 216_000 || options.BeamWidth is < 1 or > 64 ||
            options.PlateLimitsFromWave < 1 || options.PlateLimitsFromWave > plan.Scenario.Horizon ||
            options.DirectPlateLimits is null || options.DirectPlateLimits.Count > 17 || options.DirectPlateLimits.Any(limit => limit is < 0 or > 16))
            throw new ArgumentException("Invalid layout campaign search budget.");
        var clock = Stopwatch.StartNew();
        var start = PlannerSession.Create(content, plan.Scenario);
        var initial = start.Capture();
        var frontier = new List<Node> { new(initial, null, null, 0) };
        var rows = new List<LayoutCampaignTrial>();
        Node best = frontier[0];
        string? stopped = null;
        LayoutCampaignResult Result(Node terminal, string outcome, PlanningTrace? trace = null, ReplayVerification? verification = null)
        {
            var lineage = new Stack<Node>();
            for (var node = terminal; node.Parent is not null; node = node.Parent) lineage.Push(node);
            return new(plan.Id, options, outcome, lineage.Count, clock.Elapsed.TotalSeconds, rows,
                lineage.Select(node => node.Selection!).ToArray(), terminal.Checkpoint, trace, verification)
            { SelectedWaveCheckpoints = lineage.Select(node => node.Checkpoint).ToArray() };
        }
        for (var wave = 1; wave <= plan.Scenario.Horizon; wave++)
        {
            var rule = plan.Waves.Single(rule => wave >= rule.FirstWave && wave <= rule.LastWave);
            var policies = new[] { rule.Policy }.Concat(rule.Alternatives)
                .Concat(options.IncludeDefaultAlternatives ? TacticalForecastSearch.DefaultPolicies() : [])
                .Distinct();
            var caps = start.Session.TacticalSystemsEnabled && wave >= options.PlateLimitsFromWave ? options.DirectPlateLimits : [];
            var candidates = policies.SelectMany(policy => policy.KeepExistingOrders ? [policy] :
                caps.Prepend(policy.DirectPlateLimit).Distinct()
                    .Select(limit => limit == policy.DirectPlateLimit ? policy : policy with { DirectPlateLimit = limit })).Distinct().ToArray();
            var next = new List<Node>();
            for (var parent = 0; parent < frontier.Count; parent++)
            foreach (var policy in candidates)
            {
                if (cancellation.IsCancellationRequested || rows.Count >= options.MaximumTrials || clock.Elapsed.TotalSeconds >= options.MaximumWallSeconds)
                { stopped = cancellation.IsCancellationRequested ? "cancelled" : rows.Count >= options.MaximumTrials ? "trial budget reached" : "wall-time budget reached"; break; }
                var node = frontier[parent];
                var spending = CreateSpending(content, node.Checkpoint, plan.Layout, rule, wave);
                var trial = TacticalForecastSearch.Evaluate(content, node.Checkpoint, policy, spending,
                    new() { MaximumWaveTicks = options.MaximumWaveTicks }, cancellation);
                var value = trial.Checkpoint is null ? 0 : PlannerSession.Restore(content, trial.Checkpoint).Session.Towers.Sum(tower => tower.InvestedCredits);
                var row = new LayoutCampaignTrial(rows.Count + 1, wave, parent, policy.Id, trial.WaveCleared, trial.Kills,
                    trial.Lives, trial.Credits, value, trial.EscapedHealth, trial.RemainingHealth, trial.FinalTick - trial.StartTick, trial.StopReason, policy.DirectPlateLimit);
                rows.Add(row); completed?.Invoke(row, trial);
                if (!trial.WaveCleared || trial.Checkpoint is null) continue;
                var child = new Node(trial.Checkpoint, node, new(wave, policy), trial.Credits + value * .55);
                if (trial.Verification is { VerifiedWin: true, FromNormalStart: true })
                    return Result(child, "verified normal-start campaign win", trial.Trace, trial.Verification);
                if (next.All(existing => existing.Checkpoint.StateSignature != child.Checkpoint.StateSignature)) next.Add(child);
                next = next.OrderByDescending(candidate => candidate.Value).Take(options.BeamWidth).ToList();
            }
            if (next.Count > 0) { frontier = next; best = next[0]; }
            else if (stopped is null) stopped = "wave alternatives exhausted";
            if (stopped is not null) break;
        }
        return Result(best, stopped ?? "horizon cleared");
    }

    public static Action<PlannerSession> CreateSpending(GameContent content, PlanningCheckpoint initial,
        LayoutBlueprint layout, LayoutWavePlan rule, int wave)
    {
        var initialSession = PlannerSession.Restore(content, initial).Session;
        var apexGoals = layout.Towers.Where(goal => goal.ApexFromWave > 0 && wave >= goal.ApexFromWave).ToArray();
        var protectedIds = initialSession.Towers.Where(tower => apexGoals.Any(goal => Matches(tower, goal)))
            .Select(tower => tower.Id).ToHashSet();
        var salvage = CoverageSalvage.Prepare(initialSession, rule.Salvage, protectedIds);
        return adapter =>
        {
            var allowApex = rule.ApplyScheduledApex && rule.Allocation.AllowApexUpgrades;
            var accepted = rule.SpendOnLayout
                ? LayoutBlueprintSpending.Apply(content, adapter, layout, wave,
                    rule.Allocation with { AllowApexUpgrades = allowApex }).AcceptedCommands
                : 0;
            if (allowApex)
                foreach (var goal in apexGoals)
                {
                    if (accepted >= rule.Allocation.MaximumCommands) break;
                    var tower = adapter.Session.Towers.FirstOrDefault(tower => Matches(tower, goal));
                    if (tower is not null && adapter.Session.CanApexUpgrade(tower) &&
                        adapter.Session.Economy.Credits >= (long)tower.ApexUpgradeCost + rule.Allocation.ReserveCredits &&
                        adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.UpgradeTower, EntityId = tower.Id },
                            "scheduled layout Apex allocation").Accepted) accepted++;
                }
            CoverageSalvage.Apply(adapter, salvage, rule.Salvage);
        };
    }

    private static bool Matches(Towers.TowerInstance tower, LayoutTowerGoal goal) => tower.Definition.Id == goal.TowerId &&
        Math.Abs(tower.Position.X - goal.X) < .01f && Math.Abs(tower.Position.Y - goal.Y) < .01f;
}
