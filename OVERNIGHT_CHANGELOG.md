# Overnight Changelog

- Added a deterministic-safe co-op link indicator based on valid inbound-traffic age. Both players now see LIVE, DELAY, STALLED, or RESYNC in the sidebar before the existing 15-second reconnect threshold, with no gameplay state or extra protocol traffic.
- Added a native-verified Systems page to the title/pause Tactical Library. Six clean reference cards explain progression, targeting, status precedence, manual/automatic Protocols, Beacon/Surge stacking, and data-driven Pulse Plate/Forge values without crowding live Tower Intel.
- Carried every route's dash pattern continuously through corners instead of restarting it on each straight. Gold road markings no longer imply separate tiles at bends, and Surge Divide's moving power packets now flow around the complete rail without visible jumps.
- Cleaned defeat-field inspection without altering the concluded simulation: surviving enemies, defenses, roads, nodes, and ranges remain visible, while projectiles and momentary attack rings from the fatal tick are suppressed instead of appearing frozen. This is renderer-only, so solo/co-op checksums remain untouched.
- Enforced the two-player wave gate at the transport boundary. Player 2 can no longer bypass the readiness coordinator with a raw `StartWave` request, false readiness cannot count as consent, and host ready flags must agree with the authoritative P1/P2 mask.
- Tightened reconnect snapshot validation around bounded run/announcement strings and live homing-projectile references. A malformed orphan projectile can no longer restore as a client-only miss and immediately provoke another checksum repair loop.
- Removed the last visible legacy “relay node” label from Prism Circuit and standardized current documentation on Surge Nodes and tower-specific Protocols without changing stable IDs or save/network schemas.
- Added crisp geometric impact cues for direct projectiles, closing the feedback gap where Needle, Shard, Ember, Frost, and Breaker shots could vanish on contact without a hit response. Minor cues obey the existing hard effect budget and yield before displacing co-op pings, beams, or tactical blast rings.
- Expanded Tactical Library Protocol references beyond the compact live-label limit. Every unique burst now exposes its pulse damage plus exact Slow, Burn, Expose, Armor Break, or Stun magnitude/duration, while the battlefield continues showing only the first three bonuses to prevent sidebar clutter.
- Corrected Network Surge's range label to `AURA/TOWER RANGE`, making clear that the same temporary bonus expands Beacon coverage and recipient-tower reach exactly as the existing mechanics already do.
- Made automatic Protocol behavior explicit in planning UI. Tactical Library entries now show active duration, cooldown, pressure threshold, and elite override; an armed tower's live Intel likewise says that any elite/boss can trigger below the normal nearby-enemy count.
- Removed Signal Beacon's inherited, nonfunctional `RATE +75%` Protocol claim and labeled its Tactical Library header `AURA SUPPORT / NO TARGETING`. Network Surge retains only the aura-rate/range effects it actually applies, with no combat-stat change.
- Rotated Player 2's request-replay window when the host accepts a reconnect while preserving global authoritative sequence and scheduled commands. A friend who restarts their client can now resume at request ID 1 instead of having every control mistaken for an old duplicate.
- Validated and size-checked captured checkpoints before touching the primary generation. An internally inconsistent or unexpectedly huge deep-run snapshot can no longer be reported as saved only to become unreadable on the next launch.
- Separated graphics application from settings persistence. A read-only/full local-data directory no longer masquerades as an unsupported display mode or forces a successfully applied resolution back to 1280x720; the live choice remains active with a clear save warning.
- Rejected empty or oversized settings generations before JSON allocation. Startup now falls through to the last-known-good display/audio configuration when a local settings file is implausibly large.
- Applied the checkpoint safety contract to persistent Run History: generations are size-bounded before allocation, records and labels are validated on both read and write, implausible record counts are rejected, and an oversized primary transparently recovers from its last-known-good generation.
- Bounded checkpoint files and nested collections before expensive reads or reconstruction. Empty/oversized primaries, implausible tower/plate/stat counts, and oversized handled-enemy/specialization lists now recover through the known-good backup instead of risking excessive local allocation; normal save-slot quantity remains filesystem-limited rather than capped.
- Extended save backup recovery across full content-aware session reconstruction. A valid-JSON primary with an unavailable map/tower, invalid authored branch/level, or incompatible runtime invariant now falls back to the known-good generation instead of failing after the repository had already accepted it.
- Guarded the final deep-endless identity boundary. Exhausted 32-bit tower and Pulse Plate IDs now produce a clear placement-capacity message without spending, while enemy spawning remains saturated instead of wrapping into negative or duplicate IDs that could corrupt targeting, saves, or co-op checksums.
- Saturated the per-wave direct Pulse Plate purchase counter. An extreme restored endless state at the integer limit can no longer wrap its escalating-price counter negative after paying the capped price.
- Isolated save-storage failures from gameplay startup and frame pacing. New Game, Host, and Restart now fall back to an unslotted run if slot discovery fails; Load Saves reports an unavailable store without crashing; and autosave makes one attempt per completed wave instead of hammering a failing disk every frame. Manual Save and the next wave still retry normally.
- Kept Signal Beacon topology live during shared co-op pause. Deterministically placed, sold, or upgraded support towers now update planning intel and effective ranges immediately while every combat/economy/cooldown timer remains frozen.
- Unified branch-planning math with live combat math. Tier-two doctrine and tier-three specialization hover previews now include the selected tower's strongest Signal Beacon and Surge Node modifiers in every displayed damage, rate, range, and pierce comparison, matching the existing linear `NEXT` preview.
- Added a conservative 15-second co-op heartbeat timeout using the existing checksum exchange. A silently broken Wi-Fi/router path now enters preserved-session reconnection instead of waiting indefinitely for the operating system's TCP timeout; unusually large resumed-frame samples are clamped to avoid false disconnects.
- Drained in-flight connect, accept, receive, and send tasks during every co-op teardown. A late successful connection is disposed instead of becoming an unowned socket, while expected cancellation faults are observed safely.
- Added a post-snapshot checksum fence and bounded tick window. Delayed checksums from the state that was just repaired can no longer arrive after resume and trigger a false second resynchronization.
- Enforced host/client message direction after the handshake. Echoed host checksums/snapshots and client-only requests are rejected before they can mutate sync state.
- Added type-aware validation for complete co-op envelopes, including defined message kinds, bounded semantic fields, finite presence coordinates, player/command identity agreement, receipts, checksums, and authoritative snapshots. A real loopback regression proves malformed framed traffic is rejected at the transport edge.
- Bounded host and client connection handshakes to ten seconds. A half-open TCP peer can no longer occupy the private host listener indefinitely; failure returns to the existing retry/accept flow with a clear timeout message.
- Centralized structural validation for live and snapshotted co-op commands. Null IDs, nonfinite coordinates, invalid enums/entities, and unsupported speed values are rejected before sequencing or gameplay lookup; malformed authority traffic requests a clean resync.
- Added a strict 64-frame connection-level outbound budget before serialization/allocation. A stalled socket now faults into the existing preserved-session reconnect path instead of retaining an unbounded internal write queue.
- Hardened procedural-audio startup cleanup: generated WAV streams are disposed immediately, and a mid-construction device failure releases every sound already created before falling back to silent play.
- Carried shared-pause attribution through authoritative commands, reconnect snapshots, and deterministic checksums; the pause banner now states whether P1 or P2 paused the defense.
- Added visible keyboard focus to pause and result actions. Arrow keys/Tab move selection, Enter activates, unavailable save/load actions are skipped, and keyboard Restart retains the same two-step protection as mouse input.
- Reset pause/result focus only when each screen is newly entered, preventing an old destructive or leave selection from carrying into a later pause or completed run.
- Added visible keyboard focus to the online co-op entry screen. Up/Down selects Host, available Join, or Back; Enter activates; typing or choosing either connection field naturally returns focus to Join.
- Added deterministic shared co-op pause. Either player can use Esc, P, or the HUD button; both peers freeze combat on the same fixed tick, reconnect snapshots preserve the pause, and defense planning remains available until either player resumes.
- Added a visible title-menu keyboard focus. Arrow keys or Tab traverse every enabled selector/action, Left/Right adjusts the focused arena/difficulty/directive, and Enter activates it while New Game remains the safe default.
- Made the preserved-session rejoin code clickable and Ctrl+C-copyable, with clear reconnect-overlay feedback so a host can resend it without abandoning the match.
- Protected solo and co-op runs from accidental resets: Restart now arms a clearly coral `CONFIRM RESTART` action on both pause and result screens. The confirmed fresh run still claims a new slot, preserving the previous checkpoint.
- Added complete keyboard operation to Settings with a visible focus outline, Up/Down selection, bidirectional resolution/volume adjustment, Enter activation, and unchanged immediate persistence.
- Replaced unbounded newline-based co-op reads with explicit length-prefixed UTF-8 frames. Both peers reject invalid or oversized lengths before allocating the declared payload, while the existing single-writer queue preserves ordered sends.
- Made the host's six-character co-op join code a clear copy target. Clicking the code or pressing Ctrl+C copies it, with inline success/failure feedback in the waiting lobby.
- Added conventional arrow-key navigation to dynamic save slots, paginated run history, and every Tactical Library page. Selection follows page changes, cancels armed deletes, and keeps Enter confirmation deterministic.

