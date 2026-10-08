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

The five-argument `AutoPlayer` constructor accepts a `Func<GameCommand, bool>` for host-driven verification. The adapter must apply each command synchronously and return its actual acceptance; the policy may inspect the resulting state immediately. It emits player-one commands for placement, upgrades, doctrine, specialization, targeting, sales, Forge, Plates and Protocols. The existing constructor retains direct session execution. This adapter does not start waves or advance time; the host owns those actions.

## Verification

`scripts/verify.ps1` builds every client and runs the production regressions, portable parity suite and hidden UI verification. The parity suite checks raw float/color arithmetic against MonoGame, loaded authored content, tower doctrine/specialization/Apex combinations, commands, statuses, active snapshot exchange, persistence recovery and a map/difficulty campaign matrix. Each simulated tick compares `SessionChecksum` across the two assemblies.

```powershell
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release -- --full
```

`--full` exercises every competitive directive on every map and difficulty for up to 90,000 ticks per run. To compare against an independently captured desktop assembly, build the parity runner with `-p:LegacyAssemblyPath=<absolute path to MaximalBastion.dll>`. The referenced assembly must expose the current public simulation API. Same-process .NET parity does not by itself prove cross-runtime network interoperability; an engine runtime must also exercise snapshot and checksum fixtures on its own execution backend.

The parity executable also accepts `--export-runtime-fixture <file.json>`. It generates checksum samples using the referenced desktop assembly and verifies them with the portable assembly before writing them. The fixture contains initial checkpoints, scheduled commands, expected checksums and snapshot round-trip ticks for all four maps, standard and Gauntlet wave 21, and generated Endless. Import that JSON into a host and call `RuntimeConformance.Verify(content, fixtureJson)`. Inspect `Passed`, `Failures`, `SamplesChecked`, `SnapshotsChecked` and `TicksRun`; the runner has no rendering or native input dependencies. Snapshot validation permits the authored Bulwark shield ceiling while rejecting shields above that ceiling and signal shields in directives that cannot grant them.

## Execution backends

Raw checksums match between the portable and desktop assemblies when both execute on .NET 10. Mono backends can retain wider intermediate floating-point values than .NET 10. For example, repeated `distance += speed * dt` can differ by one float32 bit even when every input bit matches. Standalone Mono and Unity's embedded Mono can also differ despite reporting the same runtime version. Small rounding differences can eventually alter hit timing and damage attribution. The simulation therefore does not support cross-runtime lockstep between these backends. Checksum comparisons remain exact; they do not quantize or conceal this difference.

`RuntimeBehaviorComparison.Compare(content, fixtureJson)` provides a separate diagnostic replay that continues after checksum divergence. It reports exact final wave/result/economy/enemy outcomes, intermediate discrete differences, numeric differences against explicit absolute and relative tolerances, and strict same-runtime snapshot round trips. `FinalOutcomesMatch` is a narrower claim than `AllSamplesMatchWithinTolerance`; neither establishes network compatibility. Diagnostic fields are included at early ticks and periodic checkpoints so that a runtime mismatch can be traced to concrete state. A host must pass the strict conformance gate before claiming compatibility with another backend.

`RuntimeConformance.RecordReference(content, scenarioJson)` records a distinct baseline for the executing backend while retaining the scenario checkpoints and commands. Save its result separately from the desktop reference, then verify that frozen file in a fresh process of the actual client. A CLI-recorded reference is not an embedded-engine reference. `ReferenceRuntime` identifies the reported runtime; it does not capture every JIT or intrinsic configuration.

## Campaign acceptance replay

The compact Cinderworks victory playbook lives in `tests/MaximalBastion.Gameplay.Tests/Fixtures`. It starts a fresh Easy/Standard session, runs all 30 authored waves continuously, and submits purchases, upgrades, targeting, sales, Plates and Protocols through `AuthoritativeCommandHost`. Its late-campaign transition sells existing defenses to fund ordinary mortar purchases. It never grants resources, changes content or restores a mid-campaign checkpoint.

```powershell
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release -- --export-campaign-replay .artifacts/campaign-replay.json
& ./.dotnet/dotnet.exe run --project tests/MaximalBastion.Gameplay.Tests -c Release -- --verify-campaign-replay .artifacts/campaign-replay.json
```

Export generates the command tape, then verifies it from another fresh session before writing the file. Verification checks every command's acceptance and sequence, each wave's outcome, the final raw checksum and a legal transition into generated Endless wave 31. The current .NET 10 playbook completes the campaign with 644 commands in 93,048 ticks, with four lives remaining. The large command tape is generated output; the strategy, transition operations and generator are tracked source.

For an engine replay, deserialize the exported JSON with `GameplayJson.Deserialize<T>(string)`, which avoids exposing framework-specific JSON overloads to host compilation. Apply every `Commands` entry at its zero-based `Tick` before the corresponding 60 Hz update. `InitialSave` must match a default new session. `Waves` contains the expected wave-end checkpoints; `FinalChecksum` and `RecordedRuntime` identify the generating backend. A different backend must report its command acceptance, outcomes and strict same-backend repeatability separately instead of treating a .NET checksum mismatch as an exact replay match.

`CampaignReplayController` is a separate acceptance driver for another backend. Construct it with the tape JSON, create a fresh session with `CreateSession(content)`, and call `PrepareTick(session, tick, execute)` immediately before each 60 Hz update. `execute` applies the command synchronously through a validated host and returns its actual acceptance. The controller waits for a legal next-wave start and shifts subsequent tape ticks by that wait. Temporarily rejected Plates and Protocols remain pending and retry with fresh request IDs while other combat commands retain their timing. Other rejected commands stop the run to protect dependent purchase order. Waits and retries are bounded and recorded in `Events`; no rejected command is silently discarded. A successful acceptance run requires victory, an empty `Failure` and `CommandsComplete`, with all delays and deferrals reported. This adaptive driver proves legal campaign completion, not exact cross-backend timing or lockstep.
