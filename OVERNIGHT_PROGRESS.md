# Overnight Progress

Updated: 2026-08-15

## Verified checkpoint

- Release build: 0 warnings, 0 errors with the workspace-local .NET 10 SDK.
- Deterministic regression suite: 72/72 passing.
- Self-contained Windows x64 publish: `.build/publish/MinimalBastion.exe`.
- Content: 4 maps with independently authored campaigns, 10 towers with 20 tier-two doctrines and 20 final specializations, 10 distinct Protocols, 5 enemy bases plus elite/boss ranks, difficulty/directive profiles, tactical reserves, and endless continuation.
- Canonical five-seed Hard matrix: 140/240 wins (58.3%). A fresh three-seed audit on the current executable produced 83/144 wins (57.6%) after the bounded Rapid Array, Breach Round, and Razor Bloom role repairs; the game remains in its demanding target band and Surge Divide retains the lowest average surviving lives.
- Native visual QA covers title/settings/co-op flows, all map treatments, tactical sidebar states, Surge Node overlap intel, Protocol/Beacon markers, Pulse Plates/Forge, high-resolution and wide-aspect rendering, result/field inspection, and the complete Tower/Threat/Campaign library. The latest isolated 1920-wide publish pass reconfirmed title layout, the opening battlefield, and a complete pause/resume round trip.
- Git history is maintained on `agent/overnight-arena-progression` and mirrored to `origin`; tested feature units are committed incrementally while `main` remains untouched during the active overnight run.
- Pulse Plate snapping now rejects sidebar/out-of-canvas coordinates before route projection, preventing a tactical-control click near the exit route from becoming an accidental deployment; the authored endpoint regression is included in the suite.
- Endless simulations now distinguish authored campaign clears from reaching a requested endless target and report depth by arena. Current wave-35 audits show finite failure pressure across Easy, Normal, and Hard, while the strongest easier-profile control plans retain meaningful post-campaign runway.
- The reconstruction matrix now verifies all 64 arena/difficulty/directive combinations through local checkpoint restore plus both intermission and live-opening authoritative Player-2 snapshots with exact checksum parity.
- All 40 completed tower paths now have a Player-2 reconstruction regression covering shared management, ownership attribution, targeting, manual/automatic Protocol state, and checksum parity.
- All ten authored Protocols now have live mechanical assertions plus active-effect co-op reconstruction coverage; tactical reconnects likewise preserve a partially spent Plate and a player-two level-3 Forge through subsequent deterministic ticks.
- Fresh matched Hard audits remain stable at 83/144 wins (57.6%). Forced Prism, Arc, and Mortar sweeps keep all twelve tested cross-tree paths viable, so no speculative stat change was applied during cleanup.

## Completed work

### Deterministic self-play and balance lab

- Added rendering-independent fixed-step full-game simulation with seeded choices, safety limits, and JSON reports.
- Added 12 agents: Conservative, Economy, Aggressive, UpgradeFocused, Spam, AntiSwarm, AntiArmor, LongRange, Control, Tactical, Adaptive, and Randomized.
- Agents use continuous placement, Surge Nodes, route coverage, reserves, upgrades, selling, branches, targeting, early calls, plates, forge production, elites/boss reads, and Protocols.
- Telemetry covers economy, purchases/upgrades/sales, branches, attributed damage/kills, armor/shield/overkill, enemies, waves, plates, forge production, Protocol activations, and early-call rewards.
- CLI supports `--simulate`, `--simulate-full`, `--strategy`, `--seed`, `--runs`, `--map`, `--difficulty`, `--challenge`, `--max-wave`, `--force-build`, `--no-protocols`, and `--output`.
- Forced-build reports now distinguish the requested path from paths actually completed in each run, including completion coverage, wins among completed runs, completed tower count, and completed-run impact per credit. Expensive or late paths can no longer look weak merely because a failed run never afforded them.
- Forced sweeps also print a path-by-arena matrix with win rate, completion rate, and average wave. A fresh 1,920-run Hard corpus demonstrates why both views matter: Arc finals were actually completed in only 7–8 of 48 requested-path runs, while Breach Round completed in 20–22 and still trailed Shatter Shell after conditioning on completion.
- A matched Hard control measured Protocol value at 73/144 wins enabled versus 52/144 disabled. Every tower's Protocol activated; Tactical gained the most, confirming the active layer rewards engagement without becoming mandatory.

