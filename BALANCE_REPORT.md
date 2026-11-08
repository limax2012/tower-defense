# Current Balance Assessment

## Status

The difficulty ladder applies progressively tighter numerical pressure to the same complete authored campaign:

| Difficulty | Enemy health | Enemy speed | Opening credits | Lives | Required authored waves |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 94% | 99% | 100% | 20 | 30 |
| Medium | 100% | 100% | 100% | 12 | 30 |
| Hard | 106% | 101% | 100% | 6 | 30 |
| Bastion | 112% | 102% | 100% | 1 | 30 |

All profiles require the final ten authored waves. Wave 21 unlocks Apex, and waves 21–30 test reinvestment, branch completion, coverage saturation, and the wave-30 boss after the opening has been solved. Hard combines elevated combat pressure with limited recovery from leaks, while Bastion adds the highest pressure profile and ends on any breach.

Core Six uses the standard opening economy. Its six-tower roster remains unusually effective for the deterministic agent, so a large credit bonus made its intended constraint easier rather than clearer.

## Reference calibration witnesses

The calibration fixtures provide fresh-start Bastion / Signal Gauntlet witnesses at 60 Hz on all four maps: [Helix Reactor](docs/helix-calibration.md), [Prism Nullspace](docs/prism-calibration.md), [Rainline Heights](docs/rainline-calibration.md), and [Cinderworks](docs/cinderworks-calibration.md). Each has a variant that retains every tower and survives generated Endless wave 31 with zero sales. Rainline's current continuation requires no further purchases; Cinderworks' reference continuation requires no further permanent investment. Recorded measurements belong to their documented builds. Their supplied layouts and searched tactical decisions support matched replays; human execution margins and success rates remain separate validation questions.

The reference Cinderworks witness finishes the campaign with 38 towers, three Apex upgrades, a maxed Forge and 321 credits. An 18-second preparation interval and its targeting policy retain that defense through wave 31 with 298 credits. Bulwark shielding and Restorer healing are important tactical constraints: global Support targeting does not always select the intended signal enemy. The supplied witness establishes possibility under oracle/expert execution on its recorded build; the separate [general spending search](docs/cinderworks-search.md) reached wave 28 within its recorded budget.

## Competitive possibility matrix

With retained burn sources ranked by armor-adjusted stored DPS, the unchanged reference recipes clear 59 of 64 campaigns and 59 of 64 same-prefix Endless-31 continuations. Among those campaign wins, 36 final states are identical to the reference, one finishes with more lives, and 58 finish with the same lives. Eighteen winning scripts finish in fewer ticks and five in more; those times include tactical decisions and preparation, so they do not establish a universal damage increase.

Five separate adapted recipes restore verified campaign and Endless-31 wins for every combination: Rainline/Bastion/Gauntlet changes final-wave decision cadence; Rainline/Bastion/Core Six changes final-wave targeting; Cinderworks/Easy/Core Six and Cinderworks/Hard/Core Six change one late-wave decision cadence each; Cinderworks/Bastion/Core Six changes wave-27 targeting, wave-29 cadence, and the wave-30 target schedule. Every selected campaign and continuation uses zero sales and retains all campaign towers. The [adapted catalog](tests/MaximalBastion.Tests/Fixtures/MatrixBurnSelection/catalog.json) is separate from the original controls: 64/64 with adapted play is possibility evidence, while 59/64 measures transfer of the unchanged scripts.

Burn selection preserves weaker sources' remaining durations and limits damage to two contributors. It ranks stored DPS after subtracting unpierced effective armor per tick interval; current Armor Break and burning's armor reduction affect this score. Shields and Expose affect actual damage but never source selection. Ties prefer higher stored DPS, then lower source ID. The selected pair is predictable across shield and Expose changes, but need not maximize shield damage or actual damage while Exposed. Discrete tick timing, target choices, and tactical decisions still affect outcomes; authored prices and combat values alone do not predict practical effectiveness.

Controlled identical-application probes establish local damage losses from inactive tick timing. With authored Intense Flame/Piercing Burn, Intense Flame/Wildfire and Quick Kindling/Wildfire values on an armor-shredded Boss, the reference implementation deals approximately 80.79 burn damage and retained-source selection deals 78.84. The selected Wildfire source expires after two ticks; the waiting source has less than one tick interval left when it can resume. Its retained duration therefore does not guarantee retained damage. Armor-only ranking does not change this zero-armor case. Separately, a shield/armor regression fixture resolves 19 damage with a 10-point shield and 21 with a 100-point shield while keeping the same selected pair. Selection does not forecast or allocate the shield reserve. These local measurements are not estimates of a game-wide percentage change.

Repaired wins establish possibility without proving that failures were harmless timing shifts. Wave continuations from already-diverged gameplay states cannot isolate burn effectiveness from target lifetimes, regeneration, firing cooldowns and tactical actions. Tick eligibility, expiry and human timing tolerance remain balance-validation priorities. Matched application streams and timing perturbations provide evidence that another identical deterministic matrix rerun cannot supply.

