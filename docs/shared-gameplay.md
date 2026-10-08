# Shared gameplay library

`src/MaximalBastion.Gameplay/MaximalBastion.Gameplay.csproj` builds an engine-independent .NET Standard 2.1 assembly. It compiles the authoritative gameplay files from `src/MaximalBastion` with `PORTABLE_GAMEPLAY`. The MonoGame desktop and KNI browser clients compile those same source files with their native value types. There is one implementation of the rules, but these clients do not currently reference the portable DLL.

The portable assembly references neither MonoGame nor Unity. It contains content loading and validation, `GameSession`, every tower behavior, targeting, damage and statuses, projectiles, enemies, waves and Endless generation, economy, tactical defenses, statistics, achievements, persistence, simulation agents, commands, snapshots, deterministic stepping and checksums. `MobileSessionController`, `TowerInfo` and `SessionLabels` provide engine-independent presentation data. `ColorPalette` supplies authored color values; `PresentationFrame` supplies local interpolation without altering gameplay state.

Native drawing, audio, window/input polling, clipboard access and the TCP transport remain client services. The portable assembly includes the deterministic co-op state/command protocol, but does not open sockets. Build fingerprints include assembly identity, so different compiled clients are not advertised as interchangeable network builds.

## Consuming the library

Build with the workspace SDK:

```powershell
& ./.dotnet/dotnet.exe build src/MaximalBastion.Gameplay -c Release
```

The assembly and JSON dependencies are placed in `bin/Release/netstandard2.1`. An engine host must supply the authored `ContentData` directory and the required compatible JSON dependency assemblies. Unity already supplies framework assemblies such as `System.Memory`, `System.Buffers` and `System.Numerics.Vectors`; do not import competing framework assemblies into an engine without checking its runtime.

```csharp
using MaximalBastion;
using MaximalBastion.Data;
using MaximalBastion.Gameplay;
using MaximalBastion.Multiplayer;

var content = new ContentLoader(contentDirectory).Load();
var session = new GameSession(content, "foundry_loop", "normal", "standard");
var runner = new DeterministicSessionRunner(session);
runner.Schedule(0, new GameCommand {
    Sequence = 1, PlayerId = 1, Type = GameCommandType.PlaceTower,
    TowerDefinitionId = "needle_turret", X = 45, Y = 200
});
runner.RunTicks(1);
```

`GameSession` retains its existing namespaces and method signatures. The portable build uses `MaximalBastion.Gameplay.Vector2`, `Point`, `Rectangle` and `Color` in place of engine types. Vector arithmetic retains the desktop operation order, and `Color.PackedValue` retains the snapshot RGBA encoding. Public geometry is planar; a 3D client maps logical `(X, Y)` to its ground plane and applies elevation only to presentation. Authored ranges, path distances, placement clearances and rectangular build regions continue to use logical pixels. Neither world scale nor a visible muzzle offset changes gameplay range or hit timing.

Call `DeterministicSessionRunner.Advance` with elapsed seconds or `RunTicks` with explicit 60 Hz ticks. Do not also call `GameSession.Update` for the same elapsed time. Validate actions through `GameCommandProcessor` or schedule them through the runner. Aim poses and firing effects observe session events and state; they must not delay authored attacks or apply damage a second time. Read `PresentationFrame` for interpolation. Repository classes accept explicit storage roots so each host can select its application data directory.

## Verification

`scripts/verify.ps1` builds every client and runs the production regressions, portable parity suite and hidden UI verification. The parity suite checks raw float/color arithmetic against MonoGame, loaded authored content, tower doctrine/specialization/Apex combinations, commands, statuses, active snapshot exchange, persistence recovery and a map/difficulty campaign matrix. Each simulated tick compares `SessionChecksum` across the two assemblies.

```powershell
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release -- --full
```

`--full` exercises every competitive directive on every map and difficulty for up to 90,000 ticks per run. To compare against an independently captured desktop assembly, build the parity runner with `-p:LegacyAssemblyPath=<absolute path to MaximalBastion.dll>`. The referenced assembly must expose the current public simulation API. Same-process .NET parity does not by itself prove cross-runtime network interoperability; an engine runtime must also exercise snapshot and checksum fixtures on its own execution backend.

The parity executable also accepts `--export-runtime-fixture <file.json>`. It generates checksum samples using the referenced desktop assembly and verifies them with the portable assembly before writing them. The fixture contains initial checkpoints, scheduled commands, expected checksums and snapshot round-trip ticks for all four maps, standard and Gauntlet wave 21, and generated Endless. Import that JSON into a host and call `RuntimeConformance.Verify(content, fixtureJson)`. Inspect `Passed`, `Failures`, `SamplesChecked`, `SnapshotsChecked` and `TicksRun`; the runner has no rendering or native input dependencies. Snapshot validation permits the authored Bulwark shield ceiling while rejecting shields above that ceiling and signal shields in directives that cannot grant them.