## Arena progression, roles, protocols, and presentation pass

Date: 2026-08-15

- Added Crosswind Basin, a fourth arena with a seamless cyan-banked channel, current-chevron field motif, three compact crossfire islands, no power nodes, a distinct starting economy, and a runner-led authored 20-wave campaign.
- Completed the missing five-seed Easy matrix across the original three arenas: 150/180 wins (83.3%), with only Economy and indiscriminate Spam consistently failing.
- Extended lifetime Tower Intel beyond direct damage/kills: individual Beacons retain assisted damage, while source towers retain control, expose, and armor-break enemy-seconds. The values survive save/load and co-op snapshots and participate in deterministic state checks.
- Added data-derived campaign intel to the arena selector. Every map now previews its opening threat mix, total contacts, peak density, final health multiplier, and boss timing before a run; native high-resolution QA confirmed the extra planning line remains readable.
- Regenerated canonical five-seed matrices across all four arenas and all four difficulties after the doctrine and range-economy passes (960 runs): Easy 82.5%, Normal 75.4%, Hard 58.3%, and Bastion 16.7%. Every doctrine and final specialization appears in winning Hard runs; Easy is nearly map-neutral while Surge remains decisively hardest on Hard and Bastion.
- Added exact marginal-damage attribution for Expose and Armor Break. The resolver compares the real hit against no-expose/no-break counterfactuals without altering damage, avoids interaction double-counting, persists per-tower assist, includes it in co-op checksums, and shows it in Tower Intel/results. A 240-run Hard pass measured 2.30M Prism Expose assist and 3.51M Breaker Armor Break assist.
- Persisted historical tower-instance attribution so a lingering Slow, Expose, or Armor Break remains credited after its source tower is sold and the match is saved or resynchronized.
- Expanded deterministic checksums to cover latent next-entity IDs and invested tower/forge credits, detecting mismatches before a future placement, spawn, or sale can turn them into gameplay divergence.
- Added one-generation recovery backups for every overwritten save slot. Missing/corrupt primaries load and enumerate through the backup, while slot deletion removes both generations.
- Protected known-good save and run-history backups from being overwritten by a corrupt primary during a later autosave/update; repeated-corruption recovery now has dedicated regressions.
- Extended recovery validation beyond JSON syntax: parseable but structurally empty checkpoints and history records are now rejected before they can be loaded or rotated over a known-good recovery generation.
- Extended checkpoint validation through nested tower, tactical, economy, wave, and contribution state. Stale next-entity identities, invalid auto-Protocol references, duplicate metrics, and malformed specialization telemetry now recover from the prior generation before restore can partially mutate a session.
- Added a bounded local top-level crash report with build/runtime/OS context and the full exception. It deliberately excludes save and connection data and replaces only `%LocalAppData%\MinimalBastion\Logs\latest-crash.log`.
- Made display/audio settings atomic and recoverable with one last-known-good generation, including protection against a corrupt primary being rotated over its valid backup and normalization of nonfinite runtime volume values.
- Added persistent paginated Run History inside Load Saves. Terminal solo/co-op summaries survive independently of checkpoints, endless continuation updates the original campaign entry through a shared run ID, and records support confirmed deletion plus one-generation recovery.
- Added four independently selectable challenge directives: Standard, Close Quarters, Core Six, and No Reserves. Restrictions are authoritative across UI, hotkeys, and co-op; fixed opening compensation and directive identity persist through saves, reconnects, results, history, and checksums.
- Measured 432 Hard directive runs: Close Quarters 53.5%, Core Six 34.7%, and No Reserves 61.1%. A separate 144-run Hard endless matrix produced zero wave-40 survivors; Control peaked at wave 38 and heavy Plate strategies still failed.
- Added two data-driven tier-two doctrines to every tower. Either doctrine can feed either final role, producing four completed builds per tower while retaining the three-level silhouette language.
- Persisted doctrine identity through saves, co-op commands, reconnect snapshots, checksums, run statistics, and per-tower analytics. Legacy level-two saves without doctrine metadata remain loadable.
- Rebuilt Tower Library previews around Level 1, two clickable doctrines, and two final-role cards whose exact stats and cumulative costs update under the selected doctrine. In-match Tower Intel uses the same stacked branch controls and labels the active doctrine without covering targeting.
- Added doctrine-aware balance-agent planning that separates purchase timing from completed-build selection. A 240-run Hard matrix clears 143 runs (59.6%) versus the previous 144 (60.0%); all 20 doctrines and all 20 final roles appear in winning runs.
- Rebuilt the isolated balance benchmark to exercise Level 1, both doctrines, and all four doctrine/final-role combinations for every tower (70 configurations total), with a regression guarding that coverage.
- Corrected Watchtower's renewed coverage dominance without touching global enemy stats: Heavy Optics now trades about 5% reach for its impact/utility gains, while Deadeye Post moves from 345 to 335 range and 118 to 112 damage. The complete Hard matrix remains stable at 140/240 clears, Long Range moves from 20/20 to 18/20, and every doctrine/final role still appears in winning runs.
- Added completed-build-path telemetry plus a validated `--force-build=tower:doctrine>specialization` simulator control. Natural Hard runs exercised 39/40 paths; the omitted Quick Loader → Quake Shell mortar path cleared 19/20 forced Hard LongRange campaigns, proving it was a planner preference rather than a balance defect.
- Rechecked the complete native title, Tower Library, battlefield, Tower Intel, and pause flow at high resolution; moved selected-tower strength/interaction/upgrade lines upward so the `NEXT` preview no longer crowds the Intel card border.
- Expanded Tower Library into a Tactical Library available from title and pause. The Threats tab documents all five base archetypes, exact defenses/economy, recommended counters, Elite/Boss multipliers, the 50% boss phase, and the visual language for Slow, Expose, Armor Break, Burn, and Stun.
- Added a Campaigns tab to the Tactical Library. Each arena exposes its complete authored 20-wave manifest in two compact columns, including exact roster, contact count, base health/speed scaling, threat tags, boss timing, and aggregate campaign pressure; changing the inspected arena does not alter the title-screen selection.
- Completed the battlefield status language: Armor Break now draws paired gold chevrons and Stun uses pulsing green squares, matching the Tactical Library legend without obscuring enemy silhouettes or adding more full rings in dense waves.
- Added Easy, Normal, Hard, and Bastion profiles with persistent save/co-op identity and deterministic checksums. Hard preserves the previous authored economy and enemy values; Normal is the default onboarding experience.
- Replaced the title screen's qualitative difficulty caption with exact enemy-health, speed, starting-credit, and life modifiers so a profile can be compared before committing to a run.
- Gave Foundry Loop, Prism Circuit, and Surge Divide separate 20-wave campaigns. Surge now carries the hardest roster and opening economy instead of granting nine nodes against the same opposition as Foundry.
- Added Prism Circuit as a third arena with a distinct continuous conduit visual, a new route/build topology, and three restrained Surge Nodes.
- Added data-driven battlefield motifs for all three arenas: Foundry braces, Prism facets, and Surge circuit traces. Native visual QA confirmed that the marks distinguish empty field space without competing with routes, nodes, ranges, or targeting.
- Expanded all ten towers to two tier-3 roles (20 total), including differentiated swarm/armor, tempo/control, reach/output, and support choices. Fixed Shard Fan's armor-pierce path so the authored value is applied in combat.
- Replaced the generic flat Overdrive with ten named tower-specific Protocols, including burst damage/status, range, pierce, aura, and tempo effects. Added one optional auto-armed tower with deterministic enemy-aware activation, save/co-op/checksum support, and clear in-world/UI state.
- Added a persistent Settings screen to both title and pause menus: windowed/fullscreen, four output presets, VSync, SFX volume, and full/reduced effects. The 2560x1440 scene target and centralized palette remain independent of output scaling.
- Added compact procedural sound cues for placement, upgrades, sales, Protocols, kills, leaks, waves, Pulse Plates, and the Charge Forge. Audio initialization fails safely to silent play on systems without a usable device.
- Completed the procedural audio event language with distinct boss-phase, victory, and defeat cues, plus Plate deployment and Forge sale feedback. Attack-by-attack sounds remain intentionally omitted to preserve clarity during dense late-game fire.
- Added a deterministic two-player jitter regression that delivers authoritative shared-control commands 0-5 ticks late across placement, upgrades, targeting, Protocols, speed, and sales. Both peers must retain an identical checksum, while commands that miss the six-tick authority window are rejected for repair.
- Added low-rate remote P1/P2 battlefield cursors for co-op coordination. Presence traffic is bounded to eight updates per second, excluded from deterministic simulation/checksums, rejects invalid/sidebar coordinates, and fades after 0.75 seconds without an update; middle-click pings remain the persistent callout.
- Extended co-op presence with remote tower selection context. Four compact player-colored corner marks identify the other player's selected tower without changing shared control, ownership attribution, native tower colors, or deterministic state.
- Bounded deep-endless co-op bookkeeping: request receipts and applied-sequence replay protection now compact into rolling histories, pending/far-future commands are capped and validated during snapshots, and a stalled outbound queue pauses for reconnection instead of growing indefinitely.
- Added structural validation for authoritative reconnect snapshots before they mutate a live client: missing collections, duplicate identities, nonfinite combat values, oversized state, malformed telemetry, and invalid pending commands now fail as bounded data errors instead of surfacing as null/dictionary crashes or repeated divergent repairs.
- Compacted sold-tower telemetry after its last projectile/status expires. Aggregate lifetime contribution remains exact and lingering utility still survives co-op snapshots, while obsolete tower objects and instance-ID maps no longer grow under endless sell/rebuild churn; a 300-cycle regression guards it.
- Made extreme endless bookkeeping numerically safe: credits, income, spending, kills, leaks, sale recovery, and wave rewards saturate instead of wrapping negative; generated enemy scaling remains finite/density-capped even at the largest representable wave, which now terminates cleanly rather than overflowing its index.
- Extended deep-run saturation to contribution analytics: aggregate and individual tower hits, kills, purchases, upgrades, sales, Protocols, support assists, status uptime, enemy leaks, Plate activity, and Forge output remain finite and monotonic instead of wrapping or becoming infinity.
- Hardened periodic co-op checksums around transitional combat state: enemy death/escape cleanup, pending boss-phase feedback, active-wave identity, and projectile color/radius now trigger authoritative repair if peers differ.
- Hardened pre-match compatibility around authored content: the build fingerprint now refuses missing/empty content roots and has recursive JSON sensitivity coverage, while the loader rejects duplicate map/wave identities, campaigns bound to the wrong arena, unknown schemas, malformed build regions/routes, and invalid wave scaling instead of accepting ambiguous or partially defaulted data.
- Revalidated current endless progression over 144 Hard runs: 83 reached the campaign end, survivors averaged wave 26.5, Control averaged 34.2 and peaked at 39, and none reached 40. Campaign intel now explains final-roster inheritance and bounded scaling, while a regression proves every arena generates a distinct wave 21.
- Added a `--no-protocols` matched-control mode and per-tower activation reporting. Against the same 240 Hard seeds, disabling Protocols reduced clears from 140 to 110 while LongRange and UpgradeFocused retained their full 18/20 and 17/20 results, confirming that active execution matters without becoming universally mandatory.
- Refined large attack flashes with crisp geometric impact spokes and added true-radius impact rings to Frost, Ember, Breaker, and Mortar splash hits. Reduced-effects mode retains only the essential outer ring.
- Bounded dense-wave visuals to 384 transient effects plus eight protected co-op pings. At capacity, major tactical flashes displace beam noise so late-endless fire cannot grow an unbounded render/allocation queue.
- Added conventional keyboard paths across planning screens: Enter starts a new game, confirms a usable save, resumes pause, and advances victory/defeat results; Tab switches co-op text fields and cycles every Tactical Library page. Native 1920x1080 QA confirmed the expanded hints fit without crowding.
- Bounded Mortar's extreme-crowd scaling with deterministic, UI-visible shell caps: 6/7 through the base levels, 7 for rapid Salvo shells, and 10 for Quake's wider control impact. Radius and low fire rate still define its area role.
- Made balance-agent Beacon branch scoring placement-aware. Horizon now wins when its larger field reaches additional towers, while Tempo remains preferable for compact clusters; branch telemetry is printed in every full simulation report.
- Added compact-versus-spread Signal Beacon economy benchmarks. Tempo leads compact three-tower throughput while Horizon's added recipients win the spread formation, confirming distinct support roles.
- Gave every tower protocol its own restrained geometric signature and audio pitch while active. Reduced-effects mode keeps only the essential native-color protocol ring.
- Re-ran 180 deterministic campaign agents per key difficulty after the map, branch, protocol, Mortar, and support changes. Normal cleared 137/180 (76.1%), Hard 106/180 (58.9%), and Bastion 35/180 (19.4%); on Hard, Foundry cleared 41/60, Prism 38/60, and Surge 27/60, confirming the intended arena ordering.
- Added source-aware utility telemetry: Signal Beacon damage-equivalent and recipient-seconds plus Slow, Stun, Exposed, and Armor Break enemy-seconds. End-run contribution bars now include Beacon-assisted output while keeping direct damage visibly separate.
- Current verification: 65/65 deterministic tests, clean Release build with zero warnings, native high-DPI QA of title/settings/gameplay/pause layouts, real loopback transport coverage, and a 500-tick mid-combat reconnect soak.

