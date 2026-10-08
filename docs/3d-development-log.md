# Cinderworks 3D development

## Architecture and scope

The Unity 6000.3.25f1 / URP 17.3.0 Windows client runs the authored Cinderworks map through the shared `MaximalBastion.Gameplay` C# library. The original MonoGame desktop and browser clients remain independently buildable targets. Development is on `3d-development`; `2d-before-3d-development` preserves the original reference at `59e5a55`.

The client supports the 30-wave campaign, Endless and Sandbox, all ten towers, enemy chassis and roles, placement, targeting, doctrine/specialization/Apex upgrades, protocols, tactical devices, economy, pause/restart, victory/defeat, history and save/load. Input mutations use validated commands. Camera, animation, lighting and effects are presentation-only. Unity saves use a separate user-data directory.

Build and camera instructions are in `unity/MaximalBastion3D/README.md`. Editable Blender assets and export conventions are in `art/bastion-3d/README.md`. Generated players, images, recordings and evidence are under `.artifacts/3d-development` and are not committed.

## Visual implementation

- The foundry uses a connected smelter hall, three operating furnaces, service galleries, supply/return pipework, storage vessels, arrival infrastructure and a defended-core yard. Distant industrial buildings and a continuous ground plane support low and reverse views. The dressing has 325,016 triangles across seven batched renderers.
- Coated graphite, ceramic, steel, alloy, copper, rubber, oxide and architectural finishes have distinct metallic/roughness maps. Blender and Unity share calibrated linear emissive radiance. The palette separates warm furnaces from cool industrial lighting and weapon-specific energy colors.
- Lighting includes soft cascaded shadows, ambient occlusion, reflections, localized furnace and impact lights, restrained bloom, tone mapping and exponential-squared fog. Four-sample MSAA and SMAA preserve mechanical edges. Fans, charge rotors, road signals and volumetric steam keep the environment active.
- Tower assemblies have complete backs and undersides, articulated aiming, weapon-specific recoil, actual muzzle origins, upgrade hardware and three independently selected Shard Fan ports. Prism retains a centered nested core; the Mortar has a clear recessed bore; Regenerator energy cells sit within dark containment cages.
- Enemy locomotion uses distance-driven planted feet, articulated knees, chassis-specific stride and weight. Defeat effects use the actual chassis armor with bounded fragment counts, exact mesh support and raised-surface contact.
- The perspective camera supports orbit, pan, elevation, zoom and free flight with collision. Its default overview keeps every route corner and build deck between the HUD bars.
- Beam presentation retires when its emitter is destroyed, even if the simulation's transient effect remains active. Leaving a run clears all impact-light timers and lights.

## Original-client and shared-gameplay verification

- Desktop, browser and test projects build with zero warnings or errors.
- 104 native regressions, 1,507,921 portable comparisons and 138 hidden interface scenes / 438 assertions pass.
- Frozen-reference validation covers 4,684,641 comparisons across 64 map/difficulty/directive campaigns under .NET. No gameplay arithmetic was changed for the art pass.
- The embedded Unity fixture covers nine scenarios, 43,200 ticks, 3,744 state samples and 45 same-runtime snapshot round trips. The final Windows player passes this fixture in `final-03-verify`.
- .NET and Mono scenario final wave/result/economy/kill outcomes agree. Intermediate floating-point timing and attribution can differ. Cross-runtime raw-checksum lockstep is not supported.

## Asset and presentation acceptance

- All 37 FBXs pass closed-surface, UV, nondegenerate-face, bounds, rig and content-coverage checks. The pinned Unity editor checks 620 retained rig nodes with a maximum position error of 0.000012 m, matching directions and positive handedness. Editable Blender sources retain canonical coordinates; export copies compensate for Unity's FBX handedness conversion.
- The material GPU probe verifies base-color conversion and linear HDR emission. Render diagnostics return 118/255 for linear 18% gray and 0.533 fog attenuation at 80 m against an expected 0.528. Anti-aliasing coverage is measured from rendered pixels.
- `final-motion` passes 36 contact, scale, turn, recoil and muzzle-selection assertions, with fourteen actual-player images. Maximum planted-foot error is 1.26 mm.
- `final-wreckage` checks 71 meshes, 26,768 unique vertices and 13,632 arbitrary poses. All five chassis emit and clean up their fragments; raised-deck contact and excluded overhead/entity geometry pass with sub-micrometre measured support error. Seventeen images and a 60 FPS recording accompany the report.
- The Mortar bore passes 180 off-axis clearance rays and actual low-angle review. Nine 3840x2160 combat/environment views cover overhead, ground, rear, weapon close-up, dense combat, furnace hall, core yard and opposite approaches. Captures reject a camera inside solid geometry.
- The beam lifecycle regression reproduces failure with the old guard in an isolated copied player and passes all 18 assertions in the fixed release. It uses validated placement/sale for Prism and Arc, waits through Unity's deferred destruction, and confirms both visual retirement and menu-light cleanup.

## Final Windows acceptance — 2026-10-08

`unity-art-release-03.log` records the non-development build using the pinned editor. `final-03-verify` passes gameplay conformance, persistence, camera interaction/collision/framing and the lifecycle regression with zero runtime errors. `final-03-interface` passes 924 assertions and eight fresh captures covering tower and upgrade paths, protocols, targeting, tactics, checkpoints, settings, results and Sandbox.

`final-03-campaign` completes all 30 authored waves in 93,368 fixed ticks, accepting all 644 commands, winning with four lives and continuing to wave 31. It reaches 111 simultaneous enemies and 37 towers. Six deferred tactical commands and 317 ticks of legal wave-boundary waits accommodate backend timing differences; no commands remain pending and no runtime errors occur. This accelerated acceptance driver is not a performance benchmark.

`final-03-performance` measures the normal Direct3D11 Windows player at 1920x1080 on the RTX 4060, with 22 towers and up to 143 enemies. The dedicated dense-combat window records 2,521 frames over 25.02 seconds: mean 9.92 ms (about 101 FPS), median 9.60 ms, 95th percentile 12.14 ms and maximum 63.46 ms. Counters confirm one game-camera render per frame. GPU timers are unavailable; these are observed frame timings, not isolated GPU costs. The sample has zero runtime errors and an unchanged player manifest. The maximum records an isolated hitch, so this is not a claim that every frame exceeds 60 FPS.

Packaging requires the exact release's successful interface manifest and rechecks every runtime file and ZIP entry by SHA-256. Final capture/recording manifests will identify the reviewed player.

## Limits and remaining acceptance

- This client contains Cinderworks on Windows. Other maps, online co-op and the browser client remain available in the original 2D game; they are not implemented in this Unity slice.
- Performance is measured on the development RTX 4060 at 1080p using the normal Windows graphics backend. Batch-mode runs and video readback are not performance evidence. GPU timer availability is reported separately from observed frame timing.
- Refreshed release footage and verified packaging remain before handoff. The complete authored campaign is verified separately from funded UI fixtures and Sandbox footage.