### Waves, enemies, and strategic information

- Reauthored all 20 waves as named mixed-tier patterns while preserving 1,090 total enemies.
- Added screens, rushes, escorts, feints, endurance streams, armor pressure, elites, and a final phased Bastion Core boss.
- Added compact current/next-wave threat intelligence for swarm, speed, armor, shields, regeneration, elites, boss, and approximate count.
- Fixed the final wave-group indexing crash that originally occurred around the eighth spawn.

### Towers, branches, and active play

- Added pre-purchase, hover, placement, selection, and exact-upgrade tower intelligence.
- Added a fourth Tactical Library Systems page so progression marks, all seven targeting modes, status stacking, the shared Protocol cooldown, Beacon/Surge combination rules, and current Plate/Forge economics are discoverable before or during a run.
- Added targeting modes First, Last, Strongest, Weakest, Nearest, Fastest, and Armored.
- Added two mutually exclusive Tier-2 doctrines and two final specializations to all ten towers. Planning UI shows exact current, next, and completed-path stats before spending.
- Replaced the generic Overdrive payload with ten thematic Protocols: each tower has its own duration, cooldown, stat package, burst/status effect, animation/audio feedback, synchronized command, telemetry, and optional pressure-aware automatic activation.
- Removed Watchtower's latent level-3 armor pierce after fixing generic projectile behavior; long range and anti-armor now retain distinct jobs.
- Rebalanced Breaker Cannon's final roles with matched Hard simulations: Breach Round deals 1.5x damage to armored, elite, and boss targets, while Shatter Shell applies its area hit and armor break to at most four targets. Both doctrine pairings now have clear use cases instead of Shatter retaining unbounded crowd scaling.
- Gave Breach Round a tracking 20-unit impact capped at two targets after completion-conditioned audits showed that its boss microbenchmark strength did not translate into viable mixed campaigns. Matched seed results improved its two doctrine paths from 15–16/48 to 21/48 wins while Shatter stayed at 22–25/48; Shatter still has more than twice the radius and twice the target cap.
- Raised Razor Bloom's fixed per-shard damage from 11 to 13 after an exact 384-run A/B showed that the seven-way fan broadly wounded rushes without finishing them. Its two forced paths improved from 49/96 and 54/96 to 53/96 and 59/96, nearly matching Lance Fan's unchanged 59/96 and 60/96 while retaining much lower armor performance and shorter reach.
- Replaced the separate level badges with integrated radial level marks: one top spoke at level 1, then fixed 120/240-degree spokes for levels 2/3. Every tower also uses the same outer ring.

### Tactical defenses and economy integrity

- Added Pulse Plates: valid-road snapping, two charges, 38 area damage, radius 52, brief stun/slow, controlled knockback, and 2 armor pierce.
- Added the three-level Charge Forge with capped inventory, production cadence, damage upgrades, ownership, UI, telemetry, and bot policy.
- Forge production now advances only during active waves. Waiting before/between waves cannot generate plates.
- Removed the Pulse Plate's hidden 0.8-second global trigger lockout. Plates remember handled enemy IDs, so the same enemy cannot spend both charges and a consecutive unhandled enemy triggers reliably.
- The Plates button reports inventory/placement or direct-purchase cost. The Charge Forge button alone reports `+1 IN`, `PAUSED`, or `STORAGE FULL`, so production state is explicit without duplicate countdowns.
- Fixed the first-wave early-call bug. Only skipping an active ten-second intermission grants 20 credits.

### Maps and presentation

