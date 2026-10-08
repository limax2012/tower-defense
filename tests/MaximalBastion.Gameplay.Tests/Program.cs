extern alias legacy;
using System.Text.Json;
using System.Text.Json.Nodes;
using MaximalBastion;
using MaximalBastion.Data;
using MaximalBastion.Gameplay;
using MaximalBastion.Multiplayer;
using MaximalBastion.Persistence;
using MaximalBastion.Simulation;
using Legacy = legacy::MaximalBastion;
using Xna = Microsoft.Xna.Framework;

var checks = 0L;
var full = args.Contains("--full");
void Equal<T>(T a, T b, string label) where T : IEquatable<T>
{
    checks++;
    if (!a.Equals(b)) throw new Exception($"{label}: portable={a?.ToString()?[..Math.Min(a.ToString()!.Length, 200)]}, desktop={b?.ToString()?[..Math.Min(b.ToString()!.Length, 200)]}");
}
void Float(float a, float b, string label) => Equal(BitConverter.SingleToInt32Bits(a), BitConverter.SingleToInt32Bits(b), label);
void Vector(Vector2 a, Xna.Vector2 b, string label) { Float(a.X, b.X, label + ".X"); Float(a.Y, b.Y, label + ".Y"); }

var rng = new Random(77321);
for (var sample = 0; sample < 50000; sample++)
{
    float Next() => (float)((rng.NextDouble() - .5) * 100000);
    var a = new Vector2(Next(), Next()); var b = new Vector2(Next(), Next()); var amount = Next() / 10000;
    var xa = new Xna.Vector2(a.X, a.Y); var xb = new Xna.Vector2(b.X, b.Y);
    Vector(a + b, xa + xb, "vector add"); Vector(a - b, xa - xb, "vector subtract");
    Vector(a * amount, xa * amount, "vector multiply"); Vector(a / amount, xa / amount, "vector divide");
    Vector(Vector2.Lerp(a, b, amount), Xna.Vector2.Lerp(xa, xb, amount), "vector lerp");
    Vector(Vector2.Normalize(a), Xna.Vector2.Normalize(xa), "vector normalize");
    Float(a.Length(), xa.Length(), "length"); Float(a.LengthSquared(), xa.LengthSquared(), "length squared");
    Float(Vector2.Dot(a, b), Xna.Vector2.Dot(xa, xb), "dot");
    Float(Vector2.Distance(a, b), Xna.Vector2.Distance(xa, xb), "distance");
    Float(Vector2.DistanceSquared(a, b), Xna.Vector2.DistanceSquared(xa, xb), "distance squared");
    var packedA = (uint)rng.NextInt64(0, (long)uint.MaxValue + 1); var packedB = (uint)rng.NextInt64(0, (long)uint.MaxValue + 1);
    var c = new Color(packedA); var d = new Color(packedB); var xc = new Xna.Color(packedA); var xd = new Xna.Color(packedB);
    Equal(c.PackedValue, xc.PackedValue, "color packing");
    Equal(Color.Lerp(c, d, amount).PackedValue, Xna.Color.Lerp(xc, xd, amount).PackedValue, "color lerp");
    Equal((c * amount).PackedValue, (xc * amount).PackedValue, "color multiply");
    Equal(Color.FromNonPremultiplied(c.R, c.G, c.B, c.A).PackedValue,
        Xna.Color.FromNonPremultiplied(c.R, c.G, c.B, c.A).PackedValue, "color premultiply");
}
Console.WriteLine($"PASS value types: {checks:N0} raw-bit comparisons");

var directory = Path.Combine(AppContext.BaseDirectory, "ContentData");
var content = new ContentLoader(directory).Load();
var nativeContent = new Legacy.Data.ContentLoader(directory).Load();
Equal(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(content, ContentJson.Options), JsonSerializer.SerializeToNode(nativeContent, Legacy.Data.ContentJson.Options)), true, "authored content");
var references = typeof(GameSession).Assembly.GetReferencedAssemblies();
if (references.Any(x => x.Name!.Contains("Xna") || x.Name.Contains("MonoGame") || x.Name.Contains("Unity")))
    throw new Exception("Portable simulation has an engine assembly reference");
Console.WriteLine("PASS authored content and engine-free assembly boundary");

