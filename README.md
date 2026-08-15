# Minimal Bastion

Minimal Bastion is a colorful, data-driven 2D tower-defense game built with C#, .NET 10, and MonoGame DesktopGL. It includes four strategic maps with authored campaigns, four difficulty profiles, four decision-changing challenge directives, 20 mixed waves per map, 10 towers with 20 tier-two doctrines, 20 final specializations, and distinct tactical Protocols, elites and phased bosses, an in-game tower/threat/campaign tactical library, Pulse Plates and a Charge Forge, dynamically expanding independent saves, procedural audio, persistent display settings, post-run analysis, deterministic balance agents, and direct two-player online co-op.

## Play the verified build

Run:

```text
.build\publish\MinimalBastion.exe
```

The publish is self-contained for Windows x64; the .NET SDK is not required to play it.

## Online co-op

Online co-op is direct peer-to-peer TCP:

1. The host selects a map, chooses **Online Co-op**, then **Host Online Game**.
2. The host forwards TCP port `28741` on their router to the host PC and allows Minimal Bastion through the firewall if Windows asks.
3. The host shares their public IP address or DNS name and the six-character join code shown in-game.
4. The friend chooses **Online Co-op**, enters `address` or `address:port`, enters the join code, and joins.

No custom game server is required: the host PC is the server for this private two-player match. Direct internet play still requires TCP `28741` to reach the host, normally through router port forwarding and the Windows firewall. A peer VPN such as Tailscale or ZeroTier can be used instead of manual port forwarding. There is no matchmaking, hosted relay, automatic router configuration, or encrypted transport.

The host can click the large join-code field or press `Ctrl+C` in the waiting lobby to copy the code before sending it to their friend.

The handshake rejects mismatched compiled builds or any recursive JSON gameplay/campaign content before a match starts and times out half-open attempts after ten seconds. Ambiguous duplicate map/campaign identities and campaigns attached to the wrong arena are rejected while loading. Transport messages use bounded length-prefixed frames, a 64-frame send budget, and type/direction/content validation before gameplay dispatch, so malformed traffic cannot request unbounded allocation or enter the simulation. Large authoritative snapshots use bounded Brotli framing (2 MiB on the wire, 8 MiB after decoding), allowing dense endless defenses to reconnect without opening an unbounded decompression path. During play, the host sequences commands and sends periodic state checks; a mismatch triggers an authoritative state repair instead of ending the match, while a post-repair fence discards stale checksums that were already in flight. If Player 2 disconnects, the match pauses and remains on the host. Player 2 can restart the game if necessary, join with the same host address and six-character code, and receive the complete current wave, enemy, tower, economy, timer, pause, and ready state.

Co-op uses shared credits, lives, plates, waves, and speed. Both players can upgrade, specialize, retarget, activate or automate Protocols, or sell any tower or Charge Forge; the P1/P2 ring records who originally placed it without restricting control. Both players must ready each wave, and the wave button plus sidebar show both ready states and the same early-call countdown. The host locks the +20 reward only when the second player readies before the timer expires; one early ready does not preserve the bonus indefinitely. A compact P1/P2 crosshair shows the other player's live battlefield cursor, and four matching corner marks identify their selected tower without replacing its native colors; middle-click creates a more persistent location ping.

Either player can press Esc, P, or the HUD Pause button to pause/resume the shared deterministic simulation. Both peers stop on the same fixed tick, the banner identifies who paused, and tower placement and management remain available for joint planning. Beacon coverage and effective-stat previews refresh as the paused layout changes, but no combat, wave, economy, Forge, Protocol, cooldown, or visual-effect timer advances.

If the link drops—or valid inbound traffic disappears for 15 seconds—the match pauses and the client retries automatically. The host's preserved-session overlay keeps the six-character rejoin code visible and lets it be copied by clicking the code or pressing Ctrl+C.

At victory or defeat, **Restart Co-op** uses a two-click confirmation, keeps both players connected, and asks the host to initialize a fresh authoritative game on the same map. Both peers receive the new state before play resumes, wave-ready state is cleared, and the host assigns the restarted run a new save slot so the completed run is not overwritten. Solo restart is confirmed the same way. **Main Menu** remains the explicit action that ends the online session.

## Build and test

Install the .NET 10 SDK, or use the workspace-local SDK:

```powershell
$env:Path = "$PWD\.dotnet;$env:Path"
dotnet restore MinimalBastion.sln
dotnet build MinimalBastion.sln -c Release --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build
```

Run the isolated combat benchmark and deterministic full-game agents with:

```powershell
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --balance
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate --strategy Adaptive --seed 1337
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --runs 5
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --map relay_divide --runs 10
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --strategy LongRange "--force-build=siege_mortar:mortar_loader>quake_shell"
dotnet run --project tests\MinimalBastion.Tests -c Release --no-build -- --simulate-full --difficulty hard --no-protocols
```

