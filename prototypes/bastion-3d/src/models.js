import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

/**
 * All models have a ground-level origin and face local +Z. Towers expose yaw,
 * pitch, recoil, and muzzle groups; negative pitch.rotation.x raises the barrel.
 * The visual update functions never change world position, yaw, or weapon pitch.
 * Materials and named closed meshes remain editable through Three.js or glTF.
 * Pass { batch: false } to either factory for separate authored component meshes.
 */

const PALETTE = { metal: 0x344d62, dark: 0x131d2b, ceramic: 0x9eafbd, pale: 0xdbeaf1,
  cyan: 0x38e9ed, violet: 0xa27aff, lime: 0xd0f45c, hostile: 0xff287f };
const cache = new Map();
const footMatrix = new THREE.Matrix4();
const footCorner = new THREE.Vector3();

const grainData = new Uint8Array(64 * 64 * 4);
for (let y = 0; y < 64; y++) for (let x = 0; x < 64; x++) {
  const value = 198 + Math.round(22 * (1 + Math.sin(x * 17.3 + y * 71.7)));
  const offset = (y * 64 + x) * 4;
  grainData.set([value, value, value, 255], offset);
}
const alloyCanvas = typeof document === 'undefined' ? null : document.createElement('canvas');
if (alloyCanvas) {
  alloyCanvas.width = alloyCanvas.height = 64;
  alloyCanvas.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(grainData), 64, 64), 0, 0);
}
const alloyGrain = alloyCanvas ? new THREE.CanvasTexture(alloyCanvas)
  : new THREE.DataTexture(grainData, 64, 64, THREE.RGBAFormat);
alloyGrain.name = 'Fine machined alloy roughness';
alloyGrain.wrapS = alloyGrain.wrapT = THREE.RepeatWrapping;
alloyGrain.repeat.set(3, 3);
alloyGrain.needsUpdate = true;

function material(color, metalness = .65, roughness = .32, emission = 0) {
  return new THREE.MeshStandardMaterial({ color, metalness, roughness,
    roughnessMap: emission ? null : alloyGrain,
    emissive: emission ? color : 0x000000, emissiveIntensity: emission });
}

function palette(accent) {
  return {
    metal: material(PALETTE.metal), dark: material(PALETTE.dark, .75, .4),
    ceramic: material(PALETTE.ceramic, .48, .28), pale: material(PALETTE.pale, .36, .3),
    core: material(accent, .28, .22, 2.0), trim: material(accent, .55, .26, .75),
    white: material(0xeaffff, .2, .2, 2.3), rubber: material(0x070e18, .1, .74),
  };
}

function solid(parent, geometry, mat, name, x = 0, y = 0, z = 0) {
  const object = new THREE.Mesh(geometry, mat);
  object.name = name;
  object.position.set(x, y, z);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}

function geometry(key, create) {
  if (!cache.has(key)) cache.set(key, create());
  return cache.get(key);
}

function bevelGeometry(w, h, d, bevel = .045) {
  return geometry(`bevel:${w}:${h}:${d}:${bevel}`, () => {
    const b = Math.min(bevel, w * .16, h * .16, d * .16);
    const x = w / 2 - b, y = h / 2 - b;
    const shape = new THREE.Shape();
    shape.moveTo(-x, -y); shape.lineTo(x, -y); shape.lineTo(x, y);
    shape.lineTo(-x, y); shape.closePath();
    const g = new THREE.ExtrudeGeometry(shape, { depth: d - b * 2,
      bevelEnabled: true, bevelSegments: 1, steps: 1, bevelSize: b, bevelThickness: b });
    g.translate(0, 0, -d / 2 + b);
    return g;
  });
}

function box(parent, w, h, d, mat, name, x = 0, y = 0, z = 0, bevel = .04) {
  return solid(parent, bevelGeometry(w, h, d, bevel), mat, name, x, y, z);
}

function cylinder(parent, top, bottom, height, sides, mat, name, x = 0, y = 0, z = 0) {
  const g = geometry(`cyl:${top}:${bottom}:${height}:${sides}`,
    () => new THREE.CylinderGeometry(top, bottom, height, sides, 1, false));
  return solid(parent, g, mat, name, x, y, z);
}

