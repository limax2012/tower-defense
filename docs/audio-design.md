# Sound design

Weapon effects center on colorful laser sweeps, overlapping plasma chirps, and shimmering energy carriers. Quiet grain, pressure, and contact layers support those electronic identities. Tier changes add coupled energy bands and weight while preserving each weapon's contour. Three generated variations and small presentation-only pitch changes reduce repeated transients. The shipped menu loop, gameplay playlist, shuffle behavior, activity gain, and fallback music remain independent of effect synthesis.

| Family | Launch / operation | Contact |
| --- | --- | --- |
| Needle | Tight, bright laser dart with an upper-octave edge | Small energy spark |
| Watchtower | Long ion lance with a deep electrical tail | Dense energy collapse with an armor accent |
| Shard Fan | Overlapping plasma chirps in one spreading burst | Layered plasma splash |
| Frost Spire | Rising, glassy crystal energy | Bright descending crystal shimmer |
| Ember Coil | Warm, rounded plasma pulse | Low thermal energy bloom |
| Breaker Cannon | Deep, compact energy bolt | Fast electrical punch with an armor accent |
| Siege Mortar | Rising launched charge | Delayed, deep energy collapse |
| Arc Relay | Bright, rough electrical sweep | Short electrical leaps identify chaining |
| Prism Beam | Rising focus and a smooth, shimmering optical carrier | Sustained emitter replaces repeated hit cues |
| Signal Beacon | Quiet rising transmission pulse during supported attacks | Bounded support feedback |

`ProceduralSoundBank` creates device-independent 44.1 kHz mono PCM. One-shots combine pitch glides and evolving harmonic color with short edge fades; sustained emitter samples have crossfaded seams. Prism uses a quieter shared carrier with slow shimmer and a light noise layer. Event cues cover mounting, upgrades, Apex, sale, Protocol, plate deployment/trigger, Forge installation/upgrade, charge completion, shields, material-scaled destruction, breaches, boss arrival/overdrive, wave transitions, victory/defeat, and interface actions. Boss overdrive uses contactor, pressure, and accelerating rotor layers. Interface navigation uses short electronic chirps; confirmation, back, and deletion have distinct rising or falling gestures.

`AudioDirector` subscribes to local session events and unsubscribes on detach. `TowerFired` represents one attack, including a whole multi-projectile burst. `WeaponImpacted` follows actual contact or detonation and is independent of the number of damage recipients. Damage-over-time does not replay a launch or impact; shield collapse can still announce its actual transition. Beacon pulses follow supported attacks. A Prism engagement leases one shared continuous emitter, which releases after target loss, pause, or detach. All audio timers, variation, admission, and position panning are presentation state and are excluded from snapshots and checksums.

`SoundMixer` uses real elapsed time at both gameplay speeds. Family cooldowns and voice limits prevent volleys, chain victims, area damage, and many identical towers from stacking unbounded cues. It permits at most 20 effect voices, including at most 12 combat/detail voices and four detail voices. Conservative summed source-peak budgets reserve space for music and notifications: 0.30 for combat and 0.58 overall before the SFX volume setting. Threat and interface cues can reclaim lower-priority voices. Dense enemy populations reduce routine combat gain smoothly. Tier upgrades change timbre rather than increasing source peak.

`AudioManager` owns MonoGame playback instances, synchronizes active voices with the SFX control, and applies platform-specific immediate one-shot parameters. Music and SFX controls remain separate. Interface feedback follows changed selection/dialog state; hover, clicking the current option, and continuous slider dragging are silent.

## Verification

Run deterministic regression and hidden visual checks with `scripts/verify.ps1`. `scripts/verify-audio-mix.ps1` checks the mastered music assets with FFmpeg.

With FFmpeg on `PATH`, export effect samples and representative full mixes from the repository root:

```powershell
dotnet run --project tests\MaximalBastion.Tests -c Debug -- --verify-audio .artifacts\audio
```

`--music-root <Content/Audio>` and `--ffmpeg <executable>` support other working directories. The command exports a timed palette reel, individual base/Apex samples, and six 18-second Sandbox scenarios: a quiet opening, a mixed group, dense late combat at 1×/2×, and bosses at 1×/2×. Real session events pass through the production director and mixer; each scenario contains an effects stem, a mix with shipped music at default settings, an event trace, and measured peak/RMS/voice counts. The controlled boss scenarios force the damage threshold after eight seconds to exercise overdrive. Offline rendering approximates device panning; it does not establish perceived balance or substitute for listening to device output.

For an audible review, use Sandbox Lab to isolate each family, compare base/tier/Apex fire, disable and re-enable Prism during an engagement, and trigger Protocols and plates. Then play quiet openings, mixed groups, late waves, and bosses at both speeds with music enabled. Check placement, sale, Forge production, wave/results transitions, menu selections, confirmation cancellation, deletion, mute, and live volume adjustment. Listen for distinct energy identities, audible music, restrained routine damage, readable warnings, and clean sustained release.
