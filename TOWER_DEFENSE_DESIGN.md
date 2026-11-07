# Maximal Bastion — Technical Design

This document describes the implemented architecture and runtime invariants. Gameplay values are authored in JSON and should be read from the current content files rather than duplicated in code.

## Platform and solution

- Runtime: .NET 10.
- Framework: MonoGame DesktopGL 3.8.5.
- Game project: `src/MaximalBastion/MaximalBastion.csproj`.
- Deterministic test/simulation executable: `tests/MaximalBastion.Tests`.
- Content: runtime JSON under `src/MaximalBastion/ContentData`; the bundled Barlow and Oxanium fonts are compiled through MonoGame Content Builder.
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
- each map's complete authored campaign wave JSON

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
14. Evaluate defeat or campaign result state.

This order is a gameplay invariant. Changes require deterministic regression coverage and co-op checksum review.

## Combat

`TargetSelector` implements First, Last, Strongest, Weakest, Nearest, Fastest, Armored, and Support. First/Last use route progress; Support prioritizes active Signal Gauntlet carriers and otherwise uses strongest-target ordering.

`TowerSystem` delegates firing behavior to small behavior modules rather than a single tower switch. `ProjectileSystem` owns direct, area, chaining, beam, burn, predictive mortar, and specialized delivery behavior. `DamageResolver` centralizes shields, armor, pierce, Expose, Armor Break, rank effects, splash limits, kills, rewards, and source-attributed statistics.

Mortar prediction follows the target's current route velocity at launch. Authored impact caps bound the number of affected enemies. Status effects have deterministic duration, magnitude, source, and ticking state.

Signal Gauntlet carrier behavior is implemented by `EnemySignalRole` and `EnemySystem`. Carriers remain base enemy profiles with an additional role. `EnemyModelArt` colors the existing faceted gem with each role's shared palette; Disruptor uses saturated purple. `GameRenderer` draws each complete signal carrier after ordinary units, including its incoming attacks and compact hit feedback; it does not redraw gems independently. Carrier order therefore controls normal body/core and attack occlusion. Hostile transfers originate at the reactor and use paired rails with wavefronts or diamond packets. Shared signal palette values also drive support boundaries, tower interference, and status text.

`EnemyConditionPresentation` reads the status controller, shield, regeneration pause, and formation speed without mutating them. It supplies stable condition names and values for Intel. Expose color follows the strongest active status's source through `RunStatistics.TowerDefinitionByInstance`, including sold sources retained by attribution rules. `EnemyDebuffAuraArt` draws up to four soft color regions for sustained debuffs; `EnemyStunArt` draws bounded branching electrical discharges for Stun.

`EnemyHudPresentation` supplies visible model bounds and fixed-size closed health/shield frames. `GameRenderer` draws signal reactors and durability above transient combat light. Ordinary health bars are always present; shield meters show actual capacity whenever reserve is positive. `UIManager.EnemyIntel` holds a local selection and presents current statistics with a neutral resting model. Selection, geometry, and drawing do not enter snapshots or checksums.

`EnemyVisualTimeline` tracks shield feedback, Boss opening, and a reversible whole-cloud fog envelope in a renderer-owned cache bounded by living enemies. The first debuff starts a 0.10-second rise; the last expiry starts a 0.14-second fade. Nonempty color-combination changes preserve density and geometry, and reapplication resumes from current density. Session replacement or authoritative simulation rewind resets the cache. Removing fractional render lead during pause holds the current presentation instead of resetting transitions. The two menu feeds use separate renderer timelines. Reduced Effects keeps spatially steady fog and quiet condition cues. No presentation timestamps enter enemies, saves, snapshots, or checksums.

## Towers and progression

`TowerInstance` stores owner, position, level, doctrine, final specialization, Apex state, targeting, cooldowns, disruption/suppression, Protocol state, investment, and lifetime direct/support/control metrics.

Every tower has:

- one base level
- two tier-two doctrines
- two tier-three roles compatible with either doctrine
- one unique Protocol and auto-trigger rule
- one Apex promotion available beginning with authored wave 21

Upgrade previews derive the next runtime values using the same stat-building path as combat. Signal Beacon and Surge Node modifiers can be included in both current and preview values. Saves, history layouts, co-op snapshots, and checksums preserve all progression fields.

## Placement and maps

