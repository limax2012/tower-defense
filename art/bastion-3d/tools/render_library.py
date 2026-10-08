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
    from render_environment import render_environment
    render_environment(b)
