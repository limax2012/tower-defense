# Maximal Bastion — Current Game Design

## Design goals

Maximal Bastion is a tactical tower-defense game about readable positioning, permanent build decisions, complementary tower roles, and adapting to deliberate enemy compositions. Its industrial presentation places a municipal defense network among besieged fabrication, transit, archive, and power districts. Mechanical silhouettes and restrained neon accents keep target priority, tower identity, range, routes, statuses, and multiplayer intent legible in dense late waves.

The core campaign is intended to be learnable without being solved by one universal build. Maps, difficulty profiles, and modes change which openings, coverage patterns, and support combinations are efficient. The final ten campaign waves and Endless convert an established defense into a reinvestment and scaling problem rather than a separate match.

## Match flow

1. Choose solo or online co-op.
2. Choose an arena, difficulty, and mode. Sandbox is solo-only.
3. Spend shared credits on continuous legal placement inside the arena's build areas.
4. Start or ready the next campaign wave. Manual early calls and automatic starts chosen in advance can award 20 credits.
5. Defeat enemies before they traverse the complete route. Escapes remove lives according to enemy rank/type.
6. Between waves, add, upgrade, specialize, retarget, sell, save, or inspect Tower Intel as the selected mode permits.
7. Secure the campaign by completing all 30 waves. Wave 21 begins the final escalation and unlocks Apex promotions.
8. Continue into Endless waves beginning at 31.

Defeat occurs when lives reach zero. Victory/results preserve a read-only final layout and detailed contribution/economy/tactical statistics.

## Arenas

- **Cinderworks** is the baseline route with eight broad build areas and a 400-credit opening.
- **Rainline Heights** uses long rooftop skybridges and compact crossfire opportunities. It starts at 390 credits.
- **Prism Nullspace** compresses placement into six areas around three Surge Nodes and starts at 380 credits.
- **Helix Reactor** is the most demanding arena. Its low 360-credit opening and stronger late pressure are offset by nine small specialized Surge Nodes that reward deliberate placement.

Every arena owns its complete campaign wave data. Route length, build geometry, node access, economy, enemy order, density, and scaling are balanced together rather than applying one universal wave list.

## Difficulty and modes

Difficulty changes enemy health/speed, starting credits, and lives. Every profile uses the same 30-wave campaign. Medium is the 100% combat baseline with 12 lives. Hard raises enemy health and speed with a six-life margin. Bastion raises them further and allows no breach: it begins with one life.

Modes change available decisions:

- **Standard** is the complete ruleset.
- **Signal Gauntlet** adds enemy support and disruption roles while preserving the full defensive toolset.
- **Core Six** restricts the roster to Needle, Frost, Shard, Ember, Breaker, and Beacon with the standard opening economy.
- **Entrenched** preserves all permanent towers but removes Plates, Forge, Protocols, and selling.
- **Sandbox Lab** exposes real combat and campaign waves in a noncompetitive test environment. Test/Reset Protocol affects only the selected tower, bypassing the shared manual cooldown without changing other towers' timers or Apex automation. Reset Test clears targets, plates, tower metrics, and Protocol timers while preserving placed towers. Pulse Plates deploy freely with normal placement and active-field limits. Wave signals are optional and off by default.

Competitive modes use the selected difficulty's opening economy without compensating credits. They never change tower damage or utility over time.

## Economy

- Credits are shared in co-op.
- Enemy rewards and wave rewards fund towers, upgrades, Apex promotions, tactical devices, and emergency responses.
- Selling normally returns 60% of invested tower/Forge cost. Entrenched disables selling.
- Direct Pulse Plate purchases start at 60 credits and rise by 15 for each additional purchase in the same active wave. The price resets next wave.
- A Charge Forge exchanges a large permanent investment and remote build freedom for recurring stored Plates during active waves.
- Apex promotions provide a compact late-game credit sink beginning with the final campaign act.

The economy is intentionally tight during campaign openings. Rewards retain their full value through wave 14, are halved for waves 15–24, and are quartered from wave 25 onward. The ordinary kill-reward taper still begins after wave 10. The late campaign pressure curve accounts for those smaller reinvestment steps instead of increasing health by a visually tidy but unsupported amount. Apex and completed coverage provide compact late investments, and Endless health growth eventually outpaces a static defense.

## Tower progression

Each tower has three permanent levels:

- Level 1 establishes the base role.
- Level 2 chooses one of two doctrines.
- Level 3 chooses one of two final roles compatible with either doctrine.

This produces four complete builds per tower. Doctrines emphasize a statistical or delivery approach; final roles provide the larger mechanical identity. Upgrade previews show exact current-to-next values, including utility fields such as slow strength/duration, projectile speed, arcs, splash, Expose, Armor Break, or Beacon aura changes.

