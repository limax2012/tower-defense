# Surge Divide / Bastion / Signal Gauntlet human reference

This reference captures the successful human defense immediately after wave 29. The checkpoint uses the game save schema unchanged and can be restored by the normal checkpoint loader.

## Provenance

- Checkpoint: `surge-bastion-signal-human-wave29.checkpoint.json`
- Checkpoint SHA-256: `F97F477A3A0ED8DCA04CD0115816AA890444EC661CE0C619F35A03260F5CCBA0`
- Winning wave-30 autosave SHA-256: `EB86CFF2AB9041970B90206B0796990AFCA781FBC1769752BD37A929BEA759BB`
- Content commit: `bc382b805e98fc96440b9c217b8393702810c92b`
- Repository tree: `76e5845710f7fa0981922489f79696103af38879`
- Run: `ddbe52aaa5c344059e09c12291d69bc5`

The checkpoint intentionally retains its original run statistics. Its economy records 2,241 kills while the statistics ledger records 2,414 because the ledger includes a prior 173-enemy wave-28 attempt. Evaluate continuations with metric deltas from the checkpoint rather than treating the cumulative statistics as a single pass.

## Checkpoint anchors

| Cleared wave | Credits | Active towers | T3 / Apex | Active investment | Plates deployed / bought | Plate spend | Nodes occupied / powered towers | Combat targeting |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 10 | 333 | 13 | 10 / 0 | 3,485 | 3 / 2 | 120 | 5 / 8 | First 8, Armored 1, Support 4 |
| 13 | 63 | 17 | 16 / 0 | 5,850 | 17 / 16 | 1,380 | 5 / 8 | First 11, Fastest 1, Armored 1, Support 3 |
| 19 | 354 | 26 | 25 / 0 | 11,895 | 31 / 30 | 2,415 | 8 / 15 | First 13, Strongest 3, Fastest 3, Armored 2, Support 3 |
| 27 | 338 | 36 | 35 / 0 | 19,620 | 48 / 47 | 3,855 | 9 / 23 | First 14, Strongest 4, Fastest 6, Armored 4, Support 6 |
| 29 | 207 | 40 | 39 / 0 | 20,935 | 52 / 51 | 4,140 | 9 / 23 | First 20, Fastest 8, Support 10 |
| 30 | 112 | 40 | 39 / 1 | 21,200 | 58 / 57 | 4,725 | 9 / 23 | First 30, Fastest 6, Support 2 |

Plate spend is total economy spend less the gross cost of tower purchases and upgrades, including sold towers. The defense never purchased a Charge Forge.

## Build sequence

