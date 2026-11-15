import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createWorld,pixelAudiencePose,pixelDrunkFightPose,pixelFestivalFrame,pixelFacing,pixelDirection} from './WebGLTemplate/FastGame/world.js';

const read=name=>JSON.parse(readFileSync(new URL('./WebGLTemplate/FastGame/art/'+name,import.meta.url)));
const spriteMeta=read('sprites.json'),festivalModels=read('festival-models.json');
const state={phase:'playing',owner:2,journey:{secondsLeft:120},paused:false};
const actor=(slot=0,time=0)=>({id:'patron-'+slot,role:'guest',slot,x:2,z:-1,y:0,visible:true,moving:false,
  audienceVersion:1,action:'fight',drunkIncident:'fight',fightIndex:slot%5,fightElapsed:time,fightRemaining:4-time,
  fightPartner:'opponent',festivalAction:'horns',festivalRemaining:5});
for(let slot=0;slot<8;slot++){
  const frames=new Set();
  for(const time of [0,.08,.16,.24,.32,.4]){
    const a=actor(slot,time),saved=JSON.stringify(a),pose=pixelDrunkFightPose(a,state,99);
    assert.equal(pose.kind,'fight');assert.equal(pose.action,'highfive');assert.equal(pose.cheer,false);
    frames.add(pose.frameOverride);assert.equal(JSON.stringify(a),saved,'Rendering does not change physical actors');
    assert.equal(pixelAudiencePose(a,state,99).kind,'fight');
    assert.equal(pixelFestivalFrame(a,state,99,festivalModels),null,'A previous crowd action cannot override fighting');
  }
  assert(frames.size>=3,'Guard and extended-arm frames animate for every outfit');
}
for(const a of [{...actor(),visible:false},{...actor(),drunkIncident:'exit',action:'exit'},{...actor(),drunkIncident:null,action:'listen'}])assert.equal(pixelDrunkFightPose(a,state,1),null);
const frozen={...state,inspection:{freezePerformers:true}};
assert.equal(pixelDrunkFightPose(actor(),frozen,1),null);
assert.equal(pixelAudiencePose(actor(),frozen,1).kind,'inspection');
const opponent={id:'opponent',x:-2,z:2};
assert.equal(pixelFacing(actor(),'guest','highfive',{...state,actors:[opponent]}),pixelDirection(-4,3));

function context(){return new Proxy({draws:[],createLinearGradient(){return{addColorStop(){}};},drawImage(...args){this.draws.push(args);}},{get:(o,k)=>k in o?o[k]:(()=>{})});}
function canvas(){const ctx=context(),c={width:960,height:558,style:{},getContext:()=>ctx,getBoundingClientRect:()=>({width:960,height:558}),ctx};ctx.canvas=c;return c;}
globalThis.document={createElement:()=>canvas()};globalThis.ResizeObserver=class{observe(){}disconnect(){}};
const png=file=>{const b=readFileSync(new URL('./WebGLTemplate/FastGame/art/'+file,import.meta.url));return{id:file,width:b.readUInt32BE(16),height:b.readUInt32BE(20)};};
const atlases=Object.fromEntries(Object.entries(spriteMeta.atlases).map(([k,v])=>[k,png(v.file)]));
const festivalAtlases=Object.fromEntries(Object.entries(festivalModels.atlases).map(([k,v])=>[k,png(v.file)]));
const target=canvas(),world=createWorld(target,{assets:{venue:{width:960,height:558},spriteMeta,atlases,festivalModels,festivalAtlases,venueMeta:{occluders:[]}}});
for(let slot=0;slot<8;slot++){
  const a=actor(slot,.3);target.ctx.draws=[];world.render(100+slot*100,{...state,actors:[a,opponent]});
  const drawn=target.ctx.draws.find(d=>d[0]===atlases.guestSocial);
  assert(drawn,'Fight uses authored directional arms');assert.equal(Math.floor(drawn[2]/256),slot,'Original wardrobe row is retained');
  assert(!target.ctx.draws.some(d=>Object.values(festivalAtlases).includes(d[0])));
}
const fixed=actor(0,1),paused={...state,paused:true,actors:[fixed]};
world.render(1000,paused);const before=world.getAudiencePresentation(paused);world.render(5000,paused);assert.deepEqual(world.getAudiencePresentation(paused),before);
world.destroy();
console.log('PASS: eight original outfits, directional guard/jab/recoil frames, fight priority, inspection freeze, pause stability, exit cleanup and render-only state.');
