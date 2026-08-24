# Minimal Bastion — Technical Design

This document describes the implemented architecture and runtime invariants. Gameplay values are authored in JSON and should be read from the current content files rather than duplicated in code.

## Platform and solution

- Runtime: .NET 10.
- Framework: MonoGame DesktopGL 3.8.5.
- Game project: `src/MinimalBastion/MinimalBastion.csproj`.
- Deterministic test/simulation executable: `tests/MinimalBastion.Tests`.
- Content: runtime JSON under `src/MinimalBastion/ContentData`; the interface font is compiled through MonoGame Content Builder.
- Supported packaged target: self-contained Windows x64.

The architecture is conventional object-oriented C#. It intentionally avoids an ECS, physics engine, networking framework, and dependency-injection container.

## Coordinate and rendering model

The tactical layout uses a fixed 1280×720 logical canvas:

- Battlefield width: 960 logical pixels.
- Sidebar width: 320 logical pixels.
- Internal scene: 2560×1440 for high-density geometry and fonts.
- Output: clipped 16:9 viewport with letterboxing as required.

`Game1` owns the backbuffer, display transitions, focus-safe input, and presentation mode. `GameRenderer` renders the logical scene through `PrimitiveRenderer` and map/tower/enemy-specific helpers. `ColorPalette` centralizes theme constants so DPI and output changes do not alter the palette.

The game generates shape masks, icons, tower/enemy silhouettes, effects, menu combat, and audio procedurally. Effects are bounded, and essential tactical cues have priority over cosmetic flashes in dense waves. UI verification can render real scenes in a hidden non-activating window.

## Top-level runtime

`Game1` coordinates application screens, loaded content, settings, persistence, co-op transport, audio, input, rendering, and the active `GameSession`.

`GameSession` is the authoritative match aggregate. It owns:

- `MapRuntime`
- `Economy`
- `WaveManager`
- enemies and IDs
- `TowerInstance` objects and placement state
- `ProjectileSystem`
- `EffectSystem`
- `TacticalDefenseSystem`
- active difficulty/directive definitions
- run statistics and progression state
- solo/co-op pause, ready, speed, and Protocol automation state

Systems receive the session when they need cross-domain operations. Runtime instances contain mutable state; definitions remain immutable configuration objects.

## Content model

`ContentLoader` loads and validates:

- `Towers.json`
- `Enemies.json`
- `Tactics.json`
- `Difficulties.json`
- `Challenges.json`
- each map JSON
- each map's authored campaign/Mastery wave JSON

Important definitions include `TowerDefinition`, tower level/doctrine/specialization/Apex definitions, `EnemyDefinition`, `MapDefinition`, `PowerNodeDefinition`, `DifficultyDefinition`, `ChallengeDefinition`, `TacticalDefinition`, and `WaveDefinition`.

Validation rejects missing IDs, duplicate identities, invalid map/campaign attachment, malformed paths/build zones, unknown tower/enemy references, invalid progression, unsupported values, and other content that would make deterministic reconstruction unsafe. The recursive gameplay-content fingerprint is part of the co-op handshake.

## Fixed update order

`GameSession.Update` clamps a supplied step to 0.1 seconds and applies the selected 1x/2x speed. Solo presentation supplies frame time; co-op advances the same method through fixed 1/60-second ticks.

The active update order is:

1. Exit if the run has reached victory or defeat.
2. Handle shared co-op pause. Simulation freezes, but an existing intermission deadline continues in real planning time.
3. Advance run statistics, announcements, and global Protocol cooldown state.
4. Update Sandbox wave controls, or intermission and authored/generated wave spawning.
5. Move/update enemies and their signal abilities.
6. Update Pulse Plates and Charge Forge.
7. Evaluate the armed automatic Protocol.
8. Recalculate Beacon, node, and other tower buffs.
9. Update towers and acquire/fire on targets.
10. Update projectiles and resolve impacts.
11. Update visual effects.
12. Remove dead/escaped enemies.
13. Complete the wave if spawning and active enemies are exhausted.
14. Evaluate defeat or campaign/Mastery result state.

This order is a gameplay invariant. Changes require deterministic regression coverage and co-op checksum review.

## Combat

