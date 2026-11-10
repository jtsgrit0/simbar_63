import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createWorld,pixelFestivalPose,pixelFestivalFrame,pixelFacing,pixelDirection} from './WebGLTemplate/FastGame/world.js';

const state={phase:'playing',owner:1,journey:{secondsLeft:150},paused:false};
const manifest=JSON.parse(readFileSync(new URL('./WebGLTemplate/FastGame/art/festival-models.json',import.meta.url)));
const styles=Object.keys(manifest.styles);
const actor=(id,slot=0)=>({id:'festival-'+slot,slot,role:'guest',audienceVersion:1,x:2,z:-1,y:0,action:'listen',visible:true,moving:false,
  festivalAction:id,festivalRemaining:7,festivalDuration:8,behaviorPhase:0,animationSpeed:1});
for(const id of ['slam','mosh','surf','jump','horns','airguitar','sing','encore','headbang','dance']){
  const a=actor(id);if(id==='surf'){a.festivalLead=true;a.festivalLiftReady=true;a.y=1.2;}
  const before=JSON.stringify(a),pose=pixelFestivalPose(a,state,1);
  assert.equal(pose.festivalAction,id);assert.equal(JSON.stringify(a),before);
  if(id==='jump')assert(pixelFestivalPose(a,state,.3).lift>0);
  assert.equal(pixelFestivalPose({...a,moving:true},state,1),null);
  assert.equal(pixelFestivalPose({...a,action:'queue'},state,1),null);
  assert.equal(pixelFestivalPose(a,{...state,phase:'closing'},1),null);
}
for(const [action,definition]of Object.entries(manifest.actions))for(let slot=0;slot<8;slot++){
  const a=actor(action,slot);if(action==='surf'){a.festivalLead=true;a.festivalLiftReady=true;a.y=1.2;}
  const frames=new Set([0,.2,.4,.6,.8,1].map(time=>{
    const p=pixelFestivalFrame(a,state,time,manifest);
    assert.equal(p.row,slot);assert.equal(p.style,styles[slot]);assert.equal(p.atlas,definition.atlas);
    return p.column;
  }));
  assert.equal(frames.size,4,action+' must animate four authored cells');
}
assert.equal(pixelFestivalPose({...actor('surf'),festivalLead:false},state,1).atlasAction,'horns');
assert.equal(pixelFestivalPose({...actor('surf'),festivalSupport:true},state,1).action,'support');
assert.equal(pixelFestivalPose({...actor('surf'),festivalLead:true,y:0},state,1).atlasAction,'horns','A forming surf group keeps its leader upright until actual contact');
assert.equal(pixelFestivalPose({...actor('horns'),role:'seated',waitingSeat:true},state,1).atlasAction,undefined);
assert.equal(pixelFestivalPose({...actor('horns'),festivalRemaining:0,behavior:'dance'},state,1).atlasAction,'dance');
assert.equal(pixelFacing({waitingSeat:true,facing:-Math.PI/2},'seated','idle'),pixelDirection(-1,0));

function context(){return new Proxy({draws:[],rotations:[],translations:[],createLinearGradient(){return{addColorStop(){}};},drawImage(...a){this.draws.push(a);},rotate(a){this.rotations.push(a);},translate(...a){this.translations.push(a);}},{get:(o,k)=>k in o?o[k]:(()=>{})});}
function canvas(){const ctx=context(),value={width:960,height:558,style:{},getContext:()=>ctx,getBoundingClientRect:()=>({width:960,height:558}),ctx};ctx.canvas=value;return value;}
globalThis.document={createElement:()=>canvas()};globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const png=file=>{const b=readFileSync(new URL('./WebGLTemplate/FastGame/art/'+file,import.meta.url));return{id:file,width:b.readUInt32BE(16),height:b.readUInt32BE(20)};};
const spriteMeta=JSON.parse(readFileSync(new URL('./WebGLTemplate/FastGame/art/sprites.json',import.meta.url)));
const atlases=Object.fromEntries(Object.entries(spriteMeta.atlases).map(([k,v])=>[k,png(v.file)]));
const festivalAtlases=Object.fromEntries(Object.entries(manifest.atlases).map(([k,v])=>[k,png(v.file)]));
const target=canvas(),world=createWorld(target,{assets:{venue:{id:'venue',width:960,height:558},spriteMeta,atlases,
  venueMeta:{occluders:[],seatedActors:{'freebird-wall-0':{feet:[250,300],depth:301,hideShadow:true}}},festivalModels:manifest,festivalAtlases}});
let now=0;
for(const action of Object.keys(manifest.actions))for(let slot=0;slot<8;slot++){
  const a=actor(action,slot);if(action==='surf'){a.festivalLead=true;a.festivalLiftReady=true;a.y=1.2;}
  target.ctx.draws=[];target.ctx.rotations=[];world.render(now+=100,{...state,actors:[a]});
  const drawn=target.ctx.draws.find(a=>Object.values(festivalAtlases).includes(a[0]));
  assert(drawn,action+' must draw the actual festival artwork');assert.equal(drawn[2],slot*64);assert.equal(drawn[3],64);assert.equal(drawn[4],64);
  if(action==='surf'){assert.equal(drawn[6],-27);assert(target.ctx.rotations.every(v=>Math.abs(v)<.08),'Surf uses authored horizontal bodies rather than rotated standing drawings');}
}
const jumping=actor('jump'),frozen={...state,actors:[jumping],paused:true};
world.render(now+=100,frozen);const before=world.getAudiencePresentation(frozen);
world.render(now+=500,frozen);assert.deepEqual(world.getAudiencePresentation(frozen),before,'Paused frames and body lift freeze');
const seated={...actor('horns'),id:'freebird-wall-0',role:'seated',styleKey:'Suit',waitingSeat:true};
target.ctx.draws=[];world.render(now+=100,{...state,owner:0,actors:[seated]});
assert(world.getAudiencePresentation({...state,actors:[seated]})[0].seated);
assert(!target.ctx.draws.some(a=>Object.values(festivalAtlases).includes(a[0])),'Waiting guests retain complete seated drawings');
const walking={...seated,role:'guest',moving:true,action:'to-stage'};
assert(!world.getAudiencePresentation({...state,actors:[walking]})[0].seated,'Standing departure releases seat pixel calibration');
world.destroy();
console.log('PASS: ten real crowd poses, seven authored animated actions across eight outfits, horizontal surfing, waiting seats, travel priority and paused frames.');
