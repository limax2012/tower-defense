using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Gameplay;

/// <summary>A bounded, renderer-free replay that can be checked on each host runtime.</summary>
public sealed class RuntimeConformanceFixture
{
    public int SchemaVersion { get; set; } = 1;
    public List<RuntimeConformanceScenario> Scenarios { get; set; } = new();
}

public sealed class RuntimeConformanceScenario
{
    public string Name { get; set; } = "";
    public string InitialSaveJson { get; set; } = "";
    public int TickCount { get; set; }
    public List<RuntimeConformanceCommand> Commands { get; set; } = new();
    public List<RuntimeConformanceSample> Samples { get; set; } = new();
    public List<int> SnapshotTicks { get; set; } = new();
}

public sealed class RuntimeConformanceCommand
{
    public int Tick { get; set; }
    public GameCommand Command { get; set; } = new();
}

public sealed class RuntimeConformanceSample
{
    public long Tick { get; set; }
    public string Checksum { get; set; } = "";
}

public sealed class RuntimeConformanceResult
{
    public string Runtime { get; set; } = Environment.Version.ToString();
    public bool Passed => Failures.Count == 0;
    public int ScenariosCompleted { get; set; }
    public int SamplesChecked { get; set; }
    public int SnapshotsChecked { get; set; }
    public long TicksRun { get; set; }
    public List<string> Failures { get; set; } = new();
}

public static class RuntimeConformance
{
    public static RuntimeConformanceResult Verify(GameContent content, string fixtureJson) =>
        Verify(content, JsonSerializer.Deserialize<RuntimeConformanceFixture>(fixtureJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime fixture is empty."));

    public static RuntimeConformanceResult Verify(GameContent content, RuntimeConformanceFixture fixture)
    {
        if (fixture.SchemaVersion != 1 || fixture.Scenarios.Count is < 1 or > 128)
            throw new InvalidDataException("Unsupported runtime fixture.");
        var result = new RuntimeConformanceResult();
        foreach (var scenario in fixture.Scenarios)
        {
            try { VerifyScenario(content, scenario, result); }
            catch (Exception exception) { result.Failures.Add(scenario.Name + ": " + exception.GetBaseException().Message); }
        }
        return result;
    }

    private static void VerifyScenario(GameContent content, RuntimeConformanceScenario scenario, RuntimeConformanceResult result)
    {
        if (scenario.TickCount is < 1 or > 120000 || scenario.Commands.Count > 4096 || scenario.Samples.Count > 120000)
            throw new InvalidDataException("Runtime scenario exceeds its replay bounds.");
        if (scenario.Commands.Any(x => x.Tick < 0 || x.Tick >= scenario.TickCount) ||
            scenario.Samples.Any(x => x.Tick < 1 || x.Tick > scenario.TickCount) ||
            scenario.SnapshotTicks.Any(x => x < 1 || x > scenario.TickCount))
            throw new InvalidDataException("Runtime scenario contains an out-of-range tick.");
        var save = JsonSerializer.Deserialize<SaveGameData>(scenario.InitialSaveJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime scenario has no initial checkpoint.");
        var session = GameSession.RestoreSaveGame(content, save);
        session.ConfigureCoOp(1);
        var runner = new DeterministicSessionRunner(session);
        var commands = scenario.Commands.OrderBy(x => x.Tick).ThenBy(x => x.Command.Sequence).ToArray();
        var samples = scenario.Samples.ToDictionary(x => x.Tick);
        var snapshots = new HashSet<int>(scenario.SnapshotTicks);
        var commandIndex = 0;
        while (runner.Tick < scenario.TickCount)
        {
            while (commandIndex < commands.Length && commands[commandIndex].Tick <= runner.Tick + 120)
            {
                var command = commands[commandIndex++];
                if (!runner.Schedule(command.Tick, command.Command))
                    throw new InvalidDataException($"Command {command.Command.Sequence} could not be scheduled.");
            }
            runner.RunTicks(1);
            result.TicksRun++;
            var checksum = SessionChecksum.Compute(session, runner.Tick);
            if (samples.TryGetValue(runner.Tick, out var sample))
            {
                if (!string.Equals(checksum, sample.Checksum, StringComparison.Ordinal))
                    throw new InvalidDataException($"Tick {runner.Tick}: {checksum}, expected {sample.Checksum}.");
                result.SamplesChecked++;
            }
            if (!snapshots.Contains((int)runner.Tick)) continue;
            var snapshot = session.CaptureCoOpState(runner.Tick, 0, false);
            snapshot.PendingCommands = runner.CapturePendingCommands();
            var json = JsonSerializer.Serialize(snapshot, ContentJson.Options);
            var restored = JsonSerializer.Deserialize<CoOpStateSnapshot>(json, ContentJson.Options)!;
            session = GameSession.RestoreCoOpState(content, restored, 1);
            runner = new DeterministicSessionRunner(session, restored.Tick);
            runner.RestorePendingCommands(restored.PendingCommands);
            if (SessionChecksum.Compute(session, runner.Tick) != checksum)
                throw new InvalidDataException($"Snapshot changed state at tick {runner.Tick}.");
            if (JsonSerializer.Serialize(session.CaptureCoOpState(runner.Tick, 0, false).Projectiles, ContentJson.Options) !=
                JsonSerializer.Serialize(snapshot.Projectiles, ContentJson.Options))
                throw new InvalidDataException($"Projectile fields changed at tick {runner.Tick}.");
            result.SnapshotsChecked++;
        }
        if (scenario.Samples.Count == 0) throw new InvalidDataException("Runtime scenario has no checksum samples.");
        result.ScenariosCompleted++;
    }
}
