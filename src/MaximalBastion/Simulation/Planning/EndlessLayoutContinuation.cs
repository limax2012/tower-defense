using System.Diagnostics;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record EndlessLayoutPlan
{
    public string Id { get; init; } = "first-endless-wave";
    public int PreparationTicks { get; init; }
    public LayoutBlueprint? Layout { get; init; }
    public LayoutWavePlan Wave { get; init; } = new()
    {
        FirstWave = GameConstants.GeneratedEndlessStartWave,
        LastWave = GameConstants.GeneratedEndlessStartWave
    };

    public void Validate(GameContent content, PlanningScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 160 || PreparationTicks is < 0 or > 216_000 || Wave is null ||
            Wave.FirstWave != GameConstants.GeneratedEndlessStartWave || Wave.LastWave != Wave.FirstWave ||
            Wave.Policy is null || Wave.Alternatives is null || Wave.Alternatives.Count > 256 ||
            Wave.Alternatives.Any(policy => policy is null) || Wave.Allocation is null || Wave.Salvage is null)
            throw new InvalidDataException("A first-Endless-wave plan requires a layout and bounded wave 31 policies.");
        if (Layout is null && (Wave.SpendOnLayout || Wave.ApplyScheduledApex || Wave.Salvage.MaximumSales != 0))
            throw new InvalidDataException("A layout is required for wave-31 construction, scheduled Apex or salvage.");
        Layout?.Validate(content);
        if (Layout?.MapId is { } mapId && mapId != scenario.MapId)
            throw new InvalidDataException("The continuation layout belongs to a different map.");
        Wave.Policy.Validate();
        Wave.Allocation.Validate();
        Wave.Salvage.Validate();
        foreach (var alternative in Wave.Alternatives) alternative.Validate();
    }
}

public sealed record EndlessLayoutOptions
{
    public int MaximumTrials { get; init; } = 32;
    public double MaximumWallSeconds { get; init; } = 120;
    public int MaximumWaveTicks { get; init; } = 18_000;
    public bool ForbidCombatSales { get; init; }
}

public sealed record EndlessLayoutTrial(int Number, string PolicyId, bool WaveCleared, bool VerifiedSurvival,
    int Kills, int Lives, int Credits, int Sales, int RemainingEnemies, float RemainingHealth, float EscapedHealth,
    long Ticks, string StopReason);

public sealed record EndlessLayoutResult(string PlanId, EndlessLayoutOptions Options, string Outcome,
    string StopReason, double WallSeconds, PlanningCheckpoint Ready, IReadOnlyList<EndlessLayoutTrial> Trials,
    PlanningCheckpoint? SelectedCheckpoint, PlanningTrace? SelectedTrace, ReplayVerification? Verification);

/// <summary>Branches the first generated wave from an independently verified fresh campaign victory.</summary>
public static class EndlessLayoutContinuation
{
    public static PlanningCheckpoint Prepare(GameContent content, PlanningCheckpoint campaign, bool forbidCombatSales = false)
    {
        if (forbidCombatSales && !campaign.Scenario.ForbidCombatSales)
            campaign = campaign with { Scenario = campaign.Scenario with { ForbidCombatSales = true } };
        var scenario = campaign.Scenario;
        if (scenario.Endless || scenario.StartingProfile != "fresh" || campaign.OriginSaveJson is not null ||
            scenario.InformationProfile != "oracle" || scenario.ExecutionProfile != "expert")
            throw new InvalidDataException("Endless layout continuation requires a fresh oracle/expert campaign checkpoint.");

        var currentBuild = campaign.BuildHash == PlanningFingerprint.Build();
        var validated = currentBuild ? PlannerSession.Restore(content, campaign) : PlannerSession.Revalidate(content, campaign);
        if (!validated.CampaignVictory || validated.Session.CurrentWave != GameConstants.CampaignWaveCount ||
            validated.Session.Waves.AuthoredWaveCount != GameConstants.CampaignWaveCount)
            throw new InvalidDataException("The checkpoint must have cleared all 30 authored campaign waves.");
        if (currentBuild)
        {
            var verification = PlanningReplay.Verify(content, validated.ExportTrace());
            if (!verification.VerifiedWin || !verification.FromNormalStart)
                throw new InvalidDataException("Campaign prefix failed independent verification: " + verification.Reason);
        }

        var permitted = validated.Capture() with
        {
            Scenario = scenario with { Endless = true, Horizon = GameConstants.GeneratedEndlessStartWave }
        };
        var adapter = PlannerSession.Restore(content, permitted);
        var command = adapter.Apply(new() { PlayerId = 1, Type = GameCommandType.ContinueEndless }, "continue after campaign victory");
        if (!command.Accepted || !adapter.Session.IsEndlessMode ||
            adapter.Session.Waves.NextWave?.Number != GameConstants.GeneratedEndlessStartWave)
            throw new InvalidDataException("The game could not enter the first generated Endless wave: " + command.Reason);
        return adapter.Capture();
    }