Wave 21 unlocks one Apex promotion for completed level-three towers. Apex preserves the selected build's identity while raising its late-game ceiling. It is a permanent upgrade, remains available in Entrenched, and is recorded in saves, co-op snapshots, history layouts, and contribution data.

Each tower family has four distinct hardware stages. T1 uses a compact functional assembly; T2 adds specialized weapon supports; T3 gains advanced emitters, armor, or capacitor banks; Apex has a larger final assembly with a distinctive silhouette. Shots, beams, impacts, and Beacon broadcasts follow the same progression while retaining the family's color and weapon identity. Tier differences remain visible with Reduced Effects enabled.

## Tower roles

- **Needle Turret:** low-cost direct coverage; develops ricochet or piercing pressure.
- **Frost Spire:** area slow and damage; defaults to Fastest so it spreads control efficiently. Its crystal and coolant assembly rotate together at a steady speed, with discharge centered on the crystal.
- **Shard Fan:** short-range multi-projectile coverage against groups. An exposed central aperture launches guided plasma shards in any direction, with three radial focusing assemblies evolving into paired petals at higher tiers. The assembly turns at a steady speed independent of its targets. The core flashes at discharge, and brief filaments follow the actual departing shots. Targeting and projectile travel begin at the tower center.
- **Watchtower:** long-range priority damage and remote coverage.
- **Ember Coil:** persistent burn, with paths for faster application, intensity, area spread, or armor interaction.
- **Breaker Cannon:** armored/shielded target counter with heavy-hit and wider break options.
- **Signal Beacon:** non-targeting aura support. Overlapping Beacons use the strongest applicable bonus rather than stacking identical effects.
- **Arc Relay:** chained damage and control; gains value from groups and slowed targets without making that synergy mandatory.
- **Siege Mortar:** predictive long-range area fire. It uses current target movement at launch and bounded impact caps to prevent unbounded packed-wave damage. Its concentric launch assembly turns slowly at a constant speed, independently of the selected target.
- **Prism Beam:** sustained durable-target pressure and Expose support.

Targeting remains a player-controlled tactical layer. Signal Gauntlet adds Support, which prioritizes its signal enemies; Sandbox also exposes it for controlled signal tests. The general modes weight route progress, health, distance, speed, or armor. A targeting choice opens a menu and does not change until the replacement is confirmed.

## Protocols and automatic activation

Every tower has a named temporary Protocol with a defined duration, cooldown, effects, and automatic trigger. Manual activation rewards timing; automation reduces repetitive input and selects only one armed tower at a time. Automatic conditions reflect the tower's role, such as enemy density, armored pressure, engaged supported towers, or elite/boss presence.

Every tower's rails, emitters, and cooling assemblies carry operational lighting in its weapon palette. Localized light responds to firing, while active Protocols intensify the machinery and surround it with a luminous energy field. Short family-specific flows distinguish inward charge, swirling frost, outward heat and pressure, electrical branches, optical refraction, and transmitted signals. Fields appear promptly, remain strong throughout the active duration, and fade at expiration. Separate green receiver contacts identify active Signal Beacon support. Surge Node coverage is identified by node-colored receiving couplers during placement and by its named source in Tower Intel; placed towers do not carry an additional node marker. An attached controller marks the tower armed for automatic Protocol activation: three chasing lights indicate waiting, and a steady ON display identifies the active Protocol duration. These states remain separate from tower level, player ownership, selection, and placement feedback. Reduced Effects retains powered hardware and active Protocol fields at a fixed decorative phase. Entrenched removes manual and armed Protocol controls; Apex retains automatic activation.

Protocol ignition releases short, uneven energy discharges in the tower's weapon palette. Area Protocols propagate through their authored burst radius, with frost fragments, electrical branches, refracted light, heat, or pressure suited to the weapon. Protocols without an area burst use a compact release or inward charge around the machine. The brief ignition and sustained field are separate visual states; neither changes the Protocol's damage, targeting, or timing.

## Tactical devices

Pulse Plates snap to legal route positions and trigger twice. They deal fixed area damage, stun, slow, and bounded knockback. A per-enemy push grace and reduced elite/boss displacement prevent plate chains from becoming permanent route denial. The deployed field is capped at 16.

The Charge Forge produces stored Plates only while enemies are being fought. Its levels improve cadence and storage and later strengthen plate damage. This prevents downtime waiting from generating free inventory.

## Enemies and statuses

