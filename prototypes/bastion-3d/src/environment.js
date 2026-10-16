import * as THREE from 'three';

// Authored Cinderworks route and build decks, mapped from the 960 × 720 playfield.
const route = [[-32,104],[120,104],[120,250],[360,250],[360,112],[640,112],
  [640,386],[492,386],[492,570],[820,570],[820,642],[952,642]];
const decks = [[20,170,50,70],[170,150,140,50],[170,310,150,140],
  [410,165,180,70],[710,170,180,150],[25,470,410,185],
  [555,436,205,84],[700,680,87,22]];
const world = (x, z, y = 0) => new THREE.Vector3((x-480)/16, y, (z-360)/20);
const UP = new THREE.Vector3(0,1,0);

function metalTexture() {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 256;
  const c = canvas.getContext('2d'); const data = c.createImageData(256,256);
  let seed = 57193;
  for (let i=0;i<data.data.length;i+=4) {
    seed = (Math.imul(seed,1664525)+1013904223)>>>0;
    const value = 174 + ((seed>>>24)%50);
    data.data[i]=data.data[i+1]=data.data[i+2]=value; data.data[i+3]=255;
  }
  c.putImageData(data,0,0);
  c.strokeStyle='rgba(255,255,255,.13)'; c.lineWidth=1;
  for(let i=0;i<22;i++) { c.beginPath(); c.moveTo(i*29%256,i*47%256); c.lineTo(i*29%256+18,i*47%256-2); c.stroke(); }
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS=tex.wrapT=THREE.RepeatWrapping; tex.repeat.set(4,4);
  tex.colorSpace=THREE.SRGBColorSpace;
  return tex;
}

function strokeEdges(points, halfWidth) {
  const left=[],right=[];
  points.forEach((p,i)=> {
    const before = points[Math.max(0,i-1)], after = points[Math.min(points.length-1,i+1)];
    const incoming = i===0 ? after.clone().sub(p).normalize() : p.clone().sub(before).normalize();
    const outgoing = i===points.length-1 ? incoming : after.clone().sub(p).normalize();
    const a = new THREE.Vector3(-incoming.z,0,incoming.x);
    const b = new THREE.Vector3(-outgoing.z,0,outgoing.x);
    const m = a.clone().add(b).normalize();
    const length = halfWidth / Math.max(.1,m.dot(a));
    left.push(p.clone().addScaledVector(m,length));
    right.push(p.clone().addScaledVector(m,-length));
  });
  return {left,right};
}

