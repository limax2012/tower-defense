import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { createEnvironment } from './environment.js';
import { Combat } from './combat.js';
import { CameraRig } from './camera.js';

const canvas=document.querySelector('#scene');
const scene=new THREE.Scene();scene.name='Maximal Bastion — Cinderworks 3D combat study';
scene.background=new THREE.Color(0x101923);scene.fog=new THREE.FogExp2(0x101923,.008);
const camera=new THREE.PerspectiveCamera(48,innerWidth/innerHeight,.08,260);
const renderer=new THREE.WebGLRenderer({canvas,antialias:true,powerPreference:'high-performance',preserveDrawingBuffer:true});
renderer.setSize(innerWidth,innerHeight);renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));
renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.13;
renderer.outputColorSpace=THREE.SRGBColorSpace;
const pmrem=new THREE.PMREMGenerator(renderer);
const room=new RoomEnvironment();scene.environment=pmrem.fromScene(room,.055).texture;
scene.environmentIntensity=.52;room.dispose();pmrem.dispose();
const hemi=new THREE.HemisphereLight(0xadcaed,0x5b3022,2.15);hemi.name='Cool ambient sky and foundry bounce';scene.add(hemi);
const sun=new THREE.DirectionalLight(0xffd7a4,3.2);sun.name='Warm foundry key';sun.position.set(-17,32,17);
sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);sun.shadow.camera.left=-39;sun.shadow.camera.right=39;
sun.shadow.camera.top=31;sun.shadow.camera.bottom=-31;sun.shadow.camera.near=.5;sun.shadow.camera.far=95;
sun.shadow.normalBias=.065;sun.shadow.bias=-.00010;sun.shadow.radius=2;scene.add(sun);
const fill=new THREE.DirectionalLight(0x8cbbff,2.0);fill.name='Blue rim light';fill.position.set(20,15,-25);scene.add(fill);
const environment=createEnvironment();scene.add(environment.group);
for(const pos of [[-28,4,-17],[26,5,-17],[-2,2,18]]) {
  const light=new THREE.PointLight(0xff892c,30,19,2);light.name='Furnace spill';light.position.set(...pos);scene.add(light);
}
const combat=new Combat(scene,environment);
const rig=new CameraRig(camera,canvas,environment,combat.towers);
const target=new THREE.WebGLRenderTarget(innerWidth,innerHeight,{type:THREE.HalfFloatType,samples:4});
const composer=new EffectComposer(renderer,target);composer.addPass(new RenderPass(scene,camera));
const bloom=new UnrealBloomPass(new THREE.Vector2(innerWidth,innerHeight),.38,.32,1.1);composer.addPass(bloom);composer.addPass(new OutputPass());
let paused=false,manual=new URLSearchParams(location.search).has('capture'),accumulator=0,last=performance.now(),highQuality=true;
let frames=0,fps=60,fpsStart=performance.now();

