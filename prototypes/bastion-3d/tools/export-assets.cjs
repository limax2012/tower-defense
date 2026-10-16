const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs = require('node:fs/promises');
const path = require('node:path');

(async () => {
  const output = path.resolve(__dirname, '../assets');
  const url = process.argv[2] || 'http://127.0.0.1:5183/?capture';
  await fs.mkdir(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, channel: 'msedge',
    args: ['--enable-webgl', '--ignore-gpu-blocklist'] });
  const warnings = [];
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    page.on('pageerror', error => warnings.push(error.message));
    page.on('console', message => {
      if (['error', 'warning'].includes(message.type())) warnings.push(message.text());
    });
    await page.goto(url, { waitUntil: 'networkidle' });
    await page.waitForFunction(() => window.poc?.ready, { timeout: 60000 });
    const sceneBytes = await page.evaluate(async () => {
      const THREE = await import('three');
      window.poc.seek(8);
      const lights = [];
      window.poc.scene.traverse(object => {
        if (object.isDirectionalLight || object.isSpotLight) lights.push(object);
        if (object.isHemisphereLight) object.visible = false;
        for (const material of object.material ? [].concat(object.material) : [])
          if (!material.name) material.name = `${object.name || 'Cinderworks' } / ${material.type}`;
      });
      for (const light of lights) {
        light.lookAt(light.target.getWorldPosition(new THREE.Vector3()));
        const target = new THREE.Object3D(); target.name = `${light.name} / direction`;
        target.position.set(0, 0, -1); light.add(target); light.target = target;
      }
      window.poc.scene.updateMatrixWorld(true);
      return Array.from(new Uint8Array(await window.poc.exportGLB()));
    });
    const sceneFile = path.join(output, 'cinderworks-scene.glb');
    await fs.writeFile(sceneFile, Buffer.from(sceneBytes));

    const catalogBytes = await page.evaluate(async () => {
      const THREE = await import('three');
      const { GLTFExporter } = await import('three/addons/exporters/GLTFExporter.js');
      const { createTower, createEnemy, updateEnemyVisual } = await import('./src/models.js');
      const scene = new THREE.Scene();
      scene.name = 'Maximal Bastion / Editable model library';
      const towers = ['needle', 'prism', 'mortar'].map(type => createTower(type, { batch: false }));
      const enemies = ['runner', 'bulwark'].map(type => createEnemy(type, { batch: false }));
      [...towers, ...enemies].forEach((model, index) => {
        model.group.position.x = (index - 2) * 5;
        scene.add(model.group);
      });
      const stageMaterial = new THREE.MeshStandardMaterial({ color: 0x182330, roughness: .65, metalness: .35 });
      stageMaterial.name = 'Library / dark display deck';
      const stage = new THREE.Mesh(new THREE.BoxGeometry(25, .16, 5), stageMaterial);
      stage.name = 'Model library / display deck';
      stage.position.y = -.08;
      scene.add(stage);
      const key = new THREE.DirectionalLight(0xc8e2ff, 3);
      key.name = 'Library / cool key'; key.position.set(2, 10, 8); scene.add(key);
      const rim = new THREE.DirectionalLight(0xffb981, 1.5);
      rim.name = 'Library / warm rim'; rim.position.set(-8, 8, -6); scene.add(rim);
      for (const light of [key, rim]) {
        light.lookAt(0, 0, 0);
        const target = new THREE.Object3D(); target.position.set(0, 0, -1);
        light.add(target); light.target = target;
      }
      const tracks = [];
      for (const model of towers) {
        const times = [0, 1, 2, 3, 4];
        const rotations = [];
        for (const angle of [0, .45, 0, -.45, 0])
          rotations.push(...new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), angle).toArray());
        tracks.push(new THREE.QuaternionKeyframeTrack(`${model.yaw.uuid}.quaternion`, times, rotations));
        tracks.push(new THREE.VectorKeyframeTrack(`${model.recoil.uuid}.position`, [0, .1, .35, 1, 1.1, 1.35, 2, 2.1, 2.35, 3, 3.1, 3.35, 4],
          [0,0,0, 0,0,-.15, 0,0,0, 0,0,0, 0,0,-.15, 0,0,0, 0,0,0, 0,0,-.15, 0,0,0, 0,0,0, 0,0,-.15, 0,0,0, 0,0,0]));
      }
      for (const model of enemies) {
        const parts = [model.body, ...model.legs.flatMap(leg => [leg.hip, leg.knee])];
        const samples = parts.map(part => ({ part, rotation: [], position: [] }));
        const times = [];
        for (let i = 0; i <= 96; i++) {
          const time = i / 24; times.push(time); updateEnemyVisual(model, time, 1, 0);
          for (const sample of samples) {
            sample.rotation.push(...sample.part.quaternion.toArray());
            sample.position.push(...sample.part.position.toArray());
          }
        }
        for (const sample of samples) {
          tracks.push(new THREE.QuaternionKeyframeTrack(`${sample.part.uuid}.quaternion`, times, sample.rotation));
          tracks.push(new THREE.VectorKeyframeTrack(`${sample.part.uuid}.position`, times, sample.position));
        }
        updateEnemyVisual(model, 0, 1, 0);
      }
      const animation = new THREE.AnimationClip('Model inspection / aiming recoil and walking', 4, tracks);
      scene.updateMatrixWorld(true);
      const buffer = await new GLTFExporter().parseAsync(scene, {
        binary: true, onlyVisible: true, animations: [animation], maxTextureSize: 1024,
      });
      return Array.from(new Uint8Array(buffer));
    });
    const catalogFile = path.join(output, 'tower-and-enemy-models.glb');
    await fs.writeFile(catalogFile, Buffer.from(catalogBytes));
    const report = { scene: { file: sceneFile, bytes: sceneBytes.length },
      catalog: { file: catalogFile, bytes: catalogBytes.length }, warnings };
    await fs.writeFile(path.join(output, 'export-report.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report, null, 2));
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
