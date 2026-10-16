import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

const FLOOR = .5;
const RADIUS = .27;
const EPSILON = .012;
const UP = new THREE.Vector3(0,1,0);
const vector = value => value?.isVector3 ? value.clone() : new THREE.Vector3(...value);
const interactive = element => !!element?.closest?.('input,textarea,select,button,[contenteditable="true"],[role="slider"]');

function sweepBox(origin, delta, box) {
  let enter=0,exit=1,axis=-1,sign=0;
  for(let a=0;a<3;a++) {
    const p=origin.getComponent(a),d=delta.getComponent(a);
    const min=box.min.getComponent(a),max=box.max.getComponent(a);
    if(Math.abs(d)<1e-9) { if(p<min||p>max)return null; continue; }
    let near=(min-p)/d,far=(max-p)/d,normal=-Math.sign(d);
    if(near>far)[near,far]=[far,near];
    if(near>enter) { enter=near;axis=a;sign=normal; }
    exit=Math.min(exit,far);
    if(enter>exit)return null;
  }
  if(axis<0||enter<0||enter>1)return null;
  const normal=new THREE.Vector3();normal.setComponent(axis,sign);
  return {time:enter,normal};
}

/** Perspective inspection camera with continuous swept movement and surface sliding. */
export class CameraRig {
  constructor(camera,canvas,environment,towers=[]) {
    this.camera=camera;this.canvas=canvas;this.environment=environment;this.towers=towers;
    this.mode='orbit';this._tour=false;this._tourTime=0;this._keys=new Set();this._drag=null;
    this._velocity=new THREE.Vector3();this._dolly=0;this._yaw=0;this._pitch=0;this._focusDistance=14;
    this._listeners=[];this._normalMatrix=new THREE.Matrix3();this._raycaster=new THREE.Raycaster();
    this._colliders=(environment.colliders??[]).map(box=>box.clone().expandByScalar(RADIUS));
    this._towerMeshes=[];this._refreshTowerMeshes();
    this._controls=new OrbitControls(camera,canvas);
    this._controls.enableDamping=true;this._controls.dampingFactor=.085;
    this._controls.rotateSpeed=.6;this._controls.zoomSpeed=.85;this._controls.panSpeed=.8;
    this._controls.screenSpacePanning=true;
    this._controls.minDistance=.65;this._controls.maxDistance=125;
    this._controls.minPolarAngle=.02;this._controls.maxPolarAngle=Math.PI-.02;
    this._controls.mouseButtons={LEFT:THREE.MOUSE.ROTATE,MIDDLE:THREE.MOUSE.DOLLY,RIGHT:THREE.MOUSE.PAN};
    this._controls.touches={ONE:THREE.TOUCH.ROTATE,TWO:THREE.TOUCH.DOLLY_PAN};
    this._controls.target.set(-6,1,-4);
    this._tourCurve=new THREE.CatmullRomCurve3([
      new THREE.Vector3(-31,19,28),new THREE.Vector3(-19,4,5),
      new THREE.Vector3(-17,2.8,-3),new THREE.Vector3(-8,3.4,-12),
      new THREE.Vector3(9,8,-13),new THREE.Vector3(4,10,14),
      new THREE.Vector3(12,10,24),new THREE.Vector3(28,20,16),
      new THREE.Vector3(6,34,27),new THREE.Vector3(-17,19,26),
    ],true,'centripetal');
    this._tourTargets=new THREE.CatmullRomCurve3([
      new THREE.Vector3(-2,1,0),new THREE.Vector3(-10,1.5,-3),
      new THREE.Vector3(-9,1.8,-7),new THREE.Vector3(.6,1.8,-8),
      new THREE.Vector3(10,3,2),new THREE.Vector3(10,3,5),
      new THREE.Vector3(7.5,3,3),new THREE.Vector3(7,2,3),
      new THREE.Vector3(-2,1,0),new THREE.Vector3(-6,1,-3),
    ],true,'centripetal');
    canvas.tabIndex=canvas.tabIndex<0?0:canvas.tabIndex;
    this._listen(canvas,'pointerdown',event=>this._pointerDown(event));
    this._listen(canvas,'pointermove',event=>this._pointerMove(event));
    this._listen(canvas,'pointerup',event=>this._pointerUp(event));
    this._listen(canvas,'pointercancel',event=>this._pointerUp(event));
    this._listen(canvas,'wheel',event=> {
      if(this.mode!=='free'||this._tour||!document.hasFocus())return;
      event.preventDefault();this._dolly+=THREE.MathUtils.clamp(-event.deltaY*.009,-5,5);
    },{passive:false});
    this._listen(canvas,'contextmenu',event=>event.preventDefault());
    this._listen(window,'keydown',event=> {
      if(!document.hasFocus()||interactive(event.target)||interactive(document.activeElement))return;
      if(['KeyW','KeyA','KeyS','KeyD','KeyQ','KeyE','ShiftLeft','ShiftRight'].includes(event.code)) {
        this._keys.add(event.code);
        if(this.mode==='free')event.preventDefault();
      }
    });
    this._listen(window,'keyup',event=>this._keys.delete(event.code));
    this._listen(window,'blur',()=>this._clearInput());
    this._listen(document,'visibilitychange',()=>{if(document.hidden)this._clearInput();});
    this.setView('overview');
  }

