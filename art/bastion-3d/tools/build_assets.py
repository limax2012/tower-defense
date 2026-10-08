"""Build editable Bastion hard-surface assets and Unity FBX exports with Blender.

Run from the repository root with Blender 4.5+:
  blender --background --python art/bastion-3d/tools/build_assets.py -- --all

Public coordinates are Unity metres: Y up, +Z forward. Each metre is 20 authored
logical pixels. Blender meshes use Z up and -Y forward; FBX converts this basis.
"""
from __future__ import annotations
import argparse
import json
import math
import random
import shutil
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector, Quaternion, Matrix

sys.path.insert(0, str(Path(__file__).resolve().parent))
from material_surfaces import SURFACES, build_texture_maps, create_material, describe_material
from asset_io import write_json

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / 'art/bastion-3d'
UNITY = ROOT / 'unity/MaximalBastion3D/Assets/Art'
CONTENT = ROOT / 'src/MaximalBastion/ContentData'
CATALOG = []
MATERIALS = {}
PI = math.pi

def u(p):
    return Vector((p[0], -p[2], p[1]))

def rgb(hex_color):
    a = tuple(int(hex_color.lstrip('#')[i:i+2], 16) / 255 for i in (0, 2, 4))
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in a)

def texture_maps():
    build_texture_maps(ART, UNITY)

def mat(name, color, metallic=.65, roughness=.36, emission=0):
    if name not in MATERIALS:
        MATERIALS[name] = create_material(name, color, metallic, roughness, emission)
    return MATERIALS[name]

def palette():
    return {key: mat(name, color, metallic, roughness, emission)
            for key, (name, color, metallic, roughness, finish, amplitude, emission) in SURFACES.items()}


P = {}

def energy(id, color):
    return mat('Energy_' + id, color, .20, .30, 1.35)

def empty(name, parent=None, p=(0, 0, 0), rot=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.location = u(p)
    obj.rotation_euler = (rot[0], -rot[2], rot[1])
    obj.empty_display_size = .13
    obj.empty_display_type = 'ARROWS'
    return obj

def finish(obj, name, parent, p, material, bevel=0):
    obj.name = name
    obj.parent = parent
    obj.location = u(p)
    if material:
        obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new('Machined edge', 'BEVEL')
        shortest_edge=min((obj.data.vertices[edge.vertices[0]].co-obj.data.vertices[edge.vertices[1]].co).length for edge in obj.data.edges)
        modifier.width = min(bevel,min(obj.dimensions)*.4,shortest_edge*.32)
        modifier.segments = 2
        modifier.affect = 'EDGES'
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        modifier = obj.modifiers.new('Weighted surfaces', 'WEIGHTED_NORMAL')
        modifier.keep_sharp = True
        modifier.weight = 35
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

def box(name, parent, p, size, material, bevel=.025, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    obj = finish(obj, name, parent, p, material, bevel)
    obj.rotation_euler = (rot[0], -rot[2], rot[1])
    return obj

def cylinder(name, parent, p, radius, depth, material, vertices=12, axis=(0, 1, 0), bevel=.02, radius2=None):
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius2, depth=depth)
    obj = finish(bpy.context.object, name, parent, p, material, bevel)
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(u(axis).normalized())
    return obj

def beam(name, parent, a, b, radius, material, vertices=8):
    delta = Vector(b) - Vector(a)
    return cylinder(name, parent, (Vector(a) + Vector(b)) / 2, radius, delta.length, material, vertices, delta, .009)

def slab(name, parent, points, y, height, material, bevel=.02):
    """Closed polygon extrusion in the local XZ plane."""
    count = len(points)
    vertices = [u((x, level, z)) for level in (y - height / 2, y + height / 2) for x, z in points]
    faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
    faces.extend((i, (i+1) % count, (i+1) % count + count, i + count) for i in range(count))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, parent, (0, 0, 0), material, bevel)

def ring(name, parent, p, radius, tube, material, axis=(0, 1, 0), major_segments=24):
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=tube, major_segments=major_segments, minor_segments=6)
    obj = finish(bpy.context.object, name, parent, p, material)
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(u(axis).normalized())
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj

def sleeve(name,parent,p,outer,inner,depth,material,axis=(0,0,1),segments=16):
    """Closed annular metal sleeve with visible bore walls and a real opening."""
    vertices=[]
    for z,r in [(-depth/2,outer),(depth/2,outer),(-depth/2,inner),(depth/2,inner)]:
        vertices.extend((math.cos(i*PI*2/segments)*r,math.sin(i*PI*2/segments)*r,z) for i in range(segments))
    faces=[]
    for i in range(segments):
        j=(i+1)%segments
        faces.extend([(i,j,j+segments,i+segments),(i+2*segments,i+3*segments,j+3*segments,j+2*segments),
                      (i,i+2*segments,j+2*segments,j),(i+segments,j+segments,j+3*segments,i+3*segments)])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    obj=finish(obj,name,parent,p,material,.014)
    obj.rotation_mode='QUATERNION'
    obj.rotation_quaternion=Vector((0,0,1)).rotation_difference(u(axis).normalized())
    return obj

def bolts(parent, radius, y, count=8, offset=PI/8):
    for i in range(count):
        angle = i * PI * 2 / count + offset
        cylinder('Fastener_%02d' % i, parent, (math.sin(angle)*radius,y,math.cos(angle)*radius), .041,.025,P['black'],6,bevel=.005)

def vents(parent, center, count, spacing=.11, width=.27, axis='x'):
    for i in range(count):
        p = list(center)
        p[0 if axis == 'x' else 2] += (i-(count-1)/2)*spacing
        size = (.035,.025,width) if axis == 'x' else (width,.025,.035)
        box('Cooling_slot_%02d' % i,parent,p,size,P['black'],.006)

def tower_base(root, radius, core):
    base = empty('Base',root)
    cylinder('Ground_gasket',base,(0,.065,0),radius*.91,.13,P['rubber'],8,(0,1,0),.015)
    cylinder('Foundation',base,(0,.19,0),radius,.23,P['dark'],8,bevel=.055)
    cylinder('Plinth_chamfer',base,(0,.35,0),radius*.92,.15,P['steel'],8,bevel=.036)
    cylinder('Turret_bearing',base,(0,.50,0),radius*.57,.21,P['black'],24,bevel=.025)
    ring('Bearing_race',base,(0,.54,0),radius*.54,.042,P['edge'])
    for i in range(8):
        angle = i*PI/4
        pod = empty('Armor_sector_%02d'%i,base,(0,0,0),rot=(0,angle,0))
        box('Segment_armor',pod,(0,.39,radius*.71),(radius*.49,.105,.23),P['armor'],.032)
        light_parent=pod
        if i in [1,7]:
            light_parent=empty('Tier2_Light_%02d'%i,pod)
        elif i not in [0,4]:
            light_parent=empty('Tier3_Light_%02d'%i,pod)
        box('Indicator_lens_%02d'%i,light_parent,(0,.452,radius*.73),(radius*.30,.03,.056),core,.009)
        box('Underside_web',pod,(0,.145,radius*.64),(.12,.16,.32),P['steel'],.02)
    bolts(base,radius*.77,.45)
    apex=empty('Apex_Stabilizers',base)
    for i in range(4):
        a=i*PI/2+PI/4
        node=empty('Apex_sector_%d'%i,apex,rot=(0,a,0))
        box('Apex_fin',node,(0,.29,radius*.94),(.27,.19,.16),P['steel'],.03)
        box('Apex_lens',node,(0,.396,radius*.94),(.19,.025,.09),core,.007)
    return base

def tower_rig(id,radius,core,height):
    root=empty(id)
    root['contentId']=id
    root['unityForward']='+Z'
    root['logicalPixelsPerUnit']=20
    tower_base(root,radius,core)
    yaw=empty('Yaw',root,(0,.59,0))
    cylinder('Rotating_collar',yaw,(0,.08,0),radius*.54,.18,P['steel'],16,bevel=.03)
    cylinder('Upper_traverse_neck',yaw,(0,height-.59-.36,0),.26,.50,P['dark'],12,bevel=.023)
    cylinder('Pitch_axle',yaw,(0,height-.59-.09,0),.20,.55,P['steel'],12,(1,0,0),.022)
    tier2=empty('Tier2_Capacitor',yaw)
    box('Reserve_housing',tier2,(0,.21,-radius*.69),(.62,.22,.26),P['steel'],.025)
    box('Reserve_gauge',tier2,(0,.337,-radius*.70),(.43,.018,.09),core,.006)
    tier3=empty('Tier3_Cooling',yaw)
    for side in [-1,1]:
        box('Auxiliary_cooler',tier3,(side*radius*.65,.17,0),(.21,.25,.48),P['steel'],.03)
        box('Cooler_face',tier3,(side*radius*.65,.312,0),(.13,.025,.35),P['armor'],.013)
    pitch=empty('Pitch',yaw,(0,height-.59,0))
    recoil=empty('Recoil',pitch)
    return root,yaw,pitch,recoil