Maps use a polyline route, authored build rectangles, optional Surge Nodes, and entry/exit metadata. Placement is continuous. Validation checks the tower/Forge footprint against build regions, route clearance, other defenses, map bounds, and directive rules.

Placement assist resolves the geometrically nearest legal point within a bounded radius. It does not prioritize build areas by declaration order. The resolved ghost and range are local presentation until a click emits a placement command. Co-op transmits snapped preview state separately from confirmed simulation commands.

Pulse Plates use route projection and additional endpoint/spacing rules. The deployed field has a fixed cap. Plate knockback applies per-enemy grace and rank multipliers.

Surge Nodes are authored fields with focused bonuses. Effective tower modifiers use deterministic node/Beacon rules, and UI can report both base and modified values.

## Waves and progression

`WaveManager` owns intermission time, group index, group delay, spawn cadence, queued contacts, wave completion, campaign result flags, early-call state, and generated-wave mode.

- Waves 1–30 are authored campaign content on every difficulty.
- Waves 21–30 form the final escalation and unlock Apex tower promotions.
- Waves 31+ are created by `EndlessWaveGenerator` from the selected arena's wave-30 anchor.

Generated Endless rotates five archetypes. Its step-relative health multiplier is `min(10000, 1 + 0.085s + 0.0045s²)`. Count growth caps at 1.60×, speed at 1.30× of the anchor progression, spawn cadence has a 0.80 floor, and group delay has a 0.75 floor. Elite inserts increase to at most two per insertion point; the recurring boss archetype preserves the anchor boss group.

Result/history identity survives campaign continuation so a wave-30 victory and a later Endless terminal state update one run record.

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

`RunHistoryStore` stores terminal summaries, medal state, contribution/economy/tactical data, and serialized read-only final layouts. `UserSettings` stores display, audio, effects, hotkey-badge, and auto-start preferences. Each subsystem fails non-fatally when local persistence is unavailable.

Co-op saves are written by the host. They can be reopened as a hosted game or converted into a solo continuation without changing the underlying defense state.

## Online co-op

`LanCoOpTransport` implements bounded length-prefixed TCP frames on port 28741. Despite the class name, the same direct connection works over the internet when the host is reachable. The transport provides no relay, matchmaking, NAT traversal, or encryption.

`DeterministicSessionRunner` advances 60 fixed ticks per second. The host assigns command sequence/tick order; both peers apply identical commands and simulate locally. Pending command count, future tick distance, and applied-sequence history are bounded.

`SessionChecksum` includes economy, waves, enemies, towers, upgrades, Apex, targeting, statuses, tactical devices, Protocols, pause, speed, statistics, and relevant timers. The host sends periodic checksums. Divergence or reconnect produces an authoritative `CoOpStateSnapshot`, validated before replacement. Large snapshots use bounded Brotli framing with 2 MiB wire and 8 MiB decoded limits.

Variable-rate presentation interpolation is local-only and excluded from checksums. Remote cursors, pings, selected-tower labels, and placement ghosts are also presentation-only.

See [docs/co-op-architecture.md](docs/co-op-architecture.md) for the connection and recovery protocol.

## UI and input

`UIManager` owns screen layout, hit testing, setup flows, pause/save/history/settings/results screens, Tower Workshop/Intel, tactical controls, co-op lobby/status, medals/achievements, and Sandbox controls.

Input is processed only while the game window is active. Resolution/fullscreen changes update both viewport mapping and hit testing together. Mouse-driven menus do not retain hidden keyboard focus. Text fields explicitly own copy/paste/backspace behavior.

## Rendering

`EnemyModelArt` separates family/rank presentation radius from the unchanged `EnemyInstance.Radius` used by gameplay. `EnemyModelContours` supplies 15 mirrored material boundaries. `PrimitiveRenderer` caches their filled masks; the browser packs them into its shared primitive atlas. Family construction distinguishes Crawler's blunt front and tapered rear, Runner's dart, Brute's closed shoulder plates and ram, Aegis's notched open frame, and Regenerator's paired repair banks. Rank reinforces each family's machinery. `EnemySurfaceAnchors` locates vents, deposits, fractures, weak points, electrical contacts, and repair seams in chassis coordinates. Shields use cached, closed convex envelopes outside the hull, preserving separation from granular frost and internal channels. Every positive reserve renders the same complete shell; quantitative capacity belongs to the meter. Shield rendering reads `EnemyInstance.Shield > 0` for every family, independently of signal role; Bulwark grants can shield any eligible recipient.