## Range, branch, and menu harmony pass

- Reduced Watchtower direct damage while retaining its defining 250-290 range; seeded LongRange results moved from 10/10 perfect dominance to 8/10 viable finishes.
- Extended Foundry's remote lower-left build region toward the road and gave Shard Fan modest range plus 1/1/2 armor pierce, creating a clearer payoff for occupying close slots.
- Rebuilt Hail Lancer as the direct area-damage Frost branch while Permafrost remains the maximum-control branch.
- Rebuilt Searing Brand as a long-range, armor-piercing boss-burn branch while Wildfire remains the crowded-route branch.
- Kept Mortar unchanged after current telemetry placed it in the middle of the roster rather than among the dominant towers.
- Restored Online Co-op to green and moved Continue Checkpoint to cyan, keeping every main-menu action distinct without the disliked blue co-op treatment.

Date: 2026-08-14

## Two-player reliability pass

- Added host-authoritative active-match snapshots covering wave spawn state, enemies and statuses, projectiles, towers, targeting/upgrades, tactical defenses, economy/lives, timers, statistics, ready state, and pending deterministic commands.
- Checksum divergence and late authoritative commands now pause and repair from the host instead of terminating the match.
- The host keeps the match and listener alive after Player 2 disconnects; Player 2 automatically retries and can also restart and rejoin with the same endpoint/code.
- Both players can manage every tower and Charge Forge. P1/P2 remains visible as placement attribution only.
- Added clear in-game peer connection and per-player wave-ready status plus a preserved-match reconnect overlay.
- Added build/content fingerprint rejection so incompatible peers fail clearly before play.
- Expanded loopback, active-wave snapshot, pending-command, reconnect-listener, and shared-control regression coverage.

