# Repository Guide

## Purpose

Minimal Bastion is a .NET 10 / MonoGame DesktopGL tower-defense game. Source code, authored JSON, deterministic tests, balance agents, and documentation are maintained together in this repository.

Documentation and code comments must describe the current implementation and surrounding context. Do not add task history, prompt-specific notes, removed behavior, or transient implementation narration.

## Canonical references

- `README.md`: build, play, controls, current features, persistence, and verification.
- `GAME_DESIGN.md`: current rules and player-facing design.
- `TOWER_DEFENSE_DESIGN.md`: current architecture and invariants.
- `AUTONOMOUS_BALANCE.md`: simulation CLI, metrics, and measured baselines.
- `BALANCE_REPORT.md`: current balance interpretation and validation priorities.
- `docs/co-op-architecture.md`: online synchronization and reconnect design.

Implementation and JSON content are the source of truth when documentation disagrees.

## Repository layout

- `src/MinimalBastion`: game source.
- `src/MinimalBastion/ContentData`: tower, enemy, map, wave, profile, directive, and tactical JSON.
- `tests/MinimalBastion.Tests`: deterministic regression executable and simulation CLI.
- `scripts/verify.ps1`: isolated build/test/hidden-render workflow.
- `.build`, `.artifacts`, and `.verification`: generated local outputs; do not commit them.

## Build and verify

Prefer the workspace-local SDK when present and make it available on `PATH` for MonoGame Content Builder:

```powershell
$dotnet = if (Test-Path .\.dotnet\dotnet.exe) { (Resolve-Path .\.dotnet\dotnet.exe).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:Path = "$(Split-Path $dotnet);$env:Path"
& $dotnet restore MinimalBastion.sln
& $dotnet build MinimalBastion.sln -c Release --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
```

For routine validation, including a hidden UI render that does not take desktop focus:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Use `-SkipVisuals` only when a change cannot affect layout, rendering, data-loaded text, or screen flow.

## Development invariants

- Preserve the 1280×720 logical layout, 2560×1440 internal scene, clipped 16:9 viewport, and shared coordinate conversion.
- Keep theme colors in `Rendering/ColorPalette.cs`; output resolution must not alter palette values.
- Runtime behavior belongs in mutable instances/systems; authored values belong in validated JSON definitions.
- Maintain `GameSession.Update` order unless the change includes deterministic regression and co-op review.
- Do not use non-deterministic random/time sources inside synchronized gameplay. Use seeded/session-owned state.
- Networked actions must flow through validated `GameCommand` values and host sequencing.
- Any synchronized state addition must be represented in snapshots, validation, reconstruction, and checksums.
- Presentation-only state such as interpolation, cursor ghosts, and pings must not affect simulation checksums.
- Persistence writes must remain bounded, validated, atomic, and recoverable.
- Input must remain inactive when the game window is not focused.
- Keep content IDs stable where saves/history/discovery rely on them. UI display names may differ from internal IDs.

## Gameplay baselines

- Campaign: authored waves 1–30 on every difficulty.
- Final escalation: authored waves 21–30; Apex unlocks before wave 21.
- Generated Endless: wave 31 onward.
- Difficulties: Easy, Medium (`normal` internally), Hard, Bastion.
- Competitive directives: Standard, Signal Gauntlet (`close_quarters`), Core Six, Entrenched (`no_reserves`).
- Sandbox Lab is noncompetitive and solo-only.
- Maps: Foundry Loop, Crosswind Basin, Prism Circuit, Surge Divide (`relay_divide`).
- Co-op: direct TCP 28741, two players, shared defenses, host-command authority, deterministic 60 Hz simulation.

## Editing guidance

- Use `rg`/`rg --files` for discovery.
- Preserve unrelated user changes in a dirty worktree.
- Make focused edits; avoid large mechanical rewrites unless the task requires them.
- Update documentation when behavior, commands, profiles, persistence paths, networking requirements, or authored progression changes.
- Do not hand-edit generated balance reports or verification artifacts.

## Validation by change type

- Gameplay/content: regression suite plus focused headless simulations.
- UI/rendering/text: full hidden visual verification and screenshot inspection.
- Co-op: command, checksum, snapshot, reconnect, direction-validation, and deterministic runner tests.
- Persistence: save/history/discovery/settings round-trip, bounds, validation, backup, and deletion tests.
- Balance: matched seeds and controls first; broad matrix after focused results stabilize.

Balance-agent rates are comparative evidence, not direct human win predictions. Review per-map/per-policy data and layouts before changing universal values.

## Version-control handoff

Before committing:

1. Check `git diff --check`.
2. Run verification proportional to the change.
3. Inspect `git status --short` and avoid generated artifacts.
4. Commit a concise current-state change.
5. Push the verified commit to the active branch when requested by the project workflow.