`EnemyLocomotionPath` caches presentation-only corner paths per map. Each planned turn integrates a unit-speed direction whose curvature eases over the first and last 45%, with a short steady middle section. Smooth clearance offsets move the chassis toward the lane center through the bend; the offsets and their first two derivatives vanish at the endpoints. The tracked profile combines an outward apex adjustment with a slight displacement along the turn on the approach and exit, keeping the broad continuous hull inside the lane. Separate 128-interval Hermite tables store the adjusted center trajectories and their measured arc lengths. Consecutive position and heading queries share one exact-key cached pose per map; each curve sample uses one interval lookup for both outputs. Body heading follows the planned steering profile through a smooth inverse arc-length mapping, allowing modest anticipation independently of the tighter center trajectory. An arc-length timing profile with 24% eased shoulders and a steady middle section distributes the curved path's shorter distance over a wider approach and exit. The timing window reaches up to 3.8 motion radii, bounded by 48% of either adjacent straight segment; the curve reaches up to 1.8 motion radii within it. These windows do not overlap and rejoin the authoritative straight path exactly, preserving position and tangent continuity without accumulating visual lead or lag. `PresentationFrame` applies the appropriate local offset to every family, including Runner, at the rendered travel distance.

The path also caches a piecewise linear travel-phase mapping and its inverse for each presentation radius and family step-shortening weight. Its slope follows visual travel rate, with gradual corner weights of 0.50 for Crawler, 0.85 for Brute, and 1.25 for Aegis. `EnemyLocomotion` samples fixed world-space stance contacts and eased return steps in separate front/rear and left/right corridors, using the same curved centers and headings as the body. Returns integrate a velocity profile with 25% eased shoulders and a steady middle section, preserving zero velocity and acceleration at the planted endpoints. Contact and knee constraints preserve the paved lane margin. Gait phase derives from actual traveled distance, so Slow changes its rate and Stun stops it without a second clock. Reduced Effects retains the same contacts. The cached geometry and phase profiles add no per-frame allocations or synchronized state; they do not alter gameplay positions, movement speed, collision radius, commands, or support eligibility.

`EnemyLocomotion.ContactSlots` supplies four contacts for Crawler, six for Brute, eight for Aegis, and none for Runner or tracked Regenerator. Fixed world contacts support stance while continuous shallow return paths remain within the lane deck. `EnemyModelArt.BodyRadius` defines the size hierarchy; `MotionRadius` controls presentation rounding. Runner's dart and thruster artwork follow the shared curved flight path. Regenerator's upper tread run moves toward its nose with actual travel. Intel uses a separate bilateral resting pose. See [enemy condition rendering](docs/enemy-condition-rendering.md) for the family dimensions and review invariants.

`ColorPalette` owns resolution-independent theme values. `NightGridArt` draws procedural hardware with the same silhouettes in the field, Workshop, Intel, and placement previews. Authored tower visual colors match the centralized hardware accents so projectiles and Protocol effects retain their weapon identity.

`TowerModelArt` supplies four structural stages for each tower family, including tier-specific foundations, rotating assemblies, and Apex hardware. Foundation shadows follow the actual footprint, and tier lamps draw underneath rotating weapons. Frost's crystal and coolant banks share a concentric rotating assembly; Mortar's recoil housings surround its central launch well symmetrically. Ember's chamber and armor share the aiming pivot, with a short nozzle seated beneath the chamber. `TowerTierVisuals` shares weapon dimensions and bank positions between models, muzzle origins, and attached effects. `ProjectileArt`, `MortarShellArt`, `PrismBeamArt`, and `TowerAttackArt` provide corresponding shot, muzzle, impact, chain, and support signatures. Reduced Effects preserves the structural and attack silhouettes while fixing decorative effect phases and suppressing bloom.

`AttackAppearance` captures the firing tower's family, tier, and Apex state on local projectiles and effects. In-flight attacks retain this appearance when their source upgrades or is sold. This presentation metadata is excluded from synchronized snapshots and checksums; reconstructed shots resolve their appearance from the source tower, with recorded family identity as a fallback. Attack dimensions do not alter damage, range, targeting, or collision.

