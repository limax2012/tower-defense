import * as THREE from 'three';

const UP = new THREE.Vector3(0, 1, 0);
const sphereGeometry = new THREE.SphereGeometry(1, 24, 16);
const tubeGeometry = new THREE.CylinderGeometry(1, 1, 1, 24, 1, false);
const sparkGeometry = new THREE.OctahedronGeometry(1);
const mortarHoopGeometry = new THREE.TorusGeometry(.23,.022,6,20);
const colors = { needle: 0x48e6ff, prism: 0xac80ff, mortar: 0xd3f446, hostile: 0xff3483 };
function glow(color,opacity=1){
  if(opacity===1)return new THREE.MeshBasicMaterial({color,toneMapped:false});
  const material=new THREE.ShaderMaterial({transparent:true,depthWrite:false,blending:THREE.AdditiveBlending,toneMapped:false,
    uniforms:{tint:{value:new THREE.Color(color)},alpha:{value:opacity}},
    vertexShader:`varying vec3 vNormal;varying vec3 vView;void main(){vec4 p=modelViewMatrix*vec4(position,1.0);vNormal=normalize(normalMatrix*normal);vView=-p.xyz;gl_Position=projectionMatrix*p;}`,
    fragmentShader:`uniform vec3 tint;uniform float alpha;varying vec3 vNormal;varying vec3 vView;void main(){float rim=pow(max(0.0,dot(normalize(vNormal),normalize(vView))),1.6);gl_FragColor=vec4(tint,alpha*rim);}`});
  Object.defineProperty(material,'opacity',{get(){return this.uniforms.alpha.value;},set(value){this.uniforms.alpha.value=value;},configurable:true});
  return material;
}
function disposeEffect(mesh){mesh.removeFromParent();mesh.material.dispose();if(![sphereGeometry,tubeGeometry,sparkGeometry].includes(mesh.geometry))mesh.geometry.dispose();}

