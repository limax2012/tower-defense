# Overnight Progress

Updated: 2026-08-15

## Verified checkpoint

- Release build: 0 warnings, 0 errors with the workspace-local .NET 10 SDK.
- Deterministic regression suite: 65/65 passing.
- Self-contained Windows x64 publish: `.build/publish/MinimalBastion.exe`.
- Content: 4 maps with independently authored campaigns, 10 towers with 20 tier-two doctrines and 20 final specializations, 10 distinct Protocols, 5 enemy bases plus elite/boss ranks, difficulty/directive profiles, tactical reserves, and endless continuation.
- Canonical five-seed Hard matrix: 140/240 wins (58.3%); every doctrine and final role appears in winning runs, with Surge Divide remaining the hardest arena. A fresh three-seed audit on the current executable produced 79/144 wins (54.9%) and the same map/strategy ordering.
- Native visual QA covers title/settings/co-op flows, all map treatments, tactical sidebar states, Surge Node overlap intel, Protocol/Beacon markers, Pulse Plates/Forge, high-resolution and wide-aspect rendering, result/field inspection, and the complete Tower/Threat/Campaign library. The latest 1920-wide pass reconfirmed title and doctrine-card spacing plus Breaker/Beacon contrast.
- Git history is maintained on `agent/overnight-arena-progression` and mirrored to `origin`; tested feature units are committed incrementally while `main` remains untouched during the active overnight run.

## Completed work

### Deterministic self-play and balance lab

- Added rendering-independent fixed-step full-game simulation with seeded choices, safety limits, and JSON reports.
- Added 12 agents: Conservative, Economy, Aggressive, UpgradeFocused, Spam, AntiSwarm, AntiArmor, LongRange, Control, Tactical, Adaptive, and Randomized.
- Agents use continuous placement, Surge Zones, route coverage, reserves, upgrades, selling, branches, targeting, early calls, plates, forge production, elites/boss reads, and Overdrive.
- Telemetry covers economy, purchases/upgrades/sales, branches, attributed damage/kills, armor/shield/overkill, enemies, waves, plates, forge production, Overdrives, and early-call rewards.
- CLI supports `--simulate`, `--simulate-full`, `--strategy`, `--seed`, `--runs`, `--map`, and `--output`.

### Waves, enemies, and strategic information

- Reauthored all 20 waves as named mixed-tier patterns while preserving 1,090 total enemies.
- Added screens, rushes, escorts, feints, endurance streams, armor pressure, elites, and a final phased Bastion Core boss.
- Added compact current/next-wave threat intelligence for swarm, speed, armor, shields, regeneration, elites, boss, and approximate count.
- Fixed the final wave-group indexing crash that originally occurred around the eighth spawn.

### Towers, branches, and active play

- Added pre-purchase, hover, placement, selection, and exact-upgrade tower intelligence.
- Added targeting modes First, Last, Strongest, Weakest, Nearest, Fastest, and Armored.
- Added mutually exclusive level-3 branches for Needle, Frost, Ember, and Breaker.
- Added global Overdrive: +75% attack rate for 5 seconds on an owned combat tower, with an 18-second shared cooldown, synchronized commands, bot usage, telemetry, and explicit UI timers.
- Removed Watchtower's latent level-3 armor pierce after fixing generic projectile behavior; long range and anti-armor now retain distinct jobs.
- Replaced the separate level badges with integrated radial level marks: one top spoke at level 1, then fixed 120/240-degree spokes for levels 2/3. Every tower also uses the same outer ring.

### Tactical defenses and economy integrity

- Added Pulse Plates: road-snapped, two charges, 32 area damage, radius 52, brief stun, and 2 armor pierce.
- Added the three-level Charge Forge with capped inventory, production cadence, damage upgrades, ownership, UI, telemetry, and bot policy.
- Forge production now advances only during active waves. Waiting before/between waves cannot generate plates.
- Removed the Pulse Plate's hidden 0.8-second global trigger lockout. Plates remember handled enemy IDs, so the same enemy cannot spend both charges and a consecutive unhandled enemy triggers reliably.
- The Plates button reports inventory/placement or direct-purchase cost. The Charge Forge button alone reports `+1 IN`, `PAUSED`, or `STORAGE FULL`, so production state is explicit without duplicate countdowns.
- Fixed the first-wave early-call bug. Only skipping an active ten-second intermission grants 20 credits.

### Maps and presentation

- Restored and extended the colorful geometric/schematic direction after rejecting the black-and-white pass.
- Preserved seamless continuous roads with yellow dashes only: no tiles, seams, corner circles, square joints, or heavy outlines.
- Replaced grid/debug-looking build regions with quiet tinted fields and exact corner brackets.
- Added Surge Divide (internal ID `relay_divide`) with Overclock and Scope Surge Zones that change placement decisions.
- Renamed player-facing relay terminology to Surge Zone and added hover intel with exact radius/bonus/stacking rules.
- Moved Pulse Plate, Charge Forge, and Overdrive controls from the battlefield into the sidebar, preserving the full 960-pixel play area.
- Added auto-fitting button labels, colorful tower/enemy silhouettes, rank treatment, recoil/pulse/ring/impact feedback, polished menus, pause, and post-run analysis.
- Fixed the original main-menu logo/title overlap and added intentional whitespace.
- Preserved the 1280x720 logical layout while moving scene rendering to a fixed 2560x1440 target with double-density fonts and primitive masks, followed by linear downsampling.
- Composited the scene only inside the calculated 16:9 destination rectangle, eliminating road/effect bleed into fullscreen letterbox bars.
- Restored the original dark-teal battlefield, slate road, navy HUD, and restrained accent hierarchy while retaining every high-resolution rendering change.
- Centralized UI theme constants and disabled redundant backbuffer MSAA, which was gamma-lifting the final DesktopGL composite; the 2x scene target remains the source of edge supersampling.

