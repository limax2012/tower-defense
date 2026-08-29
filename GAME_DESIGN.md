# Minimal Bastion — Current Game Design

## Design goals

Minimal Bastion is a tactical tower-defense game about readable positioning, permanent build decisions, complementary tower roles, and adapting to authored threat compositions. Its presentation uses flat geometric forms and a restrained tactical palette so target priority, tower identity, range, routes, statuses, and multiplayer intent remain legible in dense late waves.

The core campaign is intended to be learnable without being solved by one universal build. Maps, profiles, and directives change which openings, coverage patterns, and support combinations are efficient. The final ten authored waves and Endless convert an established defense into a reinvestment and scaling problem rather than a separate match.

## Match flow

1. Choose solo or online co-op.
2. Choose an arena, difficulty, and directive. Sandbox is solo-only.
3. Spend shared credits on continuous legal placement inside authored build areas.
4. Start or ready the next authored wave. Manual early calls and configured automatic starts can award 20 credits.
5. Defeat enemies before they traverse the complete route. Escapes remove lives according to enemy rank/type.
6. Between waves, add, upgrade, specialize, retarget, sell, save, or inspect the Tactical Library as the selected directive permits.
7. Secure the campaign by completing all 30 authored waves. Wave 21 begins the final escalation and unlocks Apex promotions.
8. Continue into generated Endless waves beginning at 31.

Defeat occurs when lives reach zero. Victory/results preserve a read-only final layout and detailed contribution/economy/tactical statistics.

## Arenas

- **Foundry Loop** is the baseline route with eight broad build areas and a 400-credit opening.
- **Crosswind Basin** uses a long earth trail and compact crossfire opportunities. It starts at 390 credits.
- **Prism Circuit** compresses placement into six areas around three Surge Nodes and starts at 380 credits.
- **Surge Divide** is the most demanding authored arena. Its low 360-credit opening and stronger late pressure are offset by nine small specialized Surge Nodes that reward deliberate placement.

Every arena owns its complete authored campaign wave data. Route length, build geometry, node access, economy, roster order, density, and scaling are balanced together rather than applying one universal wave list.

## Difficulty and directives

Difficulty changes enemy health/speed, starting credits, and lives. Every profile uses the same 30-wave campaign. Medium is the authored 100% combat baseline with 12 lives. Hard raises enemy health and speed with a six-life margin. Bastion preserves Hard's combat multipliers but allows no breach: it begins with one life.

Directives change available decisions:

- **Standard** is the complete ruleset.
- **Signal Gauntlet** adds enemy support and disruption roles while preserving the full defensive toolset.
- **Core Six** restricts the roster to Needle, Frost, Shard, Ember, Breaker, and Beacon with the standard opening economy.
- **Entrenched** preserves all permanent towers but removes Plates, Forge, Protocols, and selling.
- **Sandbox Lab** exposes real combat and authored waves in a noncompetitive test environment.

Competitive directives use the selected difficulty's opening economy without compensating credits. They never change tower damage or utility over time.

## Economy

- Credits are shared in co-op.
- Enemy rewards and wave rewards fund towers, upgrades, Apex promotions, tactical devices, and emergency responses.
- Selling normally returns 60% of invested tower/Forge cost. Entrenched disables selling.
- Direct Pulse Plate purchases start at 60 credits and rise by 15 for each additional purchase in the same active wave. The price resets next wave.
- A Charge Forge exchanges a large permanent investment and remote build freedom for recurring stored Plates during active waves.
- Apex promotions provide a compact late-game credit sink beginning with the final campaign act.

The economy is intentionally tight during campaign openings. Rewards retain their authored value through wave 15, are halved for waves 16–24, and are quartered from wave 25 onward. The ordinary kill-bounty taper still begins after wave 10. Waves 21–30 sustain the wave-20 pressure baseline and use Apex and completed coverage as compact late investments. Endless health growth eventually outpaces a static defense.

## Tower progression

Each tower has three permanent levels:

- Level 1 establishes the base role.
- Level 2 chooses one of two doctrines.
- Level 3 chooses one of two final roles compatible with either doctrine.

This produces four complete builds per tower. Doctrines emphasize a statistical or delivery approach; final roles provide the larger mechanical identity. Upgrade previews show exact current-to-next values, including utility fields such as slow strength/duration, projectile speed, arcs, splash, Expose, Armor Break, or Beacon aura changes.

Wave 21 unlocks one authored Apex promotion for completed level-three towers. Apex preserves the selected build's identity while raising its late-game ceiling. It is a permanent upgrade, remains available in Entrenched, and is recorded in saves, co-op snapshots, history layouts, and contribution data.

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