Reports are written under `.build\balance`. Supported filters include `--strategy`, `--seed`, `--runs`, `--map`, `--difficulty`, `--challenge`, `--max-wave`, `--force-build`, `--no-protocols`, and `--output`. A forced build uses `tower:doctrine>specialization` and constrains that tower without changing other planning decisions; `--no-protocols` creates a matched active-ability control group. Tower reports list Protocol activations, doctrine, final-role, and completed-build-path usage, and distinguish direct damage from Signal Beacon damage-equivalent, recipient-seconds, and source-attributed Slow, Stun, Exposed, and Armor Break enemy-seconds so support/control value is not hidden behind raw kill totals.

Create a self-contained Windows build with:

```powershell
dotnet restore MinimalBastion.sln -r win-x64 --disable-build-servers
dotnet publish src\MinimalBastion -c Release -r win-x64 --self-contained true --no-restore -o .build\publish --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
```

## Controls

- **Tower Library** is available from both the title screen and pause menu. Click either tier-two doctrine to preview its exact stats, cumulative cost, and interaction with both final roles before starting or while planning a run.
- `Enter`: activate the focused action on the title, settings, pause, result, or save-slot screen. Arrow keys or `Tab` navigate title, pause, and result actions; Left/Right adjusts a focused arena, difficulty, directive, graphics option, effects volume, or music volume. Arrow keys also navigate save slots, run history, Settings, and Tactical Library entries. `Tab` cycles Tactical Library pages and switches between the co-op address/code fields.
- Online co-op: Up/Down selects Host, Join, or Back; Enter activates the focused action. Typing or selecting either connection field focuses Join, Ctrl+V pastes, Ctrl+C copies displayed host/rejoin codes, and held Backspace erases continuously.
- In Settings, Up/Down moves the visible focus, Left/Right adjusts the selected option, and Enter activates it.
- Left click: select, place, or activate a UI control.
- Right click or Escape: cancel placement; Escape pauses in solo play.
- `1`-`0`: prepare the corresponding tower.
- `Q`: prepare a stored Pulse Plate. During an active wave, buy a replacement starting at 60 credits; each additional direct purchase in that same wave costs 15 more, and the price resets to 60 when the next wave starts.
- `G`: prepare or select the Charge Forge.
- `E`: activate the selected tower's unique Protocol.
- `U`: upgrade the selected tower or Charge Forge.
- `Delete`: sell the selected tower or Charge Forge.
- `T`: cycle the selected tower's targeting mode.
- `Space`: start/ready the next wave or claim the early-call reward.
- `S`: toggle 1x/2x speed.
- `P`: pause/resume in solo play.
- Middle click: send a co-op ping.
- In the co-op address/code fields, `Ctrl+C` copies the active field, `Ctrl+V` pastes, holding Backspace continuously erases text, and `Tab` switches fields.
- `F4`: toggle the debug overlay in Debug builds.

## Gameplay notes

- The title selector separates arena, difficulty, and directive. **Standard** enables every system; **Close Quarters** removes Watchtower/Mortar; **Core Six** limits the roster to Needle/Frost/Shard/Ember/Breaker/Beacon; and **No Reserves** disables Pulse Plates/Forge. Fixed opening-credit compensation belongs to the directive and never changes tower stats by wave or elapsed time. Directive identity persists through saves, co-op, results, and run history.

