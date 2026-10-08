"""Large, quiet industrial silhouettes outside the Cinderworks combat field."""
from types import SimpleNamespace
import bpy
import bmesh


def build(api, root):
    from environment_geometry import pipe_geometry
    a=SimpleNamespace(**api) if isinstance(api,dict) else api
    p=a.P
    box,cylinder,beam,ring=a.box,a.cylinder,a.beam,a.ring
    sites=[('DistantWorks_02',-2,-60,19,15),('DistantWorks_05',64,-10,15,25),('DistantWorks_08',2,63,27,13)]
    for name,x,z,width,depth in sites:
        old=bpy.data.objects.get(name)
        if old:
            for obj in list(old.children_recursive)+[old]:
                bpy.data.objects.remove(obj,do_unlink=True)
        for obj in list(root.children_recursive):
            if obj.name.startswith(('Distant_roofplant','Distant_window')):
                position=obj.matrix_world.translation
                if abs(position.x-x)<width*.6 and abs(-position.y-z)<depth*.6:
                    bpy.data.objects.remove(obj,do_unlink=True)
    wall,roof,concrete=p['wall'],p['roof'],p['concrete']

    def block(group,name,position,size,material=wall,edge=.09):
        return box(name,group,position,size,material,edge)

    def loading_bay(group,x,z,width=4.4,height=4.5,side=1):
        block(group,'Loading_recess',(x,height/2+.13,z),(width,height,.10),p['black'],.035)
        block(group,'Loading_shutter',(x,height/2+.12,z+side*.08),(width-.35,height-.25,.12),roof,.025)
        for y in [height*.25,height*.5,height*.75]:
            block(group,'Shutter_joint',(x,y,z+side*.155),(width-.4,.08,.035),p['dark'],.009)
        for dx in [-width/2-.16,width/2+.16]:
            block(group,'Loading_jamb',(x+dx,height/2+.14,z+side*.13),(.25,height+.42,.34),p['steel'],.04)
        block(group,'Loading_lintel',(x,height+.36,z+side*.20),(width+.6,.35,.52),p['steel'],.05)
        block(group,'Loading_step',(x,.12,z+side*.65),(width+.6,.24,1.25),concrete,.05)

    # Stepped extraction hall: a low receiving floor feeds a taller process stack.
    group=a.empty('DistantWorks_02',root)
    x,z=-2,-60
    block(group,'Process_foundation',(x,.10,z),(20,.38,16),concrete,.10)
    block(group,'Process_receiving',(x,3.7,z),(19,7.4,15))
    block(group,'Process_receiving_roof',(x,7.48,z),(19.4,.28,15.4),roof,.07)
    block(group,'Process_upper',(x+1.6,12.1,z-1.8),(12.5,9.2,10.6))
    block(group,'Process_upper_cap',(x+1.6,16.88,z-1.8),(12.9,.36,11),roof,.07)
    block(group,'Extraction_head',(x+3,19.05,z-2.3),(7.5,4.1,7.8),roof,.12)
    block(group,'Extraction_cap',(x+3,21.22,z-2.3),(8,.32,8.3),p['dark'],.07)
    for front in [-1,1]:
        face=z+front*7.55
        loading_bay(group,x-4.6,face,4.4,4.8,front)
        block(group,'Process_vent_recess',(x+4.1,4.4,face),(4.2,1.2,.10),p['black'],.02)
        for row in range(3):
            block(group,'Process_vent_louver',(x+4.1,4.0+row*.36,face+front*.09),(3.95,.18,.19),roof,.015)
        for xx in [x-8.7,x+.6,x+8.7]:
            block(group,'Process_wall_rib',(xx,3.8,face),(.25,7.35,.25),p['dark'],.035)
        block(group,'Upper_monitor_recess',(x+1.6,14.6,z-1.8+front*5.34),(9.7,.72,.10),p['black'],.02)
        for dx in [-3.2,0,3.2]:
            block(group,'Upper_monitor_glass',(x+1.6+dx,14.6,z-1.8+front*5.40),(2.8,.40,.03),p['cool_pipe'],.008)
    for zz in [z-4.7,z+1.6]:
        pipe_geometry(a,group,'Extraction_riser',[(x-8,1.35,zz),(x-8,18.8,zz),(x-.45,18.8,zz)],.63,roof,False)
        for y in [2.2,7.4,12.3,15.8]:
            ring('Riser_band',group,(x-8,y,zz),.65,.085,p['steel'],major_segments=16)
        block(group,'Duct_wall_sleeve',(x-.50,18.8,zz),(.32,1.65,1.65),p['dark'],.09)

    # Storage vessels share a protected pump gallery and a closed return manifold.
    group=a.empty('DistantWorks_05',root)
    x,z=64,-10
    block(group,'Storage_plinth',(x,.22,z),(15.6,.55,25.5),concrete,.12)
    for index,(zz,height) in enumerate([(z-7.7,8.6),(z,10.4),(z+7.7,8.6)]):
        radius=2.7
        cylinder('Storage_cradle',group,(x-2,.62,zz),radius+.4,.58,wall,20,bevel=.10)
        cylinder('Storage_jacket',group,(x-2,height/2+.95,zz),radius,height,roof,32,bevel=.12)
        cylinder('Storage_domed_shoulder',group,(x-2,height+1.55,zz),radius,1.2,wall,32,bevel=.12,radius2=radius*.66)
        cylinder('Storage_top',group,(x-2,height+2.19,zz),radius*.66,.22,roof,24,bevel=.08)
        for yy in [1.25,height*.48+.95,height+.69]:
            ring('Storage_pressure_band',group,(x-2,yy,zz),radius+.07,.10,p['dark'],major_segments=32)
        block(group,'Storage_inspection_panel',(x-4.72,height*.56,zz),(.10,1.6,.8),p['dark'],.04)
        beam('Storage_feed',group,(x-.2,1.15,zz),(x+3.7,1.15,zz),.28,p['cool_pipe'],16)
    beam('Storage_return_manifold',group,(x+3.7,1.15,z-8),(x+3.7,1.15,z+8),.32,p['cool_pipe'],16)
    block(group,'Storage_pump_housing',(x+4.3,1.48,z),(3.1,2.4,5.3),wall,.13)
    for side in [-1,1]:
        block(group,'Pump_access_panel',(x+4.3,1.5,z+side*2.70),(2.4,1.7,.11),roof,.05)
        block(group,'Pump_access_handle',(x+4.3,1.45,z+side*2.78),(.12,.54,.10),p['steel'],.02)
    block(group,'Pump_gallery_deck',(x+4.25,.75,z),(3.4,.18,21),p['deck'],.06)
    for xx in [x+2.65,x+5.85]:
        beam('Pump_gallery_handrail',group,(xx,1.84,z-10.3),(xx,1.84,z+10.3),.055,p['dark'])
        for zz in [z-10.3,z-5,z,z+5,z+10.3]:
            beam('Pump_gallery_stanchion',group,(xx,.84,zz),(xx,1.84,zz),.048,p['dark'])
    for j in range(3):
        block(group,'Pump_access_step',(x+4.25,.56-j*.20,z+10.8+j*.36),(2,.18,.39),p['deck'],.024)

    # Closed sawtooth roofs provide north-light glazing over a single workshop floor.
    group=a.empty('DistantWorks_08',root)
    x,z=2,63
    block(group,'Workshop_foundation',(x,.10,z),(27.5,.38,13.5),concrete,.10)
    block(group,'Workshop_shell',(x,3.5,z),(27,7,13),wall,.10)
    for index in range(3):
        x0=x-13.5+index*9
        x1=x0+9
        vertices=[a.u(point) for point in [(x0,7,z-6.65),(x1,7,z-6.65),(x1,10.7,z-6.65),
                                           (x0,7,z+6.65),(x1,7,z+6.65),(x1,10.7,z+6.65)]]
        faces=[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
        mesh=bpy.data.meshes.new('Sawtooth_roof')
        mesh.from_pydata(vertices,[],faces)
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        obj=bpy.data.objects.new('Sawtooth_roof',mesh)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        a.finish(obj,'Sawtooth_roof',group,(0,0,0),roof,.06)
        block(group,'Monitor_frame',(x1+.05,9.05,z),(.16,3.22,12.8),p['dark'],.025)
        for zz in [-4.3,0,4.3]:
            block(group,'Monitor_glazing',(x1+.14,9.05,z+zz),(.04,2.6,3.95),p['cool_pipe'],.012)
        beam('Roof_ridge',group,(x1,10.78,z-6.7),(x1,10.78,z+6.7),.10,p['dark'])
        beam('Roof_gutter',group,(x0,7.02,z-6.7),(x0,7.02,z+6.7),.09,p['dark'])
    for side in [-1,1]:
        face=z+side*6.56
        for xx in [x-7.5,x+7.5]:
            loading_bay(group,xx,face,5.4,4.4,side)
        for xx in [x-13.3,x-4.5,x+4.5,x+13.3]:
            block(group,'Workshop_pier',(xx,3.55,face),(.31,7.15,.32),p['dark'],.03)
