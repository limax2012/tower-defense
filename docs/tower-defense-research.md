# Tower-Defense Design Research

This note records transferable design principles for Minimal Bastion. It is not a feature checklist and does not authorize copying another game's content, terminology, visual identity, balance values, maps, or progression.

## Sources reviewed

- [Bloons TD 6 — publisher-authored Steam page](https://store.steampowered.com/app/960090/Bloons_TD_6/): combines a broad tower roster with heroes, long-term updates, challenge variety, and online co-op. The durable lesson is that replayability grows when the same core combat supports meaningfully different loadouts and constraints.
- [Kingdom Rush — Ironhide](https://www.ironhidegames.com/Games/kingdom-rush): emphasizes tower specializations, enemies with distinct abilities, and limited active battlefield interventions such as reinforcements. The durable lesson is to make counterplay legible and give the player a few timely actions outside ordinary tower placement.
- [Mindustry — official site](https://mindustrygame.github.io/): combines incoming waves with material processing and multiplayer. The durable lesson is that production can create strategic tension when it competes with immediate defense, but the logistics layer must remain proportionate to the game's scope.
- [Mindustry — official source repository](https://github.com/Anuken/mindustry): exposes configurable rules, wave pacing, multiple game modes, and placement constraints. The durable lesson is to keep scenarios and balance data-driven so new challenges do not require rewriting combat code.
- [Isle of Arrows — developer/publisher-authored Steam page](https://store.steampowered.com/app/1946970/Isle_of_Arrows/): fuses tower defense with constrained, variable tile draws. The durable lesson is that replay variation can come from forcing adaptation, not merely from increasing enemy health.

## Principles selected for Minimal Bastion

### 1. Every strategic purchase should answer a readable problem

Tower identities should be visible in their range, cadence, targeting, silhouette, and concise UI copy. Enemy waves should communicate why armor break, control, splash, long range, or focused damage matters. A generalist may be convenient, but it should not erase specialist value.

Minimal Bastion application:

- Preserve the ten-tower roster and three levels rather than adding breadth prematurely. Two tier-two doctrines establish an early build direction, while either final role remains available so each tower has four completed combinations without a sprawling progression tree.
- Strengthen combinations already supported by the combat model: burn reduces effective armor, Arc Relay rewards slowed targets in proportion to Slow strength, and exposed targets reward follow-up damage. Prefer visible bonuses and strongest-only diminishing returns over hard status incompatibilities that invalidate mixed defenses.
- Use deterministic telemetry to verify that a tower's intended job appears in damage, kill, control, and support outcomes.

### 2. Active intervention is strongest when scarce and anticipated

An emergency action is interesting when the player can read the incoming threat, reserve the resource, and choose a location. If it is cheap enough to spam, it becomes another tower; if it is too rare or opaque, it is forgotten.

Minimal Bastion application:

- Pulse Plates remain a limited road-snapped emergency defense with explicit charges.
- The Charge Forge turns present-day spending into future tactical capacity, creating an understandable defense-versus-economy choice.
- Wave intel should expose enough composition information for a plate deployment to feel planned rather than lucky.

### 3. Production must compete with survival

Production systems work because they create an opportunity cost. Their value should arrive late enough to be a gamble, but early enough to matter when purchased responsibly.

Minimal Bastion application:

- Keep one generator per map, visible production progress, a storage cap, and upgradeable cadence.
- Track generated versus directly purchased plates separately so balance work can detect when the generator is mandatory, irrelevant, or exploitative.
- Do not add a full logistics network to version 1; it would overwhelm the clean continuous-placement defense game.

### 4. Replayability should change decisions, not only numbers

Interesting repeat runs arise from different constraints, information, or availability. Pure stat inflation tends to preserve the same solution while making it slower.

Minimal Bastion application:

- Keep authored mixed waves as the campaign's learnable backbone.
- Use deterministic seeds and strategy profiles for testing.
- Implemented directives now provide a full sandbox, two restricted tower rosters, and a Fundamentals mode built around permanent towers without Plates, Forge, or Protocols. A daily seeded ruleset remains a candidate; it should reuse the same authoritative restriction and telemetry seams.

### 5. Multiplayer requires shared rules before networking

Co-op is not just networking. It requires ownership rules, shared economy decisions, placement conflict handling, pause/speed authority, result attribution, and readable teammate intent.

Minimal Bastion application:

- Keep authoritative simulation state separate from presentation and input.
- Sequence all player intent through the same validated deterministic command seam used by solo play.
- The implemented direct-internet MVP uses shared resources and unrestricted shared tower management, while retaining original placer attribution, joint wave ready, checksums, repair snapshots, reconnect, and pings; hosted relay/NAT traversal remains infrastructure work rather than gameplay logic.

## Explicit non-goals

- No copied tower concepts, names, maps, art, wave tables, progression trees, or numerical balance.
- No sprawling metagame, hero roster, randomized tile board, or factory logistics layer in the current release.
- No networking shortcut that bypasses authoritative validation or deterministic checks merely to claim broader connectivity.

## Near-term implementation order informed by this research

1. Preserve the reproducible four-map, twelve-strategy balance baseline while expanding build choices.
2. Human-playtest doctrine/final-role combinations, active intervention, Surge Nodes, and end-run analysis.
3. Human-playtest the implemented map/difficulty/directive combinations and preserve statistically persistent outliers.
4. Use existing source-attributed support/status telemetry before changing low-damage utility towers.
5. Field-test direct internet co-op and reconnect before considering hosted relay infrastructure.
