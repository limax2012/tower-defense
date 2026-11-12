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

Weapon sound follows the visible mechanism and the action's purpose. A precise dart needs a short, repeatable discharge; a slow explosive charge needs separate launch and detonation; a support antenna needs a quiet acknowledgment. The palette combines harmonic glow, the readable simplicity of short 8-bit-style gestures, and tactile mechanical contacts. Its richness comes from fine material detail inside a clear action. All playback is 16-bit PCM; the arcade influence describes concise contours and recognition, not reduced sample resolution.

Mechanical detail gives energy a physical source, while each energy process supplies its own identity. Quiet intervals and short tails leave room for the soundtrack and important events, just as the dark scene leaves room for luminous action. See [map art direction](map-art-direction.md) for the visual hierarchy.

## Musical foundation

The composition reference is dry, minimal tactical synth: warm sine bass, muted triangle melody, brief glassy FM accents, tiny clock-like ticks, sparse incomplete motifs, and generous silence. Its three tonal references are 97 BPM B minor with restrained chromatic detail, 103 BPM D-minor pentatonic with suspended-second color, and 100 BPM G-major/E-minor pentatonic. These are authoring references, not measured key or tempo claims about each shipped file.

Effects complement that close, restrained language through short resonant bodies, small tonal gestures, purposeful contact noise, and controlled tails. D, G, and A provide shared pitch classes where a new stable harmonic layer is useful; established signals retain their recognizable contours. Protocol's 390/585 Hz source is approximately G4/D5. Prism places a brief D5/A5 glow behind its zap. Gameplay events remain timed to the action, independently of musical tempo.

## Weapon families

The base voice remains recognizable through tier and Apex changes. Added layers increase energy density and complexity within the same family; tiers do not apply an extra runtime volume multiplier. Small local variations avoid identical repeated transients without changing synchronized gameplay.

| Family | Visible mechanism and role | Live sound layers |
| --- | --- | --- |
| Needle | Exposed precision rails and a tiny, fast turquoise dart; frequent focused fire. | A simple 26 ms rising 1080–1420 Hz core with a microscopic ion-filament edge. Its separate contact uses a shorter, quieter fragment. Cadence supplies most of the texture. |
| Watchtower | Long precision barrel and optic; narrow white lance with an extended wake. | Concentrated rail force with a fine midrange ion seam and short tensile detail. A separate confined puncture closes inward at contact. |
| Shard Fan | Orange aperture and focusing petals launch a spreading group of plasma beads. | One rounded granular plasma body divides into petal-like details. Contact uses a shorter fragment of the same plasma material, preserving the family while changing the action. |
| Frost Spire | Central blue crystal and coolant banks project a crystalline slowing charge. | The simple 55 ms sine body carries rapidly damped ice modes and fine glass-pin texture. Contact selects a different small crystalline fragment. |
| Ember Coil | Energized red coil projects a hot core with trailing licks and embers. | A contained ignition body, warm irregular thermal motion, and a tiny bright catch. A separate local heat contact establishes burning; damage ticks remain silent. |
| Breaker Cannon | Braced breech and dense gold discharge deliver direct armor-breaking force. | A dense flux-like launch with a short seating contact; actual impact carries the harder composite fracture and small secondary fragment. |
| Siege Mortar | Recessed bore ejects a slow luminous charge that bursts over an area. | Hollow charge ejection at launch; delayed pressure diffusion and unequal energy pockets on arrival. One detonation represents the complete area hit. |
| Arc Relay | Triangular electrodes and branching blue-white filaments deliver chained current. | A crisp dielectric snap, compact direct contact, and an aggregate chain with irregular smaller attachments. The gaps make branching recognizable. |
| Prism Beam | Concentric violet optical emitter directs a straight focused luminous core. | One short, clearly bending zap per attack, a finite D5/A5 harmonic afterglow, and a tiny aperture contact. The whole gesture ends; no continuous carrier is requested. |
| Signal Beacon | Green antenna and receiver packets support nearby towers. | A quiet open-fifth transmission pulse with a soft receiving-field packet. Supported attacks request it at most once per 1.15 seconds across the family. |

