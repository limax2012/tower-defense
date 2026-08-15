# Minimal Bastion Game Design

## Core loop

Spend credits to place geometric defenses in continuous build zones around a fixed route. Read the next mixed threat, choose coverage and counters, upgrade or specialize towers, use targeting priorities, and intervene with Pulse Plates or Overdrive. Calling a wave during its ten-second preparation window grants 20 credits. Survive all 20 waves and the phased Bastion Core boss.

Permanent tower damage is mechanically consistent from wave 1 through wave 20. There is no hidden early-game damage lift.

## Economy

- Hard difficulty uses each arena's base economy: Foundry Loop 400 credits, Crosswind Basin 390, Prism Circuit 380, and Surge Divide 360, all with 20 lives.
- Easy and Normal multiply those map-specific credits and provide 30 or 24 lives; Bastion preserves base credits but reduces the margin to 15 lives.
- Enemies award kill credits; completed waves award `40 + 10 * wave`.
- Skipping a live preparation countdown awards 20 credits. The first wave never grants this reward.
- Towers and the Charge Forge sell for 60% of total invested cost.
- One Pulse Plate begins in storage. Direct replacements are available during active waves. Every wave starts at the same 60-credit price; additional direct purchases within that wave add 15 credits, and the price resets to 60 at the next wave.
- The 300-credit Charge Forge converts a large current investment into capped future plate inventory, but produces only during active waves.

## Towers and specializations

| Tower | Identity | Final specialization choice |
| --- | --- | --- |
| Needle Turret | Cheap, reliable short-range generalist. | Rapid Array for swarm cadence or Rail Pin for heavy armor-piercing shots. |
| Frost Spire | Low-damage area control that extends exposure time. | Permafrost for maximum slow and duration or Hail Lancer for fast direct area damage with a lighter slow. |
| Shard Fan | Short-range multi-projectile swarm control. | Razor Bloom for wide seven-shard clear or Lance Fan for a tight armor-piercing burst. |
| Watchtower | Long-range priority damage for runners and durable targets. | Sentinel Array for steady interception or Deadeye Post for extreme-range priority shots. |
| Ember Coil | Persistent burn pressure after range contact. | Wildfire Matrix for crowded-route burn or Searing Brand for long-range, armor-piercing boss burn. |
| Breaker Cannon | Heavy anti-armor hit and armor reduction. | Breach Round for elite/boss penetration or Shatter Shell for area armor-break. |
| Arc Relay | Density-dependent chaining with late stun. | Storm Lattice for long damage chains or Lockdown Coil for fewer, heavier control arcs. |
| Siege Mortar | Slow, long-range area burst against packed groups. | Salvo Rack for frequent compact shells or Quake Shell for wider control at lower DPS. |
| Prism Beam | Rapid focused damage and Exposed amplification. | Core Lance for shield-bypassing focus or Spectrum Split for branched beams. |
| Signal Beacon | Position-dependent range and attack-speed support. | Tempo Beacon for maximum cadence or Horizon Beacon for wider reach. |

Branch choices occur after level 2, are mutually exclusive and permanent, and preview exact role/stat changes. Every placed tower uses the same integrated level language: one top spoke at level 1, a second at 120 degrees for level 2, and a third at 240 degrees for level 3.

## Enemies, elites, and boss

- Crawler: baseline pressure.
- Runner: fast, fragile leak threat.
- Brute: slow armored target.
- Aegis: shielded, heavily armored, costs 3 lives.
- Regenerator: durable sustain threat, costs 5 lives.
- Elite rank: 1.85x health, modest speed/armor/reward increase, +1 leak damage, and 30% control resistance.
- Bastion Core boss: 4.5x base health, bonus armor and shield, at least 10 leak damage, 60% control resistance, and a telegraphed half-health phase that restores shield and accelerates.

Traits remain visible through silhouette, health/shield treatment, rank rings, motion, and wave warnings. Avoid immunity spam.

## Waves and difficulty

- Every arena has its own authored 20-wave campaign: Foundry 1,090 contacts, Crosswind 1,066, Prism 1,192, and Surge 1,063 before difficulty multipliers.
- Mixed compositions include screens, rushes, escorts, feints, armored pressure, elites, layered assaults, endurance streams, and a final boss.
- Wave intel communicates approximate count plus swarm, speed, armor, shield, regeneration, elite, and boss threats.
- Health/speed progression is authored and deterministic.
- Easy, Normal, Hard, and Bastion are player-selectable. They consistently modify enemy health/speed, starting credits, and lives without changing tower stats by wave or elapsed time.

## Status, synergy, and targeting

- Slow: strongest slow applies; Arc Relay deals 35% more damage to slowed targets.
- Burn: capped at two sources; burning lowers effective armor by 2.
- Armor reduction/pierce: creates follow-up value for rapid attacks.
- Exposed: increases incoming damage and rewards focus fire.
- Stun: short, resistance-scaled control.
- Signal Beacon: nearby attack-rate and range aura. Affected towers show a compact gold broadcast marker, while Tower Intel reports the exact Beacon-only rate and range deltas alongside effective combined stats.
- Surge Nodes: map-authored attack-rate, range, damage, or armor-pierce bonuses with deliberately small footprints. A tower center must be inside a node; bonuses stack additively with Beacon and active Protocols.

