"""Shared Blender/URP surface finishes and deterministic PBR texture channels."""
from __future__ import annotations
import math
import shutil
import bpy
import numpy as np
from asset_io import write_json


# Roughness is perceptual roughness in both Principled BSDF and URP Lit.
# Colored energy uses linear RGB radiance; base colors remain authored sRGB.
SURFACES = {
    'dark': ('Bastion_Graphite', '#29333D', .16, .56, 'coated', .016, 0),
    'steel': ('Bastion_Gunmetal', '#596974', .82, .39, 'brushed', .014, 0),
    'armor': ('Bastion_Ceramic', '#AEBAC1', .0, .47, 'ceramic', .010, 0),
    'edge': ('Bastion_EdgeAlloy', '#7B8A94', .90, .27, 'brushed', .012, 0),
    'black': ('Bastion_Recess', '#111922', .0, .80, 'coated', .016, 0),
    'rubber': ('Bastion_Rubber', '#182129', .0, .86, 'rubber', .020, 0),
    'white': ('Bastion_WhiteEnergy', '#E3F7FF', .0, .22, 'energy', 0, 2.3),
    'copper': ('Foundry_Copper', '#A06B49', .85, .38, 'brushed', .020, 0),
    'rust': ('Foundry_Oxide', '#573D32', .03, .84, 'oxide', .026, 0),
    'deck': ('Foundry_Deck', '#424C53', .30, .67, 'coated', .022, 0),
    'hazard': ('Foundry_Hazard', '#C69A4C', .0, .61, 'coated', .012, 0),
    'heat': ('Foundry_Heat', '#FF7934', .0, .35, 'energy', 0, 3),
    'wall': ('Foundry_PaintedWall', '#354149', .10, .66, 'coated', .018, 0),
    'roof': ('Foundry_Roof', '#25323B', .22, .60, 'coated', .020, 0),
    'concrete': ('Foundry_Concrete', '#444140', .0, .86, 'concrete', .032, 0),
    'cool_pipe': ('Foundry_ReturnPipe', '#416564', .12, .60, 'coated', .019, 0),
    'heat_low': ('Foundry_StatusAmber', '#FF932F', .0, .40, 'energy', 0, .65),
    'work_light': ('Foundry_WorkLight', '#ACD3DE', .0, .32, 'energy', 0, .85),
}


def linear_rgb(hex_color):
    values = tuple(int(hex_color.lstrip('#')[i:i + 2], 16) / 255 for i in (0, 2, 4))
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values)


def texture_names(name):
    return name + '_Roughness', name + '_MetallicSmoothness'


