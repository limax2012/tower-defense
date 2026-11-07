# Cinderworks / Bastion / Signal Gauntlet

Cinderworks (`foundry_loop`) has a [verified normal-start campaign and standing-board Endless 31 calibration](cinderworks-calibration.md). Both use unchanged production content, seed 1337, oracle information, expert execution and authoritative 60 Hz stepping, with zero sales. This page records the separate general spending search and finite-prefix controls.

## Evidence

The recorded general spending planner reaches wave 28 without a supplied layout. It contains 6,748 candidate-wave evaluations across 4,008.59 seconds of accumulated invocation time, with wave 28 the highest completed wave. This bounded result measures discovery coverage; the separate goal-layout calibration supplies the complete campaign witness.

The generated defenses differ substantially from the historical-layout candidates. One family concentrates on Mortars and a Horizon Beacon. Another retains an Apex Ember. A separately verified continuation from the latter family clears wave 28 with 33 towers, one life, 739 credits and zero sales. Its complete fresh prefix checks 385 accepted commands over 105,386 ticks and has state signature `B19FDDAB11D5CEF82F3E4CE2F4A6E33895B68A8E549FC45A7C1FB7EB4AB5FBAE`.

Focused comparisons cover earlier savings, Apex timing, Forge investment, doctrine and specialization choices, lower-route damage, Protocol assignments and targeting. The complete calibration combines upper beam coverage with lower Mortar damage and selective signal-role targeting. A separate segmented full-wave forecast also verifies campaign completion from the authenticated wave-29 prefix using ordinary policy changes.

Partial replays remain labeled by their actual horizon. A small first leak is insufficient evidence of a nearly complete clear when substantial enemies or health remain. Future search comparisons should use the campaign calibration as a positive control and report whether a run supplied its layout goals.

## Run and resume

Use the isolated build instructions in [the solver guide](strategy-solver.md#build-and-run) to set `$dotnet` and `$planner`.

```powershell
$out = '.build\planner\cinderworks-bastion-signal'
& $dotnet $planner --planner-solve --map foundry_loop --difficulty bastion --challenge close_quarters --seed 1337 --no-combat-sales --simulations 1500 --seconds 240 --beam 6 --breadth 10 --max-breadth 96 --decision-ticks 60 --combat-branches 2 --memory-mb 2048 --output $out
& $dotnet $planner --planner-solve --resume "$out\frontier.json" --budget deep --simulations 6000 --seconds 1200 --beam 8 --max-breadth 256 --decision-ticks 60 --combat-branches 2 --output $out
```

Keep the same executable when resuming a frontier. The decision cadence and combat-repair count above match the first invocation. A different executable requires full-prefix revalidation of an individual checkpoint before starting a new search. Wall limits are checked between bounded evaluations, so throughput and the number of completed trials vary by machine and concurrent workloads.

A separate goal-layout fixture reconstructs a 27-tower prefix through wave 28 with 1,069 credits and zero sales. It is a finite prefix control, not a campaign-win calibration. One measured reproduction required 28 evaluations and 3.80 seconds. It uses a supplied layout and does not measure independent layout discovery.

```powershell
$out = '.build\planner\cinderworks-prefix28'
& $dotnet $planner --planner-layout-campaign --plan tests\MaximalBastion.Tests\Fixtures\cinderworks-bastion-signal-prefix28-plan.json --simulations 28 --seconds 60 --beam 1 --output $out
& $dotnet $planner --planner-revalidate --checkpoint "$out\wave-28.json" --output "$out\verified-prefix"
```

Legal build-region boundaries and corners are included in placement proposals. This matters on Cinderworks' narrow upper decks, where two legal tower rows do not fit within an inset-only grid. The placement behavior is shared across maps and is covered by a synthetic narrow-deck regression.
