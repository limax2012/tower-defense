# Replay-verified strategy solver

The solver in `src/MaximalBastion/Simulation/Planning` searches spending plans through the production simulation. It preserves authored balance and player saves. Generated results belong under `.build`, `.artifacts`, or another explicit output directory and are not committed.

Read the [strategy ledger](strategy-ledger.md) for mechanics-backed heuristics, historical checkpoint provenance, and interpretation limits. The older policy harness remains available through [AUTONOMOUS_BALANCE.md](../AUTONOMOUS_BALANCE.md).

The [Helix/Bastion/Signal calibration](helix-calibration.md) includes a full normal-start victory, its goal-layout policy plan, an archived witness and regression coverage that rebuilds the campaign independently of the action trace.

The [Prism Nullspace/Bastion/Signal calibration](prism-calibration.md) validates history import, spending alternatives, the full fresh campaign and a continuation through Endless wave 31.

The [Rainline Heights/Bastion/Signal calibration](rainline-calibration.md) reconstructs the historical layout goals with current resources. Its 35-tower campaign defense survives wave 31 using only targeting and wave-start commands.

The [Cinderworks/Bastion/Signal calibration](cinderworks-calibration.md) builds 38 towers, three Apex upgrades and a maxed Forge from normal resources. Selective Bulwark and Restorer targeting clears the campaign; the same defense survives wave 31 without additional permanent investment or any sales. [General-search coverage](cinderworks-search.md) is recorded separately.

## Build and run

An isolated output path avoids replacing the executable of an open game:

```powershell
$dotnet = if (Test-Path .\.dotnet\dotnet.exe) { (Resolve-Path .\.dotnet\dotnet.exe).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:PATH = "$(Split-Path $dotnet);$env:PATH"
& $dotnet build tests\MaximalBastion.Tests\MaximalBastion.Tests.csproj -c Release --artifacts-path .artifacts\planning --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
$planner = Join-Path $PWD '.artifacts\planning\bin\MaximalBastion.Tests\release\MaximalBastion.Tests.dll'
& $dotnet $planner --planner-tests
& $dotnet $planner --planner-diagnostics --output .build\planner\diagnostics
```

The complete deterministic suite is `& $dotnet $planner`. Diagnostics use clearly labeled custom content and archive independently replayed fixture witnesses. They test Apex versus expansion, saving for an income-triggered Apex purchase, useful versus isolated Beacon placement, native repair suppression, Plate timing, and earlier-wave spending revision. The finite two-wave oracle enumerates six specified action sequences and compares solvability with the general planner; its exhaustive claim applies only to that fixture's stated action model.

The reusable experiment script runs the planner checks, diagnostics, the normal-start campaign calibrations, four hard-benchmark baselines, nine strategy families, five capability ablations, two informed execution profiles, and optionally the content matrix:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\strategy-audit.ps1 -Budget smoke -OutputDirectory .build\planner\reusable-audit
```

Its `smoke` configuration uses 80 rollouts, 15 seconds and initial breadth one per experiment for broad coverage. `standard` and `deep` use their larger configured budgets. Each calibration control uses its fixed seed 1337, 32 wave evaluations and a 120-second budget. The budgets are recorded in `configuration.json`. `-SkipBuild` reuses the isolated executable. `-SkipMatrix` omits the broad matrix and retains the focused comparisons.

## Auditing a selected matrix

`scripts/run-strategy-matrix.ps1` reruns the 64 solo competitive strategies in `tests/MaximalBastion.Tests/Fixtures/Matrix`. Every case starts with normal resources, completes the authored campaign and continues through Endless 31. Fixed recipes preserve the selected layouts, policies, spending rules and tactical segment boundaries; they do not search alternatives or import old runtime checkpoints. A failed recipe records a regression for investigation, not proof that the combination is impossible.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-strategy-matrix.ps1 -PlannerAssembly $planner -OutputDirectory .build\strategy-matrix
Get-Content .build\strategy-matrix\status.json -Raw | ConvertFrom-Json
```