`TowerFired` describes one attack, including a complete multi-projectile burst. `WeaponImpacted` follows actual projectile contact or detonation and does not multiply with the number of area-damage recipients. Shard projectiles can make separate contacts, subject to the family cooldown. Arc reports its secondary contacts together. Damage-over-time does not produce another launch or ordinary impact.

Each Prism attack requests one shot at a 0.13-second family cooldown. Its immediate beam contact is suppressed so the same attack does not produce two cues. All live weapon sounds are finite one-shots. The bank's Ember and Prism sustain cores remain available to authoring tools but are not used by gameplay or the live audition palette.

## Event and interface cues

Enemy bodies are gray armored machines with bright reactor cores. Their reactions separate armor, shields, and energy failure, with scale reserved for larger bodies and bosses. Condition effects generally remain quiet. Notifications identify a change of state; they do not use the same contour as a weapon discharge.

| Cue | Actual purpose and trigger | Sound role |
| --- | --- | --- |
| `Place` | Successful tower installation. | A mount seats and latches, followed by a compact energized ready presence. |
| `Upgrade` | Successful tier, doctrine, or specialization upgrade. | A precise aperture lock supports the brief rising sine capability signal. |
| `Apex` | Final tower promotion. | A settled 390/585/780 Hz field opens into fine containment facets as the promoted emitter comes online. |
| `Sell` | Successful tower or Forge removal and refund. | A short undocking contact supports the falling triangle removal signal. |
| `Protocol` | Tower enters its temporary powered state through manual, armed automatic, Apex automatic, or Sandbox activation. | A 200 ms glowing harmonic hum with a 390/585 Hz source body and family-specific playback pitch. Manual/Sandbox actions receive notification priority; autonomous activations use quieter, less frequent combat feedback. |
| `KillSmall` | Destruction of a light enemy. | A tiny circuit cutoff and compact casing response form one complete failure. |
| `KillArmored` | Destruction of an armored or elite enemy. | Dense panel fracture, a small powered interior, and an understated secondary fragment. |
| `KillLarge` | Regenerator or boss destruction. | Contained core and casing collapse with bounded low-mid scale; a boss receives critical priority. |
| `ShieldHit` | A direct hit removes shield strength without breaking it. | A taut energy membrane absorbs contact and returns to shape, quieter than the weapon. |
| `ShieldBreak` | Shield strength actually reaches zero, including through burning damage. | Membrane tension splits into a short field dissolution. |
| `Leak` | An enemy breaches without causing terminal defeat. | A small seal interruption supports two equal warning pulses; boss breaches receive greater weight. |
| `WaveStart` | A wave starts. | A controlled lane-opening texture supports the upward triangle readiness signal. |
| `WaveClear` | A non-final wave completes. | A small field settles beneath the 520/780 Hz completion chord. |
| `PlateDeploy` | A Pulse Plate is successfully placed and armed. | A flat disc locks into place and gives its brief armed indication. |
| `PlateTrigger` | The armed plate actually discharges. | One compact radial pressure release represents the complete area effect. |
| `ForgePlace` | A Charge Forge is successfully installed. | A powered socket and fluid actuator establish the charge assembly, followed by short readiness energy. |
| `ForgeUpgrade` | A Forge capacity upgrade succeeds. | One brief irregular reconfiguration leads into discrete harmonic ready notes. |
| `ChargeReady` | A Forge actually produces a charge. | A small capture/settle contact supports the fixed 880/1175 Hz completion pair; full storage creates no repeated completion cue. |
| `BossPhase` | A boss crosses its phase threshold and powers up. | The reactor rotor tightens into denser, faster nonmetrical load. |
| `BossArrival` | A boss spawns. | Contactor and compact loaded reactor announce a larger powered threat. |
| `Victory` | The final campaign wave completes outside Endless. | A small field opening supports the brief 392/523/659 Hz terminal triad, with critical priority. |
| `Defeat` | Lives reach zero outside Sandbox. | Quiet field extinction supports the descending 392/330 Hz ending, emitted once instead of another breach warning. |
| `UiNavigate` | Intentional input changes a selection, tab, page, or relevant interface state. | A tiny rounded touch under the low-level sine selection point. |
| `UiConfirm` | An action is dispatched or a confirmation is armed. | A soft committed press reinforces the short rising confirmation. |
| `UiBack` | A screen, detail layer, picker, or confirmation is exited or canceled. | A lighter tactile release surrounds the compact falling gesture. |
| `UiDelete` | A confirmed save or history deletion succeeds. | A definite small closure finishes the two-part descending deletion gesture. |
| `UiError` | Available in the sound bank; no live caller. | Reserved rejection cue; excluded from the live audition palette. |

