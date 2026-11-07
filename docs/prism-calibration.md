# Prism Nullspace / Bastion / Signal Gauntlet

The August 31, 2026 solo victory in run history contains a final layout of 30 towers, including six Apex upgrades. The history-guided planner purchases that defense from the ordinary empty starting board and independently verifies the 30-wave campaign. It finishes with 2,333 kills, zero leaks, one life, 198 credits and all 30 towers. The execution profile is oracle/expert, the seed is 1337, and selling is restricted to intermissions. The winning trace contains zero sales.

## Reproduce

Use the isolated build instructions in [the solver guide](strategy-solver.md#build-and-run) to set `$dotnet` and `$planner`. To reproduce the known solution on a checkout without player history:

```powershell
$out = '.build\planner\prism-check'
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\prism-bastion-signal-plan.json --simulations 32 --beam 1 --output "$out\campaign"
& $dotnet $planner --planner-tactical-forecast --first-endless-wave --checkpoint "$out\campaign\wave-30.json" --policies tests\MaximalBastion.Tests\Fixtures\prism-bastion-signal-endless31-policies.json --commit-ticks 1500 --beam 2 --phases 2 --simulations 6 --output "$out\endless31"
```

To search from the original history entry, run:

```powershell
$out = '.build\planner\prism-history-search'
& $dotnet $planner --planner-history-layout --run-id 5b0966127a964fde9509cb7fa7d9141c --apex-from-wave 28 --no-combat-sales --output "$out\history"
& $dotnet $planner --planner-layout-campaign --plan "$out\history\plan.json" --plate-limits 0,2,4,8,16 --plate-from-wave 21 --beam 4 --simulations 500 --seconds 450 --output "$out\search"
```

The history import uses surviving tower IDs as a proposed build order and keeps positions, upgrade branches, Apex goals and the automatically activated Beacon. History records the final board. The precise build order and purchase times must be found through simulation. The chosen wave 28 Apex threshold allows the planner to test an investment schedule. The fresh session supplies all resources, unlocks and enemies.

The general search without a layout prior reached wave 28 in 14,303 wave evaluations, including 4,932 unsuccessful attempts at wave 29. The history-guided search found a verified victory in 165 wave evaluations, about 23 seconds on the measured machine. These are different experiments. The guided search receives a known layout and branches on spending.

## How the strategy holds

The winning plan banks 4,827 credits after wave 27. Before wave 28 it buys five Apex upgrades across Frost, Breaker and Needle towers. Before wave 29 it buys the Split Beam Prism Apex for 2,000. It pays for two Plates on wave 25, four on wave 27 and eleven on wave 30. Earlier investment and overlapping fire let it clear waves 28–29 without buying Plates.

The original history reports 277 credits at victory. The verified reconstruction uses its own targeting and tactical decisions and records the cost and state after each accepted command. Its final campaign state signature is `DF58357CC9DEE23297D17322BB0ED0A860F052866428855B6DE3E44DB19EABC7`.

The standing board kills 137 of 140 enemies on the first generated Endless wave with no new orders after starting it. To clear the wave, the planner uses Strongest targeting for the first 1,500 ticks, then switches to First and places Plates slightly farther along the path. Frost stays on Fastest. The full fresh-start replay verifies all 140 kills, six paid Plates, zero sales and 371 credits remaining. All 30 original towers retain their positions and upgrades.
