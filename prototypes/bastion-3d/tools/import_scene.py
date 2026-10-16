"""Import the Cinderworks GLB into a self-contained, editable Blender scene.

Run with Blender 4.5 or newer:
    blender --background --python tools/import_scene.py -- \
        --input assets/cinderworks-scene.glb --output assets/cinderworks-scene.blend

The GLB provides the mesh hierarchy, materials, and sampled combat animation.
Review cameras and a studio fill rig are created when they are not in the GLB.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def arguments():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--preview", type=Path)
    parser.add_argument("--frame", type=int, default=1)
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--focus-width", type=float,
                        help="Width of the playable area when distant scenery extends the bounds.")
    parser.add_argument("--focus-ground", type=float,
                        help="Ground height used by review cameras and the fill rig.")
    parser.add_argument("--camera-preset", choices=["auto", "cinderworks"], default="auto")
    return parser.parse_args(argv)


def collection(name):
    result = bpy.data.collections.get(name)
    if result is None:
        result = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(result)
    return result


def move_to_collection(obj, target):
    for previous in list(obj.users_collection):
        previous.objects.unlink(obj)
    target.objects.link(obj)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_camera(name, position, target, lens, target_collection):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    data.clip_start = 0.04
    data.clip_end = 1000
    obj = bpy.data.objects.new(name, data)
    target_collection.objects.link(obj)
    obj.location = position
    point_at(obj, target)
    return obj


def add_area(name, position, target, energy, color, size, target_collection):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    target_collection.objects.link(obj)
    obj.location = position
    point_at(obj, target)
    return obj


def mesh_bounds(objects):
    points = [obj.matrix_world @ Vector(corner)
              for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("The GLB did not contain editable mesh objects.")
    low = Vector([min(p[axis] for p in points) for axis in range(3)])
    high = Vector([max(p[axis] for p in points) for axis in range(3)])
    return low, high


def configure_scene(scene, fps):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.fps = fps
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.5
    scene.world = bpy.data.worlds.new("Cinderworks / Night atmosphere")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.032, 0.049, 0.082, 1.0)
    background.inputs["Strength"].default_value = 0.28
    scene.use_nodes = True
    tree = scene.node_tree
    tree.nodes.clear()
    source = tree.nodes.new("CompositorNodeRLayers")
    source.location = (-300, 0)
    glow = tree.nodes.new("CompositorNodeGlare")
    glow.name = "Energy emission / restrained bloom"
    glow.label = "Energy emission / restrained bloom"
    glow.glare_type = "FOG_GLOW"
    glow.quality = "HIGH"
    glow.inputs["Threshold"].default_value = 1.8
    glow.inputs["Strength"].default_value = 0.12
    glow.inputs["Size"].default_value = 0.35
    glow.location = (-70, 0)
    output = tree.nodes.new("CompositorNodeComposite")
    output.location = (180, 0)
    tree.links.new(source.outputs["Image"], glow.inputs["Image"])
    tree.links.new(glow.outputs["Image"], output.inputs["Image"])


def main():
    args = arguments()
    source = args.input.resolve()
    destination = args.output.resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.render.fps = args.fps
    bpy.ops.import_scene.gltf(filepath=str(source), import_shading="NORMALS")
    bpy.context.view_layer.update()
    imported = list(scene.objects)
    low, high = mesh_bounds(imported)
    center = (low + high) * 0.5
    ground = low.z if args.focus_ground is None else args.focus_ground
    width = args.focus_width or max(high.x - low.x, high.y - low.y)
    extent = max(12.0, width * 0.5)
    aim = Vector((center.x, center.y, ground + min(3.0, (high.z - ground) * 0.22)))

    geometry = collection("01 / Cinderworks geometry and combat")
    lights = collection("02 / Lighting")
    cameras = collection("03 / Review cameras")
    for obj in imported:
        target = lights if obj.type == "LIGHT" else cameras if obj.type == "CAMERA" else geometry
        move_to_collection(obj, target)
    for unused in list(bpy.data.collections):
        if not unused.objects and not unused.children and unused not in (geometry, lights, cameras):
            bpy.data.collections.remove(unused)

    configure_scene(scene, args.fps)
    overview = add_camera("Review / Three-quarter overview",
                          (center.x + extent * 1.4, center.y - extent * 1.55,
                           ground + extent * 1.2), aim, 38, cameras)
    add_camera("Review / Opposite side",
               (center.x - extent * 1.20, center.y + extent * 1.28,
                ground + extent * 0.63), aim, 42, cameras)
    add_camera("Review / Road level",
               (center.x + extent * 0.86, center.y - extent * 1.07,
                ground + 2.2), aim, 35, cameras)
    add_camera("Review / Overhead",
               (center.x + 0.01, center.y - 0.01, ground + extent * 2.08), aim, 45, cameras)
    if args.camera_preset == "cinderworks":
        # glTF's Y-up world becomes Blender's Z-up world: (x, y, z) -> (x, -z, y).
        views = {
            "Review / Three-quarter overview": ((-25, 24, 29), (-3, 1, -3)),
            "Review / Road level": ((-19, 2.25, 3), (-4, 1.75, -5)),
            "Review / Opposite side": ((7, 4.6, -16), (-8, 1.6, -3)),
            "Review / Overhead": ((0, 54, .5), (0, 0, 0)),
        }
        for name, (position, target) in views.items():
            camera = bpy.data.objects[name]
            camera.location = (position[0], -position[2], position[1])
            point_at(camera, (target[0], -target[2], target[1]))
            camera.data.lens = 33
    scene.camera = overview
    add_area("Fill / cool sky", (center.x - extent * 0.4, center.y, ground + extent),
             aim, extent * extent * 12, (0.46, 0.63, 1.0), extent * 0.9, lights)
    add_area("Rim / furnace", (center.x + extent, center.y + extent * 0.4, ground + extent * 0.6),
             aim, extent * extent * 9, (1.0, 0.34, 0.095), extent * 0.6, lights)
    add_area("Fill / camera", (center.x + extent, center.y - extent, ground + extent * 0.5),
             aim, extent * extent * 5, (0.56, 0.71, 1.0), extent * 0.7, lights)

    scene.frame_start = 1
    last_frame = max((action.frame_range[1] for action in bpy.data.actions), default=1)
    scene.frame_end = max(1, math.ceil(last_frame))
    scene.frame_set(max(scene.frame_start, min(args.frame, scene.frame_end)))
    scene["source_asset"] = source.name
    scene["interactive_scene"] = "../index.html"
    scene["camera_controls"] = "Use the live scene for collision-aware free camera exploration."
    scene["materials"] = "Named glTF PBR materials retain editable base, roughness, metal and emission nodes."
    scene["animation"] = "Imported glTF animation is editable in the Dope Sheet and Graph Editor."
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.clip_start = 0.04
                area.spaces.active.clip_end = 1000
                area.spaces.active.shading.type = "MATERIAL"
                area.spaces.active.shading.use_scene_lights = True
                area.spaces.active.shading.use_scene_world = True
                area.spaces.active.region_3d.view_distance = extent * 2.2
                area.spaces.active.region_3d.view_location = aim
                area.spaces.active.region_3d.view_rotation = overview.rotation_euler.to_quaternion()
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(destination))
    report = {
        "source": str(source), "blend": str(destination), "blender": bpy.app.version_string,
        "objects": len(scene.objects),
        "meshes": len([obj for obj in scene.objects if obj.type == "MESH"]),
        "materials": len(bpy.data.materials), "actions": len(bpy.data.actions),
        "cameras": [obj.name for obj in scene.objects if obj.type == "CAMERA"],
        "frames": [scene.frame_start, scene.frame_end], "fps": args.fps,
        "camera_focus_width": width, "camera_ground": ground,
        "camera_preset": args.camera_preset,
        "bounds": {"min": list(low), "max": list(high)},
    }
    destination.with_suffix(".import.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    if args.preview:
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(args.preview.resolve())
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