Hover, clicking the already-selected option, and continuous slider dragging are silent. The bank also contains unused Ember/Prism sustain, Prism impact, and Beacon impact cores; the live audition palette excludes them. Only Arc uses the chain layer. Available authoring cores do not imply live feedback.

## Sound sources and playback

`ProceduralSoundBank` supplies 44.1 kHz mono PCM through a finished playback bank and separate authoring cores. `RenderedSoundBank` loads 303 finished PCM16 WAVs from `src/MaximalBastion/Audio/Finished`, embedded under `MaximalBastion.Audio.Finished` in both desktop and browser builds:

| Finished material | Structure | Files |
| --- | --- | --- |
| Weapon shots | Ten families × four stages × three variants | 120 |
| Weapon impacts | Eight families × four stages × three variants | 96 |
| Arc chain contacts | Four stages × three variants | 12 |
| Event/interface cues | 25 cues × three variants | 75 |

Weapon filenames use `{family}-{shot|impact|chain}-{stage}-{variant}.wav`; event filenames use `cue-{lowercase-cue}-{variant}.wav`. Protocol bypasses the finished bank and keeps its exact 200 ms procedural PCM, envelope, and family pitch mapping. Its request gain and priority depend on the activation origin. UiError also stays procedural and has no live caller. Unused bank entries remain available through their authoring cores when explicitly requested; gameplay does not request them.

Finished recipes combine three kinds of source: controlled electronic gestures from `CreateWeaponCore` and `CreateCueCore`, Csound material, and selected recordings authored with ElevenLabs. Each recipe selects an explicit source fragment, shapes its frequency range and envelope, controls its level and position within the cue, and masters the resulting short event. Material bodies have different prominence and envelopes by purpose. Needle and interface gestures retain a strong simple core; shields, destruction, and pressure releases give their material more of the body. Shard launch and impact use different selections from the same plasma source.

The selected recordings live in `scripts/audio/source-materials`; [`scripts/audio/soundscape.json`](../scripts/audio/soundscape.json) records the authoritative finishing recipes, source hashes, and generation metadata. Generated material was authored under an ElevenLabs Starter account; provenance records the account tier at generation alongside the model, prompt, settings, and download hash. Rebuilding uses those exact recordings, not a fresh model response. Generation credentials remain in ignored local configuration and are not playback or rebuild inputs.

### Reproducible finishing

[`scripts/audio/finish-soundscape.py`](../scripts/audio/finish-soundscape.py) is the offline finishing entry point. It uses the .NET SDK to export the controlled cores, NumPy 2.5.4, FFmpeg/FFprobe on `PATH`, archived selected material, and the explicit finishing recipes. It does not call a generation service.

```powershell
python -m pip install -r scripts/audio/requirements.txt
python scripts/audio/finish-soundscape.py --check
```

