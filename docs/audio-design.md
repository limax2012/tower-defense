# Sound design

## Art direction

Maximal Bastion depicts a municipal power and transit network under siege. Compact graphite and ceramic machines occupy dark utility districts. Armor, rails, focusing apertures, coolant banks, coils, and antennas explain how the equipment works. Their active energy is bright, geometric, and color-coded: narrow turquoise darts, orange plasma fragments, crystalline blue coolant, red heat, violet optics, and green signals. Clear silhouettes and bounded luminous effects remain more prominent than surface detail.

The four districts share this visual language while giving it different surroundings:

| District | Materials and atmosphere |
| --- | --- |
| Cinderworks | Warm graphite and copper, exposed heat exchangers, pipework, hazard strips, and orange heat. |
| Rainline Heights | Cool blue-gray wet decks and elevated causeways, deep cooling fins, recessed utility galleries, and sparse cyan lights. |
| Prism Nullspace | Suspended violet data structures, precise grids, luminous bus connections, and dark voids. |
| Helix Reactor | Deep green containment machinery, jade conduits, enclosed circular cores, and concentric rings. |

Weapon sound follows the visible mechanism and the action's purpose. A precise dart needs a short, repeatable discharge; a slow explosive charge needs separate launch and detonation; a support antenna needs a quiet acknowledgment. Mechanical detail gives energy a physical source, while the energy behavior supplies each family's identity. Quiet intervals and short tails leave room for the soundtrack and important events, just as the dark scene leaves room for luminous action. See [map art direction](map-art-direction.md) for the visual hierarchy.

## Weapon families

The base voice remains recognizable through tier and Apex changes. Added layers increase energy density and complexity within the same family; tiers do not apply an extra runtime volume multiplier. Small local variations avoid identical repeated transients without changing synchronized gameplay.

| Family | Visible mechanism and role | Live sound layers |
| --- | --- | --- |
| Needle | Exposed precision rails and a tiny, fast turquoise dart; frequent focused fire. | A simple 26 ms rising energy pulse, 1080–1420 Hz at base, followed by a much quieter contact spark. Its cadence supplies most of the texture. |
| Watchtower | Long precision barrel and optic; narrow white lance with an extended wake. | A tight, forceful rail release and concentrated ion tail; a separate dense contact with an armor accent. Greater energy density distinguishes it from Needle. |
| Shard Fan | Orange aperture and focusing petals launch a spreading group of plasma beads. | One rounded granular plasma burst per attack. Short clustered contacts suggest plasma fragments without a high squealing glide or a launch cue for every bead. |
| Frost Spire | Central blue crystal and coolant banks project a crystalline slowing charge. | A simple 55 ms sine body with subtle modal ice detail; contact adds a small brittle crystalline edge. The cool texture stays compact. |
| Ember Coil | Energized red coil projects a hot core with trailing licks and embers. | Brief ignition and warm turbulent expansion, with a separate restrained thermal contact. Burning damage does not repeat a bass sweep or create a cue on every tick. |
| Breaker Cannon | Braced breech and dense gold discharge deliver direct armor-breaking force. | Compressed launch, direct punch, and short fracture/contact detail. Weight is concentrated in the impact rather than a long explosive tail. |
| Siege Mortar | Recessed bore ejects a slow luminous charge that bursts over an area. | A loaded ejection at launch; a separate, delayed pressure detonation on arrival. One detonation represents the whole area hit. |
| Arc Relay | Triangular electrodes and branching blue-white filaments deliver chained current. | A crisp electrical release and irregular branch contacts. A chained hit uses one aggregate chain gesture rather than one full cue per victim. |
| Prism Beam | Concentric violet optical emitter maintains a straight focused luminous core. | Small engagement onset plus a steady, quiet luminous underlay. A shared renewable sustain fades on disengagement; repeated beam contacts are silent. |
| Signal Beacon | Green antenna and receiver packets support nearby towers. | Quiet transmission pulses when supported towers attack, limited to one pulse per 1.15 seconds across the family. It has no damage-contact sound. |