  _listen(target,name,fn,options) {target.addEventListener(name,fn,options);this._listeners.push([target,name,fn,options]);}
  get controls() {return this._controls;}
  get target() {return this._controls.target;}
  get tour() {return this._tour;}
  set tour(enabled) {
    this._tour=!!enabled;this._clearInput();
    this._controls.enabled=!this._tour&&this.mode==='orbit';
    if(!enabled)this._readOrientation();
  }
  _clearInput() {this._keys.clear();this._drag=null;this._velocity.set(0,0,0);this._dolly=0;}
  _refreshTowerMeshes() {
    this._towerMeshes=[];
    for(const tower of this.towers) {
      const root=tower?.group??tower?.model?.group??tower?.visual?.group??tower;
      root?.traverse?.(object=>{if(object.isMesh)this._towerMeshes.push(object);});
    }
  }
  _readOrientation() {
    const e=new THREE.Euler().setFromQuaternion(this.camera.quaternion,'YXZ');
    this._pitch=e.x;this._yaw=e.y;
    this._focusDistance=THREE.MathUtils.clamp(this.camera.position.distanceTo(this.target),2,50);
  }
  _pointerDown(event) {
    if(this._tour||interactive(event.target))return;
    this.canvas.focus({preventScroll:true});
    if(this.mode!=='free')return;
    if(event.button!==0&&event.button!==2)return;
    this._drag={id:event.pointerId,x:event.clientX,y:event.clientY};
    this.canvas.setPointerCapture?.(event.pointerId);event.preventDefault();
  }
  _pointerMove(event) {
    if(!this._drag||this.mode!=='free'||event.pointerId!==this._drag.id||!document.hasFocus())return;
    this._yaw-=(event.clientX-this._drag.x)*.0032;
    this._pitch=THREE.MathUtils.clamp(this._pitch-(event.clientY-this._drag.y)*.0032,-Math.PI/2+.018,Math.PI/2-.018);
    this._drag.x=event.clientX;this._drag.y=event.clientY;
  }
  _pointerUp(event) {
    if(this._drag?.id===event.pointerId) {
      this._drag=null;
      if(this.canvas.hasPointerCapture?.(event.pointerId))this.canvas.releasePointerCapture(event.pointerId);
    }
  }
  setMode(mode) {
    if(!['orbit','free'].includes(mode))throw new Error(`Unknown camera mode: ${mode}`);
    this._clearInput();this.mode=mode;this._readOrientation();
    this._controls.enabled=mode==='orbit'&&!this._tour;
    return this;
  }

  _outsideSolids(point) {
    const result=point.clone();result.y=Math.max(FLOOR,result.y);
    for(let pass=0;pass<4;pass++) {
      let changed=false;
      for(const box of this._colliders) {
        if(!box.containsPoint(result))continue;
        let distance=Infinity,axis=0,value=0;
        for(let a=0;a<3;a++)for(const side of [-1,1]) {
          const boundary=side<0?box.min.getComponent(a)-EPSILON:box.max.getComponent(a)+EPSILON;
          if(a===1&&boundary<FLOOR)continue;
          const d=Math.abs(result.getComponent(a)-boundary);
          if(d<distance){distance=d;axis=a;value=boundary;}
        }
        result.setComponent(axis,value);changed=true;
      }
      if(!changed)break;
    }
    return result;
  }

  _towerHit(origin,delta) {
    const length=delta.length();if(length<1e-6||!this._towerMeshes.length)return null;
    const direction=delta.clone().multiplyScalar(1/length);
    const offsets=[new THREE.Vector3(),new THREE.Vector3(RADIUS,0,0),new THREE.Vector3(-RADIUS,0,0),
      new THREE.Vector3(0,RADIUS,0),new THREE.Vector3(0,-RADIUS,0),new THREE.Vector3(0,0,RADIUS),new THREE.Vector3(0,0,-RADIUS)];
    let nearest=null;
    for(const offset of offsets) {
      this._raycaster.set(origin.clone().add(offset),direction);this._raycaster.near=0;this._raycaster.far=length+EPSILON;
      const hits=this._raycaster.intersectObjects(this._towerMeshes,false);
      for(const hit of hits) {
        if(!hit.face)continue;
        this._normalMatrix.getNormalMatrix(hit.object.matrixWorld);
        const normal=hit.face.normal.clone().applyMatrix3(this._normalMatrix).normalize();
        if(normal.dot(direction)>-.0001)continue;
        const time=Math.max(0,(hit.distance-EPSILON)/length);
        if(time<=1&&(!nearest||time<nearest.time))nearest={time,normal};
        break;
      }
    }
    return nearest;
  }