## Features added

- Deterministic headless self-play with 12 strategies, seeded runs, all-map execution, JSON telemetry, and isolated combat benchmarks.
- Twenty intentionally mixed waves with readable archetypes, elite groups, and a phased Bastion Core final boss.
- Pre-purchase tower intelligence, range previews, explicit placement errors, exact upgrade deltas, hotkeys, expanded targeting, and post-run analysis.
- Pulse Plate emergency road defense and a three-level Charge Forge that produces stored plates during active waves.
- Mutually exclusive final branches for Needle, Frost, Ember, and Breaker.
- Overdrive active ability with shared co-op control, 5-second duration, +75% attack rate, 18-second shared cooldown, effects, UI timers, bot policy, telemetry, and deterministic network command.
- Surge Divide map with Overclock and Scope Surge Zones and hover-explained bonuses.
- Direct two-player online co-op using public IP/DNS plus a six-character join code.

## Gameplay and balance changes

- Preserved 400 Foundry starting credits and introduced a measured 360-credit Surge opening.
- Kept tower damage mechanically consistent across all waves; no artificial early-game damage multiplier was added.
- Added a legitimate 20-credit early-call reward and fixed the first wave incorrectly receiving it.
- Strengthened tower identities through targeting, armor/status interactions, support auras, branching behavior, and map placement bonuses.
- Removed Watchtower armor pierce so Watchtower and Breaker/Rail branches retain distinct jobs.
- Made Charge Forge production wave-powered only, eliminating wait-to-generate exploitation.
- Replaced Pulse Plate's hidden lockout with per-crossing memory. The trigger pushes an enemy behind the plate, allowing a durable leak to cross again and consume the second charge, while consecutive enemies can still trigger independently.
- Closed the late-game Pulse Plate carpet exploit without reverting its useful damage and slow: push 48 -> 28, elite push 60%, boss push 25%, 0.75-second per-enemy knockback grace, 16 active-plate cap, and active-wave-only direct purchases escalating by 15 credits from a 60-credit base.