`TargetSelector` implements First, Last, Strongest, Weakest, Nearest, Fastest, Armored, and Support. First/Last use route progress; Support prioritizes active Signal Gauntlet carriers and otherwise uses strongest-target ordering.

`TowerSystem` delegates firing behavior to small behavior modules rather than a single tower switch. `ProjectileSystem` owns direct, area, chaining, beam, burn, predictive mortar, and specialized delivery behavior. `DamageResolver` centralizes shields, armor, pierce, Expose, Armor Break, rank effects, splash limits, kills, rewards, and source-attributed statistics.

Mortar prediction follows the target's current route velocity at launch. Authored impact caps bound the number of affected enemies. Status effects have deterministic duration, magnitude, source, and ticking state.

Signal Gauntlet carrier behavior is implemented by `EnemySignalRole` and `EnemySystem`. Carriers remain base enemy profiles with an additional role, not separate health/speed definitions. Rendering uses an in-body role glyph and recipient feedback, and carriers are drawn above ordinary enemies for readability.

## Towers and progression

`TowerInstance` stores owner, position, level, doctrine, final specialization, Apex state, targeting, cooldowns, disruption/suppression, Protocol state, investment, and lifetime direct/support/control metrics.

Every tower has:

- one base level
- two tier-two doctrines
- two tier-three roles compatible with either doctrine
- one unique Protocol and auto-trigger rule
- one Apex promotion available after entering Mastery

Upgrade previews derive the next runtime values using the same stat-building path as combat. Signal Beacon and Surge Node modifiers can be included in both current and preview values. Saves, history layouts, co-op snapshots, and checksums preserve all progression fields.

## Placement and maps

Maps use a polyline route, authored build rectangles, optional Surge Nodes, and entry/exit metadata. Placement is continuous. Validation checks the tower/Forge footprint against build regions, route clearance, other defenses, map bounds, and directive rules.

Placement assist resolves the geometrically nearest legal point within a bounded radius. It does not prioritize build areas by declaration order. The resolved ghost and range are local presentation until a click emits a placement command. Co-op transmits snapped preview state separately from confirmed simulation commands.

Pulse Plates use route projection and additional endpoint/spacing rules. The deployed field has a fixed cap. Plate knockback applies per-enemy grace and rank multipliers.

Surge Nodes are authored fields with focused bonuses. Effective tower modifiers use deterministic node/Beacon rules, and UI can report both base and modified values.

## Waves and progression

`WaveManager` owns intermission time, group index, group delay, spawn cadence, queued contacts, wave completion, campaign/Mastery result flags, early-call state, and generated-wave mode.

- Waves 1–20 are authored campaign content.
- Waves 21–30 are authored Mastery content.
- Waves 31+ are created by `EndlessWaveGenerator` from the selected arena's wave-30 anchor.

Generated Endless rotates five archetypes. Its step-relative health multiplier is `min(10000, 1 + 0.085s + 0.0045s²)`. Count growth caps at 1.60×, speed at 1.30× of the anchor progression, spawn cadence has a 0.80 floor, and group delay has a 0.75 floor. Elite inserts increase to at most two per insertion point; the recurring boss archetype preserves the anchor boss group.

Result/history identity survives campaign continuation so wave 20 and a later Mastery/Endless terminal state update one run record.

## Economy and tactical systems

`Economy` owns credits, lives, kills, escapes, and categorized credit totals. Normal selling returns 60% of invested value. Difficulty and directive credit modifiers are applied when the session is initialized.

`TacticalDefenseSystem` updates Plate triggers and Charge Forge production. Forge production advances only during active waves. Plate purchase escalation is scoped to the active wave. Protocol activation is a tower command and uses deterministic cooldown/duration state.

## Persistence

`SaveGameStore` provides:

- one rolling autosave
- expandable numbered manual slots
- save duplication
- confirmed deletion
- bounded JSON generations
- atomic replacement and one `.bak` recovery copy
- structural/content validation before reconstruction

`RunHistoryStore` stores terminal summaries, medal state, contribution/economy/tactical data, and serialized read-only final layouts. `DiscoveryProgress` stores discovery-gated Tactical Library entries. `UserSettings` stores display, audio, effects, hotkey-badge, and auto-start preferences. Each subsystem fails non-fatally when local persistence is unavailable.

