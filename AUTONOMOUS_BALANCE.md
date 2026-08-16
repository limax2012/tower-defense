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
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --strategy LongRange "--force-build=siege_mortar:mortar_loader>quake_shell"
```

CLI filters are `--strategy`, `--seed`, `--runs`, `--map`, `--difficulty`, `--challenge`, `--max-wave`, `--force-build`, `--no-protocols`, and `--output`. `--force-build` accepts `tower:doctrine>specialization` and is intended for controlled branch-viability diagnostics; `--no-protocols` creates a matched active-ability control group. A `--max-wave` above 20 explicitly enters deterministic endless mode; ordinary simulation remains the authored campaign. `--simulate-full` evaluates every map unless a map is supplied.

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

- Fast: 68 deterministic mechanics, content, transport, command, persistence, history, directive, doctrine, effect-budget, and simulation regressions.
- Medium: isolated `--balance` benchmark plus focused strategy/map batches.
- Deep: `--simulate-full` across 12 strategies, all four maps, four difficulties, and multiple seeds.
- Player-facing: self-contained native build inspection of menus, online setup, battlefield, workshop, tactical states, Surge Node hover, level marks, Protocols, forge timing, and result screens.

## Current baseline

The current five-seed matrices cover 12 strategies across Foundry Loop, Crosswind Basin, Prism Circuit, and Surge Divide: 240 deterministic runs per difficulty. Easy's only consistent failures are the intentionally under-defending Economy and indiscriminate level-1 Spam policies.

| Difficulty | Wins | Win rate | Average wave | Average lives |
| --- | ---: | ---: | ---: | ---: |
| Easy | 199/240 | 82.9% | 19.1 | 24.6 |
| Normal | 186/240 | 77.5% | 18.8 | 17.4 |
| Hard | 140/240 | 58.3% | 17.5 | 9.0 |
| Bastion | 73/240 | 30.4% | 14.9 | 4.0 |

Hard is the authored uncompromised baseline. Bastion now applies 112% enemy health, 102% speed, full base credits, and 18 lives: its refreshed map results range from 13.3% on Surge to 38.3% on Crosswind/Foundry. The former 115% / 104% / 15-life profile cleared only 13.8% overall and allowed just four purposeful policies to win; the tuned profile clears 30.4% and gives eight purposeful policies plus Randomized at least one success while Economy, Aggressive, and indiscriminate level-1 Spam still fail. Surge therefore remains the decisive expert arena without making Standard Bastion a one-build puzzle.

Current Hard strategy wins are Conservative 14/20, Economy 0/20, Aggressive 2/20, UpgradeFocused 20/20, Spam 0/20, AntiSwarm 16/20, AntiArmor 19/20, LongRange 19/20, Control 15/20, Tactical 14/20, Adaptive 15/20, and Randomized 6/20. A focused 20-seed rerun expanded UpgradeFocused to 74/80 (92.5%): it cleared Crosswind, Foundry, and Prism consistently but only 14/20 Surge cases. Deep upgrades are therefore a strong slot-efficiency plan rather than a universal solution, while AntiArmor, LongRange, AntiSwarm, Control, Adaptive, and Tactical remain credible alternatives. Report: `.build/balance/upgrade-focused-hard-20x.json`.

Current combined Easy/Medium/Hard report: `.build/balance/overnight-difficulty-5x.json`. The refreshed Bastion report is `.build/balance/bastion-tuned-standard-5x-20260816.json`; earlier tuning reports remain under `.build/balance` for comparison.

## Challenge directive baseline

The current five-seed matrices cover Standard and Fundamentals across all 12 policies and four arenas (240 runs each). The latest two-seed Close Quarters/Core Six audits remain included for directive context:

| Directive | Wins | Win rate | Average wave | Purpose |
| --- | ---: | ---: | ---: | --- |
| Standard | 140/240 | 58.3% | 17.5 | Full roster and every tactical system. |
| Close Quarters | 47/96 | 49.0% | 15.9 | Removes Watchtower/Mortar and rewards route-adjacent coverage. |
| Core Six | 28/96 | 29.2% | 15.6 | Advanced compact-roster planning puzzle. |
| Fundamentals | 115/240 | 47.9% | 16.3 | Full tower roster, but no Plates, Forge, or manual/automatic Protocols; fixed +25% opening funds. |

Fundamentals is therefore materially different from Standard without duplicating Core Six: it preserves composition freedom while removing all temporary intervention. At +25% opening funds, its Hard clear rate is 47.9%, 10.4 points below Standard; nine policies clear at least once, while indiscriminate Spam, Economy, and Aggressive remain unsuccessful. The tuned five-seed Bastion matrix clears 48/240 (20.0%), with map results of Crosswind 26.7%, Foundry 28.3%, Prism 20.0%, and Surge 5.0%. Six policies clear Bastion Fundamentals, led by Anti-Armor (13/20), Conservative and Upgrade-Focused (10/20 each), rather than one forced solution. A separate ten-seed Surge stress check confirms 6/120 (5.0%) with Anti-Swarm, Adaptive, and Upgrade-Focused wins; this remains a severe optional combination without being the former 1/120 outlier. Reports: `.build/balance/fundamentals-125-{hard,bastion}-5x-20260816.json` and `.build/balance/fundamentals-125-bastion-surge-10x-20260816.json`.

No tower or enemy stat changes with directive progress. Restrictions and opening compensation are fixed at session construction; the stable internal ID remains `no_reserves` for save compatibility.

## Endless validation

- A doctrine/range-era 144-run Hard matrix continued all 12 strategies across all four arenas toward wave 40. No defense reached the cap. Across all campaigns, average failure was wave 20.9; among the 83 runs that reached wave 20, average depth was 26.5. Control survivors averaged 34.2 and peaked at 39. Map survivor averages were Crosswind 28.2, Prism 26.3, Foundry 26.1, and Surge 24.9. Report: `.build/balance/doctrine-range-hard-endless40-3x.json`.
- A fresh wave-60 audit on the current executable ran all 12 policies once across all four maps on Easy, Normal, and Hard. None of 144 runs reached wave 60: Easy peaked at 43 (14 reached 30), Normal at 39 (7 reached 30), and Hard at 39 (5 reached 30). Control led the survivors but still failed on every map, confirming quadratic health escalation eventually breaks stabilized range/control economies rather than only increasing roster count. Reports: `.build/balance/overnight-endless60-{easy,normal,hard}-1x.json`.
- A later three-seed Easy stress run aimed the Adaptive policy at wave 100 on every arena. All 12 defenses failed naturally at waves 24-29 (27.0 average), with Surge earliest at 24.3; generation and reporting stayed bounded. Report: `.build/balance/endless100-adaptive-easy-3x.json`.

- Five Foundry Adaptive seeds targeted wave 30. One failed the authored campaign at wave 14; the four campaign-clearing runs reached waves 23, 24, 24, and 28 rather than encountering an artificial wave-21 wall.
- A 12-strategy Surge Divide pass produced three wave-30 survivors: AntiSwarm and Control with 10 lives, and Tactical with 12. Adaptive reached 28 and LongRange reached 25.
- Extending the three wave-30 survivors toward wave 40 defeated all three on wave 33. This confirms the curve continues rising after max-level defenses are established.
- Reports: `.build/balance/endless-wave30-adaptive-5x-20260814.json`, `.build/balance/endless-wave30-surge-strategies-20260814.json`, and `.build/balance/endless-wave40-*-20260814.json`.

## Current observations

- The doctrine matrix originally preserved the overall Hard baseline almost exactly (143/240 versus 144/240), but LongRange became seed-perfect. A surgical Watchtower pass trims Impact Optics reach by roughly 5%, Deadeye reach from 345 to 335, and Deadeye damage from 118 to 112. The resulting 140/240 matrix moves LongRange to 18/20 without changing global enemy stats.
- Crosswind remains the most forgiving doctrine-era arena at 40/60, while Surge remains hardest at 27/60 despite its nodes. Each map moved by no more than one clear from the pre-adjustment matrix.
- Conservative and LongRange reach 18/20; AntiArmor, UpgradeFocused, Control, and Adaptive reach 17/20. Multiple mixed approaches are therefore credible alternatives rather than one policy being seed-perfect.
- Economy reaches wave 15.8 on average on Hard without winning; its delayed Forge investment remains meaningful but risky. Spam also remains intentionally nonviable.
- Tactical wins 13/20 while deploying 544 plates across the Hard matrix. The 16-field cap, active-wave escalating direct cost, knockback grace, and boss resistance prevent the former endless plate lock while preserving a viable hands-on strategy.
- Mortar's deterministic shell caps reduce Hard aggregate damage/credit to 14.2 and keep it below Watchtower, Breaker, Needle, Shard, Frost, and Ember rather than allowing unlimited crowded-wave scaling.
- Every tier-two doctrine and every final specialization still appears in winning Hard runs after the Watchtower pass. The least-used doctrine and final role each have 15 winning placements, so each branch retains a demonstrated success scenario.
- Completed-path telemetry covers the actual doctrine/final pairing rather than treating the two choices independently. Natural Hard planning produced 39 of 40 possible paths; its sole omission was Mortar Quick Loader into Quake Shell because the planner preferred Quake's radius doctrine. Forcing that path across the five-seed, four-map LongRange matrix won 19/20 runs and recorded 189 completed Loader/Quake mortars in wins. The branch is viable and needs no compensating stat buff; the report is `.build/balance/loader-quake-longrange-hard-5x.json`.
- The Beacon benchmark now measures indirect output. Tempo contributes 18.4 assisted DPS to a compact three-Needle cluster, while Horizon contributes 12.0 versus Tempo's 8.0 in a spread three-Watchtower formation by reaching two extra recipients.
- Campaign telemetry now records source-attributed Slow, Stun, Exposed, and Armor Break enemy-seconds plus Beacon recipient-seconds and marginal attack-rate damage-equivalent. A one-seed, 36-run Hard sweep measured 1,539,911 Beacon assist damage, 131,592 supported tower-seconds, 130,230 control enemy-seconds, 45,313 expose enemy-seconds, and 83,348 armor-break enemy-seconds without changing gameplay outcomes.
- The same attribution is now retained per deployed tower at runtime. Tower Intel can distinguish a specific Beacon's assisted damage and a specific control/expose/break source's enemy-seconds instead of showing only its direct damage and kills; saves and co-op checksums include these records.
- Damage resolution now calculates actual marginal Expose and Armor Break damage without changing combat. In the full 240-run Hard matrix, Prism Beam adds 2,302,526 Expose assist and reaches 9.7 impact/credit; Breaker adds 3,511,181 Armor Break assist and reaches 29.0. Prism remains a lower credit-efficiency purchase because it buys long-range slot coverage, while the 16/20 Anti-Armor policy remains below the 19/20 leading mixed policies, so neither result justifies a blind stat adjustment.
- A fresh isolated economy rerun preserved Ember Coil unchanged: its 220-credit level 1 remains a modest persistent setup, while either 120-credit doctrine adds compact area coverage (152-180 dense DPS in the eight-target scenario). Wildfire clears the moving rush and Searing retains the armor/boss role, matching their campaign-conditioned success rather than supporting a global buff.
- Direct damage/credit still understates pure range coverage, but support and control roles now have reproducible campaign measurements alongside scenario outcomes.
- A cross-tower benchmark now isolates pair output above the sum of both towers alone. The former binary Arc rule gave the full +35% damage for any nonzero Slow; Frost + Arc produced +20.5% total pair DPS at level 1 and Permafrost + Storm produced +28.2%. Conductive damage now matches the target's Slow strength and caps at +30%, moving those pair bonuses to +17.6% and +24.1%. This preserves the combo while preventing a token Slow from granting its full value. Burn + Needle against armor 8 remains a credible +30.5% pair interaction through Burning's -2 armor, Shatter + Needle adds 8.3%, and Prism + Needle adds 4.2% through Expose.
- Hard status incompatibilities were rejected. Slow, Burn, Expose, and Armor Break can coexist; strongest-only Slow/Expose/Armor Break, the two-source Burn cap, and strongest-only Beacon/node stats already provide soft diminishing returns without invalidating mixed defenses.
- Storm Lattice's seven-hop Fork path is not overpowered in the focused audit. It reaches 150.6 dense DPS at 0.174 DPS/credit, below several cheaper area branches and capped Mortar's 159.8-204.1 dense DPS. A 192-run all-strategy/all-arena Hard forced-path sweep produced 29/48 wins for Fork/Storm, exactly tied with Fork/Lockdown; Capacitor/Storm reached 32/48 versus Capacitor/Lockdown's 30/48. Storm buys density coverage while Lockdown retains a competitive control use. Report: `.build/balance/arc-branches-hard-1x-20260815.json`.
- A paired 240-run Hard control group disabled all Protocol activations while preserving the same maps, strategies, and seeds. Clears moved from 140/240 (58.3%) to 110/240 (45.8%), average wave from 17.4 to 15.4, and average lives from 10.1 to 6.9. LongRange and UpgradeFocused retained 18/20 and 17/20 clears without Protocols, while the disabled matrix still produced 110 wins across multiple policies. Protocols therefore provide a meaningful execution reward without being a universal gate; no Protocol stat change is warranted. Report: `.build/balance/protocol-control-hard-5x.json`.

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
- Active Protocols should matter without becoming mandatory; the paired Hard control group remains 45.8% viable without them, and optional automation exists for players who prefer lower intervention.
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
2. Collect human outcomes on the tuned 112% HP / 102% speed / 18-life Bastion profile before moving it again.
3. Add another arena only if its placement constraint creates a new strategy rather than duplicating existing route geometry.
4. Evaluate hosted relay/NAT traversal separately from combat balance; do not couple networking services to deterministic simulation.