- Restored and extended the colorful geometric/schematic direction after rejecting the black-and-white pass.
- Preserved seamless continuous roads with yellow dashes only: no tiles, seams, corner circles, square joints, or heavy outlines.
- Replaced grid/debug-looking build regions with quiet tinted fields and exact corner brackets.
- Trimmed every build-zone boundary to the real tower-center road/edge clearance and added geometric content validation, so cyan fields never advertise an invalid strip beside a route or screen edge.
- Added Surge Divide (internal ID `relay_divide`) with nine compact, non-stacking Surge Nodes for rate, range, damage, or armor-pierce placement decisions. Its independently authored wave roster is deliberately harder to compensate for that positional upside.
- Gave Surge Divide a unique powered-rail route treatment: a continuous slate tube, slim cyan energy core, and low-clutter moving gold packets. Foundry road, Crosswind channel, Prism conduit, and Surge rail now have four distinct route identities without changing collision geometry.
- Preserved one continuous dash phase through every route bend, removing the last per-segment rhythm reset and letting Surge's animated packets travel cleanly around corners.
- Added Crosswind Basin and Prism Circuit, each with distinct route geometry, visual treatment, starting economy, and independently authored 20-wave campaign.
- Calibrated directive economy across 144-run Hard audits: Close Quarters remains matched at 48.6%, No Reserves now uses a 5% opening cushion and lands at 50.0%, while Core Six is explicitly presented as an advanced roster puzzle with a 30% fixed opening cushion.
- Renamed player-facing relay terminology to Surge Node and added placement/hover/selected-tower intel with exact radius, active bonus, and resulting stat deltas.
- Moved Pulse Plate, Charge Forge, and Overdrive controls from the battlefield into the sidebar, preserving the full 960-pixel play area.
- Added auto-fitting button labels, colorful tower/enemy silhouettes, rank treatment, recoil/pulse/ring/impact feedback, polished menus, pause, and post-run analysis.
- Added short geometric projectile motion streaks at full effect density, improving attack-direction readability without particle clutter; Reduced Effects preserves shape-only shots.
- Gave true area impacts their own expanding double-ring and six-spoke burst language instead of reusing generic tactical flashes. Reduced Effects keeps only the clean outer ring.
- Extended that truthful area language to Pulse Plates and radius-based Protocols, while temporal/self buffs keep the compact activation flash.
- Replaced generic kill flashes with brief six-segment geometric shatters; they are budgeted as low-priority feedback so dense waves retain tactical area cues.
- Tightened the Plate control to action-first wording while retaining the permanent field cap, and changed its gold surface to dark text for clean high-resolution contrast.
- Added effective HP/speed scaling to the active/next-threat HUD, including the selected difficulty multiplier, so endless escalation is observable rather than hidden.
- Made procedural music intensity crossfade with live battlefield pressure and boss presence while remaining quiet during downtime and independent of simulation state.
- Surfaced the best matching map+difficulty+directive record in the title arena summary, including endless depth, without making corrupt/unavailable history block startup.
- Added an explicit restart-confirmation guarantee that a fresh run keeps existing checkpoints, matching the already-safe new-slot behavior in solo and hosted co-op.
- Added a seamless procedural tactical music bed with mild arena-specific tuning and an independent persisted volume control; it remains optional presentation state and requires no external audio assets.
- Added compact synthesized confirm, back, and delete cues to the complete menu/pause/save/library flow. They share the persisted SFX control and never enter gameplay state, snapshots, or checksums.
- Presentation-only settings now apply without resetting the graphics device, avoiding fullscreen flicker when changing effect density or audio volume.
- Returning to the title now detaches the audio layer from the abandoned match, restoring neutral menu ambience and allowing the old session graph to be collected.
- Fixed the original main-menu logo/title overlap and added intentional whitespace.
- Preserved the 1280x720 logical layout while moving scene rendering to a fixed 2560x1440 target with double-density fonts and primitive masks, followed by linear downsampling.
- Composited the scene only inside the calculated 16:9 destination rectangle, eliminating road/effect bleed into fullscreen letterbox bars.
- Restored the original dark-teal battlefield, slate road, navy HUD, and restrained accent hierarchy while retaining every high-resolution rendering change.
- Centralized UI theme constants and disabled redundant backbuffer MSAA, which was gamma-lifting the final DesktopGL composite; the 2x scene target remains the source of edge supersampling.

### Direct online co-op

