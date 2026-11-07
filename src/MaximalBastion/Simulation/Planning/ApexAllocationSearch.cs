using System.Diagnostics;
using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

public sealed record ApexAllocationOptions
{
    public int MaximumTrials { get; init; } = 256;
    public double MaximumWallSeconds { get; init; } = 120;
    public int MaximumWaveTicks { get; init; } = 18_000;
    public int DecisionTicks { get; init; } = 60;
    public int MaximumUpgrades { get; init; } = 1;
    public int MaximumFundingSales { get; init; }
    public int FundingSaleCandidates { get; init; } = 6;
    public int DirectPlateLimit { get; init; } = 16;
    public int ActivePlateLimit { get; init; } = 8;
    public float PlateProgressOffset { get; init; }
    public IReadOnlyList<string> TargetingProfiles { get; init; } = ["split", "armored", "first", "support", "strongest"];
    public IReadOnlyDictionary<int, TargetMode> CleanupTargetModes { get; init; } = new Dictionary<int, TargetMode>();
    public float CleanupProgressThreshold { get; init; } = 0.72f;
    public bool StopAtCampaignWin { get; init; } = true;

    public void Validate()
    {
        if (MaximumTrials is < 1 or > 100_000 || !double.IsFinite(MaximumWallSeconds) || MaximumWallSeconds <= 0 ||
            MaximumWaveTicks is < 60 or > 216_000 || DecisionTicks is < 1 or > 3600 || MaximumUpgrades is < 0 or > 2 ||
            MaximumFundingSales is < 0 or > 2 || FundingSaleCandidates is < 1 or > 32 ||
            DirectPlateLimit is < 0 or > 16 || ActivePlateLimit is < 0 or > 12 ||
            !float.IsFinite(PlateProgressOffset) || PlateProgressOffset is < -0.25f or > 0.25f ||
            !float.IsFinite(CleanupProgressThreshold) || CleanupProgressThreshold is < 0 or > 1 ||
            CleanupTargetModes is null || CleanupTargetModes.Any(pair => pair.Key <= 0 || !Enum.IsDefined(pair.Value)) ||
            TargetingProfiles is null || TargetingProfiles.Count == 0 || TargetingProfiles.Any(profile => profile is not ("split" or "armored" or "first" or "support" or "strongest")))
            throw new ArgumentException("Invalid Apex allocation search configuration.");
    }
}

public sealed record ApexAllocationPlan(string Id, IReadOnlyList<GameCommand> InitialCommands,
    IReadOnlyList<int> ApexTowerIds, string TargetingProfileId);

public sealed record ApexAllocationTrial
{
    public required ApexAllocationPlan Plan { get; init; }
    public required ApexAllocationOptions Options { get; init; }
    public bool Accepted { get; init; }
    public bool WaveCleared { get; init; }
    public bool CampaignVictory { get; init; }
    public int Wave { get; init; }
    public int Kills { get; init; }
    public int Lives { get; init; }
    public int Credits { get; init; }
    public int UpfrontCost { get; init; }
    public float EscapedHealth { get; init; }
    public int RemainingEnemies { get; init; }
    public int QueuedEnemies { get; init; }
    public float RemainingHealth { get; init; }
    public long Ticks { get; init; }
    public double WallSeconds { get; init; }
    public int CombatActions { get; init; }
    public int PlatePurchases { get; init; }
    public required PlanningCheckpoint Checkpoint { get; init; }
    public PlanningTrace? WinningTrace { get; init; }
    public ReplayVerification? Verification { get; init; }
    public string? Rejection { get; init; }
}

public sealed record ApexAllocationResult(IReadOnlyList<ApexAllocationTrial> Trials, int CandidateCount,
    string StopReason, double WallSeconds);