## Simulation results

- Original single-map checkpoint: 12/60 wins (20.0%), average wave 15.0.
- Two-map pre-Overdrive checkpoint: 42/120 wins (35.0%).
- Overdrive/branch checkpoint: 49/120 wins (40.8%), average wave 16.0.
- Final checkpoint: 53/120 wins (44.2%), average wave 16.1, average lives 7.1.
- Final map results: Foundry 22/60; Surge Divide 31/60.
- Final strategy results: Conservative 5/10, Economy 0/10, Aggressive 4/10, UpgradeFocused 10/10, Spam 0/10, AntiSwarm 3/10, AntiArmor 10/10, LongRange 10/10, Control 2/10, Tactical 1/10, Adaptive 8/10, Randomized 0/10.
- Final tactical totals: 3,995 Overdrives, 552 plates, 1,060 triggers, 702 plate kills, 71,950 plate damage, and 20 forge purchases.
- Final report: `.build/balance/matrix-online-ui-surge-5x-20260814.json`.

## Visual changes

- Rejected the prior black-and-white conversion and restored a saturated navy/cyan/coral/gold/green/violet schematic palette.
- Fixed the main-menu logo covering the title and added deliberate title spacing.
- Rebuilt roads as one continuous rectangular surface with yellow dashes only; removed segment seams, heavy outlines, round corner caps, and square corner tiles.
- Replaced grid/debug build zones with low-noise tinted fields and exact corner brackets.
- Moved all tactical controls into the sidebar so no persistent UI blocks the battlefield.
- Renamed map power-field terminology from Relay to Surge Zone.
- Added Surge Zone hover intel with exact bonus, radius, placement rule, and stacking behavior.
- Standardized every tower icon with an outer ring and integrated radial level marks: top at level 1, then 120/240-degree spokes for levels 2/3. Removed the separate badges to avoid battlefield obstruction.
- Added a compact gold broadcast marker to towers affected by Signal Beacon and exact Beacon-only rate/range deltas to Tower Intel without hiding Surge Node context.
- Increased the workshop and tower-intel icon/text gutter so rings no longer crowd tower names or cost/role labels.
- Added clearer ownership, branch, elite/boss, powered/surged, Overdrive, recoil, impact, beam, and ring feedback.
- Added a fixed 2560x1440 supersampled scene, double-density interface font and primitive masks, and linear final downsampling for smoother fullscreen presentation.
- Clipped the complete scene before letterbox composition so thick roads and effects cannot render into the white side bars at wide fullscreen aspect ratios.
- Restored the lower-resolution reference's muted teal/slate/navy color relationships after the first high-resolution pass exposed a DesktopGL MSAA gamma lift. Backbuffer MSAA is now disabled while the 2x scene target retains sharp edges.
- Centralized the refined theme in `ColorPalette`: soft off-white panels, pale blue-gray cards, muted secondary text/borders, and controlled cyan, green, gold, coral, violet, and blue accents.

