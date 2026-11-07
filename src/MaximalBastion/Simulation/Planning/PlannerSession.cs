using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Simulation.Planning;

/// <summary>Fixed-tick command adapter with detached snapshots and an immutable replay prefix.</summary>
public sealed class PlannerSession
{
    private readonly GameContent _content;
    private readonly DeterministicSessionRunner _runner;
    private readonly List<PlanningActionRecord> _actions;
    private readonly string? _originSaveJson;
    private readonly string _originSignature;
    private readonly string _contentHash;

    public GameSession Session { get; }
    public PlanningScenario Scenario { get; }
    public long Tick => _runner.Tick;
    public string Signature => PlanningFingerprint.State(Session, Tick);
    public IReadOnlyList<PlanningActionRecord> Actions => _actions.AsReadOnly();
    public bool CampaignVictory => PlanningReplay.IsCampaignVictory(Session, Scenario);
    public bool HorizonSurvived => PlanningReplay.IsHorizonSurvived(Session, Scenario);
    public string RestorationMethod { get; private set; } = "new-session";

    private PlannerSession(GameContent content, GameSession session, PlanningScenario scenario, long tick,
        string? originSaveJson, string originSignature, string contentHash,
        IEnumerable<PlanningActionRecord>? actions = null)
    {
        _content = content;
        Session = session;
        Scenario = scenario;
        _runner = new DeterministicSessionRunner(session, tick);
        _originSaveJson = originSaveJson;
        _originSignature = originSignature;
        _contentHash = contentHash;
        _actions = actions?.ToList() ?? new List<PlanningActionRecord>();
    }

    public static PlannerSession Create(GameContent content, PlanningScenario scenario)
    {
        var session = CreateInitialSession(content, scenario, null);
        return new PlannerSession(content, session, scenario, 0, null,
            PlanningFingerprint.State(session, 0), PlanningFingerprint.SearchContent(content));
    }

    public static PlannerSession FromSave(GameContent content, SaveGameData save, PlanningScenario scenario)
    {
        var json = JsonSerializer.Serialize(save, PlanningFingerprint.JsonOptions);
        var session = CreateInitialSession(content, scenario, json);
        return new PlannerSession(content, session, scenario, 0, json,
            PlanningFingerprint.State(session, 0), PlanningFingerprint.SearchContent(content));
    }

    internal static GameSession CreateInitialSession(GameContent content, PlanningScenario scenario, string? saveJson)
    {
        if ((!content.Maps.ContainsKey(scenario.MapId) && content.Map.Id != scenario.MapId) ||
            !content.Difficulties.ContainsKey(scenario.DifficultyId) ||
            !content.Challenges.TryGetValue(scenario.ChallengeId, out var challenge) || challenge.IsSandbox)
            throw new InvalidDataException("Planning requires an available campaign map, difficulty and competitive directive.");
        if (scenario.Horizon <= 0)
            throw new InvalidDataException("Planning horizon must be positive.");
        if (saveJson is null && scenario.StartingProfile != "fresh")
            throw new InvalidDataException("A normal-start replay requires the fresh starting profile.");
        var session = saveJson is null
            ? new GameSession(content, scenario.MapId, scenario.DifficultyId, scenario.ChallengeId)
            : GameSession.RestoreSaveGame(content, PlanningFingerprint.Deserialize<SaveGameData>(saveJson));
        if (session.Map.Definition.Id != scenario.MapId || session.DifficultyId != scenario.DifficultyId ||
            session.ChallengeId != scenario.ChallengeId || saveJson is not null && session.IsCoOp != scenario.CoOp)
            throw new InvalidDataException("Imported checkpoint differs from the declared scenario.");
        if (scenario.CoOp) session.ConfigureCoOp(1);
        else session.ConfigureSolo();
        if (session.Speed != 1f)
            session.SetSpeed(1f);
        return session;
    }

    public PlanningActionRecord Apply(GameCommand command, string trigger = "immediate", long? requestedTick = null)
    {
        if (requestedTick is < 0 || requestedTick > Tick)
            throw new ArgumentOutOfRangeException(nameof(requestedTick));
        var before = Session.Economy.Credits;
        var duringWave = Session.Waves.IsActive;
        var resolved = command with { Sequence = _actions.Count + 1, ClientRequestId = _actions.Count + 1 };
        var result = ApplyLegalCommand(Session, Scenario, resolved);
        var action = new PlanningActionRecord
        {
            Tick = Tick,
            RequestedTick = requestedTick ?? Tick,
            ResolvedTick = Tick,
            Wave = Session.CurrentWave,
            DuringWave = duringWave,
            Trigger = trigger,
            Command = resolved,
            Accepted = result.Accepted,
            Reason = result.Reason,
            ActualCost = before - Session.Economy.Credits,
            CreditsBefore = before,
            CreditsAfter = Session.Economy.Credits,
            StateSignature = Signature
        };
        _actions.Add(action);
        return action;
    }

