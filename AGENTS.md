# Minimal Bastion Agent Runbook

## Project shape

- `src/MinimalBastion/`: MonoGame DesktopGL game targeting `net10.0`.
- `src/MinimalBastion/ContentData/`: copied-at-runtime JSON for towers, enemies, maps, waves, and tactical systems.
- `tests/MinimalBastion.Tests/`: deterministic executable test, benchmark, and self-play harness; it is not xUnit.
- `.build/publish/`: self-contained Windows x64 handoff build.
- `.build/balance/`: machine-readable simulation reports.
- `GAME_DESIGN.md`, `AUTONOMOUS_BALANCE.md`, `OVERNIGHT_PROGRESS.md`, and `OVERNIGHT_CHANGELOG.md`: current design and project state.

The repository is tracked in Git and mirrored at `https://github.com/limax2012/tower-defense`. Preserve coherent checkpoints with tested commits on the active feature branch; do not merge or push `main` unless the user explicitly requests it.

## Commands

This machine uses the workspace-local .NET 10 SDK:

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
dotnet build MinimalBastion.sln -c Release --no-restore --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
dotnet restore MinimalBastion.sln -r win-x64 --disable-build-servers
dotnet publish src\MinimalBastion -c Release -r win-x64 --self-contained true --no-restore -o .build\publish --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
```

If a build reports a locked executable, inspect `Get-Process MinimalBastion`, verify its exact path is inside this project, and stop only that process. Build servers can otherwise retain locks; keep the flags above.

## Runtime architecture

- `Game1` owns window, input, state-screen, transport, fixed-network-runner, and render lifecycle.
- One `GameSession` owns authoritative match state and updates systems in deterministic order.
- `MapRuntime`/`PathRuntime` own placement and route geometry, including Surge Node buffs.
- `WaveManager` schedules authored groups, intermissions, early calls, elites, and the final boss.
- `TowerSystem`, narrow `ITowerBehavior` modules, `TargetSelector`, `ProjectileSystem`, `DamageResolver`, status/effect systems, and `EnemySystem` execute combat.
- `TacticalDefenseSystem`, `PulsePlateInstance`, and `ChargeForgeInstance` own emergency defenses and wave-powered production.
- `UIManager`, `GameRenderer`, and `PrimitiveRenderer` are presentation-only.
- `GameCommand`, `AuthoritativeCommandHost`, `DeterministicSessionRunner`, and `SessionChecksum` form the multiplayer seam.
- `LanCoOpHost` listens dual-stack on all adapters; `LanCoOpClient` accepts DNS, IPv4, or IPv6 endpoints. The transport class and file names are internal implementation details.
- Definitions and runtime instances must remain separate.

Do not casually replace this with ECS, dependency injection, a physics engine, or a UI framework. Extract narrow services only when they improve deterministic simulation, testing, networking, or maintainability.

## Determinism and networking invariants

- Simulation must never depend on rendering, wall-clock time, hash-set iteration order, or unseeded randomness.
- Host-accepted commands receive a monotonic sequence and future fixed tick; both peers apply the same stream.
- State affecting future outcomes belongs in `SessionChecksum`. This includes map identity, ownership, specializations, Overdrive state, Pulse Plate handled-enemy IDs, forge timers, and shared economy.
- Co-op uses shared credits/lives/inventory and shared defense control. Either player may upgrade, specialize, retarget, automate, Overdrive, or sell any tower/Forge; original ownership remains attribution only.
- Wave start requires both players ready. Speed and pause are shared, command-synchronized state.
- Public internet play is direct TCP `28741` with a six-character join code and build/content fingerprint negotiation. There is no matchmaking, relay server, automatic port mapping, or encryption; hosts still need a reachable address/port forward where NAT requires it.
- A disconnected Player 2 can reconnect to the existing host. The host pauses the simulation, rotates that player's request-replay session, sends a validated authoritative snapshot (including combat, economy, wave, readiness, and pending-command state), then resumes both peers.
- Keep stable internal IDs such as `relay_divide`; the player-facing map/feature names are `Surge Divide` and `Surge Node`.

## Gameplay invariants

- Never add an early-wave-only damage multiplier. Tower damage is mechanically consistent throughout a run.
- Charge Forge production advances only while `Waves.IsActive`; waiting before or between waves must not generate plates.
- A Pulse Plate may trigger once per handled enemy. A single enemy cannot consume both charges; an unhandled consecutive enemy may trigger immediately.
- Continuous free placement inside authored regions is intentional. Do not introduce a placement grid.
- Roads render as one seamless surface with a yellow dashed centerline and no tile, seam, corner-circle, or heavy-outline artifacts.
- Tower marks encode level consistently: one top spoke at level 1, then fixed spokes at 120 and 240 degrees. Do not reintroduce separate battlefield badges or tower-specific starting mark counts.
- Keep gameplay and input in the 1280x720 logical coordinate space, but render the scene at 2560x1440. Composite only into `ViewportTransform.DestinationRectangle`; never draw logical scene geometry directly to the backbuffer because it can bleed into letterbox bars.
- Preserve double-density SpriteFont/primitive masks and linear scene downsampling unless the entire resolution strategy is deliberately replaced and visually revalidated.
- `ColorPalette` is the centralized UI theme. Preserve the muted hierarchy: teal battlefield (`#152D36`), slate road (`#384E65`), navy HUD (`#152B46`), soft off-white panels, blue-gray cards/text, and controlled cyan/green/gold/coral/violet/blue accents.
- Keep backbuffer MSAA disabled. The fixed 2x scene target already supersamples geometry; enabling backbuffer MSAA on DesktopGL gamma-shifts the final scene and washes out the authored palette.