function sphere(parent, radius, mat, name, x = 0, y = 0, z = 0) {
  const g = geometry(`sphere:${radius}`, () => new THREE.SphereGeometry(radius, 12, 8));
  return solid(parent, g, mat, name, x, y, z);
}

function bar(parent, a, b, radius, mat, name, sides = 6) {
  const av = new THREE.Vector3(...a), bv = new THREE.Vector3(...b);
  const delta = bv.clone().sub(av), midpoint = av.clone().add(bv).multiplyScalar(.5);
  const object = cylinder(parent, radius, radius, delta.length(), sides, mat, name);
  object.position.copy(midpoint);
  object.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize());
  return object;
}

function diamond(parent, w, h, depth, mat, name, x = 0, y = 0, z = 0) {
  const g = geometry(`diamond:${w}:${h}:${depth}`, () => {
    const shape = new THREE.Shape();
    shape.moveTo(0, h / 2); shape.lineTo(-w / 2, 0); shape.lineTo(0, -h / 2);
    shape.lineTo(w / 2, 0); shape.closePath();
    const bevel = Math.min(.035, depth * .2);
    const extrusion = new THREE.ExtrudeGeometry(shape, { depth: depth - 2 * bevel,
      bevelEnabled: true, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 1, steps: 1 });
    extrusion.translate(0, 0, -depth / 2 + bevel);
    return extrusion;
  });
  return solid(parent, g, mat, name, x, y, z);
}

function polygonPlate(parent, radius, height, sides, mat, name, x = 0, y = 0, z = 0) {
  const object = cylinder(parent, radius, radius, height, sides, mat, name, x, y, z);
  object.rotation.x = Math.PI / 2;
  return object;
}

function torus(parent, radius, thickness, mat, name, x = 0, y = 0, z = 0, segments = 24) {
  const g = geometry(`torus:${radius}:${thickness}:${segments}`,
    () => new THREE.TorusGeometry(radius, thickness, 6, segments));
  return solid(parent, g, mat, name, x, y, z);
}

function group(parent, name, x = 0, y = 0, z = 0) {
  const object = new THREE.Group(); object.name = name; object.position.set(x, y, z);
  if (parent) parent.add(object);
  return object;
}

function batchStaticComponents(root, movingParts) {
  const boundaries = new Set([root, ...movingParts]);
  for (const owner of boundaries) {
    if (owner.isMesh) continue;
    const buckets = new Map();
    function gather(node, parentMatrix) {
      if (boundaries.has(node)) return;
      node.updateMatrix();
      const matrix = new THREE.Matrix4().multiplyMatrices(parentMatrix, node.matrix);
      if (node.isMesh && !Array.isArray(node.material)) {
        if (!buckets.has(node.material)) buckets.set(node.material, []);
        buckets.get(node.material).push({ node, matrix });
      }
      for (const child of node.children) gather(child, matrix);
    }
    for (const child of owner.children) gather(child, new THREE.Matrix4());
    for (const [mat, pieces] of buckets) {
      if (pieces.length < 2) continue;
      const key = `batch:${root.name}:${owner.name}:${mat.name}`;
      let firstVertex = 0;
      const parts = pieces.map(({ node, matrix }) => {
        const count = node.geometry.index?.count ?? node.geometry.attributes.position.count;
        const info = { name: node.name, firstVertex, vertexCount: count, transform: matrix.toArray() };
        firstVertex += count; return info;
      });
      const merged = geometry(key, () => {
        const fragments = pieces.map(({ node, matrix }) => {
          const fragment = node.geometry.index ? node.geometry.toNonIndexed() : node.geometry.clone();
          fragment.applyMatrix4(matrix); return fragment;
        });
        const result = mergeGeometries(fragments, false);
        for (const fragment of fragments) fragment.dispose();
        result.computeBoundingSphere();
        return result;
      });
      const surface = solid(owner, merged, mat, `${owner.name} / ${mat.name.split('/').pop().trim()} components`);
      surface.userData = { components: parts, editing: 'Separate by loose parts to edit individual solid components.' };
      for (const { node } of pieces) node.removeFromParent();
    }
  }
}