    internal static GameCommandResult ApplyLegalCommand(GameSession session, PlanningScenario scenario, GameCommand command)
    {
        if (command.Speed != 1f || command.Type == GameCommandType.SetSpeed)
            return GameCommandResult.Reject("Planning uses fixed 60 Hz steps at speed one.");
        if (command.Type == GameCommandType.StartWave && command.EarlyStartEligible is not null)
            return GameCommandResult.Reject("Early-call eligibility is derived from the live intermission.");
        if (!scenario.CoOp && command.PlayerId != 1)
            return GameCommandResult.Reject("Solo commands belong to player one.");
        if (!scenario.Endless && command.Type == GameCommandType.ContinueEndless)
            return GameCommandResult.Reject("The scenario does not request Endless continuation.");
        if (scenario.ForbidsSale(command.Type, session.Waves.IsActive))
            return GameCommandResult.Reject("This planning scenario permits sales only between waves.");
        return GameCommandProcessor.Apply(session, command);
    }

    public void Advance(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        _runner.RunTicks(ticks);
    }

    public IReadOnlyList<GameCommand> EnumerateLegalActions(IEnumerable<GameCommand> proposals)
    {
        var checkpoint = Capture();
        return proposals.Where(command => Restore(_content, checkpoint).Apply(command).Accepted).ToArray();
    }

    public PlanningCheckpoint Capture() => new()
    {
        Scenario = Scenario,
        BuildHash = PlanningFingerprint.Build(),
        ContentHash = _contentHash,
        SnapshotJson = JsonSerializer.Serialize(Session.CaptureCoOpState(Tick, 0, false), PlanningFingerprint.JsonOptions),
        DetachedProjectileTargetsJson = JsonSerializer.Serialize(PlanningFingerprint.CaptureDetachedProjectileTargets(Session),
            PlanningFingerprint.JsonOptions),
        OriginSaveJson = _originSaveJson,
        OriginSignature = _originSignature,
        Tick = Tick,
        StateSignature = Signature,
        Actions = _actions.ToArray()
    };

