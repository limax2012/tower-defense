"""Closed, modular foundry architecture around the authored Cinderworks field."""
import math
from types import SimpleNamespace
import bpy
from mathutils import Vector


def pipe_geometry(a, root, name, points, radius=.15, material=None, flange=True):
    p, ring = a.P, a.ring
    material = material or p['copper']
    points = [Vector(point) for point in points]
    points = [point for i,point in enumerate(points) if i==0 or (point-points[i-1]).length>.001]
    samples = [points[0]]
    for i in range(1,len(points)-1):
        incoming = points[i-1]-points[i]
        outgoing = points[i+1]-points[i]
        trim = min(radius*3.2,incoming.length*.32,outgoing.length*.32)
        start = points[i]+incoming.normalized()*trim
        end = points[i]+outgoing.normalized()*trim
        for step in range(9):
            t=step/8
            samples.append(start*(1-t)**2+points[i]*(2*(1-t)*t)+end*t*t)
    samples.append(points[-1])
    vertices,faces=[],[]
    sides=12
    previous_tangent=None
    across=None
    for i,point in enumerate(samples):
        tangent=(samples[min(i+1,len(samples)-1)]-samples[max(i-1,0)]).normalized()
        if previous_tangent is None:
            axis=Vector((0,1,0)) if abs(tangent.y)<.95 else Vector((0,0,1))
            across=tangent.cross(axis).normalized()
        else:
            across=previous_tangent.rotation_difference(tangent)@across
            across=(across-tangent*across.dot(tangent)).normalized()
        up=across.cross(tangent).normalized()
        previous_tangent=tangent
        for j in range(sides):
            angle=j*math.tau/sides
            vertices.append(a.u(point+(across*math.cos(angle)+up*math.sin(angle))*radius))
    faces.append(tuple(reversed(range(sides))))
    for i in range(len(samples)-1):
        for j in range(sides):
            faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    faces.append(tuple(range((len(samples)-1)*sides,len(samples)*sides)))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    a.finish(obj,name,root,(0,0,0),material)
    for polygon in mesh.polygons:
        polygon.use_smooth=len(polygon.vertices)==4
    for index, (start, end) in enumerate(zip(points, points[1:])):
        direction = (end-start).normalized()
        if flange and (end-start).length>radius*8:
            for distance in [radius*4, (end-start).length-radius*4]:
                ring(name+'_flange', root, start+direction*distance, radius*1.13,
                     radius*.16, p['steel'], direction, 12)