- Replaced loopback-only same-PC co-op with direct internet host/join.
- Host listens dual-stack on all adapters at TCP `28741`; friend enters public IP/DNS with optional port plus a six-character join code.
- Host-authoritative sequenced commands, future fixed ticks, periodic checksums, duplicate rejection, strict message-direction validation, and build/content fingerprint negotiation remain intact.
- Shared credits/lives/waves/speed/pause/inventory and shared tower/Forge control; owner tint remains attribution only. Both-player wave ready, map/difficulty/directive synchronization, remote cursors/selections, colored middle-click pings, and clear disconnect states are implemented.
- Added pre-timeout co-op link observability: the sidebar distinguishes fresh traffic, delayed traffic, a stalled link, and active resynchronization without misrepresenting one-way traffic age as round-trip latency.
- A returning Player 2 receives a validated authoritative active-combat snapshot with enemies, projectiles, effects-driving state, towers/branches/Protocols, economy, wave/readiness timers, tactical systems, telemetry, and pending commands before both peers resume.
- Remote Protocol activation is covered both by mirrored fixed-tick commands and an active reconnect snapshot: effect duration, cooldown, automatic assignment, stat package, and the renderer-driving animation state restore identically.
- Large reconnect snapshots now use bounded Brotli framing, raising dense-endless headroom without relaxing the 2 MiB wire cap or permitting more than 8 MiB of decoded state.
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
  - `.build/balance/overnight-audit-easy-3x.json`
  - `.build/balance/overnight-audit-normal-3x.json`
  - `.build/balance/overnight-audit-hard-3x.json`
  - `.build/balance/overnight-audit-bastion-3x.json`
  - `.build/balance/all-difficulties-current-3x.json`
  - `.build/balance/overnight-endless60-easy-1x.json`
  - `.build/balance/overnight-endless60-normal-1x.json`
  - `.build/balance/overnight-endless60-hard-1x.json`
  - `.build/balance/breaker-heavy-bored.json`
  - `.build/balance/breaker-heavy-repeat.json`
  - `.build/balance/overnight-audit-hard-breaker-cap4-3x.json`
  - `.build/balance/overnight-audit-easy-breaker-cap4-3x.json`
  - `.build/balance/overnight-audit-normal-breaker-cap4-3x.json`
  - `.build/balance/overnight-audit-bastion-breaker-cap4-3x.json`
  - `.build/balance/overnight-audit-hard-cap4-no-protocols-3x.json`
  - `.build/balance/challenge-close-hard-3x.json`
  - `.build/balance/challenge-core6-130-hard-3x.json`
  - `.build/balance/challenge-noreserves-105-hard-3x.json`
  - `.build/balance/overnight-endless60-hard-breaker-cap4-1x.json`

### Current difficulty audit

The current executable was rerun for three seeds across 12 strategies and all four maps (144 runs per profile):

| Difficulty | Wins | Win rate | Average wave | Average lives |
|---|---:|---:|---:|---:|
| Easy | 113/144 | 78.5% | 19.0 | 22.6 |
| Normal | 106/144 | 73.6% | 18.5 | 15.9 |
| Hard | 75/144 | 52.1% | 16.8 | 8.2 |
| Bastion | 16/144 | 11.1% | 12.0 | 1.0 |

The clean separation supports preserving the current multipliers. On Hard, Surge clears 17/36 versus 19-20/36 elsewhere; on Bastion it clears 2/36 and remains the harshest arena. Easy remains broadly forgiving while the intentionally dysfunctional Economy and indiscriminate level-1 Spam policies still fail, so it does not collapse into an automatic win.

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