- Foundry Loop, Crosswind Basin, Prism Circuit, and Surge Divide each use a separately authored 20-wave roster matched to their route geometry. Their base starting credits are 400, 390, 380, and 360 before the selected difficulty modifier. Crosswind is runner-led and folds a continuous channel around three compact crossfire islands.
- The title-screen arena selector derives a compact campaign forecast directly from that map's wave JSON: opening threat mix, total contacts, peak wave density, final health multiplier, and first boss wave. Balance edits therefore update planning intel without a second hand-maintained description.
- Each arena also declares its own restrained battlefield motif: structural braces for Foundry, current chevrons for Crosswind, facets for Prism, and circuit traces for Surge. These marks sit below routes, nodes, ranges, and targeting geometry so map identity does not reduce tactical readability.
- Easy, Normal, Hard, and Bastion alter starting room and enemy health/speed through explicit profiles: Easy is 80% health / 95% speed / 125% credits / 30 lives; Normal is 90% / 98% / 112.5% / 24; Hard is the authored 100% / 100% / 100% / 20 baseline; Bastion is 115% / 104% / 100% / 15. The title screen displays these exact modifiers; tower mechanics and stats never change by wave number, elapsed time, map, or difficulty.
- Surge Divide is intentionally the hardest arena: its stronger campaign and tighter opening economy pay for nine compact Surge Nodes with focused attack-speed, range, damage, or armor-piercing bonuses. Prism Circuit provides three restrained nodes and a distinct conduit path.
- Hover a Surge Node for its exact radius and bonus. A tower's center must be inside the field; overlapping nodes use only the strongest bonus for each stat rather than stacking.
- While positioning a tower over a node, Tower Intel shows that node's name, bonus, and the exact base-to-boosted stat change. Selecting a deployed tower keeps the same node information alongside its active tower stats.
- Towers receiving a Signal Beacon aura keep their native ring color and carry a compact pulsing gold status pip inside the upper-right of their silhouette. Their Tower Intel identifies the Beacon and shows its exact base-to-boosted attack-rate and range changes separately from the combined active stats.
- Overlapping Signal Beacons do not stack additively. Attack rate and range independently use the strongest in-range Beacon, so two support towers can cover separate fronts or provide different best bonuses without creating exponential overlap. Linear upgrades, tier-two doctrines, and tier-three branch hover previews all include the selected tower's active Beacon and Surge Node modifiers in their displayed before/after values.
- End-run contribution bars credit Signal Beacons with the marginal damage-equivalent created by their attack-rate aura and credit Expose/Armor Break sources with the actual marginal damage those statuses enabled. Assist remains labeled separately from direct damage and stable across save/load.
- Signal Beacon placement and selection previews show the full support-aura radius. Selecting any deployed tower shows that individual tower's lifetime damage and kills in Tower Intel, plus assisted damage for Beacons/Expose/Armor Break or source-attributed control enemy-seconds. These per-instance records survive saves and co-op resynchronization.
- The Charge Forge produces only while a wave is active. Its sidebar timer explicitly shows running, paused, or full storage.
- Pulse Plates snap anywhere across the visible road, push their triggering enemy backward, and briefly stun and slow every enemy in the blast. Their fixed 38 damage, two charges, and group control are identical in every wave, but the field is capped at 16 active plates. The tactical button always shows `FIELD x/16` separately from Forge `STORED x/capacity`. A 0.75-second per-enemy knockback grace prevents plate carpets from creating a domino lock; elites receive 60% push and bosses 25%. Direct buying is active-wave-only and rises by 15 credits after each additional purchase in the same wave, then resets to 60 at the next wave; stored Forge charges remain free to deploy.
- Tower level is built into every silhouette: level 1 has one inward spoke from the top, level 2 adds a spoke at 120 degrees, and level 3 adds one at 240 degrees. No hover, selection, or separate badge is required. At tier 3, the centered upward or downward triangle identifies the chosen first or second final role.
- Every tower chooses one of two tier-two doctrines, then one of two tier-three roles. The doctrine persists into either role, creating four mechanically distinct completed builds per tower without adding another level. Doctrine identity is visible in Tower Intel and survives saves, co-op commands, reconnect repair, and deterministic checksums.
- Every tower also has a named, thematic Protocol. One tower may be armed for deterministic automatic activation when its trigger conditions are met; manual activation remains available for tactical timing.
- Frost Spire shots damage and slow every enemy in their impact radius. A dashed cyan enemy ring means **Slow**; a solid violet ring with a diamond means **Exposed**; paired gold chevrons mean **Armor Break**; and pulsing green squares mean **Stun**. These compact marks remain distinct when several effects overlap.
- Every splash projectile resolves with one crisp expanding ring at its actual gameplay radius, so Frost, Ember, Breaker, and Mortar coverage is readable without particle clutter. Reduced-effects mode keeps only that essential radius cue.
- Breaker Cannon's final roles deliberately split heavy-target pressure from crowd utility: Breach Round deals 1.5x damage to armored standards, elites, and bosses, while Shatter Shell can strike and armor-break four clustered enemies per impact.
- Siege Mortars predict enemy travel along the route before firing. Their shells use visible, level-specific impact caps (6 at level 1, 7 at level 2, 7 for Salvo, and 10 for Quake), so they remain excellent against dense groups without gaining unbounded damage from extreme endless-wave packing.
- Waves 15-20 apply a stronger health ramp so a successful opening build still needs additional late-game investment and coverage.
- Clearing wave 20 opens the campaign results with **Continue Endless**, **Restart**, and **Main Menu**. Continue resumes the same live battlefield during the wave-21 intermission, allowing transient attacks and effects to cool off naturally while defenses are prepared.
- Endless generation starts from the selected arena's own authored final roster, so Crosswind, Foundry, Prism, and Surge retain distinct formations after wave 20. Health growth accelerates, speed rises to a cap, roster density and cadence are performance-bounded, elite pressure increases slowly, and the Bastion Core returns every fifth wave.
- After a defeat, **View Field** returns to the final battlefield in read-only inspection mode. Towers can be selected to review their level, effects, lifetime damage, and kills; **View Results** returns to the run summary.
- Endless waves retain the wave-20 combined-arms roster and rotate balanced, runner, armored, regenerator, and recurring-boss themes. Health rises every wave, density and cadence grow within performance caps, speed has a conservative cap, and a stronger Bastion Core returns every fifth endless wave.