### Direct online co-op

- Replaced loopback-only same-PC co-op with direct internet host/join.
- Host listens dual-stack on all adapters at TCP `28741`; friend enters public IP/DNS with optional port plus a six-character join code.
- Host-authoritative sequenced commands, future fixed ticks, periodic checksums, duplicate rejection, and protocol version validation remain intact.
- Shared credits/lives/waves/speed/inventory; visible owner-only tower/forge mutation; both-player wave ready; map synchronization; colored middle-click pings; clear disconnect failures.
- Specialization and Overdrive commands are ownership-validated and deterministic.
- Added endpoint parser coverage for DNS, IPv4, and IPv6.

## Measurements

- Original 60-run single-map matrix: 12 wins (20.0%), average wave 15.0.
- Two-map pre-Overdrive matrix: 42/120 wins (35.0%), average wave 14.9.
- Overdrive/branch matrix: 49/120 wins (40.8%), average wave 16.0.
- Final reliable-plate/wave-forge matrix: 53/120 wins (44.2%), average wave 16.1.
- Final maps: Foundry 22/60, Surge Divide 31/60.
- Final strategy wins: Conservative 5, Economy 0, Aggressive 4, UpgradeFocused 10, Spam 0, AntiSwarm 3, AntiArmor 10, LongRange 10, Control 2, Tactical 1, Adaptive 8, Randomized 0.
- Final active/tactical use: 3,995 Overdrives; 552 plates; 1,060 triggers; 702 plate kills; 71,950 plate damage; 20 forge purchases.
- Reports:
  - `.build/balance/matrix-two-maps-final-5x-20260814.json`
  - `.build/balance/matrix-overdrive-5x-20260814.json`
  - `.build/balance/matrix-online-ui-surge-5x-20260814.json`
  - `.build/balance/tactical-wave-powered-forge-5x.json`
  - `.build/balance/economy-wave-powered-forge-5x.json`

## Significant decisions and rejected designs

- Preserved the conventional object-oriented deterministic architecture; no ECS/DI/physics rewrite.
- Rejected the monochrome visual pass because it removed color without simplifying complex silhouettes; restored saturated functional color and refined geometry instead.
- Rejected circle and square road-corner patches; route rendering is one continuous rectangular surface.
- Rejected a proposed early-game-only damage lift. Damage remains consistent for mechanics clarity; early balance uses starting resources, composition, and positioning.
- Rejected Pulse Plates as ordinary DPS. Bot direct purchases are capped and reactive; permanent towers dominate.
- Rejected downtime forge production as an exploitable no-risk economy.
- Kept stable data IDs such as `relay_divide` while changing only player-facing names, avoiding save/checksum/content churn.
- Chose direct IP online co-op as the smallest no-service internet MVP; hosted relay/matchmaking remains separate future infrastructure.

## Bugs resolved

- Final wave-group spawn crash.
- First wave incorrectly granting early-call credits.
- Generic projectile behavior ignoring armor pierce/shield flags.
- Shard Fan pellets repeatedly selecting one target.
- Arc chains not traversing hop-by-hop.
- Burn frame-rate/minimum-damage multiplication.
- Placement preview drawing over sidebar/off-map.
- Unaffordable cards hiding information and long labels clipping.
- Main-menu logo obscuring title/subtitle.
- Road seams, tile joints, circular/square corner artifacts, and misplaced build-region visuals.
- Hidden Pulse Plate lockout causing consecutive enemies to slide across unused charges.
- Forge generating resources during no-risk downtime.
- Tactical buttons blocking usable map space.
- Ambiguous tower levels caused by variable identity marks.
- Loopback-only co-op transport preventing remote internet connections.
- Road geometry leaking into the left white letterbox bar at wide fullscreen aspect ratios.
- Muted authored colors becoming washed out after high-resolution composition.

## Current work

- Feature pass is safely checkpointed, tested, simulated, published, visually inspected, and documented.

## Highest-value next priorities

1. Add optional hosted relay/NAT traversal or automatic port mapping so online friends do not need manual TCP forwarding.
2. Add reconnect snapshots and content/build fingerprint negotiation for longer online sessions.
3. Human-playtest both maps and internet latency with two remote PCs; preserve reports for any balance change.
4. Add audio hooks/settings, volume controls, and persistent settings.
5. Add a third map designed to challenge the currently dominant LongRange policy.
6. Add difficulty/challenge modes without disturbing the authored normal-campaign baseline.

## Blockers

- Hosted relay/matchmaking would require an external service and deployment; none is configured or authorized.
- No other blocker is active.
