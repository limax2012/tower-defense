# Minimal Bastion — Implementation-Ready Game Design and Technical Blueprint

> Historical version-1 implementation blueprint. For the current implemented game, online co-op architecture, balance results, and change history, see `README.md`, `GAME_DESIGN.md`, `AUTONOMOUS_BALANCE.md`, and `OVERNIGHT_CHANGELOG.md`.

Status: planning specification for version 1
Target: small single-player 2D desktop tower-defense game
Logical resolution: 1280 x 720
Initial map: Foundry Loop
Initial match length: approximately 18–25 minutes, including intermissions

This document is the implementation contract for the first playable version. It makes concrete decisions where a later engineer would otherwise have to redesign a system. It intentionally favors a small number of readable classes, data-driven content, and straightforward update loops over engine-like abstractions.

## 1. Executive technical summary

Minimal Bastion is a single-process C# MonoGame DesktopGL game. The game renders a 960-pixel-wide tactical board beside a 320-pixel-wide UI sidebar, all inside a 1280 x 720 logical canvas. The physical window may be resized, but the logical canvas is uniformly scaled and letterboxed so mouse coordinates and balance remain stable.

The game has five top-level states: `MainMenu`, `Playing`, `Paused`, `Victory`, and `Defeat`. Tower placement is a substate of `Playing`, not a separate global state. A `GameSession` owns the current map, economy, wave manager, enemy list, tower list, projectile list, effects, and match result. `Game1` only owns MonoGame lifecycle, graphics setup, and delegation.

Content is split into:

- JSON definitions for maps and waves, so a second map can be added without gameplay-code edits.
- JSON definitions for tower and enemy numeric data, with behavior identifiers mapped to small C# behavior modules.
- One compiled `SpriteFont` for UI text.
- Runtime-generated primitive textures; no sprite sheets, art packages, or third-party gameplay libraries.

The initial content contains:

- 20 waves.
- 10 towers, each with three total levels.
- 5 enemy tiers.
- A single path with nine turns and approximately 20 sensible tower locations.
- First/Last/Strongest/Weakest/Nearest targeting.
- Straight projectiles, splash projectiles, chain attacks, a beam, damage-over-time, slowing, armor reduction, area buffs, health bars, pause, 1x/2x speed, victory, defeat, and restart.

The intended implementation style is a conventional object-oriented game loop:

```text
Game1.Update
  -> InputRouter.Update
  -> UIManager.HandleInput
  -> GameSession.Update
       -> WaveManager.Update
       -> EnemySystem.Update
       -> TowerSystem.Update
       -> ProjectileSystem.Update
       -> StatusEffectSystem.Update
       -> EffectsSystem.Update
       -> cleanup and result checks

Game1.Draw
  -> PrimitiveRenderer / SpriteBatch
  -> map
  -> path and entities
  -> range/placement overlays
  -> HUD and panels
  -> state overlays
```

No networking, ECS, map editor, physics engine, database, or elaborate animation framework is included in version 1.

## 2. Technology choice and project configuration

### Chosen stack

- Language: C#.
- Runtime/SDK: .NET 10, `net10.0` target.
- Framework: MonoGame DesktopGL 3.8.5, pinned exactly for the initial implementation.
- Build: standard `dotnet restore`, `dotnet build`, and `dotnet run`.
- Content: `MonoGame.Content.Builder.Task` at the same pinned MonoGame version, only for the SpriteFont.
- Data serialization: `System.Text.Json`, included in .NET.
- Tests: a small dependency-free test runner project using ordinary assertions and `dotnet run`.
- Source control: Git, with `bin/`, `obj/`, generated content output, and user settings ignored.

MonoGame 3.8.5 is the selected stable baseline at the time this plan is written. The DesktopGL package targets .NET 8 and is compatible with later target frameworks; .NET 10 is selected because it is the current LTS line in this project’s target environment. If implementation begins after a newer stable MonoGame release, update the framework and content-builder packages together and rerun the smoke-test checklist rather than mixing package versions.

### Project shape

Use one executable project and one optional lightweight test project:

```text
MinimalBastion.sln
  src/MinimalBastion/MinimalBastion.csproj
  tests/MinimalBastion.Tests/MinimalBastion.Tests.csproj
```

The executable project should contain the equivalent of:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>AnyCPU</Platforms>
    <ApplicationIcon />
    <AssemblyName>MinimalBastion</AssemblyName>
    <RootNamespace>MinimalBastion</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.5" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.5" />
  </ItemGroup>

  <ItemGroup>
    <None Update="ContentData\\**\\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

The exact generated project properties may differ slightly depending on the MonoGame template. Preserve the essential decisions: `net10.0`, DesktopGL, pinned package versions, and data files copied next to the executable.

### Build and run contract

From the repository root:

```text
dotnet restore
dotnet build -c Debug
dotnet run --project src/MinimalBastion -c Debug
dotnet build -c Release
dotnet run --project tests/MinimalBastion.Tests -c Release
```

For a Windows self-contained build, use a documented publish command such as:

```text
dotnet publish src/MinimalBastion -c Release -r win-x64 --self-contained true
```

The first distribution target is Windows x64. DesktopGL keeps the core code portable for Linux and macOS later, but version 1 does not need platform-specific branches beyond the MonoGame project target.

## 3. Core gameplay loop

1. The player starts a match with 20 lives and 400 credits.
2. The player buys and places towers in legal build regions around the path.
3. The player starts wave 1 using the `START WAVE` button. A ten-second intermission countdown is used between later waves.
4. Enemies spawn in data-defined groups, follow the path, and attempt to reach the goal.
5. Towers independently acquire targets according to a shared targeting system and attack using their behavior module.
6. Killed enemies award credits. Enemies that reach the goal remove lives and award nothing.
7. The player can pause, switch between 1x and 2x simulation speed, select towers, change targeting, upgrade, sell, or cancel placement.
8. Clearing all enemies in wave 20 produces victory. Lives reaching zero produces defeat.
9. Victory and defeat provide a `RESTART` button that creates a fresh `GameSession` from the same map data.

The player is never required to micromanage attacks. Strategic decisions are placement, tower composition, upgrades, targeting, and timing of wave starts.

## 4. Exact starting game rules

| Rule | Initial value |
|---|---:|
| Starting lives | 20 |
| Starting credits | 400 |
| Waves | 20 |
| Map board | 960 x 720 logical pixels |
| UI sidebar | x = 960 to 1279 |
| Tower footprint radius | 18 px |
| Minimum center gap between towers | 40 px |
| Path visual width | 56 px |
| Placement clearance from path centerline | 50 px |
| Starting wave intermission | 10 s |
| Early-start reward | 20 credits if started before the intermission expires |
| Wave completion bonus | `40 + 10 * waveNumber` credits |
| Speed controls | 1x and 2x |
| Pause key | Escape or P |
| Lives lost by enemy | tier-specific, in the enemy table |
| Sell return | 60% of total invested credits (purchase plus upgrades), rounded down |

The player can place towers before wave 1. Starting with 400 credits permits four low-cost towers with a small reserve, making a basic four-turret opening reliable without removing the need to spend carefully later. The first wave’s possible credits are 50 wave bonus plus the rewards from eight T1 enemies, so an error is recoverable but not free.

All match timing uses `GameTimeSeconds`, not frame counts. Wave countdowns, attack cooldowns, projectile movement, status durations, and particles are multiplied by the selected speed and updated from elapsed time.

## 5. Complete 10-tower roster with numerical stats

### Common stat definitions

- `Damage` is damage per hit or per beam pulse, before enemy mitigation.
- `APS` is attacks per second. Cooldown is `1 / APS`.
- `Range` is logical pixels from the tower center.
- Costs in the `L2` and `L3` columns are upgrade costs, not cumulative costs.
- A projectile speed of `—` means instant hit, a chain event, or a non-attacking support effect.
- Towers use `First` as their default target unless listed otherwise.
- All tower level definitions are immutable data; a placed tower stores only its definition ID, level, position, cooldown, target mode, and invested credits.

