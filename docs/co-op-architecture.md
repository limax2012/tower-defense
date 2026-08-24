# Two-Player Co-op Architecture

Minimal Bastion uses direct two-player TCP with host-authoritative command order and deterministic simulation on both peers. The goal is responsive private friend-to-friend play without matchmaking or dedicated infrastructure.

## Connection model

- Default TCP port: `28741`.
- The host PC accepts one guest and acts as the authority.
- An address without a port automatically uses 28741.
- Internet hosting requires the host to be reachable through router/firewall configuration or a peer VPN.
- There is no public matchmaking, hosted relay, UPnP, NAT traversal, or encrypted transport.
- A six-character code prevents an unintended client from entering the waiting session.

The host chooses arena, difficulty, and directive after selecting Host. A joining client does not select match settings; it receives the authoritative setup.

## Handshake

The connection handshake is bounded to ten seconds and validates:

- protocol/build identity
- recursive gameplay and campaign content fingerprint
- join code
- frame shape and direction

Mismatched executable/content versions are rejected before gameplay. Content fingerprinting includes authored JSON recursively, so equal assemblies with different tower or wave data cannot begin a deterministic match.

## Authority and simulation

Both peers run `GameSession` through `DeterministicSessionRunner` at 60 fixed ticks per second. Rendering is variable-rate and may interpolate locally without changing synchronized state.

The host:

- validates gameplay requests
- assigns sequence numbers and simulation ticks
- broadcasts accepted commands
- sends periodic state checksums
- owns autosave/manual save writes and run-history persistence
- supplies authoritative snapshots for reconnect or divergence repair

Commands are normally scheduled about 200 ms ahead. The runner bounds future scheduling, pending commands, duplicate history, and sequence expiry. Commands received for an invalid direction, state, entity, tower path, target mode, position, tick, or directive are rejected.

Shared synchronized actions include:

- tower/Forge/Plate placement
- upgrades, final roles, and Apex
- targeting and automatic Protocol selection
- Protocol activation
- sales/removal
- wave readiness and start
- speed and shared pause
- restart

Credits, lives, kills, waves, enemies, towers, tactical devices, and results are shared. Either player may manage any defense. `OwnerPlayerId` is retained for visual attribution and history only.

## Checksums and repair

`SessionChecksum` covers all gameplay-relevant state, including:

- map/profile/directive and current tick
- economy and categorized statistics
- wave groups, timers, intermission, and progression mode
- enemy identity/rank/role, route position, health/shield, statuses, and ability timers
- tower ownership, position, doctrine/final role/Apex, cooldowns, targeting, Protocol/disruption state, and lifetime metrics
- projectiles
- Pulse Plates and Charge Forge
- pause, speed, ready state, IDs, and other deterministic counters

The host sends one checksum per second. A mismatch requests an authoritative repair instead of ending the match. A post-snapshot fence ignores stale checksums and commands that were already in flight before replacement.

`CoOpStateSnapshot` reconstructs the complete match. Snapshots are structurally validated before use. Brotli-framed messages are limited to 2 MiB on the wire and 8 MiB decoded. Ordinary framing, send queues, string lengths, entity counts, coordinates, finite values, and progression fields are bounded as well.

## Disconnect and reconnect

Valid inbound traffic resets a 15-second heartbeat timer. The sidebar distinguishes healthy, delayed, stalled, and resynchronizing states. When the timer expires:

- the shared match pauses
- the host keeps the authoritative session and rejoin code
- the guest retries automatically
- the guest may restart the application and join with the same address/code
- successful rejoin receives a complete snapshot and pending command state

Relevant wave, enemies, defenses, credits, lives, timers, pause, ready, tactical, Protocol animation, and progression state are restored. The host can keep the preserved session indefinitely or explicitly leave it.

## Pause and library

Escape, P, or the HUD control requests a shared pause. Both peers stop on the same fixed tick. While paused, placement, upgrades, sales, targeting, tactical systems, speed, and ready commands are locked. Combat, spawning, Forge production, cooldowns, and effects freeze.

An already-running early-call deadline continues during shared pause so pausing cannot create extra rewarded planning time. Both peers must ready before the deadline to earn the co-op early bonus.

Tab toggles the Tactical Library at any time in co-op. This is a local overlay: network polling and the shared simulation continue, but local battlefield input is blocked until the library closes.

## Presentation-only collaboration

Remote intent is intentionally excluded from deterministic checksums:

- cursor/crosshair
- location pings
- selected deployed tower label
- snapped tower or Plate placement ghost
- transient connection banners

Cursor state is heartbeat-refreshed and expires quickly when stale. Placement ghosts show the resolved candidate without creating an entity. A confirmed click still goes through the normal host-validated command path.

Auto selection, remote inspection, player ownership, and placement preview use distinct visual treatments so they do not resemble Slow or other enemy effects. Relevant towers/previews are raised above crowded defenses while the cue is active, then return to normal draw ordering.

## Save and restart behavior

The host writes co-op checkpoints at safe intermissions. Loading a co-op save can reopen it as a hosted match or continue it alone; all defense and progression state remains intact. Guest ownership markers remain informational in solo continuation.

Restart is a synchronized confirmed command. It retains the transport, recreates the selected profile from its initial state, clears both ready states, and waits for authoritative initialization before resuming. Main Menu ends the connection.

## Security and scope

The transport is intended for a trusted private friend connection. It validates and bounds all incoming data but does not provide confidentiality, account identity, anti-cheat, public discovery, relay service, or hostile-internet server hardening. Do not expose the port as a public long-running service.

## Required test coverage

Co-op changes should cover:

- handshake match/mismatch/timeout
- message direction and command validation
- duplicate and out-of-window sequence handling
- deterministic fixed-tick parity and checksums
- snapshot bounds, validation, capture, and reconstruction
- disconnect timeout and reconnect repair
- shared pause/ready/early-bonus rules
- shared tower control and original-owner retention
- restart and save continuation behavior
- presentation state isolation from checksums