    public static PlannerSession Restore(GameContent content, PlanningCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != 1 || checkpoint.BuildHash != PlanningFingerprint.Build() ||
            checkpoint.ContentHash != PlanningFingerprint.SearchContent(content))
            throw new InvalidDataException("Checkpoint schema, build or content differs from this search.");
        if (checkpoint.Tick is < 0 or > 100_000_000 || checkpoint.Actions is null || checkpoint.Actions.Count > 1_000_000)
            throw new InvalidDataException("Checkpoint replay prefix exceeds supported bounds.");
        if (checkpoint.Scenario.ForbidCombatSales && checkpoint.Actions.Any(action =>
                action.Accepted && checkpoint.Scenario.ForbidsSale(action.Command.Type, action.DuringWave)))
            throw new InvalidDataException("Checkpoint contains an active-wave sale forbidden by its scenario.");
        try
        {
            var snapshot = PlanningFingerprint.Deserialize<CoOpStateSnapshot>(checkpoint.SnapshotJson);
            if (snapshot.Tick != checkpoint.Tick || snapshot.PendingCommands.Count != 0)
                throw new InvalidDataException("Planning checkpoints must end at a resolved command boundary.");
            // A projectile can outlive a target removed at the end of the previous update.
            // Its detached target remains part of the projectile's runtime state.
            var detached = PlanningFingerprint.Deserialize<List<EnemyRuntimeState>>(checkpoint.DetachedProjectileTargetsJson);
            var detachedIds = detached.Select(enemy => enemy.Id).ToHashSet();
            snapshot.Enemies.AddRange(detached);
            var session = GameSession.RestoreCoOpState(content, snapshot, 1);
            session.Enemies.RemoveAll(enemy => detachedIds.Contains(enemy.Id));
            if (!checkpoint.Scenario.CoOp) session.ConfigureSolo();
            var restored = new PlannerSession(content, session, checkpoint.Scenario, checkpoint.Tick,
                checkpoint.OriginSaveJson, checkpoint.OriginSignature, checkpoint.ContentHash, checkpoint.Actions)
                { RestorationMethod = detached.Count == 0 ? "snapshot" : "snapshot-with-detached-projectile-targets" };
            if (PlanningFingerprint.MatchesState(session, checkpoint.Tick, checkpoint.StateSignature)) return restored;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or JsonException)
        {
            // A runtime snapshot is only an acceleration cache; the legal command prefix
            // can reconstruct states outside its representable range without normalization.
        }
        return Reconstruct(content, checkpoint);
    }

    private static PlannerSession Reconstruct(GameContent content, PlanningCheckpoint checkpoint)
    {
        var session = CreateInitialSession(content, checkpoint.Scenario, checkpoint.OriginSaveJson);
        if (!PlanningFingerprint.MatchesState(session, 0, checkpoint.OriginSignature))
            throw new InvalidDataException("Checkpoint replay origin differs.");
        var runner = new DeterministicSessionRunner(session);
        foreach (var action in checkpoint.Actions)
        {
            if (action.Tick < runner.Tick || action.Tick > checkpoint.Tick || action.ResolvedTick != action.Tick ||
                action.RequestedTick < 0 || action.RequestedTick > action.ResolvedTick)
                throw new InvalidDataException("Checkpoint replay actions are out of order.");
            runner.RunTicks(checked((int)(action.Tick - runner.Tick)));
            var before = session.Economy.Credits;
            var result = ApplyLegalCommand(session, checkpoint.Scenario, action.Command);
            if (result.Accepted != action.Accepted || before != action.CreditsBefore ||
                session.Economy.Credits != action.CreditsAfter || before - session.Economy.Credits != action.ActualCost ||
                !PlanningFingerprint.MatchesState(session, runner.Tick, action.StateSignature))
                throw new InvalidDataException($"Checkpoint replay diverged at tick {runner.Tick} ({action.Command.Type}): {result.Reason}");
        }
        runner.RunTicks(checked((int)(checkpoint.Tick - runner.Tick)));
        var restored = new PlannerSession(content, session, checkpoint.Scenario, checkpoint.Tick,
            checkpoint.OriginSaveJson, checkpoint.OriginSignature, checkpoint.ContentHash, checkpoint.Actions)
            { RestorationMethod = "legal-prefix-reconstruction" };
        if (!PlanningFingerprint.MatchesState(session, checkpoint.Tick, checkpoint.StateSignature))
            throw new InvalidDataException("Neither runtime snapshot nor legal replay prefix reproduces this checkpoint.");
        return restored;
    }

    /// <summary>Revalidates a legal prefix against this build without trusting its cached runtime snapshot.</summary>
    public static PlannerSession Revalidate(GameContent content, PlanningCheckpoint checkpoint)
    {
        if (checkpoint.SchemaVersion != 1 || checkpoint.ContentHash != PlanningFingerprint.Content(content) ||
            checkpoint.Tick is < 0 or > 100_000_000 || checkpoint.Actions is null || checkpoint.Actions.Count > 1_000_000)
            throw new InvalidDataException("Checkpoint schema, content or replay bounds are incompatible.");
        var reconstructed = Reconstruct(content, checkpoint);
        var verification = PlanningReplay.Verify(content, reconstructed.ExportTrace());
        if (!verification.Verified)
            throw new InvalidDataException("Checkpoint prefix failed independent revalidation: " + verification.Reason);
        reconstructed.RestorationMethod = "independently-revalidated-legal-prefix";
        return reconstructed;
    }

    /// <summary>Checks the complete prefix under intermission-only selling and returns a checkpoint for subsequent branches.</summary>
    public static PlanningCheckpoint ForbidCombatSales(GameContent content, PlanningCheckpoint checkpoint) =>
        Revalidate(content, checkpoint with { Scenario = checkpoint.Scenario with { ForbidCombatSales = true } }).Capture();

    public PlanningTrace ExportTrace() => new()
    {
        Scenario = Scenario,
        BuildHash = PlanningFingerprint.Build(),
        ContentHash = _contentHash,
        OriginSaveJson = _originSaveJson,
        OriginSignature = _originSignature,
        Actions = _actions.Where(action => action.Accepted).ToArray(),
        FinalTick = Tick,
        FinalSignature = Signature,
        FinalWave = Session.CurrentWave,
        FinalLives = Session.Economy.Lives,
        FinalCredits = Session.Economy.Credits,
        CampaignVictory = CampaignVictory,
        HorizonSurvived = HorizonSurvived,
        Defeat = Session.IsDefeat
    };
}