## Multiplayer status

- Removed the loopback-only restriction. Host now listens dual-stack on all network adapters at TCP `28741`.
- Online join accepts DNS, IPv4, IPv6, and optional explicit port.
- Preserved authoritative sequence assignment, future fixed-tick execution, duplicate protection, periodic checksums, and protocol validation; desync now performs host-state repair.
- Added map synchronization, shared economy/lives/inventory/speed and tower/forge control, two-player wave ready, colored pings, specialization, and Overdrive.
- Added complete active-match reconnect snapshots and build/content compatibility checks. Internet play remains direct-connect: the host must forward TCP `28741` or use a peer VPN; there is no hosted relay, matchmaking, automatic NAT traversal, or encryption.

## Quality-of-life changes

- Reworked Run Analysis into a readable two-column metric grid and surfaced previously hidden early-call income and run-wide Protocol activation totals.
- Replaced the frozen post-victory Final Field with **Continue Endless**. It resumes the same battlefield for wave-21 preparation and lets attack visuals cool off under normal simulation.
- Added deterministic endless scaling, rotating pressure themes, recurring five-wave bosses, performance-bounded roster growth, solo checkpoint persistence, co-op continuation commands, reconnect state, and endless HUD labeling.
- The Plates button now reports only stored inventory or direct-purchase cost. The Charge Forge button exclusively owns the production timer, paused state, and storage-full state, avoiding duplicate countdowns.
- Sidebar always displays Overdrive active time or cooldown.
- Forge timer explicitly changes from `PAUSED` to `RUNNING` when a wave starts.
- All placed towers expose level 1/2/3 through one shared, non-hover radial-mark language.
- Added map selector, co-op host map display, wave intel, ready status, selected structure controls, and immediate restart/menu behavior.

