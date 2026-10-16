const { chromium }=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
(async()=>{
  const output=path.resolve(process.argv[2]||path.join(__dirname,'../.artifacts/review'));await fs.mkdir(output,{recursive:true});
  const browser=await chromium.launch({headless:true,channel:'msedge',args:['--enable-webgl','--ignore-gpu-blocklist']});
  const page=await browser.newPage({viewport:{width:1600,height:1000},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
  await page.goto('http://127.0.0.1:5183/?capture',{waitUntil:'networkidle'});
  await page.waitForFunction(()=>window.poc?.ready,{timeout:60000});
  await page.evaluate(()=>{poc.seek(6);poc.setUI(false);});
  for(const view of ['overview','ground','rear','overhead']){
    await page.evaluate(view=>poc.setView(view),view);await page.screenshot({path:path.join(output,`${view}.png`)});
  }
  const stats=await page.evaluate(()=>poc.stats());
  await fs.writeFile(path.join(output,'stats.json'),JSON.stringify({stats,errors},null,2));
  console.log(JSON.stringify({stats,errors}));
  await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1;});
