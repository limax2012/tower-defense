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

CLI filters are `--strategy`, `--seed`, `--runs`, `--map`, `--difficulty`, `--challenge`, `--max-wave`, and `--output`. A `--max-wave` above 20 explicitly enters deterministic endless mode; ordinary simulation remains the authored campaign. `--simulate-full` evaluates every map unless a map is supplied.

## Architecture

- `HeadlessSimulation` creates a normal `GameSession` and advances it at a fixed 0.05-second step without rendering.
- `AutoPlayer` uses the same validated placement, targeting, doctrine, final-role, Protocol, sell, wave, Pulse Plate, and Charge Forge APIs as gameplay.
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

Agents sample continuous legal positions, route coverage, Surge Nodes, support overlap, reserves, upgrades, roles, target modes, early calls, in-wave threats, plate locations, forge production, and deterministic Protocol timing. They are comparative policies, not claims of optimal play.

## Metrics

Every JSON run includes:

- result, map, strategy, seed, wave reached, elapsed time, lives, kills, and leaks;
- credits earned, spent, unspent, recovered, and earned through early calls;
- purchases, upgrades, sales, tier-two doctrines, final-role choices, invested credits, damage, kills, shield damage, armor absorption, overkill, and damage by level;
- enemy-type and elite/boss kills/leaks;
- per-wave archetype, duration, lives lost, kills, leaks, spending, and ending credits;
- Pulse Plate deployments, direct purchases, triggers, hits, kills, and damage;
- Charge Forge purchases, upgrades, and generated charges;
- Protocol activations and utility assists.

Batch summaries derive win rate, average wave/lives, map/strategy outcomes, and tower-use efficiency tables.

## Test hierarchy

- Fast: 53 deterministic mechanics, content, transport, command, persistence, history, directive, doctrine, and simulation regressions.
- Medium: isolated `--balance` benchmark plus focused strategy/map batches.
- Deep: `--simulate-full` across 12 strategies, all four maps, four difficulties, and multiple seeds.
- Player-facing: self-contained native build inspection of menus, online setup, battlefield, workshop, tactical states, Surge Node hover, level marks, Protocols, forge timing, and result screens.

## Current baseline

The current five-seed matrices cover 12 strategies across Foundry Loop, Crosswind Basin, Prism Circuit, and Surge Divide: 240 deterministic runs per difficulty. Easy's only consistent failures are the intentionally under-defending Economy and indiscriminate level-1 Spam policies.

| Difficulty | Wins | Win rate | Average wave | Average lives |
| --- | ---: | ---: | ---: | ---: |
| Easy | 200/240 | 83.3% | 19.4 | 24.8 |
| Normal | 185/240 | 77.1% | 18.9 | 17.8 |
| Hard | 143/240 | 59.6% | 17.4 | 10.4 |
| Bastion | 47/240 | 19.6% | 13.0 | 2.3 |

Hard is the authored uncompromised baseline. After the doctrine expansion, its map results are Crosswind 41/60, Foundry 39/60, Prism 35/60, and Surge 28/60. Surge is therefore materially harder despite its nine nodes. The Easy, Normal, and Bastion rows retain the previous matched profile baseline pending the next all-difficulty doctrine sweep.

Hard strategy wins are Conservative 19/20, Economy 0/20, Aggressive 0/20, UpgradeFocused 18/20, Spam 0/20, AntiSwarm 12/20, AntiArmor 15/20, LongRange 20/20, Control 17/20, Tactical 16/20, Adaptive 17/20, and Randomized 9/20. The global win rate stayed stable, but Long Range has returned as the only seed-perfect policy and is the primary target of the next balance pass.

Canonical reports: `.build/balance/four-map-easy-5x.json`, `.build/balance/four-map-normal-5x.json`, `.build/balance/doctrines-final-hard-5x.json`, and `.build/balance/four-map-bastion-5x.json`.

## Challenge directive baseline

Hard, three seeds per strategy across all four arenas (144 runs per directive):

| Directive | Wins | Win rate | Average wave | Purpose |
| --- | ---: | ---: | ---: | --- |
| Close Quarters | 77/144 | 53.5% | 16.3 | Removes Watchtower/Mortar and rewards route-adjacent coverage. |
| Core Six | 50/144 | 34.7% | 15.6 | Advanced compact-roster planning puzzle. |
| No Reserves | 88/144 | 61.1% | 17.9 | Tower-only defense; fixed +10% opening funds replace tactical spending. |

Reports: `.build/balance/four-map-hard-close_quarters-3x.json`, `.build/balance/four-map-hard-core_six-3x.json`, and `.build/balance/four-map-hard-no_reserves-3x.json`. Surge Divide remained the hardest arena in every directive. No tower, enemy, or wave stat changes with directive progress; restrictions and opening compensation are fixed at session construction.

## Endless validation

- A 144-run Hard matrix continued all 12 strategies across all four arenas toward wave 40. No defense reached the cap: average failure was wave 22.2, Control lasted longest at 30.8 on average and peaked at 38, and Tactical failed at 26.8 on average despite individual runs deploying up to 96 Plates. Report: `.build/balance/four-map-hard-endless40-3x.json`.