Targeting remains a player-controlled tactical layer. Signal Gauntlet adds Support, which prioritizes its signal enemies; Sandbox also exposes it for controlled signal tests. The general modes weight route progress, health, distance, speed, or armor. A targeting choice opens a menu and does not change until the replacement is confirmed.

## Protocols and automatic activation

Every tower has a named temporary Protocol with authored duration, cooldown, effects, and an automatic trigger. Manual activation rewards timing; automation reduces repetitive input and selects only one armed tower at a time. Automatic conditions reflect the tower's role, such as enemy density, armored pressure, engaged supported towers, or elite/boss presence.

Protocol state is visually separate from tower level, player ownership, selected-tower state, placement ghosts, and enemy statuses. Entrenched removes Protocols entirely.

## Tactical devices

Pulse Plates snap to legal route positions and trigger twice. They deal fixed area damage, stun, slow, and bounded knockback. A per-enemy push grace and reduced elite/boss displacement prevent plate chains from becoming permanent route denial. The deployed field is capped at 16.

The Charge Forge produces stored Plates only while enemies are being fought. Its levels improve cadence and storage and later strengthen plate damage. This prevents downtime waiting from generating free inventory.

## Enemies and statuses

Five base enemy profiles cover light, fast, armored, shielding, and regenerating pressure, with Standard, Elite, and Boss ranks. Authored waves combine profiles and rank pressure per arena.

Signal Gauntlet adds visible signal roles to ordinary enemies:

- **Accelerator:** increases nearby enemy speed by 20% while the signal enemy remains in formation range.
- **Restorer:** repairs nearby enemies for 10% of maximum health every 5 seconds.
- **Bulwark:** grants nearby shields equal to 10% of maximum health every 5 seconds, up to a 20% shield reserve.
- **Jammer:** weakens the rate and damage of every combat tower in its pulse radius for a short duration.
- **Disruptor:** every five seconds, pauses the highest-investment tower in reach. Rank increases its single-target pause and reach; a recovery lockout prevents multiple Disruptors from repeatedly disabling the same tower at once.

Each arena owns its authored wave composition. Difficulty profiles scale that same arena roster, while Signal Gauntlet deterministically assigns signal roles: Accelerator appears on wave 2, Restorer on wave 3, Bulwark on wave 4, Jammer on wave 5, later formations alternate signal enemies, and elite or boss groups use Disruptor. The Campaign Library marks the exact affected enemies with bracketed signal codes and counts. Sandbox authored-wave replays use these assignments so formations can be inspected with unlimited resources; manual Sandbox spawning remains the controlled way to isolate any enemy, rank, health scale, or signal role.

Shield is a separate temporary durability pool above health. Ordinary hits remove raw shield before armor mitigation is evaluated; shield-bypassing attacks skip that pool and damage health through the normal armor calculation.

Signal enemies render above ordinary enemies, use the same in-body glyphs in combat and the Tactical Library, and show their support relationship through aura/recipient feedback.

Core statuses are Slow, Stun, Expose, and Armor Break. Their glyphs are distinct and can coexist. Damage resolution applies armor, shields, pierce, rank modifiers, Expose, Armor Break, burn, splash caps, and source attribution deterministically.

## Campaign and Endless

Waves 1–30 form the campaign on every difficulty. Waves 21–30 are the final arena-specific escalation, unlock Apex investment, and build toward a demanding wave-30 capstone. Generated Endless begins at wave 31 and rotates balanced, runner, armored, regenerator, and boss themes.

Endless health follows accelerating growth, while count, speed, cadence, and spawn delay use performance-minded caps/floors. The goal is escalating durability and composition pressure without allowing unit count alone to overwhelm rendering or networking.

## Co-op design

Co-op is the same shared defense game, not a separate power mode. Credits, lives, towers, tactical systems, waves, speed, and results are shared. Both players may manage every defense; original placement ownership is informational only.

The host sequences authoritative commands. Both peers run the deterministic simulation locally for responsive rendering and repair divergence from host snapshots. Both players must ready a wave. Disconnect pauses and preserves the host session, and a reconnect restores complete state. A co-op checkpoint can also be continued alone.

## Persistence and metagame

The game maintains one rolling autosave and expandable manual slots. Manual slots can be duplicated or deleted. Run History is independent from saves and stores a single evolving record when a completed campaign continues into Endless.

The Tactical Library is a complete planning reference. Exact towers, branches, enemies, waves, profiles, directives, and system rules are available before a run so difficulty comes from execution and strategic commitment rather than concealed counters. Medals reward notable single-run constraints and outcomes; achievements combine progression, repeated accomplishments, profile clears, and long-term records so the player retains visible goals after learning the campaign.

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