function towerMount(root, m, radius = 1.04) {
  cylinder(root, radius, radius * 1.08, .18, 6, m.dark, 'Ground socket', 0, .09);
  cylinder(root, radius * .98, radius, .15, 6, m.metal, 'Mounting plate', 0, .23);
  cylinder(root, radius * .8, radius * .89, .18, 6, m.dark, 'Upper mount', 0, .38);
  cylinder(root, .57, .75, .62, 6, m.metal, 'Tapered armored pedestal', 0, .70);
  cylinder(root, .58, .58, .13, 24, m.dark, 'Azimuth bearing', 0, 1.08);
  cylinder(root, .53, .53, .05, 24, m.trim, 'Bearing energy seam', 0, 1.17);
  cylinder(root, .62, .59, .13, 8, m.metal, 'Turret mounting ring', 0, 1.23);
  for (let i = 0; i < 6; i++) {
    const a = i * Math.PI / 3 + Math.PI / 6;
    const panel = group(root, `Pedestal face ${i + 1}`);
    panel.rotation.y = a;
    box(panel, .40, .43, .095, m.ceramic, 'Pedestal armor', 0, .69, .59);
    box(panel, .09, .30, .035, m.trim, 'Pedestal light', 0, .71, .65, .008);
    cylinder(root, .055, .055, .045, 6, m.ceramic, `Socket bolt ${i + 1}`,
      Math.sin(a) * radius * .8, .335, Math.cos(a) * radius * .8);
  }
}

function sideHinges(yaw, m, width, y = .65) {
  for (const side of [-1, 1]) {
    const panel = box(yaw, .16, .58, .7, m.ceramic, 'Elevation support', side * width, y - .1);
    panel.rotation.z = side * -.1;
    const bearing = cylinder(yaw, .24, .24, .13, 12, m.dark, 'Elevation bearing', side * (width + .08), y);
    bearing.rotation.z = Math.PI / 2;
    const cap = cylinder(yaw, .145, .145, .135, 12, m.metal, 'Bearing cap', side * (width + .12), y);
    cap.rotation.z = Math.PI / 2;
    const light = cylinder(yaw, .065, .065, .14, 10, m.trim, 'Bearing indicator', side * (width + .125), y);
    light.rotation.z = Math.PI / 2;
  }
}

function rearVents(parent, m, width, height, z) {
  box(parent, width, height, .08, m.dark, 'Recessed rear heat sink', 0, 0, z);
  for (let i = -2; i <= 2; i++)
    box(parent, width * .8, .032, .105, m.metal, 'Rear cooling vane', 0, i * height * .15, z - .02, .007);
}

function needleWeapon(recoil, m) {
  box(recoil, .83, .58, 1.04, m.metal, 'Needle receiver', 0, 0, -.18);
  box(recoil, .66, .20, .74, m.ceramic, 'Upper receiver armor', 0, .30, -.17);
  box(recoil, .60, .10, .45, m.dark, 'Receiver top service hatch', 0, .417, -.28);
  rearVents(recoil, m, .62, .38, -.735);
  for (const side of [-1, 1]) {
    box(recoil, .12, .37, .85, m.ceramic, 'Receiver flank armor', side * .48, 0, -.16);
    box(recoil, .035, .07, .56, m.trim, 'Flank energy conduit', side * .55, .10, -.17, .01);
    box(recoil, .17, .24, 1.18, m.metal, 'Accelerator rail', side * .265, 0, .84);
    box(recoil, .055, .055, 1.20, m.core, 'Accelerator rail light', side * .264, .125, .83, .008);
    box(recoil, .22, .31, .12, m.ceramic, 'Muzzle rail collar', side * .265, 0, 1.38);
  }
  const barrel = cylinder(recoil, .125, .17, 1.30, 10, m.dark, 'Central plasma barrel', 0, 0, .85);
  barrel.rotation.x = Math.PI / 2;
  const collar = polygonPlate(recoil, .19, .15, 10, m.ceramic, 'Muzzle collar', 0, 0, 1.52);
  polygonPlate(recoil, .132, .035, 10, m.dark, 'Muzzle recess', 0, 0, 1.61);
  const core = polygonPlate(recoil, .083, .035, 10, m.core, 'Cyan firing aperture', 0, 0, 1.635);
  box(recoil, .13, .16, .25, m.dark, 'Target optic', 0, .55, -.10);
  polygonPlate(recoil, .052, .03, 10, m.core, 'Target optic lens', 0, .55, .035);
  return { core, muzzleZ: 1.66 };
}