`--check` rebuilds into ignored `.artifacts/audio-finishing` and compares the checked-in WAVs without replacing them. Omit it to write rebuilt finished assets. The renderer validates every source hash, records NumPy/FFmpeg versions, and reports per-cue duration, peak/RMS, DC, endpoints, and spectral allocations. Byte-identical reproduction depends on the recorded sources, recipes, cores, and tool versions. `--dotnet <executable>` selects an SDK explicitly; `--no-build` reuses an existing Release core exporter while retaining source-hash checks.

The individual authoring cores can also be exported without running regression tests or loading the finished bank:

```powershell
dotnet run --project tests\MaximalBastion.Tests -c Release -- --export-audio-cores .artifacts\audio-cores
```

This exports every supported core layer, including unused compatibility entries. The finishing recipes select the live subset. Normal builds and playback use the embedded assets and require none of these external authoring tools.

### Csound source material

[`scripts/audio/materials.orc`](../scripts/audio/materials.orc) authors the source material for Shard, Ember, and Frost. Shard uses overlapping formant grains and rounded resonant plasma noise; Ember uses filtered thermal turbulence, a nearly steady core, and small ignition crackles; Frost uses its 55 ms 610–360 Hz body with rapidly damped crystal modes. Csound's [formant-grain synthesis](https://csound.com/docs/manual/fof.html), [modal resonator](https://csound.com/docs/manual/mode.html), and [resonant filter](https://csound.com/docs/manual/reson.html) provide separate material controls.

[`scripts/audio/render-materials.py`](../scripts/audio/render-materials.py) produces 72 mono 44.1 kHz PCM16 source WAVs under `src/MaximalBastion/Audio/Samples`: three families, shot/impact layers, four stages, and three variants. Its `--check` mode compares fresh renders with those source WAVs. On Windows it downloads a pinned, checksum-verified portable Csound 6.18.1 into ignored `.artifacts/audio-tools`; other platforms can provide an installed Csound 6.18 executable with `--csound <executable>`.

### Runtime mix

`AudioDirector` subscribes to local session events and unsubscribes on detach. Audio timers, variation, voice admission, and panning are presentation state, excluded from snapshots and checksums. Material voices have three sample variants, and combat one-shots receive small pitch changes. Protocol is exempt from that variation, preserving its family transposition for every activation origin. Interface and notification signals keep fixed intervals so their meaning remains stable. The soundtrack is independent of effect synthesis.

`SoundMixer` uses real elapsed time at both gameplay speeds. Family cooldowns and voice limits bound overlapping volleys, chains, area damage, and identical towers. It permits at most 20 effect voices, including at most 12 combat/detail voices, four detail voices, and three interface voices. Summed peak reservations are limited to 0.36 for combat/detail and 0.58 overall before the SFX volume setting. The 0.22 difference matches the mastered one-shot peak ceiling and leaves capacity outside routine combat for other cue categories.

Each finite voice reserves the largest PCM peak still ahead of its playhead, including delayed transients after silent gaps. The envelope uses 10 ms source bins and a 25 ms real-time scheduling margin before pitch conversion. Reservations therefore decline as strong transients finish while retaining headroom for any later attack. They use the voice's admitted gain before shared attenuation; releasing that attenuation cannot violate the reservation. Loop support reserves the full sample peak for every repetition. Current live requests are finite one-shots.

Combat/detail voices share a mild gain reduction driven by their admitted PCM power. The detector combines short source-power windows with each voice's gain and envelope. Estimated RMS from 0.035 to 0.075 maps to 0–2 dB of attenuation, with a 120 ms attack and 600 ms release. The detector uses unducked levels, and the resulting gain applies equally to existing and newly admitted combat voices. Enemy count does not enter effect gain. Notifications, interface feedback, and critical cues retain their own levels. Clearing playback resets the shared gain immediately.

