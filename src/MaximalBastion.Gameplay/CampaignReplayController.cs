using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Gameplay;

/// <summary>
/// Executes a campaign command playbook with legal wave-boundary waits and explicit tactical retries.
/// This is an acceptance driver, not a cross-backend lockstep replay.
/// </summary>
public sealed class CampaignReplayController
{
    private const int MaximumWaitTicks = 3600;
    private readonly List<PendingCommand> _pending = new();
    private readonly List<string> _events = new();
    private int _index;
    private long _request;
    private long _lastTick = -1;
    private long _waveWaitStarted = -1;

    public CampaignReplayController(string json)
    {
        Data = GameplayJson.Deserialize<CampaignReplayData>(json)
            ?? throw new InvalidDataException("Campaign replay is missing.");
        if (Data.Commands is null || Data.Commands.Count is < 1 or > 4096 || Data.TickCount is < 1 or > 120000)
            throw new InvalidDataException("Campaign replay bounds are invalid.");
        long previousTick = -1;
        foreach (var scheduled in Data.Commands)
        {
            if (scheduled is null || scheduled.Tick < previousTick || scheduled.Tick < 0 ||
                scheduled.Tick >= Data.TickCount || !GameCommandValidator.IsStructurallyValid(scheduled.Command))
                throw new InvalidDataException("Campaign commands must be valid and ordered within the replay bounds.");
            previousTick = scheduled.Tick;
        }
    }

    public CampaignReplayData Data { get; }
    public long ReplayTick { get; private set; }
    public long TotalDelayTicks { get; private set; }
    public long WaveWaitTicks => TotalDelayTicks;
    public int AcceptedCommands { get; private set; }
    public int RejectedAttempts { get; private set; }
    public int DeferredCommands { get; private set; }
    public int PendingCommands => _pending.Count;
    public bool CommandsComplete => _index == Data.Commands.Count && _pending.Count == 0;
    public string Failure { get; private set; } = "";
    public IReadOnlyList<string> Events => _events;

    public GameSession CreateSession(GameContent content)
    {
        var session = new GameSession(content, Data.MapId, Data.DifficultyId, Data.ChallengeId);
        var saved = GameSession.RestoreSaveGame(content, Data.InitialSave);
        if (SessionChecksum.Compute(session, 0) != SessionChecksum.Compute(saved, 0))
            throw new InvalidDataException("Campaign acceptance must start from an unmodified fresh session.");
        return session;
    }

    /// <summary>
    /// Call once before each 60 Hz simulation update with consecutive zero-based ticks.
    /// The executor applies a validated command synchronously and returns its actual acceptance.
    /// </summary>
    public bool PrepareTick(GameSession session, long tick, Func<GameCommand, bool> execute)
    {
        if (Failure.Length != 0) return false;
        if (tick != _lastTick + 1) return Fail("Campaign ticks must be consecutive and start at zero.");
        _lastTick = tick;
        if (tick > 150000) return Fail("Campaign exceeded its runtime tick bound.");
        if (session.IsDefeat) return Fail($"Campaign defeated on wave {session.CurrentWave} at tick {tick}.");
        if (session.IsVictory)
            return CommandsComplete || Fail("Campaign ended with unaccepted commands.");
        ReplayTick = tick - TotalDelayTicks;
        var waitingForWave = false;
        for (var index = _index; index < Data.Commands.Count && Data.Commands[index].Tick == ReplayTick; index++)
            waitingForWave |= Data.Commands[index].Command.Type == GameCommandType.StartWave && !session.CanStartWave;
        if (waitingForWave)
        {
            if (_waveWaitStarted < 0)
            {
                _waveWaitStarted = tick;
                _events.Add($"Tick {tick}: waiting for wave {session.CurrentWave} to clear before the next start group.");
            }
            TotalDelayTicks++;
            if (tick - _waveWaitStarted > MaximumWaitTicks) return Fail("Wave-start wait exceeded its bound.");
        }
        else
        {
            if (_waveWaitStarted >= 0)
            {
                _events.Add($"Tick {tick}: wave-start group resumed after {tick - _waveWaitStarted} ticks.");
                _waveWaitStarted = -1;
            }
            while (_index < Data.Commands.Count && Data.Commands[_index].Tick == ReplayTick)
                _pending.Add(new PendingCommand(Data.Commands[_index++].Command, tick));
        }
        for (var index = 0; index < _pending.Count;)
        {
            var pending = _pending[index];
            var command = pending.Command with { Sequence = 0, ClientRequestId = ++_request };
            if (execute(command))
            {
                AcceptedCommands++;
                if (pending.Deferred)
                    _events.Add($"Tick {tick}: accepted deferred {command.Type} after {tick - pending.FirstTick} ticks.");
                _pending.RemoveAt(index);
                continue;
            }
            RejectedAttempts++;
            if (command.Type is not (GameCommandType.DeployEmergencyDefense or GameCommandType.OverdriveTower))
                return Fail($"Tick {tick}: {command.Type} was rejected; dependent command order cannot be changed.");
            if (!pending.Deferred)
            {
                pending.Deferred = true;
                DeferredCommands++;
                _events.Add($"Tick {tick}: deferring rejected {command.Type}; the command remains pending.");
            }
            if (tick - pending.FirstTick > MaximumWaitTicks)
                return Fail($"Tick {tick}: deferred {command.Type} exceeded its retry bound.");
            index++;
        }
        return true;
    }

    private bool Fail(string message)
    {
        Failure = message;
        _events.Add(message);
        return false;
    }

    private sealed class PendingCommand
    {
        public PendingCommand(GameCommand command, long firstTick) { Command = command; FirstTick = firstTick; }
        public GameCommand Command { get; }
        public long FirstTick { get; }
        public bool Deferred { get; set; }
    }
}

public sealed class CampaignReplayData
{
    public string MapId { get; set; } = "";
    public string DifficultyId { get; set; } = "";
    public string ChallengeId { get; set; } = "";
    public SaveGameData InitialSave { get; set; } = new();
    public List<ScheduledCommandState> Commands { get; set; } = new();
    public long TickCount { get; set; }
    public string RecordedRuntime { get; set; } = "";
    public string FinalChecksum { get; set; } = "";
}
