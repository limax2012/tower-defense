using System.Globalization;
using System.Text.Json;
using MaximalBastion.Data;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;

namespace MaximalBastion.Gameplay;

/// <summary>Cross-runtime observations, separate from the strict lockstep checksum gate.</summary>
public sealed class RuntimeBehaviorComparisonResult
{
    public string Runtime { get; set; } = Environment.Version.ToString();
    public int ScenariosCompleted { get; set; }
    public long TicksRun { get; set; }
    public int ChecksumSamples { get; set; }
    public int ChecksumMismatches { get; set; }
    public int StateSamples { get; set; }
    public long NumericFieldsCompared { get; set; }
    public long DiscreteFieldsCompared { get; set; }
    public long DiscreteDifferences { get; set; }
    public long OutOfToleranceNumericFields { get; set; }
    public double AbsoluteTolerance { get; set; } = 0.001;
    public double RelativeTolerance { get; set; } = 0.00001;
    public double MaximumAbsoluteDifference { get; set; }
    public double MaximumRelativeDifference { get; set; }
    public int SnapshotsChecked { get; set; }
    public List<string> FinalOutcomeDifferences { get; set; } = new();
    public List<string> Differences { get; set; } = new();
    public List<string> Failures { get; set; } = new();
    public bool FinalOutcomesMatch => ScenariosCompleted > 0 && Failures.Count == 0 && FinalOutcomeDifferences.Count == 0;
    public bool AllSamplesMatchWithinTolerance => FinalOutcomesMatch && DiscreteDifferences == 0 && OutOfToleranceNumericFields == 0;
}

public static class RuntimeBehaviorComparison
{
    public static RuntimeBehaviorComparisonResult Compare(GameContent content, string fixtureJson)
    {
        var fixture = JsonSerializer.Deserialize<RuntimeConformanceFixture>(fixtureJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime fixture is empty.");
        if (fixture.SchemaVersion != 1 || fixture.Scenarios.Count is < 1 or > 128)
            throw new InvalidDataException("Unsupported runtime fixture.");
        var result = new RuntimeBehaviorComparisonResult();
        foreach (var scenario in fixture.Scenarios)
        {
            try { Replay(content, scenario, result); }
            catch (Exception exception) { result.Failures.Add(scenario.Name + ": " + exception.GetBaseException().Message); }
        }
        return result;
    }

    private static void Replay(GameContent content, RuntimeConformanceScenario scenario, RuntimeBehaviorComparisonResult result)
    {
        if (scenario.TickCount is < 1 or > 120000 || scenario.Commands.Count > 4096 || scenario.Samples.Count is < 1 or > 120000)
            throw new InvalidDataException("Runtime scenario exceeds its replay bounds.");
        if (scenario.Commands.Any(x => x.Tick < 0 || x.Tick >= scenario.TickCount) ||
            scenario.Samples.Any(x => x.Tick < 1 || x.Tick > scenario.TickCount) ||
            scenario.SnapshotTicks.Any(x => x < 1 || x > scenario.TickCount))
            throw new InvalidDataException("Runtime scenario contains an out-of-range tick.");
        var save = JsonSerializer.Deserialize<SaveGameData>(scenario.InitialSaveJson, ContentJson.Options)
            ?? throw new InvalidDataException("Runtime scenario has no initial checkpoint.");
        var session = GameSession.RestoreSaveGame(content, save);
        session.ConfigureCoOp(1);
        if (SessionChecksum.Compute(session, 0) != scenario.InitialChecksum)
            throw new InvalidDataException("Initial state checksum differs.");
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
                result.ChecksumSamples++;
                if (checksum != sample.Checksum) result.ChecksumMismatches++;
                if (sample.Fields.Count > 0)
                {
                    CompareFields(scenario.Name, runner.Tick, sample.Fields,
                        RuntimeConformance.DescribeFields(session.CaptureCoOpState(runner.Tick, 0, false)), result,
                        runner.Tick == scenario.TickCount);
                    result.StateSamples++;
                }
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
        if (!samples.TryGetValue(scenario.TickCount, out var finalSample) || finalSample.Fields.Count == 0)
            throw new InvalidDataException("Runtime scenario has no final diagnostic state.");
        result.ScenariosCompleted++;
    }

    private static void CompareFields(string scenario, long tick, SortedDictionary<string, string> expected,
        SortedDictionary<string, string> actual, RuntimeBehaviorComparisonResult result, bool final)
    {
        foreach (var key in expected.Keys.Union(actual.Keys))
        {
            expected.TryGetValue(key, out var wanted);
            actual.TryGetValue(key, out var observed);
            if (Numeric(wanted, out var a) && Numeric(observed, out var b))
            {
                result.NumericFieldsCompared++;
                var difference = Math.Abs(a - b);
                var scale = Math.Max(Math.Abs(a), Math.Abs(b));
                var relative = scale == 0 ? 0 : difference / scale;
                result.MaximumAbsoluteDifference = Math.Max(result.MaximumAbsoluteDifference, difference);
                result.MaximumRelativeDifference = Math.Max(result.MaximumRelativeDifference, relative);
                if (difference <= result.AbsoluteTolerance + result.RelativeTolerance * scale) continue;
                result.OutOfToleranceNumericFields++;
            }
            else
            {
                result.DiscreteFieldsCompared++;
                if (wanted == observed) continue;
                result.DiscreteDifferences++;
            }
            var detail = scenario + " tick " + tick + " " + key + "=" + (observed ?? "missing") + " expected " + (wanted ?? "missing");
            if (result.Differences.Count < 64) result.Differences.Add(detail);
            if (final && IsOutcomeField(key)) result.FinalOutcomeDifferences.Add(detail);
        }
    }

    private static bool Numeric(string? value, out double number)
    {
        number = 0;
        if (value is null) return false;
        if (value.StartsWith("f32:", StringComparison.Ordinal))
        {
            number = BitConverter.Int32BitsToSingle(unchecked((int)uint.Parse(value.Split(':')[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            return true;
        }
        if (!value.StartsWith("f64:", StringComparison.Ordinal)) return false;
        number = BitConverter.Int64BitsToDouble(unchecked((long)ulong.Parse(value.Split(':')[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        return true;
    }

    private static bool IsOutcomeField(string key) =>
        key.StartsWith("$.Economy.", StringComparison.Ordinal) || key is "$.IsVictory" or "$.IsDefeat" or
            "$.Waves.CurrentWaveNumber" or "$.Waves.IsFinalWaveCleared" or "$.Waves.EndlessModeEnabled" ||
        key.StartsWith("$.Statistics.Enemies[", StringComparison.Ordinal) &&
            (key.EndsWith(".Kills", StringComparison.Ordinal) || key.EndsWith(".Escapes", StringComparison.Ordinal) ||
             key.EndsWith(".LivesLost", StringComparison.Ordinal));
}