var exportIndex = Array.IndexOf(args, "--export-runtime-fixture");
if (exportIndex >= 0)
{
    if (exportIndex + 1 >= args.Length) throw new ArgumentException("A fixture output path is required.");
    var fixture = RuntimeFixtureBuilder.Create(nativeContent);
    var json = JsonSerializer.Serialize(fixture, ContentJson.Options);
    var result = RuntimeConformance.Verify(content, json);
    Console.WriteLine(JsonSerializer.Serialize(result, ContentJson.Options));
    if (!result.Passed) throw new Exception("Runtime fixture failed portable verification.");
    File.WriteAllText(args[exportIndex + 1], json);
    Console.WriteLine("PASS exported native runtime fixture: " + args[exportIndex + 1]);
    return;
}

void Match(GameSession session, Legacy.GameSession native, long tick, string label)
{
    Equal(SessionChecksum.Compute(session, tick), Legacy.Multiplayer.SessionChecksum.Compute(native, tick), label + " checksum tick " + tick);
}

void CheckSnapshot(GameSession session, Legacy.GameSession native, long tick)
{
    var state = session.CaptureCoOpState(tick, 0, false);
    var nativeState = native.CaptureCoOpState(tick, 0, false);
    state.RunId = nativeState.RunId;
    Equal(JsonSerializer.Serialize(state, ContentJson.Options), JsonSerializer.Serialize(nativeState, Legacy.Data.ContentJson.Options), "snapshot JSON");
    var restored = GameSession.RestoreCoOpState(content,
        JsonSerializer.Deserialize<CoOpStateSnapshot>(JsonSerializer.Serialize(nativeState, Legacy.Data.ContentJson.Options), ContentJson.Options)!, 1);
    var nativeRestored = Legacy.GameSession.RestoreCoOpState(nativeContent,
        JsonSerializer.Deserialize<Legacy.Multiplayer.CoOpStateSnapshot>(JsonSerializer.Serialize(state, ContentJson.Options), Legacy.Data.ContentJson.Options)!, 1);
    Match(restored, nativeRestored, tick, "cross-client snapshot");
    Equal(SessionChecksum.Compute(restored, tick), SessionChecksum.Compute(session, tick), "snapshot reconstructs state");
    for (var step = 1; step <= 120; step++)
    {
        restored.Update(1f / 60f); nativeRestored.Update(1f / 60f);
        Match(restored, nativeRestored, tick + step, "snapshot continuation");
    }
}

foreach (var definition in content.Towers.Values)
foreach (var doctrine in definition.Tier2Doctrines)
foreach (var specialization in definition.Specializations)
{
    var session = new GameSession(content, "foundry_loop", "normal", "close_quarters");
    var native = new Legacy.GameSession(nativeContent, "foundry_loop", "normal", "close_quarters");
    session.Economy.AddCredits(100000); native.Economy.AddCredits(100000);
    long sequence = 0;
    void Apply(GameCommand command)
    {
        command = command with { Sequence = ++sequence, PlayerId = 1 };
        var nativeCommand = JsonSerializer.Deserialize<Legacy.Multiplayer.GameCommand>(JsonSerializer.Serialize(command, ContentJson.Options), Legacy.Data.ContentJson.Options)!;
        var result = GameCommandProcessor.Apply(session, command);
        var nativeResult = Legacy.Multiplayer.GameCommandProcessor.Apply(native, nativeCommand);
        Equal(result.Accepted, nativeResult.Accepted, "command acceptance " + command.Type);
        Equal(result.Accepted, true, "authored command " + command.Type);
        Match(session, native, 0, "command " + command.Type);
    }
    Apply(new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = definition.Id, X = 45, Y = 200 });
    Apply(new() { Type = GameCommandType.ChooseDoctrine, EntityId = 1, DoctrineId = doctrine.Id });
    Apply(new() { Type = GameCommandType.SpecializeTower, EntityId = 1, SpecializationId = specialization.Id });
    Apply(new() { Type = GameCommandType.PlaceTower, TowerDefinitionId = "needle_turret", X = 190, Y = 190 });
    if (definition.Behavior != "aura")
        Apply(new() { Type = GameCommandType.SetTargetMode, EntityId = 1, TargetMode = MaximalBastion.Core.TargetMode.Armored });
    Apply(new() { Type = GameCommandType.ToggleAutoProtocol, EntityId = 1 });
    Apply(new() { Type = GameCommandType.PlaceGenerator, X = 260, Y = 390 });
    Apply(new() { Type = GameCommandType.UpgradeGenerator });
    Apply(new() { Type = GameCommandType.DeployEmergencyDefense, X = 120, Y = 220 });
    var checkpoint = session.CaptureSaveGame(); checkpoint.Waves.CurrentWaveNumber = 20;
    session = GameSession.RestoreSaveGame(content, checkpoint);
    native = Legacy.GameSession.RestoreSaveGame(nativeContent,
        JsonSerializer.Deserialize<Legacy.Persistence.SaveGameData>(JsonSerializer.Serialize(checkpoint, ContentJson.Options), Legacy.Data.ContentJson.Options)!);
    Apply(new() { Type = GameCommandType.UpgradeTower, EntityId = 1 });
    foreach (var enemy in content.Enemies.Keys)
    {
        session.SpawnEnemy(enemy, 2, .6f, "Elite", MaximalBastion.Enemies.EnemySignalRole.Jammer);
        native.SpawnEnemy(enemy, 2, .6f, "Elite", Legacy.Enemies.EnemySignalRole.Jammer);
    }
    for (var tick = 0; tick < 1500; tick++)
    {
        session.Update(1f / 60f); native.Update(1f / 60f);
        Match(session, native, tick, definition.Id + "/" + doctrine.Id + "/" + specialization.Id);
        if (tick == 360) CheckSnapshot(session, native, tick);
    }
    Console.WriteLine($"PASS tower branches {definition.Id}/{doctrine.Id}/{specialization.Id}: Apex, signals, Forge, Plate, protocol, snapshot");
}

