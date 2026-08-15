# Minimal Bastion

Minimal Bastion is a colorful, data-driven 2D tower-defense game built with C#, .NET 10, and MonoGame DesktopGL. It includes two strategic maps, 20 mixed waves, 10 towers, branching specializations, elites and a phased final boss, active Overdrive, tactical Pulse Plates and a Charge Forge, dynamically expanding independent saves, post-run analysis, deterministic balance agents, and direct two-player online co-op.

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

The handshake rejects mismatched builds/content before a match starts. During play, the host sequences commands and sends periodic state checks; a mismatch triggers an authoritative state repair instead of ending the match. If Player 2 disconnects, the match pauses and remains on the host. Player 2 can restart the game if necessary, join with the same host address and six-character code, and receive the complete current wave, enemy, tower, economy, timer, and ready state.

Co-op uses shared credits, lives, plates, waves, and speed. Both players can upgrade, specialize, retarget, Overdrive, or sell any tower or Charge Forge; the P1/P2 ring records who originally placed it without restricting control. Both players must ready each wave, and the wave button plus sidebar show both ready states and the same early-call countdown. The host locks the +20 reward only when the second player readies before the timer expires; one early ready does not preserve the bonus indefinitely. Middle-click pings the battlefield.

At victory or defeat, **Restart Co-op** keeps both players connected and asks the host to initialize a fresh authoritative game on the same map. Both peers receive the new state before play resumes, wave-ready state is cleared, and the host assigns the restarted run a new save slot so the completed run is not overwritten. **Main Menu** remains the explicit action that ends the online session.

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
```

Reports are written under `.build\balance`. Supported filters include `--strategy`, `--seed`, `--runs`, `--map`, and `--output`.

Create a self-contained Windows build with:

```powershell
dotnet restore MinimalBastion.sln -r win-x64 --disable-build-servers
dotnet publish src\MinimalBastion -c Release -r win-x64 --self-contained true --no-restore -o .build\publish --disable-build-servers /nodeReuse:false /p:UseSharedCompilation=false
```

## Controls

- **Tower Library** is available from both the title screen and pause menu, so every level, cumulative cost, and final specialization can be compared before starting or while planning a run.
- Left click: select, place, or activate a UI control.
- Right click or Escape: cancel placement; Escape pauses in solo play.
- `1`-`0`: prepare the corresponding tower.
- `Q`: prepare a stored Pulse Plate. During an active wave, buy a replacement starting at 60 credits; each additional direct purchase in that same wave costs 15 more, and the price resets to 60 when the next wave starts.
- `G`: prepare or select the Charge Forge.
- `E`: Overdrive the selected combat tower.
- `U`: upgrade the selected tower or Charge Forge.
- `Delete`: sell the selected tower or Charge Forge.
- `T`: cycle the selected tower's targeting mode.
- `Space`: start/ready the next wave or claim the early-call reward.
- `S`: toggle 1x/2x speed.
- `P`: pause/resume in solo play.
- Middle click: send a co-op ping.
- In the co-op address/code fields, `Ctrl+C` copies the active field, `Ctrl+V` pastes, and holding Backspace continuously erases text.
- `F4`: toggle the debug overlay in Debug builds.

## Gameplay notes

- Foundry Loop starts with 400 credits. Surge Divide starts with 360 and contains nine compact Surge Nodes with focused attack-speed, range, damage, or armor-piercing bonuses.
- Hover a Surge Node for its exact radius and bonus. A tower's center must be inside the field; overlapping nodes use only the strongest bonus for each stat rather than stacking.
- While positioning a tower over a node, Tower Intel shows that node's name, bonus, and the exact base-to-boosted stat change. Selecting a deployed tower keeps the same node information alongside its active tower stats.
- Towers receiving a Signal Beacon aura keep their native ring color and carry a compact pulsing gold status pip inside the upper-right of their silhouette. Their Tower Intel identifies the Beacon and shows its exact base-to-boosted attack-rate and range changes separately from the combined active stats.
- Signal Beacon placement and selection previews show the full support-aura radius. Selecting any deployed tower also shows that individual tower's lifetime damage and kills in Tower Intel.
- The Charge Forge produces only while a wave is active. Its sidebar timer explicitly shows running, paused, or full storage.
- Pulse Plates snap anywhere across the visible road, push their triggering enemy backward, and briefly stun and slow every enemy in the blast. Their fixed 38 damage, two charges, and group control are identical in every wave, but the field is capped at 16 active plates. The tactical button always shows `FIELD x/16` separately from Forge `STORED x/capacity`. A 0.75-second per-enemy knockback grace prevents plate carpets from creating a domino lock; elites receive 60% push and bosses 25%. Direct buying is active-wave-only and rises by 15 credits after each additional purchase in the same wave, then resets to 60 at the next wave; stored Forge charges remain free to deploy.
- Tower level is built into every silhouette: level 1 has one inward spoke from the top, level 2 adds a spoke at 120 degrees, and level 3 adds one at 240 degrees. No hover, selection, or separate badge is required. At tier 3, the centered upward or downward triangle identifies the chosen first or second specialization option.
- Frost Spire shots damage and slow every enemy in their impact radius. A dashed cyan enemy ring means **Slow**; a solid violet ring with a diamond means **Exposed**, which increases damage received from every source for the shown duration.
- Siege Mortars predict enemy travel along the route before firing. Their reduced firepower keeps them useful against dense groups without overwhelming other late-game towers.
- Waves 15-20 apply a stronger health ramp so a successful opening build still needs additional late-game investment and coverage.
- Clearing wave 20 opens the campaign results with **Continue Endless**, **Restart**, and **Main Menu**. Continue resumes the same live battlefield during the wave-21 intermission, allowing transient attacks and effects to cool off naturally while defenses are prepared.
- After a defeat, **View Field** returns to the final battlefield in read-only inspection mode. Towers can be selected to review their level, effects, lifetime damage, and kills; **View Results** returns to the run summary.
- Endless waves retain the wave-20 combined-arms roster and rotate balanced, runner, armored, regenerator, and recurring-boss themes. Health rises every wave, density and cadence grow within performance caps, speed has a conservative cap, and a stronger Bastion Core returns every fifth endless wave.

## Save slots

**Load Saves** on the main menu opens a paginated list showing mode, map, wave or endless depth, lives, credits, and save time. The list expands as needed: new solo and hosted co-op runs claim the lowest empty slot, then create slot 6, 7, and onward rather than stopping or silently overwriting an existing deep endless defense. Solo players can explicitly save to or overwrite a chosen slot from the pause menu. Saving remains restricted to safe downtime between waves, when no enemies are active. Occupied and unreadable slots can be permanently removed with a two-click **Delete Slot / Confirm Delete** action, and the resulting gap is reused by the next new run.

Each slot preserves the map, solo/co-op mode, economy and statistics, cleared-wave progress, endless status, towers and their owners/upgrades/targeting/cooldowns, Overdrive state, Pulse Plates, and Charge Forge. Numbered save files are stored under `%LocalAppData%\MinimalBastion\Saves` and are limited in practice only by the filesystem and positive slot-number range. The host alone writes co-op intermission saves; loading one immediately hosts the restored authoritative state and waits for player 2 to join. An existing legacy `savegame.json` is copied safely into slot 1 when no new slots exist, while the original file remains untouched.

Runtime visuals are generated from crisp geometric primitives. The 1280x720 logical layout is rendered to a 2560x1440 scene with double-density fonts and shape masks, then linearly downsampled into a clipped 16:9 viewport. This keeps geometry and text smooth at fullscreen resolutions while preventing roads or effects from bleeding into letterbox bars. The centralized tactical theme uses a muted teal/slate foundation, soft off-white and blue-gray UI surfaces, and controlled semantic accents; backbuffer MSAA remains disabled so resolution changes cannot gamma-shift those authored colors. JSON under `src\MinimalBastion\ContentData` defines towers, enemies, maps, waves, and tactical systems; only the interface font is compiled through MonoGame content.

See [GAME_DESIGN.md](GAME_DESIGN.md), [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md), and [OVERNIGHT_CHANGELOG.md](OVERNIGHT_CHANGELOG.md) for the design and measured implementation state.