Use `-Maps relay_divide`, `-Difficulties bastion` or `-Challenges close_quarters` to select cases. Independent map workers can share an output directory; each writes its own status under `_workers`, and each case has a `status.json`. Add `-Resume` to skip completed cases with matching inputs. Changed builds, content or recipes require a new output directory. After parallel workers finish, run the same command without filters and with `-SummarizeOnly` to merge their records. `-ValidateOnly` checks the catalog without simulating. Exit code 2 means at least one selected case is incomplete or did not clear; results retain both the measured outcome and its comparison with the stored baseline.

`scripts/summarize-strategy-matrix.ps1` audits a manifest of selected campaign and Endless receipts and writes `matrix-summary.json`, `.csv`, and `.md`. Each row names `Map`, `Difficulty`, `Challenge`, `CampaignTrace`, `CampaignReceipt`, `Endless31Trace`, and `Endless31Receipt`; optional checkpoint paths permit state-signature and tower-count checks. Relative artifact paths resolve against the manifest directory. `Reused` identifies an existing calibration separately from new cases, and the manifest's expected build/content hashes apply to new cases.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\summarize-strategy-matrix.ps1 -Manifest .build\matrix-20261107\summary\selected-manifest.json -OutputDirectory .build\matrix-20261107\summary -RequireComplete
Get-Content .build\matrix-20261107\summary\matrix-summary.json -Raw | ConvertFrom-Json | Select-Object Complete,VerifiedNew,VerifiedReused,Pending,Invalid
```

The audit checks scenario identity, fresh origins, matching receipt signatures, the exact campaign action prefix in the Endless continuation, mode legality, sales and standing defenses. It reads existing independent replay receipts; it does not rerun combat. Missing evidence stays pending, invalid evidence causes a nonzero exit, and `-RequireComplete` also rejects an incomplete matrix. To rerun an individual trace, use `--planner-verify --trace PATH` with its recorded build and content. A future balance version requires new simulations and receipts.

## History-guided layout search

`--planner-history-layout --run-id ID` reads a single run from the local history and writes a validated goal layout, a fresh campaign plan and a source-provenance report. Use `--history PATH` for an explicit history JSON. If an older record lacks final geometry, `--fallback-save PATH` accepts a matching save from the same run and scenario. The import preserves positions and progression goals. The resulting simulation starts with the game's normal resources.

`--apex-from-wave N` sets an inferred earliest wave for the recorded Apex goals, and `--no-combat-sales` restricts the generated plan to intermission selling. Tower ID order provides an initial construction order. Actual purchasing, tactics and timing remain search decisions.

`--planner-layout-campaign --plate-limits 0,2,4,8,16 --plate-from-wave 21` compares paid Plate budgets for each supplied policy from the chosen wave onward. The saved winning `selected-plan.json` contains the actual chosen policies and can be rerun directly. Final `wave-NN.json` files follow the selected branch, which is independently verified through the winning trace. The [Prism example](prism-calibration.md#reproduce) includes complete commands.

## Fresh spending search

Solve one fresh-start scenario, including the hard historical calibration lead:

```powershell
& $dotnet $planner --planner-solve --map relay_divide --difficulty bastion --challenge close_quarters --seed 1337 --budget smoke --output .build\planner\helix-bastion-gauntlet
```

The output includes `frontier.json`, `results.json`, `trials.csv`, and `report.md`. A cutoff returns a useful retained frontier and the explicit result `NO SOLUTION FOUND WITHIN BUDGET`. Exit code zero means the experiment completed without a tool/replay error; it does not mean the game was won.

Resume the same search with additional budget:

```powershell
& $dotnet $planner --planner-solve --resume .build\planner\helix-bastion-gauntlet\frontier.json --budget standard --output .build\planner\helix-bastion-gauntlet
```

Budgets are additional per invocation, and the frontier records invocation budgets. The saved scenario and restrictions are retained. Build, content, solver version, scenario and restriction identities must match; incompatible checkpoints and frontiers are rejected. Decision interval, maximum wave ticks and combat repair branch count must also remain unchanged when resuming because they change the meaning of previously evaluated candidates. Increasing wall time, simulations, breadth or retained-board preference can continue the same search.

For example, extend an unmodified smoke/standard search with deep resource limits while preserving its timing and repair settings:

```powershell
& $dotnet $planner --planner-solve --resume .build\planner\helix-bastion-gauntlet\frontier.json --budget deep --decision-ticks 60 --combat-branches 2 --output .build\planner\helix-bastion-gauntlet
```

Use the actual saved values when the original run used overrides. Starting a new directory is required to change those semantics, including the deep preset's default 30-tick decisions and four combat repair branches. Keep the same executable when resuming. Cancellation with Ctrl+C preserves interrupted candidates and pending repair work; a hard process termination can lose work since the last saved batch.

A checkpoint from an earlier executable can seed a new search only after its complete legal prefix reproduces the same state under the current build:

```powershell
& $dotnet $planner --planner-revalidate --checkpoint C:\path\planner-checkpoint.json --output .build\planner\revalidated-prefix
& $dotnet $planner --planner-solve --checkpoint .build\planner\revalidated-prefix\checkpoint.json --budget standard --output .build\planner\new-search
```

Revalidation ignores the cached snapshot, checks the origin and every action's cost, balance and state signature, then performs independent sequential replay. Content changes or any divergence reject the transfer. The output preserves the original fresh/imported origin and records both build identities. Search cursors and trial outcomes are not transferred across builds. Artifact readers retain a complete old version during atomic replacement; brief external file locks are retried.

Rescue an existing read-only game save, strategy checkpoint, or planner checkpoint:

```powershell
& $dotnet $planner --planner-solve --checkpoint C:\path\checkpoint.json --budget standard --output .build\planner\checkpoint-rescue
```

A normal game save establishes an imported starting state. A verified continuation does not establish its reachability from wave 1. Planner checkpoints retain their legal action prefix and original fresh/imported origin. Imported saves retain their solo/co-op setting. No command writes back to the supplied player save.

Compare initial allocations from one raw game save:

```powershell
& $dotnet $planner --planner-checkpoint-audit --checkpoint C:\path\checkpoint.json --output .build\planner\allocation-comparison
```

This experiment tests retaining the board, every affordable existing Apex, and a bounded set of geometry-ranked expansion bundles. All branches then use the same one-second targeting/Protocol/Plate policy while forbidding further tower or Forge investment and sales. Each trace is independently replayed. The report distinguishes next-wave clearance from a terminal campaign continuation and records an unchanged input copy, initial resources, exact costs and source hash. Its deliberately restricted continuation can miss solutions that need another mid-wave investment.

Search Apex choices jointly with targeting, including optional one- or two-tower sale funding:

```powershell
& $dotnet $planner --planner-apex-allocations --checkpoint C:\path\checkpoint.json --apex-upgrades 2 --funding-sales 2 --sale-candidates 8 --simulations 256 --seconds 120 --output .build\planner\joint-allocations
```

This bounded oracle/expert search compares all eligible tower types, including control and support Apex upgrades, against retaining the board. It uses the same tactics-only continuation for each allocation, with a configurable `--decision-ticks` cadence and comma-separated `--targeting` profiles. `allocations.json` includes terminal unresolved enemies and health, so a small first leak is distinguishable from an almost-cleared wave. Cleared checkpoints and independently verified terminal witnesses are saved separately.

Verify an archived trace independently:

```powershell
& $dotnet $planner --planner-verify --trace .build\planner\helix-bastion-gauntlet\witness-000123.json
```

Use an actual witness filename from `report.md`. Verification replays only recorded legal actions from the declared origin, checks costs, balances, action signatures and the final state, and performs no search or rewinds. The output distinguishes a verified legal partial trace, horizon survival, and a campaign victory. Synthetic fixture traces require their matching fixture content; `--planner-diagnostics` generates and verifies them together, whereas ordinary `--planner-verify` loads production content.

Measure execution sensitivity and attempt action deletion from a verified terminal trace:

```powershell
& $dotnet $planner --planner-stress --trace .build\planner\helix-bastion-gauntlet\witness-000123.json --delays 0,15,30,60 --remove-actions 8 --simulations 12 --seconds 120 --output .build\planner\trace-stress
```

Combat times shift relative to each actual wave start, and preparation waits for the preceding wave to clear. Illegal purchases or actions outside their original combat window fail that fixed schedule. Greedy deletion first tests targeting, then Protocol and Plate actions, retaining only deletions that still produce an independently verified result. `trace-experiments.json` and `trace-experiments.md` report action counts, Plate expenditure, margins, verification runs and successful removals. These are known-seed schedule experiments, not a held-out reactive-policy test; unsuccessful deletion does not prove an action is necessary under all alternative schedules.

## Timelapse replay videos

Export a recorded trace as a 1280×720 MP4 using the actual game renderer:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\export-strategy-replay.ps1 -Trace .build\cinderworks-refinement\final\endless31\trace.json -Output .build\replays\cinderworks.mp4 -Speed 12 -Fps 24
```

