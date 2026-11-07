import assert from 'node:assert/strict';
import {createWorld,pixelGroundPaper,pixelPaperBurst,PIXEL_PAPER_COLORS} from './WebGLTemplate/FastGame/world.js';

const floor={confettiThrows:1,tissueThrows:1,litter:56,cleaned:0};
const ground=pixelGroundPaper(floor),half=pixelGroundPaper({...floor,cleaned:28});
assert.equal(ground.filter(p=>p.kind===0).length,24);assert.equal(ground.filter(p=>p.kind===1).length,16);
assert.equal(half.length,20);assert(half.every(p=>ground.some(a=>JSON.stringify(a)===JSON.stringify(p))),'Cleaning removes stable marks instead of redistributing the litter.');
assert.equal(pixelGroundPaper({...floor,cleaned:56}).length,0);
assert.equal(pixelGroundPaper({confettiThrows:2,tissueThrows:2,litter:112,cleaned:0}).length,80);
assert.equal(pixelGroundPaper({}).length,0);
for(const kind of[0,1]){
  const particles=pixelPaperBurst({kind,serial:3});assert.equal(particles.length,kind?28:56);
  assert(particles.every(p=>p.kind===kind&&p.y>=2.6&&p.age===0));
  assert.deepEqual(particles,pixelPaperBurst({kind,serial:3}));
  if(!kind)assert.equal(new Set(particles.map(p=>p.color)).size,4);
}

function canvas(){const context=new Proxy({rects:[],draws:[],fillRect(...args){this.rects.push([this.fillStyle,...args]);},drawImage(...args){this.draws.push(args);},createLinearGradient(){return{addColorStop(){}};}},{get(o,key){return key in o?o[key]:(()=>{});}});const value={width:960,height:558,style:{},getContext(){return context;},getBoundingClientRect(){return{width:960,height:558};},ctx:context};context.canvas=value;return value;}
globalThis.document={createElement:()=>canvas()};globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const assets={venue:{width:960,height:558},spriteMeta:{roles:{guest:{}},atlases:{}},venueMeta:{occluders:[]}};
const target=canvas(),world=createWorld(target,{assets});
const base={phase:'closing',paused:false,journey:{session:1},floor,actors:[],elapsed:100,effects:[{kind:0,serial:1,time:100},{kind:1,serial:2,time:100}]};
const render=(time,state=base)=>{target.ctx.rects=[];world.render(time,state);return target.ctx.rects.slice();};
const air=rects=>rects.filter(r=>r[0]==='#fff7e8'&&r[3]===3&&r[4]===2);
let rects=render(0);assert(air(rects).length>=100,'Airborne tissue has visibly connected segments.');
assert(rects.filter(r=>PIXEL_PAPER_COLORS.includes(r[0])).length===80,'All56 airborne confetti and24 ground confetti are rendered.');
render(100);const paused=render(200,{...base,paused:true});assert.deepEqual(render(2000,{...base,paused:true}),paused,'Global pause freezes all paper pixels.');
for(let t=2100;t<10100;t+=100)rects=render(t);
assert.equal(air(rects).length,0,'Airborne paper lands instead of looping a burst.');
assert.equal(rects.filter(r=>r[0]==='#e8e2d5').length,48,'Ground tissue remains after the flying streamers land.');
assert.equal(render(10200,{...base,journey:{session:2},floor:{},effects:[]}).filter(r=>r[0]==='#e8e2d5'||PIXEL_PAPER_COLORS.includes(r[0])).length,0);
world.destroy();
const saved=canvas(),restored=createWorld(saved,{assets});
restored.render(0,{...base,elapsed:110,effects:[{kind:0,serial:1,time:100},{kind:1,serial:2,time:100}]});
assert.equal(air(saved.ctx.rects).length,0,'A persisted old throw restores litter without replaying a burst.');
assert(saved.ctx.rects.some(r=>r[0]==='#e8e2d5'));
restored.destroy();
const importedCanvas=canvas(),importedWorld=createWorld(importedCanvas,{assets});
importedWorld.render(0,base);assert(air(importedCanvas.ctx.rects).length>0);
importedCanvas.ctx.rects=[];importedWorld.render(100,{...base,floor:{...floor,cleaned:7},effects:[]});
assert.equal(air(importedCanvas.ctx.rects).length,0,'A same-session imported floor resets the old transient pool.');
assert(importedCanvas.ctx.rects.some(r=>r[0]==='#e8e2d5'),'The imported floor keeps its persisted paper debt.');
importedWorld.destroy();
console.log('PASS: larger confetti and fluttering tissue, bounded one-shot flights, debt-scaled stable debris, global pause and saved-litter restoration.');
