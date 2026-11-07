using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Simulation.Planning;

public sealed record PlanningActionRecord
{
    public long Tick { get; init; }
    public long RequestedTick { get; init; }
    public long ResolvedTick { get; init; }
    public int Wave { get; init; }
    public bool DuringWave { get; init; }
    public string Trigger { get; init; } = "immediate";
    public GameCommand Command { get; init; } = new();
    public bool Accepted { get; init; }
    public string Reason { get; init; } = "";
    public int ActualCost { get; init; }
    public int CreditsBefore { get; init; }
    public int CreditsAfter { get; init; }
    public string StateSignature { get; init; } = "";
}

public sealed record PlanningCheckpoint
{
    public int SchemaVersion { get; init; } = 1;
    public PlanningScenario Scenario { get; init; } = new();
    public string BuildHash { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public string SnapshotJson { get; init; } = "";
    public string DetachedProjectileTargetsJson { get; init; } = "[]";
    public string? OriginSaveJson { get; init; }
    public string OriginSignature { get; init; } = "";
    public long Tick { get; init; }
    public string StateSignature { get; init; } = "";
    public IReadOnlyList<PlanningActionRecord> Actions { get; init; } = Array.Empty<PlanningActionRecord>();
}

public sealed record PlanningTrace
{
    public int SchemaVersion { get; init; } = 1;
    public string SolverVersion { get; init; } = "spending-planner-v1";
    public PlanningScenario Scenario { get; init; } = new();
    public PlannerBudget? SearchBudget { get; init; }
    public PlannerRestrictions? SearchRestrictions { get; init; }
    public string BuildHash { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public string? OriginSaveJson { get; init; }
    public string OriginSignature { get; init; } = "";
    public IReadOnlyList<PlanningActionRecord> Actions { get; init; } = Array.Empty<PlanningActionRecord>();
    public long FinalTick { get; init; }
    public string FinalSignature { get; init; } = "";
    public int FinalWave { get; init; }
    public int FinalLives { get; init; }
    public int FinalCredits { get; init; }
    public bool CampaignVictory { get; init; }
    public bool HorizonSurvived { get; init; }
    public bool Defeat { get; init; }
}

public sealed record ReplayVerification
{
    public bool Verified { get; init; }
    public bool VerifiedWin { get; init; }
    public bool FromNormalStart { get; init; }
    public bool HorizonSurvived { get; init; }
    public int ActionsChecked { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CombatSales { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IntermissionSales { get; init; }
    public long TicksChecked { get; init; }
    public string FinalSignature { get; init; } = "";
    public string Reason { get; init; } = "";
}

public static class PlanningFingerprint
{
    private sealed record ContentIdentity(string Hash);
    private static readonly ConditionalWeakTable<GameContent, ContentIdentity> SearchContentIdentities = new();
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<string> AssemblyHash = new(() =>
    {
        var assembly = typeof(GameSession).Assembly;
        return File.Exists(assembly.Location)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
            : Hash(assembly.ManifestModule.ModuleVersionId.ToString("N"));
    });

    public static string Build() => AssemblyHash.Value;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Content(GameContent content) => Hash(JsonSerializer.Serialize(new
    {
        Towers = content.Towers.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        Enemies = content.Enemies.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        content.Map,
        content.Waves,
        Maps = content.Maps.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        WaveSets = content.WaveSets.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        Difficulties = content.Difficulties.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        Challenges = content.Challenges.OrderBy(pair => pair.Key, StringComparer.Ordinal),
        content.Tactics
    }, JsonOptions));

    // Loaded definitions remain immutable while search workers share them. Verification
    // recomputes Content instead of trusting this per-search object-identity cache.
    internal static string SearchContent(GameContent content) =>
        SearchContentIdentities.GetValue(content, value => new ContentIdentity(Content(value))).Hash;

    public static string State(GameSession session, long tick) => State(session, tick, canonical: true);

    internal static bool MatchesState(GameSession session, long tick, string signature) =>
        State(session, tick) == signature || State(session, tick, canonical: false) == signature;

    private static string State(GameSession session, long tick, bool canonical)
    {
        var snapshot = session.CaptureCoOpState(tick, 0, false);
        // Run IDs identify persistence slots, not simulation state.
        snapshot.RunId = "planning-state";
        // Dictionary storage slots can differ after a restored sale and replacement.
        // Attribution identity depends on keys and values, not enumeration order.
        if (canonical)
            snapshot.Statistics.TowerDefinitionByInstance = snapshot.Statistics.TowerDefinitionByInstance
                .OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
        return Hash(JsonSerializer.Serialize(new
        {
            Snapshot = snapshot,
            DetachedProjectileTargets = CaptureDetachedProjectileTargets(session)
        }, JsonOptions));
    }

    internal static IReadOnlyList<EnemyRuntimeState> CaptureDetachedProjectileTargets(GameSession session)
    {
        var activeIds = session.Enemies.Select(enemy => enemy.Id).ToHashSet();
        return session.Projectiles.Projectiles.Where(projectile => !projectile.IsExpired &&
                projectile.Target is { } target && !activeIds.Contains(target.Id))
            .Select(projectile => projectile.Target!).DistinctBy(enemy => enemy.Id).OrderBy(enemy => enemy.Id)
            .Select(enemy => enemy.CaptureCoOpState()).ToArray();
    }

    internal static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException($"Missing {typeof(T).Name}.");
}

/// <summary>A separate sequential replay using the same Update entry point as normal gameplay.</summary>
public static class PlanningReplay
{
    public static ReplayVerification Verify(GameContent content, PlanningTrace trace, bool requireCurrentBuild = true)
    {
        var checkedActions = 0;
        long tick = 0;
        try
        {
            if (trace.SchemaVersion != 1 || trace.FinalTick < 0 || trace.FinalTick > 100_000_000 ||
                trace.Actions.Count > 1_000_000)
                throw new InvalidDataException("Unsupported or unbounded replay.");
            if (requireCurrentBuild && trace.BuildHash != PlanningFingerprint.Build())
                throw new InvalidDataException("Replay build hash differs from this executable.");
            if (trace.ContentHash != PlanningFingerprint.Content(content))
                throw new InvalidDataException("Replay content hash differs from loaded content.");
            if (trace.Scenario.InformationProfile is not ("oracle" or "informed"))
                throw new InvalidDataException("Replay information profile is unknown.");
            var execution = PlanningExecutionProfile.Resolve(trace.Scenario.ExecutionProfile);
            trace.SearchBudget?.Validate();
            var reactionTicks = Math.Max(execution.ReactionDelayTicks, trace.SearchRestrictions?.ReactionTicks ?? 0);
            var minimumActionTicks = Math.Max(execution.MinimumActionIntervalTicks,
                trace.SearchRestrictions?.MinimumActionTicks ?? 0);
            var combatCounts = new Dictionary<int, int>();
            var lastCombatTicks = new Dictionary<int, long>();
            var combatSales = 0;
            var intermissionSales = 0;
            var session = PlannerSession.CreateInitialSession(content, trace.Scenario, trace.OriginSaveJson);
            if (!PlanningFingerprint.MatchesState(session, 0, trace.OriginSignature))
                throw new InvalidDataException("Replay initial state does not match its declared origin.");

            foreach (var action in trace.Actions)
            {
                if (action.Tick < tick || action.Tick > trace.FinalTick || action.ResolvedTick != action.Tick ||
                    action.RequestedTick < 0 || action.RequestedTick > action.ResolvedTick)
                    throw new InvalidDataException("Replay actions are out of order or have unsupported scheduling.");
                while (tick < action.Tick)
                {
                    session.Update(DeterministicSessionRunner.FixedStepSeconds);
                    tick++;
                }
                var before = session.Economy.Credits;
                var duringWave = session.Waves.IsActive;
                if (duringWave && action.Command.Type != GameCommandType.StartWave)
                {
                    var wave = session.CurrentWave;
                    if (tick - action.RequestedTick < reactionTicks)
                        throw new InvalidDataException($"Replay does not meet the declared reaction delay at tick {tick}.");
                    if (combatCounts.GetValueOrDefault(wave) >= execution.MaximumCombatActionsPerWave ||
                        lastCombatTicks.TryGetValue(wave, out var previousTick) &&
                        tick - previousTick < minimumActionTicks)
                        throw new InvalidDataException($"Replay exceeds the declared combat action rate or cap at tick {tick}.");
                    if (!execution.CombatPurchases && action.Command.Type is GameCommandType.PlaceTower or
                        GameCommandType.UpgradeTower or GameCommandType.ChooseDoctrine or GameCommandType.SpecializeTower or
                        GameCommandType.PlaceGenerator or GameCommandType.UpgradeGenerator)
                        throw new InvalidDataException("Replay contains a combat purchase excluded by its execution profile.");
                    if (!execution.TacticalActions && action.Command.Type is GameCommandType.DeployEmergencyDefense or
                        GameCommandType.OverdriveTower)
                        throw new InvalidDataException("Replay contains a tactical action excluded by its execution profile.");
                    combatCounts[wave] = combatCounts.GetValueOrDefault(wave) + 1;
                    lastCombatTicks[wave] = tick;
                }
                var result = PlannerSession.ApplyLegalCommand(session, trace.Scenario, action.Command);
                if (!result.Accepted)
                    throw new InvalidDataException($"Replay contains an invalid action at tick {tick} ({action.Command.Type}): {result.Reason}");
                var after = session.Economy.Credits;
                if (result.Accepted != action.Accepted || before != action.CreditsBefore ||
                    duringWave != action.DuringWave || session.CurrentWave != action.Wave ||
                    after != action.CreditsAfter || before - after != action.ActualCost ||
                    !PlanningFingerprint.MatchesState(session, tick, action.StateSignature))
                    throw new InvalidDataException($"Replay diverged at action {checkedActions}, tick {tick} ({action.Command.Type}).");
                if (PlanningScenario.IsSale(action.Command.Type))
                {
                    if (duringWave) combatSales++;
                    else intermissionSales++;
                }
                checkedActions++;
            }
            while (tick < trace.FinalTick)
            {
                session.Update(DeterministicSessionRunner.FixedStepSeconds);
                tick++;
            }
            var victory = IsCampaignVictory(session, trace.Scenario);
            var horizon = IsHorizonSurvived(session, trace.Scenario);
            if (!PlanningFingerprint.MatchesState(session, tick, trace.FinalSignature) || session.CurrentWave != trace.FinalWave ||
                session.Economy.Lives != trace.FinalLives || session.Economy.Credits != trace.FinalCredits ||
                session.IsDefeat != trace.Defeat || victory != trace.CampaignVictory || horizon != trace.HorizonSurvived)
                throw new InvalidDataException("Replay final state or terminal claim differs.");
            return new ReplayVerification
            {
                Verified = true,
                VerifiedWin = victory,
                FromNormalStart = trace.OriginSaveJson is null,
                HorizonSurvived = horizon,
                ActionsChecked = checkedActions,
                CombatSales = combatSales,
                IntermissionSales = intermissionSales,
                TicksChecked = tick,
                FinalSignature = trace.FinalSignature,
                Reason = victory ? "Complete legal campaign replay verified." :
                    horizon ? "Declared horizon survived; this is not a campaign victory." :
                    "Legal partial replay verified; no victory claimed."
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or JsonException)
        {
            return new ReplayVerification
            {
                FromNormalStart = trace.OriginSaveJson is null,
                ActionsChecked = checkedActions,
                TicksChecked = tick,
                Reason = exception.Message
            };
        }
    }

    internal static bool IsCampaignVictory(GameSession session, PlanningScenario scenario) =>
        !scenario.Endless && session.IsVictory && session.Waves.IsFinalWaveCleared &&
        !session.Waves.IsActive && session.Enemies.Count == 0 && session.Waves.QueuedEnemies == 0 &&
        session.Economy.Lives > 0 && !session.IsCoOpPaused;

    internal static bool IsHorizonSurvived(GameSession session, PlanningScenario scenario) =>
        session.CurrentWave >= scenario.Horizon && !session.IsDefeat && !session.Waves.IsActive &&
        session.Enemies.Count == 0 && session.Waves.QueuedEnemies == 0 && !session.IsCoOpPaused;
}
