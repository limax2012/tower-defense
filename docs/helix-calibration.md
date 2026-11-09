# Helix / Bastion / Signal calibration

The current production rules admit a replay-verified solo campaign victory on `relay_divide / bastion / close_quarters` from the ordinary empty starting board. Gameplay, content values and player saves are unchanged.

| Certificate | Result |
| --- | --- |
| Campaign | All 30 waves cleared; zero leaks |
| Final state | 1 life, 286 credits |
| Deterministic stepping | 97,389 ticks at 60 Hz |
| Accepted commands | 759; 614 during combat |
| Final wave | 157 kills; central Frost Apex; 16 paid Plates; 8 late sales |
| Information / execution | Oracle / expert |
| Content SHA-256 | `78A9731F9481B96673FBA95DA23D6BE08CCA86B45433D506641F17EEFC7A5399` |
| Final state SHA-256 | `8A8B5AA31220F97521660840046FCFE6A9DEC7BDB980758AE0B21A14221630CF` |

The [campaign plan](../tests/MaximalBastion.Tests/Fixtures/helix-bastion-signal-plan.json) contains a goal layout and conditional policies, with no starting money, health, enemies or recorded command sequence. `LayoutCampaignRunner` buys that layout legally as income arrives and evaluates configured tactical alternatives. It reproduces the [archived witness](../tests/MaximalBastion.Tests/Fixtures/helix-bastion-signal-witness.json) from a fresh session. `HelixCalibrationTests` checks both independent replay and rebuilding the campaign with the policy runner.

The layout was supplied by the recovered reference position. This is a demonstration-guided solution, not an unseeded discovery. The production allocator, policy search, coverage checks and scenario discovery do not contain Helix coordinates or map-specific build orders; the calibration data is an explicit input. Other maps need their own geometry-derived candidates or supplied layouts.

## Reproduce

After the isolated build in [strategy-solver.md](strategy-solver.md):

```powershell
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\helix-bastion-signal-plan.json --simulations 32 --seconds 120 --beam 1 --output .build\planner\helix-calibration
& $dotnet $planner --planner-verify --trace .build\planner\helix-calibration\witness.json
```

The result directory contains the source plan, its hash, every wave's legal checkpoint, trial metadata, exact build order, winning replay and independent verification. The campaign is reconstructed by decisions and affordability checks; the runner does not read the archived action trace. The plan clears in 30 wave evaluations on the current content. Trial and wall budgets apply to the experiment; wall time is checked between bounded wave evaluations.

## Decisions that matter

- Ordered investment and restricted midgame Plate purchases leave enough money for the complete defense. The winning plan limits paid Plates to two per wave during waves 13–21, then permits more where measured continuations require them.
- Five Prisms, seven Frost towers and two Beacons form the mature layout. Sustained hits and overlapping control matter against regenerating enemy groups; raw damage rankings alone did not find the working allocation.
- Tactical policy overrides preserve the chosen Frost targeting through cleanup. The older experienced policy's wave-below-28 cleanup rule otherwise forces First targeting and masks some tested alternatives.
- Wave 29 clears without any Plate purchases, banking 1,001 credits for wave 30.
- The final policy buys the central Frost Apex for 650 and recovers 1,830 credits through eight late sales. Sales wait for the spawn queue to empty and every remaining enemy to pass conservative coverage. Independent trajectory inspection found no subsequent reentry into any sold tower's ordinary or enlarged coverage, including knockback.

These are properties of the verified plan, not proofs that every ingredient is mandatory. Forge-based alternatives also clear late waves, but this certificate uses no Forge.

## Keeping the board for Endless

The [standing-board campaign plan](../tests/MaximalBastion.Tests/Fixtures/helix-bastion-signal-standing-plan.json) uses the same goal layout and restricts tower and Forge sales to intermissions. Capping paid Plates at two on wave 23 and zero on waves 24–29 leaves 4,376 credits for wave 30. The final wave buys the central Frost Apex for 650 and 16 Plates for 2,760. It finishes with 38 towers, 1,831 credits, one life and zero sales across the campaign.

After building the planner, run the campaign and continue that exact position into the first generated Endless wave:

```powershell
$hold = '.build\planner\helix-standing'
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\helix-bastion-signal-standing-plan.json --no-combat-sales --beam 1 --simulations 32 --output "$hold\campaign"
& $dotnet $planner --planner-endless-continuation --checkpoint "$hold\campaign\wave-30.json" --plan tests\MaximalBastion.Tests\Fixtures\helix-bastion-signal-endless31.json --no-combat-sales --output "$hold\endless31"
```

The [wave 31 experiment](../tests/MaximalBastion.Tests/Fixtures/helix-bastion-signal-endless31.json) compares two continuations from the same victory. Issuing only the commands to enter Endless and start the wave kills 118 of 159 enemies before the first leak. Using First targeting, keeping Frost on Fastest and deploying 15 paid Plates clears all 159 with one life and 108 credits. The same 38 towers remain, with identical tower IDs and no further tower investment.

A second replay-verified route spends more of the wave 29 bank on permanent upgrades. Before wave 30, promote the central Permafrost Frost, the Horizon Beacon and the Shatter Breaker near (523, 299). Cap direct Plates at six. This clears wave 30 with three Apex towers and 1,656 credits. Before wave 31, promote the Permafrost Frost near (610, 440). Armored targeting at 60-tick intervals, Frost on Fastest and 11 paid Plates clear all 159 enemies. This route finishes with all 38 towers, four Apex, 273 credits and zero sales. Keeping the same tower orders after the fourth promotion kills 125 enemies before the first leak.

## Use in later balance audits

Run the calibration first after changing simulation or content. A content change invalidates the old replay's content identity; rerun the policy plan against the new content to obtain a new result. A solver-only rebuild can independently revalidate an unchanged-content checkpoint with `--planner-revalidate`.

If the plan fails, retain its failed wave and legal prefix. Compare matched tactical continuations with `--planner-tactical-forecast`, spending allocations with `--planner-apex-allocations`, and earlier layout-policy alternatives with `--planner-layout-campaign --search-alternatives`. The general `--planner-solve` frontier remains available for geometry-derived spending and multi-wave backtracking. Preserve failed controls, alternative boards, budgets and independent replay receipts. Failure within these finite searches is not evidence of impossibility.

The current witness is not a robust-policy or human-win-rate claim. Its largest combat burst is 18 commands, including 17 target changes; peak non-target combat rate is three commands per second. Removing eight early target commands preserves the same winning state. Shifting every combat command by 15, 30 or 60 ticks causes command rejections in the fixed schedule; that does not measure an adaptive player's or policy's response to delay.
