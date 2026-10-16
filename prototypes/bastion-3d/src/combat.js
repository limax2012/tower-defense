import * as THREE from 'three';
import { createTower, createEnemy, updateTowerVisual, updateEnemyVisual } from './models.js';
import { Effects } from './effects.js';

const V=THREE.Vector3;
const SETTINGS={
  needle:{name:'Needle Turret',range:18,interval:.28,damage:11,turn:4.8},
  prism:{name:'Prism Beam',range:29,interval:.26,damage:8,turn:2.5},
  mortar:{name:'Siege Mortar',range:35,interval:3.0,damage:68,turn:2.0},
};

// Quadratic corner fillets retain the authored route while giving walkers a
// continuous tangent through bends; distance is measured along the rounded lane.
export class Route {
  constructor(points) {
    this.points=[points[0].clone()];
    for(let i=1;i<points.length-1;i++) {
      const before=points[i-1],p=points[i],after=points[i+1];
      const r=Math.min(.80,p.distanceTo(before)*.2,p.distanceTo(after)*.2);
      const a=p.clone().addScaledVector(before.clone().sub(p).normalize(),r);
      const b=p.clone().addScaledVector(after.clone().sub(p).normalize(),r);
      this.points.push(a);
      const curve=new THREE.QuadraticBezierCurve3(a,p,b);
      for(let j=1;j<=12;j++)this.points.push(curve.getPoint(j/12));
    }
    this.points.push(points.at(-1).clone());this.distances=[0];
    for(let i=1;i<this.points.length;i++)this.distances.push(this.distances.at(-1)+this.points[i].distanceTo(this.points[i-1]));
    this.length=this.distances.at(-1);
  }
  sample(distance) {
    const d=THREE.MathUtils.clamp(distance,0,this.length-.0001);
    let lo=1,hi=this.distances.length-1;
    while(lo<hi){const mid=(lo+hi)>>1;if(this.distances[mid]<d)lo=mid+1;else hi=mid;}
    const a=this.points[lo-1],b=this.points[lo];
    return {position:a.clone().lerp(b,(d-this.distances[lo-1])/(this.distances[lo]-this.distances[lo-1])),direction:b.clone().sub(a).normalize()};
  }
}
const angleDelta=(a,b)=>Math.atan2(Math.sin(b-a),Math.cos(b-a));
function disposeEnemy(enemy){enemy.model.group.removeFromParent();for(const material of Object.values(enemy.model.materials))material.dispose();}
function disposeProjectile(projectile){projectile.mesh.removeFromParent();projectile.mesh.traverse(o=>{if(o.isMesh)o.material.dispose();});}