def muzzle(parent,p,name='Muzzle'):
    return empty(name,parent,p)

def prism(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],1.05,e,1.62)
    cylinder('Core_elevation_socket',yaw,(0,.47,0),.35,.67,P['dark'],8,bevel=.035)
    for x in [-.65,.65]:
        box('Side_yoke',yaw,(x,.58,0),(.25,.71,.96),P['steel'],.055)
        box('Sloped_armor',yaw,(x,.95,-.08),(.27,.13,.89),P['armor'],.03)
        box('Rear_power_window',yaw,(x,.68,-.50),(.14,.30,.035),e,.012)
        cylinder('Pitch_bearing',yaw,(x,.99,0),.19,.28,P['dark'],12,(1,0,0),.018)
        cylinder('Bearing_cap',yaw,(x+(.15 if x>0 else -.15),.99,0),.12,.025,P['edge'],12,(1,0,0),.009)
    box('Barrel_casing',recoil,(0,.055,.82),(.265,.225,1.54),P['steel'],.025)
    box('Barrel_energy_spine',recoil,(0,.19,.94),(.125,.035,1.16),e,.008)
    box('Barrel_underside',recoil,(0,-.072,.98),(.22,.05,.90),P['black'],.012)
    box('Emitter_collar',recoil,(0,.055,1.51),(.33,.275,.13),P['steel'],.018)
    box('Emitter_lens',recoil,(0,.055,1.583),(.185,.155,.022),e,.008)
    # Every nested diamond shares the same centre and winding.
    diamond=[(0,.75),(.75,0),(0,-.75),(-.75,0)]
    levels=[(.08,.80),(.28,1.0),(.37,1.0)]
    vertices=[u((x*scale,y,z*scale)) for y,scale in levels for x,z in diamond]
    faces=[(3,2,1,0),(8,9,10,11)]
    faces.extend((level*4+i,level*4+(i+1)%4,(level+1)*4+(i+1)%4,(level+1)*4+i)
                 for level in range(2) for i in range(4))
    mesh=bpy.data.meshes.new('Diamond_frame')
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    frame=bpy.data.objects.new('Diamond_frame',mesh)
    bpy.context.collection.objects.link(frame)
    finish(frame,'Diamond_frame',recoil,(0,0,0),P['steel'],.018)
    slab('Violet_facet',recoil,[(x*.88,z*.88) for x,z in diamond],.386,.054,e,.01)
    slab('White_inner_diamond',recoil,[(x*.46,z*.46) for x,z in diamond],.42,.025,P['white'],.005)
    slab('Violet_centre',recoil,[(x*.185,z*.185) for x,z in diamond],.438,.013,e,.004)
    # The core facets form one concentric assembly above the stationary barrel spine.
    mount=empty('CoreMount',recoil,(0,.19,0))
    bpy.context.view_layer.update()
    for name in ['Diamond_frame','Violet_facet','White_inner_diamond','Violet_centre']:
        obj=bpy.data.objects[name]
        world=obj.matrix_world.copy()
        obj.parent=mount
        obj.matrix_world=world
    mount.rotation_euler.z=PI/4
    for x in [-.91,.91]:
        box('Resonator_rail',yaw,(x,.63,-.03),(.10,.35,.90),P['steel'],.023)
        box('Resonator_light',yaw,(x,.825,-.03),(.065,.018,.71),e,.006)
    vents(yaw,(0,.30,-.48),5,.1,.19)
    muzzle(recoil,(0,.055,1.605))
    return root

def needle(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],.9,e,1.60)
    cylinder('Compact_pedestal',yaw,(0,.39,0),.38,.59,P['dark'],8,bevel=.04)
    box('Receiver',recoil,(0,0,-.13),(.76,.44,.83),P['steel'],.07)
    box('Receiver_roof',recoil,(0,.26,-.23),(.66,.12,.57),P['armor'],.045)
    box('Rail_shroud',recoil,(0,-.04,.64),(.39,.27,1.07),P['dark'],.035)
    for x in [-.21,.21]:
        box('Accelerator_rail',recoil,(x,.03,.63),(.12,.20,1.25),P['edge'],.019)
        box('Plasma_trace',recoil,(x,.147,.61),(.055,.03,.99),e,.008)
    cylinder('Plasma_bore',recoil,(0,0,1.21),.12,.055,P['black'],8,(0,0,1),.007)
    cylinder('Muzzle_core',recoil,(0,0,1.245),.075,.02,e,8,(0,0,1),.006)
    for x in [-.46,.46]:
        cylinder('Side_capacitor',recoil,(x,-.10,-.22),.14,.62,P['dark'],12,(0,0,1),.025)
        ring('Capacitor_band',recoil,(x,-.10,-.10),.143,.028,e,(0,0,1))
    vents(recoil,(0,.328,-.23),5,.105,.35)
    box('Rear_connector',recoil,(0,0,-.60),(.34,.19,.16),P['copper'],.025)
    muzzle(recoil,(0,0,1.28))
    return root

def shard(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],.9,e,1.48)
    cylinder('Low_traverse_pedestal',yaw,(0,.34,0),.39,.5,P['dark'],12,bevel=.032)
    points=[(-.36,-.55),(.36,-.55),(.79,.20),(.64,.56),(-.64,.56),(-.79,.20)]
    slab('Fan_receiver',recoil,points,0,.35,P['steel'],.055)
    slab('Faceted_roof',recoil,[(x*.87,z*.87) for x,z in points],.21,.085,P['armor'],.027)
    for i,angle in enumerate([-.32,0,.32]):
        pod=empty('Emitter_%d'%i,recoil,(math.sin(angle)*.5,0,math.cos(angle)*.4),rot=(0,angle,0))
        box('Emitter_housing',pod,(0,0,.32),(.285,.285,.71),P['dark'],.026)
        box('Heat_rail',pod,(0,.159,.30),(.19,.036,.51),e,.009)
        box('Amber_lens',pod,(0,0,.685),(.19,.17,.022),e,.01)
        muzzle(pod,(0,0,.72),'Muzzle' if i==1 else 'Muzzle_%d'%i)
    vents(recoil,(0,.267,-.29),5,.11,.18)
    for x in [-.59,.59]:
        cylinder('Feed_hub',recoil,(x,-.02,-.14),.22,.16,P['copper'],8,(1,0,0),.025)
    return root

def watchtower(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],1.0,e,2.30)
    cylinder('Tall_optics_mast',yaw,(0,.83,0),.31,1.45,P['dark'],8,bevel=.04)
    for x in [-.37,.37]:
        box('Mast_plate',yaw,(x,.81,0),(.15,1.15,.40),P['armor'],.038)
        box('Mast_power',yaw,(x,1.05,.22),(.07,.46,.025),e,.007)
    box('Breech',recoil,(0,0,-.19),(.63,.48,1.03),P['steel'],.065)
    box('Long_shroud',recoil,(0,-.015,.83),(.28,.25,1.86),P['dark'],.02)
    for x in [-.18,.18]:
        box('Precision_rail',recoil,(x,.025,.89),(.07,.21,1.78),P['armor'],.015)
    box('White_spine',recoil,(0,.124,.87),(.065,.018,1.71),e,.005)
    cylinder('Focus_collar',recoil,(0,0,1.82),.23,.17,P['steel'],8,(0,0,1),.015)
    cylinder('Focus_lens',recoil,(0,0,1.916),.105,.022,e,8,(0,0,1),.005)
    box('Optics_mount',recoil,(0,.34,-.24),(.32,.18,.57),P['dark'],.026)
    cylinder('Scope_body',recoil,(0,.47,-.08),.145,.70,P['armor'],12,(0,0,1),.019)
    cylinder('Scope_lens',recoil,(0,.47,.282),.111,.021,e,12,(0,0,1),.005)
    vents(recoil,(0,.268,-.35),5,.095,.30)
    muzzle(recoil,(0,0,1.95))
    return root

