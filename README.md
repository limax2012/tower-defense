# Minimal Bastion

Minimal Bastion is a colorful, data-driven 2D tower-defense game built with C#, .NET 10, and MonoGame DesktopGL. It includes two strategic maps, 20 mixed waves, 10 towers, branching specializations, elites and a phased final boss, active Overdrive, tactical Pulse Plates and a Charge Forge, checkpoint saves, post-run analysis, deterministic balance agents, and direct two-player online co-op.

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

Co-op uses shared credits, lives, plates, waves, and speed. Both players can upgrade, specialize, retarget, Overdrive, or sell any tower or Charge Forge; the P1/P2 ring records who originally placed it without restricting control. Both players must ready each wave, and the sidebar shows connection plus individual ready status. Middle-click pings the battlefield.

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

- Left click: select, place, or activate a UI control.
- Right click or Escape: cancel placement; Escape pauses in solo play.
- `1`-`0`: prepare the corresponding tower.
- `Q`: prepare a stored Pulse Plate, or buy one for 70 credits.
- `G`: prepare or select the Charge Forge.
- `E`: Overdrive the selected combat tower.
- `U`: upgrade the selected tower or Charge Forge.
- `Delete`: sell the selected tower or Charge Forge.
- `T`: cycle the selected tower's targeting mode.
- `Space`: start/ready the next wave or claim the early-call reward.
- `S`: toggle 1x/2x speed.
- `P`: pause/resume in solo play.
- Middle click: send a co-op ping.
- `F4`: toggle the debug overlay in Debug builds.

## Gameplay notes

- Foundry Loop starts with 400 credits. Surge Divide starts with 360 and contains nine compact Surge Nodes with focused attack-speed, range, damage, or armor-piercing bonuses.
- Hover a Surge Node for its exact radius and bonus. A tower's center must be inside the field; overlapping nodes use only the strongest bonus for each stat rather than stacking.
- While positioning a tower over a node, Tower Intel shows that node's name, bonus, and the exact base-to-boosted stat change. Selecting a deployed tower keeps the same node information alongside its active tower stats.
- The Charge Forge produces only while a wave is active. Its sidebar timer explicitly shows running, paused, or full storage.
- Pulse Plates have two charges and remember enemies they already handled, so one enemy cannot waste both pulses and consecutive crossings remain reliable.
- Tower level is built into every silhouette: level 1 has one inward spoke from the top, level 2 adds a spoke at 120 degrees, and level 3 adds one at 240 degrees. No hover, selection, or separate badge is required.
- Frost Spire shots damage and slow every enemy in their impact radius. A dashed cyan enemy ring means **Slow**; a solid violet ring with a diamond means **Exposed**, which increases damage received from every source for the shown duration.
- Siege Mortars predict enemy travel along the route before firing. Their reduced firepower keeps them useful against dense groups without overwhelming other late-game towers.
- Waves 15-20 apply a stronger health ramp so a successful opening build still needs additional late-game investment and coverage.
- After wave 20, **View Final Field** returns to the frozen battlefield for inspection; **View Results** reopens the run analysis.

## Save checkpoints

Solo games can be saved or loaded from the pause menu. A cleared wave also creates an automatic checkpoint, and **Continue Checkpoint** appears on the main menu when one exists. Checkpoints are deliberately restricted to downtime between waves, when no enemies are active, so combat projectiles and transient effects do not need to be approximated on load.

The checkpoint preserves the map, economy and statistics, cleared-wave progress, towers and their upgrades/targeting/cooldowns, Overdrive state, Pulse Plates, and Charge Forge. It is stored at `%LocalAppData%\MinimalBastion\savegame.json`. Online co-op does not write or load solo checkpoints.

Runtime visuals are generated from crisp geometric primitives. The 1280x720 logical layout is rendered to a 2560x1440 scene with double-density fonts and shape masks, then linearly downsampled into a clipped 16:9 viewport. This keeps geometry and text smooth at fullscreen resolutions while preventing roads or effects from bleeding into letterbox bars. The centralized tactical theme uses a muted teal/slate foundation, soft off-white and blue-gray UI surfaces, and controlled semantic accents; backbuffer MSAA remains disabled so resolution changes cannot gamma-shift those authored colors. JSON under `src\MinimalBastion\ContentData` defines towers, enemies, maps, waves, and tactical systems; only the interface font is compiled through MonoGame content.

See [GAME_DESIGN.md](GAME_DESIGN.md), [AUTONOMOUS_BALANCE.md](AUTONOMOUS_BALANCE.md), and [OVERNIGHT_CHANGELOG.md](OVERNIGHT_CHANGELOG.md) for the design and measured implementation state.