function prismEmblem(parent, m, scale, z, name) {
  const emblem = group(parent, name, 0, 0, z);
  diamond(emblem, scale, scale, .09, m.metal, 'Diamond metal frame');
  diamond(emblem, scale * .82, scale * .82, .075, m.core, 'Violet diamond', 0, 0, .067);
  diamond(emblem, scale * .43, scale * .43, .05, m.white, 'Concentric pale diamond', 0, 0, .11);
  return diamond(emblem, scale * .20, scale * .20, .055, m.core, 'Violet diamond center', 0, 0, .147);
}

function prismWeapon(recoil, m) {
  diamond(recoil, 1.34, 1.47, 1.0, m.dark, 'Prism armored body', 0, 0, -.20);
  diamond(recoil, 1.44, 1.57, .15, m.metal, 'Forward diamond armor rim', 0, 0, .35);
  diamond(recoil, 1.20, 1.31, .16, m.core, 'Forward violet emitter housing', 0, 0, .42);
  diamond(recoil, .71, .78, .09, m.pale, 'Forward pale diamond ring', 0, 0, .52);
  diamond(recoil, .47, .53, .06, m.core, 'Forward violet emitter', 0, 0, .58);
  for (const side of [-1, 1]) {
    const armor = box(recoil, .24, 1.02, .94, m.ceramic, 'Prism flank shield', side * .64, 0, -.19);
    armor.rotation.z = side * .16;
    box(recoil, .035, .59, .085, m.trim, 'Shield illumination', side * .783, .04, -.39, .01);
    const badge = group(recoil, 'Side diamond emblem', side * .79, .04, -.05);
    badge.rotation.y = side * Math.PI / 2;
    prismEmblem(badge, m, .64, .025, 'Side emitter window');
  }
  const rear = group(recoil, 'Rear reactor face', 0, 0, -.745);
  rear.rotation.y = Math.PI;
  prismEmblem(rear, m, .88, 0, 'Rear reactor diamond');
  box(recoil, .32, .32, 1.23, m.metal, 'Prism barrel housing', 0, 0, 1.15, .024);
  for (const side of [-1, 1]) {
    box(recoil, .035, .13, 1.18, m.core, 'Barrel side energy strip', side * .167, 0, 1.15, .005);
    box(recoil, .13, .035, 1.18, m.core, 'Barrel upper lower energy strip', 0, side * .167, 1.15, .005);
  }
  box(recoil, .39, .39, .105, m.metal, 'Emitter barrel collar', 0, 0, 1.79, .021);
  const core = diamond(recoil, .23, .23, .045, m.white, 'Prism firing aperture', 0, 0, 1.862);
  return { core, muzzleZ: 1.90 };
}

function mortarWeapon(recoil, m) {
  box(recoil, 1.0, .62, .86, m.metal, 'Mortar recoil breech', 0, -.05, -.31);
  box(recoil, .87, .29, .25, m.ceramic, 'Rear breech armor', 0, .0, -.81);
  box(recoil, .40, .14, .035, m.trim, 'Rear charge indicator', 0, .04, -.948, .01);
  const profile = [.46, -.55, .46, -.30, .38, .62, .42, .72, .42, .84,
    .29, .84, .27, .65, .29, -.35, 0, -.35, 0, -.55, .46, -.55];
  const points = [];
  for (let i = 0; i < profile.length; i += 2) points.push(new THREE.Vector2(profile[i], profile[i + 1]));
  const barrelGeometry = geometry('mortar:hollow-barrel', () => new THREE.LatheGeometry(points, 16));
  const barrel = solid(recoil, barrelGeometry, m.metal, 'Hollow mortar barrel', 0, 0, .65);
  barrel.rotation.x = Math.PI / 2;
  for (const z of [.25, .76, 1.26]) torus(recoil, z === .25 ? .448 : .395, .052, m.dark, 'Barrel reinforcement', 0, 0, z, 16);
  torus(recoil, .348, .063, m.trim, 'Chartreuse muzzle ring', 0, 0, 1.502, 16);
  torus(recoil, .38, .028, m.ceramic, 'Muzzle edge', 0, 0, 1.539, 16);
  const core = sphere(recoil, .16, m.core, 'Deep charge chamber', 0, 0, .31);
  for (const side of [-1, 1]) {
    box(recoil, .20, .45, .93, m.ceramic, 'Mortar barrel side brace', side * .54, -.03, .25);
    box(recoil, .035, .27, .60, m.trim, 'Brace charge strip', side * .652, .015, .29, .012);
    bar(recoil, [side * .43, -.28, -.55], [side * .44, -.26, .87], .055, m.dark, 'Recoil piston');
  }
  box(recoil, .20, .12, .50, m.dark, 'Ballistics sensor', 0, .50, .10);
  return { core, muzzleZ: 1.57 };
}