The script requires `ffmpeg` on `PATH`, or an explicit `-Ffmpeg C:\path\ffmpeg.exe`. It builds the exporter in `.artifacts\replay-export`; use `-SkipBuild` for subsequent exports from that build. Any compatible planner trace can be used, including traces for other maps, difficulties, directives, and horizons.

`-Speed 12` compresses twelve seconds of simulation into one second of video, and `-Fps 24` sets the video frame rate. Simulation still advances at a fixed 60 Hz and executes every recorded action; these options only change frame sampling. Add `-FromWave 29` to start the video at wave 29, or `-MaxFrames 48` for a short rendering preview. Earlier waves are still replayed, and the complete trace is verified even when the video shows only an excerpt.

Export first verifies the trace against current content, including recorded costs, balances, action signatures, and its final state. A trace from another build is accepted only if this independent replay authenticates it. The rendered playback also checks its final state signature. Alongside the MP4, `<output>.replay.json` records replay verification and export settings, `<output>.poster.png` provides a preview, and `<output>.final.png` shows the final rendered frame. The exporter does not modify player saves or run history.

## Content audit

Discover scenarios and explicit exclusions:

```powershell
& $dotnet $planner --planner-registry --seeds 1337,7331 --include-coop --output .build\planner\registry
```

All loaded maps, difficulties and competitive directives register automatically. Current production content has 64 solo campaign combinations per seed. Optional co-op entries are separate shared-economy scenarios. Sandbox has no competitive terminal victory and is explicitly excluded; co-op Sandbox is invalid. Profile progression does not add combat bonuses or roster unlocks to the fresh competitive constructor. Imported states remain separate.