Admission first applies the one-shot mastering curve. A combat/detail voice may then lose at most 25% of that mastered gain to fit the remaining peak budget; a request requiring a larger reduction is rejected. This admission trim and the shared 0–2 dB reduction are separate, with a combined maximum of about 4.5 dB below the mastered gain. Higher-priority requests can reclaim eligible lower-priority voices. Reclamation chooses the lowest category, then the smallest remaining peak, then the oldest voice; replacements are committed only after the incoming request passes every admission check.

`GameSession.ProtocolActivated` reports the activation origin as event metadata. Manual and Sandbox activations request gain 0.52 at notification priority with a 0.20-second family cooldown. Armed and Apex automatic activations request gain 0.34 at combat priority with a 0.65-second cooldown, share combat headroom, and are suppressed while combat presentation is paused. A manual request can override an automatic request's lower-priority family cooldown. The event does not add synchronized state or alter activation rules.

`AudioManager` owns MonoGame playback instances, applies platform-specific immediate one-shot parameters, and synchronizes existing voices with shared mix gain and the SFX control. Music and SFX controls remain separate. Muting SFX clears active effects and mix attenuation. An unavailable or lost audio device cannot interrupt gameplay or overwrite the user's volume preference.

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

`--music-root <Content/Audio>` and `--ffmpeg <executable>` support other working directories. The command exports a timed live palette reel, individual base/Apex samples, and six 18-second Sandbox scenarios: a quiet opening, a mixed group, dense late combat at 1×/2×, and bosses at 1×/2×. Real session events pass through the production director and mixer. Each scenario contains an effects stem, a mix with shipped music at default settings, an event trace, and measured peak/RMS/voice counts. The controlled boss scenarios force the damage threshold after eight seconds to exercise overdrive.

Traces record each request, its admission or rejection reason, voice starts, and natural retirement or priority preemption. Gain diagnostics separate requested gain, one-shot mastering, peak-budget trimming, and shared combat attenuation. Per-family counts distinguish requests, admitted voices, and preemptions, including voices preempted before their first rendered frame. Scenario assertions account for every request and admitted voice and check voice caps, remaining-peak reservations, and mixed-output headroom. Envelope regressions cover delayed transients, pitch-adjusted reservations, loops, quiet recovery, and transactional rejection; Protocol regressions cover activation origins, priority, pause, and unchanged simulation checksums. Offline rendering approximates device panning; it is reproducible event and mix evidence, not a substitute for listening to device output.

For an audible review:

1. In Sandbox Lab, isolate each family and compare base, tier, and Apex fire. Listen to launch and contact separately, and check the delayed Mortar burst, Arc chains, and Beacon support rate.
2. Isolate Prism and check that each admitted attack is one recognizable zap with a brief harmonic end. Its immediate contact must not double the sound, and target loss or pause must leave no continuous tone.
3. Compare manual and automatic Protocol activation, including manual activation during an automatic cue's cooldown. Trigger plates, install and upgrade a Forge, produce a charge, and fill its storage. Check placement, upgrades, Apex, and selling.
4. With music enabled, play quiet openings, mixed groups, dense late waves, and bosses at both normal and accelerated speed. Check that repeated fire leaves music audible and threat cues remain identifiable.
5. Check shield contacts and breaks, small and large deaths, breaches, boss transitions, wave transitions, victory, and defeat. Burning ticks and area damage must not create a flood of cues.
6. Navigate menus, open and cancel confirmations, delete a disposable save/history record, mute each bus, and adjust volume during active playback. Check that changed selections receive feedback and hover/slider motion remain quiet.

Listen for family recognition, meaningful event timing, readable harmonic gestures, tactile contact detail, restrained repeated transients, comfortable upper-frequency energy, controlled bass, and short tails. Automatic checks cover source integrity, finite samples, event behavior, and bounded mixing. Perceived musical fit and acceptance of the complete soundscape require the full runtime review above.