export class Effects {
  constructor(scene) {
    this.group = new THREE.Group(); this.group.name = 'Volumetric combat effects'; scene.add(this.group);
    this.items = []; this.beams = [];
  }
  mesh(geometry, color, opacity) {
    const mesh = new THREE.Mesh(geometry, glow(color, opacity)); this.group.add(mesh); return mesh;
  }
  flash(position, type = 'needle', scale = 1) {
    const core = this.mesh(sphereGeometry, 0xf5ecff, .95);
    core.position.copy(position); core.scale.setScalar(.18 * scale);
    const halo = this.mesh(sphereGeometry, colors[type], .20);
    halo.position.copy(position); halo.scale.setScalar(.38 * scale);
    this.items.push({ kind:'flash', meshes:[core,halo], age:0, life:.13, scale });
  }
  impact(position, type = 'needle', large = false, seed = 1) {
    const color = colors[type] || colors.hostile;
    const ball = this.mesh(sphereGeometry, color, .65); ball.position.copy(position);
    const shell = this.mesh(sphereGeometry, color, .13); shell.position.copy(position);
    const meshes = [ball,shell];
    const ring = this.mesh(new THREE.TorusGeometry(1, .035, 6, 48), color, .66);
    ring.position.copy(position); ring.position.y = .23; ring.rotation.x = -Math.PI / 2;
    ring.visible = large; meshes.push(ring);
    const count = large ? 18 : 7, velocities=[];
    for (let i=0;i<count;i++) {
      const a=i*2.399963+seed*.37, z=1-2*(i+.5)/count, r=Math.sqrt(1-z*z);
      const direction=new THREE.Vector3(Math.cos(a)*r,z*.6+.45,Math.sin(a)*r).normalize();
      velocities.push(direction.multiplyScalar(large?3.8:2.4));
      const spark=this.mesh(sparkGeometry,i%3===0?0xffefc1:color,.85);
      spark.position.copy(position); spark.scale.set(.036,.036,large?.19:.12); meshes.push(spark);
    }
    this.items.push({kind:'impact',meshes,velocities,position:position.clone(),age:0,life:large?.74:.28,large});
  }
  death(position, seed) {
    this.impact(position,'hostile',true,seed);
    const shards=[];
    for(let i=0;i<9;i++) {
      const mesh=new THREE.Mesh(new THREE.BoxGeometry(.20,.15,.30),
        new THREE.MeshStandardMaterial({color:i%3===0?0x9eafbd:0x26394a,metalness:.6,roughness:.4,transparent:true}));
      mesh.position.copy(position); mesh.castShadow=true; this.group.add(mesh);
      const a=i*2.39996+seed; shards.push({mesh,velocity:new THREE.Vector3(Math.cos(a)*2.5,1.8+(i%3)*.6,Math.sin(a)*2.5)});
    }
    this.items.push({kind:'debris',meshes:shards.map(s=>s.mesh),shards,age:0,life:1.05});
  }
  createProjectile(type) {
    const group=new THREE.Group(); group.name=`${type} energy projectile`;
    const color=colors[type];
    const core=new THREE.Mesh(sphereGeometry,glow(type==='needle'?0xd1fcff:0xeeffad));
    core.scale.setScalar(type==='needle'?.085:.18); group.add(core);
    const halo=new THREE.Mesh(sphereGeometry,glow(color,.22)); halo.scale.setScalar(type==='needle'?.18:.32); group.add(halo);
    if(type==='needle') {
      core.scale.z=.38; halo.scale.z=.53;
    } else {
      const hoop=new THREE.Mesh(mortarHoopGeometry,glow(color,.85));
      hoop.rotation.x=.5; group.add(hoop); group.userData.hoop=hoop;
    }
    this.group.add(group);
    return group;
  }
  trail(position,type) {
    const ball=this.mesh(sphereGeometry,colors[type],type==='needle'?.18:.27);
    ball.position.copy(position); ball.scale.setScalar(type==='needle'?.085:.12);
    this.items.push({kind:'trail',meshes:[ball],age:0,life:type==='needle'?.11:.3});
  }
  createBeam() {
    const group=new THREE.Group(); group.name='Prism continuous volume'; this.group.add(group);
    const layers=[];
    for(const [radius,color,opacity] of [[.23,0x9663ff,.075],[.14,0xa779ff,.16],[.072,0xb38cff,.88],[.027,0xf5dcff,1]]) {
      const material=glow(color,opacity);
      const body=new THREE.Mesh(tubeGeometry,material);
      const a=new THREE.Mesh(sphereGeometry,material),b=new THREE.Mesh(sphereGeometry,material);
      group.add(body,a,b); layers.push({body,a,b,radius});
    }
    const beam={group,layers}; this.beams.push(beam); return beam;
  }
  updateBeam(beam, start, end, strength=1) {
    const delta=end.clone().sub(start),len=delta.length();
    beam.group.visible=strength>0 && len>.01;
    const q=new THREE.Quaternion().setFromUnitVectors(UP,delta.clone().normalize());
    for(const layer of beam.layers) {
      layer.body.position.copy(start).addScaledVector(delta,.5); layer.body.quaternion.copy(q);
      layer.body.scale.set(layer.radius,len,layer.radius);
      layer.a.position.copy(start); layer.b.position.copy(end);
      layer.a.scale.setScalar(layer.radius); layer.b.scale.setScalar(layer.radius);
    }
  }
  update(dt) {
    for(let i=this.items.length-1;i>=0;i--) {
      const f=this.items[i]; f.age+=dt; const p=f.age/f.life;
      if(p>=1) { for(const mesh of f.meshes)disposeEffect(mesh); this.items.splice(i,1); continue; }
      if(f.kind==='flash') {
        f.meshes[0].scale.setScalar((.08+.14*(1-p))*f.scale);
        f.meshes[1].scale.setScalar((.30+.15*p)*f.scale);
        f.meshes[0].material.opacity=(1-p)*.95; f.meshes[1].material.opacity=(1-p)*.20;
      } else if(f.kind==='impact') {
        const size=f.large?3.3:.50;
        f.meshes[0].scale.setScalar((.08+Math.sin(p*Math.PI)*size*.24));
        f.meshes[0].material.opacity=(1-p)*.62;
        f.meshes[1].scale.setScalar(.12+p*size);f.meshes[1].material.opacity=(1-p)*.13;
        f.meshes[2].scale.setScalar(.16+p*size);f.meshes[2].material.opacity=(1-p)*.6;
        for(let n=3;n<f.meshes.length;n++) {
          const mesh=f.meshes[n],v=f.velocities[n-3];
          mesh.position.copy(f.position).addScaledVector(v,f.age);
          mesh.position.y-=f.age*f.age*2;
          mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0,0,1),v.clone().normalize());
          mesh.material.opacity=1-p;
        }
      } else if(f.kind==='trail') {
        f.meshes[0].material.opacity=(1-p)*.18;f.meshes[0].scale.multiplyScalar(Math.exp(-dt*3));
      } else if(f.kind==='debris') {
        for(const shard of f.shards) {
          shard.velocity.y-=dt*8;shard.mesh.position.addScaledVector(shard.velocity,dt);
          if(shard.mesh.position.y<.23){shard.mesh.position.y=.23;shard.velocity.multiplyScalar(.6);shard.velocity.y=Math.abs(shard.velocity.y)*.2;}
          shard.mesh.rotation.x+=dt*3;shard.mesh.rotation.z+=dt*2;shard.mesh.material.opacity=1-p*p;
        }
      }
    }
  }
  clear() {
    for(const f of this.items)for(const mesh of f.meshes)disposeEffect(mesh);
    this.items=[]; for(const beam of this.beams)beam.group.visible=false;
  }
}