## Save slots

**Load Saves** on the main menu opens a paginated list showing mode, map, wave or endless depth, lives, credits, and save time. The list expands as needed: new solo and hosted co-op runs claim the lowest empty slot, then create slot 6, 7, and onward rather than stopping or silently overwriting an existing deep endless defense. Solo players can explicitly save to or overwrite a chosen slot from the pause menu. Saving remains restricted to safe downtime between waves, when no enemies are active. Occupied and unreadable slots can be permanently removed with a two-click **Delete Slot / Confirm Delete** action, and the resulting gap is reused by the next new run.

Each slot preserves the map, solo/co-op mode, economy and statistics, cleared-wave progress, endless status, towers and their owners/doctrines/final roles/targeting/cooldowns, Protocol state, Pulse Plates, and Charge Forge. Numbered save files are stored under `%LocalAppData%\MinimalBastion\Saves` and are limited in practice only by the filesystem and positive slot-number range. Individual generations are bounded to 8 MiB and validated collection limits before reconstruction, so a corrupt local file cannot request implausible allocations; this does not limit the number of slots. Every overwrite retains one `.bak` recovery generation; if the primary JSON is missing, malformed, structurally invalid, oversized, or cannot be reconstructed against the current authored content, loading transparently tries that recovery copy, and a later save never replaces a known-good backup with the bad primary. Deleting a slot removes both generations. The host alone writes co-op intermission saves; loading one immediately hosts the restored authoritative state and waits for player 2 to join. An existing legacy `savegame.json` is copied safely into slot 1 when no new slots exist, while the original file remains untouched. If local save storage is temporarily unavailable, gameplay still starts as an unslotted run; automatic saving reports one failure for that wave instead of retrying every frame, while a manual save or later wave can retry.

**Run History** inside Load Saves retains paginated victory and defeat summaries independently of checkpoints. It records arena, difficulty, solo/co-op mode, wave or endless depth, lives, kills, date, and top contributing tower. A wave-20 clear and its later endless conclusion share a persistent run identity across saves and co-op resynchronization, so endless continuation updates the same entry instead of creating duplicates. Individual records use a confirmed delete action and are stored with a recovery generation under `%LocalAppData%\MinimalBastion\History`; bounded files and validated records keep corrupt or implausibly large history data from disrupting the browser.

Runtime visuals are generated from crisp geometric primitives. The 1280x720 logical layout is rendered to a 2560x1440 scene with double-density fonts and shape masks, then linearly downsampled into a clipped 16:9 viewport. This keeps geometry and text smooth at fullscreen resolutions while preventing roads or effects from bleeding into letterbox bars. Dense-wave feedback is bounded to 384 transient effects plus eight protected co-op pings; tactical flashes displace beam noise at capacity. The centralized tactical theme uses a muted teal/slate foundation, soft off-white and blue-gray UI surfaces, and controlled semantic accents; backbuffer MSAA remains disabled so resolution changes cannot gamma-shift those authored colors.

**Settings** is available from both the title and pause menus. It persists windowed/fullscreen mode, 1280x720 through 2560x1440 output presets, VSync, sound-effects volume, and full/reduced geometric effects under `%LocalAppData%\MinimalBastion\settings.json`; writes are atomic and a last-known-good recovery generation is retained. Output settings never alter the fixed tactical canvas or theme constants. Short UI/combat sounds are synthesized at runtime, including distinct boss-phase, victory, defeat, tactical-device, and tower-specific Protocol cues; individual rapid-fire shots remain silent so dense defenses do not become an audio wall. No external audio assets are required, and a missing audio device degrades safely to silent play. JSON under `src\MinimalBastion\ContentData` defines towers, enemies, maps, waves, difficulty profiles, and tactical systems; only the interface font is compiled through MonoGame content.

An unexpected top-level failure writes `%LocalAppData%\MinimalBastion\Logs\latest-crash.log` before the process exits. The report contains the UTC time, game/build/runtime identifiers, OS architecture, and full exception, but no save contents, join code, or host address; each crash replaces the previous report so logs cannot grow without bound.

See [GAME_DESIGN.md](GAME_DESIGN.md), [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md), and [OVERNIGHT_CHANGELOG.md](OVERNIGHT_CHANGELOG.md) for the design and measured implementation state.
