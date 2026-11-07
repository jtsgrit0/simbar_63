import {launchPreparedDesk} from './native_test_workflow.mjs';
import assert from 'node:assert/strict';
import {SimBarGame,SAVE_KEY} from './WebGLTemplate/FastGame/core.js';
import {BAND_MEMBERS,AFTER_SHOW_AUDIENCE,GREETING_SECONDS,EXIT_POINT,SURFER_LOWER_SPEED} from './WebGLTemplate/FastGame/aftershow.js';

function storage(){const map=new Map();return{getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,String(v))};}
function openNight(seed=12){const g=new SimBarGame({storage:storage(),seed});g.act('start');launchPreparedDesk(g);for(let i=0;i<3;i++)g.act('guide');return g;}
function endPerformance(g){g.journey.secondsLeft=.05;g.tick(.05);assert.equal(g.phase,'closing');assert(g.afterShowPending);}
function finish(g,max=240){for(let t=0;t<max&&g.phase!=='results';t+=.05){g.tick(.05);if(g.farewellPending)g.act('farewellAck');if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}return g;}

// A night without floor missions still shows the entire farewell before paying.
const g=openNight();const wallet=g.state.money;endPerformance(g);
assert.equal(g.journey.settledSession,0);assert.equal(g.state.money,wallet);
assert.equal(g.journey.musicPaused,true);assert.equal(g.journey.musicPauseSession,g.journey.session);
g.act('musicState',{paused:false});g.act('continue');assert.equal(g.journey.musicPaused,false,'Continue paused a manually resumed track');
assert.equal(g.journey.afterShow.audience.length,20);
const visited=new Map(BAND_MEMBERS.map(id=>[id,new Set()]));
const seats=g.actors.filter(a=>a.role==='seated').map(a=>({id:a.id,x:a.x,z:a.z}));
let previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,y:a.y}]));
for(let t=0;t<240&&!g.farewellPending;t+=.05){
  g.tick(.05);
  const state=g.journey.afterShow;
  if(state.departureStarted)assert(state.members.every(m=>m.greeting===2),'Departure began before all four musicians finished both greetings');
  else assert([...state.members,...state.audience].every(m=>m.phase!=='gone'),'Someone left before the greeting barrier');
  const surfer=g.actors.find(a=>a.id==='crowd-surfer'),oldSurfer=previous.get(surfer.id);
  assert(surfer.y<=oldSurfer.y&&oldSurfer.y-surfer.y<=SURFER_LOWER_SPEED*.05+1e-8,'Crowd surfer teleported to the floor');
  for(const a of g.actors){const p=previous.get(a.id);assert(Math.hypot(a.x-p.x,a.z-p.z)<=.19,`${a.id} teleported`);}
  for(const m of g.journey.afterShow.members){
    visited.get(m.id).add(m.phase);
    const a=g.actors.find(a=>a.id===m.id),before=previous.get(m.id);
    // The aisle walker can finish a sub-1.5 cm waypoint without spending its
    // remaining frame budget; allow that small correction, never a large jump.
    assert(Math.hypot(a.x-before.x,a.z-before.z)<=.12,`${m.id} teleported`);
    assert(a.stageLevel>=0&&a.stageLevel<=1);
    if(m.phase==='highfive'||m.phase==='hug'){
      assert.equal(a.stageLevel,0);assert.equal(a.moving,false);assert(a._partner);assert(a.actionProgress>=0&&a.actionProgress<=1);
      const fan=g.actors.find(f=>f.id===a._partner),radius=m.phase==='highfive'?.60:.43;
      assert(Math.abs(Math.hypot(a.x-fan.x,a.z-fan.z)-radius)<=.041,'Gesture began before precise contact approach finished');
    }
  }
  previous=new Map(g.actors.map(a=>[a.id,{x:a.x,z:a.z,y:a.y}]));
  if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});
  if(g.afterShowPending)assert.equal(g.journey.settledSession,0,'Receipt paid before the band exited');
}
assert(g.farewellPending,JSON.stringify(g.journey.afterShow));
assert.equal(g.phase,'closing');assert.equal(g.paused,true);assert.equal(g.pauseReason,'farewell');
assert.equal(g.state.money,wallet,'Receipt paid before farewell popup was acknowledged');
const snapshot=g.exportSave(),clock=g.elapsed;g.tick(5);assert.equal(g.elapsed,clock);g.act('resume');assert.equal(g.paused,true);
assert(g.journey.afterShow.audience.every(a=>a.phase==='gone'),'All stage-front guests must exit before the popup');
for(const fan of g.journey.afterShow.audience){
  const a=g.actors.find(a=>a.id===fan.id);
  assert(Math.hypot(a.x-EXIT_POINT.x,a.z-EXIT_POINT.z)<=.181,'Audience disappeared away from the exit');
  assert.equal(a.visible,false);
  assert.equal(a.moving,false);assert.equal(a._path.length,0);
}
for(const seat of seats){const a=g.actors.find(a=>a.id===seat.id);assert.equal(a.visible,true);assert.equal(a.x,seat.x);assert.equal(a.z,seat.z);}
for(const id of BAND_MEMBERS){
  for(const phase of ['stage','approach','highfive','hug','exit','gone'])assert(visited.get(id).has(phase),`${id} missed ${phase}`);
  const a=g.actors.find(a=>a.id===id);assert.equal(a.visible,false);assert(Math.hypot(a.x-8.8,a.z+5.55)<.22);
}
const pending=new SimBarGame({storage:g.storage});pending.act('continue');assert(pending.farewellPending);assert.equal(pending.pauseReason,'farewell');assert.equal(pending.journey.musicPaused,false);
assert(pending.journey.afterShow.audience.every(item=>pending.actors.find(a=>a.id===item.id).visible===false));
g.act('farewellAck');assert.equal(g.phase,'results');assert.equal(g.journey.afterShow.noticeAcknowledged,true);
const paid=g.state.money;g.act('farewellAck');assert.equal(g.state.money,paid);