Five enemy families cover light, fast, armored, shielding, and regenerating pressure. Their visible size order is Runner, Crawler, Brute, Aegis, Regenerator. Graphite armor and recessed magenta reactors connect the family designs. Runner uses interceptor propulsion; Crawler has four legs, Brute six, Aegis eight, and Regenerator a symmetric tracked chassis. Elites and Bosses reinforce each family's machinery. At half health, Boss overdrive opens protective components and brightens the core. Walking contacts stay planted while supporting the body and return through controlled shallow swings. Turning steps shorten gradually and stay inside the road deck. Actual travel drives feet and track motion, including Slow, Stun, and resume. Campaign waves combine profiles and rank pressure per arena.

Signal Gauntlet adds visible signal roles to ordinary enemies:

- **Accelerator:** increases nearby enemy speed by 20% while the signal enemy remains in formation range.
- **Restorer:** repairs nearby enemies for 10% of maximum health every 5 seconds.
- **Bulwark:** grants nearby shields equal to 10% of maximum health every 5 seconds, up to a 20% shield reserve.
- **Jammer:** weakens the rate and damage of every combat tower in its pulse radius for a short duration.
- **Disruptor:** every five seconds, pauses the highest-investment tower in reach. Rank increases its single-target pause and reach; a recovery lockout prevents multiple Disruptors from repeatedly disabling the same tower at once.

Each arena owns its campaign wave composition. Difficulty profiles scale that same enemy lineup, while Signal Gauntlet deterministically assigns signal roles: Accelerator appears on wave 2, Restorer on wave 3, Bulwark on wave 4, Jammer on wave 5, and later formations alternate signal enemies. Wave 20 narrows the ordinary formation to one Restorer and one Jammer around its boss Disruptor; other elite or boss groups use Disruptor. Sandbox wave replays can enable these assignments for unlimited-resource formation tests; manual Sandbox spawning remains the controlled way to isolate any enemy, rank, health scale, or signal role.

Shield is a separate temporary durability pool above health. Every family displays shield light whenever its shield pool is positive, independently of signal role. Aegis has an authored shield; Bulwark signals can grant shields to nearby recipients from any family. Ordinary hits remove raw shield before armor mitigation is evaluated; shield-bypassing attacks skip that pool and damage health through the normal armor calculation.

Signal enemies identify their capability through their existing gem: white for Accelerator, green for Restorer, blue for Bulwark, orange for Jammer, and saturated purple for Disruptor. Each complete carrier body renders above ordinary enemies and combat effects; overlapping carriers occlude one another normally, including their gems. The three support sources expose their true application radii with restrained colored outlines. Successful transfers produce brief source and recipient feedback. Disruption dims a tower's powered parts and interrupts its conductors with purple fault contacts; core, diamond transmission, receiving tower, and status label share that color. Disruption purple is more saturated than Prism's lighter lavender violet. Jammer sends orange radio wavefronts to affected receivers.

Burn, Slow, Armor Break, and Expose appear as soft colored fog regions over the chassis. The cloud drifts gently, fades in when the first debuff arrives, and dissipates after the last expires. Fixed color partitions change without rotating or reshaping the cloud. Their colors match the relevant tower families. Stun has a short electrical state with fine Arc-blue branches, sparks, and restrained chassis vibration over grounded feet or tracks. Native regeneration and speed buffs do not add recipient fog or asymmetric body animations.

Hovering shows current condition names, durations, and magnitudes; clicking an enemy pins its live Intel panel. Its portrait uses a symmetrical resting stance independently of battlefield motion. Every visible enemy has a health meter with a two-pixel fill inside a closed frame; shields have a separate one-pixel fill. Hovering leaves their dimensions unchanged. The shield shell remains whole while reserve exists, reacts to impacts, and collapses when broken. Reduced Effects retains steady fog regions, signal identity, quiet electrical contacts, shields, and grounded movement. Damage resolution applies armor, shields, pierce, rank modifiers, Expose, Armor Break, burn, splash caps, and source attribution deterministically.

## Campaign and Endless

Waves 1–30 form the campaign on every difficulty. Waves 21–30 are the final arena-specific escalation, unlock Apex investment, and build toward a demanding wave-30 capstone. Endless begins at wave 31 and rotates balanced, runner, armored, regenerator, and boss themes.

Endless health follows accelerating growth, while count, speed, cadence, and spawn delay use performance-minded caps/floors. The goal is escalating durability and composition pressure without allowing unit count alone to overwhelm rendering or networking.

## Co-op design

Co-op is the same shared defense game, not a separate power mode. Credits, lives, towers, tactical systems, waves, speed, and results are shared. Both players may manage every defense; original placement ownership is informational only.