/// <summary>Compares legal Apex allocations jointly with targeting while holding subsequent investment fixed.</summary>
public static class ApexAllocationSearch
{
    public static IReadOnlyList<ApexAllocationPlan> Generate(GameSession session, ApexAllocationOptions options)
    {
        options.Validate();
        var allocations = new List<(int[] Apex, GameCommand[] Commands)> { ([], []) };
        var eligible = session.Towers.Where(session.CanApexUpgrade).OrderBy(tower => tower.Id).ToArray();
        var sets = eligible.Select(tower => new[] { tower }).ToList();
        if (options.MaximumUpgrades >= 2)
            for (var first = 0; first < eligible.Length; first++)
                for (var second = first + 1; second < eligible.Length; second++)
                    sets.Add([eligible[first], eligible[second]]);
        if (options.MaximumUpgrades == 0) sets.Clear();
        foreach (var set in sets)
        {
            var cost = set.Sum(tower => tower.ApexUpgradeCost);
            var upgradeCommands = set.Select(tower => Command(GameCommandType.UpgradeTower, tower.Id)).ToArray();
            if (cost <= session.Economy.Credits)
            { allocations.Add((set.Select(tower => tower.Id).ToArray(), upgradeCommands)); continue; }
            if (!session.SellingEnabled || options.MaximumFundingSales == 0) continue;
            var saleOptions = session.Towers.Where(tower => !set.Contains(tower))
                .OrderBy(tower => (tower.LifetimeDamage + tower.LifetimeSupportDamageEquivalent +
                    tower.LifetimeExposeDamageEquivalent + tower.LifetimeArmorBreakDamageEquivalent +
                    tower.LifetimeControlSeconds * 10) / Math.Max(1, tower.SellValue)).ThenBy(tower => tower.Id)
                .Take(options.FundingSaleCandidates).ToArray();
            foreach (var tower in saleOptions)
                if (session.Economy.Credits + tower.SellValue >= cost)
                    allocations.Add((set.Select(item => item.Id).ToArray(), [Command(GameCommandType.SellTower, tower.Id), .. upgradeCommands]));
            if (options.MaximumFundingSales < 2) continue;
            for (var first = 0; first < saleOptions.Length; first++)
                for (var second = first + 1; second < saleOptions.Length; second++)
                {
                    var one = saleOptions[first]; var two = saleOptions[second];
                    if (session.Economy.Credits + one.SellValue >= cost || session.Economy.Credits + two.SellValue >= cost ||
                        session.Economy.Credits + one.SellValue + two.SellValue < cost) continue;
                    allocations.Add((set.Select(tower => tower.Id).ToArray(),
                        [Command(GameCommandType.SellTower, one.Id), Command(GameCommandType.SellTower, two.Id), .. upgradeCommands]));
                }
        }
        return allocations.SelectMany(allocation => options.TargetingProfiles.Select(profile => new ApexAllocationPlan(
            $"apex-{string.Join('-', allocation.Apex)}-sell-{string.Join('-', allocation.Commands.Where(command => command.Type == GameCommandType.SellTower).Select(command => command.EntityId))}-{profile}",
            allocation.Commands, allocation.Apex, profile))).ToArray();
    }

    public static ApexAllocationResult Search(GameContent content, PlanningCheckpoint initial, ApexAllocationOptions options,
        Action<ApexAllocationTrial>? completed = null, CancellationToken cancellation = default)
    {
        options.Validate();
        var proposals = Generate(PlannerSession.Restore(content, initial).Session, options);
        var clock = Stopwatch.StartNew();
        var rows = new List<ApexAllocationTrial>();
        foreach (var plan in proposals)
        {
            if (rows.Count >= options.MaximumTrials || clock.Elapsed.TotalSeconds >= options.MaximumWallSeconds || cancellation.IsCancellationRequested) break;
            var row = Evaluate(content, initial, plan, options, cancellation);
            rows.Add(row); completed?.Invoke(row);
            if (row.Verification is { VerifiedWin: true } && options.StopAtCampaignWin) break;
        }
        var reason = cancellation.IsCancellationRequested ? "cancelled" :
            rows.LastOrDefault()?.Verification is { VerifiedWin: true } && options.StopAtCampaignWin ? "verified campaign win" :
            rows.Count >= options.MaximumTrials ? "trial budget reached" :
            clock.Elapsed.TotalSeconds >= options.MaximumWallSeconds ? "wall-time budget reached" : "allocation candidates exhausted";
        return new(rows, proposals.Count, reason, clock.Elapsed.TotalSeconds);
    }