## Design and visual principles

- Prefer multiple situationally strong strategies over universal towers.
- Flat armor must preserve heavy-hit and armor-piercing identities.
- Information required to spend money must be visible before purchase.
- Emergency systems complement permanent towers and must not become default damage.
- Deep navy/slate battlefield, quiet structural build fields, saturated functional accents, crisp geometry, consistent border weights, restrained rings, and unclipped labels.
- Keep the entire 960-pixel battlefield usable; persistent tactical controls belong in the 320-pixel sidebar.
- Effects communicate state and impact; they are not decoration.

## Balance procedure

1. Run all fast deterministic tests.
2. Run isolated `--balance` scenarios after combat changes.
3. Run focused strategy/map batches for changed systems.
4. Run `--simulate-full --runs 5` before a major checkpoint.
5. Compare win rate, wave reached, lives, spend, picks, upgrades, branches, Overdrives, plates, leaks, and tower contribution.
6. Preserve interesting report paths and seeds in `AUTONOMOUS_BALANCE.md` and `OVERNIGHT_PROGRESS.md`.
7. Publish and inspect the actual native window after UI changes.

## Current scope and constraints

- Current content: 4 maps with dedicated 20-wave rosters, 10 three-level towers, two mutually exclusive Tier-2 doctrines and two final specializations for every tower, 5 enemy bases plus elite/boss ranks, Pulse Plates, a three-level Charge Forge, and tower-specific Protocols with optional automatic activation.
- Four difficulty profiles (Easy, Normal, Hard, Bastion) and four challenge directives alter the run before it begins. Surge Divide is intentionally the most demanding high-pressure arena because its compact Surge Nodes reward precise placement.
- Direct online two-player co-op is functional when the host is reachable on TCP `28741`.
- Headless gameplay is fast and deterministic; MonoGame rendering still requires a graphical Windows session.
- Unlimited numbered solo/co-op save slots, atomic backup recovery, autosaves, run history, display/effect/audio settings, procedural sound effects, difficulty/challenge selectors, and basic reconnect recovery are implemented.
- Hosted relay, matchmaking, automatic NAT traversal, and cloud save synchronization are not implemented. Procedural music and effects are local presentation only and never participate in deterministic state.
- Avoid changing map coordinates, costs, damage, wave totals, or tactical cadence without simulation evidence and updated tests/docs.
