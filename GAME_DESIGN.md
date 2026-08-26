# Minimal Bastion — Current Game Design

## Design goals

Minimal Bastion is a tactical tower-defense game about readable positioning, permanent build decisions, complementary tower roles, and adapting to authored threat compositions. Its presentation uses flat geometric forms and a restrained tactical palette so target priority, tower identity, range, routes, statuses, and multiplayer intent remain legible in dense late waves.

The core campaign is intended to be learnable without being solved by one universal build. Maps, profiles, and directives change which openings, coverage patterns, and support combinations are efficient. Mastery and Endless convert a successful campaign defense into a reinvestment and scaling problem rather than a separate match.

## Match flow

1. Choose solo or online co-op.
2. Choose an arena, difficulty, and directive. Sandbox is solo-only.
3. Spend shared credits on continuous legal placement inside authored build areas.
4. Start or ready the next authored wave. Manual early calls and configured automatic starts can award 20 credits.
5. Defeat enemies before they traverse the complete route. Escapes remove lives according to enemy rank/type.
6. Between waves, add, upgrade, specialize, retarget, sell, save, or inspect the Tactical Library as the selected directive permits.
7. Easy, Medium, and Hard secure the campaign at wave 20 and may continue into authored Mastery waves 21–30. Bastion requires all 30 authored waves.
8. After Mastery, continue into generated Endless waves beginning at 31.

Defeat occurs when lives reach zero. Victory/results preserve a read-only final layout and detailed contribution/economy/tactical statistics.

## Arenas

- **Foundry Loop** is the baseline route with eight broad build areas and a 400-credit opening.
- **Crosswind Basin** uses a long earth trail and compact crossfire opportunities. It starts at 390 credits.
- **Prism Circuit** compresses placement into six areas around three Surge Nodes and starts at 380 credits.
- **Surge Divide** is the most demanding authored arena. Its low 360-credit opening and stronger late pressure are offset by nine small specialized Surge Nodes that reward deliberate placement.

Every arena owns its campaign and Mastery wave data. Route length, build geometry, node access, economy, roster order, density, and scaling are balanced together rather than applying one universal wave list.

## Difficulty and directives

Difficulty changes enemy health/speed, starting credits, lives, and the required campaign length. Medium is the authored 100% combat baseline. Hard raises enemy health and speed with a smaller life margin. Bastion preserves Hard's combat multipliers, reduces lives to 16, and makes all 30 authored waves one complete expert campaign.

Directives change available decisions:

- **Standard** is the complete ruleset.
- **Signal Gauntlet** adds enemy support and disruption roles while preserving the full defensive toolset.
- **Core Six** restricts the roster to Needle, Frost, Shard, Ember, Breaker, and Beacon with the standard opening economy.
- **Entrenched** preserves all permanent towers but removes Plates, Forge, Protocols, and selling.
- **Sandbox Lab** exposes real combat and authored waves in a noncompetitive test environment.

Directive compensation is applied once when the run is created. It never changes tower damage or utility over time.

## Economy

- Credits are shared in co-op.
- Enemy rewards and wave rewards fund towers, upgrades, Apex promotions, tactical devices, and emergency responses.
- Selling normally returns 60% of invested tower/Forge cost. Entrenched disables selling.
- Direct Pulse Plate purchases start at 60 credits and rise by 15 for each additional purchase in the same active wave. The price resets next wave.
- A Charge Forge exchanges a large permanent investment and remote build freedom for recurring stored Plates during active waves.
- Apex promotions provide a compact late-game credit sink once Mastery begins.

The economy is intentionally tight during campaign openings and increasingly flexible later. Mastery waves absorb campaign reserves through Apex and coverage investment; Endless health growth eventually outpaces a static defense.

## Tower progression

Each tower has three permanent levels:

- Level 1 establishes the base role.
- Level 2 chooses one of two doctrines.
- Level 3 chooses one of two final roles compatible with either doctrine.

This produces four complete builds per tower. Doctrines emphasize a statistical or delivery approach; final roles provide the larger mechanical identity. Upgrade previews show exact current-to-next values, including utility fields such as slow strength/duration, projectile speed, arcs, splash, Expose, Armor Break, or Beacon aura changes.

Mastery unlocks one authored Apex promotion for completed level-three towers. Apex preserves the selected build's identity while raising its late-game ceiling. It is a permanent upgrade, remains available in Entrenched, and is recorded in saves, co-op snapshots, history layouts, and contribution data.

## Tower roles

- **Needle Turret:** low-cost direct coverage; develops ricochet or piercing pressure.
- **Frost Spire:** area slow and damage; defaults to Fastest so it spreads control efficiently.
- **Shard Fan:** short-range multi-projectile coverage against groups.
- **Watchtower:** long-range priority damage and remote coverage.
- **Ember Coil:** persistent burn, with paths for faster application, intensity, area spread, or armor interaction.
- **Breaker Cannon:** armored/shielded target counter with heavy-hit and wider break options.
- **Signal Beacon:** non-targeting aura support. Overlapping Beacons use the strongest applicable bonus rather than stacking identical effects.
- **Arc Relay:** chained damage and control; gains value from groups and slowed targets without making that synergy mandatory.
- **Siege Mortar:** predictive long-range area fire. It uses current target movement at launch and authored impact caps to prevent unbounded packed-wave damage.
- **Prism Beam:** sustained durable-target pressure and Expose support.