def write_image(name, pixels, art, unity, color_space='Non-Color'):
    height, width = pixels.shape[:2]
    image = bpy.data.images.get(name) or bpy.data.images.new(name, width, height, alpha=True)
    image.colorspace_settings.name = color_space
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.update()
    image.filepath_raw = str(art / 'textures' / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
    shutil.copy2(image.filepath_raw, unity / 'Textures' / (name + '.png'))
    return image


def build_texture_maps(art, unity):
    (art / 'textures').mkdir(parents=True, exist_ok=True)
    (unity / 'Textures').mkdir(parents=True, exist_ok=True)
    records = []
    size = 512
    yy, xx = np.mgrid[0:size, 0:size] / size
    rng = np.random.default_rng(74219)
    grain = (rng.random((size, size)) - .5) * 2
    brushed = np.repeat(rng.random((size, 1)) - .5, size, axis=1) * 2
    variation = (np.sin(xx * math.tau * 3 + .65 * np.sin(yy * math.tau * 2)) +
                 .45 * np.cos(yy * math.tau * 5 + xx * math.tau)) / 1.45
    for spec in SURFACES.values():
        name, _, metallic, centre, finish, amplitude, emission = spec
        if emission > 0:
            continue
        if finish == 'brushed':
            field = .72 * brushed + .18 * grain + .10 * variation
        elif finish in ('oxide', 'concrete'):
            field = .50 * variation + .50 * grain
        else:
            field = .25 * variation + .75 * grain
        field -= field.mean()
        roughness = np.clip(centre + amplitude * field, .04, .98)
        rough_name, packed_name = texture_names(name)
        rough_pixels = np.ones((size, size, 4), dtype=np.float32)
        rough_pixels[:, :, :3] = roughness[:, :, None]
        packed_pixels = np.ones((size, size, 4), dtype=np.float32)
        packed_pixels[:, :, :3] = metallic
        packed_pixels[:, :, 3] = 1 - roughness
        write_image(rough_name, rough_pixels, art, unity)
        write_image(packed_name, packed_pixels, art, unity)
        records.extend([
            {'name': rough_name, 'file': rough_name + '.png', 'colorSpace': 'Linear',
             'channels': 'RGB perceptual roughness', 'finish': finish,
             'meanRoughness': float(roughness.mean()), 'roughnessRange': [float(roughness.min()), float(roughness.max())]},
            {'name': packed_name, 'file': packed_name + '.png', 'colorSpace': 'Linear',
             'channels': 'R metallic; A perceptual smoothness (1 - roughness)', 'finish': finish, 'metallic': metallic},
        ])

    # Low-amplitude tileable concrete variation: broad mineral clouds, not a grid.
    size = 1024
    yy, xx = np.mgrid[0:size, 0:size] / size
    rng = np.random.default_rng(84509)
    cloud = np.zeros((size, size))
    for frequency, weight in [(1, 1), (2, .55), (5, .18), (11, .06)]:
        for _ in range(4):
            fx = int(rng.integers(1, frequency + 2))
            fy = int(rng.integers(-frequency - 1, frequency + 2))
            cloud += weight * np.sin(math.tau * (fx * xx + fy * yy) + rng.random() * math.tau)
    cloud /= np.max(np.abs(cloud))
    grain = rng.random((size, size)) - .5
    tone = np.clip(.85 + .034 * cloud + .007 * grain, .78, .92)
    height = .004 * cloud + .00012 * grain
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * size / 16
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * size / 16
    normal = np.stack((-dx, -dy, np.ones_like(dx)), axis=2)
    normal /= np.linalg.norm(normal, axis=2)[:, :, None]
    normal = normal * .5 + .5
    roughness = np.clip(.81 + .035 * cloud + .012 * grain, .75, .87)
    pixels_by_name = {
        'FoundryFloor_BaseColor': np.concatenate((np.repeat(tone[:, :, None], 3, axis=2), np.ones((size, size, 1))), axis=2),
        'FoundryFloor_Normal': np.concatenate((normal, np.ones((size, size, 1))), axis=2),
        'FoundryFloor_MetallicSmoothness': np.concatenate((np.zeros((size, size, 3)), 1 - roughness[:, :, None]), axis=2),
    }
    for name, pixels in pixels_by_name.items():
        write_image(name, pixels, art, unity)
    records.extend([
        {'name': 'FoundryFloor_BaseColor', 'file': 'FoundryFloor_BaseColor.png', 'colorSpace': 'sRGB',
         'channels': 'RGB neutral mineral albedo; multiply by material tint', 'tileMeters': 8},
        {'name': 'FoundryFloor_Normal', 'file': 'FoundryFloor_Normal.png', 'colorSpace': 'NormalMap',
         'channels': 'Tangent-space RGB normal, positive green Y', 'tileMeters': 8},
        {'name': 'FoundryFloor_MetallicSmoothness', 'file': 'FoundryFloor_MetallicSmoothness.png',
         'colorSpace': 'Linear', 'channels': 'R metallic; A perceptual smoothness', 'metallic': 0, 'tileMeters': 8},
    ])
    for path in [art / 'textures.json', unity / 'Metadata/textures.json']:
        path.parent.mkdir(parents=True, exist_ok=True)
        write_json(path, {'schemaVersion': 2, 'textures': records})


def create_material(name, color, metallic, roughness, emission):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    linear = linear_rgb(color)
    material.diffuse_color = (*linear, 1)
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*linear, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Emission Color'].default_value = (*linear, 1)
    bsdf.inputs['Emission Strength'].default_value = emission
    for spec in SURFACES.values():
        if spec[0] != name:
            continue
        material['surfaceFinish'] = spec[4]
        if emission == 0:
            rough_name, packed_name = texture_names(name)
            texture = material.node_tree.nodes.new('ShaderNodeTexImage')
            texture.name = rough_name
            texture.label = spec[4].capitalize() + ' perceptual roughness'
            texture.image = bpy.data.images.get(rough_name)
            texture.interpolation = 'Linear'
            if not texture.image:
                raise RuntimeError('Generate the surface textures before the palette: ' + rough_name)
            material.node_tree.links.new(texture.outputs['Color'], bsdf.inputs['Roughness'])
            material['roughnessTexture'] = rough_name + '.png'
            material['metallicSmoothnessTexture'] = packed_name + '.png'
        break
    material['baseColorHex'] = color
    material['metallic'] = metallic
    material['roughness'] = roughness
    material['emissionStrength'] = emission
    return material


def describe_material(material):
    color = material['baseColorHex']
    emission = material['emissionStrength']
    result = {'name': material.name, 'color': color, 'metallic': material['metallic'],
              'roughness': material['roughness'], 'emission': emission,
              'emissionLinear': [v * emission for v in linear_rgb(color)]}
    for key in ('surfaceFinish', 'roughnessTexture', 'metallicSmoothnessTexture'):
        if key in material:
            result[key] = material[key]
    return result


def create_floor_material(art):
    """Match the runtime concrete finish; floor mesh UVs use metres divided by eight."""
    material = bpy.data.materials.new('FoundryFloor_Concrete')
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    tint = tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in (.25, .22, .21))
    bsdf.inputs['Base Color'].default_value = (*tint, 1)
    bsdf.inputs['Metallic'].default_value = 0
    bsdf.inputs['Roughness'].default_value = .81

    def texture(name, color_space):
        image = bpy.data.images.load(str(art / 'textures' / (name + '.png')), check_existing=False)
        image.colorspace_settings.name = color_space
        node = nodes.new('ShaderNodeTexImage')
        node.name = name
        node.image = image
        node.extension = 'REPEAT'
        return node

    base = texture('FoundryFloor_BaseColor', 'sRGB')
    multiply = nodes.new('ShaderNodeMixRGB')
    multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = 1
    multiply.inputs[2].default_value = (*tint, 1)
    links.new(base.outputs['Color'], multiply.inputs[1])
    links.new(multiply.outputs[0], bsdf.inputs['Base Color'])
    packed = texture('FoundryFloor_MetallicSmoothness', 'Non-Color')
    inverse = nodes.new('ShaderNodeMath')
    inverse.operation = 'SUBTRACT'
    inverse.inputs[0].default_value = 1
    links.new(packed.outputs['Alpha'], inverse.inputs[1])
    links.new(inverse.outputs[0], bsdf.inputs['Roughness'])
    normal = texture('FoundryFloor_Normal', 'Non-Color')
    mapping = nodes.new('ShaderNodeNormalMap')
    mapping.inputs['Strength'].default_value = .6
    links.new(normal.outputs['Color'], mapping.inputs['Color'])
    links.new(mapping.outputs['Normal'], bsdf.inputs['Normal'])
    return material


def floor_uvs(obj):
    """Project horizontal floor UVs in world metres, retaining an eight-metre repeat."""
    bpy.context.view_layer.update()
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='Floor UV')
    for face in obj.data.polygons:
        for index in face.loop_indices:
            position = obj.matrix_world @ obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv = (position.x / 8, position.y / 8)
