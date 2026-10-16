# Cinderworks 3D proof of concept

A freely explorable, real-time 3D combat scene for Maximal Bastion. The project uses Three.js 0.180.0, with local dependencies and editable procedural assets. No network connection is needed to run it.

## Open and run

Extract the ZIP before opening the project. On Windows, double-click `Launch.cmd`. Python 3 must be available on PATH. The launcher starts a local server and opens `http://127.0.0.1:5183` in your browser. Use a current browser with WebGL 2 and hardware acceleration enabled. Blender, Node.js and package installation are not required to run the scene.

Alternatively, open a terminal in this directory:

```powershell
python tools/serve.py --port 5183
```

Open `http://127.0.0.1:5183`. Keep that terminal running; Ctrl+C stops it. Loading `index.html` directly as a file is not supported by browser module loading. The Windows launcher records its background server PID in `.server.pid`; `Stop-Demo.ps1` stops that server.

To stop a server started by the Windows launcher, run this from the project directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Stop-Demo.ps1
```

Combat starts automatically. A seeded convoy and staggered reinforcements demonstrate all three weapons. The 60-second encounter repeats; Restart begins the same encounter immediately. Camera motion and combat can be controlled independently while paused.

## Camera and controls

| Action | Control |
| --- | --- |
| Orbit through 360 degrees | Left drag in Orbit mode |
| Pan | Right drag in Orbit mode |
| Zoom / dolly | Mouse wheel |
| Look freely | Left or right drag in Free camera mode |
| Move forward / left / back / right | W / A / S / D in Free camera mode |
| Descend / rise | Q / E |
| Move faster | Hold Shift |
| Pause / resume combat | Space or Pause button |
| Restart the encounter | R or Restart button |
| Hide / show the interface | H |
| Smooth tour around the scene | Camera tour button |
| Inspect a tower | Click its name below the camera controls, then orbit or zoom |
| Preset viewpoints | Overview, Ground, Rear, Overhead |

Click or drag the canvas after choosing Free camera to give it keyboard focus. Movement stops when the window loses focus. The camera slides around solid foundry structures and tower geometry and stays above the ground. Pitch covers nearly straight up and down; yaw is unrestricted. Standard quality disables bloom and shadows for slower devices.

## Scene and behavior

The route and eight build decks derive from the main game's `src/MaximalBastion/ContentData/Maps/FoundryLoop.json`; their self-contained 3D definition is in this project's `src/environment.js`. The furnace exchangers, copper transfer infrastructure, amber lane markings, and industrial skyline translate Cinderworks into depth. Coordinates use Y up, with world X and Z corresponding to the original map X and Y. Rounded corner fillets remain inside the road and give walkers continuous turning motion.

- **Needle Turret:** cyan plasma, direct targeting, yaw/elevation tracking, recoil, muzzle flash, visible projectile flight and impact.
- **Prism Beam:** violet diamond reactor, white firing flash, continuous layered cylindrical beam with rounded glow, durable-target preference and Expose-assisted damage. The beam begins at the physical barrel tip and tracks the current target.
- **Siege Mortar:** chartreuse charge chamber, elevated barrel, predicted ballistic arc, rotating energy shell, capped six-target splash, expanding volume, shock ring and debris.
- **Runner:** small triangular chassis, rapid articulated gait, faster path speed.
- **Brute:** larger hexagonal armor/reactor, slower six-legged gait and more health. `bulwark` is the local model factory key for this Brute model.

Direct weapons reject targets hidden by major structures. Projectile motion is checked against those structures. Convoys maintain spacing, hits flash armor and rock the chassis, and defeats emit geometric debris. Effects use three-dimensional tubes, spheres, rings, and fragments, including view-dependent soft glow on volumes. The scene uses PBR materials, roughness textures, environment reflections, shadows, fog, and emissive surfaces.

This is a focused visual and interaction study. It reuses the game's identity, route geometry, tower roles and color language; encounter health, range, timing and spacing are tuned for this compact demonstration. It does not run the production C# simulation, campaign, economy, upgrades or co-op systems.

## Editable project and assets

| File | Purpose |
| --- | --- |
| `src/models.js` | Closed tower and enemy geometry, named materials, recoil and gait |
| `src/environment.js` | Route, platforms, foundry structures, roughness/color textures and environment animation |
| `src/combat.js` | Fixed-step targeting, attacks, damage, movement, prediction and encounter pacing |
| `src/effects.js` | Volumetric beams, plasma, trails, impacts, flashes and debris |
| `src/camera.js` | Orbit/free camera, collision, viewpoints and continuous tour |
| `src/main.js` | Rendering, lighting, postprocessing, controls and export hooks |
| `assets/cinderworks-scene.blend` | Editable Blender scene with named objects/materials, inspection cameras and mechanical animation |
| `assets/cinderworks-scene.glb` | Portable scene geometry/materials and mechanical animation |
| `assets/tower-and-enemy-models.blend` | Five models with separately editable components |
| `assets/tower-and-enemy-models.glb` | Portable component catalog and animation |
| `renders/` | 1920×1080 screenshots and the 28-second, 30 fps camera-tour video |

Open `.blend` files with Blender 4.5 LTS or later. Materials and textures are packed. The catalog retains separate armor panels, barrel components, joints and core meshes. The live scene batches static components for performance; component names are retained in metadata. Calling `createTower(type, {batch:false})` or `createEnemy(type, {batch:false})` creates separate meshes.

Blender files contain editable mechanical inspection actions. The complete targeting, projectile trajectories, damage and repeatable combat sequence run in the browser project; these procedural systems are defined in the JavaScript sources. Live glow shaders are browser render effects. The reference illustrations guided shape and color choices; the running scene consists of actual 3D meshes.

## Rebuild exports and review

Run the following commands from this project directory with the local server active. `tools/export-assets.cjs` exports the scene and model catalog. It requires Node.js, Playwright and Microsoft Edge. Set `PLAYWRIGHT_MODULE` to an installed Playwright package directory if it is not installed locally. `tools/import_scene.py` converts GLB files into packed Blender projects.

```powershell
node tools/export-assets.cjs
blender --background --python-exit-code 1 --python tools/import_scene.py -- --input assets/cinderworks-scene.glb --output assets/cinderworks-scene.blend --focus-width 68 --focus-ground 0 --camera-preset cinderworks
blender --background --python-exit-code 1 --python tools/import_scene.py -- --input assets/tower-and-enemy-models.glb --output assets/tower-and-enemy-models.blend
```

The `blender` commands assume Blender is on PATH. Alternatively, `powershell -ExecutionPolicy Bypass -File tools/install_blender.ps1` downloads the official portable Blender 4.5.7 LTS archive and verifies its SHA-256 checksum. This optional step needs an internet connection and saves Blender inside `.tools/blender/`. Replace `blender` in the commands above with `& '.\.tools\blender\blender-4.5.7-windows-x64\blender.exe'` when using that copy.

`tools/review.cjs` captures four viewpoints. `tools/validate.cjs` checks combat damage, multi-target splash, defeat, finite transforms, camera collision and interface controls. Both write into this project's `.artifacts/review/`. `tools/capture.cjs` generates the stills and the MP4 in `renders/` using deterministic 60 Hz simulation, 30 fps sampling, and FFmpeg, which must be on PATH. Video review frames go into `.artifacts/video-review/`. These development tools use Microsoft Edge through Playwright on Windows. Their output locations are relative to the project, independent of the terminal's working directory.

## Dependencies and attribution

Three.js is distributed locally under its MIT license in `vendor/three/LICENSE`. The interface uses the project's Oxanium and Barlow fonts; their SIL Open Font Licenses are in `fonts/`. Renderer/export APIs follow the [Three.js documentation](https://threejs.org/docs/). Blender is available from [blender.org](https://www.blender.org/).
