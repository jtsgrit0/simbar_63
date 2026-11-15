import assert from 'node:assert/strict';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {BAND_MEMBERS,GREETING_AUDIENCE,EXIT_ONLY_AUDIENCE,GREETING_SECONDS,PARALLEL_EXIT_SPEED,EXIT_POINT,createAfterShow,tickAfterShow} from './WebGLTemplate/FastGame/aftershow.js';
const DT=.05,memory=()=>{const values=new Map();return{getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
function closing(owner){
 const g=new SimBarGame({storage:memory(),seed:17});g.act('owner',{index:owner});g.journey.completedNights=5;g.journey.tutorialComplete=true;g.state.fans=2400;g.state.upgrades=[3,3,3];g.journey.bookingCareer.relationships.fill(100);g.journey.bookingCareer.growth.fill(100);g.act('start');g.act('selectShow',{showId:'double'});g.act('ticket',{tier:0});g.act('deskWork',{task:0});g.act('deskWork',{task:1});assert(g.act('launch').success);while(g.phase==='guide')g.act('guide');assert.equal(g.metrics().attendance,Audience.capacity(owner));g.journey.secondsLeft=DT;g.tick(DT);assert.equal(g.phase,'closing');return g;
}
assert.deepEqual(GREETING_SECONDS,{highfive:1.1,hug:1.5});assert(PARALLEL_EXIT_SPEED>1.45);

// Prove the new speed and actual simultaneous movement in a clear doorway
// segment, rather than only checking an animation/action label.
{
 const actors=[{id:'patron-0',x:7.4,z:-5.55},{id:'patron-1',x:7.4,z:-4.6}].map(a=>({...a,role:'guest',visible:true,layout:'ff',y:0,_path:[],_pathAt:0})),state=createAfterShow(actors,1);for(const member of state.members){member.phase='gone';member.greeting=2;}
 const before=actors.map(a=>({x:a.x,z:a.z}));tickAfterShow(state,actors,DT);
 assert(actors.every((a,i)=>Math.hypot(a.x-before[i].x,a.z-before[i].z)>1.45*DT+.001),'Non-greeters did not use the faster parallel movement');
}

const outcomes=[];
for(let owner=0;owner<3;owner++){
 let g=closing(owner),state=g.journey.afterShow;const started=g.elapsed,gestures=new Set(),realGreetingIds=new Set(GREETING_AUDIENCE),nonGreeters=new Set(state.audience.filter(a=>!realGreetingIds.has(a.id)).map(a=>a.id));
 assert.equal(g.actors.filter(a=>Audience.isAudience(a)&&a.visible!==false).length,Audience.capacity(owner));
 assert(state.audience.filter(a=>a.phase!=='gone').every(a=>a.parallel===!realGreetingIds.has(a.id)));
 let earlyFormerMoving=0,maxSimultaneous=0,restored=false,acknowledgements=0,maxStep=0;
 for(let frame=0;frame<3600&&g.phase!=='results';frame++){
  const before=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,y:a.y||0,visible:a.visible!==false}])),memberBefore=new Map(state.members.map(m=>[m.id,{phase:m.phase,timer:m.timer}]));g.tick(DT);let simultaneous=0,former=0;
  for(const a of g.actors){const old=before.get(a.id);if(!old)continue;const step=Math.hypot(a.x-old.x,a.z-old.z);maxStep=Math.max(maxStep,step);const bound=nonGreeters.has(a.id)?PARALLEL_EXIT_SPEED*DT+.0151:.12;assert(step<=bound+1e-8,`Owner ${owner}: ${a.id} teleported`);if(nonGreeters.has(a.id)&&old.visible&&step>1e-6){simultaneous++;if(EXIT_ONLY_AUDIENCE.includes(a.id))former++;}if(old.visible&&a.visible===false&&(Audience.isAudience(a)||a.role==='band'))assert(Math.hypot(a.x-EXIT_POINT.x,a.z-EXIT_POINT.z)<=.181,`${a.id} vanished away from the doorway`);}
  maxSimultaneous=Math.max(maxSimultaneous,simultaneous);if(!state.departureStarted)earlyFormerMoving=Math.max(earlyFormerMoving,former);
  if(!state.departureStarted)assert([...state.members,...state.audience.filter(a=>realGreetingIds.has(a.id))].every(a=>a.phase!=='gone'),'A preserved greeting participant left early');
  for(const member of state.members){
   const old=memberBefore.get(member.id);if(GREETING_SECONDS[old.phase]&&member.phase!==old.phase)assert(old.timer+DT>=GREETING_SECONDS[old.phase]-1e-8,'A greeting was shortened');
   if(['highfive','hug'].includes(member.phase)){gestures.add(member.id+':'+member.phase);const artist=g.actors.find(a=>a.id===member.id),fan=g.actors.find(a=>a.id===artist._partner),radius=member.phase==='highfive'?.60:.43;assert(fan);assert.equal(artist.stageLevel,0);assert.equal(artist.moving,false);assert(Math.abs(Math.hypot(artist.x-fan.x,artist.z-fan.z)-radius)<=.041);}
  }
  if(!restored&&g.elapsed-started>=2){
   assert(earlyFormerMoving>=2,'Former serialized patrons did not leave concurrently during greetings');g.act('pause');const raw=g.exportSave(),saved=JSON.parse(raw),savedAfter=JSON.parse(saved.journeyJson).afterShow,positions=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z}]));const next=new SimBarGame({storage:g.storage,seed:17});assert.deepEqual(next.journey.afterShow.members.map(m=>({id:m.id,phase:m.phase,timer:m.timer,greeting:m.greeting})),savedAfter.members.map(m=>({id:m.id,phase:m.phase,timer:m.timer,greeting:m.greeting})));for(const actor of next.actors){const old=positions.get(actor.id);if(old)assert.deepEqual({x:actor.x,z:actor.z},old,'Reload moved an actor');}next.tick(1);assert.equal(next.elapsed,g.elapsed);next.act('continue');g=next;state=g.journey.afterShow;restored=true;
  }
  if(g.farewellPending){acknowledgements++;g.act('farewellAck');}if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
 }
 assert.equal(g.phase,'results',JSON.stringify({owner,remaining:state.audience.filter(a=>a.phase!=='gone')}));assert.equal(gestures.size,BAND_MEMBERS.length*2);assert(state.members.every(m=>m.greeting===2&&m.phase==='gone'));assert(state.audience.every(a=>a.phase==='gone'));assert.equal(acknowledgements,1);assert(maxSimultaneous>=4);assert.equal(g.floorTasksPending,false);
 const receipt=[g.cash,g.journey.xp,g.journey.completedNights];g.save();const receiptGame=new SimBarGame({storage:g.storage,seed:17});receiptGame.act('continue');receiptGame.tick(1);receiptGame._settleNight();assert.deepEqual([receiptGame.cash,receiptGame.journey.xp,receiptGame.journey.completedNights],receipt);
 outcomes.push({owner,capacity:Audience.capacity(owner),seconds:g.elapsed-started,earlyFormerMoving,maxSimultaneous,maxStep,gestures:gestures.size,savedParallelResume:restored,acknowledgements,onceOnlyReceipt:true});
}
console.log(JSON.stringify({passed:true,checks:['faster simultaneous actual movement','all three maximum-capacity rooms','eight physical gestures and original durations','band/greeting barrier','continuous travel and real-door exits','mid-parallel pause/save/continue','one farewell and once-only receipt'],outcomes},null,2));
