# Two-Player Online Co-op Architecture

## Current player workflow

Minimal Bastion supports direct internet co-op between two copies of the same build:

1. Host selects the map and chooses **Online Co-op > Host Online Game**.
2. Host forwards TCP `28741` to the host PC and allows the executable through the firewall if required.
3. Host shares the displayed six-character code plus public IP/DNS name.
4. Player 2 enters `host`, `host:port`, IPv4, DNS, or bracketed IPv6 plus the code.

The listener is dual-stack and binds all adapters. The join code is a lightweight session gate, not encryption or account authentication. A peer-to-peer VPN can provide reachability when router forwarding is unavailable.

## Match rules

- Two players share credits, lives, wave state, speed, emergency inventory, and the single Charge Forge.
- Towers and the forge retain an owner. Only that owner may retarget, upgrade, specialize, Overdrive, or sell the structure.
- Either player may place towers and use shared Pulse Plates.
- Both players must ready every wave. The host queues the authoritative start only after both bits are set.
- A jointly early-called intermission awards the normal shared 20-credit reward.
- Pause is disabled in online play; speed changes are shared authoritative commands.
- Middle-click emits a transient cyan/coral player ping.
- Result restart ends the online session rather than silently creating divergent rematches.

Shared economy is the smallest understandable cooperative model, while ownership prevents destructive griefing. Resource gifting is unnecessary because all spend already uses the same pool.

## Deterministic command seam

Remote intent is a `GameCommand` applied through `GameCommandProcessor`. Commands carry player identity, request/sequence IDs, action type, entity/definition identity, placement coordinates, branch, targeting mode, and speed where relevant.

The host:

1. receives a local or remote request;
2. assigns a monotonic sequence and duplicate-safe receipt;
3. schedules accepted input on a future fixed tick;
4. applies the same scheduled command locally;
5. broadcasts the authoritative command and tick.

Both peers advance `DeterministicSessionRunner` with a fixed simulation step. `SessionChecksum` includes map, waves, shared economy, enemies, towers, ownership, targeting, branches, Overdrive state/cooldown, projectiles, Pulse Plate handled-enemy IDs, and forge state. Peers exchange periodic tick/checksum messages and fail clearly on divergence or a command arriving after its tick.

Network code never implements a second copy of placement, affordability, ownership, upgrade, tactical, or selling rules; it calls the same validated `GameSession` methods as solo UI and automated players.

## Transport

- Newline-delimited JSON envelopes over `TcpClient`/`TcpListener`.
- Protocol version validation on every envelope.
- Maximum message length: 65,536 characters.
- TCP `NoDelay` enabled for command responsiveness.
- Six-character code handshake before the host accepts Player 2.
- Message types: hello/welcome/rejected, command request/receipt/authoritative command, start session, ready/wave ready, tick sync, ping, and disconnect.
- Map ID travels in `StartSession`; Player 2 instantiates the exact host map before readying.
- Host command input delay: six fixed ticks, providing a small latency buffer.

## Test coverage

- Valid direct transport handshake and command/receipt serialization through `localhost`.
- Invalid join-code rejection at client and host.
- DNS, IPv4/default-port, explicit-port, and bracketed IPv6 endpoint parsing.
- Owner-only placement mutation, targeting, upgrade, specialization, Overdrive, and duplicate rejection.
- Mirrored deterministic placement, wave start, Overdrive, active duration, cooldown, ownership, and final checksum.
- Wave-ready coordinator behavior.
- Map identity in checksums and map/session construction.
- Graceful connection close detection.

The native menu, address/code fields, map selection label, and lobby presentation have also been visually inspected.

## Current limitations

- No matchmaking, lobby directory, hosted relay, automatic NAT traversal, or UPnP/NAT-PMP mapping.
- Host must be reachable through manual port forwarding or a VPN.
- Transport is not encrypted; do not send secrets through the protocol. Current messages contain gameplay commands and state hashes only.
- Reconnect is supported through the existing join code and a host-authoritative recovery snapshot, but host migration, spectators, and more-than-two-player support are not.
- Both peers must run identical executable/content versions; build/content fingerprints reject incompatible peers before play.
- Six-tick buffering is suitable for ordinary direct connections but has not been field-tested across high-latency remote routes.
- Windows firewall/router behavior cannot be configured by the game.

## Highest-value networking follow-up

1. Field-test two remote PCs under latency/loss and tune the command buffer.
2. Evaluate automatic port mapping as an optional convenience.
3. If infrastructure is authorized, add a small encrypted rendezvous/relay service so players do not need router configuration.
4. Add host migration only if private two-player testing demonstrates that it justifies the added state-transfer complexity.