def frost(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],.9,e,1.88)
    cylinder('Cryogenic_column',yaw,(0,.64,0),.32,1.18,P['dark'],8,bevel=.035)
    cylinder('Containment_lower',recoil,(0,-.24,0),.57,.16,P['steel'],8,bevel=.027)
    cylinder('Ice_prism_lower',recoil,(0,-.02,0),.37,.54,e,4,bevel=.014,radius2=.21)
    cylinder('Ice_prism_upper',recoil,(0,.48,0),.21,.49,e,4,bevel=.012,radius2=.025)
    for i in range(4):
        a=i*PI/2+PI/4
        x,z=math.sin(a)*.43,math.cos(a)*.43
        beam('Containment_rib',recoil,(x,-.26,z),(x*.86,.56,z*.86),.08,P['armor'])
        cylinder('Rib_joint',recoil,(x,-.24,z),.13,.12,P['steel'],8,bevel=.015)
    box('Single_emitter',recoil,(0,-.13,.60),(.24,.23,.71),P['steel'],.025)
    box('Frost_lens',recoil,(0,-.13,.97),(.15,.14,.021),e,.009)
    for x in [-.56,.56]:
        cylinder('Coolant_tank',yaw,(x,.65,-.15),.18,.80,P['edge'],12,bevel=.03)
        ring('Tank_clamp',yaw,(x,.67,-.15),.18,.035,P['dark'])
    muzzle(recoil,(0,-.13,1.0))
    return root

def ember(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],.95,e,1.53)
    cylinder('Heat_insulator',yaw,(0,.35,0),.46,.50,P['dark'],10,bevel=.035)
    cylinder('Combustion_casing',recoil,(0,0,.22),.39,1.20,P['steel'],10,(0,0,1),.035)
    cylinder('Black_bore',recoil,(0,0,.842),.285,.045,P['black'],10,(0,0,1),.012)
    cylinder('Flame_core',recoil,(0,0,.872),.173,.02,e,10,(0,0,1),.01)
    for z in [-.23,-.02,.19,.4,.64]:
        ring('Coil_winding',recoil,(0,0,z),.403,.044,P['copper'],(0,0,1),20)
        ring('Hot_seam',recoil,(0,0,z+.065),.397,.017,e,(0,0,1),20)
    for x in [-.55,.55]:
        cylinder('Thermal_reservoir',yaw,(x,.60,-.22),.22,.89,P['dark'],12,bevel=.03)
        cylinder('Reservoir_cap',yaw,(x,1.06,-.22),.23,.09,P['armor'],12,bevel=.017)
        box('Red_level_window',yaw,(x,.69,.01),(.075,.44,.03),e,.01)
        beam('Coolant_feed',yaw,(x,.40,-.21),(x*.54,.70,.1),.045,P['copper'])
    muzzle(recoil,(0,0,.91))
    return root

def breaker(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],1.05,e,1.57)
    cylinder('Heavy_traverse',yaw,(0,.33,0),.58,.48,P['dark'],12,bevel=.04)
    box('Recoil_block',recoil,(0,0,-.19),(.89,.62,.95),P['steel'],.08)
    box('Armored_roof',recoil,(0,.36,-.18),(.76,.12,.81),P['armor'],.04)
    cylinder('Octagonal_barrel',recoil,(0,-.03,.69),.335,1.1,P['dark'],8,(0,0,1),.03)
    cylinder('Muzzle_brake',recoil,(0,-.03,1.21),.45,.36,P['steel'],6,(0,0,1),.028)
    cylinder('Muzzle_cavity',recoil,(0,-.03,1.401),.31,.03,P['black'],6,(0,0,1),.009)
    # One broad slit makes the cannon read as a single heavy weapon.
    box('Gold_aperture',recoil,(0,-.03,1.423),(.41,.105,.016),e,.008)
    for x in [-.56,.56]:
        cylinder('Recoil_piston',recoil,(x,-.14,.19),.12,.86,P['edge'],12,(0,0,1),.02)
        cylinder('Piston_housing',recoil,(x,-.14,-.30),.18,.42,P['dark'],12,(0,0,1),.024)
        box('Gold_status_strip',recoil,(x,.05,-.26),(.07,.024,.32),e,.006)
    vents(recoil,(0,.429,-.17),6,.105,.40)
    muzzle(recoil,(0,-.03,1.46))
    return root

def arc(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],1.0,e,1.92)
    cylinder('Inductor_pedestal',yaw,(0,.59,0),.39,.93,P['dark'],12,bevel=.03)
    for y in [.24,.42,.60,.78]:
        ring('Induction_coil',yaw,(0,y,0),.39,.038,P['copper'])
    box('Fork_crosshead',recoil,(0,-.12,0),(1.13,.24,.56),P['steel'],.045)
    for x in [-.43,.43]:
        box('Electrode_arm',recoil,(x,.17,.19),(.20,.72,.36),P['armor'],.035)
        cylinder('Electrode_sphere',recoil,(x,.61,.27),.19,.29,P['edge'],12,bevel=.027,radius2=.10)
        cylinder('Energized_tip',recoil,(x,.775,.27),.10,.075,e,12,bevel=.018)
        box('Internal_conductor',recoil,(x*.78,.19,.20),(.04,.5,.20),e,.009)
        muzzle(recoil,(x,.82,.27),'ArcSocket_L' if x<0 else 'ArcSocket_R')
    cylinder('Centre_lens',recoil,(0,-.06,.36),.19,.16,e,8,(0,0,1),.015)
    muzzle(recoil,(0,.80,.27))
    return root

def mortar(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],1.15,e,1.30)
    for x in [-.61,.61]:
        box('Siege_cradle',yaw,(x,.44,.04),(.24,.87,.89),P['steel'],.06)
        cylinder('Elevation_trunnion',yaw,(x,.71,0),.25,.32,P['armor'],12,(1,0,0),.026)
        cylinder('Trunnion_cap',yaw,(x+(.18 if x>0 else -.18),.71,0),.15,.033,P['dark'],12,(1,0,0),.009)
    pitch.rotation_euler.x=-.66
    sleeve('Launch_tube',recoil,(0,0,.2375),.42,.37,1.355,P['dark'],segments=12)
    for z in [-.36,.11,.66]:
        sleeve('Tube_reinforcement',recoil,(0,0,z),.47,.42,.13,P['steel'],segments=12)
    sleeve('Armored_muzzle',recoil,(0,0,1.06),.52,.37,.29,P['armor'],segments=12)
    cylinder('Deep_bore',recoil,(0,0,.61),.37,.018,P['black'],12,(0,0,1),.005)
    cylinder('Charge_recess',recoil,(0,0,.627),.205,.025,P['steel'],12,(0,0,1),.007)
    cylinder('Charged_bore',recoil,(0,0,.646),.15,.014,e,12,(0,0,1),.005)
    ring('Muzzle_energy_lip',recoil,(0,0,1.20),.39,.016,e,(0,0,1))
    for x in [-.31,.31]:
        box('Tube_armored_fin',recoil,(x,-.30,.23),(.14,.14,.72),P['steel'],.02)
    box('Breech_cap',recoil,(0,0,-.52),(.60,.59,.15),P['steel'],.052)
    for x in [-.73,.73]:
        cylinder('Siege_power_cell',yaw,(x,.27,-.42),.16,.55,P['dark'],12,(0,0,1),.026)
        ring('Cell_charge',yaw,(x,.27,-.21),.164,.026,e,(0,0,1))
    muzzle(recoil,(0,0,1.24))
    return root

def beacon(spec):
    e=energy(spec['id'],spec['visual']['primary'])
    root,yaw,pitch,recoil=tower_rig(spec['id'],.95,e,1.58)
    cylinder('Uplink_mast',yaw,(0,.59,0),.28,1.0,P['dark'],8,bevel=.035)
    cylinder('Crystal_lower',recoil,(0,-.07,0),.42,.68,e,3,bevel=.012,radius2=.12)
    cylinder('Crystal_crown',recoil,(0,.46,0),.12,.38,e,3,bevel=.002,radius2=.015)
    for i in range(3):
        a=i*PI*2/3
        x,z=math.sin(a)*.61,math.cos(a)*.61
        beam('Antenna_support',yaw,(x*.4,.25,z*.4),(x,1.58,z),.085,P['steel'])
        beam('Antenna_blade',yaw,(x,1.3,z),(x,2.11,z),.065,P['armor'])
        cylinder('Uplink_tip',yaw,(x,2.13,z),.083,.13,e,8,bevel=.014)
    ring('Signal_emitter',recoil,(0,-.44,0),.55,.048,P['edge'],major_segments=24)
    muzzle(recoil,(0,.68,0))
    return root

TOWER_BUILDERS={'prism_beam':prism,'needle_turret':needle,'shard_fan':shard,'watchtower':watchtower,'frost_spire':frost,'ember_coil':ember,'breaker_cannon':breaker,'arc_relay':arc,'siege_mortar':mortar,'signal_beacon':beacon}

