# Current Balance Assessment

## Status

The difficulty ladder applies progressively tighter numerical pressure to the same complete authored campaign:

| Difficulty | Enemy health | Enemy speed | Opening credits | Lives | Required authored waves |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 90% | 98% | 112.5% | 20 | 30 |
| Medium | 100% | 100% | 100% | 12 | 30 |
| Hard | 108% | 101% | 100% | 6 | 30 |
| Bastion | 112% | 102% | 100% | 1 | 30 |

All profiles require the final ten authored waves. Wave 21 unlocks Apex, and waves 21–30 test reinvestment, branch completion, coverage saturation, and the wave-30 boss after the opening has been solved. Hard combines elevated combat pressure with limited recovery from leaks, while Bastion adds the highest pressure profile and ends on any breach.

Core Six uses the standard opening economy. Its six-tower roster remains unusually effective for the deterministic agent, so a large credit bonus made its intended constraint easier rather than clearer.

## Experienced-agent assessment

The Experienced policy is a deterministic heuristic created to model known strong human practices. It is not machine learning and its completion rate is not a forecast of human win probability. It improves on the general policies by:

- opening with compact inexpensive coverage;
- reserving for Frost, armor, shield, chain, and durable-target counters on scheduled threat windows;
- using tower-specific target modes;
- fitting compact tower groups into valuable Surge Nodes;
- scoring Signal Beacons by incremental non-overlapping recipients;
- completing important tier-two and tier-three roles before excessive duplication;
- reinvesting in Apex during waves 21–30;
- recording exact final coordinates, branches, Apex state, and node occupancy for layout review.

The current full validation sweep used ten seeds for every arena/directive combination, or 160 runs per difficulty:

| Difficulty | Wins | Runs | Completion | Average wave | Average lives |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 22 | 160 | 13.8% | 26.4 | 2.1 |
| Medium | 10 | 160 | 6.2% | 21.0 | 0.6 |
| Hard | 1 | 160 | 0.6% | 16.6 | 0.0 |
| Bastion | 0 | 160 | 0.0% | 10.6 | 0.0 |

These bands keep the ladder strictly ordered for the deterministic agent across the complete 30-wave campaign. The staged late-income reductions sharply reduce clears from wave 15 onward: Easy still reaches the finale most consistently, while the small Medium, Hard, and Bastion samples are now predominantly progression measurements rather than completion estimates. Bastion remains deliberately aspirational: one leak ends the run, and no run in this fixed-seed sweep was flawless. These percentages are not forecasts of literal human win rates: a person can adapt across attempts, interpret spatial patterns, preserve a reserve for the final act, and deliberately reproduce a successful layout in ways the agent cannot.

## Arena findings

The same sweep exposes a major limitation in the Experienced policy at Bastion:

| Arena | Hard clears | Bastion clears |
| --- | ---: | ---: |
| Foundry Loop | 0/40 | 0/40 |
| Crosswind Basin | 0/40 | 0/40 |
| Prism Circuit | 1/40 | 0/40 |
| Surge Divide | 0/40 | 0/40 |

Prism remains overrepresented and Surge underrepresented in bot clears. Human evidence is materially better on Surge: informed players discovered the intended plan—early node use, Fastest Frost control, Arc group damage, and timely armor/shield counters—and cleared it after limited iteration. That does not prove Surge is easy; it means a bot zero remains a lower-bound warning rather than evidence of impossibility.

- **Foundry Loop:** baseline route and economy. It supports the widest range of mixtures.
- **Crosswind Basin:** precise early coverage remains part of the arena identity. Cheap towers need deliberately chosen corners and adjacent-lane reach.
- **Prism Circuit:** concentrated geometry is unusually compatible with deterministic placement scoring and compact support.
- **Surge Divide:** low opening credits are balanced around node use. The agent now prioritizes nodes and can fit four standard towers into large unobstructed nodes, but it still lacks human multi-wave cluster planning.

No map geometry change is justified by these aggregate percentages alone.

## Directive findings

Directive comparisons use Experienced-agent matrices at the 30-wave target. No restricted directive receives compensating opening credits.

- **Standard** is the complete strategic baseline.
- **Signal Gauntlet** changes priority and defensive reliability through signal enemies and disruption. Accelerator grants 20% speed, Restorer repairs 10% maximum health every 5 seconds, Bulwark grants a 10% shield every 5 seconds up to a 20% reserve, and Jammer suppresses every combat tower in its pulse radius. Disruptor is the precision counterpart: every 5 seconds it directly pauses one high-investment tower, with modest rank-based pause and reach increases. The current 160-run cross-difficulty sample produced one clear and averaged wave 14.6; the Experienced agent uses Support targeting on appropriate towers.
- **Core Six** is a thematic roster puzzle, not an assertion that every restricted roster must be numerically harder than Standard. Its available towers form a strong economical progression, so it no longer receives bonus opening credits.
- **Entrenched** removes correction and temporary rescue. Its lower Hard completion is consistent with permanent placement mistakes and no Protocol/Plate fallback.

## Tower ecosystem

Several strategic cores remain viable:

- Needle, Shard, and Ember provide economical local damage.
- Frost creates firing time for the whole defense.
- Arc supplies connected-group damage and stun. It has no hidden bonus damage against slowed enemies.
- Breaker and Prism answer armor, shields, elites, and boss durability.
- Watchtower and Mortar convert distant footprints into useful coverage.
- Signal Beacon rewards compact developed clusters without stacking overlapping auras.

Needle remains efficient late in good positions, especially with Ricochet or Piercing Rail. Replacing isolated Needles with completed Breaker or Prism roles is sensible when armor and shields dominate. Mortar output remains bounded by impact target caps. Beacon value depends on distinct recipients, not Beacon count.

The detailed use case for every tower and branch is documented in [STRATEGY_GUIDE.md](STRATEGY_GUIDE.md).

## Economy and progression

Kill bounties retain full value through wave 10 and begin their standard taper afterward. Kill and completion rewards retain their authored value through wave 14, are halved for waves 15–24, and are quartered from wave 25 onward. Early-call rewards remain separate, and tower prices and combat statistics do not inflate with wave number.

The staged reductions prevent dense late compositions and boss waves from producing disproportionate windfalls while preserving the opening exactly. Wave 21 sustains wave-20 durability and the following waves continue upward instead of resetting beneath the defense that cleared the boss. Generated Endless begins after wave 30 and retains quarter rewards.

## Interpretation limits

1. The agent chooses among strong local placements; it does not solve the whole map globally.
2. It follows authored counter milestones but does not learn from prior failed seeds.
3. It cannot value a complex sell-and-rebuild sequence as reliably as a human.
4. Automatic Protocol and Plate timing are approximations rather than frame-perfect play.
5. A fixed seed changes placement tie-breaking, not the policy's strategic model.
6. Aggregate completion can conceal a bad layout assumption on one map.

Balance decisions should use matched simulations, recorded layouts, and human runs together. The current data supports one consistent 30-wave structure across all difficulties. Further universal tower or map changes should target a demonstrated mechanism rather than trying to force every arena to the same bot percentage.

## Human validation priorities

1. Human Bastion completion attempts under the one-breach rule.
2. Wave-by-wave failure distribution from 21 through 30 on each arena.
3. Surge clears with and without deliberate four-tower node clusters.
4. Signal Gauntlet with Support targeting versus ordinary targeting.
5. Core Six branch diversity now that its opening bonus is removed.
6. Entrenched recovery after one imperfect early placement.
7. Experienced players repeating a solved plan across multiple seeds and arenas.