- The real loopback reconnect test now carries a 1,500-projectile authoritative session through compression and TCP, then validates, reconstructs, and compares the complete client checksum instead of stopping after payload receipt.
- Reconnect snapshots now preserve the exact telemetry attribution-maintenance phase. A regression snapshots mid-cycle, sells the same tower on both restored peers, advances through cleanup, and verifies both source tables and complete checksums remain identical.
- Completed waves now clear all private spawn-group progress before entering intermission. A reconnect captured between waves restores to the same checksum instead of immediately diverging because only the client had canonicalized inactive wave fields.
- Checkpoint and reconnect reconstruction now rejects defense layouts that are impossible on the selected map and authored values that would otherwise be silently normalized on one peer. Current saves are strict while legacy difficulty-less saves preserve their established migration behavior.
- Authoritative reconnect validation now enforces the complete two-player wave-readiness state machine. The regression suite covers valid one-player and both-player preparation snapshots plus impossible queued, early-bonus, and active-wave combinations that previously could be silently normalized.
- Online co-op shared pause now exposes the complete Tactical Library locally by click or Tab. Network polling remains active, and the overlay consumes mouse/keyboard input so planning clicks cannot leak through to the battlefield; a second Escape cleanly requests authoritative resume.
- Challenge automation now supports `--challenge all` and quiet aggregate runs. A 384-run Hard matrix measured Standard 47.9%, Close Quarters 49.0%, No Reserves 46.9%, and the intentionally severe Core Six 29.2%, supporting the current fixed opening compensation without another economy change.
- Challenge sweeps now print an arena-by-directive matrix, exposing geometry-specific restriction spikes instead of allowing them to disappear inside one aggregate directive result. The existing two-seed Hard corpus shows Close Quarters at 54.2% on Crosswind, 45.8% on Foundry, 54.2% on Prism, and 41.7% on Surge; Core Six remains consistently severe rather than containing one anomalous arena. Content loading also rejects shared or byte-equivalent campaign rosters, preserving independently authored waves for every arena.
- Full campaign automation can now sweep `--difficulty all` across every authored arena. The current three-seed, twelve-strategy, 576-run matrix measures Easy 78.5%, Normal 73.6%, Hard 52.1%, and Bastion 11.1%; Surge Divide remains the hardest Hard/Bastion arena after the Rapid Array rebalance.
- The deterministic balance bench now reports swarm kills, controlled survivors, leaks, and aggregate health removed separately. Control towers no longer appear inactive simply because slowed enemies remain alive when the scenario clock ends.
- The practical bench now exercises every completed doctrine/final-role path against a moving 45-health rush, reporting kills, survivors, leaks, health removed, and overkill. This confirms Sentinel Array as a distinct interceptor (10-12 rush kills versus Deadeye's 5-7) while Deadeye retains armor, range, and priority-target superiority, so no flattening stat change was made.
- Forced-build automation can now sweep one tower's four doctrine/final-role pairings or all forty completed paths, retaining the tested path in each machine-readable result. The first all-map Hard Mortar audit placed its four paths within 45.8%-52.1% wins; Quake's low organic pick count reflects agent preference rather than a failed branch, so no speculative stat change was made.
- A 1,920-run all-path audit exposed Rapid Array as the one clear specialization mismatch: it surrendered Rail Pin's reach and armor pierce without actually affecting a crowd. Rapid now fires a compact 16-unit burst capped at two enemies, with lower per-needle damage; Cycler Feed gains a modest cadence-throughput edge and no longer carries an unrelated range penalty. A fresh 144-run Hard audit lands at 52.1%, preserving the target difficulty while making the labeled swarm route mechanically real.
- Run History now retains and exposes a selected defense's economy, early-call, Protocol, Pulse Plate, forge, leak, duration, and top-impact details. Existing history files remain valid because newly added fields default safely to zero.
- Result analysis now exposes early-call earnings and total Protocol activations alongside economy, Pulse Plate, forge, leak, and duration metrics. The narrow panel uses a compact two-column hierarchy instead of adding more full-width rows.
- Feature pass is safely checkpointed, tested, simulated, published, visually inspected, and documented.

## Highest-value next priorities

1. Add optional hosted relay/NAT traversal or automatic port mapping so online friends do not need manual TCP forwarding.
2. Human-playtest all four maps and real internet latency with two remote PCs; preserve reports for any balance change.
3. Expand audio beyond procedural effects only if it can remain optional, lightweight, and stylistically coherent.
4. Continue native-window QA for dense late-wave effects and unusually long localized labels.

## Blockers

- Hosted relay/matchmaking would require an external service and deployment; none is configured or authorized.
- No other blocker is active.