export function createTower(type, { batch = true } = {}) {
  if (!['needle', 'prism', 'mortar'].includes(type)) throw new Error(`Unknown tower: ${type}`);
  const m = palette(PALETTE[{ needle: 'cyan', prism: 'violet', mortar: 'lime' }[type]]);
  for (const [name, mat] of Object.entries(m)) mat.name = `${type} / ${name}`;
  if (type === 'prism') { m.core.emissiveIntensity = .88; m.white.emissiveIntensity = .72; }
  const root = group(null, `${type} tower`);
  towerMount(root, m, type === 'mortar' ? 1.14 : 1.04);
  const yaw = group(root, 'Azimuth rotation', 0, 1.25);
  const pivotHeight = type === 'prism' ? .91 : type === 'mortar' ? .74 : .68;
  sideHinges(yaw, m, type === 'mortar' ? .69 : .57, pivotHeight);
  const pitch = group(yaw, 'Weapon elevation', 0, pivotHeight);
  const recoil = group(pitch, 'Recoil assembly');
  const weapon = type === 'prism' ? prismWeapon(recoil, m) : type === 'mortar' ? mortarWeapon(recoil, m) : needleWeapon(recoil, m);
  const muzzle = group(recoil, 'Muzzle attachment', 0, 0, weapon.muzzleZ);
  if (type === 'mortar') pitch.rotation.x = -.56;
  const model = { type, group: root, yaw, pitch, recoil, muzzle, core: weapon.core, materials: m,
    radius: type === 'mortar' ? 1.24 : 1.14, hitHeight: 1.9 };
  root.userData = { assetType: 'tower', towerType: type, forward: '+Z', origin: 'ground',
    controls: ['Azimuth rotation.rotation.y', 'Weapon elevation.rotation.x', 'Recoil assembly.position.z'] };
  if (batch) batchStaticComponents(root, [yaw, pitch, recoil, muzzle, weapon.core]);
  return model;
}

export function updateTowerVisual(model, time, firePulse = 0) {
  const pulse = Math.max(0, Math.min(1, firePulse));
  model.recoil.position.z = -pulse * (model.type === 'mortar' ? .22 : model.type === 'needle' ? .105 : .025);
  model.materials.core.emissiveIntensity = model.type === 'prism'
    ? .88 + .04 * Math.sin(time * 2.8) + pulse * .36
    : 1.7 + .10 * Math.sin(time * 2.8) + pulse * 1.6;
  model.materials.trim.emissiveIntensity = .60 + pulse * .4;
}

function articulatedLeg(parent, m, side, front, size, type, index) {
  const hip = group(parent, `Leg ${index + 1} hip`, side * size * .40, -.05, front * size * .43);
  const reach = size * .40;
  sphere(hip, size * .115, m.metal, 'Ball joint');
  const kneePoint = [side * reach, -.18 * size, front * size * .12];
  bar(hip, [0, 0, 0], kneePoint, size * .115, m.ceramic, 'Upper armored strut');
  const knee = group(hip, 'Knee joint', ...kneePoint);
  sphere(knee, size * .125, m.dark, 'Knee bearing');
  const shinPoint = [side * size * .10, -size * .44, -front * size * .07];
  bar(knee, [0, 0, 0], shinPoint, size * .087, m.metal, 'Lower leg');
  const foot = box(knee, size * .29, size * .10, size * .36, m.dark, 'Ground contact foot',
    shinPoint[0], shinPoint[1] - .02, shinPoint[2]);
  box(hip, size * .06, size * .08, size * .24, m.trim, 'Hip status light', side * size * .04, size * .04, 0, .008);
  return { hip, knee, foot, side, front, phase: (side * front > 0 ? 0 : Math.PI) + index * .22,
    restY: hip.position.y, restZ: hip.position.z };
}