`TowerFired` describes one attack, including a complete multi-projectile burst. `WeaponImpacted` follows actual projectile contact or detonation and does not multiply with the number of area-damage recipients. Shard projectiles can make separate contacts, subject to the family cooldown. Arc reports its secondary contacts together. Damage-over-time does not produce another launch or ordinary impact.

Prism's onset is tracked per tower engagement, while all active Prism towers share one sustained voice. Renewing its lease does not restart its carrier or alternate timbre between different tower tiers. Target loss, pause, and detach release it. Ember is a projectile weapon and does not request the bank's available sustain sample.

## Event and interface cues

Enemy bodies are gray armored machines with bright reactor cores. Their reactions separate armor, shields, and energy failure, with scale reserved for larger bodies and bosses. Condition effects generally remain quiet. Notifications identify a change of state; they do not use the same contour as a weapon discharge.

| Cue | Actual purpose and trigger | Sound role |
| --- | --- | --- |
| `Place` | Successful tower installation. | Compact mounting contact and energized ready indication. |
| `Upgrade` | Successful tier, doctrine, or specialization upgrade. | A brief rising sine confirms increased capability. |
| `Apex` | Final tower promotion. | A settled field opens into staged 390/585/780 Hz harmonics as the promoted emitter comes online. |
| `Sell` | Successful tower or Forge removal and refund. | A short falling triangle pulse releases the device. |
| `Protocol` | Tower enters its temporary powered state. | A 200 ms glowing harmonic hum with a 390/585 Hz source body and family-specific playback pitch. Its stable body announces energizing rather than projectile travel. |
| `KillSmall` | Destruction of a light enemy. | A small circuit crack and restrained panel resonance. |
| `KillArmored` | Destruction of an armored or elite enemy. | Lower armor fracture and a short secondary fragment. |
| `KillLarge` | Regenerator or boss destruction. | Compact pressure, casing breakup, and reactor collapse; a boss receives critical priority. |
| `ShieldHit` | A direct hit removes shield strength without breaking it. | A brief elastic, inharmonic field tap, quieter than the weapon. |
| `ShieldBreak` | Shield strength actually reaches zero, including through burning damage. | A split field resonance and fluttering scatter distinguish collapse from a touch. |
| `Leak` | An enemy breaches without causing terminal defeat. | Two equal warning pulses; boss breaches receive greater weight. |
| `WaveStart` | A wave starts. | A concise upward triangle readiness signal. |
| `WaveClear` | A non-final wave completes. | A settled 520/780 Hz harmonic acknowledgment that leaves music in place. |
| `PlateDeploy` | A Pulse Plate is successfully placed and armed. | Compact seating contact and a short armed ping. |
| `PlateTrigger` | The armed plate actually discharges. | One broadband pressure burst with restrained low-mid weight for the complete area effect. |
| `ForgePlace` | A Charge Forge is successfully installed. | Heavier socket contact, a small motor rise, and a steady readiness hum. |
| `ForgeUpgrade` | A Forge capacity upgrade succeeds. | A brief assembly cycle followed by two discrete harmonic ready notes. |
| `ChargeReady` | A Forge actually produces a charge. | Paired 880/1175 Hz completion pings; full storage creates no repeated completion cue. |
| `BossPhase` | A boss crosses its phase threshold and powers up. | Contactor strike and accelerating inharmonic reactor load identify increased danger. |
| `BossArrival` | A boss spawns. | A longer reactor-load announcement with a second contact and greater body presence. |
| `Victory` | The final campaign wave completes outside Endless. | A brief 392/523/659 Hz triad with higher priority than routine combat. |
| `Defeat` | Lives reach zero outside Sandbox. | Two descending notes, 392 then 330 Hz, emitted once instead of another breach warning. |
| `UiNavigate` | Intentional input changes a selection, tab, page, or relevant interface state. | A tiny, low-level sine tick. |
| `UiConfirm` | An action is dispatched or a confirmation is armed. | A short rising sine confirmation. |
| `UiBack` | A screen, detail layer, picker, or confirmation is exited or canceled. | A compact falling triangle gesture. |
| `UiDelete` | A confirmed save or history deletion succeeds. | Two short descending notes mark removal, separate from opening its confirmation. |
| `UiError` | Available in the sound bank; no live caller. | Reserved rejection cue; excluded from the live audition palette. |

