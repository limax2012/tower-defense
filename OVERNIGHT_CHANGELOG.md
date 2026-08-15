# Overnight Changelog

## Arena progression, roles, protocols, and presentation pass

Date: 2026-08-15

- Added Crosswind Basin, a fourth arena with a seamless cyan-banked channel, current-chevron field motif, three compact crossfire islands, no power nodes, a distinct starting economy, and a runner-led authored 20-wave campaign.
- Completed the missing five-seed Easy matrix across the original three arenas: 150/180 wins (83.3%), with only Economy and indiscriminate Spam consistently failing.
- Extended lifetime Tower Intel beyond direct damage/kills: individual Beacons retain assisted damage, while source towers retain control, expose, and armor-break enemy-seconds. The values survive save/load and co-op snapshots and participate in deterministic state checks.
- Added data-derived campaign intel to the arena selector. Every map now previews its opening threat mix, total contacts, peak density, final health multiplier, and boss timing before a run; native high-resolution QA confirmed the extra planning line remains readable.
- Regenerated canonical five-seed matrices across all four arenas and all four difficulties (960 runs): Easy 83.3%, Normal 77.1%, Hard 60.0%, and Bastion 19.6%. Every final specialization appears in winning Hard runs, and Surge remains the hardest arena.
- Added exact marginal-damage attribution for Expose and Armor Break. The resolver compares the real hit against no-expose/no-break counterfactuals without altering damage, avoids interaction double-counting, persists per-tower assist, includes it in co-op checksums, and shows it in Tower Intel/results. A 240-run Hard pass measured 2.30M Prism Expose assist and 3.51M Breaker Armor Break assist.
- Added Easy, Normal, Hard, and Bastion profiles with persistent save/co-op identity and deterministic checksums. Hard preserves the previous authored economy and enemy values; Normal is the default onboarding experience.
- Gave Foundry Loop, Prism Circuit, and Surge Divide separate 20-wave campaigns. Surge now carries the hardest roster and opening economy instead of granting nine nodes against the same opposition as Foundry.
- Added Prism Circuit as a third arena with a distinct continuous conduit visual, a new route/build topology, and three restrained Surge Nodes.
- Added data-driven battlefield motifs for all three arenas: Foundry braces, Prism facets, and Surge circuit traces. Native visual QA confirmed that the marks distinguish empty field space without competing with routes, nodes, ranges, or targeting.
- Expanded all ten towers to two tier-3 roles (20 total), including differentiated swarm/armor, tempo/control, reach/output, and support choices. Fixed Shard Fan's armor-pierce path so the authored value is applied in combat.
- Replaced the generic flat Overdrive with ten named tower-specific Protocols, including burst damage/status, range, pierce, aura, and tempo effects. Added one optional auto-armed tower with deterministic enemy-aware activation, save/co-op/checksum support, and clear in-world/UI state.
- Added a persistent Settings screen to both title and pause menus: windowed/fullscreen, four output presets, VSync, SFX volume, and full/reduced effects. The 2560x1440 scene target and centralized palette remain independent of output scaling.
- Added compact procedural sound cues for placement, upgrades, sales, Protocols, kills, leaks, waves, Pulse Plates, and the Charge Forge. Audio initialization fails safely to silent play on systems without a usable device.
- Refined large attack flashes with crisp geometric impact spokes and added true-radius impact rings to Frost, Ember, Breaker, and Mortar splash hits. Reduced-effects mode retains only the essential outer ring.
- Bounded Mortar's extreme-crowd scaling with deterministic, UI-visible shell caps: 6/7 through the base levels, 7 for rapid Salvo shells, and 10 for Quake's wider control impact. Radius and low fire rate still define its area role.
- Made balance-agent Beacon branch scoring placement-aware. Horizon now wins when its larger field reaches additional towers, while Tempo remains preferable for compact clusters; branch telemetry is printed in every full simulation report.
- Added compact-versus-spread Signal Beacon economy benchmarks. Tempo leads compact three-tower throughput while Horizon's added recipients win the spread formation, confirming distinct support roles.
- Gave every tower protocol its own restrained geometric signature and audio pitch while active. Reduced-effects mode keeps only the essential native-color protocol ring.
- Re-ran 180 deterministic campaign agents per key difficulty after the map, branch, protocol, Mortar, and support changes. Normal cleared 137/180 (76.1%), Hard 106/180 (58.9%), and Bastion 35/180 (19.4%); on Hard, Foundry cleared 41/60, Prism 38/60, and Surge 27/60, confirming the intended arena ordering.
- Added source-aware utility telemetry: Signal Beacon damage-equivalent and recipient-seconds plus Slow, Stun, Exposed, and Armor Break enemy-seconds. End-run contribution bars now include Beacon-assisted output while keeping direct damage visibly separate.
- Current verification: 46/46 deterministic tests, clean Release build with zero warnings, and native visual QA of title, settings, gameplay, and pause layouts.

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

- Expanded deterministic suite from the original baseline to 40 passing checks.
- Coverage includes content counts, both map openings, Surge Zone buffs/checksum, pathing, targeting, armor/DOT/status, elites/boss, economy, placement, final group, early calls, mixed waves, Arc chain, telemetry, shared co-op controls, mirrored network commands/checksum, active-wave snapshots, reconnect transport, build mismatch rejection, wave ready, endpoint parsing, tower intel/branches/Overdrive, Pulse Plate reliability, wave-only forge production, high-resolution viewport composition, tactical palette constants, and headless determinism.
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
- No persistent run-history browser beyond individual save metadata and end-of-run analysis.
- Automated strategy results are healthy but are not a substitute for human branch timing, protocol use, and late endless reorganization.
- Human remote-latency and late-wave playtesting remain necessary despite deterministic/loopback coverage.

## Highest-value next steps

1. Field-test direct online play and reconnect on two remote PCs and different consumer routers.
2. Evaluate an optional authorized hosted rendezvous/relay if port forwarding remains too burdensome.
3. Collect human results for all four difficulty profiles and adjust only statistically persistent outliers.
4. Consider a fourth arena only if it introduces a genuinely different placement constraint rather than another route variant.
