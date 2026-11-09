# Strategy evidence ledger

The production systems and authored JSON define legality and combat. Heuristics below prioritize experiments; none proves a win or an impossibility. Historical outcomes require a new replay against the current content and execution profile.

## Verified current calibration

Helix Reactor / Bastion / Signal Gauntlet has a complete normal-start certificate: 30 waves, zero leaks, one life, 286 credits, 759 accepted actions and 97,389 authoritative 60 Hz ticks. Independent replay and the reusable goal-layout policy runner reproduce the same final state. See [the calibration, source plan and audit interpretation](helix-calibration.md). The supplied layout is a recovered demonstration prior; the verified trajectory starts with ordinary resources and buys every tower legally.

The decisive search improvements were retaining investment by limiting midgame Plate purchases, preserving Frost targeting through cleanup, carrying a complete Prism/control layout into the final waves, clearing wave 29 without Plates, and funding final-wave control by selling towers after all remaining enemies passed their coverage. These measured choices provide a positive control for future possibility tests; they do not establish a broad human success rate.

The [standing-board calibration](helix-calibration.md#keeping-the-board-for-endless) verifies a different use of the same layout. Limit paid Plates to two on wave 23 and zero through wave 29 to carry 4,376 credits into wave 30. Both the single-Apex and four-Apex continuations clear the first generated Endless wave with all 38 towers still present, no leaks and zero sales. The four-Apex continuation ends with 273 credits and uses eleven paid Plates on wave 31.

The [Prism calibration](prism-calibration.md) verifies the August 31 history layout from normal starting resources. Banking 4,827 credits before wave 28 funds five Apex upgrades and preserves enough income for the Chain Beam Prism Apex before wave 29. The 30-tower board clears the campaign with six Apex, one life, 198 credits and zero sales. Its policy continuation clears generated wave 31 with 371 credits and retains the same towers.

The [Rainline calibration](rainline-calibration.md) builds 35 towers from the historical goal order and stops construction before wave 30. Seven paid Plates complete the campaign with 248 credits. Support targeting with Frost on Fastest then clears wave 31 with 968 credits, no new purchases and the same defense. The verified campaign has no Apex or Forge, so those investments are not prerequisites for this particular witness.

The [Cinderworks calibration](cinderworks-calibration.md) combines two upper Amplified Core Lances, a lower Core Lance, three lower Quake Mortars, a maxed Forge and three Apex upgrades. Its 38-tower board finishes wave 30 with 321 credits and survives wave 31 with 298 credits, no new permanent investment and zero sales. The tactical policy selects reachable Bulwarks with ready damage weapons and reachable Restorers with beams and Breakers. Independent failure diagnostics measured large signal-based shielding and repair; protecting damage output requires selecting the actual source rather than relying on the Support label alone. Forge stock, permanent investment and paid Plate limits remain separate allocation decisions.

## Competitive matrix

All 64 solo map/difficulty/directive combinations have a fresh campaign witness followed by survival through generated wave 31 at seed 1337. The selected evidence combines 60 new cases with the four Bastion/Signal Gauntlet calibrations. New cases use build `FEF75E0D921D5922B7F7E2174AE9D6CC4E9DE983A4E58E6C77357D9C3D6BC594` and content `22141FA59B9A2882876DD51B9FCC05B9B3F314B43193C6B4C5CCF3D5D3B4DC51`; reused proofs retain their own recorded build identities. All selected runs use zero combat or intermission sales and preserve their campaign towers through wave 31.

The local manifest is `.build/matrix-20261107/summary/selected-manifest.json`; its generated JSON, CSV and Markdown summaries link every campaign and Endless receipt. The [matrix auditor](strategy-solver.md#auditing-a-selected-matrix) checks the exact fresh campaign prefix, scenario, hashes, mode restrictions and retained defense. Generated evidence remains outside version control.

Transferable adaptations include investing Core Six savings in Apex, moving Precision Rails earlier in the build order when Protocols are unavailable, completing early area damage before spreading investment across more Needles, and selecting permanent Prism/Ember damage in Entrenched. Late-wave target profiles and Plate timing remain separate search dimensions. A fixed strategy can fail on a lower difficulty because enemy timing differs, so transferred-strategy failure does not rank scenario difficulty. These oracle/expert witnesses do not establish human win rates, low-micro robustness or indefinite Endless survival.

## Recovered evidence

The repository includes coverage-aware `AutoPlayer` policies, `CheckpointBeamOptimizer`, persistent `CampaignStrategyOptimizer` search, and strategy-plan artifacts. The `Randomized` policy still ranks placement candidates before sampling; it is a historical randomized baseline, not uniformly random placement. `Experienced` is a hand-authored counter/economy policy, not a globally optimal planner.

`STRATEGY_GUIDE.md`, `AUTONOMOUS_BALANCE.md`, and `BALANCE_REPORT.md` explain the earlier investigations. Some use former map display names: Foundry Loop is Cinderworks, Crosswind Basin is Rainline Heights, Prism Circuit is Prism Nullspace, and Surge Divide is Helix Reactor. Internal IDs remain the authoritative identifiers. Their reported completion samples are historical heuristic results, not current player win probabilities.

Git commit `ef84b83d981be6a5a46f67f8be133104d92569c3` contains:

- `strategies/references/surge-bastion-signal-human-blueprint.md`
- `strategies/references/surge-bastion-signal-human-wave29.checkpoint.json`

The files are recoverable with `git show <commit>:<path>` although they are absent from the current tracked tree. The blueprint identifies content commit `bc382b805e98fc96440b9c217b8393702810c92b`, run `ddbe52aaa5c344059e09c12291d69bc5`, checkpoint SHA-256 `F97F477A3A0ED8DCA04CD0115816AA890444EC661CE0C619F35A03260F5CCBA0`, and winning autosave SHA-256 `EB86CFF2AB9041970B90206B0796990AFCA781FBC1769752BD37A929BEA759BB`.

This is a checkpoint **after clearing wave 29**, with 207 credits, one life, 40 towers, 39 final roles, and no Apex. The recorded next-wave solution promotes Needle 39 to Apex for 400, sells Frost 41 for 135, buys a Needle at `(498.04547, 408.21338)` for 90, buys six Plates for 585 total, and retargets several towers. Its reported wave-30 outcome is 157 kills, no leak, and 112 credits. The blueprint explicitly lacks exact action times and Plate positions. It also reports a repeated wave-28 attempt in cumulative statistics. Therefore it is a useful imported-state calibration case, **not an exact legal replay or proof of a current fresh-start victory**. No matching timed trace of the specifically reported wave-29 allocation comparison was recovered.

Read-only inspection of the local `%LOCALAPPDATA%/MaximalBastion/Saves` repository also found checkpoints from that run:

| File | Cleared wave | Credits | Towers / Apex | Source SHA-256 |
| --- | ---: | ---: | --- | --- |
| `slot-6.json` | 27 | 338 | 36 / 0 | `9D5A7A57DCB517F225CB8238263D9AA8E342C6FE3FEAD056170EAC116A98D95E` |
| `slot-5.json` | 28 | 466 | 38 / 0 | `0BFE103B2953AB44274E4C45B3D12668BFA0782CDDC4FFBF3EEF8C49A2E438C0` |
| `slot-4.json` | 29 | 1,241 | 38 / 0 | `BE3ED58222EDEEEB54B94DAF8770FC9BD2AC593CA809392E0E947FBB7220C4B0` |

Identical copies exist in the legacy `MinimalBastion` save directory. The wave-28 and wave-29 files have the same tower identities, positions, tiers, targets, investments and total spending; their Plate deployment totals are also equal. They do not record an intervening Apex purchase. These are additional imported-state experiments, not the exact timed allocation precedent. Checkpoint timestamps and the blueprint's retry note preclude assuming that the numbered saves form one uninterrupted playthrough. Original player files remain untouched; generated experiments copy their input and record its hash.

Local `.build/balance/tuned-prism-wave30-search/campaign-search.json` records another imported post-wave-29 search: `prism_circuit / bastion / close_quarters`, 128 evaluations, `campaignCompleted`, and a wave-30 `winning-strategy.json`. Its simulation step is 0.05 seconds. That historical 20 Hz search result must not be relabeled a verified 60 Hz victory. `.build` artifacts are local evidence and are not committed.

## Mechanics-backed hypotheses

| Proposed heuristic | Current supporting implementation or experiment | Conditions | Remaining question |
| --- | --- | --- | --- |
| Favor useful exposure across adjacent route segments. | `Maps/MapRuntime.cs` defines exact path interpolation; `Simulation/AutoPlayer.cs` samples range coverage every 22 units and marginal coverage every 32. The historical human blueprint measured extensive repeated coverage. | Count every covered segment using actual range, node and Beacon bonuses; slow/stun extend exposure. | Travel time, target switching, projectile intercepts and enemy spacing require combat rollouts. A bend is not inherently better than a straight. |
| Preserve valuable near-path footprints for short-range roles. | `AutoPlayer.BuildPlacementCandidates` derives a 24-unit grid and node anchors from content; production `ValidatePlacement` checks legality. | Remote Mortar/Watchtower positions can cover route sections unavailable to short-range towers. | Coarse grids can miss legal packing and narrow coverage windows. Refine and retain off-heuristic candidates. |
| Compare complete upgrade paths with new towers using the same budget. | `GameSession.TryChooseTowerDoctrine`, `TrySpecializeTower`, `TryUpgradeTower`; `ContentData/Towers.json`. | Final roles require the preceding tiers. Branch values differ with armor, shields and crowd density. | Existing scalar value scores approximate combat and cannot establish dominance. |
| Save for a legal Apex threshold and test the exact candidate tower. | `GameSession.CanApexUpgrade`; `TowerInstance.TryApexUpgrade`; recovered human Needle 39 promotion. | Apex unlocks before wave 21, needs a finished final role, and costs its authored price. | The historical allocation and timing are incomplete; current same-state alternatives must be simulated. A local win may consume money required next wave. |
| Value Beacon coverage by incremental recipients and stronger bonuses. | `Combat/BuffSystem.cs` chooses attack-rate and range maxima independently; `AutoPlayer.ExperiencedSupportPositionScore`. | Only combat recipients in actual aura range count; disabled/disrupted sources and recipients are excluded. | Existing placement scores use base aura/estimated recipient value. Bundled support plus offense, upgrade reach and suppression need measured comparisons. |
| Use node bonuses without assuming they stack. | `MapRuntime.GetPowerBuff` takes the maximum of each overlapping field statistic; production placement remains authoritative. | Node positions/radii come from map JSON. Four diagonal anchors fit only sufficiently large, unobstructed nodes. | Occupancy is not a goal by itself; route contact and lost support footprints can outweigh a node. |
| Use targeting to remove the actual bottleneck. | `GameSession.TrySetTargetMode`, `TargetSelector`, `AutoPlayer.ExperiencedTargetMode`. | Support targeting is legal only when enabled; supports cannot target. Fastest Frost, Armored Breaker and carrier focus are candidates. | Excessive retargeting can waste damage or crowd control; compare unchanged targeting and delayed actions. |
| Keep native regeneration suppressed with sustained health damage. | `Enemies/EnemyInstance.ApplyHealthDamage` sets a one-second repair pause; `Regenerate` respects it. `Combat/DamageResolver` applies shield absorption first. | A shield-only hit does not reach `ApplyHealthDamage`. Damage-over-time and direct hits have different armor floors. | Restorer healing uses `RestoreHealth` separately and does not respect native repair pause. Measure carrier healing separately. |
| Spread exposure to signal interference when concentration becomes costly. | `GameSession.TryEmitDisruption` sorts reachable targets by invested credits, then distance, then ID. Jammer uses its own radius-based suppression. | A large promotion may change which tower is disrupted; support can also be a disruption target. | Concentration can still win through better uptime/Protocol value; simulate both distributions. |
| Treat Protocol readiness and Apex autonomy as separate resources. | `TryOverdriveTower`, `TryToggleAutoProtocol`, `TryActivateAutomaticProtocol`, Apex Protocol logic in `GameSession`. | Manual Protocols share session cooldown; Apex has its own activation/cooldown state. Entrenched disables controls while Apex self-activation remains. | Value depends on live targets, trigger geometry and suppression; average uptime is only a proposal score. |
| Budget Plates as purchases and timing actions, not free damage. | `Tactics/TacticalDefenseSystem.cs`, `GameSession.TryDeployEmergencyDefense`, `ContentData/Tactics.json`. | One starting charge; direct purchases require an active wave and cost 60 plus 15 per prior direct purchase that wave. Arming, spacing, trigger charges, knockback grace and capacity apply. | Emergency timing, recurring expenditure and forgone upgrades can make a winning plan brittle. There is one Pulse Plate device, not a separate mine item. |
| Compare Forge investment with direct Plates over several waves. | `Tactics.json`: Forge 300; upgrades 180/250; production 34/26/20 active-wave seconds and capacity 3/4/5. | Production pauses between waves and at full inventory; placement consumes a legal footprint. | Payback depends on usage and whether spending 300 now loses the next wave. The historical human reference bought no Forge. |
| Retain earlier surviving boards with different future options. | `CheckpointOptimizer.cs` and `CampaignStrategyOptimizer.cs` retain alternative checkpoints and recovery frontiers. | Similar money/lives are not equivalent if positions, branches, timers or tactical state differ. | A fixed beam and heuristic ranking discard possibilities. Search exhaustion is not an infeasibility certificate. |

## Current action/economy anchors

All solver purchases should pass the production command processor. Tower placement, doctrine, final role, Apex, sale, targeting, Protocol, Plate, Forge, wave start and Endless continuation already have `GameCommand` forms. Pause semantics must follow the chosen execution path: co-op shared pause locks battlefield commands, and solo UI pause must not be inferred from that command alone.

Authored base/Apex prices are Needle 90/400, Shard 150/650, Watchtower 190/850, Frost 140/650, Ember 220/900, Breaker 250/1100, Arc 320/1450, Mortar 360/1600, Prism 450/2000, and Beacon 300/1250. Intermediate doctrine/final-role costs are additional. Read these from JSON for experiments rather than embedding this table into policy logic.

`DamageResolver` resolves shields, armor, minimum direct damage, status applications and boss phase changes. `EnemySystem`, wave queues and the session update order determine whether a candidate clears. Approximate damage scores can order trials but cannot substitute for those systems.

## Interpretation boundaries

- The Helix Reactor/Bastion/Signal Gauntlet normal-start certificate is a positive current calibration. A bot failure warrants investigation of search, timing and adapter fidelity before a balance claim.
- Imported checkpoint results establish only continuation from that checkpoint. Fresh-start reachability needs the complete legal prefix.
- One successful candidate among many is search yield. Replaying one deterministic trace repeatedly does not sample independent player attempts or unseen-seed performance.
- A complete campaign requires the real victory state after wave 30. Endless needs a declared finite horizon and must report horizon survival separately.
- Gameplay and authored balance values remain unchanged by measurement experiments. Synthetic competence fixtures and sensitivity experiments must be labeled separately from unchanged-game results.