## Architecture changes

- Kept one deterministic `GameSession` and conventional object-oriented systems.
- Added definition/runtime separation for maps, Surge Zones, branches, tactics, elites, and boss state.
- Added narrow tower behavior modules and a shared target selector instead of a growing tower switch.
- Added `GameCommandProcessor`, authoritative host sequencer, deterministic session runner, and state checksum.
- Included map, branch, Overdrive, and Pulse Plate handled-enemy state in checksums.
- Kept networking as a transport edge around existing validated gameplay APIs.

## Tests added

- Expanded deterministic suite from the original baseline to 48 passing checks.
- Coverage includes four authored campaigns, difficulty persistence, node buffs/checksum, pathing, targeting, armor/DOT/status, elites/boss, economy, placement, endless continuation, early calls, mixed waves, tower behavior, contribution telemetry, shared co-op controls, mirrored commands, hidden-scale/stat checksum coverage, a 500-tick mid-combat reconnect soak, loopback transport, build mismatch rejection, wave ready, endpoint parsing, tower intel/roles/Protocols, Pulse Plate reliability, wave-only forge production, saves, high-resolution composition, palette constants, and headless determinism.
- Release build and self-contained publish complete with 0 warnings and 0 errors.

## Bugs resolved

- Crash after the final wave group's spawn index advanced past the group list.
- Burn damage scaling with frame rate/minimum hit floor.
- Shard Fan and Arc Relay target traversal defects.
- Armor-pierce/shield flags ignored by generic projectiles.
- First-wave early-call credit exploit.
- Pulse Plate second-charge unreliability.
- Forge downtime income exploit.
- UI over usable battlefield space.
- Ambiguous per-tower level marks.
- Main-menu logo overlap, clipped labels, obscuring unaffordable cards, off-map previews, and multiple road/build-zone visual artifacts.
- Co-op limited to two copies on one PC.
- Fullscreen road geometry bleeding into the left letterbox bar.
- High-resolution composition washing the muted tactical palette toward pale blue-gray.

## Experiments reverted or rejected

- Black-and-white presentation: removed because it reduced color without achieving coherent silhouette simplification.
- Circle and square road-corner patches: removed because they exposed path segmentation.
- Grid-like build fields: removed because placement is continuous.
- Early-wave-only tower damage lift: rejected for inconsistent mechanics.
- Disposable defenses as routine DPS: constrained because they displaced permanent defense planning.
- Downtime Charge Forge production: removed as a degenerate wait strategy.

## Remaining issues

- Direct internet host requires manual router forwarding or VPN connectivity.
- No hosted relay, matchmaking, automatic NAT traversal, or encryption.
- Automated strategy results are healthy but are not a substitute for human branch timing, protocol use, and late endless reorganization.
- Human remote-latency and late-wave playtesting remain necessary despite deterministic/loopback coverage.

## Highest-value next steps

1. Field-test direct online play and reconnect on two remote PCs and different consumer routers.
2. Evaluate an optional authorized hosted rendezvous/relay if port forwarding remains too burdensome.
3. Collect human results for all four difficulty profiles and adjust only statistically persistent outliers.
4. Add a fifth arena only when it introduces a genuinely different placement constraint or tactical system rather than another route variant.