- Five Foundry Adaptive seeds targeted wave 30. One failed the authored campaign at wave 14; the four campaign-clearing runs reached waves 23, 24, 24, and 28 rather than encountering an artificial wave-21 wall.
- A 12-strategy Surge Divide pass produced three wave-30 survivors: AntiSwarm and Control with 10 lives, and Tactical with 12. Adaptive reached 28 and LongRange reached 25.
- Extending the three wave-30 survivors toward wave 40 defeated all three on wave 33. This confirms the curve continues rising after max-level defenses are established.
- Reports: `.build/balance/endless-wave30-adaptive-5x-20260814.json`, `.build/balance/endless-wave30-surge-strategies-20260814.json`, and `.build/balance/endless-wave40-*-20260814.json`.

## Current observations

- The doctrine matrix preserves the overall Hard baseline almost exactly (143/240 versus 144/240), but its policy distribution shifted: LongRange is 20/20 while AntiSwarm is 12/20. This is a real follow-up target rather than a reason to hide the result with a global health change.
- Crosswind remains the most forgiving doctrine-era arena at 41/60, while Surge remains hardest at 28/60 despite its nodes.
- Conservative is stable at 19/20; UpgradeFocused reaches 18/20; Control and Adaptive reach 17/20; Tactical reaches 16/20. Multiple mixed approaches remain successful even though LongRange currently leads.
- Economy reaches wave 15.5 on average on Hard without winning; its delayed Forge investment remains meaningful but risky. Spam also remains intentionally nonviable.
- Tactical wins 19/20 while deploying 1,312 plates across the matrix. The 16-field cap, active-wave escalating direct cost, knockback grace, and boss resistance prevent the former endless plate lock despite making the system useful.
- Mortar's deterministic shell caps reduce Hard aggregate damage/credit to 14.2 and keep it below Watchtower, Breaker, Needle, Shard, Frost, and Ember rather than allowing unlimited crowded-wave scaling.
- Every tier-two doctrine and every final specialization appears in winning Hard runs. Rare High Frequency has 16 winning placements and Quake Shell 19; Ice Needle has 261 winning placements paired across both Frost finals. Selection frequency alone is not treated as branch failure, but each branch now has a demonstrated success scenario.
- The Beacon benchmark now measures indirect output. Tempo contributes 18.4 assisted DPS to a compact three-Needle cluster, while Horizon contributes 12.0 versus Tempo's 8.0 in a spread three-Watchtower formation by reaching two extra recipients.
- Campaign telemetry now records source-attributed Slow, Stun, Exposed, and Armor Break enemy-seconds plus Beacon recipient-seconds and marginal attack-rate damage-equivalent. A one-seed, 36-run Hard sweep measured 1,539,911 Beacon assist damage, 131,592 supported tower-seconds, 130,230 control enemy-seconds, 45,313 expose enemy-seconds, and 83,348 armor-break enemy-seconds without changing gameplay outcomes.
- The same attribution is now retained per deployed tower at runtime. Tower Intel can distinguish a specific Beacon's assisted damage and a specific control/expose/break source's enemy-seconds instead of showing only its direct damage and kills; saves and co-op checksums include these records.
- Damage resolution now calculates actual marginal Expose and Armor Break damage without changing combat. In the full 240-run Hard matrix, Prism Beam adds 2,302,526 Expose assist and reaches 9.7 impact/credit; Breaker adds 3,511,181 Armor Break assist and reaches 29.0. Prism remains a lower credit-efficiency purchase because it buys long-range slot coverage, while the 16/20 Anti-Armor policy remains below the 19/20 leading mixed policies, so neither result justifies a blind stat adjustment.
- Direct damage/credit still understates pure range coverage, but support and control roles now have reproducible campaign measurements alongside scenario outcomes.

## Reproducible cases

| Strategy | Map | Seed | Result | Observation |
| --- | --- | ---: | --- | --- |
| Control | Foundry, Hard | 25094 | Victory, 20 lives | Short/medium-range control can perfect-clear the authored baseline. |
| AntiSwarm | Surge, Hard | 17175 | Victory, 14 lives | Shard/Frost area pressure has a specialist success case on the hardest map. |
| AntiArmor | Surge, Hard | 25094 | Victory, 20 lives | Breaker-focused armor counterplay can perfect-clear favorable node placement. |
| LongRange | Surge, Hard | 1337 | Defeat, wave 20 | Long range reaches the finale but is not seed-proof. |
| Tactical | Prism, Hard | 25094 | Victory, 1 life | Plates can rescue a marginal defense without guaranteeing a comfortable clear. |
| Adaptive | Surge, Bastion | 9256 | Victory, 12 lives | A mixed policy can clear the hardest map/difficulty combination. |

Hard cases are in `.build/balance/overnight-hard-5x.json`; the Bastion case is in `.build/balance/overnight-bastion-5x.json`.

## Balance assumptions

- Balance toward several viable full-game strategies, not equal tower use in every wave.
- Preserve tower identity and flat-armor counterplay.
- Do not add early-wave-only damage or alter tower damage to conceal economy, placement, or policy problems.
- Permanent towers should dominate total damage; Pulse Plates should rescue mistakes or reward a forge investment.
- Forge production must require active-wave risk; waiting is never income.
- Active Protocols should matter without becoming mandatory; optional automation exists for players who prefer lower intervention.
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

1. Human-playtest branch legibility, late-wave pacing, and direct-internet latency beyond loopback integration tests.
2. Collect human Normal/Hard/Bastion outcomes before moving any global profile multiplier.
3. Add another arena only if its placement constraint creates a new strategy rather than duplicating existing route geometry.
4. Evaluate hosted relay/NAT traversal separately from combat balance; do not couple networking services to deterministic simulation.
