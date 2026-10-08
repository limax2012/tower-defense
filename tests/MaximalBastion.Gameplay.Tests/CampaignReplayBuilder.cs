using System.Text.Json;
using MaximalBastion;
using MaximalBastion.Data;
using MaximalBastion.Gameplay;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation;

internal static class CampaignReplayBuilder
{
    private const float StepSeconds = 1f / 60f;

    public static CampaignReplayDocument Create(GameContent content, string fixtureDirectory)
    {
        var plan = StrategyArtifactStore.LoadPlan(Path.Combine(fixtureDirectory, "CinderworksVictory.strategy.json"));
        var transitions = JsonSerializer.Deserialize<List<CampaignTransition>>(
            File.ReadAllText(Path.Combine(fixtureDirectory, "CinderworksVictory.wave29.json")), ContentJson.Options)
            ?? throw new InvalidDataException("Campaign transitions are missing.");
        var session = new GameSession(content, plan.MapId, plan.DifficultyId, plan.ChallengeId);
        var host = new AuthoritativeCommandHost();
        var replay = new CampaignReplayDocument
        {
            MapId = plan.MapId, DifficultyId = plan.DifficultyId, ChallengeId = plan.ChallengeId,
            InitialSave = session.CaptureSaveGame(), RecordedRuntime = Environment.Version.ToString()
        };
        long tick = 0, request = 0;

        bool Submit(GameCommand command)
        {
            if (command.Type == GameCommandType.SetTargetMode &&
                session.Towers.FirstOrDefault(tower => tower.Id == command.EntityId)?.TargetMode == command.TargetMode)
                return true;
            var receipt = host.Submit(session, command with { ClientRequestId = ++request, PlayerId = 1 });
            if (receipt.Accepted)
                replay.Commands.Add(new ScheduledCommandState { Tick = tick, Command = receipt.Command });
            return receipt.Accepted;
        }

        void Required(GameCommand command)
        {
            if (!Submit(command))
                throw new InvalidOperationException($"Campaign command {command.Type} for {command.EntityId} rejected at tick {tick}.");
        }

        foreach (var wave in plan.Waves)
        {
            if (wave.Wave == 29)
            {
                foreach (var transition in transitions)
                {
                    if (transition.Action == "Sell")
                        Required(new GameCommand { Type = GameCommandType.SellTower, EntityId = transition.Id });
                    else if (transition.Action == "Build")
                    {
                        Required(new GameCommand { Type = GameCommandType.PlaceTower,
                            TowerDefinitionId = "siege_mortar", X = transition.X, Y = transition.Y });
                        var id = session.Towers.Last().Id;
                        Required(new GameCommand { Type = GameCommandType.ChooseDoctrine, EntityId = id, DoctrineId = transition.Doctrine });
                        Required(new GameCommand { Type = GameCommandType.SpecializeTower, EntityId = id, SpecializationId = transition.Spec });
                    }
                    else throw new InvalidDataException("Unknown campaign transition: " + transition.Action);
                }
            }
            var player = new AutoPlayer(session, wave.StrategyOverride ?? plan.DefaultStrategy, wave.DecisionSeed,
                new SimulationOptions { WavePlan = wave, HoldFootprint = wave.Wave >= 29 }, Submit);
            player.PrepareForWave(session);
            Required(new GameCommand { Type = GameCommandType.StartWave });
            float reactionTimer = 0;
            var waveTicks = 0;
            while (!session.IsDefeat && !session.IsVictory && (session.Waves.IsActive || session.Enemies.Count > 0))
            {
                if (++waveTicks > 30000) throw new InvalidOperationException($"Campaign wave {wave.Wave} exceeded its tick bound.");
                session.Update(StepSeconds);
                tick++;
                reactionTimer += StepSeconds;
                if (!session.IsDefeat && session.Waves.IsActive && reactionTimer >= 1f)
                {
                    player.ReactDuringWave(session);
                    reactionTimer = 0;
                }
            }
            var result = CaptureWave(session, wave.Wave, tick);
            replay.Waves.Add(result);
            Console.WriteLine(JsonSerializer.Serialize(result, ContentJson.Options));
            if (session.IsDefeat || session.IsVictory) break;
        }
        replay.TickCount = tick;
        replay.Victory = session.IsVictory;
        replay.FinalChecksum = SessionChecksum.Compute(session, tick);
        if (!replay.Victory || replay.Waves.Count != 30)
            throw new InvalidOperationException("Campaign strategy did not complete all 30 authored waves.");
        return replay;
    }