Co-op saves are written by the host. They can be reopened as a hosted game or converted into a solo continuation without changing the underlying defense state.

## Online co-op

`LanCoOpTransport` implements bounded length-prefixed TCP frames on port 28741. Despite the class name, the same direct connection works over the internet when the host is reachable. The transport provides no relay, matchmaking, NAT traversal, or encryption.

`DeterministicSessionRunner` advances 60 fixed ticks per second. The host assigns command sequence/tick order; both peers apply identical commands and simulate locally. Pending command count, future tick distance, and applied-sequence history are bounded.

`SessionChecksum` includes economy, waves, enemies, towers, upgrades, Apex, targeting, statuses, tactical devices, Protocols, pause, speed, statistics, and relevant timers. The host sends periodic checksums. Divergence or reconnect produces an authoritative `CoOpStateSnapshot`, validated before replacement. Large snapshots use bounded Brotli framing with 2 MiB wire and 8 MiB decoded limits.

Variable-rate presentation interpolation is local-only and excluded from checksums. Remote cursors, pings, selected-tower labels, and placement ghosts are also presentation-only.

See [docs/co-op-architecture.md](docs/co-op-architecture.md) for the connection and recovery protocol.

## UI and input

`UIManager` owns screen layout, hit testing, setup flows, pause/library/save/history/settings/results screens, Tower Workshop/Intel, tactical controls, co-op lobby/status, medals/achievements, and Sandbox controls.

Input is processed only while the game window is active. Resolution/fullscreen changes update both viewport mapping and hit testing together. Mouse-driven menus do not retain hidden keyboard focus. Text fields explicitly own copy/paste/backspace behavior.

The Tactical Library uses left/right page navigation and discovery gates. In co-op it is a local overlay: network polling and simulation continue while local world input is blocked.

## Audio

`AudioManager` synthesizes UI, tower, enemy, boss, tactical, victory/defeat, and procedural music voices. Per-effect and global cadence limits avoid an audio wall during dense combat. Audio has no influence on deterministic state and degrades safely when no audio device is available.

## Diagnostics and verification

`CrashReporter` writes one bounded latest-crash report without save, join-code, or host-address data. `DebugOverlay` is Debug-only. `VisualVerificationGame` renders representative UI/game states without activating the window or sending input.

`scripts/verify.ps1`:

1. selects the workspace-local or installed SDK
2. builds into an isolated `%TEMP%` output
3. runs the deterministic regression executable
4. optionally runs hidden UI verification
5. writes artifacts under `.artifacts/verification`

The test executable covers content loading/validation, maps/waves, placement, combat/status behavior, progression, economy, persistence/recovery, UI state helpers, co-op commands/snapshots/checksums/reconnect rules, and simulation reports.

## Project layout

```text
src/MinimalBastion/
  Analytics/      career, medals, achievements, run statistics
  Audio/          procedural music and synthesized effects
  Combat/         targeting, buffs, projectiles, damage
  ContentData/    authored JSON definitions
  Core/           shared constants, input, viewport, metrics
  Data/           definitions and content loading
  Diagnostics/    crash and hidden visual verification
  Economy/        credits, lives, rewards, sales
  Effects/        geometric effects and statuses
  Enemies/        runtime enemies and signal roles
  Maps/           route/build/node runtime
  Multiplayer/    transport, commands, fixed ticks, snapshots
  Persistence/    saves, history, discovery, settings
  Rendering/      palette and all geometric rendering
  Simulation/     deterministic agents and reports
  Tactics/        Plates and Charge Forge
  Towers/         tower state, behaviors, progression
  UI/             screens and Tower Intel
  Waves/          authored flow, intel, Endless generation
tests/MinimalBastion.Tests/
  deterministic regression and simulation CLI
```

## Change discipline

Gameplay/content edits must preserve deterministic ordering and serialization compatibility or explicitly update their validators/migrations. Networked state additions require command/snapshot/checksum coverage. UI changes should pass hidden rendering at the representative output sizes. Balance changes should run focused controls before a full matrix and should be interpreted alongside human play rather than by aggregate win rate alone.
