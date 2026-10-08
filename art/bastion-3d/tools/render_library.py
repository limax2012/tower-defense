"""Render the editable library and assembled foundry in consistent review light."""
import argparse
import importlib.util
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

path=Path(__file__).with_name('build_assets.py')
spec=importlib.util.spec_from_file_location('bastion_assets',path)
b=importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
parser=argparse.ArgumentParser()
parser.add_argument('--ids',nargs='*')
parser.add_argument('--environment',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

def open_asset(path,id):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    b.MATERIALS={}
    return bpy.data.objects[id]

catalog=json.loads((b.ART/'catalog.json').read_text())
for item in catalog['assets']:
    id=item['id']
    if args.ids and id not in args.ids:
        continue
    if id=='cinderworks_dressing':
        continue
    root=open_asset(b.ART/item['blend'],id)
    b.studio(root,b.ART/'review'/(id+'-front.png'),width=640)
    b.studio(root,b.ART/'review'/(id+'-rear.png'),angle=math.pi,width=640)
    if id in ['prism_beam','needle_turret','breaker_cannon','siege_mortar','t2_runner','t4_aegis']:
        b.studio(root,b.ART/'review'/(id+'-low.png'),angle=-.20,width=640,elevation=.12)

if args.environment:
    root=open_asset(b.ART/'models/Environment/cinderworks_dressing.blend','cinderworks_dressing')
    b.P=b.palette()
    b.box('Assembly_foundation',root,(0,-.54,-2),(66,1.0,60),b.P['dark'],.10)
    b.box('Foundry_floor',root,(0,-.016,-2),(64,.035,58),b.P['rust'],.012)
    definition=json.loads((b.CONTENT/'Maps/FoundryLoop.json').read_text())
    points=[Vector(((p['x']-480)*.05,(p['y']-360)*.05)) for p in definition['path']]
    def outline(width):
        left=[]
        right=[]
        for i,p in enumerate(points):
            prev=(p-points[i-1]).normalized() if i else (points[1]-p).normalized()
            after=(points[i+1]-p).normalized() if i<len(points)-1 else prev
            n1=Vector((-prev.y,prev.x))
            n2=Vector((-after.y,after.x))
            normal=(n1+n2).normalized()
            offset=normal*(width/2/max(.1,normal.dot(n1)))
            left.append(tuple(p+offset))
            right.append(tuple(p-offset))
        return left+list(reversed(right))
    b.slab('Continuous_road_bed',root,outline(3.02),.035,.13,b.P['dark'],.015)
    b.slab('Copper_road_border',root,outline(2.86),.109,.020,b.P['copper'],.007)
    b.slab('Drive_surface',root,outline(2.72),.125,.020,b.mat('Foundry_Lane','#211B1A',.42,.58),.007)
    for index,r in enumerate(definition['buildableRegions']):
        x=(r['x']+r['width']/2-480)*.05
        z=(r['y']+r['height']/2-360)*.05
        w,d=r['width']*.05,r['height']*.05
        b.box('Build_deck_%d'%index,root,(x,.034,z),(w,.069,d),b.P['deck'],.018)
        for xx in [-w/2+.04,w/2-.04]:
            b.box('Deck_edge',root,(x+xx,.08,z),(.047,.025,d-.06),b.P['edge'],.005)
        for zz in [-d/2+.04,d/2-.04]:
            b.box('Deck_edge',root,(x,.08,z+zz),(w-.06,.025,.047),b.P['edge'],.005)
        for j in range(1,int(d/.60)):
            b.box('Deck_seam',root,(x,.078,z-d/2+j*.60),(w-.09,.012,.017),b.P['dark'],.002)
    b.studio(root,b.ART/'review/cinderworks-assembled-front.png',width=1440,elevation=1.1)
    b.studio(root,b.ART/'review/cinderworks-assembled-rear.png',width=1440,angle=math.pi,elevation=.85)
    bpy.ops.wm.save_as_mainfile(filepath=str(b.ART/'models/Environment/cinderworks-review-assembly.blend'))
