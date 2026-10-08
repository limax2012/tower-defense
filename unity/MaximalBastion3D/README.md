# Cinderworks 3D client

Unity 6000.3.25f1 LTS / URP 17.3.0. The client runs the authored Cinderworks map through `MaximalBastion.Gameplay`; Unity supplies presentation, input, interface, camera, and audio. The MonoGame desktop and browser clients remain separate build targets in the repository root solution.

## Build and open

Activate the pinned editor through Unity Hub. From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Sync-Gameplay.ps1
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task BuildWindows
```

The build is written to `.artifacts/3d-development/Windows/MaximalBastion3D.exe`. `Sync-Gameplay.ps1` builds the portable C# library, copies its required runtime dependencies, and copies the authoritative JSON into `StreamingAssets`. These generated copies are ignored by Git; edit source and content in `src/MaximalBastion`.

Use `-Task BuildWindowsRelease` for the non-development player in `.artifacts/3d-development/Windows-Release`. Before packaging, run `Tools/Review-Player.ps1 -Mode InterfaceTest -PlayerPath <release-executable> -CaptureDirectory <review-directory>`. `Tools/Package-Windows.ps1 -VerificationDirectory <review-directory>` packages that exact verified player and verifies every ZIP entry against its source hash. Generated players, captures and ZIPs remain under `.artifacts`.

For editor work, run `Tools/Open-Unity.ps1` and open `Assets/Scenes/Cinderworks.unity`. The **Bastion** editor menu configures the URP assets, imports the model/material catalog, verifies model bounds and sockets, and builds Windows. `Tools/Compile-Offline.ps1 -Task Editor` checks C# API compatibility without launching the editor; it does not replace an actual Unity import or player test.

## Play

- Choose **Play**, a difficulty and mode, then start the run.
- Select a tower from the bottom toolbar and click a clear build deck. Hold **Left Shift** to keep placing. Right-click or **Esc** cancels placement.
- Click a tower for stats, upgrade choices, targeting, protocols, and selling. Hover an upgrade to compare its values.
- **Space** starts a wave. The top bar controls speed and pause.
- Pulse Plates go on the road. The Charge Forge occupies a build deck.
- **Esc** opens pause. Checkpoints can be saved between waves; save/load uses the shared persistence implementation in a separate 3D user-data directory.

## Camera

- **Right drag:** orbit or look around.
- **Middle drag:** pan.
- **Wheel:** zoom; in free flight, move forward/back.
- **W/A/S/D:** move across the map or through the scene.
- **Q/E:** lower/raise the camera focus or flight position.
- **F:** toggle free flight.
- **Left Shift:** faster movement.
- **Home:** return to the overview.

Camera collision is presentation-only. It cannot change enemy paths, damage, placement rules, or targeting. Player input is inactive while the window is unfocused.

## Editable assets

Blender sources and export scripts are under `art/bastion-3d`. Unity FBXs, PBR material metadata, and textures live under `Assets/Art`. One Unity unit represents 20 logical map pixels; logical `(480,360)` maps to world origin on X/Z. Towers expose `Yaw`, `Pitch`, `Recoil`, and `Muzzle` transforms; enemies expose articulated limbs plus role/rank sockets. The FBXs contain mechanical rigs without baked animation clips. Unity's `ModelRig` supplies aiming and weapon-specific recoil; `MechanicalLocomotion` drives planted feet, articulated knees and body suspension from movement and turns. These animations do not alter simulation state.

## Verification

Run the repository's `scripts/verify.ps1` for the original clients, regression tests, portable parity checks, and hidden 2D UI scenes. Unity's **Bastion → Verify assets** checks imported bounds and every retained rig node's world position, forward/up directions and positive transform determinant against the asset metadata. Player review modes accept `--capture-directory <directory>` and use isolated temporary save data, permitting unattended simulation without enabling unfocused player input.

| Player option | Evidence |
| --- | --- |
| `--verify` | Embedded-Unity conformance, snapshots, saves, camera collision, visibility-aware picking, and beam/impact-light cleanup |
| `--verify --beam-lifecycle-only` | Prism and Arc discharge cleanup after selling their towers, plus impact-light cleanup when leaving a run |
| `--ui-verify` | Validated interface interactions, upgrade branches, checkpoints, results and screenshots |
| `--ui-review` | Sixteen interface pages |
| `--campaign-replay --replay-file <file>` | Full authored campaign through validated commands, legal waits and explicit retries |
| `--combat-review` | Sandbox combat, five viewpoints and a representative dense-combat timing sample |
| `--combat-review --art-review` | Nine combat/environment viewpoints with camera-overlap checks, without a performance claim |
| `--combat-review --video` | Continuous 30-second 1080p camera tour with synchronized game audio, encoded by ffmpeg |
| `--audio-diagnostics` | Three-second mixed-output calibration with measured audio duration and signal level |
| `--motion-review` | Articulated gait, foot contact, turn and recoil review; `--video` records 60 FPS footage |
| `--wreckage-review` | Enemy chassis breakup and fragment-lifetime review |

Measure rendered performance with the normal Windows backend, without `-batchmode`; Unity's batch graphics threading mode differs from the interactive player. ReviewCapture explicitly renders one HDR world frame at 1920×1080, converts once to sRGB, and adds the interface separately. Recording/readback runs are not performance benchmarks. Sandbox and isolated presentation fixtures do not establish campaign balance or gameplay correctness.

The development log at `docs/3d-development-log.md` records current acceptance results, runtime compatibility issues, and unfinished work. A successful build alone does not establish visual quality, cross-runtime determinism, or rendered performance.