def articulated_leg(root, name, hip, knee, foot, width=.11, core=None):
    """Rigid links expose hip and knee pivots with ground-contact foot sockets."""
    group=empty(name,root,hip)
    knee_local=Vector(knee)-Vector(hip)
    foot_local=Vector(foot)-Vector(knee)
    hinge_axis=knee_local.cross(foot_local).normalized()
    if hinge_axis.length_squared<.01:
        hinge_axis=Vector((1,0,0))
    cylinder('Hip_bearing',group,(0,0,0),width*1.36,width*1.9,P['steel'],10,hinge_axis,.018)
    beam('Upper_link',group,(0,0,0),knee_local,width,P['armor'])
    knee_node=empty('Knee',group,knee_local)
    cylinder('Knee_bearing',knee_node,(0,0,0),width*1.22,width*1.6,P['dark'],10,hinge_axis,.017)
    beam('Lower_link',knee_node,(0,0,0),foot_local,width*.66,P['steel'])
    foot_node=empty('Foot',knee_node,foot_local)
    box('Contact_pad',foot_node,(0,.04,0),(width*1.9,.11,width*3.0),P['rubber'],.025)
    if core:
        cylinder('Joint_light',knee_node,hinge_axis*(width*.9),width*.55,.023,core,8,hinge_axis,.005)
    return group

def enemy(spec):
    id=spec['id']
    e=energy(id,spec['visual']['primary'])
    root=empty(id)
    body=empty('Body',root)
    root['contentId']=id
    styles={
        't1_crawler':(.70,.64,4), 't2_runner':(.60,.95,2),
        't3_brute':(1.05,1.02,6),'t4_aegis':(1.20,1.20,4),'t5_regenerator':(1.30,1.08,6)}
    r,h,legs=styles[id]
    body['restHeight']=h
    if id=='t2_runner':
        polygon=[(0,1.0),(.40,.25),(.47,-.68),(0,-.48),(-.47,-.68),(-.40,.25)]
        slab('Kite_hull',body,polygon,h,.32,P['dark'],.045)
        slab('Kite_armor',body,[(x*.82,z*.87) for x,z in polygon],h+.20,.10,P['armor'],.022)
        slab('Dorsal_core',body,[(0,.70),(.19,.11),(0,-.25),(-.19,.11)],h+.267,.034,e,.009)
        for x in [-.28,.28]:
            box('Vector_fin',body,(x,h+.34,-.36),(.055,.31,.46),P['steel'],.015,rot=(0,0,-.3 if x<0 else .3))
        for side in [-1,1]:
            articulated_leg(root,'Leg_L' if side<0 else 'Leg_R',(side*.32,h-.06,-.15),(side*.70,.43,-.59),(side*.62,.02,.13),.095,e)
        cylinder('Forward_sensor',body,(0,h,.84),.11,.13,e,6,(0,0,1),.012)
    else:
        sides=8 if id=='t4_aegis' else 6
        cylinder('Ventral_hull',body,(0,h-.17,0),r*.68,.34,P['dark'],sides,bevel=.055,radius2=r*.86)
        cylinder('Main_carapace',body,(0,h+.075,0),r*.87,.22,P['steel'],sides,bevel=.055,radius2=r*.72)
        cylinder('Core_recess',body,(0,h+.22,0),r*.47,.085,P['black'],sides,bevel=.02)
        cylinder('Energy_core',body,(0,h+.28,0),r*.30,.11,e,sides,bevel=.021)
        cylinder('Core_cap',body,(0,h+.36,0),r*.105,.04,P['white'],sides,bevel=.01)
        for i in range(legs):
            angle=(i+.5)*PI*2/legs
            sx,sz=math.sin(angle),math.cos(angle)
            hip=(sx*r*.64,h-.13,sz*r*.64)
            knee=(sx*r*1.03,h*.52,sz*r*1.03)
            foot=(sx*r*.98,.01,sz*r*1.11)
            articulated_leg(root,'Leg_%02d'%i,hip,knee,foot,.11 if legs<6 else .13,e)
        for i in range(sides):
            a=i*PI*2/sides
            panel=empty('Carapace_sector_%02d'%i,body,(0,0,0),rot=(0,a,0))
            box('Armor_plate',panel,(0,h+.22,r*.60),(r*.40,.105,r*.34),P['armor'],.027)
            box('Sector_energy',panel,(0,h+.285,r*.62),(r*.24,.025,.055),e,.006)
        cylinder('Face_recess',body,(0,h-.02,r*.80),r*.23,.13,P['black'],6,(0,0,1),.021)
        cylinder('Face_sensor',body,(0,h-.02,r*.88),r*.13,.03,e,6,(0,0,1),.009)
        if id=='t3_brute':
            for x in [-.61,.61]:
                box('Heavy_shoulder',body,(x,h+.33,0),(.43,.38,.87),P['steel'],.065)
                box('Shoulder_shield',body,(x,h+.55,-.07),(.37,.07,.62),P['armor'],.024)
            box('Forward_ram',body,(0,h-.11,.85),(1.0,.35,.27),P['dark'],.055)
            for x in [-.30,0,.30]:
                box('Ram_tooth',body,(x,h-.1,1.00),(.16,.20,.17),P['edge'],.027)
        if id=='t4_aegis':
            cylinder('Shield_mount',body,(0,h+.40,0),.17,.18,P['steel'],8,bevel=.017)
            shield=empty('ShieldEmitter',body,(0,h+.52,0))
            cylinder('Shield_projector',shield,(0,0,0),.46,.20,P['steel'],8,bevel=.025)
            ring('Shield_phase_ring',shield,(0,.13,0),.39,.04,e)
            for i in range(4):
                a=i*PI/2+PI/4
                x,z=math.sin(a)*r*.94,math.cos(a)*r*.94
                box('Outer_bulwark',body,(x,h+.18,z),(.37,.55,.37),P['steel'],.055,rot=(0,a,0))
                beam('Shield_conduit',body,(x,h+.415,z),(x*.38,h+.58,z*.38),.047,P['steel'])
                beam('Shield_feed',body,(x,h+.45,z),(x*.38,h+.615,z*.38),.017,e)
        if id=='t5_regenerator':
            repair=energy('RegeneratorRepair','#42D392')
            for i in range(5):
                a=i*PI*2/5
                node=empty('RepairPod_%02d'%i,body,(0,0,0),rot=(0,a,0))
                box('Petal_armor',node,(0,h+.33,.78),(.33,.20,.67),P['steel'],.047)
                cylinder('Repair_cell_cage',node,(0,h+.49,.72),.135,.48,P['dark'],8,(0,0,1),.015)
                box('Repair_window',node,(0,h+.623,.72),(.05,.014,.29),repair,.004)
                for side in [-1,1]:
                    box('Cell_side_window',node,(side*.132,h+.49,.72),(.014,.039,.27),repair,.004)
                for z in [.545,.895]:
                    cylinder('Cell_clamp',node,(0,h+.49,z),.153,.055,P['steel'],8,(0,0,1),.008)
                cylinder('Cell_end_lens',node,(0,h+.49,.966),.073,.016,repair,8,(0,0,1),.004)
                cylinder('Cell_socket',node,(0,h+.49,.43),.145,.095,P['armor'],8,(0,0,1),.014)
            cylinder('Repair_spine',body,(0,h+.61,0),.20,.65,P['dark'],6,bevel=.022,radius2=.13)
            sleeve('Repair_beacon_cage',body,(0,h+.99,0),.16,.104,.14,P['steel'],axis=(0,1,0),segments=6)
            cylinder('Repair_beacon',body,(0,h+1.052,0),.101,.016,repair,6,bevel=.004)
    empty('HitPoint',body,(0,h+.12,0))
    role_z={'t1_crawler':-.55,'t2_runner':-.57,'t3_brute':-.55,'t4_aegis':-.70,'t5_regenerator':-.85}[id]
    empty('RoleSocket',body,(0,h+.46,role_z))
    rank_y=h+(.75 if id=='t4_aegis' else .56 if id=='t5_regenerator' else .32 if id=='t2_runner' else .48)
    rank_z={'t1_crawler':.35,'t2_runner':.28,'t3_brute':.35,'t4_aegis':.15,'t5_regenerator':.55}[id]
    empty('RankSocket',body,(0,rank_y,rank_z))
    empty('GroundContact',root,(0,0,0))
    return root