def build(api):
    a = SimpleNamespace(**api)
    p = a.P
    box, beam, cylinder, ring, empty = a.box, a.beam, a.cylinder, a.ring, a.empty
    wall = p.get('wall', p['rust'])
    roof = p.get('roof', p['deck'])
    concrete = p.get('concrete', p['rust'])
    return_pipe = p.get('cool_pipe', p['steel'])
    heat_low = p.get('heat_low', p['heat'])
    work_light = p.get('work_light', p['white'])
    root = empty('cinderworks_dressing')
    templates = {}

    def part(builder, name, position, yaw=0, scale=(1, 1, 1)):
        if builder not in templates:
            obj = builder(name)
            templates[builder] = obj
        else:
            source = templates[builder]
            copies = {}
            for original in a.objects_under(source):
                obj = original.copy()
                bpy.context.collection.objects.link(obj)
                copies[original] = obj
            for original, obj in copies.items():
                obj.parent = copies.get(original.parent)
            obj = copies[source]
            obj.name = name
        obj.parent = root
        obj.location = a.u(position)
        obj.rotation_euler.z = yaw
        obj.scale = (scale[0], scale[2], scale[1])
        return obj

    def pipe(name, points, radius=.15, material=None, flange=True):
        pipe_geometry(a,root,name,points,radius,material,flange)

    def handrails(name, start, end, height=1):
        start, end = Vector(start), Vector(end)
        up = Vector((0, height, 0))
        beam(name+'_top', root, start+up, end+up, .045, p['copper'])
        beam(name+'_mid', root, start+up*.5, end+up*.5, .026, p['steel'])
        count = max(1, math.ceil((end-start).length/1.8))
        for i in range(count+1):
            foot = start.lerp(end, i/count)
            beam(name+'_post', root, foot, foot+up, .038, p['steel'])

    def building(name, center, width, depth, height, detailed=True):
        x, z = center
        group = empty(name, root)
        box(name+'_foot', group, (x, .11, z), (width+.5, .34, depth+.5), concrete, .10)
        box(name+'_shell', group, (x, height/2, z), (width, height, depth), wall, .13)
        box(name+'_roof', group, (x, height+.08, z), (width+.34, .24, depth+.34), roof, .07)
        box(name+'_roof_curb', group, (x, height+.27, z), (width-.55, .18, depth-.55), p['dark'], .04)
        for xx in [-width/2+.18, width/2-.18]:
            for zz in [-depth/2+.15, depth/2-.15]:
                box(name+'_corner', group, (x+xx, height/2, z+zz), (.28, height+.15, .28), p['steel'], .03)
        if not detailed:
            return group
        columns = max(2, math.ceil(width/3.4))
        for i in range(1, columns):
            xx = x-width/2+width*i/columns
            for zz in [-depth/2-.018, depth/2+.018]:
                box(name+'_wall_seam', group, (xx, height*.48, z+zz), (.04, height*.87, .026), p['dark'], .004)
        # Front and rear both have service access and readable panel construction.
        for side in [-1, 1]:
            face = z+side*(depth/2+.04)
            box(name+'_door_recess', group, (x-width*.25, 1.46, face), (2.14, 2.85, .08), p['black'], .022)
            box(name+'_service_door', group, (x-width*.25, 1.44, face+side*.06), (1.84, 2.59, .06), roof, .018)
            for j in range(7):
                box(name+'_door_rib', group, (x-width*.25, .45+j*.33, face+side*.10), (1.67, .044, .03), p['steel'], .004)
            box(name+'_door_light', group, (x-width*.25, 3.02, face+side*.12), (1.12, .055, .13), work_light, .012)
            empty('WorkLight_'+name+('_front' if side>0 else '_rear'), group, (x-width*.25, 2.91, face+side*.34))
            vent_y = min(height-1.1, 3.4)
            box(name+'_vent', group, (x+width*.24, vent_y, face), (width*.30, 1.12, .11), p['black'], .025)
            for j in range(5):
                box(name+'_louver', group, (x+width*.24, vent_y-.4+j*.2, face+side*.10), (width*.28, .075, .15), p['steel'], .008)
        for zz in [z-depth*.28, z+depth*.28]:
            box(name+'_roof_ridge', group, (x, height+.39, zz), (width-.8, .08, .055), p['steel'], .014)
        return group

    def vessel(name, position, radius=1.25, height=4.3):
        x, y, z = position
        group = empty(name, root)
        cylinder(name+'_base', group, (x, y+.18, z), radius+.35, .36, concrete, 12, bevel=.08)
        cylinder(name+'_jacket', group, (x, y+height/2+.35, z), radius, height, roof, 20, bevel=.10)
        cylinder(name+'_shoulder', group, (x, y+height+.49, z), radius, .28, p['steel'], 20, bevel=.08, radius2=radius*.73)
        cylinder(name+'_cap', group, (x, y+height+.7, z), radius*.73, .16, roof, 16, bevel=.06)
        for yy in [y+.65, y+height*.53, y+height+.16]:
            ring(name+'_band', group, (x, yy, z), radius+.035, .065, p['copper'], major_segments=20)
        box(name+'_label_recess', group, (x, y+height*.64, z+radius+.025), (.66, .87, .06), p['dark'], .025)
        box(name+'_level_gauge', group, (x, y+height*.64, z+radius+.067), (.07, .62, .035), heat_low, .008)
        return group

    # A grounded campus surrounds the precisely authored playfield.
    box('Foundry_apron', root, (0, -1.04, -4), (184, 1.95, 176), concrete, .15)
    for x in range(-80, 81, 10):
        box('Apron_expansion_joint', root, (x, -.058, -4), (.026, .006, 160), p['dark'], 0)
    for z in range(-74, 77, 10):
        box('Apron_expansion_joint', root, (0, -.058, z), (164, .006, .026), p['dark'], 0)
    for z in [-31.7, 28.0]:
        box('Field_drain', root, (0, -.035, z), (65, .04, .38), p['black'], .008)
        for x in range(-32, 33):
            box('Drain_grille', root, (x, -.008, z), (.075, .04, .34), p['deck'], .004)
    # The forge hall is the principal industrial landmark, offset from the route.
    building('SmelterHall', (-15, -32), 23, 14, 10.4)
    building('SmelterClerestory', (-18, -34.5), 13.5, 7.5, 13.7, False)
    for x in [-24.5,-18,-11.5,-5]:
        box('Hall_front_column',root,(x,5.28,-24.88),(.33,10.48,.32),p['steel'],.045)
    box('Hall_header_beam',root,(-15,9.98,-24.84),(22.8,.35,.39),p['steel'],.055)
    for x in [-24.5,-18,-11.5]:
        beam('Hall_diagonal_brace',root,(x,7.2,-24.63),(x+6.5,9.76,-24.63),.10,p['steel'],8)
    for z in [-28.15,-30.55]:
        box('Smelter_roof_monitor',root,(-15,10.79,z),(21.9,.64,.81),roof,.065)
        for x in [-24,-21,-18,-15,-12,-9,-6]:
            box('Monitor_louver_recess',root,(x,11.13,z),(2.22,.052,.61),p['black'],.01)
            for dx in [-.75,-.25,.25,.75]:
                box('Monitor_louver',root,(x+dx,11.19,z),(.10,.12,.54),p['steel'],.014)
    for x in [-26.65,-3.35]:
        for z in [-37,-32,-27]:
            box('Smelter_external_rib',root,(x,5.4,z),(.30,9.9,.27),p['steel'],.035)
    for z in [-36.3,-35.2]:
        beam('Roof_access_ladder',root,(-26.97,.2,z),(-26.97,11.4,z),.045,p['steel'])
    for step in range(33):
        beam('Ladder_rung',root,(-26.97,.32+step*.34,-36.3),(-26.97,.32+step*.34,-35.2),.031,p['edge'])
    for x in [-23.5, -20, -16.5, -13]:
        box('Clerestory_window_recess', root, (x, 12.05, -30.70), (2.47, 1.03, .12), p['black'], .018)
        box('Clerestory_warm_window', root, (x, 12.05, -30.61), (2.14, .51, .027), heat_low, .012)
        for y in [11.93, 12.17]:
            box('Window_guard', root, (x, y, -30.55), (2.45, .085, .15), p['steel'], .012)
    for index, (x, z, scale) in enumerate([(-21,-23,1),(-12,-23,1),(19,-23,.82)]):
        part(a.furnace, 'PerimeterFurnace_%02d'%index, (x,-.1,z), scale=(scale,scale,scale))
    for index, (x, z, scale) in enumerate([(-24,-34,1.8),(-19,-35,1.55),(-3,-25,1),(24,-29,1.05)]):
        part(a.stack, 'PerimeterStack_%02d'%index, (x,13.98 if index<2 else -.1,z), scale=(1,scale,1))
    building('PumpControl', (13,-31), 17, 8, 5.1)
    building('PumpCoolingShed', (26,-35), 8, 12, 7.8)
    for x,z,height in [(31,-24,4.2),(35,-24,5.0),(33,-30,3.6)]:
        vessel('CoolingVessel', (x,0,z), 1.28,height)
    for index,x in enumerate([-24,-16,-8,0,8,16,24]):
        part(a.catwalk,'ServiceCatwalk_%02d'%index,(x,0,-20))
    for x in [-21,-12,19]:
        pipe('Furnace_feed', [(x,1.2,-21),(x,4.7,-21),(x,4.7,-20.9)], .13)
    # Access stairs connect both ends of the maintenance gallery to the apron.
    for x,direction in [(-29,-1),(29,1)]:
        for step in range(16):
            xx=x+direction*step*.28
            yy=3.19-step*.20
            box('Gallery_stair',root,(xx,yy,-20),( .32,.12,1.20),p['deck'],.013)
        for z in [-20.63,-19.37]:
            handrails('Stair_rail',(x,3.23,z),(x+direction*4.2,.23,z))
        for z in [-20.45,-19.55]:
            beam('Stair_stringer',root,(x,3.05,z),(x+direction*4.45,.05,z),.09,p['steel'])
    # The receiving yard establishes a distinct cool destination for the route.
    box('Receiving_yard',root,(31,.008,14.1),(14,.14,14),roof,.08)
    part(a.defended_core,'DefendedCore',(28,.075,14.1))
    for name,x,z in [('EnemyArrival',-25.6,-12.8),('GoalGate',24.7,14.1)]:
        part(a.gantry,name,(x,0,z),math.pi/2,(.6,.75,.8))
    building('CoreControl', (37,14.1), 6,8,4.2)
    for z in [8.3,19.9]:
        box('Containment_buttress',root,(30,.71,z),(7.2,1.3,.55),p['steel'],.10)
        box('Receiving_cool_band',root,(30,1.02,z-.29 if z<14 else z+.29),(5.9,.075,.05),a.energy('Receiving','#43DED8'),.013)
    # Lower secondary service masses keep the south and west sightlines open.
    building('SouthUtility',(-10,34),14,7,4.1)
    building('WestSubstation',(-39,4),8,14,5.7)
    for index,(x,z,height) in enumerate([(-40,19,3.4),(-35,19,4.3),(9,33,3.7),(14,33,3.7)]):
        vessel('UtilityTank_%d'%index,(x,0,z),1.5,height)
    for index,(x,z,angle) in enumerate([(-12,22,0),(0,22,0),(12,22,0),(-29,5,math.pi/2)]):
        rack=part(a.pipe_rack,'PerimeterPipeRack_%02d'%index,(x,0,z),angle)
        for obj in list(rack.children_recursive):
            if obj.name.startswith('Transfer_pipe'):
                bpy.data.objects.remove(obj,do_unlink=True)
    for i,z in enumerate([21.43,22,22.57]):
        left_x,right_x,return_z=-20-i*.6,16+i*.6,29+i*.6
        tank_x,tank_y=[(14,1.0),(9,1.2),(14,2.3)][i]
        pipe('South_circuit',[(-16.8,2.1+i*.3,31),(left_x,2.1+i*.3,31),(left_x,4.47,31),
             (left_x,4.47,z),(right_x,4.47,z),(right_x,4.47,return_z),(tank_x,4.47,return_z),
             (tank_x,tank_y,return_z),(tank_x,tank_y,31.7)],.18,return_pipe if i==1 else p['copper'])
    for i,(x,low_z,high_z) in enumerate([(-29.57,.4,9.6),(-29,0,10),(-28.43,-.4,10.4)]):
        pipe('West_utility_circuit',[(-35.1,2.1,low_z),(-35.1,4.47,low_z),(x,4.47,low_z),
             (x,4.47,high_z),(-35.1,4.47,high_z),(-35.1,2.1,high_z)],.18,return_pipe if i==0 else p['copper'])
    for index,x in enumerate([-20,-14,-8,-2,4,10,16,22]):
        part(a.perimeter_wall,'SouthWall_%02d'%index,(x,0,25))
    for index,(x,z) in enumerate([(-25,13),(25,-17),(8,20)]):
        part(a.cabinet,'Cabinet_%02d'%index,(x,0,z))
    # Heat exchangers and flush utility conduits follow the original map landmarks.
    for index,(x,z,sx,sz) in enumerate([(820,424,2,2.1),(411,366,1.03,2),(97,390,1.05,1.17),(555,671,2,.9)]):
        part(a.exchanger,'HeatExchanger_%02d'%index,((x-480)*.05,0,(z-360)*.05),scale=(sx,1,sz))
    runs=[((335,310),(335,452)),((680,150),(680,419)),((455,300),(455,675)),((153,285),(340,285)),((416,148),(588,148)),((711,341),(913,341))]
    for index,(start,end) in enumerate(runs):
        start,end=[Vector(((point[0]-480)*.05,.049,(point[1]-360)*.05)) for point in [start,end]]
        direction=(end-start).normalized()
        side=Vector((-direction.z,0,direction.x))
        for offset in [-.075,.075]:
            beam('Surface_conduit_%02d'%index,root,start+side*offset,end+side*offset,.025,p['copper'])
        steps=max(1,int((end-start).length/.9))
        for j in range(steps+1):
            box('Conduit_clamp',root,start.lerp(end,j/steps),(.28,.04,.08) if abs(direction.z)>.5 else (.08,.04,.28),p['steel'],.009)
        for point in [start,end]:
            box('Junction_box',root,(point.x,.065,point.z),(.30,.13,.31),p['dark'],.023)
            box('Junction_status',root,(point.x,.139,point.z),(.10,.02,.04),heat_low,.005)
    # Distant, closed industrial masses provide depth from every camera direction.
    skyline=[(-58,-48,18,14,15),(-34,-65,26,13,13),(-2,-60,19,15,21),(26,-62,20,12,11),(55,-47,17,17,18),
             (64,-10,15,25,11),(61,27,16,15,16),(36,56,23,12,9),(2,63,27,13,13),(-34,56,19,15,10),(-61,29,13,23,13),(-65,-9,17,19,9)]
    for index,(x,z,w,d,h) in enumerate(skyline):
        if index in (2,5,8):
            continue
        building('DistantWorks_%02d'%index,(x,z),w,d,h,False)
        for dx in [-w*.26,w*.26]:
            box('Distant_roofplant',root,(x+dx,h+1.1,z),(w*.25,2.0,d*.45),roof,.12)
        for face in [-1,1]:
            for row in [h*.45,h*.73]:
                box('Distant_window_recess',root,(x,row,z+face*(d/2+.015)),(w*.72,.44,.07),p['black'],.012)
                for j in range(3):
                    xx=x+(j-1)*w*.24
                    box('Distant_window',root,(xx,row,z+face*(d/2+.057)),(w*.12,.22,.022),heat_low if index%3 else work_light,.006)
    for x,z,h in [(-51,-55,26),(-47,-56,22),(44,-56,25),(50,-58,19),(-54,45,23),(48,43,26)]:
        cylinder('Distant_flue',root,(x,h/2,z),.74,h,wall,12,bevel=.05)
        cylinder('Distant_flue_cap',root,(x,h,z),.94,.5,p['steel'],12,bevel=.04)
        box('Aircraft_marker',root,(x,h+.34,z),(.13,.14,.13),heat_low,.016)
    rebuild_service_networks(a,root)
    from campus_geometry import build as build_campus
    build_campus(a,root)
    return root