function runnerBody(body, m) {
  const triangle = new THREE.Shape();
  triangle.moveTo(0, .61); triangle.lineTo(-.59, -.36); triangle.lineTo(.59, -.36); triangle.closePath();
  const shellGeometry = geometry('runner:triangle', () => {
    const g = new THREE.ExtrudeGeometry(triangle, { depth: .47, bevelEnabled: true, bevelThickness: .05,
      bevelSize: .05, bevelSegments: 1, steps: 1 });
    g.translate(0, 0, -.235); return g;
  });
  const shell = solid(body, shellGeometry, m.dark, 'Triangular runner shell', 0, .04);
  shell.rotation.x = -.13;
  const plate = polygonPlate(body, .46, .09, 3, m.metal, 'Triangular front armor', 0, .025, .34);
  plate.rotation.z = Math.PI;
  const corePlate = polygonPlate(body, .30, .05, 3, m.core, 'Magenta triangular reactor', 0, .025, .414);
  corePlate.rotation.z = Math.PI;
  const iris = polygonPlate(body, .15, .06, 3, m.dark, 'Triangular core inset', 0, .025, .46);
  iris.rotation.z = Math.PI;
  const core = sphere(body, .07, m.white, 'Runner reactor center', 0, .025, .508);
  for (const side of [-1, 1]) {
    const fin = box(body, .17, .56, .40, m.ceramic, 'Triangular side shoulder', side * .42, -.02, -.05);
    fin.rotation.z = side * -.50;
    box(body, .075, .12, .18, m.trim, 'Rear motive light', side * .28, -.1, -.33, .012);
  }
  diamond(body, .25, .32, .07, m.trim, 'Rear reactor vent', 0, .16, -.325);
  return core;
}

function bulwarkBody(body, m) {
  const shell = polygonPlate(body, .78, .73, 6, m.dark, 'Hexagonal armored chassis', 0, .16);
  const armor = polygonPlate(body, .83, .16, 6, m.metal, 'Front hex armor rim', 0, .16, .41);
  polygonPlate(body, .65, .15, 6, m.ceramic, 'Front ceramic reinforcement', 0, .16, .505);
  polygonPlate(body, .52, .13, 6, m.dark, 'Recessed core housing', 0, .16, .60);
  const core = polygonPlate(body, .395, .09, 6, m.core, 'Magenta hex reactor', 0, .16, .69);
  polygonPlate(body, .245, .055, 6, m.dark, 'Inner reactor housing', 0, .16, .755);
  polygonPlate(body, .15, .07, 6, m.white, 'Inner reactor light', 0, .16, .80);
  polygonPlate(body, .68, .13, 6, m.metal, 'Rear hex armor', 0, .16, -.47);
  polygonPlate(body, .31, .10, 6, m.trim, 'Rear reactor exhaust', 0, .16, -.557);
  polygonPlate(body, .24, .10, 6, m.dark, 'Rear exhaust recess', 0, .16, -.617);
  for (let i = 0; i < 6; i++) {
    const a = i * Math.PI / 3;
    const plate = box(body, .32, .15, .72, i % 2 === 0 ? m.ceramic : m.metal,
      'Segmented perimeter armor', Math.sin(a) * .72, Math.cos(a) * .72 + .16, -.01);
    plate.rotation.z = -a;
    const seam = box(body, .07, .065, .30, m.trim, 'Armor energy seam',
      Math.sin(a) * .78, Math.cos(a) * .78 + .16, -.03, .008);
    seam.rotation.z = -a;
  }
  box(body, .43, .13, .36, m.dark, 'Top reactor service panel', 0, 1.0, -.025);
  return core;
}