    public static ApexAllocationTrial Evaluate(GameContent content, PlanningCheckpoint initial, ApexAllocationPlan plan,
        ApexAllocationOptions options, CancellationToken cancellation = default)
    {
        options.Validate();
        if (initial.Scenario.InformationProfile != "oracle" || initial.Scenario.ExecutionProfile != "expert")
            throw new InvalidDataException("The matched tactical continuation uses the unrestricted oracle/expert profile.");
        var clock = Stopwatch.StartNew();
        var adapter = PlannerSession.Restore(content, initial);
        var session = adapter.Session;
        var startingTick = adapter.Tick;
        var startingKills = session.Economy.TotalKills;
        var startingCredits = session.Economy.Credits;
        var startingActions = adapter.Actions.Count;
        var wave = session.Waves.IsActive ? session.CurrentWave : session.CurrentWave + 1;
        var escapedHealth = 0f;
        session.EnemyEscaped += enemy => escapedHealth += enemy.Health + enemy.Shield;
        string? rejection = null;
        foreach (var command in plan.InitialCommands)
            if (!adapter.Apply(command, "joint Apex allocation and targeting").Accepted)
            { rejection = "Initial allocation rejected by current game rules."; break; }
        var upfrontCost = startingCredits - session.Economy.Credits;
        if (rejection is null)
        {
            var policy = new AutoPlayer(session, AutoPlayerStrategy.Experienced, initial.Scenario.Seed,
                new SimulationOptions
                {
                    HoldFootprint = true, UseApexUpgrades = false,
                    WavePlan = new WavePlan
                    {
                        Wave = wave, DecisionSeed = initial.Scenario.Seed, TargetingProfileId = plan.TargetingProfileId,
                        Parameters = new Dictionary<string, double>
                        {
                            ["apexLimit"] = 0, ["directPlateLimit"] = options.DirectPlateLimit,
                            ["activePlateLimit"] = options.ActivePlateLimit, ["finalRoleFill"] = 0,
                            ["plateProgressOffset"] = options.PlateProgressOffset, ["protocolMinimumEnemies"] = 1
                        }
                    }
                }, command =>
                {
                    if (command.Type is not (GameCommandType.SetTargetMode or GameCommandType.OverdriveTower or GameCommandType.DeployEmergencyDefense)) return false;
                    if (command.Type == GameCommandType.SetTargetMode && session.Waves.QueuedEnemies == 0 &&
                        session.Enemies.Any(enemy => !enemy.IsDead && !enemy.HasEscaped && enemy.PathProgress >= options.CleanupProgressThreshold) &&
                        options.CleanupTargetModes.TryGetValue(command.EntityId, out var cleanupMode))
                        command = command with { TargetMode = cleanupMode };
                    if (command.Type == GameCommandType.SetTargetMode && session.Towers.FirstOrDefault(tower => tower.Id == command.EntityId)?.TargetMode == command.TargetMode) return true;
                    return adapter.Apply(command, "matched targeting, Protocol and Plate continuation").Accepted;
                });
            if (!session.Waves.IsActive)
            {
                policy.PrepareForWave(session);
                if (!adapter.Apply(Command(GameCommandType.StartWave), "allocation complete").Accepted) rejection = "Wave start rejected.";
            }
            while (session.Waves.IsActive && !session.IsDefeat && adapter.Tick - startingTick < options.MaximumWaveTicks && !cancellation.IsCancellationRequested)
            {
                adapter.Advance(1);
                if ((adapter.Tick - startingTick) % options.DecisionTicks == 0 && session.Waves.IsActive && !session.IsDefeat)
                    policy.ReactDuringWave(session);
            }
        }
        var cleared = rejection is null && !session.IsDefeat && !session.Waves.IsActive && session.Enemies.Count == 0 &&
            session.Waves.QueuedEnemies == 0 && session.CurrentWave == wave;
        var trace = adapter.CampaignVictory ? adapter.ExportTrace() : null;
        var verification = trace is null ? null : PlanningReplay.Verify(content, trace);
        return new ApexAllocationTrial
        {
            Plan = plan, Options = options, Accepted = rejection is null, Wave = wave, WaveCleared = cleared,
            CampaignVictory = adapter.CampaignVictory, Kills = session.Economy.TotalKills - startingKills,
            Lives = session.Economy.Lives, Credits = session.Economy.Credits, UpfrontCost = upfrontCost,
            EscapedHealth = escapedHealth, Ticks = adapter.Tick - startingTick, WallSeconds = clock.Elapsed.TotalSeconds,
            RemainingEnemies = session.Enemies.Count(enemy => !enemy.IsDead && !enemy.HasEscaped),
            QueuedEnemies = session.Waves.QueuedEnemies,
            RemainingHealth = session.Enemies.Where(enemy => !enemy.IsDead && !enemy.HasEscaped).Sum(enemy => enemy.Health + enemy.Shield),
            CombatActions = adapter.Actions.Skip(startingActions).Count(action => action.Accepted && action.DuringWave),
            PlatePurchases = session.EmergencyDirectPurchasesThisWave, Checkpoint = adapter.Capture(),
            WinningTrace = trace, Verification = verification, Rejection = rejection
        };
    }

    private static GameCommand Command(GameCommandType type, int entityId = 0) => new() { PlayerId = 1, Type = type, EntityId = entityId };
}