def rebuild_service_networks(a, root):
    """Rebuild the service circuits while retaining the surrounding editable kit."""
    if isinstance(a,dict):
        a=SimpleNamespace(**a)
    p,box=a.P,a.box
    prefixes=('Main_supply','Main_return','Vessel_feed','Vessel_manifold',
              'Hall_vertical_service','Hall_service_clamp','Core_cooling_feed','Arrival_lane','Arrival_dock')
    for obj in list(root.children_recursive):
        if obj.type=='MESH' and any(obj.name==prefix or obj.name.startswith((prefix+'.',prefix+'_')) for prefix in prefixes):
            bpy.data.objects.remove(obj,do_unlink=True)
    cool=p.get('cool_pipe',p['steel'])
    for i,(height,z) in enumerate([(4.7,-20.9),(5.25,-21.4),(6.05,-22.3)]):
        pipe_geometry(a,root,'Main_return' if i==1 else 'Main_supply',
            [(-26.3,2,-25.4),(-27,2,-25.4),(-27,height,-25.4),(-27,height,z),
             (27,height,z),(27,height,-29.1),(27,2,-29.1)],.14 if i<2 else .11,cool if i==1 else p['copper'])
    for x,z in [(31,-24),(35,-24),(33,-30)]:
        pipe_geometry(a,root,'Vessel_feed',[(x,.9,z+1.2),(x,.9,-20.8)],.16,cool)
    pipe_geometry(a,root,'Vessel_manifold',[(27,4.7,-20.9),(27,.9,-20.8),(35,.9,-20.8)],.16,cool)
    for x,inside in [(-26.65,-26.3),(-3.35,-3.7)]:
        pipe_geometry(a,root,'Hall_vertical_service',[(inside,1.1,-36),(x,1.1,-36),(x,9.6,-36),
            (x,9.6,-28),(x,1.1,-28),(inside,1.1,-28)],.115,cool)
        for z in [-36,-28]:
            for y in [2,5,8]:
                box('Hall_service_clamp',root,((x+inside)*.5,y,z),(.35,.12,.27),p['steel'],.017)
    for sign,z in [(-1,10.8),(1,17.4)]:
        pipe_geometry(a,root,'Core_cooling_feed',[(28,.3,14.1+sign*1.4),(28,.3,z),
             (33,.3,z),(33,1.4,z),(34.2,1.4,z)],.13,cool)
    box('Arrival_lane',root,(-41.05,-.008,-12.8),(31.1,.075,2.76),p.get('roof',p['deck']),.015)
    for z in [-14.18,-11.42]:
        box('Arrival_lane_edge',root,(-41.05,.034,z),(31.1,.016,.055),p['copper'],.005)
    box('Arrival_dock_recess',root,(-56.46,1.65,-12.8),(.09,3.30,3.40),p['black'],.018)
    for z in [-14.55,-11.05]:
        box('Arrival_dock_pier',root,(-56.33,1.75,z),(.32,3.50,.26),p['steel'],.037)
    box('Arrival_dock_header',root,(-56.32,3.56,-12.8),(.36,.27,3.82),p['steel'],.034)
    box('Arrival_dock_signal',root,(-56.10,3.45,-12.8),(.025,.055,2.42),p.get('heat_low',p['heat']),.005)