def platform(id='foundry_platform'):
    root=empty(id)
    box('Platform_foundation',root,(0,-.16,0),(4,.32,4),P['dark'],.08)
    box('Working_deck',root,(0,.015,0),(3.86,.055,3.86),P['deck'],.035)
    for x in [-1.91,1.91]:
        box('Rim_rail',root,(x,.045,0),(.07,.08,3.79),P['edge'],.014)
    for z in [-1.91,1.91]:
        box('Rim_rail',root,(0,.045,z),(3.79,.08,.07),P['edge'],.014)
    for x in [-1.5,-.5,.5,1.5]:
        for z in [-1.5,-.5,.5,1.5]:
            box('Inlaid_tile',root,(x,.052,z),(.975,.018,.975),P['deck'],.008)
            for zz in [-.27,-.09,.09,.27]:
                box('Deck_tread',root,(x,.066,z+zz),(.68,.016,.025),P['deck'],.004)
    for x in [-1.73,1.73]:
        for z in [-1.73,1.73]:
            cylinder('Anchor_bolt',root,(x,.085,z),.065,.032,P['black'],6,bevel=.007)
    return root

def exchanger(id='foundry_heat_exchanger'):
    root=empty(id)
    box('Heat_plinth',root,(0,.14,0),(4,.28,2.8),P['dark'],.07)
    box('Recessed_radiator',root,(0,.31,0),(3.76,.15,2.53),P['black'],.03)
    for x in [-1.88,1.88]:
        box('Outer_frame',root,(x,.42,0),(.17,.30,2.80),P['steel'],.029)
    for z in [-1.3,1.3]:
        box('Outer_frame',root,(0,.42,z),(3.8,.30,.17),P['copper'],.025)
    for i in range(13):
        x=(i-6)*.266
        box('Thermal_blade',root,(x,.39,0),(.13,.12,2.42),P['copper'],.014)
        box('Hot_slit',root,(x+.089,.355,0),(.041,.035,2.17),P['heat'],.007)
    for z in [-.76,.76]:
        box('Grate_crossbar',root,(0,.49,z),(3.63,.085,.115),P['steel'],.017)
    for x in [-1.50,1.50]:
        for z in [-1.23,1.23]:
            cylinder('Frame_fastener',root,(x,.578,z),.065,.025,P['black'],6,bevel=.007)
    empty('Heat',root,(0,.57,0))
    return root

def furnace(id='foundry_furnace'):
    root=empty(id)
    box('Furnace_foundation',root,(0,.18,0),(6.2,.36,4.2),P['dark'],.10)
    box('Furnace_shell',root,(0,3.35,0),(5.45,6.05,3.20),P['rust'],.12)
    box('Front_hot_recess',root,(0,3.03,1.67),(4.18,4.85,.14),P['black'],.04)
    for x in [-2.45,2.45]:
        box('Load_bearing_pillar',root,(x,3.5,1.76),(.48,6.45,.46),P['steel'],.075)
        for y in [1.1,3.5,5.9]:
            box('Pillar_clamp',root,(x,y,2.03),(.57,.20,.10),P['copper'],.02)
    for x in [-1.58,-1.05,-.525,0,.525,1.05,1.58]:
        box('Vertical_heat_channel',root,(x,3.06,1.755),(.27,4.29,.075),P['heat'],.016)
    for y in [1.15,2.25,3.35,4.45,5.55]:
        box('Furnace_safety_grille',root,(0,y,1.91),(4.09,.23,.23),P['copper'],.035)
    box('Crown_frame',root,(0,6.58,0),(5.82,.32,3.60),P['steel'],.08)
    box('Crown_cap',root,(0,6.83,0),(5.32,.18,3.10),P['dark'],.05)
    for x in [-1.40,1.40]:
        cylinder('Roof_vent',root,(x,7.12,0),.66,.47,P['steel'],12,bevel=.045)
        cylinder('Vent_recess',root,(x,7.37,0),.48,.035,P['black'],12,bevel=.014)
        rotor=empty('FanRotor_L' if x<0 else 'FanRotor_R',root,(x,7.40,0))
        rotor['animation']='continuous'
        rotor['rotationAxis']='Y'
        rotor['degreesPerSecond']=90 if x<0 else -90
        cylinder('Fan_hub',rotor,(0,0,0),.095,.035,P['copper'],12,bevel=.008)
        for j in range(5):
            blade=empty('FanBlade_%02d'%j,rotor,rot=(0,j*PI*2/5,0))
            box('Fan_blade',blade,(.09,0,.24),(.20,.018,.36),P['edge'],.009,rot=(0,.3,0))
        for i in range(4):
            box('Roof_vent_slats',root,(x,7.45,(i-1.5)*.21),(.78,.027,.047),P['steel'],.009)
        empty('Exhaust_L' if x<0 else 'Exhaust_R',root,(x,7.56,0))
    for side in [-1,1]:
        x=side*2.83
        for z in [-.84,.84]:
            cylinder('Side_vertical_feed',root,(x,3.05,z),.14,4.6,P['copper'],12,bevel=.02)
        box('Side_service_panel',root,(side*2.738,3.4,0),(.05,2.0,1.15),P['steel'],.014)
    box('Back_service_door',root,(0,2.20,-1.626),(2.05,3.5,.055),P['steel'],.025)
    box('Door_split',root,(0,2.20,-1.666),(.032,3.40,.035),P['black'],.006)
    for x in [-.14,.14]:
        box('Service_handle',root,(x,2.4,-1.72),(.045,.39,.09),P['edge'],.01)
    for x in [-2,-1.3,-.6,.1,.8,1.5,2.2]:
        box('Threshold_hazard',root,(x,.378,1.93),(.28,.025,.30),P['hazard'],.006,rot=(0,.52,0))
    empty('Heat_Furnace',root,(0,3.06,2.05))
    return root

def pipe_rack(id='foundry_pipe_rack'):
    root=empty(id)
    for x in [-3.5,3.5]:
        for z in [-.66,.66]:
            box('Anchor_foot',root,(x,.09,z),(.62,.18,.58),P['dark'],.036)
            box('Rack_column',root,(x,2.22,z),(.23,4.32,.23),P['steel'],.03)
        box('Rack_crosshead',root,(x,4.35,0),(.39,.32,2.0),P['steel'],.035)
    for z in [-.57,0,.57]:
        beam('Transfer_pipe',root,(-4.0,4.47,z),(4.0,4.47,z),.18,P['copper'],16)
        for x in [-3.5,-1.65,1.65,3.5]:
            ring('Pipe_band',root,(x,4.47,z),.19,.037,P['steel'],(1,0,0),16)
    for x in [-3.45,3.45]:
        beam('Diagonal_brace',root,(x,3.25,-.64),(x,4.22,.64),.065,P['edge'])
    return root

def gantry(id='foundry_gantry'):
    root=empty(id)
    for x in [-4.1,4.1]:
        box('Gantry_base',root,(x,.15,0),(1.0,.3,1.6),P['dark'],.055)
        box('Gantry_upright',root,(x,2.86,0),(.48,5.40,.66),P['steel'],.055)
        box('Copper_upright',root,(x,2.87,.39),(.22,4.95,.095),P['copper'],.02)
        for y in [1.1,2.8,4.5]:
            box('Brace_flange',root,(x,y,0),(.66,.18,.86),P['dark'],.025)
    box('Gantry_crossbeam',root,(0,5.6,0),(8.80,.70,.97),P['steel'],.07)
    box('Header_recess',root,(0,5.6,.502),(7.15,.42,.055),P['black'],.012)
    for x in [-3,-2,-1,0,1,2,3]:
        box('Hazard_chevron',root,(x,5.6,.545),(.16,.36,.027),P['hazard'],.009,rot=(0,0,.5))
    for x in [-2.1,2.1]:
        box('Downlight',root,(x,5.18,0),(.57,.12,.57),P['dark'],.023)
        box('Downlight_lens',root,(x,5.11,0),(.36,.035,.36),P['heat'],.012)
    return root

def cabinet(id='foundry_service_cabinet'):
    root=empty(id)
    box('Cabinet_foot',root,(0,.09,0),(1.92,.18,1.04),P['dark'],.043)
    box('Cabinet_body',root,(0,1.28,0),(1.70,2.24,.86),P['steel'],.057)
    for x in [-.44,.44]:
        box('Access_panel',root,(x,1.28,.463),(.80,2.06,.055),P['dark'],.02)
        for y in [.54,.72,.90,1.08,1.26]:
            box('Vent_slot',root,(x,y,.497),(.64,.055,.016),P['black'],.004)
        box('Panel_latch',root,(x+.26,1.62,.52),(.055,.21,.05),P['edge'],.01)
        box('Indicator',root,(x-.15,2.1,.505),(.22,.055,.03),P['heat'],.01)
    box('Top_cowl',root,(0,2.43,0),(1.85,.16,1.0),P['armor'],.035)
    return root

