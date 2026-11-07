// Exercise normal complete nights with deterministic random crowd states.
import assert from 'node:assert/strict';
import {writeFileSync,mkdirSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';
const outcomes=[];
const seeds=process.argv.length>2?process.argv.slice(2).map(Number):Array.from({length:20},(_,i)=>i+1);
for(const seed of seeds){
  const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};
  const g=new SimBarGame({storage,seed});g.act('start');g.act('launch');while(g.phase==='guide')g.act('guide');
  let ticks=0,maxStep=0;
  while(g.phase!=='results'&&ticks<8400){
    const before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z}]));
    g.tick(.05);ticks++;
    for(const a of g.actors){const p=before.get(a.id);if(!p)continue;maxStep=Math.max(maxStep,Math.hypot(a.x-p.x,a.z-p.z));}
    if(g.farewellPending)g.act('farewellAck');
    if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  }
  const fans=g.journey.afterShow.audience.map(f=>{
    const a=g.actors.find(a=>a.id===f.id);return{id:f.id,phase:f.phase,distance:Math.hypot(a.x-EXIT_POINT.x,a.z-EXIT_POINT.z),moving:a.moving,visible:a.visible};});
  const result={seed,seconds:ticks*.05,phase:g.phase,maxStep,fans,members:g.journey.afterShow.members,band:g.actors.filter(a=>a.role==='band'),
    floor:g.floor,escort:g.escort,staff:g.actors.filter(a=>g.crew.includes(a.id)),reaction:g.reaction,
    unfinished:g.journey.afterShow.audience.filter(f=>f.phase!=='gone').map(f=>({fan:f,actor:g.actors.find(a=>a.id===f.id)})),
    nearbyBodies:g.crew.map(id=>{const worker=g.actors.find(a=>a.id===id);return{id,bodies:g.actors.filter(a=>a.visible&&a!==worker&&a.role!=='band'&&a.role!=='seated'&&Math.hypot(a.x-worker.targetX,a.z-worker.targetZ)<.7).map(a=>({id:a.id,x:a.x,z:a.z,action:a.action,role:a.role}))};})};
  outcomes.push(result);
  mkdirSync(new URL('../Research/PixelArtVerification/ShowDeparture/',import.meta.url),{recursive:true});
  writeFileSync(new URL('../Research/PixelArtVerification/ShowDeparture/seed-sweep.json',import.meta.url),JSON.stringify({passed:false,outcomes},null,2)+'\n');
  assert.equal(g.phase,'results',JSON.stringify(result));assert(maxStep<=.19,JSON.stringify(result));
  assert.equal(fans.length,20);assert(fans.every(f=>f.phase==='gone'&&f.distance<=.181&&!f.moving&&!f.visible),JSON.stringify(result));
  assert(g.journey.afterShow.members.every(m=>m.phase==='gone'));assert.equal(g.floorTasksPending,false);
}
const output=new URL('../Research/PixelArtVerification/ShowDeparture/seed-sweep.json',import.meta.url);
mkdirSync(new URL('.',output),{recursive:true});writeFileSync(output,JSON.stringify({passed:true,outcomes},null,2)+'\n');
console.log(JSON.stringify({passed:true,seeds:outcomes.length,maxSeconds:Math.max(...outcomes.map(o=>o.seconds)),maxStep:Math.max(...outcomes.map(o=>o.maxStep))}));