export class Combat {
  constructor(scene,environment) {
    this.scene=scene;this.environment=environment;this.route=new Route(environment.path);
    this.effects=new Effects(scene);this.enemies=[];this.projectiles=[];this.towers=[];
    this.time=0;this.spawnClock=0;this.serial=0;this.kills=0;this.loop=1;
    this.events=[];this.telemetry={needleHits:0,prismHits:0,mortarHits:0,mortarMultiHits:0,defeats:0,turns:0,occluded:0};
    this.ray=new THREE.Ray();
    for(const [type,config] of Object.entries(SETTINGS)) {
      const model=createTower(type);model.group.position.copy(environment.towerSites[type]);scene.add(model.group);
      this.towers.push({type,config,model,cooldown:type==='mortar'?.8:0,firePulse:0,target:null,shots:0,
        beam:type==='prism'?this.effects.createBeam():null,impactClock:0});
    }
    this.reset();
  }
  reset() {
    for(const e of this.enemies)disposeEnemy(e);
    for(const p of this.projectiles)disposeProjectile(p);
    this.enemies=[];this.projectiles=[];this.effects.clear();this.time=0;this.spawnClock=.5;this.serial=0;this.kills=0;
    this.events=[];
    for(const key in this.telemetry)this.telemetry[key]=0;
    for(const t of this.towers){t.cooldown=t.type==='mortar'?.9:0;t.firePulse=0;t.shots=0;t.target=null;}
    // A staggered convoy presents all three weapons immediately on opening.
    [10,16,22,31,38,46,55,61,69].forEach((distance,i)=>this.spawn(i%3===1?'bulwark':'runner',distance));
  }
  spawn(type,distance=0) {
    const model=createEnemy(type);this.scene.add(model.group);
    const e={id:++this.serial,type,model,distance,speed:type==='runner'?3.75:2.05,
      hp:type==='runner'?78:340,maxHp:type==='runner'?78:340,hit:0,alive:true,expose:0,phase:this.serial*.713};
    const sample=this.route.sample(distance);model.group.position.copy(sample.position);model.group.rotation.y=Math.atan2(sample.direction.x,sample.direction.z);
    this.enemies.push(e);return e;
  }
  aimPoint(e,future=0) {
    const p=this.route.sample(e.distance+future*(e.effectiveSpeed??e.speed)).position;
    p.y+=e.model.hitHeight+.18;return p;
  }
  visible(from,to) {
    const delta=to.clone().sub(from),length=delta.length();this.ray.set(from,delta.normalize());
    const point=new V();
    for(const box of this.environment.colliders) {
      if(this.ray.intersectBox(box,point)&&point.distanceTo(from)<length-.15){this.telemetry.occluded++;return false;}
    }
    return true;
  }
  chooseTarget(tower) {
    const origin=tower.model.yaw.getWorldPosition(new V());origin.y+=.7;
    const candidates=this.enemies.filter(e=>e.alive&&e.model.group.position.distanceTo(tower.model.group.position)<tower.config.range);
    candidates.sort(tower.type==='prism'?(a,b)=>b.hp-a.hp:(a,b)=>b.distance-a.distance);
    return candidates.find(e=>tower.type==='mortar'||this.visible(origin,this.aimPoint(e)))||null;
  }
  damage(enemy,amount,type) {
    if(!enemy?.alive)return;
    enemy.hp-=amount*(enemy.expose>0&&type!=='prism'?1.1:1);enemy.hit=1;
    this.telemetry[`${type}Hits`]++;
    if(type==='prism')enemy.expose=2;
    if(enemy.hp<=0) {
      enemy.alive=false;this.kills++;this.telemetry.defeats++;
      this.effects.death(this.aimPoint(enemy),enemy.id);
      enemy.model.group.visible=false;
    }
  }
  fire(tower,start,targetPoint) {
    const type=tower.type;tower.firePulse=1;tower.shots++;
    this.effects.flash(start,type,type==='mortar'?1.5:1);
    this.events.push({time:this.time,type,target:tower.target.id});if(this.events.length>200)this.events.shift();
    if(type==='prism') {this.damage(tower.target,tower.config.damage,type);return;}
    const mesh=this.effects.createProjectile(type);mesh.position.copy(start);
    const duration=type==='mortar'?1.45:start.distanceTo(targetPoint)/38;
    const end=type==='mortar'?this.aimPoint(tower.target,duration):targetPoint.clone();
    if(type==='mortar')end.y=.38;
    const arcHeight=type==='mortar'?Math.max(5.2,start.distanceTo(end)*.28):0;
    this.projectiles.push({type,mesh,start:start.clone(),end,target:tower.target,age:0,duration,arcHeight,damage:tower.config.damage,trailClock:0});
  }
  update(dt) {
    this.time+=dt;this.spawnClock-=dt;
    if(this.spawnClock<=0&&this.enemies.filter(e=>e.alive).length<20&&!this.enemies.some(e=>e.alive&&e.distance<3.0)) {
      this.spawn(this.serial%4===1?'bulwark':'runner');this.spawnClock=this.serial%4===0?2.1:.85;
    }
    let leader=null;
    for(const e of this.enemies.filter(e=>e.alive).sort((a,b)=>b.distance-a.distance)) {
      if(!e.alive)continue;
      const previous=e.distance;
      e.distance+=e.speed*dt;
      if(leader)e.distance=Math.min(e.distance,leader.distance-(e.model.radius+leader.model.radius)*.90-.16);
      e.distance=Math.max(previous,e.distance);e.effectiveSpeed=(e.distance-previous)/dt;leader=e;
      e.hit=Math.max(0,e.hit-dt*5);e.expose=Math.max(0,e.expose-dt);
      if(e.distance>=this.route.length){e.alive=false;e.model.group.visible=false;continue;}
      const sample=this.route.sample(e.distance);e.model.group.position.copy(sample.position);
      const desired=Math.atan2(sample.direction.x,sample.direction.z);
      const delta=angleDelta(e.model.group.rotation.y,desired);
      e.model.group.rotation.y+=delta*(1-Math.exp(-dt*14));
      if(Math.abs(delta)>.05)this.telemetry.turns++;
      updateEnemyVisual(e.model,this.time+e.phase,e.effectiveSpeed,e.hit);
    }
    this.scene.updateMatrixWorld(true);
    for(const t of this.towers) {
      t.cooldown-=dt;t.firePulse=Math.max(0,t.firePulse-dt*6);t.impactClock-=dt;
      if(!t.target?.alive||t.target.model.group.position.distanceTo(t.model.group.position)>t.config.range||
        (t.type!=='mortar'&&!this.visible(t.model.muzzle.getWorldPosition(new V()),this.aimPoint(t.target))))t.target=this.chooseTarget(t);
      if(t.target) {
        const point=this.aimPoint(t.target,t.type==='mortar'?1.45:0);
        const pivot=t.model.pitch.getWorldPosition(new V());
        const dx=point.x-pivot.x,dz=point.z-pivot.z;
        const yaw=Math.atan2(dx,dz),delta=angleDelta(t.model.yaw.rotation.y,yaw);
        t.model.yaw.rotation.y+=THREE.MathUtils.clamp(delta,-t.config.turn*dt,t.config.turn*dt);
        const range=Math.hypot(dx,dz),arcHeight=Math.max(5.2,range*.28);
        const pitch=t.type==='mortar'?-Math.atan2(.38-pivot.y+4*arcHeight,range):-Math.atan2(point.y-pivot.y,range);
        t.model.pitch.rotation.x=THREE.MathUtils.damp(t.model.pitch.rotation.x,pitch,8,dt);
        updateTowerVisual(t.model,this.time,t.firePulse);this.scene.updateMatrixWorld(true);
        const muzzle=t.model.muzzle.getWorldPosition(new V());
        const unobstructed=t.type==='mortar'||this.visible(muzzle,point);
        const aligned=Math.abs(angleDelta(t.model.yaw.rotation.y,yaw))<.075;
        if(unobstructed&&aligned&&t.cooldown<=0) {this.fire(t,muzzle,point);t.cooldown=t.config.interval;updateTowerVisual(t.model,this.time,t.firePulse);this.scene.updateMatrixWorld(true);muzzle.copy(t.model.muzzle.getWorldPosition(new V()));}
        if(t.type==='prism') {
          const active=unobstructed&&aligned&&t.target.alive;
          this.effects.updateBeam(t.beam,muzzle,this.aimPoint(t.target),active?1:0);
          if(active&&t.impactClock<=0){this.effects.impact(this.aimPoint(t.target),'prism',false,t.target.id);t.impactClock=.16;}
        }
      } else if(t.beam)t.beam.group.visible=false;
      updateTowerVisual(t.model,this.time,t.firePulse);
    }
    for(let i=this.projectiles.length-1;i>=0;i--) {
      const p=this.projectiles[i];p.age+=dt;p.trailClock-=dt;
      const old=p.mesh.position.clone();
      if(p.type==='needle') {
        if(p.target.alive)p.end.copy(this.aimPoint(p.target));
        const delta=p.end.clone().sub(p.mesh.position),distance=delta.length();
        p.mesh.position.addScaledVector(delta.normalize(),Math.min(distance,38*dt));
        if(distance<38*dt+.05)p.age=p.duration+1;
      } else {
        const u=Math.min(1,p.age/p.duration);p.mesh.position.copy(p.start).lerp(p.end,u);
        p.mesh.position.y+=4*p.arcHeight*u*(1-u);
        p.mesh.userData.hoop.rotation.z+=dt*8;
      }
      const motion=p.mesh.position.clone().sub(old);if(motion.lengthSq()>.00001)p.mesh.quaternion.setFromUnitVectors(new V(0,0,1),motion.normalize());
      if(p.type==='needle'&&!this.visible(old,p.mesh.position)){
        this.effects.impact(p.mesh.position,p.type,false,this.serial);disposeProjectile(p);this.projectiles.splice(i,1);continue;
      }
      if(p.trailClock<=0){this.effects.trail(p.mesh.position,p.type);p.trailClock=.033;}
      const arrived=p.type==='mortar'?p.age>=p.duration:p.mesh.position.distanceTo(p.end)<.08;
      if(arrived||p.age>4) {
        this.effects.impact(p.end,p.type,p.type==='mortar',this.serial);
        if(p.type==='needle')this.damage(p.target,p.damage,p.type);
        else {
          const hit=this.enemies.filter(e=>e.alive&&e.model.group.position.distanceTo(p.end)<3.5).sort((a,b)=>a.model.group.position.distanceTo(p.end)-b.model.group.position.distanceTo(p.end)).slice(0,6);
          if(hit.length>1)this.telemetry.mortarMultiHits++;
          for(const e of hit)this.damage(e,p.damage,p.type);
        }
        disposeProjectile(p);this.projectiles.splice(i,1);
      }
    }
    this.effects.update(dt);
    for(let i=this.enemies.length-1;i>=0;i--)if(!this.enemies[i].alive){disposeEnemy(this.enemies[i]);this.enemies.splice(i,1);}
  }
}