export function createEnemy(type, { batch = true } = {}) {
  if (!['runner', 'bulwark'].includes(type)) throw new Error(`Unknown enemy: ${type}`);
  const root = group(null, type === 'runner' ? 'Runner enemy' : 'Brute enemy');
  const m = palette(PALETTE.hostile);
  for (const [name, mat] of Object.entries(m)) mat.name = `${type === 'runner' ? 'runner' : 'brute'} / ${name}`;
  m.white.color.setHex(0xffb4dd); m.white.emissive.setHex(0xff90c7); m.white.emissiveIntensity = 2.0;
  const bodyY = type === 'runner' ? .61 : .82;
  const body = group(root, 'Suspended chassis', 0, bodyY);
  const core = type === 'runner' ? runnerBody(body, m) : bulwarkBody(body, m);
  const legs = [];
  const positions = type === 'runner' ? [-1, 1] : [-1, 0, 1];
  for (const side of [-1, 1]) for (const front of positions)
    legs.push(articulatedLeg(body, m, side, front, type === 'runner' ? .75 : 1.10, type, legs.length));
  root.userData = { assetType: 'enemy', enemyType: type, forward: '+Z', origin: 'ground',
    animation: 'Articulated hip and knee gait with sprung chassis, driven by path speed.' };
  if (batch) batchStaticComponents(root, [body, core, ...legs.flatMap(leg => [leg.hip, leg.knee, leg.foot])]);
  return { type, group: root, body, core, legs, materials: m, bodyY,
    radius: type === 'runner' ? .94 : 1.38, hitHeight: type === 'runner' ? .65 : 1.05 };
}

export function updateEnemyVisual(model, time, speed = 1, hitPulse = 0) {
  const movement = Math.min(1, Math.max(0, speed));
  const rate = model.type === 'runner' ? 13.0 : 5.8;
  const amplitude = model.type === 'runner' ? .29 : .19;
  for (const leg of model.legs) {
    const phase = time * rate + leg.phase;
    leg.hip.rotation.x = Math.sin(phase) * amplitude * movement;
    leg.hip.rotation.z = Math.max(0, Math.cos(phase)) * .14 * leg.side * movement;
    leg.knee.rotation.x = Math.max(0, Math.sin(phase)) * .24 * movement;
  }
  const pulse = Math.max(0, Math.min(1, hitPulse));
  model.body.position.y = model.bodyY + Math.sin(time * rate * 2) * (model.type === 'runner' ? .024 : .016) * movement;
  model.body.rotation.z = Math.sin(time * rate) * .028 * movement;
  model.body.rotation.x = -.09 * pulse;
  // The sprung chassis follows its lowest planted foot throughout the gait.
  model.body.updateMatrix();
  let supportHeight = Infinity;
  for (const leg of model.legs) {
    leg.hip.updateMatrix(); leg.knee.updateMatrix(); leg.foot.updateMatrix();
    footMatrix.copy(model.body.matrix).multiply(leg.hip.matrix).multiply(leg.knee.matrix).multiply(leg.foot.matrix);
    if (!leg.foot.geometry.boundingBox) leg.foot.geometry.computeBoundingBox();
    const bounds = leg.foot.geometry.boundingBox;
    for (let corner = 0; corner < 8; corner++) {
      footCorner.set(corner & 1 ? bounds.max.x : bounds.min.x,
        corner & 2 ? bounds.max.y : bounds.min.y, corner & 4 ? bounds.max.z : bounds.min.z).applyMatrix4(footMatrix);
      supportHeight = Math.min(supportHeight, footCorner.y);
    }
  }
  model.body.position.y += .012 - supportHeight;
  model.materials.core.emissiveIntensity = 1.5 + .17 * Math.sin(time * 3.0) + pulse * 2.5;
  model.materials.metal.emissive.setHex(PALETTE.hostile);
  model.materials.metal.emissiveIntensity = pulse * .75;
  model.materials.ceramic.emissive.setHex(0xff93bd);
  model.materials.ceramic.emissiveIntensity = pulse * .65;
}

export const modelPalette = Object.freeze({ ...PALETTE });