| # | Tower | Role and visual language | Buy | L1 stats | L2 upgrade / stats | L3 upgrade / stats |
|---:|---|---|---:|---|---|---|
| 1 | Needle Turret | Cheap generalist. Small blue square body with two thin barrels. | 90 | R125, D8, 1.8 APS, projectile 450; single target. | 55; R135, D10, 2.0 APS, projectile 480. | 85; R145, D13, 2.3 APS, projectile 520. |
| 2 | Shard Fan | Close anti-swarm. Orange triangle with a three-pronged muzzle. | 150 | R110, 7 damage x 3 pellets, 0.65 APS, projectile 380, 18° spread. | 90; R115, 8 x 4 pellets, 0.72 APS, spread 20°. | 135; R120, 10 x 5 pellets, 0.80 APS, spread 22°. |
| 3 | Watchtower | Long-range single-target specialist. Tall violet rectangle with a bright lens. | 190 | R250, D38, 0.45 APS, projectile 800; Strongest default. | 120; R270, D58, 0.48 APS, projectile 850. | 185; R290, D90, 0.52 APS, projectile 900; 6 armor pierce. |
| 4 | Frost Spire | Crowd control. Pale cyan diamond with an outer ring. | 140 | R150, D4, 0.80 APS, projectile 350; 30% slow for 2.0 s. | 90; R160, D6, 0.85 APS; 38% slow for 2.2 s. | 145; R170, D8, 0.90 APS; 45% slow for 2.5 s. |
| 5 | Ember Coil | Damage-over-time. Red ring around a dark hexagon. | 220 | R155, D12, 0.75 APS, projectile 400; burn 8/s for 3.0 s. | 140; R165, D16, 0.82 APS; burn 12/s for 3.5 s. | 210; R175, D21, 0.90 APS; burn 18/s for 4.0 s, 18 px hit splash. |
| 6 | Breaker Cannon | Armor counter. Heavy yellow square with a black bore and side plates. | 250 | R165, D24, 0.70 APS, projectile 520; ignores 6 armor. | 160; R175, D34, 0.76 APS; ignores 10 armor. | 240; R185, D48, 0.82 APS; ignores 14 armor and applies -4 armor for 4 s. |
| 7 | Arc Relay | Chain damage. Green circle with three radial rods. | 320 | R155, D18, instant; jumps to 2 additional enemies, 12 damage each, jump range 90. | 220; R165, D26; 3 jumps, 17 damage each, jump range 100. | 330; R175, D38; 4 jumps, 25 damage each, jump range 110; 0.4 s stun on the primary target. |
| 8 | Siege Mortar | Long-range area damage. Large dark circle with a red center and aiming line. | 360 | R275, D55, 0.32 APS, projectile 220; 52 px splash. | 240; R285, D78, 0.36 APS; 60 px splash. | 360; R300, D110, 0.40 APS; 70 px splash and 20% slow for 1.0 s. |
| 9 | Prism Beam | Expensive sustained single-target beam. Magenta diamond with a thin rotating line. | 450 | R205, D7 per pulse, 4.0 pulses/s; instant; applies Exposed +10% damage taken for 2 s. | 300; R220, D10 per pulse, 4.5 pulses/s; Exposed +15%. | 450; R235, D14 per pulse, 5.0 pulses/s; Exposed +20%; beam ignores shields. |
| 10 | Signal Beacon | Support tower. White-and-gold ring with a central mast; no attack projectile. | 300 | R140 aura; nearby towers gain +10% APS and +8% range. | 210; R155 aura; +17% APS and +12% range. | 330; R175 aura; +25% APS and +18% range. |

### Roles, strengths, and weaknesses

| Tower | Strengths | Weaknesses and intended constraint |
|---|---|---|
| Needle Turret | Best early value, reliable against T1/T2, cheap to upgrade. | Low per-hit damage and poor against armor. |
| Shard Fan | Excellent when the path creates a dense pack. | Short range, spread reduces single-target reliability, weak against sparse T4/T5. |
| Watchtower | Reaches important path turns and removes high-value targets. | Low fire rate and inefficient against many weak enemies. |
| Frost Spire | Makes every nearby tower more effective by extending enemy exposure time. | Low direct DPS; cannot stack its slow with another Frost Spire. |
| Ember Coil | Burn continues while the tower changes targets; good against high health. | Damage-over-time is less useful if enemies die in one hit; burn is reduced by T5 regeneration only after mitigation. |
| Breaker Cannon | Best direct answer to armored T3/T4 and late T5. | Slower and more expensive than a generalist. |
| Arc Relay | Clears tightly packed groups without projectile travel time. | Chain range and primary range limit it on spread-out path sections. |
| Siege Mortar | High splash value and long range; can cover two turns. | Slow projectile and fire rate; bad placement can miss fast T2. |
| Prism Beam | Stable damage on one important target; Exposed rewards focused fire and bypasses shields at L3. | Very expensive and single-target; does not solve a swarm. |
| Signal Beacon | Multiplies a cluster of existing towers and saves space. | Does no damage, has diminishing returns because only the strongest overlapping aura applies. |

### Targeting defaults

- Needle Turret: First.
- Shard Fan: First.
- Watchtower: Strongest.
- Frost Spire: First.
- Ember Coil: First, but will not retarget away from a burning target until the target dies or exits range.
- Breaker Cannon: Strongest.
- Arc Relay: First; chain targets are selected by highest progress within jump range.
- Siege Mortar: First, using the lead point of the target group.
- Prism Beam: Strongest.
- Signal Beacon: no target mode.

The player may cycle `First`, `Last`, `Strongest`, `Weakest`, and `Nearest` for attacking towers. The UI hides modes that do not apply to a support tower. A behavior may impose a final constraint—for example, a chain attack always chooses additional targets by progress—but it still obtains its primary target through the shared selector.

## 6. Complete 5-enemy-tier roster with numerical stats

Enemy base values are multiplied by the wave’s `healthMultiplier` and `speedMultiplier`. Rewards and lives are not multiplied. Armor is flat physical damage reduction after shield absorption, with a minimum of 1 damage from ordinary attacks unless the hit is completely blocked by a special rule.

| Tier | Name and visual | Base HP | Speed | Reward | Lives | Armor / special | Intended waves |
|---:|---|---:|---:|---:|---:|---|---|
| T1 | Crawler: small blue circle with one white notch; size 14. | 70 | 70 px/s | 8 | 1 | No armor or special behavior. | 1–20 |
| T2 | Runner: orange diamond with a double inner stripe; size 12. | 55 | 125 px/s | 10 | 1 | Fast; uses the same path and collision rules. | 3–20 |
| T3 | Brute: red hexagon with a thick dark outline and two side plates; size 21. | 260 | 48 px/s | 25 | 4 | Flat armor 4; clearly larger and slower. | 6–20 |
| T4 | Aegis: purple octagon with a cyan outer ring and a center square; size 24. | 520 | 62 px/s | 45 | 8 | Starts with 100-point shield. Shield absorbs damage before armor and does not recharge. | 10–20 |
| T5 | Regenerator: black star with five red points and a pulsing green ring; size 26. | 800 | 42 px/s | 75 | 5 | Regenerates 18 HP/s while alive; regeneration pauses for 1.0 s after taking damage. | 14–20 |

Enemy tiers communicate strength using shape, size, outline, internal markings, and movement speed as well as color. Color is a supplement, not the only identifier. T5’s pulsing ring is a simple scale/alpha effect, not a sprite animation.

### Wave scaling

For wave `w` from 1 through 20:

```text
healthMultiplier = 1.0 + 0.035 * (w - 1)
speedMultiplier  = min(1.10, 1.0 + 0.005 * (w - 1))
```

Thus wave 20 uses 1.665x base health and 1.095x base speed. Tier unlocks and increasing group quantities do most of the difficulty work; the multiplier provides a smooth reason for early enemies to remain relevant late in the match.

## 7. Upgrade system

Every tower has exactly three levels in version 1. Level 1 is the purchase state; level 2 and level 3 each require one explicit upgrade. The selected-tower panel displays the complete next-level delta before purchase.

Upgrade rules:

- The tower must be selected and the player must have enough credits.
- The upgrade is applied immediately; the current attack cooldown is not reset.
- Upgrades may occur during a wave or intermission.
- A level 3 tower has no upgrade button.
- `InvestedCredits` increases by the upgrade cost.
- Sell value is 60% of `InvestedCredits`, rounded down.
- A Signal Beacon’s aura uses upgraded values immediately and recalculates each update, so moving is not supported and no stale buff state can remain.

