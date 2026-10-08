# Cinderworks 3D development

## Delivery target

One complete Windows Cinderworks campaign in Unity URP, using the existing authored content and C# simulation. Browser support is optional. The MonoGame desktop and browser clients remain independently buildable.

Completion requires the actual 30-wave campaign loop, placement and selection, all required tower/enemy interactions, progression, tactical abilities, upgrades, economy, pause/restart, results, and save/load. The Unity client must have finished cohesive assets, functional unrestricted camera controls, readable effects/audio, multiple-angle visual review, and a tested standalone Windows build. Performance is measured at 1080p on the development RTX 4060, targeting 60 FPS during representative dense combat. A working renderer or limited combat demonstration does not satisfy this target.

## Repository

- Development branch: `3d-development`.
- Reference commit/tag: `59e5a55` / `2d-before-3d-development`.
- Existing uncommitted UI and marketing work is preserved and excluded from development commits unless explicitly incorporated later.
- JavaScript scene: `prototypes/bastion-3d`; visual reference only.
- Unity client: `unity/MaximalBastion3D`.
- Shared simulation extraction is committed in `f131f30`; the original clients and Unity compile the same authoritative gameplay source.

## Verified baseline — 2026-10-07

- Desktop, browser, and test projects built with zero warnings/errors.
- 103/103 regression tests passed.
- Hidden UI verification passed: 138 scenes and 438 assertions.
- Evidence: `.artifacts/3d-development/baseline/`.
- Preserved reference binaries: `.artifacts/3d-development/baseline-runtime/`.
- Available hardware: NVIDIA GeForce RTX 4060; approximately 119 GiB free before toolchain installation.

## Current work

- Refine the real Unity scene using rendered reviews, including continuous road geometry, authored platforms, lighting, atmosphere, combat and camera framing.
- Verify camera collision, visibility-aware selection and automatic checkpoints in the packaged player.
- Measure representative rendered combat performance and finish the Windows handoff.

## Open issues

- Unity 6000.3.25f1 and URP 17.3.0 are installed; the user activated the editor license. The short install path `C:\UnityEditors\6000.3.25f1` avoids the Windows installer path-length defect. No further installers are planned.
- Portable gameplay passes 4,684,641 frozen-baseline comparisons across 64 map/difficulty/directive campaigns under .NET. Mono retains wider intermediate floating-point values in some expressions. The nine-scenario, 43,200-tick Mono comparison matches final wave/result/economy/kill outcomes and all 45 same-runtime snapshot round trips, but intermediate timing and attribution can differ. Cross-runtime raw-checksum lockstep is not supported or claimed. Gameplay arithmetic remains unchanged.
- The packaged Unity campaign and expanded UI interaction audit pass. Final dense-combat timing, camera checks and the refined combat recording remain acceptance requirements.
- The initial hidden-window timing report measured suppressed rendering and is invalid. Performance results must use the explicit rendered capture path or a visible player.

## Integration progress — 2026-10-08

- The Unity client hosts the real fixed-step `GameSession`, routes player mutations through validated commands, and uses the existing checkpoint and history repositories in its own user-data directory.
- Initial UI covers run setup, all ten tower choices, doctrine/specialization/Apex progression, protocols, targets, tactical defenses, pause, saves, history, results, and settings.
- The scene reads Cinderworks path/build regions directly from authored content; camera and animation transforms remain presentation-only.
- Editable Blender tower/enemy/environment exports are complete and have round-trip validation. Full-scene review continues as materials, effects and camera behavior are integrated.
- A runtime verification mode exercises combat, camera directions, frame timing, and the frozen-baseline replay. The first packaged run exposed the rendering/collision/runtime-parity issues above.
- Core extraction verification passed: solution build with zero warnings/errors, 104/104 native regression tests, 1,406,249 short portable comparisons, and 138 hidden 2D UI scenes with 438 assertions. 135 rendered PNGs are byte-identical to baseline; three differ only in date/time text.
- Blender asset validation passes for all 37 FBXs, covering ten towers, five enemy bodies, seven role/rank attachments, two tactical devices and thirteen environment assets. Editable source, textures, hierarchy metadata and front/rear reviews are preserved under `art/bastion-3d`.
- FBX rig nodes use native Unity Y-up/+Z-forward axes; the actual imported Prism muzzle is `(0, 1.68, 1.61)` with forward `(0, 0, 1)`. Environment collision imports are readable.
- The standalone Windows client builds successfully. Explicit URP render requests produce real 1920×1080 world/UI captures in hidden runs. The capture uses an HDR world target and an sRGB UI/final target. Pixel-content validation rejects blank captures.
- A missing URP postprocess-data assignment was corrected; the built player now includes bloom, SMAA and tone-mapping shaders. Runtime volume variants are retained.
- Fourteen actual-player UI captures passed with zero logged errors. The Barlow/Oxanium font assignments, compact current-stat grid, visible protocol controls, populated save rows and dark input controls have been reviewed. Runtime tooltips supplement Unity's editor-only tooltip property.
- Follow-up shared-code validation passes 104/104 native regressions and 1,507,921 portable comparisons. The optional AutoPlayer command executor matches the direct agent through two complete campaign attempts and 89,885 validated commands.

## Next acceptance gate

Review dense combat under the normal Windows rendering backend, finish collision and checkpoint assertions, and record the final continuous camera tour. Preserve the independently buildable original clients and commit the verified Unity/asset baseline.

## Packaged-player acceptance

- `.artifacts/3d-development/verify-unity-backend-02`: all nine same-backend scenarios pass across 43,200 ticks, 3,744 samples and 45 snapshots. The reference was recorded by a separate embedded Unity process. Standalone Mono is a distinct backend and is not substituted for this reference.
- The native .NET comparison matches final outcomes while intermediate floating-point state differs; cross-runtime lockstep remains unsupported.
- `.artifacts/3d-development/campaign-unity-01`: the real fresh Easy/Standard campaign wins after 93,366 fixed ticks, accepting all 644 commands and retaining four lives. It reaches 111 simultaneous enemies and 37 towers, records victory, and continues to wave 31. Legal wave-boundary waits and tactical retries accommodate runtime timing differences. This accelerated acceptance run is not a performance benchmark.
- `.artifacts/3d-development/interface-interactions-02`: 919 assertions pass, including all 40 doctrine/specialization paths, Apex, protocols, validated placement, saves/load/delete, settings, sandbox and results navigation. Seven new rendered pages show unclipped two-column stat values. Funded late-game UI fixtures are separate from campaign acceptance.
- Rendered capture calibration returns 118/255 for linear 18% gray. The captured world uses a 1920×1080 HDR target with a single conversion to sRGB.
- Initial crowded batch-mode capture averaged 31.35 ms at 22 towers and 145 enemies. Batch mode uses a different graphics threading mode from the normal Windows player; final performance claims require the normal backend and profiler counters.
