"""Render the editable foundry assembly around its authored roads and decks."""
import json
import bpy
from mathutils import Vector
from material_surfaces import create_floor_material, floor_uvs


def render_environment(b):
    bpy.ops.wm.open_mainfile(filepath=str(b.ART/'models/Environment/cinderworks_dressing.blend'))
    bpy.context.preferences.filepaths.save_version=0
    root=bpy.data.objects['cinderworks_dressing']
    b.MATERIALS={material.name:material for material in bpy.data.materials if 'baseColorHex' in material}
    b.P=b.palette()
    floor_material=create_floor_material(b.ART)
    ground=b.box('Industrial_ground',root,(0,-1.2,0),(900,.4,900),floor_material,.02)
    floor_uvs(ground)
    foundation=b.box('Assembly_foundation',root,(0,-.54,-2),(66,1,60),floor_material,.10)
    floor_uvs(foundation)
    definition=json.loads((b.CONTENT/'Maps/FoundryLoop.json').read_text())
    points=[Vector(((point['x']-480)*.05,(point['y']-360)*.05)) for point in definition['path']]

    def outline(width):
        left,right=[],[]
        for i,point in enumerate(points):
            before=(point-points[i-1]).normalized() if i else (points[1]-point).normalized()
            after=(points[i+1]-point).normalized() if i<len(points)-1 else before
            n1,n2=Vector((-before.y,before.x)),Vector((-after.y,after.x))
            normal=(n1+n2).normalized()
            offset=normal*(width/2/max(.1,normal.dot(n1)))
            left.append(tuple(point+offset))
            right.append(tuple(point-offset))
        return left+list(reversed(right))

    b.slab('Continuous_road_bed',root,outline(2.90),.014,.045,b.P['copper'],.006)
    b.slab('Drive_surface',root,outline(2.72),.049,.022,b.P['dark'],.004)
    for index,region in enumerate(definition['buildableRegions']):
        x=(region['x']+region['width']/2-480)*.05
        z=(region['y']+region['height']/2-360)*.05
        width,depth=region['width']*.05,region['height']*.05
        b.box('Build_deck_%d'%index,root,(x,.101,z),(width,.20,depth),b.P['deck'],.025)
        for edge in [-width/2+.035,width/2-.035]:
            b.box('Deck_edge',root,(x+edge,.206,z),(.045,.022,depth-.06),b.P['edge'],.005)
        for edge in [-depth/2+.035,depth/2-.035]:
            b.box('Deck_edge',root,(x,.206,z+edge),(width-.06,.022,.045),b.P['edge'],.005)
        for j in range(1,int(depth/.60)):
            b.box('Deck_seam',root,(x,.208,z-depth/2+j*.60),(width-.1,.014,.018),b.P['dark'],.002)

    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1440
    scene.render.resolution_y=810
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    world=bpy.data.worlds.new('Foundry night')
    world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.080,.13,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.45
    scene.world=world
    for name,position,target,power,color,size in [
        ('Broad sky',(-20,45,0),(0,0,-5),26000,(.53,.69,1),40),
        ('Cool fill',(25,20,20),(0,0,0),12000,(.62,.81,1),35),
        ('Warm hall',(-17,8,-19),(-17,2,-14),1600,(1,.26,.06),12)]:
        data=bpy.data.lights.new(name,'AREA')
        data.energy=power
        data.color=color
        data.shape='DISK'
        data.size=size
        obj=bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj)
        obj.location=b.u(position)
        obj.rotation_euler=(b.u(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    camera_data=bpy.data.cameras.new('Foundry review camera')
    camera=bpy.data.objects.new('Foundry review camera',camera_data)
    scene.collection.objects.link(camera)
    scene.camera=camera
    camera_data.lens=34
    camera_data.clip_end=400
    views=[('front',(-43,33,40),(-2,4,-8)),('ground',(-28,4,18),(-5,6,-20)),
           ('rear',(31,10,-17),(-6,2,8)),('core',(34,5,25),(26,1.3,13))]
    for name,position,target in views:
        camera.location=b.u(position)
        camera.rotation_euler=(b.u(target)-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(b.ART/'review'/('cinderworks-assembled-'+name+'.png'))
        bpy.ops.render.render(write_still=True)
    camera.location=b.u(views[0][1])
    camera.rotation_euler=(b.u(views[0][2])-camera.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.wm.save_as_mainfile(filepath=str(b.ART/'models/Environment/cinderworks-review-assembly.blend'))