The host sequences authoritative commands. Both peers run the deterministic simulation locally for responsive rendering and repair divergence from host snapshots. Both players must ready a wave. Disconnect pauses and preserves the host session, and a reconnect restores complete state. A co-op checkpoint can also be continued alone.

## Persistence and metagame

The game maintains one rolling autosave and expandable manual slots. Manual slots can be duplicated or deleted. Run History is independent from saves and stores a single evolving record when a completed campaign continues into Endless.

Tower Intel exposes complete current statistics and exact upgrade comparisons when selecting or placing a tower. Mode and difficulty descriptions appear in run setup. Medals reward notable single-run constraints and outcomes; achievements combine progression, repeated accomplishments, profile clears, and long-term records so the player retains visible goals after learning the campaign.

## Presentation

The main menu has one primary Play action and two live defense feeds. Play opens a single setup screen with illustrated map cards, difficulty, mode, and Start Run. Selecting a map only changes the setup selection. Menus share a layered command-deck environment, restrained cyan selection accents, clear primary actions, and compact labels. Peripheral furnace banks, linked decks, virtual planes, and reactor rings connect the interface to all four districts. Moving edge signals and slow holographic rotation add atmosphere; Reduced Effects freezes decorative motion. Difficulty and mode descriptions appear only on hover, while underlines preserve selection feedback.

The HUD keeps lives, credits, wave state, and run controls visible. Tower and tactical information appears on selection or hover. Selected and placement-preview towers show complete current statistics. Upgrade comparisons use the same grid, and placed towers always show lifetime contribution beneath it. Wave hover reveals secondary wave information. Placement guidance takes priority over announcements. Results offer overview and statistics views. History lists run identity and outcome, with statistics and final layouts on a separate detail screen. Save rows show slot, map, difficulty, wave, and timestamp. Settings and career screens divide their information into tabs. Escape closes an expanded detail layer before leaving its screen. See [interface design](docs/interface-design.md).

The simulation uses a 1280×720 logical canvas rendered internally at 2560×1440 and fitted into a clipped 16:9 viewport. Palette constants are independent from scaling. Four cyberpunk environments share clear route edges, bracketed build surfaces, and dark tactical panels with ice-white text. Cinderworks uses copper furnace machinery and grated work decks; Rainline Heights uses rain-slick utility decks and raised causeways above an automated cooling and data complex; Prism Nullspace uses violet hard-light lanes and suspended armored archives; Helix Reactor uses green containment rings and coolant channels. Ceramic armor and graphite chassis unify the machines. Cyan marks precision and control hardware; amber marks kinetic and thermal weapons; violet marks focused beam hardware; green marks support; magenta identifies hostile machine optics.

Twin-barrel sentries, long railguns, spread launchers, cryo rotors, thermal coils, capacitor relays, mortar apertures, prism emitters, and holographic beacons share a modular armored chassis. Enemies use walking legs, swept drone hulls, armor, and illuminated optics. Directional weapons track targets; Shard, Frost, and Mortar assemblies turn continuously around centered emitters. Recoil, hit flashes, beam cores, trails, fragmented pressure bursts, and short-lived sparks communicate combat. Weapon impacts shed material suited to their technology: piercing sparks, plasma fragments, frost chips, heat, armor splinters, electrical discharge, or refracted light. Mortar and Pulse Plate detonations combine bright contact flashes, irregular energy pockets, and outward fragments. Destroyed enemies shed metal debris; escape feedback contracts inward, and completed Forge charges draw energy toward the machine. Rising embers, rain and distant air traffic, floating data crystals, and rotating reactor rings give each location its own ambient motion. Fitted foundation lighting, translucent hardware placement previews, and brief credit-change readouts provide interaction feedback. Range boundaries use thin continuous lines. Surge Nodes appear as embedded relay devices; compact receiving couplers beneath placement previews inherit each covering node's color without connection lines. Tower Intel names the active node and distinguishes it from green Beacon support. Reduced Effects freezes environmental animation and simplifies combat bursts while preserving their direction and weapon identity. The most important range, status, placement, node, ownership, and co-op cues retain priority under dense load.

## Balance intent

- Campaign openings should reward route understanding and efficient coverage.
- No tower or complete upgrade path should be mandatory across every arena.
- Support and control should be measured alongside direct damage, not judged only by kills.
- Long range pays for coverage but should not dominate well-positioned short-range towers.
- Surge Nodes are an arena resource, not a free global multiplier; their small footprints should force tradeoffs.
- Bastion is a premier challenge, while Easy and Medium provide room to learn systems.
- Bot completion rates compare configurations and detect regressions; they are not direct forecasts of expert human success.

Measured baselines and known interpretation limits are maintained in [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md) and [BALANCE_REPORT.md](BALANCE_REPORT.md).