def perimeter_wall(id='foundry_perimeter_wall'):
    root=empty(id)
    box('Wall_foot',root,(0,.13,0),(6.0,.26,.80),P['dark'],.045)
    box('Wall_panel',root,(0,1.02,0),(5.82,1.64,.46),P['steel'],.04)
    for x in [-2.85,0,2.85]:
        box('Wall_pier',root,(x,1.13,0),(.24,2.0,.70),P['dark'],.03)
    box('Cap_rail',root,(0,2.06,0),(6.0,.17,.72),P['copper'],.03)
    for x in [-2.30,-1.15,1.15,2.30]:
        box('Wall_recess',root,(x,1.02,.239),(.93,1.20,.038),P['dark'],.02)
        box('Running_light',root,(x,1.71,.267),(.55,.04,.02),P['heat'],.006)
    return root

def stack(id='foundry_stack'):
    root=empty(id)
    cylinder('Stack_foundation',root,(0,.24,0),1.30,.48,P['dark'],8,bevel=.065)
    cylinder('Stack_flare',root,(0,1.45,0),1.06,1.95,P['steel'],12,bevel=.06,radius2=.71)
    cylinder('Flue',root,(0,5.13,0),.71,5.46,P['rust'],16,bevel=.035)
    for y in [2.5,4.2,5.9,7.5]:
        cylinder('Flue_band',root,(0,y,0),.77,.20,P['steel'],16,bevel=.022)
    cylinder('Flue_crown',root,(0,7.98,0),.88,.54,P['steel'],16,bevel=.03)
    cylinder('Dark_exhaust',root,(0,8.26,0),.66,.025,P['black'],16,bevel=.005)
    cylinder('Heat_inside',root,(0,8.279,0),.48,.011,P['heat'],16,bevel=.004)
    empty('Exhaust',root,(0,8.32,0))
    empty('Heat',root,(0,8.28,0))
    for i in range(4):
        a=i*PI/2
        beam('Base_brace',root,(math.sin(a)*1.15,.42,math.cos(a)*1.15),(math.sin(a)*.66,2.5,math.cos(a)*.66),.09,P['edge'])
    return root

def road(id='foundry_road_straight'):
    root=empty(id)
    box('Road_bed',root,(0,-.10,0),(2.80,.20,4),P['dark'],.015)
    box('Lane_surface',root,(0,.008,0),(2.72,.018,4),P['rust'],.006)
    for x in [-1.33,1.33]:
        box('Copper_edge',root,(x,.027,0),(.06,.025,4),P['copper'],.004)
        for z in [-1.60,-.80,0,.8,1.6]:
            box('Edge_marker',root,(x-.10 if x>0 else x+.10,.043,z),(.09,.025,.22),P['heat'],.005)
    for z in [-1.50,-.50,.50,1.50]:
        box('Lane_seam',root,(0,.024,z),(2.33,.014,.026),P['dark'],.004)
    return root

def service_bay(id='foundry_service_bay'):
    root=empty(id)
    box('Service_bay_foundation',root,(0,.14,0),(7.8,.28,3.8),P['dark'],.08)
    box('Service_bay_shell',root,(0,4.15,-.18),(7.3,8.0,3.12),P['rust'],.11)
    for x in [-3.57,0,3.57]:
        box('Structural_upright',root,(x,4.26,1.49),(.28,8.14,.35),P['steel'],.04)
    for y in [1.2,4.2,7.2]:
        box('Facade_crossbeam',root,(0,y,1.58),(7.27,.24,.27),P['steel'],.035)
    for x in [-1.75,1.75]:
        box('Service_bay_shutter',root,(x,2.74,1.433),(3.15,2.63,.055),P['dark'],.02)
        for y in [1.69,2.04,2.39,2.74,3.09,3.44,3.79]:
            box('Shutter_louver',root,(x,y,1.482),(2.91,.085,.043),P['deck'],.01)
        box('Upper_vent_bank',root,(x,5.62,1.429),(2.82,1.80,.05),P['black'],.02)
        for xx in [-.95,-.57,-.19,.19,.57,.95]:
            box('Upper_vent_fin',root,(x+xx,5.62,1.53),(.10,1.56,.17),P['copper'],.014)
        box('Service_bay_light',root,(x,6.86,1.52),(2.26,.045,.055),P['heat'],.01)
    box('Roof_lip',root,(0,8.26,0),(7.73,.22,3.61),P['steel'],.048)
    for x in [-2.7,-.9,.9,2.7]:
        box('Roof_cooling_housing',root,(x,8.54,-.20),(1.2,.38,2.3),P['dark'],.055)
        for z in [-.72,-.36,0,.36,.72]:
            box('Roof_grate',root,(x,8.746,z-.20),(.92,.035,.09),P['edge'],.01)
    box('Back_panelling',root,(0,4.2,-1.776),(6.6,6.8,.055),P['deck'],.03)
    for x in [-2.7,0,2.7]:
        box('Back_wall_brace',root,(x,4.2,-1.831),(.15,6.8,.09),P['steel'],.02)
    return root

def catwalk(id='foundry_catwalk'):
    root=empty(id)
    box('Walkway_girder',root,(0,3.0,0),(8,.32,1.3),P['dark'],.036)
    box('Walkway_deck',root,(0,3.19,0),(8,.07,1.27),P['deck'],.018)
    for x in [-3.6,3.6]:
        box('Walkway_column',root,(x,1.47,0),(.18,2.94,.24),P['steel'],.023)
        box('Walkway_foot',root,(x,.09,0),(.56,.18,.66),P['dark'],.03)
    for z in [-.63,.63]:
        beam('Handrail',root,(-4,4.16,z),(4,4.16,z),.045,P['copper'])
        beam('Midrail',root,(-4,3.68,z),(4,3.68,z),.027,P['steel'])
        for x in [-3.8,-1.9,0,1.9,3.8]:
            beam('Rail_post',root,(x,3.2,z),(x,4.16,z),.04,P['steel'])
    for x in [-3.4,-2.6,-1.8,-1.0,-.2,.6,1.4,2.2,3.0,3.8]:
        box('Walkway_tread',root,(x,3.238,0),(.028,.018,.98),P['edge'],.005)
    return root

def defended_core(id='defended_core'):
    root=empty(id)
    e=energy('DefendedCore','#43DED8')
    cylinder('Core_foundation',root,(0,.22,0),1.67,.44,P['dark'],8,bevel=.08)
    cylinder('Reactor_socket',root,(0,.52,0),1.30,.30,P['steel'],8,bevel=.05)
    cylinder('Reactor_body',root,(0,1.50,0),.77,1.65,P['dark'],8,bevel=.05)
    cylinder('Reactor_energy',root,(0,1.68,0),.60,1.62,e,8,bevel=.025)
    for i in range(4):
        a=i*PI/2+PI/4
        node=empty('Core_buttress_%d'%i,root,rot=(0,a,0))
        box('Reactor_armor',node,(0,1.60,.94),(.48,2.22,.42),P['steel'],.065)
        box('Ceramic_shield',node,(0,1.75,1.178),(.36,1.5,.07),P['armor'],.027)
        box('Integrity_indicator',node,(0,1.74,1.227),(.085,1.05,.025),e,.008)
    cylinder('Reactor_crown',root,(0,2.77,0),1.21,.33,P['steel'],8,bevel=.047)
    cylinder('Crown_lens',root,(0,2.961,0),.67,.05,e,8,bevel=.013)
    for y in [.83,1.40,1.97,2.53]:
        ring('Containment_band',root,(0,y,0),.88,.067,P['edge'])
    empty('CoreLight',root,(0,2.2,0))
    empty('DamageSmoke',root,(0,3.04,0))
    return root

ENV_BUILDERS={'foundry_platform':platform,'foundry_heat_exchanger':exchanger,'foundry_furnace':furnace,
    'foundry_pipe_rack':pipe_rack,'foundry_gantry':gantry,'foundry_service_cabinet':cabinet,
    'foundry_perimeter_wall':perimeter_wall,'foundry_stack':stack,'foundry_road_straight':road,
    'foundry_service_bay':service_bay,'foundry_catwalk':catwalk,'defended_core':defended_core}