// A popup may arrive while staff still need a reaction. Acknowledging it
// resumes that mission; it must not skip cleanup or pay an early receipt.
const concurrent=new SimBarGame({storage:{getItem:()=>null,setItem(){}}});concurrent.importSave(snapshot);concurrent.act('continue');
concurrent.floor.litter=40;concurrent.floor.cleaned=0;concurrent.floor.cleanupActive=false;concurrent.floor.cleanupComplete=false;
concurrent.act('pause',{reason:'menu'});assert.equal(concurrent.pauseReason,'farewell','An external pause replaced the modal pause reason');
concurrent.act('farewellAck');assert.equal(concurrent.phase,'closing');assert.equal(concurrent.paused,false);assert.equal(concurrent.journey.settledSession,0);
finish(concurrent);assert.equal(concurrent.phase,'results');assert.equal(concurrent.floorTasksPending,false);

// Saving during an actual gesture preserves the partner, position and elapsed
// gesture time. Pausing, continuing and reloading cannot restart the handshake.
const mid=openNight(21);endPerformance(mid);
for(let t=0;t<60&&!mid.journey.afterShow.members.some(m=>m.phase==='highfive');t+=.05)mid.tick(.05);
const active=mid.journey.afterShow.members.find(m=>m.phase==='highfive');assert(active);
mid.tick(.35);mid.act('pause');const before=JSON.parse(mid.exportSave());
const restored=new SimBarGame({storage:mid.storage});
assert.deepEqual(restored.journey.afterShow,JSON.parse(before.journeyJson).afterShow);
const member=restored.journey.afterShow.members.find(m=>m.id===active.id),oldTimer=member.timer;
assert(oldTimer>0&&oldTimer<GREETING_SECONDS.highfive);
restored.tick(2);assert.equal(member.timer,oldTimer);restored.act('continue');restored.tick(.1);
assert(member.timer>oldTimer);assert(member.timer<oldTimer+.101);
finish(restored);assert.equal(restored.phase,'results');
const earned=restored.state.money,xp=restored.journey.xp,completed=restored.journey.completedNights;
restored.save();const receipt=new SimBarGame({storage:restored.storage});receipt.act('continue');receipt.tick(5);receipt._settleNight();
assert.equal(receipt.state.money,earned);assert.equal(receipt.journey.xp,xp);assert.equal(receipt.journey.completedNights,completed);
assert.equal(receipt.journey.afterShow.completed,true);
assert.equal(receipt.journey.musicPaused,true);

