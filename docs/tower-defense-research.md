# Tower-Defense Design Principles

This reference records the durable design reasoning used to evaluate Minimal Bastion. Exact values and implemented rules belong in the current JSON/content and canonical design documents.

## Coverage is a resource

Tower range is valuable only where it overlaps useful route length. Corners, switchbacks, and parallel lanes create higher-value placements than equal-area empty terrain. Map balance must therefore consider route geometry, build-region footprint, tower radius, and obstruction rules together.

Long range converts otherwise idle space into damage, but it should pay through cost, cadence, targeting constraints, or specialized output. Short-range towers need density scaling, area effects, multi-shot delivery, or stronger efficiency so nearby placements remain desirable.

## Economy should create timing decisions

Healthy tower-defense economies repeatedly ask whether to:

- buy immediate coverage
- complete an upgrade path
- invest in support or production
- retain emergency liquidity
- replace an inefficient footprint
- spend on a concentrated late-game promotion

Prices should not inflate merely because the wave number increased. Consistent mechanics make planning learnable. Wave pressure, opportunity cost, placement saturation, authored counters, and optional sinks are better tools than arbitrary time-based stat/price changes.

## Branches need different success cases

An upgrade branch is meaningful when the preferred choice changes with geometry or threats. Purely better/worse numerical branches are false choices. Useful contrasts include:

- cadence versus impact
- concentrated versus distributed coverage
- armor/shield pressure versus light-group clearing
- immediate damage versus control/support
- local strength versus wider field reach

Upgrade previews should expose the exact affected values. Strategic strengths/limits belong in the library, while live Tower Intel should prioritize current facts and concise deltas.

## Support needs attributable value

Kills and raw damage underrate slow, stun, Expose, Armor Break, aura support, and target disruption. Analytics should record recipient time, damage-equivalent contribution, control seconds, affected targets, and source attribution. Support stacking rules must be explicit and bounded.

Enemy support roles also need legible relationships. Their body glyph, aura, and affected-recipient feedback should agree with the Tactical Library so players can learn them during play.

## Manual tactics versus automation

Active abilities keep players involved but become repetitive in dense late play. Automation is valuable when:

- triggers are deterministic and readable
- manual timing remains a useful option
- automation does not secretly change the ability
- the UI clearly identifies the armed tower
- co-op produces one shared authoritative activation

Temporary tactical systems should create decisions rather than act as mandatory maintenance chores.

## Difficulty should change pressure, not rules knowledge

Difficulty profiles should preserve tower mechanics. Health, speed, lives, and opening economy provide a readable ladder. Directives can instead change the ruleset in thematic, decision-changing ways: roster restriction, permanent commitment, or additional enemy behaviors.

A mode that removes one rarely used button may not create a distinct experience. A good directive changes openings, placement risk, target priority, or recovery options throughout the run.

## Authored and generated progression serve different jobs

Authored waves teach counters, create deliberate tests, and support arena-specific pacing. Generated waves extend a solved campaign but require bounded unit density and clear scaling. A strong structure is:

- campaign: teach and test the complete baseline
- final campaign act: authored reinvestment test for an established defense
- Endless: rotating generated pressure and long-term optimization

The final authored wave should be a capstone, but the preceding sequence should ramp smoothly enough that failure teaches something.

## Readability is gameplay

Dense tower defense needs a visual priority hierarchy:

1. route and enemy movement
2. placement legality and range
3. target status/counter information
4. tower identity, level, and active state
5. co-op intent
6. cosmetic effects

Shape, line pattern, position, motion, and opacity should distinguish these systems before color alone. Similar cues must not compete: remote placement, remote selection, Auto, Slow, node effects, and range outlines each need separate visual grammar.

## Persistence supports experimentation

One rolling autosave protects ordinary progress. Independent manual slots let players preserve deep runs and test alternatives. Run history should remain separate from loadable state, update one continuing run identity, and retain final layouts/statistics for analysis.

Discovery gating gives exploration value to the Tactical Library. It should conceal future details without hiding information the player currently needs to understand an active mechanic.

## Balance-agent evidence

Automated agents are best for matched comparisons, broad permutation coverage, deterministic reproduction, and regression detection. They are weakest at discovering novel strategies, inferring visual cues, and long-horizon reorganization.

Trust increases when agents:

- use real geometry and placement validation
- value nodes and route coverage
- respond to wave composition
- complete upgrade paths
- use relevant targeting/tactical systems
- expose final layouts and per-policy results

Human play remains necessary for fairness, clarity, fun, learning time, and whether a strong strategy is discoverable rather than merely possible.
