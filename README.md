# Minimal Bastion

Minimal Bastion is a colorful geometric tower-defense game built with C# and .NET 10. The Windows build uses MonoGame DesktopGL, and the solo browser build uses WebAssembly and WebGL. Both versions combine authored campaigns, a ten-wave Mastery extension, generated Endless play, branching tower upgrades, tactical devices, persistent progression, and deterministic balance tools. The Windows build also supports direct two-player online co-op.

## Current feature set

- Four arenas with distinct route geometry, build areas, visual treatments, starting economies, and 30 authored waves each.
- Easy, Medium, Hard, and Bastion difficulty profiles.
- Standard, Signal Gauntlet, Core Six, and Entrenched directives, plus a solo Sandbox Lab.
- Ten towers. Every tower has two tier-two doctrines, two compatible tier-three roles, a unique Protocol, and an Apex promotion.
- Eight targeting modes: First, Last, Strongest, Weakest, Nearest, Fastest, Armored, and Support.
- Pulse Plates, a three-level Charge Forge, Surge Nodes, automatic Protocol activation, and configurable wave auto-start.
- One rolling autosave, expandable manual save slots, save duplication/deletion, recovery generations, run history, final-layout inspection, medals, achievements, and career records.
- A discovery-driven Tactical Library for towers, enemies, signal roles, maps, waves, profiles, directives, statuses, and game systems.
- Direct two-player Windows co-op with host-authoritative commands, deterministic local simulation, reconnect repair, shared defenses, and visible remote cursor/placement state.
- Runtime-generated vector-like visuals, procedural music, and synthesized sound effects; no external art or audio files are required.
- Headless deterministic agents, isolated regression tests, and a hidden UI renderer for verification without taking desktop focus.

The title screen includes two small live combat scenes. They use normal enemy and tower behavior with randomized groups of three to five enemies and three to five non-Beacon towers at varied levels.

## Build and run

Install the .NET 10 SDK, open PowerShell in the repository root, and run:

```powershell
dotnet restore MinimalBastion.sln
dotnet run --project src\MinimalBastion\MinimalBastion.csproj -c Release
```

This workspace may also contain a local SDK at `.dotnet\dotnet.exe`. MonoGame's content build invokes `dotnet`, so add that directory to `PATH` when using it:

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
.\.dotnet\dotnet.exe restore MinimalBastion.sln
.\.dotnet\dotnet.exe run --project src\MinimalBastion\MinimalBastion.csproj -c Release
```

A Release build creates `src\MinimalBastion\bin\Release\net10.0\MinimalBastion.exe`.

Create a self-contained Windows x64 package with:

```powershell
dotnet restore MinimalBastion.sln -r win-x64 --disable-build-servers
powershell -ExecutionPolicy Bypass -File scripts\publish-windows.ps1
```

The published application is `.build\releases\windows\MinimalBastion.exe`, and the distributable archive is `.build\releases\MinimalBastion-Windows.zip`.

### Browser build

Install the WebAssembly build tools once for optimized Release packages:

```powershell
dotnet workload install wasm-tools
```

Run the solo WebAssembly version locally with:

```powershell
dotnet run --project src\MinimalBastion.Web\MinimalBastion.Web.csproj -c Release
```

Open the local HTTP address printed by the command. The browser build must be served over HTTP or HTTPS; opening `index.html` directly from the filesystem is not supported by WebAssembly asset loading.

Create a static browser package with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-browser.ps1
```

The script writes the static site to `.build\releases\browser` and creates `.build\releases\MinimalBastion-Browser.zip`. The archive has `index.html` at its root and can be uploaded as an HTML game to a static host such as itch.io. Browser saves, discoveries, records, and settings are retained by the site origin and are separate from the Windows files under `%LocalAppData%`.