Direct-hit contact radii grow modestly across the four tower stages and remain bounded to 14 logical pixels, with smaller Shard pellet contacts. Family materials distinguish kinetic sparks, armor fragments, frost crystals, flame, electrical branches, and refracted light. Every Arc and Prism beam endpoint has a compact contact effect. Direct contacts and beam endpoints follow the recipient's displayed center through `PresentationFrame.EffectTargetPosition`, including rounded corners and the last displayed position after death or escape. These short-lived target references do not enter synchronized state. Each enemy draws its body, incoming projectiles or beams, and one merged compact contact as a complete unit. Subsequent bodies occlude that unit's transport and hit feedback together. Ordinary enemies retain their list order, followed by foreground signal carriers in their list order. Reused target buckets group incoming attacks once per frame; drawing each body does not scan the entire projectile or effect collection.

Projectile Splash effects draw with their live damage recipient, above that enemy's body and beneath subsequent bodies in the same rendering order as direct hits. The complete area effect keeps this ordering throughout its lifetime, including after the compact contact fades. Its spread follows the projectile's effective damage radius, including doctrine and Apex modifiers; visual tier scaling does not enlarge it again. Frost uses drifting crystal mist, Breaker uses debris and dust, and Ember and Mortar use overlapping fire or pressure plumes. Irregular pockets and scattered fragments suggest the affected area, with larger splashes spreading wider while remaining translucent and brief. Splash centers remain at the authoritative impact point, and target caps still determine which eligible enemies receive damage. One area effect represents each detonation, retaining the nearest surviving actual damage recipient for rendering order and a brief body contact. Unbound attacks and fading contacts on dead or escaped recipients render beneath active bodies at their retained positions. Reduced Effects retains contact and area information with fewer fragments. Burn ticks use the ongoing status presentation, and Signal Beacon retains its support feedback.

`RicochetArt` draws Needle ricochets as traveling copies of the original projectile, retaining its radius, color, and tier appearance. The initial gap and firing speed set the flight duration; the shot and its contact flash follow the selected enemy's current rendered center through `PresentationFrame`, including interpolation and rounded corners. A transient target reference retains the final displayed location after death or escape. The bounded visual timeline is presentation-only; secondary target selection and damage resolve at the primary hit.

`EffectPurpose` distinguishes combat, Protocol ignition, Forge charge completion, and enemy escape on bounded transient effects. It does not change their `EffectKind`, lifetime, capacity priority, or synchronized gameplay state. `GameRenderer` separates ground effects, enemy-owned attacks, and foreground tower/signal feedback. `ImpactBurstArt` draws family-specific contact fragments and irregular volumetric detonations using cached energy masks and short streaks. `TransientBurstArt` draws Protocol releases, Pulse Plate detonations, enemy debris, inward escape feedback, and Forge charge intake. Area Protocol effects retain the exact authored burst radius; compact activations use their local flash radius. Transient motion derives from remaining lifetime and stable positional variation, without random simulation draws or persistent particle state. Co-op pings and precise range boundaries retain their separate tactical rendering.

`TowerHardwareChannels` defines powered recessed conductors and separate Beacon receiver contacts fitted to each family's tier, scale, and aim. `TowerModelArt` draws the operational conductors with restrained ambient energy and a localized response to actual firing. `TowerProtocolArt` intensifies these conductors, draws a cached luminous energy envelope beneath tower models, and adds short family-specific flows above them. Its presentation strength reads the live Protocol duration and remaining timer with frame interpolation: an immediate 55% ignition rises to full strength over 0.08 seconds, holds until the final 0.05-second fade, and terminates with the active state. Desktop and browser initialize the field and impact density masks at renderer startup; the browser keeps them in the shared primitive atlas. Cached masks and bounded primitive loops render the fields without per-frame allocations or synchronized state changes. Reduced Effects fixes decorative phases and suppresses supplemental bloom while retaining the energy field and timer envelope. `TowerStatusArt` independently energizes green Beacon receiver contacts and draws interrupted channels and output clamps. `EnemyStatusArt` reads health, shields, regeneration cooldowns, formation speed, and status timers to draw local hull and drive effects; `EnemySignalArt` supplies carrier hardware and short transfer feedback. `SignalAppearance` records the role and source/recipient instance IDs on existing transient effects, allowing repair, shielding, and interference pulses to follow their live recipients without changing synchronized state.