  _moveSafely(origin,destination) {
    const position=this._outsideSolids(origin),remaining=destination.clone().sub(position);
    for(let pass=0;pass<4&&remaining.lengthSq()>1e-10;pass++) {
      let nearest=null;
      for(const box of this._colliders) {
        const hit=sweepBox(position,remaining,box);
        if(hit&&(!nearest||hit.time<nearest.time))nearest=hit;
      }
      if(remaining.y<0&&position.y+remaining.y<FLOOR) {
        const hit={time:Math.max(0,(FLOOR-position.y)/remaining.y),normal:UP};
        if(!nearest||hit.time<nearest.time)nearest=hit;
      }
      const towerHit=this._towerHit(position,remaining);
      if(towerHit&&(!nearest||towerHit.time<nearest.time))nearest=towerHit;
      if(!nearest) {position.add(remaining);break;}
      const travel=Math.max(0,nearest.time-EPSILON/Math.max(.01,remaining.length()));
      position.addScaledVector(remaining,travel);
      remaining.multiplyScalar(1-travel);
      remaining.addScaledVector(nearest.normal,-Math.min(0,remaining.dot(nearest.normal)));
      position.addScaledVector(nearest.normal,EPSILON*.25);
    }
    position.y=Math.max(FLOOR,position.y);
    return position;
  }

  setPose(position,target) {
    this._clearInput();
    const damping=this._controls.enableDamping;
    this._controls.enableDamping=false;this._controls.update();
    this.camera.position.copy(this._outsideSolids(vector(position)));
    this.target.copy(vector(target));this.camera.lookAt(this.target);
    this._controls.update();this._controls.enableDamping=damping;
    this._readOrientation();
    return this;
  }
  setView(name) {
    this.tour=false;
    const views={
      overview:[[-25,24,29],[-3,1,-3]],
      ground:[[-19,2.25,3],[-4,1.75,-5]],
      rear:[[7,4.6,-16],[-8,1.6,-3]],
      overhead:[[0,54,.5],[0,0,0]],
    };
    const view=views[name];if(!view)throw new Error(`Unknown camera view: ${name}`);
    return this.setPose(view[0],view[1]);
  }
  focus(point) {
    this.tour=false;this.target.copy(vector(point));this.camera.lookAt(this.target);this._readOrientation();
    return this;
  }
  tourAt(time) {
    const t=((time/28)%1+1)%1;
    const position=this._tourCurve.getPoint(t);
    const target=this._tourTargets.getPoint(t);
    this.camera.position.copy(this._moveSafely(this.camera.position,position));
    this.target.copy(target);this.camera.lookAt(target);this._readOrientation();
    return this;
  }
  update(dt) {
    dt=Math.min(.05,Math.max(0,dt));
    if(this._tour){this._tourTime+=dt;this.tourAt(this._tourTime);return;}
    if(!document.hasFocus()||document.hidden) {this._clearInput();return;}
    if(this.mode==='orbit') {
      const before=this.camera.position.clone();this._controls.update(dt);
      this.camera.position.copy(this._moveSafely(before,this.camera.position));
      this.camera.lookAt(this.target);this._readOrientation();return;
    }
    this.camera.quaternion.setFromEuler(new THREE.Euler(this._pitch,this._yaw,0,'YXZ'));
    const forward=new THREE.Vector3(0,0,-1).applyQuaternion(this.camera.quaternion);
    const right=new THREE.Vector3(1,0,0).applyQuaternion(this.camera.quaternion);
    const desired=new THREE.Vector3();
    if(!interactive(document.activeElement)) {
      if(this._keys.has('KeyW'))desired.add(forward);
      if(this._keys.has('KeyS'))desired.sub(forward);
      if(this._keys.has('KeyD'))desired.add(right);
      if(this._keys.has('KeyA'))desired.sub(right);
      if(this._keys.has('KeyE'))desired.y+=1;
      if(this._keys.has('KeyQ'))desired.y-=1;
    }
    if(desired.lengthSq()>0)desired.normalize().multiplyScalar(this._keys.has('ShiftLeft')||this._keys.has('ShiftRight')?18:6);
    this._velocity.lerp(desired,1-Math.exp(-dt*12));
    const step=this._velocity.clone().multiplyScalar(dt);
    const dolly=this._dolly*(1-Math.exp(-dt*18));this._dolly-=dolly;step.addScaledVector(forward,dolly);
    const destination=this.camera.position.clone().add(step);
    this.camera.position.copy(this._moveSafely(this.camera.position,destination));
    this.target.copy(this.camera.position).addScaledVector(forward,this._focusDistance);
  }
  dispose() {
    this._controls.dispose();for(const [target,name,fn,options] of this._listeners)target.removeEventListener(name,fn,options);
    this._listeners.length=0;this._clearInput();
  }
}
