import assert from 'node:assert/strict';
import {createWorld,PIXEL_LED_QUAD,PIXEL_STAGE_DECK,PIXEL_STAGE_LAMPS,pixelLedCoverFit,pixelStageLightingActive,pixelStageLightingEnded} from './WebGLTemplate/FastGame/world.js';

const near=(a,b)=>assert(Math.abs(a-b)<1e-9,`${a} differs from ${b}`);
assert.deepEqual(PIXEL_LED_QUAD,[[665,117],[842,163],[842,230],[665,184]]);
for(const [iw,ih]of[[600,600],[600,900],[1600,900],[2400,600]]){
  const f=pixelLedCoverFit(iw,ih),[a,b,c,d]=f.transform;
  near(f.width/f.height,iw/ih);
  near(Math.hypot(a,b),1);near(Math.hypot(c,d),1);
  near(f.width*Math.hypot(a,b)/(f.height*Math.hypot(c,d)),iw/ih);
  near(f.x*2+f.width,f.planeWidth);near(f.y*2+f.height,f.planeHeight);
  assert(f.x>=0&&f.y>=0&&f.width<=f.planeWidth+1e-9&&f.height<=f.planeHeight+1e-9);
}
assert.equal(pixelLedCoverFit(0,1),null);assert.equal(pixelLedCoverFit(Infinity,10),null);
assert.equal(PIXEL_STAGE_LAMPS.length,5);assert.equal(PIXEL_STAGE_DECK.length,6);
for(const phase of['lobby','planning','guide','playing'])assert.equal(pixelStageLightingEnded({phase,paused:true,journey:{session:0,secondsLeft:180}}),false);
assert.equal(pixelStageLightingEnded({phase:'lobby',journey:{session:0,secondsLeft:0}}),false,'Initial reference artwork must remain untouched.');
assert(pixelStageLightingEnded({phase:'closing'}));assert(pixelStageLightingEnded({phase:'results'}));
assert(pixelStageLightingEnded({phase:'playing',journey:{session:1,secondsLeft:0,musicPaused:false}}));
for(const paused of[false,true])assert(pixelStageLightingActive({phase:'playing',paused,journey:{secondsLeft:90}}));
for(const state of[
  {phase:'closing',journey:{musicPaused:false,secondsLeft:0}},
  {phase:'results',journey:{musicPaused:false}},
  {phase:'playing',journey:{floorClosing:true,musicPaused:false}},
  {phase:'playing',journey:{secondsLeft:0}},
  {phase:'playing',journey:{afterShow:{started:true}}},
  {phase:'lobby'}, {phase:'planning'}, {phase:'guide'}
])assert.equal(pixelStageLightingActive(state),false);

function canvas(){
  const obj={draws:[],transforms:[],gradients:0,composites:[],fills:[],fill(){this.fills.push(this.fillStyle);},createLinearGradient(){this.gradients++;return{addColorStop(){}};},drawImage(...args){this.draws.push(args);},transform(...args){this.transforms.push(args);}};
  const ctx=new Proxy(obj,{get(o,k){return k in o?o[k]:(()=>{});},set(o,k,v){if(k==='globalCompositeOperation')o.composites.push(v);o[k]=v;return true;}});
  const value={width:960,height:558,style:{},getContext(){return ctx;},getBoundingClientRect(){return{width:960,height:558};},ctx};ctx.canvas=value;return value;
}
globalThis.document={createElement:()=>canvas()};
globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const venue={id:'venue',width:960,height:558},covers={},target=canvas();
const world=createWorld(target,{assets:{venue,albumCovers:covers,spriteMeta:{roles:{guest:{}},atlases:{}},venueMeta:{occluders:[]}}});
const paint=(state={})=>{Object.assign(target.ctx,{draws:[],transforms:[],gradients:0,composites:[],fills:[]});world.render(0,{phase:'playing',actors:[],playlist:0,journey:{secondsLeft:90},...state});return target.ctx;};
assert.equal(paint().draws.filter(a=>a[0]!==venue&&a[0].id).length,0,'Missing optional art never gates the first frame.');
for(let i=0;i<5;i++)covers[i]={id:'original-'+i,width:i===1?600:900,height:i===1?900:900};
for(let i=0;i<5;i++){
  const ctx=paint({playlist:i}),draw=ctx.draws.find(a=>a[0]===covers[i]);
  assert(draw,'The current track chooses its own real decoded cover.');
  assert.equal(ctx.draws.filter(a=>Object.values(covers).includes(a[0])).length,1);
  assert.deepEqual(draw.slice(1,5),[0,0,covers[i].width,covers[i].height],'The renderer uses the entire original image; no crop.');
  assert.equal(ctx.gradients,4);assert(ctx.composites.includes('screen'));
  assert(!ctx.fills.includes('rgba(5,9,18,0.72)'),'An unfinished show keeps the original stage illumination.');
  assert(ctx.draws.some(a=>a[0]===venue),'Existing venue/bar ambience remains.');
}
for(const phase of['closing','results'])for(const musicPaused of[false,true]){
  const ctx=paint({phase,journey:{floorClosing:phase==='closing',secondsLeft:0,musicPaused}});
  assert.equal(ctx.gradients,0);assert(!ctx.composites.includes('screen'),'An ended show must have no beam/pool/flare pass even after music replay.');
  assert(ctx.fills.includes('rgba(5,9,18,0.72)'),'An ended show also dims baked stage illumination.');
  assert(ctx.draws.some(a=>a[0]===venue));assert(ctx.draws.some(a=>a[0]===covers[0]),'Paused or resumed music retains the selected jacket.');
}
const paused=paint({paused:true});assert.equal(paused.gradients,4,'Pausing an unfinished performance freezes its lighting instead of ending it.');
const pending={id:'pending',width:500,height:500,complete:false,naturalWidth:500};covers[0]=pending;
assert(!paint().draws.some(a=>a[0]===pending),'Loading art leaves the authored LED until decode completes.');
pending.complete=true;assert(paint().draws.some(a=>a[0]===pending),'A late decoded cover can appear without rebuilding the world.');
const overview=canvas();world.renderOverview(overview.ctx,{playlist:2,actors:[]});assert(overview.ctx.draws.some(a=>a[0]===covers[2]));
world.destroy();
console.log('PASS: stage effects stop after the performance; five cover selections retain full sources, centered contain and equal-length affine bases; lazy art never gates startup.');
