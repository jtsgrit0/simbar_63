import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {SimBarGame,Floor} from './WebGLTemplate/FastGame/core.js';
import {Audience} from './WebGLTemplate/FastGame/audience.js';
import {GREETING_SECONDS,EXIT_POINT} from './WebGLTemplate/FastGame/aftershow.js';
const DT=.05,EPS=1e-7,memory=()=>{const values=new Map();return{getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,String(v))};};
function night(owner=2,{seed=31,max=false}={}){
 const g=new SimBarGame({storage:memory(),seed});assert.equal(g.viewClosingActive,false);assert.equal(g.setViewClosingActive(true),false);g.act('owner',{index:owner});g.journey.tutorialComplete=true;
 if(max){g.journey.completedNights=5;g.state.fans=2400;g.state.upgrades=[3,3,3];g.journey.bookingCareer.relationships.fill(100);g.journey.bookingCareer.growth.fill(100);}
 g.act('start');assert.equal(g.setViewClosingActive(true),false);g.act('selectShow',{showId:max?'double':'freewill'});if(max)g.act('ticket',{tier:0});g.act('deskWork',{task:0});g.act('deskWork',{task:1});assert(g.act('launch').success);while(g.phase==='guide')g.act('guide');assert.equal(g.setViewClosingActive(true),false);return g;
}
function end(g){g.journey.secondsLeft=DT;g.tick(DT);assert.equal(g.phase,'closing');}
const receipts=g=>({money:g.cash,xp:g.journey.xp,nights:g.journey.completedNights,bonus:g.journey.resultFloorBonus,claimed:g.floor.cleanupRewardClaimed});
let observed=null;
const originalFloorTick=Floor.tick;
Floor.tick=function(state,dt,ready,running){
 const row=observed,g=row?.g,before=state.cleaned;
 if(row&&state===g.floor){
  const actual=g.crew.filter((id,i)=>{const a=g.actors.find(a=>a.id===id);return a?.visible&&id!==g.escort?.worker&&(!id.startsWith('hired-')||g._onDuty(+id.slice(-1)))&&Math.hypot(a.x-g._cleanGoal(i).x,a.z-g._cleanGoal(i).z)<=.22;}).length;
  assert(ready<=actual,'Cleanup counted an employee who had not reached the working spot');row.maxReady=Math.max(row.maxReady,actual);row.workerSeconds+=Math.max(0,dt)*Math.min(state.cleanupWorkers,ready);
 }
 const result=originalFloorTick.call(this,state,dt,ready,running),delta=state.cleaned-before;
 if(row&&state===g.floor){assert(delta<=Math.max(0,dt)*Math.min(state.cleanupWorkers,ready)*3+EPS,'Cleanup exceeded three units per arrived employee per second');if(!ready)assert.equal(delta,0);row.work+=delta;row.stats.floorCalls++;row.stats.maxWorkDelta=Math.max(row.stats.maxWorkDelta,delta);if(delta>0)row.stats.workFrames++;}
 return result;
};
function observe(g){
 const stats={steps:0,noReadySteps:0,workFrames:0,floorCalls:0,maxWorkDelta:0,sweepFrames:0,returnFrames:0,escortPhases:new Set(),guestStages:new Set([g.floor.guestStep]),gestures:new Set(),autoAcknowledgements:0,manualAcknowledgements:0};
 const step=g._step.bind(g),act=g.act.bind(g);
 g.act=(type,payload)=>{if(type==='farewellAck'){if(g.viewClosingActive)stats.autoAcknowledgements++;else stats.manualAcknowledgements++;}return act(type,payload);};
 g._step=dt=>{
  const oldCleaned=g.floor.cleaned,oldGuest=g.floor.guestStep,oldActors=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,visible:a.visible!==false}])),oldMembers=new Map((g.journey.afterShow?.members||[]).map(m=>[m.id,{phase:m.phase,timer:m.timer}])),escort=g.escort,returnGoal=escort?.phase==='return'?g.crew.includes(escort.worker)&&g.floor.cleanupActive?g._cleanGoal(g.crew.indexOf(escort.worker)):{x:escort.homeX,z:escort.homeZ}:null;
  const row={g,stats,maxReady:0,workerSeconds:0,work:0};observed=row;
  try{step(dt);}finally{observed=null;}
  stats.steps++;const delta=g.floor.cleaned-oldCleaned;assert(delta<=row.work+EPS,'Cleanup skipped the real Floor.tick work path');assert(row.workerSeconds<=dt*row.maxReady+EPS,'More than one frame of cleanup time was awarded');if(!row.maxReady){stats.noReadySteps++;assert(delta<=EPS,'Paper disappeared before an employee arrived');}
  if(g.actors.some(a=>a.action==='sweep'))stats.sweepFrames++;if(g.staffReturnPending)stats.returnFrames++;if(g.escort)stats.escortPhases.add(g.escort.phase);stats.guestStages.add(g.floor.guestStep);assert(g.floor.guestStep-oldGuest<=1,'Guest assistance stages were skipped');
  for(const a of g.actors){const old=oldActors.get(a.id);if(!old)continue;assert(Math.hypot(a.x-old.x,a.z-old.z)<=.19,`Closing teleported ${a.id}`);if(old.visible&&a.visible===false&&a.role==='assisted')assert(Math.hypot(a.x-8.7,a.z+5.4)<=.22,'The assisted guest disappeared before reaching the exit');}
  if(escort?.phase==='return'&&!g.escort){const worker=g.actors.find(a=>a.id===escort.worker);assert(Math.hypot(worker.x-returnGoal.x,worker.z-returnGoal.z)<=.22,'Escort ended before the employee physically returned');}
  for(const member of g.journey.afterShow?.members||[]){const old=oldMembers.get(member.id);if(old&&GREETING_SECONDS[old.phase]&&member.phase!==old.phase)assert(old.timer+dt>=GREETING_SECONDS[old.phase]-EPS,'Automatic closing shortened a greeting');if(['highfive','hug'].includes(member.phase)){stats.gestures.add(member.id+':'+member.phase);const artist=g.actors.find(a=>a.id===member.id),fan=g.actors.find(a=>a.id===artist._partner),radius=member.phase==='highfive'?.60:.43;assert(fan&&Math.abs(Math.hypot(artist.x-fan.x,artist.z-fan.z)-radius)<=.041,'A greeting lost physical contact');}}
 };
 return stats;
}
function automatic(g,{limit=300,stats=observe(g)}={}){
 const start=g.elapsed;for(let frame=0;frame<limit/DT&&g.phase!=='results';frame++){g.setViewClosingActive(true);g.tick(DT);}assert.equal(g.phase,'results',JSON.stringify({phase:g.phase,paused:g.paused,reason:g.pauseReason,floor:g.floor,escort:g.escort,after:g.journey.afterShow}));assert.equal(g.viewClosingActive,false);assert.equal(g.floorTasksPending,false);assert.equal(g.afterShowPending,false);assert(g.journey.afterShow.noticeAcknowledged);assert(g.journey.afterShow.members.every(m=>m.greeting===2&&m.phase==='gone'));assert(g.journey.afterShow.audience.every(a=>a.phase==='gone'));assert.equal(stats.autoAcknowledgements,1);assert.equal(stats.manualAcknowledgements,0);
 for(let i=0;i<g.crew.length;i++){const worker=g.actors.find(a=>a.id===g.crew[i]),goal=g._parkGoal(i);if(worker?.visible)assert(Math.hypot(worker.x-goal.x,worker.z-goal.z)<=.051,'A worker did not finish the physical cleanup return');}
 const receipt=receipts(g);g.save();const restored=new SimBarGame({storage:g.storage,seed:31});assert.equal(restored.viewClosingActive,false);restored.act('continue');restored.tick(1);restored.act('farewellAck');restored._settleNight();assert.deepEqual(receipts(restored),receipt,'Automatic closing paid the same reward twice');
 return{seconds:g.elapsed-start,...stats,escortPhases:[...stats.escortPhases],guestStages:[...stats.guestStages],gestures:stats.gestures.size,receipt};
}
const evidence={};
try{
 // A real random guest remains unresolved during the performance. Once the
 // show closes, tick alone performs C/F work, physical escort and acknowledgement.
 {
  const g=night(0,{seed:2});for(let i=0;i<3600&&g.playing;i++){g.setViewServiceActive(true);assert.equal(g.setViewClosingActive(true),false);g.tick(DT);}assert.equal(g.phase,'closing');assert(g.floor.guestActive);const stats=observe(g);evidence.fullNight=automatic(g,{stats});assert.equal(evidence.fullNight.receipt.bonus,70);assert(stats.sweepFrames>0&&stats.noReadySteps>0&&stats.returnFrames>0);assert.deepEqual([...stats.guestStages].sort(),[0,1,2,3]);assert(['join','exit','return'].every(p=>stats.escortPhases.has(p)));assert.equal(stats.gestures.size,8);
  assert(g.act('next').success);assert.equal(g.viewClosingActive,false);g.act('selectShow',{showId:'freewill'});g.act('deskWork',{task:0});g.act('deskWork',{task:1});g.act('launch');assert.equal(g.viewClosingActive,false);assert(g.playing);
 }
 // Maximum FF crowd still completes its preserved greetings/fast departure
 // while real workers clean and return; no manual reaction or farewell input.
 {
  const g=night(1,{max:true});end(g);assert.equal(g.metrics().attendance,80);evidence.ff80=automatic(g);assert.equal(evidence.ff80.gestures,8);assert(evidence.ff80.sweepFrames>0);
 }
 // Explicit pause does not become an implicit resume. Leaving the mode stops
 // new automated pulses; an already earned work budget can finish normally.
 {
  const g=night(2);end(g);const stats=observe(g);g.setViewClosingActive(true);for(let i=0;i<800&&!g.floor.cleaned;i++)g.tick(DT);assert(g.floor.cleaned>0);g.act('pause',{reason:'manual'});const paused={elapsed:g.elapsed,cleaned:g.floor.cleaned,rounds:g.reaction.completedRounds,presses:g.reaction.presses};g.setViewClosingActive(true);g.tick(5);assert.deepEqual({elapsed:g.elapsed,cleaned:g.floor.cleaned,rounds:g.reaction.completedRounds,presses:g.reaction.presses},paused);g.act('resume');
  const raw=g.exportSave(),loaded=new SimBarGame({storage:g.storage,seed:31});assert.equal(loaded.viewClosingActive,false);loaded.act('continue');assert.equal(loaded.viewClosingActive,false);assert(!raw.includes('_viewClosingActive'));
  g.setViewClosingActive(false);assert.equal(g.viewClosingActive,false);const rounds=g.reaction.completedRounds;for(let i=0;i<800&&g.reaction.cleanupWorkBudget>EPS;i++)g.tick(DT);assert.equal(g.reaction.completedRounds,rounds,'Leaving view mode generated another automatic response');if(!g.floor.cleanupComplete){const cleaned=g.floor.cleaned;g.tick(1);assert.equal(g.floor.cleaned,cleaned,'Manual mode cleaned without a new pulse');}
  for(let i=0;i<6000&&g.phase!=='results';i++){g.tick(DT);if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});if(g.farewellPending)g.act('farewellAck');}assert.equal(g.phase,'results');assert.equal(stats.autoAcknowledgements,0);assert.equal(stats.manualAcknowledgements,1);evidence.pauseExitReloadAndManualReturn=true;
 }
 // A saved hidden farewell may be paused with cleanup still outstanding.
 // Enabling the flag has no synchronous side effects; the next tick resumes
 // only its farewell pause, while an explicit focus pause remains respected.
 {
  const source=night(2);end(source);for(let i=0;i<6000&&!source.farewellPending;i++)source.tick(DT);assert(source.farewellPending&&source.paused);assert(source.floorTasksPending);assert.equal(source.floor.cleaned,0);source.save();const g=new SimBarGame({storage:source.storage,seed:31});g.act('continue');assert(g.farewellPending&&g.paused);assert.equal(g.viewClosingActive,false);const stats=observe(g);g.setViewClosingActive(true);assert(g.farewellPending&&g.paused,'The setter performed work before the game tick');g.act('pause',{reason:'focus'});const elapsed=g.elapsed;g.tick(1);assert.equal(g.elapsed,elapsed);assert.equal(g.pauseReason,'focus');g.act('resume');evidence.savedFarewellPaused=automatic(g,{stats});assert(evidence.savedFarewellPaused.sweepFrames>0);
 }
 // The former Petsounds day-five gather stall must finish under the same
 // unattended tick path, retaining the actual final hug and a single receipt.
 {
  const g=new SimBarGame({storage:memory(),seed:31});assert(g.importSave(readFileSync(new URL('./fixtures/aftershow-petsounds-day5-gather-blocked.json',import.meta.url),'utf8')));g.act('continue');assert.equal(g.journey.day,5);evidence.petsoundsDay5=automatic(g);assert(evidence.petsoundsDay5.gestures>0);
 }
}finally{Floor.tick=originalFloorTick;}
console.log(JSON.stringify({passed:true,checks:['tick-only automatic closing and one acknowledgement','real arrived workers and three-unit work rate','sweep/physical cleanup return','three guest stages and real escort return','preserved physical greetings/fast exits','pause and immediate automatic-input exit','manual pulse contract after exit','reload/new-night default off','late closing and saved farewell-pause recovery','once-only financial rewards'],evidence},null,2));
