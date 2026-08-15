# Autonomous Balance Lab

## Purpose

Balance decisions use deterministic full-game agents rather than displayed DPS or one scripted build. The lab exposes dead choices, dominant towers, economy failures, dangerous waves, map-specific advantages, branch usage, active-ability value, and tactical-system misuse while preserving reproducible seeds.

## Commands

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --max-wave 30
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --map relay_divide --runs 10 --output .build\balance\relay-10x.json
```

CLI filters are `--strategy`, `--seed`, `--runs`, `--map`, `--max-wave`, and `--output`. A `--max-wave` above 20 explicitly enters deterministic endless mode; ordinary simulation remains the authored campaign. `--simulate-full` evaluates every map unless a map is supplied.

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

- Fast: 40 deterministic mechanics, content, transport, command, and simulation regressions.
- Medium: isolated `--balance` benchmark plus focused strategy/map batches.
- Deep: `--simulate-full` across 12 strategies, both maps, and multiple seeds.
- Player-facing: self-contained native build inspection of menus, online setup, battlefield, workshop, tactical states, Surge Zone hover, level badges, Overdrive, forge timing, and result screens.

## Current baseline

Final report: `.build/balance/final-range-branch-pass-5x.json`.

- 120 runs: 46 victories, 38.3% win rate, average wave 16.2, average remaining lives 6.1.
- Foundry Loop: 20/60 wins, average wave 15.7, average lives 5.4.
- Surge Divide (`relay_divide` internal ID): 26/60 wins, average wave 16.7, average lives 6.9.
- Strategy wins: Conservative 7/10, Economy 0/10, Aggressive 0/10, UpgradeFocused 2/10, Spam 0/10, AntiSwarm 6/10, AntiArmor 3/10, LongRange 8/10, Control 9/10, Tactical 3/10, Adaptive 6/10, Randomized 2/10.
- The matrix used 4,403 Overdrives and earned 36,460 early-call credits.
- 627 plates produced 1,217 reliable triggers, 825 kills, and 85,060 damage; 16 runs purchased a Charge Forge.

The preceding Overdrive matrix was 49/120 (40.8%). Removing the Pulse Plate's hidden 0.8-second lockout and introducing explicit crossing state raised the result to 53/120 without changing tower damage. A later usability pass changed this into a visible pushback/re-cross interaction. The anti-carpet pass now limits push to 28, gives elites/bosses rank resistance, adds a 0.75-second per-enemy knockback grace, caps the field at 16 plates, and makes direct active-wave purchases escalate from 60 by 15 credits. The current 120-run campaign report is `.build/balance/plate-anti-chain-campaign20-5x-20260814.json`: 48/120 wins (40.0%), versus 46/120 (38.3%) immediately before the pass, so opening and campaign viability were preserved.

## Endless validation

- Five Foundry Adaptive seeds targeted wave 30. One failed the authored campaign at wave 14; the four campaign-clearing runs reached waves 23, 24, 24, and 28 rather than encountering an artificial wave-21 wall.
- A 12-strategy Surge Divide pass produced three wave-30 survivors: AntiSwarm and Control with 10 lives, and Tactical with 12. Adaptive reached 28 and LongRange reached 25.
- Extending the three wave-30 survivors toward wave 40 defeated all three on wave 33. This confirms the curve continues rising after max-level defenses are established.
- Reports: `.build/balance/endless-wave30-adaptive-5x-20260814.json`, `.build/balance/endless-wave30-surge-strategies-20260814.json`, and `.build/balance/endless-wave40-*-20260814.json`.

## Current observations

- Four level-1 Needle Turrets remain the opening-wave zero-leak reference on both maps. Foundry leaves 40 credits; Surge spends all 360.
- LongRange fell from a perfect 10/10 with 19.5 average lives to 8/10 with 13.0 after Watchtower's slot efficiency was reduced. It remains useful without being the automatic answer.
- Short/medium-range alternatives now finish reliably: Control is 9/10 and AntiSwarm is 6/10. Foundry's lower-left build region was extended toward the road so it is no longer reserved for Watchtower/Mortar coverage.
- Adaptive is strong but not perfect at 6/10. Conservative remains viable at 7/10.
- Economy reaches wave 16.5 on average without winning; its delayed investment is meaningful but risky.
- Control and AntiSwarm clear both maps in multiple seeds but are not universal answers.
- Spam, Randomized, and most Tactical runs fail around waves 11-13. Weak/awkward policies remain meaningfully punished.
- Tactical won 1/10 after plate reliability improved. Its 10-run focused report is `.build/balance/tactical-wave-powered-forge-5x.json`.
- Wave-only forge production did not collapse economy play: Economy still averages wave 16.5 in `.build/balance/economy-wave-powered-forge-5x.json`.
- Tower aggregate direct damage/credit is now led by Needle 23.9, Watchtower and Breaker 22.8, Ember 21.1, Frost 17.3, Mortar 16.1, Shard 13.6, and Arc 10.3. Frost, Beacon, Prism Exposed, armor-break, slow, and support values are understated by direct ratios.
- Frost branch use is 268 Hail Lancer versus 179 Permafrost choices, replacing the previous 481-to-0 Permafrost monopoly. Hail owns direct area damage; Permafrost owns maximum slow and duration.
- Ember branch use is 127 Wildfire versus 13 Searing choices. Searing is intentionally narrower, but appeared in six runs and five of those won; it now owns long-range, armor-piercing boss pressure while Wildfire owns crowded routes.
- Needle and Breaker retained both of their already-used branches rather than receiving unnecessary redesigns.

## Reproducible cases

| Strategy | Map | Seed | Result | Observation |
| --- | --- | ---: | --- | --- |
| Control | Foundry | 25094 | Victory, 20 lives | Short/medium-range control can perfect-clear the map after the geometry pass. |
| AntiSwarm | Surge Divide | 17175 | Victory, 20 lives | Shard/Frost area pressure has a clean specialist success case. |
| AntiArmor | Foundry | 17175 | Victory, 20 lives | Breaker-focused armor counterplay can finish without old Watchtower damage. |
| LongRange | Foundry | 17175 | Defeat, wave 14 | Long range is no longer seed-proof or an automatic perfect clear. |
| LongRange | Surge Divide | 17175 | Victory, 14 lives | The archetype remains viable on favorable geometry. |
| Adaptive | Relay Divide | 25094 | Victory, 20 lives | Mixed counter-purchasing remains a viable route. |

All cases are in `.build/balance/final-range-branch-pass-5x.json`.

## Balance assumptions

- Balance toward several viable full-game strategies, not equal tower use in every wave.
- Preserve tower identity and flat-armor counterplay.
- Do not add early-wave-only damage or alter tower damage to conceal economy, placement, or policy problems.
- Permanent towers should dominate total damage; Pulse Plates should rescue mistakes or reward a forge investment.
- Forge production must require active-wave risk; waiting is never income.
- Active Overdrive should matter without becoming mandatory. Its measured increase from 35.0% to 40.8% was acceptable.
- A full strategy-matrix regression matters more than one favorable seed.

## All-tier economy pass (2026-08-14)

The reusable `--balance` benchmark now evaluates every reachable level and specialization with cumulative and marginal cost, single-target DPS, armor-8 DPS, and eight-target dense DPS. It also creates real specialization instances; the previous summary could accidentally report level-2 combat for towers whose level 3 requires a branch.

The pass preserves starting credits, enemy and wave rewards, early-call income, the 60% sell ratio, and every tower's wave-independent behavior. Price changes are deliberately concentrated on transitions whose marginal value was below a new level-1 tower without enough range, utility, or slot-efficiency compensation:

- Needle Turret: level 2 costs 45 (was 55), Rapid Array 80 (85), and Rail Pin 90 (95).
- Frost Spire: level 2 costs 85 (90), Permafrost 140 (145), and Hail Lancer 155 (150). The final branch prices are intentionally close because their control and damage uses are complementary.
- Ember Coil: level 2 costs 120 (140) and gains a tight 16-unit impact radius; Searing Brand costs 190 (215). Wildfire remains 210 because its dense-wave value already justified the price.
- Breaker Cannon: level 2 costs 150 (160). Its two final branches remain unchanged.
- Signal Beacon: level 2 costs 180 (210) and level 3 costs 250 (330), reducing the long payback period of deep support investment.
- Charge Forge: purchase costs 300 (320), level 2 costs 180 (210), and level 3 costs 250 (310). A subsequent return-on-investment pass shortened its fixed level cadence from 42/32/24 to 34/26/20 active-wave seconds. Capacity, plate damage bonuses, and wave-only generation are unchanged.
- Shard Fan, Arc Relay, Prism Beam, Pulse Plates, and all macro-economy values remain unchanged. Watchtower and Siege Mortar discounts were tested and rejected because they restored perfect Long Range results; their existing range/coverage premium remains appropriate.

Final report: `.build/balance/all-tier-economy-final2-5x-20260814.json`.

- 120 campaign runs: 53 victories (44.2%), average wave 16.6, average remaining lives 7.5. The preceding plate baseline was 48/120 (40.0%), average wave 16.1 and 6.4 lives.
- Long Range remains 8/10, while Upgrade Focused improves from 4/10 to 5/10. Control and Adaptive are 9/10, AntiSwarm 7/10, Conservative 9/10, and deliberately poor Spam/Aggressive policies remain 0/10.
- Frost choices are 258 Permafrost versus 236 Hail Lancer, replacing the former 160-to-290 Hail skew without flipping into a new monopoly.
- Ember choices are 83 Searing Brand versus 82 Wildfire, replacing the former 15-to-118 Wildfire skew.
- Forge purchases rise from 16 to 18, upgrades from 15 to 21, and generated charges from 152 to 180. Tactical remains 2/10, so improved forge payback did not become a dominant strategy.

## Next experiments

1. Add explicit status uptime/support-attributed damage to improve Frost/Beacon valuation.
2. Human-playtest branch legibility, late-wave pacing, and direct-internet latency beyond loopback integration tests.
3. Watch AntiArmor on Surge Divide; the strategy is deliberately narrow but currently less reliable there than on Foundry.
4. Add difficulty/challenge modifiers only after preserving this authored-campaign baseline.
5. Evaluate hosted relay/NAT traversal separately from combat balance; do not couple networking services to deterministic simulation.