Run a broad first pass, then resume unresolved rows:

```powershell
& $dotnet $planner --planner-audit --seeds 1337 --budget smoke --simulations 60 --seconds 30 --workers 2 --output .build\planner\matrix
& $dotnet $planner --planner-audit --seeds 1337 --budget standard --resume-audit --workers 2 --output .build\planner\matrix
```

`--map`, `--difficulty` and `--challenge` filter the matrix. `--include-coop` adds separate co-op rows. `--workers` controls parallel scenario workers, not parallel branches inside one search. Each worker has its own sessions, frontiers and output directory; content is shared without mutation. Budgets apply to each scenario, so a large matrix can take many times the stated per-scenario wall limit. The memory threshold observes process-managed allocations and is a cooperative stop rather than an enforced peak-memory quota.

Generated `matrix.json`, `matrix.csv`, `registry.json`, and `report.md` identify coverage and link each scenario's evidence. Authored campaign waves are deterministic; different solver seeds vary explored decisions, not independent enemy schedules. Matched seeds are necessary for policy comparisons, but repeated deterministic traces do not establish unseen-condition robustness.

Generate a combined report after the focused experiments, matrix, imported-checkpoint comparisons and trace experiments finish:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\summarize-strategy-audit.ps1 -Directory .build\planner\reusable-audit
```

The summarizer writes `audit-report.md`, `audit-report.json` and `audit-report.csv`. It groups build/content identities, keeps synthetic evidence separate, distinguishes fresh campaign witnesses from imported continuations and horizon survival, and links exact source artifacts. It counts terminal witnesses only when an independent verification receipt matches the trace's hashes, signature, ticks, action count and origin. The trace must match the experiment's scenario and selling restriction. Reported combat and intermission sale counts are checked against accepted commands when present. The summarizer does not rerun simulation. Missing or inconsistent receipts are listed explicitly. Budgets and restrictions remain attached to each experiment, and duplicate final-state witnesses do not increase the headline totals. First-Endless-wave layout continuations and segmented tactical forecasts both contribute verified horizon evidence.

Endless evaluation needs a finite stated horizon:

```powershell
& $dotnet $planner --planner-solve --map relay_divide --difficulty easy --challenge standard --endless --max-wave 35 --budget standard --output .build\planner\endless-35
```

The campaign is legally completed before continuing into generated Endless. Surviving wave 35 is `VERIFIED HORIZON SURVIVAL`, not completion of an infinite mode. A campaign search stopped before wave 30 likewise reports horizon survival separately from victory.

Use `--no-combat-sales` for a fresh search, layout campaign, tactical forecast, Apex allocation or replay verification with selling restricted to intermissions. Tower and Forge sales use the live wave state. Purchases and targeting retain their normal rules. A saved frontier carries its original restriction. When applying the restriction to a checkpoint, the planner validates its entire recorded prefix first. Replay receipts report combat and intermission sales separately.

Use `--planner-endless-continuation` to compare the first generated wave from a verified campaign checkpoint. The game enters Endless through its normal continuation command. Each candidate gets the same starting board and a successful trace is independently replayed from the original fresh start. The [Helix standing-board example](helix-calibration.md#keeping-the-board-for-endless) includes runnable campaign and continuation commands.

`--planner-tactical-forecast --first-endless-wave --checkpoint campaign-wave30.json` performs the same verified transition before searching tactical policy segments. Its output distinguishes verified finite-horizon survival from campaign victory. A policy with `keepExistingOrders: true` suppresses targeting and tactical commands. With no spending callback, it tests the standing defense using only the wave-start command.

## Information, execution and comparisons

`--information oracle` permits authored upcoming-wave knowledge. `--information informed` uses a conservative live-information threat profile: towers, map, current enemies, economy and public unlocks, without pending spawn queues or future-wave composition. It does not reproduce every preview offered by the UI. Offline search among informed policies still uses alternate attempts; it is not a single-attempt player win-rate measurement.

| Execution profile | Reaction delay | Minimum combat action interval | Combat action cap per wave | Combat purchases | Tactical actions |
| --- | ---: | ---: | ---: | --- | --- |
| `expert` | 0 ticks | 0 ticks | 5,000 | Yes | Yes |
| `informed` | 30 ticks | 15 ticks | 60 | Yes | Yes |
| `low-micro` | 60 ticks | 60 ticks | 2 | No | No |

One simulation tick is 1/60 second. `--reaction-ticks` and `--action-ticks` can impose stricter delays. Pre-wave preparation has no time-pressure model. The solver does not use pause or speed changes as strategic actions; all stepping remains speed one.

```powershell
& $dotnet $planner --planner-solve --map foundry_loop --difficulty easy --challenge standard --information informed --execution low-micro --budget smoke --output .build\planner\informed-low-micro
& $dotnet $planner --planner-solve --map foundry_loop --difficulty easy --challenge standard --baseline greedy --seed 1337 --budget smoke --output .build\planner\greedy
& $dotnet $planner --planner-solve --map foundry_loop --difficulty easy --challenge standard --baseline random --seed 1337 --budget smoke --output .build\planner\random
```

Baselines are `planner`, `greedy`, `experienced`, and `random`. Greedy uses coverage-aware legal spending proposals; experienced/random use the prior policies through a command sink. The randomized baseline still ranks spatial candidates, so it is not uniformly random placement. Fixed baselines make one attempt without checkpoint search. Use oracle/unrestricted settings for prior-policy comparisons and inspect effective restrictions in each result.

Portfolio families are `unrestricted`, `concentrated`, `coverage`, `range`, `area`, `control`, `counter`, `economy`, and `deployable`; select one with `--family`. These are finite candidate filters/preferences derived from tower mechanics, not guarantees of expert optimal play within every family. Report each family's actual accepted composition and budget.

Feature controls are `--no-placement`, `--no-combat-purchases`, `--no-support`, `--no-backtracking`, and `--no-saving`. Run controls in separate output directories with matched scenario/seed/budget. They remove proposal capabilities; they do not change tower stats or the production rules. One failed ablation does not establish that a tower family is mandatory.

`--exclude-tower <content-id>` removes that tower type from purchase and upgrade proposals, for example `--exclude-tower signal_beacon`. It does not change the production roster or delete an existing tower in an imported checkpoint. Use fresh starts for roster-dependence comparisons and keep the compute budget matched. This is a search restriction, not a balance experiment changing authored statistics.

## Search and resource model

| Preset | Wave rollouts | Wall seconds per scenario | Preferred boards per wave | Initial / maximum breadth | Periodic decision ticks | Backtrack depth | Combat repair checkpoints | Audit workers | Managed memory threshold |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| `smoke` | 120 | 120 | 3 | 10 / 50 | 60 | 30 | 2 | 1 | 512 MiB |
| `standard` | 2,000 | 1,200 | 4 | 10 / 50 | 60 | 30 | 2 | 2 | 512 MiB |
| `deep` | 25,000 | 14,400 | 8 | 10 / 256 | 30 | 30 | 4 | 4 | 2,048 MiB |

Overrides include `--simulations`, `--seconds`, `--beam`, `--breadth`, `--max-breadth`, `--decision-ticks`, `--backtrack`, `--combat-branches`, `--workers`, and `--memory-mb`. Each wave rollout has an additional 14,400-tick limit in these presets. Simulation timestep never increases to save compute.

`PlannerSession` applies `GameCommand` values through production legality checks and advances `DeterministicSessionRunner`. Detached snapshots include active enemies, queued spawns, projectiles, statuses, support/disruption state, cooldowns, tactical devices and economy. The planner separately preserves removed enemies still referenced by live projectiles. State fingerprints sort the statistics attribution dictionary by numeric tower ID so a sale followed by a replacement hashes identically before and after snapshot restoration. Verification also accepts the complete legacy fingerprint computed from the replayed state, preserving archived witnesses. Revalidation reports both source and current signatures.

Restoration requires a matching state signature; if the production snapshot's normalization changes that state, the adapter reconstructs it from the legal command prefix and verifies the result. Reports record the restoration method. A checkpoint has no unresolved solver commands; pending purchases are search-policy choices, not edits to simulation state. `PlanningReplay` verifies through `GameSession.Update`, the normal simulation entry path.

`SpendingCandidateGenerator` measures path-circle intersection lengths, current support geometry and authored attack properties. It proposes alternative upgrades, branches, Apex, new towers, purchase pairs, support/offense bundles, targeting, Forge, Plates, Protocols, sales and saving. Conditional purchases wait for actual affordability. Geometry includes legal build-region boundaries and corners as well as interior grid points, existing towers and node anchors, then refines promising neighborhoods. Boundary candidates let the planner use narrow decks where multiple legal rows would be missed by an inset grid. Candidates pass placement geometry and command validation. A rotating candidate preserves limited unconventional exploration.

`SpendingPlanner` keeps wave-level alternatives with different reserves, survival and compositions. It schedules preferred boards first, retains other distinct complete snapshots, widens its candidate tranche and revisits earlier waves within the backtrack bound. Combat decisions occur after income or at a bounded periodic opportunity. A bounded, progressively coarser checkpoint sample spans the whole rollout; repair branches select earlier fractions of its trajectory. Existing experienced policy bundles add complete economic/tactical plans to the unrestricted oracle search.

`LayoutBlueprintSpending` allocates an explicit goal layout with real income, prerequisites and legal placement. An optional `generator` goal supplies `x`, `y`, `levelIndex` and `fromWave` for a Charge Forge. Starting at that wave, Forge construction and upgrades take priority over the remaining tower goals; an unaffordable Forge step retains its place in the allocation. Forge construction, tower construction and separately scheduled Apex upgrades share the allocation's reserve and accepted-command limit for each spending callback. An existing Forge at another position is reported as incompatible. `TacticalPolicy.ActivePlateLimit = 0` preserves stored charges while leaving targeting and Protocol decisions active.

`LayoutCampaignRunner` reconstructs a normal-start campaign and compares caller-supplied per-wave tactical alternatives, optionally adding the default policy catalog with `--search-alternatives`. These inputs are demonstration or candidate-layout priors; they do not import saved money or runtime state. Its bounded forward beam is distinct from `SpendingPlanner`'s persistent backtracking frontier.

`TacticalForecastSearch` evaluates full-wave consequences before committing an earlier decision interval, with alternate targeting, Protocol and Plate policies. Failed continuations are scored using escaped and unresolved enemies and health. Optional blueprint spending is evaluated through the same legal allocator:

```powershell
& $dotnet $planner --planner-tactical-forecast --checkpoint .build\planner\helix-calibration\wave-27.json --simulations 512 --seconds 300 --beam 5 --output .build\planner\tactical-repair
```

Use `--blueprint` to enable ordered investment during the continuation, or `--policies` to supply a serialized list of `TacticalPolicy` alternatives. This helper saves the initial state, policy inputs, trial metrics, selected policy segments and any verified clear. A wave clear remains separate from campaign victory.

Use `--segments` instead of `--policies` to replay a fixed list of `TacticalPolicySegment` values. Intervals must be contiguous and start at zero. Decision cadence stays anchored to the initial checkpoint across policy changes; the final policy continues until the wave clears or its rollout budget ends. This supports matched comparisons when a successful strategy changes targeting partway through a wave.

An optional `threatFocus` policy prioritizes signal roles or enemy ranks using only target modes that the production selector can reach. Rules can restrict weapon behaviors, path progress and live-enemy count. After ordinary tactical and spending decisions, ready weapons receive changed `SetTargetMode` commands at the 60 Hz simulation boundary. No enemy can be targeted directly outside the game's selectable modes. The Cinderworks policy prioritizes Bulwarks with damage towers and Restorers with beams and Breakers, while leaving Frost targeting unchanged.

`EndlessLayoutPlan.preparationTicks` advances a bounded number of ordinary simulation ticks after the verified `ContinueEndless` command and before the wave policy. The default is zero. The saved plan and replay retain this timing; Cinderworks uses 1,080 ticks, or 18 seconds.

## Evidence and limits

A `VERIFIED WIN` requires the actual terminal campaign state and an independent legal replay. Its fresh/imported origin remains part of the claim. A verified replay proves that scenario's legal witness; it does not by itself establish `VERIFIED ROBUST POLICY`. The implementation makes no `INFEASIBLE UNDER STATED MODEL` claim for production content.

Each result records build/content hashes, solver version, map/profile/mode, seed, starting and information/execution profiles, budget and restrictions. Traces retain command tick, resolved tick, entity/position, parameters, trigger, actual cost, credits and resulting state signature. Witness CSV files provide the exact build order. Trial reports include lives, reserves, completed wave, actions, Plate purchases, escapes, survivors, damage, overkill, armor absorption, sampled suppression/repair estimates and missed purchase thresholds.

Finite placement and timing proposals, heuristic scores, policy templates, beam preferences, sampled combat checkpoints, rollout duration and action limits restrict coverage. They are search choices, not proofs. The current report does not provide exact firing uptime, exact regeneration attribution, exhaustive mandatory-family removal, a complete held-out robustness matrix, or an impossibility certificate. A strong oracle result and a practical low-micro strategy are distinct findings.

Reports contain wall time, simulated ticks, aggregate successor-capture time, process-managed memory and simulation throughput. These measure tooling performance rather than game FPS. Search histories persist after each batch; no per-frame disk writes or rendered trial captures are required.
