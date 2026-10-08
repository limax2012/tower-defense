using System.Text.Json;
using System.Collections;
using System.Globalization;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Gameplay;

/// <summary>A bounded, renderer-free replay that can be checked on each host runtime.</summary>
public sealed class RuntimeConformanceFixture
{
    public int SchemaVersion { get; set; } = 1;
    public string ReferenceRuntime { get; set; } = "";
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
    public string InitialChecksum { get; set; } = "";
    public SortedDictionary<string, string> InitialFields { get; set; } = new(StringComparer.Ordinal);
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
    public SortedDictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);
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
    /// <summary>Records a distinct reference for the current execution backend; it does not establish cross-backend compatibility.</summary>
    public static string RecordReference(GameContent content, string scenarioJson)
    {
        var fixture = JsonSerializer.Deserialize<RuntimeConformanceFixture>(scenarioJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime fixture is empty.");
        if (fixture.SchemaVersion != 1 || fixture.Scenarios.Count is < 1 or > 128)
            throw new InvalidDataException("Unsupported runtime fixture.");
        fixture.ReferenceRuntime = Environment.Version.ToString();
        foreach (var scenario in fixture.Scenarios)
        {
            ValidateScenarioBounds(scenario);
            var save = JsonSerializer.Deserialize<SaveGameData>(scenario.InitialSaveJson, ContentJson.Options)
                ?? throw new InvalidDataException("Runtime scenario has no initial checkpoint.");
            var session = GameSession.RestoreSaveGame(content, save);
            session.ConfigureCoOp(1);
            scenario.InitialChecksum = SessionChecksum.Compute(session, 0);
            scenario.InitialFields = DescribeFields(session.CaptureCoOpState(0, 0, false));
            var runner = new DeterministicSessionRunner(session);
            var commands = scenario.Commands.OrderBy(x => x.Tick).ThenBy(x => x.Command.Sequence).ToArray();
            var samples = scenario.Samples.ToDictionary(x => x.Tick);
            var index = 0;
            while (runner.Tick < scenario.TickCount)
            {
                while (index < commands.Length && commands[index].Tick <= runner.Tick + 120)
                {
                    var command = commands[index++];
                    if (!runner.Schedule(command.Tick, command.Command))
                        throw new InvalidDataException($"Command {command.Command.Sequence} could not be scheduled.");
                }
                runner.RunTicks(1);
                if (!samples.TryGetValue(runner.Tick, out var sample)) continue;
                sample.Checksum = SessionChecksum.Compute(session, runner.Tick);
                if (sample.Fields.Count > 0) sample.Fields = DescribeFields(session.CaptureCoOpState(runner.Tick, 0, false));
            }
        }
        return JsonSerializer.Serialize(fixture, ContentJson.Options);
    }

    public static SortedDictionary<string, string> DescribeFields(object state)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        Describe(state, "$", fields);
        return fields;
    }

    private static void Describe(object? value, string path, SortedDictionary<string, string> fields)
    {
        if (value is null) { fields[path] = "null"; return; }
        var type = value.GetType();
        if (value is float number) { fields[path] = "f32:" + BitConverter.SingleToInt32Bits(number).ToString("X8") + ":" + number.ToString("R", CultureInfo.InvariantCulture); return; }
        if (value is double precision) { fields[path] = "f64:" + BitConverter.DoubleToInt64Bits(precision).ToString("X16") + ":" + precision.ToString("R", CultureInfo.InvariantCulture); return; }
        if (value is string || type.IsPrimitive || type.IsEnum || value is decimal || value is DateTime)
        { fields[path] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; return; }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary) Describe(entry.Value, path + "[" + entry.Key + "]", fields);
            fields[path + ".Count"] = dictionary.Count.ToString(CultureInfo.InvariantCulture);
            return;
        }
        if (value is IEnumerable sequence)
        {
            var index = 0;
            foreach (var entry in sequence) Describe(entry, path + "[" + index++ + "]", fields);
            fields[path + ".Count"] = index.ToString(CultureInfo.InvariantCulture);
            return;
        }
        const System.Reflection.BindingFlags members = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        foreach (var property in type.GetProperties(members).Where(x => x.CanRead && x.GetIndexParameters().Length == 0))
            Describe(property.GetValue(value), path + "." + property.Name, fields);
        foreach (var field in type.GetFields(members)) Describe(field.GetValue(value), path + "." + field.Name, fields);
    }

    private static string Differences(GameSession session, long tick, SortedDictionary<string, string> expected)
    {
        if (expected.Count == 0) return " No diagnostic state fields in fixture.";
        var actual = DescribeFields(session.CaptureCoOpState(tick, 0, false));
        var differences = expected.Keys.Union(actual.Keys).Where(key =>
            !expected.TryGetValue(key, out var expectedValue) || !actual.TryGetValue(key, out var actualValue) ||
            // The raw bits, rather than runtime-specific shortest decimal formatting, are authoritative.
            (expectedValue.StartsWith("f32:") || expectedValue.StartsWith("f64:")
                ? expectedValue.Split(':')[1] != actualValue.Split(':')[1] : expectedValue != actualValue));
        return " Fields: " + string.Join("; ", differences.Take(24).Select(key => key + "=" +
            (actual.TryGetValue(key, out var value) ? value : "missing") + " expected " +
            (expected.TryGetValue(key, out value) ? value : "missing")));
    }

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
        ValidateScenarioBounds(scenario);
        var save = JsonSerializer.Deserialize<SaveGameData>(scenario.InitialSaveJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime scenario has no initial checkpoint.");
        var session = GameSession.RestoreSaveGame(content, save);
        session.ConfigureCoOp(1);
        var runner = new DeterministicSessionRunner(session);
        if (scenario.InitialChecksum.Length > 0 && SessionChecksum.Compute(session, 0) != scenario.InitialChecksum)
            throw new InvalidDataException("Initial state checksum differs." + Differences(session, 0, scenario.InitialFields));
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
                    throw new InvalidDataException($"Tick {runner.Tick}: {checksum}, expected {sample.Checksum}." + Differences(session, runner.Tick, sample.Fields));
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

    private static void ValidateScenarioBounds(RuntimeConformanceScenario scenario)
    {
        if (scenario.TickCount is < 1 or > 120000 || scenario.Commands.Count > 4096 || scenario.Samples.Count > 120000)
            throw new InvalidDataException("Runtime scenario exceeds its replay bounds.");
        if (scenario.Commands.Any(x => x.Tick < 0 || x.Tick >= scenario.TickCount) ||
            scenario.Samples.Any(x => x.Tick < 1 || x.Tick > scenario.TickCount) ||
            scenario.SnapshotTicks.Any(x => x < 1 || x > scenario.TickCount))
            throw new InvalidDataException("Runtime scenario contains an out-of-range tick.");
    }
}
