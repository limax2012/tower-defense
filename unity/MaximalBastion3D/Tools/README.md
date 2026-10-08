# Unity tools

The project uses Unity **6000.3.25f1** with the bundled **URP 17.3.0** packages. Run these commands from the repository root in PowerShell.

Install the pinned Windows editor and Unity Hub:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Install-Unity.ps1 -InstallHub
```

The installer downloads from Unity's official release registry and CDN, checks the published checksum and Authenticode signature, and installs the editor under `C:\UnityEditors`. The short path avoids Windows installer path limits that can omit package files. Add `-WindowsIl2Cpp` to install the optional Windows IL2CPP module. Windows Mono build support is included with the editor; the Windows build uses Mono and does not require the Visual C++ build toolchain.

Sign in to Unity Hub and activate a license eligible for your use. Personal licenses normally activate on sign-in. If necessary, open **Settings > Licenses > Add license**. The scripts do not collect credentials or activate licenses.

Import and configure the project:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Sync-Gameplay.ps1
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task Import
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task ConfigureProject
```

Verify or build Windows:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task Verify
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task BuildWindows
```

`BuildWindows` produces a development player in `.artifacts/3d-development/Windows` with profiling support. `BuildWindowsRelease` uses `BuildOptions.None` and writes a separate player to `.artifacts/3d-development/Windows-Release`. Both targets configure and verify assets before building. Each writes `build-info.json` with the actual configuration and editor version.

Build, test and package the release player:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Invoke-Unity.ps1 -Task BuildWindowsRelease
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode InterfaceTest -PlayerPath .artifacts/3d-development/Windows-Release/MaximalBastion3D.exe -CaptureDirectory .artifacts/3d-development/release-interface
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Package-Windows.ps1 -VerificationDirectory .artifacts/3d-development/release-interface
```

Packaging requires a successful interface test of that exact release executable and checks every player file against the review's SHA-256 manifest. The ZIP contains the runtime, controls README and font licenses. Diagnostic backend fixtures, logs, symbols and Unity's explicitly non-shipping folders are excluded; the development build retains them. The script verifies every ZIP entry against its source checksum and writes a SHA-256 sidecar under `.artifacts/3d-development/Packages`. Keep the development build for full backend conformance checks, since the distributable omits those fixtures.

`Invoke-Unity.ps1` runs the editor in a hidden batch process and returns an error for a failed Unity exit. Logs go to `.artifacts/3d-development/unity-<task>.log`. It calls the corresponding `Bastion3D.Editor.BuildAutomation` method. `-NoGraphics` is available for import-only checks; keep graphics enabled for rendering validation.

Open the editor interactively:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Open-Unity.ps1
```

An editor installed through Unity Hub is also detected. Set `UNITY_EDITOR` to a Unity executable in a custom location. Automatically discovered executables must match the project version recorded in `ProjectSettings/ProjectVersion.txt`; a different installed version is ignored. An explicit `-EditorPath` can override the version, with a warning.

`Compile-Offline.ps1` performs a preliminary C# API check using the installed editor's Roslyn compiler, Unity assemblies, and compiled URP reference assemblies. It does not replace Unity import, shader compilation, play-mode verification, or a player build.

Capture the working Windows player without taking desktop focus:

```powershell
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode Verify
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode Interface
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode InterfaceTest
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode RenderDiagnostics
powershell -ExecutionPolicy Bypass -File unity/MaximalBastion3D/Tools/Review-Player.ps1 -Mode AudioDiagnostics
```

The verification capture exercises combat and camera viewpoints. The interface capture visits the menus, HUD, upgrade panels and sandbox controls. Review modes render the camera and interface into an offscreen texture, validate pixel variation, and write PNGs synchronously. The launcher requires fresh images from the current run. Images and player logs are written under `.artifacts/3d-development/review-<mode>`. `-Mode Tour` runs a longer moving-camera sequence; `-Mode Autoplay` runs the deterministic policy for a longer gameplay check.

`RenderDiagnostics` renders a small isolated fixture through the same URP request and HDR readback path. It measures partial edge coverage with MSAA and SMAA, and exponential-squared fog attenuation at eighty metres. The wrapper requires passing pixel measurements, six images and a clean full startup log; configured graphics flags alone do not pass these checks. `ConfigureProject` retains the runtime fog variant explicitly. Enemy meshes retain CPU readability for chassis support calculations, and asset verification checks this before building.

Asset verification compares every retained imported rig node against its authored position and normalized forward/up directions, requiring positive transform handedness. This includes asymmetric environment lights, firing ports and articulated enemy limbs; successful Blender export alone does not establish the Unity coordinate basis.

`-Mode Motion` requires the fourteen movement/recoil captures and all contact, scale and timing assertions. `-Mode Wreckage` requires fifteen defeat captures plus emitted and cleaned-up fragments for each of the five enemy types. These isolated presentation fixtures receive the same full-log and exact-player checksum checks as interface verification.

Add `-Video` to either Motion or Wreckage to encode the fixture during verification. The wrapper requires a fresh MP4 with the requested dimensions and valid video frames, in addition to the assertion reports and still images; ffmpeg and ffprobe must be on PATH.

`InterfaceTest` dispatches UI Toolkit navigation-submit and value-change events into the actual player controls, then checks simulation and persistence results. It covers the tower roster, doctrine/specialization/Apex upgrades, protocol readiness, targeting, tactical placements, live inspector values, save/load/delete, wave-end autosave restoration, settings and result navigation. Funded and completed-wave fixtures isolate late-run controls from campaign duration; this is an interface test, not a campaign balance test. Saves and history use a temporary verification directory. A failed control or state assertion exits the player with an error and writes `interface-verification.txt`. A successful run writes `verified-player.json`; file checksums are compared before and after the test to detect a changed build.

`ReviewCapture.ReadRgb24()` exposes packed sRGB RGB24 frames for video encoding, with the first row at the bottom. Set `IncludeInterface = false` for world-only footage. Captures validate a known linear gray sample to detect incorrect output conversion.

Combat video records the actual Unity mix through `AudioRenderer` once per fixed 30 FPS frame. It writes a float WAV and an audio verification report, then ffmpeg muxes AAC into the final MP4 without recompressing the video. A fractional final DSP block is padded to the exact video duration. Microphone input is not recorded. `AudioDiagnostics` measures a known three-second calibration tone through that same capture path and requires fresh, non-silent, synchronized audio plus a clean player log.

Add `--capture-scale 2` to a direct player review invocation to render both the HDR camera and final capture target at twice the default width and height (3840×2160 for a 1920×1080 capture). Values are limited to 1 or 2. This renders additional pixels; it does not enlarge an existing image. Use the default scale for 1080p performance measurements and video.

The editor command **Bastion > Calibrate material colors** renders linear HDR swatches without scene lights, reflections or postprocessing. It compares ordinary URP base color, HDR emission and the custom additive energy shader against expected linear radiance, writing `.artifacts/3d-development/material-calibration/color-space.txt`. This checks shader/API color handling independently of exposure, bloom and display conversion.

`PerformanceDiagnostics` records frame times, render submissions, draw calls, batches, geometry and allocation counters. Call `BeginSample` after warmup and `EndSample` before image readback or encoding. Reports distinguish unavailable GPU timers from measured zero work; an offscreen run without GPU timings cannot establish whether a slow frame is CPU- or GPU-bound by itself.