The upgrade costs are intentionally 55–450 credits. Level 2 upgrades generally cost 45–65% of purchase price; level 3 upgrades cost 75–115% of purchase price. This makes early towers attractive to develop without allowing every tower to be maxed immediately.

## 8. Economy and balance model

### Credit sources

| Source | Formula / value |
|---|---:|
| Starting credits | 400 |
| T1 kill | 8 |
| T2 kill | 10 |
| T3 kill | 25 |
| T4 kill | 45 |
| T5 kill | 75 |
| Wave completion | `40 + 10 * waveNumber` |
| Early start | 20 once per wave if started before countdown expires |
| Escape | no reward |

If every enemy in the 20-wave roster is killed, the roster yields 22,270 kill credits. Wave completion yields 2,900 credits. Together with the starting 400 and 400 credits of early-start rewards, the theoretical total is 25,970 credits. A sensible first match should lose some enemies and spend heavily, so the expected usable amount is approximately 17,000–22,000 credits.

The complete purchase cost of one copy of every tower is 2,420 credits. Maxing one copy of every tower requires another 3,970 credits, for a fully maxed demonstration roster of 6,390 credits. The map is designed for roughly 18–22 towers; normal play should prioritize a smaller strong network rather than buying all ten immediately.

### Intended economic progression

- Opening: 400 credits supports four low-cost towers with a small reserve, or one strong tower plus two cheap towers. Four Needle Turrets form a reliable first-wave baseline; mixed openings remain viable for players who want control or range.
- Waves 1–5: about 1,000–1,500 additional credits are available. The player can establish a core of 5–7 level 1 towers and begin two upgrades.
- Waves 6–10: T3 and T4 introduce armor and shields. The player should be able to add Breaker Cannon or Watchtower and upgrade a generalist cluster.
- Waves 11–15: splash, chain, burn, and support become more valuable as groups grow. One expensive tower should be affordable without making all cheaper towers obsolete.
- Waves 16–20: T5 regeneration and high health make level 3 Breaker Cannon, Prism Beam, Watchtower, or a Beacon-supported combination valuable. The final waves should require composition and placement, not only buying the most expensive tower.

### Selling

Selling returns 60% of total invested credits: the purchase cost plus every upgrade cost. The result is always rounded down to an integer. Upgrade costs count because they represent permanent investment and counting them makes experimentation less punishing. The 40% loss still prevents free repositioning every wave.

## 9. Wave design and complete initial wave progression

The wave system is data-driven. Each wave is an ordered list of groups. A group has an enemy ID, count, spawn interval, and delay before it begins. Groups do not overlap unless the data explicitly uses a zero delay and the current group has not completed; the first map uses sequential groups for readability.

Notation in the table is `enemy x count @ spawn interval`, with `gap` meaning seconds after the previous group has finished spawning.

| Wave | Groups, in order | Purpose |
|---:|---|---|
| 1 | T1 x 8 @ 0.90s | Tutorial swarm; tests placement. |
| 2 | T1 x 10 @ 0.85s | Slightly denser opening. |
| 3 | T1 x 12 @ 0.80s; gap 3s; T2 x 2 @ 1.00s | First fast enemies. |
| 4 | T1 x 14 @ 0.78s; gap 2.5s; T2 x 4 @ 0.85s | Fast tail. |
| 5 | T1 x 18 @ 0.76s; gap 2s; T2 x 6 @ 0.80s | First meaningful density test. |
| 6 | T1 x 16 @ 0.74s; gap 2s; T2 x 8 @ 0.80s; gap 3s; T3 x 2 @ 1.20s | First armored Brutes. |
| 7 | T1 x 20 @ 0.72s; gap 2s; T2 x 10 @ 0.76s; gap 3s; T3 x 4 @ 1.10s | Mixed armor introduction. |
| 8 | T1 x 18 @ 0.70s; gap 2s; T2 x 12 @ 0.74s; gap 3s; T3 x 6 @ 1.05s | More simultaneous pressure. |
| 9 | T1 x 22 @ 0.68s; gap 2s; T2 x 14 @ 0.72s; gap 3s; T3 x 8 @ 1.00s | Last pre-shield preparation wave. |
| 10 | T1 x 20 @ 0.66s; gap 2s; T2 x 16 @ 0.70s; gap 3s; T3 x 10 @ 0.95s; gap 4s; T4 x 2 @ 1.40s | First Aegis shield check. |
| 11 | T1 x 22 @ 0.65s; gap 2s; T2 x 18 @ 0.68s; gap 3s; T3 x 12 @ 0.90s; gap 4s; T4 x 4 @ 1.30s | Shielded mixed wave. |
| 12 | T1 x 20 @ 0.64s; gap 2s; T2 x 20 @ 0.66s; gap 3s; T3 x 14 @ 0.88s; gap 4s; T4 x 6 @ 1.25s | Density and shield pressure. |
| 13 | T1 x 24 @ 0.62s; gap 2s; T2 x 18 @ 0.64s; gap 3s; T3 x 16 @ 0.86s; gap 4s; T4 x 8 @ 1.20s | Forces a stronger anti-armor answer. |
| 14 | T1 x 20 @ 0.60s; gap 2s; T2 x 20 @ 0.62s; gap 3s; T3 x 18 @ 0.84s; gap 4s; T4 x 10 @ 1.15s; gap 5s; T5 x 2 @ 1.60s | First regenerating enemies. |
| 15 | T1 x 22 @ 0.58s; gap 2s; T2 x 22 @ 0.60s; gap 3s; T3 x 20 @ 0.82s; gap 4s; T4 x 12 @ 1.10s; gap 5s; T5 x 4 @ 1.50s | Full roster, controlled quantities. |
| 16 | T1 x 20 @ 0.56s; gap 2s; T2 x 24 @ 0.58s; gap 3s; T3 x 22 @ 0.80s; gap 4s; T4 x 14 @ 1.05s; gap 5s; T5 x 6 @ 1.45s | Sustained late-game test. |
| 17 | T1 x 18 @ 0.55s; gap 2s; T2 x 24 @ 0.56s; gap 3s; T3 x 24 @ 0.78s; gap 4s; T4 x 16 @ 1.00s; gap 5s; T5 x 8 @ 1.40s | More high-value targets. |
| 18 | T1 x 20 @ 0.54s; gap 2s; T2 x 24 @ 0.55s; gap 3s; T3 x 26 @ 0.76s; gap 4s; T4 x 18 @ 0.98s; gap 5s; T5 x 10 @ 1.35s | Near-final endurance wave. |
| 19 | T1 x 16 @ 0.53s; gap 2s; T2 x 26 @ 0.54s; gap 3s; T3 x 28 @ 0.74s; gap 4s; T4 x 20 @ 0.95s; gap 5s; T5 x 12 @ 1.30s | High density with fast flank. |
| 20 | T1 x 20 @ 0.52s; gap 2s; T2 x 28 @ 0.53s; gap 3s; T3 x 32 @ 0.72s; gap 4s; T4 x 24 @ 0.90s; gap 5s; T5 x 16 @ 1.25s | Final gauntlet; victory after all groups die or escape. |

The roster contains 1,090 enemies in total: 360 T1, 296 T2, 242 T3, 134 T4, and 58 T5. This is intentionally a readable endurance match rather than a boss-rush structure. No separate boss system is needed for version 1.

### Wave controls

- `START WAVE`: starts the next wave immediately and awards the 20-credit early-start bonus if the intermission timer has not expired.
- After a wave is cleared, the next wave automatically enters a ten-second countdown.
- `PAUSE`: freezes simulation and changes to `Paused`.
- `1x` and `2x`: change simulation speed. UI input, pause, and button animations remain responsive in real time.
- Starting a wave while placement mode is active is allowed; starting a wave never silently cancels placement.

## 10. Initial map design

### Map identity and bounds

```text
Map ID: foundry_loop
Display name: Foundry Loop
Logical map rectangle: (0, 0) to (960, 720)
Playable content should avoid y < 56 because the top HUD overlays that region.
Path width: 56 px
Path clearance for tower center: 50 px
Spawn: (-32, 104)
Goal: (952, 642)
```

### Path

Waypoints are ordered from spawn to goal:

```text
(-32, 104)
(120, 104)
(120, 250)
(360, 250)
(360, 112)
(640, 112)
(640, 386)
(492, 386)
(492, 570)
(820, 570)
(820, 642)
(952, 642)
```

