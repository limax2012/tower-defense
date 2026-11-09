# Deterministic Balance Harness

Maximal Bastion includes a headless simulation CLI in `tests/MaximalBastion.Tests`. It uses the production content loader, map geometry, placement validation, tower/enemy systems, wave manager, economy, tactical systems, and fixed gameplay rules. It is designed for repeatable comparisons and regression detection, not as a literal prediction of human win probability.

The [strategy solver](docs/strategy-solver.md) adds legal command traces, detached active-wave checkpoints, persistent spending search, independent replay verification, content-matrix reports, and synthetic competence diagnostics. Its simulation uses the authoritative 60 Hz tick at speed one. The legacy `--simulate`/`--optimize-strategy` harness described below defaults to 0.05-second steps; historical results from that path require a new 60 Hz replay before they can support a solver `VERIFIED WIN` claim. See the [strategy ledger](docs/strategy-ledger.md) for recovered human evidence and current mechanics.

The captured verification for armor-adjusted Burn selection and shared Slow records 59/64 campaign and Endless-31 wins with the unchanged reference matrix recipes. Five separately stored [adapted recipes](tests/MaximalBastion.Tests/Fixtures/MatrixBurnSelection/catalog.json) provide verified wins for the remaining combinations. A fresh rerun of the selected 64 strategies verifies both horizons and exactly reproduces all 128 baseline final-state signatures and gameplay metric sets, with zero sales and every campaign tower retained. The original controls remain separate so changes in mechanics and changes in policy are not conflated. See [the balance assessment](BALANCE_REPORT.md#competitive-possibility-matrix) for measured comparisons.

Reference measurements below belong to their recorded builds. Archived traces and fixed recipes support matched replays; their recorded outcomes do not substitute for current-build verification.

The [Helix/Bastion/Signal calibration](docs/helix-calibration.md) records a verified 30-wave normal-start victory. Its reusable goal-layout policy runner and archived replay have regression coverage; `scripts/strategy-audit.ps1` includes it as a demonstration-guided positive control.

The [Prism Nullspace/Bastion/Signal calibration](docs/prism-calibration.md) imports an existing history layout, purchases it from a fresh start and searches paid Plate budgets. Its reference results verify both campaign victory and survival through the first generated Endless wave with the standing board. The audit script includes its campaign plan as a second positive control.

The [Rainline Heights/Bastion/Signal calibration](docs/rainline-calibration.md) rebuilds a historical layout under current rules. It clears the campaign with 35 towers and seven paid Plates on wave 30, then survives wave 31 with targeting changes and no further purchases. Both verified traces contain zero sales and zero leaks.

The [Cinderworks/Bastion/Signal calibration](docs/cinderworks-calibration.md) records all 30 waves and generated Endless 31 with the same 38 towers, three Apex upgrades and maxed Forge. Selective targeting prioritizes Bulwarks and Restorers through legal target modes. Both recorded stages have zero leaks and zero sales; wave 31 requires no additional permanent investment. Its goal-layout plan is the audit's fourth positive control. The separate reference [general search](docs/cinderworks-search.md) reached wave 28 without supplied layout goals.

The reference solo competitive possibility matrix covers all four maps, four difficulties and four directives at seed 1337. Its 64 cases have independently verified fresh campaign victories and same-prefix Endless 31 continuations on the reference build. Every selected witness uses zero sales and retains its campaign towers. Fixed recipes and final state signatures provide matched controls for gameplay changes. These are supplied-layout and tactical-search witnesses under oracle/expert execution, not cold-search success rates. The [matrix runner and audit](docs/strategy-solver.md#auditing-a-selected-matrix) rebuild every case from normal resources and record per-case proof paths, build/content identities, baseline deltas, lives and retained defenses. Co-op and noncompetitive Sandbox are outside this matrix.

## Running the harness

Build and run from the repository root:

```powershell
$dotnet = if (Test-Path .\.dotnet\dotnet.exe) { (Resolve-Path .\.dotnet\dotnet.exe).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:Path = "$(Split-Path $dotnet);$env:Path"
& $dotnet build MaximalBastion.sln -c Release
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
```

Reports are written to `.build\balance` unless `--output` selects another path.

### Modes

- `--balance` runs isolated combat/economy comparisons.
- `--simulate` runs one visible-summary full game for a named strategy/seed.
- `--simulate-full` runs a matrix and writes detailed JSON.

### Filters and controls

- `--strategy <name>` selects one agent policy. `Experienced` is the strongest map/counter-aware reference policy.
- `--seed <n>` controls deterministic choices.
- `--runs <1-100>` selects seeds per profile/policy.
- `--map <id>` selects one arena.
- `--difficulty <id|all>` selects a profile or all four.
- `--challenge <id|all>` selects a mode or all competitive modes.
- `--max-wave <n>` sets the success target. Every campaign ends at wave 30 unless a later Endless target is requested.
- `--force-build <tower:doctrine>specialization>` constrains one complete path.
- `--force-build <tower:all>` compares a tower's four paths; `all` audits every completed path.
- `--no-protocols` and `--no-apex` create matched system controls.
- `--no-counter-support`, `--no-counter-attackers`, and `--no-counter-pressure` isolate Signal Gauntlet pressure.
- `--save-file <path>` starts each policy/seed from the same read-only checkpoint.
- `--hold-build` forbids all defensive changes.
- `--hold-footprint` allows upgrades/tactical actions but forbids new towers, Forge construction, and selling.
- `--summary-only` omits detailed run rows.
- `--output <path>` sets the JSON destination.

Examples:

```powershell
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate --strategy Experienced --seed 1337
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate-full --strategy Experienced --difficulty hard --challenge all --max-wave 30 --runs 10
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate-full --strategy Experienced --difficulty bastion --challenge all --max-wave 30 --runs 10
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --force-build siege_mortar:all --runs 10
& $dotnet run --project tests\MaximalBastion.Tests -c Release --no-build -- --simulate-full --save-file C:\path\checkpoint.json --max-wave 50 --hold-footprint
```

Internal content IDs include `normal` for Medium, `close_quarters` for Signal Gauntlet, `no_reserves` for Entrenched, and `relay_divide` for Helix Reactor.

## Agent policies

The matrix can use multiple deterministic strategies that vary opening priorities, tower mix, upgrading, range preference, anti-armor, control, support, tactical-device use, and placement scoring. Strategy diversity is useful when auditing a mechanic; one policy can make an arena look impossible because its heuristic does not recognize that arena's geometry.

The Experienced policy is the expert reference. It adds:

- an economical three-Needle opening;
- planned counter milestones for Frost, Shard, Breaker, Arc, Prism, Ember, Mortar, and Watchtower;
- Fastest Frost, Armored Breaker, Strongest priority fire, and Support targeting in Signal Gauntlet;
- compact Surge Node candidates, including four-tower patterns on sufficiently large unobstructed nodes;
- Signal Beacon scoring based on new recipients and strongest-value non-stacking rules;
- Apex scoring that includes expected independent Protocol uptime and discourages repeatedly promoting one tower role;
- upgrade breadth so duplicate generalists do not mature before key counters;
- late Forge timing and Apex reinvestment.

It remains heuristic. It does not globally solve all future placements, learn from failed runs, or perfectly plan sell/rebuild sequences. Its result is comparative evidence, not a human probability.

## Placement and report evidence

Placement scoring considers:

- legal continuous placement and footprint;
- route coverage and useful path length;
- corners and crossfire opportunities;
- overlap with existing coverage;
- tower range and role;
- Surge Node value and tower/node fit;
- Signal Beacon recipient value without double-counting overlapping auras;
- enemy composition and required counters;
- economy and upgrade completion.

Run-level output includes exact final tower coordinates, doctrine, specialization, level, Apex state, and containing Surge Node. That makes winning and losing layouts inspectable instead of reducing the result to a percentage.

Other output includes:

- result, deepest wave, lives, leaks, credits, and run time;
- towers built/upgraded/sold and completed paths;
- direct damage, support damage-equivalent, hits, kills, and impact per credit;
- Protocol activations and control/Expose/Armor Break attribution;
- Pulse Plate and Forge activity;
- Apex purchases and spend;
- campaign clear and target-wave reach.
- every enemy group still alive at defeat, including count, health, shield, rank, signal role, furthest progress, and enemies still queued.

Static wave checks measure total health and shield, armor-adjusted damage demand, spawn duration, scheduled signal carriers, and the largest speed-weighted demand arriving inside an eight-second window. These measurements are pressure proxies rather than win-rate predictions, but they catch composition and cadence cliffs that a health-multiplier sequence alone cannot reveal.

Support/control attribution is deliberately separate from direct damage. Signal Beacon, Frost Spire, Prism Beam, Breaker Cannon, and Arc Relay cannot be assessed fairly from kills alone.

## Experienced reference baseline

This archived legacy-harness sweep used two seeds for all four arenas and all four competitive modes, or 32 runs per difficulty. It precedes retained-source burn selection and is separate from both the 60 Hz recipe matrix and the ten-seed reference sweep in [BALANCE_REPORT.md](BALANCE_REPORT.md):

| Difficulty | Completion | Average wave | Average lives |
| --- | ---: | ---: | ---: |
| Easy | 9.4% | 25.2 | 1.8 |
| Medium | 3.1% | 20.1 | 0.3 |
| Hard | 0.0% | 15.8 | 0.0 |
| Bastion | 0.0% | 11.3 | 0.0 |

Every profile uses wave 30 as the campaign success target and enables Apex at wave 21. Bastion is the no-breach profile; its zero in this limited deterministic sample identifies an aspirational ceiling, not proof that human completion is impossible. See [BALANCE_REPORT.md](BALANCE_REPORT.md) for arena/mode splits and interpretation.

## Interpretation rules

1. Treat completion as comparative evidence, not a difficulty promise.
2. Compare matched seeds, strategies, map, difficulty, mode, and target wave.
3. Review arena and mode distributions before changing global values.
4. Inspect final layouts, completed-path coverage, and the full field remaining at defeat when an aggregate is surprising.
5. Use checkpoint controls to separate economy, footprint, and tactical execution.
6. Pair simulation findings with human runs, especially for node use, selling/reorganization, Protocol timing, and emergency spending.
7. Re-run focused controls after any content or heuristic change before spending time on a full matrix.

## Recommended balance workflow

1. Run regression verification.
2. Run a small smoke matrix over all arenas.
3. Reproduce the concern with one focused profile and fixed seed set.
4. Add matched controls that disable or force the suspected system.
5. Inspect layouts and per-tower attribution.
6. Make the smallest data change that addresses the observed mechanism.
7. Repeat the focused controls.
8. Run all profiles only after focused results are stable.
9. Validate the resulting opening and late-game feel with human play.

Generated reports are evidence artifacts, not source documentation, and remain outside version control.