- Waves 1-10 establish nine T3 Needles, a T3 Ember, an L2 Shard, an L1 Breaker, and an L1 Prism. Five node fields are occupied. Support targeting is already used on two Needles, the Ember, and the Prism.
- Waves 11-13 finish the Shard and Breaker, raise the Prism to L2 Aperture, add the west T3 Amplifier/Tempo Beacon, two terminal T3 Needles, and the first T3 Deep Chill/Permafrost Frost. The new Frost stays on Fastest.
- Waves 14-19 finish the first Prism and add two T3 Frosts, a T3 Survey/Quake Mortar, a T3 Repeater/Shatter Breaker, a T3 Kindling/Wildfire Ember, a T3 Aperture/Core Lance Prism, a T3 Scatter/Lance Shard, an L1 Prism, and an east T3 Repeater/Tempo Beacon. Eight of nine nodes are occupied, and the two Beacon auras cover 21 of 24 combat towers.
- Waves 20-27 finish the L1 Prism as Frequency/Spectrum Split, sell the east Tempo Beacon for 435, and replace it with Beacon 30 at `(523.7553, 439.99)`, using Repeater/Horizon. Add Frost 27, Mortars 28/31/35, Breakers 29/36, Prisms 32/34, Frosts 33/37, and retarget Prisms 13 and 23. All nine nodes become occupied. Beacon 30 is the automatic Protocol tower.
- Current-content waves 28-29 sell L1 Mortar 35 at `(312.82214, 616.0939)` for 216. Add T3 Repeater/Shatter Breaker 38 at `(548.40393, 394.40683)`, T3 Cycler/Rail Needle 39 at `(514.7994, 371.3428)`, T3 Deep Chill/Permafrost Frost 40 at `(588.936, 314.25888)`, T2 Deep Chill Frost 41 at `(669.99, 610)`, and T3 Cycler/Rail Needle 42 at `(720.6685, 358.13657)`. Retarget Breakers 8/29/36 from Armored to Support, Breaker 20 from Armored to First, Prisms 13/25/32 from Strongest to Support, Ember 21 from Strongest to First, and Prisms 23/34 from Support to First. Buy three Plates on wave 28 and one on wave 29. The Beacon Protocol count increases by 11 while Beacon 30 remains the automatic Protocol tower. Tower actions within the two-wave window are ordered by ID, but their exact wave and in-wave timing are not saved.
- Wave 30 upgrades Needle 39 to Apex for 400, sells Frost 41 for 135, and adds L1 Needle 43 at `(498.04547, 408.21338)` for 90. Buy six Plates for `60 + 75 + 90 + 105 + 120 + 135 = 585`. Retarget Breaker 8, Needle 11, Ember 12, Prisms 13/25/32, and Breakers 29/36 from Support to First, and Frost 22 from Fastest to First. Keep Needle 2 and Mortar 31 on Support; keep Frosts 17/18/27/33/37/40 on Fastest. Beacon statistics add seven Protocol activations; Needle statistics add five, consistent with the only Apex tower's autonomous Protocol behavior.

## Placement objectives

- The two off-node Beacons form overlapping clusters. Beacon 14 has a 158.4-radius Amplifier/Tempo aura; Beacon 30 has a 249.4-radius Repeater/Horizon aura. At wave 29 they cover 35 of 38 combat towers and 18,515 of 19,475 combat credits. Their six-tower overlap receives the strongest attack-rate and range bonuses independently.
- The final defense places 23 towers and 15,115 credits on Surge Nodes while keeping all nine nodes occupied. Late off-node additions remain inside Beacon 30 rather than chasing node count after the network is complete.
- Powered roles are deliberate: the inner Amplifier carries two Breakers and two Prisms; the east Accelerator carries two Prisms, a Frost, and a Breaker; the south Scope and Breach fields carry the three retained Mortars.
- Outside both Beacon auras, Needle 2 and Frost 27 cover the opening Accelerator, while Shard 24 uses the north Amplifier. These are edge-coverage specialists, not isolated placement mistakes.
- With Protocols inactive and current static node/Beacon range bonuses applied, 2-unit samples along the 2,114-unit route give 99.91% route coverage, 15.29 combat towers in range on average, and at least ten towers in range across 76.3% of the route.

## Wave-30 result metrics

- All 157 enemies die with no leak: 38 Crawlers, 36 Runners, 2 elite Runners, 43 Brutes, 20 Aegis, 2 elite Aegis, 13 Regenerators, 2 elite Regenerators, and 1 Regenerator boss.
- Towers apply 305,826.250 damage; six Plates add 1,986.689 damage and one kill.
- Direct damage by tower type: Prism 76,430.190; Breaker 68,490.440; Mortar 64,834.000; Needle 43,303.200; Ember 24,686.190; Frost 19,551.850; Shard 8,530.380.
- Utility deltas: Beacon support 73,487.620; Prism Expose 31,181.220; Breaker Armor Break 13,199.860; Frost control 4,141.130 seconds.

Historical Plate positions, failed-wave enemies, queued enemies, health, shield, progress, and action timing are not present in intermission checkpoints. Search artifacts should therefore store those fields per attempt, together with the seed, content identity, checkpoint hash, and wave-local decision sequence.