// A departure token and walking position are durable too. Reloading mid-exit
// does not restart greetings, switch to a different person or lower the surfer
// a second time, and a manual music resume remains in effect.
const departing=openNight(9);endPerformance(departing);
for(let t=0;t<100&&!departing.journey.afterShow.departingActor;t+=.05)departing.tick(.05);
assert(departing.journey.afterShow.departingActor);departing.tick(.25);departing.act('musicState',{paused:false});departing.act('pause');
const departureBefore=JSON.parse(departing.exportSave()),departureState=JSON.parse(departureBefore.journeyJson).afterShow;
const resumedExit=new SimBarGame({storage:departing.storage});assert.deepEqual(resumedExit.journey.afterShow,departureState);
const token=departureState.departingActor,position=resumedExit.actors.find(a=>a.id===token),savedPosition={x:position.x,z:position.z};
resumedExit.tick(2);assert.deepEqual({x:position.x,z:position.z},savedPosition);
resumedExit.act('continue');assert.equal(resumedExit.journey.musicPaused,false);resumedExit.tick(.05);
assert.equal(resumedExit.journey.afterShow.departingActor,token);assert(Math.hypot(position.x-savedPosition.x,position.z-savedPosition.z)<.09);
finish(resumedExit);assert.equal(resumedExit.phase,'results');

// Historical v1 receipts remain historical. Floor actors do not reappear and
// neither the new popup nor the financial settlement is replayed on Continue.
const historical=JSON.parse(receipt.exportSave()),oldReceipt=JSON.parse(historical.journeyJson);
oldReceipt.afterShow.version=1;oldReceipt.afterShow.audience=oldReceipt.afterShow.audience.slice(0,8).map(a=>({...a,phase:'done'}));
delete oldReceipt.afterShow.noticeAcknowledged;delete oldReceipt.musicPauseSession;delete oldReceipt.musicPaused;historical.journeyJson=JSON.stringify(oldReceipt);
const historicalGame=new SimBarGame({storage:{getItem:()=>null,setItem(){}}});assert(historicalGame.importSave(historical));historicalGame.act('continue');
assert.equal(historicalGame.phase,'results');assert.equal(historicalGame.farewellPending,false);assert.equal(historicalGame.state.money,earned);
assert.equal(historicalGame.journey.afterShow.audience.length,20);assert(historicalGame.journey.afterShow.audience.every(item=>item.phase==='gone'&&historicalGame.actors.find(a=>a.id===item.id).visible===false));

// A pre-feature closing save has no choreography fields. Continue starts the
// new routine from the actors' saved positions without paying a second receipt.
const legacy=openNight();endPerformance(legacy);const old=JSON.parse(legacy.exportSave());
const journey=JSON.parse(old.journeyJson);delete journey.afterShow;old.journeyJson=JSON.stringify(journey);legacy.storage.setItem(SAVE_KEY,JSON.stringify(old));
const upgraded=new SimBarGame({storage:legacy.storage});upgraded.act('continue');assert(upgraded.journey.afterShow?.started);assert.equal(upgraded.phase,'closing');finish(upgraded);assert.equal(upgraded.phase,'results');
upgraded.act('next');launchPreparedDesk(upgraded);assert.equal(upgraded.journey.musicPauseSession,0);assert.equal(upgraded.journey.musicPaused,false);
assert(AFTER_SHOW_AUDIENCE.every(id=>upgraded.actors.find(a=>a.id===id).visible),'A new night did not restore the audience');
console.log('PASS: all greetings precede 24 physical exits, surfer lowering, retained bar/lounge guests, acknowledged popup, persisted manual music resume, pause/save migration, concurrent cleanup, once-only settlement');
