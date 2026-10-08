# Bastion 3D asset library

Editable hard-surface models, shared materials, Unity FBX exports, and an authored Cinderworks dressing assembly. The library covers the ten tower IDs, five enemy IDs, both tactical devices, enemy role/rank accessories, and a modular foundry environment.

## Regenerate

Use Blender 4.5 LTS or newer from the repository root:

```powershell
blender --background --python art/bastion-3d/tools/build_assets.py -- --all --render
blender --background --python art/bastion-3d/tools/validate_assets.py
```

For a focused export, replace `--all` with `--ids prism_beam t1_crawler foundry_furnace`. Omitting `--render` exports models and metadata without review renders.

`models/<category>/<id>.blend` retains separate editable hardware parts. `unity/MaximalBastion3D/Assets/Art/Models` contains FBX files with fixed hardware batched by mechanical pivot. Materials, meshes, UVs and articulated nodes are preserved. No external add-ons or downloaded model assets are required.

## Units and rig contract

One Unity unit equals twenty logical game pixels. Assets face Unity +Z, use Y up, and stand on Y=0; platform/road foundations extend below the ground. Blender source uses Z up and -Y forward. FBX uses -Z forward / Y up axis conversion and metric unit metadata.

Tower rigs expose `Base`, `Yaw`, `Pitch`, `Recoil`, and `Muzzle`. Positive Unity yaw rotates the weapon to the right; negative local pitch raises it. Muzzle position is the actual end of the weapon. The mortar's resting pitch is elevated. The three Shard Fan ports expose `Muzzle_0`, `Muzzle`, and `Muzzle_2`.

`Tier2*`, `Tier3*`, and `Apex*` groups retain their own geometry. Toggle these for upgrade state. Tier 2 activates two additional foundation indicators and reserve power hardware; tier 3 activates the remaining indicators and cooling pods. Apex adds energy stabilizers on the foundation.

Enemy bodies expose `Body`, `HitPoint`, `GroundContact`, `RoleSocket`, and `RankSocket`. Each `Leg_*` has a `Knee*` and `Foot*` chain. Repeated Blender names can carry a numeric suffix; use the exact hierarchy in metadata or match the name prefix. Leg rest positions are supplied in local coordinates and do not assume a shared gait. Signal-role accessories attach at `RoleSocket`; elite/boss crowns use `RankSocket`. Both use identity local transforms; the two sockets occupy separate mounting points.

The material manifest provides sRGB base color, metallic, effective roughness, and emission intensity. Non-emissive metallic surfaces use `Alloy_Roughness.png`, a linear roughness map with mean .5875. Their `metallicSmoothnessTexture` entry selects an `Alloy_MetallicNNN_Smoothness.png` file with the material's authored metallic value in RGB and inverse roughness in alpha. Import these packed maps as linear and use a URP smoothness multiplier of 1. The generic `Alloy_MetallicSmoothness.png` has fixed metallic .72 and is suitable only for that metallic value. Color textures and energy values should not be interpreted as linear scalar maps.

The `FoundryFloor` set provides a neutral sRGB base-color multiplier, tangent-space normal map, and linear metallic/smoothness map. It is designed to repeat every eight metres. Its packed map uses metallic .30 and smoothness in alpha. With URP's metallic map enabled, use a smoothness multiplier of 1; the packed alpha already contains the intended finish. Keep the neutral floor base map separate from emissive and ceramic materials. `textures.json` records channel conventions. Regenerate only texture maps with `--textures-only`.

## Environment

`cinderworks_dressing.fbx` is aligned to the authored map: logical `(480,360)` is world `(0,0,0)`, world X = `(logical X - 480) × .05`, world Z = `(logical Y - 360) × .05`. It includes low interior heat exchangers, service conduits, perimeter furnaces, interconnected pipe runs, catwalks, service bays, an arrival gantry, and a defended reactor beside the goal. It does not replace the authoritative route or build polygons.

The enemy arrival gantry is centered at `(-25.6, 0, -12.8)`. The goal gate is at `(24.7, 0, 14.1)`; the core stands farther outside the path at `(28, 0, 14.1)`. Road openings remain clear.

`FanRotor_L*` and `FanRotor_R*` rotate about local Y at +90 and -90 degrees per second. `ChargeRotor` rotates about local Y at 25 degrees per second. Animation instructions are also attached to their hierarchy entries in metadata. `Exhaust*`, `Heat*`, `CoreLight`, and `DamageSmoke` are effect sockets. These presentation elements do not affect simulation or targeting.

## Review and validation

`review` contains front and rear renders of individual models. The validation script imports each FBX and compares exported bounds and sockets to the source manifest; it also checks UVs, closed meshes, nondegenerate faces, required rig nodes, and complete content-ID coverage. The report is written to `.artifacts/3d-development/art-validation.json`.

`tools/render_library.py -- --environment` renders the current source library and a complete assembly with authored roads/build decks; `tools/contact_sheets.py` uses Pillow to assemble labeled category sheets. The assembly is saved as `models/Environment/cinderworks-review-assembly.blend` and is a review scene, separate from the Unity dressing export.

The `catalog.json` and per-asset Unity metadata files contain world bounds, original source triangle counts, content IDs, source paths, energy colors, and full articulated rest transforms. Gameplay remains authoritative in the core; models and sockets provide presentation geometry only.
