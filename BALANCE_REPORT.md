# Current Balance Assessment

## Status

The current campaign balance has a coherent difficulty ladder and meaningful arena spread in deterministic testing. Easy and Medium provide learning room, Hard is demanding without being a niche-only profile, and Bastion requires substantially better openings and reinvestment. Surge Divide is consistently the hardest arena but is not an aggregate outlier large enough to justify global tower changes on bot data alone.

Signal Gauntlet is a strong challenge modifier. Its current Bastion completion is extremely low for heuristic agents, partly because those agents do not explicitly use the Support targeting mode and cannot react to carrier behavior as well as a human. Its rate should be treated as a lower-bound comparative signal, not proof that the mode is nearly impossible.

Mastery succeeds at making wave 20 a campaign milestone rather than a solved final defense. Reaching wave 30 on Bastion is rare in current agent samples. Apex provides a concentrated reinvestment option; authored waves 21–30 avoid an abrupt jump directly from campaign to generated scaling.

## Measured campaign ladder

The latest 4,160-run Standard matrix reports:

| Difficulty | Completion |
| --- | ---: |
| Easy | 71.0% |
| Medium | 58.2% |
| Hard | 39.5% |
| Bastion | 16.6% |

The latest Signal Gauntlet matrix reports 71.2%, 50.8%, 27.3%, and 7.7% at the same four difficulty levels. See [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md) for sample sizes, map splits, focused controls, and interpretation limits.

## Arena assessment

- **Foundry Loop:** baseline geometry and economy. It supports broad mixtures and is the best reference for tower-level comparisons.
- **Crosswind Basin:** early coverage is deliberately exacting because lanes sit near the edge of cheap tower range. Current geometry is beatable at an acceptable agent rate and should remain a human route-reading test rather than being widened merely to simplify the first placement.
- **Prism Circuit:** small placement count and three nodes reward concentrated support and role completion.
- **Surge Divide:** low opening credits and stronger pressure are compensated by nine nodes. Winning strategies should use those nodes early; ignoring them is a strategic error rather than an alternate equivalent opening.

## Tower ecosystem

The roster currently supports several viable cores:

- Needle/Shard/Ember provide economical local damage and early coverage.
- Frost/Arc converts grouped lanes into control and chain efficiency.
- Breaker/Prism addresses armor, shields, durable elites, and damage amplification.
- Watchtower/Mortar converts distant or awkward build space into useful coverage.
- Signal Beacon rewards dense, deliberately planned clusters.

Needle Turrets remain useful late when upgraded into efficient ricochet/pierce coverage, but replacing low-impact positions with Breaker or Prism can be correct when armor/shields dominate. Long-range towers solve remote geometry; their higher cost and specialized output should not make nearby short-range positions obsolete.

Mortar damage is bounded by per-impact target caps and predictive delivery. This prevents packed Endless waves from turning radius into unlimited damage. Breaker final roles split focused heavy pressure from broader Armor Break. Ember and Frost area effects allow short range to scale with density. Beacon auras use strongest-value rules so overlapping support does not multiply without limit.

## Economy and late progression

Campaign openings remain the tightest economic phase. By wave 20, a strong defense can hold a large reserve; this is intentional preparation room for Mastery rather than a reason to inflate all prices by wave. Tower and upgrade prices remain mechanically consistent.

Mastery adds authored pressure and Apex spending. Endless health accelerates while count, cadence, speed, and delay are capped for performance/readability. Pulse Plate direct-buy escalation and a 16-plate field cap permit emergency delaying without creating a permanent infinite-control strategy.

## Directives

- **Standard:** complete strategic baseline.
- **Signal Gauntlet:** changes target priority and defensive reliability through visible carriers and tower disruption.
- **Core Six:** a thematic roster puzzle that removes specialized long-range/chain/beam options while compensating the opening.
- **Entrenched:** a commitment mode. No selling means geometry mistakes persist, while no Plates/Forge/Protocols removes temporary rescue tools.

Core Six's 30% opening-credit modifier is deliberately larger because four expensive/specialized coverage options are unavailable. It should be reviewed from full-run completion and late composition, not opening ease alone.

## Remaining validation priorities

1. Human Bastion clears by arena/directive, including failed openings and successful layouts.
2. Signal Gauntlet with deliberate Support targeting versus ordinary priority modes.
3. Mastery wave-by-wave reserve spend, Apex purchase order, and wave-30 margin.
4. Entrenched and Core Six across multiple human tower mixes.
5. Co-op runs with normal internet latency to confirm usability without affecting deterministic outcomes.
6. Endless wave 40+ credit sinks, footprint saturation, and emergency Plate use.

No broad rebalance is justified solely by the current aggregate matrix. Future changes should be mechanism-specific, supported by matched simulation controls, and checked against human layouts before modifying universal tower values.
