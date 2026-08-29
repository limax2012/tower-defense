# Deterministic Balance Harness

Minimal Bastion includes a headless simulation CLI in `tests/MinimalBastion.Tests`. It uses the production content loader, map geometry, placement validation, tower/enemy systems, wave manager, economy, tactical systems, and fixed gameplay rules. It is designed for repeatable comparisons and regression detection, not as a literal prediction of human win probability.

## Running the harness

Build and run from the repository root:

```powershell
$dotnet = if (Test-Path .\.dotnet\dotnet.exe) { (Resolve-Path .\.dotnet\dotnet.exe).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:Path = "$(Split-Path $dotnet);$env:Path"
& $dotnet build MinimalBastion.sln -c Release
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
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
- `--challenge <id|all>` selects a directive or all competitive directives.
- `--max-wave <n>` sets the success target. Every authored campaign ends at wave 30 unless a later Endless target is requested.
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
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Experienced --seed 1337
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --strategy Experienced --difficulty hard --challenge all --max-wave 30 --runs 10
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --strategy Experienced --difficulty bastion --challenge all --max-wave 30 --runs 10
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --force-build siege_mortar:all --runs 10
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --save-file C:\path\checkpoint.json --max-wave 50 --hold-footprint
```

Internal content IDs include `normal` for Medium, `close_quarters` for Signal Gauntlet, `no_reserves` for Entrenched, and `relay_divide` for Surge Divide.

## Agent policies

The matrix can use multiple deterministic strategies that vary opening priorities, tower mix, upgrading, range preference, anti-armor, control, support, tactical-device use, and placement scoring. Strategy diversity is useful when auditing a mechanic; one policy can make an arena look impossible because its heuristic does not recognize that arena's geometry.

The Experienced policy is the expert reference. It adds:

- an economical three-Needle opening;
- authored counter milestones for Frost, Shard, Breaker, Arc, Prism, Ember, Mortar, and Watchtower;
- Fastest Frost, Armored Breaker, Strongest priority fire, and Support targeting in Signal Gauntlet;
- compact Surge Node candidates, including four-tower patterns on sufficiently large unobstructed nodes;
- Signal Beacon scoring based on new recipients and strongest-value non-stacking rules;
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

Support/control attribution is deliberately separate from direct damage. Signal Beacon, Frost Spire, Prism Beam, Breaker Cannon, and Arc Relay cannot be assessed fairly from kills alone.

## Current Experienced baseline

The latest complete validation sweep used two seeds for all four arenas and all four competitive directives, or 32 runs per difficulty:

| Difficulty | Completion | Average wave | Average lives |
| --- | ---: | ---: | ---: |
| Easy | 18.8% | 26.2 | 3.4 |
| Medium | 3.1% | 20.5 | 0.3 |
| Hard | 0.0% | 15.9 | 0.0 |
| Bastion | 0.0% | 11.4 | 0.0 |

Every profile uses wave 30 as the campaign success target and enables Apex at wave 21. Bastion is the no-breach profile; its zero in this limited deterministic sample identifies an aspirational ceiling, not proof that human completion is impossible. See [BALANCE_REPORT.md](BALANCE_REPORT.md) for map/directive splits and interpretation.

## Interpretation rules

1. Treat completion as comparative evidence, not a difficulty promise.
2. Compare matched seeds, strategies, map, difficulty, directive, and target wave.
3. Review map and directive distributions before changing global values.
4. Inspect final layouts and completed-path coverage when an aggregate is surprising.
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