This creates a left-right zigzag with an upper loop, a center return, and a final lower run. Long-range towers can cover multiple segments from the upper and center regions; short-range and splash towers benefit from the corners around `(120,250)`, `(360,250)`, `(640,386)`, and `(492,570)`.

The map runtime precomputes each segment’s length and cumulative distance. The path is not a graph in version 1: it is one ordered polyline. The data shape leaves room for branches later, but branching movement is explicitly out of scope.

### Buildable regions

Buildable regions are polygons/rectangles in map data. A tower center must lie in at least one region, fit entirely inside that region, be outside the path clearance capsule, be outside the map edge margin, and not overlap an existing tower.

Initial regions, expressed as rectangles for easy authoring:

```text
west_upper       (20, 170, 58, 70)
upper_center     (165, 150, 155, 55)
middle_left      (170, 310, 150, 140)
middle_center    (405, 165, 185, 70)
east_upper       (710, 170, 180, 150)
lower_left       (25, 470, 300, 185)
lower_center     (555, 430, 205, 90)
goal_approach    (700, 680, 100, 32)
```

The map does not expose the UI sidebar as a buildable region. Clean empty rectangles with colored outlines indicate legal continuous-placement terrain. Decorative background rectangles are not collision data.

## 11. Future map data architecture

Use JSON for map and wave content because it is easy to inspect, diff, duplicate, and add without recompiling gameplay code. Load and validate it once when constructing a `GameSession`; convert it into immutable runtime objects so update code does not repeatedly parse JSON.

Adding a second map should require:

1. Adding `Maps/SecondMap.json`.
2. Adding or reusing a wave file.
3. Registering the map ID in a small `MapCatalog` list or discovering files from the `Maps` directory.
4. Adding a menu entry if the title screen supports map selection later.

No enemy movement, placement, economy, or UI gameplay code should contain `foundry_loop` coordinates.

### Map schema

```json
{
  "schemaVersion": 1,
  "id": "foundry_loop",
  "displayName": "Foundry Loop",
  "logicalSize": { "width": 960, "height": 720 },
  "background": { "base": "#18252B", "accent": "#24383E" },
  "spawn": { "x": -32, "y": 104 },
  "goal": { "x": 952, "y": 642 },
  "pathWidth": 56,
  "path": [
    { "x": -32, "y": 104 },
    { "x": 120, "y": 104 }
  ],
  "buildableRegions": [
    { "kind": "rectangle", "x": 20, "y": 145, "width": 82, "height": 88 }
  ],
  "restrictedRegions": [],
  "waveSet": "foundry_waves",
  "startingLives": 20,
  "startingCredits": 400
}
```

`restrictedRegions` is included now for future walls or decorative no-build areas, even though the initial map can rely mostly on buildable inclusion. `optionalMetadata` may be added as a `Dictionary<string, JsonElement>` only at the outer data boundary; gameplay systems should not depend on arbitrary metadata.

## 12. Combat architecture

### Combat pipeline

Each simulation update performs the following combat work:

1. `TowerSystem` reduces each tower’s cooldown by scaled delta time.
2. A tower whose cooldown is zero asks `TargetSelector` for a valid primary target.
3. Its `IAttackBehavior` creates an `AttackCommand`.
4. The command either applies an instant effect (`Beam`, `Chain`, support aura) or spawns one or more projectiles.
5. `ProjectileSystem` moves projectiles using elapsed time, checks target validity, and resolves collision.
6. A hit becomes a `DamageEvent` plus optional `StatusEffectSpec` and splash/chain commands.
7. `DamageResolver` applies shields, armor, pierce, resistances, and health changes in one place.
8. An enemy reaching zero health is marked dead once, awards its reward, creates a small geometric effect, and is removed during cleanup.
9. Escapes are marked once, subtract lives, create a goal flash, and are removed without a reward.

### Damage resolution

For ordinary physical damage:

```text
damageAfterPierce = max(1, rawDamage - max(0, armor - armorPierce))
shieldAbsorbed    = min(shield, damageAfterPierce)
remaining         = damageAfterPierce - shieldAbsorbed
healthDamage      = remaining * damageMultiplier
```

The Aegis shield is consumed before armor. Prism Beam at level 3 sets `ignoresShield`, so it damages health after armor/pierce rules. Burn uses ordinary armor mitigation at application time, then deals its stored per-second damage on each status tick; it does not create additional rewards. Exposed changes `damageMultiplier` and is capped at one active instance per enemy.

### Projectile types

Only the following projectile behaviors are needed:

- `Straight`: travels from tower to a captured aim point and can hit the captured target if it remains near the line.
- `Homing`: updates its aim toward the captured target and resolves when within target radius plus projectile radius.
- `Mortar`: travels to an impact point; on impact it applies splash to all enemies within radius.
- `Pellet`: a straight short-lived projectile, created in a spread pattern.

Needle, Shard Fan, Watchtower, Frost Spire, Ember Coil, Breaker Cannon, and Mortar use projectiles. Arc Relay and Prism Beam are instant. Signal Beacon is an aura. A projectile with a dead target is removed unless it is an area-impact projectile; the Mortar retains its impact point to preserve useful area fire.

Collision is circle-to-circle or point-to-circle only. There are no world physics bodies. Entity counts are small enough that a full scan of active enemies for targeting and splash is appropriate.

### Chain attack

Arc Relay resolves its primary hit immediately. It then finds additional unvisited enemies within the configured jump range, ordered by progress descending, and applies reduced chain damage. The same enemy cannot be hit twice in one chain event. Chain jumps do not spawn projectile objects.

### Area buffs

Signal Beacon has no target. Each frame or whenever towers move/level—which is only at placement and upgrade time—the `BuffSystem` evaluates nearby towers. A tower receives the strongest Beacon bonus in range; bonuses do not stack. Beacon aura does not affect other Beacons.

## 13. Targeting architecture

Targeting is a reusable service, not code copied into tower classes.

```csharp
public enum TargetMode { First, Last, Strongest, Weakest, Nearest }

public interface ITargetSelector
{
    EnemyInstance? Select(
        Vector2 origin,
        float range,
        TargetMode mode,
        IReadOnlyList<EnemyInstance> enemies,
        TargetFilter filter);
}
```

Definitions:

- `First`: highest `PathProgress`; tie-break by lower enemy ID.
- `Last`: lowest `PathProgress`; tie-break by lower enemy ID.
- `Strongest`: highest current effective health (`shield + health`); tie-break by highest progress.
- `Weakest`: lowest current health percentage; tie-break by highest progress.
- `Nearest`: smallest squared distance to tower; tie-break by highest progress.

An enemy is eligible only if it is alive, not escaped, inside effective range, and passes the behavior’s filter. `PathProgress` is a normalized value from 0 to 1 and is the authoritative value for First/Last. A target reference is revalidated before attack and before projectile impact.

The UI cycles target mode with a single `TARGET: FIRST` button. A small text label and icon communicate the selected mode; mode changes do not cost credits.

## 14. Status-effect architecture

Use one reusable `StatusEffectController` on each enemy and a compact `StatusEffect` runtime record.

```csharp
public enum StatusType { Slow, Burn, ArmorBreak, Exposed, Stun }

public sealed record StatusEffect(
    StatusType Type,
    float RemainingSeconds,
    float Magnitude,
    int SourceId,
    int StackCount);
```

Rules:

| Effect | Stacking | Refresh rule | Cap |
|---|---|---|---|
| Slow | No; strongest magnitude wins. | A stronger application replaces magnitude; equal/weaker application refreshes duration only if longer. | 60% maximum slow; roster uses at most 45%. |
| Burn | Yes, maximum 2 instances. | Each instance gets its own remaining duration. | 2 stacks; total burn is additive. |
| ArmorBreak | No. | Keep the stronger reduction and refresh to the longer duration. | Cannot reduce armor below 0. |
| Exposed | No. | Keep the stronger multiplier and refresh to the longer duration. | Maximum +25% damage taken. |
| Stun | No. | Stronger/longer application replaces the current one. | Maximum 1.0 s from one hit. |

The first roster uses Slow, Burn, ArmorBreak, Exposed, and the Arc Relay L3 Stun. Status update happens before tower attacks so an effect expiring this frame has deterministic behavior: decrement timers, remove expired effects, apply regeneration pause checks, then process tower attacks.