function resize(){camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);composer.setSize(innerWidth,innerHeight);}
window.addEventListener('resize',resize);
renderer.info.autoReset=false;
const render=()=>{scene.updateMatrixWorld(true);renderer.info.reset();composer.render();};
function step(seconds){let remaining=seconds;while(remaining>1e-8){const dt=Math.min(1/60,remaining);combat.update(dt);environment.update(combat.time);remaining-=dt;}scene.updateMatrixWorld(true);}
function updateHud(){
  const seconds=Math.floor(combat.time);document.querySelector('#time').textContent=`${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;
  document.querySelector('#sequence').textContent=paused?'PAUSED':`COMBAT LOOP · ${combat.kills} DEFEATED`;
  for(const t of combat.towers)document.getElementById(`${t.type}-shots`).textContent=t.shots;
}
function animate(now){
  requestAnimationFrame(animate);const dt=Math.min(.10,(now-last)/1000);last=now;
  if(manual)return;
  if(!paused&&!document.hidden){accumulator=Math.min(accumulator+dt,.15);while(accumulator>=1/60){step(1/60);accumulator-=1/60;}if(combat.time>=60)reset();}
  rig.update(dt);render();updateHud();
  frames++;if(now-fpsStart>1000){fps=frames*1000/(now-fpsStart);frames=0;fpsStart=now;}
}
function setPaused(value){paused=value;document.querySelector('#pause').textContent=paused?'Resume':'Pause';updateHud();}
function reset(){combat.reset();environment.update(0);accumulator=0;updateHud();render();}
function setMode(mode){rig.tour=false;rig.setMode(mode);document.querySelector('#orbit').classList.toggle('active',mode==='orbit');document.querySelector('#free').classList.toggle('active',mode==='free');document.querySelector('#tour').classList.remove('active');}
document.querySelector('#pause').onclick=()=>setPaused(!paused);
document.querySelector('#restart').onclick=reset;
document.querySelector('#tour').onclick=()=>{rig.tour=!rig.tour;document.querySelector('#tour').classList.toggle('active',rig.tour);};
document.querySelector('#orbit').onclick=()=>setMode('orbit');document.querySelector('#free').onclick=()=>setMode('free');
document.querySelector('#help').onclick=()=>{const guide=document.querySelector('#guide');guide.hidden=!guide.hidden;};
for(const button of document.querySelectorAll('[data-view]'))button.onclick=()=>{rig.setView(button.dataset.view);document.querySelector('#tour').classList.remove('active');};
for(const button of document.querySelectorAll('[data-focus]'))button.onclick=()=>{
  const tower=combat.towers.find(t=>t.type===button.dataset.focus);rig.focus(tower.model.group.position.clone().add(new THREE.Vector3(0,1.8,0)));
};
document.querySelector('#quality').onclick=()=>{
  highQuality=!highQuality;renderer.setPixelRatio(highQuality?Math.min(devicePixelRatio,1.5):1);
  bloom.enabled=highQuality;renderer.shadowMap.enabled=highQuality;resize();
  document.querySelector('#quality').textContent=highQuality?'High quality':'Standard quality';
};
window.addEventListener('keydown',event=>{
  if(event.target.closest('input,textarea,select')||!document.hasFocus())return;
  if(event.code==='Space'){event.preventDefault();setPaused(!paused);}
  if(event.code==='KeyR')reset();
  if(event.code==='KeyH')document.body.classList.toggle('hide-ui');
});

function inspectionClips(){
  const tracks=[],times=[0,1,2,3,4];
  for(const tower of combat.towers){
    const yaw=tower.model.yaw.rotation.y;
    const rotations=[];for(const v of [0,.32,0,-.32,0])rotations.push(...new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0,1,0),yaw+v).toArray());
    tracks.push(new THREE.QuaternionKeyframeTrack(`${tower.model.yaw.uuid}.quaternion`,times,rotations));
    const z=tower.model.recoil.position.z;
    tracks.push(new THREE.VectorKeyframeTrack(`${tower.model.recoil.uuid}.position`,[0,.1,.35,1,1.1,1.35,2,2.1,2.35,3,3.1,3.35,4],
      [0,0,z,0,0,z-.13,0,0,z,0,0,z,0,0,z-.13,0,0,z,0,0,z,0,0,z-.13,0,0,z,0,0,z,0,0,z-.13,0,0,z,0,0,z]));
  }
  for(const e of combat.enemies){
    for(const leg of e.model.legs){
      const tt=[],qq=[];for(let i=0;i<=48;i++){const time=i/12;tt.push(time);qq.push(...new THREE.Quaternion().setFromEuler(new THREE.Euler(Math.sin(time*(e.type==='runner'?13:5.8)+leg.phase)*.22,0,0)).toArray());}
      tracks.push(new THREE.QuaternionKeyframeTrack(`${leg.hip.uuid}.quaternion`,tt,qq));
    }
  }
  return [new THREE.AnimationClip('Mechanical inspection — yaw recoil and gait',4,tracks)];
}
async function exportGLB(){
  const exporter=new GLTFExporter();
  const effectVisibility=combat.effects.group.visible;combat.effects.group.visible=false;
  try{return await exporter.parseAsync(scene,{binary:true,onlyVisible:true,animations:inspectionClips(),maxTextureSize:1024});}
  finally{combat.effects.group.visible=effectVisibility;}
}
window.poc={ready:true,scene,camera,renderer,combat,environment,rig,
  step,render,reset,exportGLB,
  setManual(value){manual=value;last=performance.now();},
  setUI(value){document.body.classList.toggle('hide-ui',!value);},
  setView(name){rig.setView(name);render();},
  setPose(position,target){rig.setPose(position,target);render();},
  tourAt(seconds){rig.tourAt(seconds);render();},
  seek(time){reset();step(time);render();updateHud();},
  stats(){return {time:combat.time,fps,enemies:combat.enemies.length,projectiles:combat.projectiles.length,
    kills:combat.kills,shots:Object.fromEntries(combat.towers.map(t=>[t.type,t.shots])),telemetry:{...combat.telemetry},
    drawCalls:renderer.info.render.calls,triangles:renderer.info.render.triangles,
    camera:camera.position.toArray(),mode:rig.mode,errors:[]};},
};
step(.01);render();updateHud();document.querySelector('#loading').remove();
requestAnimationFrame(animate);
window.addEventListener('error',event=>{const error=document.querySelector('#error');error.hidden=false;error.textContent=event.message;});