    public static CampaignReplayVerification Verify(GameContent content, CampaignReplayDocument replay)
    {
        if (replay.TickCount is < 1 or > 120000 || replay.Commands.Count is < 1 or > 4096 ||
            replay.Waves.Count != 30 || !replay.Victory)
            throw new InvalidDataException("Campaign replay bounds or expected outcome are invalid.");
        var session = new GameSession(content, replay.MapId, replay.DifficultyId, replay.ChallengeId);
        var savedInitial = GameSession.RestoreSaveGame(content, replay.InitialSave);
        if (SessionChecksum.Compute(session, 0) != SessionChecksum.Compute(savedInitial, 0))
            throw new InvalidDataException("Campaign replay must begin with an unmodified fresh session.");
        var host = new AuthoritativeCommandHost();
        var commandIndex = 0;
        var waveIndex = 0;
        long previousTick = -1;
        foreach (var scheduled in replay.Commands)
        {
            if (scheduled.Tick < previousTick || scheduled.Tick < 0 || scheduled.Tick >= replay.TickCount)
                throw new InvalidDataException("Campaign commands must be in tick order within the replay.");
            previousTick = scheduled.Tick;
        }
        for (long tick = 0; tick < replay.TickCount; tick++)
        {
            while (commandIndex < replay.Commands.Count && replay.Commands[commandIndex].Tick == tick)
            {
                var command = replay.Commands[commandIndex++].Command;
                var receipt = host.Submit(session, command);
                if (!receipt.Accepted || receipt.Duplicate || receipt.Command.Sequence != command.Sequence)
                    throw new InvalidOperationException($"Replay rejected or resequenced {command.Type} at tick {tick}: {receipt.Reason}");
            }
            session.Update(StepSeconds);
            if (waveIndex < replay.Waves.Count && replay.Waves[waveIndex].Tick == tick + 1)
            {
                var expected = replay.Waves[waveIndex++];
                if (CaptureWave(session, session.CurrentWave, tick + 1) != expected)
                    throw new InvalidOperationException($"Campaign wave {expected.Wave} outcome differed at tick {tick + 1}.");
            }
        }
        var checksum = SessionChecksum.Compute(session, replay.TickCount);
        if (commandIndex != replay.Commands.Count || waveIndex != 30 || !session.IsVictory || session.IsDefeat ||
            session.CurrentWave != 30 || session.Economy.Lives <= 0 || checksum != replay.FinalChecksum)
            throw new InvalidOperationException("Fresh-session command replay did not reproduce the campaign victory exactly.");
        var victory = CaptureWave(session, 30, replay.TickCount);
        var request = replay.Commands.Max(command => command.Command.ClientRequestId);
        var continued = host.Submit(session, new GameCommand { Type = GameCommandType.ContinueEndless,
            PlayerId = 1, ClientRequestId = ++request });
        var started = host.Submit(session, new GameCommand { Type = GameCommandType.StartWave,
            PlayerId = 1, ClientRequestId = ++request });
        var sawEnemy = false;
        for (var tick = 0; tick < 600; tick++)
        {
            session.Update(StepSeconds);
            sawEnemy |= session.Enemies.Count > 0;
        }
        if (!continued.Accepted || !started.Accepted || !session.IsEndlessMode || session.CurrentWave != 31 || !sawEnemy)
            throw new InvalidOperationException("Campaign victory did not enter an active generated Endless wave 31.");
        VerifyController(content, GameplayJson.Serialize(replay), checksum);
        return new CampaignReplayVerification(replay.Commands.Count, replay.TickCount, victory.Lives,
            victory.TotalKills, checksum, true);
    }

    private static void VerifyController(GameContent content, string json, string expectedChecksum)
    {
        var controller = new CampaignReplayController(json);
        var session = controller.CreateSession(content);
        var host = new AuthoritativeCommandHost();
        long tick = 0;
        while (!session.IsVictory && !session.IsDefeat && tick < 150000)
        {
            if (!controller.PrepareTick(session, tick, command => host.Submit(session, command).Accepted))
                throw new InvalidOperationException(controller.Failure);
            session.Update(StepSeconds);
            tick++;
        }
        if (!session.IsVictory || !controller.CommandsComplete || controller.TotalDelayTicks != 0 ||
            controller.RejectedAttempts != 0 || SessionChecksum.Compute(session, tick) != expectedChecksum)
            throw new InvalidOperationException("Campaign acceptance controller changed a same-backend replay.");
    }

    private static CampaignWaveResult CaptureWave(GameSession session, int wave, long tick) =>
        new(wave, tick, session.Economy.Lives, session.Economy.Credits, session.IsDefeat,
            session.IsVictory, session.Economy.TotalKills);
}

internal sealed class CampaignReplayDocument
{
    public string MapId { get; set; } = "";
    public string DifficultyId { get; set; } = "";
    public string ChallengeId { get; set; } = "";
    public SaveGameData InitialSave { get; set; } = new();
    public List<ScheduledCommandState> Commands { get; set; } = new();
    public long TickCount { get; set; }
    public bool Victory { get; set; }
    public List<CampaignWaveResult> Waves { get; set; } = new();
    public string FinalChecksum { get; set; } = "";
    public string RecordedRuntime { get; set; } = "";
}

internal sealed class CampaignTransition
{
    public string Action { get; set; } = "";
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string Doctrine { get; set; } = "";
    public string Spec { get; set; } = "";
}

internal sealed record CampaignWaveResult(int Wave, long Tick, int Lives, int Credits,
    bool IsDefeat, bool IsVictory, int TotalKills);
internal sealed record CampaignReplayVerification(int CommandsAccepted, long TicksRun, int Lives,
    int TotalKills, string FinalChecksum, bool EndlessWave31Started);