The reference solo competitive matrix records fresh legal winning witnesses for all 64 map/difficulty/directive combinations at seed 1337, including all four Bastion/Signal Gauntlet calibrations. Each continues through Endless 31 with zero sales and all campaign towers retained. These outcomes and final state signatures belong to the recorded reference build, before retained-source burn selection. Fixed recipes provide matched controls for gameplay changes. Lower difficulties may finish with leaks; these are existence proofs, not optimized safety margins. The [matrix runner](docs/strategy-solver.md#auditing-a-selected-matrix) rebuilds every case from normal resources and records comparisons with its stored baseline.

Strategy transfer requires adapting to the actual rules. Core Six's cheaper roster can leave enough money for substantially more Apex investment. Entrenched needs permanent coverage and damage that replace manual Protocols and Plates; opening order and early area damage matter on Prism and Rainline. Targeting and Plate timing can also require adjustment when difficulty changes enemy timing. The reference matrix establishes possibility on its measured build, but does not establish that Signal Gauntlet is the hardest directive for every map or measure human execution difficulty.

## Experienced-agent reference assessment

The Experienced policy is a deterministic heuristic created to model known strong human practices. It is not machine learning and its completion rate is not a forecast of human win probability. It improves on the general policies by:

- opening with compact inexpensive coverage;
- reserving for Frost, armor, shield, chain, and durable-target counters on scheduled threat windows;
- using tower-specific target modes;
- fitting compact tower groups into valuable Surge Nodes;
- scoring Signal Beacons by incremental non-overlapping recipients;
- completing important tier-two and tier-three roles before excessive duplication;
- reinvesting in Apex during waves 21–30;
- recording exact final coordinates, branches, Apex state, and node occupancy for layout review.

The archived Experienced-policy sweep used ten seeds for every arena/directive combination, or 160 runs per difficulty. It belongs to the legacy 0.05-second simulation harness and is separate from the 60 Hz recipe matrix. These reference measurements precede retained-source burn selection:

| Difficulty | Wins | Runs | Completion | Average wave | Average lives |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 21 | 160 | 13.1% | 25.4 | 2.2 |
| Medium | 10 | 160 | 6.2% | 21.0 | 0.6 |
| Hard | 4 | 160 | 2.5% | 17.5 | 0.1 |
| Bastion | 0 | 160 | 0.0% | 10.6 | 0.0 |

The measured completion bands kept the ladder strictly ordered for this deterministic policy across the complete 30-wave campaign. Easy reached the finale most consistently, while the Medium, Hard, and Bastion samples mainly measured progression. One leak ends a Bastion run, and no run in this fixed-seed sweep was flawless. These percentages describe the archived policy/build sample, not current completion rates or forecasts of human win probability: a person can adapt across attempts, interpret spatial patterns, preserve a reserve for the final act, and deliberately reproduce a successful layout in ways the agent cannot.

## Arena findings

The same archived sweep exposes a major limitation in the Experienced policy at Bastion:

| Arena | Hard clears | Bastion clears |
| --- | ---: | ---: |
| Foundry Loop | 0/40 | 0/40 |
| Crosswind Basin | 1/40 | 0/40 |
| Prism Circuit | 3/40 | 0/40 |
| Surge Divide | 0/40 | 0/40 |

Prism was overrepresented and Surge underrepresented in these bot clears. Recorded human evidence was materially better on Surge: informed players used early nodes, Fastest Frost control, Arc group damage, and timely armor/shield counters, and cleared it after limited iteration. That does not prove Surge is easy; it means a bot zero remains a lower-bound warning rather than evidence of impossibility.

- **Foundry Loop:** baseline route and economy. It supports the widest range of mixtures.
- **Crosswind Basin:** precise early coverage remains part of the arena identity. Cheap towers need deliberately chosen corners and adjacent-lane reach.
- **Prism Circuit:** concentrated geometry is unusually compatible with deterministic placement scoring and compact support.
- **Surge Divide:** low opening credits are balanced around node use. The agent now prioritizes nodes and can fit four standard towers into large unobstructed nodes, but it still lacks human multi-wave cluster planning.

No map geometry change is justified by these aggregate percentages alone.

## Directive findings

The comparative outcomes below use the archived Experienced-agent matrices at the 30-wave target. No restricted directive receives compensating opening credits.

- **Standard** is the complete strategic baseline.
- **Signal Gauntlet** changes priority and defensive reliability through signal enemies and disruption. Accelerator grants 20% speed, Restorer repairs 10% maximum health every 5 seconds, Bulwark grants a 10% shield every 5 seconds up to a 20% reserve, and Jammer suppresses every combat tower in its pulse radius. Disruptor is the precision counterpart: every 5 seconds it directly pauses one high-investment tower in reach, with modest rank-based pause and reach increases. Wave 20 uses one ordinary Restorer and one Jammer around the boss Disruptor so the milestone emphasizes its boss mechanic instead of stacking the full alternating carrier schedule. The reference 160-run cross-difficulty sample produced two Easy clears and averaged wave 13.9; the Experienced agent uses Support targeting on appropriate towers.
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

Frost's effective slow is capped at 60%, and Intel reports that effective value. Deep Chill to Permafrost still raises slow from 49.56% to 60%, duration from 2.596 to 4.012 seconds and splash radius from 37.76 to 49.56 for 140 credits. Its 650-credit Apex adds no slow strength, but adds 18% duration and splash radius, 8% range, 38% raw direct DPS and an independent automatic Protocol. Another completed Deep Chill Permafrost costs 365 credits, so the Apex needs a useful coverage, space or Protocol advantage to justify its opportunity cost. The other three Frost paths remain below the cap and gain slow strength from Apex. These are marginal-value tradeoffs; successful whole-defense witnesses do not establish an optimal price or a universally best branch.

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

Balance decisions should use matched simulations, recorded layouts, and human runs together. The reference evidence supports testing one consistent 30-wave structure across all difficulties. Universal tower or map changes should target a demonstrated mechanism rather than trying to force every arena to the same bot percentage.

## Human validation priorities

1. Human Bastion completion attempts under the one-breach rule.
2. Wave-by-wave failure distribution from 21 through 30 on each arena.
3. Surge clears with and without deliberate four-tower node clusters.
4. Signal Gauntlet with Support targeting versus ordinary targeting.
5. Core Six branch diversity now that its opening bonus is removed.
6. Entrenched recovery after one imperfect early placement.
7. Experienced players repeating a solved plan across multiple seeds and arenas.