Build both release packages with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-releases.ps1
```

The browser build contains the complete solo campaign, Mastery, Endless, Sandbox, persistence, library, records, audio, settings, and fullscreen flow. Online co-op remains a Windows feature.

## Campaign structure

Each arena contains an authored 20-wave campaign and a harder authored Mastery sequence from waves 21 through 30. Clearing wave 20 records a campaign result and offers **Enter Mastery**, which continues on the same battlefield. Mastery unlocks Apex tower promotions. Generated Endless waves begin at wave 31 using the arena's wave-30 roster as their anchor.

| Arena | Base credits | Build areas | Surge Nodes | Campaign contacts | Mastery contacts |
| --- | ---: | ---: | ---: | ---: | ---: |
| Foundry Loop | 400 | 8 | 0 | 1,090 | 1,095 |
| Crosswind Basin | 390 | 9 | 0 | 1,066 | 1,126 |
| Prism Circuit | 380 | 6 | 3 | 1,192 | 1,170 |
| Surge Divide | 360 | 6 | 9 | 1,063 | 1,395 |

Surge Nodes grant focused attack-rate, range, damage, or armor-piercing bonuses. The active node is shown during placement and in Tower Intel; effective node and Signal Beacon modifiers are included in displayed stats.

### Difficulties

| Difficulty | Enemy health | Enemy speed | Starting credits | Lives |
| --- | ---: | ---: | ---: | ---: |
| Easy | 80% | 95% | 125% | 30 |
| Medium | 90% | 98% | 112.5% | 24 |
| Hard | 100% | 100% | 100% | 20 |
| Bastion | 112% | 102% | 100% | 18 |

Tower statistics do not change with difficulty, wave number, map, or elapsed time.

### Directives

- **Standard** enables the complete roster and every tactical system.
- **Signal Gauntlet** adds 10% opening credits and introduces Accelerator, Restorer, Bulwark, Jammer, and Disruptor signal carriers from the early campaign onward. Carriers can support enemies or interfere with towers. The Support targeting mode prioritizes carriers and otherwise selects the strongest available target.
- **Core Six** restricts the roster to Needle Turret, Frost Spire, Shard Fan, Ember Coil, Breaker Cannon, and Signal Beacon, with 30% more opening credits.
- **Entrenched** disables Pulse Plates, Charge Forge, Protocols, and selling, with 10% more opening credits.
- **Sandbox Lab** provides unlimited resources and lives for controlled tower, upgrade, Protocol, status, enemy-rank, and authored-wave experiments. Sandbox sessions do not create competitive saves or run-history records.

## Towers and tactical systems

| Tower | Cost | Default target | Role |
| --- | ---: | --- | --- |
| Needle Turret | 90 | First | Efficient direct fire, ricochet, or piercing |
| Frost Spire | 140 | Fastest | Area damage and slowing |
| Shard Fan | 150 | First | Multi-shot short-range coverage |
| Watchtower | 190 | Strongest | Long-range priority damage |
| Ember Coil | 220 | First | Burn pressure and splash |
| Breaker Cannon | 250 | Strongest | Armor and shield counterplay |
| Signal Beacon | 300 | None | Attack-rate and range support |
| Arc Relay | 320 | First | Chained damage and control |
| Siege Mortar | 360 | First | Predictive capped splash damage |
| Prism Beam | 450 | Strongest | Durable-target pressure and Expose |

Tower silhouettes show their level without hover: one inward spoke at level 1, a second at 120 degrees at level 2, and a third at 240 degrees at level 3. The selected final role is represented inside the level-three silhouette. Tower Intel contains the exact current values, preview deltas, lifetime damage/kills/control, active boosts, node state, owner in co-op, and Apex state.

Pulse Plates begin with one stored charge, support up to 16 deployed plates, and carry two pulses. A direct active-wave purchase starts at 60 credits and rises by 15 for each additional direct purchase during that wave; the price resets on the next wave. Plates deal fixed area damage, stun and slow their blast, and use bounded knockback with reduced displacement against elites and bosses.

The Charge Forge costs 300 credits and creates stored Plates only while a wave is active. Its three levels improve production from 34 to 26 to 20 seconds, raise storage from 3 to 4 to 5, and add plate-damage bonuses at the later levels.

## Controls

- Left click selects, places, or activates a control. Right click cancels placement.
- `1`-`0` prepares the corresponding tower.
- `Q` prepares a stored Pulse Plate or buys one during an active wave.
- `G` prepares or selects the Charge Forge.
- `E` activates or resets the selected tower's Protocol.
- `A` arms or disarms automatic Protocol use.
- `U` and `I` choose the upper/first and lower/second upgrade paths.
- `X` applies an eligible Apex promotion.
- `T` opens the targeting menu without changing the current mode until a replacement is chosen.
- `Delete` sells the selected tower or Forge where selling is permitted.
- `D` enables or disables a selected tower in Sandbox.
- `Space` starts/readies a wave. `S` toggles 1x/2x speed.
- `Escape` or `P` pauses solo play. `Tab` toggles the Tactical Library during co-op.
- Middle click sends a co-op location ping.
- `F11` toggles borderless desktop fullscreen. `F4` toggles the debug overlay in Debug builds.

Settings can hide hotkey badges without disabling any keyboard shortcuts. Sandbox adds compact hotkeys for enemy selection, group/rank/health selection, spawning, test reset, tower clearing, and authored-wave selection; the active controls are shown in its interface and Tactical Library.

Placement remains continuous rather than grid-based. Tower, Forge, and Plate previews snap to the nearest legal point inside a small assistance radius, display the resolved position and range, and do not alter co-op state until placement is confirmed.

## Saves, discovery, and records

Persistent data lives under `%LocalAppData%\MinimalBastion`:

- `Saves\autosave.json` is the single rolling autosave.
- `Saves\slot-n.json` files are expandable manual slots.
- `History` stores run summaries and read-only final defense layouts.
- `discoveries.json` stores Tactical Library unlocks.
- `settings.json` stores display, audio, effects, hotkey-badge, and auto-start preferences.
- `Logs\latest-crash.log` stores the latest unexpected top-level failure.

Save and settings writes are atomic and retain one bounded `.bak` recovery generation. Saves can be duplicated into a manual slot, including the autosave, and deleted with confirmation. A co-op checkpoint can be reopened as a host or continued alone; the original tower placer remains recorded but does not restrict control.

The Tactical Library reveals authored details as the player encounters towers, upgrades, Protocols, Apex promotions, enemies, signal roles, ranks, statuses, maps, waves, profiles, directives, and mechanics. Undiscovered entries remain concealed rather than exposing future counters and progression.

Run History records campaign, Mastery, Endless, victory, and defeat outcomes under one persistent run identity. Continuing beyond wave 20 updates that run instead of creating a second campaign record. Records include tower contributions, economy, tactical-system use, leak threats, medals, achievements, and a path-cleared final layout whose towers can be inspected. The current career contains 28 run medals and 56 broader achievements, plus best-result records by profile.

## Online co-op

Online co-op uses direct TCP on port `28741` for a private two-player match. The host PC is the server; there is no matchmaking, hosted relay, automatic UPnP, or encrypted transport. Internet hosting normally requires forwarding TCP `28741` and allowing the application through the host firewall. A peer VPN such as Tailscale or ZeroTier can provide the reachable network path instead.

To play:

1. Choose **Online Co-op**.
2. The host chooses **Host Online Game**, configures the arena, difficulty, and directive, then shares the displayed address and six-character code.
3. The guest enters the host address or DNS name and code. Port `28741` is added automatically when no port is supplied.

The handshake validates the compiled build and recursive gameplay-content fingerprint. Both peers simulate deterministic fixed ticks locally while the host sequences commands and repairs divergence with bounded compressed snapshots. Shared state includes enemies, waves, credits, lives, towers, upgrades, targeting, sales, Plates, Forge, Protocols, speed, pause, and ready state. Both players may operate every tower regardless of who placed it.

If Player 2 disconnects, the host preserves and pauses the match. Rejoining with the same address and code restores the authoritative state. Co-op restart keeps the connection and initializes a fresh match; Main Menu ends the session. See [docs/co-op-architecture.md](docs/co-op-architecture.md) for protocol and synchronization details.

## Verification and balance tools

Run the complete isolated verification workflow with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1
```