Targeting modes are First, Last, Strongest, Weakest, Nearest, Fastest, and Armored. Fastest uses current post-control speed; Armored prioritizes effective armor and then durability. Non-attacking support structures do not expose targeting controls.

## Placement and maps

Placement is continuous inside authored build regions. Towers cannot overlap towers/forge, violate route clearance, leave map bounds, or use a placement grid. Build fields use quiet tint and exact corner brackets.

- Foundry Loop: broad classic road and forgiving fields; the baseline campaign mixes threats progressively.
- Crosswind Basin: cyan-banked current channel around three crossfire islands; its campaign leads with runner and alternating-speed pressure.
- Prism Circuit: violet conduit, isolated build pockets, and three modest nodes; its denser campaign emphasizes layered armor and speed.
- Surge Divide: tight road pockets and nine small nodes under the hardest authored campaign pressure. The power is real, but concentrating inside nodes creates coverage and composition tradeoffs.

Each arena has a distinct flat geometric route treatment and background motif while retaining the shared muted teal/slate tactical language.

## Active and emergency systems

- Pulse Plate: forgiving road-snapped two-charge defense. Each pulse deals 38 area damage in radius 52, briefly stuns and slows the group, pierces 2 armor, and pushes the triggering enemy up to 28 path units backward. Elites receive 60% of that push and bosses 25%; after an accepted push, an enemy has 0.75 seconds of knockback grace while still taking plate damage and status effects. The field supports at most 16 active plates. These limits preserve useful opening control without allowing a packed road to chain-lock enemies.
- Charge Forge: one per map. Level 1 produces every 34 active-wave seconds to capacity 3; levels 2/3 improve cadence to 26/20 seconds, capacity to 4/5, and plate damage by 15%/30%. Production is frozen before and between waves and while storage is full. The fixed cadence is identical at every wave number; only purchasing an upgrade changes it.
- Protocols replace the old generic Overdrive bonus. Each tower has a thematic active burst and timed stat package on a shared per-protocol cooldown; one selected tower can optionally auto-trigger under nearby pressure or against an elite/boss.

Tactical controls live in the sidebar so they never cover usable battlefield space. Plates are responsive recovery tools; permanent towers remain the strategic foundation.

## Online co-op

- Two-player direct internet TCP on port `28741`; host shares a public IP/DNS endpoint and six-character code.
- The host sequences authoritative commands; both peers execute the same fixed-tick deterministic stream and compare checksums.
- Credits, lives, plate inventory, forge, waves, speed, and victory/defeat are shared.
- Towers and forge retain visible P1/P2 placement attribution, but either player may upgrade, specialize, retarget, Overdrive, or sell any shared defense.
- Both players must ready a wave. Calling during intermission grants the normal shared early reward.
- Middle-click pings are color-coded by player.
- Pause is disabled during co-op; either peer may command shared speed.
- Restart at victory or defeat preserves the peer connection, resets both players to a fresh host-authoritative session on the same map, and allocates a new host save slot. Main Menu disconnects.
- A temporary disconnect pauses both peers in a reconnect overlay. Player 2 can reconnect with the same endpoint/code and receives a complete host-authoritative active-match snapshot before both simulations resume.

Direct TCP currently requires manual port forwarding or a peer-to-peer VPN and does not provide matchmaking, automatic NAT traversal, encryption, or a hosted relay service. The host is authoritative for command ordering and recovery snapshots. Periodic checksums detect drift, and reconnecting Player 2 receives the active wave, enemies, projectiles, defenses, economy, lives, timers, ready state, and pending commands before both peers resume.

## Run analysis and replayability

Victory/defeat reports wave reached, lives, kills, leaks, tower damage leaders, credits earned/spent/recovered, plate usage/damage, forge production, dangerous leak type, and simulated defense time. After the authored wave-20 campaign victory, **Continue Endless** resumes the same live battlefield in intermission instead of exposing a frozen final-field state. A paginated, dynamically expanding set of independent intermission saves preserves solo or host-authoritative co-op endless runs, while transient co-op reconnect snapshots preserve an active session. New runs take the lowest empty number without overwriting; occupied or unreadable saves require a two-click confirmed deletion. Each overwritten slot retains one recovery generation and transparently falls back to it if the primary file is absent or corrupt. Headless JSON additionally records role choices, Protocols, early-call credits, armor/shield outcomes, and per-wave/tower details.

Endless wave `s = wave - 20` uses the authored wave 20 as its deterministic anchor. Health multiplier grows by `1 + 0.085s + 0.0045s²`; base group counts grow by 1.25% per wave up to +60%; spawn intervals tighten by 0.75% per wave up to 20%; and speed adds 0.006 per wave up to a 1.30 multiplier. Five rotating roster themes add focused runner, armored, regenerator, or elite pressure, with a scaled Bastion Core every fifth endless wave. This keeps wave 21 recognizable, makes fixed max-level defenses eventually fail, and bounds enemy count for rendering and simulation stability.

Current replayability comes from four authored arenas/campaigns, four difficulty profiles, strategic tower pools, two final roles for every tower, targeting, manual or automatic Protocol timing, and deterministic agent/seeds. Seeded player challenges, constrained-roster modes, and run history remain future candidates.