def tactical(spec):
    id=spec['id']
    root=empty(id)
    e=energy(id,spec['visual']['primary'])
    if id=='pulse_plate':
        base=empty('Base',root)
        points=[(0,.68),(.68,0),(0,-.68),(-.68,0)]
        slab('Contact_chassis',base,points,.058,.116,P['dark'],.035)
        slab('Pressure_armor',base,[(x*.88,z*.88) for x,z in points],.132,.041,P['steel'],.016)
        cylinder('Pulse_recess',base,(0,.16,0),.30,.027,P['black'],8,bevel=.009)
        cylinder('Pulse_core',base,(0,.18,0),.19,.027,e,8,bevel=.009)
        ring('Charge_ring',base,(0,.177,0),.29,.026,e,major_segments=16)
        for i in range(4):
            pod=empty('Plate_segment_%02d'%i,base,rot=(0,i*PI/2,0))
            box('Corner_armor',pod,(0,.158,.44),(.16,.06,.19),P['armor'],.017)
            box('Armed_indicator',pod,(0,.194,.45),(.066,.023,.085),e,.006)
        empty('EffectOrigin',root,(0,.2,0))
    else:
        tower_base(root,1.1,e)
        cylinder('Forge_inner_column',root,(0,1.13,0),.43,1.18,P['dark'],12,bevel=.037)
        cylinder('Containment_base',root,(0,.69,0),.68,.20,P['steel'],12,bevel=.034)
        cylinder('Charge_core',root,(0,1.28,0),.34,.98,e,6,bevel=.027)
        for y in [.96,1.28,1.60]:
            ring('Charge_coil',root,(0,y,0),.46,.061,P['copper'])
        for i in range(3):
            a=i*PI*2/3
            pod=empty('Forge_armor_%02d'%i,root,rot=(0,a,0))
            box('Containment_pillar',pod,(0,1.24,.59),(.25,1.28,.27),P['steel'],.04)
            box('Armor_face',pod,(0,1.35,.746),(.19,.80,.055),P['armor'],.02)
            box('Charge_gauge',pod,(0,1.32,.786),(.059,.51,.026),e,.009)
        cylinder('Forge_crown',root,(0,1.95,0),.70,.20,P['steel'],6,bevel=.028)
        rotor=empty('ChargeRotor',root,(0,2.09,0))
        rotor['animation']='continuous'
        rotor['rotationAxis']='Y'
        rotor['degreesPerSecond']=25
        ring('Rotor_ring',rotor,(0,0,0),.51,.042,P['edge'],major_segments=18)
        for i in range(3):
            a=i*PI*2/3
            box('Rotor_energy',rotor,(math.sin(a)*.51,.045,math.cos(a)*.51),(.17,.07,.17),e,.013)
        empty('OutputSocket',root,(0,2.30,0))
    return root

def role_module(id):
    root=empty(id)
    kind=id.replace('enemy_role_','')
    colors={'accelerator':'#E8B637','restorer':'#42D392','bulwark':'#69CFFF','jammer':'#FF4C98','disruptor':'#A489EA','elite':'#F3C25C','boss':'#FF4C98'}
    e=energy(id,colors[kind])
    cylinder('Mounting_socket',root,(0,.035,0),.23,.07,P['dark'],6,bevel=.014)
    if kind=='accelerator':
        for x in [-.16,.16]:
            cylinder('Boost_cell',root,(x,.17,0),.09,.35,P['steel'],8,(0,0,1),.012)
            cylinder('Boost_lens',root,(x,.17,.19),.065,.025,e,8,(0,0,1),.005)
        slab('Acceleration_fin',root,[(-.03,-.19),(.03,-.19),(.03,.20),(-.03,.20)],.35,.21,P['armor'],.013)
    elif kind=='restorer':
        cylinder('Repair_pillar',root,(0,.23,0),.11,.37,P['steel'],8,bevel=.014)
        box('Repair_cross_vertical',root,(0,.48,0),(.105,.40,.11),e,.013)
        box('Repair_cross_horizontal',root,(0,.48,0),(.34,.105,.11),e,.013)
    elif kind=='bulwark':
        cylinder('Shield_stem',root,(0,.20,0),.095,.27,P['steel'],8,bevel=.012)
        cylinder('Shield_dish',root,(0,.37,0),.34,.09,P['armor'],8,bevel=.02)
        cylinder('Shield_lens',root,(0,.426,0),.24,.025,e,8,bevel=.006)
    elif kind in ('jammer','disruptor'):
        for x in [-.17,.17]:
            beam('Aerial',root,(x,.07,0),(x,.50,0),.038,P['steel'])
            if kind=='disruptor':
                box('Disruptor_bar',root,(x,.42,0),(.075,.30,.11),e,.01)
            else:
                cylinder('Jammer_node',root,(x,.52,0),.075,.07,e,8,bevel=.01)
        if kind=='jammer':
            ring('Jammer_loop',root,(0,.34,0),.16,.025,P['edge'],(0,0,1),16)
    else:
        radius=.42 if kind=='boss' else .29
        cylinder('Rank_crown',root,(0,.12,0),radius,.14,P['steel'],6,bevel=.022)
        for i in range(6):
            a=i*PI/3
            x,z=math.sin(a)*radius*.87,math.cos(a)*radius*.87
            cylinder('Crown_fin',root,(x,.28,z),.069,.27,P['armor'],4,bevel=.012,radius2=.028)
            cylinder('Rank_light',root,(x,.445,z),.037,.06,e,6,bevel=.008)
        cylinder('Rank_core',root,(0,.20,0),radius*.45,.035,e,6,bevel=.009)
    return root

def dressing():
    import importlib.util
    path = Path(__file__).with_name('environment_geometry.py')
    spec = importlib.util.spec_from_file_location('bastion_environment', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.build(globals())


def objects_under(root):
    return [root]+list(root.children_recursive)

def clear_asset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def uv_and_stats(root):
    objects=objects_under(root)
    for obj in objects:
        if obj.type!='MESH':
            continue
        mesh=bmesh.new()
        mesh.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(mesh,faces=mesh.faces)
        mesh.to_mesh(obj.data)
        mesh.free()
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        if not obj.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025)
            bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    points=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    lo=[min(p[k] for p in points) for k in range(3)]
    hi=[max(p[k] for p in points) for k in range(3)]
    # Convert bounds back from Blender coordinates.
    return {'min':[lo[0],lo[2],-hi[1]],'max':[hi[0],hi[2],-lo[1]],
            'meshCount':sum(o.type=='MESH' for o in objects),
            'triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in objects if o.type=='MESH')}

def hierarchy_data(root):
    result=[]
    for obj in objects_under(root):
        if obj.type!='EMPTY':
            continue
        pos=obj.location
        world=obj.matrix_world.translation
        forward=obj.matrix_world.to_3x3()@Vector((0,-1,0))
        up=obj.matrix_world.to_3x3()@Vector((0,0,1))
        result.append({'name':obj.name,'parent':obj.parent.name if obj.parent else None,
                       'localPosition':[pos.x,pos.z,-pos.y],
                       'worldPosition':[world.x,world.z,-world.y],
                       'worldForward':[forward.x,forward.z,-forward.y],
                       'worldUp':[up.x,up.z,-up.y],
                       'localEulerDegrees':[math.degrees(obj.rotation_euler.x),math.degrees(obj.rotation_euler.z),-math.degrees(obj.rotation_euler.y)]})
        if 'animation' in obj:
            result[-1]['animation']={'type':obj['animation'],'axis':obj['rotationAxis'],'degreesPerSecond':obj['degreesPerSecond']}
    return result

def studio(root, output, angle=0, width=720, elevation=.65):
    bounds=uv_and_stats(root)
    centre=Vector([(bounds['min'][i]+bounds['max'][i])/2 for i in range(3)])
    extent=max(bounds['max'][i]-bounds['min'][i] for i in range(3))
    ground=box('Review_ground',None,(0,bounds['min'][1]-.10,0),(200,.2,200),mat('Review_Backdrop','#17202A',.15,.55),0)
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=32
    scene.cycles.use_denoising=True
    if not scene.world:
        scene.world=bpy.data.worlds.new('Studio_world')
    scene.world.color=(.12,.12,.12)
    lights=[]
    for name,location,power,color,size in [
        ('Key',(-4,7,4),1000,(.66,.83,1),5),('Rim',(3,5,-5),1300,(.55,.65,1),4),('Warm',(-3,2,-2),500,(1,.48,.22),3)]:
        data=bpy.data.lights.new(name,'AREA')
        data.energy=power*max(1,extent/3)**2
        data.color=color
        data.shape='DISK'
        data.size=size*max(1,extent/3)
        obj=bpy.data.objects.new(name,data)
        bpy.context.collection.objects.link(obj)
        obj.location=u(location)*max(1,extent/3)
        obj.rotation_euler=(u(centre)-obj.location).to_track_quat('-Z','Y').to_euler()
        lights.append(obj)
    cam_data=bpy.data.cameras.new('Review_camera')
    cam=bpy.data.objects.new('Review_camera',cam_data)
    bpy.context.collection.objects.link(cam)
    direction=Vector((math.sin(angle+.58),elevation,math.cos(angle+.58))).normalized()
    cam.location=u(centre+direction*extent*2.1)
    cam.rotation_euler=(u(centre)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam_data.type='ORTHO'
    cam_data.ortho_scale=extent*1.43
    scene.camera=cam
    scene.render.resolution_x=width
    scene.render.resolution_y=width
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(output)
    scene.view_settings.view_transform='AgX'
    scene.use_nodes=True
    nodes=scene.node_tree.nodes
    nodes.clear()
    rl=nodes.new('CompositorNodeRLayers')
    glare=nodes.new('CompositorNodeGlare')
    glare.glare_type='FOG_GLOW'
    glare.quality='HIGH'
    glare.threshold=1.2
    comp=nodes.new('CompositorNodeComposite')
    scene.node_tree.links.new(rl.outputs['Image'],glare.inputs['Image'])
    scene.node_tree.links.new(glare.outputs['Image'],comp.inputs['Image'])
    bpy.ops.render.render(write_still=True)
    for obj in [ground,cam]+lights:
        bpy.data.objects.remove(obj,do_unlink=True)

def export_asset(root, category, spec=None, render=False):
    id=root.name
    bounds=uv_and_stats(root)
    metadata={'id':id,'category':category,'displayName':spec.get('displayName',id) if spec else id,
              'scale':.05,'forward':'+Z','up':'+Y','bounds':bounds,'hierarchy':hierarchy_data(root)}
    if spec:
        metadata['energyColor']=spec['visual']['primary']
        metadata['logicalRadius']=spec['visual']['radius']
    source=ART/'models'/category
    source.mkdir(parents=True,exist_ok=True)
    target=UNITY/'Models'/category
    target.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/(id+'.blend')),check_existing=False)
    export_root,restore=batched_export_copy(root)
    compensate_fbx_handedness(export_root)
    convert_rig_basis(export_root)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects_under(export_root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active=export_root
    fbx=target/(id+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'EMPTY','MESH'},
        axis_forward='-Z',axis_up='Y',global_scale=1.0,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,
        mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,
        path_mode='STRIP',embed_textures=False)
    for obj in objects_under(export_root):
        bpy.data.objects.remove(obj,do_unlink=True)
    for obj,name in restore:
        obj.name=name
    metadata['fbx']='Models/'+category+'/'+id+'.fbx'
    metadata['blend']='models/'+category+'/'+id+'.blend'
    write_json(UNITY/'Metadata'/(id+'.json'), metadata)
    CATALOG.append(metadata)
    if render:
        studio(root,ART/'review'/(id+'-front.png'))
        studio(root,ART/'review'/(id+'-rear.png'),PI)
    print('ASSET_READY '+id+' '+str(bounds),flush=True)
    return metadata

