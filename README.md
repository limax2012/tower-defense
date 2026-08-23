# Minimal Bastion

Minimal Bastion is a colorful, data-driven 2D tower-defense game built with C#, .NET 10, and MonoGame DesktopGL. It includes four strategic maps with 20-wave authored campaigns and optional 10-wave Mastery extensions, four difficulty profiles, four decision-changing challenge directives plus a solo Sandbox Lab, 10 towers with 20 tier-two doctrines, 20 final specializations, and distinct tactical Protocols, elites and phased bosses, an in-game tower/threat/campaign/profile tactical library, Pulse Plates and a Charge Forge, dynamically expanding independent saves, procedural audio, persistent display settings, post-run analysis, deterministic balance agents, and direct two-player online co-op.

## Build and play

Install the .NET 10 SDK, open PowerShell in the repository root, and run:

```powershell
dotnet restore MinimalBastion.sln
dotnet run --project src\MinimalBastion\MinimalBastion.csproj -c Release
```

If this working copy already has a workspace-local SDK at `.dotnet\dotnet.exe`, but `dotnet` is not installed globally, use:

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
.\.dotnet\dotnet.exe restore MinimalBastion.sln
.\.dotnet\dotnet.exe run --project src\MinimalBastion\MinimalBastion.csproj -c Release
```

A Release build places the framework-dependent executable at `src\MinimalBastion\bin\Release\net10.0\MinimalBastion.exe`. The self-contained publish command below writes its output to `.build\publish`.

## Online co-op

Online co-op is direct peer-to-peer TCP:

1. The host selects a map, chooses **Online Co-op**, then **Host Online Game**.
2. The host forwards TCP port `28741` on their router to the host PC and allows Minimal Bastion through the firewall if Windows asks.
3. The host shares their public IP address or DNS name and the six-character join code shown in-game.
4. The friend chooses **Online Co-op**, enters `address` or `address:port`, enters the join code, and joins.

No custom game server is required: the host PC is the server for this private two-player match. Direct internet play still requires TCP `28741` to reach the host, normally through router port forwarding and the Windows firewall. A peer VPN such as Tailscale or ZeroTier can be used instead of manual port forwarding. There is no matchmaking, hosted relay, automatic router configuration, or encrypted transport.

The host can click the large join-code field or press `Ctrl+C` in the waiting lobby to copy the code before sending it to their friend.

The handshake rejects mismatched compiled builds or any recursive JSON gameplay/campaign content before a match starts and times out half-open attempts after ten seconds. Ambiguous duplicate map/campaign identities and campaigns attached to the wrong arena are rejected while loading. Transport messages use bounded length-prefixed frames, a 64-frame send budget, and type/direction/content validation before gameplay dispatch, so malformed traffic cannot request unbounded allocation or enter the simulation. Large authoritative snapshots use bounded Brotli framing (2 MiB on the wire, 8 MiB after decoding), allowing dense endless defenses to reconnect without opening an unbounded decompression path. During play, both peers simulate locally at a deterministic 60 Hz while the host sequences commands and sends one state checksum per second. Variable-rate, local-only presentation smooths motion and effects on 60/120/144 Hz displays without changing synchronized state. A mismatch triggers an authoritative state repair instead of ending the match, while a post-repair fence discards stale checksums that were already in flight. If Player 2 disconnects, the match pauses and remains on the host. Player 2 can restart the game if necessary, join with the same host address and six-character code, and receive the complete current wave, enemy, tower, economy, timer, pause, ready, and active Protocol animation state.

Co-op uses shared credits, lives, plates, waves, and speed. Both players can upgrade, specialize, retarget, activate or automate Protocols, or sell any tower or Charge Forge; the P1/P2 ring records who originally placed it without restricting control. Both players must ready each wave, and the wave button plus sidebar show both ready states and the same early-call countdown. The host locks the +20 reward only when the second player readies before the timer expires; one early ready does not preserve the bonus indefinitely. The sidebar reports live, delayed, stalled, and resynchronizing link states from the age of the latest valid peer traffic before the 15-second reconnect threshold. A compact P1/P2 crosshair shows the other player's live battlefield cursor, a translucent tower or plate ghost shows their snapped placement preview, and a small color-coded P1/P2 tag identifies the deployed tower they are inspecting; middle-click creates a more persistent location ping.

Either player can press Esc, P, or the HUD Pause button to pause/resume the shared deterministic simulation. Both peers stop on the same fixed tick, the compact banner identifies who paused, and a field-preserving sidebar offers Resume, Tactical Library, synchronized Restart, and Main Menu. Building, upgrades, sales, targeting, plates, Forge changes, Protocols, speed, and new ready signals are locked until play resumes; the host also rejects late battlefield commands that reach their deterministic tick during pause. Combat, wave spawning, economy, Forge production, Protocol cooldowns, and visual effects freeze, while an existing between-wave early-call deadline continues so paused planning cannot bank the +20 reward.

If the link drops—or valid inbound traffic disappears for 15 seconds—the match pauses and the client retries automatically. The host's preserved-session overlay keeps the six-character rejoin code visible and lets it be copied by clicking the code or pressing Ctrl+C.

At victory or defeat, **Restart Co-op** uses a two-click confirmation, keeps both players connected, and asks the host to initialize a fresh authoritative game on the same map. Both peers receive the new state before play resumes and wave-ready state is cleared. The fresh run takes over the single rolling autosave after its first completed wave, while numbered manual saves remain protected. Solo restart is confirmed the same way. **Main Menu** remains the explicit action that ends the online session.

## Build and test

Install the .NET 10 SDK. The commands below automatically prefer an existing workspace-local SDK when one is available and otherwise use `dotnet` from `PATH`:

```powershell
$dotnet = if (Test-Path .\.dotnet\dotnet.exe) { (Resolve-Path .\.dotnet\dotnet.exe).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:Path = "$(Split-Path $dotnet);$env:Path"
& $dotnet restore MinimalBastion.sln
& $dotnet build MinimalBastion.sln -c Release --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
& $dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
```

To verify without disturbing a game that is already running, use:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1
```

This builds into an isolated `%TEMP%` output, runs the complete regression suite, and invokes a hidden, non-activating renderer that captures the real 2560x1440 UI under `.artifacts\verification\ui`. It does not replace the executable or DLLs used by the running game and never sends keyboard or mouse input.

Run the isolated combat benchmark and deterministic full-game agents with:

```powershell
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty all --runs 3
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --challenge all --runs 3
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --force-build siege_mortar:all --runs 2
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --map relay_divide --runs 10
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --strategy LongRange "--force-build=siege_mortar:mortar_loader>quake_shell"
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --no-protocols
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --max-wave 40 --no-apex
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --save-file C:\path\to\checkpoint.json --max-wave 50
```

Reports are written under `.build\balance`. Supported filters include `--strategy`, `--seed`, `--runs`, `--map`, `--difficulty`, `--challenge`, `--max-wave`, `--force-build`, `--no-protocols`, `--no-apex`, `--no-counter-support`, `--no-counter-attackers`, `--no-counter-pressure`, `--save-file`, `--hold-build`, `--hold-footprint`, `--summary-only`, and `--output`; use `all` with either `--difficulty` or `--challenge` for a complete sweep. Multi-profile reports include arena-by-difficulty and arena-by-directive matrices so route geometry cannot hide a localized balance extreme inside an aggregate result. A forced build uses `tower:doctrine>specialization` and constrains that tower without changing other planning decisions; use `tower:all` to compare its four complete paths or `all` to audit all forty paths. Forced-path reports retain the requested path in JSON, separate overall wins from completion coverage, wins among runs that actually reached the final path, completed tower count, and completed-run contribution per credit, then print a path-by-arena matrix of win rate, completion rate, and average wave. `--no-protocols` and `--no-apex` create matched tower-system control groups. Signal Gauntlet can isolate support carriers, tower attackers, or all counter-pressure with the three `--no-counter-*` controls. `--save-file` continues each strategy and seed from the same read-only checkpoint. `--hold-build` measures the checkpoint's existing defense without changes, while `--hold-footprint` permits upgrades and tactical actions but forbids new towers, Forge construction, and selling. Tower reports list Apex purchases and spend, Protocol activations, doctrine, final-role, and completed-build-path usage, and distinguish direct damage from Signal Beacon damage-equivalent, recipient-seconds, and source-attributed Slow, Stun, Exposed, and Armor Break enemy-seconds so support/control value is not hidden behind raw kill totals.

When `--max-wave` extends beyond the 20-wave campaign, reaching that configured cap counts as a successful simulation target. Waves 21-30 use the selected arena's authored Mastery sequence and unlock Apex upgrades; generated Endless waves begin at 31. A separate progression block reports campaign clears, target reaches, deepest wave, average depth after a clear, and the same progression split by arena, so campaign completion is never conflated with an optional Mastery or endless goal.

Create a self-contained Windows build with:

```powershell
dotnet restore MinimalBastion.sln -r win-x64 --disable-build-servers
dotnet publish src\MinimalBastion -c Release -r win-x64 --self-contained true --no-restore -o .build\publish --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
```

This command creates `.build\publish\MinimalBastion.exe`. The output is self-contained for Windows x64 and does not require the .NET SDK on the computer that runs it.

## Controls

- **Tactical Library** is available from the title screen, solo pause menu, and at any time in co-op by pressing `Tab` or using the compact shared-pause sidebar. Its Towers page previews either tier-two doctrine with exact stats, cumulative cost, and both compatible final roles; Threats explains counters and status glyphs; Campaigns exposes every authored wave, base starting credits, and a compact route preview; Profiles gives exact difficulty multipliers and directive restrictions; and Systems consolidates targeting, progression, status stacking, Protocol automation, Beacon/Surge interaction, and current Pulse Plate/Forge rules. The co-op library is a local overlay: network polling and the shared simulation continue while local battlefield input remains blocked.
- The title screen, game setup, pause menu, and Settings are intentionally mouse-driven: left click chooses an action or option, while Escape returns or resumes. They do not keep a hidden keyboard focus.
- Save Slots and Run History support Up/Down selection, Left/Right paging, Enter confirmation/viewing, and mouse controls. Result screens support arrow keys or `Tab` to change the selected action and Enter to activate it.
- In the Tactical Library, Left/Right changes pages, Up/Down changes the selected entry, and `1`-`0` directly selects a visible tower, threat, or campaign entry. `Tab` opens or closes the library during co-op; Escape, right click, or the Back button closes it elsewhere.
- Online co-op: Up/Down selects Host, Join, or Back; Enter activates the focused action. Typing or selecting either connection field focuses Join, Ctrl+V pastes, Ctrl+C copies displayed host/rejoin codes, and held Backspace erases continuously. Windows may request firewall access the first time that executable hosts; internet guests still require TCP 28741 forwarding or a shared VPN path.
- Left click: select, place, or activate a UI control.
- Right click or Escape: cancel placement; Escape pauses in solo play.
- Tower and Charge Forge placement ghosts snap to the closest legal point within a small assist radius when the cursor is just outside a valid location. Exact legal placement remains continuous rather than grid-based; the translucent ghost, range preview, and placement-status banner show the resolved position and validity. Pulse Plates similarly snap only to nearby legal route positions.
- `1`-`0`: prepare the corresponding tower.
- `Q`: prepare a stored Pulse Plate. During an active wave, buy a replacement starting at 60 credits; each additional direct purchase in that same wave costs 15 more, and the price resets to 60 when the next wave starts.
- `G`: prepare or select the Charge Forge.
- `E`: activate the selected tower's unique Protocol.
- `A`: arm/disarm automatic Protocol use for the selected tower.
- `U`: upgrade the selected tower or Charge Forge; at a branch, choose the first/upper option.
- `I`: choose the second/lower tower-upgrade branch.
- `X`: promote an eligible selected tier-three tower to Apex during Mastery or Endless.
- `D`: enable or disable the selected tower in Sandbox Lab.
- `Delete`: sell the selected tower or Charge Forge.
- `T`: open or close the selected tower's targeting picker. The current mode does not change until a replacement is chosen.
- `Space`: start/ready the next wave or claim the early-call reward.
- `S`: toggle 1x/2x speed.
- `P` or `Escape`: pause/resume in solo play.
- Middle click: send a co-op ping.
- In the co-op address/code fields, `Ctrl+C` copies the active field, `Ctrl+V` pastes, holding Backspace continuously erases text, and `Tab` switches fields. An IP address or DNS name without a port automatically uses TCP `28741`; an explicit custom port remains supported.
- `F4`: toggle the debug overlay in Debug builds.
- `F11`: toggle borderless desktop fullscreen from any game screen.

## Gameplay notes

- The title screen presents six full-width actions. **New Game** opens the arena, difficulty, and directive setup directly. **Online Co-op** opens the connection screen: joining needs the friend's address and code, while hosting opens the same setup before creating the lobby. **Standard** enables every system. **Signal Gauntlet** keeps the full roster and adds 10% opening credits, then introduces Accelerator, Restorer, Bulwark, and Jammer signal carriers from waves 2-5; later formations mix those roles while Elite and Boss signals can briefly disrupt nearby tower groups. **Core Six** is an advanced Needle/Frost/Shard/Ember/Breaker/Beacon roster lock with 30% more opening credits. **Entrenched** keeps all ten towers but disables Pulse Plates, Charge Forge, Protocols, and selling with 10% more opening credits, making every placement permanent. **Sandbox Lab** is a solo-only setup choice with unlimited credits/lives and the real combat systems, but no checkpoint or run-history progression. Opening compensation is fixed at session start and never changes tower stats by wave or elapsed time.

- Sandbox Lab provides compact experiment controls. Choose one of the five enemy profiles, a 1-target/5-pack/12-swarm group, Standard/Elite/Boss rank, and Base/Wave-10/Wave-20/Immortal health. Manual targets use the displayed fixed health scale and base movement speed; Immortal targets receive real damage and statuses without losing health. The top bar can replay any of the selected arena's 30 authored campaign and Mastery waves with the selected difficulty's real scaling and timing. **Reset Test** clears targets, shots, lifetime tower metrics, and Protocol timers while preserving placed towers; **Clear Towers** removes the defense layout while preserving targets. A selected tower can be disabled without changing the layout or removed individually with its button/Delete. `[`/`]` selects the enemy, `G` changes group size, `K` changes rank, `H` changes health, `F` spawns targets, `R` resets the test, `C` clears towers, `D` toggles the selected tower, `-`/`+` selects an authored wave, Space sends it, and `E` tests or resets the selected tower's Protocol.

- Foundry Loop, Crosswind Basin, Prism Circuit, and Surge Divide each use a separately authored 20-wave campaign plus waves 21-30 as a harder, arena-specific Mastery extension. Their base starting credits are 400, 390, 380, and 360 before the selected difficulty modifier. Crosswind is runner-led and folds a continuous earth trail around three compact crossfire clearings.
- The setup-screen arena cards derive a compact campaign forecast directly from each map's wave JSON: opening threat mix, total contacts, peak wave density, final health multiplier, and first boss wave. Balance edits therefore update planning intel without a second hand-maintained description.
- Each arena has its own restrained environment and build-zone material: steel workyards around Foundry's molten channel, dark grass and static stones around Crosswind's earth trail, sparse crystal facets and chamfered platforms around Prism's light ribbon, and broad energy basins around Surge's cyan trench.
- Easy, Medium, Hard, and Bastion alter starting room and enemy health/speed through explicit profiles: Easy is 80% health / 95% speed / 125% credits / 30 lives; Medium is 90% / 98% / 112.5% / 24; Hard is the authored 100% / 100% / 100% / 20 baseline; Bastion is 112% / 102% / 100% / 18. The setup screen displays these exact modifiers; tower mechanics and stats never change by wave number, elapsed time, map, or difficulty.
- The live active/next-threat header shows effective `HP x...` and `SPD x...` values after combining the authored wave with the selected difficulty, making campaign and endless escalation visible during play.
- Surge Divide is intentionally the hardest arena: its stronger campaign and tighter opening economy pay for nine compact Surge Nodes with focused attack-speed, range, damage, or armor-piercing bonuses. Its route is a static cyan energy trench with no directional animation; Prism Circuit instead uses a continuous violet-and-cyan light ribbon.
- Hover a Surge Node for its exact radius and bonus. A tower's center must be inside the field; overlapping nodes use only the strongest bonus for each stat rather than stacking.
- While positioning a tower over a node, Tower Intel shows that node's name, bonus, and the exact base-to-boosted stat change. Selecting a deployed tower keeps the same node information alongside its active tower stats.
- Towers receiving a Signal Beacon aura keep their native ring color and carry a compact pulsing gold status pip inside the upper-right of their silhouette. Their Tower Intel identifies the Beacon and shows its exact base-to-boosted attack-rate and range changes separately from the combined active stats.
- Overlapping Signal Beacons do not stack additively. Attack rate and range independently use the strongest in-range Beacon, so two support towers can cover separate fronts or provide different best bonuses without creating exponential overlap. Linear upgrades, tier-two doctrines, and tier-three branch hover previews all include the selected tower's active Beacon and Surge Node modifiers in their displayed before/after values.
- End-run contribution bars credit Signal Beacons with the marginal damage-equivalent created by their attack-rate aura and credit Expose/Armor Break sources with the actual marginal damage those statuses enabled. Assist is labeled separately from direct damage and persists across save/load.
- Run History retains one evolving record per defense, including economy, early-call income, Protocol use, Pulse Plate deployments/damage, forged charges, defense time, leaks, and top-tower contribution. Continuing into endless updates that same record instead of creating duplicate campaign/endless entries.
- From **Load Saves**, open **Run History**, then **Medals & Records** to browse personal records, 28 repeatable run medals, and 56 career achievements. Medals recognize individual defenses, including Bastion directive clears and deep-Endless milestones. Achievements form separate Career, Directives, Mastery, Endurance, Arsenal, Operations, and Honors ladders; the overview identifies the next unfinished goal, and the final Total Command record requires completion across the major systems.
- Signal Beacon placement and selection previews show the full support-aura radius. Selecting any deployed tower shows that individual tower's lifetime damage and kills in Tower Intel, plus assisted damage for Beacons/Expose/Armor Break or source-attributed control enemy-seconds. These per-instance records survive saves and co-op resynchronization.
- The Charge Forge produces only while a wave is active. Its sidebar timer explicitly shows running, paused, or full storage.
- Pulse Plates snap anywhere across the visible route, push their triggering enemy backward, and briefly stun and slow every enemy in the blast. Their fixed 38 damage, two charges, and group control are identical in every wave, but the field is capped at 16 active plates. The high-contrast gold control leads with `DEPLOY` or `BUY` and always shows `FIELD x/16`; Forge storage/cadence stays on its own control. A 0.75-second per-enemy knockback grace prevents plate carpets from creating a domino lock; elites receive 60% push and bosses 25%. Direct buying is active-wave-only and rises by 15 credits after each additional purchase in the same wave, then resets to 60 at the next wave; stored Forge charges remain free to deploy.
- Tower level is built into every silhouette: level 1 has one inward spoke from the top, level 2 adds a spoke at 120 degrees, and level 3 adds one at 240 degrees. No hover, selection, or separate badge is required. At tier 3, the centered upward or downward triangle identifies the chosen first or second final role. Tier-2 doctrine remains in the selected tower's progression label rather than adding another battlefield glyph, preserving tower identity in dense layouts.
- Every tower chooses one of two tier-two doctrines, then one of two tier-three roles. The doctrine persists into either role, creating four mechanically distinct completed builds per tower without adding another level. Doctrine identity is visible in Tower Intel and survives saves, co-op commands, reconnect repair, and deterministic checksums.
- Entering Mastery after wave 20 unlocks one permanent Apex promotion for every completed tier-three tower. Each tower has an authored cost and role-preserving damage, cadence, range, projectile, or utility multipliers; Entrenched permits these permanent upgrades. Tower Intel previews the exact before/after values, the Tactical Library lists each promotion, and a thin light inner halo identifies promoted towers on the battlefield. Apex state is included in checkpoints, archived layouts, co-op repair snapshots, and deterministic checksums.
- Every tower also has a named, thematic Protocol. One tower may be armed for deterministic automatic activation when its trigger conditions are met; manual activation remains available for tactical timing.
- Frost Spire shots damage and slow every enemy in their impact radius. A dashed cyan enemy ring means **Slow**; a solid violet ring with a diamond means **Exposed**; paired gold chevrons mean **Armor Break**; and pulsing green squares mean **Stun**. These compact marks remain distinct when several effects overlap.
- Every splash projectile, Pulse Plate, and radius-based Protocol resolves with one crisp expanding ring at its actual gameplay radius, so area coverage is readable without particle clutter. Reduced-effects mode keeps only that essential radius cue.
- Breaker Cannon's final roles deliberately split heavy-target pressure from crowd utility: Piercing Round tracks its priority target, deals 1.5x damage to armored standards, elites, and bosses, and can punch through to one tightly packed escort; Armor Shatter retains more than twice the radius and can strike and armor-break four clustered enemies per impact.
- Siege Mortars predict enemy travel along the route before firing. Their shells use visible, level-specific impact caps (6 at level 1, 7 at level 2, 7 for Salvo, and 10 for Quake), so they remain excellent against dense groups without gaining unbounded damage from extreme endless-wave packing.
- Waves 15-20 apply a stronger health ramp so a successful opening build still needs additional late-game investment and coverage.
- Clearing wave 20 opens the campaign results with **Enter Mastery**, **Restart**, and **Main Menu**. Mastery resumes the same live battlefield during the wave-21 intermission, allowing transient attacks and effects to cool off naturally while defenses are prepared.
- Mastery waves 21-30 are authored per arena and deliberately require reinvestment beyond a campaign-winning layout. Their pressure rises continuously from the campaign capstone while Apex upgrades provide a compact alternative to filling every remaining build area. Generated Endless waves begin at wave 31 from that arena's Mastery roster, with accelerating health, capped speed/density/cadence, rotating elite pressure, and a recurring Bastion Core.
- After a defeat, **View Field** returns to the final battlefield in read-only inspection mode. Towers can be selected to review their level, effects, lifetime damage, and kills; **View Results** returns to the run summary.
- Apex Endless waves inherit the wave-30 Mastery roster and rotate balanced, runner, armored, regenerator, and recurring-boss themes. Health rises every wave, density and cadence grow within performance caps, speed has a conservative cap, and a stronger Bastion Core returns every fifth generated wave.

## Save slots

**Load Saves** on the main menu opens a paginated list showing mode, map, difficulty, directive, wave or endless depth, lives, credits, and save time. Exactly one clearly labeled **AUTOSAVE** is replaced after each completed wave. Numbered slots are manual saves created from the pause menu and are never selected or overwritten by automatic saving. The manual list expands as needed to slot 6, 7, and onward. Saving remains restricted to safe downtime between waves, when no enemies are active. Occupied or unreadable checkpoints can be permanently removed with a two-click delete confirmation, and gaps in the numbered list are reused by the next manual save.

Each checkpoint preserves the map, difficulty, directive, solo/co-op mode, economy and statistics, cleared-wave progress, endless status, towers and their owners/doctrines/final roles/targeting/cooldowns, Protocol state, Pulse Plates, and Charge Forge. Files are stored under `%LocalAppData%\MinimalBastion\Saves`; `autosave.json` is the rolling checkpoint and `slot-n.json` files are protected manual saves. Individual generations are bounded to 8 MiB and validated before reconstruction. Every overwrite retains one `.bak` recovery generation; if the primary JSON is missing, malformed, structurally invalid, oversized, or cannot be reconstructed against current authored content, loading transparently tries that recovery copy. Deleting a checkpoint removes both generations. The host alone writes co-op intermission saves; loading one immediately hosts the restored authoritative state and waits for player 2 to join. Unsupported checkpoint files appear as unreadable and can be deleted. If local storage is temporarily unavailable, gameplay continues; automatic saving reports one failure for that wave instead of retrying every frame, while a manual save or later wave can retry.

Restart requires confirmation and begins a fresh run on the same profile. Its first completed wave replaces the rolling autosave, while all numbered manual saves remain untouched. The confirmation footer states this explicitly in solo and co-op result/pause flows.

**Run History** inside Load Saves retains paginated victory and defeat summaries independently of checkpoints. It records arena, difficulty, solo/co-op mode, wave or endless depth, lives, kills, date, and top contributing tower. A wave-20 clear and its later endless conclusion share a persistent run identity across saves and co-op resynchronization, so endless continuation updates the same entry instead of creating duplicates. Individual records use a confirmed delete action and are stored with a recovery generation under `%LocalAppData%\MinimalBastion\History`; bounded files and validated records keep corrupt or implausibly large history data from disrupting the browser.

If Run History storage is unavailable at a result screen, the game makes one non-blocking write attempt for that terminal state instead of retrying every frame. Results and field inspection remain usable, and a later distinct conclusion can try again.

The setup summary surfaces the best locally recorded result for the currently selected arena, difficulty, and directive. A deep endless result is shown as `BEST ENDLESS n`; other profiles never leak into that comparison.

Runtime visuals are generated from crisp geometric primitives. The 1280x720 logical layout is rendered to a 2560x1440 scene with double-density fonts and shape masks, then linearly downsampled into a clipped 16:9 viewport. This keeps geometry and text smooth at fullscreen resolutions while preventing routes or effects from bleeding into letterbox bars. Dense-wave feedback is bounded to 384 transient effects plus eight protected co-op pings; tactical flashes displace beam noise at capacity. The centralized tactical theme uses muted terrain foundations, soft off-white and blue-gray UI surfaces, and controlled semantic accents; backbuffer MSAA remains disabled so resolution changes cannot gamma-shift those authored colors.

Enemy defeats use a brief six-segment geometric shatter. These cues are deliberately low priority and disappear before tactical area information when the dense-wave effect budget is saturated.

**Settings** is available from both the title and pause menus. It persists windowed/fullscreen mode, 1280x720 through 2560x1440 output presets, VSync, sound-effects/music volume, full/reduced geometric effects, and optional solo wave auto-start under `%LocalAppData%\MinimalBastion\settings.json`; writes are atomic and a last-known-good recovery generation is retained. Auto-start preserves manual setup for wave 1, then offers 0, 3, 5, or 10-second breaks before later solo waves. Choosing any automatic cadence is an advance commitment, so every automatic start—including the 10-second option—earns the same +20 credits; a manual call earns it only while the live early-call window remains open. Co-op continues to require both players. `F11` switches between the saved windowed output and borderless desktop fullscreen without changing logical layout or hitboxes. Output settings never alter the fixed tactical canvas or theme constants. Short UI/combat sounds are synthesized at runtime, including confirm/back/delete interface cues plus distinct boss-phase, victory, defeat, tactical-device, Protocol, and ten tower impact/support signatures. Per-type and global cadence limits reduce those signatures as battlefield density rises, so rapid fire does not become an audio wall. No external audio assets are required, and a missing audio device degrades safely to silent play. JSON under `src\MinimalBastion\ContentData` defines towers, enemies, maps, waves, difficulty profiles, and tactical systems; only the interface font is compiled through MonoGame content.

The procedural tactical bed uses independently voiced arrangements for the menu and each arena. It crossfades smoothly from restrained intermission ambience toward full intensity as live contacts accumulate, with a bounded boss peak. This mix response is presentation-only and never enters saves, checksums, or simulation timing.

An unexpected top-level failure writes `%LocalAppData%\MinimalBastion\Logs\latest-crash.log` before the process exits. The report contains the UTC time, game/build/runtime identifiers, OS architecture, and full exception, but no save contents, join code, or host address; each crash replaces the previous report so logs cannot grow without bound.

See [GAME_DESIGN.md](GAME_DESIGN.md), [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md), and [OVERNIGHT_CHANGELOG.md](OVERNIGHT_CHANGELOG.md) for the design and measured implementation state.
