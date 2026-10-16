const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
const assert=require('node:assert/strict');
(async()=>{
  const browser=await chromium.launch({headless:true,channel:'msedge'});
  const page=await browser.newPage({viewport:{width:1440,height:900}});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto('http://127.0.0.1:5183/?capture');await page.waitForFunction(()=>window.poc?.ready);
  const results=await page.evaluate(()=>{
    poc.reset();poc.step(35);
    const stats=poc.stats();
    const finite=[];poc.scene.traverse(o=>{if(!o.position.toArray().every(Number.isFinite)||!o.quaternion.toArray().every(Number.isFinite))finite.push(o.name);});
    const towers=poc.combat.towers.map(t=>({type:t.type,shots:t.shots,target:t.target?.type}));
    const positions=[];for(let i=0;i<=56;i++){poc.rig.tourAt(i*.5);positions.push(poc.camera.position.toArray());}
    const collisionViolations=positions.filter(p=>p[1]<.49||poc.environment.colliders.some(box=>box.containsPoint({x:p[0],y:p[1],z:p[2]})));
    return {stats,finite,towers,collisionViolations,tourSamples:positions.length};
  });
  assert.equal(errors.length,0,errors.join('\n'));assert.equal(results.finite.length,0,'Non-finite scene transforms');
  for(const type of ['needle','prism','mortar'])assert.ok(results.stats.telemetry[`${type}Hits`]>0,`${type} must damage enemies`);
  assert.ok(results.stats.telemetry.mortarMultiHits>0,'Mortar must demonstrate area damage');
  assert.ok(results.stats.kills>=3,'Combat must show multiple defeats');
  assert.ok(results.stats.telemetry.turns>0,'Enemies must navigate bends');
  assert.equal(results.collisionViolations.length,0,'Tour camera must stay outside colliders');
  await page.click('#pause');assert.equal(await page.textContent('#pause'),'Resume');
  await page.click('#free');assert.equal(await page.evaluate(()=>poc.rig.mode),'free');
  await page.click('#orbit');assert.equal(await page.evaluate(()=>poc.rig.mode),'orbit');
  await page.click('#restart');assert.ok(await page.evaluate(()=>poc.combat.time<.1));
  const output=path.resolve(__dirname,'../.artifacts/review');
  await fs.mkdir(output,{recursive:true});
  await fs.writeFile(path.join(output,'validation.json'),JSON.stringify({...results,errors},null,2));
  console.log(JSON.stringify({...results,errors}));await browser.close();
})().catch(error=>{console.error(error);process.exitCode=1;});