Hover, clicking the already-selected option, and continuous slider dragging are silent. The bank also contains unused Ember sustain, Prism impact, and Beacon impact entries; the live audition palette excludes them. Only Arc uses the chain layer. Available bank entries do not imply live feedback.

## Sound sources and playback

`ProceduralSoundBank` supplies device-independent 44.1 kHz mono PCM. Simple pulses, harmonic notifications, contacts, and continuous carriers remain procedural. Csound-authored WAV material layers add shaped spectral texture to Shard Fan, Ember Coil, and Frost Spire. Those WAVs are embedded in the game; playback does not require Csound, a plugin host, or downloaded content.

The authored source is [`scripts/audio/materials.orc`](../scripts/audio/materials.orc). Shard uses short overlapping formant grains and rounded resonant plasma noise; Ember uses filtered thermal turbulence, a nearly steady energized core, and tiny ignition crackles; Frost uses a 55 ms 610–360 Hz body with quiet, rapidly damped crystal modes. Csound's [formant-grain synthesis](https://csound.com/docs/manual/fof.html), [modal resonator](https://csound.com/docs/manual/mode.html), and [resonant filter](https://csound.com/docs/manual/reson.html) provide separate controls for each material instead of imposing one oscillator contour on every family.

[`scripts/audio/render-materials.py`](../scripts/audio/render-materials.py) renders 72 mono 44.1 kHz PCM16 WAVs under `src/MaximalBastion/Audio/Samples`: three families, shot/impact layers, four stages, and three variants. Filenames use `{family}-{shot|impact}-{stage}-{variant}.wav`. The renderer fades both edges, removes DC, and normalizes shots to a 0.28 peak and impacts to 0.22 before runtime gain and mixing.

```powershell
python scripts/audio/render-materials.py
python scripts/audio/render-materials.py --check
```

On Windows the renderer downloads a pinned, checksum-verified portable Csound 6.18.1 into ignored `.artifacts/audio-tools`. Other platforms can use an installed Csound 6.18 executable or pass `--csound <executable>`. `--check` renders into ignored artifacts and compares checked-in WAV bytes without replacing them. Normal game builds use the embedded samples directly.

`AudioDirector` subscribes to local session events and unsubscribes on detach. Audio timers, variation, voice admission, and panning are presentation state, excluded from snapshots and checksums. Material voices have three sample variants, and combat one-shots receive small pitch changes. Interface and state signals keep fixed intervals so their meaning remains stable. The soundtrack is independent of effect synthesis.

`SoundMixer` uses real elapsed time at both gameplay speeds. Family cooldowns and voice limits bound overlapping volleys, chains, area damage, and identical towers. It permits at most 20 effect voices, including at most 12 combat/detail voices and four detail voices. Conservative summed source-peak budgets are 0.30 for combat and 0.58 overall before the SFX volume setting. Threat and interface cues can reclaim lower-priority voices. Dense enemy populations smoothly reduce routine combat gain. Sustained voices have short attack/release envelopes and crossfaded sample seams.

`AudioManager` owns MonoGame playback instances, applies platform-specific immediate one-shot parameters, and synchronizes existing voices with the SFX control. Music and SFX controls remain separate. An unavailable or lost audio device cannot interrupt gameplay or overwrite the user's volume preference.

## Music and mix evidence

The shipped assets are `MainMenuLoop.wav` and ten gameplay tracks: `BassyRollIn`, `ChillStroll`, `FocusedDanceParty`, `IcyInvestigation`, `KickbackHall`, `MildlyUpbeatArpeggios`, `NightclubHalo`, `QuietInvestigation`, `SneakFocusMission`, and `XylophoneBallad`. These names are asset metadata, not evidence of instrumentation, key, tempo, or perceived balance.