`BattlefieldInteractionArt` supplies foundation-fitted selection and placement lighting, ownership contacts, and the attached Auto controller. The Auto controller switches from waiting lamps to a steady active readout using the armed tower's live Protocol timer. Ground projections draw beneath rotating weapon assemblies; selected and Auto-armed hardware remains readable above transient combat. `PowerNodeArt` supplies embedded relay devices and node-colored couplers beneath local and remote snapped placement previews. Placed towers expose node coverage through Tower Intel rather than persistent tower markers or world-space connection lines; the Intel source uses the node's authored color, separate from green Beacon support. Range boundaries remain precise continuous tactical lines. Health/shield bars, co-op player terminals, and Sandbox controls retain their separate information roles. Reduced Effects preserves essential state lighting with a fixed phase while suppressing decorative glow.

`GameRenderer.TowerAim` separates directional aiming from radial machinery: Shard, Frost, and Mortar assemblies rotate at constant presentation rates, while Beacon hardware stays fixed. Frost's crystal, coolant banks, and attached state lighting share one assembly angle. Shard launch filaments follow actual near-source projectiles from its central aperture; these visual calculations do not alter projectile positions, targets, timing, or checksums.

`MapEnvironmentRenderer` draws the ground, scenery, build decks, routes, and selector emblems for four environment families. `ColorPalette.Environment` centralizes their surface and lighting colors. Map IDs and path-style keys remain stable for persistence; display names describe the current locations. Furnace machinery, elevated utility decks and cooling housings, floating wire structures, and circular reactors use separate geometry beneath the shared node and combat layers. Route edge motifs stop beyond the adjoining lane footprint at turns, and the remaining motif spacing is centered between those limits. Continuous district edges remain intact. Environmental animation reads session time without mutating it, and Reduced Effects fixes its phase at zero. See [map art direction](docs/map-art-direction.md) for the visual conventions.

`CommandSurfaceArt` separates layered graphite surfaces from asymmetric peripheral service infrastructure and bounded signal motion. HUD and nested panels use only the static material. `UiTypography` supplies separate interface and high-resolution display assets; `UIManager.Typography` applies consistent font measurement and drawing. `DefenseTerminalArt` composes the command environment, surveillance framing, and setup map previews from authored routes and shared environment palettes. `UIManager.MainMenuActions` supplies the visible actions and their hit rectangles; Play enters solo setup. `SetupCardRectangle` supplies both setup rendering and selection bounds. Ambient menu time is presentation-only; Reduced Effects freezes it and is also forwarded to the live battle renderer.

`UIManager.Presentation` holds menu theme constants, tab geometry, and overview rendering. Shared panel, button, and selection helpers give menus, dialogs, and setup consistent surfaces and interaction states. Settings visibility guards match the active tab. Tower and result detail flags belong only to the interface; Escape dismisses the detail layer first. See [interface design](docs/interface-design.md) for screen hierarchy and component conventions.

`PrimitiveRenderer` shares one premultiplied radial texture across all glow sizes, and disposes it with its other cached resources. Light pools, trails, spark rays, and beam cores use the existing bounded transient-effect collection. Rendering reads recoil and damage feedback timers without modifying the simulation. Decorative motion and HUD credit pulses are presentation state; they are excluded from snapshots and checksums. Reduced Effects suppresses decorative combat layers while preserving targeting, health, status, ownership, and selection indicators.

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
src/MaximalBastion/
  Analytics/      career, medals, achievements, run statistics
  Audio/          procedural music and synthesized effects
  Combat/         targeting, buffs, projectiles, damage
  ContentData/    authored JSON definitions
  Core/           shared constants, input, viewport, metrics
  Data/           definitions and content loading
  Diagnostics/    crash and hidden visual verification
  Economy/        credits, lives, rewards, sales
  Effects/        bounded combat effects and statuses
  Enemies/        runtime enemies and signal roles
  Maps/           route/build/node runtime
  Multiplayer/    transport, commands, fixed ticks, snapshots
  Persistence/    saves, history, settings
  Rendering/      Night Grid hardware, infrastructure, palette, and primitives
  Simulation/     deterministic agents and reports
  Tactics/        Plates and Charge Forge
  Towers/         tower state, behaviors, progression
  UI/             screens and Tower Intel
  Waves/          authored flow, intel, Endless generation
tests/MaximalBastion.Tests/
  deterministic regression and simulation CLI
```

## Change discipline

Gameplay/content edits must preserve deterministic ordering and serialization compatibility or explicitly update their validators/migrations. Networked state additions require command/snapshot/checksum coverage. UI changes should pass hidden rendering at the representative output sizes. Balance changes should run focused controls before a full matrix and should be interpreted alongside human play rather than by aggregate win rate alone.
