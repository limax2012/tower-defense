using MaximalBastion.Data;
using MaximalBastion.Multiplayer;

namespace MaximalBastion.Simulation.Planning;

/// <summary>Plays an authenticated command trace with presentation-independent 60 Hz simulation.</summary>
public sealed class PlanningTracePlayer
{
    private int _actionIndex;
    private bool _completed;

    public GameSession Session { get; }
    public PlanningTrace Trace { get; }
    public ReplayVerification Verification { get; }
    public long Tick { get; private set; }
    public long FinalTick => Trace.FinalTick;
    public int AppliedActions => _actionIndex;
    public bool IsComplete => _completed;
    public double Progress => FinalTick == 0 ? 1 : (double)Tick / FinalTick;

    private PlanningTracePlayer(GameContent content, PlanningTrace trace, ReplayVerification verification)
    {
        Trace = trace;
        Verification = verification;
        Session = PlannerSession.CreateInitialSession(content, trace.Scenario, trace.OriginSaveJson);
        if (!PlanningFingerprint.MatchesState(Session, 0, trace.OriginSignature))
            throw new InvalidDataException("Playback origin differs from the authenticated trace.");
        ApplyBoundaryActions();
        if (FinalTick == 0) VerifyFinalState();
    }

    /// <summary>Verifies the complete trace before exposing a new playback session.</summary>
    public static PlanningTracePlayer Create(GameContent content, PlanningTrace trace, bool requireCurrentBuild = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(trace);
        if (trace.Scenario is null || trace.Actions is null || trace.Actions.Count > 1_000_000 ||
            trace.Actions.Any(action => action is null || action.Command is null))
            throw new InvalidDataException("Playback requires a scenario and a complete, bounded action list.");
        var retained = trace with { Actions = Array.AsReadOnly(trace.Actions.ToArray()) };
        var verification = PlanningReplay.Verify(content, retained, requireCurrentBuild);
        if (!verification.Verified)
            throw new InvalidDataException("Playback trace failed independent verification: " + verification.Reason);
        return new PlanningTracePlayer(content, retained, verification);
    }

    public void Advance(int ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        AdvanceTo(Math.Min(FinalTick, Tick + ticks));
    }

    /// <summary>Advances forward and applies every command at the sampled boundary, including the final tick.</summary>
    public void AdvanceTo(long targetTick)
    {
        if (targetTick < Tick) throw new ArgumentOutOfRangeException(nameof(targetTick), "Playback cannot move backwards.");
        var destination = Math.Min(FinalTick, targetTick);
        while (Tick < destination)
        {
            Session.Update(DeterministicSessionRunner.FixedStepSeconds);
            Tick++;
            ApplyBoundaryActions();
        }
        if (Tick == FinalTick) VerifyFinalState();
    }

    private void ApplyBoundaryActions()
    {
        while (_actionIndex < Trace.Actions.Count && Trace.Actions[_actionIndex].Tick == Tick)
        {
            var action = Trace.Actions[_actionIndex];
            var before = Session.Economy.Credits;
            var duringWave = Session.Waves.IsActive;
            var result = PlannerSession.ApplyLegalCommand(Session, Trace.Scenario, action.Command);
            var after = Session.Economy.Credits;
            if (!result.Accepted || !action.Accepted || before != action.CreditsBefore || after != action.CreditsAfter ||
                before - after != action.ActualCost || duringWave != action.DuringWave || Session.CurrentWave != action.Wave ||
                !PlanningFingerprint.MatchesState(Session, Tick, action.StateSignature))
                throw new InvalidDataException($"Playback diverged at action {_actionIndex}, tick {Tick} ({action.Command.Type}).");
            _actionIndex++;
        }
    }

    /// <summary>Checks the actual playback state, including any changes made by a presentation caller.</summary>
    public ReplayVerification VerifyFinalState()
    {
        if (Tick != FinalTick || _actionIndex != Trace.Actions.Count)
            throw new InvalidOperationException("Playback has not reached its final command boundary.");
        if (!PlanningFingerprint.MatchesState(Session, Tick, Trace.FinalSignature) ||
            Session.CurrentWave != Trace.FinalWave || Session.Economy.Lives != Trace.FinalLives ||
            Session.Economy.Credits != Trace.FinalCredits || Session.IsDefeat != Trace.Defeat ||
            PlanningReplay.IsCampaignVictory(Session, Trace.Scenario) != Trace.CampaignVictory ||
            PlanningReplay.IsHorizonSurvived(Session, Trace.Scenario) != Trace.HorizonSurvived)
            throw new InvalidDataException("Playback final state differs from the authenticated trace.");
        _completed = true;
        return Verification with
        {
            ActionsChecked = _actionIndex,
            TicksChecked = Tick,
            FinalSignature = Trace.FinalSignature,
            Reason = "Playback final state matches the independently verified command trace."
        };
    }
}