The script builds into `%TEMP%`, runs the deterministic regression executable, and invokes a hidden non-activating renderer under `.artifacts\verification\ui`. Visual verification uses the canonical 2560x1440 scene size and includes a 3840x2160 display-density smoke scene. It does not replace files used by a running game or send keyboard/mouse input. Use `-SkipVisuals` to omit rendering.

Representative headless commands:

```powershell
dotnet run --project tests\MinimalBastion.Tests -c Release -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release -- --simulate-full --difficulty all --runs 3
dotnet run --project tests\MinimalBastion.Tests -c Release -- --simulate-full --difficulty hard --challenge all --runs 3
dotnet run --project tests\MinimalBastion.Tests -c Release -- --simulate-full --map relay_divide --max-wave 40 --runs 10
```

Reports are written under `.build\balance`. Filters include strategy, seed, run count, map, difficulty, directive, target wave, forced tower paths, checkpoint continuation, Protocol/Apex controls, Signal Gauntlet counter-pressure controls, build/footprint holds, summary output, and output path. See [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md) for the full harness and interpretation guidance.

## Documentation

- [GAME_DESIGN.md](GAME_DESIGN.md): current player-facing rules, progression, and design goals.
- [TOWER_DEFENSE_DESIGN.md](TOWER_DEFENSE_DESIGN.md): current technical architecture and runtime invariants.
- [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md): simulation harness, commands, metrics, and measured baselines.
- [BALANCE_REPORT.md](BALANCE_REPORT.md): current balance assessment and human-testing priorities.
- [docs/co-op-architecture.md](docs/co-op-architecture.md): transport, authority, synchronization, reconnect, and co-op UI.
- [docs/tower-defense-research.md](docs/tower-defense-research.md): durable design principles used by the project.