T5 regeneration is an enemy intrinsic, not a status effect. It regenerates at 18 HP/s only if the damage pause timer is zero and the enemy is not dead.

## 15. UI/UX design

### Layout

The logical canvas is divided into:

- Top HUD: `Rectangle(0, 0, 1280, 56)`.
- Map: `Rectangle(0, 56, 960, 664)` with the map’s logical coordinates translated by y = 0 for data; alternatively render the map in a 960 x 720 offscreen logical layer and let the HUD overlay it. The implementation should choose one transform and use it everywhere; the recommended choice is one world transform with a `worldOrigin` of `(0, 0)` and path data avoiding the HUD.
- Sidebar: `Rectangle(960, 56, 320, 664)`.

The simpler implementation is to use a full 1280 x 720 logical canvas, keep map coordinates in the left 960 pixels, and avoid path/buildable data under the top HUD. No separate render target is required.

### Top-level HUD

The top bar shows:

```text
LIVES 20/20   CREDITS 300   WAVE 0/20   ENEMIES 0   [START WAVE] [1x] [PAUSE]
```

Credits briefly scale or flash green on gain and red on a rejected purchase. Lives flash red when an enemy escapes. The current speed button is visibly selected.

### Tower purchase panel

The sidebar contains ten cards in a 2-column by 5-row grid. Each card shows:

- A geometric tower icon.
- Short name.
- Purchase cost.
- One-word role label.
- Affordable state.

Clicking an affordable card enters placement mode. Clicking an unaffordable card does not enter placement mode and produces a short `Not enough credits` tooltip. Hovering shows a tooltip with range, damage, attack rate, and special effect.

### Selected tower panel

When a tower is selected, the lower sidebar shows:

- Name, icon, level, and coordinates only in debug mode.
- Damage, range, APS, and special description.
- Current target mode and a cycle button.
- `UPGRADE` with cost and disabled state.
- `SELL` with exact return value.

Clicking empty map terrain deselects the tower unless placement mode is active. Clicking another tower selects it. Selecting a tower displays its range ring on the map.

### Placement feedback

During placement:

- A translucent geometric ghost follows the mouse.
- The effective range ring follows the ghost.
- Green means valid; red means invalid.
- Invalid reasons appear in a short sidebar line: `Outside build area`, `Blocks path`, `Overlaps tower`, `Too close to edge`, or `Not enough credits`.
- Left click places if valid and subtracts the purchase cost.
- Right click or Escape cancels placement without spending money.
- A click on any sidebar control is consumed by the UI and cannot also select/place on the map.

### Screens

- Main menu: title, one large `PLAY` button, a small controls line, and `QUIT`.
- Paused: dark translucent overlay, `PAUSED`, `RESUME`, `RESTART`, and `MAIN MENU`.
- Victory: match summary (wave, lives remaining, credits, towers built), `RESTART`, and `MAIN MENU`.
- Defeat: summary with escaped enemies, `RESTART`, and `MAIN MENU`.

The UI remains geometric and uses text, bars, panels, and icons rather than external interface art.

## 16. Input design

`InputRouter` polls MonoGame once per frame and produces an immutable `InputSnapshot`:

```csharp
public sealed record InputSnapshot(
    Point MousePosition,
    bool LeftPressed,
    bool LeftReleased,
    bool RightPressed,
    bool EscapePressed,
    bool PausePressed,
    bool[] KeysPressed);
```

Mouse coordinates are transformed from the physical window into logical 1280 x 720 coordinates after letterbox calculation. All game systems consume this snapshot; no tower, enemy, or panel polls `Mouse.GetState` directly.

Input priority is:

1. Window/state-level commands: Escape/P pause, title buttons, victory/defeat buttons.
2. UI hit testing: HUD controls, tower cards, selected tower buttons, speed and wave controls.
3. Placement mode: map click places; right click/Escape cancels.
4. World selection: map click selects a tower or clears selection.

The `UIManager` returns an `InputConsumption` result so the world layer knows whether the click was already handled. Keyboard focus is not needed beyond buttons in version 1.

## 17. Game-state architecture

Use a simple enum and explicit switch in `Game1`/`GameStateController`:

```csharp
public enum GameState { MainMenu, Playing, Paused, Victory, Defeat }
```

`Playing` owns a `PlacementState` record:

```csharp
public sealed record PlacementState(string? TowerDefinitionId, Vector2 CursorPosition);
```

State transitions:

```text
MainMenu --Play------> Playing
Playing --Pause------> Paused
Paused --Resume-----> Playing
Playing --all waves--> Victory
Playing --lives 0----> Defeat
Victory/Defeat --Restart--> Playing with a new GameSession
Any non-playing menu --Main Menu--> MainMenu
```

When paused, render the current game exactly as before plus an overlay. Do not advance simulation, spawn timers, cooldowns, status timers, projectiles, or particles. Menu input still updates.

## 18. Rendering approach

### Logical scaling and resize

Set the backbuffer to 1280 x 720 initially and enable `Window.AllowUserResizing`. On each draw:

1. Calculate `scale = min(backbufferWidth / 1280f, backbufferHeight / 720f)`.
2. Calculate a centered viewport rectangle.
3. Set `GraphicsDevice.Viewport` to that letterboxed rectangle.
4. Use a `Matrix.CreateScale(scale)` SpriteBatch transform or draw into a 1280 x 720 render target and scale it once.
5. Use the same inverse transform for mouse input.

The recommended version-1 choice is a 1280 x 720 render target. It gives crisp, predictable layering and makes the map/sidebar layout independent of window size. If render-target handling creates platform issues, use a single SpriteBatch transform instead.

### PrimitiveRenderer

Generate a 1 x 1 white `Texture2D` at runtime. Tinting it provides filled rectangles and lines. `PrimitiveRenderer` exposes:

```text
FillRect(rect, color)
DrawRect(rect, color, thickness)
Line(start, end, color, thickness)
Circle(center, radius, color)
Ring(center, radius, color, thickness)
Polygon(points, color)                 // optional; map rectangles are enough for v1
HealthBar(position, width, ratio, colors)
```

Lines are drawn as rotated/scaled rectangles. Circles are generated into a small cache of transparent textures keyed by integer radius; the alpha mask is filled by a one-time per-pixel circle test. Range circles use the cached filled circle with a low alpha and a cached ring texture. Common radii are reused, so no per-frame texture creation occurs.

Towers, enemies, projectiles, and effects are combinations of these helpers. Each definition has `VisualSpec` fields such as shape, primary color, accent color, radius, outline thickness, number of marks, and pulse flag. Visual distinction never depends solely on a color field.

### Layer order

```text
background
decorative terrain and buildable-region indication
path shadow and path surface
range indicators and placement ghost
tower bases
enemies and health bars
projectiles and effects
goal/spawn markers
selection rings
HUD/sidebar
tooltips and state overlay
```

### Text

Use one compiled `Interface.spritefont` asset. The source font is included/configured at build time and the compiled `.xnb` is what the distributed game loads, so runtime machines do not need the font installed. Use a normal proportional face at 14, 18, 24, 36, and 52 pixel sizes if the content pipeline asset supports separate sizes; otherwise create one SpriteFont with a broad glyph range and scale it sparingly. Avoid dynamic font loading and avoid requiring the player’s arbitrary system font.

Audio is optional. If added, keep a small `IAudioService` with `PlayUi`, `PlayAttack`, `PlayHit`, and `PlayResult` methods. Version 1 may use no audio and must not make gameplay depend on it.

## 19. Class/system architecture

### Core runtime

| Class | Responsibility |
|---|---|
| `Game1` | MonoGame lifecycle, graphics device, content load, top-level update/draw delegation. |
| `GameStateController` | State enum, transitions, active session, pause and restart commands. |
| `GameSession` | Owns one match and coordinates map, economy, waves, entities, combat, placement, and result state. |
| `GameClock` | Scaled simulation delta, current speed, pause-independent UI time if needed. |
| `InputRouter` | Single source of keyboard/mouse polling and logical-coordinate conversion. |
| `ContentLoader` | Loads JSON and SpriteFont/primitive resources, validates data. |

### Map and movement

