import assert from 'node:assert/strict';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {launchPreparedDesk} from './native_test_workflow.mjs';

const memory=(initial=[])=>{const values=new Map(initial);return{values,getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
const enter=g=>{launchPreparedDesk(g);while(g.phase==='guide')g.act('guide');assert.equal(g.phase,'playing');return g;};
const schedule=g=>({floorSeed:g.reaction.seed,paper:Array.from({length:g.reaction.paperCount},(_,i)=>[g.reaction['paperKind'+i],g.reaction['paperAt'+i]]),guest:g.reaction.guestScheduled?g.reaction.guestAt:null,inspection:g.inspection.triggerAt});
const inspectionClock=g=>{const s=g.journey.inspection;return{phase:s.phase,triggerAt:s.triggerAt,serial:s.serial,warningRemaining:s.warningRemaining,holdRemaining:s.holdRemaining,patrolElapsed:s.patrolElapsed,frozenSeconds:s.frozenSeconds};};
const reactionRecord=g=>{const s=structuredClone(g.reaction);if(!s.activeKind)delete s.pressAge;return s;};
const assertBounds=g=>{
  const s=schedule(g);assert(s.inspection>=55&&s.inspection<=90);
  assert(s.paper.length<=3);let previous=20;
  for(const [kind,at] of s.paper){assert(kind===0||kind===1);assert(at>=previous&&at<=135);previous=at;}
  for(const kind of [0,1])assert(s.paper.filter(entry=>entry[0]===kind).length<=2);
  if(s.guest!==null)assert(s.guest>=40&&s.guest<=140);
};
function finish(g){
  for(let i=0;i<6000&&g.phase!=='results';i++){g.tick(.1);if(g.farewellPending)g.act('farewellAck');if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}
  assert.equal(g.phase,'results');
}

// A genuine finished night leaves a persisted native PRNG. Reopening the
// same next-day planning save used to replay precisely the same schedule.
const original=new SimBarGame({storage:memory(),seed:1});original.act('start');enter(original);finish(original);original.act('next');original.save();
const prepared=[...original.storage.values],restoredSeed=JSON.parse(original.storage.getItem(SAVE_KEY)).native.seed;
function launch(owner,{seed}={}){
  const g=new SimBarGame({storage:memory(prepared),seed});assert.equal(g._seed,restoredSeed);
  assert.equal(g.act('owner',{index:owner}).success,true);g.act('continue');return enter(g);
}

// Freeze the old Date/Math entropy and supply a reproducible browser entropy
// stream. This isolates the regression from incidental gameplay RNG changes.
const cryptoDescriptor=Object.getOwnPropertyDescriptor(globalThis,'crypto'),oldRandom=Math.random,oldNow=Date.now;
let entropy=0x1257ab,draws=0,samples=[];
try{
  Math.random=()=>.25;Date.now=()=>1700000000000;
  Object.defineProperty(globalThis,'crypto',{configurable:true,value:{getRandomValues(out){draws++;entropy=(Math.imul(entropy,1664525)+1013904223)>>>0;out[0]=entropy;return out;}}});
  for(const owner of [0,1,2]){
    const group=Array.from({length:8},()=>launch(owner));samples.push(...group);
    for(const g of group)assertBounds(g);
    assert(new Set(group.map(g=>g.reaction.seed)).size>1,'New runs reused the saved floor seed');
    assert(new Set(group.map(g=>JSON.stringify(schedule(g).paper)+':'+schedule(g).guest)).size>1,'Floor event times repeated across new runs');
    assert(new Set(group.map(g=>g.inspection.triggerAt)).size>1,'Inspection time repeated across new runs');
  }
  assert.equal(draws,24,'Only new unseeded nights should request fresh schedule entropy');
  const seededA=launch(1,{seed:41}),seededB=launch(1,{seed:41});
  assert.deepEqual(schedule(seededA),schedule(seededB),'Explicit deterministic seeds must preserve repeatable fixtures');
  assert.equal(seededA._seed,seededB._seed);assert.equal(draws,24);
  Object.defineProperty(globalThis,'crypto',{configurable:true,value:undefined});
  const fallback=[launch(2),launch(2),launch(2)];
  assert(new Set(fallback.map(g=>JSON.stringify(schedule(g)))).size>1,'Missing crypto and a frozen clock repeated the schedule');
}finally{
  Math.random=oldRandom;Date.now=oldNow;
  if(cryptoDescriptor)Object.defineProperty(globalThis,'crypto',cryptoDescriptor);else delete globalThis.crypto;
}

// Reload/continue keep the already generated schedule and active mission
// clocks. Starting or changing venue cannot reroll a running contract.
const live=samples[0],planned=schedule(live),copy=new SimBarGame({storage:live.storage});
copy.act('continue');assert.deepEqual(schedule(copy),planned);
copy.act('start');assert.deepEqual(schedule(copy),planned);
assert.equal(copy.act('owner',{index:(copy.owner+1)%3}).success,false);assert.deepEqual(schedule(copy),planned);
copy.act('lobby');copy.act('continue');assert.deepEqual(schedule(copy),planned);
for(let t=0;t<planned.inspection+.2;t+=.05)copy.tick(.05);
assert.equal(copy.inspection.phase,'warning');assert.equal(copy.journey.inspection.serial,1);
copy.save();const warning=new SimBarGame({storage:copy.storage});warning.act('continue');
assert.deepEqual(inspectionClock(warning),inspectionClock(copy));assert.deepEqual(reactionRecord(warning),reactionRecord(copy));
const waiting=samples.find(g=>g.reaction.guestScheduled);assert(waiting,'Controlled entropy stream must cover an actual scheduled guest');
for(let t=0;t<175&&!waiting.floor.guestSpawned;t+=.05)waiting.tick(.05);
assert(waiting.floor.guestSpawned);waiting.save();
const guest=new SimBarGame({storage:waiting.storage});guest.act('continue');
assert.deepEqual(reactionRecord(guest),reactionRecord(waiting));assert.equal(guest.floor.guestStep,waiting.floor.guestStep);assert.equal(guest.floor.guestSpawned,true);
console.log(JSON.stringify({passed:true,freshRuns:24,owners:3,checks:['same planning save gets fresh schedules','independent venue schedules','unchanged event bounds','seeded fixtures','crypto fallback','saved schedule continuity','warning and guest restoration','no mid-night reroll']}));
