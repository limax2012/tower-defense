"""Check asset roster, exported FBX dimensions, mesh closure, UVs and sockets."""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'art/bastion-3d'
UNITY=ROOT/'unity/MaximalBastion3D/Assets/Art'
catalog=json.loads((ART/'catalog.json').read_text())
results=[]
errors=[]
for item in catalog['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(UNITY/item['fbx']))
    objects=list(bpy.context.scene.objects)
    meshes=[o for o in objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    low=[min(p[k] for p in points) for k in range(3)]
    high=[max(p[k] for p in points) for k in range(3)]
    # Unity changes FBX handedness by reflecting X when importing this axis basis.
    bounds={'min':[-high[0],low[2],-high[1]],'max':[-low[0],high[2],-low[1]]}
    error=max(abs(bounds[key][i]-item['bounds'][key][i]) for key in ['min','max'] for i in range(3))
    open_edges=0
    degenerate=0
    no_uv=[]
    for obj in meshes:
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        open_edges+=sum(not edge.is_manifold for edge in bm.edges)
        degenerate+=sum(face.calc_area()<1e-10 for face in bm.faces)
        bm.free()
        if not obj.data.uv_layers:
            no_uv.append(obj.name)
    names={o.name for o in objects}
    required=['Yaw','Pitch','Recoil','Muzzle'] if item['category']=='Towers' else []
    missing=[name for name in required if name not in names]
    # The socket is carried by the exported mechanical hierarchy, not a baked offset.
    socket_error=0
    axis_error=0
    for joint in item['hierarchy']:
        if joint['name'].startswith(('Muzzle','HitPoint','RoleSocket','RankSocket','OutputSocket','EffectOrigin','CoreLight','Heat_','Exhaust','Leg_')):
            obj=next((o for o in objects if o.name==joint['name']),None)
            if not obj:
                missing.append(joint['name'])
                continue
            pos=obj.matrix_world.translation
            measured=[-pos.x,pos.z,-pos.y]
            socket_error=max(socket_error,max(abs(measured[i]-joint['worldPosition'][i]) for i in range(3)))
            if 'worldForward' in joint:
                forward=obj.matrix_world.to_3x3()@Vector((0,0,1))
                measured_forward=[-forward.x,forward.z,-forward.y]
                axis_error=max(axis_error,max(abs(measured_forward[i]-joint['worldForward'][i]) for i in range(3)))
    record={'id':item['id'],'exportedMeshes':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
            'boundsError':error,'socketError':socket_error,'axisError':axis_error,'nonManifoldEdges':open_edges,'degenerateFaces':degenerate,'missingUv':no_uv,'missingNodes':missing}
    results.append(record)
    if error>.003 or socket_error>.003 or axis_error>.003 or open_edges or degenerate or no_uv or missing:
        errors.append(record)
    print('VALIDATED',json.dumps(record),flush=True)

content=ROOT/'src/MaximalBastion/ContentData'
required_ids={d['id'] for name in ['Towers.json','Enemies.json'] for d in json.loads((content/name).read_text(encoding='utf-8-sig'))}
required_ids.update(d['id'] for d in json.loads((content/'Tactics.json').read_text(encoding='utf-8-sig')).values())
missing_ids=sorted(required_ids-{d['id'] for d in catalog['assets']})
report={'assetCount':len(results),'missingContentIds':missing_ids,'passed':not errors and not missing_ids,'assets':results}
out=ROOT/'.artifacts/3d-development/art-validation.json'
out.write_text(json.dumps(report,indent=2))
print('ART_VALIDATION',str(out),'PASSED' if report['passed'] else 'FAILED',flush=True)
if not report['passed']:
    raise RuntimeError('Asset validation failed; inspect '+str(out))