The menu loop, gameplay shuffle, activity-dependent gain, separate volume control, and fallback music retain their existing playback behavior. All districts draw from the gameplay playlist. Effects leave the continuous musical role to that soundtrack: routine attacks have bounded duration and gain, support and hit details stay quieter, and important transitions can claim space from combat voices.

The shipped files have the following measured levels:

| Assets | Integrated loudness | True peak | Loudness range |
| --- | --- | --- | --- |
| Menu loop | -11.99 LUFS | -1.00 dBTP | 4.5 LU |
| Ten gameplay tracks | -16.01 to -15.92 LUFS | -4.49 to -2.00 dBTP | 2.0–5.3 LU |

In five-band RMS analysis, the strongest band lies below 120 Hz for eight gameplay tracks; `IcyInvestigation` and `XylophoneBallad` favor 120–600 Hz. Median spectral centroids span 328–1645 Hz. These measurements support keeping frequent effects clear of heavy sustained bass and avoiding an always-present bright noise layer. They do not identify instruments or establish subjective musical character.

`scripts/verify-audio-mix.ps1` measures integrated loudness, true peak, and loudness range with FFmpeg. Its menu target is -12 LUFS ±0.35, at most -0.8 dBTP, and at most 7 LU LRA. Gameplay targets are -16 LUFS ±0.35, at most -1.5 dBTP, and at most 7 LU LRA. Passing those limits or exporting a mix does not establish that the music is comfortably audible or that the effects are aesthetically compatible. Those judgments require listening to the complete mix.

## Verification and audition

Run deterministic regression and hidden visual checks with `scripts/verify.ps1`. Run `scripts/verify-audio-mix.ps1` with FFmpeg installed to measure every shipped music asset.

With FFmpeg on `PATH`, export the live effects palette and representative full mixes from the repository root:

```powershell
dotnet run --project tests\MaximalBastion.Tests -c Debug -- --verify-audio .artifacts\audio
```

`--music-root <Content/Audio>` and `--ffmpeg <executable>` support other working directories. The command exports a timed live palette reel, individual base/Apex samples, and six 18-second Sandbox scenarios: a quiet opening, a mixed group, dense late combat at 1×/2×, and bosses at 1×/2×. Real session events pass through the production director and mixer. Each scenario contains an effects stem, a mix with shipped music at default settings, an event trace, and measured peak/RMS/voice counts. The controlled boss scenarios force the damage threshold after eight seconds to exercise overdrive. Offline rendering approximates device panning; it is reproducible event and mix evidence, not a substitute for listening to device output.

For an audible review:

1. In Sandbox Lab, isolate each family and compare base, tier, and Apex fire. Listen to launch and contact separately, and check the delayed Mortar burst, Arc chains, and Beacon support rate.
2. Disable and re-enable Prism during an engagement. Check its onset, shared continuous body, and release after target loss, pause, and leaving the run.
3. Trigger Protocols and plates, install and upgrade a Forge, produce a charge, and fill its storage. Check placement, upgrades, Apex, and selling.
4. With music enabled, play quiet openings, mixed groups, dense late waves, and bosses at both normal and accelerated speed. Check that repeated fire leaves music audible and threat cues remain identifiable.
5. Check shield contacts and breaks, small and large deaths, breaches, boss transitions, wave transitions, victory, and defeat. Burning ticks and area damage must not create a flood of cues.
6. Navigate menus, open and cancel confirmations, delete a disposable save/history record, mute each bus, and adjust volume during active playback. Check that changed selections receive feedback and hover/slider motion remain quiet.

Listen for family recognition, meaningful event timing, restrained repeated transients, comfortable upper-frequency energy, controlled bass, short tails, and clean sustained release. Measurements and event traces support this review; they do not record subjective listening results.