| Class | Responsibility |
|---|---|
| `MapDefinition` | Immutable JSON DTO for map data. |
| `MapRuntime` | Validated map, path segments, cumulative lengths, buildable regions, rendering colors. |
| `PathRuntime` | Computes position, segment index, direction, and progress from distance traveled. |
| `PlacementManager` | Validity checks, preview, purchase placement, selection, and tower overlap checks. |

### Enemies and waves

| Class | Responsibility |
|---|---|
| `EnemyDefinition` | Base stats, reward, lives, visuals, intrinsic flags. |
| `EnemyInstance` | Runtime HP, shield, distance, status controller, ID, current position, death/escape flags. |
| `EnemySystem` | Movement, intrinsic regeneration, escape/death marking, health bars. |
| `WaveDefinition` | Wave multiplier and ordered groups. |
| `WaveGroupDefinition` | Enemy ID, count, interval, pre-delay. |
| `WaveManager` | Intermission, starting, spawning, active-wave completion, remaining count. |

### Towers and combat

| Class | Responsibility |
|---|---|
| `TowerDefinition` | ID, cost, level records, behavior ID, targeting defaults, visual spec. |
| `TowerLevelDefinition` | Range, damage, APS, projectile/effect parameters, upgrade cost. |
| `TowerInstance` | Position, definition ID, level, cooldown, targeting mode, invested credits, ID. |
| `TowerSystem` | Cooldown update, target requests, behavior execution, aura refresh. |
| `ITowerBehavior` | One attack decision for a tower instance. |
| `AttackBehaviorRegistry` | Maps behavior IDs to small behavior objects. |
| `TargetSelector` | Shared target acquisition and target mode rules. |
| `ProjectileInstance` | Position, velocity/aim, target, damage payload, impact behavior. |
| `ProjectileSystem` | Projectile movement, collision, impact, and removal. |
| `DamageResolver` | Shield, armor, pierce, amplification, health damage, death event. |
| `StatusEffectController` | Add, refresh, stack, tick, and query effects on one enemy. |
| `BuffSystem` | Computes Beacon bonuses and exposes effective tower stats. |

### Economy, effects, and UI

| Class | Responsibility |
|---|---|
| `Economy` | Credits, lives, purchases, rewards, wave bonuses, sell values. |
| `EffectInstance` | Short-lived flashes, rings, particles, or hit markers. |
| `EffectSystem` | Updates/removes simple effects and provides draw data. |
| `UIManager` | Panel layout, hit tests, button actions, tooltips, text and icon drawing. |
| `PrimitiveRenderer` | All geometric drawing helpers. |
| `DebugOverlay` | Development-only visual overlays and cheats. |

### Behavior modules

Do not create one switch-heavy `Tower` class. Use a small registry with behavior modules such as:

```text
SingleProjectileBehavior
PelletBurstBehavior
ChainBehavior
SplashProjectileBehavior
BeamBehavior
AuraBehavior
```

The behavior reads the current `TowerLevelDefinition`, selects a target through `TargetSelector`, and emits an `AttackCommand`. Effects such as slow, burn, armor break, and exposed are data on the command, not custom enemy subclasses.

Use one `EnemyInstance` class with `EnemyDefinition` data and a small set of intrinsic flags (`Regenerates`, `HasShield`). There is no need for an `Enemy` subclass hierarchy in version 1.

## 20. Data-definition schemas

### Tower JSON shape

```json
{
  "id": "needle_turret",
  "displayName": "Needle Turret",
  "role": "Generalist",
  "behavior": "single_projectile",
  "purchaseCost": 90,
  "defaultTargetMode": "First",
  "visual": {
    "shape": "square",
    "primary": "#4FA8FF",
    "accent": "#D9F1FF",
    "radius": 18,
    "marks": 2
  },
  "levels": [
    { "range": 125, "damage": 8, "attacksPerSecond": 1.8, "projectileSpeed": 450, "upgradeCost": 55 },
    { "range": 135, "damage": 10, "attacksPerSecond": 2.0, "projectileSpeed": 480, "upgradeCost": 85 },
    { "range": 145, "damage": 13, "attacksPerSecond": 2.3, "projectileSpeed": 520, "upgradeCost": null }
  ]
}
```

`TowerLevelDefinition` may use nullable fields for behavior-specific parameters, but the loader validates required values based on `behavior`. Invalid or missing values should fail at startup with a file/field error, not produce a half-working tower.

### Enemy JSON shape

```json
{
  "id": "t4_aegis",
  "displayName": "Aegis",
  "maxHealth": 520,
  "speed": 62,
  "reward": 45,
  "livesLost": 3,
  "armor": 8,
  "shield": 100,
  "regenerationPerSecond": 0,
  "visual": {
    "shape": "octagon",
    "primary": "#8A63D2",
    "accent": "#77E6F2",
    "radius": 24,
    "marks": 1,
    "ring": true
  }
}
```

### Wave JSON shape

```json
{
  "schemaVersion": 1,
  "mapId": "foundry_loop",
  "waves": [
    {
      "number": 1,
      "healthMultiplier": 1.0,
      "speedMultiplier": 1.0,
      "groups": [
        { "enemyId": "t1_crawler", "count": 8, "spawnInterval": 0.90, "delayBefore": 0.0 }
      ]
    }
  ]
}
```

Use `JsonStringEnumConverter` for enum-like fields and a `DataValidator` that checks unique IDs, positive costs/stats, contiguous wave numbers, known enemy IDs, nonnegative group counts, path length greater than zero, and map bounds.

## 21. Proposed folder/file structure

```text
MinimalBastion/
├─ MinimalBastion.sln
├─ src/
│  └─ MinimalBastion/
│     ├─ MinimalBastion.csproj
│     ├─ Game1.cs
│     ├─ Core/
│     │  ├─ GameState.cs
│     │  ├─ GameStateController.cs
│     │  ├─ GameSession.cs
│     │  ├─ GameClock.cs
│     │  └─ InputRouter.cs
│     ├─ Data/
│     │  ├─ ContentLoader.cs
│     │  ├─ DataValidator.cs
│     │  ├─ TowerDefinition.cs
│     │  ├─ EnemyDefinition.cs
│     │  ├─ MapDefinition.cs
│     │  └─ WaveDefinition.cs
│     ├─ Maps/
│     │  ├─ MapRuntime.cs
│     │  ├─ PathRuntime.cs
│     │  └─ PlacementManager.cs
│     ├─ Enemies/
│     │  ├─ EnemyInstance.cs
│     │  └─ EnemySystem.cs
│     ├─ Waves/
│     │  └─ WaveManager.cs
│     ├─ Towers/
│     │  ├─ TowerInstance.cs
│     │  ├─ TowerSystem.cs
│     │  ├─ TargetMode.cs
│     │  ├─ TargetSelector.cs
│     │  ├─ ITowerBehavior.cs
│     │  └─ Behaviors/
│     │     ├─ SingleProjectileBehavior.cs
│     │     ├─ PelletBurstBehavior.cs
│     │     ├─ ChainBehavior.cs
│     │     ├─ SplashProjectileBehavior.cs
│     │     ├─ BeamBehavior.cs
│     │     └─ AuraBehavior.cs
│     ├─ Combat/
│     │  ├─ AttackCommand.cs
│     │  ├─ ProjectileInstance.cs
│     │  ├─ ProjectileSystem.cs
│     │  ├─ DamageEvent.cs
│     │  ├─ DamageResolver.cs
│     │  └─ BuffSystem.cs
│     ├─ Effects/
│     │  ├─ StatusEffect.cs
│     │  ├─ StatusEffectController.cs
│     │  ├─ EffectInstance.cs
│     │  └─ EffectSystem.cs
│     ├─ Economy/
│     │  └─ Economy.cs
│     ├─ Rendering/
│     │  ├─ PrimitiveRenderer.cs
│     │  ├─ VisualSpec.cs
│     │  └─ RenderAssets.cs
│     ├─ UI/
│     │  ├─ UIManager.cs
│     │  ├─ UiLayout.cs
│     │  ├─ Button.cs
│     │  └─ Tooltip.cs
│     ├─ Debugging/
│     │  └─ DebugOverlay.cs
│     ├─ Content/
│     │  ├─ Content.mgcb
│     │  ├─ Fonts/Interface.spritefont
│     │  └─ Fonts/README.md
│     └─ ContentData/
│        ├─ Towers.json
│        ├─ Enemies.json
│        └─ Maps/
│           ├─ FoundryLoop.json
│           └─ FoundryWaves.json
├─ tests/
│  └─ MinimalBastion.Tests/
│     ├─ MinimalBastion.Tests.csproj
│     ├─ Program.cs
│     ├─ AssertEx.cs
│     ├─ PathRuntimeTests.cs
│     ├─ TargetSelectorTests.cs
│     ├─ DamageResolverTests.cs
│     ├─ EconomyTests.cs
│     ├─ StatusEffectTests.cs
│     ├─ WaveDataTests.cs
│     └─ MapLoaderTests.cs
└─ README.md
```

