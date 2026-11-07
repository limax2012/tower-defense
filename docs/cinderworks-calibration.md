# Cinderworks / Bastion / Signal Gauntlet

The layout planner clears all 30 authored Cinderworks (`foundry_loop`) waves from the ordinary empty starting board. The verified campaign ends with 2,158 kills, zero leaks, one life and 321 credits. Its 38 towers, three Apex upgrades and fully upgraded Charge Forge also survive the first generated Endless wave without further permanent investment. The scenario uses seed 1337, oracle information, expert execution and authoritative 60 Hz stepping. Mid-wave selling is prohibited; both traces contain zero sales of any kind.

## Reproduce

Use the isolated build instructions in [the solver guide](strategy-solver.md#build-and-run) to set `$dotnet` and `$planner`.

```powershell
$out = '.build\planner\cinderworks-check'
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\cinderworks-bastion-signal-plan.json --no-combat-sales --simulations 30 --seconds 120 --beam 1 --output "$out\campaign"
& $dotnet $planner --planner-endless-continuation --checkpoint "$out\campaign\wave-30.json" --plan tests\MaximalBastion.Tests\Fixtures\cinderworks-bastion-signal-endless31.json --no-combat-sales --simulations 1 --seconds 120 --output "$out\endless31"
```

The campaign fixture contains ordered geometry, upgrade goals and one selected policy per wave. It requires no save or historical checkpoint and reproduces the campaign in 30 wave evaluations. Every purchase uses normal starting resources and earned income. Independent verification replays the complete accepted command sequence from a new game. The Endless fixture preserves this prefix and enters generated play through the real `ContinueEndless` command.

## Allocation and control

The standing defense contains 16 Needles, five Frost Spires, three each of Breakers, Embers, Prisms, Mortars and Beacons, one Shard Fan and one Watchtower, with 21,310 credits invested in towers. The upper Prisms at `(454, 190)` and `(542, 190)` use Aperture with Core Lance; the lower Prism at `(434, 622)` uses Frequency with Core Lance. The three Mortars provide crowd damage while Frost slows successive groups through overlapping coverage.

The Forge goal at `(200, 600)` becomes eligible on wave 12 and costs 730 credits to build and fully upgrade. The selected Apex goals are Horizon Beacon at `(648, 436)` from wave 26, Wildfire Matrix Ember at `(542, 234)` from wave 27, and Permafrost Frost at `(740, 519)` from wave 28. These are earliest investment waves: an upgrade still requires an eligible mature tower and available credits. Wave 29 disables scheduled Apex purchases, preserving the final three-Apex board and tactical funds.

Frost remains on Fastest throughout. The fixture uses the following base tactical schedule; the JSON records the complete spending and placement settings.

| Waves | Base targeting | Decision interval | Paid Plate limit | Active Plate limit |
| --- | --- | --- | --- | --- |
| 1–18 | First | 60 ticks | 0 | 0 |
| 19–21 | Split | 60 ticks | 0 | 0 |
| 22 | Armored | 60 ticks | 0 | 8 |
| 23–26 | Split | 60 ticks | 0 | 0 |
| 27–28 | Split | 60 ticks | 0 | 8 |
| 29 | Armored | 60 ticks | 4 | 2 |
| 30 | First plus threat focus | 120 ticks | 16 | 8 |

Wave 29 places Plates with a `0.1` path-progress offset. Wave 30 holds all permanent investments, uses a zero offset and supplements the base policy with a threat-focus check every tick. It clears all 135 enemies, using 12 paid Plates for 1,710 credits and six Forge-produced Plates.

## Bulwark and Restorer priority

Signal Gauntlet support can replenish a group faster than ordinary targeting removes it. Every five seconds, a Restorer heals up to seven nearby living enemies, including itself, for 10% of each recipient's maximum health within a 100-unit radius. A Bulwark grants shields to the same-sized nearby group. Healing from the Restorer's pulse is separate from a Regenerator's natural regeneration and is not suppressed by recently taking damage.

The final-wave controller first gives all ready damage weapons except Frost priority on a selectable Bulwark. Core Lance Prisms and Breakers additionally prioritize a selectable Restorer, at lower priority than the Bulwark. The controller enumerates ordinary target modes through the production target selector and issues only legal `SetTargetMode` commands. It cannot directly assign an arbitrary enemy target. Frost keeps spreading control, and weapons without a selectable priority target retain their existing mode until the base policy updates it.

This distinction matters because Support targeting groups all signal roles together; it does not specifically select the repair or shield source. In the verified wave 30, the Bulwark dies at approximately 18.78 seconds, the boss at 92.20 seconds and the Restorer at 92.68 seconds. Removing the support sources while retaining crowd control and damage permits the existing board to finish the wave.

## Standing-board Endless control and proof scope

The continuation waits 1,080 simulation ticks, or 18 seconds, after entering Endless and before starting wave 31. It uses the Armored profile every 60 ticks, explicitly keeps Frost on Fastest, Breakers on Armored and Embers and Prisms on Strongest, then applies the same per-tick Bulwark/Restorer priorities. Tactical Plates and protocols remain available. It clears all 136 generated enemies with seven paid Plates for 735 credits, ending with one life, zero leaks, 298 credits and 2,294 total kills.

Every tower ID, position, level, doctrine, specialization, Apex state and invested-credit total remains unchanged. The Forge also retains its position, level and investment. Only the wave controls, targeting, protocols and Plate deployment commands are used in the continuation.

The campaign proof checks 993 accepted actions over 91,386 ticks and ends at state signature `991BB9BB29CA076283C8C6B126EAF12F0E29251CD46B9A8E81E9A78CEB41D657`. One fresh production reproduction, including independent replay, took 6.28 seconds. The continuation proof checks 1,594 actions over 99,544 ticks and ends at `1FD2E0069551A79A9F4F8B3DFF33BF036B9B97B069DE797B51DAA55535B24FB8`; its reproduction, including campaign-prefix verification, took 6.01 seconds. These timings measure the supplied deterministic plans, not discovery of a winning strategy, and depend on machine load.

Both receipts establish legal normal-origin execution and separately report combat and intermission sales. The calibration regression checks the exact campaign and wave-31 outcomes, retained defenses and continuation command types. This is an expert, deterministic possibility witness for the supplied scenario and policy. It does not measure human execution difficulty, prove that a simpler policy cannot win, or establish survival beyond generated wave 31.