export function createEnvironment() {
  const group = new THREE.Group(); group.name='Cinderworks — furnace transfer lanes';
  const colliders=[], fans=[], hotMaterials=[];
  const path=route.map(([x,z])=>world(x,z,.16));
  const pathWidth=3.1;
  const metalMap=metalTexture();
  const material=(color,roughness=.66,metalness=.55)=>new THREE.MeshStandardMaterial({color,roughness,metalness,map:metalMap});
  const m={
    floor:material('#28262b',.88,.28), foundation:material('#161c25',.8,.45),
    dark:material('#17212b',.6,.65), steel:material('#344551',.59,.7),
    edge:material('#697680',.48,.75), deck:material('#394046',.81,.5),
    copper:material('#9c633e',.5,.73), road:material('#322e31',.83,.35),
    distant:material('#26323d',.91,.3), distantTrim:material('#38434c',.79,.4),
    distantLight:new THREE.MeshStandardMaterial({color:'#ab754f',emissive:'#a74c20',emissiveIntensity:.9,roughness:.6}),
    black:material('#0e161e',.92,.3), warning:material('#dba34c',.7,.28),
    orange:new THREE.MeshStandardMaterial({color:'#fda151',emissive:'#ff690b',emissiveIntensity:1.8,roughness:.5,metalness:.25}),
    cyan:new THREE.MeshStandardMaterial({color:'#79f5e9',emissive:'#13baaf',emissiveIntensity:1.6,roughness:.4,metalness:.2}),
    roadLight:new THREE.MeshStandardMaterial({color:'#f5a561',emissive:'#a84d12',emissiveIntensity:1.05,roughness:.45}),
  };
  const geometries={box:new THREE.BoxGeometry(1,1,1), cylinder:new THREE.CylinderGeometry(.5,.5,1,12),
    bolt:new THREE.CylinderGeometry(.5,.5,1,6), sphere:new THREE.IcosahedronGeometry(.5,1)};
  const batches=new Map(), dummy=new THREE.Object3D();
  function instance(kind,mat,position,scale,rotation=null,shadow=true) {
    const key=kind+':'+mat.uuid+':'+shadow;
    if(!batches.has(key))batches.set(key,{kind,mat,shadow,matrices:[]});
    dummy.position.copy(position); dummy.scale.set(...scale);
    dummy.rotation.set(0,0,0);
    if(rotation instanceof THREE.Quaternion)dummy.quaternion.copy(rotation);
    else if(rotation)dummy.rotation.set(...rotation);
    dummy.updateMatrix(); batches.get(key).matrices.push(dummy.matrix.clone());
  }
  function box(name,x,y,z,w,h,d,mat,collision=false) {
    instance('box',mat,new THREE.Vector3(x,y,z),[w,h,d]);
    if(collision)colliders.push(new THREE.Box3(new THREE.Vector3(x-w/2,y-h/2,z-d/2),new THREE.Vector3(x+w/2,y+h/2,z+d/2)));
  }
  function rod(a,b,r,mat,kind='cylinder') {
    const delta=b.clone().sub(a),q=new THREE.Quaternion().setFromUnitVectors(UP,delta.clone().normalize());
    instance(kind,mat,a.clone().add(b).multiplyScalar(.5),[r*2,delta.length(),r*2],q);
  }
  function beam(a,b,width,height,mat) {
    const mid=a.clone().add(b).multiplyScalar(.5),len=a.distanceTo(b);
    const yaw=Math.atan2(b.x-a.x,b.z-a.z);
    instance('box',mat,mid,[width,height,len],[0,yaw,0]);
  }
  function bolt(x,y,z,size=.10) { instance('bolt',m.edge,new THREE.Vector3(x,y,z),[size,.055,size]); }
  function deck(x,z,w,d,index) {
    box('work deck plinth',x,.17,z,w+.28,.34,d+.28,m.dark);
    box('work deck surface',x,.38,z,w,.12,d,m.deck);
    for(let px=x-w/2+.24;px<x+w/2;px+=1.1) {
      box('deck panel joint',px,.445,z,.026,.015,d-.22,m.black);
      for(const side of [-1,1])bolt(px,.45,z+side*(d/2-.20),.08);
    }
    for(const side of [-1,1]) {
      box('deck inset edge',x,.45,z+side*(d/2-.12),w-.12,.045,.09,m.copper);
      box('deck side rail',x+side*(w/2-.05),.44,z,.06,.06,d,m.edge);
    }
    if(w>5&&d>3) {
      for(let i=0;i<5;i++)box('deck hazard bar',x-w/2+.45+i*.35,.465,z+d/2-.40,.13,.025,.44,m.warning);
      box('deck service hatch',x+w/2-.7,.455,z-d/2+.65,.78,.025,.72,m.dark);
      for(let j=0;j<4;j++)box('service grille',x+w/2-.7,.475,z-d/2+.42+j*.14,.57,.02,.038,m.edge);
    }
  }

  box('continuous foundation',0,-1.12,0,68,2.2,44,m.foundation);
  box('foundry floor',0,-.085,0,67,.16,43,m.floor);
  // The playable slab has finished structural ribs and a closed underside.
  for(let x=-31;x<=31;x+=6) {
    box('foundation rib',x,-1.0,-21.9,.35,1.8,.3,m.steel);
    box('foundation rib',x,-1.0,21.9,.35,1.8,.3,m.steel);
    box('underside cross beam',x,-2.17,0,.32,.20,42,m.steel);
  }
  for(let z=-19;z<=19;z+=6) {
    box('foundation rib',-33.9,-1,z,.3,1.8,.35,m.steel);
    box('foundation rib',33.9,-1,z,.3,1.8,.35,m.steel);
  }
  for(let x=-32;x<34;x+=4)for(let z=-20;z<22;z+=4) {
    box('floor tile joint',x,-.001,z,.027,.018,3.93,m.black);
    box('floor tile joint',x+2,-.001,z-2,4,.018,.027,m.black);
    bolt(x+.14,.015,z-1.84,.065);
  }

  const edges=strokeEdges(path,pathWidth/2);
  const outline=[...edges.left,...edges.right.toReversed()];
  const shape=new THREE.Shape();
  outline.forEach((p,i)=>i===0?shape.moveTo(p.x,-p.z):shape.lineTo(p.x,-p.z));shape.closePath();
  const laneGeometry=new THREE.ExtrudeGeometry(shape,{depth:.16,bevelEnabled:false,steps:1});
  const lane=new THREE.Mesh(laneGeometry,m.road); lane.name='Continuous serpentine lane';
  lane.rotation.x=-Math.PI/2; lane.receiveShadow=true; group.add(lane);
  for(const edge of [edges.left,edges.right])for(let i=1;i<edge.length;i++) {
    const a=edge[i-1].clone(),b=edge[i].clone(); a.y=b.y=.18;
    beam(a,b,.12,.10,m.copper);
    const offset=b.clone().sub(a).normalize(),length=a.distanceTo(b);
    for(let d=.45;d<length-.2;d+=1.35) {
      const p=a.clone().addScaledVector(offset,d); p.y=.247;
      const dir=Math.atan2(offset.x,offset.z);
      instance('box',m.warning,p,[.16,.035,.28],[0,dir+.55,0],false);
    }
  }
  // Lane plates stop short of corners; the continuous slab carries every turn.
  for(let i=1;i<path.length;i++) {
    const a=path[i-1],b=path[i],delta=b.clone().sub(a),length=delta.length(),dir=delta.normalize();
    for(let d=2.4;d<length-1.6;d+=1.35) {
      const p=a.clone().addScaledVector(dir,d);p.y=.172;
      instance('box',m.black,p,[pathWidth-.36,.018,.042],[0,Math.atan2(dir.x,dir.z),0],false);
      if(Math.floor(d*10)%3===0) {
        p.y=.188;
        instance('box',m.roadLight,p,[.10,.026,.55],[0,Math.atan2(dir.x,dir.z),0],false);
      }
    }
  }
  decks.forEach(([x,z,w,d],i)=>{const p=world(x+w/2,z+d/2);deck(p.x,p.z,w/16,d/20,i);});

  function furnace(x,z,w,d,h,index) {
    box('heat exchanger base',x,.24,z,w+.4,.48,d+.4,m.dark,true);
    box('heat exchanger body',x,.5+h/2,z,w,h,d,m.steel,true);
    box('heat exchanger top',x,h+.62,z,w+.12,.24,d+.12,m.edge);
    box('heat exchanger top inset',x,h+.76,z,w-.48,.05,d-.48,m.dark);
    const hot=m.orange.clone();hot.emissiveIntensity=1.6+index*.1;hotMaterials.push(hot);
    // Recessed glowing cores are visible through all four armored sides.
    for(const side of [-1,1]) {
      box('furnace recessed face',x,.50+h/2,z+side*(d/2+.015),w-.36,h-.28,.045,m.black);
      for(let j=0;j<Math.floor(w/.60);j++) {
        const px=x-w/2+.43+j*.60;
        box('heated slit',px,.5+h/2,z+side*(d/2+.05),.16,h-.6,.04,hot);
      }
      for(let y=.92;y<h+.3;y+=.9)box('transverse protection rail',x,y,z+side*(d/2+.11),w+.1,.14,.18,m.copper);
      box('furnace side armor',x+side*(w/2+.04),.5+h/2,z,.14,h,d+.10,m.dark);
      for(let j=0;j<3;j++)box('side heat slit',x+side*(w/2+.13),.65+h*.24*j+.4,z,.035,.44,d*.50,hot);
      for(const end of [-1,1]) {
        box('corner protection beam',x+side*(w/2-.03),.5+h/2,z+end*(d/2),.22,h+.22,.25,m.copper);
        bolt(x+side*(w/2-.2),h+.81,z+end*(d/2-.2),.14);
      }
    }
    for(let i=0;i<Math.floor(w/.5);i++) {
      const a=new THREE.Vector3(x-w/2+.25+i*.5,h+.84,z-d/2+.18);
      const b=a.clone().add(new THREE.Vector3(.22,0,.32));beam(a,b,.11,.028,m.warning);
    }
    box('coolant indicator',x+w/2+.15,h*.65,z,.05,.40,.16,m.cyan);
    const fan=new THREE.Group();fan.position.set(x,h+.86,z);
    const hub=new THREE.Mesh(new THREE.CylinderGeometry(.17,.17,.12,12),m.edge);hub.castShadow=true;fan.add(hub);
    for(let j=0;j<4;j++) {
      const blade=new THREE.Mesh(new THREE.BoxGeometry(.9,.055,.17),m.steel);
      blade.position.set(Math.cos(j*Math.PI/2)*.4,0,Math.sin(j*Math.PI/2)*.4);blade.rotation.y=-j*Math.PI/2;
      fan.add(blade);
    }
    group.add(fan); fans.push(fan);
    const guard=new THREE.Mesh(new THREE.TorusGeometry(.91,.045,6,20),m.edge);guard.rotation.x=Math.PI/2;guard.position.set(x,h+.94,z);group.add(guard);
  }
  // These heat exchangers occupy the same gaps as their 2D map landmarks.
  [[735,354,170,140,3.7],[367,294,89,144,2.2],[52,348,90,85,2.0],[470,638,170,67,1.9]].forEach(([x,z,w,d,h],i)=> {
    const p=world(x+w/2,z+d/2);furnace(p.x,p.z,w/16,d/20,h,i);
  });

  function pipe(points,r=.20) {
    for(let i=1;i<points.length;i++) {
      rod(points[i-1],points[i],r,m.copper);
      const direction=points[i].clone().sub(points[i-1]).normalize(),length=points[i].distanceTo(points[i-1]);
      for(let d=.7;d<length;d+=3) {
        const a=points[i-1].clone().addScaledVector(direction,d-.06),b=a.clone().addScaledVector(direction,.12);
        rod(a,b,r*1.35,m.steel);
      }
    }
    for(const p of points)instance('sphere',m.copper,p,[r*2.1,r*2.1,r*2.1]);
  }
  // Subsurface service runs remain clear of the movement lanes and tower decks.
  pipe([new THREE.Vector3(-31,.20,-4),new THREE.Vector3(-19,.20,-4)],.19);
  pipe([new THREE.Vector3(14,.20,-16),new THREE.Vector3(14,.20,-12)],.24);
  pipe([new THREE.Vector3(-8,.20,1),new THREE.Vector3(-8,.20,16.5)],.14);
  pipe([new THREE.Vector3(15,.20,17.7),new THREE.Vector3(27,.20,17.7),new THREE.Vector3(27,.20,19)],.22);

  function stack(x,z,r,h) {
    const center=new THREE.Vector3(x,h/2,z);
    instance('cylinder',m.dark,center,[r*2,h,r*2]);
    colliders.push(new THREE.Box3(new THREE.Vector3(x-r,0,z-r),new THREE.Vector3(x+r,h+.2,z+r)));
    for(const y of [.5,h*.32,h*.67,h-.28])instance('cylinder',m.copper,new THREE.Vector3(x,y,z),[r*2.17,.30,r*2.17]);
    for(let i=0;i<8;i++) {
      const angle=i*Math.PI/4;
      const px=x+Math.cos(angle)*r,pz=z+Math.sin(angle)*r;
      instance('box',m.steel,new THREE.Vector3(px,h/2,pz),[.18,h-.4,.3],[0,-angle,0]);
      const sx=x+Math.cos(angle+.15)*r,sz=z+Math.sin(angle+.15)*r;
      instance('box',m.orange,new THREE.Vector3(sx,h*.55,sz),[.16,h*.51,.15],[0,-angle,0]);
    }
    instance('cylinder',m.steel,new THREE.Vector3(x,h+.08,z),[r*1.68,.22,r*1.68]);
    instance('cylinder',m.black,new THREE.Vector3(x,h+.20,z),[r*1.3,.08,r*1.3]);
    rod(new THREE.Vector3(x,h+.21,z),new THREE.Vector3(x,h+1.25,z),r*.20,m.steel);
    instance('cylinder',m.copper,new THREE.Vector3(x,h+1.3,z),[r*.55,.12,r*.55]);
  }
  stack(-26,-18.1,2.0,7.5);stack(25,-17.9,2.25,9.0);
  stack(31,-5.5,1.45,5.1);
  // A perimeter maintenance bridge frames the scene while leaving combat sightlines open.
  for(const x of [-22,21.5]) {
    box('gantry footing',x,.22,-19.0,1.35,.44,1.35,m.dark,true);
    box('gantry column',x,3.7,-19.0,.58,7.4,.65,m.steel,true);
    for(let i=0;i<5;i++)box('gantry column joint',x,.65+i*1.37,-19.0,.85,.16,.83,m.copper);
  }
  box('maintenance catwalk',-.25,6.1,-19.0,44.5,.32,1.9,m.dark,true);
  for(let x=-22;x<=22;x+=2.3) {
    box('catwalk tread',x,6.28,-19,1.8,.05,1.65,m.steel);
    for(const s of [-1,1])rod(new THREE.Vector3(x,6.3,-19+s*.89),new THREE.Vector3(x,7.1,-19+s*.89),.065,m.copper);
  }
  for(const z of [-19.89,-18.11]) {
    rod(new THREE.Vector3(-22.5,7.1,z),new THREE.Vector3(22,7.1,z),.07,m.copper);
    rod(new THREE.Vector3(-22.5,6.72,z),new THREE.Vector3(22,6.72,z),.05,m.steel);
    for(let x=-22;x<21;x+=3.6)rod(new THREE.Vector3(x,5.55,z),new THREE.Vector3(x+3.3,6.0,z),.08,m.steel);
  }
  pipe([new THREE.Vector3(-25,4.3,-19),new THREE.Vector3(-20,4.3,-19),new THREE.Vector3(-20,5.5,-19),new THREE.Vector3(23,5.5,-19)],.23);

  function terminal(x,z,isGoal) {
    const light=isGoal?m.cyan:m.orange;
    for(const side of [-1,1]) {
      box('terminal buttress',x,1.9,z+side*2.05,1.1,3.8,.60,m.steel,true);
      box('terminal vertical lamp',x-.57,2,z+side*2.05,.06,2.85,.16,light);
      box('terminal foot',x,.17,z+side*2.05,1.8,.34,1,m.dark);
      for(let j=0;j<4;j++)box('terminal side fin',x,1+j*.6,z+side*2.35,1.4,.15,.30,m.copper);
    }
    box('terminal lintel',x,3.9,z,1.12,.4,4.8,m.dark,true);
    box('terminal top lamp',x-.58,3.91,z,.045,.14,3.3,light);
    box('terminal threshold',x,.105,z,1.1,.05,3.9,m.black);
    for(let j=-2;j<=2;j++)box('terminal entry mark',x,.18,z+j*.48,.75,.025,.11,light);
  }
  terminal(-32.2,-12.8,false);terminal(30.7,14.1,true);

  // Small low service details give deck-height views finished foreground surfaces.
  for(const [x,z] of [[-30,15.5],[-29,17],[29,3],[-5,18.8]]) {
    box('sealed service crate',x,.45,z,1.2,.9,.9,m.steel,true);
    box('crate top seam',x,.92,z,1.22,.05,.08,m.copper);
    for(const s of [-1,1])box('crate reinforcement',x+s*.45,.47,z,.08,1.0,.96,m.dark);
  }

  // Closed industrial blocks form a distant perimeter beyond the playable deck.
  box('district ground apron',0,-3.4,0,220,1.8,190,m.foundation);
  const city=[[-73,-55,15,11,16],[-52,-61,15,16,24],[-31,-55,12,13,14],
    [-9,-62,18,14,20],[17,-57,13,17,29],[40,-60,17,14,17],[67,-55,20,15,23],
    [-63,-29,13,15,19],[-70,-2,15,17,25],[-61,25,13,16,15],
    [63,-28,14,16,21],[70,0,17,14,27],[62,28,15,13,14],
    [-47,55,19,14,16],[-19,62,15,19,23],[10,57,18,13,12],[39,59,16,17,22],[65,57,17,13,17]];
  for(let i=0;i<city.length;i++) {
    const [x,z,w,d,h]=city[i],base=-2.48;
    box('distant foundry building',x,base+h/2,z,w,h,d,m.distant,true);
    box('distant roof cap',x,base+h+.14,z,w+.45,.28,d+.45,m.distantTrim);
    box('distant roof service room',x+w*.17,base+h+1.1,z-d*.12,w*.43,1.9,d*.42,m.distant,true);
    for(const side of [-1,1]) {
      for(let k=0;k<3;k++) {
        const px=x-w*.34+k*w*.34;
        box('distant structural pier',px,base+h/2,z+side*(d/2+.10),.24,h,.26,m.distantTrim);
      }
      for(let level=0;level<3;level++) {
        const yy=base+h*.24+level*h*.25;
        box('distant window recess',x,yy,z+side*(d/2+.08),w*.75,.48,.14,m.dark);
        for(let window=0;window<4;window++) {
          if((window+level+i)%3===0)continue;
          box('distant furnace glow',x-w*.29+window*w*.19,yy,z+side*(d/2+.16),w*.10,.20,.045,m.distantLight);
        }
      }
    }
    if(i%3===1) {
      const chimneyHeight=4+i%4;
      box('distant chimney',x-w*.29,base+h+chimneyHeight/2,z+d*.20,1.5,chimneyHeight,1.7,m.distantTrim,true);
      box('chimney crown',x-w*.29,base+h+chimneyHeight,z+d*.20,1.9,.20,2.1,m.copper);
    }
  }
  for(const z of [-45,43])for(let x=-50;x<52;x+=17) {
    box('outer pipe rack column',x,1.0,z,.50,7,.6,m.distantTrim,true);
    box('outer pipe rack lintel',x+8,4.5,z,17.0,.5,1.1,m.distantTrim);
    rod(new THREE.Vector3(x,4.8,z),new THREE.Vector3(x+17,4.8,z),.20,m.copper);
  }
  for(const batch of batches.values()) {
    const mesh=new THREE.InstancedMesh(geometries[batch.kind],batch.mat,batch.matrices.length);
    mesh.name=`Foundry ${batch.kind} / ${batch.mat.color.getHexString()}`;
    batch.matrices.forEach((matrix,i)=>mesh.setMatrixAt(i,matrix));
    mesh.castShadow=batch.shadow;mesh.receiveShadow=true;mesh.computeBoundingSphere();group.add(mesh);
  }

  const sparkCount=90,sparkPositions=new Float32Array(sparkCount*3),sparkSeeds=[];
  for(let i=0;i<sparkCount;i++)sparkSeeds.push({x:Math.sin(i*41.2)*29,z:Math.cos(i*17.7)*17,phase:(i*.613)%1,speed:.3+(i%7)*.055});
  const sparksGeo=new THREE.BufferGeometry();sparksGeo.setAttribute('position',new THREE.BufferAttribute(sparkPositions,3));
  const sparks=new THREE.Points(sparksGeo,new THREE.PointsMaterial({color:'#ffb366',size:.048,transparent:true,opacity:.72,depthWrite:false,blending:THREE.AdditiveBlending}));
  sparks.name='Rising foundry embers';sparks.frustumCulled=false;group.add(sparks);
  function update(time) {
    fans.forEach((fan,i)=>fan.rotation.y=time*(2+i*.23));
    hotMaterials.forEach((mat,i)=>mat.emissiveIntensity=1.65+Math.sin(time*1.4+i)*.12);
    sparkSeeds.forEach((s,i)=> {
      const age=(time*s.speed*.08+s.phase)%1;
      sparkPositions[i*3]=s.x+Math.sin(time*.35+i)*.35;
      sparkPositions[i*3+1]=.3+age*6;
      sparkPositions[i*3+2]=s.z+Math.cos(time*.28+i)*.28;
    });
    sparksGeo.attributes.position.needsUpdate=true;
  }
  update(0);
  return {group,path,pathWidth,colliders,update,
    towerSites:{needle:world(275,350,.44),prism:world(490,195,.44),mortar:world(670,477,.44)},
    bounds:{minX:-34,maxX:34,minZ:-22,maxZ:22}};
}
