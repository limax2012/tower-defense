# Current Balance Assessment

## Status

The difficulty ladder separates onboarding, the authored baseline, demanding campaign play, and the complete expert campaign:

| Difficulty | Enemy health | Enemy speed | Opening credits | Lives | Required authored waves |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 90% | 98% | 112.5% | 24 | 20 |
| Medium | 100% | 100% | 100% | 20 | 20 |
| Hard | 112% | 102% | 100% | 18 | 20 |
| Bastion | 112% | 102% | 100% | 16 | 30 |

Bastion is no longer Hard with two fewer lives. It incorporates the ten authored Mastery waves into the required campaign and unlocks Apex at wave 21. The additional waves test reinvestment, branch completion, coverage saturation, and the wave-30 boss after the opening has been solved.

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

The current validated sweep used eight seeds for every arena/directive combination, or 128 runs per difficulty:

| Difficulty | Wins | Runs | Completion | Average wave | Average lives |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 128 | 128 | 100.0% | 20.0 | 23.0 |
| Medium | 124 | 128 | 96.9% | 19.8 | 17.7 |
| Hard | 95 | 128 | 74.2% | 17.8 | 10.2 |
| Bastion | 16 | 128 | 12.5% | 23.1 | 1.4 |

These bands mean the ladder is ordered correctly for an informed automated player. Easy and Medium are forgiving after strategy is understood, Hard still allows many successful plans, and Bastion rejects most imperfect 30-wave executions. The Bastion percentage must not be described as a literal human 12.5% win rate: a person can adapt across attempts, interpret spatial patterns, and deliberately reproduce a successful layout in ways the agent cannot.

## Arena findings

The same sweep exposes a major limitation in the Experienced policy at Bastion:

| Arena | Hard clears | Bastion clears |
| --- | ---: | ---: |
| Foundry Loop | 29/32 | 1/32 |
| Crosswind Basin | 21/32 | 0/32 |
| Prism Circuit | 22/32 | 15/32 |
| Surge Divide | 23/32 | 0/32 |

Prism is overrepresented and Surge is underrepresented in bot clears. Human evidence is materially better on Surge: informed players discovered the intended plan—early node use, Fastest Frost control, Arc group damage, and timely armor/shield counters—and cleared it after limited iteration. That does not prove Surge is easy; it proves the bot's zero is a lower-bound artifact rather than evidence of impossibility.

- **Foundry Loop:** baseline route and economy. It supports the widest range of mixtures.
- **Crosswind Basin:** precise early coverage remains part of the arena identity. Cheap towers need deliberately chosen corners and adjacent-lane reach.
- **Prism Circuit:** concentrated geometry is unusually compatible with deterministic placement scoring and compact support.
- **Surge Divide:** low opening credits are balanced around node use. The agent now prioritizes nodes and can fit four standard towers into large unobstructed nodes, but it still lacks human multi-wave cluster planning.

No map geometry change is justified by these aggregate percentages alone.

## Directive findings

Hard results from the same sample were Standard 27/32, Signal Gauntlet 22/32, Core Six 27/32, and Entrenched 19/32. Bastion produced 4/32 clears in each directive. These results use Core Six's current standard opening economy.

- **Standard** is the complete strategic baseline.
- **Signal Gauntlet** changes priority and defensive reliability through carriers and disruption. The Experienced agent uses Support targeting on appropriate towers, unlike older simulation policies.
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

Kill bounties retain full value through wave 10, then taper smoothly to about 80% at wave 20, 67% at wave 30, and 50% at wave 50, with a 40% deep-Endless floor. Wave-clear and early-call rewards are unchanged. Tower prices and combat statistics do not inflate with wave number.

The bounty curve restrains reserve snowball without changing the opening. Bastion's required waves 21–30 create a natural use for mature reserves through Apex and completed coverage. Generated Endless begins after wave 30.

## Interpretation limits

1. The agent chooses among strong local placements; it does not solve the whole map globally.
2. It follows authored counter milestones but does not learn from prior failed seeds.
3. It cannot value a complex sell-and-rebuild sequence as reliably as a human.
4. Automatic Protocol and Plate timing are approximations rather than frame-perfect play.
5. A fixed seed changes placement tie-breaking, not the policy's strategic model.
6. Aggregate completion can conceal a bad layout assumption on one map.

Balance decisions should use matched simulations, recorded layouts, and human runs together. The current data supports the new difficulty ordering and 30-wave Bastion structure. Further universal tower or map changes should target a demonstrated mechanism rather than trying to force every arena to the same bot percentage.

## Human validation priorities

1. Bastion wave-21 entry reserves and first Apex purchase order.
2. Wave-by-wave failure distribution from 21 through 30 on each arena.
3. Surge clears with and without deliberate four-tower node clusters.
4. Signal Gauntlet with Support targeting versus ordinary targeting.
5. Core Six branch diversity now that its opening bonus is removed.
6. Entrenched recovery after one imperfect early placement.
7. Experienced players repeating a solved plan across multiple seeds and arenas.