def compensate_fbx_handedness(root):
    """Preserve authored coordinates across Unity's FBX handedness conversion."""
    objects=objects_under(root)
    original={obj:obj.matrix_world.copy() for obj in objects}
    reflection=Matrix.Diagonal((-1,1,1,1))
    for obj in objects:
        if obj.type=='MESH':
            obj.data.transform(reflection)
            obj.data.flip_normals()
            obj.data.update()
    def depth(obj):
        result=0
        while obj.parent:
            result+=1
            obj=obj.parent
        return result
    for obj in sorted(objects,key=depth):
        obj.matrix_world=reflection@original[obj]@reflection
    bpy.context.view_layer.update()


def convert_rig_basis(root):
    """Give exported nodes Unity-native local axes without moving any surfaces."""
    objects=objects_under(root)
    original={obj:obj.matrix_world.copy() for obj in objects}
    basis=Matrix.Rotation(PI/2,4,'X')
    def depth(obj):
        result=0
        while obj.parent:
            result+=1
            obj=obj.parent
        return result
    for obj in sorted(objects,key=depth):
        obj.matrix_world=original[obj]@basis if obj.type=='EMPTY' else original[obj]
    bpy.context.view_layer.update()

def batched_export_copy(root):
    """Merge fixed hardware under each mechanical pivot, retaining source parts."""
    originals=objects_under(root)
    restore=[(obj,obj.name) for obj in originals]
    names={obj:obj.name for obj in originals}
    for obj in originals:
        obj.name=names[obj]+'__source'
    copies={}
    for obj in originals:
        copy=obj.copy()
        if obj.type=='MESH':
            copy.data=obj.data.copy()
        copy.name=names[obj]
        bpy.context.collection.objects.link(copy)
        copies[obj]=copy
    for obj,copy in copies.items():
        copy.parent=copies.get(obj.parent)
    bpy.context.view_layer.update()
    pivots={'Base','Yaw','Pitch','Recoil','Body','Knee','Foot'}
    groups={}
    export_root=copies[root]
    for copy in copies.values():
        if copy.type!='MESH':
            continue
        parent=copy.parent
        while parent and parent!=export_root and parent.name not in pivots and not parent.name.startswith(('Leg_','Knee','Foot','FanRotor','ChargeRotor','Tier2','Tier3','Apex')):
            parent=parent.parent
        parent=parent or export_root
        world=copy.matrix_world.copy()
        copy.parent=parent
        copy.matrix_world=world
        groups.setdefault(parent,[]).append(copy)
    for pivot,meshes in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active=meshes[0]
        if len(meshes)>1:
            bpy.ops.object.join()
        meshes[0].name=pivot.name+'_Geometry'
    return export_root,restore

def write_catalog():
    # Preserve other categories when rebuilding only selected IDs.
    entries=[]
    for path in sorted((UNITY/'Metadata').glob('*.json')):
        if path.name in ('catalog.json','materials.json'):
            continue
        data=json.loads(path.read_text(encoding='utf-8'))
        if 'id' in data and 'fbx' in data:
            entries.append(data)
    catalog={'schemaVersion':1,'logicalPixelsPerUnit':20,'unityForward':'+Z','unityUp':'+Y','assets':entries}
    for path in [ART/'catalog.json',UNITY/'Metadata/catalog.json']:
        write_json(path, catalog)
    old=ART/'materials.json'
    material_map={m['name']:m for m in json.loads(old.read_text()).get('materials',[])} if old.exists() else {}
    for name,m in MATERIALS.items():
        if name=='Review_Backdrop':
            continue
        material_map[name] = describe_material(m)
    for path in [ART/'materials.json',UNITY/'Metadata/materials.json']:
        write_json(path, {'materials':list(material_map.values())})

def main():
    global P
    parser=argparse.ArgumentParser()
    parser.add_argument('--all',action='store_true')
    parser.add_argument('--ids',nargs='*')
    parser.add_argument('--render',action='store_true')
    parser.add_argument('--textures-only',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    texture_maps()
    P=palette()
    if args.textures_only:
        write_catalog()
        print('TEXTURES_READY',flush=True)
        return
    towers=json.loads((CONTENT/'Towers.json').read_text(encoding='utf-8-sig'))
    enemies=json.loads((CONTENT/'Enemies.json').read_text(encoding='utf-8-sig'))
    tactics=json.loads((CONTENT/'Tactics.json').read_text(encoding='utf-8-sig'))
    selected=set(args.ids or ['prism_beam','t1_crawler','foundry_platform'])
    for spec in towers:
        if args.all or spec['id'] in selected:
            clear_asset()
            root=TOWER_BUILDERS[spec['id']](spec)
            export_asset(root,'Towers',spec,args.render)
            write_catalog()
    for spec in enemies:
        if args.all or spec['id'] in selected:
            clear_asset()
            root=enemy(spec)
            export_asset(root,'Enemies',spec,args.render)
            write_catalog()
    for spec in tactics.values():
        if args.all or spec['id'] in selected:
            clear_asset()
            export_asset(tactical(spec),'Tactics',spec,args.render)
            write_catalog()
    for kind in ['accelerator','restorer','bulwark','jammer','disruptor','elite','boss']:
        id='enemy_role_'+kind
        if args.all or id in selected:
            clear_asset()
            export_asset(role_module(id),'EnemyModules',render=args.render)
            write_catalog()
    for id,builder in ENV_BUILDERS.items():
        if args.all or id in selected:
            clear_asset()
            export_asset(builder(),'Environment',render=args.render)
            write_catalog()
    if args.all or 'cinderworks_dressing' in selected:
        clear_asset()
        export_asset(dressing(),'Environment',render=args.render)
    write_catalog()
    print('ASSET_BUILD_COMPLETE',flush=True)

if __name__=='__main__':
    main()
