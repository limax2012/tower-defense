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

- `--strategy <name>` selects one agent policy.
- `--seed <n>` controls deterministic choices.
- `--runs <1-100>` selects seeds per profile/policy.
- `--map <id>` selects one arena.
- `--difficulty <id|all>` selects a profile or all four.
- `--challenge <id|all>` selects a directive or all competitive directives.
- `--max-wave <n>` sets the success target; 21–30 use authored Mastery and 31+ use generated Endless.
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
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty all --runs 20
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --challenge all --runs 10
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty bastion --challenge close_quarters --no-counter-pressure --runs 20
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --map relay_divide --difficulty bastion --max-wave 30 --runs 20
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --force-build siege_mortar:all --runs 10
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --save-file C:\path\checkpoint.json --max-wave 50 --hold-footprint
```

Internal content IDs currently include `normal` for the Medium difficulty, `close_quarters` for Signal Gauntlet, `no_reserves` for Entrenched, and `relay_divide` for Surge Divide.

## Agent policies

The full matrix uses multiple deterministic strategies that vary opening priorities, tower mix, upgrading, range preference, anti-armor, control, support, tactical-device use, and placement scoring. Strategy diversity is essential: one bot policy can make an arena look impossible simply because its heuristic does not recognize that arena's geometry.

Placement scoring considers:

- legal continuous placement and footprint
- route coverage and useful path length
- corners/crossfire opportunities
- overlap with existing coverage
- tower range and role
- Surge Node value
- enemy composition and required counters
- economy and upgrade completion

Agents can use authored node fields and do prioritize high-value node placements. They are still heuristic. They do not plan several waves ahead like an expert, infer novel synergies from observation, or perfectly reorganize a dense defense. Current Gauntlet agents also do not explicitly switch towers to the Support target mode, which makes Gauntlet completion a conservative directional measure rather than a human forecast.

## Report metrics

Run-level output includes:

- result, deepest wave, lives, leaks, credits, and run time
- towers built/upgraded/sold and completed paths
- direct damage, support damage-equivalent, hits, kills, and impact per credit
- Protocol activations
- control, Expose, Armor Break, armor interaction, and overkill
- Pulse Plate and Forge activity
- Apex purchases/spend
- campaign clear, target reach, and post-campaign depth

Aggregate output includes arena × difficulty and arena × directive matrices. Forced-path reports distinguish overall win rate, path completion coverage, wins among runs that actually completed the path, completed tower count, and completed-run contribution per credit. This prevents a requested path that was never affordable from being credited for a win.

Support/control attribution is deliberately separate from direct damage. Signal Beacon, Frost Spire, Prism Beam, Breaker Cannon, and Arc Relay cannot be assessed fairly from kills alone.

## Current measured baselines

The latest broad reports under `.build\balance` were generated on 2026-08-22 with current four-map authored campaign content.

### Standard, all difficulties

`full-matrix-final-2026-08-22.json` contains 4,160 runs: 13 policies × 4 arenas × 4 difficulties × 20 seeds.

| Difficulty | Wins | Runs | Completion | Average wave |
| --- | ---: | ---: | ---: | ---: |
| Easy | 738 | 1,040 | 71.0% | 18.6 |
| Medium | 605 | 1,040 | 58.2% | 17.7 |
| Hard | 411 | 1,040 | 39.5% | 16.2 |
| Bastion | 173 | 1,040 | 16.6% | 13.6 |

Aggregate Standard completion by arena was 48.4% Foundry, 48.5% Crosswind, 48.4% Prism, and 40.1% Surge. The complete matrix was 1,927 wins from 4,160 runs (46.3%).

### Signal Gauntlet, all difficulties

`gauntlet-matrix-final-2026-08-22.json` contains 1,040 runs.

| Difficulty | Completion |
| --- | ---: |
| Easy | 71.2% |
| Medium | 50.8% |
| Hard | 27.3% |
| Bastion | 7.7% |

Aggregate Gauntlet completion by arena was 43.1% Foundry, 43.1% Crosswind, 39.6% Prism, and 31.2% Surge. Overall completion was 39.2% with average depth 16.0.

### Focused 20-seed controls

Focused current-content matrices produced:

| Profile | Completion | Average wave |
| --- | ---: | ---: |
| Standard Hard | 56.9% | 17.5 |
| Signal Gauntlet Hard | 23.1% | 14.8 |
| Standard Bastion | 27.3% | 14.9 |
| Signal Gauntlet Bastion | 4.8% | 12.1 |

Differences from the broad matrix are expected because focused reports use a different seed/profile slice. Comparisons should use matched report settings, not mix percentages across files.

### Mastery pressure

Current 20-seed-per-arena Synergy-policy controls show:

- Bastion Standard: 2 of 80 runs reached wave 30 (2.5%), average depth 19.3.
- Bastion Entrenched: 0 of 80 reached wave 30; deepest wave 29, average depth 14.3.

Mastery is intentionally much harder than securing the 20-wave campaign. These small strategy-specific samples do not establish a human clear target by themselves.

## Interpretation rules

1. Treat bot completion as comparative evidence, not a difficulty promise.
2. Compare matched seeds, strategies, map, difficulty, directive, and target wave.
3. Review per-policy and per-arena distributions before changing global values.
4. Inspect final layouts and completed-path coverage when an aggregate is surprising.
5. Use checkpoint controls to separate economy, footprint, and tactical execution.
6. Pair simulation findings with human runs, especially for node use, targeting, selling/reorganization, Protocol timing, and late emergency spending.
7. Re-run focused controls after any content or heuristic change before spending time on a full matrix.

## Recommended balance workflow

1. Run regression verification.
2. Run a small smoke matrix over all arenas.
3. Reproduce the concern with one focused profile and fixed seed set.
4. Add matched controls that disable or force the suspected system.
5. Inspect layouts and per-tower attribution.
6. Make the smallest data change that addresses the observed mechanism.
7. Repeat the focused controls.
8. Run all difficulties/directives only when focused results are stable.
9. Validate the resulting opening and late-game feel with human play.

Generated reports are evidence artifacts, not source documentation, and remain outside version control.
