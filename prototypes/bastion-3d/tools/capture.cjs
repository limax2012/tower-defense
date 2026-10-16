const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
const {spawn}=require('node:child_process');
const {once}=require('node:events');
(async()=>{
  const root=path.resolve(__dirname,'..'),out=path.join(root,'renders');await fs.mkdir(out,{recursive:true});
  const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-webgl','--ignore-gpu-blocklist']});
  const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5183/?capture');await page.waitForFunction(()=>window.poc?.ready);
  await page.evaluate(()=>{poc.seek(8);poc.setView('overview');});
  await page.screenshot({path:path.join(out,'00-interactive-scene.png')});
  await page.evaluate(()=>poc.setUI(false));
  for(const [name,view,time] of [['01-cinderworks-overview','overview',8],['02-ground-combat','ground',6.2],['03-prism-rear-and-flank','rear',6],['04-route-overhead','overhead',13]]){
    await page.evaluate(({view,time})=>{poc.seek(time);poc.setView(view);},{view,time});
    await page.screenshot({path:path.join(out,`${name}.png`)});
  }
  await page.evaluate(()=>{poc.seek(11.1);poc.rig.tourAt(15);poc.render();});
  await page.screenshot({path:path.join(out,'05-mortar-bombardment.png')});
  console.log('Six stills captured.');
  if(process.argv.includes('--stills-only')){await browser.close();return;}
  const movie=path.join(out,'Cinderworks-3D-Combat-Tour.mp4');
  const encoder=spawn('ffmpeg',['-hide_banner','-loglevel','error','-y','-f','image2pipe','-framerate','30','-vcodec','mjpeg','-i','pipe:0','-an','-c:v','libx264','-preset','medium','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',movie]);
  let encoderError='';encoder.stderr.on('data',b=>encoderError+=b.toString());
  encoder.on('error',e=>{throw e;});
  await page.evaluate(()=>{poc.reset();poc.step(4);poc.setUI(false);});
  const keyframes=new Set([0,90,180,270,360,450,540,630,720,810]);
  const review=path.join(root,'.artifacts/video-review');await fs.mkdir(review,{recursive:true});
  for(let i=0;i<840;i++){
    await page.evaluate(i=>{poc.step(1/30);poc.rig.tourAt(i/30);poc.render();},i);
    const frame=await page.screenshot({type:'jpeg',quality:97});
    if(keyframes.has(i))await page.screenshot({path:path.join(review,`frame-${String(i).padStart(4,'0')}.png`)});
    if(!encoder.stdin.write(frame))await once(encoder.stdin,'drain');
    if(i%90===0)console.log(`Video ${Math.round(i/840*100)}% (${i}/840 frames)`);
  }
  encoder.stdin.end();const [code]=await once(encoder,'close');if(code!==0)throw new Error(encoderError);
  const stats=await page.evaluate(()=>poc.stats());
  await fs.writeFile(path.join(out,'capture-report.json'),JSON.stringify({width:1920,height:1080,fps:30,duration:28,frames:840,stats,errors},null,2));
  await browser.close();console.log(JSON.stringify({movie,stats,errors}));
})().catch(e=>{console.error(e);process.exitCode=1;});