`src/MinimalBastion/Data/` contains C# DTO/loader classes, while `src/MinimalBastion/ContentData/` contains JSON files copied beside the executable. The important rule is that code and content are clearly separated.

## 22. Important class responsibilities

### `GameSession`

`GameSession` is the only object that coordinates a match. It should expose high-level commands:

```csharp
public sealed class GameSession
{
    public void Update(GameTime gameTime, InputSnapshot input);
    public void StartNextWave();
    public void RestartPlacement();
    public bool TryPlaceTower(string towerId, Vector2 position);
    public bool TryUpgradeSelectedTower();
    public bool TrySellSelectedTower();
}
```

It does not contain draw code beyond exposing runtime collections and state. It does not know which pixels a button occupies.

### `Economy`

`Economy` is the only owner of credits and lives. Purchases, upgrades, rewards, wave bonuses, selling, and escapes go through methods such as `CanAfford`, `Spend`, `AwardKillReward`, `AwardWaveBonus`, `SellValue`, and `LoseLives`. This prevents a UI button and a game system from applying money changes differently.

### `PlacementManager`

`PlacementManager.Validate` returns a structured result rather than only a boolean:

```csharp
public enum PlacementFailure
{
    None, OutsideBuildableRegion, BlocksPath, OverlapsTower,
    TooCloseToEdge, InsufficientCredits
}
```

The UI uses the same result for its ghost color and error text. The authoritative purchase check is repeated on click.

### `WaveManager`

`WaveManager` owns intermission state, current wave number, group index, group spawn count, group timer, and active-wave status. It reports `EnemiesRemaining` from both queued groups and live enemies. It never directly awards credits; it emits a wave-cleared event or calls a session-level reward method once.

### `EnemyInstance`

Runtime fields include `Id`, `DefinitionId`, `Position`, `DistanceAlongPath`, `PathProgress`, `Health`, `MaxHealth`, `Shield`, `Armor`, `StatusEffects`, `DamagePauseTimer`, and `IsDead/HasEscaped`. Definitions are not mutated.

### `TowerInstance`

Runtime fields include `Id`, `DefinitionId`, `Position`, `Level`, `CooldownRemaining`, `TargetMode`, and `InvestedCredits`. It queries effective stats through its definition and Beacon bonus; it does not retain mutable copies of every derived stat.

### `PrimitiveRenderer`

Owns all runtime-generated textures and disposal. No gameplay class calls `Texture2D.SetData` or draws an ad-hoc primitive.

## 23. Update-loop ordering

Use this exact order for deterministic behavior:

1. Poll input and transform the mouse into logical coordinates.
2. Let `UIManager` consume menu, pause, and button input.
3. If not in `Playing`, skip simulation and only update relevant menu animation.
4. Calculate scaled simulation delta from `GameClock`.
5. Apply placement/selection commands not already consumed by the UI.
6. `WaveManager.Update`: update intermission and spawn queued enemies. Newly spawned enemies do not move until the next phase, which avoids same-frame ordering surprises.
7. `EnemySystem.Update`: move enemies along the path; process regeneration and mark escapes.
8. `StatusEffectSystem.Update`: decrement effects, tick burn, apply effect expiry, and update stun/slow state.
9. `BuffSystem.Update`: compute effective Beacon bonuses for towers.
10. `TowerSystem.Update`: process cooldowns, acquire targets, and emit attacks.
11. `ProjectileSystem.Update`: move projectiles, resolve impacts, and send damage events.
12. Resolve queued chain/splash/instant damage events in FIFO order. Death is idempotent.
13. `EffectSystem.Update`: advance flashes, rings, and particles.
14. Remove dead/escaped enemies and expired projectiles/effects.
15. `WaveManager` checks whether all groups are spawned and all enemies are gone; award one wave bonus and start the next intermission.
16. `GameSession` checks defeat first, then victory, then publishes HUD values.

Defeat takes priority if the final enemy escape reduces lives to zero on the same update that the last wave would otherwise clear. This is easier to explain to the player and avoids a contradictory victory/defeat result.

## 24. Testing strategy

Use a small dependency-free test executable. Each test constructs plain runtime objects and throws an exception with a useful message on failure. Rendering and MonoGame device tests remain manual smoke tests.

Required logic tests:

- `PathRuntimeTests`: positions at distance 0, exact waypoint distances, distance beyond a turn, progress monotonicity, and completion at 1.0.
- `TargetSelectorTests`: all five modes, range filtering, dead-target filtering, and deterministic tie-breakers.
- `DamageResolverTests`: ordinary damage, armor, pierce, shield first, shield bypass, Exposed multiplier, and minimum damage.
- `StatusEffectTests`: strongest slow, burn stack cap, refresh behavior, armor-break cap, expiration, and stun.
- `EconomyTests`: starting credits, purchase rejection, upgrade spend, 60% sell value, kill reward, wave reward, and life loss.
- `WaveDataTests`: 20 contiguous waves, known enemy IDs, group counts/intervals, total enemy count 1,090, and tier unlock points.
- `MapLoaderTests`: path is valid, map IDs are unique, buildable regions are nonempty, spawn/goal are present, and no path segment has zero length.
- `PlacementTests`: path clearance, tower overlap, edge margin, buildable inclusion, and insufficient credits.

Manual smoke checks:

1. Launch from `dotnet run` with no IDE.
2. Play from menu through wave 20.
3. Buy, place, select, upgrade, change targeting, and sell every tower.
4. Confirm every enemy tier visibly appears.
5. Pause at each major activity and confirm nothing simulates.
6. Test 1x/2x at spawn, projectile flight, status effects, and victory/defeat.
7. Resize the window to 1600 x 900 and 1024 x 768; confirm letterboxing and mouse hit tests.
8. Run the release publish output from a clean directory.

## 25. Debugging/development tools

Compile debug tools behind `#if DEBUG` and keep them unavailable in release builds:

| Key | Tool |
|---|---|
| F2 | Toggle path segment, waypoint, and progress display. |
| F3 | Toggle all tower range circles and placement region outlines. |
| F4 | Toggle FPS, frame time, entity counts, and current state. |
| F5 | Add 1,000 credits. |
| F6 | Skip the current wave after clearing queued spawns. |
| F7 | Spawn one selected enemy tier at the spawn point. |
| F8 | Toggle invulnerable lives. |
| F9 | Toggle collision/target debug lines and projectile aim points. |

The debug overlay should show enemy ID, tier, HP/shield, path progress, and current statuses near the cursor only when explicitly enabled. Do not add debug hotkeys that are easy to trigger accidentally during ordinary play.

## 26. Performance considerations

The expected active entity count is modest. Use readable `List<T>` collections and deferred removal. Do not implement ECS, multithreading, spatial hashing, object pooling, or a job system initially.

Practical safeguards:

- Do not allocate textures during `Update` or `Draw`.
- Cache primitive circle/ring textures by radius.
- Use squared distance for target and splash checks.
- Store immutable definitions once; instances hold IDs/references rather than copied JSON objects.
- Keep projectile and effect lists bounded by normal gameplay; cap particles to 256 if a visual bug could otherwise produce unbounded effects.
- Avoid LINQ in inner loops if profiling shows allocation or CPU pressure; clear `for` loops are preferred.
- Use a fixed 1/60 update only if MonoGame’s normal `GameTime.ElapsedGameTime` produces unacceptable behavior. The first choice is variable elapsed time with a maximum delta clamp of 0.1 seconds to avoid teleporting after a breakpoint.

If profiling later shows projectile allocation pressure, add a small free-list for `ProjectileInstance`. Do not add it before a demonstrated need; the architecture keeps projectiles isolated so pooling can be introduced locally.

## 27. Implementation phases in exact recommended order

### Phase 1 — Project and window

Objective: launch a blank MonoGame DesktopGL window from the command line.