foreach (var map in content.Maps.Keys.OrderBy(x => x))
foreach (var difficulty in content.Difficulties.Keys.OrderBy(x => x))
foreach (var challenge in full ? content.Challenges.Values.Where(x => !x.IsSandbox).Select(x => x.Id) : ["standard"])
{
    var session = new GameSession(content, map, difficulty, challenge);
    var native = new Legacy.GameSession(nativeContent, map, difficulty, challenge);
    var player = new AutoPlayer(session, AutoPlayerStrategy.Experienced, 318);
    var nativePlayer = new Legacy.Simulation.AutoPlayer(native, Legacy.Simulation.AutoPlayerStrategy.Experienced, 318);
    var runner = new DeterministicSessionRunner(session);
    var nativeRunner = new Legacy.Multiplayer.DeterministicSessionRunner(native);
    for (var tick = 0; tick < (full ? 90000 : 18000) && !session.IsDefeat && !session.IsVictory; tick++)
    {
        if (session.CanStartWave)
        {
            player.PrepareForWave(session); nativePlayer.PrepareForWave(native);
            Equal(session.StartNextWave(), native.StartNextWave(), "start wave");
        }
        if (tick % 30 == 0) { player.ReactDuringWave(session); nativePlayer.ReactDuringWave(native); }
        runner.RunTicks(1); nativeRunner.RunTicks(1);
        Match(session, native, runner.Tick, map + "/" + difficulty);
        if (tick == 1000) CheckSnapshot(session, native, runner.Tick);
    }
    Console.WriteLine($"PASS campaign {map}/{difficulty}/{challenge}: wave {session.CurrentWave}, {runner.Tick} matching ticks, {session.Economy.TotalKills} kills");
}

var persistenceRoot = Path.Combine(Path.GetTempPath(), "BastionPortableParity-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(persistenceRoot);
try
{
    var repository = new SaveSlotRepository(persistenceRoot);
    var nativeRepository = new Legacy.Persistence.SaveSlotRepository(persistenceRoot);
    var session = new GameSession(content, "foundry_loop");
    session.TryPlaceTower("needle_turret", new Vector2(45, 200));
    repository.Save(session);
    Match(repository.Load(content), nativeRepository.Load(nativeContent), 0, "portable save, desktop load");
    nativeRepository.Save(nativeRepository.Load(nativeContent));
    Match(repository.Load(content), nativeRepository.Load(nativeContent), 0, "desktop save, portable load");
    repository.Save(session);
    File.WriteAllText(repository.GetSlotPath(1), "invalid primary");
    Match(repository.Load(content), nativeRepository.Load(nativeContent), 0, "atomic overwrite backup recovery");
    Equal(repository.Delete(1), true, "save deletion");
    Equal(File.Exists(repository.GetSlotBackupPath(1)), false, "backup deletion");
    Console.WriteLine("PASS cross-client save/overwrite/backup recovery/deletion");
}
finally { Directory.Delete(persistenceRoot, true); }

Console.WriteLine($"PASS portable parity: {checks:N0} comparisons");