    public static EndlessLayoutResult Run(GameContent content, PlanningCheckpoint campaign, EndlessLayoutPlan plan,
        EndlessLayoutOptions? options = null, Action<EndlessLayoutTrial, TacticalRolloutTrial>? completed = null,
        CancellationToken cancellation = default)
    {
        options ??= new();
        if (options.MaximumTrials is < 1 or > 257 || !double.IsFinite(options.MaximumWallSeconds) ||
            options.MaximumWallSeconds <= 0 || options.MaximumWaveTicks is < 1 or > 216_000)
            throw new ArgumentException("Invalid Endless continuation budget.");
        plan.Validate(content, campaign.Scenario);
        var clock = Stopwatch.StartNew();
        var ready = Prepare(content, campaign, options.ForbidCombatSales);
        if (plan.PreparationTicks > 0)
        {
            var preparation = PlannerSession.Restore(content, ready);
            preparation.Advance(plan.PreparationTicks);
            ready = preparation.Capture();
        }
        var spending = plan.Layout is null ? null : LayoutCampaignRunner.CreateSpending(content, ready, plan.Layout, plan.Wave, ready.Scenario.Horizon);
        var rows = new List<EndlessLayoutTrial>();
        TacticalRolloutTrial? selected = null;
        var stop = "supplied policies exhausted";
        foreach (var policy in plan.Wave.Alternatives.Prepend(plan.Wave.Policy))
        {
            if (cancellation.IsCancellationRequested) { stop = "cancelled"; break; }
            if (rows.Count >= options.MaximumTrials) { stop = "trial budget reached"; break; }
            if (clock.Elapsed.TotalSeconds >= options.MaximumWallSeconds) { stop = "wall-time budget reached"; break; }
            var trial = TacticalForecastSearch.Evaluate(content, ready, policy, spending,
                new() { MaximumWaveTicks = options.MaximumWaveTicks, VerifyWaveClear = true }, cancellation);
            var verified = trial.Verification is { Verified: true, FromNormalStart: true, HorizonSurvived: true, VerifiedWin: false };
            if (trial.WaveCleared && !verified)
                throw new InvalidDataException("Wave 31 clear failed independent survival verification: " + trial.Verification?.Reason);
            var sales = trial.Checkpoint!.Actions.Skip(ready.Actions.Count)
                .Count(action => action.Accepted && PlanningScenario.IsSale(action.Command.Type));
            var row = new EndlessLayoutTrial(rows.Count + 1, policy.Id, trial.WaveCleared, verified,
                trial.Kills, trial.Lives, trial.Credits, sales, trial.RemainingEnemies + trial.QueuedEnemies,
                trial.RemainingHealth, trial.EscapedHealth, trial.FinalTick - trial.StartTick, trial.StopReason);
            rows.Add(row);
            completed?.Invoke(row, trial);
            if (selected is null || trial.WaveCleared && !selected.WaveCleared ||
                trial.WaveCleared == selected.WaveCleared && trial.Score > selected.Score) selected = trial;
        }
        if (cancellation.IsCancellationRequested) stop = "cancelled";
        var trace = selected?.Trace ?? (selected?.Checkpoint is { } checkpoint ? PlannerSession.Restore(content, checkpoint).ExportTrace() : null);
        var proof = selected?.Verification ?? (trace is null ? null : PlanningReplay.Verify(content, trace));
        if (proof is { Verified: false }) throw new InvalidDataException("Selected trace failed independent replay: " + proof.Reason);
        var outcome = proof is { Verified: true, FromNormalStart: true, HorizonSurvived: true, VerifiedWin: false }
            ? "verified survival through Endless wave 31" : "wave 31 survival not found within budget";
        return new(plan.Id, options, outcome, stop, clock.Elapsed.TotalSeconds, ready, rows, selected?.Checkpoint, trace, proof);
    }
}