Files: solution, `.csproj`, `Game1.cs`, `Content.mgcb`, `Interface.spritefont`, README build commands.

Completion: `dotnet build` succeeds; `dotnet run` opens a resizable 1280 x 720 logical window and closes cleanly.

Dependency: none.

### Phase 2 — Input, logical viewport, and primitives

Objective: establish stable coordinate conversion and geometric rendering.

Files: `InputRouter`, `GameClock`, `PrimitiveRenderer`, `RenderAssets`, `UiLayout`.

Completion: draw rectangles, lines, circles, rings, text, and a mouse marker correctly at multiple window sizes; Escape is detected once per press.

Dependency: Phase 1.

### Phase 3 — Data loading and map/path runtime

Objective: load and validate Foundry Loop from JSON and render the path/buildable regions.

Files: DTOs, `ContentLoader`, `DataValidator`, `MapRuntime`, `PathRuntime`, initial map JSON.

Completion: startup loads the map; debug view shows waypoints and cumulative path distances; invalid JSON gives a useful startup error.

Dependency: Phases 1–2.

### Phase 4 — Enemy movement

Objective: spawn a manually created Crawler and move it to the goal.

Files: `EnemyDefinition`, `EnemyInstance`, `EnemySystem`.

Completion: enemy position follows every segment, progress is monotonic, health bar renders, and an escape subtracts one life exactly once.

Dependency: Phase 3.

### Phase 5 — Wave manager

Objective: load data-driven groups and run one complete wave.

Files: wave DTOs, `WaveManager`, Foundry wave JSON.

Completion: groups spawn at intervals, gaps work, active/remaining counts are correct, wave 1 completion awards one bonus, and wave 2 can start.

Dependency: Phases 3–4.

### Phase 6 — One tower, targeting, and projectile combat

Objective: make Needle Turret placeable in a temporary hardcoded test position and able to kill a Crawler.

Files: `TowerDefinition`, `TowerInstance`, `TargetSelector`, `SingleProjectileBehavior`, `ProjectileSystem`, `DamageResolver`.

Completion: tower cooldown is elapsed-time based; First targeting works; projectile hit applies damage and kill reward; no duplicate death reward occurs.

Dependency: Phases 2–5.

### Phase 7 — Economy, placement, selection, and selling

Objective: expose the complete player interaction loop for one tower.

Files: `Economy`, `PlacementManager`, selected tower UI, purchase card, sell/upgrade command path.

Completion: valid/invalid ghost works; path/edge/overlap checks work; purchase/subtraction, level upgrade, target mode, selection, and 60% sell value work.

Dependency: Phase 6.

### Phase 8 — HUD, wave controls, pause, and result states

Objective: make the match readable and end-to-end for the one tower.

Files: `GameStateController`, `UIManager`, state screens, top HUD.

Completion: title-to-game, pause/resume, start/countdown, speed controls, defeat, victory, and restart all work.

Dependency: Phase 7.

### Phase 9 — Remaining direct-fire and area towers

Objective: implement Shard Fan, Watchtower, Frost Spire, Ember Coil, Breaker Cannon, Arc Relay, Siege Mortar, and Prism Beam.

Files: behavior modules, projectile variants, chain/splash/beam commands, tower data and icons.

Completion: each tower can be purchased, placed, rendered distinctly, attacks in a real wave, and its defining role is observable in a short test.

Dependency: Phase 6 and Phase 8.

### Phase 10 — Status effects and Signal Beacon

Objective: finish reusable effects and support aura.

Files: status records/controller, `BuffSystem`, `AuraBehavior`, effect visuals, tests.

Completion: slow, burn, armor break, Exposed, stun, shield bypass, and nonstacking Beacon buffs match the rules in this document.

Dependency: Phase 9.

### Phase 11 — Complete content and balance pass

Objective: add all 20 waves, all five enemy visuals, tooltips, full selected stats, and map dressing.

Completion: wave table matches data, all tiers appear at intended waves, all towers are affordable in a real progression, and the first full match is beatable with more than zero lives using sensible play.

Dependency: Phases 8–10.

### Phase 12 — Tests, debug tools, packaging, and polish

Objective: make the project easy to maintain and distribute.

Files: dependency-free tests, `DebugOverlay`, README, release publish instructions, icon/window metadata if desired.

Completion: tests pass, release build runs from a clean output directory, no debug commands are active in release, resize/input behavior is correct, and the Definition of Done below is checked off.

Dependency: all earlier phases.

## 28. Definition of Done for version 1

Version 1 is complete only when all of the following are true:

- The project builds from the command line in Debug and Release.
- The game launches outside an IDE.
- A full match can be played from title screen to victory or defeat.
- All ten tower types can be purchased, placed, selected, attacked with, upgraded, and sold.
- Every tower’s defining special behavior works.
- All five enemy tiers appear in the intended waves and are visually distinguishable.
- All 20 waves progress from data, with group intervals and gaps working.
- Enemies follow the path and report correct progress.
- Kill rewards, wave rewards, purchases, upgrades, selling, and escapes modify economy exactly once.
- Invalid placement is rejected for every documented reason.
- Target modes work and are visible in the selected tower panel.
- Status effects apply, refresh, stack, cap, and expire consistently.
- Lives decrease when enemies reach the goal.
- Victory and defeat are both reachable.
- Restart creates a clean session without carrying over entities, credits, statuses, or cooldowns.
- Pause freezes simulation.
- 1x and 2x produce sensible timing.
- No external art assets are required for the game to run.
- The initial map is reasonably beatable with multiple tower compositions.
- A second map can be added through data and a catalog/menu entry without rewriting gameplay systems.
- Debug tools are isolated from release behavior.
- The test runner passes for path, targeting, damage, effects, economy, waves, map loading, and placement.

## 29. Explicitly excluded features

Do not implement these in version 1:

- Multiplayer or networking.
- Procedural maps or a map editor.
- Branching paths or pathfinding around tower obstacles.
- Campaign progression, skill trees, meta-progression, or account systems.
- Achievements, Steam integration, online leaderboards, cloud saves, or telemetry.
- Localization framework.
- Modding framework or scripting language.
- Elaborate animation, skeletal animation, physics, or particle simulation.
- ECS, job systems, multithreaded gameplay, spatial partitioning, or predictive optimization.
- Database-backed saves.
- Complex menu hierarchy or controller-first UI.
- Boss-specific subclasses or a separate boss system.
- Random map generation.
- Rebinding, keybinding profiles, or accessibility menu beyond readable contrast and pause.
- Audio as a completion dependency.

The architecture leaves reasonable seams for these later, but no version-1 class should be created solely to prepare for a speculative feature.

## 30. Known design assumptions

1. The first shipping target is Windows x64 on an ordinary desktop PC. DesktopGL keeps other desktop platforms possible, but they are not acceptance targets for the first build.
2. The MonoGame package version is pinned at implementation time. If the current stable version changes, update the packages as a unit and verify content building before writing game code.
3. The map uses one fixed path. Additional maps can have different waypoint counts and buildable regions, but not branches in version 1.
4. Balance values are a coherent starting baseline, not a substitute for playtesting. The first tuning knobs should be wave group counts, T4 shield, T5 regeneration, and reward values in data.
5. Tower movement is not supported after placement. Selling and rebuilding is the intended repositioning mechanic.
6. Towers cannot overlap the path clearance capsule even if their visual circle appears to fit; this keeps the path readable and prevents blocking exploits.
7. All entities use circles for collision even when their rendered shape is a square, diamond, hexagon, or star.
8. Damage is deterministic. There are no critical hits, random misses, or random target selection in version 1.
9. The UI is English-only and uses one compiled font.
10. The game does not persist campaign state. A future settings file may store window size, volume, and speed preference only.
11. If a late balance pass reveals that 20 waves are too long, reduce group quantities or intermission duration before adding a skip mechanic. The data-driven wave model is intentionally the first balancing lever.

## References used for technology selection

- MonoGame repository and supported-platform overview: <https://github.com/MonoGame/MonoGame>
- MonoGame official migration guidance and package alignment: <https://docs.monogame.net/articles/migration/migrate_38.html>
- MonoGame DesktopGL package: <https://www.nuget.org/packages/MonoGame.Framework.DesktopGL>
- Official .NET support policy: <https://dotnet.microsoft.com/platform/support/policy>
