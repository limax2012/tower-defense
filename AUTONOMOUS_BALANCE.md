# Autonomous Balance Lab

## Purpose

Balance decisions use deterministic full-game agents rather than displayed DPS or one scripted build. The lab exposes dead choices, dominant towers, economy failures, dangerous waves, map-specific advantages, branch usage, active-ability value, and tactical-system misuse while preserving reproducible seeds.

## Commands

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --map relay_divide --runs 10 --output .build\balance\relay-10x.json
```

CLI filters are `--strategy`, `--seed`, `--runs`, `--map`, and `--output`. `--simulate-full` evaluates every map unless a map is supplied.

## Architecture

- `HeadlessSimulation` creates a normal `GameSession` and advances it at a fixed 0.05-second step without rendering.
- `AutoPlayer` uses the same validated placement, targeting, upgrade, specialization, Overdrive, sell, wave, Pulse Plate, and Charge Forge APIs as gameplay.
- Seeded randomness affects candidate tie-breaking and weighted policy choices; combat remains deterministic.
- Runs stop on victory, defeat, selected wave limit, or timeout.
- Runtime events plus `DamageResolver.DamageApplied` attribute damage, kills, spend, utility actions, and tactical outcomes.
- Reports preserve map, strategy, and seed for reproduction.

## Agent personalities

- Conservative: reserves, coverage, control, and anti-armor redundancy.
- Economy: larger reserves and a deliberate Charge Forge investment.
- Aggressive: immediate offensive spending.
- UpgradeFocused: fewer towers with deep upgrades.
- Spam: many inexpensive level-1 towers.
- AntiSwarm: Shard/Arc/Mortar and swarm-oriented branches.
- AntiArmor: Breaker/Prism/Watchtower and penetration branches.
- LongRange: route coverage through Watchtower/Mortar/Prism.
- Control: Frost/Ember/Arc/Beacon and control/burn branches.
- Tactical: forge and Pulse Plate usage alongside a compact defense.
- Adaptive: reads upcoming armor, speed, swarm, shields, durability, elites, and bosses; may sell mismatched specialists.
- Randomized: seeded weighted legal choices for unusual-strategy discovery.

Agents sample continuous legal positions, route coverage, Surge Zones, support overlap, reserves, upgrades, branches, target modes, early calls, in-wave threats, plate locations, forge production, and deterministic Overdrive timing. They are comparative policies, not claims of optimal play.

## Metrics

Every JSON run includes:

- result, map, strategy, seed, wave reached, elapsed time, lives, kills, and leaks;
- credits earned, spent, unspent, recovered, and earned through early calls;
- purchases, upgrades, sales, branch choices, invested credits, damage, kills, shield damage, armor absorption, overkill, and damage by level;
- enemy-type and elite/boss kills/leaks;
- per-wave archetype, duration, lives lost, kills, leaks, spending, and ending credits;
- Pulse Plate deployments, direct purchases, triggers, hits, kills, and damage;
- Charge Forge purchases, upgrades, and generated charges;
- Overdrive activations.

Batch summaries derive win rate, average wave/lives, map/strategy outcomes, and tower-use efficiency tables.

## Test hierarchy

- Fast: 29 deterministic mechanics, content, transport, command, and simulation regressions.
- Medium: isolated `--balance` benchmark plus focused strategy/map batches.
- Deep: `--simulate-full` across 12 strategies, both maps, and multiple seeds.
- Player-facing: self-contained native build inspection of menus, online setup, battlefield, workshop, tactical states, Surge Zone hover, level badges, Overdrive, forge timing, and result screens.

## Current baseline

Final report: `.build/balance/matrix-online-ui-surge-5x-20260814.json`.

- 120 runs: 53 victories, 44.2% win rate, average wave 16.1, average remaining lives 7.1.
- Foundry Loop: 22/60 wins, average wave 15.6, average lives 6.6.
- Surge Divide (`relay_divide` internal ID): 31/60 wins, average wave 16.6, average lives 7.7.
- Strategy wins: Conservative 5/10, Economy 0/10, Aggressive 4/10, UpgradeFocused 10/10, Spam 0/10, AntiSwarm 3/10, AntiArmor 10/10, LongRange 10/10, Control 2/10, Tactical 1/10, Adaptive 8/10, Randomized 0/10.
- The matrix used 3,995 Overdrives and earned 36,160 early-call credits.
- 552 plates produced 1,060 reliable triggers, 702 kills, and 71,950 damage; 20 runs purchased a Charge Forge.

The preceding Overdrive matrix was 49/120 (40.8%). Replacing the Pulse Plate's hidden 0.8-second lockout with per-enemy crossing memory raised the result to 53/120 without changing tower damage. The increase is concentrated in policies that actually deploy plates.

## Current observations

- Four level-1 Needle Turrets remain the opening-wave zero-leak reference on both maps. Foundry leaves 40 credits; Surge spends all 360.
- AntiArmor, LongRange, and UpgradeFocused are reliable finishing archetypes. Their different tower mixes demonstrate multiple viable routes, although LongRange remains especially well matched to current geometry.
- Adaptive is now strong but not perfect at 8/10. Conservative and Aggressive remain viable and volatile.
- Economy reaches wave 16.5 on average without winning; its delayed investment is meaningful but risky.
- Control and AntiSwarm can clear Surge Divide in selected seeds but are not universal answers.
- Spam, Randomized, and most Tactical runs fail around waves 11-13. Weak/awkward policies remain meaningfully punished.
- Tactical won 1/10 after plate reliability improved. Its 10-run focused report is `.build/balance/tactical-wave-powered-forge-5x.json`.
- Wave-only forge production did not collapse economy play: Economy still averages wave 16.5 in `.build/balance/economy-wave-powered-forge-5x.json`.
- Tower aggregate direct damage/credit in the final matrix is led by Watchtower 29.1, Needle 21.6, Mortar 19.5, Breaker 17.4, Shard 11.8, and Arc 10.4. Frost, Beacon, Prism Exposed, armor-break, slow, and support values are understated by direct ratios.
- Ember moved from near-dead content to 79 purchases and 145 upgrades after Wildfire/Searing branches; it remains situational rather than mandatory.

## Reproducible cases

| Strategy | Map | Seed | Result | Observation |
| --- | --- | ---: | --- | --- |
| Tactical | Surge Divide | 1337 | Victory, 3 lives | Forge/plate-heavy policy clears after reliable per-enemy triggers. |
| Economy | Foundry | 1337 | Defeat, wave 17 | Wave-powered forge remains useful but delayed investment cannot finish. |
| Conservative | Foundry | 17175 | Victory, 20 lives | Redundant coverage reaches a perfect clear. |
| AntiSwarm | Foundry | 1337 | Victory, 3 lives | Swarm specialist can finish but is fragile. |
| Control | Surge Divide | 17175 | Victory, 20 lives | Control/burn composition has a real map-specific success case. |
| Adaptive | Foundry | 25094 | Victory, 5 lives | Adaptive counter purchasing survives a volatile run. |
| LongRange | both | all five | Victory, 20 lives | Geometry-favored reference policy; monitor with future maps. |

All cases are in `.build/balance/matrix-online-ui-surge-5x-20260814.json`.

## Balance assumptions

- Balance toward several viable full-game strategies, not equal tower use in every wave.
- Preserve tower identity and flat-armor counterplay.
- Do not add early-wave-only damage or alter tower damage to conceal economy, placement, or policy problems.
- Permanent towers should dominate total damage; Pulse Plates should rescue mistakes or reward a forge investment.
- Forge production must require active-wave risk; waiting is never income.
- Active Overdrive should matter without becoming mandatory. Its measured increase from 35.0% to 40.8% was acceptable.
- A full strategy-matrix regression matters more than one favorable seed.

## Next experiments

1. Add at least one map that weakens pure long-range coverage before nerfing Watchtower from bot evidence alone.
2. Add explicit status uptime/support-attributed damage to improve Frost/Beacon valuation.
3. Human-playtest branch legibility, late-wave pacing, and direct-internet latency beyond loopback integration tests.
4. Add difficulty/challenge modifiers only after preserving this authored-campaign baseline.
5. Evaluate hosted relay/NAT traversal separately from combat balance; do not couple networking services to deterministic simulation.
