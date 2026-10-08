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
- Shared simulation extraction is in progress.

## Verified baseline — 2026-10-07

- Desktop, browser, and test projects built with zero warnings/errors.
- 103/103 regression tests passed.
- Hidden UI verification passed: 138 scenes and 438 assertions.
- Evidence: `.artifacts/3d-development/baseline/`.
- Preserved reference binaries: `.artifacts/3d-development/baseline-runtime/`.
- Available hardware: NVIDIA GeForce RTX 4060; approximately 119 GiB free before toolchain installation.

## Current work

- Extract portable gameplay with arithmetic/rule parity and existing-client compatibility.
- Install and validate the Unity editor, URP packages, and Windows build toolchain.
- Establish Blender asset scale, hierarchy, materials, and import contract, then produce the complete Cinderworks roster.
- Implement the Unity gameplay host, command-driven UI, camera, entity presentation, and repeatable verification.

## Open issues

- Unity installation and license availability have not yet been verified.
- The existing game targets .NET 10; the Unity-compatible library requires API compatibility work.
- No production Unity build or gameplay parity result exists yet.

## Next acceptance gate

Run an actual Cinderworks session from shared C# in a packaged Unity Windows client, with placement and wave commands, while the original 2D regression/visual suite remains green. Compare deterministic gameplay results before expanding final asset production.