Targeting remains a player-controlled tactical layer. Support prioritizes Signal Gauntlet carriers; other modes weight route progress, health, distance, speed, or armor. A targeting choice opens a menu and does not change until the replacement is confirmed.

## Protocols and automatic activation

Every tower has a named temporary Protocol with authored duration, cooldown, effects, and an automatic trigger. Manual activation rewards timing; automation reduces repetitive input and selects only one armed tower at a time. Automatic conditions reflect the tower's role, such as enemy density, armored pressure, engaged supported towers, or elite/boss presence.

Protocol state is visually separate from tower level, player ownership, selected-tower state, placement ghosts, and enemy statuses. Entrenched removes Protocols entirely.

## Tactical devices

Pulse Plates snap to legal route positions and trigger twice. They deal fixed area damage, stun, slow, and bounded knockback. A per-enemy push grace and reduced elite/boss displacement prevent plate chains from becoming permanent route denial. The deployed field is capped at 16.

The Charge Forge produces stored Plates only while enemies are being fought. Its levels improve cadence and storage and later strengthen plate damage. This prevents downtime waiting from generating free inventory.

## Enemies and statuses

Five base enemy profiles cover light, fast, armored, shielding, and regenerating pressure, with Standard, Elite, and Boss ranks. Authored waves combine profiles and rank pressure per arena.

Signal Gauntlet adds visible carrier roles:

- **Accelerator:** increases nearby enemy speed.
- **Restorer:** periodically repairs nearby enemies.
- **Bulwark:** periodically grants nearby shields.
- **Jammer:** weakens one tower's rate and damage for a short duration.
- **Disruptor:** temporarily pauses a nearby tower group; reserved for later elite/boss pressure.

Carriers render above ordinary enemies, use the same in-body glyphs in combat and the Tactical Library, and show their support relationship through aura/recipient feedback.

Core statuses are Slow, Stun, Expose, and Armor Break. Their glyphs are distinct and can coexist. Damage resolution applies armor, shields, pierce, rank modifiers, Expose, Armor Break, burn, splash caps, and source attribution deterministically.

## Campaign, Mastery, and Endless

Waves 1–20 form the Easy, Medium, and Hard campaign. Waves 21–30 are an optional arena-specific Mastery sequence for those profiles and the required final third of Bastion. The sequence smooths pressure toward a demanding wave-30 capstone. Generated Endless begins at wave 31 and rotates balanced, runner, armored, regenerator, and boss themes.

Endless health follows accelerating growth, while count, speed, cadence, and spawn delay use performance-minded caps/floors. The goal is escalating durability and composition pressure without allowing unit count alone to overwhelm rendering or networking.

## Co-op design

Co-op is the same shared defense game, not a separate power mode. Credits, lives, towers, tactical systems, waves, speed, and results are shared. Both players may manage every defense; original placement ownership is informational only.

The host sequences authoritative commands. Both peers run the deterministic simulation locally for responsive rendering and repair divergence from host snapshots. Both players must ready a wave. Disconnect pauses and preserves the host session, and a reconnect restores complete state. A co-op checkpoint can also be continued alone.

## Persistence and metagame

The game maintains one rolling autosave and expandable manual slots. Manual slots can be duplicated or deleted. Run History is independent from saves and stores a single evolving record when a campaign continues through Mastery or Endless.

The Tactical Library is discovery-driven. Exact future systems are not exposed until encountered. Medals reward notable single-run constraints and outcomes; achievements combine progression, repeated accomplishments, profile clears, discovery, and long-term records so the player retains visible goals after learning the campaign.

## Presentation

The simulation uses a 1280×720 logical canvas rendered internally at 2560×1440 and fitted into a clipped 16:9 viewport. Palette constants are independent from scaling. The battlefield uses desaturated teal/navy foundations, slate routes or arena-specific channels, off-white panels, and controlled semantic accents.

Shapes, motion, line treatment, and glyphs carry identity. Effects use bounded expanding rings, flashes, recoil, beams, and geometric shatters instead of particle-heavy spectacle. The most important range, status, placement, node, ownership, and co-op cues retain priority under dense load.

## Balance intent

- Campaign openings should reward route understanding and efficient coverage.
- No tower or complete upgrade path should be mandatory across every arena.
- Support and control should be measured alongside direct damage, not judged only by kills.
- Long range pays for coverage but should not dominate well-positioned short-range towers.
- Surge Nodes are an arena resource, not a free global multiplier; their small footprints should force tradeoffs.
- Bastion is a premier challenge, while Easy and Medium provide room to learn systems.
- Bot completion rates compare configurations and detect regressions; they are not direct forecasts of expert human success.

Measured baselines and known interpretation limits are maintained in [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md) and [BALANCE_REPORT.md](BALANCE_REPORT.md).
